using System.Globalization;

namespace Lumen.Charts;

/// <summary>A planner's dates, regions and filters. The views draw from a <see cref="PlannerIndex"/>, which works them out once
/// per drawing; the methods here that take a spec build one each call.</summary>
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

    /// <summary>Each region's parent by code; the first of a code used twice (which validation refuses) wins.</summary>
    internal static Dictionary<string, string?> Parents(PlannerSpec spec)
    {
        var parents = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var region in spec.Regions) parents.TryAdd(region.Code, region.Parent);
        return parents;
    }

    /// <summary><paramref name="code"/> and every region above it, nearest first; a loop (which validation refuses) ends the walk.</summary>
    internal static IEnumerable<string> Chain(Dictionary<string, string?> parents, string code)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (string? at = code; at is not null && seen.Add(at); at = parents.GetValueOrDefault(at)) yield return at;
    }

    internal static bool Includes(PlannerSpec spec, string ancestor, string code) =>
        Chain(Parents(spec), code).Contains(ancestor, StringComparer.Ordinal);

    internal static IReadOnlyList<PlannerPeriod> Periods(PlannerSpec spec) => new PlannerIndex(spec).Periods;

    internal static IReadOnlyList<PlannerEvent> Events(PlannerSpec spec) => new PlannerIndex(spec).Events;

    internal static bool Covers(DateOnly from, DateOnly? to, DateOnly day) => from <= day && day <= (to ?? from);

    internal static (IReadOnlyList<PlannerPeriod> Periods, IReadOnlyList<PlannerEvent> Events) On(PlannerSpec spec, DateOnly day) =>
        new PlannerIndex(spec).On(day);

    internal static IReadOnlyList<(DateOnly From, DateOnly To)> LongWeekends(PlannerSpec spec) => new PlannerIndex(spec).LongWeekends;

    internal static string? RegionName(PlannerSpec spec, string? code) => new PlannerIndex(spec).RegionName(code);

    internal static string Day(DateOnly d) => d.ToString("dddd d MMMM yyyy", Invariant);

    internal static string Span(DateOnly start, DateOnly? end)
    {
        if (end is not { } e || e == start) return Day(start);
        if (start.Year == e.Year && start.Month == e.Month) return $"{start.ToString("dddd d", Invariant)} to {Day(e)}";
        if (start.Year == e.Year) return $"{start.ToString("dddd d MMMM", Invariant)} to {Day(e)}";
        return $"{Day(start)} to {Day(e)}";
    }

    internal static string Name(PlannerSpec spec, PlannerEvent e) => new PlannerIndex(spec).Name(e);

    internal static string KindWords(PeriodKind kind) => kind switch
    {
        PeriodKind.PublicHoliday => "public holiday",
        PeriodKind.SchoolHoliday => "school holiday",
        _ => "period"
    };

    internal static string DayName(PlannerSpec spec, DateOnly day) => new PlannerIndex(spec).DayName(day);
}

