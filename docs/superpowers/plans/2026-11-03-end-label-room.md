# Room for long end-label names (0.53.1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Under `ChartSpec.WrapEndNotes`, give a long end-label name more room at narrow widths, so "Rider name" is whole at 320 and its note goes under it. Also fold in two recipe follow-ups: `#nullable enable` in the recipe check, and the 260 threshold in the raster recipe. Release as 0.53.1.

**Architecture:**
- **One function changes:** `Ending` (`ChartSvg.cs:1289–1300`). Under the switch its cap rises from half the drawing to the widest name's own ask, bounded so the plot keeps two fifths of the drawing.
- **`Ends`, `Spread`, the passes and the two-line writing are untouched.** They read the wider room through `Slot`.
- **Off, or with every name under the half cap,** `Ending` returns 0.53.0's double, so those drawings are byte-identical.
- **The proof** is a new `probe ends` mode compared with `v0.53.0`.

**Tech stack:**
- C# / .NET 8 for the libraries and .NET 10 for the gallery and tests.
- The executable test runner in `tests/Lumen.Charts.Tests`.
- The baseline and probe in `tests/Lumen.Charts.Baseline`.
- Python 3 for `tests/Lumen.Charts.Recipes/check.py`.
- Playwright for the visual look.

**Spec:** `docs/superpowers/specs/2026-11-03-end-label-room-design.md`. It is the binding authority. Line numbers are master at 8027260.

## Global Constraints

- **Baseline:** 441 rows in each finish, as in 0.53.0.
  - The 433 rows of 0.52.0 and the six non-`ends/*` 0.53.0 rows stay byte-identical in both finishes.
  - Of the two `ends/*` rows, only `ends/note-below-320` changes. `ends/note-below-crowded` stays byte-identical: its names are under the 94 half cap at 340. The brief expected both to change, and the spec corrects it (§4).
  - No row is added, removed or renamed.
  - Tasks 1 and 2 leave `tests/Lumen.Charts.Baseline/reference/*.txt` alone. Task 3 replaces them with the verified run.
- **Off is byte-identical:**
  - With `WrapEndNotes` false, `Ending` returns `Math.Min(need, cap)`, the expression it returns today.
  - `probe lines` (`Probe.cs:122–213`), run here and in a `v0.53.0` worktree, gives equal files.
- **On, with no name over the half cap, is byte-identical too.** `probe ends` (Task 1) is equal on every untouched line.
- **Comparing with `v0.53.0`:**
  1. `git worktree add <short path> v0.53.0`.
  2. Copy **only** `tests/Lumen.Charts.Baseline/Probe.cs` over the worktree's copy.
  3. Run the mode in both trees.
  4. Compare as each mode says.
  5. Remove the worktree.
- **No public API change:**
  - no new property, type or public constant;
  - the gradient-ID hash (`Unfitted`, `ChartSvg.cs:110–126`) is untouched;
  - `verify-api.ps1` stays at 338 checks.
- **Build:** the Release build has 0 warnings.
- **Accessibility:** every end label stays inside `Width − 4`. A cut label keeps `role='img'` and its whole words as its name and `<title>`.
- **Data:** invented data only.
- **SKILL.md:** unchanged. Its frontmatter is byte-identical in any case.
- **The brief:** never open `lumen-charts-race-face-brief.md`.
- **Git:** stage explicit file paths only.
- **The gallery locks the build:** stop `Lumen.Gallery` before the first build of a session.

## Review Focus

1. **A long name in a lower pane only.** The margin is one for the drawing, so pane 0's plot narrows by the same amount, and the two clips' right edges are equal. Test: Task 1.
2. **`HighlightLast`.** `Lead` is 14 (`ChartSvg.cs:1279`), so the name's ask is 6 more: 97.92 for "Rider name" at 320. It is still whole, and its `x` is the plot's right edge plus 14. Test: Task 1.
3. **A pane too thin for a second line.** The thin-pane guard (`:1419–1421`) keeps one line, now against the wider room. "Rider name" reads whole with its note left out, not "Rider na…". Test: Task 1.
4. **A column chart with `CellWidth` and a labelled line series.** `Margins` passes `Width = MaxWidth` (`:3404`), where the half cap never bites. It must draw as in 0.53.0. Test: `probe ends` (no `CellWidth` chart touched, every one equal), Task 1.
5. **A label that moves off its point after the margin rises.** Its room is `margin − 18`, which equals `Broad(name)` exactly when the margin is the name's ask. It must stay whole, within `Ends`' `1e-6` tolerance (`:1418`, `:1442`). Test: Task 1's changed second-pass test, where "Other rider" moves and must read whole.

