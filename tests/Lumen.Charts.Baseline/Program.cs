using System.Security.Cryptography;
using System.Text;
using Lumen.Charts;

if (args.FirstOrDefault() == "probe") { Probe.Run(); return; }

// "classic" draws every row in the classic finish, set on the style each chart draws with, and writes classic.txt.
// The property is looked up by name: code without it — anything before 0.24.0 — draws every spec exactly as given, so
// on that code this mode reproduces the plain run. "svg-out <dir>" also writes each rendering there, for a contact sheet.
var classicMode = args.Contains("classic");
var svgOut = Array.IndexOf(args, "svg-out") is var svgAt and >= 0 ? args[svgAt + 1] : null;
var finishProperty = typeof(ChartStyle).GetProperty("Finish");
ChartStyle? Finished(ChartStyle? style, ChartTheme theme)
{
    if (!classicMode || finishProperty is null) return style;
    var classic = (style ?? (theme == ChartTheme.Dark ? ChartStyle.Dark : ChartStyle.Light)) with { };
    finishProperty.SetValue(classic, Enum.Parse(finishProperty.PropertyType, "Classic"));
    return classic;
}
string Render(ChartSpec spec, bool includeTitles = true) => ChartSvg.Render(spec with { Style = Finished(spec.Style, spec.Theme) }, includeTitles: includeTitles);
string Graph(GraphSpec spec) => GraphEngine.Render(spec with { Style = Finished(spec.Style, spec.Theme) });
var svgs = new List<string>();

// Hashes one representative rendering per chart kind and theme, plus annotations, a density scatter,
// a branded chart and both graph layouts, so a refactor can prove nothing moved that should not.
var lines = new List<string>();
ChartPoint[] Points() => Enumerable.Range(0, 12).Select(i => new ChartPoint(i, 10 + i * 3 + (i % 3) * 4, $"P{i}")).ToArray();
ChartSpec Spec(ChartKind kind, ChartTheme theme) => kind switch
{
    ChartKind.Candlestick or ChartKind.Ohlc => new() { Kind = kind, Theme = theme, Series = [new("P", Enumerable.Range(0, 8).Select(i => ChartPoint.Candle(i, 10 + i, 14 + i, 8 + i, 11 + i + (i % 2 == 0 ? 1 : -2))).ToArray())] },
    ChartKind.Band => new() { Kind = kind, Theme = theme, Series = [new("F", Enumerable.Range(0, 8).Select(i => ChartPoint.Interval(i, 10 + i, 8 + i, 13 + i)).ToArray())] },
    ChartKind.Histogram => new() { Kind = kind, Theme = theme, Series = [new("S", Enumerable.Range(0, 60).Select(i => new ChartPoint(i, i % 13 + (i == 7 ? 40 : 0))).ToArray())] },
    ChartKind.Box => new() { Kind = kind, Theme = theme, Series = [new("S", Enumerable.Range(0, 60).Select(i => new ChartPoint(i, i % 13 + (i == 7 ? 40 : 0))).ToArray()), new("T", Enumerable.Range(0, 40).Select(i => new ChartPoint(i, i % 9 + 3)).ToArray())] },
    ChartKind.Donut => new() { Kind = kind, Theme = theme, Series = [new("D", [new(0, 4, "A"), new(1, 3, "B"), new(2, 2, "C")])] },
    ChartKind.Radar => new() { Kind = kind, Theme = theme, Series = [new("R", [new(0, 4, "A"), new(1, 3, "B"), new(2, 5, "C"), new(3, 2, "D")]), new("Q", [new(0, 2, "A"), new(1, 4, "B"), new(2, 3, "C"), new(3, 4, "D")])] },
    ChartKind.Heatmap => new() { Kind = kind, Theme = theme, Series = Enumerable.Range(0, 3).Select(r => new ChartSeries($"Row {r}", Enumerable.Range(0, 6).Select(c => new ChartPoint(c, (r * 7 + c * 5) % 17, $"C{c}")).ToArray())).ToArray() },
    ChartKind.Gauge => new() { Kind = kind, Theme = theme, Title = "Baseline", Description = "Default output", Series = [new("Score", [new(0, 72, "Score")])] },
    ChartKind.Ring => new() { Kind = kind, Theme = theme, Title = "Baseline", Description = "Default output", Series = [new("Move", [new(0, 540, "kcal")]) { Goal = 600 }, new("Exercise", [new(0, 47, "min")]) { Goal = 30 }, new("Stand", [new(0, 9, "h")]) { Goal = 12 }] },
    ChartKind.Timeline => new() { Kind = kind, Theme = theme, Title = "Baseline", Description = "Default output", Series = [new("A", [ChartPoint.Span(0, 2), ChartPoint.Span(5, 7)]), new("B", [ChartPoint.Span(2, 5), ChartPoint.Span(7, 9, "Last")]), new("C", [ChartPoint.Span(9, 12)])] },
    ChartKind.Range => new() { Kind = kind, Theme = theme, Title = "Baseline", Description = "Default output", Series = [new("R", Enumerable.Range(0, 8).Select(i => ChartPoint.Interval(i, i % 3 == 0 ? null : 10 + i, 6 + i, 15 + i * 2, $"P{i}")).ToArray())] },
    ChartKind.Calendar => new() { Kind = kind, Theme = theme, XAxis = AxisKind.Time, Title = "Baseline", Description = "Default output", Series = [new("C", Enumerable.Range(0, 56).Select(i => new ChartPoint(1788825600000d + i * 86400000d, i % 7 == 0 ? 0 : 10 + i * 3 % 40)).ToArray())] },
    ChartKind.Blocks => new() { Kind = kind, Theme = theme, Title = "Baseline", Description = "Default output", Series = [new("B", [ChartPoint.Block(0, 2, 5, "W"), ChartPoint.Block(2, 6, 9), ChartPoint.Block(6, 7, 7), ChartPoint.Block(8, 12, 3, "C")])] },
    ChartKind.Strip => new() { Kind = kind, Theme = theme, Title = "Baseline", Description = "Default output", Series = [new("Parts", [new(0, 4, "A"), new(1, 3, "B"), new(2, 2, "C"), new(3, 1, "D")])] },
    _ => new() { Kind = kind, Theme = theme, Title = "Baseline", Description = "Default output", Series = [new("A", Points()), new("B", Points().Select(p => p with { Y = p.Y + 5 }).ToArray())] }
};
foreach (var kind in Enum.GetValues<ChartKind>())
    foreach (var theme in Enum.GetValues<ChartTheme>())
        foreach (var titles in new[] { true, false })
            lines.Add($"{kind}/{theme}/{titles} {Hash(Render(Spec(kind, theme), includeTitles: titles))}");
lines.Add($"density {Hash(Render(new ChartSpec { Kind = ChartKind.Scatter, DensityCells = 20, Series = [new("S", Enumerable.Range(0, 400).Select(i => new ChartPoint(i % 37, i % 23)).ToArray())] }))}");
lines.Add($"annotated {Hash(Render(Spec(ChartKind.Line, ChartTheme.Light) with { Annotations = [new(AnnotationAxis.Y, 25) { Label = "Target" }, new(AnnotationAxis.X, 3) { To = 6, Label = "Window" }] }))}");
lines.Add($"branded {Hash(Render(Spec(ChartKind.Area, ChartTheme.Light) with { Style = ChartStyle.Light with { Background = "#F6F3EE", Series = ["#1D4E89", "#B03A2E"], FontFamily = "Georgia,serif" } }))}");
var graph = new GraphSpec { Nodes = [new("a", "A"), new("b", "B"), new("c", "C"), new("d", "D")], Edges = [new("a", "b"), new("b", "c"), new("a", "c", "long"), new("c", "d"), new("d", "d")] };
foreach (var layout in Enum.GetValues<GraphLayout>())
    foreach (var theme in Enum.GetValues<ChartTheme>())
        lines.Add($"graph/{layout}/{theme} {Hash(Graph(graph with { Layout = layout, Theme = theme }))}");
var box = Spec(ChartKind.Box, ChartTheme.Light);
lines.Add($"box-summary {Hash(Render(box with { Series = [.. box.Series, new("U", []) { Summary = new(4, 6, 9, 1, 14, [20, .5]) }] }))}");
var histogram = Spec(ChartKind.Histogram, ChartTheme.Light);
lines.Add($"histogram-two {Hash(Render(histogram with { Series = [.. histogram.Series, new("T", Enumerable.Range(0, 40).Select(i => new ChartPoint(i, i % 9 + 3)).ToArray())] }))}");
// Guards for the axis code 0.19.0 touches: log, time and secondary axes, minor grids and trends.
var line = Spec(ChartKind.Line, ChartTheme.Light);
ChartSeries[] Unlabelled(Func<double, double> x) => line.Series.Select(s => s with { Points = s.Points.Select(p => p with { X = x(p.X), Label = null }).ToArray() }).ToArray();
lines.Add($"guard/log-y-minor {Hash(Render(line with { YAxis = AxisKind.Log, MinorGridlines = true }))}");
lines.Add($"guard/log-x-minor {Hash(Render(line with { XAxis = AxisKind.Log, MinorGridlines = true, Series = Unlabelled(x => Math.Pow(2, x)) }))}");
lines.Add($"guard/linear-minor {Hash(Render(line with { MinorGridlines = true, Series = Unlabelled(x => x * 7.5) }))}");
lines.Add($"guard/time {Hash(Render(line with { XAxis = AxisKind.Time, Series = Unlabelled(x => 1767225600000d + x * 86400000d) }))}");
lines.Add($"guard/secondary {Hash(Render(line with { Series = [line.Series[0], line.Series[1] with { Secondary = true, Points = line.Series[1].Points.Select(p => p with { Y = p.Y / 10 }).ToArray() }], Y2Label = "Rate" }))}");
var scatter = Spec(ChartKind.Scatter, ChartTheme.Light);
lines.Add($"guard/trend {Hash(Render(scatter with { Series = [.. scatter.Series.Select((s, i) => s with { Trend = true, Points = s.Points.Select(p => p with { Y = i == 0 ? p.Y : 60 - p.Y }).ToArray() })] }))}");
var violin = Spec(ChartKind.Box, ChartTheme.Light) with { Kind = ChartKind.Violin, YAxis = AxisKind.Log, MinorGridlines = true };
lines.Add($"guard/violin-log {Hash(Render(violin with { Series = [.. violin.Series.Select(s => s with { Points = s.Points.Select(p => p with { Y = p.Y + 1 }).ToArray() })] }))}");
// 0.19.0: durations on a linear and a logarithmic axis, a reversed pace axis and compact numbers.
lines.Add($"duration-line {Hash(Render(line with { XFormat = ValueFormat.Duration, MinorGridlines = true, Series = Unlabelled(x => x * 300) }))}");
var ride = Enumerable.Range(0, 3600).Select(t => t % 600 < 15 ? 900d : t >= 1200 && t < 2400 ? 280 : 190 + t % 7).ToArray();
lines.Add($"power-curve {Hash(Render(line with { XAxis = AxisKind.Log, XFormat = ValueFormat.Duration, Series = [ChartSeries.From("Best", Training.MeanMaximal(ride, Training.StandardDurations), p => p.Seconds, p => (double?)Math.Round(p.Value, 1))] }))}");
lines.Add($"pace-reversed {Hash(Render(line with { YFormat = ValueFormat.Duration, YReversed = true, Annotations = [new(AnnotationAxis.Y, 300) { Label = "Target" }],
    Series = [new("Pace", Enumerable.Range(0, 12).Select(i => new ChartPoint(i, 330 - i * 4 + i % 3 * 5)).ToArray()) { Trend = true }] }))}");
lines.Add($"compact-column {Hash(Render(Spec(ChartKind.Column, ChartTheme.Light) with { YFormat = ValueFormat.Compact, Series = [new("Views", Enumerable.Range(0, 6).Select(i => new ChartPoint(i, 1500 + i * i * 240_000, $"W{i}")).ToArray())] }))}");
// Guards for the paths 0.20.0 touches: sampled lines and areas, markers, category bars, donuts and Y annotations.
var longRun = Enumerable.Range(0, 6000).Select(i => new ChartPoint(i, i % 997 == 500 ? null : Math.Round(100 + 40 * Math.Sin(i / 90.0) + i % 13, 1))).ToArray();
lines.Add($"guard/sampled-line {Hash(Render(line with { Series = [new("Long", longRun), new("Echo", longRun.Select(p => p with { Y = p.Y + 9 }).ToArray())] }))}");
lines.Add($"guard/sampled-area {Hash(Render(line with { Kind = ChartKind.Area, Theme = ChartTheme.Dark, Series = [new("Long", longRun)] }, includeTitles: false))}");
lines.Add($"guard/area-gaps {Hash(Render(Spec(ChartKind.Area, ChartTheme.Light) with { Series = [new("A", Points().Select((p, i) => p with { Y = i is 4 or 9 ? null : p.Y }).ToArray(), "#123456")] }))}");
lines.Add($"guard/markers {Hash(Render(scatter with { Series = [.. scatter.Series, new("Gaps", Points().Select((p, i) => p with { Y = i % 4 == 0 ? null : p.Y - 3 }).ToArray(), "#123456")] }))}");
lines.Add($"guard/bubble-dark {Hash(Render(Spec(ChartKind.Bubble, ChartTheme.Dark) with { Series = [new("B", Points().Select((p, i) => p with { Size = 1 + i * i }).ToArray())] }, includeTitles: false))}");
lines.Add($"guard/donut-many {Hash(Render(Spec(ChartKind.Donut, ChartTheme.Light) with { Series = [new("D", Enumerable.Range(0, 12).Select(i => new ChartPoint(i, 1 + i % 5, $"Slice {i}")).ToArray())] }))}");
ChartAnnotation[] YRefs(double at, double from, double to) => [new(AnnotationAxis.Y, at) { Label = "Line" }, new(AnnotationAxis.Y, from) { To = to, Label = "Band", Color = "#B03A2E" }];
foreach (var kind in new[] { ChartKind.Line, ChartKind.Area, ChartKind.Scatter, ChartKind.Column, ChartKind.Bar, ChartKind.StackedColumn, ChartKind.Band })
    lines.Add($"guard/y-annotations-{kind} {Hash(Render(Spec(kind, ChartTheme.Dark) with { Annotations = YRefs(25, 30, 40) }))}");
lines.Add($"guard/y-annotations-candles {Hash(Render(Spec(ChartKind.Candlestick, ChartTheme.Light) with { Annotations = YRefs(12, 14, 16) }))}");
lines.Add($"guard/y-annotations-log {Hash(Render(line with { YAxis = AxisKind.Log, Annotations = YRefs(25, 30, 40) }))}");
lines.Add($"guard/y-annotations-reversed {Hash(Render(line with { YReversed = true, Annotations = YRefs(25, 30, 40) }, includeTitles: false))}");
// 0.20.0: a zone-coloured line crossing several bounds over its bands, time in zone, grade colours and zone-coloured scatter.
var heart = ZoneScale.CogganHeartRate(170);
var stream = Enumerable.Range(0, 2400).Select(t => Math.Round(95 + 85 * (1 - Math.Exp(-t / 400.0)) + 12 * Math.Sin(t / 70.0) + t % 5, 1)).ToArray();
lines.Add($"zones/stream {Hash(Render(line with { XFormat = ValueFormat.Duration, YZones = heart, Series = [new("Heart rate", stream.Select((v, t) => new ChartPoint(t, v)).ToArray()) { Zones = heart }] }))}");
lines.Add($"zones/stream-dark {Hash(Render(line with { Theme = ChartTheme.Dark, YZones = heart, Annotations = [new(AnnotationAxis.Y, 150) { Label = "Target" }], Series = [new("Heart rate", stream.Take(300).Select((v, t) => new ChartPoint(t, v)).ToArray()) { Zones = heart }] }, includeTitles: false))}");
var seconds = Training.TimeInZone(stream, heart);
lines.Add($"zones/time-in-zone {Hash(Render(Spec(ChartKind.Bar, ChartTheme.Light) with { YFormat = ValueFormat.Duration, Series = [new("Time in zone", heart.Zones.Select((z, i) => new ChartPoint(i, seconds[i], z.Name) { Color = ChartStyle.Light.Zones[i] }).ToArray())] }))}");
double[] grade = [0, 1.5, 3, 6, 9, 7, 4, 1, -2, -5, -3, 0];
string Grade(double g) => g >= 6 ? "#DD4B45" : g >= 3 ? "#DB6A1F" : g >= 1 ? "#A88200" : "#2E9B58";
lines.Add($"zones/grade-area {Hash(Render(Spec(ChartKind.Area, ChartTheme.Light) with { Series = [new("Elevation", grade.Select((g, i) => new ChartPoint(i * 500, 300 + grade.Take(i).Sum() * 5) { Color = Grade(g) }).ToArray())] }))}");
lines.Add($"zones/scatter {Hash(Render(scatter with { YZones = new([new("Low", 25), new("Middle", 40, "#123456"), new("High", double.PositiveInfinity)]), Series = [.. scatter.Series.Select(s => s with { Zones = new([new("Low", 25), new("Middle", 40, "#123456"), new("High", double.PositiveInfinity)]) })] }))}");
// 0.20.1: zone bands on a horizontal bar chart share the annotation path the bar-chart fix touches, so they must not move.
lines.Add($"guard/bar-zone-bands {Hash(Render(Spec(ChartKind.Bar, ChartTheme.Dark) with { YZones = new([new("Low", 20), new("Middle", 40, "#123456"), new("High", double.PositiveInfinity)]) }))}");
// Guards for the paths 0.21.0 touches: the series loop and its order, each axis's zero, category slots, band extents,
// bubble sizes, trends beside a secondary series and a density note over two series.
var band = Spec(ChartKind.Band, ChartTheme.Light);
lines.Add($"guard/band-secondary {Hash(Render(band with { Y2Label = "Rate", Series = [band.Series[0],
    new("G", Enumerable.Range(0, 8).Select(i => ChartPoint.Interval(i, i % 3 == 1 ? null : 2 + i * .1, 1 + i * .1, 3 + i * .2)).ToArray()) { Secondary = true },
    new("H", Enumerable.Range(0, 8).Select(i => i == 4 ? new ChartPoint(i, 12) : ChartPoint.Interval(i, 12 + i, 11 + i, 14 + i)).ToArray())] }))}");
