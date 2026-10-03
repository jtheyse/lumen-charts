# Lumen.Charts API reference

Every public type a chart needs, by namespace `Lumen.Charts` unless stated. All specs are immutable records: build with an object initializer, change with `with { … }`. From 0.25.0 each package also ships its XML documentation beside the DLL (in the NuGet cache, `~/.nuget/packages/<package id>/<version>/lib/net8.0/<assembly>.xml`), so IntelliSense shows member docs and you can read them there.

## Contents
- ChartSpec
- ChartSeries
- ChartPoint
- ChartPane
- ChartAnnotation
- Enums
- ChartStyle (branding, presets, finish)
- Rendering and export
- Axes and time
- Training metrics
- Statistics
- Zones
- Graphs
- Blazor components (`Lumen.Charts.Blazor`)

## ChartSpec

| Member | Type, default | Meaning |
|---|---|---|
| `Title`, `Description`, `Source` | string | Heading, subtitle and source line; title and description are the SVG's accessible name. Default title "Untitled chart". |
| `Kind` | `ChartKind`, `Line` | The chart type, and how X is laid out (by category or continuously). |
| `Theme` | `ChartTheme`, `Light` | `Light` or `Dark`, used when no `Style` is set. |
| `Style` | `ChartStyle?` | Colours, font and finish; wins over `Theme` and a cascaded style. |
| `Series` | `IReadOnlyList<ChartSeries>` | The data. At most 32 series, 100,000 points. |
| `XLabel`, `YLabel`, `Y2Label` | string | Axis titles. `Y2Label` names the right axis. On a gauge `YLabel` is the unit written after the score (`"%"`). |
| `Width`, `Height` | int, 900 × 420 | The SVG's viewBox, 320–4096 by 240–2160; the component scales it to its container, or with `FitWidth` draws it at the container's width. |
| `XAxis`, `YAxis`, `Y2Axis` | `AxisKind`, `Linear` | `Linear`, `Log`, or (X only) `Time` in Unix milliseconds. |
| `XFormat`, `YFormat`, `Y2Format` | `ValueFormat`, `Number` | `Duration` reads seconds; `Compact` writes 1.2k; `TimeOfDay` (0.27.0) reads seconds since a midnight as `HH:mm`, wrapping at 24 h, linear axes only. None on a time axis. |
| `YReversed`, `Y2Reversed` | bool | Smaller values higher (pace). Refused for kinds that draw from zero. |
| `YAxisSide` | `AxisSide`, `Left` | `Right` moves the main axis right; refused with a secondary series. |
| `YTickLabels` | `TickLabels`, `All` | `Ends` labels only the lowest and highest tick. |
| `XMin`, `XMax`, `YMin`, `YMax`, `Y2Min`, `Y2Max` | double? | Explicit axis bounds. |
| `IncludeZero` | bool | Force zero onto the value axis. |
| `TimeZone` | string? | IANA or Windows zone id for a time axis's calendar, e.g. `"Europe/London"`. |
| `SkipWeekends`, `TimeSkips` | bool, `IReadOnlyList<TimeSkip>` | Leave weekends or spans (`TimeAxis.Day(date)` for a holiday) out of a time axis. |
| `Annotations` | `IReadOnlyList<ChartAnnotation>` | Reference lines and bands behind the data. |
| `YZones` | `ZoneScale?` | Shades each zone as a band behind the main plot; on a calendar, colours each day by its zone. |
| `Panes` | `IReadOnlyList<ChartPane>` | Extra panes beneath the main plot; pane *k* is `Panes[k − 1]`. |
| `MinorGridlines` | bool | Lighter lines between labelled ticks. |
| `Bins` | int? | Histogram bin count; null chooses from the data. |
| `DensityCells` | int? | Scatter only: aggregate into shaded cells (8–200 across). |
| `MaxRenderedPoints` | int, 1200 | Line and area sampling budget per continuous run (min/max sampling keeps extremes). |
| `GaugeSweep` | double, 270 | Gauge only (0.26.0): how far round the arc runs, 180 (semicircle) to 360 (full circle), centred at the top. Any other kind refuses a value but 270. |
| `TimelineConnectors` | bool, true | Timeline only (0.27.0): join a span to the span in another lane that starts exactly where it ends with a thin vertical line, as a hypnogram does. `false` draws a plain state chart; every other kind refuses `false`. |
| `CalendarLayout` | `CalendarLayout`, `Weeks` | Calendar only (0.28.0): `Weeks` is the contribution grid, a column per week and a row per weekday, months named above; `Months` is a small grid per month, set left to right and wrapping. Other kinds refuse it set. |
| `CalendarCell` | `CalendarCell`, `Square` | Calendar only (0.28.0): each day as a rounded `Square` (corners `BarRadius` or 3 px), a `Dot`, or a `Bubble` whose area is proportional to its value, the largest filling its cell. Other kinds refuse it set. |
| `WeekStart` | `DayOfWeek`, `Monday` | Calendar only (0.28.0): the day each week starts on, ISO's Monday unless set. Other kinds refuse it set. |

