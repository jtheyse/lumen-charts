using System.Globalization;
using static Lumen.Charts.SvgWriter;

namespace Lumen.Charts;

/// <summary>Draws a <see cref="PlannerSpec"/> as a self-contained, accessible SVG: the whole period, a month or a day.</summary>
public static class PlannerSvg
{
    private const double Left = 76, Right = 24, RowH = 56, HeaderH = 16;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Draws <paramref name="spec"/> at <paramref name="view"/>, wide or narrow. Checks the spec and the view first.</summary>
    public static string Render(PlannerSpec spec, PlannerView view, PlannerLayout layout = PlannerLayout.Wide)
    {
        PlannerValidation.Validate(spec, view);
        if (!Enum.IsDefined(layout)) throw new ArgumentException($"A planner's layout must be Wide or Narrow, not {((int)layout).ToString(Invariant)}.");
        var style = spec.Style ?? ChartSvg.Preset(spec.Theme);
        // The filtered periods and events, long weekends and region names are worked out once, and each day's looked up by day.
        var plan = new PlannerIndex(spec);
        return (view.Zoom, layout) switch
        {
            (PlannerZoom.Year, PlannerLayout.Wide) => Year(spec, plan, style),
            (PlannerZoom.Year, PlannerLayout.Narrow) => YearNarrow(spec, plan, style),
            (PlannerZoom.Month, PlannerLayout.Wide) => Month(spec, plan, style, view.Date.Year, view.Date.Month),
            (PlannerZoom.Month, PlannerLayout.Narrow) => Agenda(spec, plan, style, view.Date.Year, view.Date.Month),
            _ => DayList(spec, plan, style, view.Date)
        };
    }

    private static SvgWriter Writer(PlannerSpec spec, ChartStyle style) =>
        new() { Style = style, Painted = spec.PaintBackground, Titled = spec.DrawTitles };

