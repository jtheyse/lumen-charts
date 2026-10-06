# Places and Points Chart (0.45.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `PlacingsChart.Build(results, options)`, which turns a list of finishing places (with optional field sizes, points, dates, labels and series) into Lumen's two-pane "places and points" `ChartSpec`, and `<LumenPlacings>`, a thin Blazor component that draws it or an empty-state slot.

**Architecture:** A pure builder in `src/Lumen.Charts/Placings.cs` (records `Placing`, `PlacingsOptions`, static class `PlacingsChart`) that assembles an ordinary `ChartSpec` from existing features — panes, `YReversed`, `ChangeColors.LowerIsBetter`, `ValueLabels`, `ValueNote`, filled markers — so the renderer is untouched. A Razor component `src/Lumen.Charts.Blazor/LumenPlacings.razor` calls the builder, applies the host's `Adjust`, and renders `<LumenChart FitWidth ShowToolbar=false>`, static SVG, or the `Empty` slot.

**Tech Stack:** C# / .NET 8 (`Lumen.Charts`, `Lumen.Charts.Blazor`), the executable unit harness `tests/Lumen.Charts.Tests` (`Test`/`Check`/`Reject`, `HtmlRenderer` + `NoJs`), the rendering-hash harness `tests/Lumen.Charts.Baseline`, `tests/verify-api.ps1`, the Playwright suite `tests/Lumen.Charts.BrowserTests`, the recipe compiler `tests/Lumen.Charts.Recipes`.

**Spec:** `docs/superpowers/specs/2026-10-06-placings-chart-design.md`

## Global Constraints

- One feature per release: this plan is **0.45.0** only. No HTTP endpoint; no change to any existing renderer; the heatmap is 0.46.0.
- **No existing rendering moves:** `tests/Lumen.Charts.Baseline` against `reference/*.txt` (v0.44.0, 385 rows) shows only **three added rows** in each finish (`placings/points`, `placings/no-points`, `placings/two-series`); the other 385 are identical.
- **Never colour alone:** each place's change against the previous race of its own series is said in words in its name and tooltip (Lumen's `ChangeColors` does this: ", better than the previous" / ", worse than the previous" / ", level with the previous"). Drawn text clears **4.5:1**, marks **3:1** (the renderer already ensures this for value labels).
- Defaults verbatim from the spec: `PlaceName = "Place"`, `PointsName = "Points"`, `DateFormat = "d MMM yyyy"` (invariant culture), `Unlabelled = "#{0}"`, title `"Places and points"`, Y label `"Place"`, width **340**, height **380** with points and **260** without, description `"Finishing place out of the field, first at the top, and points. Best: {best}."` / `"Finishing place out of the field, first at the top. Best: {best}."`, the points pane `Weight = 1`.
- **Invented data only** in tests, gallery, recipes and docs. Never open, copy from or stage `lumen-charts-race-face-brief.md`. Race Face's code (`D:/CLAUDE/RaceSenseNet`) is a behavioural reference only; copy no data from it.
- Every new public member has an XML doc comment (the build fails otherwise); Release build at **0 warnings**; the unit test "The packages carry their XML documentation…" covers `LumenPlacings`.
- Version **0.45.0** in `Directory.Build.props` (Task 4).
- Repository rules (HANDOVER.md): kill only `Lumen.Gallery.exe` by image name, any other process by PID; never kill `dotnet.exe` by name; patch scripts via the Write tool; stage explicit paths only; `tests/Lumen.Charts.BrowserTests` and `samples/Lumen.Wasm` build separately from the solution.

## Review Focus

