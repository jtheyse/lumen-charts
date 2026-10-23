using System.Globalization;

namespace Lumen.Charts;

/// <summary>How many things happened on one day, for <see cref="DailyCountsChart"/> (0.47.0).</summary>
/// <param name="Date">The day.</param>
/// <param name="Count">How many, 0 or more; several counts on one day are added up.</param>
public sealed record DayCount(DateOnly Date, int Count);

/// <summary>The words, range and look of a <see cref="DailyCountsChart"/> (0.47.0).</summary>
public sealed record DailyCountsOptions
{
    /// <summary>The word for one, said in a bar's name for a count of 1: <c>registration</c> by default.</summary>
    public string Singular { get; init; } = "registration";
    /// <summary>The word for any other count, and capitalised the series' name and the title's start: <c>registrations</c> by default.</summary>
    public string Plural { get; init; } = "registrations";
    /// <summary>The first day drawn; null, the earliest date given, or <see cref="To"/> where that is earlier.</summary>
    public DateOnly? From { get; init; }
    /// <summary>The last day drawn; null, the latest date given, or <see cref="From"/> where that is later.</summary>
    public DateOnly? To { get; init; }
    /// <summary>Each day's width in pixels, at least 8: 30 by default, a bar about 22 wide.</summary>
    public double CellWidth { get; init; } = 30;
    /// <summary>How a day is written in its bar's name, the data table and CSV, in the invariant culture: <c>ddd d MMM yyyy</c> by default.
    /// A format that writes a name past 200 characters, or onto two lines, is refused by validation when the chart is rendered.</summary>
    public string DateFormat { get; init; } = "ddd d MMM yyyy";
    /// <summary>Says how many so far after each day's count in its name, the default.</summary>
    public bool RunningTotal { get; init; } = true;
    /// <summary>The chart's title; null, the plural capitalised with "per day".</summary>
    public string? Title { get; init; }
    /// <summary>The bars' <c>#RRGGBB</c> colour; null, the style's first series colour.</summary>
    public string? Color { get; init; }
    /// <summary>The chart's style; null, the Light preset.</summary>
    public ChartStyle? Style { get; init; }
}

/// <summary>
/// Builds a chart of counts per day, such as sign-ups or orders, as an ordinary <see cref="ChartSpec"/> (0.47.0): one column a day at a fixed
/// pitch (<see cref="ChartSpec.CellWidth"/>), so a long span scrolls sideways rather than squeezing; missing days drawn as 0, each as a muted
/// hairline (<see cref="ChartSeries.MarkZero"/>); the count above every bar; the day number under each, with the month under the first and
/// each 1st, and the year where the span crosses one (<see cref="ChartPoint.AxisLabel"/>); and each bar named with its date, its count in
/// the singular or the plural, and the total so far (<see cref="ChartPoint.Name"/>). Show it with
/// <c>&lt;LumenChart FitWidth="true" ScrollToEnd="true" ShowLegend="false"&gt;</c>, and change anything else with <c>spec with { … }</c>.
/// </summary>
public static class DailyCountsChart
{
    /// <summary>The most days one chart draws: the most categories a column chart with a cell width takes.</summary>
    public const int MaxDays = ChartValidation.MaxFixedCategories;

