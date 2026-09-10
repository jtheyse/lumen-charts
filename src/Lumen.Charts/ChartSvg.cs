using System.Globalization;
using System.Net;
using System.Text;

namespace Lumen.Charts;

internal sealed class SvgWriter
{
    private readonly StringBuilder output = new();
    /// <summary>Native SVG tooltips. Hosts that draw their own tooltips render marks without them.</summary>
    public bool Titles { get; init; } = true;
    public static string N(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    public static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    public void Add(string value) => output.Append(value);
    public void Text(double x, double y, string? text, string attributes = "") =>
        Add($"<text x='{N(x)}' y='{N(y)}' {attributes}>{E(text)}</text>");
    public void Line(double x1, double y1, double x2, double y2, string attributes = "") =>
        Add($"<line x1='{N(x1)}' y1='{N(y1)}' x2='{N(x2)}' y2='{N(y2)}' {attributes}/>");
    public override string ToString() => output.ToString();
}

public static class ChartSvg
{
    /// <summary>Every entry keeps at least a 3:1 contrast against both the light and the dark chart background.</summary>
    public static readonly IReadOnlyList<string> Palette = Array.AsReadOnly(new[] { "#5675E7", "#169B8D", "#B87F44", "#A775C8", "#D36B84", "#4F93AD" });
    /// <summary>Candlestick bodies are colored by direction rather than by series.</summary>
    public const string RisingColor = "#169B8D", FallingColor = "#D36B84";
    public static string SeriesColor(ChartSeries series, int index) => series.Color ?? Palette[index % Palette.Count];

    /// <summary>Renders a chart. <paramref name="includeTitles"/> controls the native SVG tooltip on each mark.</summary>
    public static string Render(ChartSpec spec, bool includeLegend = true, bool includeTitles = true)
    {
        ChartValidation.Validate(spec);
        var w = new SvgWriter { Titles = includeTitles };
        var legendColumns = Math.Max(1, (spec.Width - 48) / 180);
        var legendRows = includeLegend && spec.Kind is not ChartKind.Donut and not ChartKind.Heatmap and not ChartKind.Histogram and not ChartKind.Box
            ? (int)Math.Ceiling(spec.Series.Count / (double)legendColumns) : 0;
        Begin(w, spec.Width, spec.Height + legendRows * 22, spec.Title, spec.Description, spec.Theme);
        if (!HasData(spec))
            w.Text(spec.Width / 2, spec.Height / 2, "No data to display", "text-anchor='middle'");
        else if (spec.Kind == ChartKind.Donut) Donut(w, spec);
        else if (spec.Kind == ChartKind.Radar) Radar(w, spec);
        else if (spec.Kind == ChartKind.Heatmap) Heatmap(w, spec);
        else if (spec.Kind == ChartKind.Histogram) Histogram(w, spec);
        else if (spec.Kind == ChartKind.Box) Box(w, spec);
        else Cartesian(w, spec);
        w.Text(24, spec.Height - 12, spec.Source, "class='lumen-muted' font-size='11'");
        if (legendRows > 0)
            for (var i = 0; i < spec.Series.Count; i++)
            {
                var x = 24 + i % legendColumns * ((spec.Width - 48d) / legendColumns);
                var y = spec.Height + 10 + i / legendColumns * 22;
                w.Add($"<rect x='{N(x)}' y='{N(y - 8)}' width='9' height='9' rx='2' fill='{SeriesColor(spec.Series[i], i)}'/>");
                w.Text(x + 16, y, Short(spec.Series[i].Name, 24), "font-size='11'");
            }
        w.Add("</svg>");
        return w.ToString();
    }

