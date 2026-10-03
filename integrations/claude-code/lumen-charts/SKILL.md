---
name: lumen-charts
description: Add charts, graphs and dashboards to a C#, .NET, ASP.NET Core or Blazor project with Lumen.Charts — from line, column and donut to candlestick, box, violin, gauges, activity rings, sleep timelines, calendars and network graphs — rendered as accessible SVG on the server or as an interactive Blazor component, with SVG, PNG and CSV export. Strong on sports and training data — fitness, fatigue and form (CTL/ATL/TSB), heart-rate and power zones, power–duration curves, reversed pace axes, activity streams in panes, recovery gauges and sleep stages, plus the numbers behind them (normalized power, TSS, critical power). Use this skill whenever a .NET or Blazor project needs any chart or graph, server-rendered SVG, a charting HTTP endpoint, or fitness, training or wearable data visualised, even if the user never names Lumen. Covers installing the packages, which come from GitHub releases rather than nuget.org.
---

# Lumen.Charts

Lumen.Charts is a standalone C# chart library: no JavaScript charting engine and no CDN. The core renders a `ChartSpec` to an SVG string anywhere .NET runs; a Blazor component adds tooltips, zoom, legend toggles, a data table and exports; an ASP.NET Core package exposes it as an HTTP API. Source, docs and releases: https://github.com/jtheyse/lumen-charts (MIT). Libraries target .NET 8 and later.

Three packages — add only what the project needs:

| Package | Use it for |
|---|---|
| `Lumen.Charts` | Rendering to SVG or CSV from any .NET code: console apps, reports, background jobs, APIs. Also the training metrics. |
| `Lumen.Charts.Blazor` | The `<LumenChart>` and `<LumenGraph>` components in a Blazor Web App or WebAssembly app. Brings `Lumen.Charts`. |
| `Lumen.Charts.AspNetCore` | `app.MapLumenCharts()`: POST a chart as JSON, get SVG or CSV back. Brings `Lumen.Charts`. |

## 1. Install

The packages are attached to each GitHub release; they are **not on nuget.org**, so `dotnet add package` alone fails until a local feed exists. Run the bundled script from the **repository or solution root**, not from inside a web project's folder: the Web SDK copies a `nuget.config` it finds there into the build output and publishes it. The script it finds the latest release, downloads the three `.nupkg` files into `./local-packages`, and writes a `nuget.config` that lists that folder beside nuget.org (still needed for Lumen's own dependencies):

```bash
bash <skill-dir>/scripts/install.sh            # Git Bash, macOS, Linux
pwsh <skill-dir>/scripts/install.ps1           # Windows PowerShell 7
```

Both take an optional version tag (`v0.29.0`) to pin one. If the project already has a `nuget.config`, the scripts leave it alone and print the source line to add instead. Then:

```bash
dotnet add package Lumen.Charts.Blazor        # or Lumen.Charts / Lumen.Charts.AspNetCore
```

Commit `nuget.config` and `local-packages/` (three small files), or CI will not restore. To upgrade later, rerun the script and bump the version.

A machine that has restored Lumen before can do so from its NuGet cache without any of this, which hides a missing feed until CI or a colleague's machine fails. To prove the feed works, restore into an empty folder: `dotnet restore --packages <empty folder> --no-http-cache --force` — the log should say `Installed Lumen.Charts … from …local-packages`. Teams that want to stop a same-named package arriving from another feed can add `packageSourceMapping` to `nuget.config` (`Lumen.*` → the local source, `*` → nuget.org); note that once any mapping exists every package must map to a source, so include any private feeds the project already uses.

## 2. Draw something

**Anywhere (server, console, report):**

```csharp
using Lumen.Charts;

var spec = new ChartSpec {
    Title = "Monthly revenue", Description = "Revenue by month, ZAR thousands", Source = "Source: accounting export",
    Kind = ChartKind.Column, XLabel = "Month", YLabel = "Revenue (ZAR thousands)",
    Series = [new("Revenue", [new(1, 24, "Jan"), new(2, 38, "Feb"), new(3, 31, "Mar")])]
};
string svg = ChartSvg.Render(spec);          // self-contained SVG, write it to a file or a response
string csv = ChartExport.Csv(spec);          // the original observations
```

