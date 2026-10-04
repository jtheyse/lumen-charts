using System.Globalization;

namespace Lumen.Charts;

/// <summary>How an axis spaces its values.</summary>
public enum AxisKind
{
    /// <summary>Equal steps for equal differences.</summary>
    Linear,
    /// <summary>Equal steps for equal ratios, base 10. Positive values only.</summary>
    Log,
    /// <summary>Moments in Unix milliseconds, ticked and labelled by the calendar. X only.</summary>
    Time
}

/// <summary>
/// How an axis writes its values. <see cref="Duration"/> reads them as seconds: m:ss and h:mm:ss on a linear
/// axis, 1s, 5m and 1h on a logarithmic one. <see cref="Compact"/> writes 1.2k, 3.4M and 1.5B.
/// <see cref="TimeOfDay"/> reads seconds since a midnight and writes the clock, HH:mm. <see cref="Signed"/> writes a number with
/// its sign, +5 and −5. A time axis writes its calendar and takes none of them.
/// </summary>
public enum ValueFormat
{
    /// <summary>Plain numbers, the default.</summary>
    Number,
    /// <summary>Seconds, written as a clock or a span.</summary>
    Duration,
    /// <summary>Thousands, millions, billions and trillions with a suffix.</summary>
    Compact,
    /// <summary>Seconds since a midnight, written as the time of day, HH:mm, rounded to the minute and wrapping at 24 hours:
    /// 84600 reads 23:30 and 110400, the next morning, 06:40, so a night is one unbroken span that never crosses zero. Ticks
    /// land on whole hours, or on half and quarter hours over a short range. Linear axes only.</summary>
    TimeOfDay,
    /// <summary>Plain numbers, as <see cref="Number"/> writes them, with their sign written out: a plus for a positive value, +5, a
    /// true minus sign (U+2212) for a negative one, −5, and zero, negative zero included, as 0. For values that read above and below
    /// a balance, such as training form or a change from the day before. Taken wherever another format is: axes, value labels,
    /// tooltips, the data table and annotations.</summary>
    Signed
}

