# Event Planner (interactive, 0.44.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `<LumenPlanner>`, the interactive Blazor planner over 0.43.0's static `PlannerSvg`: zoom from the year to a month to a day by click and keys, step months and days, filter by region (as a tree), category, audience, status and relevance, draw at its box's width with the narrow layout below 640 px, and raise `EventSelected`, `DaySelected` and `ViewChanged`.

**Architecture:** A new Razor component `src/Lumen.Charts.Blazor/LumenPlanner.razor` built the way `LumenGraph.razor` is (inline `@code`, `IAsyncDisposable`, `lumen.js` imported as a module, `[JSInvokable]` callbacks, the existing `fit`/`unfit` width measurement). It holds the view, the reader's filter and the measured width, and draws `PlannerSvg.Render(spec with { Style, Filter, Width }, view, layout)` as markup. A new `attachPlanner`/`plannerDrawn` pair in `lumen.js` makes the drawing one roving tab stop over its cells (days of the year or a month, weekends of the phone year, events of a day), handles clicks, arrows, Enter/Space and Escape, and rings the focused cell; it changes only the live DOM, never `PlannerSvg.Render`'s output. The one library change is an unpainted hit cell under every day of the wide year, so a pointer can hit a weekday with nothing drawn on it and the script has a box to ring.

**Tech Stack:** C# / .NET 8 (`Lumen.Charts`, `Lumen.Charts.Blazor` Razor class library), plain ES module JavaScript (`wwwroot/lumen.js`), CSS (`wwwroot/lumen.css`), the executable unit harness `tests/Lumen.Charts.Tests` (`Test`/`Check`/`Reject`, `HtmlRenderer` with a `NoJs` runtime), the rendering-hash harness `tests/Lumen.Charts.Baseline`, `tests/verify-api.ps1`, the Playwright + axe suite `tests/Lumen.Charts.BrowserTests` (gallery on 5188, WebAssembly host on 5199).

**Spec:** `docs/superpowers/specs/2026-10-05-event-planner-design.md` — section 3 "Component (`Lumen.Charts.Blazor`)" and release 2 under "Releases". 0.43.0's plan, `docs/superpowers/plans/2026-10-05-event-planner-static.md`, built what this plan wraps.

## Global Constraints

