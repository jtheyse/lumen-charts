using System.Globalization;

namespace Lumen.Charts;

/// <summary>A timeline's events in reading order, where the Now marker stands, the headings, and the window of events shown.</summary>
internal sealed class EventView
{
    private readonly EventTimelineSpec _spec;
    private readonly TimeZoneInfo? _zone;

    private EventView(EventTimelineSpec spec, IReadOnlyList<int> order, int? nowAt)
    {
        _spec = spec;
        _zone = TimeAxis.Zone(spec.TimeZone);
        Order = order;
        NowAt = nowAt;
    }

    /// <summary>Event indexes in reading order: chronological, or newest first. Events at the same time keep the spec's order either way.</summary>
    internal IReadOnlyList<int> Order { get; }
    /// <summary>The reading position the Now marker stands before; <c>Order.Count</c> means after the last. Null without <see cref="EventTimelineSpec.Now"/>.</summary>
    internal int? NowAt { get; }

    /// <summary>Validates the spec, then orders its events and places Now.</summary>
    internal static EventView Of(EventTimelineSpec spec)
    {
        EventTimelineValidation.Spec(spec);
        var events = spec.Events;
        var indexes = Enumerable.Range(0, events.Count);
        // OrderBy is stable, so equal times keep the spec's order; the descending sort is stable too.
        var order = (spec.NewestFirst
            ? indexes.OrderByDescending(i => events[i].When.UtcTicks)
            : indexes.OrderBy(i => events[i].When.UtcTicks)).ToArray();
        int? nowAt = null;
        if (spec.Now is { } now)
            // An event at Now counts as reached, so the marker stands after it.
            nowAt = spec.NewestFirst
                ? order.Count(i => events[i].When > now)
                : order.Count(i => events[i].When <= now);
        return new EventView(spec, order, nowAt);
    }

    /// <summary>The default window, as first and last reading positions, both included. Without Now it is the first
    /// <paramref name="shown"/> positions; with Now it is the <paramref name="shown"/> positions nearest the marker, the larger half before it,
    /// topped up from the other side when one side runs short. An empty list gives (0, -1). Throws <see cref="ArgumentOutOfRangeException"/> when <paramref name="shown"/> is below 1.</summary>
    internal (int First, int Last) Window(int shown)
    {
        if (shown < 1) throw new ArgumentOutOfRangeException(nameof(shown), shown, "Shown must be at least 1.");
        var count = Order.Count;
        if (shown >= count) return (0, count - 1);
        if (NowAt is not { } at) return (0, shown - 1);
        var first = Math.Clamp(at - (shown + 1) / 2, 0, count - shown);
        return (first, first + shown - 1);
    }

    /// <summary>Extends the window by <paramref name="by"/> positions at the start of the list (<paramref name="earlier"/>) or at its end, clamped to the list.</summary>
    internal (int First, int Last) Grow((int First, int Last) window, bool earlier, int by) =>
        by < 0 ? throw new ArgumentOutOfRangeException(nameof(by), by, "A window grows by 0 or more.") : earlier ? (Math.Max(0, window.First - by), window.Last) : (window.First, Math.Min(Order.Count - 1, window.Last + by));

    /// <summary>The heading key of an event (by spec index) in the timeline's zone; empty without grouping.</summary>
    internal string GroupKey(int index) => Key(_spec.Events[index].When);
    /// <summary>The heading text of an event (by spec index); empty without grouping.</summary>
    internal string GroupLabel(int index) => Label(_spec.Events[index].When);
    /// <summary>The heading key of the Now marker's group; empty without Now or grouping.</summary>
    internal string NowGroupKey => _spec.Now is { } now ? Key(now) : "";
    /// <summary>The heading text of the Now marker's group; empty without Now or grouping.</summary>
    internal string NowGroupLabel => _spec.Now is { } now ? Label(now) : "";

    private DateTimeOffset Local(DateTimeOffset moment) =>
        _zone is null ? moment.ToUniversalTime() : TimeZoneInfo.ConvertTime(moment, _zone);

    private string Key(DateTimeOffset moment) => Format(moment, "yyyy-MM-dd", "yyyy-MM", "yyyy");
    private string Label(DateTimeOffset moment) => Format(moment, "ddd d MMM yyyy", "MMMM yyyy", "yyyy");

    private string Format(DateTimeOffset moment, string day, string month, string year) => _spec.GroupBy switch
    {
        EventGrouping.Day => Local(moment).ToString(day, CultureInfo.InvariantCulture),
        EventGrouping.Month => Local(moment).ToString(month, CultureInfo.InvariantCulture),
        EventGrouping.Year => Local(moment).ToString(year, CultureInfo.InvariantCulture),
        _ => ""
    };
}
