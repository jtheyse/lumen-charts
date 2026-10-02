using System.Globalization;
using System.Net;
using System.Text;

namespace Lumen.Charts;

internal sealed class SvgWriter
{
    private readonly StringBuilder output = new();
    /// <summary>Native SVG tooltips. Hosts that draw their own tooltips render marks without them.</summary>
    public bool Titles { get; init; } = true;
    public ChartStyle Style { get; init; } = ChartStyle.Light;
    /// <summary>Charts that draw no minor lines carry no rule for them.</summary>
    public bool MinorGrid { get; init; }
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
    public static readonly IReadOnlyList<string> Palette = ChartStyle.Light.Series;
    /// <summary>Candlestick bodies and OHLC bars are colored by direction rather than by series.</summary>
    public const string RisingColor = "#169B8D", FallingColor = "#D36B84";
    public static string SeriesColor(ChartSeries series, int index) => series.Color ?? Palette[index % Palette.Count];
    public static string SeriesColor(ChartSeries series, int index, ChartStyle style) => series.Color ?? style.SeriesColor(index);
    /// <summary>The style a spec draws with: its own, or the preset for its theme.</summary>
    public static ChartStyle ResolveStyle(ChartSpec spec) => spec.Style ?? Preset(spec.Theme);
    internal static ChartStyle Preset(ChartTheme theme) => theme == ChartTheme.Dark ? ChartStyle.Dark : ChartStyle.Light;

    /// <summary>Renders a chart. <paramref name="includeTitles"/> controls the native SVG tooltip on each mark.</summary>
    public static string Render(ChartSpec spec, bool includeLegend = true, bool includeTitles = true)
    {
        ChartValidation.Validate(spec);
        var w = new SvgWriter { Titles = includeTitles, Style = ResolveStyle(spec), MinorGrid = spec.MinorGridlines };
        var legendColumns = Math.Max(1, (spec.Width - 48) / 180);
        // A histogram of one distribution needs no key; of several, its colours are the only way to tell them apart.
        var legendRows = includeLegend && spec.Kind is not ChartKind.Donut and not ChartKind.Heatmap and not ChartKind.Box and not ChartKind.Violin
            && (spec.Kind != ChartKind.Histogram || spec.Series.Count > 1) ? (int)Math.Ceiling(spec.Series.Count / (double)legendColumns) : 0;
        Begin(w, spec.Width, spec.Height + legendRows * 22, spec.Title, spec.Description);
        if (!HasData(spec))
            w.Text(spec.Width / 2, spec.Height / 2, "No data to display", "text-anchor='middle'");
        else if (spec.Kind == ChartKind.Donut) Donut(w, spec);
        else if (spec.Kind == ChartKind.Radar) Radar(w, spec);
        else if (spec.Kind == ChartKind.Heatmap) Heatmap(w, spec);
        else if (spec.Kind == ChartKind.Histogram) Histogram(w, spec);
        else if (spec.Kind == ChartKind.Violin) Violin(w, spec);
        else if (spec.Kind == ChartKind.Box) Box(w, spec);
        else Cartesian(w, spec);
        if (spec.Kind == ChartKind.Scatter && spec.DensityCells is not null)
            w.Text(spec.Width - 30, 64, $"{Count(spec.Series.Sum(series => series.Points.Count(p => p.Y.HasValue)))} observations aggregated into {spec.DensityCells} cells across",
                "text-anchor='end' class='lumen-muted' font-size='11'");
        w.Text(24, spec.Height - 12, spec.Source, "class='lumen-muted' font-size='11'");
        if (legendRows > 0)
            for (var i = 0; i < spec.Series.Count; i++)
            {
                var x = 24 + i % legendColumns * ((spec.Width - 48d) / legendColumns);
                var y = spec.Height + 10 + i / legendColumns * 22;
                w.Add($"<rect x='{N(x)}' y='{N(y - 8)}' width='9' height='9' rx='2' fill='{SeriesColor(spec.Series[i], i, w.Style)}'/>");
                w.Text(x + 16, y, Short(spec.Series[i].Name, 24), "font-size='11'");
            }
        w.Add("</svg>");
        return w.ToString();
    }