/// <summary>A planner's periods and events as its filter leaves them, its long weekends and its region names, worked out once per
/// drawing, with each day's periods and events looked up by day rather than searched for.</summary>
internal sealed class PlannerIndex
{
    private static readonly IReadOnlyList<PlannerPeriod> NoPeriods = [];
    private static readonly IReadOnlyList<PlannerEvent> NoEvents = [];
    private readonly PlannerSpec spec;
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);
    private readonly bool[] weekend = new bool[7];
    private readonly IReadOnlyList<PlannerPeriod>[] periodsOn;
    private readonly IReadOnlyList<PlannerEvent>[] eventsOn;
    private readonly bool[] longOn;

    /// <summary>The periods the filter shows, in the spec's order.</summary>
    internal IReadOnlyList<PlannerPeriod> Periods { get; }
    /// <summary>The events the filter shows, by start, then clash first, then name.</summary>
    internal IReadOnlyList<PlannerEvent> Events { get; }
    /// <summary>Runs of three days or more off (weekend days and public holidays) that hold a public holiday.</summary>
    internal IReadOnlyList<(DateOnly From, DateOnly To)> LongWeekends { get; }

    internal PlannerIndex(PlannerSpec spec)
    {
        this.spec = spec;
        foreach (var region in spec.Regions) names.TryAdd(region.Code, region.Name);
        foreach (var day in spec.Weekend) weekend[(int)day] = true;
        var shown = Shown(spec);
        bool Applies(string? region) => region is null || shown is null || shown.Contains(region);
        Periods = spec.Periods.Where(p => Applies(p.Region)).ToArray();
        var f = spec.Filter ?? new PlannerFilter();
        var categories = new HashSet<string>(f.Categories, StringComparer.Ordinal);
        var audiences = new HashSet<string>(f.Audiences, StringComparer.Ordinal);
        var statuses = new HashSet<PlannerStatus>(f.Statuses);
        var relevances = new HashSet<PlannerRelevance>(f.Relevances);
        Events = spec.Events
            .Where(e => Applies(e.Region))
            .Where(e => categories.Count == 0 || e.Category is not null && categories.Contains(e.Category))
            .Where(e => audiences.Count == 0 || e.Audience is not null && audiences.Contains(e.Audience))
            .Where(e => statuses.Count == 0 || statuses.Contains(e.Status))
            .Where(e => relevances.Count == 0 || relevances.Contains(e.Relevance))
            .OrderBy(e => e.Start).ThenByDescending(e => e.Relevance).ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToArray();
        var days = Math.Max(0, spec.To.DayNumber - spec.From.DayNumber + 1);
        var periods = new List<PlannerPeriod>?[days];
        var events = new List<PlannerEvent>?[days];
        foreach (var p in Periods) foreach (var i in Offsets(p.From, p.To)) (periods[i] ??= []).Add(p);
        foreach (var e in Events) foreach (var i in Offsets(e.Start, e.End)) (events[i] ??= []).Add(e);
        periodsOn = periods.Select(list => list is null ? NoPeriods : list.ToArray()).ToArray();
        // A day's events are listed clash first, then by start and name; the sort is stable, so ties keep the events' order.
        eventsOn = events.Select(list => list is null ? NoEvents
            : list.OrderByDescending(e => e.Relevance).ThenBy(e => e.Start).ThenBy(e => e.Name, StringComparer.Ordinal).ToArray()).ToArray();
        // Days off are weekend days and public holidays; a run of them is a long weekend when it is three days or more and holds a holiday.
        longOn = new bool[days];
        var runs = new List<(DateOnly, DateOnly)>();
        int? start = null; var hasHoliday = false;
        for (var i = 0; i <= days; i++)
        {
            var holiday = i < days && periodsOn[i].Any(p => p.Kind == PeriodKind.PublicHoliday);
            var off = i < days && (weekend[(int)spec.From.AddDays(i).DayOfWeek] || holiday);
            if (off) { start ??= i; hasHoliday |= holiday; continue; }
            if (start is { } s && hasHoliday && i - s >= 3)
            {
                runs.Add((spec.From.AddDays(s), spec.From.AddDays(i - 1)));
                for (var k = s; k < i; k++) longOn[k] = true;
            }
            start = null; hasHoliday = false;
        }
        LongWeekends = runs;
    }

    /// <summary>The region codes the filter shows, or null when it names none: each filtered region, every region above one (a
    /// country's holiday shows under its province) and every region below one.</summary>
    private static HashSet<string>? Shown(PlannerSpec spec)
    {
        var filtered = spec.Filter?.Regions ?? [];
        if (filtered.Count == 0) return null;
        var parents = PlannerCalendar.Parents(spec);
        var wanted = new HashSet<string>(filtered, StringComparer.Ordinal);
        var shown = new HashSet<string>(StringComparer.Ordinal);
        foreach (var code in filtered) shown.UnionWith(PlannerCalendar.Chain(parents, code));
        foreach (var region in spec.Regions)
            if (PlannerCalendar.Chain(parents, region.Code).Any(wanted.Contains)) shown.Add(region.Code);
        return shown;
    }

    /// <summary>The offsets from the planner's first day of the days from <paramref name="from"/> to <paramref name="to"/> (or that
    /// one day) that lie inside the period.</summary>
    private IEnumerable<int> Offsets(DateOnly from, DateOnly? to)
    {
        var first = Math.Max(from.DayNumber, spec.From.DayNumber) - spec.From.DayNumber;
        var last = Math.Min((to ?? from).DayNumber, spec.To.DayNumber) - spec.From.DayNumber;
        for (var i = first; i <= last; i++) yield return i;
    }

    internal bool IsWeekend(DateOnly day) => weekend[(int)day.DayOfWeek];

    /// <summary>The periods and events on <paramref name="day"/>: periods in the spec's order, events clash first.</summary>
    internal (IReadOnlyList<PlannerPeriod> Periods, IReadOnlyList<PlannerEvent> Events) On(DateOnly day)
    {
        var i = day.DayNumber - spec.From.DayNumber;
        if (i >= 0 && i < periodsOn.Length) return (periodsOn[i], eventsOn[i]);
        return (Periods.Where(p => PlannerCalendar.Covers(p.From, p.To, day)).ToArray(),
            Events.Where(e => PlannerCalendar.Covers(e.Start, e.End, day)).OrderByDescending(e => e.Relevance).ThenBy(e => e.Start).ThenBy(e => e.Name, StringComparer.Ordinal).ToArray());
    }

    internal bool InLongWeekend(DateOnly day)
    {
        var i = day.DayNumber - spec.From.DayNumber;
        return i >= 0 && i < longOn.Length && longOn[i];
    }

    internal string? RegionName(string? code) => code is null ? null : names.GetValueOrDefault(code) ?? code;

    internal string Name(PlannerEvent e)
    {
        var parts = new List<string> { e.Name, PlannerCalendar.Span(e.Start, e.End) };
        if (RegionName(e.Region) is { } region) parts.Add(region);
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

    internal string DayName(DateOnly day)
    {
        var (periods, events) = On(day);
        var parts = new List<string> { PlannerCalendar.Day(day) };
        parts.AddRange(periods.Select(p => $"{p.Name} ({PlannerCalendar.KindWords(p.Kind)})"));
        if (InLongWeekend(day)) parts.Add("long weekend");
        parts.Add(events.Count switch { 0 => "no events", 1 => "1 event", var n => string.Create(CultureInfo.InvariantCulture, $"{n} events") });
        return string.Join(", ", parts);
    }
}
