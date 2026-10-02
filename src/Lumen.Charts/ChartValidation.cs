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
        Style(spec.Style);
        if (spec.YAxis == AxisKind.Time) throw new ArgumentException("Time axes are supported on X only.");
        if (spec.XAxis != AxisKind.Linear && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band))
            throw new ArgumentException("Time and log X axes apply to line, area, scatter, bubble, candlestick, OHLC and band charts; the other kinds index or derive their X values.");
        if (spec.YAxis == AxisKind.Log && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Box or ChartKind.Violin))
            throw new ArgumentException("Log Y axes require line, scatter, bubble, candlestick, OHLC, band, box or violin charts; magnitude, count and radial charts need a zero baseline.");
        if (!Enum.IsDefined(spec.Y2Axis) || spec.Y2Axis == AxisKind.Time) throw new ArgumentException("The secondary axis is numeric or logarithmic; time axes are supported on X only.");
        if (spec.Y2Axis == AxisKind.Log && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band))
            throw new ArgumentException("A logarithmic secondary axis requires line, scatter, bubble or band charts.");
        if (!Enum.IsDefined(spec.XFormat) || !Enum.IsDefined(spec.YFormat) || !Enum.IsDefined(spec.Y2Format)) throw new ArgumentException("Unknown value format.");
        if (spec.XAxis == AxisKind.Time && spec.XFormat != ValueFormat.Number)
            throw new ArgumentException("A time axis writes its own calendar; duration and compact formats apply to linear and log axes.");
        if (spec.XFormat != ValueFormat.Number && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band))
            throw new ArgumentException("An X format applies to line, area, scatter, bubble, candlestick, OHLC and band charts; the other kinds index or derive their X values.");
        if ((spec.YFormat != ValueFormat.Number || spec.Y2Format != ValueFormat.Number) && spec.Kind is ChartKind.Donut or ChartKind.Heatmap or ChartKind.Radar or ChartKind.Histogram)
            throw new ArgumentException("A Y format applies to the values a Y axis measures; donut, heatmap and radar charts have no Y axis, and a histogram's counts observations.");
        if ((spec.YReversed || spec.Y2Reversed) && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Box or ChartKind.Violin))
            throw new ArgumentException("A reversed Y axis applies to line, scatter, bubble, band, candlestick, OHLC, box and violin charts. Column, bar, stacked column, area and histogram charts draw from a zero baseline, which a reversed axis would hang from the top, and donut, heatmap and radar charts have no Y axis.");
        var secondary = spec.Series?.Any(series => series?.Secondary == true) == true;
        if (secondary)
        {
            if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Band))
                throw new ArgumentException("A secondary axis applies to line, area, scatter, bubble, column and band charts; stacked, horizontal, candlestick, OHLC and radial charts cannot measure against two.");
            if (spec.Series!.All(series => series.Secondary))
                throw new ArgumentException("A secondary axis needs at least one series on the left to measure against.");
        }
        Text(spec.Y2Label);
        if (spec.IncludeZero && (spec.XAxis == AxisKind.Log || spec.YAxis == AxisKind.Log)) throw new ArgumentException("Log axes cannot include zero.");
        Text(spec.Title); Text(spec.Description); Text(spec.Source); Text(spec.XLabel); Text(spec.YLabel);
        if (spec.Series is null || spec.Series.Count > 32) throw new ArgumentException("Provide at most 32 series.");
        if (spec.Series.Any(series => series?.Trend == true) && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble))
            throw new ArgumentException("A trend line applies to line, area, scatter and bubble charts; the other kinds place their marks by index or derive their own values.");
        if (spec.MaxRenderedPoints is < 16 or > 5000) throw new ArgumentException("MaxRenderedPoints must be between 16 and 5000.");
        if (spec.Bins is < 1 or > Statistics.MaxBins) throw new ArgumentException($"Bins must be between 1 and {Statistics.MaxBins}.");
        if (spec.Annotations is null || spec.Annotations.Count > 32) throw new ArgumentException("Provide at most 32 annotations.");
        foreach (var annotation in spec.Annotations)
        {
            if (annotation is null) throw new ArgumentException("Annotations cannot be null.");
            if (!Annotated(spec.Kind))
                throw new ArgumentException("Annotations apply to charts drawn on an X and Y axis; donut, radar, heatmap, histogram, box and violin charts do not take them yet.");
            if (!Enum.IsDefined(annotation.Axis)) throw new ArgumentException("Unknown annotation axis.");
            if (!Finite(annotation.From) || (annotation.To.HasValue && !Finite(annotation.To.Value)))
                throw new ArgumentException("Annotation values must be finite, magnitude <= 1e100.");
            if (annotation.To <= annotation.From) throw new ArgumentException("A band annotation needs To above From.");
            if (annotation.Axis == AnnotationAxis.X && spec.Kind is ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn)
                throw new ArgumentException("X annotations need a numeric axis; category charts place their bars by index.");
            var axis = annotation.Axis == AnnotationAxis.X ? spec.XAxis : spec.YAxis;
            if (axis == AxisKind.Log && annotation.From <= 0) throw new ArgumentException("Log axes require positive annotation values.");
            if (annotation.Axis == AnnotationAxis.X && spec.XAxis == AxisKind.Time && !TimeAxis.InRange(annotation.From))
                throw new ArgumentException("Time annotations must be Unix milliseconds between year 1 and year 9999.");
            Text(annotation.Label); Color(annotation.Color);
        }
        var style = ChartSvg.ResolveStyle(spec);
        if (spec.YZones is not null)
        {
            if (!Annotated(spec.Kind))
                throw new ArgumentException("Zone bands apply wherever Y annotations do, on charts drawn on an X and Y axis; donut, radar, heatmap, histogram, box and violin charts refuse them.");
            Zones(spec.YZones, style);
        }
        if (spec.DensityCells is not null)
        {
            if (spec.Kind != ChartKind.Scatter) throw new ArgumentException("Density cells apply to scatter charts; the other kinds either draw one mark per category or already sample.");
            if (spec.DensityCells is < 8 or > 200) throw new ArgumentException("DensityCells must be between 8 and 200.");
        }
        if (spec.Kind is ChartKind.Candlestick or ChartKind.Ohlc && spec.Series.Count > 1)
            throw new ArgumentException("Candlestick and OHLC charts accept one series.");
        if (spec.Kind == ChartKind.Histogram && spec.Series.Count > 4)
            throw new ArgumentException("Histograms accept at most four series; past four, the bars side by side in each bin are too narrow to read.");
        Bounds(spec.XMin, spec.XMax); Bounds(spec.YMin, spec.YMax);
        if (spec.XAxis == AxisKind.Log && (spec.XMin <= 0 || spec.XMax <= 0)) throw new ArgumentException("Log X bounds must be positive.");
        if (spec.YAxis == AxisKind.Log && (spec.YMin <= 0 || spec.YMax <= 0)) throw new ArgumentException("Log Y bounds must be positive.");
        Bounds(spec.Y2Min, spec.Y2Max);
        if (spec.Y2Axis == AxisKind.Log && (spec.Y2Min <= 0 || spec.Y2Max <= 0)) throw new ArgumentException("Log secondary bounds must be positive.");
        if (spec.TimeZone is not null)
        {
            if (spec.XAxis != AxisKind.Time) throw new ArgumentException("A time zone applies to a time X axis.");
            Text(spec.TimeZone);
            TimeAxis.Zone(spec.TimeZone);
        }
        if (spec.TimeSkips is null || spec.TimeSkips.Count > 400) throw new ArgumentException("Provide at most 400 skipped spans; weekends are generated, not listed.");
        if ((spec.SkipWeekends || spec.TimeSkips.Count > 0) && spec.XAxis != AxisKind.Time)
            throw new ArgumentException("Skipped spans apply to a time X axis.");
        foreach (var skip in spec.TimeSkips)
        {
            if (skip is null) throw new ArgumentException("Skipped spans cannot be null.");
            if (!TimeAxis.InRange(skip.From) || !TimeAxis.InRange(skip.To))
                throw new ArgumentException("Skipped spans must be Unix milliseconds between year 1 and year 9999.");
            if (skip.To <= skip.From) throw new ArgumentException("A skipped span needs To above From.");
        }
        if (spec.XAxis == AxisKind.Time && ((spec.XMin.HasValue && !TimeAxis.InRange(spec.XMin.Value)) || (spec.XMax.HasValue && !TimeAxis.InRange(spec.XMax.Value))))
            throw new ArgumentException("Time bounds must be Unix milliseconds between year 1 and year 9999.");
        var count = 0;
        foreach (var series in spec.Series)
        {
            if (series is null || series.Points is null)
                throw new ArgumentException(series?.Summary is null ? "Series and points cannot be null." : "A series with a summary still needs a points list; pass an empty one.");
            if (series.Name is null) throw new ArgumentException("Series names cannot be null.");
            Text(series.Name); Color(series.Color);
            if (series.Summary is not null) Summary(series, spec.Kind, spec.YAxis);
            if (series.Zones is not null)
            {
                if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar))
                    throw new ArgumentException("Series zones apply to line, area, scatter, bubble, column and bar charts; on the other kinds colour already says something else, such as direction, value, a stacked series or a distribution.");
                Zones(series.Zones, style);
            }
            if (spec.DensityCells is not null && (series.Zones is not null || series.Points.Any(p => p?.Color is not null)))
                throw new ArgumentException("A density scatter shades cells rather than points, so it takes neither zones nor point colours.");
            // A summary's outliers are drawn one mark each, so they count towards the limit like points.
            count += series.Points.Count + (series.Summary?.Outliers.Count ?? 0);
            if (count > MaxPoints) throw new ArgumentException($"At most {MaxPoints} points are supported per chart.");
            foreach (var p in series.Points)
            {
                if (p is null || !Finite(p.X) || (p.Y.HasValue && !Finite(p.Y.Value)) || !Finite(p.Size) || p.Size < 0)
                    throw new ArgumentException("Coordinates must be finite, magnitude <= 1e100; bubble sizes must be nonnegative.");
                Text(p.Label); Color(p.Color);
                if (p.Color is not null && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar or ChartKind.Donut))
                    throw new ArgumentException("Point colours apply to line, area, scatter, bubble, column, bar and donut charts; on the other kinds colour already says something else: direction on candlesticks and OHLC bars, value on a heatmap, and the series or distribution a mark belongs to on stacked column, radar, band, histogram, box and violin charts.");
                if (spec.XAxis == AxisKind.Log && p.X <= 0) throw new ArgumentException("Log X axes require positive X values.");
                if ((series.Secondary ? spec.Y2Axis : spec.YAxis) == AxisKind.Log && p.Y.HasValue && p.Y.Value <= 0)
                    throw new ArgumentException("Log Y axes require positive values; use a linear axis for zero or negative data.");
                if (spec.XAxis == AxisKind.Time && !TimeAxis.InRange(p.X)) throw new ArgumentException("Time X values must be Unix milliseconds between year 1 and year 9999.");
                if (spec.Kind is ChartKind.Donut or ChartKind.Radar && p.Y < 0)
                    throw new ArgumentException("Donut and radar charts require nonnegative values.");
                if (spec.Kind is ChartKind.Candlestick or ChartKind.Ohlc) Candle(p, spec.YAxis);
                if (spec.Kind == ChartKind.Band) Interval(p, spec.YAxis);
            }
            if (spec.Kind is ChartKind.Line or ChartKind.Area or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band && series.Points.Zip(series.Points.Skip(1)).Any(p => p.First.X > p.Second.X))
                throw new ArgumentException("Line, area, candlestick, OHLC and band points must be ordered by X.");
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
            if (spec.YMin > 0 || spec.YMax < 0 || spec.Y2Min > 0 || spec.Y2Max < 0) throw new ArgumentException("Magnitude charts require a zero baseline.");
    }

    private static void Candle(ChartPoint p, AxisKind axis)
    {
        if (p.Open is not { } open || p.High is not { } high || p.Low is not { } low || p.Close is not { } close)
            throw new ArgumentException("Candlestick and OHLC points require Open, High, Low and Close values.");
        if (!Finite(open) || !Finite(high) || !Finite(low) || !Finite(close))
            throw new ArgumentException("Candlestick and OHLC prices must be finite.");
        if (high < Math.Max(open, close) || low > Math.Min(open, close))
            throw new ArgumentException("Candlestick and OHLC High must be the highest price and Low the lowest.");
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

    private static void Summary(ChartSeries series, ChartKind kind, AxisKind axis)
    {
        var s = series.Summary!;
        if (kind != ChartKind.Box)
            throw new ArgumentException("A precomputed summary applies to box charts only; a violin cannot estimate a density from five numbers, and the other kinds draw observations.");
        if (series.Points.Count > 0) throw new ArgumentException("A series with a summary must have no points, because the two could disagree.");
        if (s.Outliers is null) throw new ArgumentException("Summary outliers cannot be null; use an empty list.");
        if (!Finite(s.LowerWhisker) || !Finite(s.Q1) || !Finite(s.Median) || !Finite(s.Q3) || !Finite(s.UpperWhisker) || !s.Outliers.All(Finite))
            throw new ArgumentException("Summary values must be finite, magnitude <= 1e100.");
        if (s.LowerWhisker > s.Q1 || s.Q1 > s.Median || s.Median > s.Q3 || s.Q3 > s.UpperWhisker)
            throw new ArgumentException("Summary values must satisfy LowerWhisker <= Q1 <= Median <= Q3 <= UpperWhisker.");
        if (axis == AxisKind.Log && (s.LowerWhisker <= 0 || s.Outliers.Any(v => v <= 0)))
            throw new ArgumentException("Log Y axes require positive summary values; use a linear axis for zero or negative data.");
    }

    internal static void Style(ChartStyle? style)
    {
        if (style is null) return;
        foreach (var color in new[] { style.Background, style.Text, style.Muted, style.Grid, style.Edge, style.Rising, style.Falling, style.HeatmapLow, style.HeatmapHigh })
        {
            if (color is null) throw new ArgumentException("Style colours cannot be null.");
            Color(color);
        }
        if (style.Series is null || style.Series.Count is 0 or > 32) throw new ArgumentException("A style needs between 1 and 32 series colours.");
        if (style.Zones is null || style.Zones.Count is 0 or > 32) throw new ArgumentException("A style needs between 1 and 32 zone colours.");
        foreach (var color in style.Series.Concat(style.Zones))
        {
            if (color is null) throw new ArgumentException("Style colours cannot be null.");
            Color(color);
        }
        // The font list is written into a style attribute, so anything beyond a plain family list is refused.
        if (style.FontFamily is null || !FontFamily().IsMatch(style.FontFamily))
            throw new ArgumentException("Font families may contain letters, digits, spaces, commas and hyphens, up to 200 characters.");
    }

    private static bool Annotated(ChartKind kind) => kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble
        or ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band;

    /// <summary>A zone without its own colour takes the style's ramp at its position, so a scale longer than the ramp
    /// must colour the zones past it; the alternative, wrapping round, would give two zones one colour.</summary>
    private static void Zones(ZoneScale scale, ChartStyle style)
    {
        if (scale.Zones.Count > 32) throw new ArgumentException("A zone scale drawn on a chart has at most 32 zones.");
        for (var i = 0; i < scale.Zones.Count; i++)
        {
            Text(scale.Zones[i].Name); Color(scale.Zones[i].Color);
            if (scale.Zones[i].Color is null && i >= style.Zones.Count)
                throw new ArgumentException($"The style's zone ramp has {style.Zones.Count} colours, so zones past that need colours of their own.");
        }
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
    [GeneratedRegex("^[A-Za-z0-9 ,\\-]{1,200}$")]
    private static partial Regex FontFamily();
}
