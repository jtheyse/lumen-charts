using System.Globalization;

namespace Lumen.Charts;

/// <summary>How the gridlines of a chart drawn on X and Y axes are stroked. <see cref="Hidden"/> leaves them out and keeps
/// the tick labels.</summary>
public enum GridLine
{
    /// <summary>Unbroken lines, which the classic finish draws by default.</summary>
    Solid,
    /// <summary>Dotted hairlines, which the refined finish draws by default.</summary>
    Dotted,
    /// <summary>Dashed lines.</summary>
    Dashed,
    /// <summary>No gridlines.</summary>
    Hidden
}

/// <summary>
/// How a chart is drawn beyond its colours. <see cref="Refined"/> is the default: thin strokes that keep their width at any
/// display size, line markers that appear when a point is hovered or focused, hairline dotted gridlines, legend keys shaped
/// like their marks, ticks spaced to the room they have, and reference labels kept legible and inside the plot.
/// <see cref="Classic"/> draws exactly as 0.23.0 did, byte for byte.
/// </summary>
public enum ChartFinish
{
    /// <summary>The default finish.</summary>
    Refined,
    /// <summary>The finish of 0.23.0 and earlier.</summary>
    Classic
}

/// <summary>
/// Every colour and the typeface a chart draws with. Set it on a <see cref="ChartSpec"/> or
/// <see cref="GraphSpec"/> to render a host application's brand into the SVG itself, so exported
/// files and the HTTP API carry the brand as well as the page. Colours are <c>#RRGGBB</c>.
/// </summary>
public sealed record ChartStyle
{
    private static readonly IReadOnlyList<string> DefaultSeries =
        Array.AsReadOnly(new[] { "#5675E7", "#169B8D", "#B87F44", "#A775C8", "#D36B84", "#4F93AD" });
    // Grey, blue, green, gold, orange, red and purple, as training apps order their zones. Every entry clears 3:1
    // against both preset backgrounds, so the light and dark presets share one ramp.
    private static readonly IReadOnlyList<string> DefaultZones =
        Array.AsReadOnly(new[] { "#848484", "#3F87D9", "#2E9B58", "#A88200", "#DB6A1F", "#DD4B45", "#9E63D3" });

    /// <summary>The chart's background, drawn into the SVG so that exports keep it.</summary>
    public string Background { get; init; } = "#FFFFFF";
    /// <summary>Titles, values and other primary text.</summary>
    public string Text { get; init; } = "#26324B";
    /// <summary>Axis labels, captions and other secondary text.</summary>
    public string Muted { get; init; } = "#63718A";
    /// <summary>Gridlines, and a radar's rings and spokes.</summary>
    public string Grid { get; init; } = "#E8EDF5";
    /// <summary>Graph edges and their arrowheads.</summary>
    public string Edge { get; init; } = "#8090AD";
    /// <summary>Series colours in order. A series with its own colour keeps it.</summary>
    public IReadOnlyList<string> Series { get; init; } = DefaultSeries;
    /// <summary>Zone colours from low intensity to high. A zone without its own colour takes the entry at its
    /// position in its scale, so Coggan's seven power levels use all seven and his five heart-rate levels the first five.</summary>
    public IReadOnlyList<string> Zones { get; init; } = DefaultZones;
    /// <summary>Candles and OHLC bars that close at or above their open.</summary>
    public string Rising { get; init; } = "#169B8D";
    /// <summary>Candles and OHLC bars that close below their open.</summary>
    public string Falling { get; init; } = "#D36B84";
    /// <summary>Heatmap cells are interpolated from this colour at the lowest value…</summary>
    public string HeatmapLow { get; init; } = "#E4EDFC";
    /// <summary>…to this one at the highest.</summary>
    public string HeatmapHigh { get; init; } = "#4069D0";
    /// <summary>A CSS font-family list without quotes, such as <c>Inter, Segoe UI, sans-serif</c>.</summary>
    public string FontFamily { get; init; } = "Segoe UI,Arial,sans-serif";
    /// <summary>The horizontal and vertical gridlines of a chart drawn on X and Y axes, major and minor. A radar's rings
    /// and spokes are its scale and stay solid. A style that sets none draws them dotted in the refined finish, so every
    /// preset and brand reads <see cref="GridLine.Dotted"/>, and solid in the classic finish, as 0.23.0 did.</summary>
    public GridLine Gridlines { get => gridlines ?? (Finish == ChartFinish.Classic ? GridLine.Solid : GridLine.Dotted); init => gridlines = value; }
    private readonly GridLine? gridlines;
    /// <summary>The refined finish, the default, or the classic one, which draws exactly as 0.23.0 did.</summary>
    public ChartFinish Finish { get; init; }
    /// <summary>Rounds the far end of every column and bar, the end away from its baseline, which is the bottom of a negative
    /// column; the baseline end stays square. It is clamped to half the bar's width, where the end is a semicircle, and to
    /// the bar's length, so a large radius draws capsules. A stacked column rounds only its outermost segment. Null keeps
    /// the 2 px corners all round.</summary>
    public double? BarRadius { get; init; }

