using System.Globalization;

namespace Lumen.Charts;

/// <summary>One event's result for <see cref="PlacingsChart.Build"/>: the place it finished in, and what is known about it.</summary>
/// <param name="Place">The finishing place, 1 for first. A result without a place, or with 0 or less, is left out of the chart.</param>
public sealed record Placing(int? Place)
{
    /// <summary>The size of the field, written after the place as <c>/48</c> when it is above 0.</summary>
    public int? Field { get; init; }
    /// <summary>The points the event earned; null draws a gap in the points line, never a zero.</summary>
    public double? Points { get; init; }
    /// <summary>The event's date. When every result has one, the events are drawn in date order; it also labels the event.</summary>
    public DateOnly? Date { get; init; }
    /// <summary>The event's label on the X axis, its tooltip and its name; it wins over <see cref="Date"/>.</summary>
    public string? Label { get; init; }
    /// <summary>The series the event belongs to (a league, a cup). A place is better or worse only than the previous event of
    /// the same series; each series is its own line. Surrounding spaces are ignored; null or blank is a series of its own.</summary>
    public string? Series { get; init; }
}

/// <summary>The words and colours of a <see cref="PlacingsChart"/>. The title, description, axis label, width and height are set
/// on the spec the builder returns, with <c>spec with { … }</c>.</summary>
public sealed record PlacingsOptions
{
    /// <summary>The places line's name, and the start of each line's name when there are several series. Default "Place".</summary>
    public string PlaceName { get; init; } = "Place";
    /// <summary>The points line's name and its pane's label. Default "Points".</summary>
    public string PointsName { get; init; } = "Points";
    /// <summary>How a date labels an event, in the invariant culture. Default "d MMM yyyy".</summary>
    public string DateFormat { get; init; } = "d MMM yyyy";
    /// <summary>The label of an event with neither a label nor a date; <c>{0}</c> is its number, from 1, in the drawn order. Default "#{0}".</summary>
    public string Unlabelled { get; init; } = "#{0}";
    /// <summary>The places lines' colours, used in turn; null takes the style's text colour for the first line and its palette for the others.</summary>
    public IReadOnlyList<string>? PlaceColors { get; init; }
    /// <summary>The points line's colour; null takes the style's second palette colour.</summary>
    public string? PointsColor { get; init; }
    /// <summary>The chart's style; null is <see cref="ChartStyle.Light"/>.</summary>
    public ChartStyle? Style { get; init; }
}