lines.Add($"guard/band-dark {Hash(Render(band with { Theme = ChartTheme.Dark, Series = [band.Series[0], new("H", Enumerable.Range(0, 8).Select(i => ChartPoint.Interval(i, 30 - i, 25 - i, 31 - i)).ToArray())] }, includeTitles: false))}");
var column = Spec(ChartKind.Column, ChartTheme.Light);
lines.Add($"guard/column-secondary {Hash(Render(column with { Y2Label = "Rate", Series = [.. column.Series, new("C", Points().Select((p, i) => p with { Y = i == 3 ? null : (p.Y - 20) / 10 }).ToArray()) { Secondary = true }] }))}");
lines.Add($"guard/column-three {Hash(Render(column with { Theme = ChartTheme.Dark, Series = [.. column.Series, new("C", Points().Select((p, i) => p with { Y = i % 4 == 0 ? null : 20 - p.Y }).ToArray(), "#123456")] }, includeTitles: false))}");
lines.Add($"guard/column-zones {Hash(Render(column with { Series = [column.Series[0] with { Zones = new([new("Low", 25), new("High", double.PositiveInfinity)]) }, column.Series[1] with { Points = column.Series[1].Points.Select((p, i) => i == 2 ? p with { Color = "#123456" } : p).ToArray() }] }))}");
lines.Add($"guard/stacked-signed {Hash(Render(Spec(ChartKind.StackedColumn, ChartTheme.Light) with { Series = [new("A", Points()), new("B", Points().Select(p => p with { Y = -p.Y / 2 }).ToArray()), new("C", Points().Select(p => p with { Y = p.Y % 7 - 3 }).ToArray())] }))}");
var bar = Spec(ChartKind.Bar, ChartTheme.Light);
lines.Add($"guard/bar-three {Hash(Render(bar with { Series = [.. bar.Series, new("C", Points().Select(p => p with { Y = p.Y - 25 }).ToArray())] }))}");
var area = Spec(ChartKind.Area, ChartTheme.Light);
lines.Add($"guard/area-secondary {Hash(Render(area with { Y2Label = "Rate", Series = [area.Series[0], new("R", Points().Select(p => p with { Y = 40 - p.Y }).ToArray()) { Secondary = true }] }))}");
lines.Add($"guard/line-intervals {Hash(Render(line with { Series = [new("I", Enumerable.Range(0, 8).Select(i => ChartPoint.Interval(i, 10 + i, -50, 90)).ToArray())] }))}");
lines.Add($"guard/bubble-small {Hash(Render(Spec(ChartKind.Bubble, ChartTheme.Light) with { Series = [new("S", Points().Select((p, i) => p with { Size = .01 + i * .02 }).ToArray()), new("T", Points().Select(p => p with { Y = p.Y + 3, Size = .5 }).ToArray())] }))}");
lines.Add($"guard/scatter-secondary-zero {Hash(Render(scatter with { IncludeZero = true, Series = [scatter.Series[0], scatter.Series[1] with { Secondary = true }] }))}");
lines.Add($"guard/line-order {Hash(Render(line with { Y2Label = "Half", Series = [new("A", Points()) { Trend = true }, new("B", Points().Select(p => p with { Y = 50 - p.Y }).ToArray()) { Trend = true }, new("C", Points().Select(p => p with { Y = p.Y / 2 }).ToArray()) { Secondary = true }] }))}");
lines.Add($"guard/density-two {Hash(Render(new ChartSpec { Kind = ChartKind.Scatter, DensityCells = 12, Series = [new("S", Enumerable.Range(0, 300).Select(i => new ChartPoint(i % 31, i % 17)).ToArray()), new("T", Enumerable.Range(0, 200).Select(i => new ChartPoint(i % 13 + 9, i % 11 + 4)).ToArray())] }))}");
// 0.21.0: several marks in one chart. Twelve weeks of simulated training and two planned, through the load model.
var first = new DateOnly(2026, 6, 1);
double Day(DateOnly day) => TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
var load = Training.Load(Enumerable.Range(0, 98).Select(i => (first.AddDays(i), (double)((i % 7) switch { 0 => 0, 1 => 70 + i % 5 * 6, 2 => 55, 3 => 90 + i / 7 * 3, 4 => i % 14 == 4 ? 0 : 45, 5 => 140 + i % 3 * 12, _ => 100 }))), 55, 55);
var planned = Day(first.AddDays(84));
ChartSpec Performance(ChartTheme theme) => new()
{
    Kind = ChartKind.Line, Theme = theme, XAxis = AxisKind.Time, Title = "Performance management", Description = "Fitness, fatigue and form", YLabel = "Training stress", Y2Label = "Form",
    Annotations = [new(AnnotationAxis.X, planned) { To = Day(first.AddDays(97)), Label = "Planned" }],
    Series = [ChartSeries.From("Fitness", load, d => Day(d.Day), d => Math.Round(d.Fitness, 1)) with { ProjectedFrom = planned },
        ChartSeries.From("Fatigue", load, d => Day(d.Day), d => Math.Round(d.Fatigue, 1)) with { ProjectedFrom = planned },
        ChartSeries.From("Form", load, d => Day(d.Day), d => Math.Round(d.Form, 1)) with { Kind = ChartKind.Area, Secondary = true, ProjectedFrom = planned },
        ChartSeries.From("Daily stress", load, d => Day(d.Day), d => d.Stress) with { Kind = ChartKind.Column }]
};
var weeks = Enumerable.Range(0, 12).Select(i => Day(first.AddDays(i * 7))).ToArray();
ChartSpec Target(ChartTheme theme) => new()
{
    Kind = ChartKind.Band, Theme = theme, XAxis = AxisKind.Time, Title = "Load against target", YLabel = "Weekly stress",
    Series = [new("Target", weeks.Select((x, i) => ChartPoint.Interval(x, 400 + i * 20, 340 + i * 17, 460 + i * 23)).ToArray()),
        new("Weekly load", weeks.Select((x, i) => new ChartPoint(x, 420 + i * 18 + (i % 4 == 3 ? -160 : i % 3 * 35))).ToArray()) { Kind = ChartKind.Column }]
};
double[] volume = [6.5, 7.2, 8.1, 5.0, 7.9, 8.8, 9.4, 5.6, 9.1, 10.2, 10.8, 6.0];
var rolling = Statistics.Rolling(volume.Select(v => (double?)v).ToArray(), 4, 1);
ChartSpec Weekly(ChartTheme theme) => new()
{
    Kind = ChartKind.Column, Theme = theme, Title = "Weekly volume", YLabel = "Hours",
    Series = [new("Volume", volume.Select((v, i) => new ChartPoint(i, v, $"W{i + 1}")).ToArray()),
        new("Four-week average", rolling.Select((r, i) => new ChartPoint(i, Math.Round(r!.Mean, 2), $"W{i + 1}")).ToArray()) { Kind = ChartKind.Line }]
};
lines.Add($"mixed/performance {Hash(Render(Performance(ChartTheme.Light)))}");
lines.Add($"mixed/performance-dark {Hash(Render(Performance(ChartTheme.Dark), includeTitles: false))}");
lines.Add($"mixed/target {Hash(Render(Target(ChartTheme.Light)))}");
lines.Add($"mixed/weekly-average {Hash(Render(Weekly(ChartTheme.Light)))}");
lines.Add($"mixed/weekly-two-columns {Hash(Render(Weekly(ChartTheme.Dark) with { Series = [.. Weekly(ChartTheme.Dark).Series, new("Last year", volume.Select((v, i) => new ChartPoint(i, v - 1.5, $"W{i + 1}")).ToArray())] }))}");
// Guards for the paths 0.22.0 touches: the frame, X labels and titles, the clip with its zones and annotations, column
// slots, and candles and bars on trading axes, each drawn once per pane from now on.
var opening = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero);
var sessions = Enumerable.Range(0, 45).Select(i => opening.AddDays(i)).Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && d.Date != new DateTime(2026, 4, 3)).Take(28).ToArray();
ChartPoint[] Prices() => sessions.Select((d, i) => ChartPoint.Candle(TimeAxis.Value(d), 100 + i, 104 + i + i % 3, 97 + i, 101 + i + (i % 2 == 0 ? 2 : -2))).ToArray();
ChartAnnotation[] Marked(double y) => [new(AnnotationAxis.X, TimeAxis.Value(sessions[5])) { To = TimeAxis.Value(sessions[9]), Label = "Window" }, new(AnnotationAxis.Y, y) { Label = "Level" }, new(AnnotationAxis.X, TimeAxis.Value(sessions[20])) { Label = "Event" }];
ChartSpec Trading(ChartKind kind, ChartTheme theme) => new() { Kind = kind, Theme = theme, Title = "Trading", XAxis = AxisKind.Time, SkipWeekends = true, TimeSkips = [TimeAxis.Day(new DateTime(2026, 4, 3))], XLabel = "Day", YLabel = "Price", Series = [new("P", Prices())] };
lines.Add($"guard/candles-trading {Hash(Render(Trading(ChartKind.Candlestick, ChartTheme.Light) with { MinorGridlines = true, Annotations = Marked(112), YZones = new([new("Low", 105), new("High", double.PositiveInfinity)]) }))}");
lines.Add($"guard/candles-log-reversed {Hash(Render(Trading(ChartKind.Candlestick, ChartTheme.Dark) with { YAxis = AxisKind.Log, YReversed = true, MinorGridlines = true, TimeZone = "America/New_York" }, includeTitles: false))}");
lines.Add($"guard/ohlc-trading {Hash(Render(Trading(ChartKind.Ohlc, ChartTheme.Light) with { Annotations = Marked(118), YFormat = ValueFormat.Compact }))}");
lines.Add($"guard/ohlc-zone-dark {Hash(Render(Trading(ChartKind.Ohlc, ChartTheme.Dark) with { TimeZone = "Asia/Kolkata", YReversed = true, Annotations = Marked(110) }, includeTitles: false))}");
ChartSeries[] Rated(bool trend) => [line.Series[0] with { Trend = trend }, line.Series[1] with { Secondary = true, Trend = trend, Points = line.Series[1].Points.Select(p => p with { Y = p.Y * 40 }).ToArray() }];
lines.Add($"guard/secondary-annotated {Hash(Render(line with { Y2Label = "Rate", MinorGridlines = true, Y2Format = ValueFormat.Compact, YZones = new([new("Low", 20), new("High", double.PositiveInfinity)]),
    Annotations = [new(AnnotationAxis.Y, 30) { Label = "Target" }, new(AnnotationAxis.X, 2) { To = 4, Label = "Window" }, new(AnnotationAxis.X, 9) { Label = "Launch", Dashed = false }], Series = Rated(true) }))}");
lines.Add($"guard/secondary-log-reversed {Hash(Render(line with { Theme = ChartTheme.Dark, Y2Label = "Rate", Y2Axis = AxisKind.Log, Y2Reversed = true, YReversed = true, MinorGridlines = true, Annotations = YRefs(25, 30, 40), Series = Rated(false) }, includeTitles: false))}");
lines.Add($"guard/duration-secondary {Hash(Render(line with { XFormat = ValueFormat.Duration, YFormat = ValueFormat.Duration, YReversed = true, Y2Format = ValueFormat.Duration, MinorGridlines = true, Y2Label = "Moving time",
    Annotations = [new(AnnotationAxis.Y, 300) { Label = "Target" }, new(AnnotationAxis.X, 1200) { To = 1800, Label = "Climb" }],
    Series = [new("Pace", Enumerable.Range(0, 12).Select(i => new ChartPoint(i * 300, 330 - i * 4 + i % 3 * 5)).ToArray()) { Trend = true }, new("Moving", Enumerable.Range(0, 12).Select(i => new ChartPoint(i * 300, i * 290d)).ToArray()) { Secondary = true, ProjectedFrom = 2400 }] }))}");
lines.Add($"guard/bar-minor {Hash(Render(bar with { MinorGridlines = true, XLabel = "Plan", YLabel = "Accounts", Annotations = YRefs(25, 30, 40), YZones = new([new("Low", 20), new("High", double.PositiveInfinity)]) }))}");
lines.Add($"guard/column-minor-secondary {Hash(Render(column with { MinorGridlines = true, Y2Label = "Rate", XLabel = "Month", YLabel = "Volume", Annotations = YRefs(25, 30, 40), Series = [.. column.Series, new("C", Points().Select(p => p with { Y = p.Y / 10 }).ToArray()) { Secondary = true, Kind = ChartKind.Line }] }))}");
lines.Add($"guard/density-secondary {Hash(Render(new ChartSpec { Kind = ChartKind.Scatter, DensityCells = 16, Y2Label = "Other", MinorGridlines = true, Series = [new("S", Enumerable.Range(0, 300).Select(i => new ChartPoint(i % 31, i % 17)).ToArray()), new("T", Enumerable.Range(0, 200).Select(i => new ChartPoint(i % 13 + 9, i % 11 * 40)).ToArray()) { Secondary = true }] }))}");
lines.Add($"guard/time-columns {Hash(Render(new ChartSpec { Kind = ChartKind.Line, XAxis = AxisKind.Time, SkipWeekends = true, Title = "Volume", MinorGridlines = true, Annotations = Marked(150),
    Series = [new("Close", Prices().Select(p => new ChartPoint(p.X, p.Close)).ToArray()), new("Volume", Prices().Select((p, i) => new ChartPoint(p.X, 80 + i * 7 % 50)).ToArray()) { Kind = ChartKind.Column }, new("Band", Prices().Select(p => ChartPoint.Interval(p.X, p.Close, p.Low!.Value, p.High!.Value)).ToArray()) { Kind = ChartKind.Band }] }))}");
