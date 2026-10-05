# Event Planner (static, 0.43.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a general, data-agnostic event planner to Lumen.Charts that draws a period (usually a year), any month and any day as static, accessible SVG, with weekends, holidays, school holidays and events filtered by region, category, audience, status and relevance.

**Architecture:** A new spec type `PlannerSpec` (records in `Planner.cs`), validated by `PlannerValidation`, laid out by an internal `PlannerCalendar` (dates, regions, filters, long weekends, stacking) and drawn by `PlannerSvg` through the existing internal `SvgWriter` and `ChartSvg.Begin`, the way `GraphSpec` is drawn by `GraphEngine`. A `POST planner/svg` endpoint joins the chart and graph endpoints. No existing chart kind, renderer path or rendering changes.

**Tech Stack:** C# / .NET 8, `Lumen.Charts` (net8.0), `Lumen.Charts.AspNetCore` (minimal API), the repository's executable test harness (`tests/Lumen.Charts.Tests`, `Test`/`Check`/`Reject`), the rendering-hash harness (`tests/Lumen.Charts.Baseline`), `tests/verify-api.ps1`, the Playwright suite (`tests/Lumen.Charts.BrowserTests`).

**Spec:** `docs/superpowers/specs/2026-10-05-event-planner-design.md`

## Global Constraints

- One feature per release: this plan is **0.43.0** only. The interactive `<LumenPlanner>` is 0.44.0 and is out of scope here; the static views must be complete without it.
- No existing rendering moves: `tests/Lumen.Charts.Baseline` must show only added rows against `reference/*.txt` (v0.42.0) in both finishes.
- Every drawn word clears **4.5:1** against what is behind it; every meaningful non-text mark (event stripe, holiday symbol, school-holiday band, long-weekend bracket) clears **3:1**. Checked in tests for `ChartStyle.Light`, `ChartStyle.Dark` and `ChartStyle.Midnight`.
- **Never colour alone**: relevance, status and "mine" are shown by weight, dash, hatch or outline **and** said in words in every event's accessible name and tooltip.
- Rendering is **deterministic**: the same spec and view give the same bytes.
- **Invented data only** for organizers and events in tests, gallery, recipes and docs. Real South African province names and public-holiday dates may be used (public facts). Never open or copy from `lumen-charts-race-face-brief.md`; never stage it.
- Every new public member has an XML doc comment (the build treats missing docs as errors). Release build stays at **0 warnings**.
- Period length ≤ **400** days. Narrow layout threshold for hosts: **640 px** (documented; the static renderer takes `PlannerLayout`).
- Region codes are case-sensitive, ordinal comparison. Week starts **Monday** by default; weekend defaults to **Saturday and Sunday**.
- Version bump to **0.43.0** in `Directory.Build.props`.
- Repository rules (HANDOVER.md): kill only `Lumen.Gallery.exe` by image name; write scripts with a file tool, not Bash heredocs; prefer the Edit tool for CRLF files.

## Review Focus

1. **A holiday set for a country while the filter names a province** — the holiday must still show (a `ZA` holiday under a `ZA-GP` filter). Test in Task 2.
2. **Many events on one day** (10+) — the year view must show three stripes and a "+N" that names the rest; the month view must show what fits and "+N more" naming the rest; nothing may overflow its day cell. Test in Tasks 3 and 4.
3. **A multi-day event crossing a month or week boundary, or starting before `From`** — drawn for the days inside the view, named with its full dates. Test in Tasks 2, 3 and 4.
4. **A period that crosses a year end (September to August)** — month rows in order Sep…Aug with the right years; February of a leap year has 29 days. Test in Tasks 2 and 3.
5. **Long names and empty optional fields** (a 60-character event name, no region, no category, no audience) — cut with "…" in the drawing with the full text kept in the name/tooltip; no "null" or empty commas in names. Test in Tasks 3, 4 and 5.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/Lumen.Charts/Planner.cs` (create) | Public records and enums: `PlannerSpec`, `PlannerRegion`, `PlannerPeriod`, `PlannerEvent`, `PlannerFilter`, `PlannerView`, `PlannerZoom`, `PeriodKind`, `PlannerStatus`, `PlannerRelevance`, `PlannerLayout`. |
| `src/Lumen.Charts/PlannerValidation.cs` (create) | `PlannerValidation.Validate(PlannerSpec)` and `Validate(PlannerSpec, PlannerView)`: every refusal with a reason (`ArgumentException`). |
| `src/Lumen.Charts/PlannerCalendar.cs` (create) | Internal date and region logic: days of the period, weekday-aligned columns, region inclusion, filtering, long weekends, per-day stacks, accessible wording. No SVG. |
| `src/Lumen.Charts/PlannerSvg.cs` (create) | Public `PlannerSvg.Render(spec, view, layout)` and `PlannerSvg.Table(spec, year, month)`; year, month and day views, wide and narrow; legend. Uses `SvgWriter`, `ChartSvg.Begin`, `ChartSvg.Short`, `ChartSvg.Wide`. |
| `src/Lumen.Charts.AspNetCore/ChartEndpoints.cs` (modify) | `POST planner/svg` taking `PlannerRequest(PlannerSpec Spec, PlannerView? View, PlannerLayout Layout)`. |
| `tests/Lumen.Charts.Tests/Program.cs` (modify, append before the final summary lines) | Planner unit tests. |
| `tests/Lumen.Charts.Baseline/Program.cs` (modify) | Planner hash rows (added rows only). |
| `tests/verify-api.ps1` (modify) | Planner HTTP checks. |
| `samples/Lumen.Gallery/PlannerData.cs` (create), `Components/Pages/Home.razor` (modify) | A static planner demo (year and one month) with invented events. |
| `tests/Lumen.Charts.BrowserTests/Program.cs` (modify) | Demo renders, fits a 375 px phone (narrow layout), axe clean. |
| `README.md`, `docs/VERIFICATION.md`, `Directory.Build.props`, `integrations/claude-code/lumen-charts/SKILL.md`, `references/api.md`, `references/http-api.md`, `references/recipes-race-face.md` (modify) | Documentation, recipe, version. |

Build/test commands used throughout (Git Bash, repository root `D:/CHATGPT/.NET GRAPH API`):

- Build: `taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release`
- Unit tests: `dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner|passed"`

---

### Task 1: Planner model and validation

**Files:**
- Create: `src/Lumen.Charts/Planner.cs`
- Create: `src/Lumen.Charts/PlannerValidation.cs`
- Test: `tests/Lumen.Charts.Tests/Program.cs` (append)

**Interfaces:**
- Produces: all public types below; `PlannerValidation.Validate(PlannerSpec spec)` and `PlannerValidation.Validate(PlannerSpec spec, PlannerView view)`, both throwing `ArgumentException` with a human sentence.

- [ ] **Step 1: Write the failing tests** (append to `tests/Lumen.Charts.Tests/Program.cs`, above the last lines that print the summary)

```csharp
// ---- 0.43.0: event planner (static) ----
PlannerSpec Plan(Func<PlannerSpec,PlannerSpec>? change=null)
{
    var spec=PlannerSpec.ForYear(2027) with{
        Title="Season planner",Description="Invented organizers' events",
        Regions=[new("ZA","South Africa"),new("ZA-GP","Gauteng","ZA"),new("ZA-WC","Western Cape","ZA")],
        Periods=[new(new(2027,4,27),null,"Freedom Day",PeriodKind.PublicHoliday,"ZA"),
                 new(new(2027,3,27),new DateOnly(2027,4,5),"School holiday",PeriodKind.SchoolHoliday,"ZA")],
        Events=[new("e1","Hilltop XCO",new(2027,3,13)){Region="ZA-GP",Category="XCO",Audience="Kids",Relevance=PlannerRelevance.Clash},
                new("e2","Coast Stage Race",new(2027,3,12)){End=new DateOnly(2027,3,14),Region="ZA-WC",Category="Stage",Audience="Open",Status=PlannerStatus.Provisional}]};
    return change is null?spec:change(spec);
}
Test("Planner: a valid year passes validation and a year spans 1 January to 31 December",()=>{
    var spec=Plan();PlannerValidation.Validate(spec);
    Check(spec.From==new DateOnly(2027,1,1)&&spec.To==new DateOnly(2027,12,31),$"{spec.From}..{spec.To}");
    Check(spec.WeekStart==DayOfWeek.Monday&&spec.Weekend.SequenceEqual([DayOfWeek.Saturday,DayOfWeek.Sunday]));
});
Test("Planner: refuses a period that ends before it starts, is longer than 400 days, a blank title, and too narrow a width",()=>{
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{To=s.From.AddDays(-1)})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{To=s.From.AddDays(400)})));
    PlannerValidation.Validate(Plan(s=>s with{To=s.From.AddDays(399)}));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Title=" "})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Width=319})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Weekend=[]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Weekend=[DayOfWeek.Saturday,DayOfWeek.Saturday]})));
});
Test("Planner: refuses duplicate, blank or unknown region codes and a parent chain that loops",()=>{
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Regions=[..s.Regions,new("ZA","Again")]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Regions=[..s.Regions,new(" ","Blank")]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Regions=[..s.Regions,new("ZA-KZN","KwaZulu-Natal","ZZ")]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Regions=[new("A","A","B"),new("B","B","A")]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Periods=[new(new(2027,1,1),null,"Day",PeriodKind.Other,"XX")]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Events=[new("x","Ride",new(2027,5,1)){Region="XX"}]})));
});
Test("Planner: refuses periods and events with blank names, ends before starts, duplicate event ids and events wholly outside the period",()=>{
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Periods=[new(new(2027,1,2),new DateOnly(2027,1,1),"Back",PeriodKind.Other)]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Periods=[new(new(2027,1,2),null," ",PeriodKind.Other)]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Events=[new("x","A",new(2027,5,2)){End=new DateOnly(2027,5,1)}]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Events=[new("x","A",new(2027,5,2)),new("x","B",new(2027,5,3))]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Events=[new("x"," ",new(2027,5,2))]})));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Events=[new("x","Old",new(2026,5,2))]})));
    PlannerValidation.Validate(Plan(s=>s with{Events=[new("x","Straddles",new(2026,12,30)){End=new DateOnly(2027,1,2)}]}));
    Reject(()=>PlannerValidation.Validate(Plan(s=>s with{Events=[new("x","Long note",new(2027,5,2)){Note=new string('n',121)}]})));
});
Test("Planner: a view must lie inside the period",()=>{
    var spec=Plan();
    PlannerValidation.Validate(spec,PlannerView.WholePeriod);
    PlannerValidation.Validate(spec,PlannerView.Month(2027,3));
    PlannerValidation.Validate(spec,PlannerView.Day(new(2027,12,31)));
    Reject(()=>PlannerValidation.Validate(spec,PlannerView.Month(2028,1)));
    Reject(()=>PlannerValidation.Validate(spec,PlannerView.Day(new(2026,12,31))));
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "error" | head -3`
Expected: compile errors `The type or namespace name 'PlannerSpec' could not be found`.

- [ ] **Step 3: Write `src/Lumen.Charts/Planner.cs`**