**Blazor:** add `@using Lumen.Charts` and `@using Lumen.Charts.Blazor` to `_Imports.razor`, put `<link rel="stylesheet" href="_content/Lumen.Charts.Blazor/lumen.css" />` in the host page's `<head>`, then `<LumenChart Spec="spec" PointSelected="OnPoint" />`. Rendering works in static server rendering, but the toolbar (zoom, pan, exports, data table), tooltips and callbacks need an interactive render mode (`@rendermode InteractiveServer`, or WebAssembly). `PointSelected` gives a `PointSelection(SeriesIndex, PointIndex)` into the original series.

Don't name a page or component `Training` (`Training.razor`): its generated class hides `Lumen.Charts.Training`, and `Training.Load(...)` then fails with CS0117. Call it `TrainingPage.razor` (keep `@page "/training"`), or write `Lumen.Charts.Training.Load(...)`.

Charts are drawn at `ChartSpec.Width` (900 by default) and scale down to their container, but the stylesheet keeps them at least 640 px wide and scrolls them sideways inside their own box below that, so text stays legible. For a phone layout or a narrow card, write `<LumenChart Spec="spec" FitWidth="true" />` (0.25.0 and later): once the component is interactive it measures its container, redraws at that width whenever it settles at a new one (never below 320 px), and lifts the 640 px minimum for that chart alone, so the chart fills its box with its text at its own size; zoom, hidden series and the exports keep working, at the fitted width. Before that, in the prerender or in static rendering, it is drawn at `Width` and scaled to fit. `Height` is kept, so choose one that reads well on a phone too. Only for SVG rendered on the server with `ChartSvg.Render`, which nothing measures, set `Width` to the width it will be shown at (about 360 on a phone) yourself.

**HTTP API:** see `references/http-api.md`.

## 3. How a chart is described

Everything is one immutable `ChartSpec` record; change one with `with { … }`. The full member list is in `references/api.md` — read it before using anything not shown here. From 0.25.0 the packages also carry XML documentation beside each DLL, so IntelliSense shows what a member does, and you can read it in the NuGet cache (`~/.nuget/packages/lumen.charts/<version>/lib/net8.0/Lumen.Charts.xml`, and likewise for `lumen.charts.blazor` and `lumen.charts.aspnetcore`).

