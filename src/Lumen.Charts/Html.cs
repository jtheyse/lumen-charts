using System.Net;

namespace Lumen.Charts;

/// <summary>The small pieces of HTML writing the stat tiles and the components after them share.</summary>
internal static class LumenHtml
{
    /// <summary>The CSS that hides text from the eye and keeps it for screen readers, scoped under the given root selector,
    /// such as <c>.lumen-stats</c>.</summary>
    internal static string HiddenRule(string root) =>
        root + " .lumen-sr{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap}";

    /// <summary>HTML-encoded text; an empty string for null.</summary>
    internal static string E(string? text) => text is null ? "" : WebUtility.HtmlEncode(text);

    /// <summary>True for an http, https or mailto address and for a relative one, whatever the case or the white space round it,
    /// and false for null. A scheme hidden by a tab, a control character or an entity is read as a scheme and refused. The
    /// address must still be HTML-encoded on output with <see cref="E"/>: <c>javascript&amp;#58;x</c> has no literal colon and
    /// is accepted as relative, and only encoding keeps a browser from turning its entity into one.</summary>
    internal static bool SafeLink(string? href)
    {
        if (href is null) return false;
        var s = href.Trim();
        if (s.StartsWith('#')) return true;
        var end = s.IndexOfAny(['/', '?']);
        var head = end < 0 ? s : s[..end];
        var colon = head.IndexOf(':');
        if (colon < 0) return true;
        var scheme = head[..colon];
        return scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><paramref name="colour"/> where it clears 4.5:1 against the style's background, else the style's text colour.</summary>
    internal static string Readable(string colour, ChartStyle style) =>
        Contrast.Ratio(colour, style.Background) >= 4.5 ? colour : style.Text;

    /// <summary>The inline style value that carries the style's colours and font as custom properties.</summary>
    internal static string RootStyle(ChartStyle style) =>
        $"--lumen-bg:{style.Background};--lumen-text:{style.Text};--lumen-muted:{Readable(style.Muted, style)};--lumen-grid:{style.Grid};"
        + $"--lumen-rising:{style.Rising};--lumen-falling:{style.Falling};--lumen-accent:{style.SeriesColor(0)};--lumen-font:{style.FontFamily}";
}