    private static string Year(PlannerSpec spec, PlannerIndex plan, ChartStyle style)
    {
        var months = PlannerCalendar.Months(spec);
        var width = spec.Width;
        var legend = LegendLines(width);
        // Height is fixed by content; Begin draws the titles and sets Head, which Head(spec) works out beforehand.
        var height = (int)Math.Ceiling(78 + Head(spec) + HeaderH + months.Count * RowH + 8 + legend * 18 + 16);
        var w = Writer(spec, style);
        ChartSvg.Begin(w, width, height, spec.Title, spec.Description);
        var top = 78 + w.Head;
        var col = (width - Left - Right) / PlannerCalendar.Columns;
        // Weekday initials along the top.
        for (var c = 0; c < PlannerCalendar.Columns; c++)
        {
            var dow = (DayOfWeek)(((int)spec.WeekStart + c) % 7);
            w.Text(Left + (c + .5) * col, top + 11, dow.ToString()[..1], "class='lumen-muted' font-size='10' text-anchor='middle'");
        }
        var longs = plan.LongWeekends;
        var events = plan.Events;
        for (var r = 0; r < months.Count; r++)
        {
            var (year, month) = months[r];
            var y = top + HeaderH + r * RowH;
            var lead = PlannerCalendar.Lead(year, month, spec.WeekStart);
            var first = new DateOnly(year, month, 1);
            var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            // "September 2027" is wider than the 68 pixels left of the grid, so the year goes under the month; the text still reads whole.
            w.Add($"<text class='lumen-muted lumen-month' x='{N(Left - 8)}' y='{N(y + 22)}' font-size='11' text-anchor='end'><tspan>{first.ToString("MMMM", Invariant)}</tspan><tspan x='{N(Left - 8)}' dy='13'> {year.ToString(Invariant)}</tspan></text>");
            double X(DateOnly d) => Left + (lead + d.Day - 1) * col;
            for (var d = first; d <= last; d = d.AddDays(1))
            {
                if (d < spec.From || d > spec.To) continue;
                var x = X(d);
                w.Add($"<g class='lumen-day' role='group' data-day='{Iso(d)}' aria-label='{E(plan.DayName(d))}'>");
                if (plan.IsWeekend(d)) w.Add($"<rect class='lumen-weekend' x='{N(x)}' y='{N(y)}' width='{N(col)}' height='{N(RowH - 2)}' fill='{style.Grid}'/>");
                foreach (var p in plan.On(d).Periods)
                {
                    if (p.Kind == PeriodKind.SchoolHoliday) w.Add($"<rect x='{N(x)}' y='{N(y + 2)}' width='{N(col)}' height='3' fill='{style.Muted}'/>");
                    if (p.Kind == PeriodKind.PublicHoliday) w.Add($"<path d='M{N(x + col / 2)},{N(y + 7.5)} l3.5,3.5 l-3.5,3.5 l-3.5,-3.5 Z' fill='{style.Text}'/>");
                }
                w.Add("</g>");
            }
            // A long weekend that runs into the next month is drawn in both rows, each up to its own edge.
            foreach (var (from, to) in longs.Where(l => l.From <= last && l.To >= first))
            {
                var x1 = X(Max(from, first)); var x2 = X(Min(to, last)) + col;
                w.Add($"<path d='M{N(x1 + 1)},{N(y + 41)} v3 H{N(x2 - 1)} v-3' fill='none' stroke='{style.Muted}' stroke-width='1.5'{w.Fixed}/>");
            }
            // Events: up to three lanes per day; a day with more writes "+N" naming the ones it could not draw.
            var lanes = new Dictionary<DateOnly, int>();
            var hidden = new SortedDictionary<DateOnly, List<PlannerEvent>>();
            foreach (var e in events)
            {
                var start = Max(e.Start, Max(first, spec.From));
                var end = Min(e.End ?? e.Start, Min(last, spec.To));
                if (end < start) continue;
                var lane = Days(start, end).Max(d => lanes.GetValueOrDefault(d));
                foreach (var d in Days(start, end))
                {
                    lanes[d] = lane + 1;
                    if (lane >= 3) (hidden.TryGetValue(d, out var list) ? list : hidden[d] = []).Add(e);
                }
                if (lane >= 3) continue;
                Stripe(w, plan, style, e, X(start), y + 18 + lane * 7, X(end) + col - X(start));
            }
            foreach (var (day, rest) in hidden)
            {
                var label = $"{rest.Count} more on {PlannerCalendar.Day(day)}: {string.Join("; ", rest.Select(e => e.Name))}";
                w.Add($"<text class='lumen-more' role='img' x='{N(X(day) + col / 2)}' y='{N(y + 44)}' font-size='10' text-anchor='middle' aria-label='{E(label)}'><title>{E(label)}</title>+{rest.Count}</text>");
            }
            // Busy weeks: each week of the row is named with its clash and close events; a busy one writes its count. That count and
            // "+N" are written in the text colour, since they may stand on a weekend band, where the muted colour falls below 4.5:1.
            for (var k = 0; k * 7 < lead + last.Day; k++)
            {
                var weekFirst = Max(Max(first, spec.From), first.AddDays(k * 7 - lead));
                var weekLast = Min(Min(last, spec.To), first.AddDays(k * 7 - lead + 6));
                if (weekLast < weekFirst) continue;
                var inWeek = events.Where(e => e.Start <= weekLast && (e.End ?? e.Start) >= weekFirst).ToArray();
                var clashes = inWeek.Count(e => CountsAs(e, PlannerRelevance.Clash));
                var near = inWeek.Count(e => CountsAs(e, PlannerRelevance.Near));
                var words = new List<string>();
                if (clashes > 0) words.Add(clashes == 1 ? "1 clash" : $"{clashes} clashes");
                if (near > 0) words.Add($"{near} close");
                var label = $"Week of {weekFirst.ToString("d MMMM yyyy", Invariant)}: {(words.Count == 0 ? "no clashes" : string.Join(", ", words))}";
                w.Add($"<g class='lumen-week' role='group' aria-label='{E(label)}'>");
                if (clashes + near > 0) w.Text(Left + (k * 7 + 3.5) * col, y + 54, (clashes + near).ToString(Invariant), "font-size='10' text-anchor='middle'");
                w.Add("</g>");
            }
        }
        Legend(w, spec, style, top + HeaderH + months.Count * RowH + 8);
        w.Add("</svg>");
        return w.ToString();
    }

