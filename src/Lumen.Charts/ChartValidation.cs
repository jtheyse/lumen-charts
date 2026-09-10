using System.Text.RegularExpressions;

namespace Lumen.Charts;

public static partial class ChartValidation
{
    public const int MaxPoints = 100_000;
    public static void Validate(ChartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        Dimensions(spec.Width, spec.Height);
        if (!Enum.IsDefined(spec.Kind) || !Enum.IsDefined(spec.Theme)) throw new ArgumentException("Unknown chart kind or theme.");
        if (!Enum.IsDefined(spec.XAxis) || !Enum.IsDefined(spec.YAxis)) throw new ArgumentException("Unknown axis kind.");
        if (spec.YAxis == AxisKind.Time) throw new ArgumentException("Time axes are supported on X only.");
        if (spec.XAxis != AxisKind.Linear && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Band))
            throw new ArgumentException("Time and log X axes apply to line, area, scatter, bubble, candlestick and band charts; the other kinds index or derive their X values.");
        if (spec.YAxis == AxisKind.Log && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Band or ChartKind.Box))
            throw new ArgumentException("Log Y axes require line, scatter, bubble, candlestick, band or box charts; magnitude, count and radial charts need a zero baseline.");
        if (spec.IncludeZero && (spec.XAxis == AxisKind.Log || spec.YAxis == AxisKind.Log)) throw new ArgumentException("Log axes cannot include zero.");
        Text(spec.Title); Text(spec.Description); Text(spec.Source); Text(spec.XLabel); Text(spec.YLabel);
        if (spec.Series is null || spec.Series.Count > 32) throw new ArgumentException("Provide at most 32 series.");
        if (spec.MaxRenderedPoints is < 16 or > 5000) throw new ArgumentException("MaxRenderedPoints must be between 16 and 5000.");
        if (spec.Bins is < 1 or > Statistics.MaxBins) throw new ArgumentException($"Bins must be between 1 and {Statistics.MaxBins}.");
        if (spec.Kind is ChartKind.Candlestick or ChartKind.Histogram && spec.Series.Count > 1)
            throw new ArgumentException("Candlestick and histogram charts accept one series.");
        Bounds(spec.XMin, spec.XMax); Bounds(spec.YMin, spec.YMax);
        if (spec.XAxis == AxisKind.Log && (spec.XMin <= 0 || spec.XMax <= 0)) throw new ArgumentException("Log X bounds must be positive.");
        if (spec.YAxis == AxisKind.Log && (spec.YMin <= 0 || spec.YMax <= 0)) throw new ArgumentException("Log Y bounds must be positive.");
        if (spec.XAxis == AxisKind.Time && ((spec.XMin.HasValue && !TimeAxis.InRange(spec.XMin.Value)) || (spec.XMax.HasValue && !TimeAxis.InRange(spec.XMax.Value))))
            throw new ArgumentException("Time bounds must be Unix milliseconds between year 1 and year 9999.");
        var count = 0;
        foreach (var series in spec.Series)
        {
            if (series is null || series.Points is null) throw new ArgumentException("Series and points cannot be null.");
            if (series.Name is null) throw new ArgumentException("Series names cannot be null.");
            Text(series.Name); Color(series.Color);
            count += series.Points.Count;
            if (count > MaxPoints) throw new ArgumentException($"At most {MaxPoints} points are supported per chart.");
            foreach (var p in series.Points)
            {
                if (p is null || !Finite(p.X) || (p.Y.HasValue && !Finite(p.Y.Value)) || !Finite(p.Size) || p.Size < 0)
                    throw new ArgumentException("Coordinates must be finite, magnitude <= 1e100; bubble sizes must be nonnegative.");
                Text(p.Label);
                if (spec.XAxis == AxisKind.Log && p.X <= 0) throw new ArgumentException("Log X axes require positive X values.");
                if (spec.YAxis == AxisKind.Log && p.Y.HasValue && p.Y.Value <= 0) throw new ArgumentException("Log Y axes require positive values; use a linear axis for zero or negative data.");
                if (spec.XAxis == AxisKind.Time && !TimeAxis.InRange(p.X)) throw new ArgumentException("Time X values must be Unix milliseconds between year 1 and year 9999.");
                if (spec.Kind is ChartKind.Donut or ChartKind.Radar && p.Y < 0)
                    throw new ArgumentException("Donut and radar charts require nonnegative values.");
                if (spec.Kind == ChartKind.Candlestick) Candle(p, spec.YAxis);
                if (spec.Kind == ChartKind.Band) Interval(p, spec.YAxis);
            }
            if (spec.Kind is ChartKind.Line or ChartKind.Area or ChartKind.Candlestick or ChartKind.Band && series.Points.Zip(series.Points.Skip(1)).Any(p => p.First.X > p.Second.X))
                throw new ArgumentException("Line, area, candlestick and band points must be ordered by X.");
        }
        if (spec.Kind == ChartKind.Donut && spec.Series.Count > 1) throw new ArgumentException("Donut charts accept one series.");
        if (spec.Kind is ChartKind.Bar or ChartKind.Column or ChartKind.StackedColumn or ChartKind.Heatmap or ChartKind.Radar)
        {
            foreach (var s in spec.Series)
                if (s.Points.Select(p => p.X).Distinct().Count() != s.Points.Count) throw new ArgumentException("Category X values must be unique within each series.");
            if (spec.Series.SelectMany(s => s.Points).Select(p => p.X).Distinct().Count() > 100)
                throw new ArgumentException("Category charts support at most 100 categories; aggregate first.");
        }
        if (spec.Kind == ChartKind.Donut && count > 100) throw new ArgumentException("Donut charts support at most 100 slices.");
        if (spec.Kind is ChartKind.Bar or ChartKind.Column or ChartKind.StackedColumn or ChartKind.Area or ChartKind.Histogram)
            if (spec.YMin > 0 || spec.YMax < 0) throw new ArgumentException("Magnitude charts require a zero baseline.");
    }

    private static void Candle(ChartPoint p, AxisKind axis)
    {
        if (p.Open is not { } open || p.High is not { } high || p.Low is not { } low || p.Close is not { } close)
            throw new ArgumentException("Candlestick points require Open, High, Low and Close values.");
        if (!Finite(open) || !Finite(high) || !Finite(low) || !Finite(close))
            throw new ArgumentException("Candlestick prices must be finite.");
        if (high < Math.Max(open, close) || low > Math.Min(open, close))
            throw new ArgumentException("Candlestick High must be the highest price and Low the lowest.");
        if (axis == AxisKind.Log && low <= 0) throw new ArgumentException("Log Y axes require positive prices.");
    }
    private static void Interval(ChartPoint p, AxisKind axis)
    {
        if (p.Low is null && p.High is null) return;
        if (p.Low is not { } low || p.High is not { } high)
            throw new ArgumentException("Band points need both Low and High, or neither.");
        if (!Finite(low) || !Finite(high) || low > high)
            throw new ArgumentException("Band bounds must be finite with Low no greater than High.");
        if (axis == AxisKind.Log && low <= 0) throw new ArgumentException("Log Y axes require positive band bounds.");
    }

    internal static bool Finite(double n) => double.IsFinite(n) && Math.Abs(n) <= 1e100;
    internal static void Dimensions(int width, int height)
    {
        if (width is < 320 or > 4096 || height is < 240 or > 2160) throw new ArgumentException("Dimensions must be 320–4096 by 240–2160.");
    }
    internal static void Text(string? text)
    {
        if (text?.Length > 2000) throw new ArgumentException("Labels must be at most 2000 characters.");
        if (text?.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t') == true)
            throw new ArgumentException("Labels cannot contain control characters.");
    }
    internal static void Color(string? color)
    {
        if (color is not null && !HexColor().IsMatch(color)) throw new ArgumentException("Colors must be #RRGGBB hex values.");
    }
    private static void Bounds(double? min, double? max)
    {
        if ((min.HasValue && !Finite(min.Value)) || (max.HasValue && !Finite(max.Value)) || min >= max)
            throw new ArgumentException("Axis bounds must be finite and minimum must be below maximum.");
    }
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
