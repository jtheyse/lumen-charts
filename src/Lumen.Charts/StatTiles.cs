using System.Globalization;
using System.Text;

namespace Lumen.Charts;

/// <summary>How closely a row of stat tiles is packed.</summary>
public enum TileDensity
{
    /// <summary>Tiles of at least 150 px with a 26 px value. This is the default.</summary>
    Regular,
    /// <summary>Tiles of at least 96 px with a 20 px value, for a strip of rounds or a dense summary.</summary>
    Compact
}

/// <summary>One headline number: a label, a value, and optionally its change, a note, a target and a trend.</summary>
/// <param name="Label">What the number is, 1 to 40 characters.</param>
/// <param name="Value">The number, or null when there is none yet; <see cref="Missing"/> is written then.</param>
public sealed record StatTile(string Label, double? Value)
{
    /// <summary>How the value, the previous value and the target are written. The default is plain numbers.</summary>
    public ValueFormat Format { get; init; } = ValueFormat.Number;
    /// <summary>A unit written after each number, as an axis writes it.</summary>
    public string? Unit { get; init; }
    /// <summary>The value this one is compared with; with it the tile writes the change in words.</summary>
    public double? Previous { get; init; }
    /// <summary>Which direction of change is an improvement. The default, <see cref="Lumen.Charts.Better.Neither"/>, writes up and down.</summary>
    public Better Better { get; init; } = Better.Neither;
    /// <summary>How the change is written. The default is the difference.</summary>
    public ChangeAs ChangeAs { get; init; } = ChangeAs.Difference;
    /// <summary>What <see cref="Previous"/> was, such as "last season", at most 40 characters.</summary>
    public string? Since { get; init; }
    /// <summary>Words that replace the computed change outright, such as "new best", at most 40 characters. They are written in the muted colour.</summary>
    public string? ChangeText { get; init; }
    /// <summary>The words written when <see cref="Value"/> is null, 1 to 24 characters. The default is a dash, read as "none".</summary>
    public string Missing { get; init; } = "—";
    /// <summary>One muted line under the change, at most 60 characters.</summary>
    public string? Note { get; init; }
    /// <summary>A run of values drawn as a sparkline, up to 400, nulls as gaps, with at least 2 that are not null.</summary>
    public IReadOnlyList<double?>? Trend { get; init; }
    /// <summary>A figure the value is measured against, above 0; the tile draws a meter and writes the percentage reached.</summary>
    public double? Target { get; init; }
    /// <summary>An http, https, mailto or relative address that makes the whole tile a link.</summary>
    public string? Href { get; init; }
}

/// <summary>A row or grid of stat tiles.</summary>
/// <param name="Tiles">From 1 to 48 tiles, written in order.</param>
public sealed record StatTilesSpec(IReadOnlyList<StatTile> Tiles)
{
    /// <summary>A heading written above the tiles, at most 120 characters; it also names the section.</summary>
    public string? Title { get; init; }
    /// <summary>A paragraph written under the title, at most 300 characters; it names the section when there is no title.</summary>
    public string? Description { get; init; }
    /// <summary>The level of the title's heading, from 2 to 6. The default is 2.</summary>
    public int TitleLevel { get; init; } = 2;
    /// <summary>A fixed number of columns from 1 to 6, or null to fit as many tiles as the width holds.</summary>
    public int? Columns { get; init; }
    /// <summary>How closely the tiles are packed.</summary>
    public TileDensity Density { get; init; } = TileDensity.Regular;
    /// <summary>The colours and font, or null for <see cref="ChartStyle.Light"/>.</summary>
    public ChartStyle? Style { get; init; }
}

/// <summary>Writes stat tiles as an HTML fragment: the same markup in a prerender, a circuit, a static page or an HTTP response.</summary>
public static class StatTilesHtml
{
    /// <summary>The fragment for <paramref name="spec"/>. It holds its own style block, scoped under <c>.lumen-stats</c>, and no
    /// <c>id</c> attribute, so any number of fragments share a page. Throws <see cref="ArgumentException"/> for a spec outside its limits.</summary>
    public static string Render(StatTilesSpec spec)
    {
        StatTilesValidation.Spec(spec);
        var style = spec.Style ?? ChartStyle.Light;
        var compact = spec.Density == TileDensity.Compact;
        var sb = new StringBuilder();
        var name = spec.Title ?? spec.Description ?? "Statistics";
        sb.Append("<section class='lumen-stats lumen-stats-").Append(compact ? "compact" : "regular")
          .Append("' style='").Append(LumenHtml.RootStyle(style)).Append("' aria-label='").Append(LumenHtml.E(name)).Append("'>");
        sb.Append("<style>").Append(Css(spec.Columns, compact)).Append("</style>");
        if (spec.Title is not null)
            sb.Append("<h").Append(spec.TitleLevel).Append(" class='lumen-stats-title'>").Append(LumenHtml.E(spec.Title)).Append("</h").Append(spec.TitleLevel).Append('>');
        if (spec.Description is not null) sb.Append("<p class='lumen-stats-desc'>").Append(LumenHtml.E(spec.Description)).Append("</p>");
        sb.Append("<ul class='lumen-stats-grid' role='list'>");
        foreach (var tile in spec.Tiles) Tile(sb, tile, style);
        return sb.Append("</ul></section>").ToString();
    }