```csharp
namespace Lumen.Charts;

/// <summary>What kind of day or run of days a <see cref="PlannerPeriod"/> is.</summary>
public enum PeriodKind
{
    /// <summary>A public holiday: marked on its day and counted towards long weekends.</summary>
    PublicHoliday,
    /// <summary>A school holiday: a band along the top of its days.</summary>
    SchoolHoliday,
    /// <summary>Anything else worth seeing when choosing a date, such as exam weeks or a large external event.</summary>
    Other
}

/// <summary>How settled an event's date is.</summary>
public enum PlannerStatus
{
    /// <summary>Confirmed: drawn solid.</summary>
    Confirmed,
    /// <summary>Provisional, pencilled in: drawn hatched and said "provisional".</summary>
    Provisional,
    /// <summary>Cancelled: drawn struck through and said "cancelled".</summary>
    Cancelled
}

/// <summary>How strongly an event competes with the viewer's plans, as the host decides it; the planner draws it by weight and
/// dash and says it in words, never by colour alone.</summary>
public enum PlannerRelevance
{
    /// <summary>Unrelated: drawn thin and muted.</summary>
    Other,
    /// <summary>Close: a soft clash, drawn medium and dashed and said "close".</summary>
    Near,
    /// <summary>A clash: drawn bold and said "clash".</summary>
    Clash
}

/// <summary>The level a planner is drawn at.</summary>
public enum PlannerZoom
{
    /// <summary>The whole period, a row per month.</summary>
    Year,
    /// <summary>One month as a grid of weeks.</summary>
    Month,
    /// <summary>One day as a list.</summary>
    Day
}

/// <summary>How a planner is laid out: wide for a desktop, narrow for a phone (hosts switch below 640 pixels).</summary>
public enum PlannerLayout
{
    /// <summary>The year as weekday-aligned month rows; a month as a grid of weeks.</summary>
    Wide,
    /// <summary>The year as compact month bars; a month as a list of the days that hold something.</summary>
    Narrow
}

/// <summary>A place events and holidays belong to. <paramref name="Parent"/> makes a hierarchy: a country, its provinces, their
/// districts. A region includes itself and every region below it.</summary>
/// <param name="Code">A short unique code, such as <c>ZA</c> or <c>ZA-GP</c>, compared ordinally.</param>
/// <param name="Name">The name written and said, such as <c>Gauteng</c>.</param>
/// <param name="Parent">The code of the region it lies in, or null at the top.</param>
public sealed record PlannerRegion(string Code, string Name, string? Parent = null);

/// <summary>A holiday or other day or run of days to see when choosing a date.</summary>
/// <param name="From">Its first day.</param>
/// <param name="To">Its last day; null for one day.</param>
/// <param name="Name">What it is called, such as <c>Freedom Day</c>.</param>
/// <param name="Kind">Public holiday, school holiday or other.</param>
/// <param name="Region">The region it applies to, and every region below it; null for everywhere.</param>
public sealed record PlannerPeriod(DateOnly From, DateOnly? To, string Name, PeriodKind Kind, string? Region = null);

/// <summary>An event on the planner: someone's race, ride or other occasion.</summary>
/// <param name="Id">A unique identifier, returned to the host when the event is selected.</param>
/// <param name="Name">The event's name.</param>
/// <param name="Start">Its first day.</param>
public sealed record PlannerEvent(string Id, string Name, DateOnly Start)
{
    /// <summary>Its last day, for an event over several days; null for one day.</summary>
    public DateOnly? End { get; init; }
    /// <summary>The region it is held in; null shows it under every region.</summary>
    public string? Region { get; init; }
    /// <summary>A free-text category, such as a discipline, filtered on and said in its name.</summary>
    public string? Category { get; init; }
    /// <summary>A free-text audience, such as an age group or level, filtered on and said in its name.</summary>
    public string? Audience { get; init; }
    /// <summary>How settled its date is: confirmed by default.</summary>
    public PlannerStatus Status { get; init; }
    /// <summary>How strongly it competes with the viewer's plans, as the host decides: unrelated by default.</summary>
    public PlannerRelevance Relevance { get; init; }
    /// <summary>The viewer's own event: drawn outlined and said "yours".</summary>
    public bool Mine { get; init; }
    /// <summary>A short note, at most 120 characters, said in its name and shown in the day view.</summary>
    public string? Note { get; init; }
    /// <summary>A link for the host to open; the static drawing does not follow it.</summary>
    public string? Url { get; init; }
}

/// <summary>Which periods and events a planner shows. An empty list means every value. A region includes the regions below it, and
/// a period or event set for a region above the filtered one still shows (a country's holiday under one of its provinces).</summary>
public sealed record PlannerFilter
{
    /// <summary>Region codes to show; empty for all.</summary>
    public IReadOnlyList<string> Regions { get; init; } = [];
    /// <summary>Categories to show, compared ordinally; empty for all.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];
    /// <summary>Audiences to show, compared ordinally; empty for all.</summary>
    public IReadOnlyList<string> Audiences { get; init; } = [];
    /// <summary>Statuses to show; empty for all.</summary>
    public IReadOnlyList<PlannerStatus> Statuses { get; init; } = [];
    /// <summary>Relevances to show; empty for all.</summary>
    public IReadOnlyList<PlannerRelevance> Relevances { get; init; } = [];
}

/// <summary>What a planner draws: the whole period, a month, or a day.</summary>
/// <param name="Zoom">The level.</param>
/// <param name="Date">For a month, any day in it (the first is used); for a day, the day; ignored for the whole period.</param>
public readonly record struct PlannerView(PlannerZoom Zoom, DateOnly Date)
{
    /// <summary>The whole period.</summary>
    public static PlannerView WholePeriod => new(PlannerZoom.Year, default);
    /// <summary>One month.</summary>
    public static PlannerView Month(int year, int month) => new(PlannerZoom.Month, new DateOnly(year, month, 1));
    /// <summary>One day.</summary>
    public static PlannerView Day(DateOnly day) => new(PlannerZoom.Day, day);
}

/// <summary>A planner: a period of days with weekends, holidays and events by region, drawn by <see cref="PlannerSvg.Render"/>.</summary>
public sealed record PlannerSpec
{
    /// <summary>The heading and the drawing's accessible name.</summary>
    public string Title { get; init; } = "Planner";
    /// <summary>A line under the title, also said.</summary>
    public string Description { get; init; } = "";
    /// <summary>The first day shown.</summary>
    public DateOnly From { get; init; }
    /// <summary>The last day shown, at most 399 days after <see cref="From"/>.</summary>
    public DateOnly To { get; init; }
    /// <summary>The day each week starts on: Monday by default.</summary>
    public DayOfWeek WeekStart { get; init; } = DayOfWeek.Monday;
    /// <summary>The weekend days: Saturday and Sunday by default.</summary>
    public IReadOnlyList<DayOfWeek> Weekend { get; init; } = [DayOfWeek.Saturday, DayOfWeek.Sunday];
    /// <summary>The regions periods and events refer to.</summary>
    public IReadOnlyList<PlannerRegion> Regions { get; init; } = [];
    /// <summary>Holidays and other periods.</summary>
    public IReadOnlyList<PlannerPeriod> Periods { get; init; } = [];
    /// <summary>The events.</summary>
    public IReadOnlyList<PlannerEvent> Events { get; init; } = [];
    /// <summary>Which periods and events to show; null for all.</summary>
    public PlannerFilter? Filter { get; init; }
    /// <summary>The preset drawn with when no <see cref="Style"/> is set.</summary>
    public ChartTheme Theme { get; init; }
    /// <summary>A host's colours and typeface; replaces <see cref="Theme"/>.</summary>
    public ChartStyle? Style { get; init; }
    /// <summary>The drawing's width in SVG units, 320 to 4096: 1100 by default.</summary>
    public int Width { get; init; } = 1100;
    /// <summary>Draws the title and description, the default; off, they stay the accessible name only.</summary>
    public bool DrawTitles { get; init; } = true;
    /// <summary>Paints the background, the default; off, the surface behind shows through (set <see cref="ChartStyle.Background"/> to it).</summary>
    public bool PaintBackground { get; init; } = true;

    /// <summary>A planner of one calendar year, 1 January to 31 December.</summary>
    public static PlannerSpec ForYear(int year) => new() { From = new DateOnly(year, 1, 1), To = new DateOnly(year, 12, 31) };
}
```

- [ ] **Step 4: Write `src/Lumen.Charts/PlannerValidation.cs`**

```csharp
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
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2; dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner|passed"`
Expected: 0 warnings, 0 errors; five `PASS Planner: …` lines; summary with 0 failed.

- [ ] **Step 6: Commit**

