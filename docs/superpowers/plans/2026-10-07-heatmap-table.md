# Heatmap Table (0.46.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `ChartKind.Heatmap` so a cell can write its value and a second line (`CellText`), be marked not rated (`ChartPoint.NotRated`), keep a fixed width and scroll (`CellWidth`), and be read as a real grid table (`ChartExport.HtmlTable`, the component's "View data"); fix the colour-scale wording and use a dark pair on dark backgrounds in the refined finish only.

**Architecture:** New spec and point properties, all off by default, validated in `ChartValidation`, drawn by `ChartSvg.Heatmap`. Two small internal helpers carry the new rules (`HeatmapPair(style, refined)` for the ramp ends, `CellInk(fill, style)` for text colour). `ChartExport.HtmlTable` builds the grid with a `StringBuilder` the way `PlannerSvg.Table` does. `<LumenChart>` shows that grid for a heatmap and sets `--lumen-drawn` for a heatmap with `CellWidth`.

**Tech Stack:** C# / .NET 8 (`Lumen.Charts`, `Lumen.Charts.Blazor`), the unit harness `tests/Lumen.Charts.Tests` (`Test`/`Check`/`Reject`, `HtmlRenderer` + `NoJs`), `tests/Lumen.Charts.Baseline`, `tests/verify-api.ps1`, `tests/Lumen.Charts.BrowserTests`, `tests/Lumen.Charts.Recipes`.

**Spec:** `docs/superpowers/specs/2026-10-07-heatmap-table-design.md`

## Global Constraints

- One feature per release: **0.46.0** only. The places-and-points follow-ups (0.45.1) are parked; the band-label contrast fix is out of scope.
- **Renderings:** against `tests/Lumen.Charts.Baseline/reference/*.txt` (v0.45.0, 388 rows): the four **refined** `Heatmap/*` rows change by design (legend wording; the dark pair in the two Dark rows); the four **classic** `Heatmap/*` rows and every other row are identical; new `heatmap-table/*` rows are added. The unit tests' classic hash pins (`E701C0163185BA1C`) and the refined pin `43D1B062374DE7AE` in "0.34.0's renderings do not move…" must be updated **only** for the refined pin (the classic pin must still pass unchanged).
- **The classic finish is 0.23.0 byte for byte:** no new behaviour runs when `Style.Finish == ChartFinish.Classic` unless a new option (`CellText`, `NotRated`, `CellWidth`) is set by the caller.
- **The Dark preset (`ChartStyle.Dark`) is not changed.** New spec/point properties default to null/false and are left out of the gradient-ID hash at their defaults (`CellText` needs a `ShouldSerialize` hide-when-false modifier, like `FitHeight`).
- **Never colour alone; text at 4.5:1:** a not-rated cell is dashed and said in words; cell text is drawn in whichever of `Style.Text`/`Style.Background` contrasts more with its cell, and only when that reaches 4.5:1.
- Values verbatim from the spec: sub-label ≤ 16 characters; `NotRated` 1–24 characters; `CellWidth` ≥ 24; value text 11 px, sub-label 10 px; dashed outline `stroke-dasharray='3 2'` in `Style.Muted`; legend `Color scale: {min} low to {max} high` (refined); classic legend unchanged; dark pair used when the pair is the default `#E4EDFC`→`#4069D0` and the background's relative luminance is below 0.2.
- Invented data only; never open or stage `lumen-charts-race-face-brief.md`.
- Every new public member has an XML doc comment; Release build at 0 warnings.
- Version **0.46.0** (Task 5).
- Repository rules (HANDOVER.md): kill only `Lumen.Gallery.exe` by image name, others by PID, never `dotnet.exe` by name; patch scripts with the Write tool; explicit `git add` paths; build `tests/Lumen.Charts.BrowserTests` and `samples/Lumen.Wasm` separately.

## Review Focus