/// <summary>Time axis values are Unix milliseconds. Ticks and labels read in UTC unless <see cref="ChartSpec.TimeZone"/> names a zone,
/// whose calendar the axis then follows.</summary>
public static class TimeAxis
{
    /// <summary>The earliest value a time axis takes: the start of 1 January in the year 1, UTC.</summary>
    public const double MinValue = -62135596800000d;
    /// <summary>The latest value a time axis takes: the last millisecond of 31 December 9999, UTC.</summary>
    public const double MaxValue = 253402300799999d;
    /// <summary>A moment in Unix milliseconds, the units a time axis reads.</summary>
    public static double Value(DateTimeOffset moment) => moment.ToUnixTimeMilliseconds();
    /// <summary>A time axis value back as a moment in UTC, rounded to the millisecond.</summary>
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
/// <param name="From">Where the span starts, in Unix milliseconds.</param>
/// <param name="To">Where it ends and the axis resumes.</param>
public sealed record TimeSkip(double From, double To);

/// <summary>Linear, base-10 logarithmic or time domain mapped onto a pixel interval.</summary>
public readonly record struct Axis(AxisKind Kind, double Min, double Max)
{
    /// <summary>The zone a time axis reads its calendar in. Null keeps everything in UTC.</summary>
    public TimeZoneInfo? Zone { get; init; }
    /// <summary>Spans the axis leaves out, such as the weekends between trading days. Ordered and non-overlapping.</summary>
    public IReadOnlyList<TimeSkip> Skips { get; init; } = [];
    /// <summary>How ticks, tooltips and tables write a value. A time axis ignores it and writes its calendar.</summary>
    public ValueFormat ValueFormat { get; init; }
    /// <summary>Maps the minimum to the end of the pixel interval instead of its start, so on a Y axis the
    /// smallest value sits at the top. Everything placed through <see cref="Map"/> follows.</summary>
    public bool Reversed { get; init; }
    /// <summary>Written straight after every value <see cref="Format"/> and <see cref="Ticks"/> write, exactly as given, such as <c>s</c>
    /// or <c> bpm</c>; a time axis writes its calendar and takes none. Null writes none.</summary>
    public string? Unit { get; init; }

    private const double Second = 1000, Minute = 60 * Second, Hour = 60 * Minute, Day = 24 * Hour;
    private static readonly (double Step, string Format)[] FixedSteps =
    [
        (Second, "HH:mm:ss"), (2 * Second, "HH:mm:ss"), (5 * Second, "HH:mm:ss"), (10 * Second, "HH:mm:ss"),
        (15 * Second, "HH:mm:ss"), (30 * Second, "HH:mm:ss"), (Minute, "HH:mm"), (2 * Minute, "HH:mm"),
        (5 * Minute, "HH:mm"), (10 * Minute, "HH:mm"), (15 * Minute, "HH:mm"), (30 * Minute, "HH:mm"),
        (Hour, "HH:mm"), (2 * Hour, "HH:mm"), (3 * Hour, "HH:mm"), (6 * Hour, "HH:mm"), (12 * Hour, "HH:mm"),
        (Day, "d MMM"), (2 * Day, "d MMM"), (7 * Day, "d MMM"), (14 * Day, "d MMM"), (28 * Day, "d MMM")
    ];
    /// <summary>Round durations in seconds for a linear axis, each with the parts its minor lines divide it into.</summary>
    private static readonly (double Step, int Parts)[] DurationSteps =
    [
        (1, 5), (2, 4), (5, 5), (10, 5), (15, 3), (30, 6), (60, 4), (120, 4), (300, 5), (600, 5), (900, 3), (1800, 6),
        (3600, 4), (7200, 4), (10800, 3), (21600, 6), (43200, 4)
    ];
    /// <summary>The steps of a time-of-day axis in seconds — quarter and half hours, then whole hours — each with the parts its
    /// minor lines divide it into.</summary>
    private static readonly (double Step, int Parts)[] ClockSteps =
        [(900, 3), (1800, 2), (3600, 4), (7200, 4), (10800, 3), (21600, 6), (43200, 4), (86400, 4)];
    /// <summary>Round durations for a logarithmic axis. Past five hours every whole hour is one too.</summary>
    private static readonly double[] LogDurations = [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1200, 1800, 3600, 7200, 10800, 14400, 18000];
    private static readonly (double Size, string Suffix)[] Magnitudes = [(1e3, "k"), (1e6, "M"), (1e9, "B"), (1e12, "T")];

    /// <summary>
    /// An axis fitted to <paramref name="values"/>, as a chart fits one. <paramref name="zero"/> stretches a linear axis to
    /// include zero; <paramref name="min"/> and <paramref name="max"/> replace the data's extent; <paramref name="zone"/>,
    /// <paramref name="weekends"/> and <paramref name="skips"/> set a time axis's calendar and the spans it leaves out. Throws
    /// <see cref="ArgumentException"/> when the bounds leave no range, or a logarithmic axis would reach zero or below.
    /// </summary>
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

    /// <summary>Where <paramref name="value"/> falls between <paramref name="start"/> and <paramref name="end"/>, the positions
    /// of the axis's minimum and maximum, measured in the axis's own space: logarithmic on a log axis, and with the skipped
    /// spans closed up on a time axis. A reversed axis swaps the two ends.</summary>
    public double Map(double value, double start, double end)
    {
        if (Reversed) (start, end) = (end, start);
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
        if (Reversed) (start, end) = (end, start);
        var fraction = (position - start) / (end - start);
        if (Skips.Count > 0)
        {
            double from = Elapsed(Min), to = Elapsed(Max);
            return Restore(from + fraction * (to - from));
        }
        var value = Transform(Min) + fraction * (Transform(Max) - Transform(Min));
        return Kind == AxisKind.Log ? Math.Pow(10, value) : value;
    }

    /// <summary>Label for a data value, used by tooltips and tables, with the axis's <see cref="Unit"/> after it.</summary>
    public string Format(double value) => Kind == AxisKind.Time
        ? Local(value).ToString(Max - Min < 2 * Day ? "d MMM yyyy HH:mm" : "d MMM yyyy", CultureInfo.InvariantCulture)
        : Label(value) + Unit;

    private string Label(double value) => ValueFormat switch
    {
        ValueFormat.Duration => Kind == AxisKind.Log ? Span(value) : Clock(value),
        ValueFormat.Compact => Compact(value),
        ValueFormat.TimeOfDay => TimeOfDay(value),
        ValueFormat.Signed => Signed(value),
        _ => LinearScale.Label(value)
    };

    /// <summary>A number as <see cref="ValueFormat.Number"/> writes it, after a plus for a positive value and a true minus sign, U+2212,
    /// for a negative one; zero, negative zero included, is 0.</summary>
    private static string Signed(double value) => value == 0 ? "0" : (value > 0 ? "+" : "−") + LinearScale.Label(Math.Abs(value));

    /// <summary>Seconds since a midnight as the time of day, HH:mm: rounded half up to the minute and wrapped into one day, so
    /// a value past 24 hours, or below zero, reads as the clock it stands at.</summary>
    private static string TimeOfDay(double seconds)
    {
        var minutes = Math.Round(seconds / 60, MidpointRounding.AwayFromZero) % 1440;
        if (minutes < 0) minutes += 1440;
        return string.Create(CultureInfo.InvariantCulture, $"{Math.Floor(minutes / 60):00}:{minutes % 60:00}");
    }

    /// <summary>A moment on a time axis as its zone's clock shows it, in <paramref name="format"/>.</summary>
    internal string LocalText(double value, string format) => Local(value).ToString(format, CultureInfo.InvariantCulture);

    /// <summary>Seconds as m:ss below an hour and h:mm:ss from an hour up, rounded half up to the second.</summary>
    private static string Clock(double seconds)
    {
        var total = Math.Round(Math.Abs(seconds), MidpointRounding.AwayFromZero);
        var sign = seconds < 0 && total > 0 ? "-" : "";
        double hours = Math.Floor(total / 3600), minutes = Math.Floor(total % 3600 / 60), rest = total % 60;
        return hours == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{sign}{minutes}:{rest:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{sign}{hours}:{minutes:00}:{rest:00}");
    }

    /// <summary>Seconds as the largest units they hold: 1s, 20m, 1h, and 2h30m when a value is not a whole unit.
    /// Below a minute a fraction is kept, to two places.</summary>
    private static string Span(double seconds)
    {
        var size = Math.Abs(seconds);
        var sign = seconds < 0 ? "-" : "";
        if (Math.Round(size, 2) < 60) return sign + size.ToString("0.##", CultureInfo.InvariantCulture) + "s";
        var total = Math.Round(size, MidpointRounding.AwayFromZero);
        return sign + Part(Math.Floor(total / 3600), "h") + Part(Math.Floor(total % 3600 / 60), "m") + Part(total % 60, "s");
        static string Part(double amount, string unit) => amount > 0 ? amount.ToString(CultureInfo.InvariantCulture) + unit : "";
    }

    /// <summary>1.2k, 3.4M, 1.5B and 2T, with at most one decimal. A value that rounds to a thousand of one unit
    /// is written in the next, so 999,999 reads 1M rather than 1000k.</summary>
    private static string Compact(double value)
    {
        if (Math.Abs(value) < 1000) return LinearScale.Label(value);
        foreach (var (size, suffix) in Magnitudes)
        {
            // Counting in tenths keeps the rounding exact: 1050 is 10.5 tenths of a thousand, not 1.0499999.
            var tenths = Math.Round(Math.Abs(value) / (size / 10), MidpointRounding.AwayFromZero);
            if (tenths < 10_000) return (value < 0 ? "-" : "") + (tenths / 10).ToString("0.#", CultureInfo.InvariantCulture) + suffix;
        }
        return LinearScale.Label(value);
    }

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

    /// <summary>Tick values inside the domain with their axis labels, each followed by the axis's <see cref="Unit"/>. Time ticks fall on
    /// calendar boundaries.</summary>
    public IReadOnlyList<(double Value, string Label)> Ticks(int count = 5)
    {
        var ticks = Unitless(count);
        if (string.IsNullOrEmpty(Unit) || Kind == AxisKind.Time) return ticks;
        var unit = Unit;
        return ticks.Select(tick => (tick.Value, tick.Label + unit)).ToArray();
    }

    private IReadOnlyList<(double Value, string Label)> Unitless(int count)
    {
        if (Kind == AxisKind.Log) return ValueFormat == ValueFormat.Duration ? LogDurationTicks(count) : LogTicks(count);
        if (Kind != AxisKind.Time)
        {
            var axis = this;
            if (ValueFormat is not (ValueFormat.Duration or ValueFormat.TimeOfDay)) return new LinearScale(Min, Max).Ticks(count).Select(v => (v, axis.Label(v))).ToArray();
            var target = Math.Max(2, count);
            if (ValueFormat == ValueFormat.TimeOfDay)
            {
                var hours = ClockStep(target);
                var start = Math.Ceiling(Min / hours);
                return Enumerable.Range(0, target).Select(i => (start + i) * hours).Where(value => value <= axis.Max)
                    .Select(value => (value, TimeOfDay(value))).ToArray();
            }
            var step = DurationStep(target);
            var first = Math.Ceiling(Min / step);
            // Counted in whole steps, so a huge value whose next step rounds to itself cannot loop for ever.
            return Enumerable.Range(0, target).Select(i => (first + i) * step).Where(value => value <= axis.Max)
                .Select(value => (value, Clock(value))).ToArray();
        }
        var ticks = TimeTicks(count);
        if (Skips.Count == 0) return ticks;
        var skips = Skips;
        return ticks.Where(tick => !skips.Any(skip => tick.Item1 >= skip.From && tick.Item1 < skip.To)).ToArray();
    }

    /// <summary>
    /// Values between the labelled ticks, for a lighter grid. A linear axis divides each interval into
    /// four or five depending on its step, a log axis marks the mantissas between decades, and a time
    /// axis has none, because half of a month is not a boundary anyone reads. A duration axis divides
    /// its steps into round durations, and has none when logarithmic, where a mantissa of 20 minutes
    /// is no duration anyone reads either. A time-of-day axis divides an hour into quarters and a
    /// quarter hour into fives.
    /// </summary>
    public IReadOnlyList<double> MinorTicks(int count = 5)
    {
        var major = Ticks(count).Select(t => t.Value).ToArray();
        if (Kind == AxisKind.Time || major.Length < 2 || (Kind == AxisKind.Log && ValueFormat == ValueFormat.Duration)) return [];
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
        var divisions = ValueFormat == ValueFormat.Duration ? Parts(step) : ValueFormat == ValueFormat.TimeOfDay ? Parts(step, ClockSteps)
            : Math.Abs(step / magnitude - 2) < .01 || Math.Abs(step / magnitude - 2.5) < .01 ? 4 : 5;
        // Counted as well as summed: near 1e21 a step can be too small to move the value, which would never reach Max.
        for (var (value, interval) = (major[0] - step, 0); value < Max && interval <= major.Length; value += step, interval++)
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
        var axis = this;
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
            if (value >= low * (1 - 1e-9) && value <= high * (1 + 1e-9)) result.Add((value, axis.Label(value)));
        }
    }

    /// <summary>The smallest round duration that puts no more than <paramref name="target"/> ticks on the axis.</summary>
    private double DurationStep(int target) => Step(DurationSteps, target);

    /// <summary>The smallest step of the clock — a quarter or half hour, or a whole number of hours dividing the day — that
    /// puts no more than <paramref name="target"/> ticks on the axis.</summary>
    private double ClockStep(int target) => Step(ClockSteps, target);

    private double Step((double Step, int Parts)[] ladder, int target)
    {
        double min = Min, max = Max;
        bool Fits(double step) => Math.Floor(max / step) - Math.Ceiling(min / step) + 1 <= target;
        foreach (var (step, _) in ladder) if (Fits(step)) return step;
        // Past the ladder the steps are whole days, and any fewer than this always give too many ticks.
        const double day = 86400;
        var days = Math.Floor((max - min) / day / (target + 1)) + 1;
        for (var tries = 0; tries < 1000 && !Fits(days * day); tries++) days++;
        // A span of centuries settles for a step that fits rather than trying every day count on the way.
        while (!Fits(days * day)) days *= 2;
        return days * day;
    }

    /// <summary>The parts a duration step divides into, so its minor lines land on round durations too: a
    /// minute into quarters, an hour into quarters, a day into six-hour parts and a few days into days.</summary>
    private static int Parts(double step) => Parts(step, DurationSteps);

    private static int Parts(double step, (double Step, int Parts)[] steps)
    {
        foreach (var (ladder, parts) in steps) if (ladder == step) return parts;
        var days = step / 86400;
        return days == 1 ? 4 : days <= 7 ? (int)days : 1;
    }

    /// <summary>
    /// Round durations inside the range, thinned to about <paramref name="count"/> evenly spaced on screen.
    /// The ladder values nearest each end are always kept, so the axis is labelled to its edges.
    /// </summary>
    private IReadOnlyList<(double, string)> LogDurationTicks(int count)
    {
        double low = Min * (1 - 1e-9), high = Max * (1 + 1e-9);
        // Every whole hour past five is on the ladder, so only the two beside a value are listed.
        IEnumerable<double> Ladder(double near) => LogDurations
            .Concat(new[] { Math.Floor(near / 3600), Math.Ceiling(near / 3600) }.Where(hours => hours >= 6).Select(hours => hours * 3600))
            .Where(value => value >= low && value <= high);
        var ends = Ladder(Min).Concat(Ladder(Max)).ToArray();
        if (ends.Length == 0) return [];
        double first = Math.Log10(ends.Min()), last = Math.Log10(ends.Max());
        var target = Math.Max(2, count);
        var picked = new SortedSet<double>();
        for (var i = 0; i < target; i++)
        {
            var at = first + i * (last - first) / (target - 1);
            picked.Add(Ladder(Math.Pow(10, at)).MinBy(value => Math.Abs(Math.Log10(value) - at)));
        }
        return picked.Select(value => (value, Span(value))).ToArray();
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
