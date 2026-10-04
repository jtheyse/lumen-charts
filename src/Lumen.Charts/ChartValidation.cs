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
        if (!Enum.IsDefined(spec.Kind) || !Enum.IsDefined(spec.Theme)) throw new ArgumentException("Unknown chart kind or theme.");
        Dimensions(spec.Width, spec.Height, spec.Sparkline, spec.Kind == ChartKind.Strip);
        if (spec.Sparkline) Sparkline(spec);
        if (spec.Kind == ChartKind.Strip) Strip(spec);
        Track(spec);
        if (spec.FitHeight && spec.Kind != ChartKind.Bar)
            throw new ArgumentException("FitHeight works out a horizontal bar chart's height from its rows, one a category, so it applies to bar charts only; a strip is drawn as tall as its content already, and the other kinds lay their marks out in the Height they are given.");
        if (!Enum.IsDefined(spec.XAxis) || !Enum.IsDefined(spec.YAxis)) throw new ArgumentException("Unknown axis kind.");
        Style(spec.Style);
        if (spec.YAxis == AxisKind.Time) throw new ArgumentException("Time axes are supported on X only.");
        if (spec.Kind == ChartKind.Timeline) Timeline(spec);
        if (spec.Kind == ChartKind.Calendar) Calendar(spec);
        if (spec.XAxis != AxisKind.Linear && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Timeline or ChartKind.Calendar or ChartKind.Blocks))
            throw new ArgumentException("Time and log X axes apply to line, area, scatter, bubble, candlestick, OHLC, band and range charts, and a time axis to timelines, blocks and calendars; the other kinds index or derive their X values.");
        Blocks(spec);
        if (spec.YAxis == AxisKind.Log && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Box or ChartKind.Violin or ChartKind.Blocks))
            throw new ArgumentException("Log Y axes require line, scatter, bubble, candlestick, OHLC, band, range, blocks, box or violin charts; magnitude, count and radial charts need a zero baseline.");
        if (!Enum.IsDefined(spec.Y2Axis) || spec.Y2Axis == AxisKind.Time) throw new ArgumentException("The secondary axis is numeric or logarithmic; time axes are supported on X only.");
        if (spec.Y2Axis == AxisKind.Log && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Blocks))
            throw new ArgumentException("A logarithmic secondary axis requires line, scatter, bubble, band, range or blocks charts.");
        if (!Enum.IsDefined(spec.XFormat) || !Enum.IsDefined(spec.YFormat) || !Enum.IsDefined(spec.Y2Format)) throw new ArgumentException("Unknown value format.");
        if (spec.XFormat == ValueFormat.TimeOfDay && spec.XAxis != AxisKind.Linear || spec.YFormat == ValueFormat.TimeOfDay && spec.YAxis != AxisKind.Linear
            || spec.Y2Format == ValueFormat.TimeOfDay && spec.Y2Axis != AxisKind.Linear)
            throw new ArgumentException(TimeOfDayAxes);
        if (spec.XAxis == AxisKind.Time && spec.XFormat != ValueFormat.Number)
            throw new ArgumentException("A time axis writes its own calendar; duration and compact formats apply to linear and log axes.");
        if (spec.XFormat != ValueFormat.Number && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Timeline or ChartKind.Blocks))
            throw new ArgumentException("An X format applies to line, area, scatter, bubble, candlestick, OHLC, band, range, timeline and blocks charts; the other kinds index or derive their X values.");
        if ((spec.YFormat != ValueFormat.Number || spec.Y2Format != ValueFormat.Number) && spec.Kind is ChartKind.Donut or ChartKind.Heatmap or ChartKind.Radar or ChartKind.Histogram)
            throw new ArgumentException("A Y format applies to the values a Y axis measures; donut, heatmap and radar charts have no Y axis, and a histogram's counts observations.");
        if ((spec.YReversed || spec.Y2Reversed) && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Box or ChartKind.Violin or ChartKind.Blocks))
            throw new ArgumentException(spec.Kind is ChartKind.Gauge or ChartKind.Ring
                ? "A gauge or ring has no Y axis to reverse: its scale runs clockwise round its arc."
                : "A reversed Y axis applies to line, scatter, bubble, band, range, blocks, candlestick, OHLC, box and violin charts. Column, bar, stacked column, area and histogram charts draw from a zero baseline, which a reversed axis would hang from the top, and donut, heatmap and radar charts have no Y axis.");
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
        if (!Enum.IsDefined(spec.XTicks)) throw new ArgumentException("Unknown tick source.");
        if (spec.XTicks != TickSource.Auto && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Blocks))
            throw new ArgumentException("XTicks chooses between a continuous X axis's own ticks and its points' labels, so it applies to line, area, scatter, bubble, candlestick, OHLC, band, range and blocks charts; column, bar and stacked column charts label each category, a timeline and a calendar write their own time, and the other kinds have no X axis to label.");
        if (!Enum.IsDefined(spec.XTickLabels)) throw new ArgumentException("Unknown X tick labelling.");
        if (spec.XTickLabels != TickLabels.All && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Blocks or ChartKind.Timeline))
            throw new ArgumentException("XTickLabels chooses which labels a continuous X axis writes along the bottom, so it applies to line, area, scatter, bubble, candlestick, OHLC, band, range, blocks and timeline charts; column, bar and stacked column charts label each category, a histogram the edges of its bins and a calendar its own dates, and the other kinds have no X axis.");
        if (spec.XTickLabels == TickLabels.Bounds && spec.XTicks == TickSource.PointLabels)
            throw new ArgumentException("XTickLabels = Bounds labels the X axis's own two ends, and XTicks = PointLabels asks for the points' labels instead, so a chart takes one or the other.");
        if (spec.XTickLabels == TickLabels.None)
            throw new ArgumentException("TickLabels.None leaves a Y axis unlabelled, its values read from each mark's name; the X axis is shared by every pane and is how a reader places each mark, so XTickLabels takes All, Ends or Bounds.");
        if (!Enum.IsDefined(spec.Sampling)) throw new ArgumentException("Unknown sampling method.");
        if (!Enum.IsDefined(spec.PaneTitles)) throw new ArgumentException("Unknown pane title placement.");
        if (spec.PaneTitles != PaneTitlePlacement.Axis && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.StackedColumn
            or ChartKind.Band or ChartKind.Range or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Blocks))
            throw new ArgumentException("PaneTitles names each plot above it in place of its Y axis title, so it applies to line, area, scatter, bubble, column, stacked column, band, range, candlestick, OHLC and blocks charts; a horizontal bar chart writes its value axis along the bottom, a timeline names its lanes, and the other kinds have no Y axis up the side.");
        if (spec.SharedReadout && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Blocks))
            throw new ArgumentException("SharedReadout reads every series at one X of a continuous X axis, so it applies to line, area, scatter, bubble, band, range, candlestick, OHLC and blocks charts; column, bar and stacked column charts place their series by category, and the other kinds have no X axis their series share.");
        if (secondary)
        {
            if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Band or ChartKind.Range or ChartKind.Blocks))
                throw new ArgumentException("A secondary axis applies to line, area, scatter, bubble, column, band, range and blocks charts; stacked, horizontal, candlestick, OHLC and radial charts cannot measure against two.");
            if (spec.Series!.Where(series => series is not null).GroupBy(series => series.Pane).Any(pane => pane.All(series => series.Secondary)))
                throw new ArgumentException("A secondary axis needs at least one series on the left of its pane to measure against.");
        }
        if (spec.Panes is null || spec.Panes.Count > 5) throw new ArgumentException("A chart has at most six plots: the main plot and five more in Panes.");
        if ((spec.Panes.Count > 0 || spec.Series?.Any(series => series is not null && series.Pane != 0) == true)
            && spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Blocks or ChartKind.Candlestick or ChartKind.Ohlc))
            throw new ArgumentException("Panes share one continuous X axis, so they apply to line, area, scatter, bubble, band, range, blocks, candlestick and OHLC charts; column, bar and stacked column charts place their bars by category, and the other kinds derive their X or have none.");
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
        if (!Enum.IsDefined(spec.CalendarLayout) || !Enum.IsDefined(spec.CalendarCell) || !Enum.IsDefined(spec.WeekStart))
            throw new ArgumentException("Unknown calendar layout, cell or week start.");
        if (spec.Kind != ChartKind.Calendar && (spec.CalendarLayout != CalendarLayout.Weeks || spec.CalendarCell != CalendarCell.Square || spec.WeekStart != DayOfWeek.Monday))
            throw new ArgumentException("CalendarLayout, CalendarCell and WeekStart lay out a calendar's days, so they apply to calendar charts only.");
        if (spec.Annotations is null || spec.Annotations.Count > 32) throw new ArgumentException("Provide at most 32 annotations.");
        foreach (var annotation in spec.Annotations)
        {
            if (annotation is null) throw new ArgumentException("Annotations cannot be null.");
            if (spec.Kind == ChartKind.Ring)
                throw new ArgumentException("Ring charts take no annotations: each ring's goal is its target.");
            if (!Annotated(spec.Kind) && spec.Kind is not (ChartKind.Gauge or ChartKind.Calendar))
                throw new ArgumentException("Annotations apply to charts drawn on an X and Y axis, to a gauge's arc and to a calendar's days; donut, radar, heatmap, histogram, box and violin charts do not take them yet.");
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
            if (!annotation.ShowValue && string.IsNullOrWhiteSpace(annotation.Label))
                throw new ArgumentException("ShowValue = false draws an annotation's label without its value, so it needs a Label to draw; its tooltip and accessible name still read the value.");
            if (!annotation.ShowValue && spec.Kind == ChartKind.Calendar)
                throw new ArgumentException("A calendar's key names an outlined day by its label alone already, so ShowValue does not apply to a calendar's annotations.");
            if (annotation.InFront && spec.Kind is ChartKind.Gauge or ChartKind.Calendar)
                throw new ArgumentException("InFront draws a reference over the data instead of behind it; a gauge draws its target over the score already, and a calendar outlines its day, so neither takes it.");
            Text(annotation.Label); Color(annotation.Color);
        }
        var style = ChartSvg.ResolveStyle(spec);
        if (spec.YZones is not null)
        {
            if (spec.Kind == ChartKind.Ring)
                throw new ArgumentException("Ring charts take no zones: each ring is drawn in its series' colour.");
            if (!Annotated(spec.Kind) && spec.Kind is not (ChartKind.Gauge or ChartKind.Calendar))
                throw new ArgumentException("Zone bands apply wherever Y annotations do, on charts drawn on an X and Y axis and on a gauge's track, and zones colour a calendar's days; donut, radar, heatmap, histogram, box and violin charts refuse them.");
            Zones(spec.YZones, style);
        }
        foreach (var pane in spec.Panes) Pane(pane, spec, style);
        Ticked(spec.YTickValues, spec.YAxis, spec.Kind);
        Unit(spec.YUnit, spec.Kind);
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
                if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Blocks or ChartKind.Column or ChartKind.Candlestick or ChartKind.Ohlc))
                    throw new ArgumentException("A series' own kind applies to line, area, scatter, bubble, band, range, blocks, column, candlestick and OHLC charts; horizontal bar, stacked column, donut, heatmap, radar, histogram, box and violin charts draw every series one way.");
                if (own is not (ChartKind.Line or ChartKind.Area or ChartKind.Column or ChartKind.Scatter or ChartKind.Band or ChartKind.Range or ChartKind.Blocks))
                    throw new ArgumentException("A series can be drawn as a line, area, column, scatter, band, range or blocks; bubbles share one size scale across a chart, and the other kinds lay out a whole chart rather than one series.");
                if (own == ChartKind.Blocks && spec.Kind == ChartKind.Column)
                    throw new ArgumentException("Blocks run from their X to their XEnd along a continuous axis, and a column chart places its marks by category, where there is no slot a block could take; draw blocks on a line, area, scatter, bubble, band, range or blocks chart.");
            }
            // Everything a series carries is checked against the mark it draws, which is the chart's kind unless it names its own.
            var mark = ChartSvg.Mark(spec, series);
            if (mark == ChartKind.Blocks)
            {
                if (series.Trend) throw new ArgumentException("A block series draws no trend line: its blocks are steps that hold a value across a span, not observations to fit; draw a trend through a line series beside them.");
                if (series.ProjectedFrom is not null) throw new ArgumentException("Blocks take no projection, which dashes a stroke; a planned workout is drawn as blocks of its own, and a projected line beside them can take ProjectedFrom.");
            }
            if (series.Trend && mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble))
                throw new ArgumentException("A trend line applies to series drawn as lines, areas, scatter points or bubbles; the other kinds place their marks by index or derive their own values.");
            Trended(series);
            if (series.Summary is not null) Summary(series, spec.Kind, spec.YAxis);
            Finish(series, mark, measure);
            if (series.Zones is not null)
            {
                if (mark == ChartKind.Range)
                    throw new ArgumentException("A range bar runs from its low to its high, which can lie in several zones, so a range series takes no zones; colour a bar with its point's Color instead.");
                if (mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar or ChartKind.Blocks))
                    throw new ArgumentException("Series zones apply to series drawn as lines, areas, scatter points, bubbles, columns, bars and blocks; on the other kinds colour already says something else, such as direction, value, a stacked series or a distribution.");
                Zones(series.Zones, style);
            }
            if (spec.DensityCells is not null && mark == ChartKind.Scatter && (series.Zones is not null || series.Points.Any(p => p?.Color is not null)))
                throw new ArgumentException("A density scatter shades cells rather than points, so it takes neither zones nor point colours.");
            if (spec.DensityCells is not null && mark == ChartKind.Scatter && (series.ValueLabels || series.ChangeColors != ChangeColors.None))
                throw new ArgumentException("A density scatter shades cells rather than points, so it takes no value labels or change colours.");
            Changed(series, mark);
            Ended(spec, series, mark);
            Averaged(series, mark);
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
                Text(p.Label); Color(p.Color); Text(p.ValueNote); Color(p.Highlight);
                if (p.SubLabel is not null) SubLabel(spec, p.SubLabel);
                if (p.GapLabel is not null) GapLabel(spec, mark, p);
                if (p.Highlight is not null)
                {
                    if (mark is not (ChartKind.Line or ChartKind.Scatter))
                        throw new ArgumentException("A highlight rings one point of a line or scatter series with an enlarged marker; an area's points are its outline, and the other kinds draw a bar, a span, a slice, a cell or a distribution rather than a point to ring, so colour such a mark with its point's Color.");
                    if (spec.DensityCells is not null)
                        throw new ArgumentException("A density scatter shades cells rather than points, so it rings no point with a highlight.");
                }
                if (p.ValueNote is not null)
                {
                    if (p.ValueNote.Length > 20)
                        throw new ArgumentException("A value note follows a value on the chart, so it is at most 20 characters, such as /48 after a finishing position for the size of its field; longer words belong in the point's label.");
                    if (mark is ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Range or ChartKind.Histogram or ChartKind.Box or ChartKind.Violin or ChartKind.Timeline or ChartKind.Calendar or ChartKind.Gauge or ChartKind.Ring)
                        throw new ArgumentException("A value note is written after a mark's one value, so it applies to lines, areas, bands, scatter points, bubbles, columns, bars, blocks, donut slices, heatmap cells and radar points; a candle reads four prices, a range bar two ends, a timeline's span has no value, histograms, boxes, violins and calendars add their points up, and a gauge or ring writes its value in its legend.");
                }
                if (p.Color is not null && mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar or ChartKind.Range or ChartKind.Blocks or ChartKind.Donut or ChartKind.Strip))
                    throw new ArgumentException("Point colours apply to series drawn as lines, areas, scatter points, bubbles, columns, bars, ranges, blocks, donut slices and a strip's parts; on the other kinds colour already says something else: direction on candlesticks and OHLC bars, value on a heatmap, the state a timeline's lane stands for, and the series or distribution a mark belongs to on stacked column, radar, band, histogram, box and violin charts.");
                if (p.XEnd is { } end)
                {
                    if (spec.Kind != ChartKind.Timeline && mark != ChartKind.Blocks) throw new ArgumentException("XEnd ends a span or a block, so it applies to timeline charts and to series drawn as blocks.");
                    if (!Finite(end) || end <= p.X) throw new ArgumentException(mark == ChartKind.Blocks
                        ? "A block ends after it starts: its XEnd must be finite and above its X."
                        : "A timeline span ends after it starts: its XEnd must be finite and above its X.");
                    if (spec.XAxis == AxisKind.Time && !TimeAxis.InRange(end)) throw new ArgumentException("Time X values must be Unix milliseconds between year 1 and year 9999.");
                }
                else if (spec.Kind == ChartKind.Timeline) throw new ArgumentException("Every point of a timeline is a span with an XEnd above its X; make one with ChartPoint.Span.");
                else if (mark == ChartKind.Blocks) throw new ArgumentException("Every block runs from its X to an XEnd above it; make one with ChartPoint.Block(start, end, height).");
                if (mark == ChartKind.Blocks && p.Y is null)
                    throw new ArgumentException("A block's Y is its height, how far it rises from the bottom edge of its plot, so it cannot be missing; leave a gap between blocks instead.");
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
            if (mark == ChartKind.Blocks)
            {
                var blocks = series.Points.OrderBy(p => p.X).ToArray();
                for (var i = 1; i < blocks.Length; i++)
                    if (blocks[i].X < blocks[i - 1].XEnd)
                        throw new ArgumentException($"Blocks in one series cannot overlap, and two in {series.Name} do; they may touch, one ending where the next begins, and blocks in different series may overlap.");
            }
            if (mark is ChartKind.Line or ChartKind.Area or ChartKind.Band or ChartKind.Candlestick or ChartKind.Ohlc && series.Points.Zip(series.Points.Skip(1)).Any(p => p.First.X > p.Second.X))
                throw new ArgumentException("Line, area, candlestick, OHLC and band points must be ordered by X.");
            // Scatter points and bubbles may come in any order, but a moving average runs through them in the order they come.
            if (series.Trend && series.TrendFit == TrendFit.MovingAverage && series.Points.Zip(series.Points.Skip(1)).Any(p => p.First.X > p.Second.X))
                throw new ArgumentException("A moving average runs through a series' points in the order they are listed, a window at a time, so they must be ordered by X.");
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
            if (spec.Series.SelectMany(s => s.Points).Where(p => p.SubLabel is not null).GroupBy(p => p.X).Any(category => category.Select(p => p.SubLabel).Distinct().Count() > 1))
                throw new ArgumentException("A category's sub-label is written once under its name, so the points of several series in one category may repeat it or leave it null, but not give different ones.");
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
        for (var pane = 0; pane <= spec.Panes.Count; pane++) { Spanned(spec, pane); Symmetric(spec, pane); }
        // A calendar draws a cell for every day it spans, so the span is bounded; it is read in the chart's zone, checked above.
        if (spec.Kind == ChartKind.Calendar && spec.Series.Any(series => series.Points.Count > 0))
        {
            var (first, last) = ChartSvg.CalendarSpan(spec);
            if (last.DayNumber - first.DayNumber >= MaxCalendarDays)
                throw new ArgumentException($"A calendar spans at most {MaxCalendarDays.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} days, about ten years, from its first day to its last; aggregate a longer span first.");
        }
    }

    private const int MaxCalendarDays = 3660;

    /// <summary>The most parts a strip takes: past that they are too thin to see, and its key too long to read.</summary>
    public const int MaxStripParts = 24;

    /// <summary>A strip draws one series' parts as shares of one bar, with its own key and no axes, so everything that belongs to an axis, a
    /// second series, a pane or a series' own mark has no meaning on it. Checked before the general rules, so each refusal gives the strip's
    /// reason. A strip without points is left to draw its empty state, as every kind does.</summary>
    private static void Strip(ChartSpec spec)
    {
        if (spec.Series is { Count: > 1 })
            throw new ArgumentException("A strip draws the parts of one whole, one series whose points are its parts; draw several wholes as several strips.");
        if (spec.Panes is { Count: > 0 } || spec.Series?.Any(series => series is not null && series.Pane != 0) == true)
            throw new ArgumentException("A strip is one bar, so it takes no panes.");
        if (spec.XAxis != AxisKind.Linear || spec.YAxis != AxisKind.Linear || spec.Y2Axis != AxisKind.Linear || spec.YReversed || spec.Y2Reversed || spec.XFormat != ValueFormat.Number
            || spec.XMin is not null || spec.XMax is not null || spec.YMin is not null || spec.YMax is not null || spec.Y2Min is not null || spec.Y2Max is not null || spec.YMinSpan is not null || spec.YSymmetric is not null
            || spec.IncludeZero || spec.MinorGridlines || spec.YAxisSide != AxisSide.Left || spec.YTickLabels != TickLabels.All || spec.YTickValues is not null)
            throw new ArgumentException("A strip draws its parts as shares of one bar and has no axes, so it takes no axis settings: no time, logarithmic or reversed axis, no bounds, no axis side, ticks or gridlines, and no X format; YFormat and YUnit write each part's amount in its name.");
        if (spec.Annotations is { Count: > 0 } || spec.YZones is not null)
            throw new ArgumentException("A strip has no axes, so it takes no annotations and no zones; colour each part with its point's Color.");
        if (spec.Series is not [{ } series]) return;
        if (series.Kind is not null || series.Secondary || series.Trend || series.Zones is not null || series.Gradient is not null || series.ChangeColors != ChangeColors.None || series.EndLabel is not null || series.EndNote is not null)
            throw new ArgumentException("A strip draws its one series as the parts of a bar, so the series takes no kind of its own, secondary axis, trend, zones, gradient, change colours or end label.");
        if (series.ValueLabels)
            throw new ArgumentException("A strip's key writes each part's share under the bar, and its amount is in the part's name, so it takes no value labels.");
        if (series.Points is null) return;
        if (series.Points.Count > MaxStripParts)
            throw new ArgumentException($"A strip takes at most {MaxStripParts} parts; past that they are too thin to see and its key too long to read, so group the small ones.");
        foreach (var p in series.Points)
        {
            if (p is null) continue;
            if (string.IsNullOrWhiteSpace(p.Label))
                throw new ArgumentException("Each part of a strip is named by its point's Label, which its key and its mark's name write, so it cannot be blank.");
            if (p.Y is not { } amount || double.IsNaN(amount) || amount < 0)
                throw new ArgumentException("A part of a strip is an amount, zero or more, such as seconds in a zone; it cannot be missing, negative or not a number. A part of zero keeps its place in the key.");
        }
        if (series.Points.Count > 0 && series.Points.All(p => p?.Y == 0))
            throw new ArgumentException("A strip shows each part's share of the whole, and parts that are all zero have no whole to share; show the app's own empty state instead.");
    }

    /// <summary>A track runs a bar's value axis from zero to a maximum set by hand, so it needs a bar or column chart whose axis starts at zero
    /// and ends where <see cref="ChartSpec.YMax"/> says.</summary>
    private static void Track(ChartSpec spec)
    {
        if (!spec.BarTrack) return;
        if (spec.Kind is not (ChartKind.Bar or ChartKind.Column))
            throw new ArgumentException("BarTrack draws a track behind each bar of a bar or column chart, from zero to YMax, as a meter does; a stacked column fills its whole height with its series, and the other kinds draw no bars.");
        if (spec.YMax is null)
            throw new ArgumentException("A bar's track runs to the value axis's maximum, so BarTrack needs YMax, the end of the scale, such as 100 for a score out of 100.");
        if (spec.YMin is { } min && min != 0 || spec.Series?.Any(series => series?.Points?.Any(p => p?.Y < 0) == true) == true)
            throw new ArgumentException("A bar's track runs from zero, so BarTrack needs an axis that starts at zero: YMin unset or 0, and no value below zero.");
        if (spec.Series?.Any(series => series?.Secondary == true) == true)
            throw new ArgumentException("A track runs the left-hand axis, so BarTrack measures every series on it; a chart with tracks takes no secondary series.");
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

    /// <summary>A sparkline draws its data alone at the size of a word, so it takes the marks whose shape reads without axes, and
    /// nothing that needs room or words of its own. Checked before the general rules, so each refusal gives the sparkline's reason.</summary>
    private static void Sparkline(ChartSpec spec)
    {
        if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Column))
            throw new ArgumentException("A sparkline draws a line, an area, scatter points or columns, whose shape reads without axes; the other kinds need their axes, their keys or their labels to be read, so draw them as a full chart.");
        if (spec.Series?.Any(series => series?.Kind is not (null or ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Column)) == true)
            throw new ArgumentException("A sparkline's series are drawn as lines, areas, scatter points or columns; bands, ranges and blocks need an axis to be read against, so draw them on a full chart.");
        if (spec.Panes is { Count: > 0 } || spec.Series?.Any(series => series is not null && series.Pane != 0) == true)
            throw new ArgumentException("A sparkline is one small plot, so it takes no panes; draw each measure as a sparkline of its own.");
        if (spec.Series?.Any(series => series?.ValueLabels == true) == true)
            throw new ArgumentException("A sparkline draws its data alone, so it writes no value labels; write the numbers in the words beside it, and each point's value stays in its tooltip and accessible name.");
        if (spec.SharedReadout)
            throw new ArgumentException("A sparkline is read beside the words that give its numbers, a point at a time, so it takes no shared readout; draw the series as a full chart to read them together.");
        if (spec.PaneTitles != PaneTitlePlacement.Axis)
            throw new ArgumentException("A sparkline draws its data alone, with no words, so it names no plot above it; PaneTitles applies to a full chart.");
        if (spec.YTickValues is not null)
            throw new ArgumentException("A sparkline draws no axes, so it takes no ticks; YTickValues applies to a full chart.");
        if (spec.Series?.Any(series => series?.EndLabel is not null || series?.EndNote is not null) == true)
            throw new ArgumentException("A sparkline draws its data alone, with no words, so it writes no end labels; name the series in the words beside it.");
        if (spec.Series?.Any(series => series?.Points?.Any(p => p?.GapLabel is not null) == true) == true)
            throw new ArgumentException("A sparkline draws its data alone, with no words, so it writes no gap labels; a missing value is a gap in it, and the words beside it can say why.");
    }

    /// <summary>A minimum span widens an axis fitted to the data about the data's middle, so it needs an axis that is fitted to the
    /// data, linear, and free to leave zero out. Pane 0 is the main plot.</summary>
    private static void Spanned(ChartSpec spec, int index)
    {
        var pane = ChartSvg.Pane(spec, index);
        if (pane.YMinSpan is not { } span) return;
        if (!Finite(span) || span <= 0) throw new ArgumentException("YMinSpan is the least a Y axis spans, so it must be positive and finite, magnitude <= 1e100.");
        if (spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Blocks
            or ChartKind.Area or ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Histogram))
            throw new ArgumentException("YMinSpan widens a Y axis fitted to the data, so it applies to line, scatter, bubble, band, range, candlestick, OHLC and blocks charts; donut, heatmap, radar, gauge, ring, timeline and calendar charts have no such axis, and box and violin charts fit theirs to their distributions.");
        if (pane.YMin is not null || pane.YMax is not null)
            throw new ArgumentException("YMinSpan centres an axis fitted to the data, and YMin or YMax sets where that axis ends instead, so an axis takes one or the other.");
        if (pane.YAxis == AxisKind.Log)
            throw new ArgumentException("YMinSpan centres a span of values on the data, and a logarithmic axis measures ratios, which have no one width in values; use a linear axis.");
        if (spec.IncludeZero || spec.Kind is ChartKind.Area or ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Histogram
            || spec.Series.Any(series => series.Pane == index && !series.Secondary && ChartSvg.Mark(spec, series) is ChartKind.Column or ChartKind.Area))
            throw new ArgumentException("YMinSpan centres an axis on its data, and an axis that must include zero is held at zero instead: one set to IncludeZero, a kind drawn from zero, or one that carries columns or an area.");
    }

    /// <summary>A symmetric axis runs as far below zero as above it, so it needs a linear axis whose ends nothing else sets. Pane 0 is the
    /// main plot.</summary>
    private static void Symmetric(ChartSpec spec, int index)
    {
        var pane = ChartSvg.Pane(spec, index);
        if (pane.YSymmetric is not { } least) return;
        if (!Finite(least) || least <= 0) throw new ArgumentException("YSymmetric is the least a Y axis reaches either side of zero, so it must be positive and finite, magnitude <= 1e100.");
        if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn
            or ChartKind.Band or ChartKind.Range or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Blocks))
            throw new ArgumentException("YSymmetric holds a Y axis symmetric about zero, so it applies to line, area, scatter, bubble, column, bar, stacked column, band, range, candlestick, OHLC and blocks charts; donut, heatmap, radar, gauge, ring, timeline and calendar charts have no such axis, a histogram counts up from zero, and box and violin charts fit theirs to their distributions.");
        if (pane.YMin is not null || pane.YMax is not null || pane.YMinSpan is not null)
            throw new ArgumentException("YSymmetric sets both ends of a Y axis about zero, and YMin, YMax or YMinSpan sets them another way, so an axis takes one or the other.");
        if (pane.YAxis == AxisKind.Log)
            throw new ArgumentException("YSymmetric holds a Y axis symmetric about zero, and a logarithmic axis has no zero or negative values; use a linear axis.");
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
        if (pane.Y2Axis == AxisKind.Log && spec.Kind is not (ChartKind.Line or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Blocks))
            throw new ArgumentException("A logarithmic secondary axis requires line, scatter, bubble, band, range or blocks charts.");
        if (!Enum.IsDefined(pane.YFormat) || !Enum.IsDefined(pane.Y2Format)) throw new ArgumentException("Unknown value format.");
        if (pane.YTickLabels is { } labels && !Enum.IsDefined(labels)) throw new ArgumentException("Unknown Y axis side or tick labelling.");
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
        Ticked(pane.YTickValues, pane.YAxis, spec.Kind);
        Unit(pane.YUnit, spec.Kind);
    }

    /// <summary>The kinds whose values a Y axis measures up the side or along the bottom: those drawn on an X and a Y axis.</summary>
    private static bool Measured(ChartKind kind) => kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column
        or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Blocks;

    /// <summary>Ticks set by hand stand where a Y axis can show them: few enough to read, each once, finite, positive on a logarithmic
    /// axis, and with labels short enough to sit beside the plot.</summary>
    private static void Ticked(IReadOnlyList<AxisTick>? ticks, AxisKind axis, ChartKind kind)
    {
        if (ticks is null) return;
        if (!Measured(kind))
            throw new ArgumentException("YTickValues sets the ticks of a Y axis drawn beside a plot, so it applies to line, area, scatter, bubble, column, bar, stacked column, candlestick, OHLC, band, range and blocks charts; donut, heatmap, radar, gauge, ring, timeline and calendar charts have no such axis, and histogram, box and violin charts draw their own.");
        if (ticks.Count > 24) throw new ArgumentException("YTickValues takes at most 24 ticks; more than that crowd an axis past reading.");
        foreach (var tick in ticks)
        {
            if (tick is null) throw new ArgumentException("YTickValues cannot hold a null tick.");
            if (!Finite(tick.Value)) throw new ArgumentException("A tick's value must be finite, magnitude <= 1e100.");
            if (axis == AxisKind.Log && tick.Value <= 0) throw new ArgumentException("A logarithmic axis has no zero or negative values, so its ticks must be positive.");
            Text(tick.Label);
            if (tick.Label?.Length > 24) throw new ArgumentException("A tick's label is written beside the plot, so it is at most 24 characters, such as Front or Back.");
        }
        if (ticks.Select(tick => tick.Value).Distinct().Count() != ticks.Count)
            throw new ArgumentException("Each value in YTickValues stands once; two ticks at one value would write two labels in one place.");
    }

    /// <summary>A unit follows every value an axis writes, so it is short, and it needs an axis whose values are written.</summary>
    private static void Unit(string? unit, ChartKind kind)
    {
        if (unit is null) return;
        Text(unit);
        if (unit.Length > 8) throw new ArgumentException("YUnit is written after every value on its axis, so it is at most 8 characters, such as s, % or \" bpm\".");
        if (!Measured(kind) && kind != ChartKind.Strip)
            throw new ArgumentException("YUnit follows the values a Y axis measures, so it applies to line, area, scatter, bubble, column, bar, stacked column, candlestick, OHLC, band, range and blocks charts, and to the amounts a strip's parts name; donut, heatmap, radar, gauge, ring, timeline and calendar charts have no such axis, a gauge writes its unit from YLabel, and histogram, box and violin charts do not take one yet.");
    }

    /// <summary>A trend's fit, window and degree choose the trend <see cref="ChartSeries.Trend"/> draws, so each needs a trend, and
    /// the window and degree the fit they belong to, before it can mean anything.</summary>
    private static void Trended(ChartSeries series)
    {
        if (!Enum.IsDefined(series.TrendFit)) throw new ArgumentException("Unknown trend fit.");
        if (!series.Trend && (series.TrendFit != TrendFit.Linear || series.TrendPoints != 7 || series.TrendDegree != 2))
            throw new ArgumentException("TrendFit, TrendPoints and TrendDegree choose the trend a series draws, so they need Trend = true; without it nothing is drawn.");
        if (series.TrendPoints is < 2 or > 1000)
            throw new ArgumentException("TrendPoints, a moving average's window, is from 2 to 1000 points.");
        if (series.TrendDegree is < 2 or > 4)
            throw new ArgumentException("TrendDegree, a polynomial's degree, is 2, 3 or 4: a quadratic, a cubic or a quartic.");
        if (series.TrendPoints != 7 && series.TrendFit != TrendFit.MovingAverage)
            throw new ArgumentException("TrendPoints is a moving average's window, so it applies to TrendFit.MovingAverage.");
        if (series.TrendDegree != 2 && series.TrendFit != TrendFit.Polynomial)
            throw new ArgumentException("TrendDegree is a polynomial's degree, so it applies to TrendFit.Polynomial.");
    }

    /// <summary>Change colours compare each point with the one before it along a line or among scatter points, and own the colour
    /// of every mark they touch, so they need marks in X order and refuse anything else that colours the same marks.</summary>
    private static void Changed(ChartSeries series, ChartKind mark)
    {
        if (!Enum.IsDefined(series.ChangeColors)) throw new ArgumentException("Unknown change colours.");
        if (series.ChangeColors == ChangeColors.None) return;
        if (mark is not (ChartKind.Line or ChartKind.Scatter))
            throw new ArgumentException("Change colours apply to series drawn as lines or scatter points, where each point follows the one before; on the other kinds a mark stands for a span, a category, a range or a distribution, or colour already says something else.");
        if (series.Zones is not null)
            throw new ArgumentException("Zones colour a point by the band its value falls in and change colours by how it moved from the point before, so a series takes one or the other.");
        if (series.Gradient is not null)
            throw new ArgumentException("A gradient colours a series by its value and change colours by how each point moved from the one before, so a series takes one or the other.");
        if (series.Points.Any(p => p?.Color is not null))
            throw new ArgumentException("A point's own colour would hide whether it did better or worse than the one before, so a series with change colours takes no point colours.");
        if (series.Points.Zip(series.Points.Skip(1)).Any(p => p.First is not null && p.Second is not null && p.First.X > p.Second.X))
            throw new ArgumentException("Change colours compare each point with the one before it, so the points must be ordered by X.");
    }

    /// <summary>The longest <see cref="ChartSeries.AverageOf"/>: a short span such as <c>12 s</c> or <c>a week</c>.</summary>
    private const int MaxAverageOf = 16;
    /// <summary>A series' own average is said after each of its marks' one value, so it needs a few words on one line and marks named by one
    /// value each.</summary>
    private static void Averaged(ChartSeries series, ChartKind mark)
    {
        if (series.AverageOf is not { } of) return;
        Text(of);
        if (string.IsNullOrWhiteSpace(of))
            throw new ArgumentException("AverageOf says what a series' points are averages of, such as 12 s, so it needs words; leave it null for points that are not averages.");
        if (of.Length > MaxAverageOf || of.Any(c => c is '\n' or '\r' or '\t'))
            throw new ArgumentException($"AverageOf is said after each value, as \", average of 12 s\", so it is at most {MaxAverageOf} characters on one line, such as 12 s or a week.");
        if (mark is ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Range or ChartKind.Histogram or ChartKind.Box or ChartKind.Violin or ChartKind.Timeline
            or ChartKind.Calendar or ChartKind.Donut or ChartKind.Gauge or ChartKind.Ring or ChartKind.Strip)
            throw new ArgumentException("AverageOf is said after a mark's one value, so it applies to series drawn as lines, areas, scatter points, bubbles, columns, bars, stacked columns, bands, blocks, heatmap rows and radar series; a candle reads four prices and a range bar two ends, histograms, boxes, violins and calendars count or add up their points, a timeline's span has no value, and a donut's slices, a strip's parts, a gauge's score and a ring's progress are shares or totals rather than averages.");
    }

    /// <summary>An end label names a series where its line or its points end, in the margin right of the plot, so it needs a series that
    /// ends at a point, words to write, and a right margin no axis takes.</summary>
    private static void Ended(ChartSpec spec, ChartSeries series, ChartKind mark)
    {
        if (series.EndLabel is null && series.EndNote is null) return;
        Text(series.EndLabel); Text(series.EndNote);
        if (series.EndLabel is null) throw new ArgumentException("EndNote is written after a series' EndLabel, so it needs one to follow.");
        if (string.IsNullOrWhiteSpace(series.EndLabel)) throw new ArgumentException("An end label names its series, so it needs words; leave EndLabel null to write none.");
        if (series.EndLabel.Length > 24 || series.EndNote?.Length > 24)
            throw new ArgumentException("An end label and its note each take at most 24 characters, such as a rider's short name and +12.3s; longer words belong in the series' name.");
        if (mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter))
            throw new ArgumentException("An end label is written after a series' last point, so it applies to series drawn as lines, areas or scatter points; the other kinds end in a bar, a span, a slice, a cell or a distribution, and their legend names them.");
        if (spec.DensityCells is not null && mark == ChartKind.Scatter)
            throw new ArgumentException("A density scatter shades cells rather than points, so it has no last point to write an end label after.");
        if (series.Secondary || spec.Series.Any(other => other?.Secondary == true) || spec.YAxisSide == AxisSide.Right)
            throw new ArgumentException("End labels are written in the margin right of the plot, which a right-hand axis takes for its tick labels, so a chart with a secondary series or its Y axis on the right takes none; name its series in the legend.");
    }

    /// <summary>The most characters a point's sub-label takes.</summary>
    private const int MaxSubLabel = 16;

    /// <summary>A sub-label is a second line under a category's name, so it needs a chart that names categories along an axis, words on one
    /// short line, and room to write them.</summary>
    private static void SubLabel(ChartSpec spec, string sub)
    {
        Text(sub);
        if (spec.Kind is not (ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn))
            throw new ArgumentException("A sub-label is a second line under a category's name, so it applies to column, bar and stacked column charts; the other kinds write tick labels along a continuous X axis, or no category names at all, so name the point in its Label or ValueNote.");
        if (spec.Sparkline)
            throw new ArgumentException("A sparkline draws its data alone, with no words, so its points take no sub-labels; write them in the words beside it.");
        if (string.IsNullOrWhiteSpace(sub))
            throw new ArgumentException("A sub-label is written under its category's name, so it needs words; leave SubLabel null to write none.");
        if (sub.Length > MaxSubLabel || sub.Any(c => c is '\n' or '\r' or '\t'))
            throw new ArgumentException($"A sub-label is one short line under its category's name, at most {MaxSubLabel} characters and no line breaks, such as 152 bpm or 13.0 W/kg.");
    }

    /// <summary>The most characters a point's gap label takes.</summary>
    private const int MaxGapLabel = 12;

    /// <summary>A gap label is the word a missing value is written as, so it needs words on one short line, a point with no value, and a
    /// series whose missing values leave a place on the chart: a line, an area or scatter points.</summary>
    private static void GapLabel(ChartSpec spec, ChartKind mark, ChartPoint p)
    {
        var gap = p.GapLabel!;
        Text(gap);
        if (string.IsNullOrWhiteSpace(gap))
            throw new ArgumentException("A gap label is the word a missing value is written as, such as absent, so it needs words; leave GapLabel null to write none.");
        if (gap.Length > MaxGapLabel || gap.Any(c => c is '\n' or '\r' or '\t'))
            throw new ArgumentException($"A gap label is one short word or two written in the plot, at most {MaxGapLabel} characters and no line breaks, such as absent or no result.");
        if (mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter))
            throw new ArgumentException("A gap label is written where a line, an area or scatter points miss a value, so it applies to series drawn as lines, areas or scatter points; the other kinds draw nothing at a missing value, or draw a value of their own, so name the point in its Label or ValueNote.");
        if (spec.DensityCells is not null && mark == ChartKind.Scatter)
            throw new ArgumentException("A density scatter shades cells rather than points, so it writes no gap label at a missing point.");
        if (p.Y.HasValue)
            throw new ArgumentException("A gap label is written where a value is missing, so it applies to a point whose Y is null; a point with a value is read by its value, and a note on it belongs in its ValueNote.");
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
        if (series.ValueLabels && mark is not (ChartKind.Column or ChartKind.Bar or ChartKind.Line or ChartKind.Scatter))
            throw new ArgumentException("Value labels apply to series drawn as columns, bars, lines or scatter points.");
        if (series.Gradient is not { } stops) return;
        if (mark == ChartKind.StackedColumn)
            throw new ArgumentException("A stacked column's colours tell its stacked series apart, so a gradient by value would hide which series each piece is; draw one series of columns, or a column chart, to colour by value.");
        if (mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Gauge or ChartKind.Column or ChartKind.Bar))
            throw new ArgumentException("A gradient colours a series by its value, so it applies to series drawn as lines, areas, columns or the bars of a bar chart, and to a gauge's arc.");
        if (mark is ChartKind.Column && series.Fill != AreaFill.Flat)
            throw new ArgumentException("A faded column fades its own colour towards its end, and a gradient colours it by value, so a column series takes one or the other.");
        if (series.Zones is not null)
            throw new ArgumentException("Zones colour a series in steps and a gradient colours it continuously, so a series takes one or the other.");
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
        or ChartKind.Range or ChartKind.Timeline or ChartKind.Blocks;

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

    /// <summary>Blocks run along a continuous X axis from each point's X to its XEnd, drawn as the chart's kind or as a series'
    /// own. Checked before the general rules, so the refusal gives the blocks' reason.</summary>
    private static void Blocks(ChartSpec spec)
    {
        if (spec.Series?.Any(series => series is not null && ChartSvg.Mark(spec, series) == ChartKind.Blocks) != true) return;
        if (spec.XAxis == AxisKind.Log)
            throw new ArgumentException("Blocks run from their X to their XEnd along a linear or a time X axis; a logarithmic one would draw two blocks of one length at different widths, and a block's width is its length.");
    }

    private const string CalendarTime = "A calendar places each point on the day it falls on, so it needs a time X axis: XAxis = AxisKind.Time, with X in Unix milliseconds.";

    /// <summary>A calendar colours the days of one series in a grid, so everything that belongs to a Y axis, a second axis, a pane,
    /// several series, a series' own mark or a point's own colour has no meaning on it. Checked before the general rules, so each
    /// refusal gives the calendar's reason. A calendar without points is left to draw its empty state, as every kind does.</summary>
    private static void Calendar(ChartSpec spec)
    {
        var points = spec.Series?.Where(series => series?.Points is not null).SelectMany(series => series.Points).Where(p => p is not null).ToArray() ?? [];
        if (spec.XAxis == AxisKind.Log || spec.XAxis == AxisKind.Linear && points.Length > 0) throw new ArgumentException(CalendarTime);
        if (spec.YAxis != AxisKind.Linear || spec.YReversed || spec.YMin is not null || spec.YMax is not null || spec.YAxisSide != AxisSide.Left || spec.YTickLabels != TickLabels.All)
            throw new ArgumentException("A calendar colours each day by its value rather than measuring it on a Y axis, so it takes no logarithmic or reversed axis, no bounds, no axis side and no tick labelling; YFormat still writes its values.");
        if (spec.Y2Axis != AxisKind.Linear || spec.Y2Reversed || spec.Y2Format != ValueFormat.Number || spec.Y2Min is not null || spec.Y2Max is not null || spec.Series?.Any(series => series?.Secondary == true) == true)
            throw new ArgumentException("A calendar has no secondary axis: its one series is its days.");
        if (spec.Panes is { Count: > 0 } || spec.Series?.Any(series => series is not null && series.Pane != 0) == true)
            throw new ArgumentException("A calendar draws its days in one grid, so it takes no panes.");
        if (spec.Series is { Count: > 1 }) throw new ArgumentException("A calendar draws one series of days; draw several series as several calendars.");
        if (spec.Series?.Any(series => series?.Kind is not null) == true)
            throw new ArgumentException("A series' own kind does not apply to a calendar, which draws its one series as days.");
        if (spec.Series?.Any(series => series?.Trend == true) == true)
            throw new ArgumentException("A calendar draws no trend line: its days are coloured by value, not plotted along an axis.");
        if (spec.Series?.Any(series => series?.Zones is not null) == true)
            throw new ArgumentException("A calendar takes its zones from YZones, which colour each day by its value.");
        if (spec.SkipWeekends || spec.TimeSkips is { Count: > 0 })
            throw new ArgumentException("A calendar draws every day of its weeks, so it skips none: SkipWeekends and TimeSkips apply to continuous time axes.");
        if (spec.Annotations?.Any(annotation => annotation?.Axis == AnnotationAxis.Y) == true)
            throw new ArgumentException("A calendar marks days, so it takes X annotations, each outlining its day; it has no Y axis for a Y annotation.");
        if (spec.Annotations?.Any(annotation => annotation?.Axis == AnnotationAxis.X && annotation.To is not null) == true)
            throw new ArgumentException("A calendar outlines the one day an X annotation marks, so it takes lines, not bands.");
        if (points.Any(p => p.XEnd is not null)) throw new ArgumentException("A calendar's points are days, each at its X; XEnd ends the spans of a timeline.");
        if (points.Any(p => p.Color is not null))
            throw new ArgumentException("A calendar colours each day by its value, from YZones or the style's heatmap ramp, so its points take no colours of their own.");
        if (points.Any(p => p.Y < 0))
            throw new ArgumentException("A calendar's values are amounts, such as distance, time or training stress, so none can be negative; zero or a missing value is a day without activity.");
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
    /// <summary>A sparkline needs no room for axes, a title or a legend, so it may be as small as a word; the largest drawing is the
    /// same for both. A strip is drawn as tall as its content, so it does not use its height, which may lie anywhere from 16 to 2160.</summary>
    internal static void Dimensions(int width, int height, bool sparkline = false, bool strip = false)
    {
        if (strip && !sparkline && (width is < 320 or > 4096 || height is < 16 or > 2160)) throw new ArgumentException("A strip's width must be 320–4096; it is drawn as tall as its content, and its Height, which it does not use, must lie within 16–2160.");
        if (strip && !sparkline) return;
        if (sparkline && (width is < 60 or > 4096 || height is < 16 or > 2160)) throw new ArgumentException("A sparkline's dimensions must be 60–4096 by 16–2160.");
        if (!sparkline && (width is < 320 or > 4096 || height is < 240 or > 2160)) throw new ArgumentException("Dimensions must be 320–4096 by 240–2160.");
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
