namespace Lumen.Charts;

/// <summary>The kind of chart, which decides how X is laid out and how a series that names no kind of its own is drawn.
/// Line, area, scatter, bubble, band, range, candlestick and OHLC charts place points along a continuous X axis by their X;
/// column, bar, stacked column, donut, heatmap and radar charts place them by category and show their labels; gauge and
/// ring charts draw one value a series round an arc and have no X axis; a timeline draws spans along a continuous X axis,
/// one lane a series; a calendar draws one series as a grid of days, each coloured by its value; and blocks draw spans along a
/// continuous X axis, each as wide as it runs and as tall as its value.</summary>
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
    Ohlc,
    /// <summary>One score on an open arc, as recovery and readiness scores are drawn: one series of one point, whose Y is the
    /// score and whose label is the caption written under it. The scale runs from <see cref="ChartSpec.YMin"/> to
    /// <see cref="ChartSpec.YMax"/>, 0 to 100 unless set, round <see cref="ChartSpec.GaugeSweep"/> degrees centred at the top.
    /// <see cref="ChartSpec.YZones"/> tint the track and colour the score by its zone, and a Y annotation marks a target as a
    /// tick across the arc.</summary>
    Gauge,
    /// <summary>Concentric progress rings, as activity rings are drawn: one series per ring, outermost first, at most six, each
    /// of one nonnegative point measured against the series' <see cref="ChartSeries.Goal"/>. A ring past its goal keeps going
    /// round over itself, up to three times.</summary>
    Ring,
    /// <summary>A state timeline, as sleep stages are drawn in a hypnogram: one series per state, drawn as a lane, top to
    /// bottom in series order, each point a span from its X to its <see cref="ChartPoint.XEnd"/> made with
    /// <see cref="ChartPoint.Span"/>. Spans are rounded bars in their lane's colour, joined across lanes by thin connectors
    /// where one ends as the next begins, unless <see cref="ChartSpec.TimelineConnectors"/> is off. The legend names each
    /// state with its total time and its share.</summary>
    Timeline,
    /// <summary>Floating range bars: each point a capsule from its <see cref="ChartPoint.Low"/> to its
    /// <see cref="ChartPoint.High"/>, made with <see cref="ChartPoint.Interval"/>, with a dot at its Y when Y is set, as daily
    /// heart-rate ranges and bedtime-to-wake sleep timing are drawn. It has no zero baseline, so it takes reversed and
    /// logarithmic axes. As a series' own <see cref="ChartSeries.Kind"/> it draws beside lines on a continuous chart, or in its
    /// category's slot on a column chart.</summary>
    Range,
    /// <summary>A training calendar: one series of days on a time X axis, each point's X a moment on the day it counts for, in
    /// <see cref="ChartSpec.TimeZone"/>, and its Y the day's value; several points on one day are added together, and a day whose
    /// total is zero or missing is a day without activity, drawn as an empty cell in the grid colour and not focusable. The days
    /// are laid out by <see cref="ChartSpec.CalendarLayout"/> from the first day of the data to the last, weeks starting on
    /// <see cref="ChartSpec.WeekStart"/>, and drawn as <see cref="ChartSpec.CalendarCell"/>. Each day takes the colour of its
    /// value's zone in <see cref="ChartSpec.YZones"/>, or else a colour on a ramp across the days' values that starts a third of the
    /// way from an empty cell's <see cref="ChartStyle.Grid"/> colour to the style's <see cref="ChartStyle.HeatmapHigh"/> and ends
    /// at it, as a contribution grid steps up from its empty cell, so the quietest day stands apart from a rest day on any style;
    /// the key under the grid shows the zones or the ramp. An X annotation outlines its day's cell.</summary>
    Calendar,
    /// <summary>Variable-width blocks, as Strava draws laps and TrainingPeaks and Zwift draw a structured workout: each point a
    /// block from its X to its <see cref="ChartPoint.XEnd"/>, made with <see cref="ChartPoint.Block"/>, standing on the bottom edge
    /// of its plot and rising to its Y. On an axis that includes zero a block rises from zero, as a workout's power target does; on
    /// a reversed pace axis it rises from the slowest pace up to its own, as a lap does. An axis fitted to the data reaches a little
    /// past the lowest block, so that block keeps a height. Neighbours that touch are parted by a hairline, the far end of each block
    /// is rounded by the style's <see cref="ChartStyle.BarRadius"/> or 4 pixels, up to 6, and a block takes its point's colour, its
    /// value's zone in <see cref="ChartSeries.Zones"/> or its series' colour. A block whose value stands above the bottom of its axis
    /// is drawn at least 2 pixels tall, so a bin of one among hundreds still shows; one at the bottom, such as a count of none, draws
    /// nothing visible but keeps its name and its focus. Blocks in one series cannot overlap. As a series' own
    /// <see cref="ChartSeries.Kind"/> they draw in the column layer of a continuous chart, under its lines.</summary>
    Blocks
}
/// <summary>How a calendar lays out its days.</summary>
public enum CalendarLayout
{
    /// <summary>The contribution grid: one column per week and one row per weekday, the months named above the first week of
    /// each and Mon, Wed and Fri named beside their rows.</summary>
    Weeks,
    /// <summary>One small grid per calendar month, seven columns and a row per week, named above, set left to right and
    /// wrapping to fit the width.</summary>
    Months
}
/// <summary>How a calendar draws each day.</summary>
public enum CalendarCell
{
    /// <summary>A rounded square filling its cell, rounded by the style's <see cref="ChartStyle.BarRadius"/> or 3 pixels.</summary>
    Square,
    /// <summary>A filled circle filling its cell.</summary>
    Dot,
    /// <summary>A circle whose area is proportional to the day's value, the largest filling its cell, over a track the size of
    /// the cell, as a training log sizes each day by its distance.</summary>
    Bubble
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
/// <summary>The trend a series draws when it sets <see cref="ChartSeries.Trend"/>. Each is computed in the space the chart draws
/// in, as the line always was, so it agrees with the marks it runs through on a logarithmic axis and on a time axis that skips
/// spans. Every one is dashed in the series colour and named for assistive technology.</summary>
public enum TrendFit
{
    /// <summary>A least-squares line across the whole plot, as <see cref="Statistics.Fit"/> computes it, named with its direction
    /// and R squared: <c>Accounts trend: rising, R squared 0.93</c>.</summary>
    Linear,
    /// <summary>A trailing average over <see cref="ChartSeries.TrendPoints"/> of the series' points in the order it lists them, which
    /// must be X order, drawn at the last point of each window: <c>HRV trend: 7-point moving average</c>. A point with a missing
    /// value takes its place in a window and adds nothing, as <see cref="Statistics.Rolling"/> has it, and the line breaks wherever
    /// fewer than half a window, rounded up, is present. It averages the positions the values are drawn at, so on a logarithmic
    /// axis it is the geometric mean.</summary>
    MovingAverage,
    /// <summary>A least-squares polynomial of <see cref="ChartSeries.TrendDegree"/>, as <see cref="Statistics.Polynomial"/> computes
    /// it, drawn across the X its observations cover and no further: <c>Throughput trend: quadratic fit, R squared 0.93</c>. A
    /// polynomial stays a polynomial of its degree under the scaling between data and pixels, so on plain axes it is the ordinary
    /// fit to the data.</summary>
    Polynomial,
    /// <summary><c>y = a·e^(b·x)</c>, as <see cref="Statistics.Exponential"/> computes it: fitted to the logarithm of each positive
    /// value against the X it is drawn at, leaving out zero and negative values, and drawn through the series' axis across the X its
    /// positive observations cover, so it is straight on a logarithmic Y axis and curves on a linear one: <c>Latency trend:
    /// exponential fit, rising, R squared 0.88</c>. Its R squared is measured on the logarithms, as Excel reports it. Fewer than
    /// two positive values draw none.</summary>
    Exponential
}
/// <summary>Which way a series counts a change as better, for <see cref="ChartSeries.ChangeColors"/>: up for points scored, down for
/// a finishing position or a pace. It follows the data, not the screen, so it reads the same on a reversed axis.</summary>
public enum ChangeColors
{
    /// <summary>No change colours: the series draws in its own colours, the default.</summary>
    None,
    /// <summary>A higher value than the one before is better, as points scored or a share price are.</summary>
    HigherIsBetter,
    /// <summary>A lower value than the one before is better, as a finishing position, a ranking or a pace is.</summary>
    LowerIsBetter
}
/// <summary>What labels a continuous X axis along the bottom: its own ticks, or the labels of the points that stand on it.</summary>
public enum TickSource
{
    /// <summary>Points' labels take the ticks' place when 1 to 24 labelled points stand in the visible range, each label cut to
    /// twelve characters; otherwise the axis's own ticks, as every chart did before 0.33.0. On a time axis the labels take their
    /// place only when none of them would be cut, since the dates are the axis's own; longer labels stay in the points' names.</summary>
    Auto,
    /// <summary>Always the axis's own ticks: numbers, or dates on a time axis. Points' labels stay in their names.</summary>
    Axis,
    /// <summary>Always the points' labels, at any count, thinned until they fit and each cut to twelve characters; the axis's own
    /// ticks only where no labelled point stands in the visible range.</summary>
    PointLabels
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
/// <summary>Which ticks of an axis carry a label: every one, the lowest and highest drawn, or none but the axis's own two ends. Every
/// tick keeps its gridline whichever is chosen; only the labels change.</summary>
public enum TickLabels
{
    /// <summary>Every tick carries its value, the default.</summary>
    All,
    /// <summary>Only the lowest and the highest tick drawn do.</summary>
    Ends,
    /// <summary>No tick does; instead the axis's two ends are labelled at their exact values, in the axis's format, whether or not a tick
    /// stands there: <see cref="ChartSpec.XMin"/> and <see cref="ChartSpec.XMax"/>, or the data's ends, along X, and the ends of the
    /// axis as it is fitted or bounded up the side. Along the bottom the first end's label starts at the plot's left edge and the
    /// last's ends at its right edge, so both stand whole under the plot.</summary>
    Bounds,
    /// <summary>No label at all: the gridlines stay, and each value is read from its mark's name, a tooltip or a shared readout, as a
    /// stack of channels whose headers give their numbers is read. Up the side only, on <see cref="ChartSpec.YTickLabels"/> and
    /// <see cref="ChartPane.YTickLabels"/>; the X axis, which every pane shares, refuses it.</summary>
    None
}
/// <summary>How a long line or area is thinned to <see cref="ChartSpec.MaxRenderedPoints"/>.</summary>
public enum SamplingMethod
{
    /// <summary>Each unbroken run longer than the budget keeps the lowest and the highest point of each of its buckets, and its first and
    /// last, so peaks and dips survive; every mark drawn is one of the series' own points. The default, and how every chart was thinned
    /// before 0.37.0.</summary>
    MinMax,
    /// <summary>Each bucket is drawn as one point at the mean X and mean Y of its points, as a ride's channels are smoothed, so a noisy
    /// stream reads as its trend rather than as a band of spikes. Series drawn as lines or areas only; every other mark, a band's
    /// outline and a trend keep <see cref="MinMax"/>.</summary>
    Average
}
/// <summary>Where a chart drawn on X and Y axes writes the name of each plot: the main plot's <see cref="ChartSpec.YLabel"/> and each
/// pane's <see cref="ChartPane.Label"/>.</summary>
public enum PaneTitlePlacement
{
    /// <summary>Up the side, read upwards beside the axis, as every chart did before 0.37.0. The default.</summary>
    Axis,
    /// <summary>As one horizontal line above each plot, starting at its left edge, cut with <c>…</c> where it is wider than the plot, in
    /// the style's text colour: a header such as <c>Heart rate · avg 148 · max 182 bpm</c>, which a stack of channels reads best by.</summary>
    Above
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
    /// <summary>The highest price, or the upper bound of a band point or a range bar.</summary>
    public double? High { get; init; }
    /// <summary>The lowest price, or the lower bound of a band point or a range bar.</summary>
    public double? Low { get; init; }
    /// <summary>Timeline charts and blocks only: where this span or block ends along X, above <see cref="X"/>, where it starts.
    /// Unix milliseconds on a time axis. Every other kind refuses it.</summary>
    public double? XEnd { get; init; }
    /// <summary>The closing price. A candle carries it as its Y too.</summary>
    public double? Close { get; init; }
    /// <summary>This point's mark in its own colour, ahead of a zone colour and the series colour: a column, bar, range, block,
    /// scatter or bubble mark, a donut slice, or a line or area marker together with the segment that starts from it.
    /// Kinds whose colours mean something else — direction, value, a state or a distribution — refuse it, as do stacked
    /// columns, whose colours tell the stacked series apart.</summary>
    public string? Color { get; init; }
    /// <summary>A short note written straight after this point's value, at most 20 characters, such as <c>/48</c> after a finishing
    /// position for the size of its field: in the muted colour at normal weight after a value label, and after the value in the
    /// mark's tooltip and accessible name and in the component's data table. A <c>Note</c> column carries it into CSV. Give it any
    /// space it needs: <c>" inside baseline"</c>. A missing value has nothing for a note to follow, so its note reaches the CSV
    /// alone. Marks named by more than one value, or by none, refuse it: candles, range bars, histograms, boxes, violins,
    /// timelines, calendars, gauges and rings.</summary>
    public string? ValueNote { get; init; }
    /// <summary>A <c>#RRGGBB</c> colour that rings this point of a line or scatter series with an enlarged marker in it, outlined in
    /// the background colour, whatever the series' markers, as a personal best is picked out of a run of results. It colours that
    /// marker alone: the line keeps its colour, and the point keeps its name. A colour is never enough on its own, so give the point a
    /// <see cref="ValueNote"/> that says why, such as <c>" · PB"</c>, which its tooltip and accessible name read after its value.
    /// Areas and every other mark refuse it, as does a density scatter, which shades cells rather than points.</summary>
    public string? Highlight { get; init; }

