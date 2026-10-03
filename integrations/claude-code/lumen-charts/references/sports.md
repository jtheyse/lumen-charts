# Sports and training charts

Recipes for the charts endurance and wellness apps draw (TrainingPeaks, Strava, Garmin, WHOOP, Oura, Gentler Streak, Bevel). Each is a plain `ChartSpec`; put it in `<LumenChart Spec="…" />` or render it with `ChartSvg.Render`. They compile against Lumen.Charts 0.27.0.

## Conventions the numbers follow

- **Time on a time axis** is Unix milliseconds: `TimeAxis.Value(dateTimeOffset)`. For a `DateOnly`: `TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))`.
- **Elapsed time and durations** are seconds, shown with `ValueFormat.Duration` (`5:30`, `1:02:05`).
- **Clock times** for bedtimes and wake times are seconds since the midnight before the night, shown with `ValueFormat.TimeOfDay` (`23:30`, `06:40`): 23:30 is 84600 and 06:40 the next morning 110400, so a night never crosses zero.
- **Pace** is seconds per km (or mile): 300 = `5:00 /km`. Set `YReversed = true` so faster is higher, as every running app does. Put the unit in the axis title.
- **Power** is watts; **heart rate** bpm; samples must be uniformly spaced (pass `sampleSeconds` when not 1 s). Fill or cut gaps before computing.
- **Zones:** `Zone.Upper` is inclusive, the last zone's is `double.PositiveInfinity`. `ZoneScale.CogganPower(ftp)` and `ZoneScale.CogganHeartRate(thresholdHr)` follow Allen & Coggan. A value in a published gap (55 % vs 56 %) belongs to the higher zone.
- **Not computed** (no published formula): hrTSS, rTSS, TRIMP, grade-adjusted pace, W′ balance, and vendor scores such as recovery, readiness, strain or Body Battery. When the data provides one, draw it on a gauge (below) or as an ordinary series.

Shared helper used below:

```csharp
using Lumen.Charts;
static double Day(DateOnly d) => TimeAxis.Value(new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
```

## Performance management chart (fitness, fatigue, form)

Daily training stress as columns, fitness (CTL) and fatigue (ATL) as lines, form (TSB) as an area on the right axis; planned days dashed.

```csharp
// days: one entry per workout day — (DateOnly Day, double Stress) — with planned ones after `today`.
var load = Training.Load(days, fitness: 45, fatigue: 45);   // seed with the athlete's typical daily stress
var planned = Day(today);
var pmc = new ChartSpec {
    Title = "Performance management", Description = "Fitness, fatigue and form from daily training stress",
    Kind = ChartKind.Line, XAxis = AxisKind.Time, YLabel = "Training stress per day", Y2Label = "Form",
    Series = [
        ChartSeries.From("Daily stress", load, d => Day(d.Day), d => d.Stress) with { Kind = ChartKind.Column },
        ChartSeries.From("Fitness (CTL)", load, d => Day(d.Day), d => Math.Round(d.Fitness, 1)) with { ProjectedFrom = planned, HighlightLast = true },
        ChartSeries.From("Fatigue (ATL)", load, d => Day(d.Day), d => Math.Round(d.Fatigue, 1)) with { ProjectedFrom = planned },
        ChartSeries.From("Form (TSB)", load, d => Day(d.Day), d => Math.Round(d.Form, 1)) with { Kind = ChartKind.Area, Secondary = true, ProjectedFrom = planned }]
};
```

`Training.Load` returns every calendar day from the first entry to the last (missing days count as zero), sums several workouts on one day, and computes form from yesterday's fitness and fatigue, as TrainingPeaks does. Tune fatigue with `fatigueDays` (4–12).

## Activity stream in panes

Heart rate coloured and shaded by zone, pace on a reversed axis, elevation beneath — one shared elapsed-time axis.

```csharp
var heart = ZoneScale.CogganHeartRate(170);
// seconds[i], hr[i], pace[i] (s/km), elevation[i] (m) sampled together
var stream = new ChartSpec {
    Title = "Morning run", Description = "Heart rate, pace and elevation over elapsed time",
    Kind = ChartKind.Line, XFormat = ValueFormat.Duration, XLabel = "Elapsed time",
    YLabel = "Heart rate (bpm)", YZones = heart, Height = 520,
    Panes = [new ChartPane { Label = "Pace (/km)", YFormat = ValueFormat.Duration, YReversed = true, Weight = .6 },
             new ChartPane { Label = "Elevation (m)", Weight = .45 }],
    Series = [
        new("Heart rate", seconds.Select((t, i) => new ChartPoint(t, hr[i])).ToArray()) { Zones = heart },
        new("Pace", seconds.Select((t, i) => new ChartPoint(t, pace[i])).ToArray()) { Pane = 1 },
        new("Elevation", seconds.Select((t, i) => new ChartPoint(t, elevation[i])).ToArray()) { Kind = ChartKind.Area, Fill = AreaFill.Fade, Pane = 2 }]
};
```

