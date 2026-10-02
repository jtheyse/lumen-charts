using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Lumen.Charts;

internal sealed class SvgWriter
{
    private readonly StringBuilder output = new();
    private readonly StringBuilder definitions = new();
    private readonly Dictionary<string, string> gradients = [];
    private int definitionsAt;
    private string? prefix;
    /// <summary>Native SVG tooltips. Hosts that draw their own tooltips render marks without them.</summary>
    public bool Titles { get; init; } = true;
    public ChartStyle Style { get; init; } = ChartStyle.Light;
    /// <summary>Charts that draw no minor lines carry no rule for them.</summary>
    public bool MinorGrid { get; init; }
    /// <summary>The chart being drawn, whose hash names its gradients.</summary>
    public ChartSpec? Spec { get; init; }
    /// <summary>The refined finish rather than the classic one, which draws as 0.23.0 did.</summary>
    public bool Refined => Style.Finish == ChartFinish.Refined;
    /// <summary>In the refined finish a stroke keeps its width at any display size; in the classic one it scales with the drawing.</summary>
    public string Fixed => Refined ? " vector-effect='non-scaling-stroke'" : "";
    public static string N(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    public static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    public void Add(string value) => output.Append(value);
    public void Text(double x, double y, string? text, string attributes = "") =>
        Add($"<text x='{N(x)}' y='{N(y)}' {attributes}>{E(text)}</text>");
    public void Line(double x1, double y1, double x2, double y2, string attributes = "") =>
        Add($"<line x1='{N(x1)}' y1='{N(y1)}' x2='{N(x2)}' y2='{N(y2)}' {attributes}/>");
    /// <summary>Where the definitions go if the chart turns out to need any.</summary>
    public void MarkDefinitions() => definitionsAt = output.Length;
    /// <summary>
    /// Defines a gradient, given as its element without an ID, and returns the ID to paint with. Several charts share one
    /// page, so the ID starts with a hash of the chart's own spec: two different charts cannot collide, and two identical
    /// ones define identical gradients. A gradient asked for twice is defined once.
    /// </summary>
    public string Gradient(string element)
    {
        if (gradients.TryGetValue(element, out var id)) return id;
        prefix ??= ChartSvg.IdPrefix(Spec!);
        id = $"{prefix}-{gradients.Count}";
        gradients[element] = id;
        definitions.Append(element.Insert("<linearGradient".Length, $" id='{id}'"));
        return id;
    }
    public override string ToString() =>
        definitions.Length == 0 ? output.ToString() : output.ToString().Insert(definitionsAt, $"<defs>{definitions}</defs>");
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
    /// <summary>The mark a series draws: its own kind, or the chart's.</summary>
    internal static ChartKind Mark(ChartSpec spec, ChartSeries series) => series.Kind ?? spec.Kind;
    /// <summary>What pane <paramref name="index"/> draws with: the spec's own Y properties for the main plot, and
    /// <c>Panes[index - 1]</c> below it.</summary>
    internal static ChartPane Pane(ChartSpec spec, int index) => index == 0
        ? new() { Label = spec.YLabel, Weight = 1, YAxis = spec.YAxis, YMin = spec.YMin, YMax = spec.YMax, YFormat = spec.YFormat, YReversed = spec.YReversed, YZones = spec.YZones,
            Y2Label = spec.Y2Label, Y2Axis = spec.Y2Axis, Y2Min = spec.Y2Min, Y2Max = spec.Y2Max, Y2Format = spec.Y2Format, Y2Reversed = spec.Y2Reversed }
        : spec.Panes[index - 1];

    // Fixed here rather than taken from a host, so the same spec always hashes to the same IDs. Every property exists on
    // every record, so leaving out the nulls loses nothing, and it halves the text a long series makes.
    private static readonly JsonSerializerOptions Hashing = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Unfinished } }, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    /// <summary>A classic style is serialized for hashing as 0.23.0 serialized it, without its finish.</summary>
    private static void Unfinished(JsonTypeInfo info)
    {
        if (info.Type != typeof(ChartStyle)) return;
        foreach (var property in info.Properties)
            if (property.Name == nameof(ChartStyle.Finish)) property.ShouldSerialize = (_, finish) => finish is not ChartFinish.Classic;
    }
    private static byte[] Hashed<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Hashing);
    /// <summary>
    /// The prefix of every ID a chart defines: <c>lumen-</c> and the first twelve hex digits of the SHA-256 of its spec as
    /// JSON. A classic chart hashes as 0.23.0 hashed the chart it draws, so it names its gradients as it did then: its style
    /// without the finish, and its theme's own preset as no style at all, which is how a chart on a theme was written. A
    /// refined chart hashes with the style it draws with, finish included, so a page that shows a chart in both finishes
    /// never gives the two one ID.
    /// </summary>
    internal static string IdPrefix(ChartSpec spec)
    {
        var style = ResolveStyle(spec);
        var hashed = style.Finish == ChartFinish.Refined ? spec with { Style = style }
            : Hashed(style with { Finish = ChartFinish.Refined }).AsSpan().SequenceEqual(Hashed(Preset(spec.Theme))) ? spec with { Style = null } : spec;
        return "lumen-" + Convert.ToHexString(SHA256.HashData(Hashed(hashed)))[..12].ToLowerInvariant();
    }

    /// <summary>Renders a chart. <paramref name="includeTitles"/> controls the native SVG tooltip on each mark.</summary>
    public static string Render(ChartSpec spec, bool includeLegend = true, bool includeTitles = true)
    {
        ChartValidation.Validate(spec);
        var style = ResolveStyle(spec);
        var w = new SvgWriter { Titles = includeTitles, Style = style, MinorGrid = spec.MinorGridlines && style.Gridlines != GridLine.Hidden, Spec = spec };
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
            w.Text(spec.Width - 30, 64, $"{Count(spec.Series.Where(series => Mark(spec, series) == ChartKind.Scatter).Sum(series => series.Points.Count(p => p.Y.HasValue)))} observations aggregated into {spec.DensityCells} cells across",
                "text-anchor='end' class='lumen-muted' font-size='11'");
        w.Text(24, spec.Height - 12, spec.Source, "class='lumen-muted' font-size='11'");
        if (legendRows > 0)
            for (var i = 0; i < spec.Series.Count; i++)
            {
                var x = 24 + i % legendColumns * ((spec.Width - 48d) / legendColumns);
                var y = spec.Height + 10 + i / legendColumns * 22;
                if (w.Refined)
                {
                    w.Add(Key(spec, i, w.Style, x, y - 8));
                    w.Text(x + 20, y, Short(spec.Series[i].Name, 24), "font-size='11'");
                    continue;
                }
                w.Add($"<rect x='{N(x)}' y='{N(y - 8)}' width='9' height='9' rx='2' fill='{SeriesColor(spec.Series[i], i, w.Style)}'/>");
                w.Text(x + 16, y, Short(spec.Series[i].Name, 24), "font-size='11'");
            }
        w.Add("</svg>");
        return w.ToString();
    }

    /// <summary>
    /// The legend key of series <paramref name="index"/> as a standalone 14 × 9 SVG, for a legend drawn outside the chart, such
    /// as the component's, so that it matches the chart's own: shaped like the series' mark in the refined finish, and a square
    /// in the series colour in the classic one. It draws in the style the spec draws with.
    /// </summary>
    public static string LegendKey(ChartSpec spec, int index)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var style = ResolveStyle(spec);
        var key = style.Finish == ChartFinish.Refined ? Key(spec, index, style, 0, 0)
            : $"<rect x='2.5' y='0' width='9' height='9' rx='2' fill='{SeriesColor(spec.Series[index], index, style)}'/>";
        return $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 14 9' width='14' height='9' aria-hidden='true'>{key}</svg>";
    }

    /// <summary>
    /// A refined legend key in the 14 × 9 box whose top left is (<paramref name="x"/>, <paramref name="y"/>), shaped like the
    /// series' mark: a short line for a line, dashed when the whole series is projected; a dot for scatter points and bubbles;
    /// and a square for columns, bars, areas and the rest. A key whose series draws in colours other than its own is split
    /// into them, left to right: up to four of the point colours when every drawn point has one, as time-in-zone bars do, a
    /// donut's slice colours, a heatmap row's low and high colours, and the rising and falling colours of candles and OHLC bars.
    /// </summary>
    internal static string Key(ChartSpec spec, int index, ChartStyle style, double x, double y)
    {
        var series = spec.Series[index];
        var mark = Mark(spec, series);
        var drawn = series.Points.Where(p => p.Y.HasValue).ToArray();
        IReadOnlyList<string> inks = mark is ChartKind.Candlestick or ChartKind.Ohlc ? [style.Rising, style.Falling]
            : spec.Kind == ChartKind.Heatmap ? [style.HeatmapLow, style.HeatmapHigh]
            : spec.Kind == ChartKind.Donut ? series.Points.Select((p, i) => (p, i)).Where(t => t.p.Y > 0).Select(t => t.p.Color ?? style.SeriesColor(t.i)).Distinct().Take(4).ToArray()
            : drawn.Length > 0 && drawn.All(p => p.Color is not null) ? drawn.Select(p => p.Color!).Distinct().Take(4).ToArray()
            : [SeriesColor(series, index, style)];
        if (inks.Count == 0) inks = [SeriesColor(series, index, style)];
        var middle = N(y + 4.5);
        if (mark == ChartKind.Line)
        {
            if (inks.Count > 1)
                return string.Concat(inks.Select((ink, k) => $"<line x1='{N(x + 14d * k / inks.Count)}' y1='{middle}' x2='{N(x + 14d * (k + 1) / inks.Count)}' y2='{middle}' stroke='{ink}' stroke-width='2' vector-effect='non-scaling-stroke'/>"));
            var projected = series.ProjectedFrom is { } from && drawn.Length > 0 && drawn.All(p => p.X >= from);
            return $"<line x1='{N(x + 1)}' y1='{middle}' x2='{N(x + 13)}' y2='{middle}' stroke='{inks[0]}' stroke-width='2' stroke-linecap='round'{(projected ? " stroke-dasharray='1 4'" : "")} vector-effect='non-scaling-stroke'/>";
        }
        if (inks.Count > 1)
            return string.Concat(inks.Select((ink, k) => $"<rect x='{N(x + 14d * k / inks.Count)}' y='{N(y)}' width='{N(14d / inks.Count)}' height='9' fill='{ink}'/>"));
        return mark is ChartKind.Scatter or ChartKind.Bubble
            ? $"<circle cx='{N(x + 7)}' cy='{middle}' r='4.5' fill='{inks[0]}'/>"
            : $"<rect x='{N(x + 2.5)}' y='{N(y)}' width='9' height='9' rx='2' fill='{inks[0]}'/>";
    }

    internal static void Begin(SvgWriter w, int width, int height, string title, string description)
    {
        var style = w.Style;
        w.Add($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}' class='lumen-svg' role='group' aria-label='{SvgWriter.E(string.IsNullOrWhiteSpace(description) ? title : $"{title}. {description}")}' style='--lumen-grid:{style.Grid};--lumen-muted:{style.Muted};width:100%;height:auto;display:block;background:{style.Background};color:{style.Text};font-family:{style.FontFamily};font-size:12px' fill='currentColor'>");
        w.Add($"<title>{SvgWriter.E(title)}</title><desc>{SvgWriter.E(description)}</desc>");
        // A refined line marker is drawn but transparent, so it is hovered and focused where a visible one would be, and
        // appears while it is.
        w.Add("<style>.lumen-svg .lumen-grid{stroke:var(--lumen-grid);stroke-width:1}"+(w.MinorGrid?".lumen-svg .lumen-grid-minor{stroke:var(--lumen-grid);stroke-width:1;stroke-opacity:.45}":"")+".lumen-svg .lumen-muted{fill:var(--lumen-muted)}.lumen-svg .lumen-datum{outline:none;cursor:pointer}.lumen-svg .lumen-datum:focus{stroke:currentColor;stroke-width:3}.lumen-svg .lumen-datum:hover{filter:brightness(.87)}"
            +(w.Refined?".lumen-svg .lumen-marker{opacity:0}.lumen-svg .lumen-datum:hover .lumen-marker,.lumen-svg .lumen-datum:focus .lumen-marker{opacity:1}":"")+".lumen-svg .lumen-node{cursor:grab;outline:none}.lumen-svg .lumen-node:focus circle{stroke-width:4}.lumen-svg .lumen-node:active{cursor:grabbing}</style>");
        w.MarkDefinitions();
        w.Text(24, 28, title, "font-size='17' font-weight='600'");
        w.Text(24, 49, description, "class='lumen-muted' font-size='11'");
    }

    /// <summary>A focusable, labelled data mark. <paramref name="attributes"/> are presentation attributes on the group, which
    /// its shapes inherit and the focus rule overrides, so a hollow marker still shows the focus ring.</summary>
    private static void Datum(SvgWriter w, int series, int index, string label, string shape, string attributes = "")
    {
        w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-series='{series}' data-point='{index}' aria-label='{SvgWriter.E(label)}'{attributes}>{(w.Titles ? $"<title>{SvgWriter.E(label)}</title>" : "")}{shape}</g>");
    }
    private static string PointLabel(ChartSeries s, ChartPoint p) => $"{s.Name}: {p.Label ?? LinearScale.Label(p.X)}, {(p.Y.HasValue ? LinearScale.Label(p.Y.Value) : "missing")}";
    private static string PointLabel(ChartSeries s, ChartPoint p, Axis x, Axis y) =>
        $"{s.Name}: {p.Label ?? x.Format(p.X)}, {(p.Y.HasValue ? y.Format(p.Y.Value) : "missing")}" +
        (s.Zones is { } zones && p.Y is { } value ? $", {zones.Zones[zones.IndexOf(value)].Name}" : "") +
        (s.ProjectedFrom is { } from && p.X >= from ? ", projected" : "") +
        (p.Low.HasValue && p.High.HasValue ? $" (band {y.Format(p.Low.Value)} to {y.Format(p.High.Value)})" : "");
    private static string ZoneColor(ChartStyle style, ZoneScale zones, int index) => zones.Zones[index].Color ?? style.Zones[index];
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
        // A Y axis on the right takes the margin a secondary axis would, and gives the left one back.
        var flipped = s.YAxisSide == AxisSide.Right;
        var left = horizontal ? 160d : flipped ? 30d : 76d; var right = s.Width - (secondary || flipped ? 76d : 30d);
        var points = s.Series.SelectMany(x => x.Points).ToArray();
        var bubbles = s.Series.Where(x => Mark(s, x) == ChartKind.Bubble).SelectMany(x => x.Points).ToArray();
        var maxSize = bubbles.Length == 0 ? 0 : bubbles.Max(point => point.Size);
        var cats = points.Select(p => p.X).Distinct().Order().ToArray();
        var xs = Axis.Create(s.XAxis, points.Select(p => p.X), min: s.XMin, max: s.XMax, zone: TimeAxis.Zone(s.TimeZone),
            weekends: s.SkipWeekends, skips: s.TimeSkips.Count > 0 ? s.TimeSkips : null) with { ValueFormat = s.XFormat };
        var plots = Plots(s, cats, points);
        double X(double x) => category ? left + (Array.IndexOf(cats, x) + .5) / cats.Length * (right - left) : xs.Map(x, left, right);
        var (xCount, xTicks) = category ? (5, []) : Spaced(w, xs, right - left, across: true, count: s.XAxis == AxisKind.Time ? 6 : 5);
        for (var k = 0; k < plots.Length; k++)
        {
            var (pane, top, bottom, ys, ys2, paired) = plots[k];
            double Y(double y) => ys.Map(y, bottom, top);
            // A pane's value ticks are spaced to its height, or along the bottom of a horizontal bar chart to their labels.
            var (count, ticks) = Spaced(w, ys, horizontal ? right - left : bottom - top, across: horizontal);
            if (s.MinorGridlines)
            {
                foreach (var minor in ys.MinorTicks(count))
                {
                    if (horizontal) { var x = ys.Map(minor, left, right); Gridline(w, x, top, x, bottom, minor: true); }
                    else { var y = Y(minor); Gridline(w, left, y, right, y, minor: true); }
                }
                if (!category && !horizontal)
                    foreach (var minor in xs.MinorTicks(xCount))
                    {
                        var x = X(minor);
                        Gridline(w, x, top, x, bottom, minor: true);
                    }
            }
            for (var i = 0; i < ticks.Count; i++)
            {
                if (horizontal)
                {
                    var x = ys.Map(ticks[i].Value, left, right); Gridline(w, x, top, x, bottom);
                    if (Labelled(s, ticks, i)) w.Text(x, bottom + 20, ticks[i].Label, "text-anchor='middle' class='lumen-muted'");
                }
                else
                {
                    var y = Y(ticks[i].Value); Gridline(w, left, y, right, y);
                    YTick(w, s, ticks, i, y, left, right);
                }
            }
            // Ticks on the right, but no second set of gridlines: one grid is what a reader can follow.
            if (paired)
                foreach (var (tick, label) in Spaced(w, ys2, bottom - top).Ticks)
                    w.Text(right + 12, ys2.Map(tick, bottom, top) + 4, label, "text-anchor='start' class='lumen-muted'");
            // The panes share the X axis, so it is labelled once, under the bottom one. Along the bottom a refined chart
            // labels every second, third or later category or point while two labels would touch.
            if (k == plots.Length - 1)
            {
                if (category)
                {
                    var step = Math.Max(1, (int)Math.Ceiling(cats.Length / (horizontal ? (bottom - top) / 24 : (right - left) / 65)));
                    string Name(int i) => points.First(p => p.X == cats[i]).Label ?? LinearScale.Label(cats[i]);
                    if (w.Refined && !horizontal)
                        while (step < cats.Length && !Apart(Enumerable.Range(0, cats.Length).Where(i => i % step == 0).Select(i => (X(cats[i]), Short(Name(i), 12))))) step++;
                    for (var i = 0; i < cats.Length; i += step)
                    {
                        var label = Name(i);
                        if (horizontal) w.Text(left - 12, top + (i + .5) / cats.Length * (bottom - top) + 4, Short(label, 21), "text-anchor='end' class='lumen-muted'");
                        else w.Text(X(cats[i]), bottom + 21, Short(label, 12), "text-anchor='middle' class='lumen-muted'");
                    }
                }
                else
                {
                    var labels = points.Where(p => p.Label is not null && p.X >= xs.Min && p.X <= xs.Max).DistinctBy(p => p.X).OrderBy(p => p.X).ToArray();
                    if (labels.Length is > 0 and <= 24)
                    {
                        var step = Math.Max(1, (int)Math.Ceiling(labels.Length / 7d));
                        if (w.Refined)
                            while (step < labels.Length && !Apart(labels.Where((_, i) => i % step == 0).Select(p => (X(p.X), Short(p.Label!, 12))))) step++;
                        for (var i = 0; i < labels.Length; i += step)
                            w.Text(X(labels[i].X), bottom + 21, Short(labels[i].Label!, 12), "text-anchor='middle' class='lumen-muted'");
                    }
                    else foreach (var (tick, label) in xTicks) w.Text(X(tick), bottom + 21, label, "text-anchor='middle' class='lumen-muted'");
                }
                w.Text((left + right) / 2, bottom + 44, horizontal ? s.YLabel : s.XLabel, "text-anchor='middle' class='lumen-muted'");
            }
            YTitle(w, s, horizontal ? s.XLabel : pane.Label, top, bottom);
            if (paired)
                w.Text(s.Width - 16, (top + bottom) / 2, pane.Y2Label, $"text-anchor='middle' transform='rotate(90 {N(s.Width - 16)} {N((top + bottom) / 2)})' class='lumen-muted'");
        }
        var positive = cats.ToDictionary(x => x, _ => 0d); var negative = cats.ToDictionary(x => x, _ => 0d);
        // A category chart places categories by index, so a projection starting between two is interpolated between them.
        double Projected(double from)
        {
            if (!category) return X(from);
            var after = Array.FindIndex(cats, c => c >= from);
            if (after < 0) return double.PositiveInfinity;
            if (cats[after] == from) return X(from);
            if (after == 0) return double.NegativeInfinity;
            return X(cats[after - 1]) + (from - cats[after - 1]) / (cats[after] - cats[after - 1]) * (X(cats[after]) - X(cats[after - 1]));
        }
        // Nested SVG provides a local clipping viewport without global clip-path IDs, one for each pane. It is inset by
        // one marker radius so a mark on the first or last value is drawn whole and stays hoverable, or by the ring of a
        // highlighted last point where there is one.
        for (var k = 0; k < plots.Length; k++)
        {
            var (pane, top, bottom, ys, ys2, _) = plots[k];
            double Y(double y) => ys.Map(y, bottom, top);
            var bleed = s.Series.Any(series => series.Pane == k && series.HighlightLast) ? 12d : 6d;
            // Value labels are drawn over the clip, so the label of the tallest column can rise into the margin above the
            // plot; a label whose column the plot does not show is left out with it.
            var named = new StringBuilder();
            void Name(double x, double y, string text, string anchor) =>
                named.Append($"<text x='{N(x)}' y='{N(y)}' text-anchor='{anchor}' font-size='11' aria-hidden='true'>{SvgWriter.E(text)}</text>");
            // A column's label fits across the column, so it never runs into its neighbours'.
            void Above(double x, double width, double far, bool up, string text)
            {
                if (Wide(text) > width || x + width / 2 < left || x + width / 2 > right || far < top - .5 || far > bottom + .5) return;
                Name(x + width / 2, up ? far - 5 : far + 12, text, "middle");
            }
            void Beside(double far, double y, double height, bool up, string text)
            {
                if (Wide(text) > (up ? right - far : far - left) - 6) return;
                Name(up ? far + 6 : far - 6, y + height / 2 + 4, text, up ? "start" : "end");
            }
            w.Add($"<svg x='{N(left-bleed)}' y='{N(top-bleed)}' width='{N(right-left+2*bleed)}' height='{N(bottom-top+2*bleed)}' viewBox='{N(left-bleed)} {N(top-bleed)} {N(right-left+2*bleed)} {N(bottom-top+2*bleed)}' overflow='hidden'>");
            // Behind the data, and inside the clip, so a reference pans and zooms with what it refers to. A refined chart sets
            // their labels clear of each other and writes them over the data, where a halo keeps them legible.
            Func<double, double> value = horizontal ? v => ys.Map(v, left, right) : Y;
            var references = pane.YZones is { } bands ? ZoneBands(w, bands, value, ys, horizontal, left, right, top, bottom) : [];
            // A horizontal bar chart measures along X, so there a value reference stands upright, as the zone bands do. A Y
            // reference belongs to the main plot; an X one runs through every pane and is named once, in the main plot.
            foreach (var annotation in s.Annotations)
                if (horizontal) references.Add(Measure(w, annotation with { Axis = AnnotationAxis.X }, value, value, ys, ys, left, right, top, bottom, inside: true));
                else if (k == 0) references.Add(Measure(w, annotation, X, Y, xs, ys, left, right, top, bottom));
                else if (annotation.Axis == AnnotationAxis.X) references.Add(Measure(w, annotation, X, Y, xs, ys, left, right, top, bottom, named: false));
            if (w.Refined) Place(references, left, right, top, bottom);
            foreach (var reference in references) Draw(w, reference);
            // Column series share each slot side by side. On a continuous axis a slot takes its width from the closest two X
            // values any column series in the pane has, as a candle does from its own, so no two slots overlap.
            var columns = Enumerable.Range(0, s.Series.Count).Where(i => s.Series[i].Pane == k && Mark(s, s.Series[i]) is ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn).ToArray();
            var slot = 0d;
            if (!category && columns.Length > 0)
            {
                var at = columns.SelectMany(i => s.Series[i].Points).Select(p => p.X).Distinct().Select(X).Order().ToArray();
                slot = Math.Clamp((at.Length > 1 ? Enumerable.Range(1, at.Length - 1).Min(i => at[i] - at[i - 1]) : 30) * .7, 1, 34);
            }
            // A stack's far end on each side of zero belongs to the last series stacked there.
            var outer = new Dictionary<(double X, bool Up), int>();
            if (s.Kind == ChartKind.StackedColumn)
                foreach (var i in columns)
                    foreach (var p in s.Series[i].Points)
                        if (p.Y is { } v && v != 0) outer[(p.X, v > 0)] = i;
            // Bands first, then areas, columns and candles, lines, and points last, so the broad marks stand behind the narrow
            // ones. Series keep their order within each, so a chart of one kind draws in series order.
            foreach (var si in Enumerable.Range(0, s.Series.Count).Where(i => s.Series[i].Pane == k).OrderBy(i => Layer(Mark(s, s.Series[i]))))
            {
                var series = s.Series[si]; var color = SeriesColor(series, si, w.Style); var mark = Mark(s, series); var place = Array.IndexOf(columns, si);
                // Each series is measured against its own axis from here on.
                var scale = series.Secondary ? ys2 : ys;
                double At(double y) => scale.Map(y, bottom, top);
                var paint = series.Gradient is { } stops ? $"url(#{w.Gradient(ByValue(stops, At))})" : color;
                // A point's own colour beats its zone's, which beats the series colour or gradient.
                string Ink(ChartPoint p) => p.Color ?? (series.Zones is { } zones ? ZoneColor(w.Style, zones, zones.IndexOf(p.Y!.Value)) : paint);
                if (mark == ChartKind.Scatter && s.DensityCells is { } cells) Density(w, series, color, X, At, xs, scale, cells, left, right, top, bottom);
                else if (mark == ChartKind.Candlestick) Candles(w, si, series, X, At, xs, scale);
                else if (mark == ChartKind.Ohlc) Ohlc(w, si, series, X, At, xs, scale);
                else if (mark is ChartKind.Line or ChartKind.Area or ChartKind.Band)
                {
                    if (mark == ChartKind.Band) Bands(w, series, color, X, At, s.MaxRenderedPoints);
                    var projected = series.ProjectedFrom is { } from ? Projected(from) : double.PositiveInfinity;
                    var width = series.StrokeWidth ?? (w.Refined ? 1.6 : 2.5);
                    var fill = mark == ChartKind.Area && series.Fill == AreaFill.Fade ? $"fill='url(#{w.Gradient(Fade(color, At(0), top, bottom))})'" : $"fill='{color}' fill-opacity='.12'";
                    var last = series.HighlightLast ? Enumerable.Range(0, series.Points.Count).LastOrDefault(i => series.Points[i].Y.HasValue, -1) : -1;
                    // Sample each continuous run independently, preserving missing-observation gaps.
                    var start = 0;
                    while (start < series.Points.Count)
                    {
                        if (!series.Points[start].Y.HasValue) { start++; continue; }
                        var end = start; while (end < series.Points.Count && series.Points[end].Y.HasValue) end++;
                        var run = series.Points.Skip(start).Take(end - start).ToArray();
                        var indices = Sampling.MinMax(run, s.MaxRenderedPoints);
                        var (path, line) = Trace(series.Curve, run, indices, X, At, y => scale.Invert(y, bottom, top));
                        if (mark == ChartKind.Area)
                            w.Add($"<path d='{path} L{N(X(run[^1].X))},{N(At(0))} L{N(X(run[0].X))},{N(At(0))} Z' {fill}/>");
                        if (series.Zones is null && series.ProjectedFrom is null && indices.All(i => run[i].Color is null))
                            w.Add($"<path d='{path}' fill='none' stroke='{paint}' stroke-width='{N(width)}' stroke-linejoin='round'{Rounded(w)}/>");
                        else Stroke(w, series.Zones, line, paint, At, projected, width);
                        foreach (var i in indices)
                        {
                            var p = run[i];
                            string cx = N(X(p.X)), cy = N(At(p.Y!.Value)), ink = Ink(p), r = indices.Count > 80 ? "2" : "4";
                            // A hidden marker keeps an invisible target, so the point can still be focused, hovered and announced.
                            // A refined chart's own markers are hidden the same way until the point is hovered or focused, except
                            // a point between two gaps, which has no line to show it.
                            var (shape, attributes) = start + i == last
                                ? ($"<circle cx='{cx}' cy='{cy}' r='10' fill='{ink}' fill-opacity='.2'/><circle cx='{cx}' cy='{cy}' r='5.5' fill='{ink}' stroke='{w.Style.Background}' stroke-width='2'{w.Fixed}/>", "")
                                : series.Markers switch
                                {
                                    MarkerStyle.None => ($"<circle cx='{cx}' cy='{cy}' r='{r}' fill='{ink}' fill-opacity='0'/>", ""),
                                    MarkerStyle.Hollow => ($"<circle cx='{cx}' cy='{cy}' r='{r}' fill='{w.Style.Background}'{w.Fixed}/>", $" stroke='{ink}' stroke-width='2'"),
                                    MarkerStyle.Auto when w.Refined && run.Length > 1 => ($"<circle class='lumen-marker' cx='{cx}' cy='{cy}' r='{r}' fill='{ink}'/>", ""),
                                    _ => ($"<circle cx='{cx}' cy='{cy}' r='{r}' fill='{ink}'/>", "")
                                };
                            Datum(w, si, start + i, PointLabel(series,p,xs,scale), shape, attributes);
                        }
                        start = end;
                    }
                }
                else for (var pi = 0; pi < series.Points.Count; pi++)
                {
                    var p = series.Points[pi]; if (!p.Y.HasValue) continue;
                    var y = p.Y.Value;
                    if (category && place >= 0)
                    {
                        var ci = Array.IndexOf(cats, p.X);
                        var band = (horizontal ? bottom - top : right - left) / cats.Length;
                        var stacked = s.Kind == ChartKind.StackedColumn;
                        var width = band * .72 / (stacked ? 1 : columns.Length);
                        var basis = 0d;
                        if (stacked) { var dict = y >= 0 ? positive : negative; basis = dict[p.X]; dict[p.X] += y; }
                        double rx, ry, rw, rh;
                        if (horizontal)
                        {
                            rx = ys.Map(Math.Min(0, y), left, right); ry = top + ci * band + band * .14 + place * width;
                            rw = Math.Abs(ys.Map(y, left, right) - ys.Map(0, left, right)); rh = width;
                        }
                        else
                        {
                            rx = left + ci * band + band * .14 + (stacked ? 0 : place * width); ry = Math.Min(At(basis), At(basis + y));
                            rw = width; rh = Math.Abs(At(basis + y) - At(basis));
                        }
                        var end = horizontal ? y >= 0 ? End.Right : End.Left : y >= 0 ? End.Top : End.Bottom;
                        var outermost = !stacked || outer.TryGetValue((p.X, y > 0), out var last) && last == si;
                        Datum(w, si, pi, PointLabel(series,p,xs,scale), Bar(w, rx, ry, rw, rh, end, Ink(p), series.Fill, outermost));
                        if (series.ValueLabels)
                        {
                            if (horizontal) Beside(y >= 0 ? rx + rw : rx, ry, rh, y >= 0, scale.Format(y));
                            else Above(rx, rw, At(y), y >= 0, scale.Format(y));
                        }
                    }
                    else if (place >= 0)
                    {
                        // Centred on its X within the slot, rising from zero on the series' own axis.
                        var width = slot / columns.Length;
                        var x = X(p.X) - slot / 2 + place * width;
                        Datum(w, si, pi, PointLabel(series,p,xs,scale), Bar(w, x, Math.Min(At(0), At(y)), width, Math.Abs(At(y) - At(0)), y >= 0 ? End.Top : End.Bottom, Ink(p), series.Fill));
                        if (series.ValueLabels) Above(x, width, At(y), y >= 0, scale.Format(y));
                    }
                    else
                    {
                        var radius = mark == ChartKind.Bubble ? Math.Sqrt(p.Size / Math.Max(maxSize, double.Epsilon)) * 22 : 4;
                        var ink = Ink(p);
                        string cx = N(X(p.X)), cy = N(At(y));
                        var (shape, attributes) = mark != ChartKind.Scatter ? ($"<circle cx='{cx}' cy='{cy}' r='{N(radius)}' fill='{ink}' fill-opacity='.7' stroke='{ink}'{w.Fixed}/>", "") : series.Markers switch
                        {
                            MarkerStyle.Filled => ($"<circle cx='{cx}' cy='{cy}' r='{N(radius)}' fill='{ink}'/>", ""),
                            MarkerStyle.Hollow => ($"<circle cx='{cx}' cy='{cy}' r='{N(radius)}' fill='{w.Style.Background}'{w.Fixed}/>", $" stroke='{ink}' stroke-width='2'"),
                            _ => ($"<circle cx='{cx}' cy='{cy}' r='{N(radius)}' fill='{ink}' fill-opacity='.7' stroke='{ink}'{w.Fixed}/>", "")
                        };
                        Datum(w, si, pi, PointLabel(series,p,xs,scale), shape, attributes);
                    }
                }
                if (series.Trend) Trend(w, series, color, X, At, left, right, scale.Reversed);
            }
            if (w.Refined) foreach (var reference in references) Label(w, reference);
            w.Add("</svg>");
            w.Add(named.ToString());
        }
    }

    /// <summary>A main Y axis tick label, on the side the spec puts the axis, unless the spec labels only the ends.</summary>
    private static void YTick(SvgWriter w, ChartSpec s, IReadOnlyList<(double Value, string Label)> ticks, int i, double y, double left, double right)
    {
        if (!Labelled(s, ticks, i)) return;
        if (s.YAxisSide == AxisSide.Right) w.Text(right + 12, y + 4, ticks[i].Label, "text-anchor='start' class='lumen-muted'");
        else w.Text(left - 12, y + 4, ticks[i].Label, "text-anchor='end' class='lumen-muted'");
    }
    private static bool Labelled(ChartSpec s, IReadOnlyList<(double Value, string Label)> ticks, int i) =>
        s.YTickLabels == TickLabels.All || i == 0 || i == ticks.Count - 1;
    /// <summary>A main Y axis title, read upwards on the left or downwards on the right.</summary>
    private static void YTitle(SvgWriter w, ChartSpec s, string label, double top, double bottom)
    {
        var x = s.YAxisSide == AxisSide.Right ? s.Width - 16 : 20;
        w.Text(x, (top + bottom) / 2, label, $"text-anchor='middle' transform='rotate({(s.YAxisSide == AxisSide.Right ? "90" : "-90")} {N(x)} {N((top + bottom) / 2)})' class='lumen-muted'");
    }
    /// <summary>A gridline of an X and Y axis in the style's stroke; a hidden grid draws none. A refined one is a hairline that
    /// keeps its width at any display size and lands on whole pixels.</summary>
    private static void Gridline(SvgWriter w, double x1, double y1, double x2, double y2, bool minor = false)
    {
        if (w.Style.Gridlines == GridLine.Hidden) return;
        w.Line(x1, y1, x2, y2, (minor ? "class='lumen-grid-minor'" : "class='lumen-grid'") + w.Style.Gridlines switch
        {
            GridLine.Dotted => " stroke-dasharray='1 3'",
            GridLine.Dashed => " stroke-dasharray='4 4'",
            _ => ""
        } + (w.Refined ? $"{w.Fixed} shape-rendering='crispEdges'" : ""));
    }

    /// <summary>Round ends and joins on a refined data stroke, which keeps its width at any display size.</summary>
    private static string Rounded(SvgWriter w) => w.Refined ? $" stroke-linecap='round'{w.Fixed}" : "";

    /// <summary>
    /// The ticks an axis draws along <paramref name="length"/> pixels, and the count asked of it for them. The classic finish
    /// asks for <paramref name="count"/>. The refined one asks for fewer while two would stand closer than 28 pixels up the
    /// side, or, laid <paramref name="across"/> the bottom, while two labels would touch, and while fewer still leaves two.
    /// Past that, a logarithmic axis keeps its decades, and any axis then keeps each tick at least that far from the last one
    /// it kept.
    /// </summary>
    private static (int Count, IReadOnlyList<(double Value, string Label)> Ticks) Spaced(SvgWriter w, Axis axis, double length, bool across = false, int count = 5)
    {
        var ticks = axis.Ticks(count);
        if (!w.Refined) return (count, ticks);
        double Room((double, string Label) a, (double, string Label) b) => across ? (Broad(a.Label) + Broad(b.Label)) / 2 + 8 : 28;
        double Distance((double Value, string) a, (double Value, string) b) => Math.Abs(axis.Map(b.Value, 0, length) - axis.Map(a.Value, 0, length));
        bool Crowded(IReadOnlyList<(double, string)> set) => Enumerable.Range(1, Math.Max(0, set.Count - 1)).Any(i => Distance(set[i - 1], set[i]) < Room(set[i - 1], set[i]));
        // Fewer is asked for only while it still leaves two ticks to label; a short axis can step past its whole range at once.
        while (count > 2 && Crowded(ticks) && axis.Ticks(count - 1) is { Count: >= 2 } fewer) (ticks, count) = (fewer, count - 1);
        if (!Crowded(ticks)) return (count, ticks);
        if (axis.Kind == AxisKind.Log && ticks.Where(t => Math.Abs(Math.Log10(t.Value) - Math.Round(Math.Log10(t.Value))) < 1e-9).ToArray() is { Length: > 1 } decades)
            ticks = decades;
        var kept = new List<(double Value, string Label)>();
        foreach (var tick in ticks)
            if (kept.Count == 0 || Distance(kept[^1], tick) >= Room(kept[^1], tick)) kept.Add(tick);
        return (count, kept);
    }

    /// <summary>Whether labels set along the bottom, each centred on its own position, all leave each other room.</summary>
    private static bool Apart(IEnumerable<(double At, string Text)> labels)
    {
        (double At, string Text)? last = null;
        foreach (var label in labels.OrderBy(label => label.At))
        {
            if (last is { } before && label.At - before.At < (Broad(before.Text) + Broad(label.Text)) / 2 + 8) return false;
            last = label;
        }
        return true;
    }

    /// <summary>The width of a 12 px tick label, scaled from the generous estimate for 11 px text.</summary>
    private static double Broad(string text) => Wide(text) * 12 / 11;
    /// <summary>A generous width for 11 px text, so a label judged to fit does: digits and most letters at .62 em, wider
    /// than in the common sans and serif faces, punctuation narrower and the widest letters wider.</summary>
    private static double Wide(string text) =>
        text.Sum(c => c is '.' or ',' or ':' or ' ' ? .3 : c is '-' ? .36 : c is 'm' or 'M' or 'w' or 'W' ? .9 : .62) * 11;

    private enum End { Top, Bottom, Right, Left }

    /// <summary>
    /// A column or bar whose far end is <paramref name="end"/>. Without a bar radius it is a rectangle with 2 px corners all
    /// round. With one it is a path whose far end is rounded, the radius clamped to half the bar's width and to its length,
    /// and whose baseline end is square; a segment inside a stack, not <paramref name="outermost"/>, is square at both.
    /// </summary>
    private static string Bar(SvgWriter w, double x, double y, double width, double height, End end, string ink, AreaFill fill, bool outermost = true)
    {
        var paint = fill == AreaFill.Fade ? $"url(#{w.Gradient(Lighter(ink, end))})" : ink;
        if (w.Style.BarRadius is not { } radius) return $"<rect x='{N(x)}' y='{N(y)}' width='{N(width)}' height='{N(height)}' rx='2' fill='{paint}'/>";
        var upright = end is End.Top or End.Bottom;
        var r = outermost ? Math.Min(radius, Math.Min((upright ? width : height) / 2, upright ? height : width)) : 0;
        string Arc(double toX, double toY) => r > 0 ? $" A{N(r)},{N(r)} 0 0 1 {N(toX)},{N(toY)}" : "";
        double right = x + width, bottom = y + height;
        var d = end switch
        {
            End.Top => $"M{N(x)},{N(bottom)} L{N(x)},{N(y + r)}{Arc(x + r, y)} L{N(right - r)},{N(y)}{Arc(right, y + r)} L{N(right)},{N(bottom)} Z",
            End.Bottom => $"M{N(right)},{N(y)} L{N(right)},{N(bottom - r)}{Arc(right - r, bottom)} L{N(x + r)},{N(bottom)}{Arc(x, bottom - r)} L{N(x)},{N(y)} Z",
            End.Right => $"M{N(x)},{N(y)} L{N(right - r)},{N(y)}{Arc(right, y + r)} L{N(right)},{N(bottom - r)}{Arc(right - r, bottom)} L{N(x)},{N(bottom)} Z",
            _ => $"M{N(right)},{N(bottom)} L{N(x + r)},{N(bottom)}{Arc(x, bottom - r)} L{N(x)},{N(y + r)}{Arc(x + r, y)} L{N(right)},{N(y)} Z"
        };
        return $"<path d='{d}' fill='{paint}'/>";
    }

    /// <summary>A column's fade on its own box: its colour at the baseline, lighter towards the far end, up or down.</summary>
    private static string Lighter(string ink, End end)
    {
        var (from, to) = end == End.Top ? (1, 0) : (0, 1);
        return $"<linearGradient x1='0' y1='{from}' x2='0' y2='{to}'><stop offset='0' stop-color='{ink}'/><stop offset='1' stop-color='{ink}' stop-opacity='.6'/></linearGradient>";
    }

    /// <summary>An area's fade across its pane: the series colour at .35 opacity at the top of the plot and none at the
    /// baseline, and back to .35 at the bottom when the axis runs below zero, so a fill under zero fades the same way.</summary>
    private static string Fade(string color, double baseline, double top, double bottom)
    {
        var at = (baseline - top) / (bottom - top);
        string Stop(double offset, string opacity) => $"<stop offset='{N(offset)}' stop-color='{color}' stop-opacity='{opacity}'/>";
        var stops = at >= 1 ? Stop(0, ".35") + Stop(1, "0") : at <= 0 ? Stop(0, "0") + Stop(1, ".35") : Stop(0, ".35") + Stop(at, "0") + Stop(1, ".35");
        return $"<linearGradient gradientUnits='userSpaceOnUse' x1='0' y1='{N(top)}' x2='0' y2='{N(bottom)}'>{stops}</linearGradient>";
    }

    /// <summary>
    /// A gradient by value, laid out in the plot's own coordinates up the series' axis, so each stop sits at the height its
    /// value is drawn at, on a logarithmic or reversed axis too. Past the first and last stops their colours carry on.
    /// </summary>
    private static string ByValue(IReadOnlyList<ColorStop> stops, Func<double, double> at)
    {
        double from = at(stops[0].Value), to = at(stops[^1].Value);
        // Stops a rounding error apart can land on one pixel row, where only the last colour can show.
        var span = to - from;
        var body = string.Concat(stops.Select(stop => $"<stop offset='{N(span == 0 ? 1 : (at(stop.Value) - from) / span)}' stop-color='{stop.Color}'/>"));
        return $"<linearGradient gradientUnits='userSpaceOnUse' x1='0' y1='{N(from)}' x2='0' y2='{N(to)}'>{body}</linearGradient>";
    }

    private readonly record struct Vertex(double X, double Y, double Value, string? Color);

    /// <summary>
    /// The path a run's sampled points draw for <paramref name="curve"/>, and the polyline that follows it, along which
    /// zone colours and a projection are split. A step holds each value to the next point and rises or falls there, so its
    /// polyline is exact. A smooth curve is Steffen's monotone cubic through the points on screen: between two points it
    /// stays within their values, and it is level at every peak and dip, so it invents neither. Its polyline is cut every
    /// few pixels, close enough that a split at an interpolated crossing lies on the curve. Points at one X are joined
    /// straight. Every piece keeps the colour of the point its stretch starts from. A step's corner keeps that point's value
    /// too, and a cut in a smooth curve reads its value back from its height through <paramref name="value"/>, so zone
    /// colours follow the curve.
    /// </summary>
    private static (string Path, Vertex[] Line) Trace(LineCurve curve, ChartPoint[] run, IReadOnlyList<int> indices,
        Func<double, double> X, Func<double, double> Y, Func<double, double> value)
    {
        var at = indices.Select(i => new Vertex(X(run[i].X), Y(run[i].Y!.Value), run[i].Y!.Value, run[i].Color)).ToArray();
        string Polyline(IEnumerable<Vertex> line) => string.Join(" ", line.Select((v, n) => $"{(n == 0 ? "M" : "L")}{N(v.X)},{N(v.Y)}"));
        if (curve == LineCurve.Linear) return (Polyline(at), at);
        var traced = new List<Vertex> { at[0] };
        if (curve == LineCurve.Step)
        {
            for (var n = 1; n < at.Length; n++)
            {
                if (at[n].X != at[n - 1].X && at[n].Y != at[n - 1].Y) traced.Add(at[n - 1] with { X = at[n].X });
                traced.Add(at[n]);
            }
            return (Polyline(traced), traced.ToArray());
        }
        var path = new StringBuilder($"M{N(at[0].X)},{N(at[0].Y)}");
        void Cubic(int first, int last)
        {
            var count = last - first + 1;
            if (count < 2) return;
            double Width(int i) => at[first + i + 1].X - at[first + i].X;
            double Slope(int i) => (at[first + i + 1].Y - at[first + i].Y) / Width(i);
            var m = new double[count];
            for (var i = 1; i < count - 1; i++)
            {
                double before = Slope(i - 1), after = Slope(i);
                var mean = (before * Width(i) + after * Width(i - 1)) / (Width(i - 1) + Width(i));
                m[i] = (Math.Sign(before) + Math.Sign(after)) * Math.Min(Math.Min(Math.Abs(before), Math.Abs(after)), .5 * Math.Abs(mean));
            }
            m[0] = count > 2 ? (3 * Slope(0) - m[1]) / 2 : Slope(0);
            m[^1] = count > 2 ? (3 * Slope(count - 2) - m[^2]) / 2 : Slope(count - 2);
            for (var i = 0; i < count - 1; i++)
            {
                Vertex a = at[first + i], b = at[first + i + 1];
                var third = (b.X - a.X) / 3;
                double x1 = a.X + third, y1 = a.Y + m[i] * third, x2 = b.X - third, y2 = b.Y - m[i + 1] * third;
                path.Append($" C{N(x1)},{N(y1)} {N(x2)},{N(y2)} {N(b.X)},{N(b.Y)}");
                // The control polygon is at least as long as the curve, so cutting it every 3 px bounds each chord.
                var steps = Math.Clamp((int)Math.Ceiling((double.Hypot(x1 - a.X, y1 - a.Y) + double.Hypot(x2 - x1, y2 - y1) + double.Hypot(b.X - x2, b.Y - y2)) / 3), 1, 32);
                for (var k = 1; k < steps; k++)
                {
                    double t = (double)k / steps, u = 1 - t;
                    var y = u * u * u * a.Y + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * b.Y;
                    traced.Add(new(a.X + t * (b.X - a.X), y, value(y), a.Color));
                }
                traced.Add(b);
            }
        }
        var stretch = 0;
        for (var n = 1; n <= at.Length; n++)
        {
            if (n < at.Length && at[n].X > at[n - 1].X) continue;
            Cubic(stretch, n - 1);
            if (n < at.Length) { path.Append($" L{N(at[n].X)},{N(at[n].Y)}"); traced.Add(at[n]); }
            stretch = n;
        }
        return (path.ToString(), traced.ToArray());
    }

    /// <summary>
    /// The main plot and the panes under it, top to bottom, each with the Y axes its own series are measured against. The
    /// height between the title and the X axis is shared out by weight, the main plot weighing 1, after a fixed gap
    /// between each two.
    /// </summary>
    private static (ChartPane Pane, double Top, double Bottom, Axis Ys, Axis Ys2, bool Paired)[] Plots(ChartSpec s, double[] cats, ChartPoint[] points)
    {
        const double gap = 24;
        double top = 78, bottom = s.Height - 76d;
        var room = bottom - top - gap * s.Panes.Count;
        var weight = 1 + s.Panes.Sum(p => p.Weight);
        var zero = s.IncludeZero || s.Kind is ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Area;
        var plots = new (ChartPane, double, double, Axis, Axis, bool)[s.Panes.Count + 1];
        for (var k = 0; k < plots.Length; k++)
        {
            var pane = Pane(s, k);
            var mine = s.Series.Where(x => x.Pane == k).ToArray();
            var values = mine.Where(x => !x.Secondary).SelectMany(x => x.Points).Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToList();
            // Prices and band edges reach the axis of the series that carries them.
            IEnumerable<ChartPoint> Bounded(bool right) => mine.Where(x => x.Secondary == right && Mark(s, x) is ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band)
                .SelectMany(x => x.Points).Where(p => p.Low.HasValue && p.High.HasValue);
            foreach (var p in Bounded(false)) { values.Add(p.Low!.Value); values.Add(p.High!.Value); }
            if (s.Kind == ChartKind.StackedColumn)
                foreach (var x in cats)
                {
                    values.Add(points.Where(p => p.X == x && p.Y > 0).Sum(p => p.Y!.Value));
                    values.Add(points.Where(p => p.X == x && p.Y < 0).Sum(p => p.Y!.Value));
                }
            // An axis that carries columns or an area measures them from zero, whatever kind the chart is.
            bool Filled(bool right) => mine.Any(x => x.Secondary == right && Mark(s, x) is ChartKind.Column or ChartKind.Area);
            var ys = Axis.Create(pane.YAxis, values, zero || Filled(false), pane.YMin, pane.YMax) with { ValueFormat = pane.YFormat, Reversed = pane.YReversed };
            var paired = mine.Any(x => x.Secondary);
            var secondValues = mine.Where(x => x.Secondary).SelectMany(x => x.Points).Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToList();
            foreach (var p in Bounded(true)) { secondValues.Add(p.Low!.Value); secondValues.Add(p.High!.Value); }
            var ys2 = paired ? Axis.Create(pane.Y2Axis, secondValues, zero || Filled(true), pane.Y2Min, pane.Y2Max) with { ValueFormat = pane.Y2Format, Reversed = pane.Y2Reversed } : ys;
            var below = k == plots.Length - 1 ? bottom : top + room * (pane.Weight / weight);
            plots[k] = (pane, top, below, ys, ys2, paired);
            top = below + gap;
        }
        return plots;
    }

    /// <summary>
    /// A least-squares line across the plot, fitted in the space the reader sees. The axes have already
    /// taken the logarithm and left out the spans a calendar skips, so the line is straight on screen
    /// instead of curving on a log axis or jumping where a trading axis closes. Least squares is
    /// unchanged by the scaling between data and pixels, so on plain axes this is the ordinary fit.
    /// </summary>
    private static void Trend(SvgWriter w, ChartSeries series, string color, Func<double, double> X, Func<double, double> Y, double left, double right, bool reversed)
    {
        var fit = Statistics.Fit(series.Points.Where(p => p.Y.HasValue).Select(p => (X(p.X), Y(p.Y!.Value))));
        if (fit is null) return;
        // Screen y grows downwards, so a falling line is a rising series; on a reversed axis larger values sit
        // lower, so there a falling line is a falling series.
        var rising = reversed ? fit.Slope >= 0 : fit.Slope <= 0;
        var label = $"{series.Name} trend: {(rising ? "rising" : "falling")}, R squared {fit.R2.ToString("0.00", CultureInfo.InvariantCulture)}";
        // A refined trend is three quarters the width of its series' stroke, so it reads as a guide rather than as data.
        var width = w.Refined ? N(Math.Round((series.StrokeWidth ?? 1.6) * .75, 2)) : "2";
        w.Add($"<path class='lumen-trend' d='M{N(left)},{N(fit.Predict(left))} L{N(right)},{N(fit.Predict(right))}' " +
            $"fill='none' stroke='{color}' stroke-width='{width}' stroke-dasharray='7 5' stroke-opacity='.85'{w.Fixed} role='img' aria-label='{SvgWriter.E(label)}'>" +
            $"{(w.Titles ? $"<title>{SvgWriter.E(label)}</title>" : "")}</path>");
    }

    /// <summary>
    /// The stroke of one sampled run in the colours its points call for, along the polyline <see cref="Trace"/> draws for
    /// its curve. With zones, a segment that crosses a bound is split where it crosses, interpolated on screen so the split
    /// lies on the drawn segment on any axis, and each piece takes the colour of the zone it lies in; a value exactly on a
    /// bound belongs to the zone below, as <see cref="ZoneScale.IndexOf"/> has it. A segment that starts from a point with
    /// its own colour is drawn whole in that colour, and without zones the others keep the series colour. From the screen
    /// position <paramref name="projected"/> onward the stroke is dashed, a piece that reaches it split there on the drawn
    /// segment. Pieces of one colour and style in a row share a path.
    /// </summary>
    private static void Stroke(SvgWriter w, ZoneScale? zones, IReadOnlyList<Vertex> line, string color, Func<double, double> Y, double projected, double width)
    {
        // A refined dash keeps its 6-pixel length and 4-pixel gap with its round ends counted in.
        var dash = w.Refined ? $"{N(Math.Max(6 - width, 0))} {N(4 + width)}" : "6 4";
        var paths = new List<(string Ink, bool Dashed, StringBuilder Path)>();
        void Add(string ink, bool dashed, double x1, double y1, double x2, double y2)
        {
            if (paths.Count == 0 || paths[^1].Ink != ink || paths[^1].Dashed != dashed) paths.Add((ink, dashed, new StringBuilder($"M{N(x1)},{N(y1)}")));
            paths[^1].Path.Append($" L{N(x2)},{N(y2)}");
        }
        void Piece(string ink, double x1, double y1, double x2, double y2)
        {
            if (x1 < projected && projected < x2)
            {
                var y = y1 + (projected - x1) / (x2 - x1) * (y2 - y1);
                Add(ink, false, x1, y1, projected, y);
                Add(ink, true, projected, y, x2, y2);
            }
            else Add(ink, x1 >= projected, x1, y1, x2, y2);
        }
        for (var n = 1; n < line.Count; n++)
        {
            Vertex a = line[n - 1], b = line[n];
            double xa = a.X, ya = a.Y, xb = b.X, yb = b.Y;
            if (a.Color is not null || zones is null) { Piece(a.Color ?? color, xa, ya, xb, yb); continue; }
            int from = zones.IndexOf(a.Value), to = zones.IndexOf(b.Value);
            // Values a rounding error apart can straddle a bound and still land on one pixel row, leaving nothing to split.
            if (ya == yb) to = from;
            double x = xa, y = ya, done = 0;
            for (var zone = from; zone != to; zone += Math.Sign(to - from))
            {
                var bound = Y(zones.Zones[to > from ? zone : zone - 1].Upper);
                var t = (bound - ya) / (yb - ya);
                var cross = xa + t * (xb - xa);
                if (t > done) Piece(ZoneColor(w.Style, zones, zone), x, y, cross, bound);
                (x, y, done) = (cross, bound, t);
            }
            if (done < 1) Piece(ZoneColor(w.Style, zones, to), x, y, xb, yb);
        }
        foreach (var (ink, dashed, path) in paths)
            w.Add($"<path d='{path}' fill='none' stroke='{ink}' stroke-width='{N(width)}' stroke-linejoin='round'{(dashed ? $" stroke-dasharray='{dash}'" : "")}{Rounded(w)}/>");
    }

    private static int Layer(ChartKind mark) => mark switch { ChartKind.Band => 0, ChartKind.Area => 1, ChartKind.Column or ChartKind.Candlestick or ChartKind.Ohlc => 2, ChartKind.Line => 3, _ => 4 };

    /// <summary>
    /// Each zone as a band on the value axis, drawn through the annotation path so it clips, pans and zooms as a Y
    /// annotation does. Every band is clamped to the axis — the open bottom zone and the unbounded top one included —
    /// so the bands never widen it and each label stays inside the plot, but the label reads the zone's own range
    /// rather than the clamp. Labels take the text colour: a zone colour only has to clear 3:1, and small text needs 4.5:1.
    /// </summary>
    private static List<Reference> ZoneBands(SvgWriter w, ZoneScale scale, Func<double, double> at, Axis ys, bool horizontal, double left, double right, double top, double bottom)
    {
        var bands = new List<Reference>();
        for (var i = 0; i < scale.Zones.Count; i++)
        {
            double? lower = i > 0 ? scale.Zones[i - 1].Upper : null, upper = i < scale.Zones.Count - 1 ? scale.Zones[i].Upper : null;
            double from = Math.Max(lower ?? ys.Min, ys.Min), to = Math.Min(upper ?? ys.Max, ys.Max);
            if (to <= from) continue;
            var reading = (lower, upper) switch
            {
                (null, null) => "every value",
                (null, { } u) => $"up to {ys.Format(u)}",
                ({ } l, null) => $"above {ys.Format(l)}",
                ({ } l, { } u) => $"{ys.Format(l)} to {ys.Format(u)}"
            };
            // A horizontal bar chart measures along X, so there the bands stand upright.
            bands.Add(Measure(w, new(horizontal ? AnnotationAxis.X : AnnotationAxis.Y, from) { To = to, Label = scale.Zones[i].Name, Color = ZoneColor(w.Style, scale, i) },
                at, at, ys, ys, left, right, top, bottom, reading, w.Style.Text));
        }
        return bands;
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
            // A cell's lower edge is its smaller value unless the axis is reversed.
            double near = ys.Invert(y + size, bottom, top), far = ys.Invert(y, bottom, top);
            var label = $"{series.Name}: {Count(count)} observation{(count == 1 ? "" : "s")}, " +
                        $"{xs.Format(xs.Invert(x, left, right))} to {xs.Format(xs.Invert(x + size, left, right))}, " +
                        $"{ys.Format(Math.Min(near, far))} to {ys.Format(Math.Max(near, far))}";
            Aggregate(w, label, $"<rect x='{N(x)}' y='{N(y)}' width='{N(size)}' height='{N(size)}' fill='{color}' fill-opacity='{N(Math.Round(weight, 3))}'/>");
        }
    }

    /// <summary>A reference line or band measured for drawing: its shape, its name, which its label shows, and where.</summary>
    private sealed class Reference
    {
        public required string Name { get; init; }
        public required string Shape { get; init; }
        public required string Ink { get; init; }
        /// <summary>False draws the shape alone, as an X reference does in the panes below the one that names it.</summary>
        public required bool Named { get; init; }
        /// <summary>Labelled along the top of the plot beside an upright reference, rather than at the right of a level one.</summary>
        public required bool Upright { get; init; }
        public required bool Band { get; init; }
        /// <summary>Its edges across the axis it marks, on screen; a line's are both its position.</summary>
        public required double Near { get; init; }
        public required double Far { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public string Anchor { get; set; } = "end";
        /// <summary>False leaves the label out, where the refined finish found no room for it.</summary>
        public bool Shown { get; set; } = true;
    }

    /// <summary>Measures one reference. <paramref name="reading"/> replaces the values it would otherwise read out, and
    /// <paramref name="ink"/> its label's colour. <paramref name="inside"/> sets an upright reference's label on the
    /// side of it with more of the plot, from the part of it the plot shows, so a label near the end of the axis stays
    /// in view; a reference wholly off the plot turns its label away, so the two clip together. A reference that is not
    /// <paramref name="named"/> draws its shape alone, as an X reference does in the panes below the one that names it.
    /// These are the classic finish's positions; the refined one sets the labels again with <see cref="Place"/>.</summary>
    private static Reference Measure(SvgWriter w, ChartAnnotation annotation, Func<double, double> X, Func<double, double> Y,
        Axis xs, Axis ys, double left, double right, double top, double bottom, string? reading = null, string? ink = null, bool inside = false, bool named = true)
    {
        var horizontal = annotation.Axis == AnnotationAxis.Y;
        var axis = horizontal ? ys : xs;
        var colour = annotation.Color ?? w.Style.Muted;
        var at = horizontal ? Y(annotation.From) : X(annotation.From);
        string shape;
        double labelX, labelY, near, far; string anchor;
        (double, double, string) Upright(double near, double far)
        {
            if (!inside || near > right) return (near + 6, top + 13, "start");
            if (far < left) return (far - 6, top + 13, "end");
            (near, far) = (Math.Max(near, left), Math.Min(far, right));
            return near + far > left + right ? (far - 6, top + 13, "end") : (near + 6, top + 13, "start");
        }
        if (annotation.To is { } to)
        {
            var other = horizontal ? Y(to) : X(to);
            double x = horizontal ? left : Math.Min(at, other), y = horizontal ? Math.Min(at, other) : top;
            double width = horizontal ? right - left : Math.Abs(other - at), height = horizontal ? Math.Abs(other - at) : bottom - top;
            shape = $"<rect x='{N(x)}' y='{N(y)}' width='{N(width)}' height='{N(height)}' fill='{colour}' fill-opacity='.12'/>";
            reading ??= $"{axis.Format(annotation.From)} to {axis.Format(to)}";
            (labelX, labelY, anchor) = horizontal ? (right - 6, y + 13, "end") : Upright(x, x + width);
            (near, far) = horizontal ? (y, y + height) : (x, x + width);
        }
        else
        {
            var dash = annotation.Dashed ? " stroke-dasharray='6 4'" : "";
            double x1 = horizontal ? left : at, y1 = horizontal ? at : top, x2 = horizontal ? right : at, y2 = horizontal ? at : bottom;
            // An invisible wider line carries the pointer, so a dashed reference is hoverable
            // along its whole length rather than only where a dash happens to fall. A refined line is thinner than the data.
            shape = $"<line x1='{N(x1)}' y1='{N(y1)}' x2='{N(x2)}' y2='{N(y2)}' stroke='{colour}' stroke-opacity='0' stroke-width='12'{w.Fixed}/>" +
                $"<line x1='{N(x1)}' y1='{N(y1)}' x2='{N(x2)}' y2='{N(y2)}' stroke='{colour}' stroke-width='{(w.Refined ? "1" : "1.5")}'{dash}{w.Fixed}/>";
            reading ??= axis.Format(annotation.From);
            (labelX, labelY, anchor) = horizontal ? (right - 6, at - 6, "end") : Upright(at, at);
            (near, far) = (at, at);
        }
        return new()
        {
            Name = annotation.Label is null ? reading : $"{annotation.Label}: {reading}", Shape = shape, Ink = ink ?? colour, Named = named,
            Upright = !horizontal, Band = annotation.To is not null, Near = near, Far = far, X = labelX, Y = labelY, Anchor = anchor
        };
    }

    /// <summary>Draws a reference behind the data. The classic finish writes its label with it; the refined one writes the label
    /// over the data afterwards, with <see cref="Label"/>, so the group carries the name and the shape alone.</summary>
    private static void Draw(SvgWriter w, Reference reference)
    {
        if (!reference.Named) { w.Add(reference.Shape); return; }
        Aggregate(w, reference.Name, reference.Shape + (w.Refined ? "" :
            $"<text x='{N(reference.X)}' y='{N(reference.Y)}' text-anchor='{reference.Anchor}' fill='{reference.Ink}' font-size='11'>{SvgWriter.E(reference.Name)}</text>"));
    }

    /// <summary>A refined reference label, over the data with a halo in the background colour so it reads across lines. The
    /// pointer passes through it to the marks beneath, and it is hidden from assistive technology, which reads the
    /// reference's own name.</summary>
    private static void Label(SvgWriter w, Reference reference)
    {
        if (!reference.Named || !reference.Shown) return;
        w.Add($"<text x='{N(reference.X)}' y='{N(reference.Y)}' text-anchor='{reference.Anchor}' fill='{reference.Ink}' font-size='11' " +
            $"stroke='{w.Style.Background}' stroke-width='3' stroke-linejoin='round' paint-order='stroke' pointer-events='none' aria-hidden='true'>{SvgWriter.E(reference.Name)}</text>");
    }

    /// <summary>
    /// Sets each refined label where it reads: inside the plot, a band's inside its band, and clear of every label set before
    /// it. A level reference is labelled at the right of the plot, a line's just above it or else just below, and a band's as
    /// high in the band as the labels already set allow. An upright one is labelled along the top, beside a line on whichever
    /// side has room or inside a band, a row lower each time a row is taken. The fewer places a label has, the sooner it is
    /// set: level lines, whose labels have two, then level bands, then the upright references, which can drop a row; each
    /// kind from the top or left of the plot. A label with no room, and the label of a band too thin or too narrow for its
    /// text, is left out; its reference keeps its name for the tooltip and assistive technology.
    /// </summary>
    private static void Place(List<Reference> references, double left, double right, double top, double bottom)
    {
        // An 11 px label, its baseline 9 px below its top and 3 px above its bottom, set 4 px in from an edge and 2 px from another.
        const double ascent = 9, descent = 3, inset = 4, gap = 2, row = 14;
        var taken = new List<(double X1, double X2, double Y1, double Y2)>();
        IEnumerable<(double X1, double X2, double Y1, double Y2)> Blocking(double x1, double x2, double y1, double y2) =>
            taken.Where(t => t.X2 + gap > x1 && t.X1 < x2 + gap && t.Y2 + gap > y1 && t.Y1 < y2 + gap);
        bool Take(Reference reference, double x, double y, string anchor, double width)
        {
            var x1 = anchor == "end" ? x - width : x;
            if (Blocking(x1, x1 + width, y - ascent, y + descent).Any()) return false;
            taken.Add((x1, x1 + width, y - ascent, y + descent));
            (reference.X, reference.Y, reference.Anchor) = (x, y, anchor);
            return true;
        }
        bool Level(Reference reference, double width)
        {
            var x = right - inset - gap;
            if (x - width < left + inset) return false;
            if (!reference.Band)
                return reference.Near >= top && reference.Near <= bottom &&
                    (reference.Near - 6 - ascent >= top && Take(reference, x, reference.Near - 6, "end", width) ||
                     reference.Near + 4 + ascent + descent <= bottom && Take(reference, x, reference.Near + 4 + ascent, "end", width));
            double from = Math.Max(Math.Min(reference.Near, reference.Far), top), to = Math.Min(Math.Max(reference.Near, reference.Far), bottom);
            if (to - from < ascent + descent) return false;
            var pad = Math.Min(inset, (to - from - ascent - descent) / 2);
            for (var y = from + pad + ascent; y + descent <= to - pad + 1e-9;)
            {
                if (Take(reference, x, y, "end", width)) return true;
                y = Blocking(x - width, x, y - ascent, y + descent).Max(t => t.Y2) + gap + ascent + 1e-9;
            }
            return false;
        }
        bool Upright(Reference reference, double width)
        {
            double x; string anchor;
            if (reference.Band)
            {
                double from = Math.Max(Math.Min(reference.Near, reference.Far), left), to = Math.Min(Math.Max(reference.Near, reference.Far), right);
                if (to - from < width + 2 * (inset + gap)) return false;
                (x, anchor) = (from + inset + gap, "start");
            }
            else if (reference.Near < left || reference.Near > right) return false;
            else if (reference.Near + 6 + width <= right - gap) (x, anchor) = (reference.Near + 6, "start");
            else if (reference.Near - 6 - width >= left + gap) (x, anchor) = (reference.Near - 6, "end");
            else return false;
            for (var y = top + inset + ascent; y + descent <= bottom - inset; y += row)
                if (Take(reference, x, y, anchor, width)) return true;
            return false;
        }
        foreach (var reference in references.Where(r => r.Named).OrderBy(r => r.Upright).ThenBy(r => r.Band).ThenBy(r => Math.Min(r.Near, r.Far)))
            reference.Shown = reference.Upright ? Upright(reference, Wide(reference.Name)) : Level(reference, Wide(reference.Name));
    }

    private static void Candles(SvgWriter w, int si, ChartSeries series, Func<double, double> X, Func<double, double> Y, Axis xs, Axis ys)
    {
        var columns = series.Points.Select(p => X(p.X)).ToArray();
        var gap = columns.Length > 1 ? Enumerable.Range(1, columns.Length - 1).Min(i => columns[i] - columns[i - 1]) : 30;
        var width = Math.Clamp(gap * .68, 1, 34);
        for (var pi = 0; pi < series.Points.Count; pi++)
        {
            var p = series.Points[pi];
            double open = p.Open!.Value, high = p.High!.Value, low = p.Low!.Value, close = p.Close!.Value;
            var color = close >= open ? w.Style.Rising : w.Style.Falling;
            // The body's top edge is whichever price sits higher on screen, which on a reversed axis is the lower one.
            double body = Math.Min(Y(open), Y(close)), baseline = Math.Max(Y(open), Y(close));
            Datum(w, si, pi, $"{p.Label ?? xs.Format(p.X)}: open {ys.Format(open)}, high {ys.Format(high)}, low {ys.Format(low)}, close {ys.Format(close)}",
                $"<line x1='{N(columns[pi])}' y1='{N(Y(high))}' x2='{N(columns[pi])}' y2='{N(Y(low))}' stroke='{color}' stroke-width='1.5'{w.Fixed}/>" +
                $"<rect x='{N(columns[pi] - width / 2)}' y='{N(body)}' width='{N(width)}' height='{N(Math.Max(baseline - body, 1))}' rx='1' fill='{color}'/>");
        }
    }

    /// <summary>
    /// The American bar: one vertical line over the day's range, the open ticking out to the left and the
    /// close to the right. A tick is half the width a candle body takes, so a bar occupies the same column
    /// and the two drawings of the same prices can be compared side by side.
    /// </summary>
    private static void Ohlc(SvgWriter w, int si, ChartSeries series, Func<double, double> X, Func<double, double> Y, Axis xs, Axis ys)
    {
        var columns = series.Points.Select(p => X(p.X)).ToArray();
        var gap = columns.Length > 1 ? Enumerable.Range(1, columns.Length - 1).Min(i => columns[i] - columns[i - 1]) : 30;
        var tick = Math.Clamp(gap * .34, .5, 17);
        for (var pi = 0; pi < series.Points.Count; pi++)
        {
            var p = series.Points[pi];
            double open = p.Open!.Value, high = p.High!.Value, low = p.Low!.Value, close = p.Close!.Value;
            var color = close >= open ? w.Style.Rising : w.Style.Falling;
            Datum(w, si, pi, $"{p.Label ?? xs.Format(p.X)}: open {ys.Format(open)}, high {ys.Format(high)}, low {ys.Format(low)}, close {ys.Format(close)}",
                $"<line x1='{N(columns[pi])}' y1='{N(Y(high))}' x2='{N(columns[pi])}' y2='{N(Y(low))}' stroke='{color}' stroke-width='1.5'{w.Fixed}/>" +
                $"<line x1='{N(columns[pi] - tick)}' y1='{N(Y(open))}' x2='{N(columns[pi])}' y2='{N(Y(open))}' stroke='{color}' stroke-width='1.5'{w.Fixed}/>" +
                $"<line x1='{N(columns[pi])}' y1='{N(Y(close))}' x2='{N(columns[pi] + tick)}' y2='{N(Y(close))}' stroke='{color}' stroke-width='1.5'{w.Fixed}/>");
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
        var (count, ticks) = Spaced(w, ys, bottom - top);
        if (s.MinorGridlines)
            foreach (var minor in ys.MinorTicks(count))
            {
                var y = ys.Map(minor, bottom, top);
                Gridline(w, left, y, right, y, minor: true);
            }
        for (var i = 0; i < ticks.Count; i++)
        {
            var y = ys.Map(ticks[i].Value, bottom, top);
            Gridline(w, left, y, right, y);
            YTick(w, s, ticks, i, y, left, right);
        }
        w.Text((left + right) / 2, bottom + 44, s.XLabel, "text-anchor='middle' class='lumen-muted'");
        YTitle(w, s, s.YLabel, top, bottom);
    }

    /// <summary>The plot's edges across a chart that derives its X axis, leaving room for the Y axis on its side.</summary>
    private static (double Left, double Right) Across(ChartSpec s) => s.YAxisSide == AxisSide.Right ? (30, s.Width - 76) : (76, s.Width - 30);

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
        var (left, right) = Across(s); double top = 78, bottom = s.Height - 76;
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
        // Edges stand at least 70 pixels apart, which no label of a bin edge outgrows, so they are thinned in neither finish.
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
        var (left, right) = Across(s); double top = 78, bottom = s.Height - 76;
        // A supplied summary has no observations behind it, so its whiskers and outliers are what the axis must reach.
        var supplied = s.Series.Select(series => series.Summary).OfType<BoxSummary>()
            .SelectMany(summary => summary.Outliers.Append(summary.LowerWhisker).Append(summary.UpperWhisker));
        var ys = Axis.Create(s.YAxis, observations.SelectMany(v => v).Concat(supplied), s.IncludeZero, s.YMin, s.YMax) with { ValueFormat = s.YFormat, Reversed = s.YReversed };
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
                $"<line x1='{N(center)}' y1='{N(upper)}' x2='{N(center)}' y2='{N(lower)}' stroke='{color}' stroke-width='1.5'{w.Fixed}/>" +
                $"<line x1='{N(center - width / 4)}' y1='{N(upper)}' x2='{N(center + width / 4)}' y2='{N(upper)}' stroke='{color}' stroke-width='1.5'{w.Fixed}/>" +
                $"<line x1='{N(center - width / 4)}' y1='{N(lower)}' x2='{N(center + width / 4)}' y2='{N(lower)}' stroke='{color}' stroke-width='1.5'{w.Fixed}/>" +
                $"<rect x='{N(center - width / 2)}' y='{N(Math.Min(q1, q3))}' width='{N(width)}' height='{N(Math.Max(Math.Abs(q1 - q3), 1))}' rx='2' fill='{color}' fill-opacity='.18' stroke='{color}' stroke-width='1.5'{w.Fixed}/>" +
                $"<line x1='{N(center - width / 2)}' y1='{N(median)}' x2='{N(center + width / 2)}' y2='{N(median)}' stroke='{color}' stroke-width='2.5'{w.Fixed}/>");
            string Mark(double value) => $"<circle cx='{N(center)}' cy='{N(ys.Map(value, bottom, top))}' r='3.5' fill='none' stroke='{color}' stroke-width='1.5'{w.Fixed}/>";
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
        var (left, right) = Across(s); double top = 78, bottom = s.Height - 76;
        var ys = Axis.Create(s.YAxis, observations.SelectMany(v => v), s.IncludeZero, s.YMin, s.YMax) with { ValueFormat = s.YFormat, Reversed = s.YReversed };
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
                shape = $"<path d='M{Side(estimate, 1)} {Side(estimate.Reverse(), -1)} Z' fill='{color}' fill-opacity='.22' stroke='{color}' stroke-width='1.5' stroke-linejoin='round'{w.Fixed}/>";
            }
            double q1 = ys.Map(summary.Q1, bottom, top), q3 = ys.Map(summary.Q3, bottom, top);
            var median = ys.Map(summary.Median, bottom, top);
            Aggregate(w, $"{s.Series[si].Name}: {Count(observations[si].Length)} observations, median {ys.Format(summary.Median)}, quartiles {ys.Format(summary.Q1)} to {ys.Format(summary.Q3)}, range {ys.Format(observations[si].Min())} to {ys.Format(observations[si].Max())}",
                shape +
                $"<rect x='{N(center - 4)}' y='{N(Math.Min(q1, q3))}' width='8' height='{N(Math.Max(Math.Abs(q1 - q3), 1))}' rx='2' fill='{color}' fill-opacity='.85'/>" +
                $"<line x1='{N(center - width / 2)}' y1='{N(median)}' x2='{N(center + width / 2)}' y2='{N(median)}' stroke='{color}' stroke-width='2.5'{w.Fixed}/>");
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
            var color = p.Color ?? w.Style.SeriesColor(i);
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
                Datum(w,si,pi,PointLabel(s.Series[si],p),$"<rect x='{N(x+1)}' y='{N(80+si*ch+1)}' width='{N(Math.Max(0,cw-2))}' height='{N(Math.Max(0,ch-2))}' rx='3' fill='{color}' stroke='var(--lumen-muted)' stroke-opacity='.4'{w.Fixed}/>");
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
            w.Add($"<polygon points='{string.Join(" ",Enumerable.Range(0,cats.Length).Select(i=> {var p=At(i,max*ring/4);return $"{N(p.X)},{N(p.Y)}";}))}' fill='none' class='lumen-grid'{w.Fixed}/>");
        for(var i=0;i<cats.Length;i++)
        {
            var p=At(i,max); w.Line(cx,cy,p.X,p.Y,"class='lumen-grid'"+w.Fixed); var label=At(i,max*1.14);
            w.Text(label.X,label.Y+4,Short(s.Series.SelectMany(x=>x.Points).First(p=>p.X==cats[i]).Label ?? LinearScale.Label(cats[i]),15),"text-anchor='middle' class='lumen-muted'");
        }
        for(var si=0;si<s.Series.Count;si++)
        {
            var series=s.Series[si];
            if(cats.Any(x=>!series.Points.Any(p=>p.X==x&&p.Y.HasValue))) throw new ArgumentException("Radar series must contain every category without missing values.");
            var color=SeriesColor(series,si,w.Style);
            w.Add($"<polygon points='{string.Join(" ",cats.Select((x,i)=> {var p=At(i,series.Points.First(p=>p.X==x).Y!.Value);return $"{N(p.X)},{N(p.Y)}";}))}' fill='{color}' fill-opacity='.1' stroke='{color}' stroke-width='2'{w.Fixed}/>");
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