- `Kind` picks the chart: `Line, Area, Scatter, Bubble, Column, Bar, StackedColumn, Donut, Heatmap, Radar, Candlestick, Ohlc, Band, Histogram, Box, Violin, Gauge, Ring, Timeline, Range, Calendar, Blocks`.
- `Series` is a list of `ChartSeries(name, points, color?)`; a point is `ChartPoint(x, y, label?, size)`. A null `y` is a missing observation, drawn as a gap, never as zero. `ChartSeries.From(name, items, x, y, label)` maps your own objects.
- Category charts (column, bar, stacked, donut, radar, heatmap) place points by order and show their `Label`; continuous charts (line, area, scatter, bubble, band, range, blocks, candlestick, OHLC, timeline) place them by `X`.
- **Gauges and rings** (0.26.0 and later) draw one point per series round an arc, with no X axis. `ChartKind.Gauge`: one series of one point, `Y` the score and its `Label` the caption; the scale is `YMin`–`YMax` (0–100 unless set), `GaugeSweep` 180–360 degrees (270 by default), `YLabel` the unit (`"%"`), `YZones` tint the track and colour the score, a Y annotation is a target tick. `ChartKind.Ring`: one to six series, outermost first, one nonnegative point each, `ChartSeries.Goal` the target (100 unless set) and the point's `Label` the unit; past its goal a ring runs on over itself. Recipes in `references/sports.md`.
- **Timelines and range bars** (0.27.0 and later). `ChartKind.Timeline` is a state timeline such as a sleep hypnogram: one series per state, drawn as a lane top to bottom in series order, each point a span made with `ChartPoint.Span(start, end, label?)` (it sets `XEnd`; there is no `Y`). Spans in one lane must not overlap; where one ends exactly as one in another lane starts, a connector joins them unless `TimelineConnectors = false`. The legend reads `REM 1:42, 22 %`. `ChartKind.Range` draws floating bars from `Low` to `High` with a dot at `Y` when set, made with `ChartPoint.Interval(x, y, low, high, label?)` (pass `null` for no dot); no zero baseline, so reversed and log axes are fine. It is also a series `Kind` beside lines or in a column chart's slots.
- **Calendars** (0.28.0 and later). `ChartKind.Calendar` draws one series of days on a time axis (`XAxis = AxisKind.Time` is required): each point's `X` is a moment counted on its day in `TimeZone`, its `Y` the day's value, and one day's points add up; zero or null is a rest day, an empty grey cell that takes no focus. `CalendarLayout.Weeks` (default) is the contribution grid, a column per week, weeks starting on `WeekStart` (Monday unless set); `CalendarLayout.Months` is a small grid per month. `CalendarCell` is `Square` (default), `Dot` or `Bubble` (area by value). `YZones` colour each day by its tier; without them days take the style's heatmap ramp. An X annotation outlines a day (today, a race). `XMin`/`XMax` set the first and last day shown.
- **Blocks** (0.29.0 and later) — laps sized by their length and structured workouts. `ChartKind.Blocks`, or a series' `Kind = ChartKind.Blocks` beside lines on a continuous chart: each point is `ChartPoint.Block(start, end, height, label?)` (it sets `X`, `XEnd` and `Y`), drawn exactly from `X` to `XEnd`, standing on the bottom edge of its plot and rising to `Y`. With `IncludeZero = true` a power target rises from zero; on a reversed pace axis (`YReversed = true`, `YFormat = ValueFormat.Duration`) a lap rises from past the slowest pace, since an axis fitted to the data reaches below its lowest block until that block stands a sixth of the plot. Blocks that touch are a hairline apart; `Zones` colour each by its height's zone (`ZoneScale.CogganPower(ftp)`), a point's `Color` beats it, and a line series drawn with them stands over them. Blocks in one series must not overlap (they may touch). Recipes in `references/sports.md`.
- **Time on X:** `XAxis = AxisKind.Time` and X in **Unix milliseconds** — `TimeAxis.Value(DateTimeOffset)` converts. `TimeZone = "Africa/Johannesburg"` reads the calendar locally; `SkipWeekends`/`TimeSkips` close trading gaps.
- **Durations and pace:** `XFormat`/`YFormat = ValueFormat.Duration` reads values as **seconds** (`5:30`, `1:02:05`; on a log axis `1s`, `5m`, `1h`). Pace is seconds per unit, so 300 reads `5:00`; `YReversed = true` puts the faster pace on top. `ValueFormat.Compact` writes `1.2k`. `ValueFormat.TimeOfDay` reads **seconds since a midnight** as `HH:mm` and wraps at 24 h, so a night runs as one span: 23:30 is 84600 and 06:40 the next morning 110400; with `YReversed = true` it draws sleep timing, earlier at the top. Linear axes only.
- **Log axes:** `XAxis`/`YAxis = AxisKind.Log`, positive values only.
- **Second axis:** `Secondary = true` on a series measures it on the right; name it with `Y2Label`.
- **Several marks in one chart:** a series' `Kind` overrides its mark — lines over columns, a band behind a line, range bars beside a line. The chart's own `Kind` still decides the X layout.
- **Panes:** `ChartSeries.Pane = 1` puts a series in a pane beneath the main plot, configured by `ChartSpec.Panes[0]` (pane *k* is `Panes[k − 1]`). All panes share one X axis and zoom together.
- **References:** `Annotations = [new(AnnotationAxis.Y, 55) { Label = "Target" }]`, or a band with `To`. The value is printed after the label (`Target: 55`), so keep the label plain and round the value you pass.
- **Zones:** `ChartSeries.Zones` colours a line by the zone each value is in; `ChartSpec.YZones` shades the zones behind the data.
- **Look:** the default *refined* finish draws thin non-scaling lines, shows point markers on hover or focus, and uses a dotted hairline grid. `Style = ChartStyle.Midnight` is a dark athletic preset; `ChartStyle.Light with { Finish = ChartFinish.Classic }` restores the pre-0.24 look. Per series: `StrokeWidth`, `Curve` (`Smooth`, `Step`), `Fill = AreaFill.Fade`, `Gradient`, `Markers`, `HighlightLast`, `ValueLabels`. Brand colours: `references/api.md` → ChartStyle.
- Always set `Title`, `Description` and axis labels: they become the SVG's accessible name and the screen-reader text.