/// <summary>Builds the places and points chart: finishing places on a reversed axis, first at the top, one line per series with each
/// place coloured and named by its change against the previous event of the same series, the field after each place, and the
/// points each event earned in a pane beneath.</summary>
public static class PlacingsChart
{
    /// <summary>The chart for <paramref name="results"/>, or null when no result has a place (so a page can show its own empty state).
    /// The result is an ordinary <see cref="ChartSpec"/>: render it with <see cref="ChartSvg.Render(ChartSpec, bool, bool)"/> or
    /// <c>&lt;LumenChart&gt;</c>, or change it with <c>with</c>. Each series draws only at its own races; where one series has a race,
    /// the others have no point.</summary>
    /// <exception cref="ArgumentException">A null result, a blank name or date format, an <see cref="PlacingsOptions.Unlabelled"/> that
    /// cannot be formatted, a <see cref="PlacingsOptions.DateFormat"/> that cannot parse, an empty <see cref="PlacingsOptions.PlaceColors"/>,
    /// or a style with an empty <see cref="ChartStyle.Series"/> palette when <see cref="PlacingsOptions.PlaceColors"/> is null and there
    /// are multiple series, or when <see cref="PlacingsOptions.PointsColor"/> is null and there are points.</exception>
    public static ChartSpec? Build(IEnumerable<Placing> results, PlacingsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(results);
        var o = options ?? new PlacingsOptions();
        if (string.IsNullOrWhiteSpace(o.PlaceName)) throw new ArgumentException("A places chart needs a name for its places line (PlaceName).");
        if (string.IsNullOrWhiteSpace(o.PointsName)) throw new ArgumentException("A places chart needs a name for its points line and pane (PointsName).");
        if (string.IsNullOrWhiteSpace(o.DateFormat)) throw new ArgumentException("A places chart needs a date format (DateFormat) to label an event by its date.");
        try { new DateOnly(2027, 1, 1).ToString(o.DateFormat, CultureInfo.InvariantCulture); } catch (FormatException) { throw new ArgumentException("DateFormat must be a valid format string for DateOnly.ToString."); }
        if (o.Unlabelled is null || !o.Unlabelled.Contains("{0}", StringComparison.Ordinal)) throw new ArgumentException("Unlabelled must contain {0}, where an event's number goes, such as \"R{0}\".");
        try { string.Format(CultureInfo.InvariantCulture, o.Unlabelled, 1); } catch (FormatException) { throw new ArgumentException("Unlabelled must be a valid format string for string.Format, such as \"R{0}\"."); }
        if (o.PlaceColors is { Count: 0 }) throw new ArgumentException("PlaceColors must name at least one colour, or be null for the style's.");
        var style = o.Style ?? ChartStyle.Light;
        var placed = results.Select(r => r ?? throw new ArgumentException("A places chart's results may not contain null.")).Where(r => r.Place is > 0).ToList();
        if (placed.Count == 0) return null;
        // OrderBy is stable, so events on the same date keep the order they were given in.
        var races = placed.All(r => r.Date is not null) ? placed.OrderBy(r => r.Date!.Value).ToList() : placed;
        var labels = races.Select((r, i) => (string.IsNullOrWhiteSpace(r.Label) ? null : r.Label) ?? r.Date?.ToString(o.DateFormat, CultureInfo.InvariantCulture)
            ?? string.Format(CultureInfo.InvariantCulture, o.Unlabelled, i + 1)).ToArray();
        static string Key(Placing r) => r.Series?.Trim() ?? "";
        var keys = races.Select(Key).Distinct(StringComparer.Ordinal).ToList();
        var anyPoints = races.Any(r => r.Points is not null);
        if (style.Series.Count == 0 && ((o.PlaceColors is null && keys.Count > 1) || (o.PointsColor is null && anyPoints)))
            throw new ArgumentException("The style's Series palette must not be empty when PlaceColors or PointsColor need it.");
        string Color(int k) => o.PlaceColors is { } colors ? colors[k % colors.Count] : k == 0 ? style.Text : style.Series[(k - 1) % style.Series.Count];
        // Each series is its own line, with points only at its own events, so a place is judged only against the previous event
        // of the same series. Races from other series are skipped, not nulled.
        var lines = keys.Select((key, k) => new ChartSeries(
                keys.Count == 1 || key.Length == 0 ? o.PlaceName : $"{o.PlaceName} · {key}",
                races.Select((r, i) => (r, i)).Where(x => Key(x.r) == key).Select(x => new ChartPoint(x.i, x.r.Place, labels[x.i])
                    { ValueNote = x.r.Field is > 0 ? $"/{x.r.Field}" : null }).ToArray(),
                Color(k))
            { ChangeColors = ChangeColors.LowerIsBetter, ValueLabels = true, Markers = MarkerStyle.Filled }).ToList();
        var best = races.Min(r => r.Place!.Value);
        var spec = new ChartSpec
        {
            Kind = ChartKind.Line, Width = 340, Height = anyPoints ? 380 : 260, Style = style,
            Title = "Places and points",
            Description = anyPoints ? $"Finishing place out of the field, first at the top, and points. Best: {best}."
                : $"Finishing place out of the field, first at the top. Best: {best}.",
            XMin = -0.5, XMax = races.Count - 0.5, YReversed = true, YLabel = "Place",
            Series = lines,
        };
        if (!anyPoints) return spec;
        var points = new ChartSeries(o.PointsName, races.Select((r, i) => new ChartPoint(i, r.Points, labels[i])).ToArray(),
                o.PointsColor ?? style.Series[1 % style.Series.Count])
            { Pane = 1, ValueLabels = true, Markers = MarkerStyle.Filled };
        return spec with { Panes = [new ChartPane { Label = o.PointsName, Weight = 1 }], Series = [.. lines, points] };
    }
}
