using System.Globalization;

namespace Lumen.Charts;

/// <summary>Which direction of change is an improvement, so a change can be written as better or worse and not only up or down.</summary>
public enum Better
{
    /// <summary>Neither direction is a judgement: a change is written as up or down and takes the muted colour. This is the default.</summary>
    Neither,
    /// <summary>A higher value is better, such as points scored: a rise is better and a fall is worse.</summary>
    Higher,
    /// <summary>A lower value is better, such as a race time: a rise is worse and a fall is better.</summary>
    Lower
}

/// <summary>How a change from a previous value is written.</summary>
public enum ChangeAs
{
    /// <summary>The size of the difference, written in the value's own format.</summary>
    Difference,
    /// <summary>The difference as a percentage of the size of the previous value. A previous value of 0 writes the difference instead.</summary>
    Percent,
    /// <summary>The difference followed by the percentage in brackets.</summary>
    Both
}

/// <summary>Turns values and pairs of values into text, for stat tiles and the components after them.</summary>
internal static class ChangeWords
{
    /// <summary>A value written as an axis writes it, with its unit after it.</summary>
    internal static string Value(double value, ValueFormat format, string? unit) =>
        new Axis(AxisKind.Linear, 0, 1) { ValueFormat = format, Unit = unit }.Format(value);

    /// <summary>The change from <paramref name="previous"/> to <paramref name="value"/> in words, and the colour that carries it.</summary>
    internal static (string Text, string Colour) Of(double value, double previous, Better better, ChangeAs changeAs, string? since,
        ValueFormat format, string? unit, ChartStyle style)
    {
        since = since?.Trim();
        var hasSince = !string.IsNullOrEmpty(since);
        if (value == previous)
            return (hasSince ? "= same as " + since : "= same", LumenHtml.Readable(style.Muted, style));

        var rose = value > previous;
        var arrow = rose ? "▲" : "▼";
        var difference = Value(Math.Abs(value - previous), format, unit);
        var amount = changeAs == ChangeAs.Difference || previous == 0 ? difference
            : changeAs == ChangeAs.Percent ? Percent(value, previous)
            : difference + " (" + Percent(value, previous) + ")";

        bool? good = better == Better.Neither ? null : rose == (better == Better.Higher);
        var word = good is null ? (rose ? "up" : "down") : good.Value ? "better" : "worse";
        var text = arrow + " " + amount + " · " + word;
        if (hasSince) text += (good is null ? " from " : " than ") + since;
        var colour = good is null ? style.Muted : good.Value ? style.Rising : style.Falling;
        return (text, LumenHtml.Readable(colour, style));
    }

    private static string Percent(double value, double previous)
    {
        var p = Math.Abs(value - previous) / Math.Abs(previous) * 100;
        var tenth = Math.Round(p, 1, MidpointRounding.AwayFromZero);
        var text = tenth < 10
            ? tenth.ToString("0.0", CultureInfo.InvariantCulture)
            : Math.Round(p, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        if (text.EndsWith(".0", StringComparison.Ordinal)) text = text[..^2];
        return text + "%";
    }
}
