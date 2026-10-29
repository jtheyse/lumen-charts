namespace Lumen.Charts;

/// <summary>Where an event stands, written as a word beside it. Colour never carries it alone.</summary>
public enum EventStatus
{
    /// <summary>No status is written. This is the default.</summary>
    None,
    /// <summary>The event has happened.</summary>
    Done,
    /// <summary>The event is happening now.</summary>
    Current,
    /// <summary>The event is still to come.</summary>
    Upcoming,
    /// <summary>The event will not take place.</summary>
    Cancelled
}

/// <summary>How an event timeline gathers its events under headings.</summary>
public enum EventGrouping
{
    /// <summary>No headings. This is the default.</summary>
    None,
    /// <summary>One heading for each calendar day.</summary>
    Day,
    /// <summary>One heading for each calendar month.</summary>
    Month,
    /// <summary>One heading for each calendar year.</summary>
    Year
}

/// <summary>One event on a timeline: a moment, or a span, with a title.</summary>
/// <param name="When">When the event starts. Groups and times are read in the timeline's time zone.</param>
/// <param name="Title">What happened, 1 to 120 characters.</param>
public sealed record TimelineEvent(DateTimeOffset When, string Title)
{
    /// <summary>When a span ends. It may not be before <see cref="When"/>; null means a single moment.</summary>
    public DateTimeOffset? End { get; init; }
    /// <summary>Whether the event covers whole days, so no time of day is written.</summary>
    public bool AllDay { get; init; }
    /// <summary>A line of detail, at most 200 characters.</summary>
    public string? Note { get; init; }
    /// <summary>A figure shown with the event, such as a result, at most 40 characters.</summary>
    public string? Value { get; init; }
    /// <summary>Where the event stands. The default writes no status.</summary>
    public EventStatus Status { get; init; }
    /// <summary>A short label such as a kind of event, at most 24 characters.</summary>
    public string? Category { get; init; }
    /// <summary>A link for the title: an http, https or mailto address, or a relative one.</summary>
    public string? Href { get; init; }
}

/// <summary>An event timeline: dated events in a vertical list, optionally grouped and set against a Now marker.</summary>
/// <param name="Events">The events, at most 2,000, in any order.</param>
public sealed record EventTimelineSpec(IReadOnlyList<TimelineEvent> Events)
{
    /// <summary>A heading for the timeline, at most 120 characters.</summary>
    public string? Title { get; init; }
    /// <summary>A line of context under the heading, at most 300 characters.</summary>
    public string? Description { get; init; }
    /// <summary>The level of the heading, 2 to 5. Group headings take the next level down. The default is 2.</summary>
    public int TitleLevel { get; init; } = 2;
    /// <summary>Whether the newest event comes first. Events at the same time keep the order they have in <see cref="Events"/>.</summary>
    public bool NewestFirst { get; init; }
    /// <summary>The headings the events are gathered under.</summary>
    public EventGrouping GroupBy { get; init; }
    /// <summary>The moment the Now marker stands at; null draws no marker. An event at this moment counts as reached.</summary>
    public DateTimeOffset? Now { get; init; }
    /// <summary>The zone dates and times are read in, such as <c>Africa/Johannesburg</c>. Null means UTC.</summary>
    public string? TimeZone { get; init; }
    /// <summary>The .NET format for dates, at most 40 characters. The default is <c>ddd d MMM yyyy</c>.</summary>
    public string? DateFormat { get; init; }
    /// <summary>The .NET format for times of day, at most 40 characters. The default is <c>HH:mm</c>.</summary>
    public string? TimeFormat { get; init; }
    /// <summary>How tightly the timeline is set.</summary>
    public Density Density { get; init; }
    /// <summary>Colours and type, or null for the default finish.</summary>
    public ChartStyle? Style { get; init; }
}

/// <summary>A request for an event timeline, for hosts that take a spec and a window size together.</summary>
/// <param name="Spec">The timeline.</param>
/// <param name="Shown">How many events to open with; null shows them all. When set, at least 1.</param>
public sealed record EventTimelineRequest(EventTimelineSpec Spec, int? Shown = null);