1. **A field size of 0 or missing, or a place larger than its field** (bad source data) — the place is still drawn; the "/field" note only when `Field > 0`. Test in Task 1.
2. **Points of 0** — a real zero is drawn as 0, distinct from a missing value (a gap). Test in Task 1.
3. **Two races on the same date, or series keys that differ only by surrounding spaces** (`" League "` vs `"League"`) — same-date races keep their given order (stable sort); series keys are trimmed, so the spaces do not split a series. Test in Task 1.
4. **A single placed race** — the chart still builds and renders (X from −0.5 to 0.5, no change words because there is no previous race). Test in Task 1.
5. **Many series (five) with fewer colours** — colours cycle through `PlaceColors` (or the palette), every line keeps a distinct name. Test in Task 1.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/Lumen.Charts/Placings.cs` (create) | `Placing`, `PlacingsOptions`, `PlacingsChart.Build`. |
| `src/Lumen.Charts.Blazor/LumenPlacings.razor` (create) | The component: builder + `Adjust` + `LumenChart` / static SVG / `Empty`. |
| `tests/Lumen.Charts.Tests/Program.cs` (modify) | Builder and component tests; `typeof(LumenPlacings)` in the documentation test (about line 3974). |
| `tests/Lumen.Charts.Baseline/Program.cs` (modify) | Three added rows after the 0.43.0 planner rows (about line 840). |
| `samples/Lumen.Gallery/SportsData.cs` (modify) | Invented `Placings` data; a `places-points` card at the end of the racing cards; `SportsCard.Placings` property. |
| `samples/Lumen.Gallery/Components/Pages/Sports.razor` (modify) | Render the card with `<LumenPlacings>`. |
| `tests/Lumen.Charts.BrowserTests/Program.cs`, `tests/verify-api.ps1` (modify) | Sports page chart counts +1; a card check; an HTTP check. |
| `README.md`, `integrations/claude-code/lumen-charts/SKILL.md`, `references/api.md`, `references/recipes-race-face.md`, `docs/VERIFICATION.md`, `Directory.Build.props` (modify) | Docs, recipe, version. |

Commands (Git Bash, repository or worktree root):

- Build: `taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2`
- Unit: `dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build 2>&1 | grep -E "^FAIL|Placings|LumenPlacings|passed"`
- Baseline: `cd tests/Lumen.Charts.Baseline && dotnet run -c Release && dotnet run -c Release -- classic && diff <(tr -d '\r' < reference/refined.txt) <(tr -d '\r' < baseline.txt); diff <(tr -d '\r' < reference/classic.txt) <(tr -d '\r' < classic.txt); rm baseline.txt classic.txt; cd ../..`

---

### Task 1: The builder

**Files:**
- Create: `src/Lumen.Charts/Placings.cs`
- Modify: `tests/Lumen.Charts.Tests/Program.cs` (append tests before the final summary lines)

**Interfaces:**
- Consumes: `ChartSpec`, `ChartSeries(string Name, IReadOnlyList<ChartPoint> Points, string? Color = null)` with init `Pane`, `Markers`, `ValueLabels`, `ChangeColors`; `ChartPoint(double X, double? Y, string? Label = null, double Size = 1)` with init `ValueNote`; `ChartPane` (`Label`, `Weight`); `ChartStyle.Light` (`Text`, `Series`); `ChangeColors.LowerIsBetter`; `MarkerStyle.Filled`; `ChartKind.Line`.
- Produces (Task 2, the gallery and the docs rely on these exact names): `public sealed record Placing(int? Place)` with init `Field` (`int?`), `Points` (`double?`), `Date` (`DateOnly?`), `Label` (`string?`), `Series` (`string?`); `public sealed record PlacingsOptions` with init `PlaceName`, `PointsName`, `DateFormat`, `Unlabelled` (`string`), `PlaceColors` (`IReadOnlyList<string>?`), `PointsColor` (`string?`), `Style` (`ChartStyle?`); `public static class PlacingsChart { public static ChartSpec? Build(IEnumerable<Placing> results, PlacingsOptions? options = null); }`.

- [ ] **Step 1: Write the failing tests**

```csharp
// 0.45.0: places and points.
Placing Raced(int? place,int? field,double? points,int week,string? series=null)=>new(place){Field=field,Points=points,Date=new DateOnly(2027,3,1).AddDays(7*week),Series=series};
string[] PlacedNames(ChartSpec spec)=>Regex.Matches(ChartSvg.Render(spec),"aria-label='([^']*)'").Select(m=>m.Groups[1].Value).ToArray();
Test("Placings: nothing placed is no chart, so the page shows its own empty state",()=>{
    Check(PlacingsChart.Build([])is null,"no results");
    Check(PlacingsChart.Build([new Placing(null){Points=10},new Placing(0){Field=20},new Placing(-3)])is null,"no place above 0");
});
Test("Placings: places read up as better, carry their field, and say their change in words",()=>{
    var spec=PlacingsChart.Build([Raced(30,50,40,0),Raced(24,48,52,1),Raced(27,51,47,2),Raced(19,null,58,3)])!;
    Check(spec.Kind==ChartKind.Line&&spec.YReversed&&spec.YLabel=="Place"&&spec.Title=="Places and points","shape");
    Check(spec.Width==340&&spec.Height==380&&spec.XMin==-.5&&spec.XMax==3.5,"size and X");
    Check(spec.Panes.Count==1&&spec.Panes[0].Label=="Points"&&spec.Panes[0].Weight==1,"points pane");
    Check(spec.Description=="Finishing place out of the field, first at the top, and points. Best: 19.",spec.Description);
    var names=PlacedNames(spec);
    Check(names.Contains("Place: 8 Mar 2027, 24/48, better than the previous"),"better");
    Check(names.Contains("Place: 15 Mar 2027, 27/51, worse than the previous"),"worse");
    Check(names.Contains("Place: 22 Mar 2027, 19, better than the previous"),"no field: the place alone");
    var place=spec.Series[0];
    Check(place.ChangeColors==ChangeColors.LowerIsBetter&&place.ValueLabels&&place.Markers==MarkerStyle.Filled,"place line");
});
Test("Placings: a race without points is a gap, never a zero, and a real zero stays a zero",()=>{
    var points=PlacingsChart.Build([Raced(5,20,40,0),Raced(6,20,0,1),Raced(4,20,null,2),Raced(3,20,52,3)])!.Series[^1];
    Check(points.Name=="Points"&&points.Pane==1&&points.ValueLabels&&points.Markers==MarkerStyle.Filled,"points line");
    Check(points.Points[1].Y==0&&points.Points[2].Y is null,"zero and gap");
});
Test("Placings: with no points anywhere there is no points line and no empty pane",()=>{
    var spec=PlacingsChart.Build([Raced(5,20,null,0),Raced(4,22,null,1)])!;
    Check(spec.Panes.Count==0&&spec.Series.Count==1&&spec.Height==260,"no pane");
    Check(spec.Description=="Finishing place out of the field, first at the top. Best: 4.",spec.Description);
});
Test("Placings: a place is compared only with the previous race of its own series",()=>{
    var spec=PlacingsChart.Build([Raced(30,50,40,0,"Invented League"),Raced(5,20,null,1,"Invented Open"),Raced(24,48,52,2," Invented League "),Raced(7,21,null,3,"Invented Open")])!;
    Check(spec.Series.Select(s=>s.Name).SequenceEqual(["Place · Invented League","Place · Invented Open","Points"]),string.Join("|",spec.Series.Select(s=>s.Name)));
    var names=PlacedNames(spec);
    Check(names.Contains("Place · Invented League: 15 Mar 2027, 24/48, better than the previous"),"vs 30th, not vs the open race's 5th");
    Check(names.Contains("Place · Invented Open: 22 Mar 2027, 7/21, worse than the previous"),"open vs open");
    Check(spec.Series[0].Points[1].Y is null&&spec.Series[1].Points[0].Y is null,"gaps at the other series' races");
    var blank=PlacingsChart.Build([Raced(3,9,null,0),Raced(4,9,null,1,"Invented Cup")])!;
    Check(blank.Series.Select(s=>s.Name).SequenceEqual(["Place","Place · Invented Cup"]),"a result with no series is the place line alone");
});
Test("Placings: races are ordered by date when all have one, else kept as given, and labelled by label, date or number",()=>{
    var sorted=PlacingsChart.Build([Raced(3,9,null,2),Raced(5,9,null,0),Raced(4,9,null,1)])!;
    Check(sorted.Series[0].Points.Select(p=>p.Y).SequenceEqual(new double?[]{5,4,3}),"by date");
    var same=PlacingsChart.Build([new Placing(8){Date=new(2027,3,1)},new Placing(2){Date=new(2027,3,1)}])!;
    Check(same.Series[0].Points.Select(p=>p.Y).SequenceEqual(new double?[]{8,2}),"same date keeps the given order");
    var mixed=PlacingsChart.Build([Raced(3,9,null,2),new Placing(5),new Placing(4){Label="Final"}])!;
    Check(mixed.Series[0].Points.Select(p=>p.Y).SequenceEqual(new double?[]{3,5,4}),"an undated race keeps the given order");
    Check(mixed.Series[0].Points.Select(p=>p.Label).SequenceEqual(["15 Mar 2027","#2","Final"]),string.Join("|",mixed.Series[0].Points.Select(p=>p.Label)));
    var custom=PlacingsChart.Build([Raced(3,9,null,0),new Placing(5)],new(){DateFormat="dd-MM-yyyy",Unlabelled="R{0}"})!;
    Check(custom.Series[0].Points.Select(p=>p.Label).SequenceEqual(["01-03-2027","R2"]),"custom format and number");
});
Test("Placings: colours come from the style, or cycle through the host's",()=>{
    var light=ChartStyle.Light;
    var two=PlacingsChart.Build([Raced(3,9,1,0,"A"),Raced(4,9,2,1,"B")])!;
    Check(two.Series[0].Color==light.Text&&two.Series[1].Color==light.Series[0]&&two.Series[2].Color==light.Series[1]&&two.Style==light,"defaults");
    var five=PlacingsChart.Build(Enumerable.Range(0,5).Select(i=>Raced(i+1,9,null,i,$"S{i}")),new(){PlaceColors=["#F5F6F7","#F5B642"],PointsColor="#D7DDE5",Style=ChartStyle.Dark})!;
    Check(five.Series.Select(s=>s.Color).SequenceEqual(["#F5F6F7","#F5B642","#F5F6F7","#F5B642","#F5F6F7"]),"cycled");
    Check(five.Series.Select(s=>s.Name).Distinct().Count()==5&&five.Style==ChartStyle.Dark,"names and style");
    var pts=PlacingsChart.Build([Raced(3,9,1,0)],new(){PointsColor="#D7DDE5"})!;
    Check(pts.Series[^1].Color=="#D7DDE5","points colour");
});
Test("Placings: odd but real data still draws: no field, a field of 0, a place past its field, a single race",()=>{
    var spec=PlacingsChart.Build([Raced(30,0,null,0),Raced(25,20,null,1)])!;
    Check(spec.Series[0].Points[0].ValueNote is null&&spec.Series[0].Points[1].ValueNote=="/20","notes");
    var one=PlacingsChart.Build([Raced(2,10,5,0)])!;
    Check(one.XMin==-.5&&one.XMax==.5,"one race");
    foreach(var s in new[]{spec,one}){var svg=ChartSvg.Render(s);Check(svg.StartsWith("<svg"),"renders");}
    Check(!PlacedNames(one).Any(n=>n.Contains("previous")),"no previous race, no change words");
});
Test("Placings: refusals say why",()=>{
    Reject(()=>PlacingsChart.Build(null!));
    Reject(()=>PlacingsChart.Build([new Placing(1),null!]));
    foreach(var bad in new PlacingsOptions[]{new(){PlaceName=" "},new(){PointsName=""},new(){DateFormat=""},new(){Unlabelled="R"},new(){PlaceColors=[]}})
        Reject(()=>PlacingsChart.Build([new Placing(1)],bad));
});
```

- [ ] **Step 2: Run them to see them fail**

Build. Expected: `error CS0246: The type or namespace name 'Placing' could not be found`.

- [ ] **Step 3: Write the builder**

Create `src/Lumen.Charts/Placings.cs`:

```csharp
using System.Globalization;