```bash
git add src/Lumen.Charts/Planner.cs src/Lumen.Charts/PlannerValidation.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Add the planner's data and its validation" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Calendar logic (dates, regions, filters, long weekends, stacks)

**Files:**
- Create: `src/Lumen.Charts/PlannerCalendar.cs`
- Test: `tests/Lumen.Charts.Tests/Program.cs` (append)

**Interfaces:**
- Consumes: Task 1 types.
- Produces (internal, visible to tests through `InternalsVisibleTo` — check `src/Lumen.Charts/Lumen.Charts.csproj` already has `<InternalsVisibleTo Include="Lumen.Charts.Tests" />`; if not, add it in this task):
  - `PlannerCalendar.Months(PlannerSpec) → IReadOnlyList<(int Year, int Month)>` in order.
  - `PlannerCalendar.Lead(int year, int month, DayOfWeek weekStart) → int` — blank columns before day 1 in a weekday-aligned row (0–6).
  - `PlannerCalendar.Columns = 37`.
  - `PlannerCalendar.Includes(PlannerSpec, string ancestor, string code) → bool` — `code` is `ancestor` or below it.
  - `PlannerCalendar.Applies(PlannerSpec, string? itemRegion) → bool` — item shows under the filter's regions.
  - `PlannerCalendar.Events(PlannerSpec) → IReadOnlyList<PlannerEvent>` — filtered, ordered by start, then relevance descending, then name.
  - `PlannerCalendar.Periods(PlannerSpec) → IReadOnlyList<PlannerPeriod>` — filtered by region only.
  - `PlannerCalendar.On(PlannerSpec, DateOnly) → (IReadOnlyList<PlannerPeriod> Periods, IReadOnlyList<PlannerEvent> Events)`.
  - `PlannerCalendar.LongWeekends(PlannerSpec) → IReadOnlyList<(DateOnly From, DateOnly To)>`.
  - `PlannerCalendar.IsWeekend(PlannerSpec, DateOnly) → bool`.
  - `PlannerCalendar.Name(PlannerSpec, PlannerEvent) → string` — the full accessible name.
  - `PlannerCalendar.DayName(PlannerSpec, DateOnly) → string`.
  - `PlannerCalendar.RegionName(PlannerSpec, string?) → string?`.

- [ ] **Step 1: Write the failing tests** (append)

```csharp
Test("Planner calendar: a year has twelve months in order, and a season crossing a year end runs September to August with its years",()=>{
    Check(PlannerCalendar.Months(Plan()).Count==12);
    var season=Plan(s=>s with{From=new(2026,9,1),To=new(2027,8,31),Events=[]});
    var months=PlannerCalendar.Months(season);
    Check(months.Count==12&&months[0]==(2026,9)&&months[3]==(2026,12)&&months[4]==(2027,1)&&months[^1]==(2027,8),string.Join(",",months));
});
Test("Planner calendar: rows align by weekday, so every Saturday stands in one of five columns",()=>{
    // 1 January 2027 is a Friday: four blank columns before it when weeks start on Monday, five when they start on Sunday.
    Check(PlannerCalendar.Lead(2027,1,DayOfWeek.Monday)==4,$"{PlannerCalendar.Lead(2027,1,DayOfWeek.Monday)}");
    Check(PlannerCalendar.Lead(2027,1,DayOfWeek.Sunday)==5);
    for(var m=1;m<=12;m++)for(var d=1;d<=DateTime.DaysInMonth(2027,m);d++){
        var day=new DateOnly(2027,m,d);var column=PlannerCalendar.Lead(2027,m,DayOfWeek.Monday)+d-1;
        Check(column<PlannerCalendar.Columns,$"{day} in column {column}");
        Check((column%7==5)==(day.DayOfWeek==DayOfWeek.Saturday),$"{day} column {column}");
    }
});
Test("Planner calendar: a country's holiday shows under one of its provinces, a province's event under its country, and a filtered-out province's event does not",()=>{
    var spec=Plan(s=>s with{Filter=new(){Regions=["ZA-GP"]}});
    Check(PlannerCalendar.Includes(spec,"ZA","ZA-GP")&&!PlannerCalendar.Includes(spec,"ZA-GP","ZA"));
    Check(PlannerCalendar.Periods(spec).Any(p=>p.Name=="Freedom Day"),"the national holiday is missing under Gauteng");
    var events=PlannerCalendar.Events(spec).Select(e=>e.Id).ToArray();
    Check(events.SequenceEqual(["e1"]),string.Join(",",events));
    var national=Plan(s=>s with{Filter=new(){Regions=["ZA"]}});
    Check(PlannerCalendar.Events(national).Count==2);
});
Test("Planner calendar: filters by category, audience, status and relevance, and orders a day's events clash first",()=>{
    var spec=Plan(s=>s with{Events=[..s.Events,new("e3","Club Ride",new(2027,3,13)){Region="ZA-GP",Category="Road",Relevance=PlannerRelevance.Near}]});
    Check(PlannerCalendar.Events(spec with{Filter=new(){Categories=["XCO"]}}).Single().Id=="e1");
    Check(PlannerCalendar.Events(spec with{Filter=new(){Audiences=["Open"]}}).Single().Id=="e2");
    Check(PlannerCalendar.Events(spec with{Filter=new(){Statuses=[PlannerStatus.Provisional]}}).Single().Id=="e2");
    Check(PlannerCalendar.Events(spec with{Filter=new(){Relevances=[PlannerRelevance.Clash,PlannerRelevance.Near]}}).Select(e=>e.Id).SequenceEqual(["e1","e3"]));
    var day=PlannerCalendar.On(spec,new(2027,3,13)).Events.Select(e=>e.Id).ToArray();
    Check(day.SequenceEqual(["e1","e3","e2"]),string.Join(",",day));
});
Test("Planner calendar: long weekends join a public holiday to its weekend, three days or more, and a multi-day event is on every day it spans",()=>{
    // 27 April 2027 is a Tuesday: no long weekend. Add Monday 26 April as a holiday and Saturday 24 to Tuesday 27 becomes one.
    var plain=PlannerCalendar.LongWeekends(Plan());
    Check(!plain.Any(w=>w.From<=new DateOnly(2027,4,27)&&new DateOnly(2027,4,27)<=w.To),"a lone Tuesday holiday made a long weekend");
    var spec=Plan(s=>s with{Periods=[..s.Periods,new(new(2027,4,26),null,"Invented Monday",PeriodKind.PublicHoliday,"ZA")]});
    Check(PlannerCalendar.LongWeekends(spec).Contains((new DateOnly(2027,4,24),new DateOnly(2027,4,27))),string.Join(";",PlannerCalendar.LongWeekends(spec)));
    // A holiday on a Friday: Friday to Sunday.
    var friday=Plan(s=>s with{Periods=[new(new(2027,7,16),null,"Invented Friday",PeriodKind.PublicHoliday)]});
    Check(PlannerCalendar.LongWeekends(friday).Contains((new DateOnly(2027,7,16),new DateOnly(2027,7,18))));
    Check(PlannerCalendar.On(Plan(),new(2027,3,14)).Events.Any(e=>e.Id=="e2")&&!PlannerCalendar.On(Plan(),new(2027,3,15)).Events.Any(e=>e.Id=="e2"));
});
Test("Planner calendar: names say every field in words, leave out empty ones, and say status, relevance and yours",()=>{
    var spec=Plan();
    var e1=PlannerCalendar.Name(spec,spec.Events[0]);
    Check(e1=="Hilltop XCO, Saturday 13 March 2027, Gauteng, XCO, Kids, clash",e1);
    var e2=PlannerCalendar.Name(spec,spec.Events[1]);
    Check(e2=="Coast Stage Race, Friday 12 to Sunday 14 March 2027, Western Cape, Stage, Open, provisional",e2);
    var bare=PlannerCalendar.Name(spec,new PlannerEvent("b","Bare",new(2027,6,5)){Mine=true,Status=PlannerStatus.Cancelled,Note="Moved to June"});
    Check(bare=="Bare, Saturday 5 June 2027, cancelled, yours, Moved to June",bare);
    Check(!bare.Contains(", ,")&&!bare.Contains("null"));
    Check(PlannerCalendar.DayName(spec,new(2027,4,27))=="Tuesday 27 April 2027, Freedom Day (public holiday), no events",PlannerCalendar.DayName(spec,new(2027,4,27)));
    // 13 March is outside the school holiday (27 March to 5 April) and holds two events: Hilltop XCO and day 2 of the stage race.
    Check(PlannerCalendar.DayName(spec,new(2027,3,13))=="Saturday 13 March 2027, 2 events",PlannerCalendar.DayName(spec,new(2027,3,13)));
    Check(PlannerCalendar.DayName(spec,new(2027,3,29)).StartsWith("Monday 29 March 2027, School holiday (school holiday)"),PlannerCalendar.DayName(spec,new(2027,3,29)));
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E " error" | head -3`
Expected: `The name 'PlannerCalendar' does not exist in the current context`.

- [ ] **Step 3: Write `src/Lumen.Charts/PlannerCalendar.cs`**

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2; dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner|passed"`
Expected: all `Planner` tests PASS, 0 failed. If `PlannerCalendar` is inaccessible, add `<InternalsVisibleTo Include="Lumen.Charts.Tests" />` to `src/Lumen.Charts/Lumen.Charts.csproj` and rebuild.

- [ ] **Step 5: Commit**

```bash
git add src/Lumen.Charts/PlannerCalendar.cs tests/Lumen.Charts.Tests/Program.cs src/Lumen.Charts/Lumen.Charts.csproj
git commit -m "Work out a planner's months, regions, filters and long weekends" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Year view (wide) with legend

**Files:**
- Create: `src/Lumen.Charts/PlannerSvg.cs`
- Test: `tests/Lumen.Charts.Tests/Program.cs` (append)

**Interfaces:**
- Consumes: Task 1 types, Task 2 `PlannerCalendar`, existing internals `SvgWriter` (`Add`, `Text`, `Line`, `Style`, `Painted`, `Titled`, `Head`, `Fixed`, static `N`, `E`), `ChartSvg.Begin(SvgWriter, int width, int height, string title, string description, bool wrap = false)`, `ChartSvg.Short(string, int)`, `ChartSvg.Wide(string)`, `ChartSvg.Preset(ChartTheme)`, `Contrast.Ratio(string, string)`.
- Produces: `public static string PlannerSvg.Render(PlannerSpec spec, PlannerView view, PlannerLayout layout = PlannerLayout.Wide)`; marks: each event is `<g class='lumen-datum' tabindex='0' role='button' data-event='{Id}' aria-label='{Name}'><title>{Name}</title>…</g>`; each day `<g class='lumen-day' data-day='yyyy-MM-dd' aria-label='{DayName}'>` (not focusable in the static drawing; the 0.44.0 component makes days focusable).

Layout constants (wide year): `left = 76` (month names), `right = 24`, `top = 78 + w.Head` (below titles), a header row of weekday initials 16 high, then per month a row `RowH = 56`: school band at `y+2` (height 3), holiday diamond centred at `y+11`, event stripes at `y+18`, `y+25`, `y+32` (lanes 0–2, height per relevance), long-weekend bracket at `y+41`–`y+44`, "+N" text at `y+44` (10 px, centred on its day), busy-week counts at `y+54` (10 px); `colW = (Width - left - right) / 37.0`. **Busy weeks:** for each week a month row shows (columns `7k` to `7k+6`), the clash and close events with a day in that week and month are counted; a `lumen-week` group named `Week of {first day of the week in that month, "d MMMM yyyy"}: {n} clash(es), {m} close` is drawn for every week, and when the count is non-zero its total is written centred on the week's middle column at `y+54` in the muted colour. Legend at the bottom: one line of samples with words (`clash`, `close`, `other`, `provisional`, `cancelled`, `yours`, `public holiday`, `school holiday`, `long weekend`, `weekend`), wrapping when wider than the drawing. Height = `top + 16 + months × 56 + 8 + legend lines × 18 + 16`.

Stripe drawing (by relevance, never colour alone):

| Relevance | Stroke colour | Height | Dash |
|---|---|---|---|
| Clash | `Style.Text` | 5 | solid |
| Near | `Style.Text` | 3 | dashed: drawn as a run of 4-unit segments with 2-unit gaps (separate rects, so it works without `stroke-dasharray` on a fill) |
| Other | `Style.Muted` | 2 | solid |

Status: Provisional draws the stripe as an outline (1-unit stroke, no fill) with diagonal hatch lines inside every 3 units; Cancelled draws the stripe at 40% opacity with a 1-unit line through its middle in `Style.Text`. Mine adds a 1-unit outline 1.5 units outside the stripe in `Style.Series[0]`... **and** the word "yours" in the name. Weekend columns: rect in `Style.Grid` the full row height. Public holiday: a diamond (`path`) 7×7 in `Style.Text`. School holiday: a 3-high rect in `Style.Muted` over its days. Long weekend: a 1.5-unit line in `Style.Muted` under the row (`y+44`) from the first to the last day of the run, with 3-unit end ticks.

- [ ] **Step 1: Write the failing tests** (append)

```csharp
XDocument PlanSvg(PlannerSpec spec,PlannerView? view=null,PlannerLayout layout=PlannerLayout.Wide)=>XDocument.Parse(PlannerSvg.Render(spec,view??PlannerView.WholePeriod,layout));
IEnumerable<XElement> Marks(XDocument doc)=>doc.Descendants().Where(e=>(string?)e.Attribute("class")=="lumen-datum");
Test("Planner year view: valid SVG named by its title and description, one month row each, every event a named focusable mark",()=>{
    var doc=PlanSvg(Plan());
    Check(doc.Root!.Name==ns+"svg"&&doc.Root.Attribute("aria-label")!.Value=="Season planner. Invented organizers' events");
    Check(doc.Descendants(ns+"text").Count(t=>t.Value is "January 2027" or "December 2027")==2);
    var marks=Marks(doc).ToArray();
    Check(marks.Length==2,$"{marks.Length} marks");
    Check(marks.All(m=>m.Attribute("tabindex")!.Value=="0"&&m.Attribute("role")!.Value=="button"));
    Check(marks.Any(m=>m.Attribute("aria-label")!.Value=="Hilltop XCO, Saturday 13 March 2027, Gauteng, XCO, Kids, clash"&&m.Element(ns+"title")!.Value==m.Attribute("aria-label")!.Value));
    Check(doc.Descendants().Any(e=>(string?)e.Attribute("data-day")=="2027-04-27"&&e.Attribute("aria-label")!.Value.Contains("Freedom Day (public holiday)")));
    Check(!doc.ToString().Contains("NaN")&&!doc.ToString().Contains("Infinity"));
});
Test("Planner year view: draws a mark across a multi-day event's days and a Saturday in a weekend band",()=>{
    var doc=PlanSvg(Plan());
    var stage=Marks(doc).Single(m=>m.Attribute("data-event")!.Value=="e2");
    var rects=stage.Descendants(ns+"rect").Select(r=>double.Parse(r.Attribute("width")!.Value,CultureInfo.InvariantCulture)).ToArray();
    Check(rects.Max()>2.5*(1100-76-24)/37.0,"a three-day event is not three days wide");
    Check(doc.Descendants(ns+"rect").Count(r=>(string?)r.Attribute("class")=="lumen-weekend")==104,"52 weekends of two days");
});
Test("Planner year view: ten events on one day draw three stripes and a +7 that names the rest",()=>{
    var many=Enumerable.Range(0,10).Select(i=>new PlannerEvent($"m{i}",$"Invented ride {i}",new(2027,5,8)){Region="ZA-GP"}).ToArray();
    var doc=PlanSvg(Plan(s=>s with{Events=many}));
    Check(Marks(doc).Count()==3,$"{Marks(doc).Count()} stripes");
    var more=doc.Descendants().Single(e=>((string?)e.Attribute("class")??"").Split(' ').Contains("lumen-more"));
    Check(more.Value.Contains("+7")&&more.Attribute("aria-label")!.Value.StartsWith("7 more on Saturday 8 May 2027: Invented ride 3"),more.Attribute("aria-label")!.Value);
});
Test("Planner year view: an event that starts before the period is drawn from its first day and named with its full dates",()=>{
    var doc=PlanSvg(Plan(s=>s with{Events=[new("x","New Year Tour",new(2026,12,30)){End=new DateOnly(2027,1,2)}]}));
    var mark=Marks(doc).Single();
    Check(mark.Attribute("aria-label")!.Value.StartsWith("New Year Tour, Wednesday 30 December 2026 to Saturday 2 January 2027"),mark.Attribute("aria-label")!.Value);
    var x=double.Parse(mark.Descendants(ns+"rect").First().Attribute("x")!.Value,CultureInfo.InvariantCulture);
    var col=(1100-76-24)/37.0;
    Check(Math.Abs(x-(76+4*col+1))<0.01,$"starts at {x}, not at 1 January's column");
});
Test("Planner year view: each week is named with its clashes and close events, and a busy week writes its count",()=>{
    var spec=Plan(s=>s with{Events=[..s.Events,new("e3","Club Ride",new(2027,3,10)){Relevance=PlannerRelevance.Near}]});
    var doc=PlanSvg(spec);
    var week=doc.Descendants().Single(e=>(string?)e.Attribute("class")=="lumen-week"&&e.Attribute("aria-label")!.Value.StartsWith("Week of 8 March 2027"));
    Check(week.Attribute("aria-label")!.Value=="Week of 8 March 2027: 1 clash, 1 close",week.Attribute("aria-label")!.Value);
    Check(week.Descendants(ns+"text").Single().Value=="2");
    var quiet=doc.Descendants().Single(e=>(string?)e.Attribute("class")=="lumen-week"&&e.Attribute("aria-label")!.Value.StartsWith("Week of 15 March 2027"));
    Check(quiet.Attribute("aria-label")!.Value=="Week of 15 March 2027: no clashes"&&!quiet.Descendants(ns+"text").Any());
});
Test("Planner year view: a September-to-August season draws its rows in order with leap-year February",()=>{
    var doc=PlanSvg(Plan(s=>s with{From=new(2027,9,1),To=new(2028,8,31),Events=[],Periods=[]}));
    var months=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-muted lumen-month").Select(t=>t.Value).ToArray();
    Check(months.First()=="September 2027"&&months[4]=="January 2028"&&months.Last()=="August 2028",string.Join(",",months));
    Check(doc.Descendants().Any(e=>(string?)e.Attribute("data-day")=="2028-02-29"));
});
Test("Planner year view: a 60-character name is cut in nothing it draws (the year view writes no event words) and kept whole in its name",()=>{
    var name=new string('L',60);
    var doc=PlanSvg(Plan(s=>s with{Events=[new("x",name,new(2027,6,5))]}));
    Check(Marks(doc).Single().Attribute("aria-label")!.Value.StartsWith(name+", Saturday 5 June 2027"));
});
Test("Planner year view: every drawn word clears 4.5:1 and every stripe and symbol 3:1 in Light, Dark and Midnight, and the render is byte-stable",()=>{
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight}){
        Check(Contrast.Ratio(style.Text,style.Background)>=4.5&&Contrast.Ratio(style.Muted,style.Background)>=4.5,"text colours");
        Check(Contrast.Ratio(style.Muted,style.Background)>=3,"stripes and bands");
        var spec=Plan(s=>s with{Style=style});
        Check(PlannerSvg.Render(spec,PlannerView.WholePeriod)==PlannerSvg.Render(spec,PlannerView.WholePeriod));
    }
});
Test("Planner: the legend says every pattern in words",()=>{
    var words=PlanSvg(Plan()).Descendants(ns+"text").Select(t=>t.Value).ToArray();
    foreach(var word in new[]{"clash","close","other","provisional","cancelled","yours","public holiday","school holiday","long weekend","weekend"})
        Check(words.Contains(word),$"the legend lacks '{word}'");
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E " error" | head -3`
Expected: `The name 'PlannerSvg' does not exist in the current context`.

- [ ] **Step 3: Write `src/Lumen.Charts/PlannerSvg.cs` (year view, legend, render entry; month/day/narrow throw `NotImplementedException` until Tasks 4–6)**

```csharp
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
            w.Text(Left - 8, y + 26, first.ToString("MMMM yyyy", Invariant), "class='lumen-muted lumen-month' font-size='11' text-anchor='end'");
            double X(DateOnly d) => Left + (lead + d.Day - 1) * col;
            for (var d = first; d.Month == month; d = d.AddDays(1))
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
            foreach (var (from, to) in longs.Where(l => l.From.Year == year && l.From.Month == month))
            {
                var x1 = X(from); var x2 = X(to.Month == month ? to : new DateOnly(year, month, DateTime.DaysInMonth(year, month))) + col;
                w.Add($"<path d='M{N(x1 + 1)},{N(y + 41)} v3 H{N(x2 - 1)} v-3' fill='none' stroke='{style.Muted}' stroke-width='1.5'{w.Fixed}/>");
            }
            // Events: up to three lanes per day; a day with more writes "+N" naming the rest.
            var lanes = new Dictionary<DateOnly, int>();
            foreach (var e in events)
            {
                var start = Max(e.Start, Max(first, spec.From));
                var end = Min(e.End ?? e.Start, Min(new DateOnly(year, month, DateTime.DaysInMonth(year, month)), spec.To));
                if (end < start) continue;
                var lane = Enumerable.Range(0, 99).First(l => Days(start, end).All(d => lanes.GetValueOrDefault(d) <= l));
                foreach (var d in Days(start, end)) lanes[d] = lane + 1;
                if (lane >= 3) continue;
                Stripe(w, spec, style, e, X(start), y + 18 + lane * 7, X(end) + col - X(start));
            }
            foreach (var (day, count) in lanes.Where(kv => kv.Value > 3).OrderBy(kv => kv.Key))
            {
                var rest = PlannerCalendar.On(spec, day).Events.Skip(3).ToArray();
                if (rest.Length == 0) continue;
                var label = $"{rest.Length} more on {PlannerCalendar.Day(day)}: {string.Join("; ", rest.Select(e => e.Name))}";
                w.Add($"<text class='lumen-more lumen-muted' x='{N(X(day) + col / 2)}' y='{N(y + 44)}' font-size='10' text-anchor='middle' aria-label='{E(label)}'><title>{E(label)}</title>+{rest.Length}</text>");
            }
            // Busy weeks: each week of the row is named with its clash and close events; a busy one writes its count.
            var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            for (var k = 0; k * 7 < lead + last.Day; k++)
            {
                var weekFirst = Max(first, first.AddDays(k * 7 - lead));
                var weekLast = Min(last, first.AddDays(k * 7 - lead + 6));
                if (weekLast < spec.From || weekFirst > spec.To) continue;
                var inWeek = events.Where(e => e.Start <= weekLast && (e.End ?? e.Start) >= weekFirst).ToArray();
                var clashes = inWeek.Count(e => e.Relevance == PlannerRelevance.Clash);
                var near = inWeek.Count(e => e.Relevance == PlannerRelevance.Near);
                var words = new List<string>();
                if (clashes > 0) words.Add(clashes == 1 ? "1 clash" : $"{clashes} clashes");
                if (near > 0) words.Add($"{near} close");
                var label = $"Week of {weekFirst.ToString("d MMMM yyyy", Invariant)}: {(words.Count == 0 ? "no clashes" : string.Join(", ", words))}";
                w.Add($"<g class='lumen-week' aria-label='{E(label)}'>");
                if (clashes + near > 0) w.Text(Left + (k * 7 + 3.5) * col, y + 54, (clashes + near).ToString(Invariant), "class='lumen-muted' font-size='10' text-anchor='middle'");
                w.Add("</g>");
            }
        }
        Legend(w, spec, style, top + HeaderH + months.Count * RowH + 8);
        return w.ToString() + "</svg>";
    }

    private static int Head(PlannerSpec spec) => !spec.DrawTitles ? -ChartSvg.Untitled : 14 * (ChartSvg.Wrap(spec.Description, spec.Width - 48d).Length - 1);
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static IEnumerable<DateOnly> Days(DateOnly from, DateOnly to) { for (var d = from; d <= to; d = d.AddDays(1)) yield return d; }

    /// <summary>One event's mark: weight and dash by relevance, hatch or strike by status, an outline when it is the viewer's own.</summary>
    private static void Stripe(SvgWriter w, PlannerSpec spec, ChartStyle style, PlannerEvent e, double x, double y, double width)
    {
        var name = PlannerCalendar.Name(spec, e);
        var (ink, height) = e.Relevance switch
        {
            PlannerRelevance.Clash => (style.Text, 5.0),
            PlannerRelevance.Near => (style.Text, 3.0),
            _ => (style.Muted, 2.0)
        };
        var inset = 1.0; x += inset; width = Math.Max(2, width - 2 * inset);
        w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-event='{E(e.Id)}' aria-label='{E(name)}'><title>{E(name)}</title>");
        if (e.Mine) w.Add($"<rect x='{N(x - 1.5)}' y='{N(y - 1.5)}' width='{N(width + 3)}' height='{N(height + 3)}' rx='1.5' fill='none' stroke='{style.Series[0]}' stroke-width='1'{w.Fixed}/>");
        var opacity = e.Status == PlannerStatus.Cancelled ? " fill-opacity='.4'" : "";
        if (e.Status == PlannerStatus.Provisional)
        {
            w.Add($"<rect x='{N(x)}' y='{N(y)}' width='{N(width)}' height='{N(height)}' fill='none' stroke='{ink}' stroke-width='1'{w.Fixed}/>");
            for (var hx = x + 2; hx < x + width; hx += 3) w.Add($"<line x1='{N(hx)}' y1='{N(y + height)}' x2='{N(Math.Min(hx + height, x + width))}' y2='{N(y)}' stroke='{ink}' stroke-width='.8'/>");
        }
        else if (e.Relevance == PlannerRelevance.Near)
        {
            for (var sx = x; sx < x + width; sx += 6) w.Add($"<rect x='{N(sx)}' y='{N(y)}' width='{N(Math.Min(4, x + width - sx))}' height='{N(height)}' fill='{ink}'{opacity}/>");
        }
        else w.Add($"<rect x='{N(x)}' y='{N(y)}' width='{N(width)}' height='{N(height)}' rx='1' fill='{ink}'{opacity}/>");
        if (e.Status == PlannerStatus.Cancelled) w.Add($"<line x1='{N(x)}' y1='{N(y + height / 2)}' x2='{N(x + width)}' y2='{N(y + height / 2)}' stroke='{style.Text}' stroke-width='1'/>");
        w.Add("</g>");
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
                default: StripeSample(w, style, sample, x, y + 3); break;
            }
            w.Text(x + 22, y + 9, word, "font-size='11'");
            x += wide;
        }
        w.Add("</g>");
    }
    private static void StripeSample(SvgWriter w, ChartStyle style, PlannerEvent sample, double x, double y)
    {
        // The legend's sample is drawn as the stripe is, without a mark of its own.
        var spec = new PlannerSpec { From = sample.Start, To = sample.Start };
        var inner = new SvgWriter { Style = style };
        Stripe(inner, spec, style, sample, x, y, 16);
        var drawn = inner.ToString();
        var open = drawn.IndexOf('>') + 1; var close = drawn.LastIndexOf("</g>", StringComparison.Ordinal);
        var body = drawn[open..close];
        body = body[(body.IndexOf("</title>", StringComparison.Ordinal) + "</title>".Length)..];
        w.Add(body);
    }

    private static string YearNarrow(PlannerSpec spec, ChartStyle style) => throw new NotImplementedException("Task 6");
    private static string Month(PlannerSpec spec, ChartStyle style, int year, int month) => throw new NotImplementedException("Task 4");
    private static string Agenda(PlannerSpec spec, ChartStyle style, int year, int month) => throw new NotImplementedException("Task 6");
    private static string DayList(PlannerSpec spec, ChartStyle style, DateOnly day) => throw new NotImplementedException("Task 5");
}
```

Implementation notes the code relies on:
- `SvgWriter` is `internal sealed class` in `ChartSvg.cs`; `PlannerSvg` lives in the same assembly, so it can use it. `ChartSvg.Begin`, `ChartSvg.Untitled`, `ChartSvg.Short` and `ChartSvg.Wide` are `internal`; `ChartSvg.Wrap` is private today — change its modifier to `internal` (one word, no behaviour change) so `Head(spec)` can use it.
- `w.ToString()` returns the drawing without the closing tag only if `Begin` does not close it — check `ChartSvg.Render`'s end: charts append `"</svg>"` themselves; do the same. If `ChartSvg` appends the close inside a shared helper, call that helper instead of the literal.
- The Release build must stay at 0 warnings: remove anything the compiler reports unused.

- [ ] **Step 4: Run tests to verify they pass**

Run: `taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2; dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner|passed"`
Expected: the seven new year-view and legend tests PASS; earlier tests still PASS; 0 failed. Open one render in a browser to look at it: `dotnet run --project tests/Lumen.Charts.Baseline -c Release -- svg-out C:/Users/jacqu/AppData/Local/Temp/planner-look` is added in Task 7; until then, write the SVG from a scratch console or a temporary unit test to a file outside the repo and open it.

