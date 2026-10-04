# Lumen Charts

A standalone C# chart library, Blazor components, ASP.NET Core rendering API, and an interactive gallery. Preview 0.33.0. No third-party charting engine or CDN is required.

## Run the gallery

Requires the .NET 10 SDK (the reusable packages target .NET 8).

```powershell
dotnet run --project samples/Lumen.Gallery --urls http://localhost:5188
```

Open http://localhost:5188. The gallery includes chart selection, light/dark themes, refreshed sample data, series filtering, point selection, a Midnight brand beside the Lumen, Harbour and page-CSS ones, a numeric / time / log axis switch with a power–duration curve and a reversed pace line on duration axes, a heart-rate stream coloured and shaded by zone with the time it spent in each zone, a performance management chart of fitness, fatigue and form over daily training stress with two planned weeks projected, weekly load against a target range, an activity stream of heart rate, pace and climb in three panes over one elapsed-time axis, the heart rate coloured by its value and the climb a smooth faded area, candlestick and OHLC charts with a five-day average and volume in a pane beneath, X zoom/pan/reset, original-data tables, SVG/PNG/CSV downloads, a recovery gauge in WHOOP-like tiers, activity rings with one past its goal, a night's sleep stages as a state timeline, a fortnight of daily heart-rate ranges as floating bars, sixteen weeks of training as a calendar of days in tiers, a threshold workout as blocks in Coggan's power levels with the ride's power over it, a load test's throughput and latency with a quadratic and an exponential fit, and network layouts with draggable nodes that fit a phone, turning the layered one top to bottom. Every chart draws in the [refined finish](#the-refined-finish), and the chart explorer sets [`FitWidth`](#fitting-the-width-it-is-shown-at), so on a phone it fills the screen rather than scrolling sideways.

http://localhost:5188/sports is the **Sports & performance** page: every training chart on one dashboard, drawn from one simulated athlete so the charts agree with one another — this morning's readiness on a gauge and the day's training as rings, the performance management chart, weekly load against a target, weekly zone distribution, a training calendar of the season's daily stress beside this month's runs, the next session as the workout whose stress the performance chart projects, a three-pane activity stream, time in zone, pace by kilometre, the run's laps as blocks as wide as they are long, elevation coloured by grade, the power–duration curve with its critical-power fit, a personal-best progression and overnight HRV against its baseline, each night coloured and named by its status, with its seven-night average, a Racing section of five invented races, the place each finished coloured by its change and written with its field size over the points each earned, an invented season's arc through its fields by discipline and an invented race's gaps to the leader, each rider named at the end of their line, and a Sleep and recovery section of last night's sleep stages, two weeks of sleep timing and each day's heart-rate range. Each card names the `Training` call or option that draws it, every chart sets [`FitWidth`](#fitting-the-width-it-is-shown-at) so it fills its card on a desktop and on a phone, and the page follows the theme and brand switches.

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

It uses component selectors, so the same checks, the axe sweep included, run against the gallery and against the WebAssembly host on port 5199. Some depend on the host. The brand check runs where the page wraps its charts in `LumenBrand`; a second axe sweep runs with the dark theme on where the page has a theme button, which the suite finds by its name and presses as a user would; where the first chart hides its line markers, as the refined finish does, the suite checks that hovering a point shows its marker and its tooltip and that reaching it with Tab shows its marker with the focus ring; where the page offers an activity stream behind buttons named for the line chart and the stream, the suite opens it, checks that zooming and panning move its three panes together, that a hidden marker still takes focus, draws its focus ring and reads its value, and that a PNG export keeps the gradient its heart-rate line is coloured with, and sweeps it with axe; where the page offers a Midnight brand, a fourth sweep runs with it on; where it offers gauge and ring charts behind buttons named for them, the suite checks that a score and a ring take focus with their focus ring, show their tooltip and raise selection, with no zoom to offer; where it offers timeline and range charts behind buttons named for them, the suite checks that a hypnogram span and a range bar take focus with the focus ring, show their tooltip and raise selection, and that zooming the timeline draws a span twice as wide; where it offers a calendar behind a button named for it, the suite checks that a day takes focus with the focus ring, shows its tooltip and raises selection, which the status line reads as the day's date and total, that a rest day is an empty cell outside every mark, and that there is no zoom to offer; where it offers blocks behind a button named for them, the suite checks that a block takes focus with the focus ring, shows its tooltip and raises selection, which the status line reads with the block's span and height, and that zooming draws a block twice as wide; where its chart explorer sets `FitWidth`, the suite checks that on a 375-pixel phone neither the explorer nor the page scrolls sideways; where its network graph sets `FitWidth`, it checks that a dragged node keeps its place in proportion when the graph's box narrows, that a box narrow enough to turn the graph top to bottom lets the node go and says so in the status line, and that on a 375-pixel phone, emulated with touch, overlay scrollbars and a device scale of 2, neither the page nor the graph scrolls sideways in either layout, no two of the graph's labels overlap, and no edge, walked a pixel at a time, runs through a node's label as the browser lays it out, nor any edge label lies on one; where it links a Sports & performance page, the suite opens it, checks that its twenty-four charts, three of them sparklines, are live and drawn at the width they are shown, that a hovered mark shows its tooltip, that axe finds nothing in the light, dark and Midnight looks, and that the page fits a 375-pixel phone, where the page has a Getting faster? card, that its three sparklines are drawn at their own sizes with no words or controls inside them, each personal best named so, and that a mark's tooltip stands above the drawing at the width its words need, inside the screen, on a desktop and on a 375-pixel phone, and where the page has race results, that on a 375-pixel phone, emulated with touch, overlay scrollbars and a device scale of 2, none of their value labels runs outside the drawing and each place reads its field size and its change in its name and its tooltip, and where the page has How the field finished, that on the same phone no word of that card, and no chart's title, description or source on the page, runs outside its drawing as the browser lays it out; where a page draws a chart without its legend and toolbar, as the WebAssembly host's first page and the gallery's Gap to the leader card do, that neither is drawn, that the status line, out of sight, reads the shared readout the arrow keys move, that the readout reads every line with its unit, and that on a 375-pixel phone and at 1,280 pixels every end label stands whole inside the drawing with none overlapping another; and on the first chart that sets `FitWidth`, on the first page or the Sports & performance page, it checks that the chart is drawn at the width of its container, in a container narrowed to 480 pixels and on a 375-pixel phone, with its title the same size each time, and that zoom and the SVG and PNG exports work on it at that width. A host without one says SKIP rather than failing: the gallery skips the brand check, so it runs forty-eight, and the WebAssembly page the dark sweep, the activity stream, Midnight, the gauge and rings, the timeline and range bars, the calendar, the blocks, the explorer, the fitted graph and the Sports & performance page, so it runs twenty. It stays outside the solution so the ordinary build needs no browser download.

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

