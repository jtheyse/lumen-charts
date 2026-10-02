namespace Lumen.Charts;

public enum ChartKind { Line, Area, Scatter, Bubble, Column, Bar, StackedColumn, Donut, Heatmap, Radar, Candlestick, Band, Histogram, Box, Violin, Ohlc }
public enum ChartTheme { Light, Dark }

/// <summary>Null Y is a missing observation, never an implicit zero. Size encodes bubble area.</summary>
public sealed record ChartPoint(double X, double? Y, string? Label = null, double Size = 1)
{
    /// <summary>Prices. All four are required by the candlestick and OHLC kinds and ignored by every other one.</summary>
    public double? Open { get; init; }
    public double? High { get; init; }
    public double? Low { get; init; }
    public double? Close { get; init; }
    /// <summary>This point's mark in its own colour, ahead of a zone colour and the series colour: a column, bar,
    /// scatter or bubble mark, a donut slice, or a line or area marker together with the segment that starts from it.
    /// Kinds whose colours mean something else — direction, value or a distribution — refuse it, as do stacked columns,
    /// whose colours tell the stacked series apart.</summary>
    public string? Color { get; init; }

    public static ChartPoint Candle(double x, double open, double high, double low, double close, string? label = null) =>
        new(x, close, label) { Open = open, High = high, Low = low, Close = close };
    /// <summary>A band point: Y is the central value, Low and High are the interval bounds.</summary>
    public static ChartPoint Interval(double x, double? y, double low, double high, string? label = null) =>
        new(x, y, label) { Low = low, High = high };
    /// <summary>A raw observation for histogram and box charts, which read values from Y and ignore X.</summary>
    public static ChartPoint Observation(double value) => new(value, value);
}
public sealed record ChartSeries(string Name, IReadOnlyList<ChartPoint> Points, string? Color = null)
{
    /// <summary>Measure this series against the right-hand axis instead of the left, for a series in
    /// different units. At least one series must stay on the left.</summary>
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
    /// place every series along a continuous axis, and column charts by category. Null draws the chart's kind.</summary>
    public ChartKind? Kind { get; init; }
    /// <summary>Dashes a line or area stroke from this X onward, such as planned workouts projected forward. The stroke is
    /// split exactly where it reaches the X, and each mark from there on is named projected; markers and fill are drawn
    /// as before.</summary>
    public double? ProjectedFrom { get; init; }

    public static ChartSeries From<T>(string name, IEnumerable<T> items,
        Func<T, double> x, Func<T, double?> y, Func<T, string?>? label = null) =>
        new(name, items.Select(item => new ChartPoint(x(item), y(item), label?.Invoke(item))).ToArray());
}

public sealed record ChartSpec
{
    public string Title { get; init; } = "Untitled chart";
    public string Description { get; init; } = "";
    public string Source { get; init; } = "";
    /// <summary>Lays out X for every series, and draws each series that names no kind of its own.</summary>
    public ChartKind Kind { get; init; } = ChartKind.Line;
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
    public AxisKind YAxis { get; init; } = AxisKind.Linear;
    public AxisKind Y2Axis { get; init; } = AxisKind.Linear;
    /// <summary>How each axis writes its values, in ticks, tooltips and the data table. Duration reads values as
    /// seconds and Compact writes 1.2k; a time X axis keeps <see cref="ValueFormat.Number"/>. CSV keeps raw numbers.</summary>
    public ValueFormat XFormat { get; init; }
    public ValueFormat YFormat { get; init; }
    public ValueFormat Y2Format { get; init; }
    /// <summary>Puts the smallest value at the top, so a faster pace — a smaller number — sits higher. Kinds drawn
    /// from a zero baseline refuse it.</summary>
    public bool YReversed { get; init; }
    public bool Y2Reversed { get; init; }
    public IReadOnlyList<ChartSeries> Series { get; init; } = [];
    public string XLabel { get; init; } = "";
    public string YLabel { get; init; } = "";
    /// <summary>Names the right-hand axis, which appears when a series is marked secondary.</summary>
    public string Y2Label { get; init; } = "";
    public int Width { get; init; } = 900;
    public int Height { get; init; } = 420;
    public bool IncludeZero { get; init; }
    public double? XMin { get; init; }
    public double? XMax { get; init; }
    public double? YMin { get; init; }
    public double? YMax { get; init; }
    public double? Y2Min { get; init; }
    public double? Y2Max { get; init; }
    public int MaxRenderedPoints { get; init; } = 1200;
    /// <summary>Histogram bin count. Null selects a count from the data.</summary>
    public int? Bins { get; init; }
    /// <summary>Scatter only. Set a cell count across the plot to draw one shaded cell per occupied
    /// region instead of one mark per observation. Null draws every point, which is the default.</summary>
    public int? DensityCells { get; init; }
    /// <summary>Lighter lines between the labelled ticks. Off by default; a time axis never takes them.</summary>
    public bool MinorGridlines { get; init; }
    /// <summary>Reference lines and bands drawn behind the data.</summary>
    public IReadOnlyList<ChartAnnotation> Annotations { get; init; } = [];
    /// <summary>Shades each zone as a band on the primary value axis, behind the data and any annotations, named
    /// with its range. The open bottom zone and the unbounded top one stop at the plot edge, and the bands never
    /// widen the axis. Applies wherever Y annotations do.</summary>
    public ZoneScale? YZones { get; init; }
}

/// <summary>
/// The data axis a reference marks. Y is the value axis wherever the chart draws it: up the side, or along the bottom
/// of a horizontal bar chart, where a Y reference therefore stands upright. X is the axis points are placed along by
/// their X value; category charts, horizontal bars included, place their bars by index instead, so they refuse it.
/// </summary>
public enum AnnotationAxis { X, Y }

/// <summary>
/// A reference drawn behind the data: a line at <paramref name="From"/>, or a band when <see cref="To"/>
/// is set. Values are in data coordinates, so an annotation pans and zooms with the chart.
/// </summary>
public sealed record ChartAnnotation(AnnotationAxis Axis, double From)
{
    /// <summary>The far edge of a band. Null draws a line.</summary>
    public double? To { get; init; }
    /// <summary>Shown with the value. Null shows the value alone.</summary>
    public string? Label { get; init; }
    /// <summary>Defaults to the style's muted colour.</summary>
    public string? Color { get; init; }
    public bool Dashed { get; init; } = true;
}

public sealed record GraphNode(string Id, string Label, string? Color = null);
public sealed record GraphEdge(string Source, string Target, string? Label = null);
public enum GraphLayout { Circular, Layered }
public sealed record GraphSpec
{
    public string Title { get; init; } = "Network";
    public IReadOnlyList<GraphNode> Nodes { get; init; } = [];
    public IReadOnlyList<GraphEdge> Edges { get; init; } = [];
    public GraphLayout Layout { get; init; } = GraphLayout.Layered;
    public ChartTheme Theme { get; init; }
    /// <summary>A host application's colours and typeface. When set it replaces <see cref="Theme"/>.</summary>
    public ChartStyle? Style { get; init; }
    public int Width { get; init; } = 900;
    public int Height { get; init; } = 460;
}
public sealed record NodePosition(string Id, double X, double Y);
public sealed record GraphPoint(double X, double Y);
/// <summary>Polyline for the edge at <paramref name="Edge"/>: endpoints plus one bend for each level a long edge spans.</summary>
public sealed record EdgeRoute(int Edge, IReadOnlyList<GraphPoint> Points);
public sealed record PointSelection(int SeriesIndex, int PointIndex);
