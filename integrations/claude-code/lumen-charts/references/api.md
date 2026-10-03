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
| `Title`, `Description`, `Source` | string | Heading, subtitle and source line; title and description are the SVG's accessible name. Default title "Untitled chart". From 0.35.0 each fits the drawing's width less 48, by the library's generous estimate of 11 px text: a description or source too wide goes on over a second line, broken between its ` · ` clauses where both lines then fit and otherwise between the words that set the two lines most nearly equal, past two lines ending in `…`; the plot moves down 14 px for a second description line and its bottom, X axis and axis title up 14 px for a second source line, which grows upward. A title stays one line, cut at a word with `…`. The whole of each stays in `<title>`, `<desc>` and the accessible name. Text that fits is drawn as before. |
| `Kind` | `ChartKind`, `Line` | The chart type, and how X is laid out (by category or continuously). |
| `Theme` | `ChartTheme`, `Light` | `Light` or `Dark`, used when no `Style` is set. |
| `Style` | `ChartStyle?` | Colours, font and finish; wins over `Theme` and a cascaded style. |
| `Series` | `IReadOnlyList<ChartSeries>` | The data. At most 32 series, 100,000 points. |
| `XLabel`, `YLabel`, `Y2Label` | string | Axis titles. `Y2Label` names the right axis. On a gauge `YLabel` is the unit written after the score (`"%"`). |
| `XTicks` | `TickSource`, `Auto` | What labels a continuous X axis (0.33.0). `Auto`: the points' `Label`s replace the ticks when 1–24 labelled points stand in view, each cut to 12 characters — except on a time axis, which keeps its dates unless every label fits uncut, so long names stay in the tooltips. `Axis`: always the axis's own ticks. `PointLabels`: always the labels, at any count, thinned until no two touch; the axis's ticks only where no labelled point is in view. Blocks' labels never label the axis. Line, area, scatter, bubble, candlestick, OHLC, band, range and blocks charts; others refuse it set. |
| `Width`, `Height` | int, 900 × 420 | The SVG's viewBox, 320–4096 by 240–2160 (a sparkline from 60 by 16); the component scales it to its container, or with `FitWidth` draws it at the container's width. |
| `Sparkline` | bool | Draw the data alone, word-sized (0.34.0): no title, description or source written, no axes, ticks, gridlines or legend, zone bands and annotations without their labels — no `<text>` at all. The plot fills the drawing but for a padding just wide enough for its largest mark at any edge: a highlight's ring 6.5, `HighlightLast`'s 10, a hollow marker 5, a scatter dot 4.5, a filled or hovered marker 4, a line half its stroke, columns 0 (a drawing too small for that keeps 2 units of plot). The title stays the accessible name and `<title>`, the description the `<desc>`, and every point its focusable, named mark and native tooltip. Shown at its own width (`width:Wpx;max-width:100%`), not its container's. 60–4096 by 16–2160. Line, area, scatter and column charts, their series drawn as those marks; panes and `ValueLabels` refused. The component draws it without legend, toolbar, zoom or table. |
| `XAxis`, `YAxis`, `Y2Axis` | `AxisKind`, `Linear` | `Linear`, `Log`, or (X only) `Time` in Unix milliseconds. |
| `XFormat`, `YFormat`, `Y2Format` | `ValueFormat`, `Number` | `Duration` reads seconds; `Compact` writes 1.2k; `TimeOfDay` (0.27.0) reads seconds since a midnight as `HH:mm`, wrapping at 24 h, linear axes only; `Signed` (0.36.0) writes a number as `Number` does with its sign, `+5`, `−5` (a true minus, U+2212) and `0`, in ticks, names, value labels, annotations, the readout and the data table. None on a time axis. |
| `YReversed`, `Y2Reversed` | bool | Smaller values higher (pace). Refused for kinds that draw from zero. |
| `YAxisSide` | `AxisSide`, `Left` | `Right` moves the main axis right; refused with a secondary series. |
| `YTickLabels` | `TickLabels`, `All` | `Ends` labels only the lowest and highest tick; `Bounds` (0.35.0) labels no tick, only the axis's two ends at their exact values in its format (with `IncludeZero`, `0` and the largest value); `None` (0.37.0) writes no label at all. In every pane that sets no `ChartPane.YTickLabels` of its own. Gridlines stay at every tick. |
| `XTickLabels` | `TickLabels`, `All` | Which X labels are written along the bottom (0.35.0): `All`; `Ends`, the first and last drawn, ticks or points' labels; or `Bounds`, only the axis's two ends at their exact values in its format, `XMin`/`XMax` or the data's ends, the first starting at the plot's left edge and the last ending at its right, never the points' labels. Gridlines stay. Line, area, scatter, bubble, candlestick, OHLC, band, range, blocks and timeline charts; others refuse it set, and `Bounds` is refused beside `XTicks = PointLabels`. `None` is refused: every pane reads its X from this axis. |
| `XMin`, `XMax`, `YMin`, `YMax`, `Y2Min`, `Y2Max` | double? | Explicit axis bounds. |
| `YMinSpan` | double? | The least the main plot's left axis spans, centred on its data (0.34.0): when the data's range is smaller, the axis runs from (min + max) / 2 − span / 2 to (min + max) / 2 + span / 2, so a 0.3 kg wobble on `YMinSpan = 8` reads as small; data wider than the span fits as before. A band's or range's bounds count as data; a secondary series does not. Positive; refused beside `YMin`/`YMax`, on a log axis, with `IncludeZero`, on kinds drawn from zero and when the axis carries columns or an area; line, scatter, bubble, band, range, candlestick, OHLC and blocks charts take it, reversed or not. `ChartPane.YMinSpan` sets a pane's. |
| `YSymmetric` | double? | Holds the main plot's left axis symmetric about zero (0.36.0): it runs from −m to +m, m the largest of this value and the data's distance from zero either way, so zero stays in the middle and form of +4 and −4 stand equally far from it. Positive; refused beside `YMin`, `YMax` or `YMinSpan`, on a log axis, and on kinds without such an axis (donut, heatmap, radar, histogram, box, violin, gauge, ring, timeline, calendar); allowed reversed and beside `IncludeZero`. A secondary series is not measured on it. `ChartPane.YSymmetric` sets a pane's. Pair it with `YFormat = ValueFormat.Signed`. |
| `IncludeZero` | bool | Force zero onto the value axis. |
| `TimeZone` | string? | IANA or Windows zone id for a time axis's calendar, e.g. `"Europe/London"`. |
| `SkipWeekends`, `TimeSkips` | bool, `IReadOnlyList<TimeSkip>` | Leave weekends or spans (`TimeAxis.Day(date)` for a holiday) out of a time axis. |
| `Annotations` | `IReadOnlyList<ChartAnnotation>` | Reference lines and bands behind the data, or over it with `InFront`. |
| `YZones` | `ZoneScale?` | Shades each zone as a band behind the main plot; on a calendar, colours each day by its zone. |
| `Panes` | `IReadOnlyList<ChartPane>` | Extra panes beneath the main plot, at most five (0.37.0; three before), so six plots; pane *k* is `Panes[k − 1]`. |
| `PaneTitles` | `PaneTitlePlacement`, `Axis` | `Above` (0.37.0) writes the main plot's `YLabel` and each pane's `Label` as one horizontal line over the plot's left edge, in the text colour, cut with `…` to the plot's width, its whole kept as its accessible name and tooltip (put the key fact first); the main plot moves down 18 and the gap between plots grows from 24 to 30. A right-hand title stays up the side. With no tick label written up the left (`TickLabels.None` everywhere) the left margin narrows from 76 to 30. Line, area, scatter, bubble, column, stacked column, band, range, candlestick, OHLC and blocks charts; others and sparklines refuse it. |
| `MinorGridlines` | bool | Lighter lines between labelled ticks. |
| `Bins` | int? | Histogram bin count; null chooses from the data. |
| `DensityCells` | int? | Scatter only: aggregate into shaded cells (8–200 across). |
| `MaxRenderedPoints` | int, 1200 | Line and area sampling budget per continuous run, 16–5000. From 0.37.0 a run longer than it is thinned over `XMin`–`XMax` only, with the nearest point outside each side, so a zoomed view draws more detail; a run within it is drawn whole. |
| `Sampling` | `SamplingMethod`, `MinMax` | `MinMax` keeps each bucket's lowest and highest point. `Average` (0.37.0) cuts the X range shown into `MaxRenderedPoints` equal slices, the same for every series, and draws each run's points in a slice as one point at their mean X and Y, the mean rounded to the series' own precision (at most two places), named `, average of N points` and reporting the slice's first point; the shared readout says `average of 12 s` once in each column's label and reads entries plainly; applies to series drawn as lines or areas whose points in view outnumber the budget. A highlighted point, and a `HighlightLast` point, keeps its own mark beside the average. Bands, trends and other marks keep `MinMax`. |
| `GaugeSweep` | double, 270 | Gauge only (0.26.0): how far round the arc runs, 180 (semicircle) to 360 (full circle), centred at the top. Any other kind refuses a value but 270. |
| `TimelineConnectors` | bool, true | Timeline only (0.27.0): join a span to the span in another lane that starts exactly where it ends with a thin vertical line, as a hypnogram does. `false` draws a plain state chart; every other kind refuses `false`. |
| `CalendarLayout` | `CalendarLayout`, `Weeks` | Calendar only (0.28.0): `Weeks` is the contribution grid, a column per week and a row per weekday, months named above; `Months` is a small grid per month, set left to right and wrapping. Other kinds refuse it set. |
| `CalendarCell` | `CalendarCell`, `Square` | Calendar only (0.28.0): each day as a rounded `Square` (corners `BarRadius` or 3 px), a `Dot`, or a `Bubble` whose area is proportional to its value, the largest filling its cell. Other kinds refuse it set. |
| `SharedReadout` | bool | The component alone (0.36.0): one vertical guide through every pane at the X nearest the pointer, a tap or the focused point, a ring round each shown series' point there, and one tooltip reading the X and then each series' name and value in legend order, with its `ValueNote`, zone and change words, `Form −8.7, worse than the previous`; a missing value reads `missing`. A series is read where it has a point within half the closest spacing of the X values; blocks by the X they cover. The arrow keys then step by X (Left/Right, Home/End, Page Up/Down ten), Up/Down move between the series there, Escape hides it, and the status line reads `3 Jun 2026 · Fitness 52.3 · …`. Never in the SVG or the gradient hash: `ChartSvg.Render` draws the same chart with it or without. Line, area, scatter, bubble, band, range, candlestick, OHLC and blocks charts; a sparkline and the other kinds refuse it. `ChartSvg.Readout(spec)` returns the same table (`ChartReadout` → `ReadoutColumn` → `ReadoutEntry`, positions in viewBox units) for a host that draws its own. |
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
| `ValueLabels` | Print each column's or bar's value past its end when it fits, in the text colour. From 0.33.0 also each line or scatter point's value, 4 px above its marker, in the point's colour (its change colour where it has one, a gradient's colour at its value) where that colour clears 4.5:1 against the background, else the style's `Text` colour, at 11 px weight 600, its `ValueNote` after it muted at normal weight, over a 3 px halo in the background colour. It moves in from the plot's left and right edges so it is never cut, goes 4 px below the marker where above would leave the plot or meet a value label already written (columns' included), and is left out with room in neither place; the value stays in the mark's name. Area, band, bubble and stacked series refuse it. |
| `ChangeColors` | `ChangeColors.None` (default), `HigherIsBetter` or `LowerIsBetter` (0.33.0): colour each point of a line or scatter series by how it changed from the nearest earlier point with a value — the style's `Rising` colour when better, `Falling` when worse, the series colour when level or first. Better follows the setting, not the screen, so it holds on a reversed axis and on a shared scale. The marker and the segment arriving at the point take the colour (a point's `Color` colours the segment leaving it); a gap draws no segment, but the point after it compares with the last value before it. Each mark's name and tooltip add `, better than the previous`, `, worse than the previous` or `, level with the previous`; the first adds nothing. Refused on other marks, beside `Zones`, a `Gradient` or point colours, on scatter points out of X order and on a density scatter. |
| `Goal` | Ring charts only (0.26.0): the ring's target, `double?`, positive, 100 when null; progress is `Y / Goal`. The point's `Label` is the unit (`"kcal"`). |

