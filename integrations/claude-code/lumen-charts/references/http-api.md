# HTTP API (`Lumen.Charts.AspNetCore`)

Expose chart rendering to any client — a JavaScript front end, another service, a report generator — by POSTing a `ChartSpec` as JSON.

```csharp
using System.Text.Json.Serialization;
using Lumen.Charts.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var app = builder.Build();
app.MapLumenCharts();                 // optional prefix: app.MapLumenCharts("/charts")
app.Run();
```

The `JsonStringEnumConverter` matters: specs name enums as strings (`"kind":"Column"`), and without it every request is rejected.

| Method | Path | Body → response |
|---|---|---|
| GET | `/api/charts/types` | chart-kind names |
| POST | `/api/charts/svg` | ChartSpec JSON → `image/svg+xml` |
| POST | `/api/charts/csv` | ChartSpec JSON → the original observations as CSV |
| POST | `/api/charts/graph/svg` | GraphSpec JSON → SVG |
| POST | `/api/charts/graph/layout` | GraphSpec JSON → node positions |
| POST | `/api/charts/graph/routes` | GraphSpec JSON → edge polylines |

An invalid chart answers 400 with problem details naming the rule; malformed JSON is rejected by ASP.NET Core. The endpoints fetch nothing, run no supplied code and store nothing — but add your own authorization and rate limits before exposing them publicly, and cap request size (the sample uses 16 MiB).

## Your own endpoints

`MapLumenCharts` is the generic API. For a fixed report, render on the server in an ordinary endpoint — only the core package is needed:

```csharp
app.MapGet("/reports/power-curve.svg", () =>
    Results.Text(ChartSvg.Render(BuildSpec()), "image/svg+xml; charset=utf-8"));
```

Name the charset: the SVG is UTF-8 and its labels can hold characters such as `–` and `′`.

## JSON shape

Property names are camelCase; enums are strings; time values are Unix milliseconds; durations are seconds; a zone's unbounded upper limit is the string `"Infinity"`.

```json
{"title":"Revenue","kind":"Column","series":[{"name":"Sales","points":[{"x":1,"y":24,"label":"Jan"},{"x":2,"y":38,"label":"Feb"}]}]}
{"title":"Pace","kind":"Line","xFormat":"Duration","yFormat":"Duration","yReversed":true,"series":[{"name":"Run","points":[{"x":0,"y":305},{"x":600,"y":298}]}]}
{"title":"Effort","kind":"Line","yZones":{"zones":[{"name":"Easy","upper":140},{"name":"Hard","upper":"Infinity"}]},"series":[{"name":"Heart rate","zones":{"zones":[{"name":"Easy","upper":140},{"name":"Hard","upper":"Infinity","color":"#DD4B45"}]},"points":[{"x":0,"y":120},{"x":60,"y":158}]}]}
{"title":"Training","kind":"Line","xAxis":"Time","y2Label":"Form","series":[{"name":"Fitness","projectedFrom":1787529600000,"points":[{"x":1787443200000,"y":86.4},{"x":1787529600000,"y":84.3}]},{"name":"Daily stress","kind":"Column","points":[{"x":1787443200000,"y":91},{"x":1787529600000,"y":48}]},{"name":"Form","kind":"Area","secondary":true,"points":[{"x":1787443200000,"y":-6.2},{"x":1787529600000,"y":3.1}]}]}
{"title":"Market","kind":"Candlestick","xAxis":"Time","skipWeekends":true,"panes":[{"label":"Volume","weight":0.4,"yFormat":"Compact"}],"series":[{"name":"ACME","points":[{"x":1772409600000,"open":10,"high":12,"low":9,"close":11}]},{"name":"Volume","kind":"Column","pane":1,"points":[{"x":1772409600000,"y":1500000}]}]}
{"title":"Latency","kind":"Box","series":[{"name":"Asia","points":[],"summary":{"q1":205,"median":228,"q3":252,"lowerWhisker":160,"upperWhisker":318,"outliers":[352,371]}}]}
{"title":"Classic look","kind":"Line","style":{"finish":"Classic"},"series":[{"name":"S","points":[{"x":0,"y":1},{"x":1,"y":3}]}]}
{"title":"Recovery","kind":"Gauge","gaugeSweep":270,"yLabel":"%","yZones":{"zones":[{"name":"Low","upper":33,"color":"#DD4B45"},{"name":"Moderate","upper":66,"color":"#A88200"},{"name":"Good","upper":"Infinity","color":"#2E9B58"}]},"series":[{"name":"Recovery","points":[{"x":0,"y":72,"label":"Recovery"}]}]}
{"title":"Activity","kind":"Ring","series":[{"name":"Move","goal":600,"points":[{"x":0,"y":540,"label":"kcal"}]},{"name":"Exercise","goal":30,"points":[{"x":0,"y":47,"label":"min"}]}]}
```

A series with a `summary` still sends `"points": []`. A field left out of a hand-written object reads as its default (zero for numbers), so send every number you mean.