    /// <summary>A day as written in data attributes, 2027-03-13, in the Gregorian calendar whatever the host's culture.</summary>
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", Invariant);
    private static int Head(PlannerSpec spec) => !spec.DrawTitles ? -ChartSvg.Untitled : 14 * (ChartSvg.Wrap(spec.Description, spec.Width - 48d).Length - 1);
    /// <summary>Whether <paramref name="e"/> counts as a clash or close event of <paramref name="relevance"/>: a cancelled one counts as
    /// neither, though it is still drawn and named as cancelled.</summary>
    private static bool CountsAs(PlannerEvent e, PlannerRelevance relevance) => e.Relevance == relevance && e.Status != PlannerStatus.Cancelled;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static IEnumerable<DateOnly> Days(DateOnly from, DateOnly to) { for (var d = from; d <= to; d = d.AddDays(1)) yield return d; }

    /// <summary>One event's mark: a focusable group named in words around the stripe <see cref="Shapes"/> draws.</summary>
    private static void Stripe(SvgWriter w, PlannerIndex plan, ChartStyle style, PlannerEvent e, double x, double y, double width)
    {
        var name = plan.Name(e);
        w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-event='{E(e.Id)}' aria-label='{E(name)}'><title>{E(name)}</title>");
        Shapes(w, style, e, x, y, width);
        w.Add("</g>");
    }

    /// <summary>A stripe's shapes: weight and dash by relevance, drawn by <see cref="Mark"/>. The legend draws its samples with them too.</summary>
    private static void Shapes(SvgWriter w, ChartStyle style, PlannerEvent e, double x, double y, double width)
    {
        var height = e.Relevance switch { PlannerRelevance.Clash => 5.0, PlannerRelevance.Near => 3.0, _ => 2.0 };
        var inset = 1.0;
        Mark(w, style, e, x + inset, y, Math.Max(2, width - 2 * inset), height, upright: false);
    }

    /// <summary>The marker before an event's line in a month or day list: 10 high, 4 wide when it clashes or is close, 2 when other,
    /// drawn by <see cref="Mark"/> standing up.</summary>
    private static void Marker(SvgWriter w, ChartStyle style, PlannerEvent e, double x, double y) =>
        Mark(w, style, e, x, y, 10, e.Relevance == PlannerRelevance.Other ? 2 : 4, upright: true);

    /// <summary>An event's mark, <paramref name="length"/> along and <paramref name="thick"/> across, lying from (x, y) or standing up
    /// from it: the text colour when it clashes or is close, muted when other; dashed when close; an outline with hatching when
    /// provisional or with a strike in the text colour when cancelled (never faded, so every mark keeps 3:1); and an outline in the
    /// text colour round it when it is the viewer's own.</summary>
    private static void Mark(SvgWriter w, ChartStyle style, PlannerEvent e, double x, double y, double length, double thick, bool upright)
    {
        var ink = e.Relevance == PlannerRelevance.Other ? style.Muted : style.Text;
        // Positions are worked out along the mark (a) and across it (c), then written as x and y, swapped when it stands up.
        var (a, c) = upright ? (y, x) : (x, y);
        string Box(double a0, double c0, double along, double across) => upright
            ? $"x='{N(c0)}' y='{N(a0)}' width='{N(across)}' height='{N(along)}'"
            : $"x='{N(a0)}' y='{N(c0)}' width='{N(along)}' height='{N(across)}'";
        string Segment(double a1, double c1, double a2, double c2) => upright
            ? $"x1='{N(c1)}' y1='{N(a1)}' x2='{N(c2)}' y2='{N(a2)}'"
            : $"x1='{N(a1)}' y1='{N(c1)}' x2='{N(a2)}' y2='{N(c2)}'";
        if (e.Mine) w.Add($"<rect {Box(a - 1.5, c - 1.5, length + 3, thick + 3)} rx='1.5' fill='none' stroke='{style.Text}' stroke-width='1'{w.Fixed}/>");
        if (e.Status != PlannerStatus.Confirmed)
            w.Add($"<rect {Box(a, c, length, thick)} fill='none' stroke='{ink}' stroke-width='1'{w.Fixed}/>");
        if (e.Status == PlannerStatus.Provisional)
            for (var h = a + 2; h < a + length; h += 3) w.Add($"<line {Segment(h, c + thick, Math.Min(h + thick, a + length), c)} stroke='{ink}' stroke-width='.8'/>");
        else if (e.Status == PlannerStatus.Cancelled)
            w.Add($"<line {Segment(a, c + thick / 2, a + length, c + thick / 2)} stroke='{style.Text}' stroke-width='1'/>");
        else if (e.Relevance == PlannerRelevance.Near)
            for (var s = a; s < a + length; s += 6) w.Add($"<rect {Box(s, c, Math.Min(4, a + length - s), thick)} fill='{ink}'/>");
        else w.Add($"<rect {Box(a, c, length, thick)} rx='1' fill='{ink}'/>");
    }

