# Counts per day limits (0.47.2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove 0.47.0's four remaining limits:
- "View data" shows the latest 500 rows when a chart opens at its end;
- a zero day has a 24-unit tap target;
- a fixed-pitch column chart's right-hand axis title stands beside its ticks under the 320 floor;
- the upright margin rule is written once.

**Architecture:**
- One condition in `LumenChart.razor` (the flat data table).
- Three changes in `ChartSvg.cs`: a hit-area rect inside a hairline zero's `lumen-datum` group, the right-hand title X for fixed-pitch columns, and one margin function shared by `Framed` and `DrawnWidth`.
- Tests in the unit suite, the baseline harness and the Playwright suite. Docs and the version come last.

**Tech Stack:** C# / .NET 8 (library), .NET 10 (gallery, tests), Blazor Server, Playwright for .NET.

**Spec:** `docs/superpowers/specs/2026-10-28-counts-limits-design.md` is the binding authority. Read its section for your task.

## Global Constraints

- Invented data only.
- Never colour alone; drawn text clears 4.5:1.
- No new spec property and no new public member, so the gradient-ID hash does not move.
- **Baseline** (`tests/Lumen.Charts.Baseline`, 409 rows today in each of `reference/refined.txt` and `reference/classic.txt`):
  - Only rows whose drawing has a `MarkZero` zero may change (Task 2).
  - Rows may only be added for the right-hand titles (Task 3).
  - Every other row stays identical in both finishes.
  - Each task that changes the references lists every moved and added row name in its report.
- The static SVG changes only by Task 2's hit area and Task 3's moved title.
- A Release build shows 0 warnings.
- **Processes:**
  - Stop `Lumen.Gallery.exe` only by image name (`taskkill //IM Lumen.Gallery.exe //F`), and do it before every build, since it locks the output.
  - Stop every other process only by PID: `netstat -ano`, then `taskkill //PID <pid> //F`.
- `tests/Lumen.Charts.BrowserTests` and `samples/Lumen.Wasm` are outside the solution: build them explicitly before running them.
- **The Race Face brief:** never open, read or search `lumen-charts-race-face-brief.md` in the repo root. Search only `src/`, `tests/`, `samples/`, `docs/` and `integrations/`.
- **Git:** commit with explicit paths, never `git add .` or `-A`. Never push, merge, rebase or switch branches.
- **Line endings:** keep LF in every file (`git diff --check`). Python edits use `newline="\n"`.
- **Culture:** the machine culture is en-ZA, so format with `CultureInfo.InvariantCulture`.
- **Leftovers:** delete any `.playwright-mcp` folder; save screenshots under `%TEMP%\lumen0472\`.
- **SKILL.md:** its frontmatter stays byte-identical.

## Review Focus

1. **Two zero days side by side.** Their hit areas must not overlap, so a tap picks one day. Test: Task 2, step 1.
2. **A column chart whose values are all 0 or below.** Its baseline is at the plot's top, so the hit area runs down from it and stays inside the plot. Test: Task 2, step 1.
3. **Focus on a zero day.** The ring stays on the hairline (the hit area has `stroke='none'`), as in 0.47.1. Test: Task 2, step 1.
4. **A `ScrollToEnd` chart with series of different lengths.** Each series shows its own last 500, and a series of 50 shows all 50. Test: Task 1, step 1.
5. **A fixed-pitch column chart wide enough not to meet the floor, with `YAxisSide.Right`.** Its title stays at `Width − 16`. Test: Task 3, step 1.

---

### Task 1: View data shows the last 500 when the chart opens at its end

**Files:**
- Modify: `src/Lumen.Charts.Blazor/LumenChart.razor:48-58` (the flat table branch).
- Test: `tests/Lumen.Charts.Tests/Program.cs`, a new `// 0.47.2` block after the "Gallery polish" test and before `string ChartMarkup(`.
- Test: `tests/Lumen.Charts.BrowserTests/Program.cs`, in the sign-ups card's checks.

**Interfaces:**
- Consumes: the existing `ScrollToEnd` parameter, and the tests' `DailyCounts(int days)` helper and `showData` reflection pattern (as at about line 1926).
- Produces: nothing other tasks use.

- [ ] **Step 1: Write the failing unit tests**
  - **"0.47.2: with ScrollToEnd, View data lists each series' last 500 observations and says so".**
    - Open the data of a 600-day `DailyCounts` chart rendered with `ScrollToEnd = true`.
    - The table has 500 body rows.
    - The first body row's category is day 101's full date and the last row's is day 600's.
    - The note reads exactly `Table shows the last 500 observations per series. CSV contains all original observations.`
    - The caption still counts 600 observations.
  - **"0.47.2: without ScrollToEnd, View data keeps the first 500 and the old note".** The same chart without `ScrollToEnd`: the first row is day 1, and the note is `Table shows up to 500 observations per series. CSV contains all original observations.`
  - **Review Focus 4:** a column chart with `ScrollToEnd`, series A of 600 points and series B of 50 on the same X. A's rows are its points 101–600; B's rows are all 50.