lines.Add($"guard/area-zones-dark {Hash(Render(Spec(ChartKind.Area, ChartTheme.Dark) with { MinorGridlines = true, XLabel = "Month", YLabel = "Accounts", YZones = new([new("Low", 20), new("Middle", 40, "#123456"), new("High", double.PositiveInfinity)]), Annotations = [new(AnnotationAxis.X, 0) { To = 2 }, new(AnnotationAxis.X, 11) { Label = "End" }] }, includeTitles: false))}");
// 0.22.0: panes sharing one X axis. Candles with a five-day average and a volume pane on a trading axis, in both kinds and themes.
var traded = Prices();
var fiveDay = Statistics.Rolling(traded.Select(p => p.Close).ToArray(), 5);
ChartSpec Market(ChartKind kind, ChartTheme theme) => Trading(kind, theme) with
{
    Height = 520, Annotations = Marked(112), Panes = [new() { Label = "Volume", Weight = .4, YFormat = ValueFormat.Compact }],
    Series = [new("P", traded), new("Volume", traded.Select((p, i) => new ChartPoint(p.X, 1_200_000 + i * 370_000 % 900_000)).ToArray()) { Kind = ChartKind.Column, Pane = 1 },
        new("Five-day average", traded.Select((p, i) => new ChartPoint(p.X, fiveDay[i] is { } r ? Math.Round(r.Mean, 2) : null)).ToArray()) { Kind = ChartKind.Line }]
};
// An hour's interval run every 10 seconds: heart rate over its zones, pace on a reversed duration axis, and the climb as an area.
var effort = Enumerable.Range(0, 361).Select(i => i * 10).Select(t => t < 600 ? .3 + t / 2000d : t < 3000 ? (t - 600) % 420 < 240 ? 1 : .35 : .2).ToArray();
double[] beats = new double[361], paces = new double[361];
for (double i = 0, heartNow = 96, paceNow = 390; i < 361; i++)
{
    heartNow += (100 + 85 * effort[(int)i] - heartNow) * .2; paceNow += (400 - 160 * effort[(int)i] - paceNow) * .5;
    beats[(int)i] = Math.Round(heartNow + i % 5 - 2); paces[(int)i] = Math.Round(paceNow + i % 7 - 3);
}
var heartZones = ZoneScale.CogganHeartRate(170);
ChartSpec Stream(ChartTheme theme) => new()
{
    Kind = ChartKind.Line, Theme = theme, Title = "Activity stream", Description = "Heart rate, pace and climb", Height = 640, XFormat = ValueFormat.Duration, XLabel = "Elapsed time",
    YLabel = "Heart rate (bpm)", YZones = heartZones, MinorGridlines = true,
    Annotations = [new(AnnotationAxis.X, 600) { To = 840, Label = "First interval" }, new(AnnotationAxis.Y, 160) { Label = "Ceiling" }, new(AnnotationAxis.X, 3000) { Label = "Cool-down" }],
    Panes = [new() { Label = "Pace (min/km)", Weight = .6, YFormat = ValueFormat.Duration, YReversed = true }, new() { Label = "Climb (m)" }],
    Series = [new("Heart rate", beats.Select((b, i) => new ChartPoint(i * 10, b)).ToArray()) { Zones = heartZones },
        new("Pace", paces.Select((p, i) => new ChartPoint(i * 10, p)).ToArray()) { Pane = 1, Trend = true },
        new("Climb", Enumerable.Range(0, 361).Select(i => new ChartPoint(i * 10, Math.Round(24 + 16 * Math.Sin(i / 52d) + 6 * Math.Sin(i / 17d), 1))).ToArray()) { Pane = 2, Kind = ChartKind.Area }]
};
// A pane with a right-hand axis of its own under a main plot without one, and a logarithmic main plot over columns in a pane.
ChartSpec Paired(ChartTheme theme) => line with
{
    Theme = theme, Title = "Two axes in a pane", Height = 540, MinorGridlines = true, Annotations = [new(AnnotationAxis.X, 3) { To = 5, Label = "Window" }],
    Panes = [new() { Label = "Cadence (rpm)", Y2Label = "Power (W)", Y2Format = ValueFormat.Compact, Weight = .8, YZones = new([new("Low", 80), new("High", double.PositiveInfinity)]) }],
    Series = [line.Series[0], new("Cadence", Points().Select(p => p with { Y = 70 + p.Y / 2, Label = null }).ToArray()) { Pane = 1 },
        new("Power", Points().Select((p, i) => p with { Y = 1500 + i * 120, Label = null }).ToArray()) { Pane = 1, Secondary = true, ProjectedFrom = 8 }]
};
ChartSpec Logged() => line with
{
    YAxis = AxisKind.Log, Height = 480, Panes = [new() { Label = "Count", Weight = .7 }],
    Series = [line.Series[0], new("Count", Points().Select((p, i) => p with { Y = i % 4 * 3 + 1 }).ToArray()) { Kind = ChartKind.Column, Pane = 1 }]
};
lines.Add($"panes/candles-volume {Hash(Render(Market(ChartKind.Candlestick, ChartTheme.Light)))}");
lines.Add($"panes/candles-volume-dark {Hash(Render(Market(ChartKind.Candlestick, ChartTheme.Dark), includeTitles: false))}");
lines.Add($"panes/ohlc-volume {Hash(Render(Market(ChartKind.Ohlc, ChartTheme.Light)))}");
lines.Add($"panes/activity-stream {Hash(Render(Stream(ChartTheme.Light)))}");
lines.Add($"panes/activity-stream-dark {Hash(Render(Stream(ChartTheme.Dark), includeTitles: false))}");
lines.Add($"panes/secondary-in-pane {Hash(Render(Paired(ChartTheme.Light)))}");
lines.Add($"panes/log-over-columns {Hash(Render(Logged()))}");
// Guards for the paths 0.23.0 touches: the stylesheet in each preset and a brand, columns and bars with negative values on
// category and continuous axes, stacked ends, scatter and line markers, every gridline with its minor lines, the frame
// the statistical kinds share, areas below zero, secondary columns and panes.
var harbour = new ChartStyle { Background = "#F6F3EE", Text = "#1F2A37", Muted = "#4B5563", Grid = "#E5DED3", Edge = "#6B7280", Series = ["#1D4E89", "#B03A2E", "#2E7D5B", "#9A6A12"], FontFamily = "Georgia,Cambria,serif" };
foreach (var (name, style) in new[] { ("light", ChartStyle.Light), ("dark", ChartStyle.Dark), ("brand", harbour) })
{
    lines.Add($"guard/style-{name}-line {Hash(Render(line with { Style = style, MinorGridlines = true, Annotations = YRefs(25, 30, 40) }))}");
    lines.Add($"guard/style-{name}-column {Hash(Render(column with { Style = style, Series = [.. column.Series, new("N", Points().Select(p => p with { Y = 12 - p.Y }).ToArray())] }, includeTitles: false))}");
    lines.Add($"guard/style-{name}-graph {Hash(Graph(graph with { Style = style }))}");
}
ChartPoint[] Signed() => Points().Select((p, i) => p with { Y = i % 3 == 0 ? -p.Y / 2 : p.Y - 20 }).ToArray();
lines.Add($"guard/column-negative {Hash(Render(column with { Series = [new("A", Signed()), new("B", Signed().Select(p => p with { Y = -p.Y }).ToArray()) { Zones = new([new("Low", 0), new("High", double.PositiveInfinity)]) }] }))}");
lines.Add($"guard/column-negative-dark {Hash(Render(column with { Theme = ChartTheme.Dark, YFormat = ValueFormat.Compact, Series = [new("A", Signed().Select(p => p with { Y = p.Y * 1000 }).ToArray())] }, includeTitles: false))}");
lines.Add($"guard/bar-negative {Hash(Render(bar with { Series = [new("A", Signed()), new("B", Signed().Select((p, i) => p with { Y = p.Y / 2, Color = i == 4 ? "#123456" : null }).ToArray())] }))}");
lines.Add($"guard/bar-negative-dark {Hash(Render(bar with { Theme = ChartTheme.Dark, YFormat = ValueFormat.Duration, Series = [new("A", Signed().Select(p => p with { Y = p.Y * 60 }).ToArray())] }, includeTitles: false))}");
lines.Add($"guard/stacked-ends {Hash(Render(Spec(ChartKind.StackedColumn, ChartTheme.Dark) with { Series = [new("A", Signed()), new("B", Points()), new("C", Signed().Select(p => p with { Y = p.Y < 0 ? p.Y : null }).ToArray())] }))}");
lines.Add($"guard/continuous-columns-negative {Hash(Render(line with { Y2Label = "Rate", Series = [new("Line", Points()), new("Signed", Signed()) { Kind = ChartKind.Column }, new("Rate", Signed().Select(p => p with { Y = p.Y / 4 }).ToArray()) { Kind = ChartKind.Column, Secondary = true }] }))}");
lines.Add($"guard/scatter-markers {Hash(Render(scatter with { Theme = ChartTheme.Dark, Series = [scatter.Series[0] with { Points = scatter.Series[0].Points.Select((p, i) => p with { Color = i % 4 == 0 ? "#123456" : null, Y = i == 5 ? null : p.Y }).ToArray() }, scatter.Series[1] with { Secondary = true, Zones = new([new("Low", 25), new("High", double.PositiveInfinity)]) }] }, includeTitles: false))}");
lines.Add($"guard/line-markers {Hash(Render(line with { Series = [new("A", Points().Select((p, i) => p with { Color = i == 3 ? "#123456" : null, Y = i == 7 ? null : p.Y }).ToArray()), new("B", Points()) { Zones = new([new("Low", 25), new("High", double.PositiveInfinity)]), ProjectedFrom = 8 }] }))}");
lines.Add($"guard/area-negative {Hash(Render(Spec(ChartKind.Area, ChartTheme.Dark) with { MinorGridlines = true, Series = [new("A", Signed()), new("B", Signed().Select(p => p with { Y = p.Y / 3 }).ToArray()) { ProjectedFrom = 7.5 }] }))}");
lines.Add($"guard/area-negative-only {Hash(Render(Spec(ChartKind.Area, ChartTheme.Light) with { Series = [new("A", Points().Select(p => p with { Y = -p.Y }).ToArray())] }, includeTitles: false))}");
foreach (var kind in new[] { ChartKind.Histogram, ChartKind.Box, ChartKind.Violin })
{
    var framed = Spec(kind, ChartTheme.Light) with { MinorGridlines = true, XLabel = "Value", YLabel = "Count" };
    lines.Add($"guard/frame-minor-{kind} {Hash(Render(framed))}");
    lines.Add($"guard/frame-dark-{kind} {Hash(Render(framed with { Theme = ChartTheme.Dark, MinorGridlines = false, YFormat = kind == ChartKind.Histogram ? ValueFormat.Number : ValueFormat.Compact }, includeTitles: false))}");
}
lines.Add($"guard/radar-dark {Hash(Render(Spec(ChartKind.Radar, ChartTheme.Dark) with { MinorGridlines = true }))}");
lines.Add($"guard/grid-minor-time-dark {Hash(Render(line with { Theme = ChartTheme.Dark, XAxis = AxisKind.Time, MinorGridlines = true, Series = Unlabelled(x => 1767225600000d + x * 86400000d) }, includeTitles: false))}");
lines.Add($"guard/grid-minor-column-bar {Hash(Render(bar with { MinorGridlines = true, YFormat = ValueFormat.Compact, Theme = ChartTheme.Dark }))}");
lines.Add($"guard/panes-markers {Hash(Render(line with { Height = 560, MinorGridlines = true, Panes = [new() { Label = "Below", Weight = .7, Y2Label = "Rate" }, new() { Label = "Signed", Weight = .5 }],
    Series = [new("A", Points()) { Trend = true }, new("Area", Points().Select(p => p with { Y = p.Y / 2 }).ToArray()) { Pane = 1, Kind = ChartKind.Area }, new("Rate", Points().Select(p => p with { Y = 100 - p.Y }).ToArray()) { Pane = 1, Secondary = true, ProjectedFrom = 6 },
        new("Columns", Signed()) { Pane = 2, Kind = ChartKind.Column }, new("Dots", Signed().Select(p => p with { Y = p.Y + 3 }).ToArray()) { Pane = 2, Kind = ChartKind.Scatter }] }))}");
lines.Add($"guard/panes-markers-dark {Hash(Render(Stream(ChartTheme.Dark) with { MinorGridlines = false, Annotations = [] }))}");
// 0.23.0: the finish of a fitness app. Curves, fades, gradients, markers, capsules, value labels, grids, axis sides and Midnight.
var climb = Enumerable.Range(0, 120).Select(i => new ChartPoint(i * 30, Math.Round(24 + 16 * Math.Sin(i / 9d) + 6 * Math.Sin(i / 3.1), 1))).ToArray();
ChartSpec[] finish =
[
    new() { Kind = ChartKind.Area, Title = "Smooth fade", XFormat = ValueFormat.Duration, Series = [new("Climb", climb) { Curve = LineCurve.Smooth, Fill = AreaFill.Fade, StrokeWidth = 2, Markers = MarkerStyle.None }] },
    line with { Title = "Step", Series = [new("Record", Points().Select((p, i) => p with { Y = 40 - i / 3 * 4 - (i == 7 ? 2 : 0) }).ToArray()) { Curve = LineCurve.Step }, new("Gaps", Points().Select((p, i) => p with { Y = i is 5 ? null : p.Y / 2 }).ToArray()) { Curve = LineCurve.Step, Markers = MarkerStyle.Hollow }] },
    line with { Title = "Gradient on a log axis", YAxis = AxisKind.Log, MinorGridlines = true, Series = [new("Load", Points().Select((p, i) => p with { Y = Math.Pow(10, i * .3) }).ToArray()) { Gradient = [new(1, "#2E9B58"), new(30, "#A88200"), new(1000, "#DD4B45")], StrokeWidth = 3 }] },
    column with { Title = "Capsules", Style = ChartStyle.Light with { BarRadius = 9999 }, Series = [new("Week", Signed()) { ValueLabels = true, Fill = AreaFill.Fade }, new("Last", Points().Select(p => p with { Y = p.Y / 2 }).ToArray()) { ValueLabels = true }] },
    line with { Title = "Hidden markers", Series = [new("A", Points()) { Markers = MarkerStyle.None, StrokeWidth = 1.5 }, new("B", Points().Select(p => p with { Y = p.Y + 6 }).ToArray()) { Markers = MarkerStyle.Hollow, Curve = LineCurve.Smooth }] },
    line with { Title = "Latest reading", Theme = ChartTheme.Dark, Series = [new("Resting heart rate", Points().Select((p, i) => p with { Y = 52 - i % 4 + i / 5 }).ToArray()) { Markers = MarkerStyle.None, HighlightLast = true, Curve = LineCurve.Smooth, StrokeWidth = 3 }, new("Area", Points().Select((p, i) => p with { Y = 30 + i % 3 }).ToArray()) { Kind = ChartKind.Area, HighlightLast = true, Fill = AreaFill.Fade }] },
    line with { Title = "Phone axis", YAxisSide = AxisSide.Right, YTickLabels = TickLabels.Ends, YLabel = "bpm", MinorGridlines = true, Style = ChartStyle.Light with { Gridlines = GridLine.Dotted } },
    line with { Title = "Smooth zones", XFormat = ValueFormat.Duration, YZones = heartZones, Series = [new("Heart rate", beats.Take(120).Select((b, i) => new ChartPoint(i * 10, b)).ToArray()) { Zones = heartZones, Curve = LineCurve.Smooth, ProjectedFrom = 905, Markers = MarkerStyle.None }] },
    Performance(ChartTheme.Light) with { Style = ChartStyle.Midnight, Series = [.. Performance(ChartTheme.Light).Series.Select(s => s.Name == "Fitness" ? s with { HighlightLast = true, Curve = LineCurve.Smooth } : s.Name == "Form" ? s with { Fill = AreaFill.Fade, Curve = LineCurve.Smooth } : s)] },
    Weekly(ChartTheme.Light) with { Style = ChartStyle.Midnight, Series = [Weekly(ChartTheme.Light).Series[0] with { ValueLabels = true }, Weekly(ChartTheme.Light).Series[1] with { Curve = LineCurve.Smooth, Markers = MarkerStyle.Hollow }] },
    bar with { Title = "Bars", Style = ChartStyle.Light with { BarRadius = 6, Gridlines = GridLine.Dashed }, YTickLabels = TickLabels.Ends, Series = [new("A", Signed()) { ValueLabels = true }] },
    Spec(ChartKind.StackedColumn, ChartTheme.Light) with { Title = "Stacked capsules", Style = ChartStyle.Midnight, Series = [new("A", Signed()), new("B", Points()), new("C", Signed().Select(p => p with { Y = p.Y < 0 ? p.Y : null }).ToArray())] },
    Spec(ChartKind.Box, ChartTheme.Light) with { Title = "Box on the right", YAxisSide = AxisSide.Right, YTickLabels = TickLabels.Ends, YLabel = "Value", Style = ChartStyle.Light with { Gridlines = GridLine.Hidden } },
    line with { Title = "Reversed gradient", YReversed = true, YFormat = ValueFormat.Duration, Series = [new("Pace", Enumerable.Range(0, 30).Select(i => new ChartPoint(i, 330 - i * 3 + i % 4 * 6)).ToArray()) { Gradient = [new(260, "#DD4B45"), new(300, "#A88200"), new(340, "#3F87D9")], Curve = LineCurve.Smooth, HighlightLast = true }] },
    Stream(ChartTheme.Dark) with { Style = ChartStyle.Midnight, Series = [Stream(ChartTheme.Dark).Series[0] with { Zones = null, Gradient = [new(115, "#4C9DFF"), new(145, "#36D27A"), new(165, "#F5C518"), new(180, "#FF5A5A")], Markers = MarkerStyle.None }, Stream(ChartTheme.Dark).Series[1] with { Curve = LineCurve.Smooth, Markers = MarkerStyle.None }, Stream(ChartTheme.Dark).Series[2] with { Curve = LineCurve.Smooth, Fill = AreaFill.Fade, Markers = MarkerStyle.None }] },
    scatter with { Title = "Scatter markers", Style = ChartStyle.Midnight, Series = [scatter.Series[0] with { Markers = MarkerStyle.Filled }, scatter.Series[1] with { Markers = MarkerStyle.Hollow }] },
];
string[] names = ["smooth-fade-area", "step-line", "gradient-log", "capsule-value-labels", "hidden-markers", "highlight-last", "dotted-right-ends", "smooth-zones",
    "midnight-mixed", "midnight-weekly", "bars-dashed-ends", "stacked-capsules", "box-right-hidden", "gradient-reversed", "midnight-stream", "scatter-markers"];
for (var i = 0; i < finish.Length; i++) lines.Add($"finish/{names[i]} {Hash(Render(finish[i]))}");
// 0.26.0: gauges and rings. A zoned 270-degree recovery gauge with a target, a semicircle, a duration gauge, a full circle
// with a gradient, three rings with one past its goal and one capped at three laps, and Midnight versions.
var recovery = new ZoneScale([new("Low", 33, "#DD4B45"), new("Moderate", 66, "#A88200"), new("Good", double.PositiveInfinity, "#2E9B58")]);
var gauge = new ChartSpec { Kind = ChartKind.Gauge, Title = "Recovery", Description = "Today's recovery score", YLabel = "%", YZones = recovery,
    Annotations = [new(AnnotationAxis.Y, 60) { Label = "Average" }], Series = [new("Recovery", [new(0, 72, "Recovery")])] };
var rings = new ChartSpec { Kind = ChartKind.Ring, Title = "Activity", Description = "Move, exercise and stand", Width = 540, Height = 360,
    Series = [new("Move", [new(0, 540, "kcal")], "#DD4B45") { Goal = 600 }, new("Exercise", [new(0, 47, "min")], "#2E9B58") { Goal = 30 }, new("Stand", [new(0, 9, "h")], "#3F87D9") { Goal = 12 }] };
(string Name, ChartSpec Spec)[] radial = [
    ("gauge-recovery-270", gauge),
    ("gauge-recovery-dark", gauge with { Theme = ChartTheme.Dark, Series = [new("Recovery", [new(0, 24, "Recovery")])] }),
    ("gauge-semicircle-180", gauge with { GaugeSweep = 180, Width = 540, Height = 320, Annotations = [], Series = [new("Recovery", [new(0, 48, "Recovery")])] }),
    ("gauge-duration", new ChartSpec { Kind = ChartKind.Gauge, Title = "Sleep", Description = "Time asleep against eight hours", YFormat = ValueFormat.Duration, YMin = 0, YMax = 36000,
        Annotations = [new(AnnotationAxis.Y, 28800) { Label = "Goal" }], Series = [new("Sleep", [new(0, 25740, "Asleep")])] }),
    ("gauge-full-gradient", new ChartSpec { Kind = ChartKind.Gauge, Title = "Strain", Description = "Day strain on WHOOP's 0 to 21 scale", GaugeSweep = 360, YMin = 0, YMax = 21, Width = 400, Height = 360,
        Series = [new("Strain", [new(0, 14.2, "Strain")]) { Gradient = [new(0, "#3F87D9"), new(10, "#A88200"), new(21, "#DD4B45")] }] }),
    ("gauge-clamped", gauge with { Annotations = [], Series = [new("Recovery", [new(0, 104, "Recovery")])] }),
    ("rings-overflow", rings),
    ("rings-capped", rings with { Series = [rings.Series[0], rings.Series[1] with { Points = [new(0, 130, "min")] }, rings.Series[2] with { Points = [new(0, 0, "h")] }] }),
    ("rings-one-phone", rings with { Width = 337, Series = [rings.Series[1]] }),
    ("rings-six", rings with { Series = [.. Enumerable.Range(0, 6).Select(i => new ChartSeries($"R{i}", [new(0, 20 + i * 30)]))] }),
    ("gauge-midnight", gauge with { Style = ChartStyle.Midnight, YZones = new([new("Low", 33, ChartStyle.Midnight.Zones[5]), new("Moderate", 66, ChartStyle.Midnight.Zones[3]), new("Good", double.PositiveInfinity, ChartStyle.Midnight.Zones[2])]) }),
    ("rings-midnight", rings with { Style = ChartStyle.Midnight, Series = [.. rings.Series.Select((s, i) => s with { Color = ChartStyle.Midnight.Zones[new[] { 5, 2, 1 }[i]] })] }),
];
foreach (var (name, spec) in radial) lines.Add($"radial/{name} {Hash(Render(spec))}");
// 0.27.0: state timelines and range bars. A night's hypnogram on a time axis in a zone with an event marked, an intraday state
// timeline without connectors, daily heart-rate ranges with their averages, sleep timing on a reversed time-of-day axis, a range
// series beside a line and in a column chart's slots, and Midnight versions.
var evening = TimeAxis.Value(new DateTimeOffset(2026, 9, 26, 22, 46, 0, TimeSpan.FromHours(2)));
(string Stage, int Minutes)[] stages = [("Awake", 13), ("Light", 29), ("Deep", 25), ("Light", 24), ("REM", 9), ("Awake", 3), ("Light", 29), ("Deep", 18), ("Light", 24), ("REM", 13),
    ("Light", 30), ("Deep", 11), ("Light", 25), ("REM", 22), ("Light", 31), ("Deep", 4), ("Light", 25), ("REM", 27), ("Awake", 1), ("Light", 53), ("REM", 33), ("Awake", 9)];
string[] lanes = ["Awake", "REM", "Light", "Deep"];
var night = lanes.ToDictionary(lane => lane, _ => new List<ChartPoint>());
var clock = evening;
foreach (var (stage, minutes) in stages) { night[stage].Add(ChartPoint.Span(clock, clock + minutes * 60_000d)); clock += minutes * 60_000d; }
ChartSpec Hypnogram(ChartStyle? style, IReadOnlyList<string> zones) => new()
{
    Kind = ChartKind.Timeline, Style = style, XAxis = AxisKind.Time, TimeZone = "Africa/Johannesburg", Title = "7 h 12 min asleep, 58 min deep", Description = "Into Sunday 27 September",
    XLabel = "Time (Johannesburg)", Width = 1100, Height = 360, Source = "Source: simulated night", Annotations = [new(AnnotationAxis.X, evening + 200 * 60_000d) { Label = "Alarm" }],
    Series = lanes.Select((lane, i) => new ChartSeries(lane, night[lane], zones[new[] { 4, 1, 0, 6 }[i]])).ToArray()
};
var morning = TimeAxis.Value(new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero));
ChartSpec Intraday(ChartTheme theme) => new()
{
    Kind = ChartKind.Timeline, Theme = theme, XAxis = AxisKind.Time, TimelineConnectors = false, Title = "A working day", Width = 900, Height = 300, YAxisSide = AxisSide.Right,
    Series = [new("Rest", [ChartPoint.Span(morning + 6 * 3600e3, morning + 8 * 3600e3), ChartPoint.Span(morning + 12 * 3600e3, morning + 13 * 3600e3)]),
        new("Stress", [ChartPoint.Span(morning + 8 * 3600e3, morning + 11 * 3600e3, "Meetings"), ChartPoint.Span(morning + 15 * 3600e3, morning + 17 * 3600e3)]),
        new("Activity", [ChartPoint.Span(morning + 10.5 * 3600e3, morning + 11.5 * 3600e3, "Walk"), ChartPoint.Span(morning + 17.5 * 3600e3, morning + 18.5 * 3600e3, "Run")])]
};
var fortnight = Enumerable.Range(0, 14).Select(d => new DateOnly(2026, 9, 14).AddDays(d)).ToArray();
double Morning(DateOnly day) => TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
string Short(DateOnly day) => day.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture);
var heartRates = fortnight.Select((day, d) => (Day: day, Low: 46d + d % 5, High: d % 7 is 1 or 3 or 5 ? 150d + d * 2 : 105d + d % 4 * 5, Average: 66d + d % 6)).ToArray();
ChartSpec Heart(ChartStyle? style, string ink) => new()
{
    Kind = ChartKind.Range, Style = style, XAxis = AxisKind.Time, Title = "52 to 174 bpm today", Description = "Each day's lowest and highest heart rate, the dot its average",
    Width = 540, Height = 360, YLabel = "Heart rate (bpm)", Series = [new("Heart rate", heartRates.Select(r => ChartPoint.Interval(Morning(r.Day), r.Average, r.Low, r.High, Short(r.Day))).ToArray(), ink)]
};
ChartSpec Timing(ChartTheme theme) => new()
{
    Kind = ChartKind.Range, Theme = theme, XAxis = AxisKind.Time, YFormat = ValueFormat.TimeOfDay, YReversed = true, YMin = 75600, YMax = 118800, Title = "In bed at 22:52 on average",
    Width = 540, Height = 360, YLabel = "Clock time", MinorGridlines = true, Annotations = [new(AnnotationAxis.Y, 82800) { Label = "Target bedtime" }],
    Series = [new("Sleep", fortnight.Select((day, d) => ChartPoint.Interval(Morning(day), null, 81000 + d * 397 % 3600, 109800 + d * 613 % 2700, Short(day))).ToArray())]
};
(string Name, ChartSpec Spec)[] spans = [
    ("timeline/hypnogram-zone", Hypnogram(null, ChartStyle.Light.Zones)),
    ("timeline/no-connectors", Intraday(ChartTheme.Light)),
    ("timeline/no-connectors-dark", Intraday(ChartTheme.Dark)),
    ("timeline/hypnogram-phone", Hypnogram(ChartStyle.Dark, ChartStyle.Light.Zones) with { Width = 337 }),
    ("range/heart-rate", Heart(null, ChartStyle.Light.Zones[5])),
    ("range/sleep-timing-reversed", Timing(ChartTheme.Light)),
    ("range/sleep-timing-dark", Timing(ChartTheme.Dark)),
    ("range/beside-line", line with { XAxis = AxisKind.Time, Title = "Resting heart rate and the day's range", YLabel = "bpm",
        Series = [new("Range", Heart(null, "#DD4B45").Series[0].Points) { Kind = ChartKind.Range }, new("Resting", heartRates.Select(r => new ChartPoint(Morning(r.Day), r.Low)).ToArray()) { HighlightLast = true, Curve = LineCurve.Smooth }] }),
    ("range/column-slots", column with { Series = [column.Series[0], new("Spread", Points().Select(p => ChartPoint.Interval(p.X, p.Y, p.Y!.Value - 6, p.Y.Value + 4, p.Label)).ToArray()) { Kind = ChartKind.Range }] }),
    ("range/log-reversed", line with { Kind = ChartKind.Range, YAxis = AxisKind.Log, YReversed = true, Series = [new("Load", Points().Select((p, i) => ChartPoint.Interval(p.X, Math.Pow(10, 1 + i * .2), Math.Pow(10, .8 + i * .2), Math.Pow(10, 1.3 + i * .2), p.Label)).ToArray())] }),
    ("timeline/hypnogram-midnight", Hypnogram(ChartStyle.Midnight, ChartStyle.Midnight.Zones)),
    ("range/heart-rate-midnight", Heart(ChartStyle.Midnight, ChartStyle.Midnight.Zones[5])),
];
foreach (var (name, spec) in spans) lines.Add($"{name} {Hash(Render(spec))}");
// 0.28.0: calendars. Sixteen weeks of daily stress as a contribution grid in tiers, read in New York, with today outlined; three
// months of runs as bubbles on the ramp; dots; the ramp without zones and a duration format; a race outlined in its own colour;
// Sunday as the week start; a year across New Year at a phone's width; and Midnight versions.
var june = new DateOnly(2026, 6, 8);
double At(DateOnly day, double hours = 0) => TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)) + hours * 3600e3;
var season = Enumerable.Range(0, 112).Select(d => (Day: june.AddDays(d), Stress: d % 7 == 0 ? 0d : (d * 37 % 11) * 21 + (d % 7 == 5 ? 90 : 12))).ToArray();
ZoneScale Tiers(IReadOnlyList<string> zones) => new([new("Easy", 50, zones[1]), new("Moderate", 100, zones[2]), new("Hard", 150, zones[3]), new("Very hard", double.PositiveInfinity, zones[5])]);
// Each day's stress is logged at 02:00 UTC, which is the evening before in New York.
ChartSpec Weeks(ChartStyle? style, IReadOnlyList<string> zones) => new()
{
    Kind = ChartKind.Calendar, Style = style, XAxis = AxisKind.Time, TimeZone = "America/New_York", Title = "93 days trained in 16 weeks", Description = "Each day's training stress, in tiers",
    Width = 540, Height = 360, Source = "Source: simulated season", YZones = Tiers(zones), Annotations = [new(AnnotationAxis.X, At(june.AddDays(111), 14)) { Label = "Today" }],
    Series = [new("Training stress", season.Select(d => new ChartPoint(At(d.Day, 26), d.Stress)).ToArray())]
};
var runs = Enumerable.Range(0, 92).Where(d => d % 7 is 1 or 3 or 6).Select(d => (Day: new DateOnly(2026, 7, 1).AddDays(d), Km: Math.Round(5 + d * 17 % 12 + (d % 7 == 6 ? 6 : 0) + .4, 1))).ToArray();
ChartSpec Bubbles(ChartStyle? style) => new()
{
    Kind = ChartKind.Calendar, Style = style, XAxis = AxisKind.Time, CalendarLayout = CalendarLayout.Months, CalendarCell = CalendarCell.Bubble, Title = "Running, July to September",
    Description = "Each run's distance, the longest filling its day", Width = 900, Height = 420, XMax = At(new DateOnly(2026, 9, 30)),
    Series = [new("Distance (km)", runs.Select(r => new ChartPoint(At(r.Day, 7), r.Km, r.Km > 15 ? "Long run" : null)).ToArray())]
};
(string Name, ChartSpec Spec)[] calendars = [
    ("calendar/weeks-zones-new-york", Weeks(null, ChartStyle.Light.Zones)),
    ("calendar/weeks-zones-dark", Weeks(ChartStyle.Dark, ChartStyle.Light.Zones)),
    ("calendar/months-bubbles", Bubbles(null)),
    ("calendar/dots", Weeks(null, ChartStyle.Light.Zones) with { CalendarCell = CalendarCell.Dot, TimeZone = null }),
    ("calendar/ramp-duration", Weeks(null, ChartStyle.Light.Zones) with { YZones = null, YFormat = ValueFormat.Duration, Annotations = [],
        Series = [new("Time", season.Select(d => new ChartPoint(At(d.Day, 12), d.Stress * 30)).ToArray())] }),
    ("calendar/race-outlined", Weeks(null, ChartStyle.Light.Zones) with { Annotations = [new(AnnotationAxis.X, At(june.AddDays(97), 9)) { Label = "10 km race", Color = "#DD4B45" }, new(AnnotationAxis.X, At(june.AddDays(111), 14))] }),
    ("calendar/sunday-start", Weeks(null, ChartStyle.Light.Zones) with { WeekStart = DayOfWeek.Sunday, CalendarLayout = CalendarLayout.Months, Width = 900, Height = 420 }),
    ("calendar/year-phone", new ChartSpec { Kind = ChartKind.Calendar, XAxis = AxisKind.Time, TimeZone = "Africa/Johannesburg", Title = "A year of commits", Width = 337, Height = 300,
        Series = [new("Commits", Enumerable.Range(0, 365).Select(i => new ChartPoint(At(new DateOnly(2025, 10, 1).AddDays(i), 21.5), i * 7 % 11 < 4 ? null : i * 13 % 9)).ToArray())] }),
    ("calendar/weeks-midnight", Weeks(ChartStyle.Midnight, ChartStyle.Midnight.Zones)),
    ("calendar/months-midnight", Bubbles(ChartStyle.Midnight) with { YZones = new([new("Run", double.PositiveInfinity, ChartStyle.Midnight.Zones[1])]) }),
];
foreach (var (name, spec) in calendars) lines.Add($"{name} {Hash(Render(spec))}");
// 0.29.0: blocks. Six laps of a progression run on a reversed pace axis, each as wide as its distance, with the average pace; a
// threshold workout's steps in Coggan's power levels with the executed power over them; blocks as a series' own kind beside a line;
// blocks touching and apart, one too narrow for the hairline; blocks in a pane under a line, on a time axis in their own colours and
// on a secondary axis; and Midnight versions.
(double Km, double Pace)[] laps = [(2, 336), (2, 318), (1.5, 301), (1.5, 289), (1, 276), (.42, 262)];
ChartSpec Laps(ChartStyle? style)
{
    var at = 0d;
    var blocks = laps.Select((lap, i) => { var block = ChartPoint.Block(at, Math.Round(at + lap.Km, 2), lap.Pace, $"Lap {i + 1}"); at = Math.Round(at + lap.Km, 2); return block; }).ToArray();
    return new() { Kind = ChartKind.Blocks, Style = style, Title = "8.4 km at 5:06 per km", Description = "Each lap's pace, as wide as the lap is long", Width = 540, Height = 360,
        XLabel = "Distance (km)", YLabel = "Pace (/km)", YFormat = ValueFormat.Duration, YReversed = true, Annotations = [new(AnnotationAxis.Y, 306) { Label = "Average" }],
        Series = [new("Laps", blocks)] };
}
const double ftp = 250;
(string Name, double Minutes, double Fraction)[] workout = [("Warm-up", 10, .55), ("Build", 5, .75), ("Interval 1", 8, 1), ("Recovery", 4, .5), ("Interval 2", 8, 1.02),
    ("Recovery", 4, .5), ("Interval 3", 8, 1.05), ("Cool-down", 6, .45)];