namespace Lumen.Charts;

/// <summary>One event's result for <see cref="PlacingsChart.Build"/>: the place it finished in, and what is known about it.</summary>
/// <param name="Place">The finishing place, 1 for first. A result without a place, or with 0 or less, is left out of the chart.</param>
public sealed record Placing(int? Place)
{
    /// <summary>The size of the field, written after the place as <c>/48</c> when it is above 0.</summary>
    public int? Field { get; init; }
    /// <summary>The points the event earned; null draws a gap in the points line, never a zero.</summary>
    public double? Points { get; init; }
    /// <summary>The event's date. When every result has one, the events are drawn in date order; it also labels the event.</summary>
    public DateOnly? Date { get; init; }
    /// <summary>The event's label on the X axis, its tooltip and its name; it wins over <see cref="Date"/>.</summary>
    public string? Label { get; init; }
    /// <summary>The series the event belongs to (a league, a cup). A place is better or worse only than the previous event of
    /// the same series; each series is its own line. Surrounding spaces are ignored; null or blank is a series of its own.</summary>
    public string? Series { get; init; }
}

/// <summary>The words and colours of a <see cref="PlacingsChart"/>. The title, description, axis label, width and height are set
/// on the spec the builder returns, with <c>spec with { … }</c>.</summary>
public sealed record PlacingsOptions
{
    /// <summary>The places line's name, and the start of each line's name when there are several series. Default "Place".</summary>
    public string PlaceName { get; init; } = "Place";
    /// <summary>The points line's name and its pane's label. Default "Points".</summary>
    public string PointsName { get; init; } = "Points";
    /// <summary>How a date labels an event, in the invariant culture. Default "d MMM yyyy".</summary>
    public string DateFormat { get; init; } = "d MMM yyyy";
    /// <summary>The label of an event with neither a label nor a date; <c>{0}</c> is its number, from 1, in the drawn order. Default "#{0}".</summary>
    public string Unlabelled { get; init; } = "#{0}";
    /// <summary>The places lines' colours, used in turn; null takes the style's text colour for the first line and its palette for the others.</summary>
    public IReadOnlyList<string>? PlaceColors { get; init; }
    /// <summary>The points line's colour; null takes the style's second palette colour.</summary>
    public string? PointsColor { get; init; }
    /// <summary>The chart's style; null is <see cref="ChartStyle.Light"/>.</summary>
    public ChartStyle? Style { get; init; }
}

