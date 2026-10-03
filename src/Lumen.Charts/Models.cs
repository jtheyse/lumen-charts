namespace Lumen.Charts;

/// <summary>The kind of chart, which decides how X is laid out and how a series that names no kind of its own is drawn.
/// Line, area, scatter, bubble, band, candlestick and OHLC charts place points along a continuous X axis by their X;
/// column, bar, stacked column, donut, heatmap and radar charts place them by category and show their labels.</summary>
public enum ChartKind
{
    /// <summary>Each series as a line through its points in X order.</summary>
    Line,
    /// <summary>Each series as a line filled down to zero, so its axis always includes zero.</summary>
    Area,
    /// <summary>A dot for every point.</summary>
    Scatter,
    /// <summary>A dot for every point, its area proportional to the point's <see cref="ChartPoint.Size"/> on one scale across
    /// every series.</summary>
    Bubble,
    /// <summary>Upright bars from zero, one slot per category, the series side by side within it.</summary>
    Column,
    /// <summary>Horizontal bars from zero, one row per category. The value axis runs along the bottom.</summary>
    Bar,
    /// <summary>Columns with the series stacked, positive values upward from zero and negative ones downward.</summary>
    StackedColumn,
    /// <summary>One nonnegative series as the slices of a ring.</summary>
    Donut,
    /// <summary>A grid of cells, a row for each series and a column for each distinct X, shaded from the style's
    /// <see cref="ChartStyle.HeatmapLow"/> to its <see cref="ChartStyle.HeatmapHigh"/>.</summary>
    Heatmap,
    /// <summary>Complete, nonnegative series on shared categories, drawn round a circle.</summary>
    Radar,
    /// <summary>One price series as candles made with <see cref="ChartPoint.Candle"/>, coloured by direction, beside any
    /// series that name a kind of their own.</summary>
    Candlestick,
    /// <summary>A central line inside a shaded interval, from points made with <see cref="ChartPoint.Interval"/>.</summary>
    Band,
    /// <summary>Counts of observations in equal-width bins, read from each point's Y. Up to four series share one set of bins.</summary>
    Histogram,
    /// <summary>The quartiles, Tukey whiskers and outliers of each series' observations, or a supplied
    /// <see cref="ChartSeries.Summary"/>.</summary>
    Box,
    /// <summary>Each series' observations as a mirrored density outline, with their quartiles and median.</summary>
    Violin,
    /// <summary>One price series as open-high-low-close bars, coloured by direction, beside any series that name a kind of
    /// their own.</summary>
    Ohlc
}
/// <summary>The preset a chart draws with when it sets no <see cref="ChartSpec.Style"/>.</summary>
public enum ChartTheme
{
    /// <summary>Dark text on white: <see cref="ChartStyle.Light"/>.</summary>
    Light,
    /// <summary>Light text on a dark slate background: <see cref="ChartStyle.Dark"/>.</summary>
    Dark
}
/// <summary>How a line or area runs from one point to the next. <see cref="Smooth"/> is a monotone cubic drawn on screen,
/// so between two points it stays within their values and never invents a peak or a dip. <see cref="Step"/> holds each
/// value until the next point, then rises or falls to it.</summary>
public enum LineCurve
{
    /// <summary>Straight segments from point to point.</summary>
    Linear,
    /// <summary>A monotone cubic through the points.</summary>
    Smooth,
    /// <summary>Level runs that rise or fall at each point.</summary>
    Step
}
/// <summary><see cref="Fade"/> shades an area from the series colour at the top of the plot to nothing at its baseline,
/// and a column from its colour at the baseline to a lighter tint at its far end.</summary>
public enum AreaFill
{
    /// <summary>One even tint.</summary>
    Flat,
    /// <summary>A gradient from the series colour.</summary>
    Fade
}
/// <summary>The marks on a line, area or scatter series. <see cref="Auto"/> draws each kind's own; <see cref="None"/>
/// draws nothing visible but keeps every point a focusable, labelled mark with an invisible target.</summary>
public enum MarkerStyle
{
    /// <summary>The kind's own markers. In the refined finish a line's or area's appear when its point is hovered or focused.</summary>
    Auto,
    /// <summary>No visible marker.</summary>
    None,
    /// <summary>Rings in the series colour, filled with the background.</summary>
    Hollow,
    /// <summary>Solid dots in the series colour.</summary>
    Filled
}
/// <summary>The edge the main Y axis is labelled on.</summary>
public enum AxisSide
{
    /// <summary>The left edge, the default.</summary>
    Left,
    /// <summary>The right edge.</summary>
    Right
}
/// <summary><see cref="Ends"/> labels only the lowest and highest tick of the main Y axis; every tick keeps its gridline.</summary>
public enum TickLabels
{
    /// <summary>Every tick carries its value, the default.</summary>
    All,
    /// <summary>Only the lowest and the highest tick do.</summary>
    Ends
}
/// <summary>A colour a gradient takes at <paramref name="Value"/>, measured on the axis of the series it colours.</summary>
/// <param name="Value">The value at which the stroke takes this colour.</param>
/// <param name="Color">A <c>#RRGGBB</c> colour.</param>
public sealed record ColorStop(double Value, string Color);

