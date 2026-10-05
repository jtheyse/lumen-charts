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
        var style = spec.Style ?? ChartSvg.Preset(spec.Theme);
        return (view.Zoom, layout) switch
        {
            (PlannerZoom.Year, PlannerLayout.Wide) => Year(spec, style),
            (PlannerZoom.Year, PlannerLayout.Narrow) => YearNarrow(spec, style),
            (PlannerZoom.Month, PlannerLayout.Wide) => Month(spec, style, view.Date.Year, view.Date.Month),
            (PlannerZoom.Month, PlannerLayout.Narrow) => Agenda(spec, style, view.Date.Year, view.Date.Month),
            _ => DayList(spec, style, view.Date)
        };
    }

    private static SvgWriter Writer(PlannerSpec spec, ChartStyle style) =>
        new() { Style = style, Painted = spec.PaintBackground, Titled = spec.DrawTitles };

    private static string Year(PlannerSpec spec, ChartStyle style)
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
        var longs = PlannerCalendar.LongWeekends(spec);
        var periods = PlannerCalendar.Periods(spec);
        var events = PlannerCalendar.Events(spec);
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
                w.Add($"<g class='lumen-day' data-day='{d:yyyy-MM-dd}' aria-label='{E(PlannerCalendar.DayName(spec, d))}'>");
                if (PlannerCalendar.IsWeekend(spec, d)) w.Add($"<rect class='lumen-weekend' x='{N(x)}' y='{N(y)}' width='{N(col)}' height='{N(RowH - 2)}' fill='{style.Grid}'/>");
                foreach (var p in periods.Where(p => p.From <= d && d <= (p.To ?? p.From)))
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
                Stripe(w, spec, style, e, X(start), y + 18 + lane * 7, X(end) + col - X(start));
            }
            foreach (var (day, rest) in hidden)
            {
                var label = $"{rest.Count} more on {PlannerCalendar.Day(day)}: {string.Join("; ", rest.Select(e => e.Name))}";
                w.Add($"<text class='lumen-more' x='{N(X(day) + col / 2)}' y='{N(y + 44)}' font-size='10' text-anchor='middle' aria-label='{E(label)}'><title>{E(label)}</title>+{rest.Count}</text>");
            }
            // Busy weeks: each week of the row is named with its clash and close events; a busy one writes its count. That count and
            // "+N" are written in the text colour, since they may stand on a weekend band, where the muted colour falls below 4.5:1.
            for (var k = 0; k * 7 < lead + last.Day; k++)
            {
                var weekFirst = Max(Max(first, spec.From), first.AddDays(k * 7 - lead));
                var weekLast = Min(Min(last, spec.To), first.AddDays(k * 7 - lead + 6));
                if (weekLast < weekFirst) continue;
                var inWeek = events.Where(e => e.Start <= weekLast && (e.End ?? e.Start) >= weekFirst).ToArray();
                var clashes = inWeek.Count(e => e.Relevance == PlannerRelevance.Clash);
                var near = inWeek.Count(e => e.Relevance == PlannerRelevance.Near);
                var words = new List<string>();
                if (clashes > 0) words.Add(clashes == 1 ? "1 clash" : $"{clashes} clashes");
                if (near > 0) words.Add($"{near} close");
                var label = $"Week of {weekFirst.ToString("d MMMM yyyy", Invariant)}: {(words.Count == 0 ? "no clashes" : string.Join(", ", words))}";
                w.Add($"<g class='lumen-week' aria-label='{E(label)}'>");
                if (clashes + near > 0) w.Text(Left + (k * 7 + 3.5) * col, y + 54, (clashes + near).ToString(Invariant), "font-size='10' text-anchor='middle'");
                w.Add("</g>");
            }
        }
        Legend(w, spec, style, top + HeaderH + months.Count * RowH + 8);
        w.Add("</svg>");
        return w.ToString();
    }

    private static int Head(PlannerSpec spec) => !spec.DrawTitles ? -ChartSvg.Untitled : 14 * (ChartSvg.Wrap(spec.Description, spec.Width - 48d).Length - 1);
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static IEnumerable<DateOnly> Days(DateOnly from, DateOnly to) { for (var d = from; d <= to; d = d.AddDays(1)) yield return d; }

    /// <summary>One event's mark: a focusable group named in words around the stripe <see cref="Shapes"/> draws.</summary>
    private static void Stripe(SvgWriter w, PlannerSpec spec, ChartStyle style, PlannerEvent e, double x, double y, double width)
    {
        var name = PlannerCalendar.Name(spec, e);
        w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-event='{E(e.Id)}' aria-label='{E(name)}'><title>{E(name)}</title>");
        Shapes(w, style, e, x, y, width);
        w.Add("</g>");
    }

    /// <summary>A stripe's shapes: weight and dash by relevance, an outline with hatching when provisional or with a strike when
    /// cancelled (never faded, so every mark keeps 3:1), and an outline in the text colour when it is the viewer's own. The legend
    /// draws its samples with them too.</summary>
    private static void Shapes(SvgWriter w, ChartStyle style, PlannerEvent e, double x, double y, double width)
    {
        var (ink, height) = e.Relevance switch
        {
            PlannerRelevance.Clash => (style.Text, 5.0),
            PlannerRelevance.Near => (style.Text, 3.0),
            _ => (style.Muted, 2.0)
        };
        var inset = 1.0; x += inset; width = Math.Max(2, width - 2 * inset);
        if (e.Mine) w.Add($"<rect x='{N(x - 1.5)}' y='{N(y - 1.5)}' width='{N(width + 3)}' height='{N(height + 3)}' rx='1.5' fill='none' stroke='{style.Text}' stroke-width='1'{w.Fixed}/>");
        if (e.Status != PlannerStatus.Confirmed)
            w.Add($"<rect x='{N(x)}' y='{N(y)}' width='{N(width)}' height='{N(height)}' fill='none' stroke='{ink}' stroke-width='1'{w.Fixed}/>");
        if (e.Status == PlannerStatus.Provisional)
            for (var hx = x + 2; hx < x + width; hx += 3) w.Add($"<line x1='{N(hx)}' y1='{N(y + height)}' x2='{N(Math.Min(hx + height, x + width))}' y2='{N(y)}' stroke='{ink}' stroke-width='.8'/>");
        else if (e.Status == PlannerStatus.Cancelled)
            w.Add($"<line x1='{N(x)}' y1='{N(y + height / 2)}' x2='{N(x + width)}' y2='{N(y + height / 2)}' stroke='{style.Text}' stroke-width='1'/>");
        else if (e.Relevance == PlannerRelevance.Near)
            for (var sx = x; sx < x + width; sx += 6) w.Add($"<rect x='{N(sx)}' y='{N(y)}' width='{N(Math.Min(4, x + width - sx))}' height='{N(height)}' fill='{ink}'/>");
        else w.Add($"<rect x='{N(x)}' y='{N(y)}' width='{N(width)}' height='{N(height)}' rx='1' fill='{ink}'/>");
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

    private static string YearNarrow(PlannerSpec spec, ChartStyle style) => throw new NotImplementedException("Task 6");
    private static string Month(PlannerSpec spec, ChartStyle style, int year, int month) => throw new NotImplementedException("Task 4");
    private static string Agenda(PlannerSpec spec, ChartStyle style, int year, int month) => throw new NotImplementedException("Task 6");
    private static string DayList(PlannerSpec spec, ChartStyle style, DateOnly day) => throw new NotImplementedException("Task 5");
}