Use `Gradient = [new ColorStop(120, "#3F87D9"), new ColorStop(180, "#DD4B45")]` instead of `Zones` for a continuous colour. Zooming the component moves every pane together.

## Power–duration curve with critical power

```csharp
var thisMonth = Training.MeanMaximal(rideWatts, Training.StandardDurations);
var lastMonth = Training.MeanMaximal(previousWatts, Training.StandardDurations);
var cp = Training.CriticalPower(thisMonth);               // null without efforts between 3 and 20 minutes
var curve = new ChartSpec {
    Title = "Power–duration curve", Description = "Best average power for every duration",
    Kind = ChartKind.Line, XAxis = AxisKind.Log, XFormat = ValueFormat.Duration, XLabel = "Duration", YLabel = "Power (W)",
    Annotations = cp is null ? [] : [new ChartAnnotation(AnnotationAxis.Y, Math.Round(cp.CriticalPower)) { Label = "Critical power" }],
    Series = [new("This month", thisMonth.Select(p => new ChartPoint(p.Seconds, Math.Round(p.Value))).ToArray()),
              new("Last month", lastMonth.Select(p => new ChartPoint(p.Seconds, Math.Round(p.Value))).ToArray())]
};
```

Ticks read `1s`, `10s`, `1m`, `20m`, `1h`. The curve is reported as the data has it: between durations that are not multiples of each other a longer one can score higher.

## Time in zone

```csharp
var inZone = Training.TimeInZone(hr, heart, sampleSeconds: 10);   // the stream above is sampled every 10 s
var ramp = ChartStyle.Light.Zones;                         // or the zone's own Color
var timeInZone = new ChartSpec {
    Title = "Time in zone", Description = "Time spent in each heart-rate zone",
    Kind = ChartKind.Bar, YFormat = ValueFormat.Duration, YLabel = "Time",
    Series = [new("Time in zone", heart.Zones.Select((z, i) => new ChartPoint(i, inZone[i], z.Name) { Color = z.Color ?? ramp[i] }).ToArray())]
};
```

## Weekly load against a target band

```csharp
// weeks: weekly training stress; target: (low, high) per week, e.g. 80–130 % of the previous four weeks' average
var weekly = new ChartSpec {
    Title = "Weekly load", Description = "Weekly training stress against its target range",
    Kind = ChartKind.Column, YLabel = "Training stress per week",
    Style = ChartStyle.Light with { BarRadius = 6 },
    Annotations = [new ChartAnnotation(AnnotationAxis.Y, Math.Round(weeks.Average())) { Label = "Average" }],
    Series = [new("Weekly load", weeks.Select((v, w) => new ChartPoint(w, v, $"W{w + 1}")).ToArray()) { ValueLabels = true },
              new("Target range", weeks.Select((v, w) => ChartPoint.Interval(w, (target[w].Low + target[w].High) / 2, target[w].Low, target[w].High, $"W{w + 1}")).ToArray()) { Kind = ChartKind.Band }]
};
```

## HRV against its baseline

Daily HRV as points, coloured by whether they sit inside a rolling baseline band (mean ± one standard deviation), as Garmin, Oura and Bevel show it.

```csharp
// hrv: double? per day (null = no reading), dates: matching DateOnly
var baseline = Statistics.Rolling(hrv, window: 28, minimum: 7);
var inside = "#2E9B58"; var outside = "#DB6A1F";
var hrvChart = new ChartSpec {
    Title = "Overnight HRV", Description = "Daily HRV against a 28-day baseline band",
    Kind = ChartKind.Scatter, XAxis = AxisKind.Time, YLabel = "HRV (ms)",
    Series = [
        new("Baseline", dates.Select((d, i) => baseline[i] is { } b
            ? ChartPoint.Interval(Day(d), Math.Round(b.Mean, 1), Math.Round(b.Mean - b.Deviation, 1), Math.Round(b.Mean + b.Deviation, 1))
            : new ChartPoint(Day(d), null)).ToArray()) { Kind = ChartKind.Band },
        new("HRV", dates.Select((d, i) => new ChartPoint(Day(d), hrv[i]) {
            Color = hrv[i] is { } v && baseline[i] is { } b && Math.Abs(v - b.Mean) > b.Deviation ? outside : inside }).ToArray())]
};
```

## Pace by kilometre, faster higher