/// <summary>Null Y is a missing observation, never an implicit zero. Size encodes bubble area.</summary>
/// <param name="X">Where the point stands on a continuous X axis, in Unix milliseconds on a time axis. Column, bar and stacked
/// column charts place points by their order instead.</param>
/// <param name="Y">The value. Null is a missing observation: a gap in a line or area, and left out elsewhere.</param>
/// <param name="Label">The point's category on a category chart; elsewhere it names the point in its tooltip and the data
/// table in place of its X.</param>
/// <param name="Size">A bubble's area, on one scale across every series. Other kinds ignore it.</param>
public sealed record ChartPoint(double X, double? Y, string? Label = null, double Size = 1)
{
    /// <summary>Prices. All four are required of the series a candlestick or OHLC chart draws as candles or bars, and
    /// ignored everywhere else.</summary>
    public double? Open { get; init; }
    /// <summary>The highest price, or the upper bound of a band point.</summary>
    public double? High { get; init; }
    /// <summary>The lowest price, or the lower bound of a band point.</summary>
    public double? Low { get; init; }
    /// <summary>The closing price. A candle carries it as its Y too.</summary>
    public double? Close { get; init; }
    /// <summary>This point's mark in its own colour, ahead of a zone colour and the series colour: a column, bar,
    /// scatter or bubble mark, a donut slice, or a line or area marker together with the segment that starts from it.
    /// Kinds whose colours mean something else — direction, value or a distribution — refuse it, as do stacked columns,
    /// whose colours tell the stacked series apart.</summary>
    public string? Color { get; init; }