    internal static void Begin(SvgWriter w, int width, int height, string title, string description)
    {
        var style = w.Style;
        w.Add($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}' class='lumen-svg' role='group' aria-label='{SvgWriter.E(string.IsNullOrWhiteSpace(description) ? title : $"{title}. {description}")}' style='--lumen-grid:{style.Grid};--lumen-muted:{style.Muted};width:100%;height:auto;display:block;background:{style.Background};color:{style.Text};font-family:{style.FontFamily};font-size:12px' fill='currentColor'>");
        w.Add($"<title>{SvgWriter.E(title)}</title><desc>{SvgWriter.E(description)}</desc>");
        w.Add("<style>.lumen-svg .lumen-grid{stroke:var(--lumen-grid);stroke-width:1}"+(w.MinorGrid?".lumen-svg .lumen-grid-minor{stroke:var(--lumen-grid);stroke-width:1;stroke-opacity:.45}":"")+".lumen-svg .lumen-muted{fill:var(--lumen-muted)}.lumen-svg .lumen-datum{outline:none;cursor:pointer}.lumen-svg .lumen-datum:focus{stroke:currentColor;stroke-width:3}.lumen-svg .lumen-datum:hover{filter:brightness(.87)}.lumen-svg .lumen-node{cursor:grab;outline:none}.lumen-svg .lumen-node:focus circle{stroke-width:4}.lumen-svg .lumen-node:active{cursor:grabbing}</style>");
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
    private static bool HasData(ChartSpec spec) => spec.Kind is ChartKind.Candlestick or ChartKind.Ohlc
        ? spec.Series.Any(s => s.Points.Count > 0)
        : spec.Series.Any(s => s.Summary is not null || s.Points.Any(p => p.Y.HasValue));
    private static string N(double n) => SvgWriter.N(n);
    /// <summary>Counts are grouped invariantly, so a host's culture cannot change what the chart reads.</summary>
    private static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static void Cartesian(SvgWriter w, ChartSpec s)
    {
        var horizontal = s.Kind == ChartKind.Bar;
        var category = s.Kind is ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn;
        var secondary = s.Series.Any(series => series.Secondary);
        var left = horizontal ? 160d : 76d; var right = s.Width - (secondary ? 76d : 30d);
        var top = 78d; var bottom = s.Height - 76d;
        var points = s.Series.SelectMany(x => x.Points).ToArray();
        var maxSize = points.Length == 0 ? 0 : points.Max(point => point.Size);
        var cats = points.Select(p => p.X).Distinct().Order().ToArray();
        var xs = Axis.Create(s.XAxis, points.Select(p => p.X), min: s.XMin, max: s.XMax, zone: TimeAxis.Zone(s.TimeZone),
            weekends: s.SkipWeekends, skips: s.TimeSkips.Count > 0 ? s.TimeSkips : null);
        var primary = s.Series.Where(series => !series.Secondary).SelectMany(series => series.Points).ToArray();
        var values = primary.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToList();
        if (s.Kind is ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band)
            foreach (var p in primary.Where(p => p.Low.HasValue && p.High.HasValue)) { values.Add(p.Low!.Value); values.Add(p.High!.Value); }
        if (s.Kind == ChartKind.StackedColumn)
            foreach (var x in cats)
            {
                values.Add(points.Where(p => p.X == x && p.Y > 0).Sum(p => p.Y!.Value));
                values.Add(points.Where(p => p.X == x && p.Y < 0).Sum(p => p.Y!.Value));
            }
        var zero = s.IncludeZero || category || s.Kind == ChartKind.Area;
        var ys = Axis.Create(s.YAxis, values, zero, s.YMin, s.YMax);
        var second = s.Series.Where(series => series.Secondary).SelectMany(series => series.Points).ToArray();
        var secondValues = second.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToList();
        if (s.Kind == ChartKind.Band)
            foreach (var p in second.Where(p => p.Low.HasValue && p.High.HasValue)) { secondValues.Add(p.Low!.Value); secondValues.Add(p.High!.Value); }
        var ys2 = secondary ? Axis.Create(s.Y2Axis, secondValues, zero, s.Y2Min, s.Y2Max) : ys;
        double X(double x) => category ? left + (Array.IndexOf(cats, x) + .5) / cats.Length * (right - left) : xs.Map(x, left, right);
        double Y(double y) => ys.Map(y, bottom, top);
        if (s.MinorGridlines)
        {
            foreach (var minor in ys.MinorTicks())
            {
                if (horizontal) { var x = ys.Map(minor, left, right); w.Line(x, top, x, bottom, "class='lumen-grid-minor'"); }
                else { var y = Y(minor); w.Line(left, y, right, y, "class='lumen-grid-minor'"); }
            }
            if (!category && !horizontal)
                foreach (var minor in xs.MinorTicks())
                {
                    var x = X(minor);
                    w.Line(x, top, x, bottom, "class='lumen-grid-minor'");
                }
        }
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
        // Ticks on the right, but no second set of gridlines: one grid is what a reader can follow.
        if (secondary)
            foreach (var (tick, label) in ys2.Ticks())
                w.Text(right + 12, ys2.Map(tick, bottom, top) + 4, label, "text-anchor='start' class='lumen-muted'");
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
        if (secondary)
            w.Text(s.Width - 16, (top + bottom) / 2, s.Y2Label, $"text-anchor='middle' transform='rotate(90 {N(s.Width - 16)} {N((top + bottom) / 2)})' class='lumen-muted'");
        // Nested SVG provides a local clipping viewport without global clip-path IDs. It is inset by
        // one marker radius so a mark on the first or last value is drawn whole and stays hoverable.
        const double bleed = 6;
        w.Add($"<svg x='{N(left-bleed)}' y='{N(top-bleed)}' width='{N(right-left+2*bleed)}' height='{N(bottom-top+2*bleed)}' viewBox='{N(left-bleed)} {N(top-bleed)} {N(right-left+2*bleed)} {N(bottom-top+2*bleed)}' overflow='hidden'>");
        // Behind the data, and inside the clip, so a reference pans and zooms with what it refers to.
        foreach (var annotation in s.Annotations) Annotate(w, annotation, X, Y, xs, ys, left, right, top, bottom);
        var positive = cats.ToDictionary(x => x, _ => 0d); var negative = cats.ToDictionary(x => x, _ => 0d);
        for (var si = 0; si < s.Series.Count; si++)
        {
            var series = s.Series[si]; var color = SeriesColor(series, si, w.Style);
            // Each series is measured against its own axis from here on.
            var scale = series.Secondary ? ys2 : ys;
            double At(double y) => scale.Map(y, bottom, top);
            if (s.Kind == ChartKind.Scatter && s.DensityCells is { } cells) Density(w, series, color, X, At, xs, scale, cells, left, right, top, bottom);
            else if (s.Kind == ChartKind.Candlestick) Candles(w, series, X, At, xs, scale);
            else if (s.Kind == ChartKind.Ohlc) Ohlc(w, series, X, At, xs, scale);
            else if (s.Kind is ChartKind.Line or ChartKind.Area or ChartKind.Band)
            {
                if (s.Kind == ChartKind.Band) Bands(w, series, color, X, At, s.MaxRenderedPoints);
                // Sample each continuous run independently, preserving missing-observation gaps.
                var start = 0;
                while (start < series.Points.Count)
                {
                    if (!series.Points[start].Y.HasValue) { start++; continue; }
                    var end = start; while (end < series.Points.Count && series.Points[end].Y.HasValue) end++;
                    var run = series.Points.Skip(start).Take(end - start).ToArray();
                    var indices = Sampling.MinMax(run, s.MaxRenderedPoints);
                    var path = string.Join(" ", indices.Select((i, n) => $"{(n == 0 ? "M" : "L")}{N(X(run[i].X))},{N(At(run[i].Y!.Value))}"));
                    if (s.Kind == ChartKind.Area)
                        w.Add($"<path d='{path} L{N(X(run[^1].X))},{N(At(0))} L{N(X(run[0].X))},{N(At(0))} Z' fill='{color}' fill-opacity='.12'/>");
                    w.Add($"<path d='{path}' fill='none' stroke='{color}' stroke-width='2.5' stroke-linejoin='round'/>");
                    foreach (var i in indices)
                    {
                        var p = run[i];
                        Datum(w, si, start + i, PointLabel(series,p,xs,scale), $"<circle cx='{N(X(p.X))}' cy='{N(At(p.Y!.Value))}' r='{(indices.Count > 80 ? "2" : "4")}' fill='{color}'/>");
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
                        rx = left + ci * band + band * .14 + (stacked ? 0 : si * width); ry = Math.Min(At(basis), At(basis + y));
                        rw = width; rh = Math.Abs(At(basis + y) - At(basis));
                    }
                    Datum(w, si, pi, PointLabel(series,p,xs,scale), $"<rect x='{N(rx)}' y='{N(ry)}' width='{N(rw)}' height='{N(rh)}' rx='2' fill='{color}'/>");
                }
                else
                {
                    var radius = s.Kind == ChartKind.Bubble ? Math.Sqrt(p.Size / Math.Max(maxSize, double.Epsilon)) * 22 : 4;
                    Datum(w, si, pi, PointLabel(series,p,xs,scale), $"<circle cx='{N(X(p.X))}' cy='{N(At(y))}' r='{N(radius)}' fill='{color}' fill-opacity='.7' stroke='{color}'/>");
                }
            }
            if (series.Trend) Trend(w, series, color, X, At, left, right);
        }
        w.Add("</svg>");
    }

    /// <summary>
    /// A least-squares line across the plot, fitted in the space the reader sees. The axes have already
    /// taken the logarithm and left out the spans a calendar skips, so the line is straight on screen
    /// instead of curving on a log axis or jumping where a trading axis closes. Least squares is
    /// unchanged by the scaling between data and pixels, so on plain axes this is the ordinary fit.
    /// </summary>
    private static void Trend(SvgWriter w, ChartSeries series, string color, Func<double, double> X, Func<double, double> Y, double left, double right)
    {
        var fit = Statistics.Fit(series.Points.Where(p => p.Y.HasValue).Select(p => (X(p.X), Y(p.Y!.Value))));
        if (fit is null) return;
        // Screen y grows downwards, so a falling line is a rising series.
        var label = $"{series.Name} trend: {(fit.Slope <= 0 ? "rising" : "falling")}, R squared {fit.R2.ToString("0.00", CultureInfo.InvariantCulture)}";
        w.Add($"<path class='lumen-trend' d='M{N(left)},{N(fit.Predict(left))} L{N(right)},{N(fit.Predict(right))}' " +
            $"fill='none' stroke='{color}' stroke-width='2' stroke-dasharray='7 5' stroke-opacity='.85' role='img' aria-label='{SvgWriter.E(label)}'>" +
            $"{(w.Titles ? $"<title>{SvgWriter.E(label)}</title>" : "")}</path>");
    }

    /// <summary>One shaded cell per occupied region. Cells are square in pixels, and a cell's opacity
    /// follows the logarithm of its count so a dense core does not flatten everything around it.</summary>
    private static void Density(SvgWriter w, ChartSeries series, string color, Func<double, double> X, Func<double, double> Y,
        Axis xs, Axis ys, int cells, double left, double right, double top, double bottom)
    {
        var size = (right - left) / cells;
        var counts = new Dictionary<(int Column, int Row), int>();
        foreach (var p in series.Points)
        {
            if (!p.Y.HasValue) continue;
            var key = ((int)Math.Floor((X(p.X) - left) / size), (int)Math.Floor((Y(p.Y.Value) - top) / size));
            counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
        }
        if (counts.Count == 0) return;
        var busiest = counts.Values.Max();
        foreach (var ((column, row), count) in counts.OrderBy(c => c.Key.Column).ThenBy(c => c.Key.Row))
        {
            double x = left + column * size, y = top + row * size;
            var weight = .22 + .68 * Math.Log(1 + count) / Math.Log(1 + busiest);
            var label = $"{series.Name}: {Count(count)} observation{(count == 1 ? "" : "s")}, " +
                        $"{xs.Format(xs.Invert(x, left, right))} to {xs.Format(xs.Invert(x + size, left, right))}, " +
                        $"{ys.Format(ys.Invert(y + size, bottom, top))} to {ys.Format(ys.Invert(y, bottom, top))}";
            Aggregate(w, label, $"<rect x='{N(x)}' y='{N(y)}' width='{N(size)}' height='{N(size)}' fill='{color}' fill-opacity='{N(Math.Round(weight, 3))}'/>");
        }
    }

    private static void Annotate(SvgWriter w, ChartAnnotation annotation, Func<double, double> X, Func<double, double> Y,
        Axis xs, Axis ys, double left, double right, double top, double bottom)
    {
        var horizontal = annotation.Axis == AnnotationAxis.Y;
        var axis = horizontal ? ys : xs;
        var colour = annotation.Color ?? w.Style.Muted;
        var at = horizontal ? Y(annotation.From) : X(annotation.From);
        string shape, reading;
        double labelX, labelY; string anchor;
        if (annotation.To is { } to)
        {
            var other = horizontal ? Y(to) : X(to);
            double x = horizontal ? left : Math.Min(at, other), y = horizontal ? Math.Min(at, other) : top;
            double width = horizontal ? right - left : Math.Abs(other - at), height = horizontal ? Math.Abs(other - at) : bottom - top;
            shape = $"<rect x='{N(x)}' y='{N(y)}' width='{N(width)}' height='{N(height)}' fill='{colour}' fill-opacity='.12'/>";
            reading = $"{axis.Format(annotation.From)} to {axis.Format(to)}";
            (labelX, labelY, anchor) = horizontal ? (right - 6, y + 13, "end") : (x + 6, top + 13, "start");
        }
        else
        {
            var dash = annotation.Dashed ? " stroke-dasharray='6 4'" : "";
            double x1 = horizontal ? left : at, y1 = horizontal ? at : top, x2 = horizontal ? right : at, y2 = horizontal ? at : bottom;
            // An invisible wider line carries the pointer, so a dashed reference is hoverable
            // along its whole length rather than only where a dash happens to fall.
            shape = $"<line x1='{N(x1)}' y1='{N(y1)}' x2='{N(x2)}' y2='{N(y2)}' stroke='{colour}' stroke-opacity='0' stroke-width='12'/>" +
                $"<line x1='{N(x1)}' y1='{N(y1)}' x2='{N(x2)}' y2='{N(y2)}' stroke='{colour}' stroke-width='1.5'{dash}/>";
            reading = axis.Format(annotation.From);
            (labelX, labelY, anchor) = horizontal ? (right - 6, at - 6, "end") : (at + 6, top + 13, "start");
        }
        var label = annotation.Label is null ? reading : $"{annotation.Label}: {reading}";
        Aggregate(w, label, shape + $"<text x='{N(labelX)}' y='{N(labelY)}' text-anchor='{anchor}' fill='{colour}' font-size='11'>{SvgWriter.E(label)}</text>");
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
            var color = close >= open ? w.Style.Rising : w.Style.Falling;
            double body = Y(Math.Max(open, close)), baseline = Y(Math.Min(open, close));
            Datum(w, 0, pi, $"{p.Label ?? xs.Format(p.X)}: open {ys.Format(open)}, high {ys.Format(high)}, low {ys.Format(low)}, close {ys.Format(close)}",
                $"<line x1='{N(columns[pi])}' y1='{N(Y(high))}' x2='{N(columns[pi])}' y2='{N(Y(low))}' stroke='{color}' stroke-width='1.5'/>" +
                $"<rect x='{N(columns[pi] - width / 2)}' y='{N(body)}' width='{N(width)}' height='{N(Math.Max(baseline - body, 1))}' rx='1' fill='{color}'/>");
        }
    }

