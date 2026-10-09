namespace Lumen.Charts;

/// <summary>The kind of chart, which decides how X is laid out and how a series that names no kind of its own is drawn.
/// Line, area, scatter, bubble, band, range, candlestick and OHLC charts place points along a continuous X axis by their X;
/// column, bar, stacked column, donut, heatmap and radar charts place them by category and show their labels; gauge and
/// ring charts draw one value a series round an arc and have no X axis; a timeline draws spans along a continuous X axis,
/// one lane a series; a calendar draws one series as a grid of days, each coloured by its value; and blocks draw spans along a
/// continuous X axis, each as wide as it runs and as tall as its value; and a strip draws one series' parts as shares of one bar.</summary>
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
    Blocks,
    /// <summary>A proportion strip, as time in each heart-rate zone is drawn in a training app: one series whose points are the parts of a
    /// whole in order, each point's <see cref="ChartPoint.Label"/> the part's name and its Y, zero or more, its amount, in its own
    /// <see cref="ChartPoint.Color"/> or else the style's series colours in order; X is only their order. The parts are drawn as one bar
    /// 18 units thick across the drawing, each as long as its share of the total, both outer ends rounded by the style's
    /// <see cref="ChartStyle.BarRadius"/> or 6 units, clamped to half the bar's thickness, and each part parted from the next by a 2-unit gap
    /// in the background colour, so neighbours never rely on their colours to be told apart; a part of zero draws nothing. Under the bar a
    /// key names every part in order with its swatch and its whole percentage, <c>Easy 34%</c>, the percentages adding up to exactly 100
    /// (largest remainders, ties to the part listed later), flowing left to right and wrapping onto rows as the width allows; a part of
    /// zero keeps its entry, <c>0%</c>. Each part drawn is a focusable mark named <c>Easy: 34%, 12:20</c>, its amount in
    /// <see cref="ChartSpec.YFormat"/> and <see cref="ChartSpec.YUnit"/>. It draws no axes, ticks or gridlines, and is drawn as tall as its
    /// content, its title and description if drawn, the bar and the key's rows: <see cref="ChartSpec.Height"/> is not used. At most 24
    /// parts.</summary>
    Strip
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
/// <summary>A tick of a Y axis set by hand, through <see cref="ChartSpec.YTickValues"/> or <see cref="ChartPane.YTickValues"/>: a gridline
/// at <paramref name="Value"/>, labelled <paramref name="Label"/> or else the value in the axis's format and its unit, as <c>Front</c>,
/// <c>Mid</c> and <c>Back</c> name 0, 50 and 100 on a percentile axis.</summary>
/// <param name="Value">Where the tick stands, on the axis's own scale. A value outside the axis's range is left out; it never stretches
/// the axis.</param>
/// <param name="Label">The words written for it, at most 24 characters, exactly as given: no format and no unit is added. Null writes the
/// value as the axis writes its values, with <see cref="ChartSpec.YUnit"/> after it.</param>
public sealed record AxisTick(double Value, string? Label = null);
/// <summary>A colour a gradient takes at <paramref name="Value"/>, measured on the axis of the series it colours.</summary>
/// <param name="Value">The value at which the stroke takes this colour.</param>
/// <param name="Color">A <c>#RRGGBB</c> colour.</param>
public sealed record ColorStop(double Value, string Color);

