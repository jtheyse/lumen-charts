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
    /// <summary>A sparkline's data alone: no words are written, and references draw their shapes without their labels.</summary>
    public bool Bare { get; init; }
    /// <summary>How far a chart's body moves down because its description takes a second line, 14 pixels or none.</summary>
    public int Head { get; set; }
    /// <summary>How far a chart's body moves up from its foot because its source takes a second line, 14 pixels or none.</summary>
    public int Foot { get; set; }
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

/// <summary>Draws a <see cref="ChartSpec"/> as a self-contained, accessible SVG document, anywhere .NET runs: no browser, no
/// fonts and no other package needed.</summary>
public static class ChartSvg
{
    /// <summary>Every entry keeps at least a 3:1 contrast against both the light and the dark chart background.</summary>
    public static readonly IReadOnlyList<string> Palette = ChartStyle.Light.Series;
    /// <summary>Candlestick bodies and OHLC bars are colored by direction rather than by series.</summary>
    public const string RisingColor = "#169B8D", FallingColor = "#D36B84";
    /// <summary>The colour series <paramref name="index"/> draws in on the light and dark presets: its own, or the palette's.</summary>
    public static string SeriesColor(ChartSeries series, int index) => series.Color ?? Palette[index % Palette.Count];
    /// <summary>The colour series <paramref name="index"/> draws in with <paramref name="style"/>: its own, or the style's.</summary>
    public static string SeriesColor(ChartSeries series, int index, ChartStyle style) => series.Color ?? style.SeriesColor(index);
    /// <summary>The style a spec draws with: its own, or the preset for its theme.</summary>
    public static ChartStyle ResolveStyle(ChartSpec spec) => spec.Style ?? Preset(spec.Theme);
    internal static ChartStyle Preset(ChartTheme theme) => theme == ChartTheme.Dark ? ChartStyle.Dark : ChartStyle.Light;
    /// <summary>The mark a series draws: its own kind, or the chart's.</summary>
    internal static ChartKind Mark(ChartSpec spec, ChartSeries series) => series.Kind ?? spec.Kind;
    /// <summary>What pane <paramref name="index"/> draws with: the spec's own Y properties for the main plot, and
    /// <c>Panes[index - 1]</c> below it.</summary>
    internal static ChartPane Pane(ChartSpec spec, int index) => index == 0
        ? new() { Label = spec.YLabel, Weight = 1, YAxis = spec.YAxis, YMin = spec.YMin, YMax = spec.YMax, YMinSpan = spec.YMinSpan, YFormat = spec.YFormat, YReversed = spec.YReversed, YZones = spec.YZones,
            Y2Label = spec.Y2Label, Y2Axis = spec.Y2Axis, Y2Min = spec.Y2Min, Y2Max = spec.Y2Max, Y2Format = spec.Y2Format, Y2Reversed = spec.Y2Reversed }
        : spec.Panes[index - 1];