    internal static void Begin(SvgWriter w, int width, int height, string title, string description, ChartTheme theme)
    {
        var dark = theme == ChartTheme.Dark;
        w.Add($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}' class='lumen-svg' role='group' aria-label='{SvgWriter.E(string.IsNullOrWhiteSpace(description) ? title : $"{title}. {description}")}' style='--lumen-grid:{(dark ? "#303B50" : "#E8EDF5")};--lumen-muted:{(dark ? "#AAB8CF" : "#63718A")};width:100%;height:auto;display:block;background:{(dark ? "#171E2E" : "#FFFFFF")};color:{(dark ? "#E8ECF6" : "#26324B")};font-family:Segoe UI,Arial,sans-serif;font-size:12px' fill='currentColor'>");
        w.Add($"<title>{SvgWriter.E(title)}</title><desc>{SvgWriter.E(description)}</desc>");
        w.Add("<style>.lumen-svg .lumen-grid{stroke:var(--lumen-grid);stroke-width:1}.lumen-svg .lumen-muted{fill:var(--lumen-muted)}.lumen-svg .lumen-datum{outline:none;cursor:pointer}.lumen-svg .lumen-datum:focus{stroke:currentColor;stroke-width:3}.lumen-svg .lumen-datum:hover{filter:brightness(.87)}.lumen-svg .lumen-node{cursor:grab;outline:none}.lumen-svg .lumen-node:focus circle{stroke-width:4}.lumen-svg .lumen-node:active{cursor:grabbing}</style>");
        w.Text(24, 28, title, "font-size='17' font-weight='600'");
        w.Text(24, 49, description, "class='lumen-muted' font-size='11'");
    }

    private static void Datum(SvgWriter w, int series, int index, string label, string shape)
    {
        w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-series='{series}' data-point='{index}' aria-label='{SvgWriter.E(label)}'>{(w.Titles ? $"<title>{SvgWriter.E(label)}</title>" : "")}{shape}</g>");
    }
    private static string PointLabel(ChartSeries s, ChartPoint p) => $"{s.Name}: {p.Label ?? LinearScale.Label(p.X)}, {(p.Y.HasValue ? LinearScale.Label(p.Y.Value) : "missing")}";
    private static string PointLabel(ChartSeries s, ChartPoint p, Axis x, Axis y) =>
        $"{s.Name}: {p.Label ?? x.Format(p.X)}, {(p.Y.HasValue ? y.Format(p.Y.Value) : "missing")}" +
        (p.Low.HasValue && p.High.HasValue ? $" (band {y.Format(p.Low.Value)} to {y.Format(p.High.Value)})" : "");
    private static bool HasData(ChartSpec spec) => spec.Kind == ChartKind.Candlestick
        ? spec.Series.Any(s => s.Points.Count > 0)
        : spec.Series.Any(s => s.Points.Any(p => p.Y.HasValue));
    private static string N(double n) => SvgWriter.N(n);