    private static readonly (string Word, string Kind)[] LegendItems =
    [
        ("clash", "clash"), ("close", "near"), ("other", "other"), ("provisional", "provisional"), ("cancelled", "cancelled"),
        ("yours", "mine"), ("public holiday", "holiday"), ("school holiday", "school"), ("long weekend", "long"), ("weekend", "weekend")
    ];
    private static double LegendItemWidth(string word) => 22 + ChartSvg.Wide(word) + 16;
    private static int LegendLines(int width)
    {
        double x = 24; var lines = 1;
        foreach (var (word, _) in LegendItems) { var wide = LegendItemWidth(word); if (x + wide > width - 24) { lines++; x = 24; } x += wide; }
        return lines;
    }
    private static void Legend(SvgWriter w, PlannerSpec spec, ChartStyle style, double y)
    {
        double x = 24;
        w.Add("<g class='lumen-legend' aria-hidden='true'>");
        foreach (var (word, kind) in LegendItems)
        {
            var wide = LegendItemWidth(word);
            if (x + wide > spec.Width - 24) { x = 24; y += 18; }
            var sample = new PlannerEvent("legend", word, spec.From)
            {
                Relevance = kind switch { "clash" => PlannerRelevance.Clash, "near" => PlannerRelevance.Near, _ => PlannerRelevance.Other },
                Status = kind switch { "provisional" => PlannerStatus.Provisional, "cancelled" => PlannerStatus.Cancelled, _ => PlannerStatus.Confirmed },
                Mine = kind == "mine"
            };
            switch (kind)
            {
                case "holiday": w.Add($"<path d='M{N(x + 8)},{N(y + 2)} l3.5,3.5 l-3.5,3.5 l-3.5,-3.5 Z' fill='{style.Text}'/>"); break;
                case "school": w.Add($"<rect x='{N(x)}' y='{N(y + 4)}' width='16' height='3' fill='{style.Muted}'/>"); break;
                case "long": w.Add($"<path d='M{N(x + 1)},{N(y + 3)} v3 H{N(x + 15)} v-3' fill='none' stroke='{style.Muted}' stroke-width='1.5'/>"); break;
                case "weekend": w.Add($"<rect x='{N(x)}' y='{N(y)}' width='16' height='10' fill='{style.Grid}'/>"); break;
                default: Shapes(w, style, sample, x, y + 3, 16); break;
            }
            w.Text(x + 22, y + 9, word, "font-size='11'");
            x += wide;
        }
        w.Add("</g>");
    }

