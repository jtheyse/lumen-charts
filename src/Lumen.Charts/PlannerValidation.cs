using System.Globalization;

namespace Lumen.Charts;

/// <summary>Checks a <see cref="PlannerSpec"/> before it is drawn; every refusal is an <see cref="ArgumentException"/> saying why.</summary>
public static class PlannerValidation
{
    /// <summary>The longest period a planner draws, in days.</summary>
    public const int MaxDays = 400;
    /// <summary>The most events a planner takes.</summary>
    public const int MaxEvents = 2000;
    /// <summary>The most holidays and other periods a planner takes.</summary>
    public const int MaxPeriods = 1000;
    /// <summary>The most regions a planner takes.</summary>
    public const int MaxRegions = 500;
    /// <summary>The longest title, description, or region, period or event name, in characters.</summary>
    public const int MaxText = 200;
    /// <summary>The longest note on an event, in characters.</summary>
    public const int MaxNote = 120;

    /// <summary>Checks <paramref name="spec"/>.</summary>
    public static void Validate(PlannerSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (string.IsNullOrWhiteSpace(spec.Title)) throw new ArgumentException("A planner needs a title: it is its heading and its accessible name.");
        Short(spec.Title, "A planner's title");
        if (spec.Description is null) throw new ArgumentException("A planner's description may be empty but not null.");
        Short(spec.Description, "A planner's description");
        if (spec.Width is < 320 or > 4096) throw new ArgumentException("A planner's width must be 320–4096.");
        if (spec.To < spec.From) throw new ArgumentException("A planner's period must end on or after the day it starts.");
        if (spec.To.DayNumber - spec.From.DayNumber + 1 > MaxDays) throw new ArgumentException($"A planner shows at most {MaxDays} days; draw a longer span as two planners.");
        if (!Enum.IsDefined(spec.WeekStart)) throw new ArgumentException($"A planner's week must start on a day of the week, not {Number((int)spec.WeekStart)}.");
        if (spec.Weekend is null) throw new ArgumentException("A planner's weekend is missing; leave it out for Saturday and Sunday.");
        foreach (var day in spec.Weekend)
            if (!Enum.IsDefined(day)) throw new ArgumentException($"A planner's weekend names {Number((int)day)}, which is not a day of the week.");
        if (spec.Weekend.Count == 0 || spec.Weekend.Distinct().Count() != spec.Weekend.Count) throw new ArgumentException("A planner's weekend names at least one day, each once.");
        if (spec.Regions is null) throw new ArgumentException("A planner's list of regions is missing; leave it empty for none.");
        if (spec.Regions.Count > MaxRegions) throw new ArgumentException($"A planner takes at most {MaxRegions} regions.");
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var region in spec.Regions)
        {
            if (region is null) throw new ArgumentException("A planner's list of regions holds a missing region.");
            if (string.IsNullOrWhiteSpace(region.Code) || string.IsNullOrWhiteSpace(region.Name)) throw new ArgumentException("A region needs a code and a name.");
            Short(region.Name, $"The name of the region '{region.Code}'");
            if (!codes.Add(region.Code)) throw new ArgumentException($"The region code '{region.Code}' is used twice.");
        }
        var parents = spec.Regions.ToDictionary(r => r.Code, r => r.Parent, StringComparer.Ordinal);
        foreach (var region in spec.Regions)
            if (region.Parent is not null && !codes.Contains(region.Parent)) throw new ArgumentException($"The region '{region.Code}' names a parent, '{region.Parent}', that is not in the list.");
        foreach (var region in spec.Regions)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { region.Code };
            for (var up = region.Parent; up is not null; up = parents[up])
                if (!seen.Add(up)) throw new ArgumentException($"The region '{region.Code}' lies inside itself: its parents loop.");
        }
        void Known(string? code, string what)
        {
            if (code is not null && !codes.Contains(code)) throw new ArgumentException($"{what} names the region '{code}', which is not in the planner's regions.");
        }
        if (spec.Periods is null) throw new ArgumentException("A planner's list of holidays and periods is missing; leave it empty for none.");
        if (spec.Periods.Count > MaxPeriods) throw new ArgumentException($"A planner takes at most {MaxPeriods} holidays and periods.");
        foreach (var period in spec.Periods)
        {
            if (period is null) throw new ArgumentException("A planner's list of holidays and periods holds a missing one.");
            if (string.IsNullOrWhiteSpace(period.Name)) throw new ArgumentException("A holiday or period needs a name: it is written and said.");
            Short(period.Name, $"The name of the holiday or period starting {Iso(period.From)}");
            if (!Enum.IsDefined(period.Kind)) throw new ArgumentException($"'{period.Name}' is of kind {Number((int)period.Kind)}; it must be a public holiday, a school holiday or other.");
            if (period.To is { } to && to < period.From) throw new ArgumentException($"'{period.Name}' ends before it starts.");
            Known(period.Region, $"'{period.Name}'");
        }
        if (spec.Events is null) throw new ArgumentException("A planner's list of events is missing; leave it empty for none.");
        if (spec.Events.Count > MaxEvents) throw new ArgumentException($"A planner takes at most {MaxEvents} events; filter them before drawing.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in spec.Events)
        {
            if (e is null) throw new ArgumentException("A planner's list of events holds a missing event.");
            if (string.IsNullOrWhiteSpace(e.Id)) throw new ArgumentException("An event needs an id, which is given back when it is selected.");
            if (!ids.Add(e.Id)) throw new ArgumentException($"The event id '{e.Id}' is used twice.");
            if (string.IsNullOrWhiteSpace(e.Name)) throw new ArgumentException($"The event '{e.Id}' needs a name.");
            Short(e.Name, $"The name of the event '{e.Id}'");
            if (!Enum.IsDefined(e.Status)) throw new ArgumentException($"'{e.Name}' has status {Number((int)e.Status)}; it must be confirmed, provisional or cancelled.");
            if (!Enum.IsDefined(e.Relevance)) throw new ArgumentException($"'{e.Name}' has relevance {Number((int)e.Relevance)}; it must be other, near or clash.");
            var end = e.End ?? e.Start;
            if (end < e.Start) throw new ArgumentException($"'{e.Name}' ends before it starts.");
            if (end < spec.From || e.Start > spec.To) throw new ArgumentException($"'{e.Name}' falls wholly outside the planner's period, {Iso(spec.From)} to {Iso(spec.To)}.");
            if (e.Note is { Length: > MaxNote }) throw new ArgumentException($"'{e.Name}' has a note of more than {MaxNote} characters; keep it short and link to the rest.");
            Known(e.Region, $"'{e.Name}'");
        }
        if (spec.Filter is { } filter)
        {
            if (filter.Regions is null || filter.Categories is null || filter.Audiences is null || filter.Statuses is null || filter.Relevances is null)
                throw new ArgumentException("A planner's filter has a missing list; leave a list empty to show every value.");
            if (filter.Regions.Any(r => r is null) || filter.Categories.Any(c => c is null) || filter.Audiences.Any(a => a is null))
                throw new ArgumentException("A planner's filter lists a missing value.");
            foreach (var status in filter.Statuses)
                if (!Enum.IsDefined(status)) throw new ArgumentException($"The filter names status {Number((int)status)}; it must be confirmed, provisional or cancelled.");
            foreach (var relevance in filter.Relevances)
                if (!Enum.IsDefined(relevance)) throw new ArgumentException($"The filter names relevance {Number((int)relevance)}; it must be other, near or clash.");
            foreach (var code in filter.Regions) Known(code, "The filter");
        }
    }

    /// <summary>Checks <paramref name="spec"/> and <paramref name="view"/>: a month must overlap the planner's period, and a day lie
    /// inside it.</summary>
    public static void Validate(PlannerSpec spec, PlannerView view)
    {
        Validate(spec);
        switch (view.Zoom)
        {
            case PlannerZoom.Year:
                break;
            case PlannerZoom.Month:
                var first = new DateOnly(view.Date.Year, view.Date.Month, 1);
                if (first.AddMonths(1).AddDays(-1) < spec.From || first > spec.To) throw new ArgumentException($"The month {first.ToString("yyyy-MM", CultureInfo.InvariantCulture)} is outside the planner's period.");
                break;
            case PlannerZoom.Day:
                if (view.Date < spec.From || view.Date > spec.To) throw new ArgumentException($"The day {Iso(view.Date)} is outside the planner's period.");
                break;
            default:
                throw new ArgumentException($"A planner's view must be the year, a month or a day, not {Number((int)view.Zoom)}.");
        }
    }

    private static void Short(string text, string what)
    {
        if (text.Length > MaxText) throw new ArgumentException($"{what} is longer than {MaxText} characters.");
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Number(int n) => n.ToString(CultureInfo.InvariantCulture);
}