    /// <summary>A candle or OHLC bar, its Y the close. High must be the highest of the four prices and low the lowest.</summary>
    public static ChartPoint Candle(double x, double open, double high, double low, double close, string? label = null) =>
        new(x, close, label) { Open = open, High = high, Low = low, Close = close };
    /// <summary>A band point: Y is the central value, Low and High are the interval bounds.</summary>
    public static ChartPoint Interval(double x, double? y, double low, double high, string? label = null) =>
        new(x, y, label) { Low = low, High = high };
    /// <summary>A raw observation for histogram and box charts, which read values from Y and ignore X.</summary>
    public static ChartPoint Observation(double value) => new(value, value);
}
/// <summary>One named set of points, drawn as the chart's kind or as a <see cref="Kind"/> of its own.</summary>
/// <param name="Name">Names the series in the legend, its tooltips, the data table and the CSV export.</param>
/// <param name="Points">Its observations. Lines, areas, bands, candlesticks and OHLC bars need them in X order.</param>
/// <param name="Color">A <c>#RRGGBB</c> colour. Null takes the style's series colour at the series' position.</param>
public sealed record ChartSeries(string Name, IReadOnlyList<ChartPoint> Points, string? Color = null)
{
    /// <summary>Measure this series against the right-hand axis instead of the left, for a series in
    /// different units. At least one series in each pane must stay on the left.</summary>
    public bool Secondary { get; init; }
    /// <summary>Draws a least-squares line through this series. Fitted in the space each axis draws in,
    /// so it stays straight on screen; a series with no spread in X draws none.</summary>
    public bool Trend { get; init; }
    /// <summary>Box charts only. A five-number summary computed elsewhere, such as in a warehouse, drawn as
    /// given instead of one computed from points, so a series that carries one has no points. Whiskers and
    /// outliers stand where the summary puts them and are not checked against Tukey's fences, so a host's own
    /// rule — the minimum and maximum, or the 5th and 95th percentiles — is drawn as that rule.</summary>
    public BoxSummary? Summary { get; init; }
    /// <summary>Colours this series by the zone each value falls in, and names the zone in each mark's label. A line or
    /// area stroke is split where it crosses a bound, so each piece changes colour exactly at the threshold; its fill
    /// keeps the series colour. Applies to series drawn as lines, areas, scatter points, bubbles, columns and bars.</summary>
    public ZoneScale? Zones { get; init; }
    /// <summary>Draws this series as a line, area, column, scatter or band instead of the chart's kind, so fitness lines
    /// can stand over daily stress columns. The chart's kind still lays out X: line, area, scatter, bubble and band charts
    /// place every series along a continuous axis, and column charts by category. A candlestick or OHLC chart draws the one
    /// series that names no kind as candles or bars, and takes others beside it, such as a moving average or volume. Null
    /// draws the chart's kind.</summary>
    public ChartKind? Kind { get; init; }
    /// <summary>Dashes a line or area stroke from this X onward, such as planned workouts projected forward. The stroke is
    /// split exactly where it reaches the X, and each mark from there on is named projected; markers and fill are drawn
    /// as before.</summary>
    public double? ProjectedFrom { get; init; }
    /// <summary>The pane this series is drawn in, counted from the top. Pane 0 is the main plot, set up by the spec's own
    /// Y properties; pane k above 0 is set up by <see cref="ChartSpec.Panes"/>[k - 1]. Every pane shares the X axis.</summary>
    public int Pane { get; init; }
    /// <summary>The width of a line, area or band stroke, from 0.5 to 12 pixels. Null draws 2.5.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>How a line or area runs between its points, its fill and any zone colours or projection following it. A
    /// missing value still breaks it.</summary>
    public LineCurve Curve { get; init; }
    /// <summary>Fades an area or a column series instead of filling it flat.</summary>
    public AreaFill Fill { get; init; }
    /// <summary>Colours a line or area stroke and its markers continuously by value, each stop landing at its value's height
    /// on the series' own axis, logarithmic or reversed included. Stops rise strictly, at least two of them; a series takes
    /// this or <see cref="Zones"/>, not both. Labels are unchanged, because they already read the value.</summary>
    public IReadOnlyList<ColorStop>? Gradient { get; init; }
    /// <summary>The markers on a line, area or scatter series.</summary>
    public MarkerStyle Markers { get; init; }
    /// <summary>Draws the last point of a line or area larger, with a soft ring round it, as phone apps mark the latest
    /// reading. It shows even when the other markers are hidden.</summary>
    public bool HighlightLast { get; init; }
    /// <summary>Writes each column's or bar's value just past its far end, in its axis's format. A label that would not fit
    /// within its column's width, or within the plot beside a bar, is left out.</summary>
    public bool ValueLabels { get; init; }