    /// <summary>The whole period on a phone: a 34-high block per month with its name, a slot for each of its weekends (the grid colour,
    /// a muted outline, a diamond on a public holiday, a solid mark under it for a clash and a dashed one for only close events), and
    /// the month's clash and close events counted in words on the right, each once, weekdays included; cancelled events count as
    /// neither. A weekend is a run of consecutive weekend days; one across a month's end shows in both bars, each slot covering and
    /// naming the whole weekend. Each weekend is a group named with its counts and periods.</summary>
    private static string YearNarrow(PlannerSpec spec, PlannerIndex plan, ChartStyle style)
    {
        var months = PlannerCalendar.Months(spec);
        var height = 78 + Head(spec) + months.Count * 34 + LegendLines(spec.Width) * 18 + 24;
        var w = Writer(spec, style);
        ChartSvg.Begin(w, spec.Width, height, spec.Title, spec.Description);
        var top = 78 + w.Head;
        var holidays = plan.Periods;
        var all = plan.Events;
        var weekends = new List<(DateOnly From, DateOnly To)>();
        for (var d = spec.From; d <= spec.To; d = d.AddDays(1))
            if (plan.IsWeekend(d))
                if (weekends.Count > 0 && weekends[^1].To.DayNumber == d.DayNumber - 1) weekends[^1] = (weekends[^1].From, d);
                else weekends.Add((d, d));
        static string Words(int clashes, int close, string and) =>
            string.Join(and, new[] { clashes > 0 ? (clashes == 1 ? "1 clash" : $"{clashes} clashes") : null, close > 0 ? $"{close} close" : null }.OfType<string>());
        for (var r = 0; r < months.Count; r++)
        {
            var (year, month) = months[r];
            var y = top + r * 34;
            var first = new DateOnly(year, month, 1);
            var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            w.Text(24, y + 14, first.ToString("MMM yyyy", Invariant), "class='lumen-month' font-size='12'");
            double x = 96;
            foreach (var (from, to) in weekends.Where(k => k.From <= last && k.To >= first))
            {
                var events = all.Where(e => e.Start <= to && (e.End ?? e.Start) >= from).ToArray();
                int c = events.Count(e => CountsAs(e, PlannerRelevance.Clash)), n = events.Count(e => CountsAs(e, PlannerRelevance.Near));
                var periods = holidays.Where(p => p.From <= to && (p.To ?? p.From) >= from).ToArray();
                var words = new List<string>();
                if (c + n > 0) words.Add(Words(c, n, ", "));
                words.AddRange(periods.Select(p => p.Name).Distinct());
                var span = from.Year != to.Year ? $"{from.ToString("d MMMM yyyy", Invariant)} to {to.ToString("d MMMM yyyy", Invariant)}"
                    : from.Month != to.Month ? $"{from.ToString("d MMMM", Invariant)} to {to.ToString("d MMMM yyyy", Invariant)}"
                    : from.ToString("d MMMM yyyy", Invariant);
                var label = $"Weekend of {span}: {(words.Count == 0 ? "nothing" : string.Join(", ", words))}";
                // Not focusable in the static drawing; the interactive planner gives weekends focus.
                w.Add($"<g class='lumen-week' role='group' data-weekend='{Iso(from)}' aria-label='{E(label)}'><title>{E(label)}</title>");
                // The grid colour barely shows on the background, so a muted outline that clears 3:1 draws the slot.
                w.Add($"<rect x='{N(x)}' y='{N(y + 4)}' width='10' height='12' fill='{style.Grid}' stroke='{style.Muted}' stroke-width='1'{w.Fixed}/>");
                if (periods.Any(p => p.Kind == PeriodKind.PublicHoliday))
                    w.Add($"<path d='M{N(x + 5)},{N(y + 5)} l3.5,3.5 l-3.5,3.5 l-3.5,-3.5 Z' fill='{style.Text}'/>");
                if (c > 0) w.Add($"<rect x='{N(x)}' y='{N(y + 19)}' width='10' height='4' fill='{style.Text}'/>");
                else if (n > 0) w.Add($"<rect x='{N(x)}' y='{N(y + 19)}' width='4' height='2' fill='{style.Text}'/><rect x='{N(x + 6)}' y='{N(y + 19)}' width='4' height='2' fill='{style.Text}'/>");
                w.Add("</g>");
                x += 14;
            }
            var inMonth = all.Where(e => e.Start <= Min(last, spec.To) && (e.End ?? e.Start) >= Max(first, spec.From)).ToArray();
            var summary = Words(inMonth.Count(e => CountsAs(e, PlannerRelevance.Clash)), inMonth.Count(e => CountsAs(e, PlannerRelevance.Near)), " · ");
            // The counts stand on the background right of the slots, cut where a weekend of odd days leaves them less room.
            if (summary.Length > 0) w.Text(spec.Width - 24, y + 14, Fit(summary, spec.Width - 24 - (x + 8), 11), "class='lumen-muted' font-size='11' text-anchor='end'");
        }
        Legend(w, spec, style, top + months.Count * 34 + 8);
        w.Add("</svg>");
        return w.ToString();
    }
    private const double CellH = 104, LineH = 14;

