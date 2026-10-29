using System.Globalization;
using System.Text;

namespace Lumen.Charts;

/// <summary>Writes an event timeline as an HTML fragment: the same markup in a prerender, a circuit, a static page or an HTTP response.</summary>
public static class EventTimelineHtml
{
    private const string DefaultDateFormat = "ddd d MMM yyyy";
    private const string DefaultTimeFormat = "HH:mm";
    private const int DefaultPageSize = 10;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The fragment for <paramref name="spec"/>. A null <paramref name="shown"/> writes every event; a number writes that many, the ones
    /// nearest the Now marker when there is one, with a line that says which of the events they are. The fragment holds its own style
    /// block, scoped under <c>.lumen-events</c>, and no <c>id</c> attribute. Throws <see cref="ArgumentException"/> for a spec outside its
    /// limits and <see cref="ArgumentOutOfRangeException"/> for a <paramref name="shown"/> below 1.</summary>
    public static string Render(EventTimelineSpec spec, int? shown = null)
    {
        var view = EventView.Of(spec);
        var window = shown is { } n ? view.Window(n) : (0, view.Order.Count - 1);
        return Render(spec, view, window, false);
    }

    /// <summary>The fragment for the events at reading positions <paramref name="window"/>, whose <paramref name="view"/> is already built and
    /// validated from <paramref name="spec"/>. With <paramref name="controls"/> it holds the buttons that show <paramref name="pageSize"/> more
    /// events at either end, and the component wires them up; without them it holds a line of words for a partial window.</summary>
    internal static string Render(EventTimelineSpec spec, EventView view, (int First, int Last) window, bool controls, int pageSize = DefaultPageSize)
    {
        var style = spec.Style ?? ChartStyle.Light;
        var compact = spec.Density == Density.Compact;
        var zone = TimeAxis.Zone(spec.TimeZone);
        var write = new Writer(spec, style, zone);
        var count = view.Order.Count;
        var label = !string.IsNullOrWhiteSpace(spec.Title) ? spec.Title : !string.IsNullOrWhiteSpace(spec.Description) ? spec.Description : "Events";

        var sb = new StringBuilder();
        sb.Append("<section class='lumen-events lumen-events-").Append(compact ? "compact" : "regular").Append("' style='")
          .Append(LumenHtml.RootStyle(style)).Append("' aria-label='").Append(LumenHtml.E(label)).Append("'>");
        sb.Append("<style>").Append(Css(compact)).Append("</style>");
        if (!string.IsNullOrWhiteSpace(spec.Title))
            sb.Append("<h").Append(spec.TitleLevel).Append(" class='lumen-events-title'>").Append(LumenHtml.E(spec.Title)).Append("</h").Append(spec.TitleLevel).Append('>');
        if (!string.IsNullOrWhiteSpace(spec.Description)) sb.Append("<p class='lumen-events-desc'>").Append(LumenHtml.E(spec.Description)).Append("</p>");

        if (controls) MoreButton(sb, spec, true, window.First, pageSize);

        // The reading order of what is shown, with the Now marker at the position it stands before. It is inside the window when it stands
        // from before the first event shown to after the last.
        var items = new List<Item>();
        for (var p = window.First; p <= window.Last; p++)
        {
            if (view.NowAt == p) items.Add(NowItem(view));
            items.Add(new Item(false, view.Order[p], view.GroupKey(view.Order[p]), view.GroupLabel(view.Order[p])));
        }
        if (view.NowAt == window.Last + 1) items.Add(NowItem(view));

        if (items.Count == 0) sb.Append("<p class='lumen-events-empty'>No events</p>");
        else if (spec.GroupBy == EventGrouping.None) List(sb, write, spec, items);
        else
        {
            sb.Append("<ol class='lumen-events-groups'>");
            for (var i = 0; i < items.Count;)
            {
                // Events are in order, so the items of one heading follow one another, the Now marker among them.
                var j = i;
                while (j < items.Count && items[j].Key == items[i].Key) j++;
                sb.Append("<li class='lumen-events-group'><h").Append(spec.TitleLevel + 1).Append(" class='lumen-events-heading'>").Append(LumenHtml.E(items[i].Label))
                  .Append("</h").Append(spec.TitleLevel + 1).Append('>');
                List(sb, write, spec, items.GetRange(i, j - i));
                sb.Append("</li>");
                i = j;
            }
            sb.Append("</ol>");
        }

        if (controls) MoreButton(sb, spec, false, count - 1 - window.Last, pageSize);
        else if (count > 0 && (window.First > 0 || window.Last < count - 1))
            sb.Append("<p class='lumen-events-range'>Showing ").Append((window.First + 1).ToString("N0", Inv)).Append('–').Append((window.Last + 1).ToString("N0", Inv))
              .Append(" of ").Append(count.ToString("N0", Inv)).Append(" events</p>");
        return sb.Append("</section>").ToString();
    }

    private sealed record Item(bool IsNow, int Index, string Key, string Label);