ChartSpec Workout(ChartStyle? style)
{
    var at = 0d;
    var plan = workout.Select(step => { var block = ChartPoint.Block(at, at + step.Minutes * 60, Math.Round(ftp * step.Fraction), step.Name); at += step.Minutes * 60; return block; }).ToArray();
    var done = Enumerable.Range(0, (int)(at / 30)).Select(i => new ChartPoint(i * 30 + 15, plan.Last(b => b.X <= i * 30 + 15).Y!.Value + i * 37 % 23 - 11)).ToArray();
    return new() { Kind = ChartKind.Blocks, Style = style, IncludeZero = true, XFormat = ValueFormat.Duration, Title = "3 × 8 min at threshold", Description = "The plan in Coggan's power levels, the ride over it",
        XLabel = "Elapsed time", YLabel = "Power (W)", Annotations = [new(AnnotationAxis.Y, ftp) { Label = "FTP" }],
        Series = [new("Plan", plan) { Zones = ZoneScale.CogganPower(ftp) }, new("Power", done) { Kind = ChartKind.Line }] };
}
ChartPoint[] Steps() => [ChartPoint.Block(0, 4, 20, "Easy"), ChartPoint.Block(4, 8, 35, "Hard"), ChartPoint.Block(8, 11, 25, "Steady")];
var monday = TimeAxis.Value(new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero));
(string Name, ChartSpec Spec)[] blocked = [
    ("blocks/laps-reversed-pace", Laps(null)),
    ("blocks/workout-zoned-power", Workout(null)),
    ("blocks/beside-line", line with { Title = "A plan beside the line", Series = [new("Plan", Steps()) { Kind = ChartKind.Blocks }, line.Series[0]] }),
    ("blocks/touching-and-apart", new ChartSpec { Kind = ChartKind.Blocks, Title = "Touching and apart", Series = [new("A", [ChartPoint.Block(0, 1, 3), ChartPoint.Block(1, 2, 5), ChartPoint.Block(3, 4, 4), ChartPoint.Block(4, 4.004, 6), ChartPoint.Block(4.004, 5, 2)])] }),
    ("blocks/pane", line with { Title = "Blocks in a pane", Panes = [new() { Label = "Plan" }], Series = [line.Series[0], new("Plan", Steps()) { Kind = ChartKind.Blocks, Pane = 1 }] }),
    ("blocks/time-coloured", new ChartSpec { Kind = ChartKind.Blocks, XAxis = AxisKind.Time, Title = "Weekly volume", YLabel = "Stress per week", IncludeZero = true,
        Series = [new("Weeks", Enumerable.Range(0, 6).Select(i => ChartPoint.Block(monday + i * 7 * 86400000d, monday + (i + 1) * 7 * 86400000d, 320 + i % 3 * 60, $"Week {i + 1}") with { Color = i == 3 ? "#DD4B45" : null }).ToArray())] }),
    ("blocks/secondary", line with { Title = "Blocks on the right", Y2Label = "Plan", Series = [line.Series[0], new("Plan", Steps()) { Kind = ChartKind.Blocks, Secondary = true }] }),
    ("blocks/laps-midnight", Laps(ChartStyle.Midnight)),
    ("blocks/workout-midnight", Workout(ChartStyle.Midnight)),
];
foreach (var (name, spec) in blocked) lines.Add($"{name} {Hash(Render(spec))}");
// 0.30.0: graphs top to bottom, and graphs fitted to a width. The graph above in both layouts and themes set top to bottom, which
// a circular graph ignores; then it and the gallery's pipeline, whose labels decide where it turns, fitted to a phone and to a
// wide screen, and the pipeline on a phone in Midnight.
foreach (var layout in Enum.GetValues<GraphLayout>())
    foreach (var theme in Enum.GetValues<ChartTheme>())
        lines.Add($"graph/{layout}/{theme}/TopToBottom {Hash(Graph(graph with { Layout = layout, Theme = theme, Direction = GraphDirection.TopToBottom }))}");