/// <summary>Builds the places and points chart: finishing places on a reversed axis, first at the top, one line per series with each
/// place coloured and named by its change against the previous event of the same series, the field after each place, and the
/// points each event earned in a pane beneath.</summary>
public static class PlacingsChart
{
    /// <summary>The chart for <paramref name="results"/>, or null when no result has a place (so a page can show its own empty state).
    /// The result is an ordinary <see cref="ChartSpec"/>: render it with <see cref="ChartSvg.Render(ChartSpec, bool, bool)"/> or
    /// <c>&lt;LumenChart&gt;</c>, or change it with <c>with</c>.</summary>
    /// <exception cref="ArgumentException">A null result, a blank name or date format, an <see cref="PlacingsOptions.Unlabelled"/>
    /// without <c>{0}</c>, or an empty <see cref="PlacingsOptions.PlaceColors"/>.</exception>
    public static ChartSpec? Build(IEnumerable<Placing> results, PlacingsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(results);
        var o = options ?? new PlacingsOptions();
        if (string.IsNullOrWhiteSpace(o.PlaceName)) throw new ArgumentException("A places chart needs a name for its places line (PlaceName).");
        if (string.IsNullOrWhiteSpace(o.PointsName)) throw new ArgumentException("A places chart needs a name for its points line and pane (PointsName).");
        if (string.IsNullOrWhiteSpace(o.DateFormat)) throw new ArgumentException("A places chart needs a date format (DateFormat) to label an event by its date.");
        if (o.Unlabelled is null || !o.Unlabelled.Contains("{0}", StringComparison.Ordinal)) throw new ArgumentException("Unlabelled must contain {0}, where an event's number goes, such as \"R{0}\".");
        if (o.PlaceColors is { Count: 0 }) throw new ArgumentException("PlaceColors must name at least one colour, or be null for the style's.");
        var style = o.Style ?? ChartStyle.Light;
        var placed = results.Select(r => r ?? throw new ArgumentException("A places chart's results may not contain null.")).Where(r => r.Place is > 0).ToList();
        if (placed.Count == 0) return null;
        // OrderBy is stable, so events on the same date keep the order they were given in.
        var races = placed.All(r => r.Date is not null) ? placed.OrderBy(r => r.Date!.Value).ToList() : placed;
        var labels = races.Select((r, i) => r.Label ?? r.Date?.ToString(o.DateFormat, CultureInfo.InvariantCulture)
            ?? string.Format(CultureInfo.InvariantCulture, o.Unlabelled, i + 1)).ToArray();
        static string Key(Placing r) => r.Series?.Trim() ?? "";
        var keys = races.Select(Key).Distinct(StringComparer.Ordinal).ToList();
        string Color(int k) => o.PlaceColors is { } colors ? colors[k % colors.Count] : k == 0 ? style.Text : style.Series[(k - 1) % style.Series.Count];
        // Each series is its own line, with a value only at its own events, so a place is judged only against the previous event
        // of the same series.
        var lines = keys.Select((key, k) => new ChartSeries(
                keys.Count == 1 || key.Length == 0 ? o.PlaceName : $"{o.PlaceName} · {key}",
                races.Select((r, i) => new ChartPoint(i, Key(r) == key ? r.Place : null, labels[i])
                    { ValueNote = Key(r) == key && r.Field is > 0 ? $"/{r.Field}" : null }).ToArray(),
                Color(k))
            { ChangeColors = ChangeColors.LowerIsBetter, ValueLabels = true, Markers = MarkerStyle.Filled }).ToList();
        var best = races.Min(r => r.Place!.Value);
        var anyPoints = races.Any(r => r.Points is not null);
        var spec = new ChartSpec
        {
            Kind = ChartKind.Line, Width = 340, Height = anyPoints ? 380 : 260, Style = style,
            Title = "Places and points",
            Description = anyPoints ? $"Finishing place out of the field, first at the top, and points. Best: {best}."
                : $"Finishing place out of the field, first at the top. Best: {best}.",
            XMin = -0.5, XMax = races.Count - 0.5, YReversed = true, YLabel = "Place",
            Series = lines,
        };
        if (!anyPoints) return spec;
        var points = new ChartSeries(o.PointsName, races.Select((r, i) => new ChartPoint(i, r.Points, labels[i])).ToArray(),
                o.PointsColor ?? style.Series[1 % style.Series.Count])
            { Pane = 1, ValueLabels = true, Markers = MarkerStyle.Filled };
        return spec with { Panes = [new ChartPane { Label = o.PointsName, Weight = 1 }], Series = [.. lines, points] };
    }
}
```

(If `ChartSvg.Render`'s overload in the `<see cref>` does not match the real signature, cite the real one; if `ChartSpec.Series`/`Panes` are typed so that a `List<ChartSeries>` or the collection expressions do not convert, convert with `.ToArray()` — say so in the report.)

- [ ] **Step 4: Run the build and the unit tests**

Expected: 0 warnings; the nine `Placings:` tests pass; every other test still passes. If a name format differs from the tests' expectation (for example the renderer writes the change words differently), fix the test to the renderer's actual words only after checking the words come from `ChartSvg.PointLabel`/the change code, and say so.

- [ ] **Step 5: Commit**

```bash
git add src/Lumen.Charts/Placings.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Build the places and points chart from a list of results" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The component