    /// <summary>
    /// The American bar: one vertical line over the day's range, the open ticking out to the left and the
    /// close to the right. A tick is half the width a candle body takes, so a bar occupies the same column
    /// and the two drawings of the same prices can be compared side by side.
    /// </summary>
    private static void Ohlc(SvgWriter w, ChartSeries series, Func<double, double> X, Func<double, double> Y, Axis xs, Axis ys)
    {
        var columns = series.Points.Select(p => X(p.X)).ToArray();
        var gap = columns.Length > 1 ? Enumerable.Range(1, columns.Length - 1).Min(i => columns[i] - columns[i - 1]) : 30;
        var tick = Math.Clamp(gap * .34, .5, 17);
        for (var pi = 0; pi < series.Points.Count; pi++)
        {
            var p = series.Points[pi];
            double open = p.Open!.Value, high = p.High!.Value, low = p.Low!.Value, close = p.Close!.Value;
            var color = close >= open ? w.Style.Rising : w.Style.Falling;
            Datum(w, 0, pi, $"{p.Label ?? xs.Format(p.X)}: open {ys.Format(open)}, high {ys.Format(high)}, low {ys.Format(low)}, close {ys.Format(close)}",
                $"<line x1='{N(columns[pi])}' y1='{N(Y(high))}' x2='{N(columns[pi])}' y2='{N(Y(low))}' stroke='{color}' stroke-width='1.5'/>" +
                $"<line x1='{N(columns[pi] - tick)}' y1='{N(Y(open))}' x2='{N(columns[pi])}' y2='{N(Y(open))}' stroke='{color}' stroke-width='1.5'/>" +
                $"<line x1='{N(columns[pi])}' y1='{N(Y(close))}' x2='{N(columns[pi] + tick)}' y2='{N(Y(close))}' stroke='{color}' stroke-width='1.5'/>");
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
        if (s.MinorGridlines)
            foreach (var minor in ys.MinorTicks())
            {
                var y = ys.Map(minor, bottom, top);
                w.Line(left, y, right, y, "class='lumen-grid-minor'");
            }
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

    /// <summary>
    /// Several distributions share one set of bins, chosen from their pooled observations, so a bin covers the
    /// same range for each. Their bars stand side by side within it rather than over one another, so no bar
    /// hides another; a single distribution fills each bin with one bar.
    /// </summary>
    private static void Histogram(SvgWriter w, ChartSpec s)
    {
        var observations = s.Series.Select(series => series.Points.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToArray()).ToArray();
        var counted = Statistics.SharedBins(observations, s.Bins);
        var bins = counted[0];
        double left = 76, right = s.Width - 30, top = 78, bottom = s.Height - 76;
        var xs = new Axis(AxisKind.Linear, bins[0].Start, bins[^1].End);
        var ys = Axis.Create(AxisKind.Linear, counted.SelectMany(set => set).Select(b => (double)b.Count), true, s.YMin, s.YMax);
        Frame(w, s, ys, left, right, top, bottom);
        var several = s.Series.Count > 1;
        for (var bi = 0; bi < bins.Count; bi++)
        {
            double x = xs.Map(bins[bi].Start, left, right), width = xs.Map(bins[bi].End, left, right) - x;
            var full = Math.Max(width - 1, .5);
            // Several bars inset their group, so a bin reads as one group instead of running into the next.
            var inset = several ? full * .1 : 0;
            var slot = (full - 2 * inset) / s.Series.Count;
            for (var si = 0; si < s.Series.Count; si++)
            {
                var bin = counted[si][bi];
                var y = ys.Map(bin.Count, bottom, top);
                // Bins are aggregates: they carry a label and keyboard focus but no original-observation index.
                Aggregate(w, $"{(several ? $"{s.Series[si].Name}, " : "")}{LinearScale.Label(bin.Start)} to {LinearScale.Label(bin.End)}: {bin.Count} observations",
                    $"<rect x='{N(x + inset + si * slot)}' y='{N(y)}' width='{N(slot)}' height='{N(bottom - y)}' fill='{SeriesColor(s.Series[si], si, w.Style)}'/>");
            }
        }
        var step = Math.Max(1, (int)Math.Ceiling(bins.Count / ((right - left) / 70)));
        for (var i = 0; i <= bins.Count; i += step)
        {
            var edge = i < bins.Count ? bins[i].Start : bins[^1].End;
            w.Text(xs.Map(edge, left, right), bottom + 21, LinearScale.Label(edge), "text-anchor='middle' class='lumen-muted'");
        }
        var total = observations.Sum(set => set.Length);
        w.Text(right, 64, several ? $"{total} observations across {s.Series.Count} series in {bins.Count} shared equal-width bins" : $"{total} observations in {bins.Count} equal-width bins",
            "text-anchor='end' class='lumen-muted' font-size='11'");
    }

    private static void Box(SvgWriter w, ChartSpec s)
    {
        var observations = s.Series.Select(series => series.Points.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToArray()).ToArray();
        double left = 76, right = s.Width - 30, top = 78, bottom = s.Height - 76;
        // A supplied summary has no observations behind it, so its whiskers and outliers are what the axis must reach.
        var supplied = s.Series.Select(series => series.Summary).OfType<BoxSummary>()
            .SelectMany(summary => summary.Outliers.Append(summary.LowerWhisker).Append(summary.UpperWhisker));
        var ys = Axis.Create(s.YAxis, observations.SelectMany(v => v).Concat(supplied), s.IncludeZero, s.YMin, s.YMax);
        Frame(w, s, ys, left, right, top, bottom);
        var band = (right - left) / s.Series.Count;
        for (var si = 0; si < s.Series.Count; si++)
        {
            var given = s.Series[si].Summary;
            if (given is null && observations[si].Length == 0) continue;
            var summary = given ?? Statistics.Summarize(observations[si]);
            var color = SeriesColor(s.Series[si], si, w.Style);
            var center = left + (si + .5) * band;
            var width = Math.Min(band * .45, 80);
            double q1 = ys.Map(summary.Q1, bottom, top), q3 = ys.Map(summary.Q3, bottom, top);
            double lower = ys.Map(summary.LowerWhisker, bottom, top), upper = ys.Map(summary.UpperWhisker, bottom, top);
            var median = ys.Map(summary.Median, bottom, top);
            Aggregate(w, $"{s.Series[si].Name}{(given is null ? "" : " (supplied summary)")}: median {ys.Format(summary.Median)}, quartiles {ys.Format(summary.Q1)} to {ys.Format(summary.Q3)}, whiskers {ys.Format(summary.LowerWhisker)} to {ys.Format(summary.UpperWhisker)}, {summary.Outliers.Count} outliers",
                $"<line x1='{N(center)}' y1='{N(upper)}' x2='{N(center)}' y2='{N(lower)}' stroke='{color}' stroke-width='1.5'/>" +
                $"<line x1='{N(center - width / 4)}' y1='{N(upper)}' x2='{N(center + width / 4)}' y2='{N(upper)}' stroke='{color}' stroke-width='1.5'/>" +
                $"<line x1='{N(center - width / 4)}' y1='{N(lower)}' x2='{N(center + width / 4)}' y2='{N(lower)}' stroke='{color}' stroke-width='1.5'/>" +
                $"<rect x='{N(center - width / 2)}' y='{N(Math.Min(q1, q3))}' width='{N(width)}' height='{N(Math.Max(Math.Abs(q1 - q3), 1))}' rx='2' fill='{color}' fill-opacity='.18' stroke='{color}' stroke-width='1.5'/>" +
                $"<line x1='{N(center - width / 2)}' y1='{N(median)}' x2='{N(center + width / 2)}' y2='{N(median)}' stroke='{color}' stroke-width='2.5'/>");
            string Mark(double value) => $"<circle cx='{N(center)}' cy='{N(ys.Map(value, bottom, top))}' r='3.5' fill='none' stroke='{color}' stroke-width='1.5'/>";
            if (given is not null)
            {
                // A supplied outlier is a number, not one of the series' points, so it can name no point to select.
                foreach (var value in given.Outliers) Aggregate(w, $"{s.Series[si].Name} outlier (supplied summary): {ys.Format(value)}", Mark(value));
                // The observation count is unknown, so the column claims none.
                w.Text(center, bottom + 21, Short($"{s.Series[si].Name} (summary)", 22), "text-anchor='middle' class='lumen-muted'");
                continue;
            }
            var fence = 1.5 * summary.InterquartileRange;
            for (var pi = 0; pi < s.Series[si].Points.Count; pi++)
            {
                var p = s.Series[si].Points[pi];
                if (p.Y is not { } value || (value >= summary.Q1 - fence && value <= summary.Q3 + fence)) continue;
                Datum(w, si, pi, $"{s.Series[si].Name} outlier: {ys.Format(value)}", Mark(value));
            }
            w.Text(center, bottom + 21, Short($"{s.Series[si].Name} (n={observations[si].Length})", 22), "text-anchor='middle' class='lumen-muted'");
        }
    }

    /// <summary>
    /// One kernel density estimate per series, mirrored about its own column. The estimate is made in the
    /// space the axis draws in, so a logarithmic axis shapes the violin in logarithms rather than stretching
    /// one tail across the plot. The widest point of each violin fills its column, so shapes are comparable
    /// within a chart but the width carries no units; the quartile bar and median tick carry the numbers.
    /// </summary>
    private static void Violin(SvgWriter w, ChartSpec s)
    {
        var observations = s.Series.Select(series => series.Points.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToArray()).ToArray();
        double left = 76, right = s.Width - 30, top = 78, bottom = s.Height - 76;
        var ys = Axis.Create(s.YAxis, observations.SelectMany(v => v), s.IncludeZero, s.YMin, s.YMax);
        Frame(w, s, ys, left, right, top, bottom);
        var band = (right - left) / s.Series.Count;
        var logarithmic = s.YAxis == AxisKind.Log;
        for (var si = 0; si < s.Series.Count; si++)
        {
            if (observations[si].Length == 0) continue;
            var color = SeriesColor(s.Series[si], si, w.Style);
            var center = left + (si + .5) * band;
            var width = Math.Min(band * .45, 80);
            var summary = Statistics.Summarize(observations[si]);
            var estimate = Statistics.Density(logarithmic ? observations[si].Select(Math.Log10).ToArray() : observations[si]);
            var shape = "";
            if (estimate.Count > 0)
            {
                var peak = estimate.Max(point => point.Density);
                double At(double value) => ys.Map(logarithmic ? Math.Pow(10, value) : value, bottom, top);
                string Side(IEnumerable<(double Value, double Density)> points, int direction) => string.Join(" ",
                    points.Select(point => $"{N(center + direction * point.Density / peak * width)},{N(At(point.Value))}"));
                shape = $"<path d='M{Side(estimate, 1)} {Side(estimate.Reverse(), -1)} Z' fill='{color}' fill-opacity='.22' stroke='{color}' stroke-width='1.5' stroke-linejoin='round'/>";
            }
            double q1 = ys.Map(summary.Q1, bottom, top), q3 = ys.Map(summary.Q3, bottom, top);
            var median = ys.Map(summary.Median, bottom, top);
            Aggregate(w, $"{s.Series[si].Name}: {Count(observations[si].Length)} observations, median {ys.Format(summary.Median)}, quartiles {ys.Format(summary.Q1)} to {ys.Format(summary.Q3)}, range {ys.Format(observations[si].Min())} to {ys.Format(observations[si].Max())}",
                shape +
                $"<rect x='{N(center - 4)}' y='{N(Math.Min(q1, q3))}' width='8' height='{N(Math.Max(Math.Abs(q1 - q3), 1))}' rx='2' fill='{color}' fill-opacity='.85'/>" +
                $"<line x1='{N(center - width / 2)}' y1='{N(median)}' x2='{N(center + width / 2)}' y2='{N(median)}' stroke='{color}' stroke-width='2.5'/>");
            w.Text(center, bottom + 21, Short($"{s.Series[si].Name} (n={Count(observations[si].Length)})", 22), "text-anchor='middle' class='lumen-muted'");
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
            var color = w.Style.SeriesColor(i);
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
                var color = Mix(w.Style.HeatmapLow, w.Style.HeatmapHigh, t);
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
            var color=SeriesColor(series,si,w.Style);
            w.Add($"<polygon points='{string.Join(" ",cats.Select((x,i)=> {var p=At(i,series.Points.First(p=>p.X==x).Y!.Value);return $"{N(p.X)},{N(p.Y)}";}))}' fill='{color}' fill-opacity='.1' stroke='{color}' stroke-width='2'/>");
            for(var pi=0;pi<series.Points.Count;pi++)
            {
                var p=series.Points[pi];var pos=At(Array.IndexOf(cats,p.X),p.Y!.Value);
                Datum(w,si,pi,PointLabel(series,p),$"<circle cx='{N(pos.X)}' cy='{N(pos.Y)}' r='4' fill='{color}'/>");
            }
        }
        w.Text(24,s.Height-38,$"Radial scale: 0 to {LinearScale.Label(max)}","class='lumen-muted'");
    }
    /// <summary>Linear interpolation per channel, truncated, which is how the heatmap ramp has always been computed.</summary>
    private static string Mix(string low, string high, double t)
    {
        int Channel(string hex, int offset) => int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int Blend(int offset) => (int)(Channel(low, offset) + (Channel(high, offset) - Channel(low, offset)) * t);
        return $"#{Blend(1):X2}{Blend(3):X2}{Blend(5):X2}";
    }
    internal static string Short(string text,int max) => text.Length <= max ? text : text[..(max-1)] + "…";
}