- One feature per release: this plan is **0.44.0** only. The category heatmap (Race Face #16) is 0.45.0 and out of scope. View only: the planner never creates, moves or saves events.
- **Interactivity never changes the static SVG of a view.** The component's script changes only the live DOM (tab stops, the focus ring, the viewport's scroll role). `PlannerSvg.Render` output changes only by Task 1's hit cells.
- **No existing rendering moves except by design:** against `tests/Lumen.Charts.Baseline/reference/*.txt` (v0.43.0, 385 rows) exactly four rows change, in both finishes — `planner/year-light`, `planner/year-dark`, `planner/year-midnight`, `planner/gauteng` (each gains an unpainted `rect.lumen-cell` per day) — and the other 381 are identical. No row is added or removed.
- **Never colour alone**: a pressed filter chip is shown by weight, border and a check mark as well as colour, and says `aria-pressed`; the focused cell is ringed in the text colour (`currentColor`, which clears 4.5:1 on the background). Every drawn word still clears **4.5:1**.
- The component draws at its box's measured width, clamped to **320–4096** px; it switches to `PlannerLayout.Narrow` when that width is **below 640** px, else `Wide`. Until it is interactive (prerender, static rendering) it draws at `Spec.Width`, wide, scaled to fit.
- **Invented data only.** The gallery and WebAssembly host reuse `samples/Lumen.Gallery/PlannerData.cs` (invented organizers; real South African province names and 2027 public-holiday dates are public facts). Never open, copy from or stage `lumen-charts-race-face-brief.md`.
- Every new public member has an XML doc comment (`TreatWarningsAsErrors` + `GenerateDocumentationFile` fail the build otherwise), and the unit test "The packages carry their XML documentation…" must cover `LumenPlanner`'s parameters. Release build stays at **0 warnings**.
- Version **0.44.0** in `Directory.Build.props` (Task 5).
- Culture: every date the component writes or parses uses `CultureInfo.InvariantCulture` (`yyyy-MM-dd` between C# and the script).
- Repository rules (HANDOVER.md): kill only `Lumen.Gallery.exe` by image name (`taskkill //IM Lumen.Gallery.exe //F`), any other process by PID; write patch scripts with the Write tool, not Bash heredocs; prefer the Edit tool where CRLF matters; stage explicit paths only, never the Race Face brief; delete any `.playwright-mcp` folder you create; save screenshots outside the repository. `tests/Lumen.Charts.BrowserTests` and `samples/Lumen.Wasm` are outside `Lumen.Charts.slnx`: build them explicitly before running them with `--no-build`.

## Review Focus

1. **The host replaces `Spec` with another period while a month or day is open** (next season loaded) — the planner returns to the whole period instead of throwing; a view the *host* sets outside the period is still refused with `ArgumentException`, as `PlannerSvg.Render` refuses it. Test in Task 2.
2. **A day outside the period, drawn in a month's grid** (a period starting 5 March shows 1–4 March as dates only) — clicking or pressing Enter on it keeps the month and says in the status line that the day is outside the planner; it never throws. Test in Task 2.
3. **The box is resized across 640 px while a month is open** (a tablet rotated) — the layout switches between the month grid and the agenda, the month stays, the toolbar's words stay. Test in Task 2.
4. **Filters that hide every event, and a new `Spec` that no longer offers a chosen value** — the drawing still renders (empty days), Clear filters restores everything, and a chosen value the new spec no longer offers is dropped (so no invisible chip keeps filtering) unless the host's own `Spec.Filter` names it. Test in Task 2.
5. **A weekend across a month's end on a phone** (Saturday 31 July and Sunday 1 August 2027 stand in both July's and August's bars with the same `data-weekend`) — the bar the reader clicks decides the month opened. Tests in Task 2 (C# side) and Task 4 (in the browser).

---

## File Structure

| File | Responsibility |
|---|---|
| `src/Lumen.Charts/PlannerSvg.cs` (modify, `Year()`) | Writes an unpainted `rect.lumen-cell` (`fill='none' stroke='none' pointer-events='all'`) first in every wide-year day group. |
| `src/Lumen.Charts/Lumen.Charts.csproj` (modify) | `InternalsVisibleTo Lumen.Charts.Blazor`, so the component can name an event as the drawing does (`PlannerCalendar.Name`). |
| `src/Lumen.Charts.Blazor/LumenPlanner.razor` (create) | The component: parameters, view state, toolbar (Back, Previous, where, Next), filter chips, viewport, keys hint, status line; `[JSInvokable]` `Fit`, `Open`, `Back`, `SelectEvent`. |
| `src/Lumen.Charts.Blazor/wwwroot/lumen.js` (modify, append) | `attachPlanner(root, dotnet)` and `plannerDrawn(root, focus)`. `detach` already removes what `attachPlanner` stores. |
| `src/Lumen.Charts.Blazor/wwwroot/lumen.css` (modify) | Planner box, toolbar, chips (pressed state not by colour alone), drawing minimum width 320 px. |
| `tests/Lumen.Charts.Tests/Program.cs` (modify) | Task 1's hit-cell test; Task 2's component tests and helper; `typeof(LumenPlanner)` in the documentation test. |
| `samples/Lumen.Gallery/Components/Pages/Home.razor`, `wwwroot/app.css`, `PlannerData.cs` (modify) | The `#planner` section shows `<LumenPlanner>` and what was selected; the three static drawings and their container queries go; `Season` gets a doc comment. |
| `samples/Lumen.Wasm/Lumen.Wasm.csproj`, `Host.razor` (modify) | Links `PlannerData.cs` and shows the same planner, so the browser suite runs its planner checks on both hosts. |
| `tests/Lumen.Charts.BrowserTests/Program.cs` (modify) | Replaces 0.43.0's eight static-planner checks with the component's checks. |
| `tests/verify-api.ps1` (modify) | The home page prerenders the planner component. |
| `README.md`, `docs/VERIFICATION.md`, `Directory.Build.props`, `integrations/claude-code/lumen-charts/SKILL.md`, `references/api.md`, `references/recipes-race-face.md` (modify) | Documentation, recipe, version. |

Build and test commands used throughout (Git Bash, repository root `D:/CHATGPT/.NET GRAPH API`, or the worktree root):

- Build: `taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | tail -3`
- Unit tests: `dotnet run --project tests/Lumen.Charts.Tests -c Release 2>&1 | grep -E "FAIL|Planner|passed"`
- Baseline: `cd tests/Lumen.Charts.Baseline && dotnet run -c Release && dotnet run -c Release -- classic && diff <(tr -d '\r' < reference/refined.txt) <(tr -d '\r' < baseline.txt); diff <(tr -d '\r' < reference/classic.txt) <(tr -d '\r' < classic.txt); rm baseline.txt classic.txt; cd ../..`
- Gallery: `dotnet run --project samples/Lumen.Gallery -c Release --no-build --urls http://localhost:5188` (background), then `pwsh -File tests/verify-api.ps1 -BaseUrl http://localhost:5188` and `dotnet build tests/Lumen.Charts.BrowserTests -c Release && dotnet run --project tests/Lumen.Charts.BrowserTests -c Release --no-build`
- WebAssembly host: `dotnet build samples/Lumen.Wasm -c Release && dotnet run --project samples/Lumen.Wasm -c Release --no-build --urls http://localhost:5199` (background), then `dotnet run --project tests/Lumen.Charts.BrowserTests -c Release --no-build -- http://localhost:5199`; stop it by PID (`netstat -ano | grep 5199`, `taskkill //PID <pid> //F`).

---

### Task 1: Hit cells in the wide year, and the component's access to event names

**Files:**
- Modify: `src/Lumen.Charts/PlannerSvg.cs` (in `Year()`, the day loop that writes `<g class='lumen-day' role='group' data-day=…>`, about line 101)
- Modify: `src/Lumen.Charts/Lumen.Charts.csproj` (beside `<InternalsVisibleTo Include="Lumen.Charts.Tests" />`)
- Test: `tests/Lumen.Charts.Tests/Program.cs` (append beside the other `Test("Planner: …")` entries, before the final summary lines)

**Interfaces:**
- Consumes: nothing new.
- Produces: in the wide year only, every `g.lumen-day[data-day]` starts with `<rect class='lumen-cell' x y width height fill='none' stroke='none' pointer-events='all'/>` covering the day's column and row (`width = col`, `height = RowH - 2`). `Lumen.Charts` internals (notably `PlannerCalendar.Name(PlannerSpec, PlannerEvent)`) are visible to `Lumen.Charts.Blazor`.

- [ ] **Step 1: Write the failing test**

```csharp
Test("Planner: every day of the wide year carries an unpainted cell a pointer can hit, and no other view does",()=>{
    var year=PlannerSvg.Render(PlanYear(),PlannerView.WholePeriod);
    Check(Regex.Matches(year,"<g class='lumen-day' role='group' data-day='[0-9-]+' aria-label='[^']*'><rect class='lumen-cell' [^>]*fill='none' stroke='none' pointer-events='all'/>").Count==365,"one cell first in every day");
    Check(Regex.Matches(year,"class='lumen-cell'").Count==365,"no other cells");
    foreach(var view in new[]{PlannerView.Month(2027,3),PlannerView.Day(new(2027,3,13))})
        Check(!PlannerSvg.Render(PlanYear(),view).Contains("lumen-cell"),$"none in {view.Zoom}");
    Check(!PlannerSvg.Render(PlanYear() with{Width=340},PlannerView.WholePeriod,PlannerLayout.Narrow).Contains("lumen-cell"),"none in the narrow year");
});
```

- [ ] **Step 2: Run it to see it fail**

Run the unit tests. Expected: `FAIL Planner: every day of the wide year carries an unpainted cell…: one cell first in every day`.

- [ ] **Step 3: Write the cell**

In `PlannerSvg.Year()`, directly after the line that adds `<g class='lumen-day' role='group' data-day='{Iso(d)}' aria-label='{E(plan.DayName(d))}'>`, add:

```csharp
                // An unpainted cell the size of the day, so a pointer can hit a weekday that has nothing drawn on it; the
                // interactive planner opens the month from it and rings it when it has focus. It draws nothing.
                w.Add($"<rect class='lumen-cell' x='{N(x)}' y='{N(y)}' width='{N(col)}' height='{N(RowH - 2)}' fill='none' stroke='none' pointer-events='all'/>");
```

In `src/Lumen.Charts/Lumen.Charts.csproj`, beside the existing `<InternalsVisibleTo Include="Lumen.Charts.Tests" />`, add:

```xml
    <InternalsVisibleTo Include="Lumen.Charts.Blazor" />
```

- [ ] **Step 4: Run the unit tests and the baseline**

Build, then run the unit tests. Expected: the new test passes and every other test still passes (the contrast tests skip unpainted shapes; if one counts `fill='none'` shapes as drawn, make it skip `class='lumen-cell'`, and say so in the report).

Run the baseline in both finishes. Expected: `diff` prints exactly four changed rows in each finish — `planner/year-light`, `planner/year-dark`, `planner/year-midnight`, `planner/gauteng` — and nothing else. Do not touch `reference/*.txt` (the release promotes them).

- [ ] **Step 5: Commit**

```bash
git add src/Lumen.Charts/PlannerSvg.cs src/Lumen.Charts/Lumen.Charts.csproj tests/Lumen.Charts.Tests/Program.cs
git commit -m "Give every day of a planner's year an unpainted cell a pointer can hit" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The `<LumenPlanner>` component

**Files:**
- Create: `src/Lumen.Charts.Blazor/LumenPlanner.razor`
- Modify: `tests/Lumen.Charts.Tests/Program.cs` (new helpers and tests beside the planner tests; add `typeof(LumenPlanner)` to the documentation test's `new[]{typeof(LumenChart),typeof(LumenGraph),typeof(LumenBrand)}`, about line 3974)

**Interfaces:**
- Consumes: Task 1 (`PlannerCalendar.Name` visible to the component). From 0.43.0: `PlannerSpec`, `PlannerView` (`WholePeriod`, `Month(int,int)`, `Day(DateOnly)`; a month's `Date` is its first day), `PlannerZoom`, `PlannerLayout`, `PlannerFilter` (all lists default `[]`), `PlannerEvent`, `PlannerStatus`, `PlannerRelevance`, `PlannerSvg.Render(spec, view, layout)` (throws `ArgumentException` for a view outside the period).
- Produces (Task 3's script and the docs rely on these exact names):
  - `public partial class LumenPlanner` (namespace `Lumen.Charts.Blazor`) with parameters `Spec` (`PlannerSpec`, `EditorRequired`), `View` (`PlannerView`, default `PlannerView.WholePeriod`), `ViewChanged` (`EventCallback<PlannerView>`), `EventSelected` (`EventCallback<PlannerEvent>`), `DaySelected` (`EventCallback<DateOnly>`), `ShowFilters` (`bool`, default `true`), cascading `CascadingStyle` (`ChartStyle?`).
  - `[JSInvokable] Task Fit(int width)`, `[JSInvokable] Task Open(string date, bool later)`, `[JSInvokable] Task Back(string? date)`, `[JSInvokable] Task SelectEvent(string id)`.
  - Markup contract: root `<div class="lumen-planner-box" data-zoom="year|month|day" data-layout="wide|narrow">`, children in order `div.lumen-planner-bar` (buttons Back, Previous, `span.lumen-planner-where`, Next), optional `div.lumen-planner-filters`, `div.lumen-viewport` (the SVG), `span.lumen-keys[hidden]`, `span.lumen-status[role=status]`.
  - Script calls made from `OnAfterRenderAsync`: `attachPlanner(root, reference)` and `fit(root, reference)` once; `plannerDrawn(root, focus)` after every new drawing, `focus` a `yyyy-MM-dd` string or `null`.

- [ ] **Step 1: Write the test helpers and failing tests**

Add beside the planner tests in `tests/Lumen.Charts.Tests/Program.cs` (the file already has `using System.Reflection`, `System.Text.RegularExpressions`, the Blazor and DI namespaces, and the `NoJs` runtime; add any `using` the compiler asks for):

```csharp
// Renders a LumenPlanner under a cascaded style, lets a test act on it, and returns its markup once the renderer settles.
// The act receives the component and a function that returns the markup as it stands.
string PlanComponent(PlannerSpec spec,Func<LumenPlanner,Func<Task<string>>,Task>? act=null,Dictionary<string,object?>? more=null,ChartStyle? style=null)
{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        LumenPlanner? planner=null;
        RenderFragment content=b=>{
            b.OpenComponent<LumenPlanner>(0);b.AddAttribute(1,"Spec",spec);
            var i=2;foreach(var (name,value) in more??new())b.AddAttribute(i++,name,value);
            b.AddComponentReferenceCapture(100,c=>planner=(LumenPlanner)c);b.CloseComponent();
        };
        return renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<CascadingValue<ChartStyle>>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Value",style??ChartStyle.Light},{"ChildContent",content}}));
            if(act is not null)await act(planner!,async()=>{await root.QuiescenceTask;return root.ToHtmlString();});
            await root.QuiescenceTask;
            return root.ToHtmlString();
        }).GetAwaiter().GetResult();
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
}
bool PlanDisabled(string html,string label)=>Regex.IsMatch(html,$"<button[^>]*\\bdisabled\\b[^>]*aria-label=\"{Regex.Escape(label)}\"");
bool PlanEnabled(string html,string label)=>Regex.IsMatch(html,$"<button(?![^>]*\\bdisabled\\b)[^>]*aria-label=\"{Regex.Escape(label)}\"");
Task PlanStep(LumenPlanner p,int by)=>(Task)typeof(LumenPlanner).GetMethod("Step",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(p,[by])!;
void PlanChoose(LumenPlanner p,string group,string value)=>typeof(LumenPlanner).GetMethod("Choose",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(p,[group,value]);
void PlanClear(LumenPlanner p)=>typeof(LumenPlanner).GetMethod("ClearFilters",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(p,null);
string Where(string text)=>$"<span class=\"lumen-planner-where\">{text}</span>";

Test("LumenPlanner: prerender draws the year with its toolbar, filters, keys and status, and calls no script",()=>{
    var html=PlanComponent(PlanYear());   // NoJs throws if the component calls the script while prerendering
    Check(html.Contains("class=\"lumen-planner-box\" data-zoom=\"year\" data-layout=\"wide\""),"root");
    Check(html.Contains("class='lumen-svg lumen-planner'")&&html.Contains("viewBox='0 0 1100 "),"the year at the spec's width");
    Check(html.Contains(Where("2027")),"where");
    Check(PlanDisabled(html,"Back")&&PlanDisabled(html,"Previous")&&PlanDisabled(html,"Next"),"nothing to go back or step to in the year");
    Check(html.Contains("<span class=\"lumen-keys\" hidden>Arrow keys move between days, up and down by month; Enter or Space opens the month.</span>"),"keys");
    Check(html.Contains("<span class=\"lumen-status\" role=\"status\"></span>"),"status");
    foreach(var part in new[]{"<legend>Region</legend>","aria-label=\"Gauteng, in South Africa\"","aria-label=\"Western Cape, in South Africa\"",">South Africa</button>",
        "<legend>Category</legend>",">XCO</button>",">Stage</button>","<legend>Audience</legend>",">Kids</button>",">Open</button>",
        "<legend>Status</legend>",">Confirmed</button>",">Provisional</button>","<legend>Relevance</legend>",">Clash</button>",">Other</button>"})
        Check(html.Contains(part),part);
    Check(Regex.IsMatch(html,"<button type=\"button\" disabled>Clear filters</button>"),"nothing to clear");
});
Test("LumenPlanner: a day of the year opens its month, a day of the month opens the day and raises DaySelected, and Back steps out",()=>{
    var views=new List<PlannerView>();var days=new List<DateOnly>();var receiver=new object();
    var more=new Dictionary<string,object?>{{"ViewChanged",EventCallback.Factory.Create<PlannerView>(receiver,v=>views.Add(v))},{"DaySelected",EventCallback.Factory.Create<DateOnly>(receiver,d=>days.Add(d))}};
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.Open("2027-03-10",false);
        var month=await html();
        Check(month.Contains("data-zoom=\"month\"")&&month.Contains(Where("March 2027"))&&month.Contains("Showing March 2027"),"month");
        Check(PlanEnabled(month,"Back to 2027")&&PlanEnabled(month,"Previous month")&&PlanEnabled(month,"Next month"),"toolbar in a month");
        Check(month.Contains("Arrow keys move between days; Enter or Space opens the day; Escape goes back to the year."),"month keys");
        await p.Open("2027-03-13",false);
        var day=await html();
        Check(day.Contains("data-zoom=\"day\"")&&day.Contains(Where("Saturday 13 March 2027"))&&PlanEnabled(day,"Back to March 2027")&&PlanEnabled(day,"Next day"),"day");
        await p.Open("2027-03-14",false);   // a day view opens nothing
        Check((await html()).Contains(Where("Saturday 13 March 2027")),"a day stays");
        await p.Back(null);
        Check((await html()).Contains(Where("March 2027")),"back to the month");
        await p.Back("2027-03-13");
        Check((await html()).Contains("data-zoom=\"year\""),"back to the year");
    },more);
    Check(views.SequenceEqual(new[]{PlannerView.Month(2027,3),PlannerView.Day(new(2027,3,13)),PlannerView.Month(2027,3),PlannerView.WholePeriod}),string.Join(", ",views));
    Check(days.SequenceEqual(new[]{new DateOnly(2027,3,13)}),"DaySelected once");
});
Test("LumenPlanner: Previous and Next step a month or a day and stop at the period's edges",()=>{
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.Open("2027-01-06",false);
        var january=await html();
        Check(PlanDisabled(january,"Previous month")&&PlanEnabled(january,"Next month"),"January");
        await PlanStep(p,1);
        Check((await html()).Contains(Where("February 2027")),"February");
        await PlanStep(p,-1);await PlanStep(p,-1);   // the second step is refused at the edge
        Check((await html()).Contains(Where("January 2027")),"stays in January");
    });
    PlanComponent(PlanYear(),async(p,html)=>{
        var last=await html();
        Check(PlanDisabled(last,"Next day")&&PlanEnabled(last,"Previous day")&&last.Contains(Where("Friday 31 December 2027")),"the last day");
        await PlanStep(p,-1);
        Check((await html()).Contains(Where("Thursday 30 December 2027")),"the day before");
    },new(){{"View",PlannerView.Day(new(2027,12,31))}});
    Check(PlanDisabled(PlanComponent(PlanYear(),null,new(){{"View",PlannerView.Month(2027,12)}}),"Next month"),"December");
});
Test("LumenPlanner: a day outside the period stays shut, and a new period returns the planner to its whole period",()=>{
    PlanComponent(PlanYear(s=>s with{From=new(2027,3,5)}),async(p,html)=>{
        await p.Open("2027-03-10",false);
        await p.Open("2027-03-01",false);
        var shut=await html();
        Check(shut.Contains("data-zoom=\"month\"")&&shut.Contains("Monday 1 March 2027 is outside the planner"),"outside");
        await p.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",PlanYear(s=>s with{From=new(2028,1,1),To=new(2028,12,31),Periods=[],Events=[]})}}));
        var next=await html();
        Check(next.Contains("data-zoom=\"year\"")&&next.Contains(Where("2028")),"a new period starts at the whole period");
    });
    Exception? caught=null;
    try{PlanComponent(PlanYear(),null,new(){{"View",PlannerView.Month(2028,1)}});}catch(Exception e){caught=e;}
    while(caught is not null and not ArgumentException&&caught.InnerException is not null)caught=caught.InnerException;
    Check(caught is ArgumentException,caught?.GetType().Name??"a host's view outside the period was drawn");
});
Test("LumenPlanner: the box's width sets the drawing's width and below 640 pixels the narrow layout, keeping the view",()=>{
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.Open("2027-03-10",false);
        await p.Fit(500);
        var narrow=await html();
        Check(narrow.Contains("data-zoom=\"month\" data-layout=\"narrow\"")&&narrow.Contains("viewBox='0 0 500 ")&&narrow.Contains(Where("March 2027")),"the agenda");
        Check(narrow.Contains("class='lumen-agenda-day'"),"agenda drawn");
        await p.Fit(100);
        Check((await html()).Contains("viewBox='0 0 320 "),"never narrower than 320");
        await p.Fit(900);
        var wide=await html();
        Check(wide.Contains("data-layout=\"wide\"")&&wide.Contains("viewBox='0 0 900 ")&&wide.Contains(Where("March 2027")),"the grid again");
    });
});
Test("LumenPlanner: on a phone a weekend across a month's end opens the month of the bar it stands in",()=>{
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.Fit(400);
        Check((await html()).Contains("Arrow keys move between weekends, up and down by month; Enter or Space opens the month."),"phone keys");
        await p.Open("2027-07-31",true);
        Check((await html()).Contains(Where("August 2027")),"the later bar opens August");
        await p.Back("2027-08-01");
        await p.Open("2027-07-31",false);
        Check((await html()).Contains(Where("July 2027")),"the earlier bar opens July");
    });
});
Test("LumenPlanner: selecting an event raises EventSelected and names it in the status line",()=>{
    var picked=new List<PlannerEvent>();
    var html=PlanComponent(PlanYear(),async(p,_)=>{await p.SelectEvent("e1");await p.SelectEvent("nobody");},
        new(){{"EventSelected",EventCallback.Factory.Create<PlannerEvent>(new object(),e=>picked.Add(e))}});
    Check(picked.Count==1&&picked[0].Id=="e1","EventSelected once, for a known id only");
    Check(html.Contains("Selected Hilltop XCO, Saturday 13 March 2027, Gauteng, XCO, Kids, clash"),"status");
});
Test("LumenPlanner: a host's view, a cascaded style and hidden filters are honoured",()=>{
    var html=PlanComponent(PlanYear(),null,new(){{"View",PlannerView.Month(2027,3)},{"ShowFilters",false}},ChartStyle.Midnight);
    Check(html.Contains("data-zoom=\"month\"")&&html.Contains(Where("March 2027")),"host view");
    Check(!html.Contains("lumen-planner-filters"),"no filters");
    Check(html.Contains(ChartStyle.Midnight.Background),"cascaded style");
});
Test("LumenPlanner: a chip filters the drawing, says it is pressed, and Clear filters restores everything",()=>{
    var html=PlanComponent(PlanYear(),async(p,html)=>{
        PlanChoose(p,"Region","ZA-GP");
        var gauteng=await html();
        Check(gauteng.Contains("data-event='e1'")&&!gauteng.Contains("data-event='e2'"),"Western Cape's event drops out");
        Check(gauteng.Contains("aria-pressed=\"true\" aria-label=\"Gauteng, in South Africa\""),"pressed");
        Check(gauteng.Contains("Filter Gauteng on")&&Regex.IsMatch(gauteng,"<button type=\"button\">Clear filters</button>"),"status and clear");
        PlanChoose(p,"Status","Provisional");
        Check(!(await html()).Contains("data-event="),"filtered to nothing still draws the year");
        PlanClear(p);
    });
    Check(html.Contains("data-event='e2'")&&html.Contains("Filters cleared")&&!html.Contains("aria-pressed=\"true\""),"cleared");
});
Test("LumenPlanner: the spec's filter is where the reader starts, and a new spec drops chosen values it no longer offers",()=>{
    var start=PlanYear(s=>s with{Filter=new(){Categories=["XCO","Track"]}});
    var html=PlanComponent(start,async(p,html)=>{
        var first=await html();
        Check(Regex.IsMatch(first,"aria-pressed=\"true\"[^>]*>XCO</button>")&&!first.Contains("data-event='e2'"),"starts filtered to XCO");
        PlanChoose(p,"Region","ZA-WC");
        // The next spec drops the Western Cape and its event (an event in an unknown region would be refused).
        await p.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",start with{Regions=[new("ZA","South Africa"),new("ZA-GP","Gauteng","ZA")],Events=[start.Events[0]]}}}));
    });
    Check(html.Contains("data-event='e1'")&&!html.Contains("Western Cape"),"Western Cape dropped from the filter with the region");
    Check(Regex.IsMatch(html,"<button type=\"button\">Clear filters</button>"),"XCO and the host's Track stay chosen");
});
```

Then add `typeof(LumenPlanner)` to the documentation test's type list:

```csharp
        .Concat(new[]{typeof(LumenChart),typeof(LumenGraph),typeof(LumenBrand),typeof(LumenPlanner)}.SelectMany(t=>t.GetProperties(declared)
```

- [ ] **Step 2: Run them to see them fail**

Build. Expected: the build fails with `CS0246: The type or namespace name 'LumenPlanner' could not be found`.

- [ ] **Step 3: Write the component**

Create `src/Lumen.Charts.Blazor/LumenPlanner.razor`:

```razor
@implements IAsyncDisposable
@using System.Globalization
@inject IJSRuntime JS
<div class="lumen-planner-box" @ref="root" data-zoom="@ZoomWord" data-layout="@(Narrow ? "narrow" : "wide")">
    <div class="lumen-planner-bar">
        <button type="button" @onclick="BackButton" disabled="@(view.Zoom == PlannerZoom.Year)" aria-label="@BackLabel">Back</button>
        <button type="button" @onclick="() => Step(-1)" disabled="@(Stepped(-1) is null)" aria-label="@StepLabel(-1)">Previous</button>
        <span class="lumen-planner-where">@Where</span>
        <button type="button" @onclick="() => Step(1)" disabled="@(Stepped(1) is null)" aria-label="@StepLabel(1)">Next</button>
    </div>
    @if (ShowFilters && Groups is { Count: > 0 } groups)
    {
        <div class="lumen-planner-filters" role="group" aria-label="Filters">
            @foreach (var group in groups)
            {
                <fieldset>
                    <legend>@group.Name</legend>
                    @foreach (var chip in group.Chips)
                    {
                        <button type="button" class="@(chip.Depth > 0 ? "lumen-planner-sub" : null)" aria-pressed="@(chip.On ? "true" : "false")" aria-label="@chip.Label" @onclick="() => Choose(group.Name, chip.Value)">@chip.Text</button>
                    }
                </fieldset>
            }
            <button type="button" @onclick="ClearFilters" disabled="@(!Filtered)">Clear filters</button>
        </div>
    }
    <div class="lumen-viewport">@((MarkupString)svg)</div>
    <span class="lumen-keys" hidden>@Keys</span>
    <span class="lumen-status" role="status">@status</span>
</div>
@code {
    /// <summary>The planner to draw: its period, regions, holidays and events. Its <see cref="PlannerSpec.Filter"/> is the filter
    /// the reader starts with; its <see cref="PlannerSpec.Width"/> is used only until the component has measured its box, and in
    /// static rendering. A new spec keeps the reader's view while it lies in the new period, and the whole period otherwise.</summary>
    [Parameter, EditorRequired] public PlannerSpec Spec { get; set; } = new();
    /// <summary>The view to show: the whole period, a month or a day; the whole period by default. The component changes its
    /// view as the reader zooms and steps, and raises <see cref="ViewChanged"/>; bind it with <c>@bind-View</c> to follow it.
    /// A view outside the period is refused with an <see cref="ArgumentException"/>, as <see cref="PlannerSvg.Render"/> refuses it.</summary>
    [Parameter] public PlannerView View { get; set; } = PlannerView.WholePeriod;
    /// <summary>Raised with the new view whenever the reader opens a month or a day, goes back, or steps to another month or day.
    /// Needs an interactive render mode.</summary>
    [Parameter] public EventCallback<PlannerView> ViewChanged { get; set; }
    /// <summary>Raised with the event when the reader clicks one, or chooses one in a day with Enter or Space. Needs an
    /// interactive render mode.</summary>
    [Parameter] public EventCallback<PlannerEvent> EventSelected { get; set; }
    /// <summary>Raised with the day when the reader opens a day from a month, by a click or with Enter or Space. Needs an
    /// interactive render mode.</summary>
    [Parameter] public EventCallback<DateOnly> DaySelected { get; set; }
    /// <summary>Shows the filter chips: the regions as a tree (a country, then its provinces), and every category, audience,
    /// status and relevance the events use, each group shown when it offers two or more. True by default.</summary>
    [Parameter] public bool ShowFilters { get; set; } = true;
    /// <summary>A host-wide style, usually cascaded by <see cref="LumenBrand"/>. A style on the spec itself wins.</summary>
    [CascadingParameter] public ChartStyle? CascadingStyle { get; set; }

    private sealed record Chip(string Value, string Text, string? Label, int Depth, bool On);
    private sealed record Group(string Name, IReadOnlyList<Chip> Chips);
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    // A planner is drawn from 320 to 4096 pixels wide, and in its narrow layout below 640.
    private const int Narrowest = 320, Widest = 4096, NarrowBelow = 640;
    private PlannerView view = PlannerView.WholePeriod, given = PlannerView.WholePeriod;
    private PlannerSpec? source;
    private PlannerFilter filter = new();
    private string svg = "", status = "";
    private string? told;
    // The date whose cell should take the tab stop after the next drawing; sent once, then forgotten.
    private DateOnly? focus;
    private int? fitted;
    private ElementReference root;
    private IJSObjectReference? module;
    private DotNetObjectReference<LumenPlanner>? reference;

    private bool Narrow => fitted is < NarrowBelow;
    private string ZoomWord => view.Zoom switch { PlannerZoom.Month => "month", PlannerZoom.Day => "day", _ => "year" };
    private string Period => Spec.From == new DateOnly(Spec.From.Year, 1, 1) && Spec.To == new DateOnly(Spec.From.Year, 12, 31)
        ? Spec.From.Year.ToString(Invariant)
        : $"{Spec.From.ToString("d MMMM yyyy", Invariant)} to {Spec.To.ToString("d MMMM yyyy", Invariant)}";
    private string Where => view.Zoom switch
    {
        PlannerZoom.Month => view.Date.ToString("MMMM yyyy", Invariant),
        PlannerZoom.Day => view.Date.ToString("dddd d MMMM yyyy", Invariant),
        _ => Period,
    };
    private string BackLabel => view.Zoom switch
    {
        PlannerZoom.Day => $"Back to {view.Date.ToString("MMMM yyyy", Invariant)}",
        PlannerZoom.Month => $"Back to {Period}",
        _ => "Back",
    };
    private string StepLabel(int by) => (view.Zoom, by < 0) switch
    {
        (PlannerZoom.Month, true) => "Previous month",
        (PlannerZoom.Month, false) => "Next month",
        (PlannerZoom.Day, true) => "Previous day",
        (PlannerZoom.Day, false) => "Next day",
        (_, true) => "Previous",
        _ => "Next",
    };
    private string Keys => (view.Zoom, Narrow) switch
    {
        (PlannerZoom.Year, false) => "Arrow keys move between days, up and down by month; Enter or Space opens the month.",
        (PlannerZoom.Year, true) => "Arrow keys move between weekends, up and down by month; Enter or Space opens the month.",
        (PlannerZoom.Month, _) => "Arrow keys move between days; Enter or Space opens the day; Escape goes back to the year.",
        _ => "Arrow keys move between events; Enter or Space selects one; Escape goes back to the month.",
    };

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(Spec, source))
        {
            filter = source is null ? Spec.Filter ?? new() : Offered(filter);
            source = Spec;
            if (!Inside(view)) { view = PlannerView.WholePeriod; focus = null; }
        }
        // The host's view is taken when the host changes it; the reader's own zooming is kept across the host's renders.
        if (View != given)
        {
            given = View;
            if (View != view) { view = View; focus = View.Zoom == PlannerZoom.Year ? null : View.Date; }
        }
        Render();
    }

    private void Render()
    {
        var spec = Spec with { Style = Spec.Style ?? CascadingStyle, Filter = filter, Width = fitted ?? Spec.Width };
        svg = PlannerSvg.Render(spec, view, Narrow ? PlannerLayout.Narrow : PlannerLayout.Wide);
    }

    private bool Inside(PlannerView v) => v.Zoom switch
    {
        PlannerZoom.Month => v.Date <= Spec.To && v.Date.AddMonths(1).AddDays(-1) >= Spec.From,
        PlannerZoom.Day => v.Date >= Spec.From && v.Date <= Spec.To,
        _ => true,
    };

    private PlannerView? Stepped(int by)
    {
        PlannerView? next = view.Zoom switch
        {
            PlannerZoom.Month => PlannerView.Month(view.Date.AddMonths(by).Year, view.Date.AddMonths(by).Month),
            PlannerZoom.Day => PlannerView.Day(view.Date.AddDays(by)),
            _ => null,
        };
        return next is { } v && Inside(v) ? v : null;
    }

    private async Task Go(PlannerView next, DateOnly? at)
    {
        view = next; focus = at; status = $"Showing {Where}";
        Render();
        await ViewChanged.InvokeAsync(next);
        StateHasChanged();
    }

    private async Task Step(int by)
    {
        if (Stepped(by) is { } next) await Go(next, next.Date);
    }

    private Task BackButton() => Out(view.Date);

    private async Task Out(DateOnly at)
    {
        if (view.Zoom == PlannerZoom.Day) await Go(PlannerView.Month(view.Date.Year, view.Date.Month), view.Date);
        else if (view.Zoom == PlannerZoom.Month) await Go(PlannerView.WholePeriod, at);
    }

    /// <summary>Called by the component's script when the reader opens a cell: a day of the year (or, on a phone, a weekend)
    /// opens its month, and a day of a month opens the day and raises <see cref="DaySelected"/>. <paramref name="date"/> is
    /// <c>yyyy-MM-dd</c>; <paramref name="later"/> is true for a weekend's second bar on a phone, which opens the next month.
    /// A day outside the period stays shut and is said so in the status line.</summary>
    [JSInvokable] public Task Open(string date, bool later) => InvokeAsync(async () =>
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out var day)) return;
        if (view.Zoom == PlannerZoom.Year)
        {
            var month = later ? new DateOnly(day.Year, day.Month, 1).AddMonths(1) : day;
            var next = PlannerView.Month(month.Year, month.Month);
            if (Inside(next)) await Go(next, later ? month : day);
        }
        else if (view.Zoom == PlannerZoom.Month)
        {
            if (!Inside(PlannerView.Day(day))) { status = $"{day.ToString("dddd d MMMM yyyy", Invariant)} is outside the planner"; StateHasChanged(); return; }
            await Go(PlannerView.Day(day), day);
            await DaySelected.InvokeAsync(day);
        }
    });

    /// <summary>Called by the component's script when the reader presses Escape: a day goes back to its month, and a month to
    /// the whole period with the tab stop on <paramref name="date"/> (<c>yyyy-MM-dd</c>, or null for the view's own date).</summary>
    [JSInvokable] public Task Back(string? date) => InvokeAsync(async () =>
        await Out(DateOnly.TryParseExact(date, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out var at) ? at : view.Date));

    /// <summary>Called by the component's script when the reader selects an event by its <see cref="PlannerEvent.Id"/>. It names
    /// the event in the status line and raises <see cref="EventSelected"/>; an unknown id does nothing.</summary>
    [JSInvokable] public Task SelectEvent(string id) => InvokeAsync(async () =>
    {
        var e = Spec.Events.FirstOrDefault(x => x.Id == id);
        if (e is null) return;
        status = $"Selected {PlannerCalendar.Name(Spec, e)}";
        await EventSelected.InvokeAsync(e);
        StateHasChanged();
    });

    /// <summary>Called by the component's script with the width in whole pixels that the planner's box gives it. The planner is
    /// redrawn at that width, from 320 to 4096 pixels, in its narrow layout below 640.</summary>
    [JSInvokable] public Task Fit(int width) => InvokeAsync(() =>
    {
        var next = Math.Clamp(width, Narrowest, Widest);
        if (next == fitted) return;
        fitted = next;
        Render();
        StateHasChanged();
    });

    // The filter chips: regions as a tree, then each other dimension the events use, a group shown when it offers two or more.
    private IReadOnlyList<Group> Groups
    {
        get
        {
            var groups = new List<Group>();
            var regions = new List<Chip>();
            var codes = Spec.Regions.Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
            void Add(PlannerRegion region, int depth, string? parent)
            {
                regions.Add(new(region.Code, region.Name, parent is null ? null : $"{region.Name}, in {parent}", depth, filter.Regions.Contains(region.Code)));
                foreach (var child in Spec.Regions.Where(c => c.Parent == region.Code)) Add(child, depth + 1, region.Name);
            }
            foreach (var region in Spec.Regions.Where(r => r.Parent is null || !codes.Contains(r.Parent))) Add(region, 0, null);
            if (regions.Count > 1) groups.Add(new("Region", regions));
            void Words(string name, IEnumerable<string?> values, IReadOnlyList<string> on)
            {
                var distinct = values.OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (distinct.Length > 1) groups.Add(new(name, distinct.Select(v => new Chip(v, v, null, 0, on.Contains(v))).ToArray()));
            }
            Words("Category", Spec.Events.Select(e => e.Category), filter.Categories);
            Words("Audience", Spec.Events.Select(e => e.Audience), filter.Audiences);
            var statuses = Spec.Events.Select(e => e.Status).Distinct().Order().ToArray();
            if (statuses.Length > 1) groups.Add(new("Status", statuses.Select(s => new Chip(s.ToString(), StatusWord(s), null, 0, filter.Statuses.Contains(s))).ToArray()));
            var relevances = Spec.Events.Select(e => e.Relevance).Distinct().OrderDescending().ToArray();
            if (relevances.Length > 1) groups.Add(new("Relevance", relevances.Select(r => new Chip(r.ToString(), RelevanceWord(r), null, 0, filter.Relevances.Contains(r))).ToArray()));
            return groups;
        }
    }
    private static string StatusWord(PlannerStatus s) => s switch { PlannerStatus.Provisional => "Provisional", PlannerStatus.Cancelled => "Cancelled", _ => "Confirmed" };
    private static string RelevanceWord(PlannerRelevance r) => r switch { PlannerRelevance.Clash => "Clash", PlannerRelevance.Near => "Close", _ => "Other" };
    private string Word(string group, string value) => group switch
    {
        "Region" => Spec.Regions.FirstOrDefault(r => r.Code == value)?.Name ?? value,
        "Status" => StatusWord(Enum.Parse<PlannerStatus>(value)),
        "Relevance" => RelevanceWord(Enum.Parse<PlannerRelevance>(value)),
        _ => value,
    };
    private bool IsOn(string group, string value) => group switch
    {
        "Region" => filter.Regions.Contains(value),
        "Category" => filter.Categories.Contains(value),
        "Audience" => filter.Audiences.Contains(value),
        "Status" => filter.Statuses.Contains(Enum.Parse<PlannerStatus>(value)),
        "Relevance" => filter.Relevances.Contains(Enum.Parse<PlannerRelevance>(value)),
        _ => false,
    };
    private bool Filtered => filter.Regions.Count + filter.Categories.Count + filter.Audiences.Count + filter.Statuses.Count + filter.Relevances.Count > 0;
    private static IReadOnlyList<T> Flip<T>(IReadOnlyList<T> list, T value) =>
        list.Contains(value) ? list.Where(v => !EqualityComparer<T>.Default.Equals(v, value)).ToArray() : [.. list, value];

    private void Choose(string group, string value)
    {
        filter = group switch
        {
            "Region" => filter with { Regions = Flip(filter.Regions, value) },
            "Category" => filter with { Categories = Flip(filter.Categories, value) },
            "Audience" => filter with { Audiences = Flip(filter.Audiences, value) },
            "Status" => filter with { Statuses = Flip(filter.Statuses, Enum.Parse<PlannerStatus>(value)) },
            "Relevance" => filter with { Relevances = Flip(filter.Relevances, Enum.Parse<PlannerRelevance>(value)) },
            _ => filter,
        };
        status = $"Filter {Word(group, value)} {(IsOn(group, value) ? "on" : "off")}";
        Render();
        StateHasChanged();
    }

    private void ClearFilters()
    {
        filter = new();
        status = "Filters cleared";
        Render();
        StateHasChanged();
    }

    // A new spec keeps the reader's choices it still offers, and any its own filter names.
    private PlannerFilter Offered(PlannerFilter chosen)
    {
        IReadOnlyList<string> Keep(IReadOnlyList<string> on, IEnumerable<string?> offered, IReadOnlyList<string>? host)
        {
            var allowed = offered.OfType<string>().Concat(host ?? []).ToHashSet(StringComparer.Ordinal);
            return on.Where(allowed.Contains).ToArray();
        }
        return chosen with
        {
            Regions = Keep(chosen.Regions, Spec.Regions.Select(r => r.Code), Spec.Filter?.Regions),
            Categories = Keep(chosen.Categories, Spec.Events.Select(e => e.Category), Spec.Filter?.Categories),
            Audiences = Keep(chosen.Audiences, Spec.Events.Select(e => e.Audience), Spec.Filter?.Audiences),
        };
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Lumen.Charts.Blazor/lumen.js");
            reference = DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("attachPlanner", root, reference);
            await module.InvokeVoidAsync("fit", root, reference);
        }
        if (module is not null && !ReferenceEquals(told, svg))
        {
            told = svg;
            var at = focus?.ToString("yyyy-MM-dd", Invariant);
            focus = null;
            await module.InvokeVoidAsync("plannerDrawn", root, at);
        }
    }

    /// <summary>Removes the planner's event handlers from the page and stops measuring its box.</summary>
    public async ValueTask DisposeAsync()
    {
        try { if (module is not null) { await module.InvokeVoidAsync("detach", root); await module.DisposeAsync(); } }
        catch (JSDisconnectedException) { }
        finally { reference?.Dispose(); }
    }
}
```

- [ ] **Step 4: Run the build and the unit tests**

Build. Expected: 0 warnings (a missing XML comment on a public member is an error). Run the unit tests. Expected: every `LumenPlanner:` test passes, "The packages carry their XML documentation…" passes, and the count rises by ten over Task 1's.

If a test fails because the renderer writes an attribute differently (for example `disabled=""` or a different attribute order), change the test's pattern to the renderer's actual output, not the component; say so in the report. If `SetParametersAsync` called from the test is refused by the renderer, re-render through a parent `RenderFragment` holding the spec in a captured variable instead, and say so.

- [ ] **Step 5: Commit**

```bash
git add src/Lumen.Charts.Blazor/LumenPlanner.razor tests/Lumen.Charts.Tests/Program.cs
git commit -m "Add the LumenPlanner component: zoom, steps, filters, fitted width and its callbacks" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The planner's script and styles, the gallery, and its interaction checks