**Files:**
- Create: `src/Lumen.Charts.Blazor/LumenPlacings.razor`
- Modify: `tests/Lumen.Charts.Tests/Program.cs` (tests beside Task 1's; `typeof(LumenPlacings)` in the documentation test's type list, about line 3974)

**Interfaces:**
- Consumes: Task 1's `Placing`, `PlacingsOptions`, `PlacingsChart.Build`; `LumenChart` (`Spec`, `FitWidth`, `ShowToolbar`); `ChartSvg.Render(ChartSpec)`.
- Produces: `LumenPlacings` with parameters `Results` (`IEnumerable<Placing>`, `EditorRequired`), `Options` (`PlacingsOptions?`), `Adjust` (`Func<ChartSpec, ChartSpec>?`), `Static` (`bool`), `Empty` (`RenderFragment?`), cascading `CascadingStyle` (`ChartStyle?`).

- [ ] **Step 1: Write the failing tests**

```csharp
string PlacingsMarkup(Dictionary<string,object?> parameters,ChartStyle? cascaded=null)
{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        return renderer.Dispatcher.InvokeAsync(async()=>{
            if(cascaded is null)return (await renderer.RenderComponentAsync<LumenPlacings>(ParameterView.FromDictionary(parameters))).ToHtmlString();
            RenderFragment content=b=>{b.OpenComponent<LumenPlacings>(0);b.AddMultipleAttributes(1,parameters!);b.CloseComponent();};
            return (await renderer.RenderComponentAsync<CascadingValue<ChartStyle>>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Value",cascaded},{"ChildContent",content}}))).ToHtmlString();
        }).GetAwaiter().GetResult();
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
}
Placing[] PlacedSeason=[Raced(30,50,40,0),Raced(24,48,52,1),Raced(27,51,47,2)];
Test("LumenPlacings: draws the chart as a fitted component without a toolbar, with the host's last word",()=>{
    var html=PlacingsMarkup(new(){{"Results",PlacedSeason},{"Adjust",(Func<ChartSpec,ChartSpec>)(s=>s with{Title="Position & points by race"})}});
    Check(html.Contains("class=\"lumen-chart lumen-fit\"")&&html.Contains("lumen-quiet"),"fitted, no toolbar");
    Check(html.Contains("Position &amp; points by race")||html.Contains("Position & points by race"),"adjusted title");
    Check(html.Contains("better than the previous"),"change words");
});
Test("LumenPlacings: shows its Empty slot when nothing is placed, and nothing without one",()=>{
    RenderFragment empty=b=>b.AddMarkupContent(0,"<p class=\"none\">No races yet</p>");
    Check(PlacingsMarkup(new(){{"Results",new[]{new Placing(null)}},{"Empty",empty}}).Contains("<p class=\"none\">No races yet</p>"),"slot");
    Check(PlacingsMarkup(new(){{"Results",Array.Empty<Placing>()}}).Trim().Length==0,"nothing");
});
Test("LumenPlacings: Static writes the plain SVG with no component around it",()=>{
    var html=PlacingsMarkup(new(){{"Results",PlacedSeason},{"Static",true}});
    Check(html.TrimStart().StartsWith("<svg")&&!html.Contains("lumen-chart")&&!html.Contains("lumen-tools"),html[..Math.Min(80,html.Length)]);
});
Test("LumenPlacings: a cascaded style is used unless the options set one",()=>{
    var midnight=PlacingsMarkup(new(){{"Results",PlacedSeason}},ChartStyle.Midnight);
    Check(midnight.Contains(ChartStyle.Midnight.Background),"cascade");
    var own=PlacingsMarkup(new(){{"Results",PlacedSeason},{"Options",new PlacingsOptions{Style=ChartStyle.Dark}}},ChartStyle.Midnight);
    Check(own.Contains(ChartStyle.Dark.Background)&&!own.Contains(ChartStyle.Midnight.Background),"options win");
});
```