Under the drawing the component writes a legend of toggle buttons and a toolbar: zoom, pan and reset where the chart zooms, the SVG, PNG and CSV exports, the button that shows the data table, and a status line. From 0.38.0, `ShowLegend="false"` leaves out the legend, for a chart whose series are named in its drawing already, by [end labels](#season-arcs-and-gaps-ticks-set-by-hand-units-and-end-labels) or pane headers, and `ShowToolbar="false"` leaves out the buttons, for a small phone card. Both are on by default, and with both on the markup is as before. Without its toolbar the chart keeps its status line in the page, out of sight, so a screen reader still hears the shared readout and each point chosen, and dragging across the plots and the arrow keys still work; but the keyboard loses its way to zoom, pan and reset, and the only way to the data table, so keep the toolbar where those matter. Without the legend a reader cannot hide a series.

A spec passed as a parameter into an `InteractiveServer` component travels in SignalR's circuit-start message. A large one, six channels of 600 points say, can go past SignalR's default 32 KB `MaximumReceiveMessageSize`, and the circuit then closes with only a console error, leaving a static chart without its readout or zoom. Raise the limit, `builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddHubOptions(o => o.MaximumReceiveMessageSize = 256 * 1024);`, or let the interactive component load its own data, passing it an ID rather than the spec.

### Fitting the width it is shown at

A chart is drawn at `ChartSpec.Width`, 900 by default, and scales to its container. Scaled down, its text shrinks with it, so below 640 pixels the stylesheet stops shrinking it and lets it scroll sideways in its own box. `FitWidth` draws it at the width its container gives it instead, on a phone, in a narrow card or in a sidebar, so it fills the box with its text at its own size:

```razor
<LumenChart Spec="chart" FitWidth="true" />
```

The component measures its box once it is interactive, again whenever the box settles at a new width, and redraws with `Width` replaced by that width in whole pixels, never below 320, the narrowest a chart accepts. A `lumen-fit` class on the chart's root lifts the 640-pixel minimum for that chart alone. Zoom, pan, hidden series, the data table and point selection carry on across a redraw, and the SVG and PNG exports take the width the chart is drawn at. `Height` stays as it is, so a fitted chart on a phone is taller than it is wide; pick a height that reads well at both. Nothing measures a chart that is not interactive, so a prerendered or statically rendered chart is drawn at `Spec.Width` and scaled to fit until the browser takes over; for static rendering, or for SVG from `ChartSvg.Render`, set `Width` to the width the chart will be shown at. A chart that leaves `FitWidth` off renders exactly as before.

`LumenGraph` takes `FitWidth` too (0.30.0). The graph is drawn as `GraphEngine.Fit` lays it out for the width its box reports, described under [Graphs](#graphs): at that width, a layered graph whose levels cannot stand side by side turned top to bottom and as tall as its rows need, and a circular one taller until its neighbours stand apart. A node the reader drags is held as a fraction of the drawing's width and height, so it keeps its place in proportion when the box changes, and goes back to the layout, with a word in the status line, when the graph turns between left to right and top to bottom, since its place described the other layout. A graph whose fullest level needs more than the box is drawn at the width it needs and scrolls, as every graph did before.

### Keys and the shared readout

From 0.36.0 each `<LumenChart>` has two tab stops: its scrollable viewport, a `role=region` that Tab reaches first, and one roving stop among its marks. Its script leaves one mark reachable by Tab — the first point of the first series, or the one last focused — and the arrow keys move from it: Left and Right along a series, stepping over its gaps, Up and Down to the nearest point at that X in the series before or after it, Home and End to the series' ends, and Page Up and Page Down ten points; Enter or Space selects, and Escape hides the tooltip. The keys are named in hidden words that the script makes the chart's description. The SVG itself keeps every mark at `tabindex='0'`, so a static page, with no script to move between marks, stays readable mark by mark.

`ChartSpec.SharedReadout` reads every series at once at one X:

```csharp
var form = new ChartSpec {
    Title = "Fitness and form", Kind = ChartKind.Line, XAxis = AxisKind.Time, SharedReadout = true,
    Panes = [new() { Label = "Form", YSymmetric = 10, YFormat = ValueFormat.Signed }],
    Series = [ChartSeries.From("Fitness", load, d => Day(d.Day), d => d.Fitness),
        ChartSeries.From("Fatigue", load, d => Day(d.Day), d => d.Fatigue),
        ChartSeries.From("Form", load, d => Day(d.Day), d => d.Form) with { Pane = 1 }]
};
```

In the component a vertical guide runs through every pane at the X nearest the pointer, a tap or the focused point, in the muted text colour; each shown series' point there is ringed, a dot in its colour outlined in the text colour; and one tooltip, beside the guide, reads the X and then each series in legend order with its value's note, zone and change words, `3 Jun 2026`, `Fitness 52.3`, `Fatigue 61`, `Form −8.7`. A series is read where it has a point within half the closest spacing of the chart's X values, a missing value reading `missing`, and blocks at every X they cover. The arrow keys then step the readout by X — Left and Right, Home and End, Page Up and Page Down ten — Up and Down move between the series at that X, Escape hides it, and the status line reads the same words, `3 Jun 2026 · Fitness 52.3 · Fatigue 61 · Form −8.7`, a moment after the keys stop. The readout is worked out once for each drawing and handed to the script, so moving the pointer asks nothing of the server. It never changes the SVG: `ChartSvg.Render` draws the same chart with it or without it, and it is never part of the hash that names gradients. `ChartSvg.Readout(spec)` returns what it reads — each X with its position in the drawing's units, its label and each series' entry, position and words — for a host that draws its own over a static SVG. Line, area, scatter, bubble, band, range, candlestick, OHLC and blocks charts take it; a sparkline and the kinds without a continuous X axis refuse it. In JSON: `"sharedReadout":true`, which the HTTP API's SVG ignores.

From 0.37.0 a line or an area is read at the points it draws: a long one thinned to `MaxRenderedPoints` over the X range shown, as `Sampling` thins it, so a four-hour ride at one sample a second reads at most one X for each mark drawn rather than 14,400, and where it reads averages its column says so once, `1:02:30 · average of 12 s` (the slice's width on a duration or time axis, its points on another), each entry reading its average alone, `Heart rate 152`; a slice a gap cuts short reads `average of 3 points`.

Testing the keys: a `KeyboardEvent` built and dispatched from script can move focus without bringing up the readout and the status line as a real key press does, as an app's own tests found. Test with real input, such as Playwright's `keyboard.press`.

### Text that fits

A chart's title, description and source are written from its left edge, 24 units in, and from 0.35.0 each fits the drawing's width less 48, by the library's generous estimate of a text's width, so a description written for a desktop no longer runs off a 340-pixel card rendered on the server:

- A **description** too wide for one line goes on over a second, broken between its ` · ` clauses where both lines then fit and otherwise between the words that set the two lines most nearly equal, so no word is left alone on the second; the plot moves down 14 units to make room. Past two lines the first takes as many words as fit and the second ends in `…`.
- A **source** wraps the same way, growing upward from the foot: its second line moves the plot's bottom, the X axis and its title up 14 units.
- A **title** stays one line, cut at a word with `…` where it is wider than the drawing.

The whole of each stays in the drawing's `<title>`, `<desc>` and accessible name, so nothing is lost to a screen reader or a tooltip. Text that fits is drawn exactly as before. A chart is at least 240 units tall, a sparkline at least 60 by 16, and keeps its `Height` at any width, so choose one that reads at a phone's width; a strip is drawn as tall as its content, and from 0.41.0 a bar chart with `FitHeight` as tall as its rows. A graph keeps its own description, which goes on over a second line between its clauses as it did, and its title is cut the same way.

The root `<svg>` carries its own layout and font in its `style` attribute: its width rule (`width:100%`, or a sparkline's own width), `height:auto`, `display:block`, the background, the text colour and the font family. A host must not add a `style` attribute of its own, which makes the SVG invalid XML and which an HTML page ignores, or replace this one, which drops the chart's background, colours and font. To size, place or frame a chart, wrap the SVG in an element and style the wrapper:

```razor
<div class="result-card__chart">@((MarkupString)ChartSvg.Render(spec, includeLegend: false))</div>
```

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

What labels X along the bottom is `XTicks` (0.33.0), a `TickSource`. `Auto`, the default, keeps the rule charts always had — when 1 to 24 points with a `Label` stand in the visible range, their labels take the ticks' place, each cut to twelve characters — with one exception: a time axis writes its own dates, so there the labels take their place only when none would be cut, and longer names, `Round 1 · Hilltop Classic`, stay in the points' tooltips and accessible names under date ticks. `Axis` always draws the axis's own ticks; `PointLabels` always the labels, at any count, thinned until no two touch, and the ticks only where no labelled point is in view. A block's label names its block and never labels the axis. Line, area, scatter, bubble, candlestick, OHLC, band, range and blocks charts take it; a category chart, which labels every category, a timeline, a calendar and the kinds without an X axis refuse it set. Points placed by index — races, rounds, weeks — with X 0, 1, 2… and a `Label` each read as categories on a line, and `XMin = -0.5`, `XMax = count - 0.5` stands each in the middle of its slot, clear of the plot's edges.

Which of those labels are written is `XTickLabels` (0.35.0), a `TickLabels`, as `YTickLabels` is for the Y axis. `All`, the default, writes every one; `Ends` the first and the last drawn, ticks or points' labels; and `Bounds`, new in 0.35.0 for either axis, writes no tick's label but the axis's own two ends, at their exact values in its format — `XMin` and `XMax`, or the data's ends, along X, and the ends of the axis as fitted or bounded up the side — whether or not a tick stands there. Along the bottom the first end's label starts at the plot's left edge and the last's ends at its right, so both stand whole under the plot. A histogram of finish times labels its first bin's start and its last bin's end, `35:00` and `1:20:00`, and with `IncludeZero` its Y axis `0` and the most in a bin. The gridlines stay where they are; only the labels change. `XTickLabels` applies to the continuous kinds and timelines and is refused elsewhere; `Bounds` is refused beside `XTicks = TickSource.PointLabels`, which asks for the points' labels instead.

Log ticks are decades, subdivided at 2 and 5 across one or two decades. `Axis.Create`, `Axis.Map`, `Axis.Invert`, `Axis.Ticks` and `Axis.Format` are public if you need the geometry without SVG. CSV exports of a time chart add an `XTime` column with ISO 8601 UTC timestamps beside the numeric X column.

#### Durations, compact numbers and reversed axes

`XFormat`, `YFormat` and `Y2Format` choose how an axis writes its values, whatever its kind: `ValueFormat.Number` (the default), `ValueFormat.Duration`, which reads values as seconds, `ValueFormat.Compact`, which writes 1.2k, 3.4M and 1.5B, `ValueFormat.TimeOfDay`, which reads seconds since a midnight as the clock, described under [Timelines, range bars and the time of day](#timelines-range-bars-and-the-time-of-day), or `ValueFormat.Signed` (0.36.0), which writes a number as `Number` does with its sign written out: `+5`, `−5` with a true minus sign (U+2212), and `0` for zero, negative zero included. `YReversed` and `Y2Reversed` put the smallest value at the top, so a faster pace — a smaller number — sits higher:

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

Tooltips, accessible names, annotation labels, the static SVG, and the component's data table and status line all read in the axis's format; CSV keeps the raw numbers, seconds included. A time axis writes its own calendar and refuses every format, and a logarithmic one refuses the time of day. An X format applies where X is a value — line, area, scatter, bubble, candlestick, OHLC, band, range and timeline charts — and a Y format wherever a Y axis measures values; donut, heatmap and radar charts have no such axis, and a histogram's counts observations, so they refuse one.

Reversal is a property of `Axis` that `Map` and `Invert` honour, so gridlines, ticks, marks, annotations, trend lines and zoom follow it without any of them knowing. A trend line still names the direction of the data: on a reversed pace axis, a line climbing the screen is a pace that is falling. It applies to line, scatter, bubble, band, range, candlestick, OHLC, box and violin charts. Column, bar, stacked column, area and histogram charts refuse it, because they draw from a zero baseline and a reversed one would hang their bars from the top.

### Trend lines

A series can carry a least-squares line:

```csharp
ChartSpec relationship = new() {
    Kind = ChartKind.Scatter,
    Series = [new("Accounts", observations) { Trend = true }]
};
```

The line is fitted in the space the chart draws in, which is what keeps it straight on screen: a logarithmic axis has already taken the logarithm, and a trading axis has already left out the spans it skips. On plain axes this is the ordinary least-squares fit, because the scaling between data and pixels does not change it. It is drawn dashed in the series colour, reports its direction and R squared to a pointer and to assistive technology, and is left out when a series has fewer than two observations or no spread in X. `Statistics.Fit` returns `Slope`, `Intercept`, `R2` and `Count` if you want the numbers rather than the line.

`TrendFit` chooses another family, still with `Trend = true`:

```csharp
ChartSeries[] trended = [
    new("HRV", nights) { Trend = true, TrendFit = TrendFit.MovingAverage },           // 7 points; TrendPoints = 14 for two weeks
    new("Throughput", throughput) { Trend = true, TrendFit = TrendFit.Polynomial },   // a quadratic; TrendDegree = 3 or 4
    new("Latency", latency) { Trend = true, TrendFit = TrendFit.Exponential, Secondary = true }];
```

Every family is computed in the space the chart draws, as the line is, and keeps the line's look: dashed, three quarters of the series' stroke, named for assistive technology and with a native tooltip.

- **A moving average** is a trailing window of `TrendPoints` of the series' points, 7 unless set, from 2 to 1,000, in the order the series lists them, which must be X order. Each window's average is drawn at its last point. A point with a missing value holds its place in the window and adds nothing, as `Statistics.Rolling` has it, and the line breaks wherever fewer than half the window, rounded up, is present, so a seven-day average needs four days of the seven. It averages the positions the values are drawn at, so on a logarithmic axis it is the geometric mean. A long run is thinned to `MaxRenderedPoints` as a line is. It reads `HRV trend: 7-point moving average`.
- **A polynomial** of `TrendDegree` 2, 3 or 4 is fitted by least squares to the drawn positions, X centred and scaled to run from −1 to 1 before it is solved. A polynomial stays a polynomial of its degree under the scaling between data and pixels, so on plain axes it is the ordinary fit to the data; on a trading axis it fits the trading days as they stand side by side. It is drawn every 2 pixels or so across the X its observations cover and no further, and reads `Throughput trend: quadratic fit, R squared 0.99` (cubic and quartic likewise).
- **An exponential**, `y = a·e^(b·x)`, is fitted to the natural logarithm of each positive value against the X it is drawn at, leaving out zero and negative values, and drawn through the series' own axis across the X its positive observations cover: straight on a logarithmic axis, curving on a linear one. Its R squared is measured on the logarithms, as Excel reports an exponential trendline's, not on the values themselves. Fewer than two positive values draw none. It reads `Latency trend: exponential fit, rising, R squared 0.99`.

`TrendFit`, `TrendPoints` and `TrendDegree` are refused without `Trend = true`, which they would otherwise silently do nothing without, and outside their ranges; a window is refused on any fit but a moving average and a degree on any but a polynomial. The same marks take them as take the line. For the numbers without the drawing, `Statistics.Polynomial(points, degree)` returns `PolynomialFit(Coefficients, R2, Count)`, its coefficients from the constant term upward in your own X, and `Statistics.Exponential(points)` returns `ExponentialFit(A, B, R2, Count)`; each has `Predict(x)` and returns null where no fit exists. Both keep their precision with X in Unix milliseconds: `Predict` evaluates the fit about the middle of the observations, where it was solved, even where the coefficients in your own X cancel in all but their last digits, or `A` is too small for a double and reads 0. The JSON of the HTTP API writes the fit as a string, `"trendFit":"MovingAverage"`, beside `"trendPoints"` and `"trendDegree"`.

### Race results on a line

A finishing place is better when it is smaller, points when they are larger, and a reader of a season wants each race's number and whether it beat the one before. Three settings draw that on a line, written for a race-results app's charts and general to any up-and-down series — stock days, paces, rankings:

```csharp
ChartSpec races = new() {
    Kind = ChartKind.Line, YReversed = true, XMin = -0.5, XMax = 4.5, YLabel = "Position",
    Panes = [new ChartPane { Label = "Points", Weight = 1 }],
    Series = [
        new("Position", [new(0, 31, "11-04-2026") { ValueNote = "/50" }, new(1, 24, "16-05-2026") { ValueNote = "/48" }, new(2, 27, "04-07-2026") { ValueNote = "/51" },
                         new(3, 21, "08-08-2026") { ValueNote = "/49" }, new(4, 19, "19-09-2026") { ValueNote = "/52" }]) {
            ChangeColors = ChangeColors.LowerIsBetter, ValueLabels = true, Markers = MarkerStyle.Filled },
        new("Points", [new(0, 40, "11-04-2026"), new(1, 52, "16-05-2026"), new(2, 47, "04-07-2026"), new(3, 58, "08-08-2026"), new(4, 61, "19-09-2026")]) {
            Pane = 1, ValueLabels = true, Markers = MarkerStyle.Filled }]
};
```

- **`ChartSeries.ChangeColors`** — `None`, `HigherIsBetter` or `LowerIsBetter` — compares each point with the nearest earlier point that has a value and colours it in the style's `Rising` colour when it is better, `Falling` when it is worse, and the series colour when it is level or has nothing before it. Better is what the setting says, not up the screen, so a place gained reads the same on a reversed axis and on a scale shared with the points. A point's marker and the segment that arrives at it take its colour — `ChartPoint.Color` colours the segment that leaves a point; the two answer different questions — and a step or a smooth curve arrives the same way. A gap draws no segment, but the point after it still compares with the last value before it. The colour is never the only cue: each mark's tooltip and accessible name end `, better than the previous`, `, worse than the previous` or `, level with the previous`, and the first ends with neither. It applies to series drawn as lines and scatter points, in X order, and is refused on other marks, beside `Zones`, a `Gradient` or point colours, which colour the same marks, and on a density scatter.
- **`ValueLabels`** now writes a line's or a scatter series' values too: each 4 pixels above its marker, in the axis's format and the point's colour — its change colour where it has one, its zone's, its own or, on a gradient, the gradient's colour at its value — where that colour clears 4.5:1 against the chart's background, as small text must, and in the style's text colour where it does not, at 11 px and weight 600, its note in the muted colour, which `ContrastIssues` already holds to 4.5:1, over a copy of itself stroked 3 px wide in the background colour so a line running through it does not cross the figures. It moves in from the plot's left and right edges so it is never cut, goes 4 pixels below its marker where above would leave the plot or meet a value label written before it, columns' and bars' included, and is left out where neither place is free, as a column's label is left out where it does not fit; its value stays in its mark's name. Columns' and bars' labels are unchanged.
- **`ChartPoint.ValueNote`**, at most 20 characters, is written straight after the value — give it its own space where it wants one — in the muted colour at normal weight after a value label, and after the value in the mark's tooltip and accessible name (`Position: 16-05-2026, 24/48, better than the previous`), the component's data table and status line, and a `Note` column that CSV adds only when some point has a note, so existing exports are unchanged. A note needs no `ValueLabels` to reach the name, table and CSV. A missing value has nothing for a note to follow, so its note reaches the CSV alone. Columns, bars, blocks, donut slices, heatmap cells and radar points carry notes too; candles, range bars, histograms, boxes, violins, timelines, calendars, gauges and rings, whose marks read several values or none, refuse them.

On the Sports & performance page the HRV chart draws every night as one series, coloured by where it falls against its baseline with `ChartPoint.Color` and named with it by a note, `62 inside baseline`, so that its seven-night moving average could join it; and the Racing section draws the five races above as two panes. The skill's `references/recipes-race-face.md` has the faithful single-scale version, the season on a time axis and a dark style built from design tokens. In JSON: `"changeColors":"LowerIsBetter"` on a series, `"valueNote":"/48"` on a point and `"xTicks":"Axis"` on the chart; each is left out of the hash that names gradients at its default, so no gradient ID moves.

### Sparklines

A sparkline is a chart the size of a word, read beside the words that give its numbers: a run of finish times getting faster, a weight logged over a year. Three settings draw one, each general to any app:

```csharp
double[] times = [1450, 1432, 1445, 1411, 1420, 1367];   // an invented run of 5 km races, oldest first, in seconds
bool Best(int i) => i > 0 && times.Take(i).All(before => times[i] < before);
ChartSpec fiveK = new() {
    Title = "5 km: 24:10 to 22:47 over 6 races", Description = "Each race's time, oldest first, faster higher",
    Kind = ChartKind.Line, Width = 120, Height = 32, Sparkline = true, YReversed = true, YFormat = ValueFormat.Duration,
    Series = [new("5 km", times.Select((t, i) => new ChartPoint(i, t, $"Race {i + 1}") { Highlight = Best(i) ? "#DD4B45" : null, ValueNote = Best(i) ? " · PB" : null }).ToArray(), "#848484")]
};
ChartSpec weight = new() {
    Title = "Weight: 4 measurements, from 37.9 to 38.1 kg", Description = "Scale 34 to 42 kg",
    Kind = ChartKind.Line, Width = 270, Height = 54, Sparkline = true, YMinSpan = 8,
    Series = [new("Weight", new[] { 37.9, 37.8, 38.2, 38.1 }.Select((kg, i) => new ChartPoint(i, kg) { ValueNote = " kg" }).ToArray(), "#848484")]
};
```

- **`ChartSpec.Sparkline`** draws the data alone. No title, description or source is written; there are no axes, ticks, gridlines or legend; zone bands and annotations keep their shapes and lose their labels — the drawing holds no `<text>` at all. The plot fills the drawing but for a padding just wide enough for its largest mark at any edge: 6.5 units for a highlight's ring, 10 for `HighlightLast`'s, 5 for a hollow marker, 4.5 for a scatter dot, 4 for a filled marker or one a line shows on hover, half the stroke for a line with no markers, nothing for columns; a drawing too small to hold that on both sides keeps two units of plot and cuts a ring at its edge. The title stays the drawing's accessible name and its `<title>`, the description its `<desc>`, and every point keeps its focusable mark, named as on any chart and carrying its native tooltip, so a static SVG reads point by point with no script. It is shown at its own width, `width:120px;max-width:100%`, as a word is, rather than at the width of its box. A sparkline may be as small as 60 by 16, and only a sparkline: every other chart keeps 320 by 240. Line, area, scatter and column charts take it, their series drawn as those marks; other kinds, other marks, panes and value labels are refused, each with its reason. In `<LumenChart>` a sparkline is its drawing alone with its tooltips — no legend, toolbar, zoom, data table or scrolling region — and a tooltip stands just above the drawing, as wide as its words, rather than over the line; `FitWidth` fits it down to 60 pixels.
- **`ChartPoint.Highlight`** rings one point of a line or scatter series with an enlarged marker in that colour, radius 5.5, outlined 2 pixels in the background colour, whatever the series' markers, on a sparkline or a full chart. It colours that marker alone: the line keeps its colour and its course, sampling keeps the point, and on the latest point `HighlightLast`'s ring takes the colour. A ring speaks only to those who see it, so pair it with a `ValueNote`, `" · PB"`, which the point's tooltip and accessible name read after its value: `5 km: Race 2, 23:52 · PB`. Areas and every other mark refuse it, as does a density scatter.
- **`ChartSpec.YMinSpan`**, and `ChartPane.YMinSpan` for a pane, is the least the left-hand Y axis spans, centred on its data: where the data's range is smaller, the axis runs from the data's middle less half the span to its middle plus half — 37.8 to 38.2 kg about 38.0 with a span of 8 runs from 34 to 42 — so a few hundred grams read as steady rather than filling the plot; data as wide as the span or wider is fitted as before. A band's or a range's bounds count as its data, and a series on the right-hand axis does not. It reverses with the axis, and is refused beside `YMin` or `YMax`, on a logarithmic axis, with `IncludeZero`, on the kinds drawn from zero and on an axis that carries columns or an area, and on kinds without such an axis.
- **`ChartSpec.YSymmetric`** (0.36.0), and `ChartPane.YSymmetric` for a pane, holds the left-hand Y axis symmetric about zero: it runs from −m to +m, where m is the largest of the value given and the data's distance from zero either way, so zero stands in the middle of the plot and +4 and −4 stand equally far from it. Training form (TSB) is the case it is made for: with `YSymmetric = 10` a quiet stretch of +2 and −3 still reads as near zero on an axis from −10 to +10, and a form of −24 runs it from −24 to +24. Pair it with `YFormat = ValueFormat.Signed` for ticks of `+10`, `0` and `−10`. It must be positive, and is refused beside `YMin`, `YMax` or `YMinSpan`, which set the axis's ends another way, on a logarithmic axis, which has no zero, and on kinds without such an axis; it reverses with the axis, takes `IncludeZero`, columns and areas, and a series on the right-hand axis is not measured on it.

Write the numbers beside it in the page: `5 km 24:10 → 22:47 over 6 races · best 22:47`, `scale 34–42 kg`. The Sports & performance page's Getting faster? card draws the athlete's weekly fastest 5 km and fastest kilometre in each session of repeats this way, each best ringed, and a weekly weigh-in on an 8 kg scale; the skill's `references/recipes-race-face.md` has a race-results app's personal-best and growth-log sparklines, and `references/sports.md` the personal-best sparkline. In JSON: `"sparkline":true`, `"yMinSpan":8` on a chart or a pane and `"highlight":"#E30613"` on a point; each is left out of the hash that names gradients at its default, so no gradient ID moves. From 0.36.0 `"ySymmetric":10` on a chart or a pane holds its axis symmetric about zero, and `"yFormat":"Signed"` writes its values with their signs.

### Statistical, financial and radial families

| Kind | Input | Rendering |
|---|---|---|
| `Candlestick` | One series whose points carry `Open`, `High`, `Low`, `Close` — use `ChartPoint.Candle` — and any others beside it in kinds of their own, such as a moving average or [volume in a pane](#panes) | Wick across the low-high range, body from open to close, colored by direction (`ChartSvg.RisingColor` and `FallingColor`) |
| `Ohlc` | The same as `Candlestick` — one series of `ChartPoint.Candle` points, and companions beside it | Vertical line across the low-high range, a tick to the left at the open and a tick to the right at the close, colored by direction |
| `Band` | `Y` with `Low` and `High` bounds — use `ChartPoint.Interval` | Filled interval behind the central line; points without bounds break the band into runs |
| `Histogram` | One to four series of raw observations in `Y`; `X` is ignored | Equal-width bins over a zero baseline, chosen from the pooled observations and shared by every series. `Bins` sets the count; otherwise Freedman–Diaconis chooses it, falling back to Sturges when the interquartile range is zero. Several series stand side by side within each bin |
| `Box` | One series per distribution: raw observations in `Y` with `X` ignored, or a precomputed `Summary` and no points | Quartile box, Tukey whiskers at 1.5 interquartile ranges, and outliers as circles; a supplied summary is drawn as given |
| `Violin` | One series per distribution, raw observations in `Y`; `X` is ignored | Kernel density outline mirrored about each column, with a quartile bar and a median tick |
| `Gauge` | One series of one point: `Y` the score, its label the caption; the scale `YMin` to `YMax`, 0 to 100 unless set | An open arc of `GaugeSweep` degrees, tinted by any `YZones`, the score's arc in its zone's colour with a knob at its end, the score in the centre and a Y annotation as a tick across the arc. See [Gauges and rings](#gauges-and-rings) |
| `Ring` | One to six series, one point each, `Y` measured against the series' `Goal` | Concentric progress rings from twelve o'clock clockwise, each running on over itself past its goal, up to 300 % |
| `Timeline` | One series per state, each point a span from `X` to `XEnd` — use `ChartPoint.Span` | One lane per state, top to bottom, each span a rounded bar, joined across lanes where one ends as the next begins; the legend totals each state. See [Timelines, range bars and the time of day](#timelines-range-bars-and-the-time-of-day) |
| `Range` | `Low` and `High`, and an optional `Y` — use `ChartPoint.Interval` | A capsule from low to high with a dot at `Y`, floating with no zero baseline, as a chart or beside lines and columns |
| `Calendar` | One series of days on a time axis: each point's `X` a moment, its `Y` the day's value, one day's points added together | A grid of days — weeks by weekdays, or a small grid per month — each a square, dot or bubble in its zone's colour or on the heatmap ramp; a day without activity is an empty cell. See [Calendars](#calendars) |
| `Blocks` | Spans with a height, from `X` to `XEnd` rising to `Y` — use `ChartPoint.Block` | A block exactly as wide as its span, standing on the bottom edge of its plot and rising to its value, a hairline between neighbours, its far end rounded, in its zone's colour; as a chart, or beside lines and in panes. See [Blocks](#blocks) |

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

`Statistics.Quantile`, `Statistics.Summarize`, `Statistics.Bins` and `Statistics.SharedBins` are public, so the same numbers are available without rendering. Quantiles interpolate linearly between order statistics, matching NumPy's default and Excel's `PERCENTILE.INC`. CSV exports add `Open,High,Low,Close` for candlestick charts, `Low,High` for band and range series, `Goal` for ring charts and `XEnd` for timelines and blocks.

### Gauges and rings

A gauge draws one score on an open arc, as WHOOP draws recovery and strain, Oura readiness, and Garmin training readiness and Body Battery. A ring chart draws activity rings, as Apple does: each ring one value against its goal.

```csharp
ChartSpec recovery = new() {
    Kind = ChartKind.Gauge, Title = "Recovery", Description = "This morning's recovery score",
    YLabel = "%",                                     // a gauge's unit, written after the score
    YZones = new([new("Low", 33, "#DD4B45"), new("Moderate", 66, "#A88200"), new("Good", double.PositiveInfinity, "#2E9B58")]),
    Annotations = [new(AnnotationAxis.Y, 61) { Label = "7-day average" }],
    Series = [new("Recovery", [new(0, 72, "Recovery")])]   // Y is the score, the label its caption
};

ChartSpec activity = new() {
    Kind = ChartKind.Ring, Title = "Activity", Description = "Today's move, exercise and stand",
    Series = [new("Move", [new(0, 540, "kcal")], "#DD4B45") { Goal = 600 },   // a ring's label is its unit
              new("Exercise", [new(0, 47, "min")], "#2E9B58") { Goal = 30 },
              new("Stand", [new(0, 9, "h")], "#3F87D9") { Goal = 12 }]
};
```

**A gauge** takes one series of one point: its `Y` is the score and its `Label`, if any, the caption written under it. The scale runs from `YMin` to `YMax`, 0 to 100 unless set, round `GaugeSweep` degrees — 270 by default, anything from 180, a semicircle, to 360, a full circle — centred at twelve o'clock, so the minimum stands at minus half the sweep, the maximum at plus half, and a score's angle is linear between them. A score off the scale stands at the end it passed, and its name says so: `Recovery: 104 %, above the scale, drawn at 100`. The track is the grid colour. `YZones` tint the stretch of track each zone covers, clipped to the scale, and each zone is a named aggregate, `Good: above 66`; the score's arc then takes its own zone's colour, and its name the zone's, `Recovery: 72 %, Good`. Without zones the arc takes the series colour, or a `Gradient` laid along its length, each stop at its value's angle. A knob in the background colour marks the end of the arc. The score is written large in the centre in `YFormat` — a number, compact, or a duration such as `7:09:00` — with `YLabel` as its unit at half the size, the caption and the zone's name under it, and the scale's ends under the arc's ends. A Y annotation is a target: a tick across the arc at its value, haloed so it shows on any colour, and named. Its label goes outside the arc where it fits within the drawing and clear of the description, or else inside, as near the arc as it fits whole; a label with room in neither place, clear of the score, is left out, as a reference label is, and the tick keeps its name for the tooltip and assistive technology. The arc is as large as fits between the title and the source line, its thickness .16 of its radius, and it moves down to make room for a target's label over its upper half.

**A ring chart** takes one to six series, one per ring, outermost first, each of one nonnegative point. `ChartSeries.Goal` is the ring's target, 100 unless set, and progress is `Y ÷ Goal`. Each ring is a track of its own colour at a fifth of its strength, with the progress over it from twelve o'clock clockwise, 360 degrees to the goal, both ends rounded. Past its goal a ring keeps going round: it lies whole, and its leading end is drawn again over the lap beneath, behind a soft shadow just ahead of it, so the overlap reads as Apple's does. Progress past 300 % is drawn at 300 %, and the ring's name says so. The rings fill a circle as wide as the smaller of the room across and down, between the title and the source line; the pitch from one ring to the next is .22 of that circle's radius, or .72 of it shared among the rings when there are four or more, and each ring is .84 of the pitch, so the gaps are even and the middle stays open. The centre is left empty. The legend names each ring with its value and goal, `Move: 540 of 600 kcal`, the point's label being the unit, in `YFormat`, and each ring's name adds its progress, `Move: 540 of 600 kcal, 90 %`. CSV adds a `Goal` column.

Each score and each ring is one focusable mark, a button that raises `PointSelected` like any point, with a focus ring round it. The component offers no zoom or pan for either; its legend and status line read a value as the chart's own legend does, its data table reads a ring as `540 of 600`, and hiding every ring leaves the chart's empty state. In JSON, `{"kind":"Gauge","gaugeSweep":180,"yLabel":"%","series":[{"name":"Recovery","points":[{"x":0,"y":72}]}]}` and `{"kind":"Ring","series":[{"name":"Move","goal":600,"points":[{"x":0,"y":540,"label":"kcal"}]}]}`.

Both refuse what means nothing on them, each with its reason: time and logarithmic axes, a reversed axis, an axis on the right or labelled only at its ends, panes, secondary series, trend lines, a series' own kind, series zones, point colours, `DensityCells` and X annotations. A gauge refuses a second series or point, a missing score, a sweep outside 180 to 360, a scale whose `YMin` is not below its `YMax`, a band annotation, a target off the scale, and zones together with a gradient. A ring chart refuses a seventh ring, a missing or negative value, a goal that is not positive and finite, `YZones`, annotations and gradients. Every other kind refuses `Goal` and any `GaugeSweep` but 270.

### Timelines, range bars and the time of day

Sleep stages, the first chart of every sleep app, are a state timeline: each stage a lane, each stretch of it a bar along the clock, joined where sleep moves from one stage to the next. Daily heart rate, Body Battery and sleep timing are ranges: a bar from a day's low to its high that stands wherever the day was, not on zero.

```csharp
// Last night's stages: one series per stage, top to bottom, each period a span from its start to its end.
static ChartPoint[] Spans(IEnumerable<(DateTimeOffset From, DateTimeOffset To)> periods) =>
    periods.Select(p => ChartPoint.Span(TimeAxis.Value(p.From), TimeAxis.Value(p.To))).ToArray();

ChartSpec night = new() {
    Kind = ChartKind.Timeline, XAxis = AxisKind.Time, TimeZone = "Europe/London",
    Title = "7 h 12 min asleep, 58 min deep", XLabel = "Time",
    Series = [new("Awake", Spans(awake), "#DB6A1F"), new("REM", Spans(rem), "#3F87D9"),
              new("Light", Spans(light), "#848484"), new("Deep", Spans(deep), "#9E63D3")]
};

// Each day's heart rate from its lowest to its highest, its average a dot on the bar.
ChartSpec heart = new() {
    Kind = ChartKind.Range, XAxis = AxisKind.Time, YLabel = "Heart rate (bpm)",
    Series = [new("Heart rate", days.Select(d => ChartPoint.Interval(TimeAxis.Value(d.Date), d.Average, d.Lowest, d.Highest)).ToArray())]
};

// Bedtime to waking in seconds since the evening's midnight: 23:30 is 84600, and 06:40 the next morning 110400.
ChartSpec timing = new() {
    Kind = ChartKind.Range, XAxis = AxisKind.Time,
    YFormat = ValueFormat.TimeOfDay, YReversed = true,          // HH:mm, earlier at the top
    Series = [new("Sleep", nights.Select(n => ChartPoint.Interval(TimeAxis.Value(n.Morning), null, n.Bedtime, n.Wake)).ToArray())]
};
```

**A timeline** draws each series as a lane, top to bottom in series order, named on the side the Y axis would stand, `YAxisSide` included. Each point is a span from its `X` to `ChartPoint.XEnd`, made with `ChartPoint.Span(start, end, label)`, and has no `Y`. X is continuous: a time axis in Unix milliseconds, read in `TimeZone`, or a linear one in a duration, time-of-day or plain format. A span is a bar half its lane's height, up to 24 pixels, in its lane's colour, at its exact extents and at least a pixel wide, its corners rounded by the style's `BarRadius` or else 4 pixels, so Midnight draws capsules. Where a span ends exactly where one in another lane starts, a hairline in the muted colour joins the middles of the two lanes behind the bars, which is a hypnogram's step; `TimelineConnectors = false` leaves the connectors out, for a plain state chart. Spans in one lane cannot overlap; spans in different lanes may, as intraday states such as stress and activity do. Gridlines stand at the X ticks. The legend names each state with its total time as h:mm and its share of the time in every lane to the whole percent — `REM 1:42, 22 %` — and so does the component's. Each span is a focusable button that raises `PointSelected`, named with its state, its label if it has one, its start, its end and its length — `REM: 02:14 to 02:41, 27 min` — on the clock of the axis's zone, with the day too once the axis covers two days or more. The component zooms and pans X, keeps a hidden state's lane in its place, and reads a span in its status line and data table. CSV adds an `XEnd` column.

**A range bar** is a capsule from a point's `Low` to its `High`, made with `ChartPoint.Interval(x, y, low, high, label)`. Its `Y`, if set, is a dot on the bar, filled with the background and ringed in the bar's colour; null draws the bar alone, and a point with no bounds and no value is a missing day. There is no zero baseline: the axis spans the bars, and it can be logarithmic, reversed or on the right. `ChartKind.Range` lays X out continuously, as a line chart does, and a series' `Kind = ChartKind.Range` draws ranges beside lines, areas and points, or on a column chart in each category's slot beside its columns. A bar takes the share of the slot a column would — on a continuous axis 0.7 of the closest gap on screen between neighbouring X values, up to 34 pixels, and on a column chart the category's slot — and is as wide as its share up to 18 pixels, centred in it. A continuous chart that draws range bars insets its X axis by half a slot at each end, so its first and last bars stand whole; a chart without them is laid out as before. A bar takes its point's `Color` ahead of its series'. Each bar is named with its label or X, its two ends and its average in the axis's format — `12 Sep: 52 to 168, average 74` — led by its series' name where more than one series draws ranges.

**`ValueFormat.TimeOfDay`** reads values as seconds since a midnight and writes them as the clock, `HH:mm`, rounded half up to the minute and wrapping every 24 hours: 84600 reads `23:30` and 110400, the next morning, `06:40`, so a night is one span that never crosses zero, and a value below zero reads back from the midnight before. Ticks step through 15 and 30 minutes and 1, 2, 3, 6 and 12 hours and a day, taking the smallest step that puts no more ticks on the axis than were asked for, so they land on whole hours, or on half and quarter hours over a short range; minor gridlines divide an hour into quarters and a quarter hour into fives. It applies to linear axes, X or Y, on any kind that takes a format; a time axis writes its own calendar and a logarithmic one has no clock to show, so both refuse it. With `YReversed = true` it draws Oura's sleep timing, earlier at the top.

In JSON: `{"kind":"Timeline","timelineConnectors":false,"xFormat":"TimeOfDay","series":[{"name":"REM","points":[{"x":84600,"xEnd":86220}]}]}` and `{"kind":"Range","yFormat":"TimeOfDay","yReversed":true,"series":[{"name":"Sleep","points":[{"x":0,"low":82800,"high":109800}]}]}`.

Each refuses what has no meaning on it, with its reason. A timeline refuses zones, trend lines, secondary series, a series' own kind, panes and Y annotations, and anything set on its Y axis — logarithmic or reversed, a format, bounds or end-only tick labels; it takes X annotations, which mark moments in the night. It refuses a logarithmic X axis, a point without an `XEnd` above its `X`, spans that overlap in one lane, and point colours, because a lane's colour is its state. A range series refuses `ProjectedFrom`, trend lines and zones, a point with one bound and not the other, a low above its high, an average outside its bar, and two bars at one X on a continuous axis. Every other kind refuses `XEnd`, and every kind but a timeline refuses `TimelineConnectors = false`.

Limits. Connectors join spans whose ends meet exactly, so a span that starts a moment after the last one ended is not joined to it; a connector is one hairline in the muted colour, not a blend of the two lanes' colours. Lanes are equal in height and spans one height, with no lane for missing data, no colour of a span's own and no text written on a span; lane names are cut at 14 characters. A span too short to see is drawn a pixel wide, so a run of very short spans can look longer than it is. Totals and shares count every span, whatever is zoomed, and a share is of the time in all the lanes, which on a sleep record is the time in bed. CSV writes a span's end as a raw number, without an ISO column beside it on a time axis. Range bars are capped at 18 pixels whatever room they have, and a range has one bar per point: no whiskers and no inner range, so Body Battery's charge and drain are two series. The time of day has no date and no seconds, so two nights plotted on one axis must each be measured from their own evening's midnight, as above; the component's data table and status line read a time-axis span with its date.

### Calendars

A training calendar shows a season at a glance. Strava's training log sizes each day's bubble by its distance, Bevel, Peloton and Atoms shade the days of a contribution grid by how much was done, WHOOP colours them by recovery, and TrainingPeaks by how closely the plan was kept.

```csharp
// The season's daily training stress as a contribution grid, each day in its tier, today outlined.
ChartSpec season = new() {
    Kind = ChartKind.Calendar, XAxis = AxisKind.Time, TimeZone = "Europe/London",
    Title = "93 days trained, 20 of them hard", Description = "Each day's training stress, in tiers",
    YZones = new([new("Easy", 50, "#3F87D9"), new("Moderate", 100, "#2E9B58"),
                  new("Hard", 150, "#A88200"), new("Very hard", double.PositiveInfinity, "#DD4B45")]),
    Annotations = [new(AnnotationAxis.X, TimeAxis.Value(today)) { Label = "Today" }],
    Series = [ChartSeries.From("Training stress", days, d => TimeAxis.Value(d.Date), d => d.Stress)]
};

// This month's runs as bubbles sized by distance, on a grid of the whole month.
ChartSpec month = new() {
    Kind = ChartKind.Calendar, XAxis = AxisKind.Time,
    CalendarLayout = CalendarLayout.Months, CalendarCell = CalendarCell.Bubble,
    XMin = TimeAxis.Value(first), XMax = TimeAxis.Value(first.AddMonths(1).AddDays(-1)),
    Title = "129 km run in September",
    YZones = new([new("Run", double.PositiveInfinity, "#3F87D9")]),   // one colour: a bubble's size is its distance
    Series = [new("Distance (km)", runs.Select(r => new ChartPoint(TimeAxis.Value(r.Start), r.Kilometres, r.Name)).ToArray())]
};
```

**A calendar** draws one series of days. Each point's `X` is a moment on a time axis, in Unix milliseconds, and counts for the day it falls on in `TimeZone`, UTC unless set, so a run logged at 23:30 in New York stays on its own day there; its `Y` is the day's value, and the points on one day — two sessions — are added together. A day whose total is zero or missing is a day without activity: an empty cell in the grid colour that takes no focus. The calendar runs from the day of its earliest point to the day of its latest, or from `XMin` to `XMax` where they are set, and draws no day outside them.

`CalendarLayout.Weeks`, the default, is the contribution grid: a column per week and a row per weekday, the weeks starting on `WeekStart` — Monday unless set, as ISO 8601 has it — with each month named above the week its first day falls in, and Mon, Wed and Fri named beside their rows. Where two month names would touch, the later is kept, so a grid that starts on the last days of a month is not named for it. `CalendarLayout.Months` draws a small grid for each calendar month: seven columns from the week start, a row per week, the month's name and the weekdays' initials above it. The months stand left to right a column apart and wrap, as many to a row as give the largest cells, every month as tall as the longest; across more than one year each name carries its year.

Every day is a cell on one pitch, which fills the width or, where it is the tighter, the height, since a chart keeps its `Height`; a fifth of the pitch, at most 6 pixels, is the gap between cells across and down alike, and the grid is centred in the room it leaves, so `FitWidth` refits it on a phone. `ChartSpec.CalendarCell` draws each day as a `Square`, the default, a rounded square whose corners are the style's `BarRadius` or else 3 pixels, so Midnight's capsules draw circles; as a `Dot`, a filled circle; or as a `Bubble`, whose area is proportional to the day's total, the largest filling its cell, over a track circle the size of the cell.

With `YZones` each day takes the colour of the zone its total falls in — the zone's own, or the style's zone ramp at its position — and its name says which. Without zones it takes a colour on a ramp that steps up from an empty day, as GitHub's contribution grid does: the lowest total among the days with activity takes the colour a third of the way from the grid colour of an empty cell to the style's `HeatmapHigh`, and the highest `HeatmapHigh` itself, with the heatmap's hairline round each day. The quietest day therefore always stands apart from a rest day, darker on a light style and lighter on a dark one; on the light preset it is `#B0C1E8` against the empty cell's `#E8EDF5`, 1.53:1, on the dark preset 1.29:1 and on Midnight 1.62:1, where the heatmap's low end gave 1.00:1, an inverted 9.5:1 and a darker 1.37:1 in 0.29.0. A calendar never uses `HeatmapLow`; a heatmap still runs from it. The key under the grid names the zones, or reads the ramp's lowest and highest totals either side of five steps along it, and then names each outlined day.

Each day with activity is a focusable button that raises `PointSelected` with the index of its first point, named with its date, its points' labels, its total in `YFormat` and its zone: `Sun 27 Sep 2026, Hilly progression run: 154, Very hard`. An X annotation outlines its day in the gap round the cell, in the annotation's colour or the muted one, names it — `Today: Sun 27 Sep 2026` — and adds it to the key by its label or its date; an annotation outside the days draws nothing. The component offers no zoom, keys its legend with the zone or ramp colours, reads a selected day in its status line as `Training stress: Sun 27 Sep 2026 = 154`, and lists the original points in its data table. CSV writes each original point as other time charts do, with its `XTime`, so a day's two sessions are two rows. In JSON: `{"kind":"Calendar","xAxis":"Time","calendarLayout":"Months","calendarCell":"Bubble","weekStart":"Sunday","series":[{"name":"Distance (km)","points":[{"x":1789387200000,"y":8.6}]}]}`.

A calendar refuses what has no meaning on it, each with its reason: an X axis other than a time axis, a logarithmic or reversed Y axis, Y bounds, a Y axis on the right or labelled at its ends, anything on a secondary axis, panes, more than one series, a series' own kind, trend lines, series zones (it takes `YZones`), Y annotations and X annotation bands, `XEnd`, point colours, since a day's colour is its value, negative values, `SkipWeekends` and `TimeSkips`, and a span of more than 3,660 days. Every other kind refuses `CalendarLayout`, `CalendarCell` and `WeekStart` set.

Limits. One series is one calendar: a ride and a run on one day are added together, so sports kept apart are separate calendars, and a day carries no rings, no icons and no text of its own. There are no streaks, no weekly totals beside the grid and no planned days drawn differently; a day after the last point but inside `XMax` is an empty cell like a rest day. The ramp is linear between the lowest and highest active totals, not GitHub's four quantile levels, so one outlier pales the rest, and it cannot be fixed across two calendars, since `YMin` and `YMax` are refused. The ramp's steps are close together on the dark presets, whose grid colour is near the background; tiers in `YZones` stay the choice where the tiers mean something, such as easy and hard days. A long span makes small cells: a year is about 15 pixels a day at 900 pixels wide and 5 at a phone's width, where the weekday names are left out and month names thin out.

### Blocks

Two charts that endurance apps draw are made of blocks whose width means something. Strava's lap chart sizes each lap by its distance and raises it to its pace, faster higher, with the average pace across them; TrainingPeaks and Zwift draw a structured workout as steps as long as they last and as high as their target, coloured by power level, with the ride laid over them to check how closely it was followed.

```csharp
// Laps: each as wide as its distance and as high as its pace on a reversed axis, with the average pace across them.
ChartSpec laps = new() {
    Kind = ChartKind.Blocks, Title = "8.4 km at 5:06 per km", XLabel = "Distance (km)", YLabel = "Pace (/km)",
    YFormat = ValueFormat.Duration, YReversed = true,                  // m:ss, faster higher
    Annotations = [new(AnnotationAxis.Y, 306) { Label = "Average" }],
    Series = [new("Laps", [ChartPoint.Block(0, 2, 336, "Lap 1"), ChartPoint.Block(2, 4, 318, "Lap 2"), ChartPoint.Block(4, 5.5, 301, "Lap 3"),
                           ChartPoint.Block(5.5, 7, 289, "Lap 4"), ChartPoint.Block(7, 8.42, 270, "Lap 5")])]
};

// A threshold workout: each step from its start to its end in seconds at its target power, in Coggan's levels, the ride over it.
ChartSpec workout = new() {
    Kind = ChartKind.Blocks, Title = "3 × 8 min at threshold", XLabel = "Elapsed time", YLabel = "Power (W)",
    XFormat = ValueFormat.Duration, IncludeZero = true,                // steps rise from zero
    Annotations = [new(AnnotationAxis.Y, 250) { Label = "FTP" }],
    Series = [new("Plan", steps.Select(s => ChartPoint.Block(s.Start, s.End, s.Watts, s.Name)).ToArray()) { Zones = ZoneScale.CogganPower(250) },
              new("Power", ride.Select(r => new ChartPoint(r.Seconds, r.Watts)).ToArray(), "#D36B84") { Kind = ChartKind.Line }]
};
```

**A block** is a point made with `ChartPoint.Block(start, end, height, label)`, which sets its `X`, its `XEnd` and its `Y`. It is drawn exactly from `X` to `XEnd` along a continuous X axis — a linear one in any format, or a time axis — and stands on the bottom edge of its plot, rising to its `Y` on its series' own axis. That one rule serves both charts. On an axis that includes zero, as `IncludeZero` makes it, a power target rises from zero. On a reversed pace axis the bottom edge is the slow end, so a lap rises from below the slowest pace to its own and the fastest stands tallest. An axis fitted to the data would leave the lowest block, the slowest lap, with no height at all, so where a pane's axis carries blocks and its bottom is neither held at zero nor set — by `YMin`, or by `YMax` on a reversed axis — the bottom moves out past that block until it stands a sixth of the plot's height, measured in the axis's own space, decades on a logarithmic axis. A line on the axis that already reaches lower leaves it where it was.

Where a block in a series ends as the next begins, each gives up half a pixel there, so neighbours stand a hairline apart and a workout's steps read as steps; a block narrower than two pixels gives up a quarter of its width instead, and blocks of two series are not parted. The far end, the top, is rounded by the style's `BarRadius`, or 4 pixels where it sets none, and by at most 6, so Midnight's capsule radius rounds a wide block's corners rather than doming it; the radius is clamped to half the block's width and to its height, and the end it stands on is square. A block takes its point's `Color`, or the colour of the zone its height falls in when its series has `Zones` — a workout's steps in Coggan's power levels from `ZoneScale.CogganPower(ftp)` — or else its series' colour. The legend keys a zoned series by up to four of the zone colours its blocks draw in, and a plain one by two steps, the second taller.

From 0.35.0 a block whose value stands above the bottom of its axis is drawn at least 2 pixels tall, so it never vanishes: in a histogram of 312 finish times on a 240-unit card, the last bin's one finisher would be 0.98 units tall and is drawn at 2. A block at the bottom, a count of none, draws nothing visible but keeps its name and its focus. That makes blocks the way to draw a histogram whose bins are counted elsewhere — by a results service, a warehouse, an app's own rule — each bin `ChartPoint.Block(from, to, count)` on an axis that `IncludeZero`, the reader's bin in a colour of its own with a `ValueNote` that says why, the median an X annotation `InFront` of the bins; `ChartKind.Histogram` bins raw observations itself. The race-results recipe "How the field finished" in the Claude Code skill draws one.

`ChartKind.Blocks` lays X out continuously, as a line chart does, and takes series of other kinds beside it; a series' own `Kind = ChartKind.Blocks` draws blocks on a line, area, scatter, bubble, band, range, candlestick or OHLC chart, in any pane and on the right-hand axis. Blocks draw in the column layer, over bands and areas and under lines and points, so the executed power stands over its plan whatever the series order, and a Y annotation — an average pace, a threshold — runs across them. The X axis reaches the last block's end and keeps its ticks under blocks rather than writing their labels there.

Each block is a focusable button that raises `PointSelected`, named with its label, its span in the X axis's format, its height in its axis's format and its zone — `Lap 2: 2 to 4, 5:18` or `Interval 2: 27:00 to 35:00, 250, Lactate threshold`; a block without a label, and every block where several series draw blocks, is led by its series' name. The component zooms and pans X across blocks, clipping those at the edges, reads a selected block in its status line as `Plan: Interval 2, 27:00 to 35:00 = 250` and in its data table as `250 from 27:00 to 35:00`, and keys a zoned series in its legend as the chart does. CSV carries each block's `XEnd`. In JSON: `{"kind":"Blocks","yFormat":"Duration","yReversed":true,"series":[{"name":"Laps","points":[{"x":0,"xEnd":2,"y":336,"label":"Lap 1"},{"x":2,"xEnd":4,"y":318}]}]}`.

Blocks refuse what has no meaning for them, each with its reason: a point without an `XEnd` above its `X`, or without a `Y`; two blocks of one series that overlap, though they may touch and blocks of different series may overlap; blocks on a column chart, whose categories have no slot a block could take; a logarithmic X axis, on which two blocks of one length would be drawn at different widths; `Trend` and `ProjectedFrom`; and the finishes that belong to lines and columns — stroke widths, curves, markers, fades, value labels and gradients. Every mark but a block and a timeline's span still refuses `XEnd`.

Limits. A block is flat: a ramp in a workout is drawn at its average or as several steps, and there are no sloped, stacked or labelled blocks, so a lap's number and a step's target are read from its name. On a pace axis the heights are measured from the bottom edge, not from zero, so a lap twice as tall is not twice as fast, as on Strava's chart. The X axis writes plain numbers, so a distance's unit goes in `XLabel`. A gap between two blocks is drawn as empty space, with nothing to say it was a rest. A reference line stands behind the blocks, as it does behind columns, so it shows only where they fall short of it, though its label is written over them. The executed effort is an ordinary line: compliance — time on target in each step, or how far off it — is not computed. The hairline is a unit of the drawing, so it thins with a chart drawn wider than it is shown.

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

`ChartFinish.Classic` is the exact way back. It draws charts as 0.23.0 did, byte for byte, gradient IDs included, which the [release baseline](docs/VERIFICATION.md) checks across 189 renderings. Network graphs are the exception: 0.31.0 moved their edges and edge labels clear of node labels and widened their circles in both finishes, so a classic graph is laid out as 0.31.0 lays it out.

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
- **`Gradient`** colours a line or area stroke and its markers continuously by value. It is a vertical gradient laid out in the plot's own coordinates through the series' own axis, so each stop sits exactly at its value's height, on a logarithmic, reversed or right-hand axis too, and past the first and last stops their colours carry on. Stops rise strictly, at least two and at most 32, in `#RRGGBB`. A series takes a gradient or `Zones`, not both: zones colour in steps and name the zone in each label, while a gradient colours continuously and adds nothing to a label, which already reads the value. From 0.40.0 it also fills columns and bars by value; see [Lap columns and best efforts](#lap-columns-and-best-efforts-columns-filled-by-value-and-a-second-label-line).
- **`Markers`** are `Auto` (each kind's own: in the refined finish a line's or area's appear on hover and focus, and in the classic they are always drawn), `Hollow`, `Filled` or `None`. A hidden marker is still there: every point keeps its focusable, labelled mark, with a transparent target the size of the marker, so keyboard and screen-reader users reach each reading and the focus ring still draws. A scatter series is its markers, so it refuses `None`.
- **`HighlightLast`** draws the last reading of a line or area larger, with a soft ring at low opacity, even when the other markers are hidden. Its pane's clip widens to 12 pixels so the ring is drawn whole at the plot's edge.
- **`ValueLabels`** writes each column's or bar's value just past its far end, in its axis's format and the style's text colour. A label that would not fit across its column, or within the plot beside its bar, is left out rather than overlapping. The labels are hidden from assistive technology, because each bar's accessible name already reads its value. From 0.33.0 it writes a line's or a scatter series' values above their points in the point's colour too, described under [Race results on a line](#race-results-on-a-line).
- **`ChartStyle.BarRadius`** rounds the far end of every column and bar — the bottom of a negative column, the left of a negative bar — and keeps the baseline end square. It is clamped to half the bar's width, which makes a semicircle, and to the bar's length, so a large radius draws capsules; a stack rounds only its outermost segment on each side of zero. Null keeps the 2 px corners.
- **`ChartStyle.Gridlines`** is `Solid`, `Dotted`, `Dashed` or `Hidden`. It changes the horizontal and vertical gridlines, minor ones included, and nothing else; `Hidden` keeps the tick labels. A style that sets none draws `Dotted` in the refined finish and `Solid` in the classic.
- **`ChartSpec.YAxisSide = AxisSide.Right`** labels the main Y axis of every pane on the right and gives the left margin back. A chart with a secondary series refuses it, because the right edge is taken, and so does a horizontal bar chart, whose value axis runs along the bottom.
- **`ChartSpec.YTickLabels = TickLabels.Ends`** labels only the lowest and highest tick of the main Y axis and keeps every gridline; a secondary axis labels all of its own. `TickLabels.Bounds` (0.35.0) labels the axis's two ends at their exact values instead, and `XTickLabels` does either for the X axis, as described under [Axes](#axes).
- **`ChartStyle.Midnight`** is a third preset: a near-black background, `#0B0E14`, with a vivid palette, dotted gridlines and capsule bars. Its six series colours measure 6.64:1 to 11.99:1 against the background, its zone ramp 6.31:1 to 11.85:1, its candles 6.48:1 and 11.31:1 and its edges 5.34:1, and its text and muted text 17.7:1 and 7.7:1, so `ContrastIssues` reports nothing.

Gradients need IDs, which Lumen had avoided because several charts share one page. A chart that uses a gradient or a fade defines each once, in a `<defs>` block after its stylesheet, named `lumen-`, the first twelve hex digits of the SHA-256 of its spec serialized as JSON with options fixed in the library, and a counter. The same spec always yields the same IDs, two different charts cannot collide, and two identical charts define identical gradients, so whichever a reference resolves to paints the same. A chart that uses neither has no ID and no `<defs>`. The SVG stays self-contained: every reference is to a fragment of the same document, so the PNG export, which rasterizes that SVG through a canvas, keeps the gradients, and the browser suite checks that it does.

In JSON: `"curve":"Smooth"`, `"fill":"Fade"`, `"markers":"None"`, `"highlightLast":true`, `"valueLabels":true`, `"strokeWidth":3` and `"gradient":[{"value":120,"color":"#3F87D9"},{"value":180,"color":"#DD4B45"}]` on a series; `"yAxisSide":"Right"`, `"yTickLabels":"Ends"` and, from 0.35.0, `"xTickLabels":"Bounds"` on the chart; `"gridlines":"Dotted"`, `"barRadius":8` and `"finish":"Classic"` in its style.

Each option is refused where it cannot apply: a stroke width outside 0.5 to 12 or on a mark without a stroke, a curve on anything but a line or area, a fade on anything but an area or a column, a marker style on anything but a line, area or scatter, a highlight on anything but a line or area, value labels on anything but columns, bars, lines and scatter points, and a gradient on anything but a line or area, beside zones, with fewer than two stops or stops that do not rise, or with a stop at or below zero on a logarithmic axis. A negative or non-finite bar radius is refused too.

Limits. A smooth curve passes through the sampled points, so on a long line it smooths what sampling kept. Step is step-after only. A gradient colours by the Y value alone, not by X or by another measure, and leaves the fill, the legend swatch and the component's data table in the series colour (columns and bars filled by one, from 0.40.0, key its stops). A faded column's tip is lighter than the colour `ContrastIssues` measures, so a palette that only just clears 3:1 falls below it there. Horizontal bars and stacked columns cannot fade, and stacked columns take no value labels. Value labels are fitted by an estimate of their width rather than measured, since the server has no fonts; it holds for Segoe UI, Arial, Georgia and Times New Roman, but a wide face such as Verdana draws labels up to about a tenth wider, so a label that only just fits can touch its neighbour. The bar radius applies to every column and bar a style draws; candle bodies, box plots, histogram bins and legend swatches keep their corners. A radar's rings and spokes are its scale and keep their solid lines, and annotations keep their own dashes. A highlighted series' pane clips 12 pixels outside the plot rather than 6, so a zoomed line runs that much further past the edge. Hashing serializes the spec on every render that uses a gradient or a fade, which measured about 8 ms more at 10,000 points and 20 ms at 100,000 for this release, through System.Text.Json's reflection-based resolver, which a host that trims away reflection metadata must keep. Two identical charts share gradient IDs, which is harmless while both are displayed; if the first is hidden with `display:none`, a browser may not paint the second's gradients, so give such charts different titles.

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

`From` alone draws a line, dashed unless `Dashed` is false; adding `To` draws a band. Values are in data coordinates, so an annotation zooms and pans with what it refers to and clips at the plot edge. They render behind the data unless `InFront` is set, take the style's muted colour unless `Color` names one, and each is a focusable, labelled aggregate reading `Target: 55` — the value is always in its name, and drawn beside its label unless `ShowValue` is off, so a reference can never sit somewhere other than where it claims. In the refined finish the label is written over the data with a halo, kept inside the plot and its band, and nudged clear of the others, as [described above](#the-refined-finish); in the classic finish it is drawn with its reference, behind the data.

`AnnotationAxis` names an axis of the data, not a direction on the screen. A horizontal bar chart draws its values along the bottom, so there a Y annotation stands upright at its value, line or band, as the zone bands do. In the classic finish its label sits at the top of the plot on the side of it with more room, reading from the part of it the plot shows, so a target near the end of the axis keeps its label in view; one wholly off the plot turns its label away and clips with it. In the refined finish an upright line's label sits on whichever side of it has room, and a reference off the plot is not labelled.

Two settings change how one is drawn (0.35.0). `ShowValue = false` draws its `Label` alone, `median` rather than `median: 47:12`, where the value is read elsewhere — on the axis, or in the chart's description — and its room is measured on those words; its tooltip and accessible name still read the label and the value, so the reference still says where it stands. It needs a `Label`, and a calendar, whose key names an outlined day by its label alone already, refuses it. `InFront = true` draws the line or band over the data rather than behind it, so the columns or blocks it runs through cannot hide it: a median over a histogram's bins, a target over columns. A line in front stands on a halo of the background colour, as a gauge's target does, so a muted line still shows over grey bars. Its label is written over the data either way. An X annotation in front stands over the data of every pane it crosses, and a timeline's over its spans; a gauge, which draws its target over the score already, and a calendar, which outlines its day, refuse it. In JSON: `"showValue":false` and `"inFront":true`.

```csharp
ChartSpec field = new() {
    Kind = ChartKind.Blocks, IncludeZero = true, XFormat = ValueFormat.Duration, XTickLabels = TickLabels.Bounds,
    Annotations = [new(AnnotationAxis.X, 2832) { Label = "median", ShowValue = false, InFront = true }],    // draws "median", reads "median: 47:12"
    Series = [new("Finishers", bins.Select(b => ChartPoint.Block(b.From, b.To, b.Count)).ToArray())]
};
```

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

A series can draw as something other than its chart. `ChartSeries.Kind` takes a line, area, column, scatter, band or, since 0.27.0, [range](#timelines-range-bars-and-the-time-of-day), and the chart's own kind still lays out X: line, area, scatter, bubble, band and range charts place every series along a continuous axis — numeric, logarithmic or time — and a column chart places them by category. The performance management chart draws fitness and fatigue as lines over each day's training stress as columns, with form as an area against the right-hand axis and the planned days dashed:

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
- **Lines, areas, points and bands on a column chart** connect or mark the centre of each category, which is where its columns stand, so a weekly column chart can carry its average as a line or its target as a band. Only column and range series share a category's slot.
- **Drawing order.** Bands are drawn first, then areas, columns and lines, and scatter points and bubbles last, so the broad marks stand behind the narrow ones. Series keep their order within each group, so a chart of one kind draws exactly as it did.
- **Axes.** An axis that carries a column or area series includes zero, and refuses to be logarithmic, reversed or bounded away from zero, as a column or area chart does, because those marks draw from a zero baseline. The other axis is free: the line on the left of a chart with columns on the right can still be logarithmic or reversed. Each series is measured against its own axis, as before.
- **Everything per series follows the mark.** Zones, point colours, trend lines, the order X must run in and a band's bounds are checked and drawn for the mark a series draws rather than the chart's kind: columns on a line chart take their zone colours, a line on a column chart takes a trend fitted through the category centres, and a band on a line chart refuses point colours as a band chart does. Tooltips, accessible names and the component's data table and status line read each series against its own axis. CSV adds the `Low,High` columns whenever any series draws a band.
- **Projections.** `ChartSeries.ProjectedFrom` dashes a line or area stroke from that X onward, for planned workouts carried forward. The stroke is split exactly where the drawn segment reaches the X, interpolated on screen as zone crossings are, so on a logarithmic axis too; on a column chart, which places categories by index, a projection between two is interpolated between their centres. Markers and fill are drawn as before, each mark from the projection on is named `projected` — `Fitness: 24 Aug 2026, 84.3, projected` — and the other marks refuse one.

Line, area, scatter, bubble, band, range and column charts take a series kind, and since 0.22.0 candlestick and OHLC charts take one on every series beside their candles; horizontal bars, stacked columns and the radial and statistical kinds refuse it, because they lay out every series of a chart together. A series cannot be drawn as a bubble, which shares one size scale across its chart, nor as one of those kinds. In JSON a series kind is a string and a projection a number: `{"name":"Form","kind":"Area","secondary":true,"projectedFrom":1787529600000,"points":[…]}`.

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

Line, area, scatter, bubble, band, range, candlestick and OHLC charts take panes, because they lay X out continuously; the other kinds refuse `Panes` and any nonzero `Pane`. A chart has at most six plots in all, the main plot and five panes (four before 0.37.0), every pane needs a series, a series' `Pane` needs a `ChartPane` to describe it, and a weight must be positive and finite. In JSON, `"panes":[{"label":"Volume","weight":0.4,"yFormat":"Compact"}]` on the chart and `"pane":1` on a series.

Limits. Without `SharedReadout` (0.36.0), which reads every pane at one X, pointing at a mark names that mark alone. The gap between panes is fixed and their heights follow their weights, so a short pane does not grow to fit its ticks or its title; in the refined finish it labels fewer ticks, at least 28 pixels apart, and for more of them give it more weight or the chart more height. Panes cannot be resized or reordered by dragging. `IncludeZero` applies to every pane, and the legend keys every series together, whatever its pane.

### Ride channels: long streams and plots named above

A bike computer records a channel a second, 7,200 samples an hour. 0.37.0 draws a stack of such channels as a training app does: each in a plot of its own, named above it with its numbers, smoothed rather than spiky, read all at once, and zoomed by dragging across it.

```csharp
string Header(string name, IEnumerable<double?> values, string unit)
{
    var present = values.OfType<double>().ToArray();                 // a dropout is a gap, never a zero
    return $"{name} · avg {present.Average():0} · max {present.Max():0} · min {present.Min():0} {unit}";
}
var channels = new ChartSpec {
    Title = "Saturday's ride", Kind = ChartKind.Line, XFormat = ValueFormat.Duration, Height = 640,
    Sampling = SamplingMethod.Average, MaxRenderedPoints = 600,
    YTickLabels = TickLabels.None, PaneTitles = PaneTitlePlacement.Above, SharedReadout = true,
    YLabel = Header("Heart rate", heart, "bpm"),
    Panes = [new() { Label = Header("Power", power, "W"), Weight = 1 }, new() { Label = Header("Cadence", cadence, "rpm"), Weight = 1 }],
    Series = [Channel("Heart rate", heart, 0), Channel("Power", power, 1), Channel("Cadence", cadence, 2)]
};
```

- **`ChartSpec.Sampling`** and the enum `SamplingMethod`. `MinMax`, the default, thins a long line as every chart has been thinned, keeping each bucket's lowest and highest point. `Average` divides the X range shown into `MaxRenderedPoints` slices of equal width, the same for every series, and draws a series' points in each slice as one point at their mean X and mean Y, the mean written as precisely as the series' own values (to at most two places, so whole beats per minute stay whole), so a 1 Hz stream reads as its trend and channels recorded at the same moments line up slice for slice. It applies to series drawn as lines or areas whose points in view outnumber the budget; a series within it is drawn whole, and bands' outlines, trends and every other mark keep `MinMax`. Each run keeps its own slices, so a missing value stays a gap; a slice of one point draws that point. An average's name and tooltip end `, average of 12 points`, and it reports its slice's first point to `PointSelected`; a highlighted point, and the last point of a series with `HighlightLast`, keeps a mark of its own beside its slice's average, off the line, and an average's change words are left out, since it stands for a slice.
- **The window.** A run longer than `MaxRenderedPoints` is thinned over `XMin` to `XMax` only, with the nearest point outside each side so the line still runs to the plot's edges, in both methods; zoomed far enough in, every point in view is drawn. A run within the budget is drawn whole, as before.
- **Six plots.** `Panes` takes five, so a chart draws up to six plots.
- **`ChartPane.YTickLabels`** labels a pane's ticks its own way; null takes the spec's. **`TickLabels.None`** writes no tick label at all, the gridlines staying, on `YTickLabels` or a pane's; `XTickLabels` refuses it, since every pane reads its X from that axis.
- **`ChartSpec.PaneTitles`** and the enum `PaneTitlePlacement`. `Above` writes the main plot's `YLabel` and each pane's `Label` as one horizontal line over the plot's left edge, in the style's text colour, cut with `…` where the generous width estimate finds it wider than the plot, its whole kept as its accessible name and tooltip (`role='img'`, `aria-label` and a `<title>`), as a title's is; put the key fact first and keep it short, `HR · avg 147 · max 191 · min 89 bpm`; the main plot moves down 18 units and the gap between plots grows from 24 to 30. A right-hand axis keeps its title up the side. Where no left-hand axis writes a label either, the left margin narrows from 76 to 30. Line, area, scatter, bubble, column, stacked column, band, range, candlestick, OHLC and blocks charts take it.
- **Drag to zoom** (the component). With a mouse or a pen, pressing on the plots and dragging 8 pixels or more across them draws a band through every pane, the text colour at a tenth edged in the muted colour, and letting go zooms to the X it covers, no narrower than a hundredth of the whole, as the zoom buttons do; Reset view brings the whole range back. Escape lets a drag go; a shorter drag is a click; a touch keeps the tap that reads the chart. Keyboard users keep the zoom and pan buttons. `ChartSvg.Plot(spec)` returns where the plots and their shared X axis stand in the drawing, a `ChartPlot`, so another host can do the same over a static SVG.

If the app already buckets its channels, to 600 points say, pass those points and leave `Sampling` alone. In JSON: `"sampling":"Average"`, `"paneTitles":"Above"`, `"yTickLabels":"None"`, on the chart or a pane. The Sports & performance page's Ride channels card draws an invented two-hour ride this way.

### Season arcs and gaps: ticks set by hand, units and end labels

0.38.0 adds three things a results page needs to draw a season's arc through its fields and a race's gaps to the leader: ticks where the author puts them, a unit after every value, and lines named at their ends instead of in a legend.

```csharp
var gaps = new ChartSpec {
    Title = "Gap to the leader", Kind = ChartKind.Line, Width = 340, Height = 320,
    YReversed = true, YMin = 0, YFormat = ValueFormat.Signed, YUnit = "s",          // 0s at the top, then +25s, +50s
    Series = riders.Select(r => new ChartSeries(r.Name, r.Gaps) { EndLabel = r.Short, EndNote = r.Note }).ToArray()
};
var arc = new ChartSpec {
    Title = "Season arc", Kind = ChartKind.Line, YReversed = true, YMin = 0, YMax = 100, YUnit = "%",
    YTickValues = [new(0, "Front"), new(50, "Mid"), new(100, "Back")], Series = disciplines
};
string svg = ChartSvg.Render(gaps, includeLegend: false);      // <LumenChart Spec="gaps" ShowLegend="false" />
```

- **`ChartSpec.YTickValues`**, **`ChartPane.YTickValues`** and the record `AxisTick(Value, Label)`. A list of ticks set by hand replaces the ticks the axis would choose: a gridline exactly at each value, labelled with its `Label` as given, or else with its value in the axis's format and unit. A value outside the axis's range is left out and never stretches it, so set `YMin` and `YMax` to the range the ticks need; with `YReversed` and `YMin = 0`, 0 stands exactly at the top. `YTickLabels` still chooses which labels are written (`Ends`, `Bounds`, `None`), and the axis draws no minor gridlines of its own. At most 24 ticks, no value twice, every value finite and positive on a logarithmic axis, every label at most 24 characters. Charts drawn on an X and a Y axis take them, the horizontal bar chart's value axis along the bottom included; donut, heatmap, radar, histogram, box, violin, gauge, ring, timeline and calendar charts and a sparkline refuse them. In JSON, `"yTickValues":[{"value":0,"label":"Front"},{"value":50}]`.
- **`ChartSpec.YUnit`** and **`ChartPane.YUnit`**: up to 8 characters written straight after every value the left-hand axis writes, exactly as given, so `"s"` gives `+12.3s` and `" bpm"` gives `152 bpm`: its automatic tick labels, its bounds labels, its marks' names and tooltips, their value labels, its zones' and annotations' readings, the shared readout, and the component's status line and data table. A tick labelled by hand is written as given; the right-hand axis takes no unit, and a pane takes its own, not the spec's. CSV keeps raw numbers. `Axis.Unit` is what writes it, so an app writing values beside the chart writes them alike: `new Axis(AxisKind.Linear, 0, 1) { ValueFormat = ValueFormat.Signed, Unit = "s" }.Format(12.3)` is `+12.3s`. The kinds that take `YTickValues` take it, and a sparkline's names take it too.
- **`ChartSeries.EndLabel`** and **`ChartSeries.EndNote`**, each up to 24 characters. The label is written just right of the series' last point drawn in view, centred on it, at 12 px and weight 600, in the series colour where that clears 4.5:1 against the background and in the style's text colour where it does not; the note follows on the same line at normal weight in the muted colour, or in the text colour where a style's muted colour falls short of 4.5:1. Each is written over a copy of itself stroked in the background colour, as value labels are. The right margin grows from 30 to hold the widest label and note, by the library's generous width estimate, up to the point where the plot would keep less than half the drawing's width; past that a label is cut with `…`, its whole kept as its tooltip and accessible name (`role='img'`, `aria-label` and a `<title>`), as a cut pane header's is. At 340 that leaves about 10 characters, so name riders by a letter or a short name and keep the full name for the series. Labels whose spans across the drawing overlap never overlap each other: sorted by their points' heights, they are moved apart as little as they can be (least squares), 14 units a line, within their plot and 8 units past its top and bottom; a label moved more than 3 units off its point steps 6 units right and is joined to it by a 1-unit line in the series colour, or the muted colour where the series colour falls short of 3:1. Where even 12 units a line do not fit, the lowest labels are left out. The last point's name says the label and note, `You: Lap 6, +8.7s, labelled You · +8.7s`, so neither is drawn only. Line, area and scatter series take them (a scatter series ends at its point furthest along X); bars, bands, ranges, blocks and the other marks, a density scatter, a chart with a right-hand axis, whose labels take the right margin, and a sparkline refuse them, and a note needs a label. When every series has one, leave out the legend: `includeLegend: false`, or `ShowLegend="false"` on the component.

Limits. An end label inside the plot, after a line that stops early, does not avoid value labels or a reference's label. End labels are 12 px: a chart with many lines that end close together needs about 14 units of plot height a line. Ticks set by hand are not thinned when they crowd. Histograms, boxes and violins take neither ticks set by hand nor a unit yet. The Racing section of the Sports & performance page draws a Season arc and a Gap to the leader card this way, and the Claude Code skill's `references/recipes-race-face.md` gives both as recipes.

### Proportions and meters: strips, bar tracks and titles kept unwritten

0.39.0 adds a strip of shares, as a training app draws time in each zone, meter bars on tracks, as a results page draws a score out of 100, and a way to keep a chart's title as its name without drawing it, for a page that writes its own heading.

```csharp
var zones = new ChartSpec {
    Title = "Effort zones", Description = "Time in each heart-rate zone", Kind = ChartKind.Strip, Width = 340,
    YFormat = ValueFormat.Duration, DrawTitles = false,                          // the card's own heading names it
    Series = [new("Time in zone", [new(0, 740, "Easy") { Color = "#3FD17A" }, new(1, 1290, "Moderate") { Color = "#D7DDE5" },
        new(2, 820, "Hard") { Color = "#F5B642" }, new(3, 250, "Very hard") { Color = "#E30613" }])]
};
var scores = new ChartSpec {
    Title = "Race scores", Kind = ChartKind.Bar, Width = 340, Height = 240, YMin = 0, YMax = 100, BarTrack = true,
    YTickLabels = TickLabels.None, Series = [new("Score", [new(0, 82, "Execution"), new(1, 64, "Improvement")]) { ValueLabels = true }]
};
```

- **`ChartKind.Strip`**: one series whose points are the parts of a whole, in order, each point's `Label` the part's name and its `Y`, zero or more, its amount; X is only their order. The parts are drawn as one bar 18 units thick across the drawing less 24 units each side, each as long as its share of the total, in its point's `Color` or else the style's series colours in order; both outer ends are rounded by the style's `BarRadius`, or 6 units, clamped to half the bar's thickness, and each part is parted from the next by a 2-unit gap in the background colour, so neighbours never rely on their colours alone to be told apart. A part of zero draws nothing; a part too small to show keeps 1 unit. Under the bar a key names every part in order with a 10-unit swatch and its whole percentage, `Easy 24%`, in the text colour: the percentages add up to exactly 100, each share rounded down and the points left over going to the largest remainders, ties to the part listed later, so three equal thirds read 33, 33 and 34. The entries flow left to right and wrap onto rows 20 units apart; a part of zero keeps its entry, `Hard 0%`. Each part drawn is a focusable mark named, and tooltipped, `Easy: 24%, 12:20`, its amount in `YFormat` and `YUnit` with its `ValueNote` after it, and the arrow keys step from part to part. `ChartSvg.PartLabel(spec, index)` returns the same words, which the component's status line reads. A strip draws no axes, ticks or gridlines and is drawn as tall as its content, the bar 64 units from the top under its title and description (14 with `DrawTitles = false`) and 16 units under the last row of its key, or room for its source line: `Height` is not used, and may be anything from 16 to 2160. `Render(includeLegend: true)` adds no series legend under it, and the component draws no legend of toggle buttons for it, since its key names its parts and its one series cannot be hidden; `ShowLegend` changes nothing there. It refuses more than one series, panes, a missing, negative or not-a-number amount, a blank label, all-zero amounts, more than 24 parts (`ChartValidation.MaxStripParts`), annotations, zones, value labels, a series' own kind, secondary axis, trend, zones, gradient, change colours or end label, and every axis setting (a time, log or reversed axis, bounds, `IncludeZero`, a right-hand axis, tick labelling or ticks set by hand, minor gridlines, an X format); a width under 320 is refused as for any chart. CSV writes its parts as rows. In JSON, `"kind":"Strip"`.
- **`ChartSpec.BarTrack`**: on a `Bar` or `Column` chart, a track behind each bar from the value axis's minimum, zero, to its maximum, `YMax`, in the style's `Grid` colour and rounded as the bar is (2-unit corners, or the far end rounded by `BarRadius`), drawn with the data, outside the bar's mark, so it is neither focused nor named. It needs `YMax` and an axis from zero (`YMin` unset or 0, and no value below zero), and every series on the left-hand axis; a stacked column chart and the other kinds refuse it. A value above `YMax` is drawn at the track's end, and its name says so, `Score: Effort, 104, above the scale, drawn at 100`, while its value label keeps its value. On a horizontal bar chart a bar on a track is at most 18 units thick, centred in its row; with `ValueLabels` its label stands 6 units past the track's end, in the text colour, and the right margin grows from 30 to hold the widest label and its note; the left margin fits the widest category name, by the library's generous estimate for 12-pixel text, with 12 units each side of it (18 more where `XLabel` titles the categories up the left), up to 45 % of the width, a longer name cut with `…`, its whole in its bar's name, instead of the fixed 160; and with nothing written under the plot, `YTickLabels = TickLabels.None` and no `YLabel`, the bottom margin narrows from 76 to 24, or 36 above a source line. On a column chart each track stands upright from zero to the top of the plot, and a value label stands above the track's top. Tracks stand over zone bands and references behind the data; draw a target `InFront`. An empty `YTickValues = []` leaves out the gridlines, so no dotted line crosses the tracks whatever the style. In JSON, `"barTrack":true`.
- **`ChartSpec.DrawTitles`**, on by default: off, neither the `Title` nor the `Description` is drawn, and the body moves up into their room, 50 units, a plot from 28 rather than 78, a strip's bar from 14 rather than 64 and its drawing 50 units shorter; both stay the drawing's `<title>`, `<desc>` and accessible name. Every chart kind takes it; a sparkline draws neither in any case, and network graphs (`GraphSpec`) always draw their title. It is not what `ChartSvg.Render`'s `includeTitles` does: that writes or leaves out the native tooltip, the `<title>`, in each mark, and nothing else. In JSON, `"drawTitles":false`.

A strip's share is of its own total, in whole percentages that add up to 100, so pass raw amounts, seconds say, rather than shares the app has already rounded: rounded shares that add up to 99 or 101 are shared out again over their total and can differ from the amounts by a point. From 0.40.0 a part whose amount, in `YFormat` and `YUnit`, writes exactly as its share is named once, `Moderate: 30%` rather than `Moderate: 30%, 30%`; one that differs keeps both, `Hard: 34%, 33%`.

Both settings are left out of the hash that names gradients at their defaults, so every chart drawn before keeps its IDs and its drawing. Limits: a strip is one whole, at most 24 parts, its bar always 18 units thick; it has no labels on the bar itself, no second strip beside it to compare and no target marker, and a sliver of a part is drawn 1 unit wide rather than to scale. A track runs from zero to `YMax` only, and a bar on a track has no target tick or zones of its own. The Sports & performance page's Latest session section draws its time in zone as a strip and three illustrative session scores on tracks, and the Claude Code skill's `references/recipes-race-face.md` gives "Effort zones" and "Score bars" as recipes.

### Lap columns and best efforts: columns filled by value and a second label line

0.40.0 fills columns and bars with a gradient by value, as a lap chart colours each lap by how hard it was, and writes a second line under each category's name, as a best-efforts card writes watts per kilogram under each duration.

```csharp
var laps = new ChartSpec {
    Title = "Heart rate per lap", Kind = ChartKind.Column, Width = 340, Height = 260,
    Series = [new("Heart rate", [new(0, 152, "L1") { SubLabel = "152 bpm" }, new(1, 161, "L2") { SubLabel = "161 bpm" },
        new(2, 168, "L3") { SubLabel = "168 bpm" }, new(3, 174, "L4") { SubLabel = "174 bpm" }])
        { Gradient = [new(0, "#A88200"), new(174, "#DD4B45")] }]                  // gold at the base, red at the hardest lap
};
```

- **`ChartSeries.Gradient` on columns and bars.** A series drawn as columns, on a column chart or as a column series on another, and the bars of a horizontal bar chart, take a gradient by value: one gradient laid along the value axis in the plot's own coordinates (`gradientUnits='userSpaceOnUse'`), up the series' own axis for columns, the right-hand one included, and across the plot along X for bars, each stop at its value's position, so every column shares it and takes at each height the colour of the value drawn there; a taller column reaches further along it, and past the first and last stops their colours carry on. A point's own `Color` still fills its column flat. Value labels stay in the text colour, and the legend key, in the chart and in the component, shows up to four of the stops' colours. The classic finish draws the same. A stacked column chart refuses it, since its colours tell the stacked series apart, as do a faded `Fill` and `Zones` beside it; columns' axes already refuse log and reversed scales. The stops are colours of a filled mark: choose ones that clear 3:1 against the background at every stop. In JSON, `"gradient":[{"value":152,"color":"#A88200"},{"value":174,"color":"#DD4B45"}]` on a column series.
- **`ChartPoint.SubLabel`**, at most 16 characters on one line: a second line under the point's category name, in the muted colour at 11 px. On a column or stacked column chart it stands 14 units under the name, cut with `…` past 12 characters as the name is, and the plot gives up 14 units at its foot for it, the X axis title moving down with it, only when some point has one, so a chart without sub-labels draws as before. On a horizontal bar chart the name moves up half a line and the sub-label stands half a line under the bar's centre, rows thin at 38 units rather than 24, and on tracks the left margin fits the wider of the two. Column names with sub-labels are thinned by the room their words take, the wider of the two lines keeping neighbouring columns 8 units apart, in either finish, rather than by the fixed 65 units a column chart's names otherwise keep: at 340 units four columns keep `152 bpm` each, and about eight keep a number alone. A category takes the first sub-label any series gives it, so every mark in it says the same words; series may repeat it or leave it null, and two different sub-labels for one category are refused. Each mark's name and tooltip say it after the name, `Heart rate: L3 · 168 bpm, 168`, as do the component's status line and data table. Line, area, scatter and the other kinds write tick labels or no category names, so they refuse it, as does a sparkline; it is refused blank, past 16 characters or across lines. In JSON, `"subLabel":"152 bpm"`.
- **End labels give up their note first.** Where an end label and its note do not fit the margin, the note is cut with `…`, then left out where not even its first letter fits, and only then is the label cut; the whole stays the text's tooltip and accessible name as before. A name that fits alone is no longer drawn `Rider name…` when only its note was lost.

`SubLabel` is left out of the hash that names gradients while it is null, so every chart drawn before keeps its IDs and its drawing. Limits: a sub-label is one line, at most 16 characters, and CSV does not carry it; a horizontal bar chart's names and sub-labels share one rule of 38 units a row; a gradient colours by the value axis only, so the base of every column shares the first stop's colour, and it has no contrast check of its own. The Sports & performance page draws Heart rate by lap in its Latest session section and Best efforts in its Fitness section, and the Claude Code skill's `references/recipes-race-face.md` gives "Heart rate per lap" and "Best efforts" as recipes.

### Fits a card: rows that set the height, an unpainted background and averages made by the app

0.41.0 adds three settings for charts that live on a phone card, none of which moves any existing drawing.

```csharp
var meters = new ChartSpec {
    Title = "Race scores", Kind = ChartKind.Bar, Width = 340, YMin = 0, YMax = 100, BarTrack = true,
    YTickLabels = TickLabels.None, DrawTitles = false,
    FitHeight = true,                                                    // 28 + 3 × 36 + 24 = 160 units tall
    PaintBackground = false, Style = ChartStyle.Light with { Background = "#F3F6FB" },   // the card's own colour
    Series = [new("Score", [new(0, 82, "Execution"), new(1, 64, "Improvement"), new(2, 91, "Effort")]) { ValueLabels = true }]
};
var power = new ChartSeries("Power", buckets) { AverageOf = "12 s" };   // points the app averaged itself
```

- **`ChartSpec.FitHeight`**, horizontal bar charts only: the chart is drawn as tall as its rows need instead of `Height`. Each category takes 36 units on tracks, 32 without, and 38 where any category writes a sub-label, so names never collide; round the rows the chart keeps the room it draws in: 78 units above for the title and description (14 more for a description on two lines, 28 in all with `DrawTitles` off), 76 under them for the value axis's ticks and title, or 24 where neither is written (36 above a source, 14 more for a second source line), and 22 a row for the legend `Render` includes. No 240-unit floor applies: one tracked meter without titles, ticks or source is 88 units tall. `Height` is still checked, 240 to 2160, and otherwise unused. The component honours it, its `FitWidth` scaling unchanged; other kinds refuse it.
- **`ChartSpec.PaintBackground`**, on by default: off, the root `<svg>` writes no background, and nothing fills the drawing, so the card it sits on shows through. Lumen still treats `Style.Background` as the colour the chart stands on: contrast is checked against it, and its halos and separators are drawn in it (value-label and end-label halos, references in front, hollow markers, a highlight's outline, a gauge's knob), so set `Style.Background` to the colour of the card. The root carries that colour as `--lumen-ground` instead, which the component's readout rings read for their halo. The SVG export has no background, and the PNG export is transparent where nothing is drawn. `GraphSpec.PaintBackground` does the same for graphs.
- **`ChartSeries.AverageOf`**, at most 16 characters on one line: says the series' points are averages the app made already, over `"12 s"` or `"a week"`. Each mark with a value ends its name and tooltip `, average of 12 s`; the shared readout says it once in a column's label, `1:02:30 · average of 12 s`, where every entry there shares it, its entries then reading plainly, and otherwise after each such entry's value, and the component's status line reads the same. Where Lumen averaged a mark itself under `SamplingMethod.Average`, its own words, `, average of 4 points`, stand. CSV and the data table keep the values. Series whose marks carry one value take it; candles, range bars, histograms, boxes, violins, timelines, calendars, donuts, gauges, rings and strips refuse it.

All three are left out of the hash that names gradients at their defaults, so every chart drawn before keeps its IDs and its drawing.

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

The component toolbar exports SVG, PNG and CSV. PNG is produced in the browser: the same SVG is serialized to a blob, loaded as an image, drawn into a canvas at twice the chart's pixel size over the chart's own background, or over nothing for a chart with `PaintBackground = false` (0.41.0), so its PNG is transparent there, and saved. The scale is capped so the longest edge stays within 8192 pixels. Text is rasterized with the fonts the browser has, so a host that needs an exact typeface must install or embed it. There is no server-side PNG or PDF rendering — that needs a rasterizer dependency, and these packages have none.

Interactive charts draw their own HTML tooltips: hovering or focusing a mark shows its label, Escape hides it. Those charts render with `includeTitles: false` so the browser's slow native tooltip does not compete with it:

```csharp
var interactive = ChartSvg.Render(spec, includeLegend: false, includeTitles: false);
var exported = ChartSvg.Render(spec);   // keeps a <title> on every mark
```

Static and server-rendered output keeps the native titles by default, so an exported SVG still explains every mark without JavaScript. `includeTitles` covers these per-mark tooltips and nothing else: a chart's own title and description are always its `<title>`, `<desc>` and accessible name, and are drawn at its top unless `ChartSpec.DrawTitles` is off (0.39.0). `LumenGraph` is unchanged and keeps native titles.

Bind application data with `ChartSeries.From("Revenue", rows, r => r.Month, r => r.Amount, r => r.Name)`. Replace the `Spec` parameter to update a chart. `ChartSvg.Render(spec)` and `ChartExport.Csv(spec)` work without a browser.

### Graphs

`LumenGraph` takes a `GraphSpec`, and `GraphEngine` exposes the geometry without SVG:

```csharp
IReadOnlyList<NodePosition> nodes = GraphEngine.Layout(spec);   // one position per node
IReadOnlyList<EdgeRoute> routes = GraphEngine.Routes(spec);     // polyline per edge, bends included
int crossings = GraphEngine.Crossings(spec);                    // in the drawing it produces
string svg = GraphEngine.Render(spec, positions);               // positions override the layout
GraphSpec phone = GraphEngine.Fit(spec, 375);                   // laid out for a box 375 pixels wide
```

Layered graphs assign longest-path levels, add one routing point per level a long edge spans, then run barycenter sweeps in both directions and keep the ordering with the fewest crossings. Circular graphs place nodes on a ring and report interleaved chords as their crossing count. The ring stands in from either side by half the widest node label, or a node's 23-pixel radius if that is more, and 24 pixels (0.31.0; before, a fixed 100), so its labels keep inside the drawing however long they are, and a graph of short labels uses more of its width. Both are deterministic: the same spec always produces the same drawing.

A node's label hangs under it, 12-pixel text whose baseline is 42 pixels below the centre, cut to 22 characters; the engine judges it as a box as wide as the library's generous estimate of the text and a line, 14.4 pixels, high. An edge leaves and reaches a node 25 pixels from its centre, clear of the circle, unless the straight run from the node towards the edge's next point would cross that box: then, in every layout (0.31.0), it meets the node at the foot of the label, 50 pixels below the centre, and a straight edge aims its other end at that foot, so an arrowhead arriving from below lands under the words rather than on them. An edge's label stands at the first free place along the edge as drawn (0.31.0): half its length first, then 0.4, 0.6, 0.3, 0.7, 0.25 and 0.75, at each place above the edge before below it, 9 pixels off it either way, or top to bottom beside it, on its right before its left. A place is free when the label's box, its estimated width by 12 pixels and 4 more all round, keeps off every node's circle, every node's label and every edge label placed before it, in edge order, and stays inside the drawing. Where no place is free the label stands where it always has, above the middle of the edge's middle stretch, or top to bottom beside it.

`GraphSpec.Direction` (0.30.0) runs a layered graph's levels `LeftToRight`, the default, or `TopToBottom`: in rows down the drawing between the same 90-pixel ends that left to right keeps across, each level's nodes and bends spread across the width in equal bands inside 24-pixel margins, with the same ordering, the same routing through bend points, and arrows pointing down. Top to bottom, an edge leaves its node from under the node's label rather than through it, an edge's label stands beside the edge instead of above it, on the left where the right would run off the drawing or is taken, and a self-loop stands at its node's right, since edges arrive from above. A circular graph ignores the direction.

`GraphEngine.Fit(spec, width)` returns the graph as it should be drawn in a box that wide, clamped to 320 to 4,096 pixels: the spec itself when it already fits at its own width and direction, so a drawing that fits never moves, and otherwise a copy at that width. Neighbours need room for the widest node label as drawn, at 12 pixels and cut to 22 characters, and 16 pixels more, or for a node's diameter, 46 pixels, and 24 more. A layered graph whose levels cannot stand that far apart side by side turns top to bottom and grows as tall as its rows need, 110 pixels a level, never shorter than its own `Height` and at most 2,160; one already set top to bottom stays so. When its fullest level, bends included, cannot stand that far apart across the width either, it takes the narrowest width that holds it, and the component scrolls. A circular graph keeps its circle, stood in from the sides by half its widest label and 24 pixels, and grows taller, up to 2,160, until neighbours round it stand that far apart; two nodes at one height, a node and its mirror image, which no height can part, need the same room, their labels 16 pixels apart and their circles 24 (0.31.0; before, only not touching), and a narrower width becomes the narrowest that has it. The gallery's pipeline of six levels turns top to bottom in a box of 632 pixels or less; on a phone it is drawn 337 by 730 top to bottom, or 337 by 460 as a circle, its own height, with Charts and Transform, side by side at the bottom, standing their labels 35.6 pixels apart by the estimate. A graph's own description, which names its nodes, connections, layout and crossings, goes on over a second line where the drawing is too narrow for it.

```razor
<LumenGraph Spec="graph" NodeSelected="OnNode" />

@code {
    void OnNode(string id) { /* the node's Id */ }
}
```

Dragging a node previews with a transform and commits on release; arrow keys nudge a focused node by eight units and Enter selects it. Moved positions are kept until the node set, layout or direction changes, and the toolbar's Reset layout restores the computed ones. `FitWidth="true"` draws the graph as `GraphEngine.Fit` lays it out for the width of its box, as under [Fitting the width it is shown at](#fitting-the-width-it-is-shown-at). Rendering itself stays static: `GraphEngine.Render(spec)` needs no browser, and the second argument accepts stored positions if your application persists them.

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
{"title":"Recovery","kind":"Gauge","gaugeSweep":270,"yLabel":"%","yZones":{"zones":[{"name":"Low","upper":33,"color":"#DD4B45"},{"name":"Moderate","upper":66,"color":"#A88200"},{"name":"Good","upper":"Infinity","color":"#2E9B58"}]},"annotations":[{"axis":"Y","from":61,"label":"7-day average"}],"series":[{"name":"Recovery","points":[{"x":0,"y":72,"label":"Recovery"}]}]}
{"title":"Activity","kind":"Ring","series":[{"name":"Move","goal":600,"points":[{"x":0,"y":540,"label":"kcal"}]},{"name":"Exercise","goal":30,"points":[{"x":0,"y":47,"label":"min"}]},{"name":"Stand","goal":12,"points":[{"x":0,"y":9,"label":"h"}]}]}
{"title":"Last night","kind":"Timeline","xFormat":"TimeOfDay","series":[{"name":"Light","points":[{"x":82800,"xEnd":84600}]},{"name":"REM","points":[{"x":84600,"xEnd":86220}]}]}
{"title":"Heart rate","kind":"Range","xAxis":"Time","series":[{"name":"Heart rate","points":[{"x":1789171200000,"y":74,"low":52,"high":168,"label":"12 Sep"},{"x":1789257600000,"y":70,"low":48,"high":150,"label":"13 Sep"}]}]}
{"title":"Sleep timing","kind":"Range","yFormat":"TimeOfDay","yReversed":true,"series":[{"name":"Sleep","points":[{"x":0,"low":82800,"high":109800,"label":"Mon"},{"x":1,"low":84600,"high":111600,"label":"Tue"}]}]}
{"title":"Training","kind":"Calendar","xAxis":"Time","timeZone":"Europe/London","yZones":{"zones":[{"name":"Easy","upper":50},{"name":"Hard","upper":"Infinity"}]},"annotations":[{"axis":"X","from":1789387200000,"label":"Race"}],"series":[{"name":"Stress","points":[{"x":1789387200000,"y":40,"label":"Ride"},{"x":1789390800000,"y":30},{"x":1789560000000,"y":20}]}]}
{"title":"September","kind":"Calendar","xAxis":"Time","calendarLayout":"Months","calendarCell":"Bubble","weekStart":"Sunday","series":[{"name":"Distance (km)","points":[{"x":1789387200000,"y":8.6},{"x":1789560000000,"y":14}]}]}
{"title":"Laps","kind":"Blocks","yFormat":"Duration","yReversed":true,"annotations":[{"axis":"Y","from":306,"label":"Average"}],"series":[{"name":"Laps","points":[{"x":0,"xEnd":2,"y":336,"label":"Lap 1"},{"x":2,"xEnd":4,"y":318,"label":"Lap 2"},{"x":4,"xEnd":5.5,"y":301,"label":"Lap 3"}]}]}
{"title":"Workout","kind":"Blocks","xFormat":"Duration","includeZero":true,"series":[{"name":"Plan","zones":{"zones":[{"name":"Easy","upper":187.5},{"name":"Tempo","upper":225},{"name":"Threshold","upper":"Infinity"}]},"points":[{"x":0,"xEnd":600,"y":150,"label":"Warm-up"},{"x":600,"xEnd":1080,"y":250,"label":"Interval 1"}]},{"name":"Power","kind":"Line","points":[{"x":0,"y":118},{"x":300,"y":152},{"x":800,"y":256}]}]}
```

Invalid chart semantics return HTTP 400 problem details. Malformed JSON is rejected by ASP.NET Core. The endpoints do not fetch URLs, execute supplied code, save submitted data, or contact outside services. Add application-specific authorization and rate limits when hosting publicly. The sample limits request bodies to 16 MiB.

## Accessibility

Measured by the regression suite, so a change that breaks one of these fails the build:

- Every data mark is a focusable element with an accessible name carrying its series, category and value — `Workspace: Sep, 60.3`. A series whose markers are hidden keeps each one as a transparent target, so its points are reached and announced as any others are; in the refined finish a line's or area's own markers are hidden that way until their point is hovered or focused, and focusing one shows it with the focus ring. Interactive marks use `role="button"`; histogram bins, box glyphs and the outliers of a supplied box summary, which are aggregates, use `role="img"`.
- A gauge's score and each ring are one such mark, named with the unit, the zone or the goal the chart shows — `Recovery: 72 %, Good`, `Move: 540 of 600 kcal, 90 %` — and a gauge's zones and targets are named aggregates, as zone bands and reference lines are.
- So is each span of a timeline and each range bar, named with its state, times and length or its two ends and average — `REM: 02:14 to 02:41, 27 min`, `12 Sep: 52 to 168, average 74`; a timeline's legend gives each state's total and share as text, not only as colour.
- So is each day with activity on a calendar, named with its date, total and zone — `Tue 15 Sep 2026: 54, Moderate` — while a day without activity is an empty cell that takes no tab stop; the key names the zones or reads the ramp's ends as text, and an outlined day is a named aggregate.
- So is each block, named with its label, its span and its height in the axes' formats and its zone — `Interval 2: 27:00 to 35:00, 250, Lactate threshold`, `Lap 2: 2 to 4, 5:18` — so a workout's structure and a run's laps are read without their colours or widths.
- Each chart and graph exposes its title and description as the accessible name of the drawing.
- Series colors and the zone ramp keep at least 3:1 contrast against both the light and the dark chart background, and Midnight's against its own, and every text color keeps at least 4.5:1, zone band labels included over their band's tint. Heatmap cells carry a hairline so the palest ones stay distinguishable.
- No element takes a positive tab index. The toolbar status is a live region, legend buttons expose `aria-pressed`, the data toggle exposes `aria-expanded`, and the data table has a caption with scoped column headers.
- Keyboard: each chart in `<LumenChart>` has one roving stop among its marks (0.36.0), besides its scrolling region's own: Tab reaches the region and then one mark — the first point of the first series, or the one last focused — and the arrow keys move from it, Left and Right along a series, stepping over its gaps, Up and Down to the nearest point at that X in the series before or after it (then its references and other aggregates), Home and End to the series' ends, and Page Up and Page Down ten points. Enter or Space selects a mark or node; Escape hides the tooltip or the shared readout; arrow keys nudge a focused graph node. The keys are named in hidden words that the component's script makes the region's description. The static SVG keeps every mark a tab stop of its own, so a page without the script stays readable. With `SharedReadout`, Left and Right move the readout from one X to the next and Up and Down between the series there, and the status line reads every series at that X.

Confirmed in a browser accessibility tree: each mark appears as a named button, the chart appears as a named group, and both status regions announce. Every continuous-integration run also sweeps both sample hosts with axe-core, and the gallery a second time in its dark theme, a third on its activity stream's panes and a fourth in its Midnight brand, restricted to the WCAG 2.0 and 2.1 A and AA rules, and fails on any violation.

Not done, and not claimed: no screen-reader run (NVDA, JAWS or VoiceOver), no WCAG conformance statement, and no testing with speech or magnification software. An automated sweep catches only what automation can see — roughly a third of the success criteria — so a clean axe run is a floor, not a certificate. Before 0.36.0 a chart with many marks produced many tab stops — 1,200 at the default sampling budget; in the component it is now one roving stop beside the scrolling region's own, and a static SVG, which has no script to move between its marks, keeps one for each.

## WebAssembly

`samples/Lumen.Wasm` is a standalone Blazor WebAssembly host that references the component package directly:

```powershell
dotnet run --project samples/Lumen.Wasm --urls http://localhost:5199
```

It sits outside `Lumen.Charts.slnx` so the solution build does not need the WebAssembly workload. The suite asserts that `Lumen.Charts` references only framework assemblies and that `Lumen.Charts.Blazor` adds only the Blazor component assemblies — nothing server-only, which is what makes a WebAssembly host possible at all.

Verified in the browser on 10 September 2026: the WebAssembly host loads the class library's static assets, renders the line, column, candlestick, histogram, box and donut samples, shows tooltips, raises point selection back into .NET, exports PNG through the canvas path, and drags graph nodes — the same behavior as the server gallery.

Getting there required a fix rather than a test. `Lumen.Charts.Blazor` previously declared `<FrameworkReference Include="Microsoft.AspNetCore.App"/>`, which asks for the ASP.NET Core shared framework. A WebAssembly host has none, so any Blazor WebAssembly project referencing the library failed to build with `NETSDK1082`, hunting for an ASP.NET Core runtime pack for `browser-wasm` that is not published anywhere. The library now references the `Microsoft.AspNetCore.Components.Web` package instead, which is how a component library serves both hosting models. Building a WebAssembly app also needs the `wasm-tools` workload installed.

## Supported behavior and limits

- Line, area, scatter, bubble, column, horizontal bar, signed stacked column, donut, heatmap, radar, candlestick, OHLC bar, uncertainty band, histogram, box plot, violin, score gauge, activity rings, state timeline, floating range bar, calendar of days, variable-width blocks, proportion strip. Lines, areas, columns, scatter points, bands, range bars and blocks can share one line, area, scatter, bubble, band, range, blocks or column chart, each series naming its own mark — blocks on the continuous kinds only — and can stand beside the candles of a candlestick or OHLC chart. Line, area, scatter, bubble, band, range, blocks, candlestick and OHLC charts stack up to six plots over one X axis.
- Linear, base-10 logarithmic and UTC time axes on X and Y, and an optional second Y axis on the right; category labels on categorical charts. Time and log X axes apply to line, area, scatter, bubble, candlestick, OHLC, band and range charts, a time axis to blocks, and to timelines and calendars, which require one; log Y applies to line, scatter, bubble, candlestick, OHLC, band, range, blocks, box and violin charts, because magnitude, count and radial charts need a zero baseline. Log axes reject zero and negative values. `XFormat`, `YFormat` and `Y2Format` write values as durations in seconds or as compact numbers on linear and log axes, and as the time of day on linear ones, never on a time axis; a linear duration axis steps by a second at the finest and rounds what it shows to the second, and histograms, donuts, heatmaps and radar charts take no format. `YReversed` and `Y2Reversed` apply to line, scatter, bubble, band, range, blocks, candlestick, OHLC, box and violin charts, and only Y axes reverse. Time values must be Unix milliseconds between year 1 and year 9999, and `TimeZone` decides the calendar they are read in. `SkipWeekends` and `TimeSkips` compress a time axis over spans it should not draw, at most 400 listed spans per chart; the axis stays piecewise proportional, so a gap in the data itself still reads as a gap. Irregular tick placement is not implemented.
- `MinorGridlines` adds lighter lines between the labelled ticks: four or five divisions per interval on a linear axis depending on its step, the mantissas between decades on a logarithmic one, and none on a time axis, because half of a month is not a boundary anyone reads. Off by default.
- Explicit limits via XMin/XMax/YMin/YMax. Bars and areas enforce a zero baseline. Null Y preserves gaps in lines/areas and is omitted elsewhere.
- Line/area min/max sampling preserves original indices and extrema per continuous run; this is not a total chart-wide point budget. `SamplingMethod.Average` (0.37.0) draws slice means instead, and a run longer than the budget is thinned over the X range shown. CSV always exports original observations.
- Up to 100,000 input points, 32 series; 100 categories/slices. Sampling holds a line or area chart at its mark budget, so the browser cost is the same for 1,000 points as for 100,000: about 33 ms either way on the machine in [the measurements](docs/PERFORMANCE.md). Scatter and bubble render every point by default, which is comfortable to about 10,000; 50,000 points means 150,000 DOM elements and 12 MB of markup, and 100,000 means 300,000 elements and 25 MB. A scatter chart can set `DensityCells` to aggregate instead, which takes 100,000 points to 6,504 elements and 33 ms. Bubble has no equivalent, because binning would destroy the size encoding. There is no GPU acceleration and no million-point claim.
- Bubble area is proportional to Size across all series. Radar requires complete, nonnegative series on common categories. Donut accepts one nonnegative series.
- A gauge shows one score: several scores are several gauges. It has no needle style, no second value such as yesterday's score on an inner arc, and no target range drawn on the arc — `YZones` shade ranges and Y annotations mark single values; a target label with no room clear of the score is left out. Ring charts take one to six rings with a centre left empty, a track at a fixed fifth of the ring's strength, one colour per ring rather than Apple's gradient along it, and progress drawn to 300 % at most. The overlap shadow is three flat, faint discs rather than a blur, so that it rasterizes identically in every exporter. Neither animates. Gauge zones are tints rather than solid colour, so they read as the scale's background; the score's arc and the zone's name carry the information, and a zone's tint is not held to 3:1.
- A trend applies to series drawn as lines, areas, scatter points or bubbles; columns, bars, bands, ranges, blocks, timelines, calendars and the radial and derived kinds refuse it. It is one trend per series — a least-squares line, a moving average, a polynomial of degree 2 to 4 or an exponential — fitted over every observation in the series rather than the zoomed window, and it is not an observation: it raises no point selection, appears in no CSV export and adds no row to the data table. There is no logarithmic, power or logistic fit, no centred or weighted moving average, no confidence band round a fit and no extrapolation: a curve stops at the first and last X it was fitted to, and only the line spans the plot. A moving average needs its points in X order, and a scatter series out of order is refused one.
- Change colours compare consecutive values of one series by the sense it names, `HigherIsBetter` or `LowerIsBetter`; there is no threshold below which a change counts as level, no comparison with anything but the point before, and no third colour for level beyond the series colour. They take the style's `Rising` and `Falling` colours, which on the light and dark presets are also the second and fifth series colours, so a chart that sets change colours should give its other series colours apart from them. A line's or a scatter point's value label is placed by an estimate of its width, as a column's is, and keeps clear of other value labels but not of other series' lines or markers; a label with no free place above or below its point is left out. A mark's colour need only clear 3:1, and a value label is small text, so a line's or a scatter point's label takes its point's colour only where that colour clears 4.5:1 against the chart's background, and the style's text colour where it does not: on the light preset the first series' blue, 4.12:1, and the rising and falling colours, 3.44:1 and 3.38:1, all give way to the text colour, while Midnight's and the dark preset's rising and falling colours keep their labels. The labels are hidden from assistive technology, which reads each mark's name. X tick labels are not moved in from the drawing's edges as value labels are, so a labelled point at the plot's edge can still run its label past the drawing's edge; points by index take `XMin = -0.5`, `XMax = count - 0.5`.
- A sparkline draws line, area, scatter and column marks alone, with no axis, so it shows a shape and not a scale: the words beside it carry the numbers, and each point's value is in its tooltip and accessible name. It has no value labels, no panes, no other kinds and no last-value text of its own; its padding follows the largest mark it draws, so two sparklines of one size whose marks differ have plots of different sizes, and a drawing smaller than twice that padding cuts a ring at its edge. A highlight is a ring of one size in one colour per point; it does not change the point's name, so a highlighted point needs a note to say why. A minimum span is centred on the data and only ever widens the axis; it is not a fixed scale shared across sparklines, for which `YMin` and `YMax` remain.
- A chart's title, description and source are fitted to its width by the library's generous estimate of a text's width, not by the font the browser picks, so they wrap or cut a little early: a description a narrow font would fit on one line may take two. Only the description and the source wrap, to two lines at most, and only the title is cut; axis titles, tick labels, category labels, legend entries and a graph's node labels keep their own rules, and a graph's description its clause-by-clause wrap. A second line takes its 14 units from the plot, so on a short chart the plot is the shorter for it.
- `TickLabels.Bounds` writes an axis's exact ends, which need not be round numbers: an axis fitted to weights from 37.8 to 38.2 is labelled `37.8` and `38.2`. Its two labels along the bottom are anchored inward and not checked against each other, so on a plot narrower than both together they meet. `InFront` chooses between behind the data and over it, not an order among references: those in front stand over every series, in the order given, and a band in front tints the marks under it.
- A violin estimates its outline with a Gaussian kernel at Silverman's bandwidth, taking the smaller of the standard deviation and the interquartile range so one long tail cannot smooth the shape away. The estimate is drawn over the observed range and no further, so the outline claims no values the data never had, and it is computed in the space the axis draws in, so a logarithmic axis shapes the violin in logarithms. The widest point of each violin fills its column: widths are comparable within a chart but carry no units, and the quartile bar and median tick carry the numbers. A violin is an aggregate, like a histogram bin or a box: focusable and named, raising no point selection. A series with fewer than two observations, or with no spread, draws its quartile bar and median without an outline. The bandwidth is not configurable, and split or paired violins are not implemented.
- Candlestick and OHLC bar draw one series as candles or bars and take others beside it in kinds of their own, and a histogram accepts up to four series. Candlestick and OHLC bar take the same input: all four prices with High highest and Low lowest, colored by direction rather than by series. An OHLC tick is half the width of a candle body, so the two drawings of one dataset stand in the same columns and can be compared; volume goes beneath either as a column series in a pane. Band points need both bounds or neither. Histogram and box read observations from Y and ignore X. A histogram of several series bins them over one set of edges chosen from the pooled observations and stands their bars side by side; counts are raw, not normalised, so a larger series draws taller bars. A box series may instead carry a precomputed `Summary` and no points: it is drawn as given, claims no observation count, applies to box charts only, and its outliers count towards the 100,000-point limit. Histogram bins and box glyphs are labelled, focusable aggregates that report no observation index, so they raise no point selection; candlesticks, OHLC bars and box outliers computed from observations do, and the outliers of a supplied summary do not.
- Layered graphs use longest-path levels, then barycenter sweeps that keep the ordering with the fewest crossings found. This is a heuristic, not minimal crossings. Edges spanning several levels bend once per level and are drawn as smooth curves; there is no orthogonal routing, no force simulation and no automatic node overlap removal. Self-loops are allowed in layered graphs and draw as a loop on their node; longer cycles still need the circular layout. Nodes can be dragged or nudged with the arrow keys in the component, which needs an interactive render mode. At most 250 nodes / 2,000 edges; dense graphs can still overlap. An edge keeps out of its own nodes' labels, not out of other nodes': a long chord across a crowded circle, or a long edge bending through a crowded row, can still pass through a third node's label. An edge's label keeps off nodes, node labels and the edge labels before it, but not off other edges' lines, and where it finds no free place it stands where it always did, on whatever is there. A layered graph left to right at its own size can still stand two long labels at one height into each other; `GraphEngine.Fit` turns such a graph top to bottom, and it does not make room for edge labels.
- On a narrow screen a chart keeps at least 640 pixels and scrolls sideways in a keyboard-focusable viewport, so its labels stay legible, unless it sets `FitWidth`, which draws it at the width of its container, down to 320 pixels, with its text at its own size. A sparkline is drawn at its own width, never wider than its container, and in the component fits down to 60 pixels. A graph keeps the scrolling viewport too unless it sets `FitWidth`, which draws it at its container's width and turns a layered graph top to bottom where its levels cannot stand side by side; a graph whose fullest level needs more still scrolls.
- HTML tooltips on hover and keyboard focus in the component, native SVG tooltips in exported and server-rendered charts, keyboard-focusable data marks, point selection, tables, and accessible labels. See [Accessibility](#accessibility) for what is measured and what is not. This is not a claim of WCAG certification.
- SVG, PNG and CSV exports. PNG is rasterized in the browser from the same SVG, so it needs an interactive render mode; there is no server-side PNG or PDF rendering, 3D, or streaming transport yet.
- Research materials are excluded from packages. No vendor source code or book images are redistributed.

See [research and architecture](docs/RESEARCH.md), [verification](docs/VERIFICATION.md) and [measured performance](docs/PERFORMANCE.md). This is an original preview implementation, not a claim of feature or performance parity with mature commercial products.

## 0.41.0 additions

Fits a card: three settings from a race-results app's feedback, none of which moves an existing drawing. `ChartSpec.FitHeight` draws a horizontal bar chart as tall as its rows need, 36 units a row on tracks, 32 without and 38 with sub-labels, plus the title, axis and source it draws, with no 240-unit floor, so three meters on a phone card are 160 units tall rather than 240. `ChartSpec.PaintBackground` and `GraphSpec.PaintBackground`, off, leave the drawing's background unpainted so the card it sits on shows through, while contrast checks, halos and separators keep using `Style.Background`, which should then be the card's colour; the component's PNG export is transparent there. `ChartSeries.AverageOf` says a series' points are averages the app made, `"12 s"`: each mark's name and tooltip end `, average of 12 s`, and the shared readout and the component's status line say it once a column where every series there shares it.

Every chart drawn before renders as it did: v0.40.0's 360 hashed renderings match byte for byte in both finishes, `FitHeight` is left out of the hash that names gradients at its default, and `PaintBackground` and `AverageOf`, which change no gradient, are never part of it. Eight new renderings cover unpainted charts in light and Midnight, a line averaged by its app, fitted bars of one and four rows on tracks and without, and the score-bars recipe at 340 fitted to its rows. Limits: `FitHeight` takes horizontal bar charts only, its row pitch is fixed (several series share a row, so their bars thin), and in the component hiding the only series in a category drops its row; `PaintBackground` cannot know the surface's colour, so `Style.Background` must be set to it by hand; `AverageOf` is words only, so the data table and CSV keep the values without it.

## 0.40.0 additions

Lap columns and best efforts: the eleventh and thirteenth charts of a race-results app move onto Lumen as general features any app can use. `ChartSeries.Gradient` now fills columns, on a column chart or as a column series, and the bars of a horizontal bar chart, one gradient laid along the value axis so each column takes the colour of the value drawn at each height; a stacked column chart, a faded fill and zones refuse it. `ChartPoint.SubLabel` writes a second line under a category's name on column, stacked column and bar charts, thinned with its name and said in each mark's name, the component's status line and its data table. Two fixes: a strip part whose amount writes exactly as its share is named once, `Moderate: 30%`, and an end label that does not fit with its note gives up the note before its own letters. Described under [Lap columns and best efforts](#lap-columns-and-best-efforts-columns-filled-by-value-and-a-second-label-line). `"subLabel"` and a column series' `"gradient"` round-trip through the HTTP API's JSON.

Every chart drawn before renders as it did: v0.39.0's 351 hashed renderings match byte for byte in both finishes, `SubLabel` is left out of the hash that names gradients while null, and the component's markup is unchanged for a chart without sub-labels. Nine new renderings cover gradient-filled columns in light and Midnight and bars, sub-labels on columns and on bars, the lap and best-efforts recipes at 340 on a Race Face card, a strip whose shares are its amounts and an end label that gives up its note. The Sports & performance page gains Heart rate by lap and Best efforts. The Claude Code skill's `references/recipes-race-face.md` adds "Best efforts", with a Race Face power–duration curve, and "Heart rate per lap", and the "Effort zones" recipe says to pass amounts rather than rounded shares.

## 0.39.0 additions

Proportions and meters: the twelfth and fourteenth charts of a race-results app move onto Lumen as general features any app can use. `ChartKind.Strip` draws one series' parts as shares of one bar, parted by gaps, with a key under it of every part's whole percentage, adding up to exactly 100 by the largest remainder, each part a focusable mark named `Easy: 24%, 12:20`; it is drawn as tall as its content, and `ChartSvg.PartLabel` gives a part's words to any host. `ChartSpec.BarTrack` draws a track behind each bar of a bar or column chart from zero to `YMax`, a horizontal bar chart's value labels standing past the track's end in margins that fit its names and labels. `ChartSpec.DrawTitles` off keeps the title and description as the drawing's name without drawing them, and moves the body up into their room. Described under [Proportions and meters](#proportions-and-meters-strips-bar-tracks-and-titles-kept-unwritten). `"kind":"Strip"`, `"barTrack"` and `"drawTitles"` round-trip through the HTTP API's JSON.

Every chart drawn before renders as it did: v0.38.0's 338 hashed renderings match byte for byte in both finishes, the new properties are left out of the hash that names gradients at their defaults, and the component's markup is unchanged for every kind but a strip, which draws no legend of toggle buttons. Thirteen new renderings cover the strip in each preset and in light, Midnight, at 340 and 900, with a zone of none and as a cadence split, score bars on tracks at 340 in light and Midnight, columns on tracks and a chart that keeps its title unwritten. The Sports & performance page's Latest session section gains a Time in zone strip and Session scores on tracks, and the chart explorer a strip. The Claude Code skill's `references/recipes-race-face.md` adds "Effort zones" and "Score bars", and notes on the gap chart's readout names, riders who stop mid-race and a moving lead.

## 0.38.0 additions

Season arc and gap to the leader: the ninth and tenth charts of a race-results app move onto Lumen as general features any app can use. `ChartSpec.YTickValues` and `ChartPane.YTickValues` set an axis's ticks by hand, each an `AxisTick` with its own label or its value in the axis's format, as `Front`, `Mid` and `Back` on a reversed percentile axis. `ChartSpec.YUnit` and `ChartPane.YUnit` write a unit after every value the axis writes, ticks, names, value labels, the readout and the component's status line and table, so a gap reads `+12.3s`; `Axis.Unit` writes it for any host. `ChartSeries.EndLabel` and `EndNote` name a line at its end, in its colour where that clears 4.5:1, moved apart where lines end close together, in a right margin that grows to hold them, and said in the last point's name. `<LumenChart>` takes `ShowLegend` and `ShowToolbar`, both on by default; without its toolbar the chart keeps its status line, out of sight. Described under [Season arcs and gaps](#season-arcs-and-gaps-ticks-set-by-hand-units-and-end-labels) and [Blazor integration](#blazor-integration). `"yTickValues"`, `"yUnit"`, `"endLabel"` and `"endNote"` round-trip through the HTTP API's JSON.

Every chart drawn before renders as it did: v0.37.0's 331 hashed renderings match byte for byte in both finishes, the new properties are left out of the hash that names gradients at their defaults, and the component's markup with both its legend and its toolbar on is byte for byte as before. Seven new renderings cover ticks set by hand, a unit on a signed axis, eight end labels ending within a whisker, and an invented season's arc and an invented race's gaps to the leader at 340 in light and Midnight. The Sports & performance page's Racing section gains a Season arc and a Gap to the leader card. The Claude Code skill's `references/recipes-race-face.md` adds "Season arc" and "Gap to the lap leader", and notes on SignalR's message size for a large spec on an interactive island, on keeping decimals in a channel's points, and on `ShowLegend` and `ShowToolbar` for phone cards.

## 0.37.0 additions

Ride channels: the seventh chart of a race-results app, a ride's channels stacked one above another, moves onto Lumen as general features any app can use. `ChartSpec.Sampling = SamplingMethod.Average` draws a long line or area as the means of equal slices of the X range shown, the same slices for every series, each average named `, average of 12 points`; a run longer than `MaxRenderedPoints` is now thinned over the X range shown, with the nearest point outside each side, so zooming in shows more of its detail; and the shared readout reads the points the chart draws, so a four-hour ride reads at most one X a mark. A chart takes six plots, five panes under the main one. `ChartPane.YTickLabels` labels a pane's ticks its own way, and `TickLabels.None` writes none. `ChartSpec.PaneTitles = PaneTitlePlacement.Above` names each plot on one line above it. In `<LumenChart>` a drag across the plots with a mouse or a pen zooms to the stretch it covers, and `ChartSvg.Plot` gives any host where the plots and their X axis stand. Described under [Ride channels](#ride-channels-long-streams-and-plots-named-above). `"sampling"`, `"paneTitles"` and a pane's `"yTickLabels"` round-trip through the HTTP API's JSON.

Every chart drawn before renders as it did: v0.36.0's 325 hashed renderings match byte for byte in both finishes, and the new properties are left out of the hash that names gradients at their defaults, so no gradient ID moves. A run within the budget is drawn whole, outside a zoomed window too, as before. Six new renderings cover an averaged line, a zoomed line thinned by minimum and maximum and by average, a pane without tick labels, and an invented ride in six plots named above them at 340 by 640 in light and Midnight. The Sports & performance page gains a Long ride section with Ride channels, an invented two-hour ride in six plots. The Claude Code skill's `references/recipes-race-face.md` adds "Ride channels", and its docs now say that an interactive chart has a roving point stop beside its viewport's own, that an app which computes its training load draws those values, and how to test the keys.

## 0.36.0 additions

Reading a chart day by day: the eighth chart of a race-results app, a rider's fitness, fatigue and form, moves onto Lumen, and every chart gains keyboard reading, each as a general feature any app can use. In `<LumenChart>` every chart is now one tab stop, and the arrow keys move between its points — Left and Right along a series, stepping over gaps, Up and Down to the series beside it, Home and End to its ends, Page Up and Page Down ten points — the keys named in the chart's description; a network graph's arrow keys still move its node. `ChartSpec.SharedReadout` reads every series at once at one X in the component: a guide through every pane at the X nearest the pointer, a tap or the focused point, a ring round each series' point there, one tooltip that reads the X and then each series in legend order with its notes and change words, the arrow keys stepping it by X, and the status line reading the same words. It never touches the SVG, and `ChartSvg.Readout` gives the same table to any other host. `ChartSpec.YSymmetric` and `ChartPane.YSymmetric` hold a Y axis symmetric about zero, at least the value given either way, and `ValueFormat.Signed` writes `+5` and `−5`. Described under [Keys and the shared readout](#keys-and-the-shared-readout), [Axes](#axes), [Sparklines](#sparklines) (beside the minimum span) and [Accessibility](#accessibility). `YSymmetric`, `"Signed"` and `sharedReadout` round-trip through the HTTP API's JSON.

Every chart drawn before renders as it did: v0.35.0's 318 hashed renderings match byte for byte in both finishes, the new properties are left out of the hash that names gradients at their defaults, and `SharedReadout` is never part of it, so no gradient ID moves, and `ChartSvg.Render` draws the same SVG with a shared readout or without one. Seven new renderings cover a symmetric, signed axis with its data inside it, past it and reversed, signed value labels and an annotation, columns on a symmetric axis, and a fitness and form chart in two panes at 340 by 420 in light and Midnight. The component's markup gains one hidden element, the words that name the keys. The Sports & performance page's performance chart is redrawn in two panes, form beneath on a symmetric, signed axis, with the shared readout; the home page's first chart misses one month of one plan, so its gap shows; and the WebAssembly host adds a fitness and form chart with the shared readout. The Claude Code skill's `references/recipes-race-face.md` adds "Fitness & form", with the app's 7, 28, 90 and 365-day ranges sliced by the host, and the recipe check now also renders the charts a recipe's own `ChartSpec` function makes.

## 0.35.0 additions

How the field finished, and text that fits: the fifth chart of a race-results app moves onto Lumen, and its feedback on 0.33.0 — descriptions cut off at 340 pixels — is answered, each as a general feature any app can use. A block above the bottom of its axis is now drawn at least 2 pixels tall, so a histogram of bins counted elsewhere, drawn as blocks, never loses a bin of one, and a bin of none stays named and focusable. `ChartAnnotation.ShowValue = false` draws a reference's label alone, `median`, its tooltip and name still reading `median: 47:12`; `ChartAnnotation.InFront` draws a line or band over the data, a line on a halo of the background colour, so bars and blocks cannot hide it. `ChartSpec.XTickLabels` chooses the X axis's labels as `YTickLabels` does the Y axis's, and the new `TickLabels.Bounds` labels either axis at its two exact ends, `35:00` and `1:20:00`. A description or a source too wide for the drawing goes on over a second line — between its clauses where both then fit, else as evenly as its words allow — and the plot gives it 14 units; past two lines it ends in `…`, and a title too wide is cut with `…`, the whole of each kept in the SVG's title, desc and accessible name. Described under [Text that fits](#text-that-fits), [Axes](#axes), [Annotations](#annotations) and [Blocks](#blocks). All of it round-trips through the HTTP API's JSON.

A chart that uses none of this, and whose words fit, renders as before: 308 of 0.34.0's 310 hashed renderings match byte for byte in both finishes, and the new properties are left out of the hash that names gradients at their defaults, so no gradient ID moves. Two rows moved by design, `race/recommended-340-light` and `race/recommended-340-midnight`, whose description, 309 units by the library's estimate in a drawing that leaves 292, now takes two lines. Eight new renderings cover the finish-time histogram at 340 by 240 in light and Midnight, the same chart as a 1080 by 1350 card, bounds on a Y axis, a target in front of columns, and at 340 a description and a source too long for one line and a title too long for the drawing. The Sports & performance page's Racing section gains How the field finished: an invented field of 52 for the last race in bins by the page's own rule, the athlete's bin red and noted, the median a dashed line over the bins, and the finishers off the chart in its source line. The Claude Code skill's `references/recipes-race-face.md` adds the recipe, with the app-side bin rule as caller code and a 1080 by 1350 social card, and notes that results from different competitions go in a series each, since change colours compare within one series.

## 0.34.0 additions

Sparklines: the next two charts of a race-results app move onto Lumen, a run of personal bests and a growth log, each built as a general feature any app can use. `ChartSpec.Sparkline` draws a line, area, scatter or column chart's data alone at the size of a word — no title, axes, gridlines, legend or any other text, a plot filling the drawing but for the room its largest ring needs, shown at its own width — while its title and description stay its accessible name and every point its focusable, named mark and native tooltip; a sparkline may be as small as 60 by 16. `ChartPoint.Highlight` rings one point of a line or scatter series in a colour of its own, whatever the markers, leaving the line its colour; paired with a `ValueNote` it says in words why the point is ringed. `ChartSpec.YMinSpan` and `ChartPane.YMinSpan` hold a Y axis at least so tall, centred on its data, so a small wobble reads as small. In `<LumenChart>` a sparkline is its drawing and its tooltips alone, and its tooltip stands above the drawing at the width its words need. Described under [Sparklines](#sparklines). All of it round-trips through the HTTP API's JSON.

A chart that uses none of this renders as before: the 303 hashed renderings of 0.33.0 match byte for byte in both finishes, and the new properties are left out of the hash that names gradients at their defaults, so no gradient ID moves. Seven new renderings cover a personal-best sparkline in light and Midnight at 120 by 32, a growth sparkline on an 8 kg span at 270 by 54, a sparkline of columns, highlights on a full line and on scatter points, and a minimum span on a full chart's main plot and in a pane. The Sports & performance page gains a Getting faster? card: the athlete's fastest 5 km each week and fastest kilometre in each session of repeats as sparklines with each best ringed and noted, and an invented weekly weigh-in in grey alone on an 8 kg scale, each beside its numbers. The Claude Code skill's `references/recipes-race-face.md` adds the personal-best and growth-log sparklines, and `references/sports.md` a personal-best sparkline beside its step line.

## 0.33.0 additions

Race results on a line: the first two charts of a race-results app's move onto Lumen, each built as a general feature any app can use. `ChartSeries.ChangeColors` and the new enum `ChangeColors` colour each point of a line or scatter series, and the segment arriving at it, in the style's rising colour when it beat the point before and its falling colour when it did worse, by the series' own sense of better, and name the change in every mark's tooltip and accessible name. `ValueLabels` now writes values on lines and scatter points, in the point's colour where it clears 4.5:1 as text and the text colour where it does not, moved in from the plot's edges and below the point where there is no room above. `ChartPoint.ValueNote` writes a short note after a value — a field size, a status — in the label, the name, the component's table and status line, and a CSV `Note` column. `ChartSpec.XTicks` and the new enum `TickSource` choose what labels a continuous X axis; at its default a time axis keeps its dates where points' labels would be cut, so a round's long name stays in its tooltip. Described under [Race results on a line](#race-results-on-a-line) and [Axes](#axes). All of it round-trips through the HTTP API's JSON.

A chart that uses none of this renders as before: the 288 hashed renderings of 0.32.0 match byte for byte in both finishes, columns' and bars' value labels among them, and no time-axis row moved, because none labels its points with names longer than twelve characters; the new properties are left out of the hash that names gradients at their defaults, so no gradient ID moves. Fifteen new renderings cover the race season as the hand-built chart draws it and as two panes, each at 900 and 340 pixels in light and Midnight, the season of rounds on a time axis at 340 in Midnight, change colours on a line and a step across a gap in each theme, value labels at the plot's edges and top beside scatter points, and a time axis under long labels with each tick source. The Sports & performance page's HRV chart becomes one series coloured by status with a note naming it, under its seven-night moving average, and a Racing section adds the race results in two panes. The Claude Code skill adds `references/recipes-race-face.md`: a dark brand style from design tokens that passes `ContrastIssues()`, the race chart faithful and recommended, the season of rounds, the season strip's rule, and how each renders statically at a phone's width.

## 0.32.0 additions

Trend families, the regression item the original roadmap left. `ChartSeries.TrendFit` and the new enum `TrendFit` draw a series' trend as a moving average, a polynomial or an exponential as well as the least-squares line; `TrendPoints` sets a moving average's window, 7 points unless set, and `TrendDegree` a polynomial's degree, a quadratic unless set. Each is fitted in the space the chart draws, as the line always was, so an exponential is straight on a logarithmic axis and a moving average there is the geometric mean; a polynomial and an exponential are drawn only across the X they were fitted to, and a moving average breaks where less than half a window is present. Each is dashed, named for assistive technology — `HRV trend: 7-point moving average`, `Throughput trend: quadratic fit, R squared 0.99`, `Latency trend: exponential fit, rising, R squared 0.99` — and refused without `Trend = true`. `Statistics.Polynomial` and `Statistics.Exponential` give hosts the numbers, precise with X in Unix milliseconds. Described under [Trend lines](#trend-lines). The properties round-trip through the HTTP API's JSON, the fit as a string.

A chart that uses none of this renders as before: the 275 hashed renderings of 0.31.0 match byte for byte in both finishes, a trend line among them, and the three new properties are left out of the hash that names gradients at their defaults, so no gradient ID moves. Thirteen new renderings cover a moving average, a quadratic and an exponential in each theme, a cubic and a quartic, an exponential on a logarithmic axis, a moving average across gaps, a cubic on a trading axis, and a load test and nightly HRV in Midnight. The gallery's chart explorer adds a scatter view, Curved fits, of a simulated load test whose throughput takes a quadratic and whose latency, on the right-hand axis, an exponential, beside the scatter's least-squares lines; the Sports & performance page keeps its HRV chart as it was, three status series whose names carry each night's status to assistive technology; one series coloured by status would say it by colour alone, so the page waits for a way to name each night's status before it draws the average there.

## 0.31.0 additions

Graphs that keep their words clear, the open item 0.30.0 left for the circular layout. An edge whose straight run from a node would cross that node's label now meets the node at the foot of the label, in every layout, as every edge leaving a node top to bottom already did. On the home page's circle, Ingestion's edge down to Validation leaves from under its label and Charts' and Chart API's edges reach Reports under its label, where they ran through the words; on a phone Validation's edge down to Transform leaves from under its label too. An edge's label stands at the first place along its edge that keeps off every node, every node's label and the edge labels placed before it, trying above the edge before below it, so the audit trail, which printed on the Sources label, stands under its edge. A circle stands in from the sides by half its widest label, or a node's radius, and 24 pixels instead of a fixed 100, and `GraphEngine.Fit` gives two nodes at one height the 16 pixels between their labels that neighbours get, where it gave them only room not to touch: on a 337-pixel phone the home page's circle keeps its own 460 pixels of height, where it grew to 587, and Charts and Transform at its foot stand their labels 35.6 pixels apart by the library's estimate, where they stood 1.9. Described under [Graphs](#graphs).

No API changed. Graph drawings move by design, and nothing else does: of the 269 hashed renderings of 0.30.0, the 249 that are not graphs match byte for byte in both finishes, and all twenty graph rows change in both, every circular one because its circle widened, every layered one because the label of its long labelled edge, which bends, now stands at the middle of the drawn edge rather than at the middle of its middle stretch between bends; no layered row's edge ends moved. Six new renderings cover the gallery's pipeline in each layout at its own 900 pixels and fitted to a 337-pixel phone, and a crowded graph of ten long labels in each layout, where some labels find no free place. The browser suite's phone check now also walks every edge of the home page's graph and finds none running through a node's label and no edge label on one, in either layout; the same measure finds five such crossings on 0.30.0's phone circle, and on its 937-pixel circle three and the audit trail on Sources.

## 0.30.0 additions

Two changes, one from each of the open items 0.29.0 left. A calendar drawn without `YZones` now ramps from a third of the way between an empty day's grid colour and `HeatmapHigh` up to `HeatmapHigh`, as GitHub's contribution grid steps up from its empty cell, in its days, dots and bubbles alike and in the key under it and the component's legend key. Its quietest day stood at 1.00:1 against a rest day on the light preset, darker than it on Midnight and, on the dark preset, the brightest cell of all; it now stands 1.53:1, 1.62:1 and 1.29:1 apart, on the side towards `HeatmapHigh`, so the ramp reads on every style. Calendars no longer use `HeatmapLow`; heatmaps are unchanged, and tiers remain the choice where tiers mean something. Described under [Calendars](#calendars).

`LumenGraph` takes `FitWidth`, so the home page's network graph no longer scrolls sideways on a phone. Fitting width alone could not work: the gallery's six levels would stand 36 pixels apart at 360 pixels, closer than a node is wide. So `GraphSpec.Direction` and the new `GraphDirection` lay a layered graph out `TopToBottom`, in rows with each level spread across the width, and the new `GraphEngine.Fit` decides, for the width a graph is shown at, whether it turns, how tall it grows and, where a level cannot fit even top to bottom, the narrowest width it needs. A circular graph grows taller instead. A node the reader drags is held in proportion to the drawing and returns to the layout when the graph turns. Described under [Graphs](#graphs) and [Fitting the width it is shown at](#fitting-the-width-it-is-shown-at). The direction round-trips through the HTTP API's JSON as `"direction":"TopToBottom"`.

A chart that uses none of this renders as before: of the 256 hashed renderings of 0.29.0, the 249 that are not calendars without zones match byte for byte in both finishes, every graph among them, and the seven calendars on the ramp change in both, as they were meant to. Thirteen new renderings cover graphs set top to bottom in both layouts and themes, which a circular graph ignores, and graphs fitted to 360 and 1,200 pixels, the gallery's pipeline among them, and once in Midnight. The gallery's home page sets `FitWidth` on its network graph.

## 0.29.0 additions

Variable-width blocks, the eighth and last step of the build order in [FITNESS.md](docs/FITNESS.md), described under [Blocks](#blocks). Five of the fourteen apps researched there draw splits and laps, Strava sizing each lap by its length, and three draw a structured workout's steps as blocks, TrainingPeaks and Zwift colouring them by zone with the ride over them. `ChartKind.Blocks` and the new `ChartPoint.Block` draw each point as a block exactly from its `X` to its `XEnd`, standing on the bottom edge of its plot and rising to its `Y`: from zero on an axis that includes it, and from past the slowest pace on a reversed one, where an axis fitted to the data now reaches far enough below its lowest block for that block to keep a height. Neighbours in a series are a hairline apart and each block's far end is rounded. As a series' own kind, blocks draw in the column layer of a continuous chart, under its lines, in any pane and on either axis. They take series zones and point colours, are focusable, named marks that raise `PointSelected`, zoom and pan with the component, carry `XEnd` into CSV, and round-trip through the HTTP API's JSON as `"kind":"Blocks"` and `"xEnd"`.

A chart that uses none of this renders as before: the 243 hashed renderings match 0.28.0 byte for byte in both finishes. No spec property was added, so the hash that names gradient IDs is untouched, and a unit check rebuilds five rows of 0.28.0's baseline, one of them defining a gradient, and matches their hashes in both finishes. Thirteen new renderings cover the kind in both themes with and without native tooltips, six laps on a reversed pace axis with their average, a threshold workout in Coggan's levels with the ride over it, blocks beside a line, blocks touching and apart with one too narrow for the hairline, blocks in a pane, on a time axis in colours of their own and on the right-hand axis, and the laps and the workout in Midnight. The gallery's chart explorer adds a threshold workout in Coggan's power levels with the ride's power over it, and the Sports & performance page adds the latest run's laps to its Latest session section, split where the activity stream marks the steps of the progression and sharing the elevation's distance axis, and the next session as planned to its Training load section, its stress the performance chart's projected column for its day.

## 0.28.0 additions

A calendar layout, the seventh step of the build order in [FITNESS.md](docs/FITNESS.md), described under [Calendars](#calendars). Six or seven of the fourteen apps researched there draw a training calendar: Strava's training log, Bevel's month grid, Apple Fitness's rings by day, WHOOP's days coloured by recovery, Peloton's and Atoms's contribution grids and TrainingPeaks' compliance colours. `ChartKind.Calendar` draws one series of days on a time axis, counted in the chart's zone and each day's points added together, as a contribution grid of weeks or, with `ChartSpec.CalendarLayout = CalendarLayout.Months`, a small grid for each month, the weeks starting on `ChartSpec.WeekStart`, Monday unless set. `ChartSpec.CalendarCell` draws each day as a rounded square, a dot or a bubble sized by its value. A day takes its zone's colour from `YZones`, or a colour on the style's heatmap ramp; a day without activity is an empty cell; an X annotation outlines a day, such as today or a race; and the key shows the zones or the ramp's ends. Each day with activity is a focusable, named mark that raises `PointSelected`, and the component reads it in its status line. CSV carries each original point as other time charts do, and the spec round-trips through the HTTP API's JSON as `"kind":"Calendar"`, `"calendarLayout"`, `"calendarCell"` and `"weekStart"`.

A chart that uses none of this renders as before: the 229 hashed renderings match 0.27.0 byte for byte in both finishes. Gradient IDs are named after a hash of the spec, so the three new properties are left out of it at their defaults, as the gauge's sweep and the timeline's connectors are, and a unit check proves the hash against the serialization without them. Fourteen new renderings cover the kind in both themes with and without native tooltips, sixteen weeks of daily stress in tiers read in New York with today outlined, in the dark theme and in Midnight, three months of runs as bubbles on the ramp and in Midnight, dots, the ramp without zones in a duration format, a race outlined in its own colour, months starting on Sunday, and a year read in Johannesburg at a phone's width. The gallery's chart explorer adds sixteen weeks of simulated training as a contribution grid in tiers of daily stress and now sets `FitWidth`, so on a phone it fills the screen instead of scrolling sideways; the Sports & performance page adds a Training calendar to its Training load section, the season's daily stress — the performance chart's own — in four illustrative tiers beside this month's runs as bubbles sized by distance, today outlined in both.

## 0.27.0 additions

State timelines and range bars, the sixth step of the build order in [FITNESS.md](docs/FITNESS.md), described under [Timelines, range bars and the time of day](#timelines-range-bars-and-the-time-of-day). Five of the fourteen apps researched there draw a sleep hypnogram, and Apple Fitness, Garmin, Oura, WHOOP and Calm draw daily ranges and sleep timing as floating bars. `ChartKind.Timeline` draws one lane per state, each period a span made with the new `ChartPoint.Span` and its `XEnd`, joined across lanes by connectors that `ChartSpec.TimelineConnectors` can turn off, and its legend totals each state and gives its share. `ChartKind.Range` draws a capsule from a point's low to its high with its average as a dot, as a chart kind or as a series' own kind beside lines or in a column chart's slots, with no zero baseline, so it takes reversed and logarithmic axes. `ValueFormat.TimeOfDay` reads seconds since a midnight as the clock and wraps at 24 hours, so a night runs past midnight as one span; with `YReversed` it draws Oura's sleep timing. Spans and bars are focusable, named marks that raise `PointSelected`; the component zooms both kinds along X and keeps a hidden state's lane; CSV carries a span's `XEnd` and a bar's bounds; and all of it round-trips through the HTTP API's JSON as `"kind":"Timeline"`, `"kind":"Range"`, `"xEnd"`, `"timelineConnectors"` and `"yFormat":"TimeOfDay"`.

A chart that uses none of this renders as before: the 209 hashed renderings match 0.26.0 byte for byte in both finishes. Gradient IDs are named after a hash of the spec, so the connectors' default is left out of it, as the gauge's sweep is, and a unit check rebuilds five rows of 0.26.0's baseline, two of them with gradients, and matches their hashes in both finishes. Twenty new renderings cover each kind in both themes with and without native tooltips, a night's hypnogram on a time axis in a zone with an event marked, at a phone's width in the dark theme and in Midnight, an intraday state timeline without connectors in both themes, daily heart-rate ranges with their averages and in Midnight, sleep timing on a reversed time-of-day axis with a target bedtime in both themes, and range bars beside a line, in a column chart's slots and on a reversed logarithmic axis. The gallery's chart explorer adds a night's hypnogram and a fortnight of heart-rate ranges, and the Sports & performance page ends with a Sleep and recovery section drawn from the same athlete: last night's sleep stages, whose deep sleep follows the HRV that this morning's readiness reads, two weeks of sleep timing that end on that night, and each day's heart rate from the night's lowest to the day's highest, today's highest being the activity stream's.

## 0.26.0 additions

Gauges and rings, the fifth step of the build order in [FITNESS.md](docs/FITNESS.md), described under [Gauges and rings](#gauges-and-rings). Score gauges appear in eight of the fourteen training apps researched there, and Apple's activity rings are the most recognisable single chart among them. `ChartKind.Gauge` draws one score on an open arc whose sweep `ChartSpec.GaugeSweep` sets, from a semicircle to a full circle; `YZones` tint its track and colour the score by its zone, as WHOOP's red, yellow and green recovery tiers do, a gradient can colour it along its length instead, and a Y annotation marks a target with a tick. `ChartKind.Ring` draws one to six concentric rings, each a value against `ChartSeries.Goal`, running on over itself past the goal with its leading end shadowed. `ChartSvg.LegendLabel` returns what the legend writes for a series, which on a ring chart carries each ring's value and goal and which the component's legend and status line now use. Each score and each ring is a focusable, named mark that raises `PointSelected`; every colour follows the style, Midnight included; CSV carries each ring's goal; and both round-trip through the HTTP API's JSON as `"kind":"Gauge"`, `"gaugeSweep"` and `"goal"`.

A chart that sets none of this renders as before: the 189 hashed renderings match 0.25.0 byte for byte in both finishes. Gradient IDs are named after a hash of the spec, and a new property would have changed every hash, so a sweep left at its default is left out of it, which a unit check proves against 0.25.0's serialization. Twenty new renderings cover each kind in both themes with and without native tooltips, a zoned 270-degree recovery gauge with a target, a low score in the dark theme, a 180-degree semicircle, a duration gauge, a full circle with a gradient, a score off the scale, three rings with one past its goal, one capped at three laps beside an empty one, a single ring at a phone's width, six rings, and Midnight versions of a gauge and of rings. The gallery's chart explorer adds a recovery gauge in WHOOP-like tiers and Move, Exercise and Stand rings with exercise past its goal, and the Sports & performance page opens with a Today row: the athlete's readiness on a gauge, an illustrative score from last night's HRV against its baseline and today's form, with the 28-day average as its target, and the day's training as rings — the last run's active calories, its minutes and its training stress against yesterday's fitness — so they agree with the HRV, performance and activity-stream charts beneath.

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

Candlestick, uncertainty band, histogram and box-plot families, the `Statistics` helpers behind them, `Open`/`High`/`Low`/`Close` on `ChartPoint` with the `Candle`, `Interval` and `Observation` factories, the `Bins` specification field, family-specific CSV columns, and gallery demonstrations for all four. See [Statistical and financial families](#statistical-financial-and-radial-families).

## 0.2.0 additions

Time and logarithmic axes as described under [Axes](#axes), including calendar tick selection, UTC invariant labels, date tooltips and table entries, zoom and pan in log and time space, the `XTime` CSV column, and `xAxis` / `yAxis` fields on the JSON endpoints. The gallery has a Numeric / Time X / Log Y switch on the charts that support them.

## 0.1.1 fixes

SVG exports include a visible series legend (donut and heatmap already have labels). The legend footer adds 22 pixels per row to the SVG viewBox beyond the specified plot height. Pass `includeLegend: false` to `ChartSvg.Render` to omit it. Blazor keeps its interactive legend, and downloads include the static legend. Bubble areas share one size scale across series. SVG CSS uses prefixed selectors and per-chart color variables so mixed themes can coexist without styling unrelated host elements.

## License

MIT. See [LICENSE](LICENSE). The packages carry the same expression, so consumers see it in their dependency reports.