**Files:**
- Modify: `src/Lumen.Charts.Blazor/wwwroot/lumen.js` (append two exported functions after `attachGraph`)
- Modify: `src/Lumen.Charts.Blazor/wwwroot/lumen.css`
- Modify: `samples/Lumen.Gallery/Components/Pages/Home.razor` (the `#planner` section, about lines 62–75, and its `@code`), `samples/Lumen.Gallery/wwwroot/app.css` (the planner rules, lines 9–19)
- Modify: `tests/Lumen.Charts.BrowserTests/Program.cs` (the planner block, about lines 664–743)

**Interfaces:**
- Consumes: Task 2's markup contract and `[JSInvokable]` names (`Open(date, later)`, `Back(date)`, `SelectEvent(id)`, `Fit(width)`); Task 1's `rect.lumen-cell`; 0.43.0's drawing: wide-year and month cells `g.lumen-day[data-day]`, phone-year cells `g.lumen-week[data-weekend]` (first `rect` is the slot), events `.lumen-datum[data-event]` (static `tabindex='0'`), the agenda's and day's `g.lumen-day[data-day]`. Module-level `handlers`, `described` and `NS` in `lumen.js`; `detach(root)` already removes `bindings`, disconnects `sized` and deletes the entry.
- Produces: `export function attachPlanner(root, dotnet)` and `export function plannerDrawn(root, focus)`; a focused cell's ring `rect.lumen-ring`; the gallery's `#planner` section with `<LumenPlanner>` and `p#planner-picked`.