Add `typeof(LumenPlacings)` to the documentation test's list:

```csharp
        .Concat(new[]{typeof(LumenChart),typeof(LumenGraph),typeof(LumenBrand),typeof(LumenPlanner),typeof(LumenPlacings)}.SelectMany(t=>t.GetProperties(declared)
```

- [ ] **Step 2: Run them to see them fail**

Build. Expected: `CS0246: The type or namespace name 'LumenPlacings' could not be found`.

- [ ] **Step 3: Write the component**

Create `src/Lumen.Charts.Blazor/LumenPlacings.razor`:

```razor
@if (spec is { } chart)
{
    if (Static)
    {
        @((MarkupString)ChartSvg.Render(chart))
    }
    else
    {
        <LumenChart Spec="chart" FitWidth="true" ShowToolbar="false" />
    }
}
else
{
    @Empty
}
@code {
    /// <summary>The results to draw, one per event; see <see cref="PlacingsChart.Build"/>.</summary>
    [Parameter, EditorRequired] public IEnumerable<Placing> Results { get; set; } = [];
    /// <summary>The chart's words and colours. When it sets no <see cref="PlacingsOptions.Style"/>, a cascaded style is used.</summary>
    [Parameter] public PlacingsOptions? Options { get; set; }
    /// <summary>The host's last word on the chart, such as its title, axis label or a style for a raised card.</summary>
    [Parameter] public Func<ChartSpec, ChartSpec>? Adjust { get; set; }
    /// <summary>Writes the chart as plain SVG with no script, for a static page; otherwise it is an interactive
    /// <see cref="LumenChart"/> that fits its box, without a toolbar.</summary>
    [Parameter] public bool Static { get; set; }
    /// <summary>What to show when no result has a place; nothing is shown without it.</summary>
    [Parameter] public RenderFragment? Empty { get; set; }
    /// <summary>A host-wide style, usually cascaded by <see cref="LumenBrand"/>. A style in <see cref="Options"/> wins.</summary>
    [CascadingParameter] public ChartStyle? CascadingStyle { get; set; }

    private ChartSpec? spec;

    protected override void OnParametersSet()
    {
        var options = Options ?? new PlacingsOptions();
        if (options.Style is null && CascadingStyle is not null) options = options with { Style = CascadingStyle };
        var built = PlacingsChart.Build(Results, options);
        spec = built is null ? null : Adjust is null ? built : Adjust(built);
    }
}
```

- [ ] **Step 4: Run the build and the unit tests**

Expected: 0 warnings; the four `LumenPlacings:` tests and the documentation test pass; all others still pass. If the prerendered markup quotes or escapes differently from a test pattern, adjust the pattern to the renderer's real output and say so.

- [ ] **Step 5: Commit**

```bash
git add src/Lumen.Charts.Blazor/LumenPlacings.razor tests/Lumen.Charts.Tests/Program.cs
git commit -m "Add LumenPlacings, which draws the places and points chart or an empty state" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Baseline rows, the gallery card, and its browser and HTTP checks

**Files:**
- Modify: `tests/Lumen.Charts.Baseline/Program.cs` (after the planner rows, about line 840)
- Modify: `samples/Lumen.Gallery/SportsData.cs` (`SportsCard` record about line 47; the `Cards(...)` list's racing cards about line 1115)
- Modify: `samples/Lumen.Gallery/Components/Pages/Sports.razor` (the card rendering branch, about lines 46–62)
- Modify: `tests/Lumen.Charts.Tests/Program.cs` (the racing card ids check, about line 7569)
- Modify: `tests/Lumen.Charts.BrowserTests/Program.cs` (the Sports page count 31 → 32, about lines 1685–1693; a new card check)
- Modify: `tests/verify-api.ps1` (the Sports page's `lumen-chart lumen-fit` count 28 → 29 and its description, line 10; a new check)

**Interfaces:**
- Consumes: Tasks 1–2.
- Produces: `SportsData.Placings` (`IReadOnlyList<Placing>`), `SportsData.PlacesLook(ChartSpec)`, `SportsCard.Placings` (`IReadOnlyList<Placing>?`), a card with id `places-points`.

- [ ] **Step 1: Baseline rows**

After the planner rows in `tests/Lumen.Charts.Baseline/Program.cs` add (invented data; `Finished(style, theme)` is the harness's own finish helper, as the planner rows use it):

```csharp
// 0.45.0: places and points.
Placing[] placed = [
    new(18) { Field = 40, Points = 33, Date = new(2027, 3, 13), Series = "Invented League" },
    new(6) { Field = 22, Date = new(2027, 4, 3), Series = "Invented Open" },
    new(14) { Field = 42, Points = 37, Date = new(2027, 4, 24), Series = "Invented League" },
    new(16) { Field = 41, Points = 35, Date = new(2027, 6, 19), Series = "Invented League" },
    new(11) { Field = 44, Points = 40, Date = new(2027, 8, 14), Series = "Invented League" }];
