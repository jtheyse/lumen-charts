# View data as a table (0.51.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `<LumenChart>`'s flat "View data" table becomes a `<LumenTable>`. That gives it paging and sorting, and removes the 500-row cut. A new public builder, `ChartExport.DataTable(spec)`, returns the same table for servers and static pages.

**Architecture:**
- Move the component's private value and category wording (`Value`, `Category`, `Measure`, `FullRange` in `LumenChart.razor`) into an internal core helper, unchanged. The component and the new builder both use it, so the words exist once.
- `ChartExport.DataTable` builds a `TableSpec` from the chart.
- `<LumenChart>` writes `<LumenTable Spec="@ChartExport.DataTable(…)"/>` inside a plain `.lumen-table` wrapper.

**Tech stack:**
- C# / .NET 8 for the libraries and .NET 10 for the gallery and tests.
- Blazor Server and WebAssembly.
- Playwright for .NET with axe-core.

**Spec:** `docs/superpowers/specs/2026-11-01-view-data-table-design.md`, approved by the owner on 1 Nov 2026. It is the binding authority.

## Global Constraints

- **Baseline:** no rendering changes. The 426 baseline rows stay byte-identical in both finishes, and no row is added.
- **Heatmaps:** the heatmap's grid table (`ChartExport.HtmlTable`, with its "Chart data" region) is unchanged.
- **Wording:** the words of every value and category cell match today's `Value(series, point)` and `Category(point)` exactly, for every chart kind.
- **Public API:** every new public member has an XML doc comment, and the Release build shows 0 warnings. Validation errors are `ArgumentException`, in `ChartValidation`'s voice.
- **Data:** invented data only.
- **Process:** follow `how-to-run.md` in the SDD workspace for processes, git (explicit file paths), leftovers and the report contract. Never open `lumen-charts-race-face-brief.md`. The SKILL.md frontmatter stays byte-identical.

## Review Focus

1. **A Number value with no gaps:** a plain line chart with a `Duration` Y format and a unit sorts by value. Missing values go last, and a `GapLabel` becomes the missing cell's note. Test: Task 1.
2. **A chart whose series use different formats** (a secondary axis with its own `Y2Format`, or panes with their own `YFormat`) falls back to a Text value that cannot be sorted, keeping today's words. Test: Task 1.
3. **Exactly 100 rows** gets no `PageSize`, and 101 rows gets `PageSize` 100. With `ScrollToEnd`, the table opens on the last page. Test: Tasks 1 and 2.
4. **Every chart kind in the gallery opens View data without an error**, with axe clean in Light and Dark. Test: Task 2.
5. **A legend toggle followed by View data** still lists every series, as before. Test: Task 2.

---

### Task 1: The shared wording and `ChartExport.DataTable`

**Files:**
- Create: `src/Lumen.Charts/ChartData.cs`, holding `internal static class ChartData`.
- Modify: `src/Lumen.Charts/ChartExport.cs`, adding `public static TableSpec DataTable(ChartSpec spec)`.
- Modify: `src/Lumen.Charts.Blazor/LumenChart.razor`. Its private `Value` and `Category` call `ChartData` (or are deleted, with callers moved over), and its `Measure`/`FullRange` use moves to `ChartData`. The component's zoom code keeps its own `FullRange` if it needs it. The component's output must not change: the status line and the old table still say the same words.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, a `// 0.51.0` block before `string ChartMarkup(`.

**Interfaces:**
- **Produces** `internal static class ChartData`:
  - `static Axis XAxisOf(ChartSpec spec)`: today's `FullRange`, the X axis over every point and every `XEnd`, with `XMin`/`XMax`, the zone, weekends, skips and `XFormat`;
  - `static Axis Measure(ChartSpec spec, ChartSeries series)`: today's `Measure`, including panes, the secondary axis, formats and units;
  - `static string Category(ChartSpec spec, Axis x, ChartPoint point)`: today's `Category`, including the sub-label rule;
  - `static string Value(ChartSpec spec, Axis x, ChartSeries series, ChartPoint point)`: today's `Value` switch, unchanged.
- **Produces** `public static TableSpec ChartExport.DataTable(ChartSpec spec)`:
  - It validates first (`ChartValidation.Validate`).
  - A heatmap is refused with `ArgumentException("A heatmap's data is a grid; use HtmlTable for it.")`, worded in the house voice.
  - It writes the columns, caption, paging and value rules exactly as spec §2:
    - **Series:** a Text column, sortable.
    - **X / Category:** a Text column, `RowHeader = true`, `Sortable = false`.
    - **Value:** a Number column with `Format`/`Unit` from `Measure` when every series is "plain" and all series share one `Measure` format and unit. Otherwise a Text column with `Sortable = false`.
      - A series is "plain" when its mark (`series.Kind ?? spec.Kind`) is not Candlestick, Ohlc, Ring, Timeline, Blocks or Range, and no point has `Low`/`High`, `Open`/`High`/`Low`/`Close` or `XEnd`.
      - In a Number column, `Y` goes in `Number`. `ValueNote`, or the `GapLabel` (plus `ValueNote`) when `Y` is null, goes in `Note`.
  - **Caption:** `{Title} — original data`, or `Chart data — original data` when the title is blank. If that is longer than 120 characters, cut it to 119 and add `…`.
  - **Paging:** `PageSize = rows > 100 ? 100 : null`, and `StartAtEnd = false`. The component sets it.
  - **Style:** `spec.Style`, else the preset for `spec.Theme` (the same resolution `ChartSvg.ResolveStyle` uses).
  - **Rows:** series by series, points in their order.