- [ ] **Step 1: Write the failing browser checks**

In `tests/Lumen.Charts.BrowserTests/Program.cs`, replace the whole 0.43.0 planner block — `if (await page.Locator("#planner").CountAsync() > 0) { … } else Console.WriteLine("SKIP planner checks: this host shows no planner");` with its eight checks and the `plannerShown` script — by:

```csharp
// 0.44.0: the interactive planner. Its checks run on any host that shows one in a #planner section.
async Task PlannerFitted(IPage tab) => await tab.WaitForFunctionAsync(
    "() => { const v = document.querySelector('#planner .lumen-viewport'), s = v?.querySelector('svg');" +
    " return !!s && Math.abs(s.viewBox.baseVal.width - Math.max(320, v.clientWidth)) <= 1" +
    " && document.querySelectorAll('#planner .lumen-viewport [tabindex=\"0\"]').length === 1; }", null, new() { Timeout = 60_000 });
async Task PlannerAt(IPage tab, string zoom) => await tab.WaitForSelectorAsync($"#planner .lumen-planner-box[data-zoom='{zoom}']");
async Task PlannerHome()
{
    for (var i = 0; i < 2 && await page.Locator("#planner .lumen-planner-box[data-zoom='year']").CountAsync() == 0; i++)
        await page.Locator("#planner .lumen-planner-bar button").First.ClickAsync();
    await PlannerAt(page, "year");
    await PlannerFitted(page);
}
async Task<string?> PlannerFocus() => await page.EvaluateAsync<string?>("() => document.activeElement?.dataset?.day ?? document.activeElement?.dataset?.event ?? null");
async Task PlannerPicked(string words) => await page.WaitForFunctionAsync($"() => (document.querySelector('#planner-picked')?.textContent ?? '').includes({System.Text.Json.JsonSerializer.Serialize(words)})");
if (await page.Locator("#planner .lumen-planner-box").CountAsync() > 0)
{
    await Test("Planner: the drawing is one tab stop on a day, the year's events are not tab stops, and the keys are described", async () =>
    {
        await PlannerHome();
        var facts = await page.EvaluateAsync<string>(@"() => {
            const svg = document.querySelector('#planner .lumen-viewport svg'), stop = svg.querySelector('[tabindex=""0""]');
            const keys = document.getElementById(svg.getAttribute('aria-describedby') ?? '')?.textContent ?? '';
            return [stop.matches('g.lumen-day[data-day]'), svg.querySelectorAll('.lumen-datum[tabindex=""0""]').length, keys.startsWith('Arrow keys')].join('|');
        }");
        Check(facts == "true|0|true", facts);
    });
    await Test("Planner: arrow keys move by day and by month, Enter opens the month on that day, and Escape returns to the day in the year", async () =>
    {
        await PlannerHome();
        await page.EvaluateAsync("() => document.querySelector('#planner g.lumen-day[data-day=\"2027-03-10\"]').focus()");
        Check(await page.Locator("#planner .lumen-ring").CountAsync() == 1, "the focused day is ringed");
        await page.Keyboard.PressAsync("ArrowRight");
        Check(await PlannerFocus() == "2027-03-11", "ArrowRight");
        await page.Keyboard.PressAsync("ArrowDown");
        Check(await PlannerFocus() == "2027-04-11", "ArrowDown is the next month");
        await page.Keyboard.PressAsync("Enter");
        await PlannerAt(page, "month");
        await page.WaitForFunctionAsync("() => document.activeElement?.dataset?.day === '2027-04-11'");
        Check(await page.Locator("#planner .lumen-planner-where").TextContentAsync() == "April 2027", "April");
        await page.Keyboard.PressAsync("ArrowDown");
        Check(await PlannerFocus() == "2027-04-18", "ArrowDown is the next week in a month");
        await page.Keyboard.PressAsync("Escape");
        await PlannerAt(page, "year");
        await page.WaitForFunctionAsync("() => document.activeElement?.dataset?.day === '2027-04-18'");
    });
    await Test("Planner: a click on a weekday of the year opens its month, a click on a day opens it and raises DaySelected, and Back returns", async () =>
    {
        await PlannerHome();
        await page.Locator("#planner g.lumen-day[data-day='2027-03-10']").ClickAsync();
        await PlannerAt(page, "month");
        await page.Locator("#planner g.lumen-day[data-day='2027-03-13']").ClickAsync(new() { Position = new() { X = 4, Y = 4 } });
        await PlannerAt(page, "day");
        await PlannerPicked("Opened Saturday 13 March 2027");
        await page.GetByRole(AriaRole.Button, new() { Name = "Back to March 2027" }).ClickAsync();
        await PlannerAt(page, "month");
    });
    await Test("Planner: in a day the arrow keys move between its events, Enter selects one, and the status line names it", async () =>
    {
        await PlannerHome();
        await page.EvaluateAsync("() => document.querySelector('#planner g.lumen-day[data-day=\"2027-03-13\"]').focus()");
        await page.Keyboard.PressAsync("Enter");
        await PlannerAt(page, "month");
        await page.WaitForFunctionAsync("() => document.activeElement?.dataset?.day === '2027-03-13'");
        await page.Keyboard.PressAsync("Enter");
        await PlannerAt(page, "day");
        await page.WaitForFunctionAsync("() => !!document.activeElement?.matches?.('#planner .lumen-datum[data-event]')");
        var first = await PlannerFocus();
        await page.Keyboard.PressAsync("ArrowDown");
        var second = await PlannerFocus();
        Check(first != second && second is not null, $"{first} then {second}");
        var words = await page.EvaluateAsync<string>(@"() => { const e = document.activeElement, t = e.querySelector('text'), r = e.querySelector('.lumen-focus');
            return getComputedStyle(t).stroke + '|' + (r ? getComputedStyle(r).stroke : 'no ring'); }");
        Check(words.StartsWith("none|") && !words.EndsWith("|none") && !words.EndsWith("no ring"), "a focused event keeps its words unstroked and shows its ring: " + words);
        await page.Keyboard.PressAsync("Enter");
        await PlannerPicked("Selected event");
        var status = await page.Locator("#planner .lumen-status").TextContentAsync();
        Check(status?.StartsWith("Selected ") == true, status ?? "no status");
        await page.Keyboard.PressAsync("Escape");
        await PlannerAt(page, "month");
        await page.Keyboard.PressAsync("Escape");
        await PlannerAt(page, "year");
    });
    await Test("Planner: a click on an event's stripe in the year selects it without zooming", async () =>
    {
        await PlannerHome();
        await page.Locator("#planner .lumen-datum[data-event='hx1']").First.ClickAsync();
        await PlannerPicked("Hilltop XCO #1");
        Check(await page.Locator("#planner .lumen-planner-box[data-zoom='year']").CountAsync() == 1, "still the year");
    });
    await Test("Planner: a region chip filters the drawing and says it is pressed, and Clear filters restores it", async () =>
    {
        await PlannerHome();
        var chip = page.GetByRole(AriaRole.Button, new() { Name = "Gauteng, in South Africa" });
        await chip.ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#planner .lumen-datum[data-event=\"cs\"]').length === 0");
        Check(await chip.GetAttributeAsync("aria-pressed") == "true", "pressed");
        Check(await page.Locator("#planner .lumen-datum[data-event='hx1']").CountAsync() > 0, "Gauteng's events stay");
        await page.GetByRole(AriaRole.Button, new() { Name = "Clear filters" }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#planner .lumen-datum[data-event=\"cs\"]').length > 0");
    });
    await Test("Planner: Previous and Next step months and stop at the period's edges", async () =>
    {
        await PlannerHome();
        await page.Locator("#planner g.lumen-day[data-day='2027-01-06']").ClickAsync();
        await PlannerAt(page, "month");
        Check(await page.GetByRole(AriaRole.Button, new() { Name = "Previous month" }).IsDisabledAsync(), "January has no previous month");
        await page.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('#planner .lumen-planner-where')?.textContent === 'February 2027'");
        await PlannerHome();
    });
    if (await page.Locator("#planner details summary").CountAsync() > 0)
        await Test("Planner: the month as a table holds every day", async () =>
        {
            await page.Locator("#planner details summary").ClickAsync();
            Check(await page.Locator("#planner table td").CountAsync() >= 31, "31 days");
        });
}
else Console.WriteLine("SKIP planner checks: this host shows no planner");
```

