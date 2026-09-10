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
    /// <summary>Time X values are Unix milliseconds UTC. Log axes are base 10 and require positive values.</summary>
    public AxisKind XAxis { get; init; } = AxisKind.Linear;
    public AxisKind YAxis { get; init; } = AxisKind.Linear;
    public IReadOnlyList<ChartSeries> Series { get; init; } = [];
    public string XLabel { get; init; } = "";
    public string YLabel { get; init; } = "";
    public int Width { get; init; } = 900;
    public int Height { get; init; } = 420;
    public bool IncludeZero { get; init; }
    public double? XMin { get; init; }
    public double? XMax { get; init; }
    public double? YMin { get; init; }
    public double? YMax { get; init; }
    public int MaxRenderedPoints { get; init; } = 1200;
    /// <summary>Histogram bin count. Null selects a count from the data.</summary>
    public int? Bins { get; init; }
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
    public int Width { get; init; } = 900;
    public int Height { get; init; } = 460;
}
public sealed record NodePosition(string Id, double X, double Y);
public sealed record GraphPoint(double X, double Y);
/// <summary>Polyline for the edge at <paramref name="Edge"/>: endpoints plus one bend for each level a long edge spans.</summary>
public sealed record EdgeRoute(int Edge, IReadOnlyList<GraphPoint> Points);
public sealed record PointSelection(int SeriesIndex, int PointIndex);
