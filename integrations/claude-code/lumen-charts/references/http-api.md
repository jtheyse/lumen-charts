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
| POST | `/api/charts/planner/svg` | PlannerRequest JSON (`spec`, optional `view`, `layout`) → SVG |

An invalid chart or planner answers 400 with problem details naming the rule; malformed JSON is rejected by ASP.NET Core. The endpoints fetch nothing, run no supplied code and store nothing — but add your own authorization and rate limits before exposing them publicly, and cap request size (the sample uses 16 MiB).

## Season planner (0.43.0)

`POST /api/charts/planner/svg` takes a `PlannerRequest` and answers the SVG of a `PlannerSpec` as the whole period, a month or a day, wide or narrow. Send `from` and `to` (a field left out reads as its default, so a period left unset is the single day `0001-01-01`); dates are `"2027-03-01"`, enums are strings (so the `JsonStringEnumConverter` above is needed), `view` is `{"zoom":"Year"|"Month"|"Day","date":"…"}` (the whole period when left out; the date is ignored for `Year`, any day of the month for `Month`) and `layout` is `"Wide"` (default) or `"Narrow"`. A broken spec or a view outside the period is answered 400 with the rule's message. Add `"width"` to draw at the width the page shows it (320–4096), and `"layout":"Narrow"` for a box under 640 px.

```json
{"spec":{"title":"Season planner","description":"Invented organizers' events","from":"2027-01-01","to":"2027-12-31","width":1100,
 "regions":[{"code":"ZA","name":"South Africa"},{"code":"ZA-GP","name":"Gauteng","parent":"ZA"},{"code":"ZA-WC","name":"Western Cape","parent":"ZA"}],
 "periods":[{"from":"2027-04-27","name":"Freedom Day","kind":"PublicHoliday","region":"ZA"},{"from":"2027-06-26","to":"2027-07-18","name":"Invented school holiday","kind":"SchoolHoliday","region":"ZA"}],
 "events":[{"id":"e1","name":"Hilltop XCO","start":"2027-03-13","region":"ZA-GP","category":"XCO","audience":"Kids","relevance":"Clash"},
           {"id":"e2","name":"Coast Stage Race","start":"2027-03-12","end":"2027-03-14","region":"ZA-WC","status":"Provisional"},
           {"id":"mine","name":"Our Spring Enduro","start":"2027-09-18","region":"ZA-GP","mine":true}],
 "filter":{"regions":["ZA-GP"]}},
 "view":{"zoom":"Month","date":"2027-03-01"},"layout":"Wide"}
```