(If the file already declares a local function or variable with one of these names, rename the new one and say so. `AriaRole` and `WaitUntilState` come from `Microsoft.Playwright`, already imported.)

- [ ] **Step 2: Run them to see them fail**

Build the solution and `tests/Lumen.Charts.BrowserTests`, start the gallery, run the browser suite. Expected: `SKIP planner checks: this host shows no planner` (the gallery still shows the static drawings).

- [ ] **Step 3: Put the component in the gallery**

In `samples/Lumen.Gallery/Components/Pages/Home.razor`, replace the `#planner` section's body (the `@foreach (var (shown, width, drawn) in PlannerDrawings) { … }` loop and its introduction) so the section reads:

```razor
<section id="planner" class="card">
    <h2>Planning a season</h2>
    <p>A <code>&lt;LumenPlanner&gt;</code> over a <code>PlannerSpec</code> of invented events, with South Africa's provinces and 2027 public holidays. Click a day, or move to it with the arrow keys and press Enter, to open its month; open a day in the month to list its events; Escape or Back goes out. The chips filter by region, category, audience, status and relevance. It is drawn at its box's width, and below 640 pixels in its phone layout.</p>
    @Branded(@<LumenPlanner Spec="PlannerData.Season(Theme)" EventSelected="OnPlannerEvent" DaySelected="OnPlannerDay"/>)
    <p class="quiet" id="planner-picked">@(plannerPicked ?? "Select an event, or open a day")</p>
    <details><summary>March as a table</summary>@((MarkupString)PlannerSvg.Table(PlannerData.Season(Theme), 2027, 3))</details>
</section>
```