    private static void Cartesian(SvgWriter w, ChartSpec s)
    {
        var horizontal = s.Kind == ChartKind.Bar;
        var category = s.Kind is ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn;
        var left = horizontal ? 160d : 76d; var right = s.Width - 30d;
        var top = 78d; var bottom = s.Height - 76d;
        var points = s.Series.SelectMany(x => x.Points).ToArray();
        var maxSize = points.Length == 0 ? 0 : points.Max(point => point.Size);
        var cats = points.Select(p => p.X).Distinct().Order().ToArray();
        var xs = Axis.Create(s.XAxis, points.Select(p => p.X), min: s.XMin, max: s.XMax);
        var values = points.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToList();
        if (s.Kind is ChartKind.Candlestick or ChartKind.Band)
            foreach (var p in points.Where(p => p.Low.HasValue && p.High.HasValue)) { values.Add(p.Low!.Value); values.Add(p.High!.Value); }
        if (s.Kind == ChartKind.StackedColumn)
            foreach (var x in cats)
            {
                values.Add(points.Where(p => p.X == x && p.Y > 0).Sum(p => p.Y!.Value));
                values.Add(points.Where(p => p.X == x && p.Y < 0).Sum(p => p.Y!.Value));
            }
        var ys = Axis.Create(s.YAxis, values, s.IncludeZero || category || s.Kind == ChartKind.Area, s.YMin, s.YMax);
        double X(double x) => category ? left + (Array.IndexOf(cats, x) + .5) / cats.Length * (right - left) : xs.Map(x, left, right);
        double Y(double y) => ys.Map(y, bottom, top);
        foreach (var (tick, label) in ys.Ticks())
        {
            if (horizontal)
            {
                var x = ys.Map(tick, left, right); w.Line(x, top, x, bottom, "class='lumen-grid'");
                w.Text(x, bottom + 20, label, "text-anchor='middle' class='lumen-muted'");
            }
            else
            {
                var y = Y(tick); w.Line(left, y, right, y, "class='lumen-grid'");
                w.Text(left - 12, y + 4, label, "text-anchor='end' class='lumen-muted'");
            }
        }
        if (category)
        {
            var step = Math.Max(1, (int)Math.Ceiling(cats.Length / (horizontal ? (bottom - top) / 24 : (right - left) / 65)));
            for (var i = 0; i < cats.Length; i += step)
            {
                var label = points.First(p => p.X == cats[i]).Label ?? LinearScale.Label(cats[i]);
                if (horizontal) w.Text(left - 12, top + (i + .5) / cats.Length * (bottom - top) + 4, Short(label, 21), "text-anchor='end' class='lumen-muted'");
                else w.Text(X(cats[i]), bottom + 21, Short(label, 12), "text-anchor='middle' class='lumen-muted'");
            }
        }
        else
        {
            var labels = points.Where(p => p.Label is not null && p.X >= xs.Min && p.X <= xs.Max).DistinctBy(p => p.X).OrderBy(p => p.X).ToArray();
            if (labels.Length is > 0 and <= 24)
                for (var i = 0; i < labels.Length; i += Math.Max(1, (int)Math.Ceiling(labels.Length / 7d)))
                    w.Text(X(labels[i].X), bottom + 21, Short(labels[i].Label!, 12), "text-anchor='middle' class='lumen-muted'");
            else foreach (var (tick, label) in xs.Ticks(s.XAxis == AxisKind.Time ? 6 : 5)) w.Text(X(tick), bottom + 21, label, "text-anchor='middle' class='lumen-muted'");
        }
        w.Text((left + right) / 2, bottom + 44, horizontal ? s.YLabel : s.XLabel, "text-anchor='middle' class='lumen-muted'");
        w.Text(20, (top + bottom) / 2, horizontal ? s.XLabel : s.YLabel, $"text-anchor='middle' transform='rotate(-90 20 {N((top + bottom) / 2)})' class='lumen-muted'");
        // Nested SVG provides a local clipping viewport without global clip-path IDs.
        w.Add($"<svg x='{N(left)}' y='{N(top)}' width='{N(right-left)}' height='{N(bottom-top)}' viewBox='{N(left)} {N(top)} {N(right-left)} {N(bottom-top)}' overflow='hidden'>");
        var positive = cats.ToDictionary(x => x, _ => 0d); var negative = cats.ToDictionary(x => x, _ => 0d);
        for (var si = 0; si < s.Series.Count; si++)
        {
            var series = s.Series[si]; var color = SeriesColor(series, si);
            if (s.Kind == ChartKind.Candlestick) Candles(w, series, X, Y, xs, ys);
            else if (s.Kind is ChartKind.Line or ChartKind.Area or ChartKind.Band)
            {
                if (s.Kind == ChartKind.Band) Bands(w, series, color, X, Y, s.MaxRenderedPoints);
                // Sample each continuous run independently, preserving missing-observation gaps.
                var start = 0;
                while (start < series.Points.Count)
                {
                    if (!series.Points[start].Y.HasValue) { start++; continue; }
                    var end = start; while (end < series.Points.Count && series.Points[end].Y.HasValue) end++;
                    var run = series.Points.Skip(start).Take(end - start).ToArray();
                    var indices = Sampling.MinMax(run, s.MaxRenderedPoints);
                    var path = string.Join(" ", indices.Select((i, n) => $"{(n == 0 ? "M" : "L")}{N(X(run[i].X))},{N(Y(run[i].Y!.Value))}"));
                    if (s.Kind == ChartKind.Area)
                        w.Add($"<path d='{path} L{N(X(run[^1].X))},{N(Y(0))} L{N(X(run[0].X))},{N(Y(0))} Z' fill='{color}' fill-opacity='.12'/>");
                    w.Add($"<path d='{path}' fill='none' stroke='{color}' stroke-width='2.5' stroke-linejoin='round'/>");
                    foreach (var i in indices)
                    {
                        var p = run[i];
                        Datum(w, si, start + i, PointLabel(series,p,xs,ys), $"<circle cx='{N(X(p.X))}' cy='{N(Y(p.Y!.Value))}' r='{(indices.Count > 80 ? "2" : "4")}' fill='{color}'/>");
                    }
                    start = end;
                }
            }
            else for (var pi = 0; pi < series.Points.Count; pi++)
            {
                var p = series.Points[pi]; if (!p.Y.HasValue) continue;
                var y = p.Y.Value;
                if (category)
                {
                    var ci = Array.IndexOf(cats, p.X);
                    var band = (horizontal ? bottom - top : right - left) / cats.Length;
                    var stacked = s.Kind == ChartKind.StackedColumn;
                    var width = band * .72 / (stacked ? 1 : s.Series.Count);
                    var basis = 0d;
                    if (stacked) { var dict = y >= 0 ? positive : negative; basis = dict[p.X]; dict[p.X] += y; }
                    double rx, ry, rw, rh;
                    if (horizontal)
                    {
                        rx = ys.Map(Math.Min(0, y), left, right); ry = top + ci * band + band * .14 + si * width;
                        rw = Math.Abs(ys.Map(y, left, right) - ys.Map(0, left, right)); rh = width;
                    }
                    else
                    {
                        rx = left + ci * band + band * .14 + (stacked ? 0 : si * width); ry = Math.Min(Y(basis), Y(basis + y));
                        rw = width; rh = Math.Abs(Y(basis + y) - Y(basis));
                    }
                    Datum(w, si, pi, PointLabel(series,p,xs,ys), $"<rect x='{N(rx)}' y='{N(ry)}' width='{N(rw)}' height='{N(rh)}' rx='2' fill='{color}'/>");
                }
                else
                {
                    var radius = s.Kind == ChartKind.Bubble ? Math.Sqrt(p.Size / Math.Max(maxSize, double.Epsilon)) * 22 : 4;
                    Datum(w, si, pi, PointLabel(series,p,xs,ys), $"<circle cx='{N(X(p.X))}' cy='{N(Y(y))}' r='{N(radius)}' fill='{color}' fill-opacity='.7' stroke='{color}'/>");
                }
            }
        }
        w.Add("</svg>");
    }