    /// <summary>The default look, identical to <see cref="ChartTheme.Light"/>.</summary>
    public static ChartStyle Light { get; } = new();
    /// <summary>Identical to <see cref="ChartTheme.Dark"/>.</summary>
    public static ChartStyle Dark { get; } = new() { Background = "#171E2E", Text = "#E8ECF6", Muted = "#AAB8CF", Grid = "#303B50" };
    /// <summary>
    /// A near-black chart with vivid colours, dotted gridlines and capsule bars, in the manner of WHOOP and Oura. Every
    /// series, zone, candle and edge colour clears 3:1 against the background, and both text colours clear 4.5:1.
    /// </summary>
    public static ChartStyle Midnight { get; } = new()
    {
        Background = "#0B0E14", Text = "#F3F5F9", Muted = "#9BA4B5", Grid = "#353C49", Edge = "#7D879A",
        Series = Array.AsReadOnly(new[] { "#4C9DFF", "#2FE0A0", "#FFC23D", "#FF5D8F", "#B18CFF", "#2CD3F0" }),
        Zones = Array.AsReadOnly(new[] { "#8E97A8", "#4C9DFF", "#36D27A", "#F5C518", "#FF8A3D", "#FF5A5A", "#C38BFF" }),
        Rising = "#2FE0A0", Falling = "#FF5D6E", HeatmapLow = "#1A2638", HeatmapHigh = "#4C9DFF",
        Gridlines = GridLine.Dotted, BarRadius = 9999
    };

    /// <summary>The palette colour for the series at <paramref name="index"/>, the palette repeating when there are more
    /// series than colours.</summary>
    public string SeriesColor(int index) => Series[index % Series.Count];

    /// <summary>
    /// Turns a CSS <c>font-family</c> value into the quote-free list a style accepts, keeping only letters,
    /// digits, spaces and hyphens in each family. Returns null when nothing usable remains.
    /// </summary>
    public static string? FontFamilyFrom(string? css)
    {
        if (string.IsNullOrWhiteSpace(css)) return null;
        var families = css.Split(',')
            .Select(family => string.Join(' ', new string(family.Where(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-').ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(family => family.Length > 0);
        var list = string.Join(",", families);
        if (list.Length == 0) return null;
        return list.Length <= 200 ? list : list[..200].TrimEnd(',', ' ', '-');
    }

    /// <summary>
    /// Colour pairs below the WCAG 2.1 minimum for this style: 4.5:1 for text, 3:1 for marks, zones and edges.
    /// A brand palette is the most likely source of an inaccessible chart, so check it before shipping.
    /// </summary>
    public IReadOnlyList<ContrastIssue> ContrastIssues()
    {
        var issues = new List<ContrastIssue>();
        void Require(string element, string foreground, double minimum)
        {
            var ratio = Contrast.Ratio(foreground, Background);
            if (ratio < minimum) issues.Add(new(element, foreground, Background, Math.Round(ratio, 2), minimum));
        }
        Require("Text", Text, 4.5);
        Require("Muted text", Muted, 4.5);
        for (var i = 0; i < Series.Count; i++) Require($"Series {i + 1}", Series[i], 3);
        for (var i = 0; i < Zones.Count; i++) Require($"Zone {i + 1}", Zones[i], 3);
        Require("Rising candles", Rising, 3);
        Require("Falling candles", Falling, 3);
        Require("Graph edges", Edge, 3);
        return issues;
    }
}

/// <summary>A colour pair below the WCAG 2.1 minimum, as <see cref="ChartStyle.ContrastIssues"/> reports it.</summary>
/// <param name="Element">What the colour is used for, such as <c>Series 3</c> or <c>Muted text</c>.</param>
/// <param name="Foreground">The colour that falls short.</param>
/// <param name="Background">The background it falls short against.</param>
/// <param name="Ratio">The contrast the pair reaches, rounded to two decimals.</param>
/// <param name="Required">The contrast it needs: 4.5 for text, 3 for marks, zones and edges.</param>
public sealed record ContrastIssue(string Element, string Foreground, string Background, double Ratio, double Required);

/// <summary>WCAG 2.1 relative-luminance contrast.</summary>
public static class Contrast
{
    /// <summary>The contrast ratio of two <c>#RRGGBB</c> colours, from 1 to 21, whichever order they come in.</summary>
    public static double Ratio(string first, string second)
    {
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static double Luminance(string hex) =>
        .2126 * Channel(hex, 1) + .7152 * Channel(hex, 3) + .0722 * Channel(hex, 5);

    private static double Channel(string hex, int offset)
    {
        var value = int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return value <= .03928 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
    }
}
