using System.Globalization;

namespace Lumen.Charts;

/// <summary>The checks an event timeline passes before it is drawn, in the voice of <see cref="ChartValidation"/>.</summary>
internal static class EventTimelineValidation
{
    internal static void Request(EventTimelineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Spec(request.Spec);
        if (request.Shown is < 1) throw new ArgumentException("Shown may be left out, or be at least 1.");
    }

    internal static void Spec(EventTimelineSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Events is null) throw new ArgumentException("An event timeline needs events, which may be none.");
        if (spec.Events.Count > 2_000) throw new ArgumentException("An event timeline holds at most 2,000 events.");
        if (spec.Title is { Length: > 120 }) throw new ArgumentException("A title may be at most 120 characters.");
        if (spec.Description is { Length: > 300 }) throw new ArgumentException("A description may be at most 300 characters.");
        if (spec.TitleLevel is < 2 or > 5) throw new ArgumentException("A title level is from 2 to 5.");
        if (!Enum.IsDefined(spec.GroupBy)) throw new ArgumentException("Unknown event grouping.");
        if (!Enum.IsDefined(spec.Density)) throw new ArgumentException("Unknown timeline density.");
        TimeAxis.Zone(spec.TimeZone);
        Format(spec.DateFormat, "date");
        Format(spec.TimeFormat, "time");
        ChartValidation.Style(spec.Style);
        for (var i = 0; i < spec.Events.Count; i++) Event(spec.Events[i], i + 1);
    }

    private static void Format(string? format, string what)
    {
        if (format is null) return;
        if (format.Length > 40) throw new ArgumentException($"A {what} format may be at most 40 characters.");
        try { new DateTimeOffset(2026, 1, 31, 13, 5, 0, TimeSpan.Zero).ToString(format, CultureInfo.InvariantCulture); }
        catch (FormatException) { throw new ArgumentException($"The {what} format is not a valid .NET format."); }
    }

    private static void Event(TimelineEvent e, int number)
    {
        var at = $"Event {number}: ";
        if (e is null) throw new ArgumentException($"Event {number} cannot be null.");
        if (string.IsNullOrWhiteSpace(e.Title) || e.Title.Length > 120)
            throw new ArgumentException(at + "a title is required and may be at most 120 characters.");
        if (e.Note is { Length: > 200 }) throw new ArgumentException(at + "a note may be at most 200 characters.");
        if (e.Value is { Length: > 40 }) throw new ArgumentException(at + "a value may be at most 40 characters.");
        if (e.Category is { Length: > 24 }) throw new ArgumentException(at + "a category may be at most 24 characters.");
        if (e.Href is not null && !LumenHtml.SafeLink(e.Href))
            throw new ArgumentException(at + "a link must be an http, https or mailto address, or a relative one.");
        if (e.End is { } end && end < e.When) throw new ArgumentException(at + "the end cannot be before the start.");
        if (!Enum.IsDefined(e.Status)) throw new ArgumentException(at + "unknown status.");
    }
}
