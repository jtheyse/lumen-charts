using System.Text.RegularExpressions;

namespace Lumen.Charts;

/// <summary>The rules a chart must meet before it is drawn or exported. <see cref="ChartSvg.Render"/> and
/// <see cref="ChartExport.Csv"/> check them first.</summary>
public static partial class ChartValidation
{
    /// <summary>The most points a chart takes, across all its series.</summary>
    public const int MaxPoints = 100_000;
    /// <summary>Checks a spec without drawing it, and throws <see cref="ArgumentException"/> with a plain message naming the
    /// first rule it breaks.</summary>
    public static void Validate(ChartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        Dimensions(spec.Width, spec.Height);
        if (!Enum.IsDefined(spec.Kind) || !Enum.IsDefined(spec.Theme)) throw new ArgumentException("Unknown chart kind or theme.");
        if (!Enum.IsDefined(spec.XAxis) || !Enum.IsDefined(spec.YAxis)) throw new ArgumentException("Unknown axis kind.");
        Style(spec.Style);
        if (spec.YAxis == AxisKind.Time) throw new ArgumentException("Time axes are supported on X only.");
        if (spec.Kind == ChartKind.Timeline) Timeline(spec);
        if (spec.XAxis != AxisKind.Linear && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Timeline))
            throw new ArgumentException("Time and log X axes apply to line, area, scatter, bubble, candlestick, OHLC, band and range charts, and a time axis to timelines; the other kinds index or derive their X values.");
        if (spec.YAxis == AxisKind.Log && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Box or ChartKind.Violin))
            throw new ArgumentException("Log Y axes require line, scatter, bubble, candlestick, OHLC, band, range, box or violin charts; magnitude, count and radial charts need a zero baseline.");
        if (!Enum.IsDefined(spec.Y2Axis) || spec.Y2Axis == AxisKind.Time) throw new ArgumentException("The secondary axis is numeric or logarithmic; time axes are supported on X only.");
        if (spec.Y2Axis == AxisKind.Log && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range))
            throw new ArgumentException("A logarithmic secondary axis requires line, scatter, bubble, band or range charts.");
        if (!Enum.IsDefined(spec.XFormat) || !Enum.IsDefined(spec.YFormat) || !Enum.IsDefined(spec.Y2Format)) throw new ArgumentException("Unknown value format.");
        if (spec.XFormat == ValueFormat.TimeOfDay && spec.XAxis != AxisKind.Linear || spec.YFormat == ValueFormat.TimeOfDay && spec.YAxis != AxisKind.Linear
            || spec.Y2Format == ValueFormat.TimeOfDay && spec.Y2Axis != AxisKind.Linear)
            throw new ArgumentException(TimeOfDayAxes);
        if (spec.XAxis == AxisKind.Time && spec.XFormat != ValueFormat.Number)
            throw new ArgumentException("A time axis writes its own calendar; duration and compact formats apply to linear and log axes.");
        if (spec.XFormat != ValueFormat.Number && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Timeline))
            throw new ArgumentException("An X format applies to line, area, scatter, bubble, candlestick, OHLC, band, range and timeline charts; the other kinds index or derive their X values.");
        if ((spec.YFormat != ValueFormat.Number || spec.Y2Format != ValueFormat.Number) && spec.Kind is ChartKind.Donut or ChartKind.Heatmap or ChartKind.Radar or ChartKind.Histogram)
            throw new ArgumentException("A Y format applies to the values a Y axis measures; donut, heatmap and radar charts have no Y axis, and a histogram's counts observations.");
        if ((spec.YReversed || spec.Y2Reversed) && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Box or ChartKind.Violin))
            throw new ArgumentException(spec.Kind is ChartKind.Gauge or ChartKind.Ring
                ? "A gauge or ring has no Y axis to reverse: its scale runs clockwise round its arc."
                : "A reversed Y axis applies to line, scatter, bubble, band, range, candlestick, OHLC, box and violin charts. Column, bar, stacked column, area and histogram charts draw from a zero baseline, which a reversed axis would hang from the top, and donut, heatmap and radar charts have no Y axis.");
        var secondary = spec.Series?.Any(series => series?.Secondary == true) == true;
        if (!Enum.IsDefined(spec.YAxisSide) || !Enum.IsDefined(spec.YTickLabels)) throw new ArgumentException("Unknown Y axis side or tick labelling.");
        if (spec.YAxisSide == AxisSide.Right)
        {
            if (spec.Kind is ChartKind.Bar or ChartKind.Donut or ChartKind.Heatmap or ChartKind.Radar or ChartKind.Gauge or ChartKind.Ring)
                throw new ArgumentException("The Y axis moves to the right on charts that draw it up the side; a horizontal bar chart draws its value axis along the bottom, and donut, heatmap, radar, gauge and ring charts have none.");
            if (secondary) throw new ArgumentException("A secondary series measures against the right-hand edge, so a chart with one keeps its Y axis on the left.");
        }
        if (spec.YTickLabels != TickLabels.All && spec.Kind is ChartKind.Donut or ChartKind.Heatmap or ChartKind.Radar or ChartKind.Gauge or ChartKind.Ring)
            throw new ArgumentException("Tick labels apply to a Y axis, which donut, heatmap, radar, gauge and ring charts do not have.");
        if (secondary)
        {
            if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Band or ChartKind.Range))
                throw new ArgumentException("A secondary axis applies to line, area, scatter, bubble, column, band and range charts; stacked, horizontal, candlestick, OHLC and radial charts cannot measure against two.");
            if (spec.Series!.Where(series => series is not null).GroupBy(series => series.Pane).Any(pane => pane.All(series => series.Secondary)))
                throw new ArgumentException("A secondary axis needs at least one series on the left of its pane to measure against.");
        }
        if (spec.Panes is null || spec.Panes.Count > 3) throw new ArgumentException("A chart has at most four panes: the main plot and three more in Panes.");
        if ((spec.Panes.Count > 0 || spec.Series?.Any(series => series is not null && series.Pane != 0) == true)
            && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Candlestick or ChartKind.Ohlc))
            throw new ArgumentException("Panes share one continuous X axis, so they apply to line, area, scatter, bubble, band, range, candlestick and OHLC charts; column, bar and stacked column charts place their bars by category, and the other kinds derive their X or have none.");
        Text(spec.Y2Label);
        if (spec.IncludeZero && (spec.XAxis == AxisKind.Log || spec.YAxis == AxisKind.Log)) throw new ArgumentException("Log axes cannot include zero.");
        Text(spec.Title); Text(spec.Description); Text(spec.Source); Text(spec.XLabel); Text(spec.YLabel);
        if (spec.Series is null || spec.Series.Count > 32) throw new ArgumentException("Provide at most 32 series.");
        if (spec.MaxRenderedPoints is < 16 or > 5000) throw new ArgumentException("MaxRenderedPoints must be between 16 and 5000.");
        if (spec.Bins is < 1 or > Statistics.MaxBins) throw new ArgumentException($"Bins must be between 1 and {Statistics.MaxBins}.");
        if (spec.Kind == ChartKind.Gauge)
        {
            if (!(spec.GaugeSweep is >= 180 and <= 360))
                throw new ArgumentException("A gauge's sweep is between 180 degrees, a semicircle, and 360, a full circle.");
            if (!((spec.YMin ?? 0) < (spec.YMax ?? 100)))
                throw new ArgumentException("A gauge's scale runs from YMin to YMax, 0 to 100 unless set, so YMin must be below YMax.");
        }
        else if (spec.GaugeSweep != 270)
            throw new ArgumentException("GaugeSweep sets how far round a gauge's arc runs, so it applies to gauge charts only.");
        if (!spec.TimelineConnectors && spec.Kind != ChartKind.Timeline)
            throw new ArgumentException("TimelineConnectors joins the spans of a timeline's lanes, so only a timeline can turn it off.");
        if (spec.Annotations is null || spec.Annotations.Count > 32) throw new ArgumentException("Provide at most 32 annotations.");
        foreach (var annotation in spec.Annotations)
        {
            if (annotation is null) throw new ArgumentException("Annotations cannot be null.");
            if (spec.Kind == ChartKind.Ring)
                throw new ArgumentException("Ring charts take no annotations: each ring's goal is its target.");
            if (!Annotated(spec.Kind) && spec.Kind != ChartKind.Gauge)
                throw new ArgumentException("Annotations apply to charts drawn on an X and Y axis, and to a gauge's arc; donut, radar, heatmap, histogram, box and violin charts do not take them yet.");
            if (!Enum.IsDefined(annotation.Axis)) throw new ArgumentException("Unknown annotation axis.");
            if (!Finite(annotation.From) || (annotation.To.HasValue && !Finite(annotation.To.Value)))
                throw new ArgumentException("Annotation values must be finite, magnitude <= 1e100.");
            if (annotation.To <= annotation.From) throw new ArgumentException("A band annotation needs To above From.");
            if (spec.Kind == ChartKind.Gauge)
            {
                if (annotation.Axis == AnnotationAxis.X) throw new ArgumentException("A gauge has no X axis: it takes Y annotations, drawn as ticks across its arc.");
                if (annotation.To is not null) throw new ArgumentException("A gauge marks a Y annotation as a tick across its arc, so it takes lines, not bands; YZones shade its ranges.");
                if (annotation.From < (spec.YMin ?? 0) || annotation.From > (spec.YMax ?? 100))
                    throw new ArgumentException("A gauge's annotation must lie on its scale, from YMin to YMax, 0 to 100 unless set.");
            }
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
            if (spec.Kind == ChartKind.Ring)
                throw new ArgumentException("Ring charts take no zones: each ring is drawn in its series' colour.");
            if (!Annotated(spec.Kind) && spec.Kind != ChartKind.Gauge)
                throw new ArgumentException("Zone bands apply wherever Y annotations do, on charts drawn on an X and Y axis and on a gauge's track; donut, radar, heatmap, histogram, box and violin charts refuse them.");
            Zones(spec.YZones, style);
        }
        foreach (var pane in spec.Panes) Pane(pane, spec, style);
        if (spec.DensityCells is not null)
        {
            if (spec.Kind != ChartKind.Scatter) throw new ArgumentException("Density cells apply to scatter charts; the other kinds either draw one mark per category or already sample.");
            if (spec.DensityCells is < 8 or > 200) throw new ArgumentException("DensityCells must be between 8 and 200.");
        }
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
            if (series.Pane < 0 || series.Pane > spec.Panes.Count)
                throw new ArgumentException("A series' pane is 0, the main plot, or one that Panes sets up: pane k is Panes[k - 1].");
            // Each series is measured on the axes of its own pane.
            var pane = ChartSvg.Pane(spec, series.Pane);
            var measure = series.Secondary ? pane.Y2Axis : pane.YAxis;
            if (series.Kind is { } own)
            {
                if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Column or ChartKind.Candlestick or ChartKind.Ohlc))
                    throw new ArgumentException("A series' own kind applies to line, area, scatter, bubble, band, range, column, candlestick and OHLC charts; horizontal bar, stacked column, donut, heatmap, radar, histogram, box and violin charts draw every series one way.");
                if (own is not (ChartKind.Line or ChartKind.Area or ChartKind.Column or ChartKind.Scatter or ChartKind.Band or ChartKind.Range))
                    throw new ArgumentException("A series can be drawn as a line, area, column, scatter, band or range; bubbles share one size scale across a chart, and the other kinds lay out a whole chart rather than one series.");
            }
            // Everything a series carries is checked against the mark it draws, which is the chart's kind unless it names its own.
            var mark = ChartSvg.Mark(spec, series);
            if (series.Trend && mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble))
                throw new ArgumentException("A trend line applies to series drawn as lines, areas, scatter points or bubbles; the other kinds place their marks by index or derive their own values.");
            if (series.Summary is not null) Summary(series, spec.Kind, spec.YAxis);
            Finish(series, mark, measure);
            if (series.Zones is not null)
            {
                if (mark == ChartKind.Range)
                    throw new ArgumentException("A range bar runs from its low to its high, which can lie in several zones, so a range series takes no zones; colour a bar with its point's Color instead.");
                if (mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar))
                    throw new ArgumentException("Series zones apply to series drawn as lines, areas, scatter points, bubbles, columns and bars; on the other kinds colour already says something else, such as direction, value, a stacked series or a distribution.");
                Zones(series.Zones, style);
            }
            if (spec.DensityCells is not null && mark == ChartKind.Scatter && (series.Zones is not null || series.Points.Any(p => p?.Color is not null)))
                throw new ArgumentException("A density scatter shades cells rather than points, so it takes neither zones nor point colours.");
            if (series.ProjectedFrom is { } from)
            {
                if (mark is not (ChartKind.Line or ChartKind.Area))
                    throw new ArgumentException("A projection dashes a stroke, so it applies to series drawn as lines or areas.");
                if (!Finite(from)) throw new ArgumentException("A projection must start at a finite X, magnitude <= 1e100.");
                if (spec.XAxis == AxisKind.Log && from <= 0) throw new ArgumentException("Log X axes require a positive projection start.");
                if (spec.XAxis == AxisKind.Time && !TimeAxis.InRange(from))
                    throw new ArgumentException("A projection on a time axis must start at Unix milliseconds between year 1 and year 9999.");
            }
            // A summary's outliers are drawn one mark each, so they count towards the limit like points.
            count += series.Points.Count + (series.Summary?.Outliers.Count ?? 0);
            if (count > MaxPoints) throw new ArgumentException($"At most {MaxPoints} points are supported per chart.");
            foreach (var p in series.Points)
            {
                if (p is null || !Finite(p.X) || (p.Y.HasValue && !Finite(p.Y.Value)) || !Finite(p.Size) || p.Size < 0)
                    throw new ArgumentException("Coordinates must be finite, magnitude <= 1e100; bubble sizes must be nonnegative.");
                Text(p.Label); Color(p.Color);
                if (p.Color is not null && mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar or ChartKind.Range or ChartKind.Donut))
                    throw new ArgumentException("Point colours apply to series drawn as lines, areas, scatter points, bubbles, columns, bars, ranges and donut slices; on the other kinds colour already says something else: direction on candlesticks and OHLC bars, value on a heatmap, the state a timeline's lane stands for, and the series or distribution a mark belongs to on stacked column, radar, band, histogram, box and violin charts.");
                if (p.XEnd is { } end)
                {
                    if (spec.Kind != ChartKind.Timeline) throw new ArgumentException("XEnd ends a span, so it applies to timeline charts only.");
                    if (!Finite(end) || end <= p.X) throw new ArgumentException("A timeline span ends after it starts: its XEnd must be finite and above its X.");
                    if (spec.XAxis == AxisKind.Time && !TimeAxis.InRange(end)) throw new ArgumentException("Time X values must be Unix milliseconds between year 1 and year 9999.");
                }
                else if (spec.Kind == ChartKind.Timeline) throw new ArgumentException("Every point of a timeline is a span with an XEnd above its X; make one with ChartPoint.Span.");
                if (spec.XAxis == AxisKind.Log && p.X <= 0) throw new ArgumentException("Log X axes require positive X values.");
                if (measure == AxisKind.Log && p.Y.HasValue && p.Y.Value <= 0)
                    throw new ArgumentException("Log Y axes require positive values; use a linear axis for zero or negative data.");
                if (spec.XAxis == AxisKind.Time && !TimeAxis.InRange(p.X)) throw new ArgumentException("Time X values must be Unix milliseconds between year 1 and year 9999.");
                if (spec.Kind is ChartKind.Donut or ChartKind.Radar && p.Y < 0)
                    throw new ArgumentException("Donut and radar charts require nonnegative values.");
                if (mark is ChartKind.Candlestick or ChartKind.Ohlc) Candle(p, measure);
                if (mark == ChartKind.Band) Interval(p, measure);
                if (mark == ChartKind.Range) Range(p, measure);
            }
            if (spec.Kind == ChartKind.Timeline)
            {
                var spans = series.Points.OrderBy(p => p.X).ToArray();
                for (var i = 1; i < spans.Length; i++)
                    if (spans[i].X < spans[i - 1].XEnd)
                        throw new ArgumentException($"Spans in one lane cannot overlap, and two in {series.Name} do; spans in different lanes may.");
            }
            if (mark is ChartKind.Line or ChartKind.Area or ChartKind.Band or ChartKind.Candlestick or ChartKind.Ohlc && series.Points.Zip(series.Points.Skip(1)).Any(p => p.First.X > p.Second.X))
                throw new ArgumentException("Line, area, candlestick, OHLC and band points must be ordered by X.");
            // Two columns at one X would stand in one place; a category chart already refuses that for every series.
            if (mark == ChartKind.Column && spec.Kind != ChartKind.Column && series.Points.Select(p => p.X).Distinct().Count() != series.Points.Count)
                throw new ArgumentException("Columns on a continuous axis need unique X values within each series.");
            if (mark == ChartKind.Range && spec.Kind != ChartKind.Column && series.Points.Select(p => p.X).Distinct().Count() != series.Points.Count)
                throw new ArgumentException("Range bars on a continuous axis need unique X values within each series.");
            if (series.Goal is { } goal)
            {
                if (spec.Kind != ChartKind.Ring) throw new ArgumentException("A goal is what a ring's progress is measured against, so it applies to ring charts only.");
                if (!Finite(goal) || goal <= 0) throw new ArgumentException("A ring's goal must be positive and finite, magnitude <= 1e100.");
            }
        }
        Radial(spec);
        if (spec.Kind is ChartKind.Candlestick or ChartKind.Ohlc && spec.Series.Count > 0 && spec.Series.Count(series => series.Kind is null) != 1)
            throw new ArgumentException("Candlestick and OHLC charts draw exactly one series as candles or bars, the one that names no kind; any others name their own kind, such as a line for a moving average or columns for volume.");
        if (spec.Panes.Count > 0)
            for (var pane = 0; pane <= spec.Panes.Count; pane++)
                if (!spec.Series.Any(series => series.Pane == pane)) throw new ArgumentException($"Every pane needs a series, and pane {pane} has none.");
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
        // The rules above follow the chart's kind; a series drawn as columns or an area on another kind, or in another pane,
        // meets them on its own axis.
        foreach (var series in spec.Series.Where(series => ChartSvg.Mark(spec, series) is ChartKind.Column or ChartKind.Area))
        {
            var pane = ChartSvg.Pane(spec, series.Pane);
            var (axis, reversed, min, max) = series.Secondary ? (pane.Y2Axis, pane.Y2Reversed, pane.Y2Min, pane.Y2Max) : (pane.YAxis, pane.YReversed, pane.YMin, pane.YMax);
            if (axis == AxisKind.Log) throw new ArgumentException("Column and area series draw from a zero baseline, which a logarithmic axis cannot show; measure them against a linear one.");
            if (reversed) throw new ArgumentException("Column and area series draw from a zero baseline, which a reversed axis would hang from the top.");
            if (min > 0 || max < 0) throw new ArgumentException("Column and area series draw from a zero baseline, so the bounds of their axis must include zero.");
        }
    }

    /// <summary>A gauge draws one score and a ring chart one value a ring, so each series carries exactly one point. A chart
    /// with no series is left to draw its empty state, as every kind does, and as the component's legend leaves it when every
    /// series is hidden.</summary>
    private static void Radial(ChartSpec spec)
    {
        if (spec.Kind == ChartKind.Gauge && spec.Series.Count > 0)
        {
            if (spec.Series.Count > 1) throw new ArgumentException("A gauge shows one score, so it takes one series; draw several scores as several gauges.");
            var series = spec.Series[0];
            if (series.Points.Count != 1 || series.Points[0].Y is null)
                throw new ArgumentException("A gauge's series has exactly one point, whose Y is the score, and the score cannot be missing.");
            if (series.Gradient is not null && spec.YZones is not null)
                throw new ArgumentException("A gauge takes its colour from YZones or from a gradient, not both.");
        }
        if (spec.Kind != ChartKind.Ring) return;
        if (spec.Series.Count > 6) throw new ArgumentException("Ring charts take one to six rings, one series each; past six the rings are too thin to read.");
        foreach (var series in spec.Series)
        {
            if (series.Points.Count != 1 || series.Points[0].Y is not { } value)
                throw new ArgumentException("Each ring's series has exactly one point, whose Y is its value, and the value cannot be missing.");
            if (value < 0) throw new ArgumentException("A ring's value cannot be negative: its progress is the value over its goal.");
        }
    }

    /// <summary>A pane's settings meet the rules the spec's own Y properties meet for the main plot, on the kinds that take panes.</summary>
    private static void Pane(ChartPane? pane, ChartSpec spec, ChartStyle style)
    {
        if (pane is null) throw new ArgumentException("Panes cannot be null.");
        if (!Finite(pane.Weight) || pane.Weight <= 0)
            throw new ArgumentException("A pane's weight is its height beside the main plot's 1, so it must be positive and finite, magnitude <= 1e100.");
        Text(pane.Label); Text(pane.Y2Label);
        if (!Enum.IsDefined(pane.YAxis) || !Enum.IsDefined(pane.Y2Axis) || pane.YAxis == AxisKind.Time || pane.Y2Axis == AxisKind.Time)
            throw new ArgumentException("A pane's axes are numeric or logarithmic; time axes are supported on X only.");
        if (pane.YAxis == AxisKind.Log && spec.Kind == ChartKind.Area)
            throw new ArgumentException("Log Y axes require line, scatter, bubble, candlestick, OHLC, band, box or violin charts; magnitude, count and radial charts need a zero baseline.");
        if (pane.Y2Axis == AxisKind.Log && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range))
            throw new ArgumentException("A logarithmic secondary axis requires line, scatter, bubble, band or range charts.");
        if (!Enum.IsDefined(pane.YFormat) || !Enum.IsDefined(pane.Y2Format)) throw new ArgumentException("Unknown value format.");
        if (pane.YFormat == ValueFormat.TimeOfDay && pane.YAxis != AxisKind.Linear || pane.Y2Format == ValueFormat.TimeOfDay && pane.Y2Axis != AxisKind.Linear)
            throw new ArgumentException(TimeOfDayAxes);
        if ((pane.YReversed || pane.Y2Reversed) && spec.Kind == ChartKind.Area)
            throw new ArgumentException("A reversed Y axis applies to line, scatter, bubble, band, candlestick, OHLC, box and violin charts. Column, bar, stacked column, area and histogram charts draw from a zero baseline, which a reversed axis would hang from the top, and donut, heatmap and radar charts have no Y axis.");
        if (spec.IncludeZero && pane.YAxis == AxisKind.Log) throw new ArgumentException("Log axes cannot include zero.");
        Bounds(pane.YMin, pane.YMax); Bounds(pane.Y2Min, pane.Y2Max);
        if (pane.YAxis == AxisKind.Log && (pane.YMin <= 0 || pane.YMax <= 0)) throw new ArgumentException("Log Y bounds must be positive.");
        if (pane.Y2Axis == AxisKind.Log && (pane.Y2Min <= 0 || pane.Y2Max <= 0)) throw new ArgumentException("Log secondary bounds must be positive.");
        if (spec.Kind == ChartKind.Area && (pane.YMin > 0 || pane.YMax < 0 || pane.Y2Min > 0 || pane.Y2Max < 0)) throw new ArgumentException("Magnitude charts require a zero baseline.");
        if (pane.YZones is not null) Zones(pane.YZones, style);
    }

    /// <summary>Each finishing touch applies to the marks that can show it.</summary>
    private static void Finish(ChartSeries series, ChartKind mark, AxisKind axis)
    {
        if (!Enum.IsDefined(series.Curve) || !Enum.IsDefined(series.Fill) || !Enum.IsDefined(series.Markers))
            throw new ArgumentException("Unknown curve, fill or marker style.");
        if (series.StrokeWidth is { } width)
        {
            if (mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Band))
                throw new ArgumentException("A stroke width applies to series drawn as lines, areas or bands.");
            if (!(width is >= .5 and <= 12)) throw new ArgumentException("A stroke width must be between 0.5 and 12 pixels.");
        }
        if (series.Curve != LineCurve.Linear && mark is not (ChartKind.Line or ChartKind.Area))
            throw new ArgumentException("A smooth or stepped curve applies to series drawn as lines or areas.");
        if (series.Fill != AreaFill.Flat && mark is not (ChartKind.Area or ChartKind.Column))
            throw new ArgumentException("A faded fill applies to series drawn as areas or columns.");
        if (series.Markers != MarkerStyle.Auto && mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter))
            throw new ArgumentException("Marker styles apply to series drawn as lines, areas or scatter points; a bubble's marker is its size, and the other kinds draw no markers.");
        if (series.Markers == MarkerStyle.None && mark == ChartKind.Scatter)
            throw new ArgumentException("A scatter series is drawn as its markers, so it cannot hide them.");
        if (series.HighlightLast && mark is not (ChartKind.Line or ChartKind.Area))
            throw new ArgumentException("Highlighting the last point applies to series drawn as lines or areas.");
        if (series.ValueLabels && mark is not (ChartKind.Column or ChartKind.Bar))
            throw new ArgumentException("Value labels apply to series drawn as columns or bars.");
        if (series.Gradient is not { } stops) return;
        if (mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Gauge))
            throw new ArgumentException("A gradient colours a stroke and its markers, so it applies to series drawn as lines or areas, and to a gauge's arc.");
        if (series.Zones is not null)
            throw new ArgumentException("Zones colour a stroke in steps and a gradient colours it continuously, so a series takes one or the other.");
        if (stops.Count is < 2 or > 32) throw new ArgumentException("A gradient needs between 2 and 32 colour stops.");
        for (var i = 0; i < stops.Count; i++)
        {
            if (stops[i] is null || stops[i].Color is null) throw new ArgumentException("Colour stops and their colours cannot be null.");
            Color(stops[i].Color);
            if (!Finite(stops[i].Value)) throw new ArgumentException("Colour stop values must be finite, magnitude <= 1e100.");
            if (i > 0 && stops[i].Value <= stops[i - 1].Value) throw new ArgumentException("Colour stop values must rise strictly.");
            if (axis == AxisKind.Log && stops[i].Value <= 0) throw new ArgumentException("Log Y axes require positive colour stop values.");
        }
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
    /// <summary>A range bar needs both bounds, low no greater than high, and a typical value, if any, within them; a point
    /// with neither bound and no value is a missing day.</summary>
    private static void Range(ChartPoint p, AxisKind axis)
    {
        if (p.Low is null && p.High is null && p.Y is null) return;
        if (p.Low is not { } low || p.High is not { } high)
            throw new ArgumentException("A range point needs both Low and High, the ends of its bar; its Y, if any, is the dot within them. Make one with ChartPoint.Interval.");
        if (!Finite(low) || !Finite(high) || low > high)
            throw new ArgumentException("Range bounds must be finite with Low no greater than High.");
        if (p.Y is { } y && (y < low || y > high))
            throw new ArgumentException("A range point's Y is its typical value, drawn as a dot on its bar, so it must lie between its Low and its High.");
        if (axis == AxisKind.Log && low <= 0) throw new ArgumentException("Log Y axes require positive range bounds.");
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
        if (!Enum.IsDefined(style.Gridlines)) throw new ArgumentException("Unknown gridline style.");
        if (!Enum.IsDefined(style.Finish)) throw new ArgumentException("Unknown finish.");
        if (style.BarRadius is { } radius && (!Finite(radius) || radius < 0))
            throw new ArgumentException("A bar radius must be finite and nonnegative; each bar clamps it to half its width, so a large one draws capsules.");
        // The font list is written into a style attribute, so anything beyond a plain family list is refused.
        if (style.FontFamily is null || !FontFamily().IsMatch(style.FontFamily))
            throw new ArgumentException("Font families may contain letters, digits, spaces, commas and hyphens, up to 200 characters.");
    }

    private static bool Annotated(ChartKind kind) => kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble
        or ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band
        or ChartKind.Range or ChartKind.Timeline;

    private const string TimeOfDayAxes = "A time-of-day format reads seconds since a midnight and wraps at 24 hours, so it applies to linear axes: a time axis writes its own calendar, and a logarithmic one has no clock to show.";

    /// <summary>A timeline stacks its states in lanes along X, so everything that belongs to a Y axis, a second axis, a pane or a
    /// series' own mark has no meaning on it. Checked before the general rules, so each refusal gives the timeline's reason.</summary>
    private static void Timeline(ChartSpec spec)
    {
        if (spec.XAxis == AxisKind.Log)
            throw new ArgumentException("A timeline runs along a linear or a time X axis; a logarithmic one would stretch the first minutes of a span over most of the plot.");
        if (spec.YAxis != AxisKind.Linear || spec.Y2Axis != AxisKind.Linear || spec.YReversed || spec.Y2Reversed || spec.YFormat != ValueFormat.Number || spec.Y2Format != ValueFormat.Number
            || spec.YMin is not null || spec.YMax is not null || spec.Y2Min is not null || spec.Y2Max is not null || spec.YTickLabels != TickLabels.All)
            throw new ArgumentException("A timeline stacks its states in lanes rather than measuring values on a Y axis, so it takes no Y axis settings: no logarithmic or reversed axis, no format, no bounds and no tick labelling.");
        if (spec.YZones is not null)
            throw new ArgumentException("A timeline takes no zones: its lanes are its states, each drawn in its series' colour.");
        if (spec.Panes is { Count: > 0 } || spec.Series?.Any(series => series is not null && series.Pane != 0) == true)
            throw new ArgumentException("A timeline draws its states as the lanes of one plot, so it takes no panes.");
        if (spec.Series?.Any(series => series?.Secondary == true) == true)
            throw new ArgumentException("A timeline has no secondary axis: each series is a lane, not values measured on the right.");
        if (spec.Series?.Any(series => series?.Kind is not null) == true)
            throw new ArgumentException("A series' own kind does not apply to a timeline, which draws every series as a lane of spans.");
        if (spec.Series?.Any(series => series?.Trend == true) == true)
            throw new ArgumentException("A timeline draws no trend line: its spans are states, not values to fit.");
        if (spec.Annotations?.Any(annotation => annotation?.Axis == AnnotationAxis.Y) == true)
            throw new ArgumentException("A timeline marks moments, so it takes X annotations; it has no Y axis for a Y annotation.");
    }

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