- [ ] **Step 5: Commit**

```bash
git add src/Lumen.Charts/PlannerSvg.cs src/Lumen.Charts/ChartSvg.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Draw a planner's year: weekday-aligned months, weekends, holidays and events by relevance" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Month view (wide) and the month as an HTML table

**Files:**
- Modify: `src/Lumen.Charts/PlannerSvg.cs` (replace `Month` stub; add `Table`)
- Test: `tests/Lumen.Charts.Tests/Program.cs` (append)

**Interfaces:**
- Consumes: Task 3 `Stripe` is **not** used here; month cells write words.
- Produces: `public static string PlannerSvg.Table(PlannerSpec spec, int year, int month)` → `<table class='lumen-planner-table'><caption>…</caption><thead><tr><th scope='col'>Monday</th>…</tr></thead><tbody><tr><td>…</td>…</tr></tbody></table>`.

Layout: 7 columns, `colW = (Width - 48) / 7`, top = `78 + Head`, weekday header row 20 high, week rows of height `CellH = 104` (5 or 6 weeks). Each day cell: rect (weekend in `Style.Grid`, else none) with a 1-unit `Style.Grid` border; date number at `(x+6, y+15)` 12 px bold; holiday names (public and other periods) at `y+29` in 10 px muted, cut to fit `colW-12` with `ChartSvg.Short` measured by `Wide`; school holiday as a 3-high muted band at the cell's top; then event lines from `y+44`, 14 apart, while `line y + 14 <= y + CellH - 16`: each line is a `lumen-datum` group with a 4×10 marker rect (weight by relevance as in the year legend: clash 4 wide `Style.Text`, close 4 wide dashed — two 4×4 rects, other 2 wide `Style.Muted`; provisional hollow; cancelled struck) followed by text `Name · Region short · word` (`word` = "clash", "close" or nothing) cut to the cell width; overflow line `+N more` (class `lumen-more`, `aria-label` naming them). Multi-day events: drawn on each day they cover in that cell's list (a stage race on Friday, Saturday and Sunday appears in all three cells, its line ending with `· day 2 of 3`), which keeps the "underneath each other" stacking simple and readable; the spec's "one bar across their days" is met in the year view; record this choice in the docs.

- [ ] **Step 1: Write the failing tests** (append)

```csharp
Test("Planner month view: a grid of weeks from Monday, each day's events stacked underneath each other with words",()=>{
    // 1400 wide gives each day 193 units, room for "Coast Stage Race · day 2 of 3" before the region is cut.
    var spec=Plan(s=>s with{Width=1400,Events=[..s.Events,new("e3","Club Ride",new(2027,3,13)){Region="ZA-GP",Relevance=PlannerRelevance.Near}]});
    var doc=PlanSvg(spec,PlannerView.Month(2027,3));
    var heads=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-muted lumen-weekday").Select(t=>t.Value).ToArray();
    Check(heads.SequenceEqual(["Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday"]),string.Join(",",heads));
    var day=doc.Descendants().Single(e=>(string?)e.Attribute("data-day")=="2027-03-13");
    var lines=day.Descendants().Where(e=>(string?)e.Attribute("class")=="lumen-datum").Select(e=>e.Attribute("aria-label")!.Value).ToArray();
    Check(lines.Length==3&&lines[0].StartsWith("Hilltop XCO")&&lines[1].StartsWith("Club Ride")&&lines[2].StartsWith("Coast Stage Race"),string.Join(" | ",lines));
    var drawn=day.Descendants(ns+"text").Select(t=>t.Value).ToArray();
    Check(drawn.Any(t=>t.Contains("clash"))&&drawn.Any(t=>t.Contains("close"))&&drawn.Any(t=>t.Contains("day 2 of 3")),string.Join(" | ",drawn));
});
Test("Planner month view: twelve events on a day show what fits and +N more naming the rest, inside the cell",()=>{
    var many=Enumerable.Range(0,12).Select(i=>new PlannerEvent($"m{i}",$"Invented ride {i}",new(2027,5,8))).ToArray();
    var doc=PlanSvg(Plan(s=>s with{Events=many}),PlannerView.Month(2027,5));
    var day=doc.Descendants().Single(e=>(string?)e.Attribute("data-day")=="2027-05-08");
    var shown=day.Descendants().Count(e=>(string?)e.Attribute("class")=="lumen-datum");
    var more=day.Descendants().Single(e=>((string?)e.Attribute("class")??"").Contains("lumen-more"));
    Check(shown is >=2 and <12&&more.Value==$"+{12-shown} more",$"{shown} shown, '{more.Value}'");
    var cellTop=double.Parse(day.Elements(ns+"rect").First().Attribute("y")!.Value,CultureInfo.InvariantCulture);
    var lowest=day.Descendants(ns+"text").Max(t=>double.Parse(t.Attribute("y")!.Value,CultureInfo.InvariantCulture));
    Check(lowest<=cellTop+104,$"text at {lowest} runs out of a cell starting at {cellTop}");
});
Test("Planner month view: a 60-character name is cut with … in its line and whole in its name, and holidays are written in their cell",()=>{
    var name=new string('L',60);
    var doc=PlanSvg(Plan(s=>s with{Events=[new("x",name,new(2027,4,27))]}),PlannerView.Month(2027,4));
    var day=doc.Descendants().Single(e=>(string?)e.Attribute("data-day")=="2027-04-27");
    Check(day.Descendants(ns+"text").Any(t=>t.Value=="Freedom Day"));
    var mark=day.Descendants().Single(e=>(string?)e.Attribute("class")=="lumen-datum");
    Check(mark.Attribute("aria-label")!.Value.StartsWith(name)&&mark.Descendants(ns+"text").Single().Value.EndsWith("…"));
});
Test("Planner month view: an event running from one month into the next is listed on its days in each, counted day by day",()=>{
    var spec=Plan(s=>s with{Width=1400,Events=[new("x","Tour",new(2027,3,30)){End=new DateOnly(2027,4,2)}]});
    var april=PlanSvg(spec,PlannerView.Month(2027,4));
    var first=april.Descendants().Single(e=>(string?)e.Attribute("data-day")=="2027-04-01");
    Check(first.Descendants(ns+"text").Any(t=>t.Value.Contains("day 3 of 4")),string.Join(" | ",first.Descendants(ns+"text").Select(t=>t.Value)));
    Check(april.Descendants().Where(e=>(string?)e.Attribute("data-event")=="x").Count()==2,"listed on 1 and 2 April");
    Check(PlanSvg(spec,PlannerView.Month(2027,3)).Descendants().Count(e=>(string?)e.Attribute("data-event")=="x")==2,"listed on 30 and 31 March");
});
Test("Planner month table: rows are weeks, columns weekdays, each cell its day's holidays and events in words",()=>{
    var html=PlannerSvg.Table(Plan(),2027,3);
    var doc=XDocument.Parse(html);
    Check(doc.Root!.Name.LocalName=="table"&&doc.Descendants("caption").Single().Value=="Season planner, March 2027");
    Check(doc.Descendants("th").Count(th=>(string?)th.Attribute("scope")=="col")==7);
    var cell=doc.Descendants("td").Single(td=>td.Value.StartsWith("13 "));
    Check(cell.Value.Contains("Hilltop XCO, Saturday 13 March 2027, Gauteng, XCO, Kids, clash"),cell.Value);
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner month"`
Expected: FAIL with `Task 4` (NotImplementedException) and a compile error for `Table` until Step 3.

- [ ] **Step 3: Implement the month view and the table** (replace the `Month` stub in `PlannerSvg.cs`; add `Table`)

```csharp
    private const double CellH = 104, LineH = 14;

    private static string Month(PlannerSpec spec, ChartStyle style, int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var lead = PlannerCalendar.Lead(year, month, spec.WeekStart);
        var days = DateTime.DaysInMonth(year, month);
        var weeks = (lead + days + 6) / 7;
        var height = (int)Math.Ceiling(78 + Head(spec) + 20 + weeks * CellH + LegendLines(spec.Width) * 18 + 24);
        var w = Writer(spec, style);
        ChartSvg.Begin(w, spec.Width, height, spec.Title, $"{first.ToString("MMMM yyyy", Invariant)}{(spec.Description.Length > 0 ? " · " + spec.Description : "")}");
        var top = 78 + w.Head;
        var col = (spec.Width - 48) / 7.0;
        for (var c = 0; c < 7; c++)
            w.Text(24 + c * col + 6, top + 13, ((DayOfWeek)(((int)spec.WeekStart + c) % 7)).ToString(), "class='lumen-muted lumen-weekday' font-size='11'");
        var periods = PlannerCalendar.Periods(spec);
        for (var d = first; d.Month == month; d = d.AddDays(1))
        {
            var cell = lead + d.Day - 1;
            var x = 24 + cell % 7 * col; var y = top + 20 + cell / 7 * CellH;
            var inPeriod = d >= spec.From && d <= spec.To;
            w.Add($"<g class='lumen-day' data-day='{d:yyyy-MM-dd}' aria-label='{E(PlannerCalendar.DayName(spec, d))}'>");
            var fill = PlannerCalendar.IsWeekend(spec, d) ? style.Grid : "none";
            w.Add($"<rect x='{N(x)}' y='{N(y)}' width='{N(col)}' height='{N(CellH)}' fill='{fill}' stroke='{style.Grid}' stroke-width='1'{w.Fixed}/>");
            w.Text(x + 6, y + 15, d.Day.ToString(Invariant), "font-size='12' font-weight='600'");
            if (!inPeriod) { w.Add("</g>"); continue; }
            var (onDay, events) = PlannerCalendar.On(spec, d);
            if (onDay.Any(p => p.Kind == PeriodKind.SchoolHoliday)) w.Add($"<rect x='{N(x)}' y='{N(y)}' width='{N(col)}' height='3' fill='{style.Muted}'/>");
            var named = onDay.Where(p => p.Kind != PeriodKind.SchoolHoliday).Select(p => p.Name).ToArray();
            if (named.Length > 0) w.Text(x + 6, y + 29, Fit(string.Join(" · ", named), col - 12, 10), "class='lumen-muted' font-size='10'");
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
                var name = PlannerCalendar.Name(spec, e);
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
                w.Add($"<text class='lumen-more lumen-muted' x='{N(x + 6)}' y='{N(y + 44 + shown * LineH)}' font-size='10' aria-label='{E(label)}'><title>{E(label)}</title>+{rest.Length} more</text>");
            }
            w.Add("</g>");
        }
        Legend(w, spec, style, top + 20 + weeks * CellH + 12);
        return w.ToString() + "</svg>";
    }

    /// <summary>The marker before an event's line in a month or day list: its relevance by width and dash, its status by hatch or strike.</summary>
    private static void Marker(SvgWriter w, ChartStyle style, PlannerEvent e, double x, double y)
    {
        var ink = e.Relevance == PlannerRelevance.Other ? style.Muted : style.Text;
        var wide = e.Relevance == PlannerRelevance.Other ? 2.0 : 4.0;
        if (e.Mine) w.Add($"<rect x='{N(x - 1.5)}' y='{N(y - 1.5)}' width='{N(wide + 3)}' height='13' fill='none' stroke='{style.Series[0]}' stroke-width='1'{w.Fixed}/>");
        if (e.Status == PlannerStatus.Provisional) w.Add($"<rect x='{N(x)}' y='{N(y)}' width='{N(wide)}' height='10' fill='none' stroke='{ink}' stroke-width='1'{w.Fixed}/>");
        else if (e.Relevance == PlannerRelevance.Near) w.Add($"<rect x='{N(x)}' y='{N(y)}' width='{N(wide)}' height='4' fill='{ink}'/><rect x='{N(x)}' y='{N(y + 6)}' width='{N(wide)}' height='4' fill='{ink}'/>");
        else w.Add($"<rect x='{N(x)}' y='{N(y)}' width='{N(wide)}' height='10' fill='{ink}'{(e.Status == PlannerStatus.Cancelled ? " fill-opacity='.4'" : "")}/>");
    }

    /// <summary><paramref name="text"/> cut with "…" to fit <paramref name="room"/> units at <paramref name="size"/> pixels.</summary>
    private static string Fit(string text, double room, double size)
    {
        double Width(string t) => ChartSvg.Wide(t) * size / 11;
        if (Width(text) <= room) return text;
        var cut = text;
        while (cut.Length > 1 && Width(cut + "…") > room) cut = cut[..^1];
        return cut.TrimEnd() + "…";
    }

    /// <summary>The month <paramref name="year"/>-<paramref name="month"/> as an HTML table for static pages: rows are weeks, columns
    /// weekdays, each cell its day's holidays and events in words.</summary>
    public static string Table(PlannerSpec spec, int year, int month)
    {
        PlannerValidation.Validate(spec, PlannerView.Month(year, month));
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
                var (periods, events) = PlannerCalendar.On(spec, d);
                var words = new List<string> { day.ToString(Invariant) };
                words.AddRange(periods.Select(p => $"{p.Name} ({PlannerCalendar.KindWords(p.Kind)})"));
                words.AddRange(events.Select(e => PlannerCalendar.Name(spec, e)));
                b.Append($"<td>{E(string.Join(" · ", words))}</td>");
            }
            if (cell % 7 == 6) b.Append("</tr>");
        }
        b.Append("</tbody></table>");
        return b.ToString();
    }
```

Note: the month test expects the cell text to start with `"13 "`; `string.Join(" · ", …)` writes `"13 · Hilltop…"`, which starts with `"13 "`. Keep it.

- [ ] **Step 4: Run tests to verify they pass**

Run: `taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2; dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner|passed"`
Expected: four new month/table tests PASS; all earlier PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Lumen.Charts/PlannerSvg.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Draw a planner's month with each day's events stacked in words, and the month as a table" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Day view

**Files:**
- Modify: `src/Lumen.Charts/PlannerSvg.cs` (replace `DayList` stub)
- Test: `tests/Lumen.Charts.Tests/Program.cs` (append)

**Interfaces:**
- Produces: the day view: title, the day's date as a 15 px heading, its periods as muted lines, then one block per event: marker, name 13 px bold, then a muted 11 px line `Region full name · Category · Audience · status word · relevance word · yours`, then the note (11 px) when present; blocks 46 high (58 with a note). Height fits content. Empty day: "No events" muted.

- [ ] **Step 1: Write the failing tests** (append)

```csharp
Test("Planner day view: the day's holidays, then every event with its region's full name, category, audience, status and relevance in words",()=>{
    var spec=Plan(s=>s with{Events=[..s.Events,new("e4","Freedom Ride",new(2027,4,27)){Region="ZA-GP",Note="Invented charity ride",Mine=true}]});
    var doc=PlanSvg(spec,PlannerView.Day(new(2027,4,27)));
    var words=doc.Descendants(ns+"text").Select(t=>t.Value).ToArray();
    Check(words.Contains("Tuesday 27 April 2027")&&words.Any(w=>w=="Freedom Day (public holiday)"),string.Join(" | ",words));
    Check(words.Contains("Freedom Ride")&&words.Any(w=>w.StartsWith("Gauteng")&&w.Contains("yours"))&&words.Contains("Invented charity ride"),string.Join(" | ",words));
    Check(Marks(doc).Single().Attribute("aria-label")!.Value.StartsWith("Freedom Ride, Tuesday 27 April 2027, Gauteng"));
});
Test("Planner day view: a day with nothing says so, and empty optional fields leave no stray separators",()=>{
    var doc=PlanSvg(Plan(s=>s with{Events=[new("b","Bare",new(2027,6,5))]}),PlannerView.Day(new(2027,6,6)));
    Check(doc.Descendants(ns+"text").Any(t=>t.Value=="No events"));
    var bare=PlanSvg(Plan(s=>s with{Events=[new("b","Bare",new(2027,6,5))]}),PlannerView.Day(new(2027,6,5)));
    Check(!bare.Descendants(ns+"text").Any(t=>t.Value.Contains(" ·  ")||t.Value.StartsWith(" · ")||t.Value.EndsWith(" · ")));
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner day"`
Expected: FAIL with `Task 5`.

- [ ] **Step 3: Implement** (replace the `DayList` stub)

```csharp
    private static string DayList(PlannerSpec spec, ChartStyle style, DateOnly day)
    {
        var (periods, events) = PlannerCalendar.On(spec, day);
        double Block(PlannerEvent e) => string.IsNullOrWhiteSpace(e.Note) ? 46 : 58;
        var body = 30 + periods.Count * 16 + (events.Count == 0 ? 20 : events.Sum(Block));
        var height = (int)Math.Ceiling(78 + Head(spec) + body + 16);
        var w = Writer(spec, style);
        ChartSvg.Begin(w, spec.Width, height, spec.Title, spec.Description);
        var y = 78 + w.Head + 14;
        w.Add($"<g class='lumen-day' data-day='{day:yyyy-MM-dd}' aria-label='{E(PlannerCalendar.DayName(spec, day))}'>");
        w.Text(24, y, PlannerCalendar.Day(day), "font-size='15' font-weight='600'");
        y += 20;
        foreach (var p in periods) { w.Text(24, y, $"{p.Name} ({PlannerCalendar.KindWords(p.Kind)})", "class='lumen-muted' font-size='11'"); y += 16; }
        if (events.Count == 0) w.Text(24, y + 4, "No events", "class='lumen-muted' font-size='11'");
        foreach (var e in events)
        {
            var name = PlannerCalendar.Name(spec, e);
            w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-event='{E(e.Id)}' aria-label='{E(name)}'><title>{E(name)}</title>");
            Marker(w, style, e, 24, y + 4);
            w.Text(34, y + 14, Fit(e.Name, spec.Width - 58, 13), "font-size='13' font-weight='600'");
            var facts = new[]
            {
                PlannerCalendar.RegionName(spec, e.Region), e.Category, e.Audience,
                e.Status switch { PlannerStatus.Provisional => "provisional", PlannerStatus.Cancelled => "cancelled", _ => null },
                e.Relevance switch { PlannerRelevance.Clash => "clash", PlannerRelevance.Near => "close", _ => null },
                e.Mine ? "yours" : null,
                (e.End is { } end && end != e.Start) ? PlannerCalendar.Span(e.Start, e.End) : null
            }.Where(f => !string.IsNullOrWhiteSpace(f));
            var line = string.Join(" · ", facts);
            if (line.Length > 0) w.Text(34, y + 30, Fit(line, spec.Width - 58, 11), "class='lumen-muted' font-size='11'");
            if (!string.IsNullOrWhiteSpace(e.Note)) w.Text(34, y + 44, Fit(e.Note!, spec.Width - 58, 11), "font-size='11'");
            w.Add("</g>");
            y += Block(e);
        }
        w.Add("</g>");
        return w.ToString() + "</svg>";
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2; dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner|passed"`
Expected: two new day-view tests PASS; all earlier PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Lumen.Charts/PlannerSvg.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Draw a planner's day as a list of its holidays and events" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Narrow layouts (phone)

**Files:**
- Modify: `src/Lumen.Charts/PlannerSvg.cs` (replace `YearNarrow` and `Agenda` stubs)
- Test: `tests/Lumen.Charts.Tests/Program.cs` (append)

**Interfaces:**
- Produces: `Render(spec, view, PlannerLayout.Narrow)` for Year and Month (Day is the same list in both layouts).
  - Narrow year: one block per month, 34 high: month name (12 px) on the left, then a bar of the month's weekends only (each weekend a 10-wide slot, `Style.Grid`; a weekend with a clash gets a 4-high `Style.Text` mark under it, one with only close events a 2-high dashed mark, a public holiday a diamond), and on the right the month's counts in words: `2 clashes · 1 close`. Each weekend slot is a `lumen-week` group named `Weekend of 13 March 2027: 1 clash, 1 close, School holiday`.
  - Narrow month (agenda): only the days with events or periods, each a header line (`Saturday 13 March` 12 px bold, holidays muted after it) followed by its events one per line (marker + name + region + word), 18 per line, wrapping nothing (cut with `Fit`). Empty month: "Nothing scheduled" muted.
  - Widths down to 320 must keep every word inside the drawing.

- [ ] **Step 1: Write the failing tests** (append)

```csharp
Test("Planner narrow year: twelve month bars, each weekend named with its clashes, and every word inside a 340-wide drawing",()=>{
    var spec=Plan(s=>s with{Width=340});
    var doc=PlanSvg(spec,PlannerView.WholePeriod,PlannerLayout.Narrow);
    Check(doc.Descendants(ns+"text").Count(t=>(string?)t.Attribute("class")=="lumen-month")==12);
    var weekend=doc.Descendants().Single(e=>(string?)e.Attribute("data-weekend")=="2027-03-13");
    Check(weekend.Attribute("aria-label")!.Value=="Weekend of 13 March 2027: 1 clash",weekend.Attribute("aria-label")!.Value);
    foreach(var t in doc.Descendants(ns+"text")){
        var x=double.Parse(t.Attribute("x")!.Value,CultureInfo.InvariantCulture);
        var anchor=(string?)t.Attribute("text-anchor");
        var size=double.Parse((string?)t.Attribute("font-size")??"12",CultureInfo.InvariantCulture);
        var wide=ChartSvg.Wide(t.Value)*size/11;
        var (l,r)=anchor=="end"?(x-wide,x):anchor=="middle"?(x-wide/2,x+wide/2):(x,x+wide);
        Check(l>=0&&r<=340,$"'{t.Value}' runs from {l:0} to {r:0}");
    }
});
Test("Planner narrow month: an agenda of only the days that hold something, events listed under their day",()=>{
    var doc=PlanSvg(Plan(s=>s with{Width=340}),PlannerView.Month(2027,3),PlannerLayout.Narrow);
    var heads=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-agenda-day").Select(t=>t.Value).ToArray();
    Check(heads.SequenceEqual(["Friday 12 March","Saturday 13 March","Sunday 14 March","Saturday 27 March","Sunday 28 March","Monday 29 March","Tuesday 30 March","Wednesday 31 March"]),string.Join(",",heads));
    Check(Marks(doc).Count(m=>m.Attribute("data-event")!.Value=="e2")==3,"the stage race is not listed on each of its days");
    var empty=PlanSvg(Plan(s=>s with{Width=340,Events=[],Periods=[]}),PlannerView.Month(2027,6),PlannerLayout.Narrow);
    Check(empty.Descendants(ns+"text").Any(t=>t.Value=="Nothing scheduled"));
});
```

Note: the expected agenda heads include 27–31 March because the `Plan()` school holiday runs 27 March to 5 April; days covered by a period are listed even without events.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner narrow"`
Expected: FAIL with `Task 6`. (`ChartSvg.Wide` must be reachable from the tests: it is `internal`, and the tests already see internals.)

- [ ] **Step 3: Implement** (replace both stubs)

```csharp
    private static string YearNarrow(PlannerSpec spec, ChartStyle style)
    {
        var months = PlannerCalendar.Months(spec);
        var height = (int)Math.Ceiling(78 + Head(spec) + months.Count * 34 + LegendLines(spec.Width) * 18 + 24);
        var w = Writer(spec, style);
        ChartSvg.Begin(w, spec.Width, height, spec.Title, spec.Description);
        var top = 78 + w.Head;
        var holidays = PlannerCalendar.Periods(spec);
        for (var r = 0; r < months.Count; r++)
        {
            var (year, month) = months[r];
            var y = top + r * 34;
            var first = new DateOnly(year, month, 1);
            w.Text(24, y + 14, first.ToString("MMM yyyy", Invariant), "class='lumen-month' font-size='12'");
            var weekends = new List<DateOnly>();
            for (var d = first; d.Month == month; d = d.AddDays(1))
                if (d >= spec.From && d <= spec.To && PlannerCalendar.IsWeekend(spec, d) && (weekends.Count == 0 || d.DayNumber - weekends[^1].DayNumber > 1)) weekends.Add(d);
            double x = 96;
            int clashes = 0, close = 0;
            foreach (var start in weekends)
            {
                var days = Enumerable.Range(0, 7).Select(start.AddDays).TakeWhile(d => d.Month == month && PlannerCalendar.IsWeekend(spec, d)).ToArray();
                var events = PlannerCalendar.Events(spec).Where(e => days.Any(d => e.Start <= d && d <= (e.End ?? e.Start))).ToArray();
                var c = events.Count(e => e.Relevance == PlannerRelevance.Clash); var n = events.Count(e => e.Relevance == PlannerRelevance.Near);
                clashes += c; close += n;
                var periods = holidays.Where(p => days.Any(d => p.From <= d && d <= (p.To ?? p.From))).Select(p => p.Name).Distinct();
                var words = new List<string>();
                if (c > 0) words.Add(c == 1 ? "1 clash" : $"{c} clashes");
                if (n > 0) words.Add($"{n} close");
                words.AddRange(periods);
                var label = $"Weekend of {start.ToString("d MMMM yyyy", Invariant)}: {(words.Count == 0 ? "nothing" : string.Join(", ", words))}";
                w.Add($"<g class='lumen-week' data-weekend='{start:yyyy-MM-dd}' aria-label='{E(label)}'><title>{E(label)}</title>");
                w.Add($"<rect x='{N(x)}' y='{N(y + 4)}' width='10' height='12' fill='{style.Grid}'/>");
                if (holidays.Any(p => p.Kind == PeriodKind.PublicHoliday && days.Any(d => p.From <= d && d <= (p.To ?? p.From))))
                    w.Add($"<path d='M{N(x + 5)},{N(y + 5)} l3.5,3.5 l-3.5,3.5 l-3.5,-3.5 Z' fill='{style.Text}'/>");
                if (c > 0) w.Add($"<rect x='{N(x)}' y='{N(y + 19)}' width='10' height='4' fill='{style.Text}'/>");
                else if (n > 0) w.Add($"<rect x='{N(x)}' y='{N(y + 19)}' width='4' height='2' fill='{style.Text}'/><rect x='{N(x + 6)}' y='{N(y + 19)}' width='4' height='2' fill='{style.Text}'/>");
                w.Add("</g>");
                x += 14;
            }
            var summary = clashes + close == 0 ? "" : string.Join(" · ", new[] { clashes > 0 ? (clashes == 1 ? "1 clash" : $"{clashes} clashes") : null, close > 0 ? $"{close} close" : null }.Where(s => s is not null));
            if (summary.Length > 0) w.Text(spec.Width - 24, y + 14, summary, "class='lumen-muted' font-size='11' text-anchor='end'");
        }
        Legend(w, spec, style, top + months.Count * 34 + 8);
        return w.ToString() + "</svg>";
    }

    private static string Agenda(PlannerSpec spec, ChartStyle style, int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var days = new List<(DateOnly Day, IReadOnlyList<PlannerPeriod> Periods, IReadOnlyList<PlannerEvent> Events)>();
        for (var d = first; d.Month == month; d = d.AddDays(1))
        {
            if (d < spec.From || d > spec.To) continue;
            var (periods, events) = PlannerCalendar.On(spec, d);
            if (periods.Count > 0 || events.Count > 0) days.Add((d, periods, events));
        }
        var body = days.Count == 0 ? 24 : days.Sum(x => 22 + x.Events.Count * 18 + 6);
        var height = (int)Math.Ceiling(78 + Head(spec) + body + 16);
        var w = Writer(spec, style);
        ChartSvg.Begin(w, spec.Width, height, spec.Title, $"{first.ToString("MMMM yyyy", Invariant)}{(spec.Description.Length > 0 ? " · " + spec.Description : "")}", wrap: true);
        var y = 78 + w.Head + 12;
        if (days.Count == 0) w.Text(24, y, "Nothing scheduled", "class='lumen-muted' font-size='11'");
        foreach (var (day, periods, events) in days)
        {
            w.Add($"<g class='lumen-day' data-day='{day:yyyy-MM-dd}' aria-label='{E(PlannerCalendar.DayName(spec, day))}'>");
            w.Text(24, y, day.ToString("dddd d MMMM", Invariant), "class='lumen-agenda-day' font-size='12' font-weight='600'");
            if (periods.Count > 0)
            {
                var headWide = ChartSvg.Wide(day.ToString("dddd d MMMM", Invariant)) * 12 / 11 + 8;
                w.Text(24 + headWide, y, Fit(string.Join(" · ", periods.Select(p => p.Name)), spec.Width - 48 - headWide, 10), "class='lumen-muted' font-size='10'");
            }
            y += 18;
            foreach (var e in events)
            {
                var name = PlannerCalendar.Name(spec, e);
                var word = e.Relevance switch { PlannerRelevance.Clash => " · clash", PlannerRelevance.Near => " · close", _ => "" };
                w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-event='{E(e.Id)}' aria-label='{E(name)}'><title>{E(name)}</title>");
                Marker(w, style, e, 26, y - 9);
                w.Text(36, y, Fit(e.Name + (e.Region is null ? "" : " · " + e.Region) + word, spec.Width - 60, 11), "font-size='11'");
                w.Add("</g>");
                y += 18;
            }
            w.Add("</g>");
            y += 10;
        }
        return w.ToString() + "</svg>";
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2; dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner|passed"`
Expected: two narrow tests PASS. If the 340-wide check fails on the legend, reduce `LegendItemWidth` padding for narrow drawings or let `Legend` wrap earlier; the test is the authority.

- [ ] **Step 5: Commit**

```bash
git add src/Lumen.Charts/PlannerSvg.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Lay a planner out for a phone: month bars of weekends and a month agenda" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: HTTP endpoint and rendering baseline

**Files:**
- Modify: `src/Lumen.Charts.AspNetCore/ChartEndpoints.cs`
- Modify: `tests/verify-api.ps1`
- Modify: `tests/Lumen.Charts.Baseline/Program.cs`
- Test: `tests/Lumen.Charts.Tests/Program.cs` (append one JSON round-trip test)

**Interfaces:**
- Produces: `public sealed record PlannerRequest(PlannerSpec Spec, PlannerView? View = null, PlannerLayout Layout = PlannerLayout.Wide)` in `Lumen.Charts` (in `Planner.cs`), and `POST {prefix}/planner/svg`.

- [ ] **Step 1: Write the failing tests**

Unit (append):

```csharp
Test("Planner: a spec round-trips through the HTTP API's JSON and draws the same SVG",()=>{
    var json=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    json.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    var request=new PlannerRequest(Plan(),PlannerView.Month(2027,3));
    var back=System.Text.Json.JsonSerializer.Deserialize<PlannerRequest>(System.Text.Json.JsonSerializer.Serialize(request,json),json)!;
    Check(PlannerSvg.Render(back.Spec,back.View!.Value,back.Layout)==PlannerSvg.Render(request.Spec,request.View!.Value));
    var text=System.Text.Json.JsonSerializer.Serialize(request,json);
    Check(text.Contains("\"zoom\":\"Month\"")&&text.Contains("\"relevance\":\"Clash\""),text[..200]);
});
```

HTTP (`tests/verify-api.ps1`): find the block of graph checks (search for `graph/svg`) and add, in the same style as its neighbours:

```powershell
$planner = @{
  spec = @{
    title = 'Season planner'; from = '2027-01-01'; to = '2027-12-31'
    regions = @(@{ code = 'ZA'; name = 'South Africa' }, @{ code = 'ZA-GP'; name = 'Gauteng'; parent = 'ZA' })
    periods = @(@{ from = '2027-04-27'; name = 'Freedom Day'; kind = 'PublicHoliday'; region = 'ZA' })
    events = @(@{ id = 'e1'; name = 'Hilltop XCO'; start = '2027-03-13'; region = 'ZA-GP'; relevance = 'Clash' })
  }
  view = @{ zoom = 'Month'; date = '2027-03-01' }
}
$svg = Post "$BaseUrl/api/charts/planner/svg" $planner
Check ($svg -match "aria-label='Hilltop XCO, Saturday 13 March 2027, Gauteng, clash'") 'Planner month draws the event named in words'
$bad = PostStatus "$BaseUrl/api/charts/planner/svg" (@{ spec = @{ title = 'Bad'; from = '2027-01-02'; to = '2027-01-01' } })
Check ($bad -eq 400) 'Planner refuses a period that ends before it starts'
$narrow = Post "$BaseUrl/api/charts/planner/svg" (@{ spec = $planner.spec; layout = 'Narrow' })
Check ($narrow -match "class='lumen-month'") 'Planner draws the narrow year'
```

Use the script's existing helper names: open `tests/verify-api.ps1`, find how existing checks post JSON and read status codes (the helpers may be named differently from `Post`/`PostStatus`/`Check`), and use those names exactly.

Baseline (`tests/Lumen.Charts.Baseline/Program.cs`): after the last block of named rows (search for the `0.42.0` block, e.g. `gap/two-close`), add a `0.43.0` block. The harness hashes strings through `Hash(...)` and adds `"{name} {hash}"` to `lines`; follow the existing pattern exactly. Planner styles must go through the harness's `Finished(style, theme)` so the classic run draws them in the classic finish:

```csharp
// 0.43.0: the event planner.
PlannerSpec Planned(ChartTheme theme, ChartStyle? style = null) => PlannerSpec.ForYear(2027) with
{
    Title = "Season planner", Description = "Invented organizers", Theme = theme, Style = Finished(style, theme),
    Regions = [new("ZA", "South Africa"), new("ZA-GP", "Gauteng", "ZA"), new("ZA-WC", "Western Cape", "ZA")],
    Periods = [new(new(2027, 4, 27), null, "Freedom Day", PeriodKind.PublicHoliday, "ZA"), new(new(2027, 6, 26), new DateOnly(2027, 7, 18), "School holiday", PeriodKind.SchoolHoliday, "ZA")],
    Events = Enumerable.Range(0, 30).Select(i => new PlannerEvent($"e{i}", $"Invented event {i}", new DateOnly(2027, 1, 2).AddDays(i * 11))
    {
        End = i % 7 == 0 ? new DateOnly(2027, 1, 2).AddDays(i * 11 + 2) : null, Region = i % 2 == 0 ? "ZA-GP" : "ZA-WC", Category = i % 3 == 0 ? "XCO" : "Road",
        Status = (PlannerStatus)(i % 3), Relevance = (PlannerRelevance)(i % 3), Mine = i == 5
    }).ToArray()
};
foreach (var (name, theme, style) in new[] { ("light", ChartTheme.Light, (ChartStyle?)null), ("dark", ChartTheme.Dark, null), ("midnight", ChartTheme.Light, ChartStyle.Midnight) })
{
    lines.Add($"planner/year-{name} {Hash(PlannerSvg.Render(Planned(theme, style), PlannerView.WholePeriod))}");
    lines.Add($"planner/month-{name} {Hash(PlannerSvg.Render(Planned(theme, style), PlannerView.Month(2027, 3)))}");
    lines.Add($"planner/day-{name} {Hash(PlannerSvg.Render(Planned(theme, style), PlannerView.Day(new DateOnly(2027, 1, 13))))}");
}
lines.Add($"planner/year-narrow-340 {Hash(PlannerSvg.Render(Planned(ChartTheme.Light) with { Width = 340 }, PlannerView.WholePeriod, PlannerLayout.Narrow))}");
lines.Add($"planner/month-narrow-340 {Hash(PlannerSvg.Render(Planned(ChartTheme.Light) with { Width = 340 }, PlannerView.Month(2027, 3), PlannerLayout.Narrow))}");
lines.Add($"planner/gauteng {Hash(PlannerSvg.Render(Planned(ChartTheme.Light) with { Filter = new() { Regions = ["ZA-GP"] } }, PlannerView.WholePeriod))}");
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E " error" | head -3`
Expected: `PlannerRequest` not found.

- [ ] **Step 3: Implement**

In `Planner.cs` add:

```csharp
/// <summary>The body of <c>POST planner/svg</c>: a planner, the view to draw (the whole period when null) and the layout.</summary>
/// <param name="Spec">The planner.</param>
/// <param name="View">What to draw; null for the whole period.</param>
/// <param name="Layout">Wide or narrow.</param>
public sealed record PlannerRequest(PlannerSpec Spec, PlannerView? View = null, PlannerLayout Layout = PlannerLayout.Wide);
```

In `ChartEndpoints.MapLumenCharts`, after the graph lines:

```csharp
        group.MapPost("/planner/svg",(PlannerRequest request)=>Render(()=>PlannerSvg.Render(request.Spec,request.View??PlannerView.WholePeriod,request.Layout),"image/svg+xml"));
```

and extend the XML doc summary sentence with "; <c>POST planner/svg</c> takes a <see cref=\"PlannerRequest\"/>". `PlannerView` is a `readonly record struct` with a positional constructor; System.Text.Json binds it by constructor parameters (`zoom`, `date`) under the web defaults. If binding fails in the HTTP check, add `[JsonConstructor]` is not available in the core project without a reference — instead give `PlannerView` `{ get; init; }` properties and a parameterless constructor, and keep the factory methods; re-run the unit round-trip test.

- [ ] **Step 4: Run tests to verify they pass**

Run:
```bash
taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2
dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "Planner|passed"
cd tests/Lumen.Charts.Baseline && dotnet run -c Release && dotnet run -c Release -- classic && diff <(tr -d '\r' < reference/refined.txt) <(tr -d '\r' < baseline.txt); diff <(tr -d '\r' < reference/classic.txt) <(tr -d '\r' < classic.txt); rm baseline.txt classic.txt; cd ../..
```
Expected: unit tests pass; both diffs show only `>` lines, all starting `planner/` (11 rows each), no `<` lines. Then start the gallery (`dotnet run --project samples/Lumen.Gallery -c Release --no-build --urls http://localhost:5188`, background) and run `pwsh -File tests/verify-api.ps1 -BaseUrl http://localhost:5188`: all checks pass including the three planner ones. Stop the gallery with `taskkill //IM Lumen.Gallery.exe //F`.

- [ ] **Step 5: Commit** (do **not** replace `reference/*.txt` here; the release step does)

```bash
git add src/Lumen.Charts/Planner.cs src/Lumen.Charts.AspNetCore/ChartEndpoints.cs tests/verify-api.ps1 tests/Lumen.Charts.Baseline/Program.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Serve the planner over HTTP and hash its views in the baseline" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Gallery demo and browser checks

**Files:**
- Create: `samples/Lumen.Gallery/PlannerData.cs`
- Modify: `samples/Lumen.Gallery/Components/Pages/Home.razor`
- Modify: `tests/Lumen.Charts.BrowserTests/Program.cs`

**Interfaces:**
- Consumes: `PlannerSvg.Render`, `PlannerSvg.Table`.
- Produces: a home-page section `id="planner"` titled "Planning a season" with the year view (static SVG, `MarkupString`), a month view (March) below it, and the March table inside a `<details><summary>March as a table</summary>…</details>`. The page picks `PlannerLayout.Narrow` when rendering for its narrow breakpoint is not possible server-side, so render **both** wide and narrow SVGs and show one by CSS media query (`@media (max-width: 639px)`), each in its own `div` with `aria-hidden` on the hidden one is not needed — use `display:none`, which removes it from the accessibility tree.

- [ ] **Step 1: Write the failing browser checks** (add to `tests/Lumen.Charts.BrowserTests/Program.cs`, near the other home-page checks; follow the file's existing `Test("…", async () => …)` and `Check` helpers and its existing phone-context helper)

```csharp
if (await page.Locator("#planner").CountAsync() > 0)
{
    await Test("Planner: the year and March are drawn, every event a named focusable mark, the table holds every day", async () =>
    {
        var marks = page.Locator("#planner .planner-wide svg .lumen-datum");
        Check(await marks.CountAsync() > 10, $"{await marks.CountAsync()} marks");
        var label = await marks.First.GetAttributeAsync("aria-label");
        Check(label is not null && label.Contains(", 2027"), label ?? "no label");
        await page.Locator("#planner details summary").ClickAsync();
        Check(await page.Locator("#planner table td").CountAsync() >= 31);
    });
    await Test("Planner: on a 375-pixel phone the narrow layout shows and no word runs outside its drawing", async () =>
    {
        await using var phone = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 });
        var tab = await phone.NewPageAsync();
        await tab.GotoAsync(baseUrl + "#planner");
        Check(await tab.Locator("#planner .planner-narrow").IsVisibleAsync() && !await tab.Locator("#planner .planner-wide").IsVisibleAsync());
        var outside = await tab.EvaluateAsync<int>("() => [...document.querySelectorAll('#planner .planner-narrow svg text')].filter(t => { const s = t.ownerSVGElement.getBoundingClientRect(), b = t.getBoundingClientRect(); return b.left < s.left - 0.5 || b.right > s.right + 0.5; }).length");
        Check(outside == 0, $"{outside} words outside");
    });
}
```

Also add the planner section to the existing home-page axe sweeps by making sure it is on the page when they run (no code change if the sweep covers the whole page).

- [ ] **Step 2: Run to verify they fail**

Build the browser tests explicitly (`dotnet build tests/Lumen.Charts.BrowserTests -c Release`), start the gallery, run `dotnet run --project tests/Lumen.Charts.BrowserTests -c Release --no-build`.
Expected: the planner checks are skipped (no `#planner` yet) — that is the "fail" signal for this task: the count of planner PASS lines is 0.

