using System.Globalization;

namespace Lumen.Charts;

public readonly record struct LinearScale(double Min, double Max)
{
    public double Map(double value, double start, double end) => start + (value - Min) / (Max - Min) * (end - start);

    public static LinearScale Create(IEnumerable<double> values, bool zero = false, double? min = null, double? max = null)
    {
        var data = values.ToArray();
        var lo = data.Length == 0 ? 0 : data.Min();
        var hi = data.Length == 0 ? 1 : data.Max();
        if (zero) { lo = Math.Min(lo, 0); hi = Math.Max(hi, 0); }
        if (lo == hi) { var padding = Math.Max(Math.Abs(lo) * 0.05, 1); lo -= padding; hi += padding; }
        if (min.HasValue) lo = min.Value;
        if (max.HasValue) hi = max.Value;
        if (lo >= hi) throw new ArgumentException("Axis bounds exclude the data extent or have no range.");
        return new(lo, hi);
    }

    public IEnumerable<double> Ticks(int count = 5)
    {
        if (count < 2) throw new ArgumentOutOfRangeException(nameof(count));
        var raw = (Max - Min) / (count - 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / magnitude;
        var step = (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 2.5 ? 2.5 : fraction <= 5 ? 5 : 10) * magnitude;
        var first = Math.Ceiling(Min / step) * step;
        for (var i = 0; i < count + 2; i++)
        {
            var value = first + i * step;
            if (value > Max + step * 1e-10) break;
            yield return Math.Abs(value) < step * 1e-10 ? 0 : value;
        }
    }
    public static string Label(double value) => value == 0 ? "0" :
        Math.Abs(value) is >= 1e6 or < 0.01 ? value.ToString("0.##E+0", CultureInfo.InvariantCulture) :
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