    /// <summary>A series from your own objects, in the order given: <paramref name="x"/> and <paramref name="y"/> read each
    /// item's position and value, and <paramref name="label"/>, if given, its label.</summary>
    public static ChartSeries From<T>(string name, IEnumerable<T> items,
        Func<T, double> x, Func<T, double?> y, Func<T, string?>? label = null) =>
        new(name, items.Select(item => new ChartPoint(x(item), y(item), label?.Invoke(item))).ToArray());
}

/// <summary>
/// Everything about one chart: its data, its kind, its axes and its look. It is immutable: build one with an object
/// initializer and change it with <c>with</c>. <see cref="ChartSvg.Render"/> checks it against
/// <see cref="ChartValidation"/> and draws it as SVG, and the same spec serializes as the HTTP API's JSON.
/// </summary>
public sealed record ChartSpec
{
    /// <summary>The heading. With <see cref="Description"/> it is the drawing's accessible name.</summary>
    public string Title { get; init; } = "Untitled chart";
    /// <summary>The line under the title, read after it as part of the accessible name.</summary>
    public string Description { get; init; } = "";
    /// <summary>A line at the foot of the chart saying where the data came from.</summary>
    public string Source { get; init; } = "";
    /// <summary>Lays out X for every series, and draws each series that names no kind of its own.</summary>
    public ChartKind Kind { get; init; } = ChartKind.Line;
    /// <summary>The preset the chart draws with when it sets no <see cref="Style"/>.</summary>
    public ChartTheme Theme { get; init; }
    /// <summary>A host application's colours and typeface. When set it replaces <see cref="Theme"/>.</summary>
    public ChartStyle? Style { get; init; }
    /// <summary>Time X values are Unix milliseconds UTC. Log axes are base 10 and require positive values.</summary>
    public AxisKind XAxis { get; init; } = AxisKind.Linear;
    /// <summary>The zone a time axis reads its calendar in, such as <c>America/New_York</c>. Null keeps it in UTC.</summary>
    public string? TimeZone { get; init; }
    /// <summary>Leaves the weekends out of a time axis, so trading days sit side by side. Counted in <see cref="TimeZone"/>.</summary>
    public bool SkipWeekends { get; init; }
    /// <summary>Further spans a time axis leaves out, such as market holidays. Use <see cref="TimeAxis.Day"/> for one.</summary>
    public IReadOnlyList<TimeSkip> TimeSkips { get; init; } = [];
    /// <summary>The main plot's left-hand axis, linear or logarithmic. A time axis is refused on Y.</summary>
    public AxisKind YAxis { get; init; } = AxisKind.Linear;
    /// <summary>The main plot's right-hand axis, which measures the series marked <see cref="ChartSeries.Secondary"/>.</summary>
    public AxisKind Y2Axis { get; init; } = AxisKind.Linear;
    /// <summary>How each axis writes its values, in ticks, tooltips and the data table. Duration reads values as
    /// seconds and Compact writes 1.2k; a time X axis keeps <see cref="ValueFormat.Number"/>. CSV keeps raw numbers.</summary>
    public ValueFormat XFormat { get; init; }
    /// <summary>How the main plot's left-hand axis writes its values, as <see cref="XFormat"/> describes.</summary>
    public ValueFormat YFormat { get; init; }
    /// <summary>How the main plot's right-hand axis writes its values, as <see cref="XFormat"/> describes.</summary>
    public ValueFormat Y2Format { get; init; }
    /// <summary>Puts the smallest value at the top, so a faster pace — a smaller number — sits higher. Kinds drawn
    /// from a zero baseline refuse it.</summary>
    public bool YReversed { get; init; }
    /// <summary>Puts the smallest value at the top of the main plot's right-hand axis, as <see cref="YReversed"/> does on the left.</summary>
    public bool Y2Reversed { get; init; }
    /// <summary>Labels the main Y axis of every pane on the right, as phone apps do. A chart with a secondary series keeps it
    /// on the left, because the right edge is taken; a horizontal bar chart, whose value axis runs along the bottom, and the
    /// charts without a Y axis refuse it.</summary>
    public AxisSide YAxisSide { get; init; }
    /// <summary>Which ticks of the main Y axis carry a label. Gridlines stay at every tick; a secondary axis labels all of its own.</summary>
    public TickLabels YTickLabels { get; init; }
    /// <summary>The data: at most 32 series and 100,000 points in all.</summary>
    public IReadOnlyList<ChartSeries> Series { get; init; } = [];
    /// <summary>Names the X axis.</summary>
    public string XLabel { get; init; } = "";
    /// <summary>Names the main plot's left-hand axis.</summary>
    public string YLabel { get; init; } = "";
    /// <summary>Names the main plot's right-hand axis, which appears when one of its series is marked secondary.</summary>
    public string Y2Label { get; init; } = "";
    /// <summary>The drawing's width in SVG units, from 320 to 4096. The SVG scales to the width of its container, and its text
    /// is 12 units high, so a chart shown at the width it is drawn shows its text at 12 pixels. The Blazor component's
    /// <c>FitWidth</c> draws it at the width it is shown.</summary>
    public int Width { get; init; } = 900;
    /// <summary>The drawing's height in SVG units, from 240 to 2160. The SVG keeps this proportion to <see cref="Width"/> as it scales.</summary>
    public int Height { get; init; } = 420;
    /// <summary>Stretches the value axis to include zero. Kinds drawn from a zero baseline always include it.</summary>
    public bool IncludeZero { get; init; }
    /// <summary>The lowest value the X axis shows. Null fits the data.</summary>
    public double? XMin { get; init; }
    /// <summary>The highest value the X axis shows. Null fits the data.</summary>
    public double? XMax { get; init; }
    /// <summary>The bottom of the main plot's left-hand axis. Null fits the data; a kind drawn from zero refuses a bound that
    /// leaves zero out.</summary>
    public double? YMin { get; init; }
    /// <summary>The top of the main plot's left-hand axis. Null fits the data.</summary>
    public double? YMax { get; init; }
    /// <summary>The bottom of the main plot's right-hand axis. Null fits its series.</summary>
    public double? Y2Min { get; init; }
    /// <summary>The top of the main plot's right-hand axis. Null fits its series.</summary>
    public double? Y2Max { get; init; }
    /// <summary>The most points a line or area draws for each unbroken run of points. Longer runs are thinned by keeping
    /// each bucket's lowest and highest point, so peaks survive, and every point drawn keeps its original index.</summary>
    public int MaxRenderedPoints { get; init; } = 1200;
    /// <summary>Histogram bin count. Null selects a count from the data.</summary>
    public int? Bins { get; init; }
    /// <summary>Scatter only. Set a cell count across the plot to draw one shaded cell per occupied
    /// region instead of one mark per observation. Null draws every point, which is the default.</summary>
    public int? DensityCells { get; init; }
    /// <summary>Lighter lines between the labelled ticks. Off by default; a time axis never takes them.</summary>
    public bool MinorGridlines { get; init; }
    /// <summary>Reference lines and bands drawn behind the data: a Y reference on the main plot, an X one through every pane.</summary>
    public IReadOnlyList<ChartAnnotation> Annotations { get; init; } = [];
    /// <summary>Shades each zone as a band on the main plot's primary value axis, behind the data and any annotations, named
    /// with its range. The open bottom zone and the unbounded top one stop at the plot edge, and the bands never
    /// widen the axis. Applies wherever Y annotations do.</summary>
    public ZoneScale? YZones { get; init; }
    /// <summary>Plots stacked under the main one, sharing its X axis, each with Y axes of its own, such as volume under
    /// prices. The main plot is pane 0 and takes this spec's Y properties; <c>Panes[k - 1]</c> sets up pane k, which holds
    /// the series whose <see cref="ChartSeries.Pane"/> is k. Line, area, scatter, bubble, band, candlestick and OHLC charts
    /// take them, at most three. Empty draws one plot.</summary>
    public IReadOnlyList<ChartPane> Panes { get; init; } = [];
}