1. **Every cell not rated, or all rated cells equal** — the colour scale has no rated values (legend says so: `Color scale: no rated cells`) or pads as `LinearScale` does; nothing throws. Test in Task 2.
2. **A pale fill where neither text colour reaches 4.5:1** (a custom mid-grey ramp) — the cell writes no text; its name still carries the value. Test in Task 2.
3. **Long sub-labels and narrow cells** (16-character sub-label at 340 px with eight columns) — the sub-label is dropped first, then the value; no text overflows its cell. Test in Task 2.
4. **`CellWidth` with many columns** (100 columns × 24) — width = 165 + 2400 = 2565 ≤ 4096, labels thinned without overlap; 100 × 40 = 4165 > 4096 refused with a reason. Test in Tasks 1 and 2.
5. **`NotRated` on a point with a null value** — the cell is still drawn ("—"), focusable, and named without a value. Test in Task 2.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/Lumen.Charts/Models.cs` (modify) | `ChartSpec.CellText`, `ChartSpec.CellWidth`, `ChartPoint.NotRated` with docs. |
| `src/Lumen.Charts/ChartValidation.cs` (modify) | Heatmap now accepts `YFormat`, `YUnit`, `SubLabel`; per-cell sub-labels exempt from the category rule on heatmaps; new refusals. |
| `src/Lumen.Charts/ChartSvg.cs` (modify) | Hash modifier for `CellText`; `Heatmap()` draws text, not-rated cells, the resolved pair, the new legend, `CellWidth`; legend keys use the resolved pair. |
| `src/Lumen.Charts/ChartExport.cs` (modify) | `HtmlTable(ChartSpec)`; CSV `NotRated` column. |
| `src/Lumen.Charts.Blazor/LumenChart.razor` (modify) | "View data" grid for heatmaps; `--lumen-drawn` for a heatmap with `CellWidth`. |
| `tests/Lumen.Charts.Tests/Program.cs` (modify) | Tests for all of the above; refined pin updated. |
| `tests/Lumen.Charts.Baseline/Program.cs` (modify) | Added `heatmap-table/*` rows. |
| `samples/Lumen.Gallery/SportsData.cs`, `Components/Pages/Sports.razor` (modify) | A "Category heatmap" card. |
| `tests/Lumen.Charts.BrowserTests/Program.cs`, `tests/verify-api.ps1` (modify) | Card checks; page counts +1. |
| `README.md`, `SKILL.md`, `references/api.md`, `references/recipes-race-face.md`, `docs/VERIFICATION.md`, `Directory.Build.props` (modify) | Docs, recipe, version. |

Commands: Build `taskkill //IM Lumen.Gallery.exe //F; dotnet build Lumen.Charts.slnx -c Release 2>&1 | grep -E "Warn|Error" | tail -2`; Unit `dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build 2>&1 | grep -E "^FAIL|Heatmap table|passed"`; Baseline as in the 0.45.0 plan (both finishes, diff against `reference/*.txt`, delete outputs).

---

### Task 1: Model and validation

**Files:** `src/Lumen.Charts/Models.cs`, `src/Lumen.Charts/ChartValidation.cs`, `src/Lumen.Charts/ChartSvg.cs` (hash modifier only), `tests/Lumen.Charts.Tests/Program.cs`.

**Interfaces:**
- Produces: `public bool CellText { get; init; }` and `public double? CellWidth { get; init; }` on `ChartSpec`; `public string? NotRated { get; init; }` on `ChartPoint`. Heatmaps accept `YFormat` (any `ValueFormat`), `YUnit` (≤ 8 characters, as elsewhere) and `SubLabel` (≤ 16, as elsewhere), and per-cell sub-labels may differ within a category.

- [ ] **Step 1: Write the failing tests** (append beside other heatmap tests, before the final summary lines):

```csharp
// 0.46.0: heatmap tables.
ChartSpec HeatGrid(Func<ChartSpec,ChartSpec>? change=null)
{
    var spec=new ChartSpec{Title="Points per start",Description="Invented categories by season",Kind=ChartKind.Heatmap,Width=600,Height=320,CellText=true,
        Series=[new("Sprint",[new ChartPoint(0,2.8,"2025"){SubLabel="/12 starts"},new ChartPoint(1,3.1,"2026"){SubLabel="/14 starts"}]),
                new("Long distance",[new ChartPoint(0,1.2,"2025"){SubLabel="/4 starts",NotRated="too few starts to rate"},new ChartPoint(1,0.4,"2026"){SubLabel="/11 starts"}])]};
    return change is null?spec:change(spec);
}
Test("Heatmap table: cell text, sub-labels per cell, a format, a unit and not-rated cells are accepted on heatmaps",()=>{
    ChartValidation.Validate(HeatGrid());
    ChartValidation.Validate(HeatGrid(s=>s with{YFormat=ValueFormat.Compact,YUnit=" pts",CellWidth=24}));
    ChartValidation.Validate(HeatGrid(s=>s with{Series=[s.Series[0],s.Series[1] with{Points=[new ChartPoint(0,null,"2025"){NotRated="no starts"},s.Series[1].Points[1]]}]}));
});
Test("Heatmap table: the new options are refused where they mean nothing, and outside their limits",()=>{
    var line=new ChartSpec{Title="Line",Kind=ChartKind.Line,Series=[new("A",[new(0,1),new(1,2)])]};
    Reject(()=>ChartValidation.Validate(line with{CellText=true}));
    Reject(()=>ChartValidation.Validate(line with{CellWidth=30}));
    Reject(()=>ChartValidation.Validate(line with{Series=[new("A",[new ChartPoint(0,1){NotRated="x"},new(1,2)])]}));
    Reject(()=>ChartValidation.Validate(HeatGrid(s=>s with{CellWidth=23})));
    Reject(()=>ChartValidation.Validate(HeatGrid(s=>s with{Series=[new("A",Enumerable.Range(0,100).Select(i=>new ChartPoint(i,i)).ToArray())],CellWidth=40})));   // 165+4000 > 4096
    foreach(var bad in new[]{""," ",new string('x',25),"a\nb"})
        Reject(()=>ChartValidation.Validate(HeatGrid(s=>s with{Series=[new("A",[new ChartPoint(0,1){NotRated=bad}])]})));
});
```

- [ ] **Step 2: Run to see them fail.** Build: `CS0117`/`CS1061` for `CellText`, `CellWidth`, `NotRated`.

- [ ] **Step 3: Add the properties** to `Models.cs`:
  - On `ChartSpec` (beside `DensityCells`/`CalendarCell`):
    ```csharp
    /// <summary>On a heatmap, writes each cell's value and, on a second line, its <see cref="ChartPoint.SubLabel"/>, in whichever of
    /// the style's text and background colours stands out more against the cell, and only where that reaches 4.5:1 and the text fits;
    /// the cell's name always carries both. Heatmaps only; false by default.</summary>
    public bool CellText { get; init; }
    /// <summary>On a heatmap, the width of every column in pixels, at least 24: the drawing grows to 165 plus the columns times this
    /// width instead of squeezing into <see cref="Width"/>, and in <c>&lt;LumenChart FitWidth&gt;</c> it scrolls sideways when wider
    /// than its box. Heatmaps only; null by default.</summary>
    public double? CellWidth { get; init; }
    ```
  - On `ChartPoint` (beside `GapLabel`):
    ```csharp
    /// <summary>On a heatmap, marks the cell as not rated and says why, 1 to 24 characters, such as "too few starts to rate": the cell
    /// is drawn unshaded with a dashed outline, left out of the colour scale, written with its value or "—", and named
    /// "…, not rated: {reason}". A not-rated cell is drawn even when its value is null. Heatmaps only.</summary>
    public string? NotRated { get; init; }
    ```
- [ ] **Step 4: Validation** in `ChartValidation.cs`:
  - Y format (≈ line 44): remove `ChartKind.Heatmap` from the kinds refused a Y format (keep Donut, Radar, Histogram); update the message to drop "heatmap".
  - `Unit` (≈ line 555): allow `kind == ChartKind.Heatmap`; drop "heatmap" from the message's list of kinds with no axis.
  - `SubLabel` (≈ line 634): allow `ChartKind.Heatmap`; mention heatmap cells in the message.
  - Category rule (≈ line 332): apply the "a category's sub-label is written once" check only when `spec.Kind is not ChartKind.Heatmap`.
  - New refusals, each with a reason in the house style (one line per rule):
    - `CellText` or `CellWidth` on a kind other than Heatmap: "Cell text and cell width apply to heatmaps, which draw a grid of cells; …".
    - `CellWidth < 24`: "A heatmap's cell is at least 24 pixels wide, so its text and focus ring fit."
    - `165 + columns × CellWidth > 4096`: "At {CellWidth} pixels a column, {n} columns make a drawing {w} pixels wide, past the 4096 a chart may be; narrow the cells or show fewer columns."
    - `NotRated` on a kind other than Heatmap; `NotRated` blank, longer than 24, or containing line breaks.
- [ ] **Step 5: Hash modifier** in `ChartSvg.cs` `Unfitted` (≈ line 110): add `else if (property.Name == nameof(ChartSpec.CellText)) property.ShouldSerialize = (_, text) => text is true;` and extend the doc comment's sentence. (`CellWidth` and `NotRated` are null by default and already left out.)
- [ ] **Step 6: Run** build (0 warnings) and the unit suite: the two new tests pass; every other test passes (nothing renders differently yet). Run the baseline: no row changes.
- [ ] **Step 7: Commit** `git add src/Lumen.Charts/Models.cs src/Lumen.Charts/ChartValidation.cs src/Lumen.Charts/ChartSvg.cs tests/Lumen.Charts.Tests/Program.cs` — message "Accept cell text, sub-labels, formats and not-rated cells on heatmaps" with the trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

---

### Task 2: Drawing

**Files:** `src/Lumen.Charts/ChartSvg.cs` (`Heatmap()` ≈ 3079–3102; the legend keys ≈ 354), `tests/Lumen.Charts.Tests/Program.cs` (tests; the refined pin `43D1B062374DE7AE` in "0.34.0's renderings do not move…" ≈ 6792 updated to the new refined hash).

**Interfaces:**
- Consumes: Task 1's properties; `Contrast.Ratio(string, string)` (`ChartStyle.cs` ≈ 159); `Wide(string)` text-width estimate (≈ 1375, 11 px); `Mix`; `PointLabel`/`Of`/`Datum`; the formatter the Y axis uses for `YFormat`+`YUnit` (find how column value labels format with `YFormat` and `YUnit` and reuse it — do not invent a new formatter).
- Produces: `internal static (string Low, string High) HeatmapPair(ChartStyle style)` and `internal static string? CellInk(string fill, ChartStyle style)` (null = no text), both in `ChartSvg`.

Rules (verbatim from the spec; write them as code in `Heatmap()`):
1. **Pair:** `HeatmapPair(style)`: if `style.Finish == ChartFinish.Refined` and `(style.HeatmapLow, style.HeatmapHigh)` equals `("#E4EDFC", "#4069D0")` (case-insensitive) and the relative luminance of `style.Background` < 0.2, return a dark pair (pick `Low` within a few steps of the background, e.g. `#22304A` on `#171E2E`, and a `High` such as `#6E9BFF`; a unit test must show `Contrast.Ratio(High, style.Background) >= 3` for Dark); otherwise return the style's pair. Use it for the cells **and** for the heatmap's legend keys (≈ line 354). Classic never resolves.
2. **Scale:** built from the non-null values of points **without** `NotRated`. If there are none, skip the ramp (no rated cells).
3. **Cells:** a point is drawn if it has a value or `NotRated`. Rated: fill from the ramp as today. Not rated: `fill='{Style.Background}'`, `stroke='{Style.Muted}' stroke-dasharray='3 2'` (full opacity), same geometry and `rx`.
4. **Names:** rated `PointLabel(series, p) + Of(series, p)` with the sub-label written the way column charts name it (`"{row}: {label} · {sub}, {value}"`) — `PointLabel` already does this through `Under(sub)` when a point has a sub-label; values formatted with `YFormat`+`YUnit` (default Number keeps today's text). Not rated: the same, with the value part omitted when the value is null, then `", not rated: {NotRated}"`. Example: `Long distance: 2025 · /4 starts, 1.2, not rated: too few starts to rate`.
5. **Cell text** (`CellText`): value text (formatted, 11 px, weight 600) centred; sub-label (10 px) on a second line below when present; not rated: value or "—" in `Style.Text`. Rated cells: ink = `CellInk(fill, style)`: the one of `Style.Text`/`Style.Background` with the higher `Contrast.Ratio` against `fill`, or null if below 4.5. Fit: available width `cw − 6`, height `ch − 4`; if two lines do not fit (`Wide(sub)` scaled for 10 px, line heights 13 + 12), drop the sub-label; if the value does not fit one line (height 13, `Wide(value)`), drop it. Text elements carry `aria-hidden='true'` (the cell's name already says it).
6. **CellWidth:** when set, `cw = CellWidth` and the drawing's width is `165 + cats × CellWidth` — compute it before the SVG is opened (the effective width must reach `ChartSvg.Begin`/the viewBox; find where `s.Width` feeds the root size and give heatmaps with `CellWidth` their computed width there). Column labels: when `CellWidth` is set, step labels so no two overlap (`Wide(label)` at 11 px + 6 px gap ≤ step × cw); otherwise today's rule.
7. **Legend** (≈ the `Color scale` line): classic unchanged byte for byte. Refined: `Color scale: {min} low to {max} high` with min/max formatted like the cells; if there are no rated cells, `Color scale: no rated cells`; if `130 + Wide(line) > width − 12`, drop the `Color scale: ` prefix; if still too wide, cut with `Short` to fit.

- [ ] **Step 1: Write the failing tests:**

```csharp
string HeatSvg(ChartSpec spec)=>ChartSvg.Render(spec);
string[] HeatNames(ChartSpec spec)=>Regex.Matches(HeatSvg(spec),"aria-label='([^']*)'").Select(m=>System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).ToArray();
Test("Heatmap table: cells write their value and second line, names carry both, and a not-rated cell is dashed, unshaded and outside the scale",()=>{
    var svg=HeatSvg(HeatGrid());
    Check(svg.Contains(">2.8<")&&svg.Contains(">/12 starts<"),"cell text");
    Check(Regex.IsMatch(svg,$"fill='{ChartStyle.Light.Background}'[^>]*stroke-dasharray='3 2'")||Regex.IsMatch(svg,$"stroke-dasharray='3 2'[^>]*fill='{ChartStyle.Light.Background}'"),"not-rated cell");
    var names=HeatNames(HeatGrid());
    Check(names.Contains("Sprint: 2025 · /12 starts, 2.8"),string.Join(" | ",names));
    Check(names.Contains("Long distance: 2025 · /4 starts, 1.2, not rated: too few starts to rate"),"not rated named");
    Check(svg.Contains("Color scale: 0.4 low to 3.1 high"),"scale leaves the not-rated 1.2 out");
});
Test("Heatmap table: text takes whichever colour stands out more, and no text where neither reaches 4.5:1",()=>{
    var svg=HeatSvg(HeatGrid());
    foreach(Match m in Regex.Matches(svg,"<rect[^>]*fill='(#[0-9A-F]{6})'[^>]*/>\\s*(?:</?g[^>]*>\\s*)*<text[^>]*fill='(#[0-9A-Fa-f]{6})'"))
        Check(Contrast.Ratio(m.Groups[1].Value,m.Groups[2].Value)>=4.5,$"{m.Groups[2].Value} on {m.Groups[1].Value}");
    var grey=HeatGrid(s=>s with{Style=ChartStyle.Light with{HeatmapLow="#777777",HeatmapHigh="#787878"}});
    Check(ChartSvg.CellInk("#777777",grey.Style!) is null,"mid grey takes no text");
    Check(HeatNames(grey).Contains("Sprint: 2025 · /12 starts, 2.8"),"the name keeps it");
});
Test("Heatmap table: a sub-label that does not fit is dropped first, then the value",()=>{
    const string sub="/12 starts riddn";   // 16 characters, the most a sub-label takes
    var narrow=HeatGrid(s=>s with{Width=340,Series=[new("Sprint",Enumerable.Range(0,8).Select(i=>new ChartPoint(i,i+1.5,$"S{i}"){SubLabel=sub}).ToArray())]});
    var svg=HeatSvg(narrow);
    Check(!svg.Contains($">{sub}<"),"sub-label dropped at about 22 px a cell");
    Check(HeatNames(narrow).Any(n=>n.Contains(sub)),"the name keeps it");
});
Test("Heatmap table: a format and a unit reach the cells, the names and the scale",()=>{
    var svg=HeatSvg(HeatGrid(s=>s with{YUnit=" pts"}));
    Check(svg.Contains(">2.8 pts<")&&svg.Contains("0.4 pts low to 3.1 pts high"),"unit");
});
Test("Heatmap table: a not-rated cell without a value is drawn, written '—' and named without a value",()=>{
    var spec=HeatGrid(s=>s with{Series=[s.Series[0],s.Series[1] with{Points=[new ChartPoint(0,null,"2025"){SubLabel="/0 starts",NotRated="no starts"},s.Series[1].Points[1]]}]});
    Check(HeatSvg(spec).Contains(">—<"),"dash");
    Check(HeatNames(spec).Contains("Long distance: 2025 · /0 starts, not rated: no starts"),string.Join(" | ",HeatNames(spec)));
});
Test("Heatmap table: every cell not rated draws, and says there are no rated cells",()=>{
    var spec=HeatGrid(s=>s with{Series=[new("A",[new ChartPoint(0,1){NotRated="x"},new ChartPoint(1,2){NotRated="y"}])]});
    Check(HeatSvg(spec).Contains("Color scale: no rated cells"),"no scale");
});
Test("Heatmap table: CellWidth widens the drawing and thins its column labels without overlap",()=>{
    var wide=HeatGrid(s=>s with{CellWidth=48,Series=[new("A",Enumerable.Range(0,30).Select(i=>new ChartPoint(i,i,$"Season {2000+i}")).ToArray())]});
    Check(HeatSvg(wide).Contains($"viewBox='0 0 {165+30*48} "),"width");
    var hundred=HeatGrid(s=>s with{CellWidth=24,Series=[new("A",Enumerable.Range(0,100).Select(i=>new ChartPoint(i,i,$"C{i}")).ToArray())]});
    Check(HeatSvg(hundred).Contains($"viewBox='0 0 {165+100*24} "),"100 columns at 24");
});
Test("Heatmap table: the refined scale says low and high and fits a phone; the classic keeps 0.23.0's words",()=>{
    var phone=HeatGrid(s=>s with{Width=340,Series=[new("A",[new ChartPoint(0,123456.5,"a"),new ChartPoint(1,987654.25,"b")])]});
    var line=Regex.Match(HeatSvg(phone),">((?:Color scale: )?[^<]* low to [^<]*)<").Groups[1].Value;
    Check(line.Length>0&&130+ChartSvg.Wide(line)<=340-12,line);
    var classic=HeatGrid(s=>s with{CellText=false,Style=ChartStyle.Light with{Finish=ChartFinish.Classic}});
    Check(HeatSvg(classic).Contains("(light) to")&&HeatSvg(classic).Contains("(dark)"),"classic words");
});
Test("Heatmap table: on a dark background the default ramp turns dark, its high end clearing 3:1; the classic finish and other pairs are untouched",()=>{
    var (low,high)=ChartSvg.HeatmapPair(ChartStyle.Dark);
    Check(low!="#E4EDFC"&&Contrast.Ratio(high,ChartStyle.Dark.Background)>=3,$"{low} {high}");
    Check(ChartSvg.HeatmapPair(ChartStyle.Dark with{Finish=ChartFinish.Classic})==("#E4EDFC","#4069D0"),"classic as given");
    Check(ChartSvg.HeatmapPair(ChartStyle.Light)==("#E4EDFC","#4069D0"),"light as given");
    Check(ChartSvg.HeatmapPair(ChartStyle.Midnight)==(ChartStyle.Midnight.HeatmapLow,ChartStyle.Midnight.HeatmapHigh),"midnight as given");
});
```

- [ ] **Step 2: Run to see them fail.**
- [ ] **Step 3: Implement** the seven rules in `ChartSvg.Heatmap()` and the helpers; keep today's code path byte-identical for a spec with none of the new options in the classic finish, and for a light-background refined heatmap except the legend line.
- [ ] **Step 4: Run** build (0 warnings), unit (all pass; update **only** the refined pin `43D1B062374DE7AE` to the new hash, saying so in the report — the classic pin must pass unchanged), baseline in both finishes: exactly the four refined `Heatmap/*` rows change; every classic row and every other refined row identical.
- [ ] **Step 5: Commit** "Write heatmap cells' values, mark not-rated cells, widen cells, and say low and high" with the trailer.

---

### Task 3: The grid table, CSV and the component

**Files:** `src/Lumen.Charts/ChartExport.cs`, `src/Lumen.Charts.Blazor/LumenChart.razor`, `tests/Lumen.Charts.Tests/Program.cs`.

**Interfaces:**
- Consumes: Task 1–2 (formatting of values as the cells write them; use the same formatter).
- Produces: `public static string HtmlTable(ChartSpec spec)` on `ChartExport`.

- [ ] **Step 1: Write the failing tests:**

```csharp
Test("Heatmap table: HtmlTable is a real grid, rows by columns, with headers, sub-labels and not-rated words",()=>{
    var html=ChartExport.HtmlTable(HeatGrid());
    Check(html.StartsWith("<table class='lumen-grid-table'><caption>Points per start</caption>"),html[..Math.Min(90,html.Length)]);
    Check(html.Contains("<thead><tr><td></td><th scope='col'>2025</th><th scope='col'>2026</th></tr></thead>"),"column headers");
    Check(html.Contains("<tr><th scope='row'>Sprint</th><td>2.8 · /12 starts</td><td>3.1 · /14 starts</td></tr>"),"a row");
    Check(html.Contains("<td>1.2 · /4 starts, not rated: too few starts to rate</td>"),"not rated");
    Check(ChartExport.HtmlTable(HeatGrid(s=>s with{Title="A <b>"})).Contains("<caption>A &lt;b&gt;</caption>"),"encoded");
    Reject(()=>ChartExport.HtmlTable(new ChartSpec{Title="L",Kind=ChartKind.Line,Series=[new("A",[new(0,1),new(1,2)])]}));
});
Test("Heatmap table: CSV carries a not-rated column only when a cell is not rated",()=>{
    var csv=ChartExport.Csv(HeatGrid());
    Check(csv.Split("\r\n")[0].EndsWith(",NotRated")&&csv.Contains(",too few starts to rate\r\n"),csv.Split("\r\n")[0]);
    Check(!ChartExport.Csv(HeatGrid(s=>s with{Series=[s.Series[0]]})).Contains("NotRated"),"none");
});
Test("Heatmap table: the component's data table is the grid for a heatmap, and a heatmap with CellWidth keeps its width when fitted",()=>{
    var html=Operate(HeatGrid(),c=>{typeof(LumenChart).GetField("showData",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,true);return Task.CompletedTask;});
    Check(html.Contains("lumen-grid-table")&&html.Contains("<th scope='row'>Sprint</th>"),"grid in View data");
    var fitted=Operate(HeatGrid(s=>s with{CellWidth=48}),async c=>{await (Task)typeof(LumenChart).GetMethod("Fit")!.Invoke(c,[340])!;},fit:true);
    Check(fitted.Contains("--lumen-drawn:")&&fitted.Contains($"viewBox='0 0 {165+2*48} "),"drawn width kept");
});
```

(Adjust `Operate`'s use to its real signature — it takes `(ChartSpec, Func<LumenChart,Task>, bool fit=false)`; if `showData` toggling needs `StateHasChanged`, call the component's own toggle method by reflection instead, and say so.)

- [ ] **Step 2: Run to see them fail.**
- [ ] **Step 3: Implement:**
  - `ChartExport.HtmlTable`: validate; refuse non-heatmaps with `ArgumentException("An HTML grid table reads a heatmap's rows by its columns; other kinds have no grid — use the component's data table or Csv.")`; build with `StringBuilder` and `WebUtility.HtmlEncode`, single-quoted attributes: `<table class='lumen-grid-table'><caption>{title}</caption><thead><tr><td></td>` + `<th scope='col'>{column label}</th>` per column (labels as the heatmap writes them, uncut) + `</tr></thead><tbody>`, then per series `<tr><th scope='row'>{name}</th>` + one `<td>` per column: value (formatted like the cells) + `" · {sub}"` + `", not rated: {words}"`; `—` for not rated without a value (then the sub-label and words as above); empty `<td></td>` for no point or a null value; `</tr>` … `</tbody></table>`. XML doc comment.
  - CSV: a `NotRated` column after `Note` when any point has `NotRated` (`Cell(p.NotRated ?? "")`).
  - `LumenChart.razor`: when `Spec.Kind == ChartKind.Heatmap`, the "View data" area renders `@((MarkupString)ChartExport.HtmlTable(VisibleSpec()))` (hidden rows excluded, as the drawing excludes them) instead of the flat table; other kinds unchanged. When `Spec.Kind == ChartKind.Heatmap && Spec.CellWidth is not null`, the fitted width is the drawing's own (`165 + columns × CellWidth`) and the viewport gets `style="--lumen-drawn:{width}px"` exactly as `LumenGraph.razor` does, so it scrolls instead of squeezing.
  - `lumen.css`: `.lumen-grid-table` gets the same table look as `.lumen-table table` (read the CSS; reuse its rules).
- [ ] **Step 4: Run** build, unit (all pass), baseline (no change from Task 2's state).
- [ ] **Step 5: Commit** "Read a heatmap as a grid table, in the component and for static pages" with the trailer.

---

### Task 4: Baseline rows, gallery card, browser and HTTP checks

**Files:** `tests/Lumen.Charts.Baseline/Program.cs`, `samples/Lumen.Gallery/SportsData.cs`, `samples/Lumen.Gallery/Components/Pages/Sports.razor` (only if the card needs a branch; a normal card needs none), `tests/Lumen.Charts.Tests/Program.cs` (Sports page counts and racing ids), `tests/Lumen.Charts.BrowserTests/Program.cs`, `tests/verify-api.ps1`, and the Sports sidebar badge (Sports.razor and Home.razor, as 0.45.0 did).

- [ ] **Step 1: Baseline rows** after the placings rows, invented data, the harness's `Finished(style, theme)`:
  - `heatmap-table/light`, `heatmap-table/dark`, `heatmap-table/midnight`: four categories × four seasons, `CellText`, sub-labels "/n starts", two not-rated cells (one with a value, one without), `YUnit = " pts"`.
  - `heatmap-table/cell-width`: the light one with `CellWidth = 56` and twelve seasons.
  Expected baseline diff vs `reference/*.txt`: the four refined `Heatmap/*` rows changed (Task 2), four rows added per finish, nothing else.
- [ ] **Step 2: Gallery card** "Category heatmap" (id `category-heatmap`) at the end of the racing cards: invented categories (e.g. "Sprint", "Middle distance", "Long distance", "Relay") by seasons 2023–2026, points per start with `YUnit = " pts"`, `SubLabel` "/n starts", `ValueNote` such as "34 pts, 5 riders", `NotRated = "too few starts to rate"` below 10 starts, `CellText = true`, `CellWidth = 64`, the theme's style; drawn by the page's normal `<LumenChart FitWidth="true">` path. Update every Sports-page count (charts 32 → 33, fitted 29 → 30, the sidebar badge, unit-test counts, browser tooltip waits, the phone check's count) and the racing ids test.
- [ ] **Step 3: Checks:**
  - Browser (Sports page): the card's marks name "not rated: too few starts to rate" and a sub-label; clicking its "View data" shows `table.lumen-grid-table` with `th[scope=row]` for each category; at a 375 × 812 phone context the card's viewport scrolls sideways (`scrollWidth > clientWidth`) and a cell's drawn width is ≥ 56 px (`getBoundingClientRect`); the existing axe sweeps cover it in light, dark and Midnight.
  - HTTP (`verify-api.ps1`): the Sports page prerenders `id="category-heatmap"` and an `aria-label` containing `not rated: too few starts to rate` (raw HTML encodes non-ASCII: "·" is `&#183;`, "—" is `&#8212;`).
- [ ] **Step 4: Run** build, unit, baseline (as expected above), gallery + verify-api + browser suite (report counts). Screenshots of the card at 1400 (light, dark, Midnight) and 375 to `C:/Users/jacqu/AppData/Local/Temp/lumen046/`.
- [ ] **Step 5: Commit** "Hash heatmap tables and show a category heatmap on the Sports & performance page" with the trailer.

---

### Task 5: Documentation, recipe and version

- [ ] `Directory.Build.props` → 0.46.0.
- [ ] README: a "Heatmap tables" section (CellText, sub-labels per cell, NotRated, YFormat/YUnit on heatmaps, CellWidth and scroll, HtmlTable, the component's grid, CSV column), the legend wording fix and dark pair (refined only; classic unchanged), `## 0.46.0 additions`, limits.
- [ ] `references/api.md`, `SKILL.md` (frontmatter ≤ 1024 characters, no unquoted `: `).
- [ ] `references/recipes-race-face.md`: "Category heatmap" (#16), invented data, compiled by `tests/Lumen.Charts.Recipes` (a `csharp` block that builds `new ChartSpec`, so `check.py` counts it).
- [ ] `docs/VERIFICATION.md`: 0.46.0 paragraphs with measured counts (unit, HTTP, browser gallery and host, baseline rows: 388 + 4 added, 4 changed by design, recipes).
- [ ] Build, unit, recipe check; commit "Document heatmap tables, their recipe and 0.46.0" with the trailer.

---

### Task 6: Release (the controller)

Final review, one fix wave, merge on the owner's word; verify on master; **commit the release first, then pack** (so the packages carry the tagged commit); refresh `artifacts/SHA256.json`; push, CI, tag, GitHub release with the three `.nupkg`; skill; Race Face message; brain; HANDOVER; worktree cleanup.