---

### Task 1: The margin rule

**Files:**
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - `Ending` (1289–1300) and its `<summary>` (1284–1288);
  - one internal constant beside `EndPad` (1274), with its `<summary>` sentence.
- Modify `src/Lumen.Charts/Models.cs:878–884`: `WrapEndNotes`' doc comment. Its sentence "where that leaves the plot at least half the drawing (0.53.0)" gains the 0.53.1 rule.
- Modify `tests/Lumen.Charts.Tests/Program.cs`:
  - change three 0.53.0 tests: 13341, 13353 and 13453, with `Second53` at 13451–13452;
  - add a `// 0.53.1: room for long end-label names` block after the thin-pane test (13465–13480), before 13481.
- Modify `tests/Lumen.Charts.Baseline/Probe.cs`:
  - the `ends` mode, dispatched beside `lines` (11);
  - its comment, in the style of 122–124.
- No change to `tests/Lumen.Charts.Baseline/Program.cs`. The `ends/note-below-320` row (1002–1003) keeps its name and spec; only its hash changes.

**Interfaces:**
- **Produces** `private const double EndPlot = 2d / 5;`, the share of the drawing the plot keeps under `WrapEndNotes`.
  - Write the floor as `2d * s.Width / 5`, not `s.Width * EndPlot`, so it is exact at whole widths: 128, 136, 144.
  - The constant documents the share; the expression uses the exact form. Or drop the constant and say "two fifths" in `Ending`'s summary. Either is fine; do not add both.
- **`Ending(ChartSpec s, double left)`, same signature:**
  - Keep `need` and `cap` as they are (1291–1298).
  - In the same loop, `names = Math.Max(names, Lead(series) + EndShift + Broad(series.EndLabel) + EndPad)`.
  - `if (!s.WrapEndNotes) return Math.Min(need, cap);`, the 0.53.0 expression.
  - `var wide = s.Width - left - 2d * s.Width / 5;`
  - `return Math.Min(need, Math.Max(cap, Math.Min(names, wide)));`
  - The summary says:
    - the cap rises, under the switch (0.53.1), to the widest name's own ask, its lead, step and pad included;
    - it never rises past the margin that leaves the plot two fifths of the drawing;
    - notes never raise it;
    - where no name asks more than half, the result is 0.53.0's.
  - Keep the `Margins` / `MaxWidth` sentence (1287–1288). It still holds: the half cap is 1972 there.
- **Produces** the probe mode `ends <out.txt>`, run as `dotnet run -c Release -- probe ends out.txt`:
  - `new Random(531)`, 10,000 charts, using only 0.53.0 API.
  - The generator is `Lines`' (125–213) with these differences. Factor out the shared part only if it keeps `lines`' output identical; it is compared with `v0.53.0` running the same file, so a shared helper is fine.
    - `WrapEndNotes = true` on every spec.
    - Every non-column series has an `EndLabel` of 1–24 letters from `"EaRmw "` (spaces not leading or trailing), so `m` and `w` (.9 em) occur.
    - Notes on none, some or all, as `Lines`.
    - `Width` is 320–700.
    - A quarter of the column charts set `Kind = ChartKind.Column` and `CellWidth = 30 + random.Next(30)`. The line series stays `Kind = Line`.
  - It writes `i hash`, or `i refused`. It appends ` touched` where the spec has no `CellWidth` and some series with an `EndLabel` has `(HighlightLast ? 14 : 8) + 6 + B(EndLabel) + 4 > Width - 76 - Width / 2d`.
    - `B` is a local copy of `ChartSvg.Broad`: per character `.3` for `. , : space`, `.36` for `-`, `.9` for `m M w W`, else `.62`, summed, times 12.
    - `left` is 76 for every chart the probe draws: Y axis on the left, pane titles on the axis, no secondary series.
  - The comment states the contract: untouched lines must equal `v0.53.0`'s; touched lines may differ.