    private static string Month(PlannerSpec spec, PlannerIndex plan, ChartStyle style, int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var lead = PlannerCalendar.Lead(year, month, spec.WeekStart);
        var days = DateTime.DaysInMonth(year, month);
        var weeks = (lead + days + 6) / 7;
        var description = $"{first.ToString("MMMM yyyy", Invariant)}{(spec.Description.Length > 0 ? " · " + spec.Description : "")}";
        // The month leads the description, so its room is worked out on that longer line.
        var height = (int)Math.Ceiling(78 + Head(spec with { Description = description }) + 20 + weeks * CellH + LegendLines(spec.Width) * 18 + 24);
        var w = Writer(spec, style);
        ChartSvg.Begin(w, spec.Width, height, spec.Title, description);
        var top = 78 + w.Head;
        var col = (spec.Width - 48) / 7.0;
        for (var c = 0; c < 7; c++)
            w.Text(24 + c * col + 6, top + 13, ((DayOfWeek)(((int)spec.WeekStart + c) % 7)).ToString(), "class='lumen-muted lumen-weekday' font-size='11'");
        for (var d = first; d.Month == month; d = d.AddDays(1))
        {
            var cell = lead + d.Day - 1;
            var x = 24 + cell % 7 * col; var y = top + 20 + cell / 7 * CellH;
            var weekend = plan.IsWeekend(d);
            w.Add($"<g class='lumen-day' role='group' data-day='{Iso(d)}' aria-label='{E(plan.DayName(d))}'>");
            w.Add(weekend
                ? $"<rect class='lumen-weekend' x='{N(x)}' y='{N(y)}' width='{N(col)}' height='{N(CellH)}' fill='{style.Grid}' stroke='{style.Grid}' stroke-width='1'{w.Fixed}/>"
                : $"<rect x='{N(x)}' y='{N(y)}' width='{N(col)}' height='{N(CellH)}' fill='none' stroke='{style.Grid}' stroke-width='1'{w.Fixed}/>");
            w.Text(x + 6, y + 15, d.Day.ToString(Invariant), "font-size='12' font-weight='600'");
            if (d < spec.From || d > spec.To) { w.Add("</g>"); continue; }
            var (onDay, events) = plan.On(d);
            if (onDay.Any(p => p.Kind == PeriodKind.SchoolHoliday)) w.Add($"<rect x='{N(x)}' y='{N(y)}' width='{N(col)}' height='3' fill='{style.Muted}'/>");
            var named = onDay.Where(p => p.Kind != PeriodKind.SchoolHoliday).Select(p => p.Name).ToArray();
            // Muted words fall below 4.5:1 on a weekend cell's grid colour, so there the holidays and "+N more" are in the text colour.
            if (named.Length > 0) w.Text(x + 6, y + 29, Fit(string.Join(" · ", named), col - 12, 10), weekend ? "font-size='10'" : "class='lumen-muted' font-size='10'");
            // Four lines fit under the date and holidays; a day with more writes three and "+N more" naming the rest.
            var room = (int)Math.Floor((CellH - 44 - 4) / LineH);
            var shown = events.Count <= room ? events.Count : room - 1;
            for (var i = 0; i < shown; i++)
            {
                var e = events[i];
                var ly = y + 44 + i * LineH;
                var span = (e.End ?? e.Start).DayNumber - e.Start.DayNumber + 1;
                var word = e.Relevance switch { PlannerRelevance.Clash => " · clash", PlannerRelevance.Near => " · close", _ => "" };
                var dayOf = span > 1 ? $" · day {d.DayNumber - e.Start.DayNumber + 1} of {span}" : "";
                var region = e.Region is null ? "" : " · " + e.Region;
                var name = plan.Name(e);
                w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-event='{E(e.Id)}' aria-label='{E(name)}'><title>{E(name)}</title>");
                Marker(w, style, e, x + 6, ly - 9);
                // The region comes last: where the cell is narrow it is cut first, and it is said whole in the name and the day view.
                w.Text(x + 14, ly, Fit(e.Name + word + dayOf + region, col - 20, 10), "font-size='10'");
                w.Add("</g>");
            }
            if (shown < events.Count)
            {
                var rest = events.Skip(shown).ToArray();
                var label = $"{rest.Length} more on {PlannerCalendar.Day(d)}: {string.Join("; ", rest.Select(e => e.Name))}";
                // The tooltip sits on a presentational group, so the text's own value is just "+N more" and it is named once.
                w.Add($"<g role='presentation'><title>{E(label)}</title><text class='lumen-more{(weekend ? "" : " lumen-muted")}' role='img' x='{N(x + 6)}' y='{N(y + 44 + shown * LineH)}' font-size='10' aria-label='{E(label)}'>+{rest.Length} more</text></g>");
            }
            w.Add("</g>");
        }
        Legend(w, spec, style, top + 20 + weeks * CellH + 12);
        w.Add("</svg>");
        return w.ToString();
    }

