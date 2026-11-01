using System.Globalization;

namespace Lumen.Charts;

/// <summary>One event's result for <see cref="PlacingsChart.Build"/>: the place it finished in, and what is known about it.</summary>
/// <param name="Place">The finishing place, 1 for first. A result without a place, or with 0 or less, is left out of the chart.</param>
public sealed record Placing(int? Place)
{
    /// <summary>The size of the field, written after the place as <c>/48</c> when it is above 0.</summary>
    public int? Field { get; init; }
    /// <summary>The points the event earned. Null is never a zero: in a series that scores at other events it is a gap in that series' points line,
    /// and in a series that never scores it has no points line at all.</summary>
    public double? Points { get; init; }
    /// <summary>The event's date. Events with a date are drawn in date order, after the events without one (which keep the order given); it also labels the event.</summary>
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
    /// <summary>The points line's name, and the start of each points line's name when several series score ("Points · League"); also the points pane's label
    /// unless <see cref="PaneLabel"/> sets one. Default "Points".</summary>
    public string PointsName { get; init; } = "Points";
    /// <summary>The points pane's label, so the legend can say "Pts" (<see cref="PointsName"/>) while the pane says "Points". Null uses <see cref="PointsName"/>; blank is refused.</summary>
    public string? PaneLabel { get; init; }
    /// <summary>The chart's description. Every <c>{best}</c> is replaced by the best place, written in the invariant culture; text without it is used as given, and "" is allowed.
    /// Null gives "Finishing place out of the field, first at the top, and points. Best: 3." (without ", and points" when no event scored).</summary>
    public string? Description { get; init; }
    /// <summary>How a date labels an event, in the invariant culture. Default "d MMM yyyy".</summary>
    public string DateFormat { get; init; } = "d MMM yyyy";
    /// <summary>The label of an event with neither a label nor a date; <c>{0}</c> is its number, from 1, in the drawn order. Default "#{0}".</summary>
    public string Unlabelled { get; init; } = "#{0}";
    /// <summary>The places lines' colours, used in turn; null takes the style's series colours in turn, leaving out any within 60 (sRGB distance)
    /// of its <see cref="ChartStyle.Rising"/> or <see cref="ChartStyle.Falling"/> colour so that a line never looks like a change, and the first line the first of them.</summary>
    public IReadOnlyList<string>? PlaceColors { get; init; }
    /// <summary>The colour of a lone points line (when only one series scores); null takes the next of the places lines' default colours after theirs (wrapping round).
    /// When several series score, each points line takes its places line's colour and this is ignored.</summary>
    public string? PointsColor { get; init; }
    /// <summary>The chart's style; null is <see cref="ChartStyle.Light"/>.</summary>
    public ChartStyle? Style { get; init; }
}