```csharp
var paceChart = new ChartSpec {
    Title = "Pace by kilometre", Description = "Pace per kilometre with its trend and a 5:00 target",
    Kind = ChartKind.Line, YFormat = ValueFormat.Duration, YReversed = true, XLabel = "Kilometre", YLabel = "Pace (/km)",
    Annotations = [new ChartAnnotation(AnnotationAxis.Y, 300) { Label = "Target 5:00" }],
    Series = [new("Pace", splits.Select((s, k) => new ChartPoint(k + 1, s)).ToArray()) { Trend = true }]
};
```

## Elevation coloured by grade

Each segment of a line takes the colour of the point it starts from.

```csharp
var grades = new ZoneScale([new("Flat", 3, "#2E9B58"), new("Rolling", 6, "#A88200"), new("Steep", 9, "#DB6A1F"), new("Very steep", double.PositiveInfinity, "#DD4B45")]);
var climb = new ChartSpec {
    Title = "Elevation", Description = "Elevation profile coloured by grade",
    Kind = ChartKind.Area, XLabel = "Distance (km)", YLabel = "Elevation (m)",
    Series = [new("Elevation", km.Select((x, i) => {
        var grade = i + 1 < km.Length ? Math.Abs(metres[i + 1] - metres[i]) / ((km[i + 1] - km[i]) * 1000) * 100 : 0;
        return new ChartPoint(x, metres[i]) { Color = grades.Zones[grades.IndexOf(grade)].Color };
    }).ToArray()) { Fill = AreaFill.Fade }]
};
```

## Personal-best progression

A step line holds each record until it is broken; faster is higher.

```csharp
var record = double.MaxValue;
var best = new ChartSpec {
    Title = "5 km personal best", Description = "Best 5 km time over the season",
    Kind = ChartKind.Line, XAxis = AxisKind.Time, YFormat = ValueFormat.Duration, YReversed = true, YLabel = "Time",
    Series = [new("Personal best", efforts.OrderBy(e => e.Date).Select(e => {
        record = Math.Min(record, e.Seconds);
        return new ChartPoint(Day(e.Date), record);
    }).ToArray()) { Curve = LineCurve.Step }]
};
```

## Recovery or readiness gauge

One score on an open arc, tinted by tiers — WHOOP's red, yellow and green recovery, Oura's and Garmin's readiness bands — with its recent average as a target tick.

```csharp
// score: today's 0–100 score from the device; average: the last seven days'
var tiers = new ZoneScale([new("Low", 33, "#DD4B45"), new("Moderate", 66, "#A88200"), new("Good", double.PositiveInfinity, "#2E9B58")]);
var recovery = new ChartSpec {
    Title = "Recovery", Description = "This morning's recovery against its seven-day average",
    Kind = ChartKind.Gauge, YLabel = "%", YZones = tiers,                 // scale 0–100 unless YMin/YMax say otherwise
    Annotations = [new ChartAnnotation(AnnotationAxis.Y, Math.Round(average)) { Label = "7-day average" }],
    Series = [new("Recovery", [new ChartPoint(0, score, "Recovery")])]    // one point; its label is the caption
};
```

The score is drawn in its tier's colour and named with it (`Recovery: 72 %, Good`); a score off the scale stands at its end and says so. `GaugeSweep = 180` draws a semicircle, `360` a full circle. For WHOOP strain set `YMax = 21` and colour the arc with `Gradient = [new ColorStop(0, "#3F87D9"), new ColorStop(21, "#DD4B45")]` on the series instead of `YZones`; for sleep against a goal, `YFormat = ValueFormat.Duration` with seconds (`YMax = 36000` reads `10:00:00`). The tier colours above are entries of `ChartStyle.Light.Zones`, which clear 3:1 on every preset; on `ChartStyle.Midnight` take `ChartStyle.Midnight.Zones[5]`, `[3]` and `[2]` for brighter ones.

## Activity rings

Apple's concentric rings: each a value against its goal, outermost first, running on over itself past the goal.

```csharp
// moveKcal, exerciseMinutes and standHours for the day
var activity = new ChartSpec {
    Title = "Activity", Description = "Today's move, exercise and stand against their goals",
    Kind = ChartKind.Ring, Width = 400, Height = 360,
    Series = [new("Move", [new ChartPoint(0, moveKcal, "kcal")], "#DD4B45") { Goal = 600 },      // the label is the unit
              new("Exercise", [new ChartPoint(0, exerciseMinutes, "min")], "#2E9B58") { Goal = 30 },
              new("Stand", [new ChartPoint(0, standHours, "h")], "#3F87D9") { Goal = 12 }]
};
```

