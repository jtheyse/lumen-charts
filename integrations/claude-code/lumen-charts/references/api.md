# Lumen.Charts API reference

Every public type a chart needs, by namespace `Lumen.Charts` unless stated. All specs are immutable records: build with an object initializer, change with `with { … }`.

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
| `XLabel`, `YLabel`, `Y2Label` | string | Axis titles. `Y2Label` names the right axis. |
| `Width`, `Height` | int, 900 × 420 | The SVG's viewBox; the component scales it to its container. |
| `XAxis`, `YAxis`, `Y2Axis` | `AxisKind`, `Linear` | `Linear`, `Log`, or (X only) `Time` in Unix milliseconds. |
| `XFormat`, `YFormat`, `Y2Format` | `ValueFormat`, `Number` | `Duration` reads seconds; `Compact` writes 1.2k. Not on a time axis. |
| `YReversed`, `Y2Reversed` | bool | Smaller values higher (pace). Refused for kinds that draw from zero. |
| `YAxisSide` | `AxisSide`, `Left` | `Right` moves the main axis right; refused with a secondary series. |
| `YTickLabels` | `TickLabels`, `All` | `Ends` labels only the lowest and highest tick. |
| `XMin`, `XMax`, `YMin`, `YMax`, `Y2Min`, `Y2Max` | double? | Explicit axis bounds. |
| `IncludeZero` | bool | Force zero onto the value axis. |
| `TimeZone` | string? | IANA or Windows zone id for a time axis's calendar, e.g. `"Europe/London"`. |
| `SkipWeekends`, `TimeSkips` | bool, `IReadOnlyList<TimeSkip>` | Leave weekends or spans (`TimeAxis.Day(date)` for a holiday) out of a time axis. |
| `Annotations` | `IReadOnlyList<ChartAnnotation>` | Reference lines and bands behind the data. |
| `YZones` | `ZoneScale?` | Shades each zone as a band behind the main plot. |
| `Panes` | `IReadOnlyList<ChartPane>` | Extra panes beneath the main plot; pane *k* is `Panes[k − 1]`. |
| `MinorGridlines` | bool | Lighter lines between labelled ticks. |
| `Bins` | int? | Histogram bin count; null chooses from the data. |
| `DensityCells` | int? | Scatter only: aggregate into shaded cells (8–200 across). |
| `MaxRenderedPoints` | int, 1200 | Line and area sampling budget per continuous run (min/max sampling keeps extremes). |

## ChartSeries

`new ChartSeries(string Name, IReadOnlyList<ChartPoint> Points, string? Color = null)`, plus init properties:

| Member | Meaning |
|---|---|
| `Secondary` | Measure on the right-hand axis. At least one series per pane stays on the left. |
| `Kind` | Override this series' mark: `Line`, `Area`, `Column`, `Scatter` or `Band`. Allowed on line, area, scatter, bubble, band, column, candlestick and OHLC charts. |
| `Pane` | 0 is the main plot; *k* needs `ChartSpec.Panes[k − 1]`. |
| `Trend` | Draw a least-squares line (line, area, scatter, bubble marks). |
| `ProjectedFrom` | Dash a line or area from this X onward (planned values). |
| `Zones` | Colour the series by the zone each value falls in (`ZoneScale`). |
| `Summary` | A precomputed `BoxSummary` for a box chart (then `Points` must be empty). |
| `StrokeWidth` | 0.5–12 px for line, area and band strokes. |
| `Curve` | `LineCurve.Linear`, `Smooth` (monotone, never overshoots), `Step`. |
| `Fill` | `AreaFill.Flat` or `Fade` (vertical gradient) on areas and columns. |
| `Gradient` | `IReadOnlyList<ColorStop>` — colour a stroke continuously by value; not with `Zones`. |
| `Markers` | `MarkerStyle.Auto` (hover/focus in the refined finish), `None`, `Hollow`, `Filled`. |
| `HighlightLast` | Ring the latest point of a line or area. |
| `ValueLabels` | Print each column's or bar's value past its end when it fits. |

`ChartSeries.From<T>(name, items, x: item => …, y: item => …, label: item => …)` maps your own objects.

## ChartPoint