## 4. Sports and training charts

For fitness, training, wearable or health data, read `references/sports.md`. It has tested recipes for the performance management chart (CTL/ATL/TSB from `Training.Load`), a recovery or readiness gauge, activity rings, a multi-pane activity stream with heart-rate zones and reversed pace, the power–duration curve with a critical-power fit, time in zone, weekly load against a target band, an HRV baseline band, grade-coloured elevation and a personal-best step line, a sleep hypnogram, sleep timing, daily heart-rate ranges, a training calendar, laps sized by their length and a structured workout in power levels — and the conventions the numbers follow (seconds, Unix milliseconds, zone bounds).

## 5. When a chart is refused

`ChartSvg.Render` validates first and throws `ArgumentException` with a plain message; the HTTP API answers 400 with the same message. Read the message — it names the rule. The ones projects hit most:

- Time values must be Unix milliseconds (not ticks, not seconds), between year 1 and 9999.
- Log axes refuse zero and negative values; a log Y axis refuses column, bar, area, histogram, donut and radar charts, which need a zero baseline.
- Line, area, candlestick, OHLC and band points must be ordered by X.
- Column and area series draw from zero, so their axis always includes zero and cannot be logarithmic or reversed.
- Candlestick and OHLC take exactly one price series (`ChartPoint.Candle`); other series beside it must set their own `Kind`.
- Panes need a continuous chart (line, area, scatter, bubble, band, candlestick, OHLC), at most four, and every `Pane` index needs its `ChartPane`.
- A zone scale's last `Upper` must be `double.PositiveInfinity`; in JSON write `"Infinity"`.
- A gauge takes one series with one point and a scale with `YMin` below `YMax`; a ring chart one to six series of one nonnegative point each, each `Goal` positive. Both refuse time, log and reversed axes, panes, secondary series, trends and series kinds, and rings refuse zones and annotations.
- A timeline needs every point to be a span (`XEnd` above `X`), refuses overlapping spans in one lane, and refuses Y-axis settings, zones, trends, secondary series, series kinds, panes and Y annotations (X annotations are fine). A range point needs both `Low` and `High`, its `Y` between them; range series refuse `ProjectedFrom`, trends and zones. `XEnd` is for timelines only, and `ValueFormat.TimeOfDay` is refused on time and log axes.
- A calendar needs a time X axis and takes one series of nonnegative values; it refuses Y-axis settings (log, reversed, bounds, side, tick labels), secondary series, panes, trends, series kinds, series zones (use `YZones`), Y annotations and X annotation bands, `XEnd`, point colours, `SkipWeekends`/`TimeSkips` and spans over 3,660 days. `CalendarLayout`, `CalendarCell` and `WeekStart` are refused on other kinds.
- Blocks need every point to be a block (`XEnd` above `X`, and a `Y`), refuse overlapping blocks in one series, a column chart (no category slot for a block), a logarithmic X axis, `Trend` and `ProjectedFrom`, and the line-only finishes (stroke width, curves, markers, fades, value labels, gradients). `XEnd` is for timelines and blocks only.
- Limits: 100,000 points per chart, 32 series.

## 6. Check the result

Don't hand back a chart nobody has looked at. Render it and look:

```csharp
File.WriteAllText("chart.svg", ChartSvg.Render(spec));   // open in a browser
```

For Blazor, run the app and open the page; hover a point to see its tooltip. If the project has tests, assert on the SVG (it is plain XML: `XDocument.Parse(svg)`) rather than on pixels. Contrast-check a custom brand with `style.ContrastIssues()`.

## Limits worth knowing

No server-side PNG (PNG export rasterizes in the browser), no PDF, no 3D, no map tiles (routes are out of scope), no streaming transport, no shared crosshair across panes. Vendor scores (WHOOP recovery, Garmin readiness and similar) are not computed — their formulas are unpublished — but a score the data provides draws on a `Gauge`, or as any other series.