/// <summary>
/// A plot stacked under the main one: pane k of a chart is <see cref="ChartSpec.Panes"/>[k - 1]. It shares the chart's X
/// axis and has Y axes of its own, and each property means for it what the spec's property of the same name means for
/// the main plot. Annotations stay on the main plot, except that an X annotation runs through every pane.
/// </summary>
public sealed record ChartPane
{
    /// <summary>Names the pane's left-hand axis, as <see cref="ChartSpec.YLabel"/> names the main plot's.</summary>
    public string Label { get; init; } = "";
    /// <summary>The pane's height beside the main plot's, which weighs 1.</summary>
    public double Weight { get; init; } = .5;
    /// <summary>The pane's left-hand axis, linear or logarithmic.</summary>
    public AxisKind YAxis { get; init; } = AxisKind.Linear;
    /// <summary>The bottom of the pane's left-hand axis. Null fits the pane's data.</summary>
    public double? YMin { get; init; }
    /// <summary>The top of the pane's left-hand axis. Null fits the pane's data.</summary>
    public double? YMax { get; init; }
    /// <summary>How the pane's left-hand axis writes its values.</summary>
    public ValueFormat YFormat { get; init; }
    /// <summary>Puts the smallest value at the top of the pane's left-hand axis.</summary>
    public bool YReversed { get; init; }
    /// <summary>Shades each zone as a band behind this pane's data.</summary>
    public ZoneScale? YZones { get; init; }
    /// <summary>Names the pane's right-hand axis, which appears when one of its series is secondary.</summary>
    public string Y2Label { get; init; } = "";
    /// <summary>The pane's right-hand axis, linear or logarithmic.</summary>
    public AxisKind Y2Axis { get; init; } = AxisKind.Linear;
    /// <summary>The bottom of the pane's right-hand axis. Null fits its series.</summary>
    public double? Y2Min { get; init; }
    /// <summary>The top of the pane's right-hand axis. Null fits its series.</summary>
    public double? Y2Max { get; init; }
    /// <summary>How the pane's right-hand axis writes its values.</summary>
    public ValueFormat Y2Format { get; init; }
    /// <summary>Puts the smallest value at the top of the pane's right-hand axis.</summary>
    public bool Y2Reversed { get; init; }
}

