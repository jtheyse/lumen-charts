# Lumen Charts

A standalone C# chart library, Blazor components, ASP.NET Core rendering API, and an interactive gallery. Preview 0.19.0. No third-party charting engine or CDN is required.

## Run the gallery

Requires the .NET 10 SDK (the reusable packages target .NET 8).

```powershell
dotnet run --project samples/Lumen.Gallery --urls http://localhost:5188
```

Open http://localhost:5188. The gallery includes chart selection, light/dark themes, refreshed sample data, series filtering, point selection, a numeric / time / log axis switch with a power–duration curve and a reversed pace line on duration axes, X zoom/pan/reset, original-data tables, SVG/PNG/CSV downloads, and network layouts with draggable nodes.

## Build and verify

```powershell
dotnet restore Lumen.Charts.slnx --configfile NuGet.Config
dotnet build Lumen.Charts.slnx -c Release --no-restore
dotnet run --project tests/Lumen.Charts.Tests -c Release
dotnet pack src/Lumen.Charts -c Release -o artifacts/packages
dotnet pack src/Lumen.Charts.Blazor -c Release -o artifacts/packages
dotnet pack src/Lumen.Charts.AspNetCore -c Release -o artifacts/packages
```

With a host running, `tests/Lumen.Charts.BrowserTests` drives it in a real browser:

```powershell
dotnet build tests/Lumen.Charts.BrowserTests -c Release
pwsh tests/Lumen.Charts.BrowserTests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet run --project tests/Lumen.Charts.BrowserTests -c Release --no-build -- http://localhost:5188
```

It uses component selectors only, so the same fourteen checks, the axe sweep included, run against the gallery and against the WebAssembly host on port 5199. It stays outside the solution so the ordinary build needs no browser download.

The repository NuGet.Config restores from nuget.org for one dependency: `Lumen.Charts.Blazor` references `Microsoft.AspNetCore.Components.Web` (8.0.0) rather than the ASP.NET Core shared framework, because a WebAssembly host has no shared framework to reference. `Lumen.Charts` and `Lumen.Charts.AspNetCore` add no packages of their own. With the gallery running, execute `./tests/verify-api.ps1` for HTTP integration checks.

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