    private static Item NowItem(EventView view) => new(true, -1, view.NowGroupKey, view.NowGroupLabel);

    private static void List(StringBuilder sb, Writer write, EventTimelineSpec spec, List<Item> items)
    {
        sb.Append("<ol class='lumen-events-list'>");
        foreach (var item in items)
        {
            if (item.IsNow) write.Now(sb);
            else write.Event(sb, spec.Events[item.Index]);
        }
        sb.Append("</ol>");
    }

    private static void MoreButton(StringBuilder sb, EventTimelineSpec spec, bool earlier, int remaining, int pageSize)
    {
        // In reading order the first side holds the earlier events, or with the newest first, the newer ones.
        var word = earlier ? (spec.NewestFirst ? "newer" : "earlier") : (spec.NewestFirst ? "older" : "later");
        sb.Append("<button type='button' class='lumen-events-more' data-lumen-more='").Append(earlier ? "earlier" : "later").Append('\'');
        // aria-disabled, not disabled, so a button that stops moving keeps keyboard focus; the component ignores a press on it.
        if (remaining <= 0) sb.Append(" aria-disabled='true'>No ").Append(word).Append(" events");
        else sb.Append(">Show ").Append(Math.Min(pageSize, remaining).ToString(Inv)).Append(' ').Append(word);
        sb.Append("</button>");
    }

    /// <summary>Writes the parts of a timeline whose text depends on its zone and formats.</summary>
    private sealed class Writer(EventTimelineSpec spec, ChartStyle style, TimeZoneInfo? zone)
    {
        private readonly string _date = spec.DateFormat ?? DefaultDateFormat;
        private readonly string _time = spec.TimeFormat ?? DefaultTimeFormat;

        private DateTimeOffset Local(DateTimeOffset moment) => zone is null ? moment.ToUniversalTime() : TimeZoneInfo.ConvertTime(moment, zone);

        public void Now(StringBuilder sb) =>
            sb.Append("<li class='lumen-events-now'><span class='lumen-event-dot' aria-hidden='true'></span>Now · ")
              .Append(LumenHtml.E(Local(spec.Now!.Value).ToString(_date, Inv))).Append("</li>");

        public void Event(StringBuilder sb, TimelineEvent e)
        {
            sb.Append("<li class='lumen-event lumen-event-").Append(Class(e.Status)).Append("'>");
            sb.Append("<span class='lumen-event-dot' aria-hidden='true'></span>");
            var start = Local(e.When);
            sb.Append("<time datetime='").Append(start.ToString(e.AllDay ? "yyyy-MM-dd" : "yyyy-MM-dd'T'HH:mm:sszzz", Inv)).Append("'>")
              .Append(LumenHtml.E(When(e, start))).Append("</time>");
            if (e.Href is not null && LumenHtml.SafeLink(e.Href))
                sb.Append("<a class='lumen-event-title' href='").Append(LumenHtml.E(e.Href)).Append("'>").Append(LumenHtml.E(e.Title)).Append("</a>");
            else sb.Append("<span class='lumen-event-title'>").Append(LumenHtml.E(e.Title)).Append("</span>");
            if (!string.IsNullOrEmpty(e.Value)) sb.Append("<span class='lumen-event-value'>").Append(LumenHtml.E(e.Value)).Append("</span>");
            Status(sb, e.Status);
            if (!string.IsNullOrEmpty(e.Category)) sb.Append("<span class='lumen-event-category'>").Append(LumenHtml.E(e.Category)).Append("</span>");
            if (!string.IsNullOrEmpty(e.Note)) sb.Append("<p class='lumen-event-note'>").Append(LumenHtml.E(e.Note)).Append("</p>");
            sb.Append("</li>");
        }

        private void Status(StringBuilder sb, EventStatus status)
        {
            var (glyph, word) = status switch
            {
                EventStatus.Done => ("✓", "Done"),
                EventStatus.Current => ("●", "Now"),
                EventStatus.Upcoming => ("○", "Upcoming"),
                EventStatus.Cancelled => ("✕", "Cancelled"),
                _ => ("", "")
            };
            if (word.Length == 0) return;
            sb.Append("<span class='lumen-event-status'");
            if (status == EventStatus.Current) sb.Append(" style='color:").Append(LumenHtml.Readable(style.SeriesColor(0), style)).Append('\'');
            sb.Append("><span aria-hidden='true'>").Append(glyph).Append("</span> ").Append(word).Append("</span>");
        }

        private string When(TimelineEvent e, DateTimeOffset start)
        {
            string D(DateTimeOffset d) => d.ToString(_date, Inv);
            string T(DateTimeOffset d) => d.ToString(_time, Inv);
            if (e.End is not { } raw) return e.AllDay ? D(start) : $"{D(start)} · {T(start)}";
            var end = Local(raw);
            if (e.AllDay)
            {
                if (end.Date == start.Date) return D(start);
                // The short forms assume the default format's shape; a format of the reader's own is written in full at both ends.
                if (spec.DateFormat is null)
                {
                    if (end.Year == start.Year && end.Month == start.Month)
                        return $"{start.ToString("ddd d", Inv)} – {D(end)}";
                    if (end.Year == start.Year)
                        return $"{start.ToString("ddd d MMM", Inv)} – {D(end)}";
                }
                return $"{D(start)} – {D(end)}";
            }
            if (raw == e.When) return $"{D(start)} · {T(start)}";
            if (end.Date == start.Date) return $"{D(start)} · {T(start)}–{T(end)}";
            return $"{D(start)} · {T(start)} – {D(end)} · {T(end)}";
        }

