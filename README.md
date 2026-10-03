# Lumen Charts

A standalone C# chart library, Blazor components, ASP.NET Core rendering API, and an interactive gallery. Preview 0.25.0. No third-party charting engine or CDN is required.

## Run the gallery

Requires the .NET 10 SDK (the reusable packages target .NET 8).

```powershell
dotnet run --project samples/Lumen.Gallery --urls http://localhost:5188
```

Open http://localhost:5188. The gallery includes chart selection, light/dark themes, refreshed sample data, series filtering, point selection, a Midnight brand beside the Lumen, Harbour and page-CSS ones, a numeric / time / log axis switch with a power–duration curve and a reversed pace line on duration axes, a heart-rate stream coloured and shaded by zone with the time it spent in each zone, a performance management chart of fitness, fatigue and form over daily training stress with two planned weeks projected, weekly load against a target range, an activity stream of heart rate, pace and climb in three panes over one elapsed-time axis, the heart rate coloured by its value and the climb a smooth faded area, candlestick and OHLC charts with a five-day average and volume in a pane beneath, X zoom/pan/reset, original-data tables, SVG/PNG/CSV downloads, and network layouts with draggable nodes. Every chart draws in the [refined finish](#the-refined-finish).

http://localhost:5188/sports is the **Sports & performance** page: every training chart on one dashboard, drawn from one simulated athlete so the charts agree with one another — the performance management chart, weekly load against a target, weekly zone distribution, a three-pane activity stream, time in zone, pace by kilometre, elevation coloured by grade, the power–duration curve with its critical-power fit, a personal-best progression and overnight HRV against its baseline. Each card names the `Training` call or option that draws it, every chart sets [`FitWidth`](#fitting-the-width-it-is-shown-at) so it fills its card on a desktop and on a phone, and the page follows the theme and brand switches.

## Build and verify

```powershell
dotnet restore Lumen.Charts.slnx --configfile NuGet.Config
dotnet build Lumen.Charts.slnx -c Release --no-restore
dotnet run --project tests/Lumen.Charts.Tests -c Release
dotnet pack src/Lumen.Charts -c Release -o artifacts/packages
dotnet pack src/Lumen.Charts.Blazor -c Release -o artifacts/packages
dotnet pack src/Lumen.Charts.AspNetCore -c Release -o artifacts/packages
```

Each package carries its XML documentation file beside its assembly, so IntelliSense, and a coding agent reading the package, can show what a member does without the source.

With a host running, `tests/Lumen.Charts.BrowserTests` drives it in a real browser:

```powershell
dotnet build tests/Lumen.Charts.BrowserTests -c Release
pwsh tests/Lumen.Charts.BrowserTests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet run --project tests/Lumen.Charts.BrowserTests -c Release --no-build -- http://localhost:5188
```

It uses component selectors, so the same checks, the axe sweep included, run against the gallery and against the WebAssembly host on port 5199. Some depend on the host. The brand check runs where the page wraps its charts in `LumenBrand`; a second axe sweep runs with the dark theme on where the page has a theme button, which the suite finds by its name and presses as a user would; where the first chart hides its line markers, as the refined finish does, the suite checks that hovering a point shows its marker and its tooltip and that reaching it with Tab shows its marker with the focus ring; where the page offers an activity stream behind buttons named for the line chart and the stream, the suite opens it, checks that zooming and panning move its three panes together, that a hidden marker still takes focus, draws its focus ring and reads its value, and that a PNG export keeps the gradient its heart-rate line is coloured with, and sweeps it with axe; where the page offers a Midnight brand, a fourth sweep runs with it on; where it links a Sports & performance page, the suite opens it, checks that its ten charts are live and drawn at the width they are shown, that a hovered mark shows its tooltip, that axe finds nothing in the light, dark and Midnight looks, and that the page fits a 375-pixel phone; and on the first chart that sets `FitWidth`, on the first page or the Sports & performance page, it checks that the chart is drawn at the width of its container, in a container narrowed to 480 pixels and on a 375-pixel phone, with its title the same size each time, and that zoom and the SVG and PNG exports work on it at that width. A host without one says SKIP rather than failing: the gallery skips the brand check and has no fitted chart on its first page, so it runs thirty-one, and the WebAssembly page the dark sweep, the activity stream, Midnight and the Sports & performance page, so it runs twenty. It stays outside the solution so the ordinary build needs no browser download.

The repository NuGet.Config restores from nuget.org for one dependency: `Lumen.Charts.Blazor` references `Microsoft.AspNetCore.Components.Web` (8.0.0) rather than the ASP.NET Core shared framework, because a WebAssembly host has no shared framework to reference. `Lumen.Charts` and `Lumen.Charts.AspNetCore` add no packages of their own. With the gallery running, execute `./tests/verify-api.ps1` for HTTP integration checks.

## Use it from Claude Code

[`integrations/claude-code/lumen-charts`](integrations/claude-code/lumen-charts/SKILL.md) is a Claude Code skill: it teaches Claude to install Lumen in any .NET project, use the API, and draw the sports and training charts, and Claude loads it on its own whenever a project asks for a chart. Install it once for every project by copying the folder to `~/.claude/skills/lumen-charts/`, or for one project to that project's `.claude/skills/lumen-charts/`. Its `scripts/install.sh` and `install.ps1` fetch the packages from the latest GitHub release into a local NuGet feed, since they are not on nuget.org. Tested on two tasks against runs without it: a Blazor training page and an SVG report endpoint, both built, served and checked from a clean package cache.

## Blazor integration

Reference `Lumen.Charts.Blazor`, add these imports to `_Imports.razor`, and add the stylesheet to your host page:

```razor
@using Lumen.Charts
@using Lumen.Charts.Blazor
```

```html
<link rel="stylesheet" href="_content/Lumen.Charts.Blazor/lumen.css" />
```

```razor
<LumenChart Spec="chart" PointSelected="OnPoint" />

@code {
    ChartSpec chart = new() {
        Title = "Monthly revenue",
        Description = "Revenue by month, in thousands of rand",
        Source = "Source: accounting export",
        Kind = ChartKind.Column,
        XLabel = "Month", YLabel = "Revenue (ZAR thousands)",
        Series = [new("Revenue", [new(1, 24, "Jan"), new(2, 38, "Feb")])]
    };
    void OnPoint(PointSelection point) { /* Original series/point indices */ }
}
```