Use an interactive render mode in a Blazor Web App to enable toolbar actions and callbacks. Rendering itself supports static HTML. The gallery demonstrates Interactive Server and [samples/Lumen.Wasm](#webassembly) demonstrates standalone WebAssembly; both are exercised. JavaScript is limited to event delegation, tooltips, canvas rasterization and browser downloads.

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
| `Candlestick` | One series; every point carries `Open`, `High`, `Low`, `Close` — use `ChartPoint.Candle` | Wick across the low-high range, body from open to close, colored by direction (`ChartSvg.RisingColor` and `FallingColor`) |
| `Ohlc` | The same as `Candlestick` — one series of `ChartPoint.Candle` points | Vertical line across the low-high range, a tick to the left at the open and a tick to the right at the close, colored by direction |
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
3. **`Theme`** — the built-in `Light` and `Dark`, used when neither of the above is set. `ChartStyle.Light` and `ChartStyle.Dark` reproduce them exactly.

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

`ContrastIssues` applies the WCAG 2.1 minimums — 4.5:1 for text, 3:1 for series, candles and edges — and both built-in presets report none. `LumenBrand` raises `Resolved` with each style it reads, so an application can check a page-supplied brand at run time too.

Limits: server rendering cannot read a stylesheet, so `ChartSvg.Render` and the HTTP API need an explicit `ChartStyle`. `LumenBrand` discards transparency, since a chart colour is drawn opaque, and maps series, background, text, muted, grid and candle colours; graph edges and the heatmap ramp come from `Fallback`. Font lists are reduced to letters, digits, spaces, commas and hyphens because they are written into a style attribute; `ChartStyle.FontFamilyFrom` performs that reduction on any CSS value.

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

At least one series must stay on the left, so the left axis always means something. A secondary axis applies to line, area, scatter, bubble, column and band charts; stacked columns, horizontal bars, candlesticks and the radial kinds reject it rather than imply a comparison they cannot make. Annotations measure against the left axis.

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

`From` alone draws a line, dashed unless `Dashed` is false; adding `To` draws a band. Values are in data coordinates, so an annotation zooms and pans with what it refers to and clips at the plot edge. They render behind the data, take the style's muted colour unless `Color` names one, and each is a focusable, labelled aggregate reading `Target: 55` — the value is always shown, so a reference can never sit somewhere other than where it claims.

Annotations apply to the charts drawn on an X and Y axis. Donut, radar, heatmap, histogram and box charts reject them rather than place them arbitrarily, and an X annotation is refused on a category chart, whose bars sit at indices rather than at values. At most 32 per chart.

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

`Training` computes the numbers endurance-training charts draw, as Allen and Coggan's *Training and Racing with a Power Meter* and TrainingPeaks define them; [FITNESS.md](docs/FITNESS.md) gives the sources and the published values the tests check against. It draws nothing itself. The results are plain numbers and records for the chart kinds above; the [duration axes](#durations-compact-numbers-and-reversed-axes) arrived in 0.19.0, and the zone colours and mixed marks the training charts need arrive in later releases.

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
```

Invalid chart semantics return HTTP 400 problem details. Malformed JSON is rejected by ASP.NET Core. The endpoints do not fetch URLs, execute supplied code, save submitted data, or contact outside services. Add application-specific authorization and rate limits when hosting publicly. The sample limits request bodies to 16 MiB.

## Accessibility

Measured by the regression suite, so a change that breaks one of these fails the build:

- Every data mark is a focusable element with an accessible name carrying its series, category and value — `Workspace: Sep, 60.3`. Interactive marks use `role="button"`; histogram bins, box glyphs and the outliers of a supplied box summary, which are aggregates, use `role="img"`.
- Each chart and graph exposes its title and description as the accessible name of the drawing.
- Series colors keep at least 3:1 contrast against both the light and the dark chart background, and every text color keeps at least 4.5:1. Heatmap cells carry a hairline so the palest ones stay distinguishable.
- No element takes a positive tab index. The toolbar status is a live region, legend buttons expose `aria-pressed`, the data toggle exposes `aria-expanded`, and the data table has a caption with scoped column headers.
- Keyboard: Tab reaches marks, legend, toolbar and graph nodes; Enter or Space selects a mark or node; Escape hides the tooltip; arrow keys nudge a focused graph node.

Confirmed in a browser accessibility tree: each mark appears as a named button, the chart appears as a named group, and both status regions announce. Every continuous-integration run also sweeps both sample hosts with axe-core, restricted to the WCAG 2.0 and 2.1 A and AA rules, and fails on any violation.

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

- Line, area, scatter, bubble, column, horizontal bar, signed stacked column, donut, heatmap, radar, candlestick, OHLC bar, uncertainty band, histogram, box plot, violin.
- Linear, base-10 logarithmic and UTC time axes on X and Y, and an optional second Y axis on the right; category labels on categorical charts. Time and log X axes apply to line, area, scatter, bubble, candlestick, OHLC and band charts; log Y applies to line, scatter, bubble, candlestick, OHLC, band, box and violin charts, because magnitude, count and radial charts need a zero baseline. Log axes reject zero and negative values. `XFormat`, `YFormat` and `Y2Format` write values as durations in seconds or as compact numbers on linear and log axes, never on a time axis; a linear duration axis steps by a second at the finest and rounds what it shows to the second, and histograms, donuts, heatmaps and radar charts take no format. `YReversed` and `Y2Reversed` apply to line, scatter, bubble, band, candlestick, OHLC, box and violin charts, and only Y axes reverse. Time values must be Unix milliseconds between year 1 and year 9999, and `TimeZone` decides the calendar they are read in. `SkipWeekends` and `TimeSkips` compress a time axis over spans it should not draw, at most 400 listed spans per chart; the axis stays piecewise proportional, so a gap in the data itself still reads as a gap. Irregular tick placement is not implemented.
- `MinorGridlines` adds lighter lines between the labelled ticks: four or five divisions per interval on a linear axis depending on its step, the mantissas between decades on a logarithmic one, and none on a time axis, because half of a month is not a boundary anyone reads. Off by default.
- Explicit limits via XMin/XMax/YMin/YMax. Bars and areas enforce a zero baseline. Null Y preserves gaps in lines/areas and is omitted elsewhere.
- Line/area min/max sampling preserves original indices and extrema per continuous run; this is not a total chart-wide point budget. CSV always exports original observations.
- Up to 100,000 input points, 32 series; 100 categories/slices. Sampling holds a line or area chart at its mark budget, so the browser cost is the same for 1,000 points as for 100,000: about 33 ms either way on the machine in [the measurements](docs/PERFORMANCE.md). Scatter and bubble render every point by default, which is comfortable to about 10,000; 50,000 points means 150,000 DOM elements and 12 MB of markup, and 100,000 means 300,000 elements and 25 MB. A scatter chart can set `DensityCells` to aggregate instead, which takes 100,000 points to 6,504 elements and 33 ms. Bubble has no equivalent, because binning would destroy the size encoding. There is no GPU acceleration and no million-point claim.
- Bubble area is proportional to Size across all series. Radar requires complete, nonnegative series on common categories. Donut accepts one nonnegative series.
- A trend line applies to line, area, scatter and bubble charts; category, radial and derived kinds refuse it. It is one least-squares line per series, fitted over every observation in the series rather than the zoomed window, and it is not an observation: it raises no point selection, appears in no CSV export and adds no row to the data table. Other fits — moving averages, polynomial, exponential regression — are not drawn; `Statistics.Rolling` computes a moving average a host can draw as a series of its own.
- A violin estimates its outline with a Gaussian kernel at Silverman's bandwidth, taking the smaller of the standard deviation and the interquartile range so one long tail cannot smooth the shape away. The estimate is drawn over the observed range and no further, so the outline claims no values the data never had, and it is computed in the space the axis draws in, so a logarithmic axis shapes the violin in logarithms. The widest point of each violin fills its column: widths are comparable within a chart but carry no units, and the quartile bar and median tick carry the numbers. A violin is an aggregate, like a histogram bin or a box: focusable and named, raising no point selection. A series with fewer than two observations, or with no spread, draws its quartile bar and median without an outline. The bandwidth is not configurable, and split or paired violins are not implemented.
- Candlestick and OHLC bar accept one series, and a histogram up to four. Candlestick and OHLC bar take the same input: all four prices with High highest and Low lowest, colored by direction rather than by series. An OHLC tick is half the width of a candle body, so the two drawings of one dataset stand in the same columns and can be compared; neither carries a volume pane. Band points need both bounds or neither. Histogram and box read observations from Y and ignore X. A histogram of several series bins them over one set of edges chosen from the pooled observations and stands their bars side by side; counts are raw, not normalised, so a larger series draws taller bars. A box series may instead carry a precomputed `Summary` and no points: it is drawn as given, claims no observation count, applies to box charts only, and its outliers count towards the 100,000-point limit. Histogram bins and box glyphs are labelled, focusable aggregates that report no observation index, so they raise no point selection; candlesticks, OHLC bars and box outliers computed from observations do, and the outliers of a supplied summary do not.
- Layered graphs use longest-path levels, then barycenter sweeps that keep the ordering with the fewest crossings found. This is a heuristic, not minimal crossings. Edges spanning several levels bend once per level and are drawn as smooth curves; there is no orthogonal routing, no force simulation and no automatic node overlap removal. Self-loops are allowed in layered graphs and draw as a loop on their node; longer cycles still need the circular layout. Nodes can be dragged or nudged with the arrow keys in the component, which needs an interactive render mode. At most 250 nodes / 2,000 edges; dense graphs can still overlap.
- Narrow screens use a keyboard-focusable, horizontally scrollable chart viewport to preserve label readability.
- HTML tooltips on hover and keyboard focus in the component, native SVG tooltips in exported and server-rendered charts, keyboard-focusable data marks, point selection, tables, and accessible labels. See [Accessibility](#accessibility) for what is measured and what is not. This is not a claim of WCAG certification.
- SVG, PNG and CSV exports. PNG is rasterized in the browser from the same SVG, so it needs an interactive render mode; there is no server-side PNG or PDF rendering, 3D, or streaming transport yet.
- Research materials are excluded from packages. No vendor source code or book images are redistributed.

See [research and architecture](docs/RESEARCH.md), [verification](docs/VERIFICATION.md) and [measured performance](docs/PERFORMANCE.md). This is an original preview implementation, not a claim of feature or performance parity with mature commercial products.

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