In its `@code`, delete the `PlannerDrawings` field and the comment above it, and add:

```csharp
    private string? plannerPicked;
    private void OnPlannerEvent(PlannerEvent e)=>plannerPicked=$"Selected event {e.Name}";
    private void OnPlannerDay(DateOnly day)=>plannerPicked=$"Opened {day.ToString("dddd d MMMM yyyy",System.Globalization.CultureInfo.InvariantCulture)}";
```

In `samples/Lumen.Gallery/wwwroot/app.css`, delete the comment about the three static drawings and the rules `#planner .planner-medium,#planner .planner-narrow{display:none}` and both `@container` rules; keep `#planner{…}` (drop `container-type:inline-size` from it), `#planner>p`, `#planner h3`, `#planner details`, `#planner summary`, `#planner table`, `#planner td,#planner th`; change `#planner svg{margin-bottom:12px}` to `#planner .lumen-planner-box{margin-bottom:8px}`.

- [ ] **Step 4: Write the script**

Append to `src/Lumen.Charts.Blazor/wwwroot/lumen.js`, after `attachGraph`:

```js
/// Makes a planner drawn by <LumenPlanner> one tab stop. Its cells (the days of the year or of a month, the weekends of a
/// phone's year, the events of a day) share a roving tab stop: the arrow keys, Home and End move it, Enter or Space opens a
/// cell (a month from the year, a day from a month) or selects an event, and Escape goes back out. A click opens or selects
/// the same way. Events in the year and in a month are not tab stops; a keyboard reaches them by opening their day. The
/// focused cell is ringed in the text colour. Nothing here changes what PlannerSvg.Render drew, only the live page.
export function attachPlanner(root, dotnet) {
    const viewport = root.querySelector(':scope > .lumen-viewport');
    const words = root.querySelector(':scope > .lumen-keys');
    const keys = words ? words.id || (words.id = 'lumen-keys-' + ++described) : null;
    const state = { lastDate: null, engaged: false, ring: null };
    const drawing = () => viewport.querySelector('svg');
    const zoom = () => root.dataset.zoom;
    const narrow = () => root.dataset.layout === 'narrow';
    const selector = () => zoom() === 'day' ? '.lumen-datum[data-event], g.lumen-day[data-day]'
        : zoom() === 'year' && narrow() ? 'g.lumen-week[data-weekend]' : 'g.lumen-day[data-day]';
    // A day's cells are its events; a day with none is its own single cell, so Escape still reaches it.
    const cells = () => {
        if (zoom() !== 'day') return [...viewport.querySelectorAll(selector())];
        const events = [...viewport.querySelectorAll('.lumen-datum[data-event]')];
        return events.length ? events : [...viewport.querySelectorAll('g.lumen-day[data-day]')];
    };
    const cellOf = target => {
        const cell = target && target.closest ? target.closest(selector()) : null;
        return cell && viewport.contains(cell) ? cell : null;
    };
    const dateOf = cell => cell?.dataset.day || cell?.dataset.weekend || null;
    const rove = cell => {
        for (const other of cells()) other.setAttribute('tabindex', other === cell ? '0' : '-1');
        const date = dateOf(cell);
        if (date) state.lastDate = date;
    };
    const unring = () => { state.ring?.remove(); state.ring = null; };
    const ring = cell => {
        unring();
        const svg = drawing();
        if (!svg || !cell || cell.matches('.lumen-datum')) return;   // an event draws its own ring
        const box = cell.getBBox();
        const rect = document.createElementNS(NS, 'rect');
        const attributes = { class: 'lumen-ring', x: box.x - 2, y: box.y - 2, width: box.width + 4, height: box.height + 4, rx: 3,
            fill: 'none', stroke: 'currentColor', 'stroke-width': 2, 'pointer-events': 'none', 'aria-hidden': 'true' };
        for (const [name, value] of Object.entries(attributes)) rect.setAttribute(name, String(value));
        svg.appendChild(rect);
        state.ring = rect;
    };
    // A weekend across a month's end stands in both months' bars under one key; the second opens the later month.
    const open = cell => {
        if (cell.dataset.weekend) {
            const same = cells().filter(other => other.dataset.weekend === cell.dataset.weekend);
            dotnet.invokeMethodAsync('Open', cell.dataset.weekend, same.indexOf(cell) > 0);
        } else if (cell.dataset.day) dotnet.invokeMethodAsync('Open', cell.dataset.day, false);
    };
    const shift = (iso, days) => { const d = new Date(iso + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };
    const shiftMonth = (iso, months) => {
        const d = new Date(iso + 'T00:00:00Z');
        const first = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + months, 1));
        const last = new Date(Date.UTC(first.getUTCFullYear(), first.getUTCMonth() + 1, 0)).getUTCDate();
        first.setUTCDate(Math.min(d.getUTCDate(), last));
        return first.toISOString().slice(0, 10);
    };
    const byDate = iso => viewport.querySelector(`g.lumen-day[data-day="${iso}"]`);
    const slot = (cell, axis) => Number(cell.querySelector('rect')?.getAttribute(axis) ?? 0);
    const target = (cell, key) => {
        const all = cells(), i = all.indexOf(cell);
        const back = key === 'ArrowLeft' || key === 'ArrowUp';
        if (zoom() === 'day' || (zoom() === 'month' && narrow()))
            return key === 'Home' ? all[0] : key === 'End' ? all[all.length - 1] : all[i + (back ? -1 : 1)];
        if (zoom() === 'year' && narrow()) {
            if (key === 'ArrowLeft' || key === 'ArrowRight') return all[i + (back ? -1 : 1)];
            const row = all.filter(other => slot(other, 'y') === slot(cell, 'y'));
            if (key === 'Home') return row[0];
            if (key === 'End') return row[row.length - 1];
            const rows = [...new Set(all.map(other => slot(other, 'y')))].sort((a, b) => a - b);
            const next = rows[rows.indexOf(slot(cell, 'y')) + (back ? -1 : 1)];
            if (next === undefined) return null;
            return all.filter(other => slot(other, 'y') === next)
                .reduce((best, other) => !best || Math.abs(slot(other, 'x') - slot(cell, 'x')) < Math.abs(slot(best, 'x') - slot(cell, 'x')) ? other : best, null);
        }
        const day = cell.dataset.day, month = all.filter(other => other.dataset.day.slice(0, 7) === day.slice(0, 7));
        switch (key) {
            case 'ArrowLeft': return byDate(shift(day, -1));
            case 'ArrowRight': return byDate(shift(day, 1));
            case 'ArrowUp': return byDate(zoom() === 'year' ? shiftMonth(day, -1) : shift(day, -7));
            case 'ArrowDown': return byDate(zoom() === 'year' ? shiftMonth(day, 1) : shift(day, 7));
            case 'Home': return month[0];
            case 'End': return month[month.length - 1];
        }
        return null;
    };
    const moves = ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'];
    const keydown = event => {
        const cell = cellOf(event.target);
        if (!cell || event.target !== cell) return;
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            state.engaged = true;
            if (cell.dataset.event) dotnet.invokeMethodAsync('SelectEvent', cell.dataset.event);
            else if (zoom() !== 'day') open(cell);
        } else if (event.key === 'Escape') {
            if (zoom() === 'year') return;
            event.preventDefault();
            state.engaged = true;
            dotnet.invokeMethodAsync('Back', dateOf(cell));
        } else if (moves.includes(event.key)) {
            event.preventDefault();
            const next = target(cell, event.key);
            if (next) { rove(next); next.focus(); }
        }
    };
    // A click on an event selects it; a click anywhere else in a cell opens it, and a click on a mark drawn over a cell (a
    // "+N") opens the cell beneath.
    const click = event => {
        if (!viewport.contains(event.target)) return;
        const mark = event.target.closest('.lumen-datum[data-event]');
        if (mark) {
            if (zoom() === 'day') rove(mark);
            dotnet.invokeMethodAsync('SelectEvent', mark.dataset.event);
            return;
        }
        if (zoom() === 'day') return;
        const cell = cellOf(event.target) || document.elementsFromPoint(event.clientX, event.clientY).map(cellOf).find(Boolean);
        if (!cell) return;
        state.engaged = true;
        rove(cell);
        open(cell);
    };
    const focusin = event => {
        const cell = cellOf(event.target);
        if (cell && cells().includes(cell)) { rove(cell); ring(cell); }
    };
    const focusout = event => {
        if (!cellOf(event.relatedTarget)) unring();
        if (event.relatedTarget && !root.contains(event.relatedTarget)) state.engaged = false;
    };
    // The drawing fits its box, so the viewport is a tab stop and a region only while a box narrower than 320 pixels scrolls it.
    const region = () => {
        if (viewport.scrollWidth > viewport.clientWidth + 1) {
            viewport.setAttribute('tabindex', '0'); viewport.setAttribute('role', 'region'); viewport.setAttribute('aria-label', 'Scrollable planner');
        } else { viewport.removeAttribute('tabindex'); viewport.removeAttribute('role'); viewport.removeAttribute('aria-label'); }
    };
    const sized = new ResizeObserver(region);
    sized.observe(viewport);
    // Each new drawing: every event leaves the tab order, the cell for the date asked for (or the last one focused) takes the
    // stop, and if the reader's keypress or click replaced the drawing under their focus, the focus follows to that cell.
    state.settle = focus => {
        unring();
        const svg = drawing();
        if (!svg) return;
        if (keys) svg.setAttribute('aria-describedby', keys);
        for (const mark of svg.querySelectorAll('.lumen-datum')) mark.setAttribute('tabindex', '-1');
        region();
        const all = cells();
        if (all.length === 0) return;
        const date = focus || state.lastDate;
        let current = all[0];
        if (date && zoom() !== 'day')
            current = zoom() === 'year' && narrow()
                ? all.filter(cell => cell.dataset.weekend <= date).pop() || all[0]
                : all.find(cell => cell.dataset.day >= date) || all[all.length - 1];
        rove(current);
        const active = document.activeElement;
        if (state.engaged && (!active || active === document.body)) current.focus();
    };
    const bindings = [['keydown', keydown], ['click', click], ['focusin', focusin], ['focusout', focusout]];
    for (const [type, handler] of bindings) root.addEventListener(type, handler);
    handlers.set(root, { bindings, sized, planner: state });
    state.settle(null);
}

/// Tells a planner's script that its drawing changed, and which date (yyyy-MM-dd), if any, should hold its tab stop.
export function plannerDrawn(root, focus) {
    handlers.get(root)?.planner?.settle(focus);
}
```