var pipeline = new GraphSpec
{
    Title = "From source to insight",
    Nodes = [new("sources", "Sources"), new("ingest", "Ingestion"), new("validate", "Validation"), new("transform", "Transform"), new("charts", "Charts"), new("api", "Chart API"), new("reports", "Reports")],
    Edges = [new("sources", "ingest"), new("ingest", "validate"), new("validate", "transform"), new("transform", "charts"), new("transform", "api"), new("charts", "reports"), new("api", "reports"), new("ingest", "reports", "audit trail")]
};
foreach (var layout in Enum.GetValues<GraphLayout>())
    foreach (var width in new[] { 360, 1200 })
    {
        lines.Add($"fit/{layout}/{width} {Hash(Graph(GraphEngine.Fit(graph with { Layout = layout }, width)))}");
        lines.Add($"fit/pipeline-{layout}/{width} {Hash(Graph(GraphEngine.Fit(pipeline with { Layout = layout }, width)))}");
    }
lines.Add($"fit/pipeline-midnight-360 {Hash(Graph(GraphEngine.Fit(pipeline with { Style = ChartStyle.Midnight }, 360)))}");
// 0.31.0: edges that meet a node at the foot of its label, edge labels at the first free place along their edge, and a circle stood
// in from the sides by half its widest label. The gallery's pipeline in each layout at its own 900 pixels and fitted to a 337-pixel
// phone, and a crowded graph of long labels, half its edges labelled, in each layout at its own size, where some labels find no
// free place.
foreach (var layout in Enum.GetValues<GraphLayout>())
{
    lines.Add($"graph/pipeline-{layout}/900 {Hash(Graph(pipeline with { Layout = layout }))}");
    lines.Add($"fit/pipeline-{layout}/337 {Hash(Graph(GraphEngine.Fit(pipeline with { Layout = layout }, 337)))}");
}
var crowded = new GraphSpec
{
    Title = "A crowded pipeline",
    Nodes = [new("orders", "Customer orders feed"), new("crm", "CRM contacts export"), new("web", "Web analytics events"), new("lake", "Raw data lake (landing zone)"), new("clean", "Cleansing and dedupe"),
        new("join", "Identity resolution"), new("model", "Revenue attribution model"), new("warehouse", "Analytics warehouse"), new("dash", "Executive dashboards"), new("alerts", "Anomaly alerts")],
    Edges = [new("orders", "lake", "nightly"), new("crm", "lake", "hourly"), new("web", "lake", "stream"), new("lake", "clean"), new("clean", "join"), new("crm", "join", "match keys"), new("join", "model"),
        new("clean", "warehouse", "audited rows"), new("model", "warehouse"), new("warehouse", "dash"), new("warehouse", "alerts", "thresholds"), new("model", "alerts"), new("web", "dash", "live"), new("orders", "model")]
};
foreach (var layout in Enum.GetValues<GraphLayout>())
    lines.Add($"graph/crowded-{layout} {Hash(Graph(crowded with { Layout = layout }))}");
// 0.32.0: trend families. A seven-point moving average, a quadratic and an exponential in each theme; a cubic and a quartic; an
// exponential on a logarithmic axis, where it is straight; a moving average across gaps in a line; a cubic on a trading axis that
// skips weekends and a holiday; and in Midnight a load test's quadratic and exponential, one on each axis, and nightly HRV over its
// baseline with its seven-night average.
ChartPoint[] Wave() => Enumerable.Range(0, 40).Select(i => new ChartPoint(i, Math.Round(50 + 12 * Math.Sin(i / 5d) + i * .6 + i % 4 * 2, 1))).ToArray();
ChartPoint[] Arc() => Enumerable.Range(0, 24).Select(i => new ChartPoint(i * 5, Math.Round(10 + 6 * i - .25 * i * i + i % 3 * 3, 1))).ToArray();
ChartPoint[] Growth() => Enumerable.Range(0, 20).Select(i => new ChartPoint(i, Math.Round(3 * Math.Exp(.22 * i) * (1 + (i % 5 - 2) * .06), 2))).ToArray();
foreach (var theme in Enum.GetValues<ChartTheme>())
{
    lines.Add($"trend/moving-average/{theme} {Hash(Render(Spec(ChartKind.Line, theme) with { Title = "Moving average", Series = [new("Daily", Wave()) { Kind = ChartKind.Scatter, Trend = true, TrendFit = TrendFit.MovingAverage }] }))}");
    lines.Add($"trend/quadratic/{theme} {Hash(Render(Spec(ChartKind.Scatter, theme) with { Title = "Quadratic", Series = [new("Arc", Arc()) { Trend = true, TrendFit = TrendFit.Polynomial }] }))}");
    lines.Add($"trend/exponential/{theme} {Hash(Render(Spec(ChartKind.Scatter, theme) with { Title = "Exponential", Series = [new("Growth", Growth()) { Trend = true, TrendFit = TrendFit.Exponential }] }))}");
}
lines.Add($"trend/cubic {Hash(Render(Spec(ChartKind.Scatter, ChartTheme.Light) with { Title = "Cubic", Series = [new("Arc", Arc()) { Trend = true, TrendFit = TrendFit.Polynomial, TrendDegree = 3 }, new("Wave", Wave().Select(p => p with { X = p.X * 3 }).ToArray()) { Trend = true, TrendFit = TrendFit.Polynomial, TrendDegree = 3 }] }))}");
lines.Add($"trend/quartic {Hash(Render(line with { Title = "Quartic", Series = [new("Wave", Wave()) { Trend = true, TrendFit = TrendFit.Polynomial, TrendDegree = 4, StrokeWidth = 2 }] }, includeTitles: false))}");
lines.Add($"trend/exponential-log {Hash(Render(Spec(ChartKind.Scatter, ChartTheme.Light) with { Title = "Exponential on a log axis", YAxis = AxisKind.Log, MinorGridlines = true, Series = [new("Growth", Growth()) { Trend = true, TrendFit = TrendFit.Exponential }] }))}");
lines.Add($"trend/moving-average-gaps {Hash(Render(line with { Title = "Moving average across gaps", Series = [new("Daily", Wave().Select((p, i) => i % 9 is 4 or 5 or 6 || i is 20 or 22 ? p with { Y = null } : p).ToArray()) { Trend = true, TrendFit = TrendFit.MovingAverage, TrendPoints = 5 }] }))}");
lines.Add($"trend/cubic-trading {Hash(Render(Trading(ChartKind.Line, ChartTheme.Light) with { Title = "Cubic on a trading axis", Kind = ChartKind.Scatter,
    Series = [new("Close", sessions.Select((d, i) => new ChartPoint(TimeAxis.Value(d), 100 + 2 * i - .08 * i * i + i % 3)).ToArray()) { Trend = true, TrendFit = TrendFit.Polynomial, TrendDegree = 3 }] }))}");
var users = Enumerable.Range(1, 20).Select(i => i * 10d).ToArray();
lines.Add($"trend/midnight-load-test {Hash(Render(Spec(ChartKind.Scatter, ChartTheme.Dark) with { Title = "Load test", Style = ChartStyle.Midnight, XLabel = "Users", YLabel = "Throughput", Y2Label = "Latency (ms)",
    Series = [new("Throughput", users.Select((u, i) => new ChartPoint(u, Math.Round(26 * u - .1 * u * u + (i % 5 - 2) * 40))).ToArray()) { Trend = true, TrendFit = TrendFit.Polynomial },
        new("Latency", users.Select((u, i) => new ChartPoint(u, Math.Round(40 * Math.Exp(.015 * u) * (1 + (i % 4 - 1.5) * .08)))).ToArray()) { Secondary = true, Trend = true, TrendFit = TrendFit.Exponential }] }))}");
var nightsHrv = Enumerable.Range(0, 60).Select(i => Math.Round(64 + 6 * Math.Sin(i / 9d) + i % 5 - 2, 1)).ToArray();
lines.Add($"trend/midnight-hrv {Hash(Render(Spec(ChartKind.Line, ChartTheme.Dark) with { Title = "Nightly HRV", Style = ChartStyle.Midnight, XAxis = AxisKind.Time,
    Series = [new("Baseline", nightsHrv.Select((v, i) => ChartPoint.Interval(1788825600000d + i * 86400000d, 64, 60, 68)).ToArray(), ChartStyle.Midnight.Zones[0]) { Kind = ChartKind.Band },
        new("Nightly HRV", nightsHrv.Select((v, i) => new ChartPoint(1788825600000d + i * 86400000d, v) { Color = v < 60 ? ChartStyle.Midnight.Zones[4] : v > 68 ? ChartStyle.Midnight.Zones[1] : ChartStyle.Midnight.Zones[2] }).ToArray(), ChartStyle.Midnight.Zones[6])
            { Kind = ChartKind.Scatter, Markers = MarkerStyle.Filled, Trend = true, TrendFit = TrendFit.MovingAverage }] }))}");
// 0.33.0: race results on a line. An invented season of five races by index, each in the middle of its slot: the place coloured by its
// change, written with its field size as a value note, and the points earned; as the hand-built chart draws it, on one scale padded 12 %
// of its range with no gridlines, and as recommended, in two panes with the place reversed, each at 900 and 340 in light and Midnight.
// The season's rounds on a time axis whose long names keep the dates, at 340 in Midnight. Change colours on a plain line across a gap in
// each theme; value labels at the plot's left and right edges and its top, one below another's; and a time axis with long labels under
// each tick source.
(int? Position, int? Field, int? Points)[] races = [(31, 50, 40), (24, 48, 52), (27, 51, 47), (21, 49, 58), (19, 52, 61)];
string[] raced = ["11-04-2026", "16-05-2026", "04-07-2026", "08-08-2026", "19-09-2026"];
DateOnly[] raceDays = [new(2026, 4, 11), new(2026, 5, 16), new(2026, 7, 4), new(2026, 8, 8), new(2026, 9, 19)];
string[] roundNames = ["Round 1 · Hilltop Classic", "Round 2 · River Valley", "Round 3 · Quarry Loop", "Round 4 · Forest Sprint", "Round 5 · Final Ridge"];
ChartSeries Placed() => new("Position", races.Select((r, i) => new ChartPoint(i, r.Position, raced[i]) { ValueNote = r.Field is int field ? $"/{field}" : null }).ToArray())
    { ChangeColors = ChangeColors.LowerIsBetter, ValueLabels = true, Markers = MarkerStyle.Filled };