/// <summary>
/// The data axis a reference marks. Y is the value axis wherever the chart draws it: up the side, or along the bottom
/// of a horizontal bar chart, where a Y reference therefore stands upright. X is the axis points are placed along by
/// their X value; category charts, horizontal bars included, place their bars by index instead, so they refuse it.
/// </summary>
public enum AnnotationAxis
{
    /// <summary>A value along X, marked through every pane.</summary>
    X,
    /// <summary>A value on the main plot's value axis.</summary>
    Y
}

/// <summary>
/// A reference drawn behind the data: a line at <paramref name="From"/>, or a band when <see cref="To"/>
/// is set. Values are in data coordinates, so an annotation pans and zooms with the chart.
/// </summary>
/// <param name="Axis">The axis the value is read on.</param>
/// <param name="From">The value marked, or where a band starts. Unix milliseconds on a time axis.</param>
public sealed record ChartAnnotation(AnnotationAxis Axis, double From)
{
    /// <summary>The far edge of a band. Null draws a line.</summary>
    public double? To { get; init; }
    /// <summary>Shown with the value. Null shows the value alone.</summary>
    public string? Label { get; init; }
    /// <summary>Defaults to the style's muted colour.</summary>
    public string? Color { get; init; }
    /// <summary>Draws a reference line dashed, the default, or solid.</summary>
    public bool Dashed { get; init; } = true;
}