- [ ] **Step 5: Write the styles**

Read `src/Lumen.Charts.Blazor/wwwroot/lumen.css` first. Add `.lumen-planner-bar button,.lumen-planner-filters button` to the selector list of the existing rule that styles the legend's and toolbar's buttons, and to the existing `:focus-visible` outline rule for those buttons, so the planner's buttons look and focus like the chart's. Then append:

```css
.lumen-planner-box{min-width:0;position:relative}.lumen-planner-box>.lumen-viewport>svg{min-width:320px}.lumen-planner-box .lumen-viewport svg g:focus{outline:none}
.lumen-planner-bar,.lumen-planner-filters{display:flex;gap:8px;flex-wrap:wrap;align-items:center;padding:8px 0}.lumen-planner-where{font-size:14px;font-weight:600;min-width:12ch;text-align:center}.lumen-planner-box .lumen-status{display:block;font-size:12px;min-height:1.4em}
.lumen-planner-filters fieldset{display:flex;gap:6px;flex-wrap:wrap;align-items:center;border:0;margin:0;padding:0}.lumen-planner-filters legend{float:left;margin-right:4px;font-size:12px}.lumen-planner-sub{margin-left:12px}
.lumen-planner-filters button[aria-pressed=true]{font-weight:700;border-width:2px}.lumen-planner-filters button[aria-pressed=true]::before{content:"\2713\00a0"/""}.lumen-planner-bar button:disabled,.lumen-planner-filters button:disabled{opacity:.55;cursor:default}
```