- [ ] **Step 2: Run the tests and check the new ones fail.** Run `taskkill //IM Lumen.Gallery.exe //F`, then `dotnet run --project tests/Lumen.Charts.Tests -c Release`. Expected: the three new tests FAIL; everything else passes.
- [ ] **Step 3: Implement.** In the flat table branch, take `series.Points.TakeLast(500)` when `ScrollToEnd` is true, else `Take(500)`, and choose the note's text by the same condition.
- [ ] **Step 4: Run the unit tests.** Expected: all pass.
- [ ] **Step 5: Browser check.** On the Sign-ups per day card at 1280×900, after "View data" is pressed, the table's note contains `the last 500 observations` and its last body row starts with `Mon 2 Nov 2026`. Add a SKIP line where the host has no `#sign-ups` card. Build the browser tests, start the gallery as in `HANDOVER.md`, and run the suite. Expected: the new check passes and the old ones still pass.
- [ ] **Step 6: Commit** `LumenChart.razor` and both test files.

### Task 2: A 24-unit tap target for a zero day

**Files:**
- Modify: `src/Lumen.Charts/ChartSvg.cs`, at the two hairline sites: about 1119–1122 (category columns, including bar tracks) and 1137–1140 (columns on a continuous X).
- Test: `tests/Lumen.Charts.Tests/Program.cs`, in the `// 0.47.2` block.
- Test: `tests/Lumen.Charts.BrowserTests/Program.cs`, in the sign-ups card's phone checks.
- Modify: `tests/Lumen.Charts.Baseline/reference/refined.txt` and `reference/classic.txt`, for the moved rows only.

**Interfaces:**
- Produces: `private const double ZeroReach = 24;` and `private static string ZeroTarget(double x, double width, double baseline, double top, double bottom)` in `ChartSvg.cs`. It returns `<rect x='…' y='…' width='…' height='…' fill='transparent' stroke='none'/>`. The rect starts at `max(top, baseline − ZeroReach)` and is `min(ZeroReach, bottom − top)` high, moved up if needed so it never passes `bottom`. Coordinates are written with `N(…)` as the hairline's are.

