namespace Lumen.Charts;

public enum ChartKind { Line, Area, Scatter, Bubble, Column, Bar, StackedColumn, Donut, Heatmap, Radar, Candlestick, Band, Histogram, Box }
public enum ChartTheme { Light, Dark }

/// <summary>Null Y is a missing observation, never an implicit zero. Size encodes bubble area.</summary>
public sealed record ChartPoint(double X, double? Y, string? Label = null, double Size = 1)
{
    /// <summary>Candlestick prices. All four are required by that chart kind and ignored by every other one.</summary>
    public double? Open { get; init; }
    public double? High { get; init; }
    public double? Low { get; init; }
    public double? Close { get; init; }

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

    public static ChartSeries From<T>(string name, IEnumerable<T> items,
        Func<T, double> x, Func<T, double?> y, Func<T, string?>? label = null) =>
        new(name, items.Select(item => new ChartPoint(x(item), y(item), label?.Invoke(item))).ToArray());
}

public sealed record ChartSpec
{
    public string Title { get; init; } = "Untitled chart";
    public string Description { get; init; } = "";
    public string Source { get; init; } = "";
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
}

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