- [ ] **Step 3: Implement**

`samples/Lumen.Gallery/PlannerData.cs`:

```csharp
using Lumen.Charts;

namespace Lumen.Gallery;

/// <summary>An invented season of events by invented organizers, with South Africa's provinces and its 2027 public holidays,
/// for the home page's planner.</summary>
public static class PlannerData
{
    public static PlannerSpec Season(ChartTheme theme, int width = 1100) => PlannerSpec.ForYear(2027) with
    {
        Title = "Planning a season", Description = "Invented organizers' events in Gauteng and the Western Cape",
        Theme = theme, Width = width,
        Regions = [new("ZA", "South Africa"), new("ZA-GP", "Gauteng", "ZA"), new("ZA-WC", "Western Cape", "ZA")],
        Periods =
        [
            new(new(2027, 1, 1), null, "New Year's Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 3, 22), null, "Human Rights Day (observed)", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 3, 26), null, "Good Friday", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 3, 29), null, "Family Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 4, 27), null, "Freedom Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 5, 1), null, "Workers' Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 6, 16), null, "Youth Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 8, 9), null, "National Women's Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 9, 24), null, "Heritage Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 12, 16), null, "Day of Reconciliation", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 12, 25), null, "Christmas Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 12, 26), null, "Day of Goodwill", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 12, 27), null, "Day of Goodwill (observed)", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 3, 26), new DateOnly(2027, 4, 5), "Invented school holiday", PeriodKind.SchoolHoliday, "ZA")
        ],
        Events =
        [
            new("hx1", "Hilltop XCO #1", new(2027, 2, 13)) { Region = "ZA-GP", Category = "XCO", Audience = "Kids", Relevance = PlannerRelevance.Clash },
            new("cs", "Coast Stage Race", new(2027, 3, 12)) { End = new DateOnly(2027, 3, 14), Region = "ZA-WC", Category = "Stage", Audience = "Open", Status = PlannerStatus.Provisional },
            new("hx2", "Hilltop XCO #2", new(2027, 3, 13)) { Region = "ZA-GP", Category = "XCO", Audience = "Kids", Relevance = PlannerRelevance.Clash },
            new("cr", "Invented Club Ride", new(2027, 3, 13)) { Region = "ZA-GP", Category = "Road", Audience = "Open", Relevance = PlannerRelevance.Near },
            new("mine", "Our Spring Enduro", new(2027, 9, 18)) { Region = "ZA-GP", Category = "Enduro", Audience = "Juniors", Mine = true },
            new("cx", "Cancelled Night Race", new(2027, 5, 8)) { Region = "ZA-GP", Category = "XCO", Status = PlannerStatus.Cancelled, Relevance = PlannerRelevance.Near, Note = "Moved to 2028" },
        ]
    };
}
```