(`content:"✓ "/""` gives the check mark an empty alternative, so it is seen but not read; the pressed state is read from `aria-pressed`. The disabled buttons' words must still clear 4.5:1 at that opacity on the gallery's light and dark surfaces: measure them with the browser's computed colours, and raise the opacity until they do; report the ratios.)

- [ ] **Step 6: Run the build, the unit tests and the browser suite**

Build the solution (0 warnings), run the unit tests (all pass), build `tests/Lumen.Charts.BrowserTests`, start the gallery, run `verify-api.ps1` (all pass; Task 4 adds its planner check) and the browser suite. Expected: the eight `Planner:` checks above PASS, every other check still passes, and no check from before this task is lost except 0.43.0's eight static-planner checks (Task 4 replaces their width and axe coverage).

Look at the planner yourself at 1400×1000: the year, a month, a day, a chip pressed, the ring on a focused day. Save screenshots outside the repository and list their paths.

- [ ] **Step 7: Commit**

```bash
git add src/Lumen.Charts.Blazor/wwwroot/lumen.js src/Lumen.Charts.Blazor/wwwroot/lumen.css samples/Lumen.Gallery/Components/Pages/Home.razor samples/Lumen.Gallery/wwwroot/app.css tests/Lumen.Charts.BrowserTests/Program.cs
git commit -m "Make the planner one tab stop that zooms by click and keys, and show it in the gallery" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The WebAssembly host, widths from phone to desktop, axe at 768 and 375, and the HTTP check

**Files:**
- Modify: `samples/Lumen.Gallery/PlannerData.cs` (a doc comment on `Season`)
- Modify: `samples/Lumen.Wasm/Lumen.Wasm.csproj`, `samples/Lumen.Wasm/Host.razor`
- Modify: `tests/Lumen.Charts.BrowserTests/Program.cs` (inside Task 3's `if (… ".lumen-planner-box" …) { … }` block, after its last check)
- Modify: `tests/verify-api.ps1` (after the home page's checks, about line 13)

**Interfaces:**
- Consumes: Task 3's `PlannerFitted(IPage)`, `PlannerAt(IPage, string)`; the suite's `browser`, `address`, `SweepOf(IPage)`; `PlannerData.Season(ChartTheme, int width = 1100, string title = "Season planner")`.
- Produces: the WebAssembly host's `<section id="planner">` with `<LumenPlanner>` and `p#planner-picked`, so the planner checks run on both hosts.

- [ ] **Step 1: Write the failing browser checks and the HTTP check**

Inside the planner block in `tests/Lumen.Charts.BrowserTests/Program.cs`, after the table check, add:

```csharp
    async Task<IPage> PlannerTab(IBrowserContext context)
    {
        var tab = await context.NewPageAsync();
        await tab.GotoAsync(address + "#planner", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
        await PlannerFitted(tab);
        return tab;
    }
    foreach (var (width, height, phone) in new[] { (375, 812, true), (768, 1024, false), (1024, 768, false), (1280, 900, false), (1440, 900, false) })
        await Test($"Planner: at {width} by {height} it is drawn at its box's width, narrow only below 640 pixels, its words at 9 pixels or more, and the page does not scroll sideways", async () =>
        {
            var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height }, IsMobile = phone, HasTouch = phone, DeviceScaleFactor = phone ? 2 : 1 });
            try
            {
                var tab = await PlannerTab(context);
                var facts = await tab.EvaluateAsync<string>(@"() => {
                    const box = document.querySelector('#planner .lumen-planner-box'), viewport = box.querySelector('.lumen-viewport'), svg = viewport.querySelector('svg');
                    const expected = viewport.clientWidth < 640 ? 'narrow' : 'wide', scale = svg.getBoundingClientRect().width / svg.viewBox.baseVal.width, problems = [];
                    if (box.dataset.layout !== expected) problems.push('layout ' + box.dataset.layout + ' at ' + viewport.clientWidth);
                    if (10 * scale < 9) problems.push('10-unit words at ' + (10 * scale).toFixed(1) + ' px');
                    if (document.documentElement.scrollWidth > window.innerWidth + 1) problems.push('the page scrolls sideways to ' + document.documentElement.scrollWidth);
                    return problems.length ? problems.join('; ') : 'ok';
                }");
                Check(facts == "ok", facts);
                if (phone) Check(await tab.Locator("#planner .lumen-planner-box[data-layout='narrow']").CountAsync() == 1, "a phone gets the narrow layout");
            }
            finally { await context.CloseAsync(); }
        });
    await Test("Planner: on a 375-pixel phone a weekend across a month's end stands in both months' bars, and the later bar opens the later month", async () =>
    {
        var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 });
        try
        {
            var tab = await PlannerTab(context);
            var slots = tab.Locator("#planner g.lumen-week[data-weekend='2027-07-31']");
            Check(await slots.CountAsync() == 2, "in July's bar and August's");
            await slots.Nth(1).ClickAsync();
            await PlannerAt(tab, "month");
            await tab.WaitForFunctionAsync("() => document.querySelector('#planner .lumen-planner-where')?.textContent === 'August 2027'");
        }
        finally { await context.CloseAsync(); }
    });
    foreach (var (width, height, phone) in new[] { (768, 1024, false), (375, 812, true) })
        await Test($"axe-core reports no WCAG A or AA violation at {width} by {height} in the planner's year, a month and a day", async () =>
        {
            var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height }, IsMobile = phone, HasTouch = phone, DeviceScaleFactor = phone ? 2 : 1 });
            try
            {
                var tab = await PlannerTab(context);
                await SweepOf(tab);
                await tab.EvaluateAsync("() => { const c = document.querySelector('#planner g.lumen-day[data-day], #planner g.lumen-week[data-weekend]'); c.focus(); }");
                await tab.Keyboard.PressAsync("Enter");
                await PlannerAt(tab, "month");
                await PlannerFitted(tab);
                await SweepOf(tab);
                await tab.EvaluateAsync("() => document.querySelector('#planner g.lumen-day[data-day=\"2027-01-01\"]')?.focus()");
                await tab.Keyboard.PressAsync("Enter");
                await PlannerAt(tab, "day");
                await SweepOf(tab);
            }
            finally { await context.CloseAsync(); }
        });
```

(If the suite names its browser variable differently, use its name. If `SweepOf` scrolls or reloads the page, call it as it is meant to be called and say so. 1 January 2027 is a public holiday, so the phone's agenda for January holds it.)

In `tests/verify-api.ps1`, after the home page's `Verify` about the network graph (line 13; `$r` still holds the home page), add:

```powershell
Verify ($r.Content.Contains('class="lumen-planner-box" data-zoom="year" data-layout="wide"') -and $r.Content.Contains("class='lumen-svg lumen-planner'") -and $r.Content.Contains('>Clear filters</button>') -and $r.Content.Contains('aria-label="Gauteng, in South Africa"') -and $r.Content.Contains('id="planner-picked"')) 'The home page prerenders its season planner as a component: the year at its own width with its toolbar and filter chips, and the line that reports what was picked'
```

- [ ] **Step 2: Run them to see the new browser checks pass on the gallery and the host show no planner**

Run `verify-api.ps1` (the new check passes) and the browser suite against the gallery (the new checks pass). Build and start the WebAssembly host and run the suite against `http://localhost:5199`. Expected: `SKIP planner checks: this host shows no planner`.

- [ ] **Step 3: Show the planner on the WebAssembly host**

In `samples/Lumen.Gallery/PlannerData.cs`, give `Season` a doc comment (the WebAssembly host builds with warnings as errors):

```csharp
    /// <summary>The 2027 season for the planner, in <paramref name="theme"/>, drawn at <paramref name="width"/> and named
    /// <paramref name="title"/>.</summary>
```

In `samples/Lumen.Wasm/Lumen.Wasm.csproj`, add:

```xml
  <ItemGroup><Compile Include="../Lumen.Gallery/PlannerData.cs" Link="PlannerData.cs" /></ItemGroup>
```

In `samples/Lumen.Wasm/Host.razor`, after the last chart section and inside `<LumenBrand …>`, add:

```razor
    <section id="planner">
        <h2>Season planner</h2>
        <LumenPlanner Spec="season" EventSelected="OnPlannerEvent" DaySelected="OnPlannerDay" />
        <p id="planner-picked">@(plannerPicked ?? "Select an event, or open a day")</p>
    </section>
```

and in its `@code` block:

```csharp
    private readonly PlannerSpec season = Lumen.Gallery.PlannerData.Season(ChartTheme.Light);
    private string? plannerPicked;
    private void OnPlannerEvent(PlannerEvent e) => plannerPicked = $"Selected event {e.Name}";
    private void OnPlannerDay(DateOnly day) => plannerPicked = $"Opened {day.ToString("dddd d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}";
```

Match the file's existing markup style (section headings, classes) where it differs from the above.

- [ ] **Step 4: Run everything**

Build the solution (0 warnings), the browser suite and the WebAssembly host. Run the unit tests, `verify-api.ps1`, the browser suite against the gallery and against the host. Expected: every check passes on both hosts; the host runs every planner check except the table's. Report the exact counts (`N passed; 0 failed`) for unit, HTTP, gallery and host — measured, not estimated.

Look at the planner at 375×812 (Playwright with a real phone context, not a resized desktop page), 768×1024 and 1024×768, in the year, a month and a day. Save screenshots outside the repository and list their paths.

- [ ] **Step 5: Commit**

```bash
git add samples/Lumen.Gallery/PlannerData.cs samples/Lumen.Wasm/Lumen.Wasm.csproj samples/Lumen.Wasm/Host.razor tests/Lumen.Charts.BrowserTests/Program.cs tests/verify-api.ps1
git commit -m "Check the planner on both hosts from a phone to a desktop, with axe at 768 and 375" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Documentation, recipe and version

**Files:**
- Modify: `Directory.Build.props` (`<Version>0.44.0</Version>`)
- Modify: `README.md` (`## Blazor integration`; `### Planning a season`; `## Supported behavior and limits`; a new `## 0.44.0 additions` above `## 0.43.0 additions`)
- Modify: `integrations/claude-code/lumen-charts/SKILL.md`, `references/api.md`, `references/recipes-race-face.md`
- Modify: `docs/VERIFICATION.md`

**Interfaces:**
- Consumes: the public surface of Tasks 1–4 exactly as built (read `LumenPlanner.razor` before writing).
- Produces: documentation only.

- [ ] **Step 1: Version**

Set `<Version>0.44.0</Version>` in `Directory.Build.props`.

- [ ] **Step 2: README**

- In `## Blazor integration`, after the `<LumenGraph>` material, add a short `### The planner component` subsection with this example and the facts below it:

```razor
<LumenPlanner Spec="season" @bind-View="view" EventSelected="OnEvent" DaySelected="OnDay" />

@code {
    PlannerSpec season = PlannerSpec.ForYear(2027) with { /* regions, periods, events */ };
    PlannerView view = PlannerView.WholePeriod;
    void OnEvent(PlannerEvent e) { /* open the event's page */ }
    void OnDay(DateOnly day) { /* offer the day for a new event */ }
}
```

  Facts to state: the reader clicks a day of the year (or moves to it with the arrow keys and presses Enter) to open its month, and a day of a month to open the day; Escape or Back goes out; Previous and Next step months or days and stop at the period's edges. The drawing is one tab stop: arrows move between days (up and down by month in the year, by week in a month), between weekends on a phone, between events in a day; Home and End go to the month's or the list's ends. Events in the year and a month are clicked, or reached by opening their day. Filter chips: the regions as a tree, then category, audience, status and relevance, each group shown when it offers two or more; Clear filters; `Spec.Filter` is where the reader starts; `ShowFilters="false"` hides them. It draws at its box's width (320–4096) and in the narrow layout below 640 px, so no `FitWidth` parameter; before it is interactive it is drawn at `Spec.Width`, wide, and scaled. `ViewChanged` and `@bind-View`; a host view outside the period is refused; a new spec keeps the view if it still lies in the period. The status line names what was shown or selected. Needs an interactive render mode; with Blazor Server, a large year may need the SignalR `MaximumReceiveMessageSize` note already in this README.
- In `### Planning a season`, change the **Sizes** sentence that says the interactive `<LumenPlanner>` "will do this itself" to say it does, and mention the wide year's unpainted day cells (`rect.lumen-cell`, 0.44.0) in **The views**.
- In `## Supported behavior and limits`, update the planner bullet: interactive zoom and filters by `<LumenPlanner>` (0.44.0); still view only; a keyboard reaches an event in the year or a month by opening its day.
- Add `## 0.44.0 additions` above `## 0.43.0 additions`, in the style of its neighbours: `<LumenPlanner>` (zoom, keys, steps, filters, fitted width and narrow layout, the three callbacks), the wide year's hit cells, and "no other rendering moved".

- [ ] **Step 3: The skill**

- `SKILL.md`: the package table's `Lumen.Charts.Blazor` row and the `## 2. Draw something` **Blazor:** paragraph name `<LumenPlanner>`; the "**Season planners** (0.43.0)" bullet in `## 3` gains one sentence on `<LumenPlanner>` (0.44.0). Edit the frontmatter description only if it stays at or under 1024 characters with no unquoted `: ` (check with `python -c "import re,sys;t=open(sys.argv[1],encoding='utf-8').read();d=re.search(r'^description: (.*)$',t,re.M).group(1);print(len(d))" integrations/claude-code/lumen-charts/SKILL.md`).
- `references/api.md`: under `## Blazor components (Lumen.Charts.Blazor)`, a `<LumenPlanner …>` bullet with its parameters, callbacks and keys; in `## Season planner`, a row for `LumenPlanner`.
- `references/recipes-race-face.md`, `## Season planner`: add a ```` ```razor ```` block showing `<LumenPlanner Spec="seasonPlanner" EventSelected="…" DaySelected="…" />` beside the static calls (a razor block is not compiled by `tests/Lumen.Charts.Recipes`; keep the existing ```` ```csharp ```` block compiling as it is), and rewrite the **Phones**, **Sizes** and **Limits** bullets so they say the component chooses the layout and width itself and that interactive zoom has arrived, keeping the static instructions for pages without the component.

- [ ] **Step 4: Verification record**

In `docs/VERIFICATION.md`, add a `0.44.0 (<date>)` paragraph at the top of `## Automated results` in the style of 0.43.0's: what was added, the unit checks added (name them by subject), the four baseline rows changed by design and why (an unpainted `rect.lumen-cell` per day of the wide year; nothing visible moves), the HTTP check, the browser checks (both hosts, five widths, axe at 768 and 375 in the year, a month and a day). Update the bullet list's counts with the numbers **you measured** in Task 4 and this task (unit, HTTP, browser on the gallery and on the host, baseline 385 rows with four changed by design, recipes). Add a `0.44.0` paragraph at the top of `## Browser checks` describing what you looked at and at which sizes.

- [ ] **Step 5: Run the checks the docs touch**

Build (0 warnings), run the unit tests (the documentation test passes), and run the recipe check (`cd tests/Lumen.Charts.Recipes && python check.py && dotnet run -c Release; cd ../..`; every recipe chart compiles and renders).

- [ ] **Step 6: Commit**

```bash
git add Directory.Build.props README.md integrations/claude-code/lumen-charts/SKILL.md integrations/claude-code/lumen-charts/references/api.md integrations/claude-code/lumen-charts/references/recipes-race-face.md docs/VERIFICATION.md
git commit -m "Document the planner component, its recipe and 0.44.0" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Whole-release verification and release (the handover's ritual; the controller does this)

- [ ] Final whole-branch review, one fix wave, re-review; merge to `master` on the owner's word.
- [ ] On `master`: build (0 warnings), unit, baseline in both finishes (exactly the four rows changed; then promote `reference/*.txt` and update the baseline README to v0.44.0, 385 rows, four changed by design), gallery + `verify-api.ps1` + browser suite, WebAssembly host + browser suite, recipes. Correct `docs/VERIFICATION.md` counts to what was measured.
- [ ] Pack the three packages to `artifacts/packages`, clear `~/.nuget/packages/lumen.charts*/0.44.0`, smoke-test in a fresh console app (render a planner; check that `LumenPlanner` exists in the Blazor package's assembly).
- [ ] Scan the diff for real data; commit with explicit paths (never the brief); push; `gh run watch`; tag `v0.44.0` on the verified commit; `gh release create` with the three `.nupkg` and honest notes (new, worth knowing, verification with measured counts).
- [ ] Package the skill from the tag; send it and the screenshots to the owner; message "RACEFACE RUNNING EXPANSION 2" (upgrade 0.43 to 0.44; Perform moves with the shared components; the planner page still waits for its own spec with the owner; deploys stay the owner's).
- [ ] Update the brain (`Release.Latest`, `Roadmap.Next` = 0.45.0 heatmap, the 0.44.0 task done) and `HANDOVER.md`; remove the worktree and branch.