`relevance` is `Other`, `Near` or `Clash`, `status` `Confirmed`, `Provisional` or `Cancelled`, `kind` `PublicHoliday`, `SchoolHoliday` or `Other`; an event may also send `note` (≤ 120 characters) and `url`. A filter sends `regions`, `categories`, `audiences`, `statuses` and `relevances`. The relevance is yours to decide: the planner draws what it is told.

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
{"title":"Gap to the leader","kind":"Line","width":340,"height":320,"yReversed":true,"yMin":0,"yFormat":"Signed","yUnit":"s","series":[{"name":"Rider A","endLabel":"A","endNote":"leader","points":[{"x":0,"y":0},{"x":1,"y":0}]},{"name":"You","endLabel":"You","endNote":"+8.7s","color":"#34d399","strokeWidth":3.2,"points":[{"x":0,"y":0},{"x":1,"y":8.7}]}]}
{"title":"Effort zones","kind":"Strip","width":340,"drawTitles":false,"yFormat":"Duration","series":[{"name":"Time in zone","points":[{"x":0,"y":740,"label":"Easy","color":"#3FD17A"},{"x":1,"y":1290,"label":"Moderate","color":"#D7DDE5"},{"x":2,"y":820,"label":"Hard","color":"#F5B642"},{"x":3,"y":250,"label":"Very hard","color":"#E30613"}]}]}
{"title":"Heart rate per lap","kind":"Column","width":340,"height":260,"series":[{"name":"Heart rate","gradient":[{"value":152,"color":"#A88200"},{"value":174,"color":"#DD4B45"}],"points":[{"x":0,"y":152,"label":"L1","subLabel":"152 bpm"},{"x":1,"y":174,"label":"L2","subLabel":"174 bpm"}]}]}
{"title":"Race scores","kind":"Bar","width":340,"height":240,"yMin":0,"yMax":100,"barTrack":true,"fitHeight":true,"yTickLabels":"None","yTickValues":[],"drawTitles":false,"series":[{"name":"Score","valueLabels":true,"points":[{"x":0,"y":82,"label":"Execution"},{"x":1,"y":64,"label":"Improvement"}]}]}
{"title":"Season arc","kind":"Line","xMin":-0.5,"xMax":2.5,"yReversed":true,"yMin":0,"yMax":100,"yUnit":"%","yTickValues":[{"value":0,"label":"Front"},{"value":50,"label":"Mid"},{"value":100,"label":"Back"}],"series":[{"name":"XCO","points":[{"x":0,"y":44,"label":"7 Feb","valueNote":" · P18/40"},{"x":2,"y":26,"label":"14 Mar"}]},{"name":"XCC","points":[{"x":1,"y":26,"label":"21 Feb"}]}]}
{"title":"How the field finished","description":"312 finishers · median 47:12","source":"Off the chart: 3 faster and 12 slower","kind":"Blocks","width":340,"height":240,"includeZero":true,"xFormat":"Duration","xTickLabels":"Bounds","yTickLabels":"Bounds","annotations":[{"axis":"X","from":2832,"label":"median","showValue":false,"inFront":true}],"series":[{"name":"Finishers","points":[{"x":2100,"xEnd":2400,"y":18},{"x":2400,"xEnd":2700,"y":64},{"x":2700,"xEnd":3000,"y":88},{"x":3000,"xEnd":3300,"y":57,"color":"#E30613","valueNote":" · you"},{"x":3300,"xEnd":3600,"y":1}]}]}
```

A series sends `"changeColors"` (`None`, `HigherIsBetter`, `LowerIsBetter`), a point `"valueNote"`, and a chart `"xTicks"` (`Auto`, `Axis`, `PointLabels`) (0.33.0). A chart sends `"sparkline":true` with its small `"width"` and `"height"` (60 by 16 at the least), and `"yMinSpan"` for the least its Y axis spans, a pane its own `"yMinSpan"`, and a point `"highlight":"#E30613"` (0.34.0). A chart sends `"xTickLabels"` (`All`, `Ends`, `Bounds`) and `"yTickLabels":"Bounds"`, and an annotation `"showValue":false` (with a `"label"`) and `"inFront":true` (0.35.0); a long `"description"` or `"source"` wraps to two lines on its own. A chart or a pane sends `"ySymmetric":10` for a Y axis held symmetric about zero, a format `"Signed"` writes `+5` and `−5`, and a chart may send `"sharedReadout":true`, which the SVG ignores, so a spec shared with a `<LumenChart>` keeps it (0.36.0). A chart sends `"sampling"` (`MinMax`, `Average`) and `"paneTitles"` (`Axis`, `Above`), a chart or a pane `"yTickLabels":"None"`, and `"panes"` takes up to five (0.37.0); `"xTickLabels":"None"` is refused with its reason. A chart or a pane sends `"yTickValues":[{"value":0,"label":"Front"},{"value":50}]` for ticks set by hand and `"yUnit":"s"` for a unit after every value, and a series `"endLabel":"You"` and `"endNote":"+8.7s"` (0.38.0); a duplicate tick value, a tick past 24, a unit past 8 characters or an end label on a bar series is answered 400 with its reason. A chart sends `"kind":"Strip"` for a strip of shares, one series whose points' `"label"` name its parts and `"y"` their amounts, `"barTrack":true` (with `"yMax"`) on a bar or column chart, and `"drawTitles":false` to keep its title and description as its name without drawing them (0.39.0); a strip's `"height"` is not used. A strip of two series, a negative or missing part, all zeros, annotations or value labels on a strip, a track without `"yMax"`, below zero or on stacked columns are answered 400 with their reasons. A column or bar series sends `"gradient"` to fill its columns by value, and a point `"subLabel"` for a second line under its category's name on a column, stacked column or bar chart (0.40.0); a gradient on stacked columns or beside `"fill":"Fade"`, a sub-label on a line chart, past 16 characters or two different ones for one category are answered 400 with their reasons. A chart or a graph sends `"paintBackground":false` to leave its background unpainted (set its style's `background` to the surface it sits on), a bar chart `"fitHeight":true` to be drawn as tall as its rows need, its `"height"` then checked but not used, and a series `"averageOf":"12 s"` for points the app has averaged already (0.41.0); `"fitHeight"` on any other kind, or an `"averageOf"` that is blank, past 16 characters, on two lines or on a candle, range, histogram, box, violin, timeline, calendar, donut, gauge, ring or strip series, is answered 400 with its reason. A point whose `"y"` is `null` sends `"gapLabel":"absent"` to write that word where its value is missing and name the point as a mark, `Share: Round 3, absent` (0.42.0); a gap label on a point with a value, on any kind but a line, area or scatter series, on a density scatter or a sparkline, blank, past 12 characters or on two lines is answered 400 with its reason. `GET types` now lists 23 kinds. A calendar needs `"xAxis":"Time"`, takes one series, and adds up the points that fall on one day in its `"timeZone"`; `"calendarLayout"` (`Weeks`, `Months`), `"calendarCell"` (`Square`, `Dot`, `Bubble`) and `"weekStart"` (`Monday` unless set, any `DayOfWeek` name) lay it out. A block sends `"xEnd"` and its `"y"`, its height (`"kind":"Blocks"` as the chart's kind or a series' own); a timeline point sends `"xEnd"` and no `"y"`; `"timelineConnectors":false` turns its connectors off. A range point sends `"low"` and `"high"`, and `"y"` only for its dot. A time-of-day axis takes seconds since a midnight, past 86400 for the morning after. A series with a `summary` still sends `"points": []`. A field left out of a hand-written object reads as its default (zero for numbers), so send every number you mean.