- [ ] **Step 1: Change the three 0.53.0 tests to the new rule.** Keep their names; change their assertions and comments:
  - **"End notes 0.53.0: at 320 the note goes whole on a second line" (13341):**
    - `blocks.Count==2`;
    - exactly one block has a `Note`: Rider's, `+12.3s`;
    - "Other" with "+4.1s" (74.16) now fits the 79.92 it has, on one line;
    - Rider's label text is `Rider name`, with no `…`.
  - **"End notes 0.53.0: the plot widens where the label alone fits under the cap" (13353):**
    - the `Rider` line is unchanged;
    - with `Rider name`, `PlotRight53(on) - PlotRight53(off)` is `-7.92` within `0.01`, since the margin is 91.92, not 84;
    - fix the comment at 13356, which says 91.9 "meets the cap, so the plot is as wide as today".
  - **"End notes 0.53.0: a second pass marks another label" (13453), with `Second53` (13451–13452):**
    - B becomes `EndLabel="Anna"`, `EndNote="+123.4s"`. That is 29.76 + 3.6 + 48.24 = 81.6.
    - Rewrite the comment at 13449–13450 and the test's own comments. "Other rider" (78) now asks a margin of 96, so a label has 84 where it stands and 78 once it has moved. 81.6 fits 84 but not 78, so B needs the second pass.
    - The `alone` check: `X53(b.Text)==232` in place of 244.
    - The two-line check: `X53(b.Text)==238` for both, in place of 250.
    - The `Y` values: pin from the run. Expect `122,150`, since the vertical geometry is the 0.53.0 test's.
    - Add: A's label text is `Other rider`, whole, though it moved: its room is exactly 78. This is Review Focus 5.
- [ ] **Step 2: Write the failing 0.53.1 tests.** Use `Wrap53(width, wrap, name, note, other:false)` (13336–13337), so one series sets the margin. Its last point stands at the plot's right edge and never moves.
  - **"End label room 0.53.1: at 320 'Rider name' is whole with its note under it":**
    - on: the label text is exactly `Rider name`, its `x` is `236.08`, and its note `tspan` with `dy='14'` reads `+12.3s`;
    - off, the 0.53.0 cut: the label starts `Rider na` and ends `…`;
    - `Outside52` finds nothing.
  - **"End label room 0.53.1: at 340 and 360 'Rider name' keeps 0.53.0's margin":** with the switch on, the label's `x` is `256.08` at 340 and `276.08` at 360. Those are 0.53.0's: its margin was already 91.92, under the half caps of 94 and 104.
  - **"End label room 0.53.1: a 14-letter name at 320, 340 and 360":** `Vandermeulenko` with `+12.3s`.
    - 320: text `Vandermeulen…`, `x` 212;
    - 340: text `Vandermeulenko`, `x` 222.48;
    - 360: text `Vandermeulenko`, `x` 242.48;
    - each note is whole under it;
    - off, at the same widths: `Vanderme…`, `Vandermeu…`, `Vandermeul…`.
  - **"End label room 0.53.1: the plot keeps two fifths of the drawing":**
    - a 24-letter name (`new string('E', 24)`) at 320: `PlotRight53(on) - PlotRight53(off)` is `-32` within `0.01` (116 against 84);
    - its label ends `…`, and its `x + Broad(text) <= 316`.
  - **"End label room 0.53.1: a note never raises the margin":** at 320, `Ann` with `+12.3s behind the leader` and `Ann` with `+1s`, both on. Their `PlotRight53` values are equal.
  - **"End label room 0.53.1: a highlighted last point" (Review Focus 2):**
    - `Rider name` at 320 with `HighlightLast = true` on its series;
    - the label is whole and its `x` is `PlotRight53 + 14`;
    - `320 - PlotRight53` is `97.92` within `0.01`.
  - **"End label room 0.53.1: a long name in a lower pane widens every pane" (Review Focus 1):**
    - 320 × 420, one `Panes` entry;
    - pane 0 has an unlabelled series; pane 1 has `Rider name` / `+12.3s`;
    - the two clips' right edges are equal, and each is 7.92 left of the same chart's with the switch off.
  - **"End label room 0.53.1: a thin pane keeps one line, now whole" (Review Focus 3):**
    - the thin-pane chart of 13469–13470 at weight `.05`, both finishes;
    - the label reads exactly `Rider name`, with no note `tspan`, `role='img'` and `aria-label='Rider name +12.3s'`;
    - `Outside52` finds nothing.
