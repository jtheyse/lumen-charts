namespace Lumen.Charts;

/// <summary>The checks a stat tiles spec passes before it is drawn, in the voice of <see cref="ChartValidation"/>.</summary>
internal static class StatTilesValidation
{
    internal static void Spec(StatTilesSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Tiles is null || spec.Tiles.Count is 0 or > 48)
            throw new ArgumentException("Stat tiles need between 1 and 48 tiles.");
        if (spec.Title is { Length: > 120 }) throw new ArgumentException("A title may be at most 120 characters.");
        if (spec.Description is { Length: > 300 }) throw new ArgumentException("A description may be at most 300 characters.");
        if (spec.TitleLevel is < 2 or > 6) throw new ArgumentException("A title level is a heading level from 2 to 6.");
        if (spec.Columns is { } columns && columns is < 1 or > 6) throw new ArgumentException("Columns may be left out for automatic, or be from 1 to 6.");
        if (!Enum.IsDefined(spec.Density)) throw new ArgumentException("Unknown tile density.");
        ChartValidation.Style(spec.Style);
        for (var i = 0; i < spec.Tiles.Count; i++) Tile(spec.Tiles[i], i + 1);
    }

    private static void Tile(StatTile tile, int number)
    {
        var at = $"Tile {number}: ";
        if (tile is null) throw new ArgumentException($"Tile {number} cannot be null.");
        if (string.IsNullOrWhiteSpace(tile.Label) || tile.Label.Length > 40)
            throw new ArgumentException(at + "a label is required and may be at most 40 characters.");
        if (tile.Value is { } value && !ChartValidation.Finite(value)) throw new ArgumentException(at + "a value must be finite.");
        if (tile.Previous is { } previous && !ChartValidation.Finite(previous)) throw new ArgumentException(at + "a previous value must be finite.");
        if (tile.Target is { } target && (!ChartValidation.Finite(target) || target <= 0))
            throw new ArgumentException(at + "a target must be finite and greater than 0.");
        if (!Enum.IsDefined(tile.Format)) throw new ArgumentException(at + "unknown value format.");
        if (!Enum.IsDefined(tile.Better)) throw new ArgumentException(at + "unknown Better.");
        if (!Enum.IsDefined(tile.ChangeAs)) throw new ArgumentException(at + "unknown ChangeAs.");
        if (string.IsNullOrWhiteSpace(tile.Missing) || tile.Missing.Length > 24)
            throw new ArgumentException(at + "the words for a missing value may be from 1 to 24 characters and cannot be blank.");
        if (tile.Note is { Length: > 60 }) throw new ArgumentException(at + "a note may be at most 60 characters.");
        if (tile.Since is { Length: > 40 }) throw new ArgumentException(at + "a since phrase may be at most 40 characters.");
        if (tile.ChangeText is { Length: > 40 }) throw new ArgumentException(at + "change text may be at most 40 characters.");
        if (tile.Trend is { } trend)
        {
            if (trend.Count > 400) throw new ArgumentException(at + "a trend may hold at most 400 values.");
            if (trend.Any(v => v is { } x && !ChartValidation.Finite(x))) throw new ArgumentException(at + "trend values must be finite.");
            if (trend.Count(v => v is not null) < 2) throw new ArgumentException(at + "a trend needs at least 2 values that are not null.");
        }
        if (tile.Href is not null && !LumenHtml.SafeLink(tile.Href))
            throw new ArgumentException(at + "a link may be an http, https or mailto address or a relative one.");
    }
}
