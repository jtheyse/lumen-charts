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
{"title":"Last night","kind":"Timeline","xAxis":"Time","timeZone":"Europe/London","series":[{"name":"Light","points":[{"x":1790466000000,"xEnd":1790467800000}]},{"name":"Deep","points":[{"x":1790467800000,"xEnd":1790470200000}]}]}
{"title":"Heart rate","kind":"Range","xAxis":"Time","series":[{"name":"Heart rate","points":[{"x":1789171200000,"y":74,"low":52,"high":168,"label":"12 Sep"},{"x":1789257600000,"y":70,"low":48,"high":150,"label":"13 Sep"}]}]}
{"title":"Sleep timing","kind":"Range","yFormat":"TimeOfDay","yReversed":true,"series":[{"name":"Sleep","points":[{"x":0,"low":82800,"high":109800,"label":"Mon"},{"x":1,"low":84600,"high":111600,"label":"Tue"}]}]}
{"title":"Training calendar","kind":"Calendar","xAxis":"Time","timeZone":"Europe/London","yZones":{"zones":[{"name":"Easy","upper":50},{"name":"Hard","upper":"Infinity"}]},"annotations":[{"axis":"X","from":1789560000000,"label":"Today"}],"series":[{"name":"Stress","points":[{"x":1789387200000,"y":40,"label":"Ride"},{"x":1789390800000,"y":30},{"x":1789560000000,"y":120}]}]}
{"title":"Runs in September","kind":"Calendar","xAxis":"Time","calendarLayout":"Months","calendarCell":"Bubble","weekStart":"Sunday","xMin":1788220800000,"xMax":1790726400000,"series":[{"name":"Distance (km)","points":[{"x":1789387200000,"y":8.6},{"x":1789560000000,"y":14}]}]}
{"title":"Laps","kind":"Blocks","yFormat":"Duration","yReversed":true,"annotations":[{"axis":"Y","from":306,"label":"Average"}],"series":[{"name":"Laps","points":[{"x":0,"xEnd":2,"y":336,"label":"Lap 1"},{"x":2,"xEnd":4,"y":318,"label":"Lap 2"},{"x":4,"xEnd":5.5,"y":289,"label":"Lap 3"}]}]}
{"title":"Race results","kind":"Line","xMin":-0.5,"xMax":2.5,"yReversed":true,"series":[{"name":"Position","changeColors":"LowerIsBetter","valueLabels":true,"points":[{"x":0,"y":31,"label":"11-04-2026","valueNote":"/50"},{"x":1,"y":24,"label":"16-05-2026","valueNote":"/48"},{"x":2,"y":27,"label":"04-07-2026","valueNote":"/51"}]}]}
{"title":"Season","kind":"Line","xAxis":"Time","xTicks":"Axis","yReversed":true,"series":[{"name":"Position","points":[{"x":1775901600000,"y":31,"label":"Round 1 · Hilltop Classic"},{"x":1778925600000,"y":24,"label":"Round 2 · River Valley"}]}]}
{"title":"Threshold intervals","kind":"Blocks","xFormat":"Duration","includeZero":true,"series":[{"name":"Plan","zones":{"zones":[{"name":"Endurance","upper":187.5},{"name":"Tempo","upper":225},{"name":"Threshold","upper":"Infinity"}]},"points":[{"x":0,"xEnd":600,"y":150,"label":"Warm-up"},{"x":600,"xEnd":1080,"y":250,"label":"Interval 1"},{"x":1080,"xEnd":1320,"y":125,"label":"Recovery"}]},{"name":"Power","kind":"Line","points":[{"x":0,"y":120},{"x":300,"y":152},{"x":800,"y":256},{"x":1200,"y":131}]}]}
```

A series sends `"changeColors"` (`None`, `HigherIsBetter`, `LowerIsBetter`), a point `"valueNote"`, and a chart `"xTicks"` (`Auto`, `Axis`, `PointLabels`) (0.33.0). A chart sends `"sparkline":true` with its small `"width"` and `"height"` (60 by 16 at the least), and `"yMinSpan"` for the least its Y axis spans, a pane its own `"yMinSpan"`, and a point `"highlight":"#E30613"` (0.34.0). A calendar needs `"xAxis":"Time"`, takes one series, and adds up the points that fall on one day in its `"timeZone"`; `"calendarLayout"` (`Weeks`, `Months`), `"calendarCell"` (`Square`, `Dot`, `Bubble`) and `"weekStart"` (`Monday` unless set, any `DayOfWeek` name) lay it out. A block sends `"xEnd"` and its `"y"`, its height (`"kind":"Blocks"` as the chart's kind or a series' own); a timeline point sends `"xEnd"` and no `"y"`; `"timelineConnectors":false` turns its connectors off. A range point sends `"low"` and `"high"`, and `"y"` only for its dot. A time-of-day axis takes seconds since a midnight, past 86400 for the morning after. A series with a `summary` still sends `"points": []`. A field left out of a hand-written object reads as its default (zero for numbers), so send every number you mean.