/// <summary>Null Y is a missing observation, never an implicit zero. Size encodes bubble area.</summary>
/// <param name="X">Where the point stands on a continuous X axis, in Unix milliseconds on a time axis. Column, bar and stacked
/// column charts place points by their order instead.</param>
/// <param name="Y">The value. Null is a missing observation: a gap in a line or area, and left out elsewhere; <see cref="GapLabel"/>
/// writes a word there instead of leaving it silent.</param>
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
    /// <summary>A short note written straight after this point's value, at most 20 characters, or 40 on a heatmap cell, from 0.46.1,
    /// which draws it in the cell only with <see cref="ChartSpec.CellNotes"/>, from 0.46.2, such as <c>/48</c> after a finishing
    /// position for the size of its field: in the muted colour at normal weight after a value label, and after the value in the
    /// mark's tooltip and accessible name and in the component's data table. A <c>Note</c> column carries it into CSV. Give it any
    /// space it needs: <c>" inside baseline"</c>. A missing value has nothing for a note to follow, so its note reaches the CSV
    /// alone, unless a <see cref="GapLabel"/> gives it a word to follow in its mark's name. Marks named by more than one value, or by none, refuse it: candles, range bars, histograms, boxes, violins,
    /// timelines, calendars, gauges and rings.</summary>
    public string? ValueNote { get; init; }
    /// <summary>A <c>#RRGGBB</c> colour that rings this point of a line or scatter series with an enlarged marker in it, outlined in
    /// the background colour, whatever the series' markers, as a personal best is picked out of a run of results. It colours that
    /// marker alone: the line keeps its colour, and the point keeps its name. A colour is never enough on its own, so give the point a
    /// <see cref="ValueNote"/> that says why, such as <c>" · PB"</c>, which its tooltip and accessible name read after its value.
    /// Areas and every other mark refuse it, as does a density scatter, which shades cells rather than points.</summary>
    public string? Highlight { get; init; }
    /// <summary>A second line under this point's category name, at most 16 characters on one line, such as <c>152 bpm</c> under a lap's
    /// <c>L3</c> or <c>13.0 W/kg</c> under a best effort's <c>5s</c>, in the muted colour at 11 px: on a column or stacked column chart
    /// 14 units under the name, cut with <c>…</c> past 12 characters as the name is, the plot giving up 14 units at its foot for it only
    /// when some point has one; on a horizontal bar chart under the name beside the bar, the two lines centred on the bar together. It is
    /// thinned with its name, the wider of the two keeping neighbouring columns 8 units apart, by the room their words take, in either
    /// finish, rather than by the 65 units a column chart's names otherwise keep each, so at 340 units about four columns keep a sub-label
    /// as wide as <c>152 bpm</c> each, and more leave every other category's words out; a number alone, <c>152</c>, fits about eight. It is
    /// said after the name in the mark's tooltip and accessible name, <c>Heart rate: L3 · 152 bpm, 152</c>, and in the component's
    /// status line and data table. A category takes the first sub-label any series gives it, so every mark in it says the same words;
    /// series may repeat it or leave it null, but two different sub-labels for one category are refused. On a heatmap, from 0.46.0, a
    /// cell takes its own, as a second line in the cell when <see cref="ChartSpec.CellText"/> is set and in its name always, so the cells
    /// of one column may differ, as <c>/12 starts</c> and <c>/4 starts</c> do. Continuous X axes write tick labels rather than categories,
    /// so every kind but column, bar, stacked column and heatmap charts refuses it, as does a sparkline.</summary>
    public string? SubLabel { get; init; }
    /// <summary>
    /// The word a missing value is written as, such as <c>absent</c> for a round the rider was entered in and did not ride: 1 to 12
    /// characters on one line, on a point whose <see cref="Y"/> is null in a series drawn as a line, an area or scatter points, or, from
    /// 0.46.1, on a heatmap cell with no value, which is drawn unshaded and dashed and named with the word. The point
    /// is still a missing value, so a line or an area still breaks there, but it is no longer silent: the word is written at the point's
    /// X, centred on it and moved in from the plot's sides so it is never cut, just inside the plot beside the start of the series' value
    /// axis, above the plot's bottom edge, or below its top edge where a reversed axis puts the start at the top, at 11 px and weight 600,
    /// over a copy of itself stroked in the background colour, as a value label is. It is written in the point's <see cref="Color"/> where
    /// that clears 4.5:1 against the background, else in the series colour where that does, else in the style's
    /// <see cref="ChartStyle.Text"/> colour. Gap labels and value labels keep clear of each other as value labels do: one that would meet
    /// a label written before it, in this series or an earlier one, is left out, its word kept in its mark's name. The point becomes a
    /// focusable mark like any other, an invisible box round its word, or a narrow one at its X where the word is left out, named and tooltipped <c>Share: Round 3, absent</c>, the word in
    /// place of <c>missing</c> and any <see cref="ValueNote"/> after it, so the arrow keys reach it, the component's shared readout reads
    /// it, <c>Share absent</c>, and its status line and data table say it. A point the X range shown leaves out is left out with its word.
    /// A point that has a value, a series of any other kind, a density scatter and a sparkline refuse it, as does a heatmap cell that is
    /// also <see cref="NotRated"/>: a cell is either not rated or has a gap label. Leave a point out altogether,
    /// rather than give it a null Y, where nothing was missed, such as a round the rider's category did not hold: the line then simply joins
    /// over it. CSV does not carry it.
    /// </summary>
    public string? GapLabel { get; init; }
    /// <summary>On a heatmap, marks the cell as not rated and says why, 1 to 24 characters, such as "too few starts to rate": the cell
    /// is drawn unshaded with a dashed outline, left out of the colour scale, and named "…, not rated: {reason}". With
    /// <see cref="ChartSpec.CellText"/> it writes the reason in place of its value, from 0.46.1 (before, its value or "—"), wrapped onto
    /// the lines that fit, then its sub-label if a line is left. With <see cref="ChartSpec.NotRatedKeepsValue"/> it keeps its value instead,
    /// muted, and writes the reason as its last lines (0.46.2). A not-rated cell is drawn even when its value is null. Heatmaps only.</summary>
    public string? NotRated { get; init; }

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
    /// the arc along its length instead, each stop at its value's angle; a gauge takes this or <see cref="ChartSpec.YZones"/>.
    /// From 0.40.0 it also fills columns, on a column chart or as a column series, and the bars of a horizontal bar chart: one
    /// gradient laid along the value axis in the plot's own coordinates, up for columns and across for bars, so each column takes at
    /// each height the colour of the value drawn there and a taller column reaches further along it; past the first and last stops
    /// their colours carry on. A point's own <see cref="ChartPoint.Color"/> still fills its column flat. Value labels stay in the text
    /// colour, and the legend key shows up to four of the stops' colours. A faded <see cref="Fill"/> and stacked columns, whose colours
    /// tell the stacked series apart, refuse it. The colours meet no contrast rule of their own: pick stops that each clear 3:1
    /// against the background, as a filled mark needs.</summary>
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
    /// <summary>
    /// Names this series at the end of its line, so a chart can do without a legend: written just right of the series' last point drawn
    /// in view, the one furthest along X that has a value, centred on it, at 12 px and weight 600 in the series colour where that clears
    /// 4.5:1 against the background and in the style's text colour where it does not, followed on the same line by <see cref="EndNote"/>
    /// in the muted colour. At most 24 characters. The right margin grows to hold the widest label and note, but never past the point
    /// where the plot would keep less than half the drawing's width; a label wider than that is cut with <c>…</c>, its whole kept as its
    /// tooltip and accessible name. From 0.40.0 the note gives way first: it is cut, or left out where not even its first letter fits, and
    /// the label itself is cut only where it does not fit alone. Labels whose spans across the drawing overlap never overlap each other: each set is sorted by the height
    /// of its points and moved apart up or down as little as it can be, 14 units a line, within its plot and 8 units past its top and
    /// bottom; a label moved more than 3 units from its point is joined to it by a short line in the series colour, or in the muted
    /// colour where the series colour does not clear 3:1. Where even 12 units a line do not fit, the lowest labels are left out. The label
    /// and its note are also said in the last point's name, <c>, labelled You · leader</c>, so they are never drawn only. Line, area and
    /// scatter series take it, except a density scatter's; it is refused on a series measured on the right-hand axis and on a chart whose
    /// Y axis stands on the right, which take the right margin, and on a sparkline. When every series has one, draw the chart without its
    /// legend: <c>includeLegend: false</c>, or <c>ShowLegend="false"</c> on the component.
    /// </summary>
    public string? EndLabel { get; init; }
    /// <summary>A note written after <see cref="EndLabel"/> on the same line, at normal weight in the muted colour, such as <c>leader</c>
    /// or <c>+12.3s</c>, at most 24 characters; it needs an end label to follow.</summary>
    public string? EndNote { get; init; }
    /// <summary>
    /// Says that this series' points are already averages the app made, and over what, such as <c>12 s</c> for a ride's channels bucketed
    /// into 12-second slices or <c>a week</c> for weekly means: at most 16 characters on one line, written exactly as given. Each of its
    /// marks with a value is then named, and its tooltip read, with <c>, average of 12 s</c> after its value's words, where
    /// <see cref="SamplingMethod.Average"/> writes its own; and the shared readout says it too: once in a column's label,
    /// <c>1:02:30 · average of 12 s</c>, where every series read there says the same, its entries reading their values alone, and otherwise
    /// after each such entry's value, <c>Power 212, average of 12 s</c>. The component's status line reads the same words. Where Lumen itself
    /// averages a mark, under <see cref="SamplingMethod.Average"/>, that mark keeps Lumen's own words, <c>, average of 4 points</c>, and a
    /// column of such marks its slice, <c>average of 24 s</c>, since those are what it draws. A missing value says nothing, and CSV and the
    /// data table keep the values as given. It changes no drawing, only words. Series whose marks are named by one value take it: lines,
    /// areas, scatter points, bubbles, columns, bars, stacked columns, bands, blocks, heatmap rows and radar series, a sparkline's
    /// included; candles, range bars, histograms, boxes, violins, timelines, calendars, donut slices, gauges, rings and a strip's parts,
    /// named by several values, by none, by a count, a sum or a share, refuse it. Null, the default, says nothing.
    /// </summary>
    public string? AverageOf { get; init; }

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
    /// <summary>
    /// Ticks of the main plot's left-hand axis set by hand, in place of the ticks it would choose: a gridline at each value, labelled with
    /// its <see cref="AxisTick.Label"/> or else its value in the axis's format and <see cref="YUnit"/>, as <c>Front</c>, <c>Mid</c> and
    /// <c>Back</c> name 0, 50 and 100. A value outside the axis's range is left out, and never stretches it: set <see cref="YMin"/> and
    /// <see cref="YMax"/> to the range the ticks need. <see cref="YTickLabels"/> still chooses which labels are written, and the axis takes
    /// no minor gridlines of its own. At most 24, none twice, each finite, positive on a logarithmic axis, and each label at most 24
    /// characters. Null, the default, lets the axis choose. Charts drawn on an X and a Y axis take it; donut, heatmap, radar, histogram,
    /// box, violin, gauge, ring, timeline and calendar charts and a sparkline refuse it.
    /// </summary>
    public IReadOnlyList<AxisTick>? YTickValues { get; init; }
    /// <summary>
    /// A unit written straight after every value the main plot's left-hand axis writes, exactly as given, at most 8 characters: its
    /// automatic tick labels, the names and tooltips of the marks measured on it, their value labels, its bounds, zones and annotations, the
    /// shared readout, and the component's status line and data table. <c>"s"</c> writes <c>+12.3s</c> and <c>" bpm"</c> writes
    /// <c>152 bpm</c>, so give it the space it needs. A tick set by <see cref="YTickValues"/> with a label of its own is written as given,
    /// and the right-hand axis takes none. CSV keeps raw numbers. Null writes none. The kinds that take <see cref="YTickValues"/> take it,
    /// and from 0.46.0 so does a heatmap, though it takes no <see cref="YTickValues"/>, having no Y axis: its unit follows its cells'
    /// values, as its <see cref="YFormat"/> does.
    /// </summary>
    public string? YUnit { get; init; }
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
    /// <summary>Stretches the value axis to include zero. Kinds drawn from a zero baseline always include it. On a heatmap in the refined
    /// finish, from 0.46.1, it widens the colour scale to run through 0; the classic finish ignores it.</summary>
    public bool IncludeZero { get; init; }
    /// <summary>The lowest value the X axis shows. Null fits the data.</summary>
    public double? XMin { get; init; }
    /// <summary>The highest value the X axis shows. Null fits the data.</summary>
    public double? XMax { get; init; }
    /// <summary>The bottom of the main plot's left-hand axis. Null fits the data; a kind drawn from zero refuses a bound that
    /// leaves zero out. On a heatmap in the refined finish, from 0.46.1, it is the low end of the colour scale, used as given; the
    /// classic finish ignores it.</summary>
    public double? YMin { get; init; }
    /// <summary>The top of the main plot's left-hand axis. Null fits the data. On a heatmap in the refined finish, from 0.46.1, it is the
    /// high end of the colour scale, used as given; the classic finish ignores it.</summary>
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
    /// <summary>On a heatmap, writes each cell's value and, on a second line, its <see cref="ChartPoint.SubLabel"/>, where they fit, in
    /// whichever of the style's text and background colours stands out more against the cell where that reaches 4.5:1, and otherwise in
    /// black or white, whichever stands out more, so every word clears 4.5:1; the cell's name always carries both. A not-rated cell writes
    /// its <see cref="ChartPoint.NotRated"/> reason, and a cell with a <see cref="ChartPoint.GapLabel"/> its word, in place of a value, from
    /// 0.46.1. <see cref="CellNotes"/> adds each cell's note, and <see cref="NotRatedKeepsValue"/> keeps a not-rated cell's value (0.46.2).
    /// Heatmaps only; false by default.</summary>
    public bool CellText { get; init; }
    /// <summary>On a heatmap, the width of every column in pixels, at least 24: the drawing grows to the name column, at least 130, plus 35,
    /// plus the columns times this width, and never narrower than 320, the room left over at its right, instead of squeezing into
    /// <see cref="Width"/>, and in <c>&lt;LumenChart FitWidth&gt;</c> it scrolls sideways when wider than its box. Heatmaps only; null by
    /// default.</summary>
    public double? CellWidth { get; init; }
    /// <summary>Writes a heatmap's column labels above its grid, under its title and description, as a table's header row reads, instead of
    /// under it; the grid takes the room they leave below (0.46.1). Heatmaps only, refused elsewhere; false by default, and left out of the
    /// gradient-ID hash while false.</summary>
    public bool ColumnLabelsOnTop { get; init; }
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
    /// <summary>
    /// Draws a track behind each bar of a <see cref="ChartKind.Bar"/> or <see cref="ChartKind.Column"/> chart, as a meter does: from the
    /// value axis's minimum, zero, to its maximum, <see cref="YMax"/>, in the style's <see cref="ChartStyle.Grid"/> colour and rounded as
    /// the bar is, so a score of 72 out of 100 reads as a bar filled 72 % of the way along its track. It needs <see cref="YMax"/>, an axis
    /// from zero (<see cref="YMin"/> unset or 0, and no value below zero), and every series on the left-hand axis. A value above
    /// <see cref="YMax"/> is drawn at the track's end and its name says so. On a horizontal bar chart a bar on a track is at most 18 units
    /// thick, centred in its row; its value label, with <see cref="ChartSeries.ValueLabels"/>, stands just past the track's end, the right
    /// margin growing to hold the widest; the left margin fits the widest category label instead of the fixed 160, up to 45 % of the
    /// width, a label too long for it cut with <c>…</c>; and where nothing is written under the plot, <see cref="YTickLabels"/>
    /// <see cref="TickLabels.None"/> and no <see cref="YLabel"/>, the bottom margin narrows from 76 to 24. On a column chart the value
    /// label stands above the track's top. The tracks are drawn with the data, over zone bands and references behind it: draw a target
    /// <see cref="ChartAnnotation.InFront"/>. Off by default; other kinds, stacked columns included, refuse it.
    /// </summary>
    public bool BarTrack { get; init; }
    /// <summary>
    /// Draws the <see cref="Title"/> and <see cref="Description"/> at the top of the chart, the default. Off, neither is drawn and the body
    /// moves up into their space, 50 units, for a page that writes its own heading over the chart; both stay the drawing's
    /// <c>&lt;title&gt;</c>, <c>&lt;desc&gt;</c> and accessible name. Every chart kind takes it, a strip included, whose drawing is then
    /// that much shorter. A sparkline draws neither in any case. Network graphs (<see cref="GraphSpec"/>) always draw their title. It is
    /// not what <see cref="ChartSvg.Render"/>'s <c>includeTitles</c> controls: that writes or leaves out the native tooltip, the
    /// <c>&lt;title&gt;</c>, in each mark.
    /// </summary>
    public bool DrawTitles { get; init; } = true;
    /// <summary>
    /// Paints the drawing's background in the style's <see cref="ChartStyle.Background"/>, the default. Off, the root <c>&lt;svg&gt;</c>
    /// carries no background, and no shape fills the drawing, so the surface it sits on shows through, as a card of another colour does;
    /// nothing else moves. Lumen still reads <see cref="ChartStyle.Background"/> as the colour the chart stands on: every contrast rule is
    /// checked against it, a value label too pale for it is written in the text colour, and the halos and separators drawn in it stay in
    /// it — a value label's halo, an end label's, a reference line drawn <see cref="ChartAnnotation.InFront"/>, a hollow marker's fill, a highlighted point's outline, a gauge's knob and target tick; a strip's gaps are left empty, so the surface shows through them. So when the background is left
    /// unpainted, set <see cref="ChartStyle.Background"/> to the colour of the surface the chart sits on, or the halos show as patches of
    /// another colour and the contrast checks measure against the wrong one. The root then carries the colour as <c>--lumen-ground</c>
    /// in place of <c>background</c>, so the component's readout rings keep their halo. The SVG export carries no background either, and
    /// the component's PNG export is transparent where nothing is drawn. Every kind takes it; network graphs have
    /// <see cref="GraphSpec.PaintBackground"/>.
    /// </summary>
    public bool PaintBackground { get; init; } = true;
    /// <summary>
    /// Horizontal bar charts and, from 0.46.1, heatmaps: draws the chart as tall as its rows need instead of <see cref="Height"/>, as a strip is drawn as tall as
    /// its content, so a card of three or four meters keeps no spare room. Each category takes a row of 36 units on tracks
    /// (<see cref="BarTrack"/>), 32 without, or 38 where any category writes a <see cref="ChartPoint.SubLabel"/>, so names never collide;
    /// and round the rows the chart keeps the room it draws in: 78 units above them for the title and description, 14 more for a
    /// description on two lines, or 28 in all where <see cref="DrawTitles"/> is off; 76 under them for the value axis's tick labels and its
    /// title, or 24 where neither is written (<see cref="YTickLabels"/> <see cref="TickLabels.None"/> and no <see cref="YLabel"/>), with
    /// or without tracks, or 36 above a <see cref="Source"/> line, 14 more for a source on two lines; and 22 a row for the legend
    /// <see cref="ChartSvg.Render"/> draws when it includes one. So three meters on tracks without titles, ticks or source are
    /// 28 + 3 × 36 + 24 = 160 units tall, and one is 88: no 240-unit floor applies. <see cref="Height"/> is still checked, 240 to 2160, and
    /// otherwise not used. The height follows the categories the chart draws, so the component, which draws a hidden series without its
    /// points, draws a chart shorter when the series it hides held a category alone; its <c>FitWidth</c> scaling is unchanged. A heatmap's
    /// rows are as tall as the tallest cell's written words need, 12 units a line and 13 for a value, plus 4, and at least 36 (0.46.2).
    /// A fitted heatmap with an empty <see cref="Source"/> reserves no room for a source line: the grid still keeps its colour-scale line
    /// under it, and its column labels there too unless <see cref="ColumnLabelsOnTop"/> puts them above. The usual 240-unit floor applies.
    /// Off by default; other kinds refuse it.
    /// </summary>
    public bool FitHeight { get; init; }
    /// <summary>
    /// On a heatmap with <see cref="CellText"/>, writes each cell's <see cref="ChartPoint.ValueNote"/> in the cell, under its sub-label
    /// (0.46.2): 10 px, in the cell's ink, without the separator it starts with for its name (leading spaces, then one <c>·</c> or <c>,</c>
    /// and the spaces after it), so <c>" · 1840 pts, 12 athletes"</c> reads <c>1840 pts, 12 athletes</c>. It wraps onto as many lines as it
    /// needs, breaking after its commas first and then between words; a single word wider than the cell is cut with <c>…</c>. A cell with a
    /// value, or with a <see cref="ChartPoint.GapLabel"/>, writes its note, as its name says it. Where the cell is too short for every line,
    /// the note is the first to go, whole; with <see cref="FitHeight"/> the rows grow so it never has to. A note with a line break or a tab
    /// is refused while it is set. Heatmaps with cell text only; false by default, and left out of the gradient-ID hash while false.
    /// </summary>
    public bool CellNotes { get; init; }
    /// <summary>
    /// On a heatmap with <see cref="CellText"/>, a not-rated cell keeps its value (0.46.2): it writes its value, if it has one, 11 px and
    /// weight 600, in the style's <see cref="ChartStyle.Muted"/> colour where that clears 4.5:1 against <see cref="ChartStyle.Background"/>,
    /// and in the cell's ink otherwise; then its sub-label; then its note, with <see cref="CellNotes"/>, if it has a value; then its
    /// <see cref="ChartPoint.NotRated"/> reason, wrapped, as its last lines. A not-rated cell with no value writes its sub-label and then
    /// its reason, with no value line and no note. The dashed outline, the reason and the cell's name still say it is not rated, so the
    /// muted colour is never the only cue. Off, a not-rated cell writes its reason in place of its value, as 0.46.1 does. Heatmaps with cell
    /// text only; false by default, and left out of the gradient-ID hash while false.
    /// </summary>
    public bool NotRatedKeepsValue { get; init; }
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
    /// <summary>Ticks of the pane's left-hand axis set by hand, as <see cref="ChartSpec.YTickValues"/> sets the main plot's, and refused
    /// where it is. Null lets the axis choose.</summary>
    public IReadOnlyList<AxisTick>? YTickValues { get; init; }
    /// <summary>A unit written after every value the pane's left-hand axis writes, as <see cref="ChartSpec.YUnit"/> is for the main plot's;
    /// a pane does not take the spec's. Null writes none.</summary>
    public string? YUnit { get; init; }
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
    /// <summary>Paints the drawing's background in the style's <see cref="ChartStyle.Background"/>, the default. Off, the root
    /// <c>&lt;svg&gt;</c> carries no background, so the surface it sits on shows through, as <see cref="ChartSpec.PaintBackground"/>
    /// leaves a chart's. A graph draws nothing else in the background colour, but set <see cref="ChartStyle.Background"/> to the colour
    /// of the surface all the same, so <see cref="ChartStyle.ContrastIssues"/> measures the graph's colours against what is behind them.</summary>
    public bool PaintBackground { get; init; } = true;
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