    private static void Candles(SvgWriter w, ChartSeries series, Func<double, double> X, Func<double, double> Y, Axis xs, Axis ys)
    {
        var columns = series.Points.Select(p => X(p.X)).ToArray();
        var gap = columns.Length > 1 ? Enumerable.Range(1, columns.Length - 1).Min(i => columns[i] - columns[i - 1]) : 30;
        var width = Math.Clamp(gap * .68, 1, 34);
        for (var pi = 0; pi < series.Points.Count; pi++)
        {
            var p = series.Points[pi];
            double open = p.Open!.Value, high = p.High!.Value, low = p.Low!.Value, close = p.Close!.Value;
            var color = close >= open ? RisingColor : FallingColor;
            double body = Y(Math.Max(open, close)), baseline = Y(Math.Min(open, close));
            Datum(w, 0, pi, $"{p.Label ?? xs.Format(p.X)}: open {ys.Format(open)}, high {ys.Format(high)}, low {ys.Format(low)}, close {ys.Format(close)}",
                $"<line x1='{N(columns[pi])}' y1='{N(Y(high))}' x2='{N(columns[pi])}' y2='{N(Y(low))}' stroke='{color}' stroke-width='1.5'/>" +
                $"<rect x='{N(columns[pi] - width / 2)}' y='{N(body)}' width='{N(width)}' height='{N(Math.Max(baseline - body, 1))}' rx='1' fill='{color}'/>");
        }
    }

    private static void Bands(SvgWriter w, ChartSeries series, string color, Func<double, double> X, Func<double, double> Y, int budget)
    {
        // Band outlines follow the same runs and sampling as the central line.
        var start = 0;
        while (start < series.Points.Count)
        {
            if (series.Points[start].Low is null) { start++; continue; }
            var end = start; while (end < series.Points.Count && series.Points[end].Low is not null) end++;
            var run = series.Points.Skip(start).Take(end - start).ToArray();
            var indices = Sampling.MinMax(run, budget);
            var upper = string.Join(" ", indices.Select((i, n) => $"{(n == 0 ? "M" : "L")}{N(X(run[i].X))},{N(Y(run[i].High!.Value))}"));
            var lower = string.Join(" ", indices.Reverse().Select(i => $"L{N(X(run[i].X))},{N(Y(run[i].Low!.Value))}"));
            w.Add($"<path d='{upper} {lower} Z' fill='{color}' fill-opacity='.16'/>");
            start = end;
        }
    }

