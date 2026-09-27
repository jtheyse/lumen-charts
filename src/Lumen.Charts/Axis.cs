using System.Globalization;

namespace Lumen.Charts;

public enum AxisKind { Linear, Log, Time }

/// <summary>Time axis values are Unix milliseconds. Ticks and labels are UTC; local time zones are not applied.</summary>
public static class TimeAxis
{
    public const double MinValue = -62135596800000d;
    public const double MaxValue = 253402300799999d;
    public static double Value(DateTimeOffset moment) => moment.ToUnixTimeMilliseconds();
    public static DateTimeOffset Moment(double value) => DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(value));
    internal static double Value(DateTime utc) => Value(new DateTimeOffset(utc, TimeSpan.Zero));
    internal static bool InRange(double value) => value >= MinValue && value <= MaxValue;

    /// <summary>
    /// The weekends between two moments, as spans a time axis can leave out. Days are counted in
    /// <paramref name="zone"/>, so a market's weekend is its own, not UTC's.
    /// </summary>
    public static IReadOnlyList<TimeSkip> Weekends(double from, double to, TimeZoneInfo? zone = null)
    {
        if (to <= from) return [];
        var skips = new List<TimeSkip>();
        var start = Moment(from).UtcDateTime;
        if (zone is not null) start = TimeZoneInfo.ConvertTimeFromUtc(start, zone);
        for (var day = start.Date.AddDays(-7); skips.Count < 4000; day = day.AddDays(1))
        {
            if (day.DayOfWeek is not DayOfWeek.Saturday) continue;
            double opens = Local(day, zone), closes = Local(day.AddDays(2), zone);
            if (closes <= from) continue;
            if (opens >= to) break;
            skips.Add(new(Math.Max(opens, from), Math.Min(closes, to)));
        }
        return skips;
    }

    /// <summary>
    /// Clips spans to an axis range, drops the empty ones and merges any that overlap or touch, which is
    /// the order and spacing <see cref="Axis.Skips"/> relies on to compress a domain.
    /// </summary>
    public static IReadOnlyList<TimeSkip> Normalise(IEnumerable<TimeSkip> skips, double from, double to)
    {
        var merged = new List<TimeSkip>();
        foreach (var skip in skips.Select(s => new TimeSkip(Math.Max(s.From, from), Math.Min(s.To, to)))
                                  .Where(s => s.To > s.From).OrderBy(s => s.From))
            if (merged.Count > 0 && skip.From <= merged[^1].To) merged[^1] = new(merged[^1].From, Math.Max(merged[^1].To, skip.To));
            else merged.Add(skip);
        return merged;
    }

    /// <summary>A whole day, as a span a time axis can leave out. Use it for a market holiday.</summary>
    public static TimeSkip Day(DateTime date, TimeZoneInfo? zone = null) =>
        new(Local(date.Date, zone), Local(date.Date.AddDays(1), zone));

    private static double Local(DateTime local, TimeZoneInfo? zone)
    {
        if (zone is null) return Value(DateTime.SpecifyKind(local, DateTimeKind.Utc));
        var moment = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        for (var attempt = 0; attempt < 4 && zone.IsInvalidTime(moment); attempt++) moment = moment.AddMinutes(30);
        return Value(new DateTimeOffset(moment, zone.GetUtcOffset(moment)));
    }

    /// <summary>Resolves a zone identifier, such as <c>Europe/London</c>. Null or blank means UTC.</summary>
    public static TimeZoneInfo? Zone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException($"Unknown time zone '{id}'. Use an identifier the host recognises, such as Europe/London.");
        }
    }
}

/// <summary>A span of time an axis leaves out, so the data either side of it sits together.</summary>
public sealed record TimeSkip(double From, double To);