    private static string Css(int? columns, bool compact)
    {
        var grid = columns is { } n ? $"repeat({n},minmax(0,1fr))" : $"repeat(auto-fill,minmax({(compact ? 96 : 150)}px,1fr))";
        var value = compact ? 20 : 26;
        var pad = compact ? 8 : 12;
        const string r = ".lumen-stats";
        return $"{r}{{display:block;box-sizing:border-box;background:var(--lumen-bg);color:var(--lumen-text);font-family:var(--lumen-font);font-size:14px;line-height:1.3}}"
            + $"{r} *{{box-sizing:border-box}}"
            + $"{r} p{{margin:0}}"
            + $"{r} .lumen-stats-title{{margin:0 0 4px;font-size:18px;font-weight:600;color:var(--lumen-text)}}"
            + $"{r} .lumen-stats-desc{{margin:0 0 10px;font-size:13px;color:var(--lumen-muted)}}"
            + $"{r} .lumen-stats-grid{{display:grid;grid-template-columns:{grid};gap:{(compact ? 8 : 12)}px;margin:0;padding:0;list-style:none}}"
            + $"{r} .lumen-stat{{position:relative;min-width:0;padding:{pad}px;border:1px solid var(--lumen-grid);border-radius:8px;background:var(--lumen-bg)}}"
            + $"{r} .lumen-stat-link{{display:block;margin:-{pad}px;padding:{pad}px;color:inherit;text-decoration:none;border-radius:8px}}"
            + $"{r} .lumen-stat-link:focus-visible{{outline:2px solid var(--lumen-accent);outline-offset:2px}}"
            + $"{r} .lumen-stat-label{{font-size:{(compact ? 12 : 13)}px;color:var(--lumen-muted);overflow-wrap:anywhere}}"
            + $"{r} .lumen-stat-value{{margin-top:2px;font-size:{value}px;font-weight:700;line-height:1.15;color:var(--lumen-text);overflow-wrap:anywhere}}"
            + $"{r} .lumen-stat-change{{margin-top:4px;font-size:12px;font-weight:600}}"
            + $"{r} .lumen-stat-note{{margin-top:2px;font-size:12px;color:var(--lumen-muted);overflow-wrap:anywhere}}"
            + $"{r} .lumen-stat-meter{{margin-top:8px;height:6px;border-radius:3px;background:var(--lumen-grid);overflow:hidden}}"
            + $"{r} .lumen-stat-meter span{{display:block;height:100%;background:var(--lumen-accent)}}"
            + $"{r} .lumen-stat-target{{margin-top:3px;font-size:12px;color:var(--lumen-muted)}}"
            + $"{r} .lumen-stat-trend{{margin-top:8px;line-height:0}}"
            + LumenHtml.HiddenRule(r);
    }

    private static void Tile(StringBuilder sb, StatTile t, ChartStyle style)
    {
        sb.Append("<li class='lumen-stat'>");
        if (t.Href is not null) sb.Append("<a class='lumen-stat-link' href='").Append(LumenHtml.E(t.Href)).Append("'>");

        sb.Append("<p class='lumen-stat-label'>").Append(LumenHtml.E(t.Label)).Append("</p>");
        sb.Append("<p class='lumen-stat-value'>");
        if (t.Value is { } value) sb.Append(LumenHtml.E(ChangeWords.Value(value, t.Format, t.Unit)));
        else
        {
            sb.Append(LumenHtml.E(t.Missing));
            if (t.Missing == "—") sb.Append("<span class='lumen-sr'> none</span>");
        }
        sb.Append("</p>");

        if (t.Value is { } now && (t.Previous is not null || !string.IsNullOrEmpty(t.ChangeText)))
        {
            var (text, colour) = !string.IsNullOrEmpty(t.ChangeText)
                ? (t.ChangeText, LumenHtml.Readable(style.Muted, style))
                : ChangeWords.Of(now, t.Previous!.Value, t.Better, t.ChangeAs, t.Since, t.Format, t.Unit, style);
            sb.Append("<p class='lumen-stat-change' style='color:").Append(colour).Append("'>").Append(LumenHtml.E(text)).Append("</p>");
        }

        if (!string.IsNullOrEmpty(t.Note)) sb.Append("<p class='lumen-stat-note'>").Append(LumenHtml.E(t.Note)).Append("</p>");

        if (t.Value is { } reached && t.Target is { } target)
        {
            var pct = Math.Round(reached / target * 100, MidpointRounding.AwayFromZero);
            var width = Math.Clamp(pct, 0, 100).ToString("0", CultureInfo.InvariantCulture);
            sb.Append("<div class='lumen-stat-meter' aria-hidden='true'><span style='width:").Append(width).Append("%'></span></div>");
            sb.Append("<p class='lumen-stat-target'>").Append(pct.ToString("0", CultureInfo.InvariantCulture)).Append("% of target ")
              .Append(LumenHtml.E(ChangeWords.Value(target, t.Format, t.Unit))).Append("</p>");
        }

        if (t.Trend is { } trend)
        {
            var points = trend.Select((v, i) => new ChartPoint(i, v)).ToArray();
            var spark = ChartSvg.Render(new ChartSpec
            {
                Kind = ChartKind.Line, Sparkline = true, Width = 120, Height = 28, Style = style,
                Series = [new ChartSeries(t.Label, points)]
            }, includeLegend: false);
            var seen = trend.Where(v => v is not null).Select(v => v!.Value).ToArray();
            string F(double v) => LumenHtml.E(ChangeWords.Value(v, t.Format, t.Unit));
            sb.Append("<div class='lumen-stat-trend' aria-hidden='true'>").Append(spark).Append("</div>");
            sb.Append("<span class='lumen-sr'>").Append(seen.Length.ToString(CultureInfo.InvariantCulture)).Append(" values, lowest ")
              .Append(F(seen.Min())).Append(", highest ").Append(F(seen.Max())).Append(", last ").Append(F(seen[^1])).Append("</span>");
        }

        if (t.Href is not null) sb.Append("</a>");
        sb.Append("</li>");
    }
}