    /// <summary>Y gridlines, tick labels and axis titles shared by the charts that derive their X axis.</summary>
    private static void Frame(SvgWriter w, ChartSpec s, Axis ys, double left, double right, double top, double bottom)
    {
        foreach (var (tick, label) in ys.Ticks())
        {
            var y = ys.Map(tick, bottom, top);
            w.Line(left, y, right, y, "class='lumen-grid'");
            w.Text(left - 12, y + 4, label, "text-anchor='end' class='lumen-muted'");
        }
        w.Text((left + right) / 2, bottom + 44, s.XLabel, "text-anchor='middle' class='lumen-muted'");
        w.Text(20, (top + bottom) / 2, s.YLabel, $"text-anchor='middle' transform='rotate(-90 20 {N((top + bottom) / 2)})' class='lumen-muted'");
    }

    private static void Aggregate(SvgWriter w, string label, string shape) =>
        w.Add($"<g class='lumen-datum' tabindex='0' role='img' aria-label='{SvgWriter.E(label)}'>{(w.Titles ? $"<title>{SvgWriter.E(label)}</title>" : "")}{shape}</g>");

    private static void Histogram(SvgWriter w, ChartSpec s)
    {
        var observations = s.Series[0].Points.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToArray();
        var bins = Statistics.Bins(observations, s.Bins);
        double left = 76, right = s.Width - 30, top = 78, bottom = s.Height - 76;
        var xs = new Axis(AxisKind.Linear, bins[0].Start, bins[^1].End);
        var ys = Axis.Create(AxisKind.Linear, bins.Select(b => (double)b.Count), true, s.YMin, s.YMax);
        Frame(w, s, ys, left, right, top, bottom);
        var color = SeriesColor(s.Series[0], 0);
        foreach (var bin in bins)
        {
            double x = xs.Map(bin.Start, left, right), width = xs.Map(bin.End, left, right) - x, y = ys.Map(bin.Count, bottom, top);
            // Bins are aggregates: they carry a label and keyboard focus but no original-observation index.
            Aggregate(w, $"{LinearScale.Label(bin.Start)} to {LinearScale.Label(bin.End)}: {bin.Count} observations",
                $"<rect x='{N(x)}' y='{N(y)}' width='{N(Math.Max(width - 1, .5))}' height='{N(bottom - y)}' fill='{color}'/>");
        }
        var step = Math.Max(1, (int)Math.Ceiling(bins.Count / ((right - left) / 70)));
        for (var i = 0; i <= bins.Count; i += step)
        {
            var edge = i < bins.Count ? bins[i].Start : bins[^1].End;
            w.Text(xs.Map(edge, left, right), bottom + 21, LinearScale.Label(edge), "text-anchor='middle' class='lumen-muted'");
        }
        w.Text(right, 64, $"{observations.Length} observations in {bins.Count} equal-width bins", "text-anchor='end' class='lumen-muted' font-size='11'");
    }

