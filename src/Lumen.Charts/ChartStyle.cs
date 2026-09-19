using System.Globalization;

namespace Lumen.Charts;

/// <summary>
/// Every colour and the typeface a chart draws with. Set it on a <see cref="ChartSpec"/> or
/// <see cref="GraphSpec"/> to render a host application's brand into the SVG itself, so exported
/// files and the HTTP API carry the brand as well as the page. Colours are <c>#RRGGBB</c>.
/// </summary>
public sealed record ChartStyle
{
    private static readonly IReadOnlyList<string> DefaultSeries =
        Array.AsReadOnly(new[] { "#5675E7", "#169B8D", "#B87F44", "#A775C8", "#D36B84", "#4F93AD" });

    public string Background { get; init; } = "#FFFFFF";
    public string Text { get; init; } = "#26324B";
    /// <summary>Axis labels, captions and other secondary text.</summary>
    public string Muted { get; init; } = "#63718A";
    public string Grid { get; init; } = "#E8EDF5";
    /// <summary>Graph edges and their arrowheads.</summary>
    public string Edge { get; init; } = "#8090AD";
    /// <summary>Series colours in order. A series with its own colour keeps it.</summary>
    public IReadOnlyList<string> Series { get; init; } = DefaultSeries;
    public string Rising { get; init; } = "#169B8D";
    public string Falling { get; init; } = "#D36B84";
    /// <summary>Heatmap cells are interpolated from this colour at the lowest value…</summary>
    public string HeatmapLow { get; init; } = "#E4EDFC";
    /// <summary>…to this one at the highest.</summary>
    public string HeatmapHigh { get; init; } = "#4069D0";
    /// <summary>A CSS font-family list without quotes, such as <c>Inter, Segoe UI, sans-serif</c>.</summary>
    public string FontFamily { get; init; } = "Segoe UI,Arial,sans-serif";

    /// <summary>The default look, identical to <see cref="ChartTheme.Light"/>.</summary>
    public static ChartStyle Light { get; } = new();
    /// <summary>Identical to <see cref="ChartTheme.Dark"/>.</summary>
    public static ChartStyle Dark { get; } = new() { Background = "#171E2E", Text = "#E8ECF6", Muted = "#AAB8CF", Grid = "#303B50" };

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
    /// Colour pairs below the WCAG 2.1 minimum for this style: 4.5:1 for text, 3:1 for marks and edges.
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
        Require("Rising candles", Rising, 3);
        Require("Falling candles", Falling, 3);
        Require("Graph edges", Edge, 3);
        return issues;
    }
}

public sealed record ContrastIssue(string Element, string Foreground, string Background, double Ratio, double Required);

/// <summary>WCAG 2.1 relative-luminance contrast.</summary>
public static class Contrast
{
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