    /// <summary>A candle or OHLC bar, its Y the close. High must be the highest of the four prices and low the lowest.</summary>
    public static ChartPoint Candle(double x, double open, double high, double low, double close, string? label = null) =>
        new(x, close, label) { Open = open, High = high, Low = low, Close = close };
    /// <summary>A band point or a range bar: Low and High are the interval bounds, and Y is the band's central value or the
    /// range's typical value, drawn as a dot within it; a range bar without one passes null.</summary>
    public static ChartPoint Interval(double x, double? y, double low, double high, string? label = null) =>
        new(x, y, label) { Low = low, High = high };
    /// <summary>A timeline span from <paramref name="start"/> to <paramref name="end"/> along X, in the lane of the series
    /// that holds it. It has no Y. <paramref name="label"/>, if given, names it in its tooltip.</summary>
    public static ChartPoint Span(double start, double end, string? label = null) =>
        new(start, null, label) { XEnd = end };
    /// <summary>A block from <paramref name="start"/> to <paramref name="end"/> along X, standing on the bottom edge of its plot
    /// and rising to <paramref name="height"/> on its series' axis: a lap's pace, or a workout step's target. <paramref name="label"/>,
    /// if given, names it in its tooltip, such as <c>Lap 3</c>.</summary>
    public static ChartPoint Block(double start, double end, double height, string? label = null) =>
        new(start, height, label) { XEnd = end };
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
    /// <summary>Draws a trend through this series: a least-squares line, unless <see cref="TrendFit"/> chooses another. Fitted in
    /// the space each axis draws in, so a line stays straight on screen; a series with no spread in X draws none. Applies to series
    /// drawn as lines, areas, scatter points and bubbles.</summary>
    public bool Trend { get; init; }
    /// <summary>The trend <see cref="Trend"/> draws: a least-squares line across the plot, the default; a moving average over
    /// <see cref="TrendPoints"/>; a polynomial of <see cref="TrendDegree"/>; or an exponential. Set without <see cref="Trend"/>, it
    /// is refused, since it would draw nothing.</summary>
    public TrendFit TrendFit { get; init; }
    /// <summary>A <see cref="TrendFit.MovingAverage"/>'s window, in points, from 2 to 1000; 7 unless set. Set away from 7 it is
    /// refused on any other fit and without <see cref="Trend"/>.</summary>
    public int TrendPoints { get; init; } = 7;
    /// <summary>A <see cref="TrendFit.Polynomial"/>'s degree: 2, a quadratic, unless set; 3, a cubic; or 4, a quartic. Set away from
    /// 2 it is refused on any other fit and without <see cref="Trend"/>.</summary>
    public int TrendDegree { get; init; } = 2;
    /// <summary>Box charts only. A five-number summary computed elsewhere, such as in a warehouse, drawn as
    /// given instead of one computed from points, so a series that carries one has no points. Whiskers and
    /// outliers stand where the summary puts them and are not checked against Tukey's fences, so a host's own
    /// rule — the minimum and maximum, or the 5th and 95th percentiles — is drawn as that rule.</summary>
    public BoxSummary? Summary { get; init; }
    /// <summary>Colours this series by the zone each value falls in, and names the zone in each mark's label. A line or
    /// area stroke is split where it crosses a bound, so each piece changes colour exactly at the threshold; its fill
    /// keeps the series colour. Applies to series drawn as lines, areas, scatter points, bubbles, columns, bars and blocks.</summary>
    public ZoneScale? Zones { get; init; }
    /// <summary>Draws this series as a line, area, column, scatter, band, range or blocks instead of the chart's kind, so
    /// fitness lines can stand over daily stress columns and executed power over a workout's planned blocks. The chart's kind
    /// still lays out X: line, area, scatter, bubble, band, range and blocks charts place every series along a continuous axis,
    /// and column charts by category, where blocks cannot stand. A candlestick or OHLC chart draws the one series that names no
    /// kind as candles or bars, and takes others beside it, such as a moving average or volume. Null draws the chart's kind.</summary>
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
    /// this or <see cref="Zones"/>, not both. Labels are unchanged, because they already read the value. On a gauge it colours
    /// the arc along its length instead, each stop at its value's angle; a gauge takes this or <see cref="ChartSpec.YZones"/>.</summary>
    public IReadOnlyList<ColorStop>? Gradient { get; init; }
    /// <summary>The markers on a line, area or scatter series.</summary>
    public MarkerStyle Markers { get; init; }
    /// <summary>Draws the last point of a line or area larger, with a soft ring round it, as phone apps mark the latest
    /// reading. It shows even when the other markers are hidden.</summary>
    public bool HighlightLast { get; init; }
    /// <summary>Writes each column's or bar's value just past its far end, in its axis's format and the text colour, and from 0.33.0
    /// each line or scatter point's value above its marker, in its axis's format and the point's colour, its change colour where it
    /// has one, at weight 600, followed by its <see cref="ChartPoint.ValueNote"/> in the muted colour. A mark's colour need only clear
    /// 3:1 and a label is small text, so a point colour that does not clear 4.5:1 against the background writes its label in the
    /// style's <see cref="ChartStyle.Text"/> colour instead. A point's label is moved in
    /// from the plot's sides so it is never cut, and goes below its marker where above would leave the plot or meet a value label
    /// written before it. A label with no room, within its column's width, within the plot beside a bar, or above and below a
    /// point, is left out; the value stays in its mark's name.</summary>
    public bool ValueLabels { get; init; }
    /// <summary>Colours each point of a line or scatter series by how it changed from the nearest earlier point that has a value:
    /// the style's <see cref="ChartStyle.Rising"/> colour when it is better, <see cref="ChartStyle.Falling"/> when it is worse, and
    /// the series colour when it is level or has no earlier value. Better is what the setting says, not up the screen, so it holds on
    /// a reversed axis and on a scale shared with another measure alike. A point's marker and the segment that arrives at it take its
    /// colour, unlike <see cref="ChartPoint.Color"/>, which colours the segment that leaves; a gap draws no segment, but the point
    /// after it still compares with the last value before it. Each mark's name says the change in words, <c>better than the
    /// previous</c>, <c>worse than the previous</c> or <c>level with the previous</c>, so the colour is never the only cue. Applies to
    /// series drawn as lines and scatter points, in X order, and is refused beside <see cref="Zones"/>, a <see cref="Gradient"/> and
    /// point colours, which colour the same marks.</summary>
    public ChangeColors ChangeColors { get; init; }
    /// <summary>Ring charts only: the target this ring's value is measured against, so its progress is Y ÷ Goal. Positive;
    /// null means 100. The point's label, if any, is the unit both are written in, such as <c>kcal</c>.</summary>
    public double? Goal { get; init; }

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
    /// <summary>The heading. With <see cref="Description"/> it is the drawing's accessible name. It stays one line: where the
    /// library's generous estimate of its width runs past the drawing's width less 48, it is cut at a word with <c>…</c>, and the
    /// whole of it stays in the drawing's <c>&lt;title&gt;</c> and accessible name.</summary>
    public string Title { get; init; } = "Untitled chart";
    /// <summary>The line under the title, read after it as part of the accessible name. Where it is wider than the drawing's width
    /// less 48, by the library's generous estimate, it goes on over a second line, broken between its <c> · </c> clauses where both
    /// lines then fit and otherwise between the words that set the two lines most nearly equal, and the plot moves down by that line,
    /// 14 pixels. Past two lines the first takes as many words as fit and the second ends in <c>…</c>, and the whole of it stays in the
    /// drawing's <c>&lt;desc&gt;</c> and accessible name.</summary>
    public string Description { get; init; } = "";
    /// <summary>A line at the foot of the chart saying where the data came from. It wraps as <see cref="Description"/> does, growing
    /// upward: a second line moves the bottom of the plot, and the X axis and its title with it, up by 14 pixels.</summary>
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
    /// seconds, TimeOfDay reads them as seconds since a midnight and writes the clock, and Compact writes 1.2k; a time X axis
    /// keeps <see cref="ValueFormat.Number"/>. CSV keeps raw numbers.</summary>
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
    /// <summary>Which ticks of the main Y axis carry a label, in every pane that does not set its own <see cref="ChartPane.YTickLabels"/>:
    /// all of them, the lowest and highest drawn, with <see cref="TickLabels.Bounds"/> none but the axis's two ends at their exact values,
    /// or with <see cref="TickLabels.None"/> none at all. Gridlines stay at every tick; a secondary axis labels all of its own.</summary>
    public TickLabels YTickLabels { get; init; }
    /// <summary>The data: at most 32 series and 100,000 points in all.</summary>
    public IReadOnlyList<ChartSeries> Series { get; init; } = [];
    /// <summary>Names the X axis.</summary>
    public string XLabel { get; init; } = "";
    /// <summary>What labels a continuous X axis along the bottom: <see cref="TickSource.Auto"/>, the default, puts 1 to 24 labelled
    /// points' labels in place of the ticks, except that on a time axis it keeps the dates unless every label fits uncut;
    /// <see cref="TickSource.Axis"/> always draws the axis's own ticks, and <see cref="TickSource.PointLabels"/> always the points'
    /// labels. Line, area, scatter, bubble, candlestick, OHLC, band, range and blocks charts take it; the others refuse it set.</summary>
    public TickSource XTicks { get; init; }
    /// <summary>Which of the X axis's labels along the bottom are written: all of them, the default; the first and last drawn, with
    /// <see cref="TickLabels.Ends"/>, as <see cref="YTickLabels"/> does up the side; or, with <see cref="TickLabels.Bounds"/>, none but
    /// the axis's two ends at their exact values in its format, <see cref="XMin"/> and <see cref="XMax"/> or the data's ends, as a
    /// histogram of finish times labels its first start and its last end. Gridlines stay where they are. A continuous X axis takes it:
    /// line, area, scatter, bubble, candlestick, OHLC, band, range, blocks and timeline charts; the others refuse it set, and
    /// <see cref="TickLabels.Bounds"/> is refused beside <see cref="TickSource.PointLabels"/>, which asks for the points' labels instead.
    /// <see cref="TickLabels.None"/> is refused: every pane shares the X axis, and its labels are how a reader places each mark.</summary>
    public TickLabels XTickLabels { get; init; }
    /// <summary>Names the main plot's left-hand axis. On a gauge it is the unit written after the score, such as <c>%</c>.</summary>
    public string YLabel { get; init; } = "";
    /// <summary>Names the main plot's right-hand axis, which appears when one of its series is marked secondary.</summary>
    public string Y2Label { get; init; } = "";
    /// <summary>The drawing's width in SVG units, from 320 to 4096, or from 60 for a <see cref="Sparkline"/>. The SVG scales to the
    /// width of its container, and its text is 12 units high, so a chart shown at the width it is drawn shows its text at 12 pixels.
    /// The Blazor component's <c>FitWidth</c> draws it at the width it is shown.</summary>
    public int Width { get; init; } = 900;
    /// <summary>The drawing's height in SVG units, from 240 to 2160, or from 16 for a <see cref="Sparkline"/>. The SVG keeps this
    /// proportion to <see cref="Width"/> as it scales.</summary>
    public int Height { get; init; } = 420;
    /// <summary>
    /// Draws the data alone, as a sparkline: a line, an area, scatter points or columns the size of a word, with no title, description
    /// or source written, no axes, ticks, gridlines or legend, and zone bands and annotations drawn without their labels. The plot fills
    /// the drawing but for a padding just wide enough for its largest marker or ring, so a point at an edge is drawn whole. The title
    /// stays the drawing's accessible name and <c>&lt;title&gt;</c> and the description its <c>&lt;desc&gt;</c>, and every point keeps its
    /// focusable, named mark and native tooltip, so the drawing reads point by point with no script. It may be as small as 60 by 16, and
    /// is shown at its own width rather than its container's, never wider than the container. Other kinds, panes and value labels are
    /// refused: a sparkline is read beside words that say its numbers. The component draws it without its legend, toolbar, zoom or data
    /// table.
    /// </summary>
    public bool Sparkline { get; init; }
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
    /// <summary>The least the main plot's left-hand axis spans, centred on its data: where the data's range is smaller, the axis runs
    /// from the middle of the data less half the span to the middle plus half, so a wobble of 0.3 in a span of 8 reads as small instead
    /// of filling the plot; where the data's range is wider, the axis fits the data as it would without it. Null fits the data. It must
    /// be positive, and is refused beside <see cref="YMin"/> or <see cref="YMax"/>, on a logarithmic axis, and on an axis that must
    /// include zero: with <see cref="IncludeZero"/>, on the kinds drawn from zero and on an axis that carries columns or an area. Line,
    /// scatter, bubble, band, range, candlestick, OHLC and blocks charts take it, reversed or not.</summary>
    public double? YMinSpan { get; init; }
    /// <summary>Holds the main plot's left-hand axis symmetric about zero, so zero stands in the middle of the plot: the axis runs
    /// from −m to +m, where m is the largest of this value and the data's distance from zero either way, so it is at least ±this value
    /// tall and every value fits. For values read above and below a balance, such as training form, whose sign is the point: +4 and −4
    /// stand equally far from the middle, and a quiet stretch near zero still reads as near zero. Null fits the data. It must be
    /// positive, and is refused beside <see cref="YMin"/>, <see cref="YMax"/> or <see cref="YMinSpan"/>, which set the axis another
    /// way, and on a logarithmic axis, which has no zero; it reverses with the axis. Line, area, scatter, bubble, column, bar, stacked
    /// column, band, range, candlestick, OHLC and blocks charts take it. <see cref="ValueFormat.Signed"/> writes its ticks +5 and −5.</summary>
    public double? YSymmetric { get; init; }
    /// <summary>The bottom of the main plot's right-hand axis. Null fits its series.</summary>
    public double? Y2Min { get; init; }
    /// <summary>The top of the main plot's right-hand axis. Null fits its series.</summary>
    public double? Y2Max { get; init; }
    /// <summary>The most points a line or area draws for each unbroken run of points, from 16 to 5000. Longer runs are thinned as
    /// <see cref="Sampling"/> says: by default by keeping each bucket's lowest and highest point, so peaks survive, and every point drawn
    /// keeps its original index. A run longer than this is thinned over the X range the chart shows, <see cref="XMin"/> to
    /// <see cref="XMax"/>, with the nearest point outside it at each side so the line still runs to the plot's edges, so a zoomed view
    /// draws more of its own detail; a run within it is drawn whole, as before 0.37.0.</summary>
    public int MaxRenderedPoints { get; init; } = 1200;
    /// <summary>How a line or area longer than <see cref="MaxRenderedPoints"/> is thinned. <see cref="SamplingMethod.MinMax"/>, the
    /// default, keeps each bucket's lowest and highest point. <see cref="SamplingMethod.Average"/> divides the X range the chart shows
    /// into <see cref="MaxRenderedPoints"/> equal slices, the same for every series, and draws a series' points in each slice as one point
    /// at their mean X and mean Y, the mean written as precisely as the series' own values, to at most two places, so channels recorded at
    /// the same moments line up slice for slice; on evenly sampled data, such as a
    /// 1 Hz ride, every slice holds the same number of points, give or take one. It applies to a series drawn as a line or an area whose
    /// points in view number more than the budget: a series within it is drawn whole. Each unbroken run keeps its own slices, so a
    /// missing value stays a gap; a slice of one point draws that point. An averaged mark's name and tooltip end <c>, average of N
    /// points</c>, and it reports its slice's first point; a highlighted point, and the last point of a series with
    /// <see cref="ChartSeries.HighlightLast"/>, keeps a mark of its own beside the average of its slice, off the line. Other marks,
    /// bands' outlines and trends keep MinMax.</summary>
    public SamplingMethod Sampling { get; init; }
    /// <summary>Histogram bin count. Null selects a count from the data.</summary>
    public int? Bins { get; init; }
    /// <summary>Scatter only. Set a cell count across the plot to draw one shaded cell per occupied
    /// region instead of one mark per observation. Null draws every point, which is the default.</summary>
    public int? DensityCells { get; init; }
    /// <summary>Lighter lines between the labelled ticks. Off by default; a time axis never takes them.</summary>
    public bool MinorGridlines { get; init; }
    /// <summary>Reference lines and bands drawn behind the data, or over it where one is <see cref="ChartAnnotation.InFront"/>: a Y
    /// reference on the main plot, an X one through every pane.</summary>
    public IReadOnlyList<ChartAnnotation> Annotations { get; init; } = [];
    /// <summary>Shades each zone as a band on the main plot's primary value axis, behind the data and any annotations, named
    /// with its range. The open bottom zone and the unbounded top one stop at the plot edge, and the bands never
    /// widen the axis. Applies wherever Y annotations do. On a calendar it colours each day in its value's zone instead.</summary>
    public ZoneScale? YZones { get; init; }
    /// <summary>Plots stacked under the main one, sharing its X axis, each with Y axes of its own, such as volume under
    /// prices. The main plot is pane 0 and takes this spec's Y properties; <c>Panes[k - 1]</c> sets up pane k, which holds
    /// the series whose <see cref="ChartSeries.Pane"/> is k. Line, area, scatter, bubble, band, range, blocks, candlestick and
    /// OHLC charts take them, at most five, so a chart draws up to six plots. Empty draws one plot.</summary>
    public IReadOnlyList<ChartPane> Panes { get; init; } = [];
    /// <summary>Where each plot is named: up the side, the default, or with <see cref="PaneTitlePlacement.Above"/> as one horizontal
    /// header line above it, the main plot's <see cref="YLabel"/> and each pane's <see cref="ChartPane.Label"/>, starting at the plot's
    /// left edge and cut with <c>…</c> to its width, in the style's text colour; a header cut short keeps its whole as its accessible name
    /// and its native tooltip, as a title does, so put its key fact first. The main plot moves down 18 pixels and the gap between
    /// two plots grows from 24 to 30 to hold them; a right-hand axis keeps its title up the side. Where no left-hand axis writes a label
    /// either, every one set to <see cref="TickLabels.None"/>, the left margin narrows from 76 pixels to 30, as nothing is left to stand
    /// in it. Line, area, scatter, bubble, column, stacked column, band, range, candlestick, OHLC and blocks charts take it; a horizontal
    /// bar chart, a sparkline and the kinds without a Y axis up the side refuse it.</summary>
    public PaneTitlePlacement PaneTitles { get; init; }
    /// <summary>Gauge charts only: how far round the arc runs, in degrees, from 180, a semicircle, to 360, a full circle. The arc
    /// is centred at the top, so the default 270 leaves its opening at the bottom.</summary>
    public double GaugeSweep { get; init; } = 270;
    /// <summary>Timeline charts only: joins a span to the span in another lane that starts where it ends with a thin vertical
    /// connector, as a hypnogram does. On by default; off draws a plain state chart. Every other kind refuses it off.</summary>
    public bool TimelineConnectors { get; init; } = true;
    /// <summary>Calendar charts only: the contribution grid of weeks, the default, or a small grid for each month. Every other
    /// kind refuses it set.</summary>
    public CalendarLayout CalendarLayout { get; init; }
    /// <summary>Calendar charts only: how each day is drawn, a rounded square by default, a dot, or a bubble sized by its value.
    /// Every other kind refuses it set.</summary>
    public CalendarCell CalendarCell { get; init; }
    /// <summary>Calendar charts only: the day each week starts on, Monday by default, as ISO 8601 has it. Every other kind refuses
    /// it set.</summary>
    public DayOfWeek WeekStart { get; init; } = DayOfWeek.Monday;
    /// <summary>
    /// In the Blazor component, reads every series at once at one X: a vertical guide through every pane at the X nearest the
    /// pointer or the focused point, a ring round each shown series' point there, and one tooltip that reads the X first and then
    /// each series' name and value in legend order, its <see cref="ChartPoint.ValueNote"/>, zone and change words included, as
    /// <c>3 Jun 2026</c>, <c>Fitness 52.3</c>, <c>Fatigue 61</c>, <c>Form −8.7</c>. The arrow keys then step from one X to the next,
    /// Up and Down moving between the series there, and the component's status line reads the same words. A series is read where it
    /// has a point within half the closest spacing of the chart's X values; a missing value reads <c>missing</c>. It never changes the
    /// static drawing: <see cref="ChartSvg.Render"/> draws the same SVG with it or without it, and <see cref="ChartSvg.Readout"/> gives
    /// what it reads to any other host. Line, area, scatter, bubble, band, range, candlestick, OHLC and blocks charts take it; a
    /// sparkline, read beside words, and the kinds without a continuous X axis refuse it.
    /// </summary>
    public bool SharedReadout { get; init; }
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
    /// <summary>The least the pane's left-hand axis spans, centred on the pane's data, as <see cref="ChartSpec.YMinSpan"/> sets the
    /// main plot's, and refused where it is.</summary>
    public double? YMinSpan { get; init; }
    /// <summary>Holds the pane's left-hand axis symmetric about zero, at least ±this value tall, as <see cref="ChartSpec.YSymmetric"/>
    /// holds the main plot's, and refused where it is.</summary>
    public double? YSymmetric { get; init; }
    /// <summary>How the pane's left-hand axis writes its values.</summary>
    public ValueFormat YFormat { get; init; }
    /// <summary>Puts the smallest value at the top of the pane's left-hand axis.</summary>
    public bool YReversed { get; init; }
    /// <summary>Which ticks of the pane's left-hand axis carry a label, as <see cref="ChartSpec.YTickLabels"/> says for the main plot;
    /// null, the default, takes the spec's. <see cref="TickLabels.None"/> writes none, the gridlines staying.</summary>
    public TickLabels? YTickLabels { get; init; }
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
/// A reference drawn behind the data, or over it when <see cref="InFront"/>: a line at <paramref name="From"/>, or a band when
/// <see cref="To"/> is set. Values are in data coordinates, so an annotation pans and zooms with the chart.
/// </summary>
/// <param name="Axis">The axis the value is read on.</param>
/// <param name="From">The value marked, or where a band starts. Unix milliseconds on a time axis.</param>
public sealed record ChartAnnotation(AnnotationAxis Axis, double From)
{
    /// <summary>The far edge of a band. Null draws a line.</summary>
    public double? To { get; init; }
    /// <summary>Shown with the value, as <c>Target: 55</c>. Null shows the value alone.</summary>
    public string? Label { get; init; }
    /// <summary>Defaults to the style's muted colour.</summary>
    public string? Color { get; init; }
    /// <summary>Draws a reference line dashed, the default, or solid.</summary>
    public bool Dashed { get; init; } = true;
    /// <summary>Writes the value after the label drawn on the chart, the default. Off, the chart draws the <see cref="Label"/> alone, as
    /// <c>median</c> beside a median line whose time the axis already reads, while its tooltip and accessible name still read the label
    /// and the value, <c>median: 47:12</c>. Off needs a label to draw, and is refused on a calendar, whose key names an outlined day by
    /// its label alone already.</summary>
    public bool ShowValue { get; init; } = true;
    /// <summary>Draws the line or band over the data instead of behind it, so columns or blocks do not hide it: a median over a
    /// histogram's bins, or a target over columns. A line in front stands on a halo of the background colour, as a gauge's target does,
    /// so it shows over a mark of any colour. Its label is written over the data either way. A gauge draws its target over the score
    /// already, and a calendar outlines its day, so both refuse it.</summary>
    public bool InFront { get; init; }
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
    /// <summary>In levels by longest path, from left to right or from top to bottom as <see cref="GraphSpec.Direction"/> says,
    /// each level ordered to cut edge crossings. Refuses cycles longer than a self-loop.</summary>
    Layered
}
/// <summary>Which way the levels of a layered graph run.</summary>
public enum GraphDirection
{
    /// <summary>Levels in columns from left to right, each level's nodes spread down the height. The default.</summary>
    LeftToRight,
    /// <summary>Levels in rows from top to bottom, each level's nodes spread across the width, so that a graph with many levels
    /// fits a narrow box such as a phone's. Its edges point down, and each leaves its node from under the node's label.</summary>
    TopToBottom
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
    /// <summary>Which way a layered graph's levels run: from left to right by default, or from top to bottom. A circular graph
    /// ignores it. <see cref="GraphEngine.Fit"/> turns a graph top to bottom when its levels cannot stand side by side in the
    /// width it is shown at.</summary>
    public GraphDirection Direction { get; init; }
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