## ChartSeries

`new ChartSeries(string Name, IReadOnlyList<ChartPoint> Points, string? Color = null)`, plus init properties:

| Member | Meaning |
|---|---|
| `Secondary` | Measure on the right-hand axis. At least one series per pane stays on the left. |
| `Kind` | Override this series' mark: `Line`, `Area`, `Column`, `Scatter`, `Band`, `Range` (0.27.0) or `Blocks` (0.29.0). Allowed on line, area, scatter, bubble, band, range, blocks, column, candlestick and OHLC charts; blocks are refused on a column chart. |
| `Pane` | 0 is the main plot; *k* needs `ChartSpec.Panes[k − 1]`. |
| `Trend` | Draw a trend (line, area, scatter, bubble marks): a least-squares line across the plot unless `TrendFit` says otherwise. Dashed in the series colour, named for assistive technology. |
| `TrendFit` | `TrendFit.Linear` (default), `MovingAverage`, `Polynomial` or `Exponential` (0.32.0). All are fitted in the space the chart draws, so on a log Y axis a moving average is the geometric mean and an exponential is straight. A polynomial or an exponential is drawn only across the X its observations cover (an exponential's positive ones), named `Load trend: quadratic fit, R squared 0.93` or `… exponential fit, rising, R squared 0.88` (R² on the logarithms, as Excel reports it); a moving average is drawn at the last point of each trailing window, named `HRV trend: 7-point moving average`. Refused without `Trend = true`. |
| `TrendPoints` | A moving average's window, 2–1000 points, 7 by default (0.32.0). A missing value holds its slot and adds nothing; the line breaks where fewer than half the window (rounded up) is present. Points must be in X order. Refused set on any other fit. |
| `TrendDegree` | A polynomial's degree, 2 (quadratic, default), 3 (cubic) or 4 (quartic) (0.32.0). Refused set on any other fit. |
| `ProjectedFrom` | Dash a line or area from this X onward (planned values). |
| `Zones` | Colour the series by the zone each value falls in (`ZoneScale`): lines, areas, scatter points, bubbles, columns, bars and blocks. |
| `Summary` | A precomputed `BoxSummary` for a box chart (then `Points` must be empty). |
| `StrokeWidth` | 0.5–12 px for line, area and band strokes. |
| `Curve` | `LineCurve.Linear`, `Smooth` (monotone, never overshoots), `Step`. |
| `Fill` | `AreaFill.Flat` or `Fade` (vertical gradient) on areas and columns. |
| `Gradient` | `IReadOnlyList<ColorStop>` — colour a stroke continuously by value; not with `Zones`. On a gauge it colours the arc along its length; not with `YZones`. |
| `Markers` | `MarkerStyle.Auto` (hover/focus in the refined finish), `None`, `Hollow`, `Filled`. |
| `HighlightLast` | Ring the latest point of a line or area. |
| `ValueLabels` | Print each column's or bar's value past its end when it fits. |
| `Goal` | Ring charts only (0.26.0): the ring's target, `double?`, positive, 100 when null; progress is `Y / Goal`. The point's `Label` is the unit (`"kcal"`). |

`ChartSeries.From<T>(name, items, x: item => …, y: item => …, label: item => …)` maps your own objects.

## ChartPoint

`new ChartPoint(double X, double? Y, string? Label = null, double Size = 1)` — `Size` is bubble area. Init properties: `Open`, `High`, `Low`, `Close`, `Color` (this mark's colour; beats zone and series colour), and `XEnd` (0.27.0, timelines and, from 0.29.0, blocks only: where a span or block ends, above `X`; Unix milliseconds on a time axis).

Factories: `ChartPoint.Candle(x, open, high, low, close, label?)`; `ChartPoint.Interval(x, y, low, high, label?)` for band and range points (`y` may be `null`; on a range it is the dot, and must lie between `low` and `high`); `ChartPoint.Span(start, end, label?)` (0.27.0) for a timeline span, with no `Y`; `ChartPoint.Block(start, end, height, label?)` (0.29.0) for a block from `start` to `end` rising to `height`; `ChartPoint.Observation(value)` for histogram, box and violin input.

## ChartPane

Init properties: `Label` (left axis title), `Weight` (height beside the main plot's 1, default 0.5), `YAxis`, `YMin`, `YMax`, `YFormat`, `YReversed`, `YZones`, and the same `Y2…` set for a right axis in that pane.

## ChartAnnotation

`new ChartAnnotation(AnnotationAxis Axis, double From)` with `To` (makes it a band), `Label`, `Color`, `Dashed` (default true). The drawn label is `Label: value` (or `Label: from to to` for a band), formatted by the axis, so pass a plain label and a rounded value. `AnnotationAxis.Y` is the value axis wherever the chart draws it; `X` is refused on category charts. Time-axis annotations take Unix milliseconds.

## Enums

`ChartKind { Line, Area, Scatter, Bubble, Column, Bar, StackedColumn, Donut, Heatmap, Radar, Candlestick, Band, Histogram, Box, Violin, Ohlc, Gauge, Ring, Timeline, Range, Calendar, Blocks }` · `CalendarLayout { Weeks, Months }` · `CalendarCell { Square, Dot, Bubble }` · `ChartTheme { Light, Dark }` · `AxisKind { Linear, Log, Time }` · `ValueFormat { Number, Duration, Compact, TimeOfDay }` · `LineCurve { Linear, Smooth, Step }` · `TrendFit { Linear, MovingAverage, Polynomial, Exponential }` (0.32.0) · `AreaFill { Flat, Fade }` · `MarkerStyle { Auto, None, Hollow, Filled }` · `AxisSide { Left, Right }` · `TickLabels { All, Ends }` · `GridLine { Solid, Dotted, Dashed, Hidden }` · `ChartFinish { Refined, Classic }` · `AnnotationAxis { X, Y }` · `GraphLayout { Circular, Layered }` · `GraphDirection { LeftToRight, TopToBottom }` (0.30.0).

## ChartStyle

A record of colours and typeface rendered into the SVG itself, so exports and the HTTP API carry it. Members: `Background`, `Text`, `Muted`, `Grid`, `Edge`, `Series` (palette), `Zones` (zone ramp, low to high), `Rising`, `Falling`, `HeatmapLow`, `HeatmapHigh`, `FontFamily`, `Gridlines` (`GridLine`), `Finish` (`ChartFinish`), `BarRadius` (null = 2 px; large = capsule).

Presets: `ChartStyle.Light`, `ChartStyle.Dark`, `ChartStyle.Midnight`. `ChartStyle.Light with { Finish = ChartFinish.Classic }` draws exactly as 0.23.0 did. `style.ContrastIssues()` lists WCAG failures (4.5:1 text, 3:1 marks); check any brand palette with it. `ChartStyle.FontFamilyFrom(css)` cleans a CSS font list.

```csharp
public static readonly ChartStyle Brand = new() {
    Background = "#F6F3EE", Text = "#1F2A37", Muted = "#4B5563", Grid = "#E5DED3",
    Series = ["#1D4E89", "#B03A2E", "#2E7D5B"], FontFamily = "Georgia,Cambria,serif"
};
```

## Rendering and export

- `ChartSvg.Render(spec, includeLegend = true, includeTitles = true)` → SVG string. Throws `ArgumentException` for an invalid spec.
- `ChartSvg.ResolveStyle(spec)`, `ChartSvg.SeriesColor(series, index, style)`, `ChartSvg.LegendKey(spec, index)` (a series' legend key as a small SVG) and `ChartSvg.LegendLabel(spec, index)` (what the legend writes: the name, and on a ring `Move: 540 of 600 kcal`, on a gauge `Recovery: 72 %`, on a timeline `REM 1:42, 22 %`).
- `ChartExport.Csv(spec)` → CSV of the original observations (time charts add an ISO `XTime` column, band and range series `Low,High`, ring charts a `Goal` column, timelines and blocks an `XEnd` column; a calendar writes each original point, not each day's total).

A calendar (`ChartKind.Calendar`, 0.28.0) takes one series on a time X axis. A point counts for the day its `X` falls on in `TimeZone`, and one day's points add up; a zero or null total is a rest day, drawn as an empty cell that takes no focus. Days run from the earliest point to the latest, or from `XMin` to `XMax`. Each day with activity is a focusable mark named `Tue 15 Sep 2026: 54, Moderate` (date, its points' labels, total in `YFormat`, zone); `PointSelected` reports the day's first point. With `YZones` a day takes its zone's colour; without, a ramp across the active days from a third of the way between the empty cell's `Grid` colour and `HeatmapHigh` up to `HeatmapHigh` (0.30.0; it does not use `HeatmapLow`), so the quietest day stands apart from a rest day on light and dark styles alike. An X annotation outlines its day and joins the key under the grid. Values must be nonnegative; `YMin`/`YMax` are refused, so the ramp cannot be pinned across charts.
Blocks (`ChartKind.Blocks`, or a series' `Kind = ChartKind.Blocks` on a continuous chart, 0.29.0) draw each point made with `ChartPoint.Block(start, end, height, label?)` as a block exactly from `X` to `XEnd`, standing on the bottom edge of its plot (its pane's, on its series' own axis, left or right) and rising to `Y`. `IncludeZero = true` raises them from zero; otherwise an axis fitted to the data reaches below the lowest block — the slowest, on a reversed axis — until it stands a sixth of the plot, unless that end's bound is set. Blocks of one series that touch are parted by a 1-pixel hairline, half from each (a quarter of the width of a block under 2 pixels); the far end is rounded by `BarRadius`, or 4 pixels, at most 6; colour is the point's `Color`, else its zone in the series' `Zones`, else the series colour. They draw in the column layer, under lines and points. Each is a focusable mark named `Lap 2: 1 to 2, 4:52` or `Interval 2: 10:00 to 14:00, 275, Lactate threshold` (label, span in `XFormat`, height in the axis's format, zone), led by its series' name when it has no label or several series draw blocks. Refused: overlap within a series, a missing `XEnd` or `Y`, a log X axis, column charts, `Trend`, `ProjectedFrom`.
- `ChartValidation.Validate(spec)` validates without rendering.

## Axes and time

- `TimeAxis.Value(DateTimeOffset)` → Unix milliseconds; `TimeAxis.Moment(double)` back.
- `TimeAxis.Weekends(from, to, zone?)`, `TimeAxis.Day(date, zone?)` → `TimeSkip`s; `TimeAxis.Zone(id)` resolves a zone.
- `Axis.Create(kind, values, …)`, `axis.Map`, `axis.Invert`, `axis.Ticks`, `axis.Format` give the geometry without SVG.

## Training metrics

Static class `Training`; power in watts, time in seconds, samples uniformly spaced.

| Member | Returns |
|---|---|
| `NormalizedPower(watts, sampleSeconds = 1)` | `double?` — 30 s rolling average, 4th-power mean, 4th root; null if shorter than 30 s. |
| `IntensityFactor(np, ftp)` | NP / FTP. |
| `StressScore(seconds, np, ftp)` | TSS = hours × IF² × 100. |
| `Load(IEnumerable<(DateOnly Day, double Stress)>, fitness = 0, fatigue = 0, fitnessDays = 42, fatigueDays = 7)` | `IReadOnlyList<LoadDay(Day, Stress, Fitness, Fatigue, Form)>` — TrainingPeaks' recurrence; every day from first to last; form uses yesterday's values. |
| `TimeInZone(samples, ZoneScale, sampleSeconds = 1)` | Seconds per zone, in scale order. |
| `MeanMaximal(samples, durations, sampleSeconds = 1)` | `(Seconds, Value)` best average per duration; `Training.StandardDurations` is 1 s to 4 h. |
| `CriticalPower(efforts)` | `CriticalPowerFit?(CriticalPower, WPrime, R2, Count)` — Monod fit over 3–20 minute efforts. |

## Statistics

`Statistics.Fit(points)` → `LinearFit?(Slope, Intercept, R2, Count)` · `Statistics.Polynomial(points, degree)` (degree 1–4, 0.32.0) → `PolynomialFit?(Coefficients, R2, Count)`, coefficients from the constant term up in your X, `Predict(x)` precise with Unix-millisecond X · `Statistics.Exponential(points)` (0.32.0) → `ExponentialFit?(A, B, R2, Count)` for `y = A·e^(B·x)` over the positive Y only, R² on the logarithms, `Predict(x)` (A can read 0 for Unix-millisecond X; `Predict` still holds) · each returns null where no fit exists · `Statistics.Rolling(values, window, minimum?)` → `RollingWindow?(Mean, Deviation, Count)` per entry (a moving average or a baseline band) · `Statistics.Summarize(values)` → `BoxSummary` · `Statistics.Quantile(sorted, p)` · `Statistics.Density(values, samples = 64)` · `Statistics.Bins(values, count?)` · `Statistics.SharedBins(sets, count?)`.

## Zones

`new Zone(string Name, double Upper, string? Color = null)` — `Upper` is inclusive; the last zone's is `double.PositiveInfinity`. `new ZoneScale(zones)`, `scale.IndexOf(value)`. Factories: `ZoneScale.CogganPower(ftp)` (seven levels) and `ZoneScale.CogganHeartRate(thresholdHeartRate)` (five levels). Zones without a colour take the style's `Zones` ramp.

## Graphs

`GraphSpec { Title, Nodes = [new GraphNode(id, label, color?)], Edges = [new GraphEdge(source, target, label?)], Layout = GraphLayout.Layered or Circular, Direction, Theme, Style, Width, Height }`. `GraphEngine.Render(graph, positions?)` → SVG; `GraphEngine.Layout(graph)`, `Routes(graph)`, `Crossings(graph)`. Layered graphs refuse cycles longer than a self-loop; use `Circular` for those.

`Direction = GraphDirection.TopToBottom` (0.30.0) puts a layered graph's levels in rows down the drawing, each level spread across the width, arrows pointing down; edges leave from under a node's label, edge labels stand beside their edge and a self-loop at its node's right. Circular graphs ignore it. `GraphEngine.Fit(graph, width)` returns the graph as it should be drawn in a box that wide (clamped 320–4096): unchanged when it fits; a layered graph turns top to bottom when its levels would stand closer than its widest label (cut to 22 characters) plus 16 px, or 70 px, and grows 110 px a level up to 2160; a level too full for the width even then takes the narrowest width that holds it; a circular graph grows taller instead, and widens where two nodes at one height cannot stand their labels 16 px apart. Never shorter than its own `Height`.

In every layout (0.31.0) an edge whose straight run from a node would cross that node's label meets the node at the foot of the label, 50 px below its centre, instead of running through the words; an edge's label takes the first place along its edge, from the middle outwards, above then below it (top to bottom: right then left), that keeps off every node, node label and earlier edge label, and otherwise stands at the middle as before. A circle stands in from the sides by half its widest label (or a node's radius) plus 24 px. An edge can still cross another node's label in a crowded graph, and an edge label can sit on another edge's line.

## Blazor components (`Lumen.Charts.Blazor`)

- `<LumenChart Spec="…" PointSelected="(PointSelection p) => …" />` — `PointSelection(SeriesIndex, PointIndex)`.
- `<LumenChart Spec="…" FitWidth="true" />` (0.25.0 and later) draws the chart at the width of its container instead of `Spec.Width`: measured once the component is interactive and again when the container settles at a new width, in whole pixels, never below 320. It lifts the stylesheet's 640 px minimum for that chart alone (a `lumen-fit` class on its root), keeps zoom, hidden series and point selection across a redraw, and exports SVG and PNG at the fitted width. Prerendered and static charts are drawn at `Spec.Width` and scaled until then. Default `false`, which renders as before.
- `<LumenGraph Spec="…" NodeSelected="(string id) => …" />` — draggable nodes in an interactive render mode. `FitWidth="true"` (0.30.0 and later) draws it as `GraphEngine.Fit` lays it out for its container's width, so a phone shows a deep layered graph top to bottom instead of scrolling sideways; dragged nodes keep their place in proportion and return to the layout when it turns.
- `<LumenBrand Series="--bs-primary, --bs-success" Background="--bs-body-bg" Text="--bs-body-color" Muted="--bs-secondary-color" Grid="--bs-border-color">…</LumenBrand>` reads the host page's CSS custom properties and cascades a `ChartStyle` to every chart inside. Or cascade one yourself: `<CascadingValue Value="Brand">…</CascadingValue>`.
- Stylesheet: `_content/Lumen.Charts.Blazor/lumen.css`. Chrome colours via `--lumen-accent`, `--lumen-control-border`, `--lumen-tooltip-bg`, `--lumen-tooltip-fg`.