/// <summary>Linear, base-10 logarithmic or time domain mapped onto a pixel interval.</summary>
public readonly record struct Axis(AxisKind Kind, double Min, double Max)
{
    /// <summary>The zone a time axis reads its calendar in. Null keeps everything in UTC.</summary>
    public TimeZoneInfo? Zone { get; init; }
    /// <summary>Spans the axis leaves out, such as the weekends between trading days. Ordered and non-overlapping.</summary>
    public IReadOnlyList<TimeSkip> Skips { get; init; } = [];

    private const double Second = 1000, Minute = 60 * Second, Hour = 60 * Minute, Day = 24 * Hour;
    private static readonly (double Step, string Format)[] FixedSteps =
    [
        (Second, "HH:mm:ss"), (2 * Second, "HH:mm:ss"), (5 * Second, "HH:mm:ss"), (10 * Second, "HH:mm:ss"),
        (15 * Second, "HH:mm:ss"), (30 * Second, "HH:mm:ss"), (Minute, "HH:mm"), (2 * Minute, "HH:mm"),
        (5 * Minute, "HH:mm"), (10 * Minute, "HH:mm"), (15 * Minute, "HH:mm"), (30 * Minute, "HH:mm"),
        (Hour, "HH:mm"), (2 * Hour, "HH:mm"), (3 * Hour, "HH:mm"), (6 * Hour, "HH:mm"), (12 * Hour, "HH:mm"),
        (Day, "d MMM"), (2 * Day, "d MMM"), (7 * Day, "d MMM"), (14 * Day, "d MMM"), (28 * Day, "d MMM")
    ];

    public static Axis Create(AxisKind kind, IEnumerable<double> values, bool zero = false, double? min = null, double? max = null,
        TimeZoneInfo? zone = null, bool weekends = false, IEnumerable<TimeSkip>? skips = null)
    {
        if (kind == AxisKind.Linear)
        {
            var scale = LinearScale.Create(values, zero, min, max);
            return new(kind, scale.Min, scale.Max);
        }
        var data = kind == AxisKind.Log ? values.Where(v => v > 0).ToArray() : values.ToArray();
        var lo = data.Length == 0 ? kind == AxisKind.Log ? 1 : 0 : data.Min();
        var hi = data.Length == 0 ? kind == AxisKind.Log ? 10 : Hour : data.Max();
        if (lo == hi)
        {
            if (kind == AxisKind.Log) { lo /= Math.Sqrt(10); hi *= Math.Sqrt(10); }
            else { lo -= Hour; hi += Hour; }
        }
        if (min.HasValue) lo = min.Value;
        if (max.HasValue) hi = max.Value;
        if (lo >= hi) throw new ArgumentException("Axis bounds exclude the data extent or have no range.");
        if (kind == AxisKind.Log && lo <= 0) throw new ArgumentException("Log axes require positive bounds.");
        var axis = new Axis(kind, lo, hi) { Zone = kind == AxisKind.Time ? zone : null };
        if (kind != AxisKind.Time || (!weekends && skips is null)) return axis;
        var wanted = weekends ? TimeAxis.Weekends(lo, hi, axis.Zone) : [];
        return axis with { Skips = TimeAxis.Normalise(skips is null ? wanted : wanted.Concat(skips), lo, hi) };
    }

    public double Map(double value, double start, double end)
    {
        if (Skips.Count == 0)
            return start + (Transform(value) - Transform(Min)) / (Transform(Max) - Transform(Min)) * (end - start);
        double from = Elapsed(Min), to = Elapsed(Max);
        return to == from ? start : start + (Elapsed(value) - from) / (to - from) * (end - start);
    }

    /// <summary>The distance from the axis start with the skipped spans taken out; a value inside one
    /// sits at its near edge, since the axis has no room to draw it anywhere else.</summary>
    private double Elapsed(double value)
    {
        var elapsed = value;
        foreach (var skip in Skips)
        {
            if (value <= skip.From) break;
            elapsed -= Math.Min(value, skip.To) - skip.From;
        }
        return elapsed;
    }

    /// <summary>The value at a compressed distance. Both edges of a skipped span share one position,
    /// and this returns the far one, where the axis resumes and the data sits.</summary>
    private double Restore(double elapsed)
    {
        var value = elapsed;
        foreach (var skip in Skips)
        {
            if (value < skip.From) break;
            value += skip.To - skip.From;
        }
        return value;
    }

    /// <summary>Reverses <see cref="Map"/> for a position in the same pixel interval.</summary>
    public double Invert(double position, double start, double end)
    {
        var fraction = (position - start) / (end - start);
        if (Skips.Count > 0)
        {
            double from = Elapsed(Min), to = Elapsed(Max);
            return Restore(from + fraction * (to - from));
        }
        var value = Transform(Min) + fraction * (Transform(Max) - Transform(Min));
        return Kind == AxisKind.Log ? Math.Pow(10, value) : value;
    }

    /// <summary>Label for a data value, used by tooltips and tables.</summary>
    public string Format(double value) => Kind == AxisKind.Time
        ? Local(value).ToString(Max - Min < 2 * Day ? "d MMM yyyy HH:mm" : "d MMM yyyy", CultureInfo.InvariantCulture)
        : LinearScale.Label(value);

    /// <summary>The moment as the axis's zone shows it, which is UTC when no zone is set.</summary>
    private DateTime Local(double value)
    {
        var moment = TimeAxis.Moment(value).UtcDateTime;
        return Zone is null ? moment : TimeZoneInfo.ConvertTimeFromUtc(moment, Zone);
    }

    /// <summary>The axis value for a local wall-clock reading, stepping over a gap left by a clock change.</summary>
    private double Absolute(DateTime local)
    {
        if (Zone is null) return TimeAxis.Value(DateTime.SpecifyKind(local, DateTimeKind.Utc));
        var moment = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // Spring forward removes an hour from the local clock; a tick that lands inside it moves to the
        // first reading the zone actually had.
        for (var attempt = 0; attempt < 4 && Zone.IsInvalidTime(moment); attempt++) moment = moment.AddMinutes(30);
        return TimeAxis.Value(new DateTimeOffset(moment, Zone.GetUtcOffset(moment)));
    }

    /// <summary>Tick values inside the domain with their axis labels. Time ticks fall on calendar boundaries.</summary>
    public IReadOnlyList<(double Value, string Label)> Ticks(int count = 5)
    {
        if (Kind == AxisKind.Log) return LogTicks(count);
        if (Kind != AxisKind.Time) return new LinearScale(Min, Max).Ticks(count).Select(v => (v, LinearScale.Label(v))).ToArray();
        var ticks = TimeTicks(count);
        if (Skips.Count == 0) return ticks;
        var skips = Skips;
        return ticks.Where(tick => !skips.Any(skip => tick.Item1 >= skip.From && tick.Item1 < skip.To)).ToArray();
    }

    /// <summary>
    /// Values between the labelled ticks, for a lighter grid. A linear axis divides each interval into
    /// four or five depending on its step, a log axis marks the mantissas between decades, and a time
    /// axis has none, because half of a month is not a boundary anyone reads.
    /// </summary>
    public IReadOnlyList<double> MinorTicks(int count = 5)
    {
        var major = Ticks(count).Select(t => t.Value).ToArray();
        if (Kind == AxisKind.Time || major.Length < 2) return [];
        var result = new List<double>();
        if (Kind == AxisKind.Log)
        {
            // A decade is divided by its own mantissas, which is what makes a log grid readable.
            foreach (var tick in major)
                for (var mantissa = 2; mantissa <= 9; mantissa++)
                {
                    var value = tick * mantissa;
                    if (value > Min && value < Max) result.Add(value);
                }
            return result;
        }
        var step = major[1] - major[0];
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(step))));
        var divisions = Math.Abs(step / magnitude - 2) < .01 || Math.Abs(step / magnitude - 2.5) < .01 ? 4 : 5;
        for (var value = major[0] - step; value < Max; value += step)
            for (var division = 1; division < divisions; division++)
            {
                var minor = value + step * division / divisions;
                if (minor > Min && minor < Max) result.Add(minor);
            }
        return result;
    }

    private double Transform(double value) => Kind == AxisKind.Log ? Math.Log10(value) : value;

    private IReadOnlyList<(double, string)> LogTicks(int count)
    {
        // Decades fully inside the domain. One or two of them are subdivided at 2 and 5 instead.
        double low = Min, high = Max;
        var lowest = (int)Math.Ceiling(Math.Log10(low) - 1e-9);
        var highest = (int)Math.Floor(Math.Log10(high) + 1e-9);
        var result = new List<(double, string)>();
        if (highest - lowest + 1 <= 2)
            for (var exponent = (int)Math.Floor(Math.Log10(low)); exponent <= (int)Math.Floor(Math.Log10(high)); exponent++)
                foreach (var mantissa in (double[])[1, 2, 5]) Add(mantissa * Math.Pow(10, exponent));
        else
        {
            var stride = Math.Max(1, (int)Math.Ceiling((highest - lowest + 1) / (double)Math.Max(2, count)));
            for (var exponent = lowest; exponent <= highest; exponent += stride) Add(Math.Pow(10, exponent));
        }
        return result;
        void Add(double value)
        {
            if (value >= low * (1 - 1e-9) && value <= high * (1 + 1e-9)) result.Add((value, LinearScale.Label(value)));
        }
    }

    private IReadOnlyList<(double, string)> TimeTicks(int count)
    {
        var span = Max - Min;
        var target = Math.Max(2, count);
        foreach (var (step, format) in FixedSteps)
        {
            if (span / step > target) continue;
            // Steps of two days or more align to Monday 5 January 1970 rather than the epoch Thursday.
            var origin = step >= 2 * Day ? 4 * Day : 0;
            var result = new List<(double, string)>();
            if (Zone is null)
            {
                for (var value = origin + Math.Ceiling((Min - origin) / step) * step; value <= Max; value += step)
                    result.Add((value, TimeAxis.Moment(value).UtcDateTime.ToString(format, CultureInfo.InvariantCulture)));
                return result;
            }
            // In a zone the boundaries belong to the local clock, so each tick is realigned to it; a
            // clock change therefore shifts the following ticks rather than the whole series drifting.
            var next = Align(Min, step, origin);
            while (next <= Max)
            {
                result.Add((next, Local(next).ToString(format, CultureInfo.InvariantCulture)));
                next = Align(next + step * .5, step, origin);
                if (result.Count > 64) break;
            }
            return result;
        }
        foreach (var months in (int[])[1, 3, 6])
            if (span / (months * 30.44 * Day) <= target) return MonthTicks(months);
        for (var magnitude = 1; magnitude <= 1_000_000; magnitude *= 10)
            foreach (var factor in (int[])[1, 2, 5])
                if (span / (magnitude * factor * 365.25 * Day) <= target) return YearTicks(magnitude * factor);
        return YearTicks(1_000_000);
    }

    /// <summary>The first local boundary of <paramref name="step"/> at or after <paramref name="from"/>.</summary>
    private double Align(double from, double step, double origin)
    {
        var local = Local(from);
        var day = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Utc);
        if (step >= Day)
        {
            // Whole days and multiples of them keep the epoch's Monday phase, counted in local days.
            var days = (int)Math.Round(step / Day);
            var since = (int)Math.Floor((day - new DateTime(1970, 1, 5, 0, 0, 0, DateTimeKind.Utc)).TotalDays);
            var phase = ((since % days) + days) % days;
            day = day.AddDays(-phase);
            var value = Absolute(day);
            return value >= from ? value : Absolute(day.AddDays(days));
        }
        var elapsed = local.TimeOfDay.TotalMilliseconds;
        var floored = day.AddMilliseconds(Math.Floor(elapsed / step) * step);
        var at = Absolute(floored);
        return at >= from ? at : Absolute(floored.AddMilliseconds(step));
    }

    private IReadOnlyList<(double, string)> MonthTicks(int step)
    {
        var start = Local(Min);
        var index = start.Year * 12 + start.Month - 1;
        if (start.Day > 1 || start.TimeOfDay > TimeSpan.Zero) index++;
        index = (int)(Math.Ceiling(index / (double)step) * step);
        var result = new List<(double, string)>();
        while (index / 12 <= 9999)
        {
            var moment = new DateTime(index / 12, index % 12 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var value = Absolute(moment);
            if (value > Max) break;
            if (value >= Min) result.Add((value, moment.ToString("MMM yyyy", CultureInfo.InvariantCulture)));
            index += step;
        }
        return result;
    }

    private IReadOnlyList<(double, string)> YearTicks(int step)
    {
        var start = Local(Min);
        var year = start.Year + (start.Month > 1 || start.Day > 1 || start.TimeOfDay > TimeSpan.Zero ? 1 : 0);
        year = (int)(Math.Ceiling(year / (double)step) * step);
        var result = new List<(double, string)>();
        for (; year is >= 1 and <= 9999; year += step)
        {
            var value = Absolute(new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            if (value > Max) break;
            if (value >= Min) result.Add((value, year.ToString(CultureInfo.InvariantCulture)));
        }
        return result;
    }
}