    /// <summary>The chart of <paramref name="counts"/>, or null when there is no day to draw or every day in the range is 0, so the page shows
    /// its own empty state. Counts outside the range are left out.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="counts"/> is null.</exception>
    /// <exception cref="ArgumentException">A count is null or negative, a word is blank, the date format cannot format a date, From is after To,
    /// or the range is longer than <see cref="MaxDays"/> days.</exception>
    public static ChartSpec? Build(IEnumerable<DayCount> counts, DailyCountsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var o = options ?? new DailyCountsOptions();
        if (string.IsNullOrWhiteSpace(o.Singular) || string.IsNullOrWhiteSpace(o.Plural))
            throw new ArgumentException("A daily-counts chart needs a word for one and a word for several (Singular and Plural), such as registration and registrations.");
        try { new DateOnly(2027, 1, 1).ToString(o.DateFormat, CultureInfo.InvariantCulture); }
        catch (FormatException) { throw new ArgumentException("DateFormat must be a valid format string for DateOnly.ToString."); }
        if (o.From is { } setFrom && o.To is { } setTo && setFrom > setTo)
            throw new ArgumentException(FormattableString.Invariant($"From, {setFrom:yyyy-MM-dd}, is after To, {setTo:yyyy-MM-dd}."));
        var list = counts.ToList();
        if (list.Any(c => c is null)) throw new ArgumentException("A daily count cannot be null.");
        if (list.Any(c => c.Count < 0)) throw new ArgumentException("A day's count cannot be negative.");
        if (list.Count == 0 && o.From is null && o.To is null) return null;
        // An end that is not set stretches to take in the one that is.
        DateOnly? earliest = list.Count > 0 ? list.Min(c => c.Date) : null, latest = list.Count > 0 ? list.Max(c => c.Date) : null;
        var from = o.From ?? new[] { earliest, o.To }.Where(d => d.HasValue).Min()!.Value;
        var to = o.To ?? new[] { latest, o.From }.Where(d => d.HasValue).Max()!.Value;
        var days = to.DayNumber - from.DayNumber + 1;
        if (days > MaxDays)
            throw new ArgumentException(FormattableString.Invariant($"A daily-counts chart draws at most {MaxDays:N0} days, and {from:yyyy-MM-dd} to {to:yyyy-MM-dd} is {days:N0}; narrow the range."));
        var totals = new long[days];
        foreach (var c in list)
            if (c.Date >= from && c.Date <= to) totals[c.Date.DayNumber - from.DayNumber] += c.Count;
        if (totals.All(n => n == 0)) return null;
        var style = o.Style ?? ChartStyle.Light;
        if (o.Color is null && style.Series.Count == 0)
            throw new ArgumentException("The style has no series colours to draw the bars in; give Color or a style with a palette.");
        string Noun(long n) => n == 1 ? o.Singular : o.Plural;
        static string Number(long n) => n.ToString("N0", CultureInfo.InvariantCulture);
        static string Capital(string word) => char.ToUpperInvariant(word[0]) + word[1..];
        var crosses = from.Year != to.Year;
        var points = new ChartPoint[days];
        long running = 0;
        for (var i = 0; i < days; i++)
        {
            var day = from.AddDays(i);
            running += totals[i];
            var label = day.ToString(o.DateFormat, CultureInfo.InvariantCulture);
            var month = i == 0 || day.Day == 1 ? day.ToString(crosses && (i == 0 || day.Month == 1) ? "MMM yyyy" : "MMM", CultureInfo.InvariantCulture) : null;
            var number = day.Day.ToString(CultureInfo.InvariantCulture);
            points[i] = new ChartPoint(i, totals[i], label)
            {
                AxisLabel = month is null ? number : $"{number}\n{month}",
                Name = $"{label}: {Number(totals[i])} {Noun(totals[i])}" + (o.RunningTotal ? $", {Number(running)} so far" : "")
            };
        }
        // Whole-number ticks, at most five steps, one step above the tallest day so its count stands clear of the description.
        var largest = totals.Max();
        long step = 1;
        for (var k = 0; largest / step + 1 > 5; k++) step = k % 3 == 1 ? step * 5 / 2 : step * 2;
        var top = step * (largest / step + 1);
        var ticks = new List<AxisTick>();
        for (var v = 0L; v <= top; v += step) ticks.Add(new AxisTick(v));
        var total = totals.Sum();
        return new ChartSpec
        {
            Kind = ChartKind.Column, CellWidth = o.CellWidth, Width = 320, Height = 320, Style = style,
            Title = o.Title ?? $"{Capital(o.Plural)} per day",
            Description = $"{Number(total)} {Noun(total)} across {Number(days)} {(days == 1 ? "day" : "days")}",
            YMax = top, YTickValues = ticks,
            Series = [new ChartSeries(Capital(o.Plural), points) { Color = o.Color ?? style.Series[0], ValueLabels = true, MarkZero = true }]
        };
    }
}