    /// <summary><paramref name="text"/> cut with "…" to fit <paramref name="room"/> units at <paramref name="size"/> pixels.</summary>
    /// <remarks>It keeps the longest start that fits with the "…" after it (one character at least), found in one pass by adding up the
    /// characters' widths as <see cref="ChartSvg.Wide"/> does, and never ends between the two halves of a surrogate pair.</remarks>
    private static string Fit(string text, double room, double size)
    {
        if (ChartSvg.Wide(text) * size / 11 <= room) return text;
        // The same sums, in the same order, as the width of each start with "…" after it, so the cut is the one a search would find.
        double sum = 0; var keep = 1;
        for (var i = 0; i < text.Length - 1; i++)
        {
            sum += ChartSvg.Glyph(text[i]);
            if ((sum + ChartSvg.Glyph('…')) * 11 * size / 11 > room) break;
            keep = i + 1;
        }
        if (keep > 0 && char.IsHighSurrogate(text[keep - 1])) keep--;
        return text[..keep].TrimEnd() + "…";
    }

    /// <summary>The month <paramref name="year"/>-<paramref name="month"/> as an HTML table for static pages: rows are weeks, columns
    /// weekdays, each cell its day's holidays and events in words. Checks the spec and the month first.</summary>
    public static string Table(PlannerSpec spec, int year, int month)
    {
        PlannerValidation.Validate(spec, PlannerView.Month(year, month));
        var plan = new PlannerIndex(spec);
        var first = new DateOnly(year, month, 1);
        var lead = PlannerCalendar.Lead(year, month, spec.WeekStart);
        var days = DateTime.DaysInMonth(year, month);
        var b = new System.Text.StringBuilder();
        b.Append($"<table class='lumen-planner-table'><caption>{E(spec.Title)}, {first.ToString("MMMM yyyy", Invariant)}</caption><thead><tr>");
        for (var c = 0; c < 7; c++) b.Append($"<th scope='col'>{(DayOfWeek)(((int)spec.WeekStart + c) % 7)}</th>");
        b.Append("</tr></thead><tbody>");
        for (var cell = 0; cell < (lead + days + 6) / 7 * 7; cell++)
        {
            if (cell % 7 == 0) b.Append("<tr>");
            var day = cell - lead + 1;
            if (day < 1 || day > days) b.Append("<td></td>");
            else
            {
                var d = new DateOnly(year, month, day);
                var words = new List<string> { day.ToString(Invariant) };
                // As in the drawing, a day outside the period is its date alone.
                if (d >= spec.From && d <= spec.To)
                {
                    var (periods, events) = plan.On(d);
                    words.AddRange(periods.Select(p => $"{p.Name} ({PlannerCalendar.KindWords(p.Kind)})"));
                    words.AddRange(events.Select(plan.Name));
                }
                b.Append($"<td>{E(string.Join(" · ", words))}</td>");
            }
            if (cell % 7 == 6) b.Append("</tr>");
        }
        b.Append("</tbody></table>");
        return b.ToString();
    }
    /// <summary>A month on a phone, as an agenda of only the days that hold a period or an event: each a bold header with its periods
    /// muted after it, then its events one per line (marker, name, region and relevance), cut to fit. An empty month says so.</summary>
    private static string Agenda(PlannerSpec spec, PlannerIndex plan, ChartStyle style, int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var days = new List<(DateOnly Day, IReadOnlyList<PlannerPeriod> Periods, IReadOnlyList<PlannerEvent> Events)>();
        for (var d = first; d.Month == month; d = d.AddDays(1))
        {
            if (d < spec.From || d > spec.To) continue;
            var (periods, events) = plan.On(d);
            if (periods.Count > 0 || events.Count > 0) days.Add((d, periods, events));
        }
        var description = $"{first.ToString("MMMM yyyy", Invariant)}{(spec.Description.Length > 0 ? " · " + spec.Description : "")}";
        var body = days.Count == 0 ? 24 : days.Sum(x => 22 + x.Events.Count * 18 + 6);
        // As in the month grid, the description is set in two lines at most, each cut to the width, and the room is that of the
        // month-led line Begin draws.
        var height = 78 + Head(spec with { Description = description }) + body + 16;
        var w = Writer(spec, style);
        ChartSvg.Begin(w, spec.Width, height, spec.Title, description);
        var y = 78 + w.Head + 12;
        if (days.Count == 0) w.Text(24, y, "Nothing scheduled", "class='lumen-muted' font-size='11'");
        foreach (var (day, periods, events) in days)
        {
            w.Add($"<g class='lumen-day' role='group' data-day='{Iso(day)}' aria-label='{E(plan.DayName(day))}'>");
            var head = day.ToString("dddd d MMMM", Invariant);
            w.Text(24, y, head, "class='lumen-agenda-day' font-size='12' font-weight='600'");
            if (periods.Count > 0)
            {
                var headWide = ChartSvg.Wide(head) * 12 / 11 + 8;
                w.Text(24 + headWide, y, Fit(string.Join(" · ", periods.Select(p => p.Name)), spec.Width - 48 - headWide, 10), "class='lumen-muted' font-size='10'");
            }
            y += 18;
            foreach (var e in events)
            {
                var name = plan.Name(e);
                // A cancelled event is neither a clash nor close, and says it is cancelled.
                var word = e.Status == PlannerStatus.Cancelled ? " · cancelled"
                    : e.Relevance switch { PlannerRelevance.Clash => " · clash", PlannerRelevance.Near => " · close", _ => "" };
                w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-event='{E(e.Id)}' aria-label='{E(name)}'><title>{E(name)}</title>");
                Marker(w, style, e, 26, y - 9);
                w.Text(36, y, Fit(e.Name + (e.Region is null ? "" : " · " + e.Region) + word, spec.Width - 60, 11), "font-size='11'");
                w.Add("</g>");
                y += 18;
            }
            w.Add("</g>");
            y += 10;
        }
        w.Add("</svg>");
        return w.ToString();
    }

