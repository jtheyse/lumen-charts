using System.Globalization;

namespace Lumen.Charts;

/// <summary>A planner's dates, regions and filters, worked out once for the views that draw them.</summary>
internal static class PlannerCalendar
{
    /// <summary>Columns in a weekday-aligned month row: up to six blank days before the 1st, plus 31.</summary>
    internal const int Columns = 37;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    internal static IReadOnlyList<(int Year, int Month)> Months(PlannerSpec spec)
    {
        var list = new List<(int, int)>();
        for (var d = new DateOnly(spec.From.Year, spec.From.Month, 1); d <= spec.To; d = d.AddMonths(1)) list.Add((d.Year, d.Month));
        return list;
    }

    internal static int Lead(int year, int month, DayOfWeek weekStart) =>
        ((int)new DateOnly(year, month, 1).DayOfWeek - (int)weekStart + 7) % 7;

    internal static bool IsWeekend(PlannerSpec spec, DateOnly day) => spec.Weekend.Contains(day.DayOfWeek);

    internal static bool Includes(PlannerSpec spec, string ancestor, string code)
    {
        var parents = spec.Regions.ToDictionary(r => r.Code, r => r.Parent, StringComparer.Ordinal);
        for (string? at = code; at is not null; at = parents.GetValueOrDefault(at))
            if (string.Equals(at, ancestor, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>An item set for <paramref name="itemRegion"/> shows when no region is filtered, when it has no region, when it lies in
    /// a filtered region, or when a filtered region lies in it (a country's holiday under one of its provinces).</summary>
    internal static bool Applies(PlannerSpec spec, string? itemRegion)
    {
        var regions = spec.Filter?.Regions ?? [];
        if (itemRegion is null || regions.Count == 0) return true;
        return regions.Any(r => Includes(spec, r, itemRegion) || Includes(spec, itemRegion, r));
    }

    internal static IReadOnlyList<PlannerPeriod> Periods(PlannerSpec spec) =>
        spec.Periods.Where(p => Applies(spec, p.Region)).ToArray();

    internal static IReadOnlyList<PlannerEvent> Events(PlannerSpec spec)
    {
        var f = spec.Filter ?? new PlannerFilter();
        return spec.Events
            .Where(e => Applies(spec, e.Region))
            .Where(e => f.Categories.Count == 0 || e.Category is not null && f.Categories.Contains(e.Category, StringComparer.Ordinal))
            .Where(e => f.Audiences.Count == 0 || e.Audience is not null && f.Audiences.Contains(e.Audience, StringComparer.Ordinal))
            .Where(e => f.Statuses.Count == 0 || f.Statuses.Contains(e.Status))
            .Where(e => f.Relevances.Count == 0 || f.Relevances.Contains(e.Relevance))
            .OrderBy(e => e.Start).ThenByDescending(e => e.Relevance).ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool Covers(DateOnly from, DateOnly? to, DateOnly day) => from <= day && day <= (to ?? from);

    internal static (IReadOnlyList<PlannerPeriod> Periods, IReadOnlyList<PlannerEvent> Events) On(PlannerSpec spec, DateOnly day) =>
        (Periods(spec).Where(p => Covers(p.From, p.To, day)).ToArray(),
         Events(spec).Where(e => Covers(e.Start, e.End, day)).OrderByDescending(e => e.Relevance).ThenBy(e => e.Start).ThenBy(e => e.Name, StringComparer.Ordinal).ToArray());

    internal static IReadOnlyList<(DateOnly From, DateOnly To)> LongWeekends(PlannerSpec spec)
    {
        var holidays = Periods(spec).Where(p => p.Kind == PeriodKind.PublicHoliday).ToArray();
        bool Holiday(DateOnly d) => holidays.Any(p => Covers(p.From, p.To, d));
        var runs = new List<(DateOnly, DateOnly)>();
        DateOnly? start = null; var hasHoliday = false;
        for (var d = spec.From; d <= spec.To.AddDays(1); d = d.AddDays(1))
        {
            var off = d <= spec.To && (IsWeekend(spec, d) || Holiday(d));
            if (off) { start ??= d; hasHoliday |= Holiday(d); continue; }
            if (start is { } s && hasHoliday && d.DayNumber - s.DayNumber >= 3) runs.Add((s, d.AddDays(-1)));
            start = null; hasHoliday = false;
        }
        return runs;
    }

    internal static string? RegionName(PlannerSpec spec, string? code) =>
        code is null ? null : spec.Regions.FirstOrDefault(r => r.Code == code)?.Name ?? code;

    internal static string Day(DateOnly d) => d.ToString("dddd d MMMM yyyy", Invariant);

    internal static string Span(DateOnly start, DateOnly? end)
    {
        if (end is not { } e || e == start) return Day(start);
        if (start.Year == e.Year && start.Month == e.Month) return $"{start.ToString("dddd d", Invariant)} to {Day(e)}";
        if (start.Year == e.Year) return $"{start.ToString("dddd d MMMM", Invariant)} to {Day(e)}";
        return $"{Day(start)} to {Day(e)}";
    }

    internal static string Name(PlannerSpec spec, PlannerEvent e)
    {
        var parts = new List<string> { e.Name, Span(e.Start, e.End) };
        if (RegionName(spec, e.Region) is { } region) parts.Add(region);
        if (!string.IsNullOrWhiteSpace(e.Category)) parts.Add(e.Category!);
        if (!string.IsNullOrWhiteSpace(e.Audience)) parts.Add(e.Audience!);
        if (e.Status == PlannerStatus.Provisional) parts.Add("provisional");
        if (e.Status == PlannerStatus.Cancelled) parts.Add("cancelled");
        if (e.Relevance == PlannerRelevance.Clash) parts.Add("clash");
        if (e.Relevance == PlannerRelevance.Near) parts.Add("close");
        if (e.Mine) parts.Add("yours");
        if (!string.IsNullOrWhiteSpace(e.Note)) parts.Add(e.Note!);
        return string.Join(", ", parts);
    }

    internal static string KindWords(PeriodKind kind) => kind switch
    {
        PeriodKind.PublicHoliday => "public holiday",
        PeriodKind.SchoolHoliday => "school holiday",
        _ => "period"
    };

    internal static string DayName(PlannerSpec spec, DateOnly day)
    {
        var (periods, events) = On(spec, day);
        var parts = new List<string> { Day(day) };
        parts.AddRange(periods.Select(p => $"{p.Name} ({KindWords(p.Kind)})"));
        if (LongWeekends(spec).Any(w => w.From <= day && day <= w.To)) parts.Add("long weekend");
        parts.Add(events.Count switch { 0 => "no events", 1 => "1 event", var n => $"{n} events" });
        return string.Join(", ", parts);
    }
}