- [ ] **Step 1: Write the failing unit tests:**
  - **"DataTable for a 600-point line chart"**: the column kinds, the row header, which columns sort, the Number format and unit, the notes, the caption, `PageSize` 100 and the row order.
  - **Review Focus 1.**
  - **Review Focus 2** (a secondary axis, and panes with different formats).
  - **Text values** for candlestick, range, timeline, ring and blocks charts, equal to the strings today's component writes for them. Pin one exact string for each kind.
  - **Review Focus 3:** 100 and 101 rows.
  - **A long title** is cut to 120; **a blank title** gives `Chart data — original data`.
  - **A heatmap is refused.**
  - **A guard that the component's words are unchanged:** a prerendered `<LumenChart>` with the data open (use the existing `showData` reflection pattern) shows the same value and category texts as before, for one plain chart and one candlestick chart.
- [ ] **Step 2: Run the tests and check they fail.**
- [ ] **Step 3: Implement.** Move the code; do not rewrite it.
- [ ] **Step 4: Run the unit tests and both baselines.** All tests pass, and the 426 rows are identical.
- [ ] **Step 5: Commit.**

### Task 2: `<LumenChart>` uses `<LumenTable>`

**Files:**
- Modify: `src/Lumen.Charts.Blazor/LumenChart.razor`, in the flat `showData` branch.
- Modify: `tests/Lumen.Charts.Tests/Program.cs`. Update the about 24 uses that read the old table: `Table shows …`, the old `<caption>` or the `<td>`s. Keep what each test proves, and say in the report which tests changed and why.
- Modify: `tests/Lumen.Charts.BrowserTests/Program.cs`. Update the uses of the old table (the 0.47.2 sign-ups check near line 1941, and any test that reads the flat table) and add new tests.

**Interfaces:**
- Consumes `ChartExport.DataTable` and `<LumenTable>`.
- The flat branch becomes `<div class="lumen-table"><LumenTable Spec="@DataSpec"/></div>`. The wrapper is plain, with no `role`, `tabindex` or `aria-label`.
- `DataSpec` is `ChartExport.DataTable(Spec) with { Style = Style, StartAtEnd = ScrollToEnd }`. Here `Style` is the component's resolved style. Cache it per `Spec` instance (`ReferenceEquals`), so a re-render does not rebuild a 100,000-row table, and `<LumenTable>`'s own view cache keeps working.
- `ShowExport` stays off, because the chart's toolbar already has "Export CSV".
- The heatmap branch is unchanged.

- [ ] **Step 1: Write the failing tests.**
  - **Unit tests:**
    - The prerendered View data (data open) contains `class='lumen-datatable` inside `class="lumen-table"`, and the wrapper has no `role` or `tabindex`.
    - With `ScrollToEnd`, a 600-day chart's table shows `Rows 501–600 of 600`.
    - A legend toggle that hides a series still leaves that series' rows in the table (Review Focus 5).
  - **Browser tests:**
    - The Sign-ups per day card's View data opens on its last page, its last row is `Mon 2 Nov 2026`, and "Previous page" moves back with focus kept.
    - On the explorer, every chart kind's View data opens without a console error (Review Focus 4).
    - Axe passes with a chart's data open, in Light and Dark.
- [ ] **Step 2: Build and run the tests, and check the new ones fail.**
- [ ] **Step 3: Implement, and update the old tests.**
- [ ] **Step 4: Run the full suites:**
  - the unit tests;
  - verify-api;
  - the gallery browser suite;
  - the WebAssembly suite (the component changed);
  - both baselines, unchanged.
- [ ] **Step 5: Commit.**

### Task 3: Docs and the version

**Files:**
- Modify: `Directory.Build.props` to 0.51.0.
- Modify: README:
  - the Blazor section's View data text;
  - the 0.47.2 and 0.47.1 limit lines about the 500 cut, which now say what is true;
  - a "## 0.51.0 additions" section;
  - the `ChartExport` section (`DataTable`).
- Modify: `api.md`, `SKILL.md` (one body line), VERIFICATION and the Baseline README (row count unchanged).

- [ ] **Step 1: Make the edits.** Every count comes from your own runs.
- [ ] **Step 2: Check the diff.** Run `git diff --check`; the SKILL frontmatter is unchanged.
- [ ] **Step 3: Run everything:**
  - the Release build (0 warnings);
  - the unit tests;
  - both baselines (426, identical);
  - verify-api;
  - the gallery suite;
  - the WebAssembly suite;
  - the recipes.
- [ ] **Step 4: Commit.**