`new ChartPoint(double X, double? Y, string? Label = null, double Size = 1)` — `Size` is bubble area. Init properties: `Open`, `High`, `Low`, `Close`, `Color` (this mark's colour; beats zone and series colour).

Factories: `ChartPoint.Candle(x, open, high, low, close, label?)`; `ChartPoint.Interval(x, y, low, high, label?)` for band charts and band series; `ChartPoint.Observation(value)` for histogram, box and violin input.

## ChartPane

Init properties: `Label` (left axis title), `Weight` (height beside the main plot's 1, default 0.5), `YAxis`, `YMin`, `YMax`, `YFormat`, `YReversed`, `YZones`, and the same `Y2…` set for a right axis in that pane.

## ChartAnnotation

`new ChartAnnotation(AnnotationAxis Axis, double From)` with `To` (makes it a band), `Label`, `Color`, `Dashed` (default true). The drawn label is `Label: value` (or `Label: from to to` for a band), formatted by the axis, so pass a plain label and a rounded value. `AnnotationAxis.Y` is the value axis wherever the chart draws it; `X` is refused on category charts. Time-axis annotations take Unix milliseconds.

## Enums

`ChartKind { Line, Area, Scatter, Bubble, Column, Bar, StackedColumn, Donut, Heatmap, Radar, Candlestick, Band, Histogram, Box, Violin, Ohlc }` · `ChartTheme { Light, Dark }` · `AxisKind { Linear, Log, Time }` · `ValueFormat { Number, Duration, Compact }` · `LineCurve { Linear, Smooth, Step }` · `AreaFill { Flat, Fade }` · `MarkerStyle { Auto, None, Hollow, Filled }` · `AxisSide { Left, Right }` · `TickLabels { All, Ends }` · `GridLine { Solid, Dotted, Dashed, Hidden }` · `ChartFinish { Refined, Classic }` · `AnnotationAxis { X, Y }` · `GraphLayout { Circular, Layered }`.

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
- `ChartSvg.ResolveStyle(spec)`, `ChartSvg.SeriesColor(series, index, style)`.
- `ChartExport.Csv(spec)` → CSV of the original observations (time charts add an ISO `XTime` column).
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

`Statistics.Fit(points)` → `LinearFit?(Slope, Intercept, R2, Count)` · `Statistics.Rolling(values, window, minimum?)` → `RollingWindow?(Mean, Deviation, Count)` per entry (a moving average or a baseline band) · `Statistics.Summarize(values)` → `BoxSummary` · `Statistics.Quantile(sorted, p)` · `Statistics.Density(values, samples = 64)` · `Statistics.Bins(values, count?)` · `Statistics.SharedBins(sets, count?)`.

## Zones

`new Zone(string Name, double Upper, string? Color = null)` — `Upper` is inclusive; the last zone's is `double.PositiveInfinity`. `new ZoneScale(zones)`, `scale.IndexOf(value)`. Factories: `ZoneScale.CogganPower(ftp)` (seven levels) and `ZoneScale.CogganHeartRate(thresholdHeartRate)` (five levels). Zones without a colour take the style's `Zones` ramp.

## Graphs

`GraphSpec { Title, Nodes = [new GraphNode(id, label, color?)], Edges = [new GraphEdge(source, target, label?)], Layout = GraphLayout.Layered or Circular, Theme, Style, Width, Height }`. `GraphEngine.Render(graph, positions?)` → SVG; `GraphEngine.Layout(graph)`, `Routes(graph)`, `Crossings(graph)`. Layered graphs refuse cycles longer than a self-loop; use `Circular` for those.

## Blazor components (`Lumen.Charts.Blazor`)

- `<LumenChart Spec="…" PointSelected="(PointSelection p) => …" />` — `PointSelection(SeriesIndex, PointIndex)`.
- `<LumenGraph Spec="…" NodeSelected="(string id) => …" />` — draggable nodes in an interactive render mode.
- `<LumenBrand Series="--bs-primary, --bs-success" Background="--bs-body-bg" Text="--bs-body-color" Muted="--bs-secondary-color" Grid="--bs-border-color">…</LumenBrand>` reads the host page's CSS custom properties and cascades a `ChartStyle` to every chart inside. Or cascade one yourself: `<CascadingValue Value="Brand">…</CascadingValue>`.
- Stylesheet: `_content/Lumen.Charts.Blazor/lumen.css`. Chrome colours via `--lumen-accent`, `--lumen-control-border`, `--lumen-tooltip-bg`, `--lumen-tooltip-fg`.