    private static void Box(SvgWriter w, ChartSpec s)
    {
        var observations = s.Series.Select(series => series.Points.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToArray()).ToArray();
        double left = 76, right = s.Width - 30, top = 78, bottom = s.Height - 76;
        var ys = Axis.Create(s.YAxis, observations.SelectMany(v => v), s.IncludeZero, s.YMin, s.YMax);
        Frame(w, s, ys, left, right, top, bottom);
        var band = (right - left) / s.Series.Count;
        for (var si = 0; si < s.Series.Count; si++)
        {
            if (observations[si].Length == 0) continue;
            var summary = Statistics.Summarize(observations[si]);
            var color = SeriesColor(s.Series[si], si);
            var center = left + (si + .5) * band;
            var width = Math.Min(band * .45, 80);
            double q1 = ys.Map(summary.Q1, bottom, top), q3 = ys.Map(summary.Q3, bottom, top);
            double lower = ys.Map(summary.LowerWhisker, bottom, top), upper = ys.Map(summary.UpperWhisker, bottom, top);
            var median = ys.Map(summary.Median, bottom, top);
            Aggregate(w, $"{s.Series[si].Name}: median {ys.Format(summary.Median)}, quartiles {ys.Format(summary.Q1)} to {ys.Format(summary.Q3)}, whiskers {ys.Format(summary.LowerWhisker)} to {ys.Format(summary.UpperWhisker)}, {summary.Outliers.Count} outliers",
                $"<line x1='{N(center)}' y1='{N(upper)}' x2='{N(center)}' y2='{N(lower)}' stroke='{color}' stroke-width='1.5'/>" +
                $"<line x1='{N(center - width / 4)}' y1='{N(upper)}' x2='{N(center + width / 4)}' y2='{N(upper)}' stroke='{color}' stroke-width='1.5'/>" +
                $"<line x1='{N(center - width / 4)}' y1='{N(lower)}' x2='{N(center + width / 4)}' y2='{N(lower)}' stroke='{color}' stroke-width='1.5'/>" +
                $"<rect x='{N(center - width / 2)}' y='{N(Math.Min(q1, q3))}' width='{N(width)}' height='{N(Math.Max(Math.Abs(q1 - q3), 1))}' rx='2' fill='{color}' fill-opacity='.18' stroke='{color}' stroke-width='1.5'/>" +
                $"<line x1='{N(center - width / 2)}' y1='{N(median)}' x2='{N(center + width / 2)}' y2='{N(median)}' stroke='{color}' stroke-width='2.5'/>");
            var fence = 1.5 * summary.InterquartileRange;
            for (var pi = 0; pi < s.Series[si].Points.Count; pi++)
            {
                var p = s.Series[si].Points[pi];
                if (p.Y is not { } value || (value >= summary.Q1 - fence && value <= summary.Q3 + fence)) continue;
                Datum(w, si, pi, $"{s.Series[si].Name} outlier: {ys.Format(value)}",
                    $"<circle cx='{N(center)}' cy='{N(ys.Map(value, bottom, top))}' r='3.5' fill='none' stroke='{color}' stroke-width='1.5'/>");
            }
            w.Text(center, bottom + 21, Short($"{s.Series[si].Name} (n={observations[si].Length})", 22), "text-anchor='middle' class='lumen-muted'");
        }
    }

    private static void Donut(SvgWriter w, ChartSpec s)
    {
        var series = s.Series[0]; var total = series.Points.Sum(p => p.Y ?? 0);
        if (total <= 0) { w.Text(s.Width / 2, s.Height / 2, "No positive values", "text-anchor='middle'"); return; }
        var cx = s.Width * .35; var cy = (s.Height + 30) / 2d;
        var r = Math.Min(s.Width * .23, (s.Height - 140) / 2d); var inner = r * .67;
        var angle = -Math.PI / 2;
        for (var i = 0; i < series.Points.Count; i++)
        {
            var p = series.Points[i]; if (p.Y is null or <= 0) continue;
            var sweep = p.Y.Value / total * Math.PI * 2;
            var end = angle + Math.Min(sweep, Math.PI * 2 - .000001);
            var large = sweep > Math.PI ? 1 : 0;
            string At(double radius, double a) => $"{N(cx + radius * Math.Cos(a))},{N(cy + radius * Math.Sin(a))}";
            var path = $"M{At(r,angle)} A{N(r)},{N(r)} 0 {large} 1 {At(r,end)} L{At(inner,end)} A{N(inner)},{N(inner)} 0 {large} 0 {At(inner,angle)} Z";
            var color = Palette[i % Palette.Count];
            Datum(w, 0, i, $"{p.Label ?? LinearScale.Label(p.X)}: {LinearScale.Label(p.Y.Value)} ({p.Y / total:P1})", $"<path d='{path}' fill='{color}'/>");
            if (i < 10)
            {
                var ly = 95 + i * 25;
                w.Add($"<circle cx='{N(s.Width * .65)}' cy='{ly-4}' r='4' fill='{color}'/>");
                w.Text(s.Width * .65 + 14, ly, $"{Short(p.Label ?? LinearScale.Label(p.X),18)}  {LinearScale.Label(p.Y.Value)}");
            }
            angle += sweep;
        }
        w.Text(cx, cy, LinearScale.Label(total), "text-anchor='middle' font-size='28' font-weight='600'");
        w.Text(cx, cy + 22, "TOTAL", "text-anchor='middle' class='lumen-muted' font-size='10'");
    }

