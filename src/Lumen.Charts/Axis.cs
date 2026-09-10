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
}

/// <summary>Linear, base-10 logarithmic or time domain mapped onto a pixel interval.</summary>
public readonly record struct Axis(AxisKind Kind, double Min, double Max)
{
    private const double Second = 1000, Minute = 60 * Second, Hour = 60 * Minute, Day = 24 * Hour;
    private static readonly (double Step, string Format)[] FixedSteps =
    [
        (Second, "HH:mm:ss"), (2 * Second, "HH:mm:ss"), (5 * Second, "HH:mm:ss"), (10 * Second, "HH:mm:ss"),
        (15 * Second, "HH:mm:ss"), (30 * Second, "HH:mm:ss"), (Minute, "HH:mm"), (2 * Minute, "HH:mm"),
        (5 * Minute, "HH:mm"), (10 * Minute, "HH:mm"), (15 * Minute, "HH:mm"), (30 * Minute, "HH:mm"),
        (Hour, "HH:mm"), (2 * Hour, "HH:mm"), (3 * Hour, "HH:mm"), (6 * Hour, "HH:mm"), (12 * Hour, "HH:mm"),
        (Day, "d MMM"), (2 * Day, "d MMM"), (7 * Day, "d MMM"), (14 * Day, "d MMM"), (28 * Day, "d MMM")
    ];

    public static Axis Create(AxisKind kind, IEnumerable<double> values, bool zero = false, double? min = null, double? max = null)
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
        return new(kind, lo, hi);
    }

    public double Map(double value, double start, double end) =>
        start + (Transform(value) - Transform(Min)) / (Transform(Max) - Transform(Min)) * (end - start);

    /// <summary>Reverses <see cref="Map"/> for a position in the same pixel interval.</summary>
    public double Invert(double position, double start, double end)
    {
        var fraction = (position - start) / (end - start);
        var value = Transform(Min) + fraction * (Transform(Max) - Transform(Min));
        return Kind == AxisKind.Log ? Math.Pow(10, value) : value;
    }

    /// <summary>Label for a data value, used by tooltips and tables.</summary>
    public string Format(double value) => Kind == AxisKind.Time
        ? TimeAxis.Moment(value).UtcDateTime.ToString(Max - Min < 2 * Day ? "d MMM yyyy HH:mm" : "d MMM yyyy", CultureInfo.InvariantCulture)
        : LinearScale.Label(value);

    /// <summary>Tick values inside the domain with their axis labels. Time ticks fall on calendar boundaries.</summary>
    public IReadOnlyList<(double Value, string Label)> Ticks(int count = 5) => Kind switch
    {
        AxisKind.Time => TimeTicks(count),
        AxisKind.Log => LogTicks(count),
        _ => new LinearScale(Min, Max).Ticks(count).Select(v => (v, LinearScale.Label(v))).ToArray()
    };

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
            for (var value = origin + Math.Ceiling((Min - origin) / step) * step; value <= Max; value += step)
                result.Add((value, TimeAxis.Moment(value).UtcDateTime.ToString(format, CultureInfo.InvariantCulture)));
            return result;
        }
        foreach (var months in (int[])[1, 3, 6])
            if (span / (months * 30.44 * Day) <= target) return MonthTicks(months);
        for (var magnitude = 1; magnitude <= 1_000_000; magnitude *= 10)
            foreach (var factor in (int[])[1, 2, 5])
                if (span / (magnitude * factor * 365.25 * Day) <= target) return YearTicks(magnitude * factor);
        return YearTicks(1_000_000);
    }

    private IReadOnlyList<(double, string)> MonthTicks(int step)
    {
        var start = TimeAxis.Moment(Min).UtcDateTime;
        var index = start.Year * 12 + start.Month - 1;
        if (start.Day > 1 || start.TimeOfDay > TimeSpan.Zero) index++;
        index = (int)(Math.Ceiling(index / (double)step) * step);
        var result = new List<(double, string)>();
        while (index / 12 <= 9999)
        {
            var moment = new DateTime(index / 12, index % 12 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var value = TimeAxis.Value(moment);
            if (value > Max) break;
            if (value >= Min) result.Add((value, moment.ToString("MMM yyyy", CultureInfo.InvariantCulture)));
            index += step;
        }
        return result;
    }

    private IReadOnlyList<(double, string)> YearTicks(int step)
    {
        var start = TimeAxis.Moment(Min).UtcDateTime;
        var year = start.Year + (start.Month > 1 || start.Day > 1 || start.TimeOfDay > TimeSpan.Zero ? 1 : 0);
        year = (int)(Math.Ceiling(year / (double)step) * step);
        var result = new List<(double, string)>();
        for (; year is >= 1 and <= 9999; year += step)
        {
            var value = TimeAxis.Value(new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            if (value > Max) break;
            if (value >= Min) result.Add((value, year.ToString(CultureInfo.InvariantCulture)));
        }
        return result;
    }
}