- [ ] **Step 3: Build and run the unit tests:** `dotnet run -c Release --project tests/Lumen.Charts.Tests`. Expected: the changed and new tests FAIL against 0.53.0's `Ending`, each on its new value (for example `x` 244 where 236.08 is expected). Every other test passes.
- [ ] **Step 4: Implement** `Ending`, the constant or summary wording, and the two doc comments, as in Interfaces.
- [ ] **Step 5: Run the unit tests.** Expected: all pass, including 0.53.0's pane test (13397) and its "note wider than its line" test (13382), whose assertions do not change. If either fails, the rule is wrong, not the test: stop and report.
- [ ] **Step 6: Add `probe ends`, and run both probes** here and in a `v0.53.0` worktree, as Global Constraints says.
  - `probe lines`: the two files are equal, line for line.
  - `probe ends`:
    - every line without ` touched` is equal;
    - at least one touched line differs;
    - the counts of touched and of differing lines go in the report;
    - no line of a `CellWidth` chart is touched.
- [ ] **Step 7: Run both baselines.**
  - `dotnet run -c Release` and `dotnet run -c Release -- classic` in `tests/Lumen.Charts.Baseline` each write 441 rows.
  - Diffed against `reference/*.txt`, the only change in each is `ends/note-below-320`. `ends/note-below-crowded` is equal.
  - Write `svg-out`. Look at `ends/note-below-320` in Playwright at 320, 340 and 360 with a real viewport: "Rider name" and "Other rider" are whole, each note is under its name, and nothing runs past the drawing.
- [ ] **Step 8: Commit.** Stage `src/Lumen.Charts/ChartSvg.cs`, `src/Lumen.Charts/Models.cs`, `tests/Lumen.Charts.Tests/Program.cs` and `tests/Lumen.Charts.Baseline/Probe.cs`, then:

  ```bash
  git commit -m "0.53.1 end-label room: under WrapEndNotes the margin rises to the widest name, the plot keeping two fifths; probe ends"
  ```

### Task 2: The recipe follow-ups

**Files:**
- Modify `tests/Lumen.Charts.Recipes/check.py:26`: the program begins `'#nullable enable\n'`, before the `using` lines.
- Modify `tests/Lumen.Charts.Recipes/README.md`: one sentence. The check compiles with nullable analysis on: `check.py` writes `#nullable enable` at the top, since the compiler treats a `.g.cs` file as generated code and turns the analysis off in it.
- Modify, only where the run shows a failure and the fix is trivially correct:
  - `integrations/claude-code/lumen-charts/references/sports.md`;
  - `integrations/claude-code/lumen-charts/references/recipes-race-face.md`;
  - `tests/Lumen.Charts.Recipes/Inputs.txt`.
- Modify `integrations/claude-code/lumen-charts/references/recipes-race-face.md:1133`: the threshold sentence.

**Interfaces:**
- **Consumes** nothing from Task 1.
- **A trivially-correct fix** is one of these, and it keeps the recipe's meaning and its rendered SVG:
  - a `?` on a declaration that can hold null;
  - a `!` after a call whose own docs say it is not null where the recipe calls it, such as `Placings.Build` or `DailyCounts.Build` given points, which recipes already write as `Build(...)!`;
  - a null test the recipe's prose already implies.
- **Anything else is not edited.** Note its block, line and warning for the report.
- **The new sentence**, in place of "(190 with `DrawTitles` off; the legend is then taller than 210, about ten one-line rows of 22)":
  - "(190 with `DrawTitles` off): the legend is then taller than 210, about ten one-line rows of 22, or 260, about twelve, with `DrawTitles` off".
  - Keep the rest of the paragraph as it is.

- [ ] **Step 1: Write the failing check.** Add `#nullable enable` to `check.py`. Run `python check.py`, then `dotnet run -c Release` in `tests/Lumen.Charts.Recipes`.
  - Expected: it fails with nullable warnings as errors, or it passes. Either is information.
  - Record each `CS86xx` error: its file, the recipe block and the line.
- [ ] **Step 2: Fix the trivially-correct ones,** and only those, in the recipe source, never in `obj/Recipes.g.cs`. Rerun until only the non-trivial ones remain, or none.
- [ ] **Step 3: If any non-trivial failure remains, stop here and report it to the owner** with its block and warning. Do not commit a `check.py` that fails. Otherwise, confirm:
  - every recipe compiles and renders;
  - the chart count printed is 41 or what 0.53.0's last run printed (`docs/VERIFICATION.md:106`);
  - `obj/out/shareCardPng.png` is still 1080 × 1350;
  - each fixed recipe's SVG in `obj/out` is byte-identical to the one before the fix. Run `check.py` on master first and keep a copy of `obj/out`.