var league = placed.Where(p => p.Series == "Invented League").Select(p => p with { Series = null }).ToArray();
lines.Add($"placings/points {Hash(ChartSvg.Render(PlacingsChart.Build(league, new() { Style = Finished(null, ChartTheme.Light) })!))}");
lines.Add($"placings/no-points {Hash(ChartSvg.Render(PlacingsChart.Build(league.Select(p => p with { Points = null }), new() { Style = Finished(null, ChartTheme.Light) })!))}");
lines.Add($"placings/two-series {Hash(ChartSvg.Render(PlacingsChart.Build(placed, new() { Style = Finished(null, ChartTheme.Light) })!))}");
```

Run the baseline in both finishes. Expected: `diff` shows exactly three added rows (`> placings/points …`, `> placings/no-points …`, `> placings/two-series …`) in each finish, nothing removed or changed. Do not edit `reference/*.txt`.

- [ ] **Step 2: Write the failing gallery checks**

In `tests/Lumen.Charts.Tests/Program.cs`, change the racing ids check (about line 7569) to expect `["race-results","field","season-arc","gap","places-points"]`.

In `tests/Lumen.Charts.BrowserTests/Program.cs`, change the Sports page's 31 to 32 everywhere in its "renders its thirty-one charts" check (the `drawnToFit` script, the count, the loop, the tooltip count) and its name to "thirty-two". After that check add:

```csharp
    await Test("The Sports & performance page draws its places and points with LumenPlacings, each place named with its series and its change", async () =>
    {
        var card = sports.Locator("#places-points .lumen-chart");
        Check(await card.CountAsync() == 1, "one chart in the card");
        var names = await card.Locator(".lumen-datum[data-point]").EvaluateAllAsync<string[]>("marks => marks.map(m => m.getAttribute('aria-label') ?? '')");
        Check(names.Any(n => n.StartsWith("Place · Invented League: ") && n.Contains("14/42") && n.EndsWith("better than the previous")), string.Join(" | ", names));
        Check(names.Any(n => n.StartsWith("Place · Invented Open: ") && n.EndsWith("worse than the previous")), "the open series compared with itself");
        Check(names.Any(n => n.StartsWith("Points: ")), "the points pane");
    });
```

In `tests/verify-api.ps1` line 10, change `-eq 28` to `-eq 29` and "twenty-eight" to "twenty-nine" in its message; after the race-results check (about line 204) add:

```powershell
Verify ($r.Content.Contains('id="places-points"') -and $r.Content.Contains("aria-label='Place · Invented League: 25 Apr 2026, 14/42, better than the previous'") -and $r.Content.Contains("aria-label='Place · Invented Open: 23 May 2026, 9/25, worse than the previous'")) 'The Sports & performance page prerenders its places and points from PlacingsChart: one line per series, each place named with its field and its change against the same series'
```

(Make sure `$r` holds the Sports page response at that point, as the race-results check's does.)

Run the unit tests (the ids check fails), start the gallery and run `verify-api.ps1` (the new checks fail) — expected failures before Step 3.

- [ ] **Step 3: The gallery card**

In `samples/Lumen.Gallery/SportsData.cs`:
- Add to `SportsCard` (beside its other init properties): `/// <summary>Results this card draws with <c>&lt;LumenPlacings&gt;</c> instead of its own spec.</summary> public IReadOnlyList<Placing>? Placings { get; init; }`
- Add invented data and the card's look:

```csharp
    /// <summary>An invented season in two series, a league and an open race, for the places and points card: the league's points are
    /// earned, the open races score none.</summary>
    public static readonly IReadOnlyList<Placing> Placings =
    [
        new(18) { Field = 40, Points = 33, Date = new(2026, 3, 14), Series = "Invented League" },
        new(6) { Field = 22, Date = new(2026, 4, 4), Series = "Invented Open" },
        new(14) { Field = 42, Points = 37, Date = new(2026, 4, 25), Series = "Invented League" },
        new(9) { Field = 25, Date = new(2026, 5, 23), Series = "Invented Open" },
        new(16) { Field = 41, Points = 35, Date = new(2026, 6, 20), Series = "Invented League" },
        new(11) { Field = 44, Points = 40, Date = new(2026, 8, 15), Series = "Invented League" },
    ];

    /// <summary>The places and points card's size and words, on top of what <see cref="PlacingsChart.Build"/> draws.</summary>
    public static ChartSpec PlacesLook(ChartSpec spec) => spec with { Width = 1100, Height = 400, Title = "Places and points, in two series", Source = Source };
```

- At the end of the racing cards in `Cards(...)`, after the `gap` card:

```csharp
            new("racing", "places-points", "Places and points", "The same invented kind of season in two series, built by one call, `PlacingsChart.Build`, and drawn by `<LumenPlacings>`: each series its own line, so a place is better or worse only than the previous race of the same series, the field after each place, and the league's points in a pane beneath, the open races' missing points gaps, never zeros.", true,
                PlacesLook(PlacingsChart.Build(Placings, new() { Style = theme == ChartTheme.Dark ? ChartStyle.Dark : ChartStyle.Light })!)) { Placings = Placings },
```

In `samples/Lumen.Gallery/Components/Pages/Sports.razor`, in the card branch before `else if (card.Beside is { } beside)`, add:

```razor
                            else if (card.Placings is { } placings)
                            {
                                @Branded(@<LumenPlacings Results="placings" Options="PlacesOptions" Adjust="SportsData.PlacesLook"/>)
                            }
```

and in its `@code`:

```csharp
    // The Lumen brand cascades no style, so the card asks for the theme's own; the other brands cascade theirs.
    private PlacingsOptions PlacesOptions => Look.Brand == BrandDemo.Lumen ? new() { Style = Look.Dark ? ChartStyle.Dark : ChartStyle.Light } : new();
```

(Keep the order of the branches so sparkline and paired cards still render as before. If `Source` is not a static member reachable from `PlacesLook`, use the expression the other cards use for their source line and say so.)

- [ ] **Step 4: Run everything**

Build (0 warnings), unit (all pass), baseline (three added rows only), start the gallery, `verify-api.ps1` (all pass; report the count), build and run the browser suite against the gallery (all pass; report the count; the Sports page's axe sweeps in light, dark and Midnight include the new card). Look at the card in light, dark and Midnight at 1400 and at a 375 phone; save screenshots outside the repository and list them. Stop the gallery.

- [ ] **Step 5: Commit**

```bash
git add tests/Lumen.Charts.Baseline/Program.cs samples/Lumen.Gallery/SportsData.cs samples/Lumen.Gallery/Components/Pages/Sports.razor tests/Lumen.Charts.Tests/Program.cs tests/Lumen.Charts.BrowserTests/Program.cs tests/verify-api.ps1
git commit -m "Hash the places and points chart and show it on the Sports & performance page" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Documentation, recipe and version

**Files:** `Directory.Build.props`, `README.md`, `integrations/claude-code/lumen-charts/SKILL.md`, `references/api.md`, `references/recipes-race-face.md`, `docs/VERIFICATION.md`.

- [ ] **Step 1:** `<Version>0.45.0</Version>`.
- [ ] **Step 2: README.** A `### Places and points` subsection (beside the other chart-building sections, near `### Planning a season`) with a C# example of `PlacingsChart.Build` and a razor example of `<LumenPlacings>` (`Results`, `Options`, `Adjust`, `Static`, `Empty`), and the rules in plain words: unplaced results dropped and `null` when none is placed; date order when every result has a date, else the given order; label precedence; one line per series (trimmed keys) compared only within the series; "/field" when above 0; points in a pane, gaps never zeros, no pane without points; the defaults (names, format, colours, 340 × 380/260, title, description with the best place); refusals. A `## 0.45.0 additions` section above `## 0.44.0 additions`. Update `## Supported behavior and limits` with one bullet.
- [ ] **Step 3: The skill.** `SKILL.md`: name `PlacingsChart.Build` and `<LumenPlacings>` where the Blazor components and the chart-building helpers are listed (keep the frontmatter description ≤ 1024 characters with no unquoted `: `; check its length as 0.44.0's Task 5 did). `references/api.md`: a "Places and points" section with the records, options (defaults) and the method; a bullet under the Blazor components. `references/recipes-race-face.md`: in "Position and points by race, recommended: two panes", add a ```` ```csharp ```` block first that builds the same chart with one call, using the recipe file's invented races (map them to `Placing` with `Field`, `Points`, a date or label) and Race Face's words as options (`PlaceName = "Pos/field"`, `PointsName = "Pts"`, `DateFormat = "dd-MM-yyyy"`, `Unlabelled = "R{0}"`, `PlaceColors`/`PointsColor` from the recipe's `raceFace` style), then `with { Title = "Position & points by race", YLabel = "Position" }`; keep the hand-built block after it as the explanation of what the builder does, and a ```` ```razor ```` line for `<LumenPlacings>`. The new csharp block must compile with the others in `tests/Lumen.Charts.Recipes` (`python check.py`, then `dotnet run -c Release`); give its variables names that do not collide with the file's others.
- [ ] **Step 4: Verification record.** A `0.45.0 (<date>)` paragraph at the top of `## Automated results` (what was added; the unit checks by subject; three baseline rows added and none moved; the HTTP and browser checks), the bullet counts updated with numbers you measured (unit, HTTP, browser gallery and WebAssembly host — the host shows no places card, so its count should be unchanged at 62; run it to confirm — baseline 388 rows, recipes), and a `0.45.0` paragraph at the top of `## Browser checks` for what you looked at.
- [ ] **Step 5:** Build (0 warnings), unit, recipe check; commit:

```bash
git add Directory.Build.props README.md integrations/claude-code/lumen-charts/SKILL.md integrations/claude-code/lumen-charts/references/api.md integrations/claude-code/lumen-charts/references/recipes-race-face.md docs/VERIFICATION.md
git commit -m "Document the places and points chart, its recipe and 0.45.0" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Release (the controller does this)

- [ ] Final whole-branch review, one fix wave, re-review; merge to `master` on the owner's word.
- [ ] On `master`: build, unit, baseline (three added rows; promote `reference/*.txt`; baseline README to v0.45.0, 388 rows), gallery + `verify-api.ps1` + browser suite, WebAssembly host + browser suite, recipes; correct `docs/VERIFICATION.md` counts.
- [ ] Pack the three packages, clear `~/.nuget/packages/lumen.charts*/0.45.0`, smoke-test (`PlacingsChart.Build` in a fresh console app; `LumenPlacings` type present).
- [ ] Scan the diff for real data; commit explicit paths; push; CI; tag `v0.45.0`; `gh release create` with the three `.nupkg` and honest notes.
- [ ] Package the skill from the tag; send it and the screenshots; message "RACEFACE RUNNING EXPANSION 2" with the version and how to replace `PositionPointsChart.razor`'s `Spec(...)` body and markup (map `ResultDto`/`ProgPoint` to `Placing` with `Series = SeriesBase(RaceName)`, options with its words and colours and `Style = RaceFaceChartStyle.Dark`, `Adjust` for title, `YLabel = "Position"` and `OnRaised`; note the mixed-dates order difference).
- [ ] Brain (`Release.Latest`, `Roadmap.Next` = 0.46.0 heatmap) and `HANDOVER.md`; remove the worktree and branch.