        private static string Class(EventStatus status) => status switch
        {
            EventStatus.Done => "done",
            EventStatus.Current => "current",
            EventStatus.Upcoming => "upcoming",
            EventStatus.Cancelled => "cancelled",
            _ => "none"
        };
    }

    private static string Css(bool compact)
    {
        var r = compact ? ".lumen-events.lumen-events-compact" : ".lumen-events.lumen-events-regular";
        var pad = compact ? 3 : 7;
        var dotTop = pad + 3;
        return $"{r}{{display:block;box-sizing:border-box;margin:0;padding:0;background:var(--lumen-bg);color:var(--lumen-text);font-family:var(--lumen-font);font-size:{(compact ? 13 : 14)}px;line-height:1.35}}"
            + $"{r} *{{box-sizing:border-box}}"
            + $"{r} p{{margin:0}}"
            + $"{r} ol{{margin:0;padding:0;list-style:none}}"
            + $"{r} .lumen-events-title{{margin:0 0 4px;font-size:18px;font-weight:600;line-height:1.25}}"
            + $"{r} .lumen-events-desc{{margin:0 0 10px;font-size:13px;color:var(--lumen-muted)}}"
            + $"{r} .lumen-events-group{{margin:0;padding:0;list-style:none}}"
            + $"{r} .lumen-events-heading{{margin:{(compact ? 8 : 14)}px 0 4px;font-size:14px;font-weight:600}}"
            + $"{r} .lumen-events-list{{position:relative;margin:0;padding:0;list-style:none}}"
            + $"{r} .lumen-events-list::before{{content:\"\";position:absolute;left:5px;top:0;bottom:0;width:2px;background:var(--lumen-grid)}}"
            + $"{r} .lumen-event,{r} .lumen-events-now{{position:relative;display:flex;flex-wrap:wrap;align-items:baseline;gap:2px 10px;margin:0;padding:{pad}px 0 {pad}px 24px;list-style:none}}"
            + $"{r} .lumen-event-dot{{position:absolute;left:0;top:{dotTop}px;width:12px;height:12px;border-radius:50%;border:2px solid var(--lumen-muted);background:var(--lumen-bg)}}"
            + $"{r} .lumen-event-done .lumen-event-dot,{r} .lumen-events-now .lumen-event-dot{{border-color:var(--lumen-accent);background:var(--lumen-accent)}}"
            + $"{r} .lumen-event-current .lumen-event-dot{{border-color:var(--lumen-accent);box-shadow:0 0 0 3px var(--lumen-grid)}}"
            + $"{r} .lumen-event-upcoming .lumen-event-dot{{border-color:var(--lumen-accent)}}"
            + $"{r} .lumen-event-cancelled .lumen-event-dot{{background:var(--lumen-grid)}}"
            + $"{r} .lumen-events-now{{font-weight:600}}"
            + $"{r} time{{font-size:13px;color:var(--lumen-muted);white-space:nowrap}}"
            + $"{r} .lumen-event-title{{font-weight:600;overflow-wrap:anywhere}}"
            + $"{r} a{{color:inherit}}"
            + $"{r} .lumen-event-cancelled .lumen-event-title{{text-decoration:line-through}}"
            + $"{r} .lumen-event-value{{font-weight:600}}"
            + $"{r} .lumen-event-status{{font-size:13px;color:var(--lumen-muted)}}"
            + $"{r} .lumen-event-category{{padding:0 8px;border:1px solid var(--lumen-grid);border-radius:10px;font-size:12px;color:var(--lumen-muted)}}"
            + $"{r} .lumen-event-note{{flex-basis:100%;font-size:13px;color:var(--lumen-muted);overflow-wrap:anywhere}}"
            + $"{r} .lumen-events-more{{display:block;margin:6px 0 6px 24px;padding:4px 10px;border:1px solid var(--lumen-grid);border-radius:6px;background:var(--lumen-bg);color:var(--lumen-text);font:inherit;font-size:13px;cursor:pointer}}"
            + $"{r} .lumen-events-more[aria-disabled='true']{{color:var(--lumen-muted);cursor:default}}"
            + $"{r} .lumen-events-more:focus-visible{{outline:2px solid var(--lumen-accent);outline-offset:2px}}"
            + $"{r} .lumen-events-range,{r} .lumen-events-empty{{margin:8px 0 0 24px;font-size:13px;color:var(--lumen-muted)}}"
            + LumenHtml.HiddenRule(".lumen-events");
    }
}