    private static void Heatmap(SvgWriter w, ChartSpec s)
    {
        var cats = s.Series.SelectMany(x => x.Points).Select(p => p.X).Distinct().Order().ToArray();
        var values = s.Series.SelectMany(x => x.Points).Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToArray();
        var scale = LinearScale.Create(values);
        var cw = (s.Width - 165d) / cats.Length; var ch = (s.Height - 160d) / s.Series.Count;
        for (var si = 0; si < s.Series.Count; si++)
        {
            w.Text(118, 80 + (si + .5) * ch + 4, Short(s.Series[si].Name,17), "text-anchor='end' class='lumen-muted'");
            for (var pi = 0; pi < s.Series[si].Points.Count; pi++)
            {
                var p = s.Series[si].Points[pi]; if (!p.Y.HasValue) continue;
                var x = 130 + Array.IndexOf(cats,p.X)*cw;
                var t = scale.Map(p.Y.Value, 0, 1);
                var color = $"#{(int)(228-164*t):X2}{(int)(237-132*t):X2}{(int)(252-44*t):X2}";
                // A hairline keeps the palest cells distinguishable from the chart background.
                Datum(w,si,pi,PointLabel(s.Series[si],p),$"<rect x='{N(x+1)}' y='{N(80+si*ch+1)}' width='{N(Math.Max(0,cw-2))}' height='{N(Math.Max(0,ch-2))}' rx='3' fill='{color}' stroke='var(--lumen-muted)' stroke-opacity='.4'/>");
            }
        }
        for (var i = 0; i < cats.Length; i += Math.Max(1,(int)Math.Ceiling(cats.Length/12d)))
            w.Text(130+(i+.5)*cw, s.Height-62, Short(s.Series.SelectMany(x=>x.Points).First(p=>p.X==cats[i]).Label ?? LinearScale.Label(cats[i]),10), "text-anchor='middle' class='lumen-muted'");
        w.Text(130, s.Height-36, $"Color scale: {LinearScale.Label(scale.Min)} (light) to {LinearScale.Label(scale.Max)} (dark)", "class='lumen-muted'");
    }

    private static void Radar(SvgWriter w, ChartSpec s)
    {
        var cats = s.Series.SelectMany(x=>x.Points).Select(p=>p.X).Distinct().Order().ToArray();
        if (cats.Length < 3) throw new ArgumentException("Radar charts require at least three categories.");
        var max = Math.Max(1,s.Series.SelectMany(x=>x.Points).Max(p=>p.Y ?? 0));
        var cx=s.Width/2d; var cy=(s.Height+32)/2d; var r=(s.Height-180)/2d;
        (double X,double Y) At(int i,double value) { var a=2*Math.PI*i/cats.Length-Math.PI/2; return(cx+r*value/max*Math.Cos(a),cy+r*value/max*Math.Sin(a)); }
        for(var ring=1;ring<=4;ring++)
            w.Add($"<polygon points='{string.Join(" ",Enumerable.Range(0,cats.Length).Select(i=> {var p=At(i,max*ring/4);return $"{N(p.X)},{N(p.Y)}";}))}' fill='none' class='lumen-grid'/>");
        for(var i=0;i<cats.Length;i++)
        {
            var p=At(i,max); w.Line(cx,cy,p.X,p.Y,"class='lumen-grid'"); var label=At(i,max*1.14);
            w.Text(label.X,label.Y+4,Short(s.Series.SelectMany(x=>x.Points).First(p=>p.X==cats[i]).Label ?? LinearScale.Label(cats[i]),15),"text-anchor='middle' class='lumen-muted'");
        }
        for(var si=0;si<s.Series.Count;si++)
        {
            var series=s.Series[si];
            if(cats.Any(x=>!series.Points.Any(p=>p.X==x&&p.Y.HasValue))) throw new ArgumentException("Radar series must contain every category without missing values.");
            var color=SeriesColor(series,si);
            w.Add($"<polygon points='{string.Join(" ",cats.Select((x,i)=> {var p=At(i,series.Points.First(p=>p.X==x).Y!.Value);return $"{N(p.X)},{N(p.Y)}";}))}' fill='{color}' fill-opacity='.1' stroke='{color}' stroke-width='2'/>");
            for(var pi=0;pi<series.Points.Count;pi++)
            {
                var p=series.Points[pi];var pos=At(Array.IndexOf(cats,p.X),p.Y!.Value);
                Datum(w,si,pi,PointLabel(series,p),$"<circle cx='{N(pos.X)}' cy='{N(pos.Y)}' r='4' fill='{color}'/>");
            }
        }
        w.Text(24,s.Height-38,$"Radial scale: 0 to {LinearScale.Label(max)}","class='lumen-muted'");
    }
    internal static string Short(string text,int max) => text.Length <= max ? text : text[..(max-1)] + "…";
}