ChartSeries Earned() => new("Points", races.Select((r, i) => new ChartPoint(i, r.Points, raced[i])).ToArray()) { ValueLabels = true, Markers = MarkerStyle.Filled };
ChartSpec Faithful(ChartStyle style, int width) => new()
{
    Kind = ChartKind.Line, Style = style with { Gridlines = GridLine.Hidden }, Title = "Position & points by race", Description = "Place over field size, and points",
    Width = width, Height = 300, XMin = -.5, XMax = 4.5, YMin = 19 - 5.04, YMax = 61 + 5.04, Series = [Placed(), Earned()]
};
ChartSpec Recommended(ChartStyle style, int width) => new()
{
    Kind = ChartKind.Line, Style = style, Title = "Position & points by race", Description = "Place over field size, first at the top, and points",
    Width = width, Height = 380, XMin = -.5, XMax = 4.5, YReversed = true, YLabel = "Position", Panes = [new() { Label = "Points", Weight = 1 }], Series = [Placed(), Earned() with { Pane = 1 }]
};
foreach (var (look, style) in new[] { ("light", ChartStyle.Light), ("midnight", ChartStyle.Midnight) })
    foreach (var width in new[] { 900, 340 })
    {
        lines.Add($"race/faithful-{width}-{look} {Hash(Render(Faithful(style, width)))}");
        lines.Add($"race/recommended-{width}-{look} {Hash(Render(Recommended(style, width)))}");
    }
ChartSpec Rounds(TickSource ticks, ChartStyle? style, int width) => new()
{
    Kind = ChartKind.Line, Style = style, XAxis = AxisKind.Time, TimeZone = "Africa/Johannesburg", XTicks = ticks, YReversed = true, Title = "Your season, round by round",
    Description = "Your place in each round, first at the top", Width = width, Height = 260,
    Series = [new("Position", races.Select((r, i) => new ChartPoint(TimeAxis.Value(new DateTimeOffset(raceDays[i].ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(2))), r.Position, roundNames[i])).ToArray(), "#FF5A54")
        { Markers = MarkerStyle.Filled, HighlightLast = true }]
};
lines.Add($"race/season-340-midnight {Hash(Render(Rounds(TickSource.Auto, ChartStyle.Midnight, 340)))}");
ChartPoint[] Zigzag() => [new(0, 5, "A"), new(1, 3, "B"), new(2, 3, "C"), new(3, 4, "D"), new(4, null, "E"), new(5, 2, "F"), new(6, 6, "G"), new(7, 1, "H")];
foreach (var theme in Enum.GetValues<ChartTheme>())
    lines.Add($"change/line/{theme} {Hash(Render(Spec(ChartKind.Line, theme) with { Title = "Change colours", Series = [new("Position", Zigzag()) { ChangeColors = ChangeColors.LowerIsBetter }, new("Points", Zigzag().Select(p => p with { Y = 10 - p.Y }).ToArray()) { ChangeColors = ChangeColors.HigherIsBetter, Curve = LineCurve.Step }] }))}");
lines.Add($"labels/edges {Hash(Render(line with { Title = "Value labels at the edges", XMin = 0, XMax = 6, YMin = 0, YMax = 10,
    Series = [new("A", [new(0, 5) { ValueNote = "/48" }, new(3, 4), new(6, 10) { ValueNote = "/52" }]) { ValueLabels = true, Markers = MarkerStyle.Filled }, new("B", [new(1, 6), new(3, 4.2), new(5, 7)]) { Kind = ChartKind.Scatter, ValueLabels = true }] }))}");
foreach (var ticks in Enum.GetValues<TickSource>())
    lines.Add($"ticks/time-long-{ticks} {Hash(Render(Rounds(ticks, null, 900)))}");
// 0.34.0: sparklines. An invented run of six 5 km times by index, faster higher on a reversed duration axis, each personal best ringed by a
// highlight in the style's red and noted, at 120 by 32 in light and Midnight; a weight logged five times, steady about 38 kg, in one neutral
// colour on an axis held 8 kg tall at 270 by 54; a week of columns at 120 by 32; highlights on a full line and on scatter points; and a
// minimum span on a full chart's main plot and in a pane.
double[] pbTimes = [1450, 1432, 1445, 1411, 1420, 1367];
string[] weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
ChartSpec Pb(ChartStyle style) => new()
{
    Kind = ChartKind.Line, Style = style, Title = "5 km: 24:10 to 22:47 over 6 races", Description = "Each race's time, oldest first, faster higher",
    Width = 120, Height = 32, Sparkline = true, YReversed = true, YFormat = ValueFormat.Duration,
    Series = [new("5 km", pbTimes.Select((t, i) => new ChartPoint(i, t, $"Race {i + 1}") { Highlight = i % 2 == 1 ? style.Zones[5] : null, ValueNote = i % 2 == 1 ? " · PB" : null }).ToArray(), style.Zones[0]) { StrokeWidth = 2 }]
};
lines.Add($"spark/pb-120-light {Hash(Render(Pb(ChartStyle.Light)))}");
lines.Add($"spark/pb-120-midnight {Hash(Render(Pb(ChartStyle.Midnight)))}");
lines.Add($"spark/growth-270 {Hash(Render(new ChartSpec { Kind = ChartKind.Line, Theme = ChartTheme.Dark, Title = "Weight: 5 measurements, from 37.9 to 38.1 kg", Description = "Scale 34 to 42 kg",
    Width = 270, Height = 54, Sparkline = true, YMinSpan = 8, Series = [new("Weight", new[] { 37.9, 37.8, 38.2, 38.0, 38.1 }.Select((kg, i) => new ChartPoint(i, kg) { ValueNote = " kg" }).ToArray(), "#B7BCC4") { StrokeWidth = 2 }] }))}");
lines.Add($"spark/columns-120 {Hash(Render(new ChartSpec { Kind = ChartKind.Column, Title = "Seven days", Description = "Minutes each day", Width = 120, Height = 32, Sparkline = true,
    Series = [new("Minutes", new[] { 30, 0, 45, 20, 60, 0, 90 }.Select((m, i) => new ChartPoint(i, m, weekdays[i])).ToArray())] }))}");
lines.Add($"highlight/line {Hash(Render(line with { Title = "Highlights", Series = [line.Series[0] with { Points = line.Series[0].Points.Select((p, i) => i is 4 or 9 ? p with { Highlight = "#DD4B45", ValueNote = " · best" } : p).ToArray() },
    line.Series[1] with { Kind = ChartKind.Scatter, Points = line.Series[1].Points.Select((p, i) => i == 11 ? p with { Highlight = "#2E9B58" } : p).ToArray() }] }))}");
ChartPoint[] Weights() => new[] { 37.9, 37.8, 38.2, 38.0, 38.1, 38.0, 37.9 }.Select((kg, i) => new ChartPoint(i, kg, $"Week {i + 1}")).ToArray();
lines.Add($"span/line {Hash(Render(line with { Title = "Minimum span", YMinSpan = 8, Series = [new("Weight", Weights()) { Markers = MarkerStyle.Filled }] }))}");
lines.Add($"span/pane {Hash(Render(line with { Title = "Minimum span in a pane", YReversed = true, YFormat = ValueFormat.Duration, Panes = [new() { Label = "Weight", Weight = 1, YMinSpan = 8 }],
    Series = [new("5 km", pbTimes.Select((t, i) => new ChartPoint(i, t)).ToArray()), new("Weight", Weights().Take(6).ToArray()) { Pane = 1 }] }))}");
// 0.35.0: how the field finished, and text that fits. An invented race of 312 finishers in five-minute bins supplied as blocks from 35:00 to
// 1:20:00 on an axis held at zero, the last bin's one finisher kept 2 pixels tall, the reader's bin in red and noted, the median a dashed line
// over the bins labelled without its time, both axes labelled at their bounds, at 340 by 240 in light and Midnight; the same chart as a
// 1080 by 1350 card, every bar red with no median; bounds on a Y axis; a target in front of columns; and at 340 a description and a source
// too long for one line and a title too long for the drawing.
(double From, double To, int Count)[] fieldBins = [(2100, 2400, 18), (2400, 2700, 64), (2700, 3000, 88), (3000, 3300, 57), (3300, 3600, 33), (3600, 3900, 18), (3900, 4200, 10), (4200, 4500, 8), (4500, 4800, 1)];
ChartSpec Field(ChartStyle style) => new()
{
    Kind = ChartKind.Blocks, Style = style with { BarRadius = 2 }, Title = "How the field finished", Description = "312 finishers · median 47:12", Source = "Off the chart: 3 faster and 12 slower",
    Width = 340, Height = 240, IncludeZero = true, XFormat = ValueFormat.Duration, XTickLabels = TickLabels.Bounds, YTickLabels = TickLabels.Bounds,
    Annotations = [new(AnnotationAxis.X, 2832) { Label = "median", ShowValue = false, InFront = true, Color = style.Text }],
    Series = [new("Finishers", fieldBins.Select(b => ChartPoint.Block(b.From, b.To, b.Count) with { Color = b.From == 3000 ? style.Zones[5] : null, ValueNote = b.From == 3000 ? " · you" : null }).ToArray(), style.Zones[0])]
};
string Unlegended(ChartSpec spec) => ChartSvg.Render(spec with { Style = Finished(spec.Style, spec.Theme) }, includeLegend: false);
lines.Add($"field/340-light {Hash(Unlegended(Field(ChartStyle.Light)))}");
lines.Add($"field/340-midnight {Hash(Unlegended(Field(ChartStyle.Midnight)))}");
var fieldCard = Field(ChartStyle.Midnight);
lines.Add($"field/card-1080 {Hash(Unlegended(fieldCard with { Width = 1080, Height = 1350, Annotations = [],
    Series = [fieldCard.Series[0] with { Color = ChartStyle.Midnight.Zones[5], Points = fieldCard.Series[0].Points.Select(p => p with { Color = null, ValueNote = null }).ToArray() }] }))}");
lines.Add($"bounds/y {Hash(Render(line with { Title = "Labelled at its bounds", YTickLabels = TickLabels.Bounds }))}");
lines.Add($"front/columns {Hash(Render(Spec(ChartKind.Column, ChartTheme.Light) with { Title = "A target in front", Annotations = [new(AnnotationAxis.Y, 25) { Label = "Target", InFront = true }] }))}");
ChartSpec Narrow(string description, string source, string title = "How the field finished") => line with { Width = 340, Title = title, Description = description, Source = source };
lines.Add($"text/description-340 {Hash(Render(Narrow("Every finisher's time in five-minute bins from the 1st to the 99th percentile, the reader's own in red and the median a dashed line over the bins", "Source: invented")))}");
lines.Add($"text/source-340 {Hash(Render(Narrow("312 finishers · median 47:12", "Off the chart: 3 faster and 12 slower · counted from the race's 312 finishers")))}");
lines.Add($"text/title-340 {Hash(Render(Narrow("312 finishers · median 47:12", "Source: invented", "Nineteenth of fifty-two riders in the final round, eleven places better than the first")))}");
// 0.36.0: reading a chart day by day. A Y axis held symmetric about zero and written with its sign, the data inside it, past it and on a
// reversed axis; signed value labels and a signed annotation; columns on a symmetric axis; and an invented fitness and form chart in two
// panes at 340 by 420, fitness and fatigue over daily stress and form beneath, in the light palette and in Midnight in the race-results
// recipe's colours, which are for a dark background.
// A shared readout never changes the drawing, so it has no row of its own: the fitness rows set it.
double[] formValues = [-3, 4, 2, -6, 1, 7, -2, 0, 5, -4, 3, 6];
ChartSpec Symmetric(params double[] values) => line with { Title = "Form", YSymmetric = 10, YFormat = ValueFormat.Signed, Series = [new("Form", values.Select((v, i) => new ChartPoint(i, v)).ToArray()) { Markers = MarkerStyle.Filled }] };
lines.Add($"symmetric/inside {Hash(Render(Symmetric(formValues)))}");
lines.Add($"symmetric/past {Hash(Render(Symmetric(formValues.Select(v => v * 2.5).ToArray())))}");
lines.Add($"symmetric/reversed {Hash(Render(Symmetric(formValues) with { YReversed = true }))}");
lines.Add($"signed/labels {Hash(Render(Symmetric(formValues.Take(6).ToArray()) with { YSymmetric = null, Annotations = [new(AnnotationAxis.Y, 2) { Label = "Fresh" }],
    Series = [new("Form", formValues.Take(6).Select((v, i) => new ChartPoint(i, v)).ToArray()) { ValueLabels = true, Markers = MarkerStyle.Filled }] }))}");
lines.Add($"symmetric/columns {Hash(Render(Spec(ChartKind.Column, ChartTheme.Light) with { Title = "Change", YSymmetric = 20, YFormat = ValueFormat.Signed,
    Series = [new("Change", formValues.Select((v, i) => new ChartPoint(i, v * 3, $"W{i + 1}")).ToArray())] }))}");
ChartSpec Fitness(ChartStyle style)
{
    var start = new DateOnly(2026, 6, 1);
    var dark = style == ChartStyle.Midnight;
    var load = Training.Load(Enumerable.Range(0, 56).Select(i => (start.AddDays(i), (double)((i % 7) switch { 0 => 0, 2 => 120, 5 => 150, _ => 60 }))), 40, 40);
    double When(DateOnly day) => TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
    return new()
    {
        Kind = ChartKind.Line, Style = style, XAxis = AxisKind.Time, Width = 340, Height = 420, Title = "Fitness & form", Description = "Fitness, fatigue and stress above, form below",
        SharedReadout = true, YLabel = "Stress", Panes = [new() { Label = "Form", Weight = .6, YSymmetric = 10, YFormat = ValueFormat.Signed }],
        Annotations = [new(AnnotationAxis.X, When(start.AddDays(20))) { Label = "Race", ShowValue = false, Color = dark ? "#a78bfa" : null }],
        Series = [ChartSeries.From("Fitness", load, d => When(d.Day), d => Math.Round(d.Fitness, 1)) with { Color = dark ? "#38bdf8" : null },
            ChartSeries.From("Fatigue", load, d => When(d.Day), d => Math.Round(d.Fatigue, 1)) with { Color = dark ? "#f87171" : null },
            ChartSeries.From("Form", load, d => When(d.Day), d => Math.Round(d.Form, 1)) with { Color = dark ? "#f59e0b" : null, Pane = 1 },
            ChartSeries.From("Stress", load, d => When(d.Day), d => d.Stress) with { Kind = ChartKind.Column, Color = style.Zones[0] }]
    };
}
lines.Add($"fitness/340-light {Hash(Render(Fitness(ChartStyle.Light)))}");
lines.Add($"fitness/340-midnight {Hash(Render(Fitness(ChartStyle.Midnight)))}");
// 0.37.0: ride channels. A long line averaged into slices, and one thinned by minimum and maximum over a zoomed window; tick labels left
// out of one pane; and an invented ride in six plots named above them, no tick label up the side, averaged into 300 slices, its heart
// rate missing for 45 seconds, at 340 by 640 in the light palette and in Midnight.
ChartPoint[] Wavy(int count) => Enumerable.Range(0, count).Select(i => new ChartPoint(i, Math.Round(100 + 40 * Math.Sin(i / 90.0) + 12 * Math.Sin(i * 1.7), 1))).ToArray();
lines.Add($"sampling/average {Hash(Render(line with { Title = "Averaged", Sampling = SamplingMethod.Average, MaxRenderedPoints = 120, Series = [new("Power", Wavy(6000))] }))}");
lines.Add($"sampling/window-minmax {Hash(Render(line with { Title = "Zoomed", MaxRenderedPoints = 120, XMin = 1500, XMax = 2700, Series = [new("Power", Wavy(6000))] }))}");
lines.Add($"sampling/window-average {Hash(Render(line with { Title = "Zoomed", Sampling = SamplingMethod.Average, MaxRenderedPoints = 120, XMin = 1500, XMax = 2700, Series = [new("Power", Wavy(6000))] }))}");
lines.Add($"ticks/pane-none {Hash(Render(line with { Title = "Labels in the main plot alone", Panes = [new() { Label = "Lower", Weight = 1, YTickLabels = TickLabels.None }],
    Series = [line.Series[0], line.Series[1] with { Pane = 1 }] }))}");