    /// <summary>A day as a list: its date, its holidays and periods as muted lines, then a block for each event (46 high, 58 with a note)
    /// with its marker, name and a muted line of region, category, audience, status, relevance and who it is for in words.</summary>
    private static string DayList(PlannerSpec spec, PlannerIndex plan, ChartStyle style, DateOnly day)
    {
        var (periods, events) = plan.On(day);
        double Block(PlannerEvent e) => string.IsNullOrWhiteSpace(e.Note) ? 46 : 58;
        var body = 30 + periods.Count * 16 + (events.Count == 0 ? 20 : events.Sum(Block));
        var height = (int)Math.Ceiling(78 + Head(spec) + body + 16);
        var w = Writer(spec, style);
        ChartSvg.Begin(w, spec.Width, height, spec.Title, spec.Description);
        double y = 78 + w.Head + 14;
        w.Add($"<g class='lumen-day' role='group' data-day='{Iso(day)}' aria-label='{E(plan.DayName(day))}'>");
        w.Text(24, y, PlannerCalendar.Day(day), "font-size='15' font-weight='600'");
        y += 20;
        foreach (var p in periods) { w.Text(24, y, Fit($"{p.Name} ({PlannerCalendar.KindWords(p.Kind)})", spec.Width - 48, 11), "class='lumen-muted' font-size='11'"); y += 16; }
        if (events.Count == 0) w.Text(24, y + 4, "No events", "class='lumen-muted' font-size='11'");
        foreach (var e in events)
        {
            var name = plan.Name(e);
            w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-event='{E(e.Id)}' aria-label='{E(name)}'><title>{E(name)}</title>");
            Marker(w, style, e, 24, y + 4);
            w.Text(34, y + 14, Fit(e.Name, spec.Width - 58, 13), "font-size='13' font-weight='600'");
            var facts = new[]
            {
                plan.RegionName(e.Region), e.Category, e.Audience,
                e.Status switch { PlannerStatus.Provisional => "provisional", PlannerStatus.Cancelled => "cancelled", _ => null },
                e.Relevance switch { PlannerRelevance.Clash => "clash", PlannerRelevance.Near => "close", _ => null },
                e.Mine ? "yours" : null,
                e.End is { } end && end != e.Start ? PlannerCalendar.Span(e.Start, end) : null
            }.Where(f => !string.IsNullOrWhiteSpace(f));
            var line = string.Join(" · ", facts);
            if (line.Length > 0) w.Text(34, y + 30, Fit(line, spec.Width - 58, 11), "class='lumen-muted' font-size='11'");
            if (!string.IsNullOrWhiteSpace(e.Note)) w.Text(34, y + 44, Fit(e.Note!, spec.Width - 58, 11), "font-size='11'");
            w.Add("</g>");
            y += Block(e);
        }
        w.Add("</g></svg>");
        return w.ToString();
    }
}