    // Fixed here rather than taken from a host, so the same spec always hashes to the same IDs. Every property exists on
    // every record, so leaving out the nulls loses nothing, and it halves the text a long series makes.
    private static readonly JsonSerializerOptions Hashing = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Unfinished, Unswept, Unconnected, Uncalendared, Untrended, Unchanged, Unsparked, Unmarked } }, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    /// <summary>A classic style is serialized for hashing as 0.23.0 serialized it, without its finish.</summary>
    private static void Unfinished(JsonTypeInfo info)
    {
        if (info.Type != typeof(ChartStyle)) return;
        foreach (var property in info.Properties)
            if (property.Name == nameof(ChartStyle.Finish)) property.ShouldSerialize = (_, finish) => finish is not ChartFinish.Classic;
    }
    /// <summary>A spec that leaves a gauge's sweep at its default is serialized for hashing as 0.25.0, which had no sweep,
    /// serialized it, so every chart drawn before gauges keeps its IDs.</summary>
    private static void Unswept(JsonTypeInfo info)
    {
        if (info.Type != typeof(ChartSpec)) return;
        foreach (var property in info.Properties)
            if (property.Name == nameof(ChartSpec.GaugeSweep)) property.ShouldSerialize = (_, sweep) => sweep is not 270d;
    }
    /// <summary>A spec that leaves a timeline's connectors on, the default, is serialized for hashing as 0.26.0, which had no
    /// timelines, serialized it, so every chart drawn before them keeps its IDs.</summary>
    private static void Unconnected(JsonTypeInfo info)
    {
        if (info.Type != typeof(ChartSpec)) return;
        foreach (var property in info.Properties)
            if (property.Name == nameof(ChartSpec.TimelineConnectors)) property.ShouldSerialize = (_, connected) => connected is false;
    }
    /// <summary>A spec that leaves a calendar's layout, cell and week start at their defaults is serialized for hashing as 0.27.0,
    /// which had no calendars, serialized it, so every chart drawn before them keeps its IDs.</summary>
    private static void Uncalendared(JsonTypeInfo info)
    {
        if (info.Type != typeof(ChartSpec)) return;
        foreach (var property in info.Properties)
            if (property.Name == nameof(ChartSpec.CalendarLayout)) property.ShouldSerialize = (_, layout) => layout is not CalendarLayout.Weeks;
            else if (property.Name == nameof(ChartSpec.CalendarCell)) property.ShouldSerialize = (_, cell) => cell is not CalendarCell.Square;
            else if (property.Name == nameof(ChartSpec.WeekStart)) property.ShouldSerialize = (_, start) => start is not DayOfWeek.Monday;
    }
    /// <summary>A series that leaves its trend's fit, window and degree at their defaults is serialized for hashing as 0.31.0, which
    /// drew only the line, serialized it, so every chart drawn before them, a trend line included, keeps its IDs.</summary>
    private static void Untrended(JsonTypeInfo info)
    {
        if (info.Type != typeof(ChartSeries)) return;
        foreach (var property in info.Properties)
            if (property.Name == nameof(ChartSeries.TrendFit)) property.ShouldSerialize = (_, fit) => fit is not TrendFit.Linear;
            else if (property.Name == nameof(ChartSeries.TrendPoints)) property.ShouldSerialize = (_, points) => points is not 7;
            else if (property.Name == nameof(ChartSeries.TrendDegree)) property.ShouldSerialize = (_, degree) => degree is not 2;
    }
    /// <summary>A series that leaves its change colours off, and a chart that leaves its X ticks to the rule it always had, are
    /// serialized for hashing as 0.32.0, which had neither, serialized them, so every chart drawn before them keeps its IDs. A point's
    /// value note is null unless set, and nulls are left out already.</summary>
    private static void Unchanged(JsonTypeInfo info)
    {
        if (info.Type == typeof(ChartSeries))
            foreach (var property in info.Properties)
                if (property.Name == nameof(ChartSeries.ChangeColors)) property.ShouldSerialize = (_, change) => change is not ChangeColors.None;
        if (info.Type == typeof(ChartSpec))
            foreach (var property in info.Properties)
                if (property.Name == nameof(ChartSpec.XTicks)) property.ShouldSerialize = (_, ticks) => ticks is not TickSource.Auto;
    }
    /// <summary>A chart that is not a sparkline is serialized for hashing as 0.33.0, which had none, serialized it, so every chart drawn
    /// before them keeps its IDs. A minimum span and a point's highlight are null unless set, and nulls are left out already.</summary>
    private static void Unsparked(JsonTypeInfo info)
    {
        if (info.Type != typeof(ChartSpec)) return;
        foreach (var property in info.Properties)
            if (property.Name == nameof(ChartSpec.Sparkline)) property.ShouldSerialize = (_, sparkline) => sparkline is true;
    }
    /// <summary>An annotation that writes its value and stands behind the data, and a chart that labels every X tick, are serialized for
    /// hashing as 0.34.0, which had none of these settings, serialized them, so every chart drawn before them keeps its IDs.</summary>
    private static void Unmarked(JsonTypeInfo info)
    {
        if (info.Type == typeof(ChartAnnotation))
            foreach (var property in info.Properties)
                if (property.Name == nameof(ChartAnnotation.ShowValue)) property.ShouldSerialize = (_, shown) => shown is false;
                else if (property.Name == nameof(ChartAnnotation.InFront)) property.ShouldSerialize = (_, front) => front is true;
        if (info.Type == typeof(ChartSpec))
            foreach (var property in info.Properties)
                if (property.Name == nameof(ChartSpec.XTickLabels)) property.ShouldSerialize = (_, labels) => labels is not TickLabels.All;
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
        // A sparkline draws no gridlines, so it carries no rule for minor ones either.
        var bare = spec.Sparkline;
        var w = new SvgWriter { Titles = includeTitles, Style = style, MinorGrid = !bare && spec.MinorGridlines && style.Gridlines != GridLine.Hidden, Spec = spec, Bare = bare };
        // A ring's key carries its value and goal as well as its name, so its columns are wider.
        var ring = spec.Kind == ChartKind.Ring;
        var legendColumns = Math.Max(1, (spec.Width - 48) / (ring ? 220 : 180));
        // A histogram of one distribution needs no key; of several, its colours are the only way to tell them apart.
        // A gauge's one score is written in its centre, so it needs no key either, and a calendar draws its colour scale under its days.
        // A sparkline is read beside words that name what it draws.
        var legendRows = includeLegend && !bare && spec.Kind is not ChartKind.Donut and not ChartKind.Heatmap and not ChartKind.Box and not ChartKind.Violin and not ChartKind.Gauge and not ChartKind.Calendar
            && (spec.Kind != ChartKind.Histogram || spec.Series.Count > 1) ? (int)Math.Ceiling(spec.Series.Count / (double)legendColumns) : 0;
        Begin(w, spec.Width, spec.Height + legendRows * 22, spec.Title, spec.Description);
        // The source wraps as the description does, but upward from the foot, so a second line takes 14 pixels from the bottom of the
        // body, which is laid out knowing it.
        var source = bare ? [] : Wrap(spec.Source, spec.Width - 48);
        w.Foot = 14 * Math.Max(0, source.Length - 1);
        // An empty sparkline is an empty drawing: its title, which a host writes for the data it has, says what is missing.
        if (!HasData(spec))
        {
            if (!bare) w.Text(spec.Width / 2, spec.Height / 2, "No data to display", "text-anchor='middle'");
        }
        else if (spec.Kind == ChartKind.Gauge) Gauge(w, spec);
        else if (spec.Kind == ChartKind.Ring) Rings(w, spec);
        else if (spec.Kind == ChartKind.Donut) Donut(w, spec);
        else if (spec.Kind == ChartKind.Radar) Radar(w, spec);
        else if (spec.Kind == ChartKind.Heatmap) Heatmap(w, spec);
        else if (spec.Kind == ChartKind.Histogram) Histogram(w, spec);
        else if (spec.Kind == ChartKind.Violin) Violin(w, spec);
        else if (spec.Kind == ChartKind.Box) Box(w, spec);
        else if (spec.Kind == ChartKind.Timeline) Timeline(w, spec);
        else if (spec.Kind == ChartKind.Calendar) Calendar(w, spec);
        else Cartesian(w, spec);
        if (spec.Kind == ChartKind.Scatter && spec.DensityCells is not null && !bare)
            w.Text(spec.Width - 30, 64 + w.Head, $"{Count(spec.Series.Where(series => Mark(spec, series) == ChartKind.Scatter).Sum(series => series.Points.Count(p => p.Y.HasValue)))} observations aggregated into {spec.DensityCells} cells across",
                "text-anchor='end' class='lumen-muted' font-size='11'");
        for (var i = 0; i < source.Length; i++) w.Text(24, spec.Height - 12 - 14 * (source.Length - 1 - i), source[i], "class='lumen-muted' font-size='11'");
        if (legendRows > 0)
            for (var i = 0; i < spec.Series.Count; i++)
            {
                var x = 24 + i % legendColumns * ((spec.Width - 48d) / legendColumns);
                var y = spec.Height + 10 + i / legendColumns * 22;
                if (w.Refined)
                {
                    w.Add(Key(spec, i, w.Style, x, y - 8));
                    w.Text(x + 20, y, Short(LegendLabel(spec, i), ring ? 30 : 24), "font-size='11'");
                    continue;
                }
                w.Add($"<rect x='{N(x)}' y='{N(y - 8)}' width='9' height='9' rx='2' fill='{SeriesColor(spec.Series[i], i, w.Style)}'/>");
                w.Text(x + 16, y, Short(LegendLabel(spec, i), ring ? 30 : 24), "font-size='11'");
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
    /// What the legend writes for series <paramref name="index"/>: its name, and on a ring or a gauge its value too, so that a
    /// ring reads <c>Move: 540 of 600 kcal</c>, its point's label being the unit, and a gauge <c>Recovery: 72 %</c>, its
    /// <see cref="ChartSpec.YLabel"/> being the unit. Values are written in <see cref="ChartSpec.YFormat"/>. On a timeline a
    /// state adds its total time and its share of every lane's, <c>REM 1:42, 22 %</c>. The chart's own legend and the
    /// component's write the same.
    /// </summary>
    public static string LegendLabel(ChartSpec spec, int index)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var series = spec.Series[index];
        if (spec.Kind == ChartKind.Timeline) return Lane(spec, index);
        if (spec.Kind is not (ChartKind.Ring or ChartKind.Gauge) || series.Points.Count != 1 || series.Points[0].Y is not { } value) return series.Name;
        var scale = Radial(spec);
        return spec.Kind == ChartKind.Ring
            ? $"{series.Name}: {scale.Format(value)} of {scale.Format(series.Goal ?? 100)}{Unit(series.Points[0].Label)}"
            : $"{series.Name}: {scale.Format(value)}{Unit(spec.YLabel)}";
    }
    /// <summary>The scale a gauge or a ring writes its values on: a gauge's runs from YMin to YMax, 0 to 100 unless set.</summary>
    private static Axis Radial(ChartSpec s) => new(AxisKind.Linear, s.YMin ?? 0, s.YMax ?? 100) { ValueFormat = s.YFormat };
    /// <summary>A unit written after a value, with a space; none when it is blank.</summary>
    private static string Unit(string? unit) => string.IsNullOrWhiteSpace(unit) ? "" : " " + unit.Trim();

    /// <summary>
    /// A refined legend key in the 14 × 9 box whose top left is (<paramref name="x"/>, <paramref name="y"/>), shaped like the
    /// series' mark: a short line for a line, dashed when the whole series is projected; a dot for scatter points and bubbles;
    /// a rounded bar for a timeline's lane and an upright capsule for range bars; two steps, the second taller, for blocks; and a
    /// square for columns, bars, areas and the rest. A key whose series draws in colours other than its own is split
    /// into them, left to right: up to four of the point colours when every drawn point has one, as time-in-zone bars do, up to
    /// four of the colours zoned blocks draw in, a donut's slice colours, a gauge's zone or gradient colours, a heatmap row's low
    /// and high colours, a calendar's zone colours or its ramp's low and high colours, and the rising and falling colours of
    /// candles and OHLC bars.
    /// </summary>
    internal static string Key(ChartSpec spec, int index, ChartStyle style, double x, double y)
    {
        var series = spec.Series[index];
        var mark = Mark(spec, series);
        // A range bar is drawn from its bounds, with or without a typical value.
        var drawn = series.Points.Where(p => p.Y.HasValue || mark == ChartKind.Range && p.Low.HasValue).ToArray();
        IReadOnlyList<string> inks = mark is ChartKind.Candlestick or ChartKind.Ohlc ? [style.Rising, style.Falling]
            : spec.Kind == ChartKind.Heatmap ? [style.HeatmapLow, style.HeatmapHigh]
            : spec.Kind == ChartKind.Calendar ? spec.YZones is { } tiers ? tiers.Zones.Select((zone, i) => zone.Color ?? style.Zones[i]).Distinct().Take(4).ToArray() : [CalendarLow(style), style.HeatmapHigh]
            : spec.Kind == ChartKind.Donut ? series.Points.Select((p, i) => (p, i)).Where(t => t.p.Y > 0).Select(t => t.p.Color ?? style.SeriesColor(t.i)).Distinct().Take(4).ToArray()
            : spec.Kind == ChartKind.Gauge && spec.YZones is { } zones ? zones.Zones.Select((zone, i) => zone.Color ?? style.Zones[i]).Distinct().Take(4).ToArray()
            : spec.Kind == ChartKind.Gauge && series.Gradient is { } stops ? stops.Select(stop => stop.Color).Distinct().Take(4).ToArray()
            // Blocks coloured by zone are keyed by the zone colours they draw in, a point's own colour first.
            : mark == ChartKind.Blocks && series.Zones is { } levels && drawn.Length > 0 ? drawn.Select(p => p.Color ?? ZoneColor(style, levels, levels.IndexOf(p.Y!.Value))).Distinct().Take(4).ToArray()
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
        // A timeline's span is a rounded bar along X, and a range bar a capsule standing upright.
        if (mark == ChartKind.Timeline) return $"<rect x='{N(x)}' y='{N(y + 1.5)}' width='14' height='6' rx='3' fill='{inks[0]}'/>";
        if (mark == ChartKind.Range) return $"<rect x='{N(x + 4)}' y='{N(y)}' width='6' height='9' rx='3' fill='{inks[0]}'/>";
        // Blocks are keyed by two steps side by side, the second taller, a hairline apart.
        if (mark == ChartKind.Blocks) return $"<rect x='{N(x + 1)}' y='{N(y + 4)}' width='5.5' height='5' fill='{inks[0]}'/><rect x='{N(x + 7.5)}' y='{N(y)}' width='5.5' height='9' fill='{inks[0]}'/>";
        return mark is ChartKind.Scatter or ChartKind.Bubble
            ? $"<circle cx='{N(x + 7)}' cy='{middle}' r='4.5' fill='{inks[0]}'/>"
            : $"<rect x='{N(x + 2.5)}' y='{N(y)}' width='9' height='9' rx='2' fill='{inks[0]}'/>";
    }

    /// <summary>Opens the drawing with its title and description. The title stays one line, cut at a word with <c>…</c> where it is
    /// too wide. A chart's description too wide for one line goes on over a second, as <see cref="Wrap"/> sets it, and the chart's body
    /// moves down by that line. With <paramref name="wrap"/>, a graph's description of clauses parted by <c> · </c> that is too wide,
    /// as its own may be on a phone, goes on over a second line as it always has: as many clauses as fit on the first and the rest
    /// on the second.</summary>
    internal static void Begin(SvgWriter w, int width, int height, string title, string description, bool wrap = false)
    {
        var style = w.Style;
        // A chart fills the width of its box. A sparkline is shown at its own width, as a word is, and never wider than its box.
        var shown = w.Bare ? $"width:{width}px;max-width:100%" : "width:100%";
        w.Add($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}' class='lumen-svg' role='group' aria-label='{SvgWriter.E(string.IsNullOrWhiteSpace(description) ? title : $"{title}. {description}")}' style='--lumen-grid:{style.Grid};--lumen-muted:{style.Muted};{shown};height:auto;display:block;background:{style.Background};color:{style.Text};font-family:{style.FontFamily};font-size:12px' fill='currentColor'>");
        w.Add($"<title>{SvgWriter.E(title)}</title><desc>{SvgWriter.E(description)}</desc>");
        // A refined line marker is drawn but transparent, so it is hovered and focused where a visible one would be, and
        // appears while it is.
        w.Add("<style>.lumen-svg .lumen-grid{stroke:var(--lumen-grid);stroke-width:1}"+(w.MinorGrid?".lumen-svg .lumen-grid-minor{stroke:var(--lumen-grid);stroke-width:1;stroke-opacity:.45}":"")+".lumen-svg .lumen-muted{fill:var(--lumen-muted)}.lumen-svg .lumen-datum{outline:none;cursor:pointer}.lumen-svg .lumen-datum:focus{stroke:currentColor;stroke-width:3}.lumen-svg .lumen-datum:hover{filter:brightness(.87)}"
            +(w.Refined?".lumen-svg .lumen-marker{opacity:0}.lumen-svg .lumen-datum:hover .lumen-marker,.lumen-svg .lumen-datum:focus .lumen-marker{opacity:1}":"")+".lumen-svg .lumen-node{cursor:grab;outline:none}.lumen-svg .lumen-node:focus circle{stroke-width:4}.lumen-svg .lumen-node:active{cursor:grabbing}</style>");
        w.MarkDefinitions();
        // A sparkline's title and description are its accessible name, its title and its desc, and are not written.
        if (w.Bare) return;
        // The title is 17 pixels to the description's 11, so its width is the estimate for 11 px text scaled by 17 / 11.
        var room = width - 48d;
        w.Text(24, 28, Wide(title) * 17 / 11 <= room ? title : Cut(title, room * 11 / 17), "font-size='17' font-weight='600'");
        if (!wrap)
        {
            var lines = Wrap(description, room);
            for (var i = 0; i < lines.Length; i++) w.Text(24, 49 + 14 * i, lines[i], "class='lumen-muted' font-size='11'");
            w.Head = 14 * (lines.Length - 1);
            return;
        }
        var clauses = description.Split(" · ");
        if (clauses.Length > 1 && Wide(description) > room)
        {
            var count = Clauses(clauses, room);
            w.Text(24, 49, string.Join(" · ", clauses[..count]), "class='lumen-muted' font-size='11'");
            w.Text(24, 62, string.Join(" · ", clauses[count..]), "class='lumen-muted' font-size='11'");
        }
        else w.Text(24, 49, description, "class='lumen-muted' font-size='11'");
    }

    /// <summary>How many of <paramref name="clauses"/> go on a first line <paramref name="room"/> pixels wide: as many as fit, at least
    /// one, the rest going on the second.</summary>
    private static int Clauses(string[] clauses, double room)
    {
        var count = clauses.Length - 1;
        while (count > 1 && Wide(string.Join(" · ", clauses[..count])) > room) count--;
        return count;
    }

    /// <summary>
    /// 11 px text set in at most two lines <paramref name="room"/> pixels wide, by the library's generous estimate of its width: as given
    /// where it fits on one; else broken between its <c> · </c> clauses, as a graph's description is, where both lines then fit; else
    /// between the words that set the two lines most nearly equal, so no word is left alone on the second; and where no break leaves
    /// both lines within the room, the first takes as many words as fit and the second the rest, cut at a word with <c>…</c>. A word too
    /// wide for a line by itself is broken, or cut, between its letters.
    /// </summary>
    internal static string[] Wrap(string text, double room)
    {
        if (Wide(text) <= room) return [text];
        var clauses = text.Split(" · ");
        if (clauses.Length > 1)
        {
            var count = Clauses(clauses, room);
            string first = string.Join(" · ", clauses[..count]), second = string.Join(" · ", clauses[count..]);
            if (Wide(first) <= room && Wide(second) <= room) return [first, second];
        }
        // A clause's separator at the break would end one line or start the other, so it is left out there.
        string[]? balanced = null;
        var widest = double.MaxValue;
        for (var space = text.IndexOf(' '); space >= 0; space = text.IndexOf(' ', space + 1))
        {
            string first = text[..space].TrimEnd(' ', '·'), second = text[(space + 1)..].TrimStart(' ', '·');
            if (first.Length == 0 || second.Length == 0) continue;
            var wider = Math.Max(Wide(first), Wide(second));
            if (wider <= room && wider < widest) (balanced, widest) = ([first, second], wider);
        }
        if (balanced is not null) return balanced;
        var start = Fill(text, room, "");
        var rest = text[start.Length..].TrimStart(' ', '·');
        return rest.Length == 0 ? [start] : [start.TrimEnd(' ', '·'), Wide(rest) <= room ? rest : Cut(rest, room)];
    }

    /// <summary>Text cut at a word, with <c>…</c> after it, to fit <paramref name="room"/> pixels of 11 px text; a first word too wide by
    /// itself is cut between its letters. The punctuation a cut would leave before the ellipsis is dropped.</summary>
    private static string Cut(string text, double room) => Fill(text, room, "…").TrimEnd(' ', ',', ';', ':', '·', '-', '—') + "…";

    /// <summary>The longest start of <paramref name="text"/> that ends between words and fits <paramref name="room"/> pixels with
    /// <paramref name="after"/> written after it, or, where its first word alone does not, the most letters that do, and one at least.</summary>
    private static string Fill(string text, double room, string after)
    {
        var end = 0;
        for (var space = text.IndexOf(' '); space >= 0; space = text.IndexOf(' ', space + 1))
        {
            var line = text[..space].TrimEnd();
            if (line.Length == 0) continue;
            if (Wide(line + after) > room) break;
            end = space;
        }
        if (end > 0) return text[..end];
        var letters = 1;
        while (letters < text.Length && Wide(text[..(letters + 1)] + after) <= room) letters++;
        return text[..letters];
    }

    /// <summary>A focusable, labelled data mark. <paramref name="attributes"/> are presentation attributes on the group, which
    /// its shapes inherit and the focus rule overrides, so a hollow marker still shows the focus ring.</summary>
    private static void Datum(SvgWriter w, int series, int index, string label, string shape, string attributes = "")
    {
        w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-series='{series}' data-point='{index}' aria-label='{SvgWriter.E(label)}'{attributes}>{(w.Titles ? $"<title>{SvgWriter.E(label)}</title>" : "")}{shape}</g>");
    }
    private static string PointLabel(ChartSeries s, ChartPoint p) => $"{s.Name}: {p.Label ?? LinearScale.Label(p.X)}, {(p.Y.HasValue ? LinearScale.Label(p.Y.Value) + p.ValueNote : "missing")}";
    /// <summary>A mark's name: its series, its label or X, its value and the value's note, then what colours it — its zone, or how it
    /// changed from the point before — whether it is projected, and a band's bounds.</summary>
    private static string PointLabel(ChartSeries s, ChartPoint p, Axis x, Axis y, int? change = null) =>
        $"{s.Name}: {p.Label ?? x.Format(p.X)}, {(p.Y.HasValue ? y.Format(p.Y.Value) + p.ValueNote : "missing")}" +
        (s.Zones is { } zones && p.Y is { } value ? $", {zones.Zones[zones.IndexOf(value)].Name}" : "") +
        change switch { > 0 => ", better than the previous", < 0 => ", worse than the previous", 0 => ", level with the previous", _ => "" } +
        (s.ProjectedFrom is { } from && p.X >= from ? ", projected" : "") +
        (p.Low.HasValue && p.High.HasValue ? $" (band {y.Format(p.Low.Value)} to {y.Format(p.High.Value)})" : "");
    private static string ZoneColor(ChartStyle style, ZoneScale zones, int index) => zones.Zones[index].Color ?? style.Zones[index];
    /// <summary>How each point of a series with change colours moved from the nearest earlier point that has a value: 1 better, −1
    /// worse and 0 level, by the series' own sense of better; null for a missing value, for the first value, and for every point of a
    /// series without change colours. A gap is passed over, so the point after it compares with the last value before it.</summary>
    private static int?[] Changes(ChartSeries series)
    {
        var changes = new int?[series.Points.Count];
        if (series.ChangeColors == ChangeColors.None) return changes;
        var sense = series.ChangeColors == ChangeColors.LowerIsBetter ? -1 : 1;
        double? before = null;
        for (var i = 0; i < series.Points.Count; i++)
        {
            if (series.Points[i].Y is not { } value) continue;
            if (before is { } last) changes[i] = Math.Sign(value - last) * sense;
            before = value;
        }
        return changes;
    }
    // A timeline's spans and a range's bars have no Y of their own to be missing, and a calendar draws every day it spans.
    private static bool HasData(ChartSpec spec) => spec.Kind is ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Timeline or ChartKind.Calendar
        ? spec.Series.Any(s => s.Points.Count > 0)
        : spec.Series.Any(s => s.Summary is not null || s.Points.Any(p => p.Y.HasValue || p.Low.HasValue && Mark(spec, s) == ChartKind.Range));
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
        // A sparkline has no axes to make room for: its plot fills the drawing but for the padding its largest mark needs.
        var pad = s.Sparkline ? Padding(s, w.Refined) : 0;
        var left = s.Sparkline ? pad : horizontal ? 160d : flipped ? 30d : 76d; var right = s.Width - (s.Sparkline ? pad : secondary || flipped ? 76d : 30d);
        var points = s.Series.SelectMany(x => x.Points).ToArray();
        var bubbles = s.Series.Where(x => Mark(s, x) == ChartKind.Bubble).SelectMany(x => x.Points).ToArray();
        var maxSize = bubbles.Length == 0 ? 0 : bubbles.Max(point => point.Size);
        var cats = points.Select(p => p.X).Distinct().Order().ToArray();
        // A block reaches to its XEnd, and only a block has one here.
        var xs = Axis.Create(s.XAxis, points.Select(p => p.X).Concat(points.Where(p => p.XEnd.HasValue).Select(p => p.XEnd!.Value)), min: s.XMin, max: s.XMax, zone: TimeAxis.Zone(s.TimeZone),
            weekends: s.SkipWeekends, skips: s.TimeSkips.Count > 0 ? s.TimeSkips : null) with { ValueFormat = s.XFormat };
        var plots = Plots(s, cats, points, pad, w.Head, w.Foot);
        // A range bar stands centred on its X, so a continuous chart that draws range bars insets its X axis by half the slot
        // they take, and the first and last bars stand whole inside the plot. The slot follows the closest gap on screen, which
        // the inset narrows, so the two are settled together.
        var inset = 0d;
        var ranged = category ? Array.Empty<double>() : s.Series.Where(series => Mark(s, series) == ChartKind.Range).SelectMany(series => series.Points).Select(p => xs.Map(p.X, 0, 1)).Distinct().Order().ToArray();
        if (ranged.Length > 0)
            for (var pass = 0; pass < 3; pass++)
                inset = Math.Clamp((ranged.Length > 1 ? Enumerable.Range(1, ranged.Length - 1).Min(i => ranged[i] - ranged[i - 1]) * (right - left - 2 * inset) : 30) * .7, 1, 34) / 2;
        double X(double x) => category ? left + (Array.IndexOf(cats, x) + .5) / cats.Length * (right - left) : xs.Map(x, left + inset, right - inset);
        var (xCount, xTicks) = category ? (5, []) : Spaced(w, xs, right - left - 2 * inset, across: true, count: s.XAxis == AxisKind.Time ? 6 : 5);
        // A sparkline draws no axes: no gridlines, ticks or axis titles.
        for (var k = 0; k < plots.Length && !s.Sparkline; k++)
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
            YBounds(w, s, ys, left, right, top, bottom, horizontal);
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
                    // A block's label names the block, not the moment it starts, so the axis keeps its ticks under blocks.
                    var labels = s.Series.Where(series => Mark(s, series) != ChartKind.Blocks).SelectMany(series => series.Points)
                        .Where(p => p.Label is not null && p.X >= xs.Min && p.X <= xs.Max).DistinctBy(p => p.X).OrderBy(p => p.X).ToArray();
                    // A time axis writes its own dates, so the points' labels take their place only where none would be cut short;
                    // a round's long name stays in its point's name rather than reading "Round 1 · Hi…" under it.
                    // An axis labelled at its bounds writes its own two ends, never the points' labels.
                    var pointed = s.XTickLabels != TickLabels.Bounds && s.XTicks switch
                    {
                        TickSource.Axis => false,
                        TickSource.PointLabels => labels.Length > 0,
                        _ => labels.Length is > 0 and <= 24 && (s.XAxis != AxisKind.Time || labels.All(p => p.Label!.Length <= 12))
                    };
                    if (s.XTickLabels == TickLabels.Bounds) XBounds(w, xs, X(xs.Min), X(xs.Max), bottom + 21);
                    else if (pointed)
                    {
                        var step = Math.Max(1, (int)Math.Ceiling(labels.Length / 7d));
                        if (w.Refined)
                            while (step < labels.Length && !Apart(labels.Where((_, i) => i % step == 0).Select(p => (X(p.X), Short(p.Label!, 12))))) step++;
                        // Labelled at its ends, the axis keeps the first and the last of the labels it would draw.
                        var drawn = (labels.Length - 1) / step + 1;
                        for (var i = 0; i < labels.Length; i += step)
                            if (Written(s.XTickLabels, i / step, drawn)) w.Text(X(labels[i].X), bottom + 21, Short(labels[i].Label!, 12), "text-anchor='middle' class='lumen-muted'");
                    }
                    else
                        for (var i = 0; i < xTicks.Count; i++)
                            if (Written(s.XTickLabels, i, xTicks.Count)) w.Text(X(xTicks[i].Value), bottom + 21, xTicks[i].Label, "text-anchor='middle' class='lumen-muted'");
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
        // highlighted last point, or of a highlighted point, where there is one.
        for (var k = 0; k < plots.Length; k++)
        {
            var (pane, top, bottom, ys, ys2, _) = plots[k];
            double Y(double y) => ys.Map(y, bottom, top);
            var bleed = s.Series.Any(series => series.Pane == k && series.HighlightLast) ? 12d
                : s.Series.Any(series => series.Pane == k && series.Points.Any(p => p.Highlight is not null)) ? 7d : 6d;
            // Value labels are drawn over the clip, so the label of the tallest column can rise into the margin above the
            // plot; a label whose column the plot does not show is left out with it.
            var named = new StringBuilder();
            // Every value label written in the pane, as a box round its 11 px text, so a point's label can keep clear of the ones
            // before it, columns' and bars' included.
            var written = new List<(double X1, double X2, double Y1, double Y2)>();
            // A value's note follows it at normal weight in the muted colour.
            string Noted(string text, string? note) => SvgWriter.E(text) + (note is null ? "" : $"<tspan class='lumen-muted' font-weight='400'>{SvgWriter.E(note)}</tspan>");
            void Name(double x, double y, string text, string anchor, string? note)
            {
                var width = Wide(text + note);
                var x1 = anchor == "middle" ? x - width / 2 : anchor == "end" ? x - width : x;
                written.Add((x1, x1 + width, y - 9, y + 3));
                named.Append($"<text x='{N(x)}' y='{N(y)}' text-anchor='{anchor}' font-size='11' aria-hidden='true'>{Noted(text, note)}</text>");
            }
            // A column's label fits across the column, so it never runs into its neighbours'.
            void Above(double x, double width, double far, bool up, string text, string? note)
            {
                if (Wide(text + note) > width || x + width / 2 < left || x + width / 2 > right || far < top - .5 || far > bottom + .5) return;
                Name(x + width / 2, up ? far - 5 : far + 12, text, "middle", note);
            }
            void Beside(double far, double y, double height, bool up, string text, string? note)
            {
                if (Wide(text + note) > (up ? right - far : far - left) - 6) return;
                Name(up ? far + 6 : far - 6, y + height / 2 + 4, text, up ? "start" : "end", note);
            }
            // A line's or a scatter point's label stands above its marker of radius r, 4 pixels clear of it, moved in from the plot's
            // sides so that it is never cut; it goes below where above would leave the plot or meet a label already written, and with
            // room in neither place it is left out, as a column's is, its value kept in its mark's name. A point the plot does not show
            // is left out with its label. The line it labels often runs through where it stands, so it is written over a copy of itself
            // stroked 3 pixels wide in the background colour; a copy, rather than paint-order, so a rasteriser without SVG 2 draws it too.
            void Over(double cx, double cy, double r, string text, string? note, string ink)
            {
                var width = Wide(text + note);
                if (width > right - left || cx < left - .5 || cx > right + .5 || cy < top - .5 || cy > bottom + .5) return;
                var x = Math.Clamp(cx, left + width / 2, right - width / 2);
                foreach (var y in new[] { cy - r - 4, cy + r + 12 })
                {
                    if (y - 9 < top || y + 3 > bottom || written.Any(t => t.X2 > x - width / 2 && t.X1 < x + width / 2 && t.Y2 > y - 9 && t.Y1 < y + 3)) continue;
                    written.Add((x - width / 2, x + width / 2, y - 9, y + 3));
                    var at = $"x='{N(x)}' y='{N(y)}'";
                    var ground = w.Style.Background;
                    named.Append($"<g class='lumen-value' text-anchor='middle' font-size='11' font-weight='600' pointer-events='none' aria-hidden='true'>" +
                        $"<text {at} fill='{ground}' stroke='{ground}' stroke-width='3' stroke-linejoin='round'>{SvgWriter.E(text)}{(note is null ? "" : $"<tspan font-weight='400'>{SvgWriter.E(note)}</tspan>")}</text>" +
                        $"<text {at} fill='{ink}'>{Noted(text, note)}</text></g>");
                    return;
                }
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
            foreach (var reference in references) if (!reference.Front) Draw(w, reference);
            // Column and range series share each slot side by side. On a continuous axis a slot takes its width from the closest
            // two X values any of them in the pane has, as a candle does from its own, so no two slots overlap.
            var columns = Enumerable.Range(0, s.Series.Count).Where(i => s.Series[i].Pane == k && Mark(s, s.Series[i]) is ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Range).ToArray();
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
                // A change colour stands for a point that did better or worse than the one before; a level one keeps the series colour.
                var changes = Changes(series);
                string? Moved(int i) => changes[i] switch { > 0 => w.Style.Rising, < 0 => w.Style.Falling, _ => null };
                // A value label takes its point's colour, and on a gradient the colour the gradient takes at its value, since text
                // painted with the gradient would take the colour at its own height instead. A mark's colour need only clear 3:1, and
                // 11 px text needs 4.5:1, so a colour that falls short gives the label the style's text colour instead.
                string Lettered(int i, ChartPoint p)
                {
                    var ink = Moved(i) ?? (p.Color is null && series.Zones is null && series.Gradient is { } blend ? Blend(blend, p.Y!.Value) : Ink(p));
                    return Contrast.Ratio(ink, w.Style.Background) >= 4.5 ? ink : w.Style.Text;
                }
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
                        // Sampling keeps every highlighted point, so the point a host picks out is always drawn.
                        if (run.Any(p => p.Highlight is not null))
                            indices = indices.Union(Enumerable.Range(0, run.Length).Where(n => run[n].Highlight is not null)).Order().ToArray();
                        // A stroke piece takes the colour of the point it starts from, and a change colour belongs to the segment that
                        // arrives at its point, so for the stroke each drawn point carries the change colour of the next one drawn.
                        var traced = run;
                        if (series.ChangeColors != ChangeColors.None)
                        {
                            traced = [.. run];
                            for (var n = 0; n < indices.Count; n++)
                                traced[indices[n]] = run[indices[n]] with { Color = n + 1 < indices.Count ? Moved(start + indices[n + 1]) : null };
                        }
                        var (path, line) = Trace(series.Curve, traced, indices, X, At, y => scale.Invert(y, bottom, top));
                        if (mark == ChartKind.Area)
                            w.Add($"<path d='{path} L{N(X(run[^1].X))},{N(At(0))} L{N(X(run[0].X))},{N(At(0))} Z' {fill}/>");
                        if (series.Zones is null && series.ProjectedFrom is null && indices.All(i => traced[i].Color is null))
                            w.Add($"<path d='{path}' fill='none' stroke='{paint}' stroke-width='{N(width)}' stroke-linejoin='round'{Rounded(w)}/>");
                        else Stroke(w, series.Zones, line, paint, At, projected, width);
                        foreach (var i in indices)
                        {
                            var p = run[i];
                            string cx = N(X(p.X)), cy = N(At(p.Y!.Value)), ink = Moved(start + i) ?? Ink(p), r = indices.Count > 80 ? "2" : "4";
                            // A hidden marker keeps an invisible target, so the point can still be focused, hovered and announced.
                            // A refined chart's own markers are hidden the same way until the point is hovered or focused, except
                            // a point between two gaps, which has no line to show it. A highlighted point is ringed in its highlight,
                            // whatever the markers, and the last point's ring takes it too.
                            var (shape, attributes) = start + i == last
                                ? ($"<circle cx='{cx}' cy='{cy}' r='10' fill='{p.Highlight ?? ink}' fill-opacity='.2'/><circle cx='{cx}' cy='{cy}' r='5.5' fill='{p.Highlight ?? ink}' stroke='{w.Style.Background}' stroke-width='2'{w.Fixed}/>", "")
                                : p.Highlight is { } highlight ? Ringed(w, cx, cy, highlight)
                                : series.Markers switch
                                {
                                    MarkerStyle.None => ($"<circle cx='{cx}' cy='{cy}' r='{r}' fill='{ink}' fill-opacity='0'/>", ""),
                                    MarkerStyle.Hollow => ($"<circle cx='{cx}' cy='{cy}' r='{r}' fill='{w.Style.Background}'{w.Fixed}/>", $" stroke='{ink}' stroke-width='2'"),
                                    MarkerStyle.Auto when w.Refined && run.Length > 1 => ($"<circle class='lumen-marker' cx='{cx}' cy='{cy}' r='{r}' fill='{ink}'/>", ""),
                                    _ => ($"<circle cx='{cx}' cy='{cy}' r='{r}' fill='{ink}'/>", "")
                                };
                            Datum(w, si, start + i, PointLabel(series,p,xs,scale,changes[start + i]), shape, attributes);
                            if (series.ValueLabels) Over(X(p.X), At(p.Y!.Value), start + i == last || p.Highlight is not null ? Highlighted : indices.Count > 80 ? 2 : 4, scale.Format(p.Y!.Value), p.ValueNote, Lettered(start + i, p));
                        }
                        start = end;
                    }
                }
                else if (mark == ChartKind.Range)
                {
                    // A range bar takes its share of the slot a column would, on a category chart or a continuous axis alike. One
                    // range series needs no name in each bar's label; several are told apart by it.
                    var several = s.Series.Count(other => Mark(s, other) == ChartKind.Range) > 1;
                    for (var pi = 0; pi < series.Points.Count; pi++)
                    {
                        var p = series.Points[pi];
                        if (p.Low is not { } low || p.High is not { } high) continue;
                        double share, from;
                        if (category)
                        {
                            var band = (right - left) / cats.Length;
                            share = band * .72 / columns.Length;
                            from = left + Array.IndexOf(cats, p.X) * band + band * .14 + place * share;
                        }
                        else
                        {
                            share = slot / columns.Length;
                            from = X(p.X) - slot / 2 + place * share;
                        }
                        Datum(w, si, pi, RangeLabel(series, p, xs, scale, several), Capsule(w, from, share, At(low), At(high), p.Y is { } y ? At(y) : null, p.Color ?? color));
                    }
                }
                else if (mark == ChartKind.Blocks)
                {
                    // A block covers its X to its XEnd exactly and stands on the bottom edge of its pane, rising to its Y on its
                    // series' own axis, so a reversed pace axis raises it from the slowest pace and one that includes zero from
                    // zero. Where a block in the series ends as the next begins, each gives up half a hairline there, or a quarter
                    // of its width when it is narrower than two, so the steps read as steps.
                    var several = s.Series.Count(other => Mark(s, other) == ChartKind.Blocks) > 1;
                    var starts = series.Points.Select(p => p.X).ToHashSet();
                    var ends = series.Points.Select(p => p.XEnd!.Value).ToHashSet();
                    var radius = Math.Min(w.Style.BarRadius ?? 4, 6);
                    for (var pi = 0; pi < series.Points.Count; pi++)
                    {
                        var p = series.Points[pi]; var end = p.XEnd!.Value;
                        double from = X(p.X), to = X(end);
                        var gap = Math.Min(.5, (to - from) / 4);
                        if (ends.Contains(p.X)) from += gap;
                        if (starts.Contains(end)) to -= gap;
                        var far = Math.Min(At(p.Y!.Value), bottom);
                        // A block above the bottom of its plot keeps 2 pixels of height, so a bin of one among hundreds still shows;
                        // one at the bottom, a count of none, draws nothing visible and keeps its name and its focus.
                        if (bottom - far > 1e-9) far = Math.Min(far, bottom - 2);
                        Datum(w, si, pi, BlockLabel(series, p, xs, scale, several), Block(from, far, to - from, bottom - far, radius, Ink(p)));
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
                            if (horizontal) Beside(y >= 0 ? rx + rw : rx, ry, rh, y >= 0, scale.Format(y), p.ValueNote);
                            else Above(rx, rw, At(y), y >= 0, scale.Format(y), p.ValueNote);
                        }
                    }
                    else if (place >= 0)
                    {
                        // Centred on its X within the slot, rising from zero on the series' own axis.
                        var width = slot / columns.Length;
                        var x = X(p.X) - slot / 2 + place * width;
                        Datum(w, si, pi, PointLabel(series,p,xs,scale), Bar(w, x, Math.Min(At(0), At(y)), width, Math.Abs(At(y) - At(0)), y >= 0 ? End.Top : End.Bottom, Ink(p), series.Fill));
                        if (series.ValueLabels) Above(x, width, At(y), y >= 0, scale.Format(y), p.ValueNote);
                    }
                    else
                    {
                        var radius = mark == ChartKind.Bubble ? Math.Sqrt(p.Size / Math.Max(maxSize, double.Epsilon)) * 22 : 4;
                        var ink = Moved(pi) ?? Ink(p);
                        string cx = N(X(p.X)), cy = N(At(y));
                        var (shape, attributes) = mark != ChartKind.Scatter ? ($"<circle cx='{cx}' cy='{cy}' r='{N(radius)}' fill='{ink}' fill-opacity='.7' stroke='{ink}'{w.Fixed}/>", "")
                            : p.Highlight is { } highlight ? Ringed(w, cx, cy, highlight) : series.Markers switch
                        {
                            MarkerStyle.Filled => ($"<circle cx='{cx}' cy='{cy}' r='{N(radius)}' fill='{ink}'/>", ""),
                            MarkerStyle.Hollow => ($"<circle cx='{cx}' cy='{cy}' r='{N(radius)}' fill='{w.Style.Background}'{w.Fixed}/>", $" stroke='{ink}' stroke-width='2'"),
                            _ => ($"<circle cx='{cx}' cy='{cy}' r='{N(radius)}' fill='{ink}' fill-opacity='.7' stroke='{ink}'{w.Fixed}/>", "")
                        };
                        Datum(w, si, pi, PointLabel(series,p,xs,scale,changes[pi]), shape, attributes);
                        if (series.ValueLabels) Over(X(p.X), At(y), p.Highlight is null ? radius : Highlighted, scale.Format(y), p.ValueNote, Lettered(pi, p));
                    }
                }
                if (series.Trend) Trend(w, series, color, X, At, left, right, scale.Reversed, s.MaxRenderedPoints);
            }
            // A reference in front stands over the data, so columns and blocks do not hide it, and under the labels.
            foreach (var reference in references) if (reference.Front) Draw(w, reference);
            if (w.Refined) foreach (var reference in references) Label(w, reference);
            w.Add("</svg>");
            w.Add(named.ToString());
        }
    }

    /// <summary>The radius of a highlighted point's marker, as large as the latest point's ring, outlined 2 units wide in the
    /// background colour, so it reaches 6.5 from its centre.</summary>
    private const double Highlighted = 5.5;

    /// <summary>A highlighted point's marker: a dot in its highlight colour, outlined in the background colour so it stands off the
    /// line it sits on. The outline is set on the mark's group, as a hollow marker's is, so the focus rule overrides it and a focused
    /// highlight shows its ring.</summary>
    private static (string Shape, string Attributes) Ringed(SvgWriter w, string cx, string cy, string highlight) =>
        ($"<circle cx='{cx}' cy='{cy}' r='{N(Highlighted)}' fill='{highlight}'{w.Fixed}/>", $" stroke='{w.Style.Background}' stroke-width='2'");

    /// <summary>
    /// How far a sparkline's plot stands in from each edge of its drawing: just far enough that its largest mark, stroke included, is
    /// drawn whole at any edge. A ring round the last point reaches 10, a highlighted point 6.5, a hollow marker 5, a scatter point's
    /// outlined dot 4.5, a filled marker, or a marker shown on hover, 4, and a line or an area half its stroke; columns need none. A
    /// drawing too small to hold that on both sides keeps 2 units of plot, and a ring at its edge is cut.
    /// </summary>
    private static double Padding(ChartSpec s, bool refined)
    {
        var pad = 0d;
        foreach (var series in s.Series)
        {
            var mark = Mark(s, series);
            if (mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter)) continue;
            if (mark != ChartKind.Scatter) pad = Math.Max(pad, (series.StrokeWidth ?? (refined ? 1.6 : 2.5)) / 2);
            var marker = series.Markers switch { MarkerStyle.None => 0, MarkerStyle.Hollow => 5, MarkerStyle.Filled => 4, _ => mark == ChartKind.Scatter ? 4.5 : 4 };
            if (series.Points.Any(p => p.Highlight is not null)) marker = Math.Max(marker, Highlighted + 1);
            if (series.HighlightLast) marker = 10;
            pad = Math.Max(pad, marker);
        }
        return Math.Min(pad, (Math.Min(s.Width, s.Height) - 2) / 2d);
    }

    /// <summary>A main Y axis tick label, on the side the spec puts the axis, unless the spec labels only the ends or the bounds.</summary>
    private static void YTick(SvgWriter w, ChartSpec s, IReadOnlyList<(double Value, string Label)> ticks, int i, double y, double left, double right)
    {
        if (!Labelled(s, ticks, i)) return;
        YLabel(w, s, y, ticks[i].Label, left, right);
    }
    /// <summary>A main Y axis label at <paramref name="y"/>, beside the plot on the side the spec puts the axis.</summary>
    private static void YLabel(SvgWriter w, ChartSpec s, double y, string label, double left, double right)
    {
        if (s.YAxisSide == AxisSide.Right) w.Text(right + 12, y + 4, label, "text-anchor='start' class='lumen-muted'");
        else w.Text(left - 12, y + 4, label, "text-anchor='end' class='lumen-muted'");
    }
    private static bool Labelled(ChartSpec s, IReadOnlyList<(double Value, string Label)> ticks, int i) => Written(s.YTickLabels, i, ticks.Count);
    /// <summary>Whether tick <paramref name="i"/> of the <paramref name="count"/> an axis draws carries its label: every one does, or the
    /// first and the last, or, where the axis is labelled at its bounds, none, its ends being labelled instead.</summary>
    private static bool Written(TickLabels labels, int i, int count) => labels == TickLabels.All || labels == TickLabels.Ends && (i == 0 || i == count - 1);
    /// <summary>The main Y axis's two ends, labelled at their exact values in its format where the spec labels its bounds: beside the
    /// plot, or along the bottom of a horizontal bar chart, the lower end's label starting at its end and the upper's ending at its.</summary>
    private static void YBounds(SvgWriter w, ChartSpec s, Axis ys, double left, double right, double top, double bottom, bool horizontal = false)
    {
        if (s.YTickLabels != TickLabels.Bounds) return;
        // A horizontal bar chart's value ticks stand 20 pixels under its plot.
        if (horizontal) XBounds(w, ys, ys.Map(ys.Min, left, right), ys.Map(ys.Max, left, right), bottom + 20);
        else foreach (var end in new[] { ys.Min, ys.Max }) YLabel(w, s, ys.Map(end, bottom, top), ys.Format(end), left, right);
    }
    /// <summary>An axis along the bottom labelled at its two ends, at their exact values in its format, on the baseline
    /// <paramref name="y"/>: the first's label starts at <paramref name="from"/> and the last's ends at <paramref name="to"/>, so both
    /// stand whole under the plot however long they are.</summary>
    private static void XBounds(SvgWriter w, Axis axis, double from, double to, double y)
    {
        w.Text(from, y, axis.Format(axis.Min), "text-anchor='start' class='lumen-muted'");
        w.Text(to, y, axis.Format(axis.Max), "text-anchor='end' class='lumen-muted'");
    }
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
    internal static double Wide(string text) =>
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
        return $"<path d='{Outline(x, y, width, height, end, r)}' fill='{paint}'/>";
    }

    /// <summary>The outline of a bar whose far end is <paramref name="end"/>: that end's two corners rounded by
    /// <paramref name="r"/>, already clamped, and its baseline end square.</summary>
    private static string Outline(double x, double y, double width, double height, End end, double r)
    {
        string Arc(double toX, double toY) => r > 0 ? $" A{N(r)},{N(r)} 0 0 1 {N(toX)},{N(toY)}" : "";
        double right = x + width, bottom = y + height;
        return end switch
        {
            End.Top => $"M{N(x)},{N(bottom)} L{N(x)},{N(y + r)}{Arc(x + r, y)} L{N(right - r)},{N(y)}{Arc(right, y + r)} L{N(right)},{N(bottom)} Z",
            End.Bottom => $"M{N(right)},{N(y)} L{N(right)},{N(bottom - r)}{Arc(right - r, bottom)} L{N(x + r)},{N(bottom)}{Arc(x, bottom - r)} L{N(x)},{N(y)} Z",
            End.Right => $"M{N(x)},{N(y)} L{N(right - r)},{N(y)}{Arc(right, y + r)} L{N(right)},{N(bottom - r)}{Arc(right - r, bottom)} L{N(x)},{N(bottom)} Z",
            _ => $"M{N(right)},{N(bottom)} L{N(x + r)},{N(bottom)}{Arc(x, bottom - r)} L{N(x)},{N(y + r)}{Arc(x + r, y)} L{N(right)},{N(y)} Z"
        };
    }

    /// <summary>A block from <paramref name="x"/> across <paramref name="width"/>, from its top at <paramref name="y"/> down to
    /// the bottom edge of its plot, <paramref name="height"/> below. Its far end, the top, is rounded by <paramref name="radius"/>,
    /// clamped to half its width and to its height, and the end it stands on is square.</summary>
    private static string Block(double x, double y, double width, double height, double radius, string ink) =>
        $"<path class='lumen-block' d='{Outline(x, y, width, height, End.Top, Math.Max(0, Math.Min(radius, Math.Min(width / 2, height))))}' fill='{ink}'/>";

    /// <summary>A block's name: its label, its span in the X axis's format, its height in its own axis's and its zone where its
    /// series has zones — <c>Lap 3: 1 to 2, 4:52</c> or <c>Interval 2: 10:00 to 14:00, 275, Lactate threshold</c>. A block
    /// without a label is led by its series' name, and so is every block where several series draw blocks.</summary>
    private static string BlockLabel(ChartSeries s, ChartPoint p, Axis x, Axis y, bool several) =>
        $"{(p.Label is null ? s.Name : several ? $"{s.Name}, {p.Label}" : p.Label)}: {x.Format(p.X)} to {x.Format(p.XEnd!.Value)}, {y.Format(p.Y!.Value)}{p.ValueNote}" +
        (s.Zones is { } zones ? $", {zones.Zones[zones.IndexOf(p.Y.Value)].Name}" : "");

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

    /// <summary>How high the lowest block stands, as a fraction of its plot's height, on an axis fitted to the data.</summary>
    private const double Rise = 1 / 6d;

    /// <summary>
    /// The main plot and the panes under it, top to bottom, each with the Y axes its own series are measured against. The
    /// height between the title and the X axis is shared out by weight, the main plot weighing 1, after a fixed gap
    /// between each two; a description on two lines takes <paramref name="head"/> from its top and a source on two lines
    /// <paramref name="foot"/> from its bottom. A sparkline's one plot fills its drawing but for <paramref name="pad"/>.
    /// </summary>
    private static (ChartPane Pane, double Top, double Bottom, Axis Ys, Axis Ys2, bool Paired)[] Plots(ChartSpec s, double[] cats, ChartPoint[] points, double pad, int head, int foot)
    {
        const double gap = 24;
        double top = s.Sparkline ? pad : 78 + head, bottom = s.Height - (s.Sparkline ? pad : 76d + foot);
        var room = bottom - top - gap * s.Panes.Count;
        var weight = 1 + s.Panes.Sum(p => p.Weight);
        var zero = s.IncludeZero || s.Kind is ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Area;
        var plots = new (ChartPane, double, double, Axis, Axis, bool)[s.Panes.Count + 1];
        for (var k = 0; k < plots.Length; k++)
        {
            var pane = Pane(s, k);
            var mine = s.Series.Where(x => x.Pane == k).ToArray();
            var values = mine.Where(x => !x.Secondary).SelectMany(x => x.Points).Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToList();
            // Prices, band edges and the ends of range bars reach the axis of the series that carries them.
            IEnumerable<ChartPoint> Bounded(bool right) => mine.Where(x => x.Secondary == right && Mark(s, x) is ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range)
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
            // Blocks stand on the bottom edge of the plot, so an axis fitted to them would leave the lowest — the slowest, on a
            // reversed axis — with no height. Unless the axis is held at zero or its bottom is set, its bottom moves out until
            // that block stands a sixth of the plot's height, measured in the axis's own space.
            Axis Footed(Axis axis, bool right, bool held, double? bottomBound)
            {
                if (held || bottomBound is not null) return axis;
                var heights = mine.Where(x => x.Secondary == right && Mark(s, x) == ChartKind.Blocks).SelectMany(x => x.Points)
                    .Where(p => p.Y.HasValue).Select(p => axis.Map(p.Y!.Value, 0, 1)).ToArray();
                if (heights.Length == 0 || heights.Min() >= Rise) return axis;
                var foot = axis.Invert((heights.Min() - Rise) / (1 - Rise), 0, 1);
                return axis.Reversed ? axis with { Max = foot } : axis with { Min = foot };
            }
            // A minimum span centres the axis on its data wherever the data spans less, so a small wobble reads as small. It is refused
            // beside set bounds, so it stands in their place; blocks still reach below the lowest block, as on any fitted axis.
            var (low, high) = pane.YMinSpan is { } span && values.Count > 0 && values.Max() - values.Min() < span
                ? ((values.Max() + values.Min()) / 2 - span / 2, (values.Max() + values.Min()) / 2 + span / 2) : (pane.YMin, pane.YMax);
            var ys = Footed(Axis.Create(pane.YAxis, values, zero || Filled(false), low, high) with { ValueFormat = pane.YFormat, Reversed = pane.YReversed },
                false, zero || Filled(false), pane.YReversed ? pane.YMax : pane.YMin);
            var paired = mine.Any(x => x.Secondary);
            var secondValues = mine.Where(x => x.Secondary).SelectMany(x => x.Points).Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToList();
            foreach (var p in Bounded(true)) { secondValues.Add(p.Low!.Value); secondValues.Add(p.High!.Value); }
            var ys2 = paired ? Footed(Axis.Create(pane.Y2Axis, secondValues, zero || Filled(true), pane.Y2Min, pane.Y2Max) with { ValueFormat = pane.Y2Format, Reversed = pane.Y2Reversed },
                true, zero || Filled(true), pane.Y2Reversed ? pane.Y2Max : pane.Y2Min) : ys;
            var below = k == plots.Length - 1 ? bottom : top + room * (pane.Weight / weight);
            plots[k] = (pane, top, below, ys, ys2, paired);
            top = below + gap;
        }
        return plots;
    }

    /// <summary>
    /// A series' trend, fitted in the space the reader sees. The axes have already taken the logarithm and left out the spans a
    /// calendar skips, so a line is straight on screen instead of curving on a log axis or jumping where a trading axis closes,
    /// and a curve bends only where the data does. Least squares is unchanged by the scaling between data and pixels, and a
    /// polynomial stays a polynomial of its degree under it, so on plain axes a line or a polynomial is the ordinary fit. A line
    /// spans the plot. A polynomial and an exponential are drawn every 2 pixels or so across the X their observations cover and no
    /// further, and a moving average from window to window, broken where a window is less than half full; a long run of it is
    /// thinned to <paramref name="budget"/> as a line is.
    /// </summary>
    private static void Trend(SvgWriter w, ChartSeries series, string color, Func<double, double> X, Func<double, double> Y, double left, double right, bool reversed, int budget)
    {
        var present = series.Points.Where(p => p.Y.HasValue);
        string d, label;
        switch (series.TrendFit)
        {
            case TrendFit.MovingAverage:
            {
                // Each window's mean of the drawn positions present in it, at the window's last point; a missing value holds its
                // place in the window and adds nothing.
                var windows = Statistics.Rolling(series.Points.Select(p => p.Y is { } v ? Y(v) : (double?)null).ToArray(), series.TrendPoints, (series.TrendPoints + 1) / 2);
                var path = new StringBuilder();
                var run = new List<ChartPoint>();
                void Close()
                {
                    // A lone window has nothing to join to.
                    if (run.Count > 1)
                    {
                        var kept = Sampling.MinMax(run, budget);
                        for (var n = 0; n < kept.Count; n++) path.Append($"{(path.Length == 0 ? "" : " ")}{(n == 0 ? "M" : "L")}{N(run[kept[n]].X)},{N(run[kept[n]].Y!.Value)}");
                    }
                    run.Clear();
                }
                for (var i = 0; i < windows.Count; i++)
                    if (windows[i] is { } window) run.Add(new(X(series.Points[i].X), window.Mean));
                    else Close();
                Close();
                if (path.Length == 0) return;
                d = path.ToString();
                label = $"{series.Name} trend: {series.TrendPoints.ToString(CultureInfo.InvariantCulture)}-point moving average";
                break;
            }
            case TrendFit.Polynomial:
            {
                var drawn = present.Select(p => (X: X(p.X), Y: Y(p.Y!.Value))).ToArray();
                if (Statistics.Polynomial(drawn, series.TrendDegree) is not { } fit || Across(drawn, fit.Predict) is not { } curve) return;
                d = curve;
                label = $"{series.Name} trend: {series.TrendDegree switch { 3 => "cubic", 4 => "quartic", _ => "quadratic" }} fit, R squared {fit.R2.ToString("0.00", CultureInfo.InvariantCulture)}";
                break;
            }
            case TrendFit.Exponential:
            {
                // Fitted to the logarithm of each positive value against the X it is drawn at, and drawn through the series' axis, so
                // it is straight on a logarithmic axis and curves on a linear one. A value's own logarithm is taken whatever the axis,
                // so the fit is the same on either.
                var drawn = present.Where(p => p.Y > 0).Select(p => (X: X(p.X), Y: p.Y!.Value)).ToArray();
                if (Statistics.Exponential(drawn) is not { } fit || Across(drawn, x => Y(fit.Predict(x))) is not { } curve) return;
                d = curve;
                label = $"{series.Name} trend: exponential fit, {(fit.B >= 0 ? "rising" : "falling")}, R squared {fit.R2.ToString("0.00", CultureInfo.InvariantCulture)}";
                break;
            }
            default:
            {
                var fit = Statistics.Fit(present.Select(p => (X(p.X), Y(p.Y!.Value))));
                if (fit is null) return;
                // Screen y grows downwards, so a falling line is a rising series; on a reversed axis larger values sit
                // lower, so there a falling line is a falling series.
                var rising = reversed ? fit.Slope >= 0 : fit.Slope <= 0;
                label = $"{series.Name} trend: {(rising ? "rising" : "falling")}, R squared {fit.R2.ToString("0.00", CultureInfo.InvariantCulture)}";
                d = $"M{N(left)},{N(fit.Predict(left))} L{N(right)},{N(fit.Predict(right))}";
                break;
            }
        }
        // A refined trend is three quarters the width of its series' stroke, so it reads as a guide rather than as data.
        var width = w.Refined ? N(Math.Round((series.StrokeWidth ?? 1.6) * .75, 2)) : "2";
        w.Add($"<path class='lumen-trend' d='{d}' " +
            $"fill='none' stroke='{color}' stroke-width='{width}' stroke-dasharray='7 5' stroke-opacity='.85'{w.Fixed} role='img' aria-label='{SvgWriter.E(label)}'>" +
            $"{(w.Titles ? $"<title>{SvgWriter.E(label)}</title>" : "")}</path>");

        // A curve from the first observation's X to the last, every 2 pixels or so. Past the plot, and the 12-pixel bleed its clip
        // allows, nothing would show, so a zoomed chart does not sample the stretch it hides.
        string? Across((double X, double Y)[] drawn, Func<double, double> at)
        {
            double from = Math.Max(drawn.Min(p => p.X), left - 12), to = Math.Min(drawn.Max(p => p.X), right + 12);
            if (from > to) return null;
            var steps = Math.Max(1, (int)Math.Ceiling((to - from) / 2));
            return string.Join(" ", Enumerable.Range(0, steps + 1).Select(k =>
            {
                var x = k == steps ? to : from + (to - from) * k / steps;
                return $"{(k == 0 ? "M" : "L")}{N(x)},{N(at(x))}";
            }));
        }
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

    private static int Layer(ChartKind mark) => mark switch { ChartKind.Band => 0, ChartKind.Area => 1, ChartKind.Column or ChartKind.Range or ChartKind.Blocks or ChartKind.Candlestick or ChartKind.Ohlc => 2, ChartKind.Line => 3, _ => 4 };

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
            // A horizontal bar chart measures along X, so there the bands stand upright.
            bands.Add(Measure(w, new(horizontal ? AnnotationAxis.X : AnnotationAxis.Y, from) { To = to, Label = scale.Zones[i].Name, Color = ZoneColor(w.Style, scale, i) },
                at, at, ys, ys, left, right, top, bottom, Range(lower, upper, ys), w.Style.Text));
        }
        return bands;
    }

    /// <summary>What a zone holds, read on <paramref name="axis"/>: up to its own bound, above the one before it, or between the two.</summary>
    private static string Range(double? lower, double? upper, Axis axis) => (lower, upper) switch
    {
        (null, null) => "every value",
        (null, { } u) => $"up to {axis.Format(u)}",
        ({ } l, null) => $"above {axis.Format(l)}",
        ({ } l, { } u) => $"{axis.Format(l)} to {axis.Format(u)}"
    };

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

    /// <summary>A reference line or band measured for drawing: its shape, its name, the label it shows, and where.</summary>
    private sealed class Reference
    {
        public required string Name { get; init; }
        /// <summary>The label drawn on the chart: its name, or its annotation's label alone where it shows no value.</summary>
        public required string Text { get; init; }
        /// <summary>Drawn over the data rather than behind it.</summary>
        public bool Front { get; init; }
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
            // along its whole length rather than only where a dash happens to fall. A refined line is thinner than the data. A line in
            // front of the data stands on a halo of the background colour, as a gauge's target does, so it shows over a bar of any
            // colour, a muted line over grey bins included.
            var thin = w.Refined ? 1 : 1.5;
            shape = $"<line x1='{N(x1)}' y1='{N(y1)}' x2='{N(x2)}' y2='{N(y2)}' stroke='{colour}' stroke-opacity='0' stroke-width='12'{w.Fixed}/>" +
                (annotation.InFront ? $"<line x1='{N(x1)}' y1='{N(y1)}' x2='{N(x2)}' y2='{N(y2)}' stroke='{w.Style.Background}' stroke-width='{N(thin + 2)}'{w.Fixed}/>" : "") +
                $"<line x1='{N(x1)}' y1='{N(y1)}' x2='{N(x2)}' y2='{N(y2)}' stroke='{colour}' stroke-width='{N(thin)}'{dash}{w.Fixed}/>";
            reading ??= axis.Format(annotation.From);
            (labelX, labelY, anchor) = horizontal ? (right - 6, at - 6, "end") : Upright(at, at);
            (near, far) = (at, at);
        }
        // The name always reads the value; the label drawn leaves it out where the annotation shows its label alone.
        var name = annotation.Label is null ? reading : $"{annotation.Label}: {reading}";
        return new()
        {
            Name = name, Text = annotation.ShowValue ? name : annotation.Label!, Front = annotation.InFront, Shape = shape, Ink = ink ?? colour, Named = named,
            Upright = !horizontal, Band = annotation.To is not null, Near = near, Far = far, X = labelX, Y = labelY, Anchor = anchor
        };
    }

    /// <summary>Draws a reference, behind the data or, in front, over it. The classic finish writes its label with it; the refined one writes the label
    /// over the data afterwards, with <see cref="Label"/>, so the group carries the name and the shape alone, as a sparkline's does in
    /// either finish.</summary>
    private static void Draw(SvgWriter w, Reference reference)
    {
        if (!reference.Named) { w.Add(reference.Shape); return; }
        Aggregate(w, reference.Name, reference.Shape + (w.Refined || w.Bare ? "" :
            $"<text x='{N(reference.X)}' y='{N(reference.Y)}' text-anchor='{reference.Anchor}' fill='{reference.Ink}' font-size='11'>{SvgWriter.E(reference.Text)}</text>"));
    }

    /// <summary>A refined reference label, over the data with a halo in the background colour so it reads across lines. The
    /// pointer passes through it to the marks beneath, and it is hidden from assistive technology, which reads the
    /// reference's own name.</summary>
    private static void Label(SvgWriter w, Reference reference)
    {
        if (!reference.Named || !reference.Shown || w.Bare) return;
        w.Add($"<text x='{N(reference.X)}' y='{N(reference.Y)}' text-anchor='{reference.Anchor}' fill='{reference.Ink}' font-size='11' " +
            $"stroke='{w.Style.Background}' stroke-width='3' stroke-linejoin='round' paint-order='stroke' pointer-events='none' aria-hidden='true'>{SvgWriter.E(reference.Text)}</text>");
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
            reference.Shown = reference.Upright ? Upright(reference, Wide(reference.Text)) : Level(reference, Wide(reference.Text));
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

    /// <summary>
    /// A range bar in the share of its slot that starts at <paramref name="from"/>: a capsule centred in the share, as wide as
    /// the share up to 18 pixels, running exactly from one end of its range to the other on screen, or 1 pixel long when they
    /// meet, its ends rounded by half its width, or by half its length when it is shorter than it is wide. A typical value is
    /// a dot on the bar's centre line, filled with the background and ringed in the bar's colour, 3 to 4.5 pixels in radius,
    /// so it stands proud of a thin bar and reads as a hole in a wide one.
    /// </summary>
    private static string Capsule(SvgWriter w, double from, double share, double y1, double y2, double? dot, string ink)
    {
        var width = Math.Min(share, 18);
        var x = from + (share - width) / 2;
        double top = Math.Min(y1, y2), length = Math.Abs(y2 - y1);
        if (length < 1) { top -= (1 - length) / 2; length = 1; }
        var shape = $"<rect class='lumen-range' x='{N(x)}' y='{N(top)}' width='{N(width)}' height='{N(length)}' rx='{N(Math.Min(width, length) / 2)}' fill='{ink}'/>";
        return dot is { } at
            ? shape + $"<circle cx='{N(x + width / 2)}' cy='{N(at)}' r='{N(Math.Clamp(width / 2, 3, 4.5))}' fill='{w.Style.Background}' stroke='{ink}' stroke-width='2'{w.Fixed}/>"
            : shape;
    }

    /// <summary>A range bar's name: its category or X, its two ends in the axis's format and its typical value, as
    /// <c>12 Sep: 52 to 168, average 74</c>, led by its series' name where several series draw ranges.</summary>
    private static string RangeLabel(ChartSeries s, ChartPoint p, Axis x, Axis y, bool named) =>
        $"{(named ? s.Name + ", " : "")}{p.Label ?? x.Format(p.X)}: {y.Format(p.Low!.Value)} to {y.Format(p.High!.Value)}{(p.Y is { } value ? $", average {y.Format(value)}" : "")}";

    /// <summary>
    /// A state timeline: one lane per series, top to bottom in series order, named on the side the Y axis would stand. Each
    /// span is a rounded bar in its lane's colour from its X to its XEnd, at least 1 pixel wide, half the lane's height up to
    /// 24 pixels and centred in it, its corners the style's bar radius, or 4 pixels, clamped to half its width and height.
    /// Where a span ends as one in another lane begins, a hairline joins the middles of the two lanes at that moment, drawn
    /// behind the bars, unless <see cref="ChartSpec.TimelineConnectors"/> is off. Gridlines stand at the X ticks. The spans and
    /// any X annotations are clipped to the plot, so a zoom cuts spans at its edges, as it does lines.
    /// </summary>
    private static void Timeline(SvgWriter w, ChartSpec s)
    {
        var flipped = s.YAxisSide == AxisSide.Right;
        var names = s.Series.Select(series => Short(series.Name, 14)).ToArray();
        // The lane names stand 12 pixels from the plot, with room beyond them for the Y title.
        var margin = Math.Clamp(Math.Ceiling(names.Max(Broad)) + 42, 76, 180);
        double left = flipped ? 30 : margin, right = s.Width - (flipped ? margin : 30), top = 78 + w.Head, bottom = s.Height - 76 - w.Foot;
        var xs = Axis.Create(s.XAxis, s.Series.SelectMany(series => series.Points).SelectMany(p => new[] { p.X, p.XEnd!.Value }), min: s.XMin, max: s.XMax,
            zone: TimeAxis.Zone(s.TimeZone), weekends: s.SkipWeekends, skips: s.TimeSkips.Count > 0 ? s.TimeSkips : null) with { ValueFormat = s.XFormat };
        double X(double x) => xs.Map(x, left, right);
        var lane = (bottom - top) / s.Series.Count;
        var thick = Math.Min(lane * .5, 24);
        double Middle(int i) => top + (i + .5) * lane;
        var (xCount, xTicks) = Spaced(w, xs, right - left, across: true, count: s.XAxis == AxisKind.Time ? 6 : 5);
        if (s.MinorGridlines)
            foreach (var minor in xs.MinorTicks(xCount)) { var x = X(minor); Gridline(w, x, top, x, bottom, minor: true); }
        for (var i = 0; i < xTicks.Count; i++)
        {
            var x = X(xTicks[i].Value);
            Gridline(w, x, top, x, bottom);
            if (Written(s.XTickLabels, i, xTicks.Count)) w.Text(x, bottom + 21, xTicks[i].Label, "text-anchor='middle' class='lumen-muted'");
        }
        if (s.XTickLabels == TickLabels.Bounds) XBounds(w, xs, left, right, bottom + 21);
        for (var i = 0; i < s.Series.Count; i++)
            w.Text(flipped ? right + 12 : left - 12, Middle(i) + 4, names[i], $"text-anchor='{(flipped ? "start" : "end")}' class='lumen-muted'");
        w.Text((left + right) / 2, bottom + 44, s.XLabel, "text-anchor='middle' class='lumen-muted'");
        YTitle(w, s, s.YLabel, top, bottom);
        const double bleed = 6;
        w.Add($"<svg x='{N(left - bleed)}' y='{N(top - bleed)}' width='{N(right - left + 2 * bleed)}' height='{N(bottom - top + 2 * bleed)}' viewBox='{N(left - bleed)} {N(top - bleed)} {N(right - left + 2 * bleed)} {N(bottom - top + 2 * bleed)}' overflow='hidden'>");
        // Moments marked along X stand behind the spans, as references do behind data.
        var references = s.Annotations.Select(annotation => Measure(w, annotation, X, y => y, xs, xs, left, right, top, bottom)).ToList();
        if (w.Refined) Place(references, left, right, top, bottom);
        foreach (var reference in references) if (!reference.Front) Draw(w, reference);
        if (s.TimelineConnectors)
        {
            // The lanes each moment starts a span in; within a lane spans cannot overlap, so a lane starts at most one there.
            var starts = new Dictionary<double, List<int>>();
            for (var i = 0; i < s.Series.Count; i++)
                foreach (var p in s.Series[i].Points)
                {
                    if (!starts.TryGetValue(p.X, out var lanes)) starts[p.X] = lanes = new List<int>();
                    lanes.Add(i);
                }
            var path = new StringBuilder();
            for (var i = 0; i < s.Series.Count; i++)
                foreach (var p in s.Series[i].Points)
                    if (starts.TryGetValue(p.XEnd!.Value, out var next))
                        foreach (var j in next.Where(j => j != i))
                            path.Append($"{(path.Length == 0 ? "" : " ")}M{N(X(p.XEnd.Value))},{N(Middle(i))} L{N(X(p.XEnd.Value))},{N(Middle(j))}");
            if (path.Length > 0)
                w.Add($"<path class='lumen-connectors' d='{path}' fill='none' stroke='{w.Style.Muted}' stroke-opacity='.5' stroke-width='1'{w.Fixed}/>");
        }
        var radius = w.Style.BarRadius ?? 4;
        string When(double x) => s.XAxis == AxisKind.Time ? xs.LocalText(x, xs.Max - xs.Min < 2 * 86_400_000 ? "HH:mm" : "d MMM HH:mm") : xs.Format(x);
        for (var i = 0; i < s.Series.Count; i++)
        {
            var series = s.Series[i]; var color = SeriesColor(series, i, w.Style);
            for (var pi = 0; pi < series.Points.Count; pi++)
            {
                var p = series.Points[pi]; var end = p.XEnd!.Value;
                double x1 = X(p.X), width = Math.Max(X(end) - x1, 1);
                Datum(w, i, pi, $"{series.Name}: {(p.Label is null ? "" : p.Label + ", ")}{When(p.X)} to {When(end)}, {Spoken(s, end - p.X)}",
                    $"<rect class='lumen-span' x='{N(x1)}' y='{N(Middle(i) - thick / 2)}' width='{N(width)}' height='{N(thick)}' rx='{N(Math.Min(radius, Math.Min(width, thick) / 2))}' fill='{color}'/>");
            }
        }
        foreach (var reference in references) if (reference.Front) Draw(w, reference);
        if (w.Refined) foreach (var reference in references) Label(w, reference);
        w.Add("</svg>");
    }

    /// <summary>How long a stretch of a timeline's X lasts in seconds: a time axis counts milliseconds, and a duration or
    /// time-of-day axis seconds. A plain number has no unit, so it has no length in time.</summary>
    private static double? Seconds(ChartSpec s, double length) =>
        s.XAxis == AxisKind.Time ? length / 1000 : s.XFormat is ValueFormat.Duration or ValueFormat.TimeOfDay ? length : null;

    /// <summary>A span's length as it is said: <c>45 s</c>, <c>27 min</c>, <c>2 h</c> or <c>1 h 42 min</c>, rounded half up to
    /// the second below a minute and to the minute from one; a length in no unit of time is the plain number.</summary>
    private static string Spoken(ChartSpec s, double length)
    {
        if (Seconds(s, length) is not { } seconds) return LinearScale.Label(length);
        var whole = Math.Round(seconds, MidpointRounding.AwayFromZero);
        if (whole < 60) return string.Create(CultureInfo.InvariantCulture, $"{whole} s");
        var minutes = Math.Round(seconds / 60, MidpointRounding.AwayFromZero);
        if (minutes < 60) return string.Create(CultureInfo.InvariantCulture, $"{minutes} min");
        var (hours, rest) = (Math.Floor(minutes / 60), minutes % 60);
        return rest == 0 ? string.Create(CultureInfo.InvariantCulture, $"{hours} h") : string.Create(CultureInfo.InvariantCulture, $"{hours} h {rest} min");
    }

    /// <summary>A timeline lane's legend: its state, its total time as h:mm, and its share of the time in every lane as a whole
    /// percentage, as <c>REM 1:42, 22 %</c>. A lane in no unit of time totals the plain number; a chart with no spans names
    /// the state alone.</summary>
    private static string Lane(ChartSpec s, int index)
    {
        static double Total(ChartSeries series) => series.Points.Sum(p => p.XEnd is { } end ? end - p.X : 0);
        var all = s.Series.Sum(Total);
        var name = s.Series[index].Name;
        if (!(all > 0)) return name;
        var total = Total(s.Series[index]);
        var share = Math.Round(total / all * 100, MidpointRounding.AwayFromZero);
        string text;
        if (Seconds(s, total) is { } seconds)
        {
            var minutes = Math.Round(seconds / 60, MidpointRounding.AwayFromZero);
            text = string.Create(CultureInfo.InvariantCulture, $"{Math.Floor(minutes / 60)}:{minutes % 60:00}");
        }
        else text = LinearScale.Label(total);
        return string.Create(CultureInfo.InvariantCulture, $"{name} {text}, {share} %");
    }

    /// <summary>The day a moment falls on in <paramref name="zone"/>, or in UTC when it is null.</summary>
    internal static DateOnly CalendarDay(double x, TimeZoneInfo? zone)
    {
        var utc = TimeAxis.Moment(x).UtcDateTime;
        return DateOnly.FromDateTime(zone is null ? utc : TimeZoneInfo.ConvertTimeFromUtc(utc, zone));
    }

    /// <summary>The first and last day a calendar draws: those of <see cref="ChartSpec.XMin"/> and <see cref="ChartSpec.XMax"/>
    /// when set, and otherwise of its earliest and latest points, in its zone. A first day after the last draws that day alone.</summary>
    internal static (DateOnly First, DateOnly Last) CalendarSpan(ChartSpec s)
    {
        var zone = TimeAxis.Zone(s.TimeZone);
        var points = s.Series.SelectMany(series => series.Points).ToArray();
        var first = s.XMin is { } from ? CalendarDay(from, zone) : points.Min(p => CalendarDay(p.X, zone));
        var last = s.XMax is { } to ? CalendarDay(to, zone) : points.Max(p => CalendarDay(p.X, zone));
        return (first, last < first ? first : last);
    }

    /// <summary>
    /// A calendar of the days from the first to the last, in a grid of weeks or of months. Each day is a cell on a pitch that fills
    /// the width, or the height when that is the tighter: a fifth of the pitch, up to 6 pixels, is the gap between cells, across
    /// and down alike, and the grid is centred in the room it leaves. A day with activity is a focusable mark in its zone's colour,
    /// or on a ramp across the active days' totals that starts a third of the way from an empty day's grid colour to the style's
    /// heatmap high and ends at that high, where it takes the heatmap's hairline as well; a day without activity is an empty cell
    /// in the grid colour.
    /// An X annotation outlines its day in the gap round it. The key under the grid names the zones or reads the ramp's ends,
    /// and names each outlined day.
    /// </summary>
    private static void Calendar(SvgWriter w, ChartSpec s)
    {
        const double margin = 24;
        double top = 64 + w.Head, bottom = s.Height - 30d - w.Foot;
        var series = s.Series[0];
        var zone = TimeAxis.Zone(s.TimeZone);
        var (first, last) = CalendarSpan(s);
        // Each day's total, the first point on it, which its mark reports, and the labels of its points.
        var days = new Dictionary<DateOnly, (double Total, int Point, List<string> Labels)>();
        for (var i = 0; i < series.Points.Count; i++)
        {
            var p = series.Points[i]; var day = CalendarDay(p.X, zone);
            if (day < first || day > last) continue;
            var (total, point, labels) = days.TryGetValue(day, out var entry) ? entry : (0d, i, new List<string>());
            if (p.Label is not null) labels.Add(p.Label);
            days[day] = (total + (p.Y ?? 0), point, labels);
        }
        var active = days.Where(d => d.Value.Total > 0).ToDictionary(d => d.Key, d => d.Value);
        var values = new Axis(AxisKind.Linear, 0, 1) { ValueFormat = s.YFormat };
        var zones = s.YZones;
        var ramp = LinearScale.Create(active.Values.Select(d => d.Total));
        var largest = active.Count == 0 ? 1 : active.Values.Max(d => d.Total);
        // The ramp steps up from an empty day, as a contribution grid does, so its quietest day stands apart from a rest day on light
        // and dark styles alike. The heatmap's low end would not: it is a light style's track colour and a dark style's brightest.
        var palest = CalendarLow(w.Style);
        string Ink(double value) => zones is not null ? ZoneColor(w.Style, zones, zones.IndexOf(value))
            : Mix(palest, w.Style.HeatmapHigh, Math.Clamp(ramp.Map(value, 0, 1), 0, 1));
        string Date(DateOnly day) => day.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);
        var round = s.CalendarCell != CalendarCell.Square;
        var outlined = s.Annotations.Select(annotation => (Annotation: annotation, Day: CalendarDay(annotation.From, zone)))
            .Where(a => a.Day >= first && a.Day <= last).ToArray();

        // The key: the zones by name, or the ramp's lowest total, five steps along it and its highest; then each outlined day.
        // An item is never split across lines; the lines run as wide as the drawing allows.
        string Swatch(double x, double y, string paint) => round
            ? $"<circle cx='{N(x + 5)}' cy='{N(y - 4)}' r='5' {paint}/>" : $"<rect x='{N(x)}' y='{N(y - 9)}' width='10' height='10' rx='2' {paint}/>";
        string Words(double x, double y, string text) => $"<text x='{N(x)}' y='{N(y)}' font-size='11'>{SvgWriter.E(text)}</text>";
        var key = new List<(double Width, Func<double, double, string> Draw)>();
        if (zones is not null)
            for (var i = 0; i < zones.Zones.Count; i++)
            {
                var (name, ink) = (zones.Zones[i].Name, ZoneColor(w.Style, zones, i));
                key.Add((14 + Wide(name), (x, y) => Swatch(x, y, $"fill='{ink}'") + Words(x + 14, y, name)));
            }
        else if (active.Count > 0)
        {
            string low = values.Format(ramp.Min), high = values.Format(ramp.Max);
            key.Add((Wide(low) + 66 + Wide(high), (x, y) => Words(x, y, low)
                + string.Concat(Enumerable.Range(0, 5).Select(k => Swatch(x + Wide(low) + 4 + k * 12, y, $"fill='{Mix(palest, w.Style.HeatmapHigh, k / 4d)}'")))
                + Words(x + Wide(low) + 66, y, high)));
        }
        foreach (var (annotation, day) in outlined)
        {
            var text = annotation.Label ?? day.ToString("d MMM", CultureInfo.InvariantCulture);
            var colour = annotation.Color ?? w.Style.Muted;
            key.Add((14 + Wide(text), (x, y) => Swatch(x, y, $"fill='none' stroke='{colour}' stroke-width='1.5'{w.Fixed}") + Words(x + 14, y, text)));
        }
        var lines = new List<List<(double Width, Func<double, double, string> Draw)>>();
        var used = 0d;
        foreach (var item in key)
        {
            if (lines.Count == 0 || used + 14 + item.Width > s.Width - 2 * margin) { lines.Add([]); used = -14; }
            lines[^1].Add(item); used += 14 + item.Width;
        }
        var widest = lines.Count == 0 ? 0 : lines.Max(line => line.Sum(item => item.Width) + 14 * (line.Count - 1));
        var keyHeight = lines.Count == 0 ? 0 : 27 + (lines.Count - 1) * 18;

        // The pitch that fits count cells into room, a fifth of it the gap after each but the last, the gap at most 6 pixels.
        static double Fit(double room, double count, double gaps) => room / (count - .2 * gaps) is var pitch && pitch * .2 > 6 ? (room + 6 * gaps) / count : pitch;
        double pitch, gridLeft, gridBottom;
        var placed = new Dictionary<DateOnly, (double X, double Y)>();
        int Offset(DateOnly day) => ((int)day.DayOfWeek - (int)s.WeekStart + 7) % 7;
        if (s.CalendarLayout == CalendarLayout.Weeks)
        {
            // One column per week and a row per weekday; the months are named above and Mon, Wed and Fri beside their rows.
            const double head = 18, side = 30;
            var start = first.AddDays(-Offset(first));
            var weeks = (last.DayNumber - start.DayNumber) / 7 + 1;
            double across = s.Width - 2 * margin - side, down = bottom - top - head - keyHeight;
            pitch = Math.Max(.25, Math.Min(Fit(across, weeks, 1), Fit(down, 7, 1)));
            var gap = Math.Min(pitch * .2, 6);
            double width = weeks * pitch - gap, height = 7 * pitch - gap;
            gridLeft = margin + side + Math.Max(0, (across - width) / 2);
            var gridTop = top + Math.Max(0, (bottom - top - head - height - keyHeight) / 2) + head;
            gridBottom = gridTop + height;
            for (var day = first; day <= last; day = day.AddDays(1))
                placed[day] = (gridLeft + (day.DayNumber - start.DayNumber) / 7 * pitch, gridTop + Offset(day) * pitch);
            // A month is named above the week its first day falls in, or above the first week for a month already begun. Where two
            // names would touch, the later is kept: the earlier is a month the calendar shows only the end of, or too little of.
            var months = new List<(double X, string Text)>();
            for (var month = new DateOnly(first.Year, first.Month, 1); month <= last; month = month.AddMonths(1))
            {
                var text = month.ToString("MMM", CultureInfo.InvariantCulture);
                months.Add((Math.Min(placed[month < first ? first : month].X, s.Width - 4 - Wide(text)), text));
            }
            for (var k = months.Count - 2; k >= 0; k--)
                if (months[k].X + Wide(months[k].Text) + 6 > months[k + 1].X) months.RemoveAt(k);
            foreach (var (x, text) in months) w.Text(x, gridTop - 6, text, "class='lumen-muted' font-size='11'");
            if (pitch * 2 >= 12)
                for (var row = 0; row < 7; row++)
                    if ((DayOfWeek)(((int)s.WeekStart + row) % 7) is (DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday) and var named)
                        w.Text(gridLeft - 6, gridTop + row * pitch + (pitch - gap) / 2 + 4, named.ToString()[..3], "text-anchor='end' class='lumen-muted' font-size='11'");
        }
        else
        {
            // A small grid for each month, seven columns and a row per week under its name and the weekdays' initials, set left
            // to right a column apart and wrapping; as many months to a row as give the largest cells. Every month takes the rows
            // of the longest, so the months in a row line up.
            const double name = 18, initials = 14, between = 16, head = name + initials;
            var months = Enumerable.Range(0, (last.Year - first.Year) * 12 + last.Month - first.Month + 1)
                .Select(k => new DateOnly(first.Year, first.Month, 1).AddMonths(k)).ToArray();
            var depth = months.Max(month => (Offset(month) + DateTime.DaysInMonth(month.Year, month.Month) + 6) / 7);
            double across = s.Width - 2 * margin, down = bottom - top - keyHeight;
            var (best, perRow) = (0d, 1);
            for (var k = 1; k <= months.Length; k++)
            {
                var rows = (months.Length + k - 1) / k;
                var fit = Math.Min(Fit(across, 8 * k - 1, 1), Fit(down - rows * head - (rows - 1) * between, depth * rows, rows));
                if (fit > best) (best, perRow) = (fit, k);
            }
            pitch = Math.Max(.25, best);
            var gap = Math.Min(pitch * .2, 6);
            var block = 7 * pitch - gap;
            var bands = (months.Length + perRow - 1) / perRow;
            double width = (perRow - 1) * 8 * pitch + block, height = bands * (head + depth * pitch - gap) + (bands - 1) * between;
            gridLeft = margin + Math.Max(0, (across - width) / 2);
            var gridTop = top + Math.Max(0, (bottom - top - height - keyHeight) / 2);
            gridBottom = gridTop + height;
            var years = first.Year != last.Year;
            for (var i = 0; i < months.Length; i++)
            {
                var month = months[i];
                double bx = gridLeft + i % perRow * 8 * pitch, by = gridTop + i / perRow * (head + depth * pitch - gap + between);
                var title = month.ToString(years ? "MMMM yyyy" : "MMMM", CultureInfo.InvariantCulture);
                if (Wide(title) * 12 / 11 > block + pitch) title = month.ToString(years ? "MMM yyyy" : "MMM", CultureInfo.InvariantCulture);
                w.Text(bx, by + 13, title, "font-weight='600'");
                if (pitch >= 9)
                    for (var c = 0; c < 7; c++)
                        w.Text(bx + c * pitch + (pitch - gap) / 2, by + name + 10, ((DayOfWeek)(((int)s.WeekStart + c) % 7)).ToString()[..1], "text-anchor='middle' class='lumen-muted' font-size='10'");
                var offset = Offset(month);
                for (var day = month; day.Month == month.Month; day = day.AddDays(1))
                    if (day >= first && day <= last)
                        placed[day] = (bx + (offset + day.Day - 1) % 7 * pitch, by + head + (offset + day.Day - 1) / 7 * pitch);
            }
        }
        var spacing = Math.Min(pitch * .2, 6);
        var cell = pitch - spacing;
        var radius = Math.Min(w.Style.BarRadius ?? 3, cell / 2);
        string Square(double x, double y, double size, double corner, string paint) =>
            $"<rect x='{N(x)}' y='{N(y)}' width='{N(size)}' height='{N(size)}' rx='{N(corner)}' {paint}/>";
        string Circle(double x, double y, double r, string paint) => $"<circle cx='{N(x + cell / 2)}' cy='{N(y + cell / 2)}' r='{N(r)}' {paint}/>";
        string Track(double x, double y) => round ? Circle(x, y, cell / 2, $"class='lumen-track' fill='{w.Style.Grid}'") : Square(x, y, cell, radius, $"class='lumen-track' fill='{w.Style.Grid}'");
        // A ramp's days take the heatmap's hairline, which is their own stroke, so a shape that draws nothing carries the focus ring.
        var hairline = zones is null ? $" stroke='var(--lumen-muted)' stroke-opacity='.4'{w.Fixed}" : "";
        foreach (var (day, (x, y)) in placed.OrderBy(d => d.Key))
        {
            if (!active.TryGetValue(day, out var entry)) { w.Add(Track(x, y)); continue; }
            var ink = Ink(entry.Total);
            var shape = s.CalendarCell switch
            {
                CalendarCell.Square => Square(x, y, cell, radius, $"class='lumen-day' fill='{ink}'{hairline}") + (zones is null ? Square(x, y, cell, radius, $"fill='none'{w.Fixed}") : ""),
                CalendarCell.Dot => Circle(x, y, cell / 2, $"class='lumen-day' fill='{ink}'{hairline}") + (zones is null ? Circle(x, y, cell / 2, $"fill='none'{w.Fixed}") : ""),
                _ => Track(x, y) + Circle(x, y, cell / 2 * Math.Sqrt(entry.Total / largest), $"class='lumen-day' fill='{ink}'{(zones is null ? hairline : " stroke='none'")}")
            };
            var label = $"{Date(day)}{(entry.Labels.Count > 0 ? ", " + string.Join(", ", entry.Labels) : "")}: {values.Format(entry.Total)}"
                + (zones is null ? "" : $", {zones.Zones[zones.IndexOf(entry.Total)].Name}");
            Datum(w, 0, entry.Point, label, shape);
        }
        // An outline round each annotated day, in the gap between it and its neighbours.
        foreach (var (annotation, day) in outlined)
        {
            var (x, y) = placed[day];
            var paint = $"class='lumen-outline' fill='none' stroke='{annotation.Color ?? w.Style.Muted}' stroke-width='2'{w.Fixed}";
            Aggregate(w, annotation.Label is null ? Date(day) : $"{annotation.Label}: {Date(day)}",
                round ? Circle(x, y, (cell + spacing) / 2, paint) : Square(x - spacing / 2, y - spacing / 2, cell + spacing, radius + spacing / 2, paint));
        }
        var left = Math.Max(margin, Math.Min(gridLeft, s.Width - margin - widest));
        for (var j = 0; j < lines.Count; j++)
        {
            var x = left;
            foreach (var (width, draw) in lines[j]) { w.Add(draw(x, gridBottom + 24 + j * 18)); x += width + 14; }
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
        YBounds(w, s, ys, left, right, top, bottom);
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
        var (left, right) = Across(s); double top = 78 + w.Head, bottom = s.Height - 76 - w.Foot;
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
        w.Text(right, 64 + w.Head, several ? $"{total} observations across {s.Series.Count} series in {bins.Count} shared equal-width bins" : $"{total} observations in {bins.Count} equal-width bins",
            "text-anchor='end' class='lumen-muted' font-size='11'");
    }

    private static void Box(SvgWriter w, ChartSpec s)
    {
        var observations = s.Series.Select(series => series.Points.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToArray()).ToArray();
        var (left, right) = Across(s); double top = 78 + w.Head, bottom = s.Height - 76 - w.Foot;
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
        var (left, right) = Across(s); double top = 78 + w.Head, bottom = s.Height - 76 - w.Foot;
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

    /// <summary>
    /// One score on an open arc, drawn in a group whose origin is the arc's centre. The arc runs <see cref="ChartSpec.GaugeSweep"/>
    /// degrees clockwise and is centred at twelve o'clock, so the scale's minimum stands at minus half the sweep, its maximum at
    /// plus half, and a value's angle is linear between them; a score off the scale stands at the end it passed, and its name
    /// says so. The track is the grid colour, tinted by any zones; the score's arc takes its zone's colour, a gradient along
    /// its length, or its series colour, and ends in a knob. The arc is as large as fits between the title and the source line
    /// with its end labels under it, its thickness .16 of its radius.
    /// </summary>
    private static void Gauge(SvgWriter w, ChartSpec s)
    {
        var series = s.Series[0]; var point = series.Points[0]; var value = point.Y!.Value;
        var scale = Radial(s);
        double min = scale.Min, max = scale.Max, sweep = s.GaugeSweep, start = -sweep / 2;
        double Angle(double v) => start + sweep * (Math.Clamp(v, min, max) - min) / (max - min);
        const double ratio = .16;
        // A target's label over the upper half of the arc stands above it, so the arc starts lower to leave it room.
        var top = (s.Annotations.Any(annotation => Math.Abs(Angle(annotation.From)) < 70) ? 78d : 64d) + w.Head;
        var bottom = s.Height - 30d - w.Foot;
        // How far below the centre the arc's ends reach, in radii; under them go the end labels.
        var drop = Math.Max(0, -Math.Cos(sweep / 2 * Math.PI / 180));
        var radius = Math.Min((s.Width - 48d) / (2 + ratio), (bottom - top - 18) / (1 + ratio + drop));
        var thick = radius * ratio;
        double outer = radius + thick / 2, inner = radius - thick / 2;
        double cx = s.Width / 2d, cy = top + outer + (bottom - top - (outer + drop * radius + thick / 2 + 18)) / 2;
        w.Add($"<g class='lumen-gauge' transform='translate({R(cx)} {R(cy)})'>");
        w.Add($"<path class='lumen-gauge-track' d='{Band(inner, outer, start, -start)}' fill='{w.Style.Grid}'/>");
        // Each zone tints the stretch of track it covers, clipped to the scale, and is named with its own range.
        var zones = s.YZones;
        if (zones is not null)
            for (var i = 0; i < zones.Zones.Count; i++)
            {
                double? lower = i > 0 ? zones.Zones[i - 1].Upper : null, upper = i < zones.Zones.Count - 1 ? zones.Zones[i].Upper : null;
                double from = Math.Max(lower ?? min, min), to = Math.Min(upper ?? max, max);
                if (to <= from) continue;
                Aggregate(w, $"{zones.Zones[i].Name}: {Range(lower, upper, scale)}",
                    $"<path class='lumen-gauge-zone' d='{Band(inner, outer, Angle(from), Angle(to), from == min, to == max)}' fill='{ZoneColor(w.Style, zones, i)}' fill-opacity='.3'/>");
            }
        var end = Angle(value);
        var zone = zones is null ? null : zones.Zones[zones.IndexOf(value)].Name;
        var shape = new StringBuilder();
        if (series.Gradient is { } stops && end > start)
        {
            // A gradient is laid along the arc in pieces of two degrees at most, each in the colour of the value at its middle and
            // reaching a little into the next, so that no seam shows between them.
            var pieces = (int)Math.Ceiling((end - start) / 2);
            var step = (end - start) / pieces;
            for (var k = 0; k < pieces; k++)
            {
                double from = start + k * step, to = k == pieces - 1 ? end : from + step + Math.Min(.4, step / 2);
                var middle = min + (from + step / 2 - start) / sweep * (max - min);
                shape.Append($"<path class='lumen-gauge-value' d='{Band(inner, outer, from, to, k == 0, k == pieces - 1)}' fill='{Blend(stops, middle)}' stroke='none'/>");
            }
        }
        else
        {
            var ink = series.Gradient is { } gradient ? Blend(gradient, min) : zones is not null ? ZoneColor(w.Style, zones, zones.IndexOf(value)) : SeriesColor(series, 0, w.Style);
            shape.Append($"<path class='lumen-gauge-value' d='{Band(inner, outer, start, end)}' fill='{ink}' stroke='none'/>");
        }
        var (kx, ky) = Polar(radius, end);
        // A knob in the background colour marks the score; the arc's outline draws nothing until the score is focused, when it
        // takes the focus ring.
        shape.Append($"<circle class='lumen-gauge-knob' cx='{N(kx)}' cy='{N(ky)}' r='{R(thick * .3)}' fill='{w.Style.Background}' stroke='none'/><path d='{Band(inner, outer, start, end)}' fill='none'/>");
        var named = $"{point.Label ?? series.Name}: {scale.Format(value)}{Unit(s.YLabel)}"
            + (value > max ? $", above the scale, drawn at {scale.Format(max)}" : value < min ? $", below the scale, drawn at {scale.Format(min)}" : "")
            + (zone is null ? "" : $", {zone}");
        Datum(w, 0, 0, named, shape.ToString());
        // The score in the centre, in the scale's format, its unit at half its size; the caption and the zone under it. The
        // block is centred on the arc's centre unless the ends do not reach far enough below it, as on a semicircle, where it
        // rises to stand on the line between them. Every text set here is kept as a box, so that targets' labels keep clear.
        var taken = new List<(double X1, double Y1, double X2, double Y2)>();
        var number = scale.Format(value); var unit = Unit(s.YLabel).TrimStart();
        var ems = Wide(number) / 11 + (unit.Length > 0 ? .1 + Wide(unit) / 11 * .5 : 0);
        var size = Math.Round(Math.Clamp(Math.Min(radius * .42, inner * 1.5 / ems), 12, 72), 1);
        var block = size * .72 + (point.Label is null ? 0 : 20) + (zone is null ? 0 : 17);
        var baseline = -Math.Max(0, block / 2 - drop * radius * .8) - block / 2 + size * .72;
        w.Add($"<text x='0' y='{R(baseline)}' text-anchor='middle' font-size='{N(size)}' font-weight='600'>{SvgWriter.E(number)}" +
            (unit.Length > 0 ? $"<tspan font-size='{N(Math.Round(size * .5, 1))}' dx='{N(Math.Round(size * .02, 1))}'>{SvgWriter.E(unit)}</tspan>" : "") + "</text>");
        var across = Math.Max(ems * size, Math.Max(Wide(point.Label ?? "") * 13 / 11, Wide(zone ?? "") * 12 / 10));
        taken.Add((-across / 2, baseline - size * .72, across / 2, baseline - size * .72 + block + 3));
        var line = baseline;
        if (point.Label is not null) w.Text(0, Math.Round(line += 20, 4), point.Label, "text-anchor='middle' class='lumen-muted' font-size='13'");
        if (zone is not null) w.Text(0, Math.Round(line + (point.Label is null ? 20 : 17), 4), zone, "text-anchor='middle' font-size='12' font-weight='600'");
        // The scale's ends under the arc's ends, set either side of them where the two ends nearly meet, as on a full circle.
        var (sx, sy) = Polar(radius, start); var (ex, ey) = Polar(radius, -start);
        var close = ex - sx < 70;
        void End(double x, double y, string text, string anchor)
        {
            w.Text(x, y, text, $"text-anchor='{anchor}' class='lumen-muted' font-size='11'");
            var left = x - (anchor == "end" ? Wide(text) : anchor == "middle" ? Wide(text) / 2 : 0);
            taken.Add((left, y - 9, left + Wide(text), y + 3));
        }
        End(close ? sx - 4 : sx, Math.Round(sy + thick / 2 + 14, 4), scale.Format(min), close ? "end" : "middle");
        End(close ? ex + 4 : ex, Math.Round(ey + thick / 2 + 14, 4), scale.Format(max), close ? "start" : "middle");
        // A target is a tick across the arc over the score, haloed so it shows on any colour. Its label goes outside the arc,
        // within the drawing and clear of the description; or else inside it, as near the arc as it fits whole; and in either
        // place clear of every text already set. A label with no such place is left out, as a reference label is, and the
        // tick keeps its name for the tooltip and assistive technology.
        bool Free((double X1, double Y1, double X2, double Y2) box) => taken.All(t => box.X2 + 2 < t.X1 || box.X1 - 2 > t.X2 || box.Y2 + 2 < t.Y1 || box.Y1 - 2 > t.Y2);
        foreach (var annotation in s.Annotations)
        {
            var angle = Angle(annotation.From);
            var colour = annotation.Color ?? w.Style.Muted;
            var name = annotation.Label is null ? scale.Format(annotation.From) : $"{annotation.Label}: {scale.Format(annotation.From)}";
            // The tick's name always reads its value; the label drawn leaves it out where the annotation shows its label alone.
            var text = annotation.ShowValue ? name : annotation.Label!;
            var (x1, y1) = Polar(inner - 5, angle); var (x2, y2) = Polar(outer + 5, angle);
            string Tick(string ink, string width) => $"<line class='lumen-gauge-target' x1='{N(x1)}' y1='{N(y1)}' x2='{N(x2)}' y2='{N(y2)}' stroke='{ink}' stroke-width='{width}' stroke-linecap='round'{w.Fixed}/>";
            Aggregate(w, name, Tick(w.Style.Background, "5") + Tick(colour, "2"));
            var wide = Wide(text);
            // Near the top or the bottom of the arc a label is centred on its tick; elsewhere it runs away from the tick, outward
            // outside the arc and toward the middle inside it.
            (double X, double Y, string Anchor, (double X1, double Y1, double X2, double Y2) Box) Place(double distance, bool outside)
            {
                var (x, y) = Polar(distance, angle);
                var level = Math.Abs(x) < wide / 2;
                var anchor = level ? "middle" : x > 0 == outside ? "start" : "end";
                var baseline = level ? y + (y < 0 == outside ? -3 : 12) : y + 4;
                var left = x - (anchor == "start" ? 0 : anchor == "end" ? wide : wide / 2);
                return (x, baseline, anchor, (left, baseline - 9, left + wide, baseline + 3));
            }
            var label = Place(outer + 9, true);
            var placed = cx + label.Box.X1 >= 8 && cx + label.Box.X2 <= s.Width - 8 && cy + label.Box.Y1 >= 56 + w.Head && Free(label.Box);
            for (var distance = inner - 9; !placed && distance > inner / 3; distance -= 4)
            {
                label = Place(distance, false);
                var (lx1, ly1, lx2, ly2) = label.Box;
                placed = new[] { (lx1, ly1), (lx2, ly1), (lx1, ly2), (lx2, ly2) }.All(c => double.Hypot(c.Item1, c.Item2) < inner - 3) && Free(label.Box);
            }
            if (!placed) continue;
            taken.Add(label.Box);
            w.Add($"<text x='{R(label.X)}' y='{R(label.Y)}' text-anchor='{label.Anchor}' fill='{colour}' font-size='11' stroke='{w.Style.Background}' stroke-width='3' stroke-linejoin='round' paint-order='stroke' pointer-events='none' aria-hidden='true'>{SvgWriter.E(text)}</text>");
        }
        w.Add("</g>");
    }

    /// <summary>
    /// Concentric progress rings in a group whose origin is their centre, the first series outermost. A ring's progress is its
    /// value over its goal, and runs clockwise from twelve o'clock, 360 degrees to the goal, over a track of its own colour at
    /// a fifth of its strength. The rings fill a circle as wide as the smaller of the room across and down; each is .84 of the
    /// pitch between two, the pitch a fifth of the radius and a little more, or less for more than three rings, so the gaps
    /// stay even and the middle open. Past its goal a ring lies whole, and its leading end is drawn again over the lap beneath,
    /// with a soft shadow just ahead of it, so the overlap reads. It stops at three laps and its name says so.
    /// </summary>
    private static void Rings(SvgWriter w, ChartSpec s)
    {
        double top = 64 + w.Head, bottom = s.Height - 30d - w.Foot;
        var outer = Math.Min(s.Width - 48d, bottom - top) / 2;
        var pitch = Math.Min(outer * .22, outer * .72 / s.Series.Count);
        var thick = pitch * .84;
        var scale = Radial(s);
        w.Add($"<g class='lumen-rings' transform='translate({R(s.Width / 2d)} {R((top + bottom) / 2)})'>");
        for (var i = 0; i < s.Series.Count; i++)
        {
            var series = s.Series[i]; var point = series.Points[0]; var value = point.Y!.Value; var goal = series.Goal ?? 100;
            var color = SeriesColor(series, i, w.Style);
            var centre = outer - thick / 2 - i * pitch;
            double ro = centre + thick / 2, ri = centre - thick / 2;
            var progress = value / goal;
            var end = 360 * Math.Min(progress, 3);
            var shape = new StringBuilder($"<path class='lumen-ring-track' d='{Annulus(ri, ro)}' fill='{color}' fill-opacity='.2' stroke='none'/>");
            if (end > 360)
            {
                shape.Append($"<path class='lumen-ring-progress' d='{Annulus(ri, ro)}' fill='{color}' stroke='none'/>");
                // Three discs the width of the ring, a little further ahead of the leading end each, darken the lap beneath into a
                // soft edge.
                foreach (var (ahead, opacity) in new[] { (.42, ".06"), (.28, ".1"), (.14, ".14") })
                {
                    var (x, y) = Polar(centre, end + ahead * thick / centre * 180 / Math.PI);
                    shape.Append($"<circle class='lumen-ring-shadow' cx='{N(x)}' cy='{N(y)}' r='{R(thick / 2)}' fill='#000000' fill-opacity='{opacity}' stroke='none'/>");
                }
                shape.Append($"<path class='lumen-ring-lead' d='{Band(ri, ro, end - 90, end, false, true)}' fill='{color}' stroke='none'/>");
            }
            else if (end > 0) shape.Append($"<path class='lumen-ring-progress' d='{Band(ri, ro, 0, end)}' fill='{color}' stroke='none'/>");
            // The ring's outline draws nothing until it is focused, when it takes the focus ring.
            shape.Append($"<path d='{Annulus(ri, ro)}' fill='none'/>");
            var percent = Math.Round(progress * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
            Datum(w, i, 0, $"{series.Name}: {scale.Format(value)} of {scale.Format(goal)}{Unit(point.Label)}, {percent} %{(progress > 3 ? ", drawn at 300 %" : "")}", shape.ToString());
        }
        w.Add("</g>");
    }

    /// <summary>A point <paramref name="radius"/> from a radial chart's centre at <paramref name="degrees"/> clockwise from twelve
    /// o'clock, rounded to four places so that no coordinate reads -0.</summary>
    private static (double X, double Y) Polar(double radius, double degrees)
    {
        var angle = degrees * Math.PI / 180;
        return (Math.Round(radius * Math.Sin(angle), 4) + 0d, Math.Round(-radius * Math.Cos(angle), 4) + 0d);
    }
    private static string At(double radius, double degrees) { var (x, y) = Polar(radius, degrees); return $"{N(x)},{N(y)}"; }
    /// <summary>A length rounded to four places, so that no coordinate reads -0.</summary>
    private static string R(double n) => N(Math.Round(n, 4) + 0d);
    /// <summary>Arcs round a circle of <paramref name="radius"/> from one angle to another, continuing a path from the first,
    /// clockwise when the second is the larger, in pieces of a quarter turn at most so that no arc's flags are in doubt.</summary>
    private static string Sweep(double radius, double from, double to)
    {
        if (to == from) return "";
        var pieces = (int)Math.Ceiling(Math.Abs(to - from) / 90 - 1e-9);
        var arcs = new StringBuilder();
        for (var k = 1; k <= pieces; k++)
            arcs.Append($" A{R(radius)},{R(radius)} 0 0 {(to > from ? 1 : 0)} {At(radius, from + (to - from) * k / pieces)}");
        return arcs.ToString();
    }
    /// <summary>A closed band between two radii from one angle clockwise to another, each end square or, where asked, rounded
    /// into a semicircle beyond it, as a round line cap is.</summary>
    private static string Band(double inner, double outer, double from, double to, bool roundFrom = true, bool roundTo = true)
    {
        var half = R((outer - inner) / 2);
        return $"M{At(outer, from)}{Sweep(outer, from, to)}{(roundTo ? $" A{half},{half} 0 0 1 " : " L")}{At(inner, to)}{Sweep(inner, to, from)}" +
            $"{(roundFrom ? $" A{half},{half} 0 0 1 {At(outer, from)}" : "")} Z";
    }
    /// <summary>A whole ring between two radii: the outer circle clockwise and the inner one back, so the middle stays empty.</summary>
    private static string Annulus(double inner, double outer) => $"M{At(outer, 0)}{Sweep(outer, 0, 360)} Z M{At(inner, 0)}{Sweep(inner, 360, 0)} Z";
    /// <summary>The colour a gradient takes at <paramref name="value"/>: its first stop's below the first, its last's above the
    /// last, and between two stops a blend of theirs.</summary>
    private static string Blend(IReadOnlyList<ColorStop> stops, double value)
    {
        if (value <= stops[0].Value) return stops[0].Color;
        for (var i = 1; i < stops.Count; i++)
            if (value <= stops[i].Value) return Mix(stops[i - 1].Color, stops[i].Color, (value - stops[i - 1].Value) / (stops[i].Value - stops[i - 1].Value));
        return stops[^1].Color;
    }

    private static void Donut(SvgWriter w, ChartSpec s)
    {
        var series = s.Series[0]; var total = series.Points.Sum(p => p.Y ?? 0);
        if (total <= 0) { w.Text(s.Width / 2, s.Height / 2, "No positive values", "text-anchor='middle'"); return; }
        // A description or a source on two lines takes its 14 pixels from the height the ring and its key are laid out in.
        var cx = s.Width * .35; var cy = w.Head + (s.Height - w.Head - w.Foot + 30) / 2d;
        var r = Math.Min(s.Width * .23, (s.Height - w.Head - w.Foot - 140) / 2d); var inner = r * .67;
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
            Datum(w, 0, i, $"{p.Label ?? LinearScale.Label(p.X)}: {LinearScale.Label(p.Y.Value)}{p.ValueNote} ({p.Y / total:P1})", $"<path d='{path}' fill='{color}'/>");
            if (i < 10)
            {
                var ly = 95 + w.Head + i * 25;
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
        // A description or a source on two lines takes its 14 pixels from the rows' height.
        var cw = (s.Width - 165d) / cats.Length; var ch = (s.Height - w.Head - w.Foot - 160d) / s.Series.Count;
        for (var si = 0; si < s.Series.Count; si++)
        {
            w.Text(118, 80 + w.Head + (si + .5) * ch + 4, Short(s.Series[si].Name,17), "text-anchor='end' class='lumen-muted'");
            for (var pi = 0; pi < s.Series[si].Points.Count; pi++)
            {
                var p = s.Series[si].Points[pi]; if (!p.Y.HasValue) continue;
                var x = 130 + Array.IndexOf(cats,p.X)*cw;
                var t = scale.Map(p.Y.Value, 0, 1);
                var color = Mix(w.Style.HeatmapLow, w.Style.HeatmapHigh, t);
                // A hairline keeps the palest cells distinguishable from the chart background.
                Datum(w,si,pi,PointLabel(s.Series[si],p),$"<rect x='{N(x+1)}' y='{N(80+w.Head+si*ch+1)}' width='{N(Math.Max(0,cw-2))}' height='{N(Math.Max(0,ch-2))}' rx='3' fill='{color}' stroke='var(--lumen-muted)' stroke-opacity='.4'{w.Fixed}/>");
            }
        }
        for (var i = 0; i < cats.Length; i += Math.Max(1,(int)Math.Ceiling(cats.Length/12d)))
            w.Text(130+(i+.5)*cw, s.Height-w.Foot-62, Short(s.Series.SelectMany(x=>x.Points).First(p=>p.X==cats[i]).Label ?? LinearScale.Label(cats[i]),10), "text-anchor='middle' class='lumen-muted'");
        w.Text(130, s.Height-w.Foot-36, $"Color scale: {LinearScale.Label(scale.Min)} (light) to {LinearScale.Label(scale.Max)} (dark)", "class='lumen-muted'");
    }

    private static void Radar(SvgWriter w, ChartSpec s)
    {
        var cats = s.Series.SelectMany(x=>x.Points).Select(p=>p.X).Distinct().Order().ToArray();
        if (cats.Length < 3) throw new ArgumentException("Radar charts require at least three categories.");
        var max = Math.Max(1,s.Series.SelectMany(x=>x.Points).Max(p=>p.Y ?? 0));
        // A description or a source on two lines takes its 14 pixels from the height the web is laid out in.
        var cx=s.Width/2d; var cy=w.Head+(s.Height-w.Head-w.Foot+32)/2d; var r=(s.Height-w.Head-w.Foot-180)/2d;
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
        w.Text(24,s.Height-w.Foot-38,$"Radial scale: 0 to {LinearScale.Label(max)}","class='lumen-muted'");
    }
    /// <summary>Linear interpolation per channel, truncated, which is how the heatmap ramp has always been computed.</summary>
    private static string Mix(string low, string high, double t)
    {
        int Channel(string hex, int offset) => int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int Blend(int offset) => (int)(Channel(low, offset) + (Channel(high, offset) - Channel(low, offset)) * t);
        return $"#{Blend(1):X2}{Blend(3):X2}{Blend(5):X2}";
    }
    /// <summary>Where a calendar's ramp starts: a third of the way from an empty day's grid colour to the heatmap's high end.</summary>
    internal static string CalendarLow(ChartStyle style) => Mix(style.Grid, style.HeatmapHigh, 1 / 3d);
    internal static string Short(string text,int max) => text.Length <= max ? text : text[..(max-1)] + "…";
}