/// <summary>Builds the places and points chart: finishing places on a reversed axis, first at the top, one line per series with each
/// place coloured and named by its change against the previous event of the same series, the field after each place, and the
/// points each event earned in a pane beneath, one line for each series that scores.</summary>
public static class PlacingsChart
{
    /// <summary>The chart for <paramref name="results"/>, or null when no result has a place (so a page can show its own empty state).
    /// The result is an ordinary <see cref="ChartSpec"/>: render it with <see cref="ChartSvg.Render(ChartSpec, bool, bool)"/> or
    /// <c>&lt;LumenChart&gt;</c>, or change it with <c>with</c>. Each series draws only at its own races; where one series has a race,
    /// the others have no point. Events without a date come first, in the order given, then the dated ones by date. Each series that scores has its own points line in the pane beneath
    /// (named <see cref="PlacingsOptions.PointsName"/> alone, or "Points · League" when several score); a race without points is a gap in it, and a series that never scores has none.</summary>
    /// <exception cref="ArgumentException">A null result, a blank name or date format, an <see cref="PlacingsOptions.Unlabelled"/> that
    /// cannot be formatted, a <see cref="PlacingsOptions.DateFormat"/> that cannot parse, a blank <see cref="PlacingsOptions.PaneLabel"/>, an empty <see cref="PlacingsOptions.PlaceColors"/>,
    /// or a style with an empty <see cref="ChartStyle.Series"/> palette when <see cref="PlacingsOptions.PlaceColors"/> is null,
    /// or when <see cref="PlacingsOptions.PointsColor"/> is null and exactly one series scores, or more lines than a chart holds (32: a line for each series and one for each series that scores).</exception>
    public static ChartSpec? Build(IEnumerable<Placing> results, PlacingsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(results);
        var o = options ?? new PlacingsOptions();
        if (string.IsNullOrWhiteSpace(o.PlaceName)) throw new ArgumentException("A places chart needs a name for its places line (PlaceName).");
        if (string.IsNullOrWhiteSpace(o.PointsName)) throw new ArgumentException("A places chart needs a name for its points line and pane (PointsName).");
        if (o.PaneLabel is not null && string.IsNullOrWhiteSpace(o.PaneLabel)) throw new ArgumentException("A places chart's PaneLabel names its points pane; leave it null to use PointsName, not blank.");
        if (string.IsNullOrWhiteSpace(o.DateFormat)) throw new ArgumentException("A places chart needs a date format (DateFormat) to label an event by its date.");
        try { new DateOnly(2027, 1, 1).ToString(o.DateFormat, CultureInfo.InvariantCulture); } catch (FormatException) { throw new ArgumentException("DateFormat must be a valid format string for DateOnly.ToString."); }
        if (o.Unlabelled is null || !o.Unlabelled.Contains("{0}", StringComparison.Ordinal)) throw new ArgumentException("Unlabelled must contain {0}, where an event's number goes, such as \"R{0}\".");
        try { string.Format(CultureInfo.InvariantCulture, o.Unlabelled, 1); } catch (FormatException) { throw new ArgumentException("Unlabelled must be a valid format string for string.Format, such as \"R{0}\"."); }
        if (o.PlaceColors is { Count: 0 }) throw new ArgumentException("PlaceColors must name at least one colour, or be null for the style's.");
        var style = o.Style ?? ChartStyle.Light;
        var placed = results.Select(r => r ?? throw new ArgumentException("A places chart's results may not contain null.")).Where(r => r.Place is > 0).ToList();
        if (placed.Count == 0) return null;
        // Undated events first, as given, then dated ones by date. OrderBy is stable, so events on the same date keep the order they were given in.
        var races = placed.Where(r => r.Date is null).Concat(placed.Where(r => r.Date is not null).OrderBy(r => r.Date!.Value)).ToList();
        var labels = races.Select((r, i) => (string.IsNullOrWhiteSpace(r.Label) ? null : r.Label) ?? r.Date?.ToString(o.DateFormat, CultureInfo.InvariantCulture)
            ?? string.Format(CultureInfo.InvariantCulture, o.Unlabelled, i + 1)).ToArray();
        static string Key(Placing r) => r.Series?.Trim() ?? "";
        var keys = races.Select(Key).Distinct(StringComparer.Ordinal).ToList();
        var scoring = keys.Where(key => races.Any(r => Key(r) == key && r.Points is not null)).ToList();
        var anyPoints = scoring.Count > 0;
        var needPalette = o.PlaceColors is null || (o.PointsColor is null && scoring.Count == 1);
        if (style.Series.Count == 0 && needPalette)
            throw new ArgumentException("The style's Series palette must not be empty when PlaceColors or PointsColor need it.");
        // 32 is the renderer's limit on series (ChartValidation); each series that scores adds a points line.
        if (keys.Count + scoring.Count is var drawn and > 32)
            throw new ArgumentException($"A places chart draws a line for each series and one for each series that scores: {drawn} lines is more than the 32 a chart can hold.");
        var palette = needPalette ? DefaultPalette(style) : new List<string>();
        string Color(int k) => o.PlaceColors is { } colors ? colors[k % colors.Count] : palette[k % palette.Count];
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
            Description = o.Description is { } description ? description.Replace("{best}", best.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                : anyPoints ? $"Finishing place out of the field, first at the top, and points. Best: {best}."
                : $"Finishing place out of the field, first at the top. Best: {best}.",
            XMin = -0.5, XMax = races.Count - 0.5, YReversed = true, YLabel = "Place",
            XTicks = TickSource.PointLabels, Series = lines,
        };
        if (!anyPoints) return spec;
        // One points line for each series that scores, at that series' own races: a race without points is a gap in it. A lone points line keeps
        // PointsName and PointsColor; several are named for their series and coloured as their places lines.
        var several = scoring.Count > 1;
        var pointsLines = scoring.Select(key => new ChartSeries(
                !several || key.Length == 0 ? o.PointsName : $"{o.PointsName} · {key}",
                races.Select((r, i) => (r, i)).Where(x => Key(x.r) == key).Select(x => new ChartPoint(x.i, x.r.Points, labels[x.i])).ToArray(),
                several ? Color(keys.IndexOf(key)) : o.PointsColor ?? palette[lines.Count % palette.Count])
            { Pane = 1, ValueLabels = true, Markers = MarkerStyle.Filled }).ToList();
        return spec with { Panes = [new ChartPane { Label = o.PaneLabel ?? o.PointsName, Weight = 1 }], Series = [.. lines, .. pointsLines] };
    }

    // The style's series colours without those close to its rising or falling colour (sRGB distance 60 or less), which a line
    // or its legend swatch would otherwise borrow to say "better" or "worse"; all of them when none would be left.
    static List<string> DefaultPalette(ChartStyle style)
    {
        var kept = style.Series.Where(c => Apart(c, style.Rising) > 60 && Apart(c, style.Falling) > 60).ToList();
        return kept.Count > 0 ? kept : [.. style.Series];
    }

    // A colour that is not #RRGGBB is far from everything here; the renderer refuses it later with its own message.
    static double Apart(string? a, string? b)
    {
        static bool Rgb(string? c, out int value)
        {
            value = 0;
            return c is { Length: 7 } && c[0] == '#' && int.TryParse(c.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
        }
        if (!Rgb(a, out var x) || !Rgb(b, out var y)) return double.PositiveInfinity;
        double Channel(int shift) => ((x >> shift) & 255) - ((y >> shift) & 255);
        return Math.Sqrt(Channel(16) * Channel(16) + Channel(8) * Channel(8) + Channel(0) * Channel(0));
    }
}