- [ ] **Step 4: Edit the threshold sentence** at `recipes-race-face.md:1133`, and the Recipes README sentence. Rerun the check: still passes.
- [ ] **Step 5: Commit.** Stage `tests/Lumen.Charts.Recipes/check.py`, `tests/Lumen.Charts.Recipes/README.md`, `integrations/claude-code/lumen-charts/references/recipes-race-face.md`, and any of `integrations/claude-code/lumen-charts/references/sports.md` and `tests/Lumen.Charts.Recipes/Inputs.txt` that changed, then:

  ```bash
  git commit -m "0.53.1 recipes: the check compiles with #nullable enable; the raster recipe's 190 floor needs a legend over 260"
  ```

### Task 3: Docs and the version

**Files:**
- Modify `Directory.Build.props:7` to `0.53.1`.
- Modify `README.md`:
  - **"## 0.53.1 additions"** above `## 0.53.0 additions` (1539). It says:
    - the rule, in one sentence;
    - the 320 / 340 / 360 numbers for "Rider name" and a 14-letter name, as the spec's table (§2.3);
    - what is still cut;
    - that off, and on with names under the half cap, nothing changes;
    - that `ends/note-below-320` is the only changed baseline row;
    - that the recipe check now runs with nullable analysis.
  - **1018, the end-label bullet:** replace "still capped at half the drawing: at 320 a name wider than its room is still cut" with the 0.53.1 rule, and say "Rider name" is whole at 320.
  - **1544, 0.53.0 additions:** leave the record as written, and add "(0.53.1 gives the name more room: see 0.53.1 additions)" after its limit sentence.
- Modify `integrations/claude-code/lumen-charts/references/api.md:107`, the `WrapEndNotes` row: replace "still capped at half the drawing, so at 320 a name wider than its room (`Rider name`, 73.9 against 72) is still cut" with the rule. "Rider name" is whole at 320, and a name past about 13 letters is still cut there (0.53.1).
- Modify `HANDOVER.md`:
  - **7:** the release line becomes v0.53.1, with this spec and plan. Keep 0.53.0's line under it as the previous release.
  - **8:** the open question and the two small follow-ups are done. "Next" says what is still open.
  - **71:** the feedback item. Under `WrapEndNotes`, a long name is whole at 320 from 0.53.1; a name past about 13 letters is still cut at 320.
- Modify `docs/VERIFICATION.md`:
  - a 0.53.1 paragraph above 7, in its voice;
  - the counts at 102–106, from your own runs: unit tests; HTTP, still 338; browser and WebAssembly, unchanged; recipes; baseline, 441 with one changed row.
  - Line 7's "(at 320 a name wider than its room is still cut)" gains "(0.53.1 gives it more room)".
- Modify `tests/Lumen.Charts.Baseline/README.md`:
  - 14: the reference is **v0.53.1**, 441 rows, 440 identical to v0.53.0, `ends/note-below-320` changed;
  - 10 and 22: the `probe ends` mode, and its touched-line rule.
- Replace `tests/Lumen.Charts.Baseline/reference/refined.txt` and `classic.txt` with the verified run.
- No change to `integrations/claude-code/lumen-charts/SKILL.md`. Its 0.53.0 line (105) does not state the limit.

**Interfaces:**
- **Consumes** Task 1's rule and numbers, and Task 2's outcome: which recipes were fixed, and any reported.

- [ ] **Step 1: Make the edits.** Every count comes from your own runs.
- [ ] **Step 2: Check the diff:**
  - `git diff --check` is clean;
  - `git grep -n "still capped at half\|cap is still half\|Rider name\`, 73.9 against 72"` finds no line that states the limit as current. 0.53.0's record at README 1544 may keep it, with its pointer.
  - `git diff --stat src/Lumen.Charts.Blazor integrations/claude-code/lumen-charts/SKILL.md` is empty.
- [ ] **Step 3: Run everything:**
  - the Release build: 0 warnings;
  - the unit tests;
  - both baselines: 441 rows each, only `ends/note-below-320` differing from `reference/*.txt` before you replace them;
  - `verify-api.ps1` against the gallery: 338;
  - the gallery browser suite and the WebAssembly suite, unchanged, both passing;
  - the recipe check.
- [ ] **Step 4: Replace the reference files** with the verified run. Rerun both baselines: no difference.
- [ ] **Step 5: Commit.** Stage each changed file by its path, then:

  ```bash
  git commit -m "0.53.1: README, api.md, HANDOVER, VERIFICATION, Baseline README and references, and the version"
  ```
