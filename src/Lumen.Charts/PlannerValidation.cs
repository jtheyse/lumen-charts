namespace Lumen.Charts;

/// <summary>Checks a <see cref="PlannerSpec"/> before it is drawn; every refusal is an <see cref="ArgumentException"/> saying why.</summary>
public static class PlannerValidation
{
    /// <summary>The longest period a planner draws, in days.</summary>
    public const int MaxDays = 400;

    /// <summary>Checks <paramref name="spec"/>.</summary>
    public static void Validate(PlannerSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (string.IsNullOrWhiteSpace(spec.Title)) throw new ArgumentException("A planner needs a title: it is its heading and its accessible name.");
        if (spec.Width is < 320 or > 4096) throw new ArgumentException("A planner's width must be 320–4096.");
        if (spec.To < spec.From) throw new ArgumentException("A planner's period must end on or after the day it starts.");
        if (spec.To.DayNumber - spec.From.DayNumber + 1 > MaxDays) throw new ArgumentException($"A planner shows at most {MaxDays} days; draw a longer span as two planners.");
        if (spec.Weekend.Count == 0 || spec.Weekend.Distinct().Count() != spec.Weekend.Count) throw new ArgumentException("A planner's weekend names at least one day, each once.");
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var region in spec.Regions)
        {
            if (string.IsNullOrWhiteSpace(region.Code) || string.IsNullOrWhiteSpace(region.Name)) throw new ArgumentException("A region needs a code and a name.");
            if (!codes.Add(region.Code)) throw new ArgumentException($"The region code '{region.Code}' is used twice.");
        }
        var parents = spec.Regions.ToDictionary(r => r.Code, r => r.Parent, StringComparer.Ordinal);
        foreach (var region in spec.Regions)
        {
            if (region.Parent is not null && !codes.Contains(region.Parent)) throw new ArgumentException($"The region '{region.Code}' names a parent, '{region.Parent}', that is not in the list.");
            var seen = new HashSet<string>(StringComparer.Ordinal) { region.Code };
            for (var up = region.Parent; up is not null; up = parents[up])
                if (!seen.Add(up)) throw new ArgumentException($"The region '{region.Code}' lies inside itself: its parents loop.");
        }
        void Known(string? code, string what)
        {
            if (code is not null && !codes.Contains(code)) throw new ArgumentException($"{what} names the region '{code}', which is not in the planner's regions.");
        }
        foreach (var period in spec.Periods)
        {
            if (string.IsNullOrWhiteSpace(period.Name)) throw new ArgumentException("A holiday or period needs a name: it is written and said.");
            if (period.To is { } to && to < period.From) throw new ArgumentException($"'{period.Name}' ends before it starts.");
            Known(period.Region, $"'{period.Name}'");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in spec.Events)
        {
            if (string.IsNullOrWhiteSpace(e.Id)) throw new ArgumentException("An event needs an id, which is given back when it is selected.");
            if (!ids.Add(e.Id)) throw new ArgumentException($"The event id '{e.Id}' is used twice.");
            if (string.IsNullOrWhiteSpace(e.Name)) throw new ArgumentException($"The event '{e.Id}' needs a name.");
            var end = e.End ?? e.Start;
            if (end < e.Start) throw new ArgumentException($"'{e.Name}' ends before it starts.");
            if (end < spec.From || e.Start > spec.To) throw new ArgumentException($"'{e.Name}' falls wholly outside the planner's period, {spec.From:yyyy-MM-dd} to {spec.To:yyyy-MM-dd}.");
            if (e.Note is { Length: > 120 }) throw new ArgumentException($"'{e.Name}' has a note of more than 120 characters; keep it short and link to the rest.");
            Known(e.Region, $"'{e.Name}'");
        }
        if (spec.Filter is { } filter) foreach (var code in filter.Regions) Known(code, "The filter");
    }

    /// <summary>Checks <paramref name="spec"/> and that <paramref name="view"/> lies inside its period.</summary>
    public static void Validate(PlannerSpec spec, PlannerView view)
    {
        Validate(spec);
        switch (view.Zoom)
        {
            case PlannerZoom.Month:
                var first = new DateOnly(view.Date.Year, view.Date.Month, 1);
                if (first.AddMonths(1).AddDays(-1) < spec.From || first > spec.To) throw new ArgumentException($"The month {first:yyyy-MM} is outside the planner's period.");
                break;
            case PlannerZoom.Day:
                if (view.Date < spec.From || view.Date > spec.To) throw new ArgumentException($"The day {view.Date:yyyy-MM-dd} is outside the planner's period.");
                break;
        }
    }
}