Use an interactive render mode in a Blazor Web App to enable toolbar actions and callbacks. Rendering itself supports static HTML. The gallery demonstrates Interactive Server and [samples/Lumen.Wasm](#webassembly) demonstrates standalone WebAssembly; both are exercised. JavaScript is limited to event delegation, tooltips, measuring a fitted chart's box, canvas rasterization and browser downloads.

### Fitting the width it is shown at

A chart is drawn at `ChartSpec.Width`, 900 by default, and scales to its container. Scaled down, its text shrinks with it, so below 640 pixels the stylesheet stops shrinking it and lets it scroll sideways in its own box. `FitWidth` draws it at the width its container gives it instead, on a phone, in a narrow card or in a sidebar, so it fills the box with its text at its own size:

```razor
<LumenChart Spec="chart" FitWidth="true" />
```

The component measures its box once it is interactive, again whenever the box settles at a new width, and redraws with `Width` replaced by that width in whole pixels, never below 320, the narrowest a chart accepts. A `lumen-fit` class on the chart's root lifts the 640-pixel minimum for that chart alone. Zoom, pan, hidden series, the data table and point selection carry on across a redraw, and the SVG and PNG exports take the width the chart is drawn at. `Height` stays as it is, so a fitted chart on a phone is taller than it is wide; pick a height that reads well at both. Nothing measures a chart that is not interactive, so a prerendered or statically rendered chart is drawn at `Spec.Width` and scaled to fit until the browser takes over; for static rendering, or for SVG from `ChartSvg.Render`, set `Width` to the width the chart will be shown at. A chart that leaves `FitWidth` off renders exactly as before. `LumenGraph` has no `FitWidth`: a node the reader drags keeps its place in the drawing's coordinates, which a new width would move out from under it.

### Axes

`XAxis` and `YAxis` choose between `AxisKind.Linear` (default), `AxisKind.Log` and `AxisKind.Time`:

```csharp
ChartSpec traffic = new() {
    Kind = ChartKind.Line,
    XAxis = AxisKind.Time,          // X values are Unix milliseconds, UTC
    YAxis = AxisKind.Log,           // base 10, positive values only
    XLabel = "Time (UTC)", YLabel = "Requests per minute (log scale)",
    Series = [new("Edge", rows.Select(r =>
        new ChartPoint(TimeAxis.Value(r.Timestamp), r.Requests)).ToArray())]
};
```

`TimeAxis.Value(DateTimeOffset)` and `TimeAxis.Moment(double)` convert between moments and axis values. Time ticks fall on calendar boundaries — seconds, minutes, hours, days, fortnights, months or years — and are formatted with the invariant culture. They read in UTC unless `TimeZone` names one:

```csharp
ChartSpec shifts = new() {
    XAxis = AxisKind.Time, TimeZone = "America/New_York",
    Series = [new("Orders", readings)]
};
```

A day then begins where the zone begins it, not at 00:00 UTC, and months and years start on their local first. Ticks hold their local boundary across a clock change, so the hour a zone skips or repeats moves the ticks that follow rather than drifting the whole axis; a tick landing inside a skipped hour moves to the first reading the zone actually had. Tooltips, the data table and the axis all read in the same zone, and the identifier is whatever the host recognises — `Europe/London` on any current .NET, Windows identifiers too. A market does not trade at the weekend, and a strictly proportional axis spends two sevenths of its width saying so. `SkipWeekends` leaves those spans out, and `TimeSkips` leaves out any others, such as the days an exchange is shut:

```csharp
ChartSpec prices = new() {
    Kind = ChartKind.Candlestick, XAxis = AxisKind.Time,
    SkipWeekends = true, TimeSkips = [TimeAxis.Day(new DateTime(2026, 4, 3))],
    Series = [new("ACME", bars)]
};
```

Weekends are counted in `TimeZone`, so a market's weekend is its own rather than UTC's, and they are worked out over the axis range the chart settles on, including after a zoom. Ticks inside a skipped span are not drawn, which is why a weekday axis carries no Saturday label. Two consequences are worth knowing: a moment inside a skipped span has no position of its own and sits where the span opens, and reading a position back — which is what a zoom does — gives the moment the axis resumes at. `TimeAxis.Weekends`, `TimeAxis.Day` and `TimeAxis.Normalise` are public if you want to build the spans yourself.

Log ticks are decades, subdivided at 2 and 5 across one or two decades. `Axis.Create`, `Axis.Map`, `Axis.Invert`, `Axis.Ticks` and `Axis.Format` are public if you need the geometry without SVG. CSV exports of a time chart add an `XTime` column with ISO 8601 UTC timestamps beside the numeric X column.

#### Durations, compact numbers and reversed axes

`XFormat`, `YFormat` and `Y2Format` choose how an axis writes its values, whatever its kind: `ValueFormat.Number` (the default), `ValueFormat.Duration`, which reads values as seconds, or `ValueFormat.Compact`, which writes 1.2k, 3.4M and 1.5B. `YReversed` and `Y2Reversed` put the smallest value at the top, so a faster pace — a smaller number — sits higher:

```csharp
ChartSpec power = new() {
    Kind = ChartKind.Line,
    XAxis = AxisKind.Log, XFormat = ValueFormat.Duration,    // 1s, 10s, 1m, 10m, 1h
    XLabel = "Duration", YLabel = "Power (W)",
    Series = [ChartSeries.From("Best", Training.MeanMaximal(watts, Training.StandardDurations),
        p => p.Seconds, p => (double?)p.Value)]
};

ChartSpec pace = new() {
    Kind = ChartKind.Line,
    XFormat = ValueFormat.Duration,                     // elapsed time: 0:00, 15:00, 1:00:00
    YFormat = ValueFormat.Duration, YReversed = true,   // 4:45 above 5:15
    XLabel = "Elapsed time", YLabel = "Pace (min per km)",
    Series = [new("Pace", samples)]
};
```

On a linear axis a duration reads `m:ss` below an hour and `h:mm:ss` from an hour up — `0:00`, `5:30`, `1:02:05`, and `48:00:00` for two days — rounded half up to the second, with a minus sign when negative. Its ticks step through 1, 2, 5, 10, 15 and 30 seconds, the same in minutes, then 1, 2, 3, 6 and 12 hours and whole days, taking the smallest step that puts no more ticks on the axis than were asked for. Minor gridlines divide a step into round durations too: a minute into quarters, fifteen minutes into fives, a day into six-hour parts. Pace is a duration per unit, so 300 reads `5:00`; the unit belongs in the axis title.

On a logarithmic axis — the power–duration curve — ticks come from 1, 2, 5, 10, 15 and 30 seconds, 1, 2, 5, 10, 20 and 30 minutes, 1, 2, 3, 4 and 5 hours and every whole hour after that. Those inside the range are thinned to about five, spaced evenly on screen, always keeping the round duration nearest each end, and read `1s`, `30s`, `1m`, `20m`, `1h`, or `2h30m` for a value between units. A range that ends between two of them is labelled to the last one inside it, so the four-hour end of `Training.StandardDurations` reads `4h`. There are no minor gridlines, because the mantissas between non-decade ticks are no duration anyone reads.

Compact keeps the tick positions a plain axis would choose and changes only the words: at most one decimal, a trailing `.0` dropped, plain numbers below 1000, then k, M, B and T. A value that rounds to a thousand of one unit is written in the next, so 999,999 reads `1M`. With one decimal, ticks a quarter of a unit apart read unevenly — `1.3k`, `1.5k`, `1.8k`.

Tooltips, accessible names, annotation labels, the static SVG, and the component's data table and status line all read in the axis's format; CSV keeps the raw numbers, seconds included. A time axis writes its own calendar and refuses both formats. An X format applies where X is a value — line, area, scatter, bubble, candlestick, OHLC and band charts — and a Y format wherever a Y axis measures values; donut, heatmap and radar charts have no such axis, and a histogram's counts observations, so they refuse one.

Reversal is a property of `Axis` that `Map` and `Invert` honour, so gridlines, ticks, marks, annotations, trend lines and zoom follow it without any of them knowing. A trend line still names the direction of the data: on a reversed pace axis, a line climbing the screen is a pace that is falling. It applies to line, scatter, bubble, band, candlestick, OHLC, box and violin charts. Column, bar, stacked column, area and histogram charts refuse it, because they draw from a zero baseline and a reversed one would hang their bars from the top.

### Trend lines

A series can carry a least-squares line:

```csharp
ChartSpec relationship = new() {
    Kind = ChartKind.Scatter,
    Series = [new("Accounts", observations) { Trend = true }]
};
```

The line is fitted in the space the chart draws in, which is what keeps it straight on screen: a logarithmic axis has already taken the logarithm, and a trading axis has already left out the spans it skips. On plain axes this is the ordinary least-squares fit, because the scaling between data and pixels does not change it. It is drawn dashed in the series colour, reports its direction and R squared to a pointer and to assistive technology, and is left out when a series has fewer than two observations or no spread in X. `Statistics.Fit` returns `Slope`, `Intercept`, `R2` and `Count` if you want the numbers rather than the line.

### Statistical and financial families

| Kind | Input | Rendering |
|---|---|---|
| `Candlestick` | One series whose points carry `Open`, `High`, `Low`, `Close` — use `ChartPoint.Candle` — and any others beside it in kinds of their own, such as a moving average or [volume in a pane](#panes) | Wick across the low-high range, body from open to close, colored by direction (`ChartSvg.RisingColor` and `FallingColor`) |
| `Ohlc` | The same as `Candlestick` — one series of `ChartPoint.Candle` points, and companions beside it | Vertical line across the low-high range, a tick to the left at the open and a tick to the right at the close, colored by direction |
| `Band` | `Y` with `Low` and `High` bounds — use `ChartPoint.Interval` | Filled interval behind the central line; points without bounds break the band into runs |
| `Histogram` | One to four series of raw observations in `Y`; `X` is ignored | Equal-width bins over a zero baseline, chosen from the pooled observations and shared by every series. `Bins` sets the count; otherwise Freedman–Diaconis chooses it, falling back to Sturges when the interquartile range is zero. Several series stand side by side within each bin |
| `Box` | One series per distribution: raw observations in `Y` with `X` ignored, or a precomputed `Summary` and no points | Quartile box, Tukey whiskers at 1.5 interquartile ranges, and outliers as circles; a supplied summary is drawn as given |
| `Violin` | One series per distribution, raw observations in `Y`; `X` is ignored | Kernel density outline mirrored about each column, with a quartile bar and a median tick |

```csharp
ChartSpec prices = new() {
    Kind = ChartKind.Candlestick, XAxis = AxisKind.Time,
    Series = [new("ACME", bars.Select(b =>
        ChartPoint.Candle(TimeAxis.Value(b.Day), b.Open, b.High, b.Low, b.Close)).ToArray())]
};

ChartSpec latency = new() {
    Kind = ChartKind.Box, YLabel = "Latency (ms)",
    Series = [new("Europe", europe.Select(ChartPoint.Observation).ToArray()),
              new("Africa", africa.Select(ChartPoint.Observation).ToArray())]
};

ChartSpec warehouse = new() {
    Kind = ChartKind.Box, YLabel = "Latency (ms)",
    Series = [new("Asia", []) { Summary = new(Q1: 205, Median: 228, Q3: 252,
        LowerWhisker: 160, UpperWhisker: 318, Outliers: [352, 371]) }]
};
```

`Statistics.Quantile`, `Statistics.Summarize`, `Statistics.Bins` and `Statistics.SharedBins` are public, so the same numbers are available without rendering. Quantiles interpolate linearly between order statistics, matching NumPy's default and Excel's `PERCENTILE.INC`. CSV exports add `Open,High,Low,Close` for candlestick charts and `Low,High` for band charts.

### Branding

Charts can draw in a host application's colours and typeface. A `ChartStyle` holds them, and it renders into the SVG itself, so an exported SVG or PNG and the HTTP API carry the brand exactly as the page shows it. Three ways to supply one, from most specific to least:

1. **`ChartSpec.Style`** or **`GraphSpec.Style`** — one chart, anywhere, including server rendering and the HTTP API.
2. **A cascaded `ChartStyle`** — every chart and graph beneath it in a Blazor tree.
3. **`Theme`** — the built-in `Light` and `Dark`, used when neither of the above is set. `ChartStyle.Light` and `ChartStyle.Dark` reproduce them exactly, and `ChartStyle.Midnight` is a third preset, described under [Finish and styling](#finish-and-styling).

```csharp
public static readonly ChartStyle Brand = new() {
    Background = "#F6F3EE", Text = "#1F2A37", Muted = "#4B5563", Grid = "#E5DED3",
    Series = ["#1D4E89", "#B03A2E", "#2E7D5B"], FontFamily = "Georgia,Cambria,serif"
};
```

```razor
<CascadingValue Value="Brand">
    @Body   @* every LumenChart and LumenGraph in the app *@
</CascadingValue>
```

Most applications already define their brand in CSS. `LumenBrand` reads it from the page instead, by custom property name, and cascades the result:

```razor
<LumenBrand Series="--bs-primary, --bs-success, --bs-warning, --bs-danger"
            Background="--bs-body-bg" Text="--bs-body-color"
            Muted="--bs-secondary-color" Grid="--bs-border-color">
    <LumenChart Spec="revenue" />
    <LumenGraph Spec="pipeline" />
</LumenBrand>
```

It accepts any colour the browser accepts, plus Bootstrap-style `13, 110, 253` triples, and uses the font family in effect where it sits unless `UseHostFont` is false. Anything the page does not define comes from `Fallback`, which is also what renders before the page has been read. It reads the page again after every parent render and redraws only when the colours changed, so a host that switches to dark mode by changing its custom properties is followed without further wiring. It also sets the component chrome — focus rings, control borders and an inverse tooltip — through `--lumen-accent`, `--lumen-control-border`, `--lumen-tooltip-bg` and `--lumen-tooltip-fg`, which a host can also set in its own stylesheet.

A brand palette is the likeliest way to make a chart inaccessible, so check it:

```csharp
foreach (var issue in Brand.ContrastIssues())
    Console.WriteLine($"{issue.Element} {issue.Foreground}: {issue.Ratio}:1, needs {issue.Required}:1");
```

`ContrastIssues` applies the WCAG 2.1 minimums — 4.5:1 for text, 3:1 for series, zone colours, candles and edges — and all three built-in presets report none. `LumenBrand` raises `Resolved` with each style it reads, so an application can check a page-supplied brand at run time too.

Limits: server rendering cannot read a stylesheet, so `ChartSvg.Render` and the HTTP API need an explicit `ChartStyle`. `LumenBrand` discards transparency, since a chart colour is drawn opaque, and maps series, background, text, muted, grid and candle colours; graph edges and the heatmap ramp come from `Fallback`. Font lists are reduced to letters, digits, spaces, commas and hyphens because they are written into a style attribute; `ChartStyle.FontFamilyFrom` performs that reduction on any CSS value.

### The refined finish

Since 0.24.0 every chart draws in the refined finish unless its style says otherwise. `ChartStyle.Finish` is `ChartFinish.Refined` by default, so every preset and every brand takes it:

- **Thin strokes that stay thin.** A line, area or band draws a 1.6-pixel stroke when it sets no `StrokeWidth`, with round ends and joins. Every stroke carries `vector-effect="non-scaling-stroke"`: data lines, gridlines, references, trends, candle wicks, OHLC ticks, box and violin outlines, radar rings, heatmap cell outlines and graph edges. A chart shown wider than its `Width` therefore keeps its lines at the width they were drawn, where before they thickened with the drawing. A projected stretch keeps its 6-pixel dashes and 4-pixel gaps with the round ends counted in.
- **Guides thinner than data.** A trend line is three quarters of its series' stroke, 1.2 pixels by default, and a reference line is 1 pixel.
- **Markers on demand.** With `Markers = MarkerStyle.Auto`, a line's or area's markers stay hidden until their point is hovered or focused by keyboard, when the marker appears with the focus ring. Every point keeps its focusable, labelled mark at the size the classic marker had, so it is as easy to point at and is announced as before, and the tooltip still reads it. A point between two missing values, which no line can show, keeps its marker in view, as does a highlighted last reading. Scatter and bubble marks stay visible, and a `Markers` value a series names keeps its 0.23.0 meaning.
- **Hairline dotted gridlines.** A style that sets no `Gridlines` draws them dotted, one pixel wide at any size and landing on whole pixels, so `ChartStyle.Light.Gridlines` reads `Dotted`. A style that sets them keeps what it set.
- **Legend keys shaped like their marks.** A line series is keyed by a short line, dashed when the whole series is projected; scatter points and bubbles by a dot; columns, bars, areas, bands and the rest by a square. A series drawn in colours of its own is keyed by them, side by side: up to four of its point colours when every point it draws has one, as time-in-zone bars do, a donut's slice colours, a heatmap row's low and high colours, and the rising and falling colours of candles and OHLC bars. The component's HTML legend draws the same keys, from `ChartSvg.LegendKey`.
- **Ticks spaced to the room.** A plot or pane asks its axis for fewer ticks while two labelled ones would stand closer than 28 pixels, so a short pane labels three ticks where it crowded five; past the fewest an axis offers, a logarithmic axis keeps its decades and any axis keeps every tick at least 28 pixels from the last. Along the bottom, category labels, labelled points, and numeric and time ticks are thinned until no two touch, judged by the same generous width estimate value labels use; histogram edges already stand 70 pixels apart, wider than any edge's label.
- **References that stay legible.** Annotation and zone band labels are written over the data, with a halo in the background colour, so they read across a line, and they never leave the plot. A band's label stays inside its band and is left out when the band is thinner, or an upright band narrower, than its text; a line's label sits just above it, or just below where the plot ends above it; and labels that would land on one another are nudged apart: a band's label moves down its band, an upright reference's label drops a row. A label that finds no room is left out, and its reference keeps its accessible name and tooltip.

`ChartFinish.Classic` is the exact way back. It draws as 0.23.0 did, byte for byte, gradient IDs included, which the [release baseline](docs/VERIFICATION.md) checks across 189 renderings:

```csharp
// One chart, on its theme's preset or on a brand of its own.
var classic = spec with { Style = ChartSvg.ResolveStyle(spec) with { Finish = ChartFinish.Classic } };
```

```razor
@* Every chart under a cascaded style. *@
<CascadingValue Value="@(ChartStyle.Light with { Finish = ChartFinish.Classic })">@Body</CascadingValue>
```

In JSON it is `"style":{"finish":"Classic"}`. A classic style that sets no `Gridlines` draws them solid, as 0.23.0 did, and the component's legend keeps its dots. A chart that sets a style only to get the classic finish is named for its gradients as the same chart on its theme was, so even its IDs match; one that set `Style = ChartStyle.Light` explicitly in 0.23.0 gets the theme's IDs instead, which changes the IDs and nothing they paint.

Limits. Label widths are estimated rather than measured, since the server has no fonts, as for value labels; the estimate is generous for Segoe UI, Arial, Georgia and Times New Roman. A label left out for want of room is not moved outside its band or the plot, so a narrow upright band, a run of references close together or a thin zone can lose its visible label; its name stays in the tooltip and the accessible name. Markers appear on hover through the chart's own stylesheet, so an SVG embedded as an image, a PNG export or a printout shows the lines without them. The halo is a stroke in the background colour, so a label over a band's tint shows a thin margin of plain background round each letter. A gradient or zone-coloured series is keyed in its series colour, as before.

### Finish and styling

The phone apps in [FITNESS.md](docs/FITNESS.md) finish their charts the same way: smooth lines over a fade, a line coloured by its value, the latest reading ringed, capsule bars with their values at the ends, quiet dotted gridlines, and the Y axis on the right labelled at its ends. Each is an option here, drawn in whichever finish the chart's style takes. All of them are off by default.

```csharp
ChartSpec recovery = new() {
    Kind = ChartKind.Line, Style = ChartStyle.Midnight,      // near-black, vivid, dotted grid, capsule bars
    YAxisSide = AxisSide.Right, YTickLabels = TickLabels.Ends,
    Series = [
        new("Resting heart rate", nights) {
            Curve = LineCurve.Smooth, StrokeWidth = 3,
            Markers = MarkerStyle.None, HighlightLast = true,
            Gradient = [new(48, "#2FE0A0"), new(56, "#FFC23D"), new(64, "#FF5D6E")]
        },
        new("Weekly average", weeks) { Kind = ChartKind.Area, Curve = LineCurve.Step, Fill = AreaFill.Fade }
    ]
};

ChartSpec weekly = new() {
    Kind = ChartKind.Column,
    Style = ChartStyle.Light with { BarRadius = 9999, Gridlines = GridLine.Dotted },   // capsules
    Series = [new("Load", load) { ValueLabels = true, Fill = AreaFill.Fade }]
};
```

- **`StrokeWidth`** sets a line, area or band stroke from 0.5 to 12 pixels, zone and projected pieces included; null draws 1.6 in the refined finish and 2.5 in the classic. Markers keep their size.
- **`Curve`** draws a line or area `Smooth` or as a `Step`, its fill, zone colours and projection with it. Smooth is Steffen's monotone cubic, computed on screen: between two points it stays within their values, and it is level at every peak and dip, so it never overshoots a reading or invents one. Step holds each value until the next point and then rises or falls to it. A missing value breaks either, as it breaks a line. Zone colours and a projection are split along a copy of the curve cut every 3 pixels of its control polygon, so each split lies within a fraction of a pixel of the curve; a step's splits are exact.
- **`Fill = AreaFill.Fade`** shades an area from its colour at 0.35 opacity at the top of the plot to nothing at its baseline, and back towards the bottom when the axis runs below zero, so a fill under zero fades away from zero too. A faded column keeps its colour at the baseline and lightens to 0.6 opacity at its far end.
- **`Gradient`** colours a line or area stroke and its markers continuously by value. It is a vertical gradient laid out in the plot's own coordinates through the series' own axis, so each stop sits exactly at its value's height, on a logarithmic, reversed or right-hand axis too, and past the first and last stops their colours carry on. Stops rise strictly, at least two and at most 32, in `#RRGGBB`. A series takes a gradient or `Zones`, not both: zones colour in steps and name the zone in each label, while a gradient colours continuously and adds nothing to a label, which already reads the value.
- **`Markers`** are `Auto` (each kind's own: in the refined finish a line's or area's appear on hover and focus, and in the classic they are always drawn), `Hollow`, `Filled` or `None`. A hidden marker is still there: every point keeps its focusable, labelled mark, with a transparent target the size of the marker, so keyboard and screen-reader users reach each reading and the focus ring still draws. A scatter series is its markers, so it refuses `None`.
- **`HighlightLast`** draws the last reading of a line or area larger, with a soft ring at low opacity, even when the other markers are hidden. Its pane's clip widens to 12 pixels so the ring is drawn whole at the plot's edge.
- **`ValueLabels`** writes each column's or bar's value just past its far end, in its axis's format and the style's text colour. A label that would not fit across its column, or within the plot beside its bar, is left out rather than overlapping. The labels are hidden from assistive technology, because each bar's accessible name already reads its value.
- **`ChartStyle.BarRadius`** rounds the far end of every column and bar — the bottom of a negative column, the left of a negative bar — and keeps the baseline end square. It is clamped to half the bar's width, which makes a semicircle, and to the bar's length, so a large radius draws capsules; a stack rounds only its outermost segment on each side of zero. Null keeps the 2 px corners.
- **`ChartStyle.Gridlines`** is `Solid`, `Dotted`, `Dashed` or `Hidden`. It changes the horizontal and vertical gridlines, minor ones included, and nothing else; `Hidden` keeps the tick labels. A style that sets none draws `Dotted` in the refined finish and `Solid` in the classic.
- **`ChartSpec.YAxisSide = AxisSide.Right`** labels the main Y axis of every pane on the right and gives the left margin back. A chart with a secondary series refuses it, because the right edge is taken, and so does a horizontal bar chart, whose value axis runs along the bottom.
- **`ChartSpec.YTickLabels = TickLabels.Ends`** labels only the lowest and highest tick of the main Y axis and keeps every gridline; a secondary axis labels all of its own.
- **`ChartStyle.Midnight`** is a third preset: a near-black background, `#0B0E14`, with a vivid palette, dotted gridlines and capsule bars. Its six series colours measure 6.64:1 to 11.99:1 against the background, its zone ramp 6.31:1 to 11.85:1, its candles 6.48:1 and 11.31:1 and its edges 5.34:1, and its text and muted text 17.7:1 and 7.7:1, so `ContrastIssues` reports nothing.

Gradients need IDs, which Lumen had avoided because several charts share one page. A chart that uses a gradient or a fade defines each once, in a `<defs>` block after its stylesheet, named `lumen-`, the first twelve hex digits of the SHA-256 of its spec serialized as JSON with options fixed in the library, and a counter. The same spec always yields the same IDs, two different charts cannot collide, and two identical charts define identical gradients, so whichever a reference resolves to paints the same. A chart that uses neither has no ID and no `<defs>`. The SVG stays self-contained: every reference is to a fragment of the same document, so the PNG export, which rasterizes that SVG through a canvas, keeps the gradients, and the browser suite checks that it does.

In JSON: `"curve":"Smooth"`, `"fill":"Fade"`, `"markers":"None"`, `"highlightLast":true`, `"valueLabels":true`, `"strokeWidth":3` and `"gradient":[{"value":120,"color":"#3F87D9"},{"value":180,"color":"#DD4B45"}]` on a series; `"yAxisSide":"Right"` and `"yTickLabels":"Ends"` on the chart; `"gridlines":"Dotted"`, `"barRadius":8` and `"finish":"Classic"` in its style.

Each option is refused where it cannot apply: a stroke width outside 0.5 to 12 or on a mark without a stroke, a curve on anything but a line or area, a fade on anything but an area or a column, a marker style on anything but a line, area or scatter, a highlight on anything but a line or area, value labels on anything but columns and bars, and a gradient on anything but a line or area, beside zones, with fewer than two stops or stops that do not rise, or with a stop at or below zero on a logarithmic axis. A negative or non-finite bar radius is refused too.

Limits. A smooth curve passes through the sampled points, so on a long line it smooths what sampling kept. Step is step-after only. A gradient colours by the Y value alone, not by X or by another measure, and leaves the fill, the legend swatch and the component's data table in the series colour. A faded column's tip is lighter than the colour `ContrastIssues` measures, so a palette that only just clears 3:1 falls below it there. Horizontal bars and stacked columns cannot fade, and stacked columns take no value labels. Value labels are fitted by an estimate of their width rather than measured, since the server has no fonts; it holds for Segoe UI, Arial, Georgia and Times New Roman, but a wide face such as Verdana draws labels up to about a tenth wider, so a label that only just fits can touch its neighbour. The bar radius applies to every column and bar a style draws; candle bodies, box plots, histogram bins and legend swatches keep their corners. A radar's rings and spokes are its scale and keep their solid lines, and annotations keep their own dashes. A highlighted series' pane clips 12 pixels outside the plot rather than 6, so a zoomed line runs that much further past the edge. Hashing serializes the spec on every render that uses a gradient or a fade, which measured about 8 ms more at 10,000 points and 20 ms at 100,000 for this release, through System.Text.Json's reflection-based resolver, which a host that trims away reflection metadata must keep. Two identical charts share gradient IDs, which is harmless while both are displayed; if the first is hidden with `display:none`, a browser may not paint the second's gradients, so give such charts different titles.

### A second axis

Series in different units can be read against a right-hand axis. Mark the series and name the axis:

```csharp
ChartSpec growth = new() {
    Kind = ChartKind.Line,
    YLabel = "Active accounts (thousands)", Y2Label = "Conversion (%)",
    Series = [
        new("Accounts", accounts),
        new("Conversion", conversion) { Secondary = true }
    ]
};
```

The right axis takes its own scale from its own series, and `Y2Axis`, `Y2Min` and `Y2Max` control it exactly as `YAxis`, `YMin` and `YMax` control the left. Tooltips and the data table read each point in its own units. Only the left axis draws gridlines, because two sets of lines through one plot are harder to read than one, and the plot narrows to leave room for the right-hand labels.

At least one series in each pane must stay on the left, so every left axis means something. A secondary axis applies to line, area, scatter, bubble, column and band charts; stacked columns, horizontal bars, candlesticks and the radial kinds reject it rather than imply a comparison they cannot make. Annotations measure against the main plot's left axis.

Two axes make unrelated series look related, and the relationship you see depends on where each scale happens to start. Use one when the units genuinely differ and the shapes are worth comparing, not to fit an extra series into a chart that has run out of room.

### Annotations

A chart can carry references the data is read against — a target, a threshold, the window a campaign ran in:

```csharp
ChartSpec revenue = new() {
    Kind = ChartKind.Line,
    Annotations = [
        new(AnnotationAxis.Y, 55) { Label = "Target" },
        new(AnnotationAxis.X, 7) { To = 9, Label = "Campaign" }
    ],
    Series = [new("Revenue", months)]
};
```

`From` alone draws a line, dashed unless `Dashed` is false; adding `To` draws a band. Values are in data coordinates, so an annotation zooms and pans with what it refers to and clips at the plot edge. They render behind the data, take the style's muted colour unless `Color` names one, and each is a focusable, labelled aggregate reading `Target: 55` — the value is always shown, so a reference can never sit somewhere other than where it claims. In the refined finish the label is written over the data with a halo, kept inside the plot and its band, and nudged clear of the others, as [described above](#the-refined-finish); in the classic finish it is drawn with its reference, behind the data.

`AnnotationAxis` names an axis of the data, not a direction on the screen. A horizontal bar chart draws its values along the bottom, so there a Y annotation stands upright at its value, line or band, as the zone bands do. In the classic finish its label sits at the top of the plot on the side of it with more room, reading from the part of it the plot shows, so a target near the end of the axis keeps its label in view; one wholly off the plot turns its label away and clips with it. In the refined finish an upright line's label sits on whichever side of it has room, and a reference off the plot is not labelled.

Annotations apply to the charts drawn on an X and Y axis. Donut, radar, heatmap, histogram and box charts reject them rather than place them arbitrarily, and an X annotation is refused on a category chart, horizontal bars included, whose bars sit at indices rather than at values. At most 32 per chart.

### Training zones

A `ZoneScale` from [Training metrics](#training-metrics) draws on a chart in three ways, and any point can carry a colour of its own:

```csharp
var heart = ZoneScale.CogganHeartRate(thresholdHeartRate: 170);

// Heart rate through a run sampled every 10 seconds, the line coloured by zone over the zones' bands.
ChartSpec stream = new() {
    Kind = ChartKind.Line, XFormat = ValueFormat.Duration,
    YZones = heart,
    Series = [new("Heart rate", bpm.Select((b, i) => new ChartPoint(i * 10, b)).ToArray()) { Zones = heart }]
};

// Time in zone: one bar per zone, each in its zone's colour.
var seconds = Training.TimeInZone(bpm, heart, sampleSeconds: 10);
ChartSpec timeInZone = new() {
    Kind = ChartKind.Bar, YFormat = ValueFormat.Duration,
    Series = [new("Time in zone", heart.Zones.Select((zone, i) =>
        new ChartPoint(i, seconds[i], zone.Name) { Color = zone.Color ?? ChartStyle.Light.Zones[i] }).ToArray())]
};

// An elevation profile coloured by a grade the host works out.
ChartSpec climb = new() {
    Kind = ChartKind.Area,
    Series = [new("Elevation", route.Select(p => new ChartPoint(p.Metres, p.Altitude) { Color = Steepness(p.Grade) }).ToArray())]
};
```

- **Zone colours.** A `Zone` takes an optional colour, `new Zone("Tempo", 160, "#2E9B58")`. A zone without one takes the style's `Zones` ramp at its position — grey, blue, green, gold, orange, red and purple, from low intensity to high — so Coggan's seven power levels use all seven and his five heart-rate levels the first five. Every entry clears 3:1 against both preset backgrounds, the closest being 3.45:1 on light and 4.10:1 on dark, so the two presets share one ramp, and `ContrastIssues` checks a brand's ramp as it checks its series. A scale with more zones than the ramp must colour the zones past it: the chart refuses rather than give two zones one colour.
- **Series zones.** `ChartSeries.Zones` colours a line, area, scatter, bubble, column or bar series by the zone each value falls in. A line or area stroke is split where it crosses a bound, at the crossing point interpolated on screen, so each piece changes colour exactly at the threshold, on a logarithmic axis too; a value exactly on a bound belongs to the zone below it, as `ZoneScale.IndexOf` has it. An area keeps its fill in the series colour. Markers and bars take their value's zone colour, and every mark's accessible name, and so its tooltip, names the zone after the value: `Heart rate: 21:40, 148, Tempo`.
- **Point colours.** `ChartPoint.Color` colours one mark: a column, bar, scatter or bubble mark, a donut slice and its key, or a line or area marker. A point's colour beats its zone's, which beats the series colour. On a line or area a segment takes the colour of the point it starts from, drawn whole even across a zone bound, so a host can colour a line by anything it can compute, such as grade.
- **Zone bands.** `ChartSpec.YZones` shades each zone along the primary value axis, at low opacity in its colour, behind the data and any annotations. Each band is a focusable, labelled aggregate that reads its zone's range — `Tempo: 141.1 to 159.8`, `Active recovery: up to 115.6`, `VO2max: above 178.5` — in the text colour, because a zone colour only has to clear 3:1 and small text needs 4.5:1. The open bottom zone and the unbounded top one stop at the plot edge, a zone wholly off the axis draws nothing, and the bands never widen the axis. They are drawn by the annotation code, so they clip, pan and zoom as Y annotations do and apply wherever those apply; on a horizontal bar chart they stand upright across the value axis.

In JSON a scale is `{"zones":[{"name":"Easy","upper":140},{"name":"Hard","upper":"Infinity"}]}`. JSON has no number for the unbounded top zone's bound, so it is the string `"Infinity"`, which is also how the library writes it. A scale that breaks its own rules — bounds that do not rise, a bounded top zone — is refused while the JSON is read, so the HTTP API answers 400, as it does for any malformed body.

Limits. A long line is sampled before it is coloured, so a crossing shorter than the sampling resolution can be absorbed: a brief excursion past a bound between two kept points is drawn as those points have it, and raising `MaxRenderedPoints` keeps more of them. The legend shows the series colour, which a zone-coloured series may never draw, and the component's data table and status line read a value without its zone; the zones are named on the bands and in every mark's name. Point colours and series zones are refused where colour already says something: direction on candlesticks and OHLC bars, value on a heatmap, and the series or distribution a mark belongs to on stacked column, radar, band, histogram, box and violin charts. Donuts take point colours but not zones, and a density scatter refuses both, because it shades cells rather than points. In the classic finish a band's label sits at its top right, so a band thinner than a line of text lets its label run into the next or past the plot's edge, and the data can cross a label; the refined finish keeps it inside its band or leaves it out, and writes it over the data with a halo. A scale drawn on a chart has at most 32 zones.

### Several marks in one chart

A series can draw as something other than its chart. `ChartSeries.Kind` takes a line, area, column, scatter or band, and the chart's own kind still lays out X: line, area, scatter, bubble and band charts place every series along a continuous axis — numeric, logarithmic or time — and a column chart places them by category. The performance management chart draws fitness and fatigue as lines over each day's training stress as columns, with form as an area against the right-hand axis and the planned days dashed:

```csharp
var load = Training.Load(days.Select(d => (d.Day, d.Stress)));   // planned workouts are later entries
double When(LoadDay d) => TimeAxis.Value(new DateTimeOffset(d.Day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
var planned = TimeAxis.Value(new DateTimeOffset(firstPlannedDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

ChartSpec performance = new() {
    Kind = ChartKind.Line, XAxis = AxisKind.Time,
    YLabel = "Training stress per day", Y2Label = "Form",
    Series = [
        ChartSeries.From("Fitness", load, When, d => d.Fitness) with { ProjectedFrom = planned },
        ChartSeries.From("Fatigue", load, When, d => d.Fatigue) with { ProjectedFrom = planned },
        ChartSeries.From("Form", load, When, d => d.Form) with { Kind = ChartKind.Area, Secondary = true, ProjectedFrom = planned },
        ChartSeries.From("Daily stress", load, When, d => d.Stress) with { Kind = ChartKind.Column }
    ]
};

// Weekly volume by category, with its four-week average through the middle of each week's column.
ChartSpec weekly = new() {
    Kind = ChartKind.Column,
    Series = [new("Volume", weeks), new("Four-week average", averages) { Kind = ChartKind.Line }]
};
```

- **Columns on a continuous axis.** A column series on a line, area, scatter, bubble or band chart draws one bar centred on each X, rising from zero on its own axis, left or right. A bar is 0.7 of the smallest gap on screen between neighbouring X values, clamped to between 1 and 34 pixels as a candle body is, so daily columns fill their days at any zoom. Several column series share one slot per X and stand side by side in it, in series order; the slot is sized from the closest two X values any of them has, so no two slots overlap. X values must be unique within a column series.
- **Lines, areas, points and bands on a column chart** connect or mark the centre of each category, which is where its columns stand, so a weekly column chart can carry its average as a line or its target as a band. Only column series share a category's slot.
- **Drawing order.** Bands are drawn first, then areas, columns and lines, and scatter points and bubbles last, so the broad marks stand behind the narrow ones. Series keep their order within each group, so a chart of one kind draws exactly as it did.
- **Axes.** An axis that carries a column or area series includes zero, and refuses to be logarithmic, reversed or bounded away from zero, as a column or area chart does, because those marks draw from a zero baseline. The other axis is free: the line on the left of a chart with columns on the right can still be logarithmic or reversed. Each series is measured against its own axis, as before.
- **Everything per series follows the mark.** Zones, point colours, trend lines, the order X must run in and a band's bounds are checked and drawn for the mark a series draws rather than the chart's kind: columns on a line chart take their zone colours, a line on a column chart takes a trend fitted through the category centres, and a band on a line chart refuses point colours as a band chart does. Tooltips, accessible names and the component's data table and status line read each series against its own axis. CSV adds the `Low,High` columns whenever any series draws a band.
- **Projections.** `ChartSeries.ProjectedFrom` dashes a line or area stroke from that X onward, for planned workouts carried forward. The stroke is split exactly where the drawn segment reaches the X, interpolated on screen as zone crossings are, so on a logarithmic axis too; on a column chart, which places categories by index, a projection between two is interpolated between their centres. Markers and fill are drawn as before, each mark from the projection on is named `projected` — `Fitness: 24 Aug 2026, 84.3, projected` — and the other marks refuse one.

Line, area, scatter, bubble, band and column charts take a series kind, and since 0.22.0 candlestick and OHLC charts take one on every series beside their candles; horizontal bars, stacked columns and the radial and statistical kinds refuse it, because they lay out every series of a chart together. A series cannot be drawn as a bubble, which shares one size scale across its chart, nor as one of those kinds. In JSON a series kind is a string and a projection a number: `{"name":"Form","kind":"Area","secondary":true,"projectedFrom":1787529600000,"points":[…]}`.

Limits. Every series here shares one plot; [panes](#panes), which stack plots over one X axis, arrived in 0.22.0. A column at either end of a continuous axis is centred on its X, so a wide one is cut by the plot edge, as an end candle is; set `XMin` and `XMax` half a step beyond the data to show it whole. A band series is drawn whole in its place, so its central line and markers stand behind any columns. In the classic finish the legend draws every series as a square whatever its mark; the refined finish keys each series by its mark, and a projected-only line by a dashed one. The component's data table and status line do not say that a value is projected.

### Panes

A chart can stack panes under its main plot, each with Y axes of its own and all sharing one X axis: volume beneath prices, or heart rate, pace and elevation over the same elapsed time. `ChartSeries.Pane` puts a series in a pane, and `ChartSpec.Panes` sets the panes up. Pane 0 is the main plot and takes the spec's own Y properties; pane k takes `Panes[k - 1]`.

```csharp
var closes = bars.Select(b => (double?)b.Close).ToArray();
var average = Statistics.Rolling(closes, window: 20);    // null until 20 closes are in
double Day(Bar b) => TimeAxis.Value(b.Day);

ChartSpec market = new() {
    Kind = ChartKind.Candlestick, XAxis = AxisKind.Time, SkipWeekends = true, Height = 520,
    YLabel = "Price (ZAR)",
    Panes = [new() { Label = "Volume", Weight = .4, YFormat = ValueFormat.Compact }],
    Series = [
        new("ACME", bars.Select(b => ChartPoint.Candle(Day(b), b.Open, b.High, b.Low, b.Close)).ToArray()),
        new("20-day average", bars.Select((b, i) => new ChartPoint(Day(b), average[i]?.Mean)).ToArray()) { Kind = ChartKind.Line },
        new("Volume", bars.Select(b => new ChartPoint(Day(b), b.Volume)).ToArray()) { Kind = ChartKind.Column, Pane = 1 }
    ]
};
```

- **Layout.** `Height` is still the whole chart. The height between the title and the X axis is shared out by weight, the main plot weighing 1 and a pane 0.5 unless its `Weight` says otherwise, after a fixed gap of 24 pixels between each two.
- **One X axis.** Every pane places X through the same mapping: one domain, time zone, set of skipped spans, format and zoom. The X ticks and title appear once, under the bottom pane, and the component's zoom and pan move every pane together.
- **Y axes of its own.** Each pane has its own left axis, gridlines, ticks and title, measured from its own series alone, and its own right-hand axis when one of its series is secondary; the plot narrows for a right-hand axis if any pane has one, so the panes stay aligned. `ChartPane` carries `Label`, `Weight`, `YAxis`, `YMin`, `YMax`, `YFormat`, `YReversed` and `YZones`, and `Y2Label`, `Y2Axis`, `Y2Min`, `Y2Max`, `Y2Format` and `Y2Reversed`; each means for its pane what the spec's property of the same name means for the main plot, and meets the same rules.
- **References.** An X annotation runs through every pane and is named once, in the main plot. Y annotations and the spec's `YZones` belong to the main plot, and a pane's own `YZones` shade that pane. Minor gridlines are drawn in every pane.
- **Everything per series follows its pane.** Columns and areas draw from zero on the axis that measures them, so a logarithmic price axis can stand over volume columns. Zones, trend lines, point colours and a projection's dash are drawn on the pane's axes, and tooltips, accessible names and the component's data table and status line read a value in its pane's format. Each pane clips its own marks and draws them in the order of 0.21.0, with candles and OHLC bars among the columns, so a moving average lies over its candles.
- **Candles with companions.** A candlestick or OHLC chart draws exactly one series as candles or bars, the one that names no kind. Others name their own kind, a line, area, column, scatter or band, such as a moving average over the candles or volume as columns beneath. Candlestick and OHLC charts still refuse a secondary axis.
- **In the component** the legend is shared. Hiding every series in a pane closes it and the panes below move up; when that leaves the main plot empty, the first pane left takes its place and its settings, and the main plot's Y annotations go with it. Hidden candles stay without their points, so their companions still draw.

Line, area, scatter, bubble, band, candlestick and OHLC charts take panes, because they lay X out continuously; the other kinds refuse `Panes` and any nonzero `Pane`. A chart has at most four panes in all, every pane needs a series, a series' `Pane` needs a `ChartPane` to describe it, and a weight must be positive and finite. In JSON, `"panes":[{"label":"Volume","weight":0.4,"yFormat":"Compact"}]` on the chart and `"pane":1` on a series.

Limits. There is no crosshair or tooltip shared across panes: pointing at a mark names that mark alone. The gap between panes is fixed and their heights follow their weights, so a short pane does not grow to fit its ticks or its title; in the refined finish it labels fewer ticks, at least 28 pixels apart, and for more of them give it more weight or the chart more height. Panes cannot be resized or reordered by dragging. `IncludeZero` applies to every pane, and the legend keys every series together, whatever its pane.

### Dense scatter charts

A scatter chart draws every observation, which stops being readable long before it stops being fast: fifty thousand points saturate into solid shapes, and an overlapping series disappears underneath the one drawn after it. Setting `DensityCells` bins the plot into a square grid and shades one cell per occupied region instead:

```csharp
ChartSpec cloud = new() {
    Kind = ChartKind.Scatter,
    DensityCells = 90,           // cells across the plot; 8 to 200, null draws every point
    Series = [new("Cohort A", observations)]
};
```

Cell opacity follows the logarithm of the count, so a dense core does not flatten the sparse edges into invisibility. Each cell is an aggregate: it carries a label reading how many observations it holds and the range it covers, it is focusable, and — like a histogram bin — it reports no single observation, so `PointSelected` does not fire for it. The chart states the total it aggregated and the grid size, so a reader is never shown a thinned cloud that claims to be the whole. CSV export is unaffected and still contains every original observation.

Series keep their own colour and bin independently, so overlapping cohorts stay distinguishable. The measured effect is in [docs/PERFORMANCE.md](docs/PERFORMANCE.md).

### Exports and tooltips

The component toolbar exports SVG, PNG and CSV. PNG is produced in the browser: the same SVG is serialized to a blob, loaded as an image, drawn into a canvas at twice the chart's pixel size over the chart's own background, and saved. The scale is capped so the longest edge stays within 8192 pixels. Text is rasterized with the fonts the browser has, so a host that needs an exact typeface must install or embed it. There is no server-side PNG or PDF rendering — that needs a rasterizer dependency, and these packages have none.

Interactive charts draw their own HTML tooltips: hovering or focusing a mark shows its label, Escape hides it. Those charts render with `includeTitles: false` so the browser's slow native tooltip does not compete with it:

```csharp
var interactive = ChartSvg.Render(spec, includeLegend: false, includeTitles: false);
var exported = ChartSvg.Render(spec);   // keeps a <title> on every mark
```

Static and server-rendered output keeps the native titles by default, so an exported SVG still explains every mark without JavaScript. `LumenGraph` is unchanged and keeps native titles.

Bind application data with `ChartSeries.From("Revenue", rows, r => r.Month, r => r.Amount, r => r.Name)`. Replace the `Spec` parameter to update a chart. `ChartSvg.Render(spec)` and `ChartExport.Csv(spec)` work without a browser.

### Graphs

`LumenGraph` takes a `GraphSpec`, and `GraphEngine` exposes the geometry without SVG:

```csharp
IReadOnlyList<NodePosition> nodes = GraphEngine.Layout(spec);   // one position per node
IReadOnlyList<EdgeRoute> routes = GraphEngine.Routes(spec);     // polyline per edge, bends included
int crossings = GraphEngine.Crossings(spec);                    // in the drawing it produces
string svg = GraphEngine.Render(spec, positions);               // positions override the layout
```

Layered graphs assign longest-path levels, add one routing point per level a long edge spans, then run barycenter sweeps in both directions and keep the ordering with the fewest crossings. Circular graphs place nodes on a ring and report interleaved chords as their crossing count. Both are deterministic: the same spec always produces the same drawing.

```razor
<LumenGraph Spec="graph" NodeSelected="OnNode" />

@code {
    void OnNode(string id) { /* the node's Id */ }
}
```

Dragging a node previews with a transform and commits on release; arrow keys nudge a focused node by eight units and Enter selects it. Moved positions are kept until the node set or layout changes, and the toolbar's Reset layout restores the computed ones. Rendering itself stays static: `GraphEngine.Render(spec)` needs no browser, and the second argument accepts stored positions if your application persists them.

## Training metrics

`Training` computes the numbers endurance-training charts draw, as Allen and Coggan's *Training and Racing with a Power Meter* and TrainingPeaks define them; [FITNESS.md](docs/FITNESS.md) gives the sources and the published values the tests check against. It draws nothing itself. The results are plain numbers and records for the chart kinds above; the [duration axes](#durations-compact-numbers-and-reversed-axes) arrived in 0.19.0, [zones on charts](#training-zones) in 0.20.0, [several marks in one chart](#several-marks-in-one-chart) in 0.21.0, which draws the example below as the apps do, with daily stress as columns, form as an area and planned days dashed, and [panes](#panes) in 0.22.0, which stack an activity stream's heart rate, pace and elevation over one elapsed-time axis. The gallery's [Sports & performance page](#run-the-gallery) shows them all together.

```csharp
var zones = ZoneScale.CogganPower(ftp: 290);             // seven levels; each Upper is inclusive
double? np = Training.NormalizedPower(watts);            // one sample a second
double stress = Training.StressScore(watts.Count, np!.Value, ftp: 290);
var minutes = Training.TimeInZone(watts, zones).Select(s => s / 60);
var curve = Training.MeanMaximal(watts, Training.StandardDurations);
CriticalPowerFit? cp = Training.CriticalPower(curve);

// Fitness and fatigue as lines, form against the right-hand axis.
var load = Training.Load(rides.Select(r => (r.Day, r.Stress)));
double When(LoadDay d) => TimeAxis.Value(new DateTimeOffset(d.Day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
ChartSpec performance = new() {
    Kind = ChartKind.Line, XAxis = AxisKind.Time, YLabel = "Training stress per day", Y2Label = "Form",
    Series = [ChartSeries.From("Fitness", load, When, d => d.Fitness),
              ChartSeries.From("Fatigue", load, When, d => d.Fatigue),
              ChartSeries.From("Form", load, When, d => d.Form) with { Secondary = true }]
};
```

- **Zones.** A `ZoneScale` is a list of `Zone`s whose `Upper` is inclusive: a value belongs to the first zone whose bound is at least the value, so each zone runs from above the previous bound up to its own, and the last zone must be unbounded. Published tables print whole-percent ranges with a gap between them, and this is the rule that reproduces Coggan's worked example — at an FTP of 290 W, 160, 218, 261, 305 and 348 W once rounded half up. The bounds are kept exact, so active recovery at that FTP ends at 159.5 W rather than the 160 a printed table shows. `CogganPower` and `CogganHeartRate` build Coggan's seven power levels and five heart-rate levels from a threshold.
- **Normalized power, intensity and stress.** Normalized power is a 30-second rolling average raised to the fourth power, averaged, and its fourth root. The window is 30 seconds rounded half up to whole samples, and a record shorter than one window gives null. The sources define neither gaps nor a changing recording rate, so pass one uniformly sampled series with any gaps filled or cut. `IntensityFactor` is normalized power over FTP and `StressScore` is hours × IF² × 100.
- **Fitness, fatigue and form.** `Load` runs TrainingPeaks' daily recurrence: fitness moves a 42nd of the way toward each day's stress, fatigue a 7th, and form is yesterday's fitness minus yesterday's fatigue. Every day from the first entry to the last is returned, a day without an entry counting as zero, and one day's entries are added together, so planned workouts are simply later entries. Both time constants and both starting values are parameters, because Allen and Coggan suggest tuning fatigue between about 4 and 12 days and seeding an athlete with no history at their typical daily stress, which starts form at zero.
- **Time in zone** is seconds per zone, in the scale's order. NaN and infinite samples count in no zone.
- **Mean-maximal curves** give the best average over each duration, rounded to whole samples; a duration shorter than one sample or longer than the record is left out. The curve is not forced downhill: between durations that are not multiples of one another a longer one can score higher, and it is reported as the data has it.
- **Critical power** fits Monod's model, total work = W′ + CP × time, to the efforts lasting 3 to 20 minutes, and is null without two different durations among them. The model overestimates what can be held for short efforts, and its R squared is near 1 for any plausible set of efforts, so it says little about the fit.
- **Rolling baselines.** `Statistics.Rolling` gives each entry the mean, sample standard deviation and count of the values in its trailing window, skipping missing ones and returning null below a minimum count; with a minimum of one it is a moving average.

Not implemented, because no source used here defines them: heart-rate and running training stress (TRIMP, hrTSS, rTSS), grade-adjusted pace, W′ balance, Friel's zones, and the vendors' own load, strain, recovery and readiness scores.

## HTTP API

Reference `Lumen.Charts.AspNetCore` and add:

```csharp
using Lumen.Charts.AspNetCore;
using System.Text.Json.Serialization;
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// after builder.Build():
app.MapLumenCharts();
```

| Method | Path | Input / output |
|---|---|---|
| GET | `/api/charts/types` | Chart-kind names |
| POST | `/api/charts/svg` | ChartSpec JSON → SVG |
| POST | `/api/charts/csv` | ChartSpec JSON → original CSV |
| POST | `/api/charts/graph/svg` | GraphSpec JSON → SVG |
| POST | `/api/charts/graph/layout` | GraphSpec JSON → node positions |
| POST | `/api/charts/graph/routes` | GraphSpec JSON → edge polylines |

```json
{"title":"Revenue","kind":"Column","series":[{"name":"Sales","points":[{"x":1,"y":24,"label":"Jan"},{"x":2,"y":38,"label":"Feb"}]}]}
{"title":"Traffic","kind":"Line","xAxis":"Time","yAxis":"Log","series":[{"name":"Edge","points":[{"x":1767225600000,"y":12},{"x":1769904000000,"y":940}]}]}
{"title":"Pace","kind":"Line","xFormat":"Duration","yFormat":"Duration","yReversed":true,"series":[{"name":"Run","points":[{"x":0,"y":305},{"x":600,"y":298}]}]}
{"title":"Latency","kind":"Box","series":[{"name":"Asia","points":[],"summary":{"q1":205,"median":228,"q3":252,"lowerWhisker":160,"upperWhisker":318,"outliers":[352,371]}}]}
{"title":"Effort","kind":"Line","yZones":{"zones":[{"name":"Easy","upper":140},{"name":"Hard","upper":"Infinity"}]},"series":[{"name":"Heart rate","zones":{"zones":[{"name":"Easy","upper":140},{"name":"Hard","upper":"Infinity","color":"#DD4B45"}]},"points":[{"x":0,"y":120},{"x":60,"y":158,"color":"#9E63D3"}]}]}
{"title":"Training","kind":"Line","xAxis":"Time","y2Label":"Form","series":[{"name":"Fitness","projectedFrom":1787529600000,"points":[{"x":1787443200000,"y":86.4},{"x":1787529600000,"y":84.3},{"x":1787616000000,"y":83.3}]},{"name":"Daily stress","kind":"Column","points":[{"x":1787443200000,"y":91},{"x":1787529600000,"y":48},{"x":1787616000000,"y":60}]},{"name":"Form","kind":"Area","secondary":true,"points":[{"x":1787443200000,"y":-6.2},{"x":1787529600000,"y":3.1},{"x":1787616000000,"y":4}]}]}
{"title":"Market","kind":"Candlestick","xAxis":"Time","skipWeekends":true,"panes":[{"label":"Volume","weight":0.4,"yFormat":"Compact"}],"series":[{"name":"ACME","points":[{"x":1772409600000,"open":10,"high":12,"low":9,"close":11},{"x":1772496000000,"open":11,"high":13,"low":10,"close":10.4}]},{"name":"Average","kind":"Line","points":[{"x":1772409600000,"y":11},{"x":1772496000000,"y":10.7}]},{"name":"Volume","kind":"Column","pane":1,"points":[{"x":1772409600000,"y":1500000},{"x":1772496000000,"y":2100000}]}]}
```

Invalid chart semantics return HTTP 400 problem details. Malformed JSON is rejected by ASP.NET Core. The endpoints do not fetch URLs, execute supplied code, save submitted data, or contact outside services. Add application-specific authorization and rate limits when hosting publicly. The sample limits request bodies to 16 MiB.

## Accessibility

Measured by the regression suite, so a change that breaks one of these fails the build:

- Every data mark is a focusable element with an accessible name carrying its series, category and value — `Workspace: Sep, 60.3`. A series whose markers are hidden keeps each one as a transparent target, so its points are reached and announced as any others are; in the refined finish a line's or area's own markers are hidden that way until their point is hovered or focused, and focusing one shows it with the focus ring. Interactive marks use `role="button"`; histogram bins, box glyphs and the outliers of a supplied box summary, which are aggregates, use `role="img"`.
- Each chart and graph exposes its title and description as the accessible name of the drawing.
- Series colors and the zone ramp keep at least 3:1 contrast against both the light and the dark chart background, and Midnight's against its own, and every text color keeps at least 4.5:1, zone band labels included over their band's tint. Heatmap cells carry a hairline so the palest ones stay distinguishable.
- No element takes a positive tab index. The toolbar status is a live region, legend buttons expose `aria-pressed`, the data toggle exposes `aria-expanded`, and the data table has a caption with scoped column headers.
- Keyboard: Tab reaches marks, legend, toolbar and graph nodes; Enter or Space selects a mark or node; Escape hides the tooltip; arrow keys nudge a focused graph node.

Confirmed in a browser accessibility tree: each mark appears as a named button, the chart appears as a named group, and both status regions announce. Every continuous-integration run also sweeps both sample hosts with axe-core, and the gallery a second time in its dark theme, a third on its activity stream's panes and a fourth in its Midnight brand, restricted to the WCAG 2.0 and 2.1 A and AA rules, and fails on any violation.

Not done, and not claimed: no screen-reader run (NVDA, JAWS or VoiceOver), no WCAG conformance statement, and no testing with speech or magnification software. An automated sweep catches only what automation can see — roughly a third of the success criteria — so a clean axe run is a floor, not a certificate. One known rough edge: a chart with many marks produces many tab stops — 1,200 at the default sampling budget — so keyboard users reaching content past a chart may prefer the data table, which stays a single stop and holds the original observations.

## WebAssembly

`samples/Lumen.Wasm` is a standalone Blazor WebAssembly host that references the component package directly:

```powershell
dotnet run --project samples/Lumen.Wasm --urls http://localhost:5199
```

It sits outside `Lumen.Charts.slnx` so the solution build does not need the WebAssembly workload. The suite asserts that `Lumen.Charts` references only framework assemblies and that `Lumen.Charts.Blazor` adds only the Blazor component assemblies — nothing server-only, which is what makes a WebAssembly host possible at all.

Verified in the browser on 10 September 2026: the WebAssembly host loads the class library's static assets, renders the line, column, candlestick, histogram, box and donut samples, shows tooltips, raises point selection back into .NET, exports PNG through the canvas path, and drags graph nodes — the same behavior as the server gallery.

Getting there required a fix rather than a test. `Lumen.Charts.Blazor` previously declared `<FrameworkReference Include="Microsoft.AspNetCore.App"/>`, which asks for the ASP.NET Core shared framework. A WebAssembly host has none, so any Blazor WebAssembly project referencing the library failed to build with `NETSDK1082`, hunting for an ASP.NET Core runtime pack for `browser-wasm` that is not published anywhere. The library now references the `Microsoft.AspNetCore.Components.Web` package instead, which is how a component library serves both hosting models. Building a WebAssembly app also needs the `wasm-tools` workload installed.

## Supported behavior and limits

- Line, area, scatter, bubble, column, horizontal bar, signed stacked column, donut, heatmap, radar, candlestick, OHLC bar, uncertainty band, histogram, box plot, violin. Lines, areas, columns, scatter points and bands can share one line, area, scatter, bubble, band or column chart, each series naming its own mark, and can stand beside the candles of a candlestick or OHLC chart. Line, area, scatter, bubble, band, candlestick and OHLC charts stack up to four panes over one X axis.
- Linear, base-10 logarithmic and UTC time axes on X and Y, and an optional second Y axis on the right; category labels on categorical charts. Time and log X axes apply to line, area, scatter, bubble, candlestick, OHLC and band charts; log Y applies to line, scatter, bubble, candlestick, OHLC, band, box and violin charts, because magnitude, count and radial charts need a zero baseline. Log axes reject zero and negative values. `XFormat`, `YFormat` and `Y2Format` write values as durations in seconds or as compact numbers on linear and log axes, never on a time axis; a linear duration axis steps by a second at the finest and rounds what it shows to the second, and histograms, donuts, heatmaps and radar charts take no format. `YReversed` and `Y2Reversed` apply to line, scatter, bubble, band, candlestick, OHLC, box and violin charts, and only Y axes reverse. Time values must be Unix milliseconds between year 1 and year 9999, and `TimeZone` decides the calendar they are read in. `SkipWeekends` and `TimeSkips` compress a time axis over spans it should not draw, at most 400 listed spans per chart; the axis stays piecewise proportional, so a gap in the data itself still reads as a gap. Irregular tick placement is not implemented.
- `MinorGridlines` adds lighter lines between the labelled ticks: four or five divisions per interval on a linear axis depending on its step, the mantissas between decades on a logarithmic one, and none on a time axis, because half of a month is not a boundary anyone reads. Off by default.
- Explicit limits via XMin/XMax/YMin/YMax. Bars and areas enforce a zero baseline. Null Y preserves gaps in lines/areas and is omitted elsewhere.
- Line/area min/max sampling preserves original indices and extrema per continuous run; this is not a total chart-wide point budget. CSV always exports original observations.
- Up to 100,000 input points, 32 series; 100 categories/slices. Sampling holds a line or area chart at its mark budget, so the browser cost is the same for 1,000 points as for 100,000: about 33 ms either way on the machine in [the measurements](docs/PERFORMANCE.md). Scatter and bubble render every point by default, which is comfortable to about 10,000; 50,000 points means 150,000 DOM elements and 12 MB of markup, and 100,000 means 300,000 elements and 25 MB. A scatter chart can set `DensityCells` to aggregate instead, which takes 100,000 points to 6,504 elements and 33 ms. Bubble has no equivalent, because binning would destroy the size encoding. There is no GPU acceleration and no million-point claim.
- Bubble area is proportional to Size across all series. Radar requires complete, nonnegative series on common categories. Donut accepts one nonnegative series.
- A trend line applies to series drawn as lines, areas, scatter points or bubbles; columns, bars, bands and the radial and derived kinds refuse it. It is one least-squares line per series, fitted over every observation in the series rather than the zoomed window, and it is not an observation: it raises no point selection, appears in no CSV export and adds no row to the data table. Other fits — moving averages, polynomial, exponential regression — are not drawn; `Statistics.Rolling` computes a moving average a host can draw as a series of its own.
- A violin estimates its outline with a Gaussian kernel at Silverman's bandwidth, taking the smaller of the standard deviation and the interquartile range so one long tail cannot smooth the shape away. The estimate is drawn over the observed range and no further, so the outline claims no values the data never had, and it is computed in the space the axis draws in, so a logarithmic axis shapes the violin in logarithms. The widest point of each violin fills its column: widths are comparable within a chart but carry no units, and the quartile bar and median tick carry the numbers. A violin is an aggregate, like a histogram bin or a box: focusable and named, raising no point selection. A series with fewer than two observations, or with no spread, draws its quartile bar and median without an outline. The bandwidth is not configurable, and split or paired violins are not implemented.
- Candlestick and OHLC bar draw one series as candles or bars and take others beside it in kinds of their own, and a histogram accepts up to four series. Candlestick and OHLC bar take the same input: all four prices with High highest and Low lowest, colored by direction rather than by series. An OHLC tick is half the width of a candle body, so the two drawings of one dataset stand in the same columns and can be compared; volume goes beneath either as a column series in a pane. Band points need both bounds or neither. Histogram and box read observations from Y and ignore X. A histogram of several series bins them over one set of edges chosen from the pooled observations and stands their bars side by side; counts are raw, not normalised, so a larger series draws taller bars. A box series may instead carry a precomputed `Summary` and no points: it is drawn as given, claims no observation count, applies to box charts only, and its outliers count towards the 100,000-point limit. Histogram bins and box glyphs are labelled, focusable aggregates that report no observation index, so they raise no point selection; candlesticks, OHLC bars and box outliers computed from observations do, and the outliers of a supplied summary do not.
- Layered graphs use longest-path levels, then barycenter sweeps that keep the ordering with the fewest crossings found. This is a heuristic, not minimal crossings. Edges spanning several levels bend once per level and are drawn as smooth curves; there is no orthogonal routing, no force simulation and no automatic node overlap removal. Self-loops are allowed in layered graphs and draw as a loop on their node; longer cycles still need the circular layout. Nodes can be dragged or nudged with the arrow keys in the component, which needs an interactive render mode. At most 250 nodes / 2,000 edges; dense graphs can still overlap.
- On a narrow screen a chart keeps at least 640 pixels and scrolls sideways in a keyboard-focusable viewport, so its labels stay legible, unless it sets `FitWidth`, which draws it at the width of its container, down to 320 pixels, with its text at its own size. Graphs keep the scrolling viewport.
- HTML tooltips on hover and keyboard focus in the component, native SVG tooltips in exported and server-rendered charts, keyboard-focusable data marks, point selection, tables, and accessible labels. See [Accessibility](#accessibility) for what is measured and what is not. This is not a claim of WCAG certification.
- SVG, PNG and CSV exports. PNG is rasterized in the browser from the same SVG, so it needs an interactive render mode; there is no server-side PNG or PDF rendering, 3D, or streaming transport yet.
- Research materials are excluded from packages. No vendor source code or book images are redistributed.

See [research and architecture](docs/RESEARCH.md), [verification](docs/VERIFICATION.md) and [measured performance](docs/PERFORMANCE.md). This is an original preview implementation, not a claim of feature or performance parity with mature commercial products.

## 0.25.0 additions

Two gaps that testing the [Claude Code skill](#use-it-from-claude-code) on two fresh projects found in the library itself.

`LumenChart` takes `FitWidth`, described under [Fitting the width it is shown at](#fitting-the-width-it-is-shown-at). To fit a chart to a phone or a narrow card, an app had to measure the container, set `ChartSpec.Width` from it and override the stylesheet's 640-pixel minimum, as the gallery's Sports & performance page did by hand. The component now measures its own box with a `ResizeObserver`, waits for it to settle, redraws at its width and lifts the minimum for that chart through a class on its root; it stops measuring when it is disposed. The first render, prerendered or static, is drawn at `Spec.Width` as before. A chart that leaves it off renders exactly as 0.24.0 did, which four hashes of the component's prerendered markup, taken before the change, confirm. The Sports & performance page sets it on every chart and has lost its width plumbing: its script no longer measures two kinds of card and feeds their widths into every spec. It still reports one thing, whether the activity stream's card is too narrow for the kilometre markers' labels to stand clear of the zones', because that decides what the chart shows rather than how wide it is drawn. The WebAssembly sample sets it on its chart, so the browser suite checks it on both hosts. `LumenGraph` does not take it, because a dragged node keeps its place in the drawing's coordinates.

The packages carry their XML documentation beside each assembly, so IntelliSense and coding agents can read what a member does from the package itself. The members a consumer uses most gained comments where they had none: every property of `ChartSpec`, `ChartSeries`, `ChartPoint`, `ChartPane`, `ChartAnnotation` and `ChartStyle`, the zone types and what `Training` and `Statistics` return, the chart kinds and the other enums, the graph types, the rendering, export and validation entry points, `MapLumenCharts`, and every component parameter. Of the 355 public members the compiler checks in the core and ASP.NET Core packages, 346 are documented, where 177 were missing a comment before, and a new public member there without one fails the build. The nine left, `LinearScale`'s eight members and the `Sampling` class, are low-level helpers whose missing-comment warning is suppressed for those two types alone. The Blazor package's 25 component parameters and methods are documented too, though nothing enforces it there, since Razor's generated code switches the warning off. Nothing draws differently: the 189 hashed renderings match 0.24.0.

## 0.24.0 changes

Every chart looks different by default. The [refined finish](#the-refined-finish) is now what every preset and every brand draws: 1.6-pixel strokes with round ends that keep their width at any display size, trend and reference lines thinner than the data, line and area markers that appear on hover and keyboard focus, hairline dotted gridlines, legend keys shaped like their marks, ticks spaced to the room a plot or pane has, and annotation and zone labels kept inside the plot and their band, nudged clear of each other and written over the data with a halo. `ChartStyle.Finish` chooses it, `ChartFinish.Refined` by default. To keep the old look, set `Finish = ChartFinish.Classic` on the style a chart draws with, `ChartSvg.ResolveStyle(spec) with { Finish = ChartFinish.Classic }` for a chart on a theme, or `"finish":"Classic"` in JSON: the classic finish draws exactly as 0.23.0 did, byte for byte.

It fixes four readability defects as it goes, in the refined finish only. A short pane, such as the pace pane of an activity stream, crowded five tick labels into it; it now labels as many as stand 28 pixels apart, and X labels are thinned until none touch. A zone band thinner than its label let the label run out of it, so the bottom zone of a heart-rate chart was cut off at the plot's edge, and two labels on one edge could land on each other, as a weekly-load chart's average did on a band's; labels now stay inside their band and the plot or are left out, and are nudged apart. A series drawn in colours of its own, such as time in zone, was keyed in a series colour it never drew; its key now shows its own colours. `ChartSvg.LegendKey` returns a series' key as a small SVG, which the component's legend draws.

All 189 hashed renderings of the release baseline match 0.23.0 in the classic finish, and every one of them changes in the refined; a contact sheet of all 189, classic beside refined, was looked through for clipped text, labels over marks, missing marks, broken panes and wrong colours, which found a point between two gaps vanishing with its hidden marker, and labels set in the wrong order losing a thin band's label; both are corrected. The gallery draws in the refined finish.

## 0.23.0 additions

The finish of a fitness app, from the phone conventions in [FITNESS.md](docs/FITNESS.md), described under [Finish and styling](#finish-and-styling). On a series: `StrokeWidth`; `Curve`, smooth as a monotone cubic on screen or stepped, which a fill, zone colours and a projection follow; `Fill`, which fades an area or a column; `Gradient`, which colours a line or area and its markers by value through the series' own axis; `Markers`, which can hide them while keeping every point focusable and announced; `HighlightLast`; and `ValueLabels`, left out where they would not fit. On a style: `BarRadius`, which rounds only a bar's far end and makes capsules, and `Gridlines`, dotted, dashed or hidden. On a chart: `YAxisSide` and `YTickLabels`, for an axis on the right labelled at its ends. `ChartStyle.Midnight` is a near-black preset whose vivid colours clear 3:1 for every mark and 4.5:1 for text. Gradients brought the library's first SVG IDs, named after a hash of the chart's spec so that charts sharing a page cannot collide, and written only by a chart that uses one. All of it round-trips through the HTTP API's JSON.

A chart that sets none of this renders as before, without an ID: the 143 hashed renderings match, and so do thirty more, hashed before the change, that guard the stylesheet in each preset and a brand, columns and bars with negative values on category and continuous axes, stacked ends, scatter and line markers, minor gridlines on lines, bars, time axes and the frame the statistical kinds share, areas below zero, a radar's rings, and panes. Sixteen new ones cover a smooth faded area, step lines with hollow markers, gradients on a logarithmic axis and on a reversed pace axis, capsule columns with value labels and fades, hidden markers, highlighted last readings, dotted gridlines with the axis on the right labelled at its ends, a smooth zone-coloured line with a projection, horizontal bars with dashed gridlines, stacked capsules, a box plot with its axis on the right and no grid, scatter markers, and Midnight on the performance management chart, weekly volume and the activity stream. The gallery's brand switcher adds Midnight; its activity stream colours heart rate by value over the zone bands and draws the climb as a smooth faded area, with the markers hidden; its performance management chart rings the fitness to arrive with; and under the Lumen brand its weekly load stands as capsules. The heart-rate zones and the other demonstrations stay plain, so the difference shows.

## 0.22.0 additions

Panes stacked under one X axis, the second half of the fourth step of the build order in [FITNESS.md](docs/FITNESS.md) and the volume pane from the older roadmap, described under [Panes](#panes). `ChartSeries.Pane` puts a series in a pane, and `ChartSpec.Panes` sets up each pane below the main plot with a `ChartPane`: its title, its weight beside the main plot's 1, and its own Y and right-hand axis kinds, bounds, formats, reversal and zones. The panes share the plot by weight a fixed gap apart, and share one X axis, its domain, time zone, skipped spans, format and zoom, labelled once under the bottom pane; each has its own gridlines, ticks and titles, its own right-hand axis when it needs one, and its own clip. X annotations run through every pane, named once; Y annotations and the spec's zones stay in the main plot. Candlestick and OHLC charts now take companion series beside the one they draw as candles or bars, in kinds of their own, so a moving average from `Statistics.Rolling` can lie over the candles and volume stand as columns beneath. Everything 0.21.0 does per series follows the series' pane: the zero baseline, zones, trends, point colours, projections, labels and the component's data table and status line. The component zooms and pans every pane together, closes a pane whose series are all hidden, and keeps hidden candles in place so their companions still draw. All of it round-trips through the HTTP API's JSON.

A chart that sets none of this renders as before: the 124 hashed renderings match, and so do twelve more, hashed before the change, that guard candlestick and OHLC charts on trading axes with weekends and a holiday skipped, in a time zone, logarithmic and reversed; secondary axes with X and Y annotations, zones, minor gridlines and a compact, logarithmic or reversed right-hand axis; duration axes on both sides; minor gridlines on horizontal bars and on a column chart with a secondary line; a density scatter beside a secondary series; columns and a band on a trading axis; and zone bands on a dark area chart. Seven new ones cover candles and OHLC bars with a five-day average and a volume pane, in both themes; a three-pane activity stream on a duration axis, with heart rate over its zones, pace on a reversed axis and the climb as an area, in both themes; a pane with a right-hand axis of its own; and a logarithmic main plot over columns in a pane. The gallery's candlestick and OHLC charts now carry a five-day average and the volume beneath, and its line chart adds an Activity stream mode.

Building this found one defect older than it. A chart with a secondary series and a null entry in its series list threw a null reference while counting the series on the left, before the check that refuses null series could run, so the HTTP API answered with a server error rather than 400. Counting by pane skips null entries, and the request is now refused.

## 0.21.0 additions

Several marks in one chart, the first half of the fourth step of the build order in [FITNESS.md](docs/FITNESS.md), described under [Several marks in one chart](#several-marks-in-one-chart). `ChartSeries.Kind` draws a series as a line, area, column, scatter or band on a line, area, scatter, bubble, band or column chart, whose own kind still lays out X. Columns on a continuous axis take their width from the closest gap between X values, as candles do, and several stand side by side; lines, points and bands on a column chart mark the category centres. Bands are drawn first, then areas, columns, lines and points. An axis that carries columns or an area includes zero and, as a column chart's does, refuses to be logarithmic, reversed or bounded away from zero. Zones, point colours, trend lines, labels, the component's data table and CSV's band columns follow the mark a series draws rather than the chart's kind. `ChartSeries.ProjectedFrom` dashes a line or area from an X onward, split exactly where the stroke reaches it, and names each mark from there projected. Between them they draw the performance management chart, load against a target range, and a weekly column chart with its average. All of it round-trips through the HTTP API's JSON. Panes stacked over one X axis, the second half of that step, come later.

A chart that sets none of this renders as before: the 106 hashed renderings match, and so do thirteen more, hashed before the change, that guard the series loop and its order, each axis's zero, category slots with three series, bands on both axes, signed stacks, three horizontal bars, small bubbles, trends beside a secondary series and a density note over two series. Five new ones cover a performance management chart built with `Training.Load` in both themes, load against a moving target band on a time axis, and a weekly column chart with its average line, alone and beside a second column series. The gallery's line chart adds a Fitness and fatigue mode — twelve simulated weeks through the load model and two planned weeks dashed — and its column chart a Load against target mode.

Building this found one defect older than it. A band chart checked every series' band bounds against the left axis, so a band measured on a logarithmic right-hand axis accepted a bound of zero or below and drew `NaN` into the SVG. Each band's bounds are now checked against the axis that measures it, which a band drawn on another kind of chart needed anyway.

## 0.20.1 fixes

Two defects older than 0.20.0, and nothing new.

A value annotation on a horizontal bar chart was drawn across the plot at a height that meant nothing: the annotation code laid every Y reference along the screen's vertical, and a horizontal bar chart draws its values along X. It now stands upright at its value, as a line or a band, through the same path as the zone bands, with its label on the side with more of the plot so a target near the end of the axis stays in view. `AnnotationAxis` now says what each axis means, described under [Annotations](#annotations): Y is the value axis wherever the chart draws it, and X stays refused on every category chart, horizontal bars included. The gallery's bar demonstration draws its target upright at 55. Being a fix, this changes renderings on purpose: of the 106 hashed renderings, the one with Y annotations on a horizontal bar chart changed and the other 105 match, among them a new guard, hashed before the change, for zone bands on a horizontal bar chart, which share the code.

The gallery's dark theme failed axe-core's contrast rule on 13 nodes, all in its own chrome. Its accent blue, `#4B66CA`, and its section-label grey, `#636D80`, were the same in both themes and measured between 2.67:1 and 3.46:1 on the dark surfaces. They are now the page tokens `--accent` and `--eyebrow`, which the dark theme lightens in their own hue to `#8295DA` and `#9099A9`, at least 4.82:1 and 4.85:1 wherever they sit; every text use of the accent takes the token, the headline and brand mark included, so the dark theme keeps one accent, and the light theme is unchanged. The browser suite swept only the light theme, which is why it never saw them. It now presses the gallery's theme button and sweeps again, and says SKIP on a host without one. The WebAssembly page has no dark theme, and its one use of `#4B66CA`, on white, measures 5.18:1.

## 0.20.0 additions

Zones on charts, the third step of the build order in [FITNESS.md](docs/FITNESS.md), described under [Training zones](#training-zones). `Zone` takes an optional colour, and `ChartStyle.Zones` gives the zones without one a seven-colour ramp that clears 3:1 on both presets and that `ContrastIssues` now checks. `ChartSeries.Zones` colours a line, area, scatter, bubble, column or bar series by the zone of each value, splitting a line or area stroke exactly where it crosses a bound and naming the zone in every label. `ChartPoint.Color` colours a single mark ahead of its zone and its series, and a line segment from the point it starts at. `ChartSpec.YZones` shades each zone as a labelled band behind the data, through the annotation path, without widening the axis. Between them they draw the activity stream, time in zone, a grade-coloured elevation profile and a zone-coloured scatter. All of it round-trips through the HTTP API's JSON, the unbounded top zone as `"Infinity"`.

A chart that sets none of this renders as before: the 84 hashed renderings match, and so do sixteen more, hashed before the change, that guard sampled lines and areas, markers, donuts, and Y annotations on seven kinds and on log and reversed axes. Five new ones cover a zone-coloured heart-rate stream over its bands in both themes, time-in-zone bars, a grade-coloured area and a zone-coloured scatter. The gallery's line and bar charts add a Heart-rate zones mode: a simulated interval run coloured and shaded by Coggan's heart-rate zones, and the time it spent in each.

Building this found two things. A zone scale checks itself as it is constructed, so a broken one posted to the HTTP API threw from inside the JSON reader and was answered with a server error; it is now reported as invalid JSON, and answered 400. And a Y annotation on a horizontal bar chart had always been drawn across the plot at a vertical position rather than upright at its value. Correcting that would change existing renderings, so it was left for a release of its own, [0.20.1](#0201-fixes); zone bands, which share the annotation code, were drawn upright there from the start.

## 0.19.0 additions

Axis formats and reversed axes, the second step of the build order in [FITNESS.md](docs/FITNESS.md), described under [Durations, compact numbers and reversed axes](#durations-compact-numbers-and-reversed-axes). `ValueFormat` and `ChartSpec.XFormat`, `YFormat` and `Y2Format` write an axis's values as durations — `m:ss` and `h:mm:ss` with ticks on round durations on a linear axis, `1s`, `5m` and `1h` on a logarithmic one — or as compact numbers such as `1.2k` and `3.4M`. `YReversed` and `Y2Reversed` put the smallest value at the top, so a faster pace sits higher. Reversal is one property on `Axis`, honoured by `Map` and `Invert`, so everything placed through them follows, and a trend line still says whether the data rises or falls. Every place that reads out a value — ticks, tooltips, accessible names, annotation labels, the data table and the status line — reads it in the axis's format, and CSV keeps raw numbers. Formats are refused on a time axis and on kinds with no axis to carry them, and reversal on the kinds drawn from a zero baseline. All of it round-trips through the HTTP API's JSON, enums as strings.

A chart that sets none of this renders as before: the 73 hashed renderings match, and so do seven more, hashed before the change, that guard the log, time and secondary axes, minor gridlines, trend lines and a logarithmic violin. Four new ones cover a duration line, a power–duration curve, a reversed pace line and a compact column chart. One thing does read differently: the component's status line used to print a selected value as the raw number in the host's culture, and now reads it as the tooltip does, in the axis's format.

Limits: a linear duration axis steps by a second at the finest and rounds what it shows to the second, so sub-second durations are not shown; compact labels carry one decimal, so close ticks can round unevenly; histograms take no format; and only Y axes reverse. The gallery's line chart adds two axis modes: a power–duration curve built with `Training.MeanMaximal` on a logarithmic duration axis, its critical-power fit drawn as a reference line, and a pace line on a reversed duration axis. Its logarithmic demonstration now writes thousands compactly.

Building this found a hang older than it: with minor gridlines on an axis near 1e21, where a tick step is too small to move the value it is added to, the loop that places the minor lines never ended, and the HTTP API would accept such a request. The loop now counts its intervals as well, which changes nothing at ordinary magnitudes.

## 0.18.0 additions

Training metrics, the first step of the build order in [FITNESS.md](docs/FITNESS.md). They are computation only, so nothing draws differently: the 73 hashed renderings match. `ZoneScale` holds ordered, named zones and builds Coggan's seven power levels and five heart-rate levels from a threshold; `Training` computes normalized power, intensity factor and training stress, the fitness–fatigue–form model, time in zone, mean-maximal curves and a critical-power fit; and `Statistics.Rolling` gives the trailing mean and deviation a baseline band needs, which is also the moving average planned under the regression families. Each is checked against what the sources publish where they publish a value: Coggan's zones at an FTP of 290 W, an hour at threshold scoring 100, Allen and Coggan's seven-hour ride scoring 528.5 within the rounding of its intensity factor, and TrainingPeaks' recurrence worked by hand over four days. Where the sources leave a choice open it is made and documented — which zone a value in a published gap belongs to, how normalized power counts its window in samples, and what form is on the first day. Heart-rate and running training stress, grade-adjusted pace, W′ balance and the vendors' own scores are left out because no source defines them. There is no gallery demonstration yet; the charts these numbers feed come in later releases.

## 0.17.0 additions

Precomputed box summaries. `ChartSeries.Summary` takes a `BoxSummary` — quartiles, median, whiskers and outliers — computed somewhere else, such as a warehouse that never hands over its rows, and the box chart draws it as given instead of computing one. A series with a summary carries no points, and one with both is refused, because the two could disagree; in JSON, send `"points":[]` beside the summary. The numbers must be finite and ordered, `LowerWhisker <= Q1 <= Median <= Q3 <= UpperWhisker`, and positive on a log axis, which reaches the whiskers and outliers. The outliers are not checked against the whiskers: a host's own rule may put the whiskers at the minimum and maximum, or at the 5th and 95th percentiles, rather than at Tukey's fences, and the chart draws what it is given. The observation count is unknown, so the column reads `Asia (summary)` rather than claiming an `n`, the box's accessible name says it is a supplied summary, and its outliers are focusable, labelled aggregates that raise no point selection, because there is no point behind them. Every other kind refuses a summary; a violin, in particular, cannot estimate a density from five numbers. CSV export and the component's data table list observations, so a summary series adds no rows to either.

Histograms of several distributions. Up to four series share one set of equal-width bins chosen from their pooled observations — `Bins` if set, the automatic rule otherwise — so a bin covers the same range for every series, and their bars stand side by side within it, in their series colours, rather than over one another. Each bar's label names its series, the caption says the bins are shared, and the static SVG adds a legend. `Statistics.SharedBins` returns the same counts without rendering. Past four series the bars are too narrow to read, so a fifth is refused. The bins are fitted to the pooled range, so a narrow series sits in the few bins it reaches, and counts are not normalised, so series of different sizes compare by height only as far as their sizes allow. Overlaid, stacked and density histograms are not implemented.

A single-series histogram and every box chart without a summary render byte for byte as before: the 71 hashed renderings match, and two new ones cover the new cases. The gallery's histogram compares response times before and after a cache, and its box plot adds Asia as a warehouse summary beside the three sampled regions.

## 0.16.0 additions

`ChartKind.Ohlc`, the open-high-low-close bar: a vertical line over the day's range with the open ticked out to the left and the close to the right. It is the American alternative to the candlestick, and it reads the same input — one series of `ChartPoint.Candle` points — so switching one kind redraws the same prices. It carries everything the candlestick does: time and log axes, skipped weekends and holidays, annotations, the four-price CSV columns and the same per-mark label. The limits are the candlestick's too: one series, all four prices required, no volume pane. A chart of any other kind is unchanged across the same renderings, which are now 71 rather than 67 because the new kind adds four of its own. The gallery draws the same 30 trading days as the candlestick demonstration, so the two glyphs can be held against each other.

## 0.15.0 additions

`ChartKind.Violin`, and the `Statistics.Density` kernel density estimate behind it, for the shape of a distribution rather than its five-number summary. A violin chart shows what a box plot cannot: a set of observations with two clusters reads as one box, but as two bulges. The estimate is made in the space the axis draws in and over the observed range only. The gallery's latency demonstration draws the same three regions as its box plot.

## 0.14.0 additions

Trend lines: `ChartSeries.Trend` draws a least-squares fit through a series, and `Statistics.Fit` exposes the slope, intercept and R squared behind it. Fitting in the space the chart draws in means one straight line on a logarithmic axis and one straight line on a trading axis, rather than a curve or a jump at every weekend. A chart without a trend is unchanged across the same 63 renderings. The gallery's scatter demonstration fits each of its three series.

## 0.13.0 additions

Business calendars: `ChartSpec.SkipWeekends` and `ChartSpec.TimeSkips`, so a trading chart puts its days side by side instead of spending two sevenths of its width on closed markets. The compression sits in `Axis`, which means the rendering, the zoom, the tooltips and the data table all read one domain. A chart that skips nothing is unchanged down to the byte across the same 63 renderings. The gallery's candlestick demonstration now draws 30 trading days with the weekends and Good Friday left out.

## 0.12.0 additions

`ChartSpec.MinorGridlines` and `Axis.MinorTicks`, for a lighter grid between the labelled ticks. A chart that does not ask for them is unchanged down to the byte, including the stylesheet, which carries no rule for lines it never draws. The gallery's logarithmic demonstration divides its decades.

## 0.11.0 additions

`ChartSpec.TimeZone`, so a time axis reads its calendar where the data happened rather than in UTC. Day, month and year ticks land on their local boundaries and hold them across a clock change, and tooltips and the data table follow. A chart without a zone is unchanged, which was checked across 63 renderings before and after. The gallery's time demonstration reads in Johannesburg.

## 0.10.0 additions

A secondary Y axis, described under [A second axis](#a-second-axis): `ChartSeries.Secondary`, `Y2Label`, `Y2Axis`, `Y2Min` and `Y2Max`. Charts without one render exactly as before, which was checked across 63 renderings before and after the change. The gallery's line demonstration measures conversion against the right-hand axis.

## 0.9.0 additions

Reference lines and bands, described under [Annotations](#annotations): `ChartAnnotation` and `ChartSpec.Annotations`, drawn behind the data in data coordinates so they move with it. The gallery's line, area, column and bar demonstrations carry a target and a campaign window.

## 0.8.0 additions

Branding, described under [Branding](#branding): `ChartStyle` with `Light` and `Dark` presets, `ChartSpec.Style` and `GraphSpec.Style`, a cascaded style for Blazor trees, `LumenBrand` to read a brand from the page's own CSS, `ContrastIssues` and `Contrast.Ratio` for checking it, `ChartStyle.FontFamilyFrom`, and CSS custom properties for the component chrome. Output for a spec without a style is byte-for-byte unchanged, which was checked across 61 renderings before and after the change. The gallery has a Lumen / Harbour (C#) / Page CSS switch, and the WebAssembly sample takes its brand and typeface from its own stylesheet.

## 0.7.0 additions

`DensityCells` for scatter charts, described under [Dense scatter charts](#dense-scatter-charts), and `tests/Lumen.Charts.Profile` with the measurements that motivated it. Also fixes a caption that grouped its counts with the host's culture rather than invariantly, so a chart rendered on a French or South African machine read `50 000` where every other number in the library reads `50,000`.

## 0.6.2 accessibility

An axe-core sweep of both sample hosts now runs on every continuous-integration run. Its first pass reported 62 serious colour-contrast failures in the gallery's own chrome and 4 on the WebAssembly page, all from sample styling rather than the library: muted text at 3.7:1, section labels at 3.2:1, accent links at 3.9:1, a caption dimmed further by a container opacity, and a bare `button` rule on the WebAssembly page that restyled the component's own legend. The sample palettes were darkened and that rule scoped; both hosts now report no WCAG A or AA violation.

The library binaries are unchanged from 0.6.1: every fix in this version is in the sample hosts and in the test suite.

## 0.6.1 fixes

Marks on the first and last value of a line, area, scatter or bubble chart were drawn exactly on the plot's clipping boundary, so half of each was cut off and a pointer at the mark's own centre missed it. The clipping viewport is now inset by one marker radius. Found by the new browser suite, and covered by both it and the executable suite.

## 0.6.0 additions

An accessibility pass with contrast, role, name and tab-order assertions in the suite; three palette colors darkened to reach 3:1 on a light background (`#18A999`, `#DA9650` and `#58A4C1` measured 2.93, 2.48 and 2.80); heatmap cell outlines; chart and graph descriptions folded into the accessible name; scoped data-table headers; and a WebAssembly sample host, which exposed and fixed a real defect: the component library demanded the ASP.NET Core shared framework and therefore could not be referenced from any Blazor WebAssembly project. See [Accessibility](#accessibility) and [WebAssembly](#webassembly).

## 0.5.0 additions

Layered graph crossing reduction, bend routing for long edges, `GraphEngine.Routes`, `GraphEngine.Crossings`, position overrides on `GraphEngine.Render`, node dragging and keyboard nudging with a `NodeSelected` callback and a reset control on `LumenGraph`, and the `/api/charts/graph/routes` endpoint. See [Graphs](#graphs). Layered layouts now accept self-loops instead of rejecting them as cycles.

## 0.4.0 additions

Browser PNG export and HTML tooltips, described under [Exports and tooltips](#exports-and-tooltips), plus the `includeTitles` parameter on `ChartSvg.Render` and an export status message in the component toolbar.

## 0.3.0 additions

Candlestick, uncertainty band, histogram and box-plot families, the `Statistics` helpers behind them, `Open`/`High`/`Low`/`Close` on `ChartPoint` with the `Candle`, `Interval` and `Observation` factories, the `Bins` specification field, family-specific CSV columns, and gallery demonstrations for all four. See [Statistical and financial families](#statistical-and-financial-families).

## 0.2.0 additions

Time and logarithmic axes as described under [Axes](#axes), including calendar tick selection, UTC invariant labels, date tooltips and table entries, zoom and pan in log and time space, the `XTime` CSV column, and `xAxis` / `yAxis` fields on the JSON endpoints. The gallery has a Numeric / Time X / Log Y switch on the charts that support them.

## 0.1.1 fixes

SVG exports include a visible series legend (donut and heatmap already have labels). The legend footer adds 22 pixels per row to the SVG viewBox beyond the specified plot height. Pass `includeLegend: false` to `ChartSvg.Render` to omit it. Blazor keeps its interactive legend, and downloads include the static legend. Bubble areas share one size scale across series. SVG CSS uses prefixed selectors and per-chart color variables so mixed themes can coexist without styling unrelated host elements.

## License

MIT. See [LICENSE](LICENSE). The packages carry the same expression, so consumers see it in their dependency reports.