/// <summary>A node of a graph.</summary>
/// <param name="Id">What edges call the node by: nonempty and unique within the graph.</param>
/// <param name="Label">The text drawn with the node and read as its accessible name.</param>
/// <param name="Color">A <c>#RRGGBB</c> colour. Null takes the style's series colours.</param>
public sealed record GraphNode(string Id, string Label, string? Color = null);
/// <summary>A directed edge, drawn with an arrowhead at its target.</summary>
/// <param name="Source">The <see cref="GraphNode.Id"/> it leaves.</param>
/// <param name="Target">The <see cref="GraphNode.Id"/> it reaches. The same as the source draws a loop.</param>
/// <param name="Label">Text written along the edge. Null writes none.</param>
public sealed record GraphEdge(string Source, string Target, string? Label = null);
/// <summary>How a graph's nodes are placed.</summary>
public enum GraphLayout
{
    /// <summary>Evenly round a circle, in the order given. Takes cycles of any length.</summary>
    Circular,
    /// <summary>In levels from left to right by longest path, each level ordered to cut edge crossings. Refuses cycles longer
    /// than a self-loop.</summary>
    Layered
}
/// <summary>A network graph: nodes, the directed edges between them and how to lay them out, drawn by
/// <see cref="GraphEngine.Render"/>. At most 250 nodes and 2,000 edges.</summary>
public sealed record GraphSpec
{
    /// <summary>The heading and the drawing's accessible name.</summary>
    public string Title { get; init; } = "Network";
    /// <summary>The nodes, at most 250.</summary>
    public IReadOnlyList<GraphNode> Nodes { get; init; } = [];
    /// <summary>The edges, at most 2,000, each between two of the nodes.</summary>
    public IReadOnlyList<GraphEdge> Edges { get; init; } = [];
    /// <summary>How the nodes are placed: layered by default.</summary>
    public GraphLayout Layout { get; init; } = GraphLayout.Layered;
    /// <summary>The preset the graph draws with when it sets no <see cref="Style"/>.</summary>
    public ChartTheme Theme { get; init; }
    /// <summary>A host application's colours and typeface. When set it replaces <see cref="Theme"/>.</summary>
    public ChartStyle? Style { get; init; }
    /// <summary>The drawing's width in SVG units, from 320 to 4096.</summary>
    public int Width { get; init; } = 900;
    /// <summary>The drawing's height in SVG units, from 240 to 2160.</summary>
    public int Height { get; init; } = 460;
}
/// <summary>Where a layout puts the centre of the node <paramref name="Id"/>, in the drawing's coordinates.</summary>
/// <param name="Id">The node's <see cref="GraphNode.Id"/>.</param>
/// <param name="X">Its centre's distance from the drawing's left edge.</param>
/// <param name="Y">Its centre's distance from the drawing's top edge.</param>
public sealed record NodePosition(string Id, double X, double Y);
/// <summary>A point in a graph drawing's coordinates, from its top left corner.</summary>
/// <param name="X">The distance from the left edge.</param>
/// <param name="Y">The distance from the top edge.</param>
public sealed record GraphPoint(double X, double Y);
/// <summary>Polyline for the edge at <paramref name="Edge"/>: endpoints plus one bend for each level a long edge spans.</summary>
/// <param name="Edge">The edge's index in <see cref="GraphSpec.Edges"/>.</param>
/// <param name="Points">Its points from source to target, in the drawing's coordinates.</param>
public sealed record EdgeRoute(int Edge, IReadOnlyList<GraphPoint> Points);
/// <summary>The point a reader selected in the Blazor component, as indices into the original spec, whatever is hidden or zoomed.</summary>
/// <param name="SeriesIndex">The series' index in <see cref="ChartSpec.Series"/>.</param>
/// <param name="PointIndex">The point's index in that series' <see cref="ChartSeries.Points"/>.</param>
public sealed record PointSelection(int SeriesIndex, int PointIndex);