One to six rings, one nonnegative point each; `Goal` defaults to 100, so percentages need none. The legend reads `Move: 540 of 600 kcal` and each ring's name adds its progress, `90 %`; past 300 % a ring is drawn at 300 % and says so. A ring in seconds takes `YFormat = ValueFormat.Duration`, which applies to every ring of the chart. CSV adds a `Goal` column.

## Sleep stages (hypnogram)

Last night as a state timeline: one series per stage, top to bottom, each period a span, joined where the stage changes, as Oura, Apple Health, Garmin and WHOOP draw it.

```csharp
// stages: (string Stage, DateTimeOffset From, DateTimeOffset To) for each period of the night, in any order
string[] order = ["Awake", "REM", "Light", "Deep"];
string[] colours = ["#DB6A1F", "#3F87D9", "#848484", "#9E63D3"];   // ChartStyle.Light.Zones[4], [1], [0], [6]
var hypnogram = new ChartSpec {
    Title = "Last night", Description = "Sleep stages through the night",
    Kind = ChartKind.Timeline, XAxis = AxisKind.Time, TimeZone = "Europe/London", XLabel = "Time",
    Series = order.Select((stage, i) => new ChartSeries(stage, stages.Where(s => s.Stage == stage)
        .Select(s => ChartPoint.Span(TimeAxis.Value(s.From), TimeAxis.Value(s.To))).ToArray(), colours[i])).ToArray()
};
```

Each lane is named on the left and the legend reads each stage's total and share, `Deep 1:13, 17 %`; each span reads `REM: 02:14 to 02:41, 27 min` on the zone's clock. Spans in one lane must not overlap; a connector joins two spans only where one ends exactly as the next begins, so build the night from contiguous periods. `TimelineConnectors = false` draws a plain state chart, such as rest, stress and activity through a day, where lanes may overlap. A `ChartAnnotation` on X marks a moment, such as an alarm.

## Sleep timing

Bedtime to waking for each night as floating bars on a clock that runs past midnight, earlier at the top, as Oura, WHOOP and Calm show it.

```csharp
// nights: (DateOnly Morning, double Bedtime, double Wake), seconds since the midnight before each night
var timing = new ChartSpec {
    Title = "Sleep timing", Description = "Bedtime to waking, the last two weeks",
    Kind = ChartKind.Range, XAxis = AxisKind.Time, YFormat = ValueFormat.TimeOfDay, YReversed = true, YLabel = "Clock time",
    YMin = 75600, YMax = 118800,                                     // 21:00 to 09:00, both ends labelled
    Series = [new("Sleep", nights.Select(n => ChartPoint.Interval(Day(n.Morning), null, n.Bedtime, n.Wake,
        n.Morning.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture))).ToArray(), "#3F87D9")]
};
```

A bar reads `27 Sep: 22:46 to 06:24`. Ticks land on whole hours, or on half and quarter hours over a short range. Add a target as `new ChartAnnotation(AnnotationAxis.Y, 82800) { Label = "Target bedtime" }`.

## Daily heart-rate range

Each day's lowest to highest heart rate, the dot its average, as Apple Fitness draws it; the same shape draws Garmin's Body Battery high and low.

```csharp
// heartDays: (DateOnly Day, double Low, double High, double Average)
var heartRange = new ChartSpec {
    Title = "Heart rate", Description = "Each day's lowest and highest heart rate, the dot its average",
    Kind = ChartKind.Range, XAxis = AxisKind.Time, YLabel = "Heart rate (bpm)",
    Series = [new("Heart rate", heartDays.Select(d => ChartPoint.Interval(Day(d.Day), d.Average, d.Low, d.High,
        d.Day.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture))).ToArray(), "#DD4B45")]
};
```

A bar reads `12 Sep: 52 to 168, average 74`. There is no zero baseline, so the axis spans the bars. A range can also be a series' own kind: `new("Range", points) { Kind = ChartKind.Range }` beside a resting-heart-rate line, or on a column chart in each category's slot. Range series refuse `Trend`, `Zones` and `ProjectedFrom`; colour a single bar with its point's `Color`.

## Look

- The default refined finish suits these charts: thin lines, markers on hover, dotted grid.
- `Style = ChartStyle.Midnight` is a near-black, high-contrast preset in the style of WHOOP and Oura.
- `Curve = LineCurve.Smooth` for heart rate and elevation (monotone — no invented peaks); `Fill = AreaFill.Fade` for areas; `ChartStyle.BarRadius` for capsule columns; `HighlightLast` for "today".
- Many apps put the Y axis on the right with few labels: `YAxisSide = AxisSide.Right, YTickLabels = TickLabels.Ends` (not with a secondary series).