ChartSpec Channels(ChartStyle style)
{
    var dark = style == ChartStyle.Midnight;
    var seconds = 3600;
    ChartPoint[] Channel(Func<int, double?> value) => Enumerable.Range(0, seconds).Select(t => new ChartPoint(t, value(t))).ToArray();
    string Header(string name, ChartPoint[] points, string unit) => $"{name} · avg {points.Where(p => p.Y.HasValue).Average(p => p.Y!.Value):0} · max {points.Max(p => p.Y):0} · min {points.Min(p => p.Y):0} {unit}";
    ChartPoint[] heart = Channel(t => t is >= 1800 and < 1845 ? null : Math.Round(140 + 20 * Math.Sin(t / 300.0))), power = Channel(t => Math.Round(Math.Max(0, 200 + 90 * Math.Sin(t / 240.0) + 25 * Math.Sin(t * 1.7)))),
        cadence = Channel(t => Math.Round(85 + 4 * Math.Sin(t * .83))), speed = Channel(t => Math.Round(31 + 6 * Math.Sin(t / 200.0), 1)),
        elevation = Channel(t => Math.Round(140 + 60 * Math.Sin(t / 600.0), 1)), temperature = Channel(t => Math.Round(18 + 4.0 * t / seconds, 1));
    ChartSeries Series(string name, ChartPoint[] points, string color, int pane) => new(name, points, dark ? color : null) { Pane = pane, Markers = MarkerStyle.None, StrokeWidth = 1.5 };
    return new()
    {
        Kind = ChartKind.Line, Style = style, Width = 340, Height = 640, Title = "Ride channels", Description = "An invented hour, each channel averaged over 12 seconds",
        XFormat = ValueFormat.Duration, Sampling = SamplingMethod.Average, MaxRenderedPoints = 300, YTickLabels = TickLabels.None, PaneTitles = PaneTitlePlacement.Above, SharedReadout = true,
        YLabel = Header("Heart rate", heart, "bpm"),
        Panes = [new() { Label = Header("Power", power, "W"), Weight = 1 }, new() { Label = Header("Cadence", cadence, "rpm"), Weight = 1 }, new() { Label = Header("Speed", speed, "km/h"), Weight = 1 },
            new() { Label = Header("Elevation", elevation, "m"), Weight = 1 }, new() { Label = Header("Temperature", temperature, "°C"), Weight = 1 }],
        Series = [Series("Heart rate", heart, "#e24b4a", 0), Series("Power", power, "#7048e8", 1), Series("Cadence", cadence, "#1098ad", 2), Series("Speed", speed, "#0ca678", 3),
            Series("Elevation", elevation, "#868e96", 4), Series("Temperature", temperature, "#e8950c", 5)]
    };
}
lines.Add($"channels/340-light {Hash(Unlegended(Channels(ChartStyle.Light)))}");
lines.Add($"channels/340-midnight {Hash(Unlegended(Channels(ChartStyle.Midnight)))}");
// 0.38.0: season arc and gap to the leader. Ticks set by hand on a reversed percentile axis, a unit after every value, eight end labels
// ending within a whisker, and an invented season's arc and an invented race's gaps to the leader at 340 in light and Midnight.
lines.Add($"ticks/set {Hash(Render(line with { Title = "Ticks set by hand", YReversed = true, YMin = 0, YMax = 60, YTickValues = [new(0, "Front"), new(30), new(60, "Back"), new(90, "Outside")] }))}");
lines.Add($"units/seconds {Hash(Render(Symmetric(formValues) with { YUnit = "s", Series = [new("Form", formValues.Select((v, i) => new ChartPoint(i, v)).ToArray()) { ValueLabels = true, Markers = MarkerStyle.Filled }] }))}");
lines.Add($"ends/crowded {Hash(Render(line with { Title = "Crowded endings", Width = 340, Height = 300,
    Series = Enumerable.Range(0, 8).Select(k => new ChartSeries($"S{k}", [new(0, 10 + k * 9), new(5, 50 + k * .3)]) { EndLabel = $"S{k}", EndNote = $"+{k}.0s" }).ToArray() }))}");
ChartSpec SeasonArc(ChartStyle style)
{
    var dark = style == ChartStyle.Midnight;
    (string Discipline, int? Position, int Field)[] races = [("XCO", 18, 40), ("XCC", 9, 32), ("XCO", 12, 44), ("XCM", 31, 60), ("XCO", null, 41), ("XCC", 6, 30), ("XCO", 7, 42), ("Enduro", 22, 55), ("XCM", 19, 58), ("XCO", 5, 40)];
    (string Name, string Color)[] kinds = [("XCC", "#38bdf8"), ("XCO", "#34d399"), ("XCM", "#f59e0b"), ("Other", "#a78bfa")];
    string Kind(string d) => kinds.Any(k => k.Name == d) ? d : "Other";
    return new()
    {
        Kind = ChartKind.Line, Style = style, Width = 340, Height = 300, Title = "Season arc", Description = "Each race's place in its field, front at the top",
        XMin = -0.5, XMax = races.Length - 0.5, YReversed = true, YMin = 0, YMax = 100, YUnit = "%", YTickValues = [new(0, "Front"), new(50, "Mid"), new(100, "Back")],
        Series = kinds.Select(k => new ChartSeries(k.Name, races.Select((r, i) => (r, i)).Where(t => Kind(t.r.Discipline) == k.Name)
            .Select(t => new ChartPoint(t.i, t.r.Position is int p ? Math.Round(100.0 * (p - 1) / (t.r.Field - 1)) : null, $"R{t.i + 1}") { ValueNote = t.r.Position is int q ? $" · P{q}/{t.r.Field}" : null }).ToArray(),
            dark ? k.Color : null) { Markers = MarkerStyle.Filled }).ToArray()
    };
}
lines.Add($"arc/340-light {Hash(Render(SeasonArc(ChartStyle.Light)))}");
lines.Add($"arc/340-midnight {Hash(Render(SeasonArc(ChartStyle.Midnight)))}");
ChartSpec Gaps(ChartStyle style)
{
    var dark = style == ChartStyle.Midnight;
    var cumulative = Enumerable.Range(0, 8).Select(r => { var total = 0.0; return Enumerable.Range(0, 7).Select(lap => lap == 0 ? 0 : total += Math.Round(r == 7 ? 309 - 3.1 * lap + 2 * Math.Sin(lap * 1.9) : 296 + r * 2.4 + 6 * Math.Sin(r * 1.7 + lap * 2.3), 1)).ToArray(); }).ToArray();
    var gaps = cumulative.Select(times => times.Select((t, lap) => Math.Round(t - cumulative.Min(other => other[lap]), 1)).ToArray()).ToArray();
    var signed = new Axis(AxisKind.Linear, 0, 1) { ValueFormat = ValueFormat.Signed, Unit = "s" };
    return new()
    {
        Kind = ChartKind.Line, Style = style, Width = 340, Height = 320, Title = "Gap to the leader", Description = "Seconds behind the leader at each lap", XLabel = "Lap",
        YReversed = true, YMin = 0, YFormat = ValueFormat.Signed, YUnit = "s",
        Series = Enumerable.Range(0, 8).Select(r => new ChartSeries(r == 7 ? "You" : $"Rider {(char)('A' + r)}", gaps[r].Select((g, lap) => new ChartPoint(lap, g, lap == 0 ? "Start" : $"Lap {lap}")).ToArray(),
            r == 7 ? (dark ? "#34d399" : style.Series[0]) : style.Zones[0]) { StrokeWidth = r == 7 ? 3.2 : 2, Markers = MarkerStyle.None, EndLabel = r == 7 ? "You" : ((char)('A' + r)).ToString(),
            EndNote = gaps[r][^1] == 0 ? "leader" : signed.Format(gaps[r][^1]) }).ToArray()
    };
}
lines.Add($"gap/340-light {Hash(Unlegended(Gaps(ChartStyle.Light)))}");
lines.Add($"gap/340-midnight {Hash(Unlegended(Gaps(ChartStyle.Midnight)))}");
// 0.39.0: proportions and meters. An invented ride's effort zones as a strip, at 340 in light and Midnight and wide, with a zone of none, and
// its cadence split; score bars on tracks at 340 in light and Midnight; columns on tracks; and a chart that keeps its title unwritten.
ChartSpec Effort(ChartStyle? style, int width = 340, double hard = 820) => new()
{
    Kind = ChartKind.Strip, Style = style, Width = width, Title = "Effort zones", Description = "Time in each heart-rate zone", YFormat = ValueFormat.Duration,
    Series = [new("Zones", [new(0, 740, "Easy") { Color = "#3FD17A" }, new(1, 1290, "Moderate") { Color = "#D7DDE5" }, new(2, hard, "Hard") { Color = "#F5B642" }, new(3, 250, "Very hard") { Color = "#E30613" }])]
};
lines.Add($"strip/340-light {Hash(Render(Effort(ChartStyle.Light) with { Series = [new("Zones", Effort(null).Series[0].Points.Select(p => p with { Color = null }).ToArray())] }))}");
lines.Add($"strip/340-midnight {Hash(Render(Effort(ChartStyle.Midnight)))}");
lines.Add($"strip/900-light {Hash(Render(Effort(ChartStyle.Light, 900) with { Series = [new("Zones", Effort(null).Series[0].Points.Select(p => p with { Color = null }).ToArray())] }))}");
lines.Add($"strip/zero-part {Hash(Render(Effort(ChartStyle.Midnight, hard: 0)))}");
lines.Add($"strip/cadence-split {Hash(Render(new ChartSpec { Kind = ChartKind.Strip, Style = ChartStyle.Midnight, Width = 340, DrawTitles = false, Title = "Cadence split", Description = "Pedalling against coasting",
    YFormat = ValueFormat.Duration, Series = [new("Cadence", [new(0, 4310, "Pedalling") { Color = "#D7DDE5" }, new(1, 890, "Coasting") { Color = "#80858E" }])] }))}");
ChartSpec Scores(ChartStyle style) => new()
{
    Kind = ChartKind.Bar, Style = style with { Gridlines = GridLine.Hidden }, Width = 340, Height = 240, Title = "Race scores", Description = "Each out of 100", DrawTitles = false,
    YMin = 0, YMax = 100, BarTrack = true, YTickLabels = TickLabels.None,
    Series = [new("Score", [new(0, 82, "Execution"), new(1, 64, "Improvement"), new(2, 91, "Effort"), new(3, 58, "Consistency")]) { ValueLabels = true }]
};
lines.Add($"scores/340-light {Hash(Unlegended(Scores(ChartStyle.Light)))}");
lines.Add($"scores/340-midnight {Hash(Unlegended(Scores(ChartStyle.Midnight)))}");
lines.Add($"track/columns {Hash(Render(new ChartSpec { Kind = ChartKind.Column, Title = "Columns on tracks", YMax = 100, BarTrack = true,
    Series = [new("Score", [new(0, 82, "A"), new(1, 64, "B"), new(2, 105, "C")]) { ValueLabels = true }, new("Last", [new(0, 70, "A"), new(1, 50, "B"), new(2, 90, "C")])] }))}");
lines.Add($"titles/undrawn {Hash(Render(line with { Title = "Not drawn", Description = "Named, not written", DrawTitles = false }))}");
// 0.40.0: lap columns and best efforts. Columns filled by a gradient along their value axis in light and Midnight, and bars filled across
// the plot; a second line under each category's name on columns and on bars; the lap and best-efforts recipes at 340 on the Race Face card;
// a strip whose shares are its amounts, said once; and an end label that gives up its note before its name.
ChartSpec Laps40(ChartStyle style, string low, string high, bool subs) => new()
{
    Kind = ChartKind.Column, Style = style, Width = 340, Height = 260, Title = "Heart rate per lap", Description = "Average heart rate in each lap", XLabel = "Lap",
    Series = [new("Heart rate", new[] { 152, 161, 168, 174 }.Select((bpm, i) => new ChartPoint(i, bpm, $"L{i + 1}") { SubLabel = subs ? bpm + " bpm" : null }).ToArray())
        { ValueLabels = !subs, Gradient = [new(152, low), new(174, high)] }]
};
lines.Add($"gradient/columns-light {Hash(Unlegended(Laps40(ChartStyle.Light, "#A88200", "#DD4B45", false)))}");
lines.Add($"gradient/columns-midnight {Hash(Unlegended(Laps40(ChartStyle.Midnight, "#F5C518", "#FF5A5A", false)))}");
lines.Add($"gradient/bars {Hash(Render(new ChartSpec { Kind = ChartKind.Bar, Title = "Best efforts", Description = "Watts at four durations",
    Series = [new("Power", [new(0, 780, "5s"), new(1, 420, "1m"), new(2, 290, "5m"), new(3, 240, "20m")]) { ValueLabels = true, Gradient = [new(240, "#3F87D9"), new(780, "#DD4B45")] }] }))}");
lines.Add($"sublabel/columns {Hash(Unlegended(Laps40(ChartStyle.Light, "#A88200", "#DD4B45", true)))}");
lines.Add($"sublabel/bars {Hash(Render(new ChartSpec { Kind = ChartKind.Bar, Title = "Best efforts", Description = "Watts, and watts per kilogram",
    Series = [new("Power", [new(0, 780, "5s") { SubLabel = "15.0 W/kg" }, new(1, 420, "1m") { SubLabel = "8.1 W/kg" }, new(2, 290, "5m"), new(3, 240, "20m") { SubLabel = "4.6 W/kg" }]) { ValueLabels = true }] }))}");
var raceFace40 = new ChartStyle
{
    Background = "#161618", Text = "#F5F6F7", Muted = "#80858E", Grid = "#2D2D2F", Edge = "#80858E",
    Series = ["#FF5A54", "#D7DDE5", "#F5B642", "#3FD17A", "#C2C6D2", "#CD7F46"], Zones = ["#80858E", "#D7DDE5", "#3FD17A", "#F5B642", "#F2545B"],
    Rising = "#34d399", Falling = "#f87171", HeatmapLow = "#1E1F22", HeatmapHigh = "#E30613", FontFamily = "Inter, Segoe UI, Arial, sans-serif"
};
double[] lapHeart40 = [152, 161, 168, 174];
lines.Add($"recipe/lap-heart-340 {Hash(Unlegended(new ChartSpec { Title = "Heart rate per lap", Description = "Average heart rate in each lap, from 152 to 174 bpm",
    Kind = ChartKind.Column, Width = 340, Height = 260, Style = raceFace40 with { Gridlines = GridLine.Hidden }, DrawTitles = false, YTickLabels = TickLabels.None,
    Series = [new("Heart rate", lapHeart40.Select((bpm, lap) => new ChartPoint(lap, bpm, $"L{lap + 1}") { SubLabel = (int)bpm + " bpm" }).ToArray())
        { Gradient = [new(0, "#f59e0b"), new(lapHeart40.Max(), "#f87171")] }] }))}");
var effortWatts40 = Enumerable.Range(0, 5400).Select(t => Math.Round(t % 600 < 5 ? 640 + t / 30.0 : t is >= 900 and < 960 ? 420 : t is >= 1500 and < 1800 ? 290
    : t is >= 2400 and < 3600 ? 238 + 6 * Math.Sin(t / 90.0) : 170 + 25 * Math.Sin(t / 300.0) + 10 * Math.Sin(t / 23.0))).ToArray();
(double Seconds, string Name)[] effortDurations40 = [(5, "5s"), (60, "1m"), (300, "5m"), (1200, "20m"), (3600, "60m")];
lines.Add($"recipe/best-efforts-340 {Hash(Unlegended(new ChartSpec { Title = "Best efforts", Description = "Best average power at five durations, in watts, with watts per kilogram under each",
    Kind = ChartKind.Column, Width = 340, Height = 260, Style = raceFace40 with { BarRadius = 3, Gridlines = GridLine.Hidden }, DrawTitles = false,
    YTickLabels = TickLabels.None, XLabel = "W/kg under each duration",
    Series = [new("Best power", Training.MeanMaximal(effortWatts40, effortDurations40.Select(d => d.Seconds)).Select((effort, i) => new ChartPoint(i, Math.Round(effort.Value), effortDurations40[i].Name)
        { SubLabel = (effort.Value / 52).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) }).ToArray(), "#38bdf8") { ValueLabels = true }] }))}");
lines.Add($"strip/shares-once {Hash(Render(new ChartSpec { Kind = ChartKind.Strip, Width = 340, Title = "Effort zones", Description = "Shares the app worked out", YUnit = "%",
    Series = [new("Zones", [new(0, 33, "Easy"), new(1, 33, "Moderate"), new(2, 33, "Hard")])] }))}");
lines.Add($"ends/note-first {Hash(Unlegended(new ChartSpec { Kind = ChartKind.Line, Width = 340, Height = 260, Title = "Gap", Description = "A name that fits without its note",
    Series = [new("Rider", [new(0, 0), new(1, 4)]) { EndLabel = "Rider name", EndNote = "+12.3s" }] }))}");