- [ ] **Step 1: Write the failing unit tests.** Use a column chart with `MarkZero = true` and values `[0, 5, 0, 0, 8]`, read with `XElement`.
  - **"0.47.2: a zero's mark holds its hairline and then a transparent, unstroked hit area 24 high on the baseline, as wide as the hairline".**
    - In the group for point 0, the first rect is the 1-high hairline and the second has `fill='transparent'`, `stroke='none'`, the hairline's `x` and `width`, and `height='24'`.
    - The hit area's `y + height` equals the hairline's `y + 1`.
  - **"0.47.2: a bar that is not zero has no hit area".** Point 1's group holds exactly one rect, or one path, as before.
  - **Review Focus 1:** the hit areas of points 2 and 3 have disjoint X ranges.
  - **Review Focus 2:** for values `[0, -5, 0]` with `YMax = 0` (the baseline at the plot's top), point 0's hit area is 24 high, its top is at the baseline (the hairline's `y + 1`), and it ends inside the plot.
  - **Review Focus 3:** this is covered by the `stroke='none'` assertion above.
  - **Bar track:** in the same test style, a `BarTrack` chart with a zero (as in the baseline row `columns/track-mark-zero`) also gets the hit area.
- [ ] **Step 2: Run the unit tests and check the new ones fail.**
- [ ] **Step 3: Implement `ZeroTarget`** and append its rect after the hairline in both `shape` strings, using the pane's `top` and `bottom` at each site.
- [ ] **Step 4: Run the unit tests.** Expected: all pass.
- [ ] **Step 5: Baseline.**
  - Run `tests/Lumen.Charts.Baseline` in both finishes, as `HANDOVER.md` describes, and diff each output against its reference.
  - Expected: only `MarkZero` rows differ, such as `columns/mark-zero`, `columns/track-mark-zero` and the `daily-counts/*` rows that hold a zero.
  - If any other row differs, stop and report it.
  - Update both references with the moved rows only, and list them.
- [ ] **Step 6: Browser check** at 375×812 with `IsMobile` and `HasTouch`, in the style of the phone tests near line 670.
  - Scroll a zero day of the sign-ups card into view (its `aria-label` contains `: 0 registrations`).
  - Tap 12 CSS pixels above its hairline's bottom edge, centred on the bar.
  - Check that the card's tooltip or status shows that day's name.
  - Run the gallery suite, and the WebAssembly host's if it draws a counts chart (else a SKIP line).
- [ ] **Step 7: Commit** `ChartSvg.cs`, the two references and both test files.

### Task 3: The right-hand titles under the floor, and one margin rule

**Files:**
- Modify: `src/Lumen.Charts/ChartSvg.cs`:
  - `YTitle` (about 1375–1379) and its callers;
  - the pane's Y2 title (about 804);
  - `Framed` (about 573–600);
  - `ColumnMargins` and `DrawnWidth` (about 3180–3203), and the `cref` to `ColumnMargins` in the doc comment at about 1189.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, in the `// 0.47.2` block.
- Modify: `tests/Lumen.Charts.Baseline/Program.cs`, adding rows after `columns/track-mark-zero`, and both references.

**Interfaces:**
- Produces:
  - `private static (double Left, double Right) Margins(ChartSpec s, double endingWidth)`, which replaces `ColumnMargins`. It returns the left and right margins of an upright, non-sparkline chart on X and Y axes:
    - left is 30 with the Y axis on the right or no labels up the left, else 76;
    - right is 76 beside a secondary or right-hand axis, else the larger of 30 and the end labels' room, capped by `endingWidth`.
  - `Framed` uses it for upright charts with `s.Width`, and `DrawnWidth` with `MaxWidth`.
  - `YTitle` gains a `double right` parameter.

- [ ] **Step 1: Write the failing unit tests.**
  - **"0.47.2: under the 320 floor a fixed-pitch column chart's right-hand Y title stands 60 right of its plot, beside its ticks".**
    - A column chart with `CellWidth = 30`, 3 categories, `YAxisSide.Right` and `YLabel = "Count"`.
    - The plot ends at 30 + 3 × 30 = 120, and the title's X is 180, not 304.
    - The Y tick labels start at 132.
  - **The same for a pane's Y2 title**, if a column chart with `CellWidth` and a secondary series validates. If it does not, assert the refusal and note it in the report.
  - **Review Focus 5:** the same chart with 12 categories is 30 + 12 × 30 + 76 = 466 wide and has its title at 450, which is `Width − 16`.
- [ ] **Step 2: Run the unit tests and check the new ones fail.**
- [ ] **Step 3: Implement the titles.**
  - On a column chart with `CellWidth`, the right-hand Y title and the Y2 title stand at `right + 60`.
  - Every other chart keeps `s.Width − 16` exactly (spec §2.3).
- [ ] **Step 4: Implement `Margins`.**
  - Write `Margins`, call it from `Framed`'s upright branch and from `DrawnWidth`, and delete `ColumnMargins`.
  - The horizontal and sparkline branches of `Framed` are unchanged.
- [ ] **Step 5: Run the unit tests.** Expected: all pass.
- [ ] **Step 6: Baseline.**
  - Add the rows `columns/cell-width-right-title` (the 3-category chart above, in Light) and, where allowed, `columns/cell-width-y2-title`.
  - Run both finishes.
  - Expected: every row is identical to the references Task 2 left, and only the new rows are added (410 or 411). Report the count.
  - Update the references with the added rows.
- [ ] **Step 7: Commit** `ChartSvg.cs`, the baseline `Program.cs`, both references and the unit tests.

### Task 4: Docs and the version

**Files:**
- Modify: `Directory.Build.props`: `0.47.1` → `0.47.2`.
- Modify: `README.md`:
  - a "## 0.47.2 additions" section above "## 0.47.1 additions", in its style: what changed, which baseline rows moved or were added and why, and that the classic finish is unchanged for every spec 0.23.0 could draw;
  - the last three sentences of 0.47.1's "Limits" paragraph, rewritten to say what is true now, keeping that a chart without `ScrollToEnd` shows its first 500;
  - the Counts per day and Blazor sections, where they describe these behaviours.
- Modify: `integrations/claude-code/lumen-charts/references/api.md`, the `MarkZero` row (about 102) and the data-table and `ScrollToEnd` lines (about 252 and 257).
- Modify: `integrations/claude-code/lumen-charts/SKILL.md`, the body only (the counts line at about 99, if it names these limits).
- Modify: `docs/VERIFICATION.md` (0.47.2's counts) and `tests/Lumen.Charts.Baseline/README.md` (the row count).

- [ ] **Step 1:** Make the edits. Every count you write comes from a run you made, not from this plan.
- [ ] **Step 2:** Run `git diff --check`, and confirm that SKILL.md's frontmatter is unchanged (`git diff` of its first lines is empty).
- [ ] **Step 3:** Run the Release build (0 warnings), the unit tests, both baselines, verify-api, the gallery browser suite, the WebAssembly host's browser suite, and the recipes (`python check.py`, then `dotnet run -c Release`). Report each count.
- [ ] **Step 4: Commit** the docs and the version by explicit paths.