`Home.razor`: add near the other phone-card sections:

```razor
<section id="planner" class="card">
    <h2>Planning a season</h2>
    <p>A <code>PlannerSpec</code> drawn by <code>PlannerSvg.Render</code>: months aligned by weekday so weekends line up down the year, public holidays ◆, a school holiday band, and invented events by relevance — clash bold, close dashed, other thin, provisional hatched, cancelled struck through, yours outlined — each named in full.</p>
    <div class="planner-wide">
        @((MarkupString)PlannerSvg.Render(PlannerData.Season(ChartTheme.Light), PlannerView.WholePeriod))
        @((MarkupString)PlannerSvg.Render(PlannerData.Season(ChartTheme.Light), PlannerView.Month(2027, 3)))
    </div>
    <div class="planner-narrow">
        @((MarkupString)PlannerSvg.Render(PlannerData.Season(ChartTheme.Light, 340), PlannerView.WholePeriod, PlannerLayout.Narrow))
        @((MarkupString)PlannerSvg.Render(PlannerData.Season(ChartTheme.Light, 340), PlannerView.Month(2027, 3), PlannerLayout.Narrow))
    </div>
    <details><summary>March as a table</summary>@((MarkupString)PlannerSvg.Table(PlannerData.Season(ChartTheme.Light), 2027, 3))</details>
</section>
```