`ChartSeries.From<T>(name, items, x: item => …, y: item => …, label: item => …)` maps your own objects.

## ChartPoint

`new ChartPoint(double X, double? Y, string? Label = null, double Size = 1)` — `Size` is bubble area. Init properties: `Open`, `High`, `Low`, `Close`, `Color` (this mark's colour; beats zone and series colour), `XEnd` (0.27.0, timelines and, from 0.29.0, blocks only: where a span or block ends, above `X`; Unix milliseconds on a time axis), and `Highlight` (0.34.0: a `#RRGGBB` colour that rings this point of a line or scatter series with an enlarged marker, radius 5.5, outlined 2 px in the background colour, whatever the series' `Markers` and in sparklines and full charts alike; the line keeps its colour and course, sampling keeps the point, and `HighlightLast`'s ring takes the colour. Pair it with a `ValueNote` such as `" · PB"`, so the tooltip and accessible name say why the point is ringed — the colour is never the only cue. Refused on areas and every other mark, on a density scatter, and in any other colour form), and `ValueNote` (0.33.0: at most 20 characters written straight after the value — include any space yourself — such as `"/48"` for a field size or `" inside baseline"` for a status: muted after a value label, and after the value in the mark's tooltip and accessible name, `Position: 16-05-2026, 24/48, better than the previous`, the component's data table and status line, and a CSV `Note` column. A missing value has none in its name; the CSV keeps it. Refused on candles, range bars, histograms, boxes, violins, timelines, calendars, gauges and rings).

Factories: `ChartPoint.Candle(x, open, high, low, close, label?)`; `ChartPoint.Interval(x, y, low, high, label?)` for band and range points (`y` may be `null`; on a range it is the dot, and must lie between `low` and `high`); `ChartPoint.Span(start, end, label?)` (0.27.0) for a timeline span, with no `Y`; `ChartPoint.Block(start, end, height, label?)` (0.29.0) for a block from `start` to `end` rising to `height`; `ChartPoint.Observation(value)` for histogram, box and violin input.

## ChartPane

Init properties: `Label` (left axis title), `Weight` (height beside the main plot's 1, default 0.5), `YAxis`, `YMin`, `YMax`, `YMinSpan` (0.34.0), `YSymmetric` (0.36.0), `YFormat`, `YReversed`, `YTickLabels` (0.37.0, null takes the spec's), `YZones`, and the same `Y2…` set for a right axis in that pane.

## ChartAnnotation

`new ChartAnnotation(AnnotationAxis Axis, double From)` with `To` (makes it a band), `Label`, `Color`, `Dashed` (default true), `ShowValue` (default true) and `InFront` (default false), both 0.35.0. The drawn label is `Label: value` (or `Label: from to to` for a band), formatted by the axis, so pass a plain label and a rounded value. `ShowValue = false` draws the `Label` alone (`median`), and its room is measured on those words; the tooltip and accessible name still read `median: 47:12`. It needs a `Label`, and is refused on a calendar, whose key names an outlined day by its label alone already. `InFront = true` draws the line or band over the data rather than behind it, in every pane an X annotation crosses and on a timeline, so columns and blocks do not hide it, a line on a halo of the background colour 2 px wider than itself so it shows over a mark of any colour; its label is over the data either way. A gauge (its target is drawn over the score already) and a calendar refuse it. `AnnotationAxis.Y` is the value axis wherever the chart draws it; `X` is refused on category charts. Time-axis annotations take Unix milliseconds.

## Enums

`ChartKind { Line, Area, Scatter, Bubble, Column, Bar, StackedColumn, Donut, Heatmap, Radar, Candlestick, Band, Histogram, Box, Violin, Ohlc, Gauge, Ring, Timeline, Range, Calendar, Blocks }` · `ChangeColors { None, HigherIsBetter, LowerIsBetter }` (0.33.0) · `TickSource { Auto, Axis, PointLabels }` (0.33.0) · `CalendarLayout { Weeks, Months }` · `CalendarCell { Square, Dot, Bubble }` · `ChartTheme { Light, Dark }` · `AxisKind { Linear, Log, Time }` · `ValueFormat { Number, Duration, Compact, TimeOfDay, Signed }` · `LineCurve { Linear, Smooth, Step }` · `TrendFit { Linear, MovingAverage, Polynomial, Exponential }` (0.32.0) · `AreaFill { Flat, Fade }` · `MarkerStyle { Auto, None, Hollow, Filled }` · `AxisSide { Left, Right }` · `TickLabels { All, Ends, Bounds, None }` (`Bounds` 0.35.0, `None` 0.37.0) · `SamplingMethod { MinMax, Average }` (0.37.0) · `PaneTitlePlacement { Axis, Above }` (0.37.0) · `GridLine { Solid, Dotted, Dashed, Hidden }` · `ChartFinish { Refined, Classic }` · `AnnotationAxis { X, Y }` · `GraphLayout { Circular, Layered }` · `GraphDirection { LeftToRight, TopToBottom }` (0.30.0).

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
- `ChartExport.Csv(spec)` → CSV of the original observations (time charts add an ISO `XTime` column, band and range series `Low,High`, ring charts a `Goal` column, timelines and blocks an `XEnd` column, and a chart with any `ValueNote` a last `Note` column (0.33.0); a calendar writes each original point, not each day's total).

A calendar (`ChartKind.Calendar`, 0.28.0) takes one series on a time X axis. A point counts for the day its `X` falls on in `TimeZone`, and one day's points add up; a zero or null total is a rest day, drawn as an empty cell that takes no focus. Days run from the earliest point to the latest, or from `XMin` to `XMax`. Each day with activity is a focusable mark named `Tue 15 Sep 2026: 54, Moderate` (date, its points' labels, total in `YFormat`, zone); `PointSelected` reports the day's first point. With `YZones` a day takes its zone's colour; without, a ramp across the active days from a third of the way between the empty cell's `Grid` colour and `HeatmapHigh` up to `HeatmapHigh` (0.30.0; it does not use `HeatmapLow`), so the quietest day stands apart from a rest day on light and dark styles alike. An X annotation outlines its day and joins the key under the grid. Values must be nonnegative; `YMin`/`YMax` are refused, so the ramp cannot be pinned across charts.
Blocks (`ChartKind.Blocks`, or a series' `Kind = ChartKind.Blocks` on a continuous chart, 0.29.0) draw each point made with `ChartPoint.Block(start, end, height, label?)` as a block exactly from `X` to `XEnd`, standing on the bottom edge of its plot (its pane's, on its series' own axis, left or right) and rising to `Y`. `IncludeZero = true` raises them from zero; otherwise an axis fitted to the data reaches below the lowest block — the slowest, on a reversed axis — until it stands a sixth of the plot, unless that end's bound is set. Blocks of one series that touch are parted by a 1-pixel hairline, half from each (a quarter of the width of a block under 2 pixels); the far end is rounded by `BarRadius`, or 4 pixels, at most 6; a block whose value stands above the bottom of its axis is at least 2 pixels tall (0.35.0), so a count of one among hundreds still shows, while one at the bottom, a count of zero, draws nothing visible and stays a named, focusable mark; colour is the point's `Color`, else its zone in the series' `Zones`, else the series colour. They draw in the column layer, under lines and points. Each is a focusable mark named `Lap 2: 1 to 2, 4:52` or `Interval 2: 10:00 to 14:00, 275, Lactate threshold` (label, span in `XFormat`, height in the axis's format, zone), led by its series' name when it has no label or several series draw blocks. Refused: overlap within a series, a missing `XEnd` or `Y`, a log X axis, column charts, `Trend`, `ProjectedFrom`.
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
- Keys (0.36.0): every chart has one roving point stop beside its scrolling viewport's own (`role=region`, which Tab reaches first) — its script leaves one mark at `tabindex=0` (the first point of the first series, then the last one focused) and the rest at −1, while the static SVG keeps every mark at 0. Left/Right move along a series, skipping gaps; Up/Down to the nearest point at that X in the series before or after (then the chart's references and other aggregates); Home/End to its ends; Page Up/Down ten points; Enter/Space select; Escape hides the tooltip. The keys are named in hidden words that the script makes the viewport's `aria-describedby` (a sparkline's drawing's). `LumenGraph`'s arrow keys still move a node. With `SharedReadout` the keys step the readout by X instead. Test the keys with real input (Playwright's `keyboard.press`): a `KeyboardEvent` dispatched from script can move focus without bringing up the readout.
- Drag to zoom (0.37.0): on a chart with zoom, a mouse or pen pressed on the plots and dragged 8 px or more draws a translucent band through every pane, and letting go zooms to its X (no narrower than 1 % of the whole); Escape lets it go, a shorter drag is a click, and a touch keeps the tap readout. Reset view restores. `ChartSvg.Plot(spec)` returns a `ChartPlot(Left, Right, Top, Bottom, X)`: where the X axis's ends and the plots' top and bottom stand in the drawing's units, and the `Axis` to `Map` and `Invert` with them, or null for a chart without a continuous X axis.
- `<LumenChart Spec="…" FitWidth="true" />` (0.25.0 and later) draws the chart at the width of its container instead of `Spec.Width`: measured once the component is interactive and again when the container settles at a new width, in whole pixels, never below 320 (60 for a sparkline). It lifts the stylesheet's 640 px minimum for that chart alone (a `lumen-fit` class on its root), keeps zoom, hidden series and point selection across a redraw, and exports SVG and PNG at the fitted width. Prerendered and static charts are drawn at `Spec.Width` and scaled until then. Default `false`, which renders as before.
- A sparkline (`Spec.Sparkline = true`, 0.34.0) renders as its drawing alone in a root marked `lumen-chart lumen-spark`: no legend, toolbar, zoom, data table or scrolling region, its marks' tooltips the only chrome. A tooltip stands just above the drawing, as wide as its words (at most 280 px) and kept 16 px inside the page, rather than over the line. Point selection and `PointSelected` work as on a chart.
- `<LumenGraph Spec="…" NodeSelected="(string id) => …" />` — draggable nodes in an interactive render mode. `FitWidth="true"` (0.30.0 and later) draws it as `GraphEngine.Fit` lays it out for its container's width, so a phone shows a deep layered graph top to bottom instead of scrolling sideways; dragged nodes keep their place in proportion and return to the layout when it turns.
- `<LumenBrand Series="--bs-primary, --bs-success" Background="--bs-body-bg" Text="--bs-body-color" Muted="--bs-secondary-color" Grid="--bs-border-color">…</LumenBrand>` reads the host page's CSS custom properties and cascades a `ChartStyle` to every chart inside. Or cascade one yourself: `<CascadingValue Value="Brand">…</CascadingValue>`.
- Stylesheet: `_content/Lumen.Charts.Blazor/lumen.css`. Chrome colours via `--lumen-accent`, `--lumen-control-border`, `--lumen-tooltip-bg`, `--lumen-tooltip-fg`.
