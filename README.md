# Lumen Charts

A standalone C# chart library, Blazor components, ASP.NET Core rendering API, and an interactive gallery. Preview 0.6.1. No third-party charting engine or CDN is required.

## Run the gallery

Requires the .NET 10 SDK (the reusable packages target .NET 8).

```powershell
dotnet run --project samples/Lumen.Gallery --urls http://localhost:5188
```

Open http://localhost:5188. The gallery includes chart selection, light/dark themes, refreshed sample data, series filtering, point selection, a numeric / time / log axis switch, X zoom/pan/reset, original-data tables, SVG/PNG/CSV downloads, and network layouts with draggable nodes.

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

`TimeAxis.Value(DateTimeOffset)` and `TimeAxis.Moment(double)` convert between moments and axis values. Time ticks fall on calendar boundaries — seconds, minutes, hours, days, fortnights, months or years — and are formatted in UTC with the invariant culture; local time zones are not applied. Log ticks are decades, subdivided at 2 and 5 across one or two decades. `Axis.Create`, `Axis.Map`, `Axis.Invert`, `Axis.Ticks` and `Axis.Format` are public if you need the geometry without SVG. CSV exports of a time chart add an `XTime` column with ISO 8601 UTC timestamps beside the numeric X column.

### Statistical and financial families

| Kind | Input | Rendering |
|---|---|---|
| `Candlestick` | One series; every point carries `Open`, `High`, `Low`, `Close` — use `ChartPoint.Candle` | Wick across the low-high range, body from open to close, colored by direction (`ChartSvg.RisingColor` and `FallingColor`) |
| `Band` | `Y` with `Low` and `High` bounds — use `ChartPoint.Interval` | Filled interval behind the central line; points without bounds break the band into runs |
| `Histogram` | One series of raw observations in `Y`; `X` is ignored | Equal-width bins over a zero baseline. `Bins` sets the count; otherwise Freedman–Diaconis chooses it, falling back to Sturges when the interquartile range is zero |
| `Box` | One series per distribution, raw observations in `Y`; `X` is ignored | Quartile box, Tukey whiskers at 1.5 interquartile ranges, and outliers as circles |

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
```

`Statistics.Quantile`, `Statistics.Summarize` and `Statistics.Bins` are public, so the same numbers are available without rendering. Quantiles interpolate linearly between order statistics, matching NumPy's default and Excel's `PERCENTILE.INC`. CSV exports add `Open,High,Low,Close` for candlestick charts and `Low,High` for band charts.

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
```

Invalid chart semantics return HTTP 400 problem details. Malformed JSON is rejected by ASP.NET Core. The endpoints do not fetch URLs, execute supplied code, save submitted data, or contact outside services. Add application-specific authorization and rate limits when hosting publicly. The sample limits request bodies to 16 MiB.

## Accessibility

Measured by the regression suite, so a change that breaks one of these fails the build:

- Every data mark is a focusable element with an accessible name carrying its series, category and value — `Workspace: Sep, 60.3`. Interactive marks use `role="button"`; histogram bins and box glyphs, which are aggregates, use `role="img"`.
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

- Line, area, scatter, bubble, column, horizontal bar, signed stacked column, donut, heatmap, radar, candlestick, uncertainty band, histogram, box plot.
- Linear, base-10 logarithmic and UTC time axes; category labels on categorical charts. Time and log X axes apply to line, area, scatter and bubble charts; log Y applies to line, scatter and bubble charts, because magnitude and radial charts need a zero baseline. Log axes reject zero and negative values. Time values must be Unix milliseconds between year 1 and year 9999. Time zones, business calendars and irregular tick placement are not implemented.
- Explicit limits via XMin/XMax/YMin/YMax. Bars and areas enforce a zero baseline. Null Y preserves gaps in lines/areas and is omitted elsewhere.
- Line/area min/max sampling preserves original indices and extrema per continuous run; this is not a total chart-wide point budget. CSV always exports original observations.
- Up to 100,000 input points, 32 series; 100 categories/slices. SVG scatter/bubble renders every point. Large interactive datasets require profiling; there is no GPU acceleration or million-point claim.
- Bubble area is proportional to Size across all series. Radar requires complete, nonnegative series on common categories. Donut accepts one nonnegative series.
- Candlestick and histogram accept one series. Candlestick requires all four prices with High highest and Low lowest, and colors bodies by direction rather than by series. Band points need both bounds or neither. Histogram and box read observations from Y and ignore X; box computes its own quartiles, so precomputed five-number summaries are not accepted yet. Histogram bins and box glyphs are labelled, focusable aggregates that report no observation index, so they raise no point selection; candlesticks and box outliers do.
- Layered graphs use longest-path levels, then barycenter sweeps that keep the ordering with the fewest crossings found. This is a heuristic, not minimal crossings. Edges spanning several levels bend once per level and are drawn as smooth curves; there is no orthogonal routing, no force simulation and no automatic node overlap removal. Self-loops are allowed in layered graphs and draw as a loop on their node; longer cycles still need the circular layout. Nodes can be dragged or nudged with the arrow keys in the component, which needs an interactive render mode. At most 250 nodes / 2,000 edges; dense graphs can still overlap.
- Narrow screens use a keyboard-focusable, horizontally scrollable chart viewport to preserve label readability.
- HTML tooltips on hover and keyboard focus in the component, native SVG tooltips in exported and server-rendered charts, keyboard-focusable data marks, point selection, tables, and accessible labels. See [Accessibility](#accessibility) for what is measured and what is not. This is not a claim of WCAG certification.
- SVG, PNG and CSV exports. PNG is rasterized in the browser from the same SVG, so it needs an interactive render mode; there is no server-side PNG or PDF rendering, 3D, annotations, or streaming transport yet.
- Research materials are excluded from packages. No vendor source code or book images are redistributed.

See [research and architecture](docs/RESEARCH.md) and [verification](docs/VERIFICATION.md). This is an original preview implementation, not a claim of feature or performance parity with mature commercial products.

## 0.6.1 fixes

An axe-core sweep of both sample hosts now runs on every continuous-integration run. Its first pass reported 62 serious colour-contrast failures in the gallery's own chrome and 4 on the WebAssembly page, all from sample styling rather than the library: muted text at 3.7:1, section labels at 3.2:1, accent links at 3.9:1, a caption dimmed further by a container opacity, and a bare `button` rule on the WebAssembly page that restyled the component's own legend. The sample palettes were darkened and that rule scoped; both hosts now report no WCAG A or AA violation.


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