If the home page follows the theme switch with a variable (e.g. `theme`), use it instead of `ChartTheme.Light`. Add to `samples/Lumen.Gallery/wwwroot/app.css`:

```css
#planner .planner-narrow{display:none}
@media (max-width:639px){#planner .planner-wide{display:none}#planner .planner-narrow{display:block}}
#planner svg{margin-bottom:12px}
#planner table{border-collapse:collapse;font-size:12px}#planner td,#planner th{border:1px solid var(--lumen-control-border,#dce3ee);padding:4px;vertical-align:top}
```

Update any count of home-page sections or charts the HTTP checks or browser tests assert (search `verify-api.ps1` and `BrowserTests/Program.cs` for the home page's chart or card count, and adjust by the number this adds).

- [ ] **Step 4: Run to verify they pass**

```bash
taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2
dotnet build tests/Lumen.Charts.BrowserTests -c Release 2>&1 | grep -E "Warn|Error" | tail -2
```
Start the gallery (background), then `pwsh -File tests/verify-api.ps1 -BaseUrl http://localhost:5188` and `dotnet run --project tests/Lumen.Charts.BrowserTests -c Release --no-build`.
Expected: all pass, two new planner PASS lines, axe clean. Take screenshots of `#planner` at 1280 and on the 375 px phone into `C:\Users\jacqu\AppData\Local\Temp\lumen043\` (outside the repo) and look at them. Stop the gallery.

- [ ] **Step 5: Commit**

```bash
git add samples/Lumen.Gallery/PlannerData.cs samples/Lumen.Gallery/Components/Pages/Home.razor samples/Lumen.Gallery/wwwroot/app.css tests/Lumen.Charts.BrowserTests/Program.cs tests/verify-api.ps1
git commit -m "Show a season's planner on the gallery's home page" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Documentation, recipe and version

**Files:**
- Modify: `README.md` (a "Planning a season" section after "Calendars"; a "0.43.0 additions" entry where earlier releases have theirs)
- Modify: `docs/VERIFICATION.md` (a 0.43.0 paragraph at the top, in the file's style: what was added, the checks, the counts)
- Modify: `integrations/claude-code/lumen-charts/SKILL.md` (one bullet; frontmatter description stays ≤ 1024 characters with no unquoted `: `)
- Modify: `integrations/claude-code/lumen-charts/references/api.md` (the planner types and `PlannerSvg` members)
- Modify: `integrations/claude-code/lumen-charts/references/http-api.md` (`POST /api/charts/planner/svg` with the JSON example from Task 7)
- Modify: `integrations/claude-code/lumen-charts/references/recipes-race-face.md` (recipe "Season planner")
- Modify: `Directory.Build.props` (`<Version>0.43.0</Version>`)
- Modify: `tests/Lumen.Charts.Recipes/check.py` only if the new recipe block needs a stub type it cannot find (run it first)

- [ ] **Step 1: Write the recipe** (append to `recipes-race-face.md`, after "Team rider", in the file's style; variable names must be unique across all recipes — prefix with `season`)

````markdown
## Season planner

Organizers choosing a date see the year at a glance: months aligned by weekday so the weekends line up down the page, public holidays, school holidays and long weekends marked, and other organizers' events drawn by how much they compete with yours — the app decides that, Lumen only draws it. Zoom by drawing a month or a day. Every event is invented here.

```csharp
// The app's regions, holidays and events. The relevance is the app's own rule, worked out for the organizer viewing:
// same day, same province and same discipline or audience is a clash; an adjacent weekend or a neighbouring province is close.
PlannerRegion[] seasonRegions = [new("ZA", "South Africa"), new("ZA-GP", "Gauteng", "ZA"), new("ZA-WC", "Western Cape", "ZA")];
PlannerPeriod[] seasonDays =
[
    new(new(2027, 4, 27), null, "Freedom Day", PeriodKind.PublicHoliday, "ZA"),
    new(new(2027, 6, 26), new DateOnly(2027, 7, 18), "Invented school holiday", PeriodKind.SchoolHoliday, "ZA")
];
PlannerEvent[] seasonEvents =
[
    new("e1", "Hilltop XCO", new(2027, 3, 13)) { Region = "ZA-GP", Category = "XCO", Audience = "Kids", Relevance = PlannerRelevance.Clash },
    new("e2", "Coast Stage Race", new(2027, 3, 12)) { End = new DateOnly(2027, 3, 14), Region = "ZA-WC", Category = "Stage", Status = PlannerStatus.Provisional },
    new("mine", "Our Spring Enduro", new(2027, 9, 18)) { Region = "ZA-GP", Category = "Enduro", Mine = true }
];
var seasonPlanner = PlannerSpec.ForYear(2027) with
{
    Title = "Season planner", Description = "Gauteng and the country's holidays", Style = raceFace, Width = 1100,
    Regions = seasonRegions, Periods = seasonDays, Events = seasonEvents,
    Filter = new() { Regions = ["ZA-GP"] }   // the organizer's province; the country's holidays still show
};
string seasonYear = PlannerSvg.Render(seasonPlanner, PlannerView.WholePeriod);
string seasonMarch = PlannerSvg.Render(seasonPlanner, PlannerView.Month(2027, 3));
string seasonPhone = PlannerSvg.Render(seasonPlanner with { Width = 340 }, PlannerView.WholePeriod, PlannerLayout.Narrow);
string seasonTable = PlannerSvg.Table(seasonPlanner, 2027, 3);   // the month for screen readers and static pages
```

- **Relevance is yours to decide.** `Clash`, `Near` and `Other` are drawn bold, dashed and thin and said "clash" and "close" in every event's name; the planner never computes them, so the rule can change without a Lumen release.
- **Regions.** A filter on a province still shows the country's holidays and events set for the whole country; events in other provinces drop out. Leave `Filter` null to see everything.
- **Phones.** Below 640 pixels draw `PlannerLayout.Narrow`: the year becomes a bar of weekends per month with their clash counts in words, a month an agenda of the days that hold something.
- **Not colour.** Provisional is hatched, cancelled struck through, yours outlined, and each is said in words; on the card every stripe colour (`hi`, `low`) clears 3:1 and every word 4.5:1.
- **Interactive zoom** comes with `<LumenPlanner>` in 0.44.0; until then draw the view the page asks for.
````

Use `raceFace` only if the recipe file defines it before this point (it does near the top); otherwise use `ChartStyle.Dark`.

- [ ] **Step 2: Compile the recipes together**

Run: `cd tests/Lumen.Charts.Recipes && python check.py && dotnet run -c Release; cd ../..`
Expected: one more recipe block; all compile and render; no failures. Fix the recipe, not the harness, if a variable name collides.

- [ ] **Step 3: Write the docs**
- README "Planning a season": what it is, the data (one paragraph per record), the views and layouts, regions and filters, words and patterns, the table, the endpoint, limits (view only; `<LumenPlanner>` in 0.44.0; multi-day events listed on each day in the month view; at most three stripes per day in the year view; 400 days).
- `api.md`: every public type and member added, one line each.
- `http-api.md`: the endpoint with the Task 7 JSON.
- `SKILL.md`: one bullet under the chart list: "**Season planners** (0.43.0): `PlannerSpec` + `PlannerSvg.Render(spec, view, layout)` — a year aligned by weekday, a month, a day; holidays, weekends, events by relevance and region; `PlannerSvg.Table` for the month in words."
- `VERIFICATION.md`: the 0.43.0 paragraph with the counts measured in Task 10.
- `Directory.Build.props`: `0.43.0`.

- [ ] **Step 4: Verify the skill description length**

Run: `python - <<'EOF'` is not allowed (heredoc); instead write a two-line script with the Write tool to `C:\Users\jacqu\AppData\Local\Temp\skilllen.py` that reads `integrations/claude-code/lumen-charts/SKILL.md`, extracts the frontmatter `description`, prints its length and whether it contains an unquoted `": "`, and run `python C:/Users/jacqu/AppData/Local/Temp/skilllen.py`.
Expected: length ≤ 1024; no unquoted `: `.

- [ ] **Step 5: Commit**

```bash
git add README.md docs/VERIFICATION.md Directory.Build.props integrations/claude-code/lumen-charts/SKILL.md integrations/claude-code/lumen-charts/references/api.md integrations/claude-code/lumen-charts/references/http-api.md integrations/claude-code/lumen-charts/references/recipes-race-face.md
git commit -m "Document the season planner, add its recipe, and bump to 0.43.0" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Whole-release verification and release (the handover's ritual)

**Files:** none new; `tests/Lumen.Charts.Baseline/reference/*.txt` and its README replaced at the end.

- [ ] **Step 1: Full verification** (repository root)

```bash
taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2
dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | tail -1
dotnet build tests/Lumen.Charts.BrowserTests -c Release 2>&1 | grep -E "Warn|Error" | tail -2
dotnet build samples/Lumen.Wasm -c Release 2>&1 | grep -E "Warn|Error" | tail -2
```
Expected: 0 warnings everywhere; unit tests 0 failed.

Baseline in both finishes (only added `planner/` rows), gallery on 5188 + `verify-api.ps1` + browser suite, WebAssembly host on 5199 + browser suite with argument `http://localhost:5199` (stop it by PID: `netstat -ano | grep :5199`), recipes harness. Look at the screenshots. Scan the diff for anything that looks like a real person's name or result.

- [ ] **Step 2: Pack and smoke-test**

Clear `~/.nuget/packages/lumen.charts*/0.43.0`, `dotnet pack` the three projects into `artifacts/packages`, install `Lumen.Charts 0.43.0` into a scratch console app with a local `nuget.config`, render `PlannerSvg.Render(PlannerSpec.ForYear(2027) with { Events = [new("a","Invented",new(2027,3,13))] }, PlannerView.Month(2027,3))` and check the output contains `aria-label='Invented, Saturday 13 March 2027'`.

- [ ] **Step 3: Promote the baseline**

Copy `baseline.txt` → `reference/refined.txt` and `classic.txt` → `reference/classic.txt`, delete the outputs, and update `tests/Lumen.Charts.Baseline/README.md`'s reference paragraph to "v0.43.0, N rows … with 0.43.0's planner rows added".

- [ ] **Step 4: Commit, push, CI, tag, release**

```bash
git add -u
git commit -m "Promote the baseline to v0.43.0" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
gh run list --limit 1 --json databaseId -q '.[0].databaseId'   # then: gh run watch <id> --exit-status
git tag -a v0.43.0 <sha> -m "v0.43.0: a season planner"
git push origin v0.43.0
gh release create v0.43.0 artifacts/packages/Lumen.Charts.0.43.0.nupkg artifacts/packages/Lumen.Charts.Blazor.0.43.0.nupkg artifacts/packages/Lumen.Charts.AspNetCore.0.43.0.nupkg --title "v0.43.0 — a season planner" --notes-file <notes file outside the repo>
```

Never stage `lumen-charts-race-face-brief.md`. Release notes: New / Worth knowing / Verification, with measured counts and screenshots of the year, March and the phone layout.

- [ ] **Step 5: Hand-offs**
- Package the skill from the tag (`git archive v0.43.0 integrations/claude-code/lumen-charts`, `package_skill`), send it to the owner with the screenshots.
- Message the Race Face session ("RACEFACE RUNNING EXPANSION 2") that 0.43.0 adds the static planner and its recipe, that the interactive planner follows in 0.44.0, and that its own sub-project (events store, holidays, clash rules, the page) needs its own spec; deploys stay the owner's.
- Update the brain (`Release.Latest`, the roadmap, a task for 0.44.0) and `HANDOVER.md` (released, next, counts, the map row for the new files).
````