// 0.41.0: fits a card. A line on a tinted card with its background unpainted, in light and Midnight; a line whose points the app averaged;
// bars drawn as tall as their rows, one row and four, on tracks and without; and the score-bars recipe at 340 fitted to its rows.
ChartSpec Resting(ChartStyle style) => new()
{
    Kind = ChartKind.Line, Width = 340, Height = 260, Title = "Resting heart rate", Description = "An invented week", Style = style, PaintBackground = false, XMin = -0.5, XMax = 6.5,
    Series = [new("Resting", new double[] { 52, 51, 53, 50, 49, 51, 48 }.Select((bpm, i) => new ChartPoint(i, bpm, $"D{i + 1}")).ToArray()) { ValueLabels = true, EndLabel = "Rest" }]
};
lines.Add($"paint/unpainted-light {Hash(Unlegended(Resting(ChartStyle.Light with { Background = "#F3F6FB" })))}");
lines.Add($"paint/unpainted-midnight {Hash(Unlegended(Resting(ChartStyle.Midnight with { Background = "#151A24" })))}");
lines.Add($"average/line {Hash(Render(new ChartSpec { Kind = ChartKind.Line, Title = "Power", Description = "Averaged by the app over 12 seconds", XFormat = ValueFormat.Duration,
    Series = [new("Power", Enumerable.Range(0, 40).Select(i => new ChartPoint(6 + 12 * i, i == 20 ? null : Math.Round(210 + 40 * Math.Sin(i / 5.0)))).ToArray()) { AverageOf = "12 s" }] }))}");
ChartSpec Fitted(int rows, bool tracked) => new()
{
    Kind = ChartKind.Bar, Width = 340, Title = "Scores", Description = "Each out of 100", YMin = 0, YMax = 100, BarTrack = tracked, FitHeight = true, YTickLabels = TickLabels.None,
    Series = [new("Score", Enumerable.Range(0, rows).Select(i => new ChartPoint(i, 55 + 9 * i, $"Score {i + 1}")).ToArray()) { ValueLabels = true }]
};
lines.Add($"fit/one-tracked {Hash(Unlegended(Fitted(1, true)))}");
lines.Add($"fit/four-tracked {Hash(Unlegended(Fitted(4, true)))}");
lines.Add($"fit/one-plain {Hash(Unlegended(Fitted(1, false)))}");
lines.Add($"fit/four-plain {Hash(Render(Fitted(4, false)))}");
(string Name, double Score)[] raceScores41 = [("Execution", 82), ("Improvement", 64), ("Effort", 91), ("Consistency", 58)];
lines.Add($"recipe/score-bars-340-fitted {Hash(Unlegended(new ChartSpec { Title = "Race scores", Description = "Execution, improvement, effort and consistency, each out of 100",
    Kind = ChartKind.Bar, Width = 340, Height = 240, Style = raceFace40 with { Gridlines = GridLine.Hidden }, DrawTitles = false,
    YMin = 0, YMax = 100, BarTrack = true, YTickLabels = TickLabels.None, FitHeight = true,
    Series = [new("Score", raceScores41.Select((s, i) => new ChartPoint(i, s.Score, s.Name)).ToArray(), "#D7DDE5") { ValueLabels = true }] }))}");
// 0.42.0: words at a missing value. A round the rider missed written "absent" at the foot of the plot in light and Midnight, at the top of
// a reversed axis, two missed rounds side by side, the second's word left out, and the team-rider recipe at 340 on the Race Face card.
ChartSpec Missed(ChartStyle style, bool reversed = false, params int[] gaps) => new()
{
    Kind = ChartKind.Line, Width = 340, Height = 260, Title = "Team rider", Description = "Share of the category finished ahead of", Style = style,
    XMin = -0.5, XMax = 9.5, YMin = 0, YMax = 100, YUnit = "%", YReversed = reversed,
    Series = [new("Share", Enumerable.Range(0, 10).Select(i => gaps.Contains(i) ? new ChartPoint(i, null, $"R{i + 1}") { GapLabel = "absent", Color = "#8A6500" }
        : new ChartPoint(i, 60 + 3 * i % 35, $"R{i + 1}")).ToArray()) { Markers = MarkerStyle.Filled }]
};
lines.Add($"gap/light {Hash(Unlegended(Missed(ChartStyle.Light, false, 4)))}");
lines.Add($"gap/midnight {Hash(Unlegended(Missed(ChartStyle.Midnight, false, 4)))}");
lines.Add($"gap/reversed {Hash(Unlegended(Missed(ChartStyle.Light, true, 4)))}");
lines.Add($"gap/two-close {Hash(Unlegended(Missed(ChartStyle.Light, false, 4, 5)))}");
static string Ordinal42(int place) => place + (place % 100 is 11 or 12 or 13 ? "th" : (place % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
(int Round, int? Place, int Field)[] teamRounds42 = [(1, 9, 38), (2, 14, 41), (3, 5, 37), (5, 7, 40), (6, null, 39), (7, 3, 36), (8, 6, 42)];
lines.Add($"recipe/team-rider-340 {Hash(Render(new ChartSpec { Title = "Team rider", Description = "Share of the category finished ahead of, round by round; round 4 was not held",
    Kind = ChartKind.Line, Width = 340, Height = 260, Style = raceFace40, DrawTitles = false, XMin = -0.5, XMax = teamRounds42.Length - 0.5,
    YMin = 0, YMax = 100, YUnit = "%", YTickValues = [new(0, "0%"), new(50, "50%"), new(100, "100%")],
    Series = [new("Share", teamRounds42.Select((r, i) => r.Place is int place
        ? new ChartPoint(i, Math.Round(100d * (r.Field - place) / (r.Field - 1), MidpointRounding.AwayFromZero), $"Round {r.Round}") { ValueNote = $" · {Ordinal42(place)} of {r.Field}" }
        : new ChartPoint(i, null, $"Round {r.Round}") { GapLabel = "absent", Color = "#e0a800" }).ToArray(), "#22d3ee") { StrokeWidth = 2, Markers = MarkerStyle.Filled }] }))}");
// 0.43.0: the event planner.
PlannerSpec Planned(ChartTheme theme, ChartStyle? style = null) => PlannerSpec.ForYear(2027) with
{
    Title = "Season planner", Description = "Invented organizers", Theme = theme, Style = Finished(style, theme),
    Regions = [new("ZA", "South Africa"), new("ZA-GP", "Gauteng", "ZA"), new("ZA-WC", "Western Cape", "ZA")],
    Periods = [new(new(2027, 4, 27), null, "Freedom Day", PeriodKind.PublicHoliday, "ZA"), new(new(2027, 6, 26), new DateOnly(2027, 7, 18), "School holiday", PeriodKind.SchoolHoliday, "ZA")],
    Events = Enumerable.Range(0, 30).Select(i => new PlannerEvent($"e{i}", $"Invented event {i}", new DateOnly(2027, 1, 2).AddDays(i * 11))
    {
        End = i % 7 == 0 ? new DateOnly(2027, 1, 2).AddDays(i * 11 + 2) : null, Region = i % 2 == 0 ? "ZA-GP" : "ZA-WC", Category = i % 3 == 0 ? "XCO" : "Road",
        Status = (PlannerStatus)(i % 3), Relevance = (PlannerRelevance)(i % 3), Mine = i == 5
    }).ToArray()
};
foreach (var (name, theme, style) in new[] { ("light", ChartTheme.Light, (ChartStyle?)null), ("dark", ChartTheme.Dark, null), ("midnight", ChartTheme.Light, ChartStyle.Midnight) })
{
    lines.Add($"planner/year-{name} {Hash(PlannerSvg.Render(Planned(theme, style), PlannerView.WholePeriod))}");
    lines.Add($"planner/month-{name} {Hash(PlannerSvg.Render(Planned(theme, style), PlannerView.Month(2027, 3)))}");
    lines.Add($"planner/day-{name} {Hash(PlannerSvg.Render(Planned(theme, style), PlannerView.Day(new DateOnly(2027, 1, 13))))}");
}
lines.Add($"planner/year-narrow-340 {Hash(PlannerSvg.Render(Planned(ChartTheme.Light) with { Width = 340 }, PlannerView.WholePeriod, PlannerLayout.Narrow))}");
lines.Add($"planner/month-narrow-340 {Hash(PlannerSvg.Render(Planned(ChartTheme.Light) with { Width = 340 }, PlannerView.Month(2027, 3), PlannerLayout.Narrow))}");
lines.Add($"planner/gauteng {Hash(PlannerSvg.Render(Planned(ChartTheme.Light) with { Filter = new() { Regions = ["ZA-GP"] } }, PlannerView.WholePeriod))}");
// 0.45.0: places and points.
Placing[] placed = [
    new(18) { Field = 40, Points = 33, Date = new(2027, 3, 13), Series = "Invented League" },
    new(6) { Field = 22, Date = new(2027, 4, 3), Series = "Invented Open" },
    new(14) { Field = 42, Points = 37, Date = new(2027, 4, 24), Series = "Invented League" },
    new(16) { Field = 41, Points = 35, Date = new(2027, 6, 19), Series = "Invented League" },
    new(11) { Field = 44, Points = 40, Date = new(2027, 8, 14), Series = "Invented League" }];
var league = placed.Where(p => p.Series == "Invented League").Select(p => p with { Series = null }).ToArray();
lines.Add($"placings/points {Hash(ChartSvg.Render(PlacingsChart.Build(league, new() { Style = Finished(null, ChartTheme.Light) })!))}");
lines.Add($"placings/no-points {Hash(ChartSvg.Render(PlacingsChart.Build(league.Select(p => p with { Points = null }), new() { Style = Finished(null, ChartTheme.Light) })!))}");
lines.Add($"placings/two-series {Hash(ChartSvg.Render(PlacingsChart.Build(placed, new() { Style = Finished(null, ChartTheme.Light) })!))}");
// 0.46.0: heatmap tables. Four invented categories by four seasons, points per start with the number of starts beneath, two cells too
// thin to rate (one with a value, one without), and the same at a fixed cell width over twelve seasons.
ChartSpec CategoryGrid(ChartTheme theme, ChartStyle? style, int seasons, double? cellWidth) => new()
{
    Kind = ChartKind.Heatmap, Theme = theme, Style = style, Title = "Points per start", Description = "Invented categories by season",
    Width = 600, Height = 320, YUnit = " pts", CellText = true, CellWidth = cellWidth,
    Series = new[] { "Sprint", "Middle distance", "Long distance", "Relay" }.Select((category, r) => new ChartSeries(category, Enumerable.Range(0, seasons).Select(c =>
    {
        ChartPoint cell = new(c, Math.Round(1 + (r * 5 + c * 3) % 9 * .4, 1), $"{2026 - seasons + 1 + c}") { SubLabel = $"/{10 + (r * 7 + c * 5) % 5} starts" };
        return (r, c) switch
        {
            (2, 0) => cell with { SubLabel = "/4 starts", NotRated = "too few starts to rate" },
            (3, 2) => cell with { Y = null, SubLabel = "/2 starts", NotRated = "too few starts to rate" },
            _ => cell
        };
    }).ToArray())).ToArray()
};
foreach (var (name, theme, style) in new[] { ("light", ChartTheme.Light, (ChartStyle?)null), ("dark", ChartTheme.Dark, null), ("midnight", ChartTheme.Light, ChartStyle.Midnight) })
    lines.Add($"heatmap-table/{name} {Hash(Render(CategoryGrid(theme, style, 4, null)))}");
lines.Add($"heatmap-table/cell-width {Hash(Render(CategoryGrid(ChartTheme.Light, null, 12, 56)))}");
// 0.46.1: heatmap follow-ups, on the same invented table: its column labels on top, its height fitted to its rows, a first row named at
// length, a fifth season nobody raced, its colour scale pinned to include 0, and its reasons written in cells 90 wide.
var table = CategoryGrid(ChartTheme.Light, null, 4, null);
lines.Add($"heatmap-table/top {Hash(Render(table with { ColumnLabelsOnTop = true }))}");
lines.Add($"heatmap-table/fit {Hash(Render(table with { FitHeight = true, Source = "" }))}");
lines.Add($"heatmap-table/long-name {Hash(Render(table with { Series = [table.Series[0] with { Name = "Junior mixed team relay" }, .. table.Series.Skip(1)] }))}");
lines.Add($"heatmap-table/no-race {Hash(Render(table with { Series = table.Series.Select(row => row with { Points = [.. row.Points, new ChartPoint(4, null, "2027") { SubLabel = "/0 starts", GapLabel = "did not race" }] }).ToArray() }))}");
lines.Add($"heatmap-table/zero {Hash(Render(table with { IncludeZero = true }))}");
lines.Add($"heatmap-table/reasons {Hash(Render(table with { CellWidth = 90, FitHeight = true }))}");
// 0.46.2: heatmap cells show it all, on the same invented table with a points note in every cell: notes drawn with rows fitted to them,
// notes in the fixed height that gives them up first, not-rated cells keeping their value, and both switches on the Dark preset.
var noted = table with { Series = table.Series.Select((row, r) => row with { Points = row.Points.Select((p, c) => p with { ValueNote = $" · {12 + r * 7 + c * 3} pts, {3 + (r + c) % 4} riders" }).ToArray() }).ToArray() };
lines.Add($"heatmap-cells/notes {Hash(Render(noted with { CellWidth = 90, FitHeight = true, CellNotes = true }))}");
lines.Add($"heatmap-cells/notes-fixed {Hash(Render(noted with { CellWidth = 90, CellNotes = true }))}");
lines.Add($"heatmap-cells/keeps-value {Hash(Render(noted with { CellWidth = 90, FitHeight = true, NotRatedKeepsValue = true }))}");
lines.Add($"heatmap-cells/both-dark {Hash(Render(CategoryGrid(ChartTheme.Dark, null, 4, 90) with { FitHeight = true, CellNotes = true, NotRatedKeepsValue = true, Series = noted.Series }))}");
if (args.FirstOrDefault() == "dump-finish")
{
    Directory.CreateDirectory(args[1]);
    for (var i = 0; i < finish.Length; i++) File.WriteAllText(Path.Combine(args[1], $"{i}.html"), $"<html><body style='margin:0;background:#888'><div style='width:900px;margin:8px'>{Render(finish[i])}</div></body></html>");
    return;
}
if (args.FirstOrDefault() == "dump-panes")
{
    ChartSpec[] shown = [Market(ChartKind.Candlestick, ChartTheme.Light), Market(ChartKind.Candlestick, ChartTheme.Dark), Market(ChartKind.Ohlc, ChartTheme.Light),
        Stream(ChartTheme.Light), Stream(ChartTheme.Dark), Paired(ChartTheme.Light), Paired(ChartTheme.Dark), Logged()];
    File.WriteAllText(args[1], "<html><body style='margin:0;background:#888'>" + string.Join("", shown.Select(s => $"<div style='width:900px;margin:8px'>{Render(s)}</div>")) + "</body></html>");
    return;
}
if (args.FirstOrDefault() == "dump")
{
    ChartSpec[] shown = [Performance(ChartTheme.Light), Performance(ChartTheme.Dark), Target(ChartTheme.Light), Target(ChartTheme.Dark), Weekly(ChartTheme.Light),
        Weekly(ChartTheme.Light) with { Series = [.. Weekly(ChartTheme.Light).Series, new("Last year", volume.Select((v, i) => new ChartPoint(i, v - 1.5, $"W{i + 1}")).ToArray())] }];
    File.WriteAllText(args[1], "<html><body style='margin:0;background:#888'>" + string.Join("", shown.Select(s => $"<div style='width:900px;margin:8px'>{Render(s)}</div>")) + "</body></html>");
    return;
}
var output = args.FirstOrDefault(a => a.EndsWith(".txt")) ?? (classicMode ? "classic.txt" : "baseline.txt");
File.WriteAllLines(output, lines);
Console.WriteLine($"{lines.Count} renderings hashed to {output}{(classicMode ? $" in the classic finish{(finishProperty is null ? ", which this code does not have, so as given" : "")}" : "")}");
if (svgOut is not null)
{
    Directory.CreateDirectory(svgOut);
    for (var i = 0; i < lines.Count; i++) File.WriteAllText(Path.Combine(svgOut, $"{i:000}.svg"), svgs[i]);
    File.WriteAllLines(Path.Combine(svgOut, "names.txt"), lines.Select(l => l[..l.LastIndexOf(' ')]));
}
string Hash(string svg) { svgs.Add(svg); return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(svg)))[..16]; }
