# Look changes (0.53.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give Race Face the four look changes it asked for, each an opt-in `ChartSpec` switch that leaves every existing drawing byte-identical, and write #19 as a raster recipe:
- `LegendFlow`: the static legend flows its entries along rows and wraps a long name instead of cutting it;
- `WrapEndNotes`: an end label's note goes on a second line where today it would be cut;
- `AnnotationLabelsAvoidMarks`: a refined reference label is set clear of the series' marks;
- `PadFittedCells`: a fitted heatmap row gets 8 more units round its words (#16b);
- #19: the host turns Lumen's SVG into a PNG with Svg.Skia in its own code.

**Architecture:**
- **Legend flow** is one internal helper, `LegendFlowLayout`, that `Render` reads for both the drawing's height and the entries' positions, so the two cannot disagree.
- **End notes** generalise `Spread` from `i * gap` to per-label offsets, and settle which labels take two lines in passes inside `Ends`. `Ending` asks for less margin under the switch.
- **Annotation labels** are recorded marks: the pane loop records what it draws while the switch is on, and `Place` is then called after the marks with a longer list of spots. Off, the call stays where it is.
- **Fitted cells** are one expression in `HeatmapRowNeeds`, which `FittedHeight`, `HeatmapRows`, the drawing and the frozen band already share.
- **#19** is docs and a compiled recipe. Lumen's packages gain no dependency.

**Tech stack:**
- C# / .NET 8 for the libraries and .NET 10 for the gallery and tests.
- ASP.NET Core minimal API.
- Blazor Server and WebAssembly.
- Playwright for .NET with axe-core.
- Svg.Skia, in `tests/Lumen.Charts.Recipes` only.

**Spec:** `docs/superpowers/specs/2026-11-02-look-changes-design.md`. It is the binding authority; where this plan settles one of its ambiguities, or corrects it against the code, the plan says so. Line numbers are master at 27b25ba (the code is as at b2fe044).

## Global Constraints

- **Baseline:** the 433 existing baseline rows stay byte-identical in both finishes. Only the design's eight named rows are added, giving 441. Tasks 1–4 leave `tests/Lumen.Charts.Baseline/reference/*.txt` alone and accept only added rows in the diff; Task 5 replaces the reference files with the verified run.
- **Each new option:**
  - is `false` by default;
  - is left out of the gradient-ID hash while false, by a line in `Unfitted` (`ChartSvg.cs:109–125`);
  - is refused where the design says, with its reason;
  - is taken by the JSON API under its camel-case name (`legendFlow`, `wrapEndNotes`, `annotationLabelsAvoidMarks`, `padFittedCells`; the API's web defaults give these, so no attribute is added);
  - has an XML doc comment that says what it does, where it applies, "(0.53.0)", that it is false by default and that it is left out of the gradient-ID hash while false.
- **A switch at `true` is in the hash.** So every "equals the output without the switch" test and row in this plan uses a chart that draws no gradient (no `Gradient`, no `AreaFill.Fade`): IDs are written only by `SvgWriter.Gradient` (`ChartSvg.cs:54–62`).
- **Build:** the Release build has 0 warnings.
- **Validation:** errors are `ArgumentException` in `ChartValidation`'s voice, a plain sentence that names the option and says why.
- **Accessibility:** never colour alone; drawn text clears 4.5:1; every new text stays inside the drawing at 320–360 px.
- **Data:** invented data only.
- **SKILL.md:** the frontmatter stays byte-identical.
- **The brief:** never open `lumen-charts-race-face-brief.md`.
- **Git:** stage explicit file paths only.
- **The gallery locks the build:** stop `Lumen.Gallery` before the first build of a session.
- **"Off is byte-identical"** is proved for each switch by the baseline (both finishes) and, where it applies, a random probe run on this branch and on `v0.52.0`:
  - `LegendFlow`, `WrapEndNotes`, `AnnotationLabelsAvoidMarks`: the new `probe lines` mode (Task 1), rerun in Tasks 2 and 3;
  - `PadFittedCells`: the existing `probe heatmaps` mode (Task 4).

  Comparing with `v0.52.0`: `git worktree add <short path> v0.52.0`, copy **only** `tests/Lumen.Charts.Baseline/Probe.cs` over the worktree's copy (v0.52.0's `Program.cs:5` already dispatches `probe`; this branch's `Program.cs` uses the new switches and would not compile there), run the mode in both, and require every line of the two files to be equal. Remove the worktree afterwards.

## Review Focus

1. **A legend name that is one unbreakable word wider than a row** (a 40-letter name without spaces at 320). `Wrap` breaks it between its letters, and both lines stay inside `Width − 24`. Test: Task 1.
2. **A flowed legend on a column chart with `CellWidth`**, drawn wider than its `Width` (`AsDrawn`, `ChartSvg.cs:3276–3282`). The rows run across the drawn width, not the given one, and every entry is inside the `viewBox`. Test: Task 1.
3. **A two-line end label in a lower pane, at that pane's bottom.** Its note line stays within the pane's `bottom + 1`, so it does not run into the pane below, and `Ends` is called per pane with that pane's bounds (`ChartSvg.cs:1189`). Test: Task 2.
4. **A horizontal bar chart's value reference** is upright (`Measure` with `inside: true`, `ChartSvg.cs:934`). Its label clears the bars it would sit on. Test: Task 3.
5. **A level reference's label where a value label stands at its right-above spot.** Value labels are in `written` (`ChartSvg.cs:853`), so the label moves to right-below. Test: Task 3.

---

### Task 1: Legend flow

**Files:**
- Modify `src/Lumen.Charts/Models.cs`: add `LegendFlow` after `FitEachRow` (868), before `ChartSpec`'s closing brace (869).
- Modify `src/Lumen.Charts/ChartValidation.cs`: one refusal after the 0.52.0 ones (129–132).
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - `Unfitted` (109–125) and its `<summary>` (100–108);
  - `Render` (247–311):
    - `legendColumns` (260) and `legendRows` (264–265) stay; they still decide whether a legend is drawn;
    - the drawing's height at `Begin` (269);
    - the legend loop (295–308);
    - its `<summary>` (243–246), one sentence on `LegendFlow`;
  - add `LegendFlowLayout` beside `LegendKey` (318).
- No change to `src/Lumen.Charts.Blazor/LumenChart.razor`. Its HTML legend (15–24) already flows, its drawing renders with `includeLegend:false` (196), and its exports call `ChartSvg.Render(VisibleSpec())` with the legend (311–312), so they follow the spec.
- Modify `tests/Lumen.Charts.Tests/Program.cs`: add a `// 0.53.0: legend flow` block before `string ChartMarkup(` (13241).
- Modify `tests/Lumen.Charts.Baseline/Program.cs`: a new `// 0.53.0` block after `titles/undrawn-190` (996), before `if (args.FirstOrDefault() == "dump-finish")` (997).
- Modify `tests/Lumen.Charts.Baseline/Probe.cs`: add the `lines` mode.

**Interfaces:**
- **Produces** `public bool LegendFlow { get; init; }` on `ChartSpec`.
- **Refusal**, after `ChartValidation.cs:132`:
  - `if (spec.LegendFlow && (spec.Sparkline || spec.Kind is ChartKind.Donut or ChartKind.Heatmap or ChartKind.Box or ChartKind.Violin or ChartKind.Gauge or ChartKind.Calendar or ChartKind.Strip)) throw new ArgumentException("LegendFlow sets out the series legend drawn under a chart, so it applies to the kinds that draw one; a sparkline, a donut, a heatmap, a box, a violin, a gauge, a calendar and a strip name what they draw in their own drawing.");`
  - A one-series histogram is accepted (design §7.2).
- **The hash:** `else if (property.Name == nameof(ChartSpec.LegendFlow)) property.ShouldSerialize = (_, flow) => flow is true;`. The `<summary>` gains a sentence: "The legend flow, end notes on a second line, annotation labels clear of the marks and padded fitted cells, from 0.53.0, are false by default and left out unless set." Task 1 writes the whole sentence; Tasks 2–4 add their own lines to `Unfitted` only.
- **Produces** `internal static (List<(double X, double Y, string[] Lines)> Entries, double Height) LegendFlowLayout(ChartSpec spec, bool refined)`:
  - `spec` is the spec as drawn (after `AsDrawn`), so a column chart with `CellWidth` flows across its drawn width (Review Focus 2).
  - It takes `refined`, not `ring` as the design suggests: under the flow nothing is cut by `Short`, so the ring's 30-character limit has no use, and the label's start (`x + 20` refined, `x + 16` classic) is what differs.
  - `key = refined ? 20 : 16`; `room = spec.Width - 48 - key`.
  - For each series `i` in order, `label = LegendLabel(spec, i)`:
    - `lines = Wide(label) <= room ? [label] : Wrap(label, room)`;
    - `entry = key + lines.Max(Wide) + 16`.
  - Rows start at x = 24. Before placing an entry: if `x + entry > spec.Width - 24` and the row already holds an entry, start a new row. Then the entry is at `x`, and `x += entry`. The fit test counts the trailing 16, as the design writes it.
  - A row is 22 tall, or 35 where any of its entries has two lines. Each entry's `Y` is its first line's baseline: `spec.Height + 10` plus the heights of the rows above.
  - `Height` is the sum of the rows' heights; 0 with no series.
- **`Render` under `LegendFlow`, when `legendRows > 0`:**
  - `layout = LegendFlowLayout(spec, w.Refined)`, and `Begin` takes `height + layout.Height` in place of `height + legendRows * 22`;
  - the loop writes each entry's key at `(X, Y − 8)` exactly as today (`Key` refined, the 9 × 9 `rect` classic);
  - its first line at `X + key`, `Y`, with `font-size='11'`;
  - a second line, where there is one, at `X + key`, `Y + 13`, the same attributes.
- **Without `LegendFlow`** the code at 260–269 and 295–308 runs as today. Keep it one branch, not a rewrite of the old path.
- **Produces** the probe mode `lines <out.txt>`, run as `dotnet run -c Release -- probe lines out.txt`:
  - It writes `i hash` for 10,000 charts from `new Random(53)`, using only 0.52.0 API; `i refused` where a spec throws (both runs must agree).
  - The random choices:
    - kind Line, Area or Scatter, or Column with one series drawn as a line;
    - 1–8 series, each name 1–40 characters from a fixed word list;
    - 2–30 points with Y gaps;
    - `Width` 320–1200 and `Height` 240–600;
    - `Markers` and `HighlightLast`;
    - `EndLabel`s (1–12 characters) and `EndNote`s (`+0.0s` to `+99.9s`) on none, some or all series;
    - `ValueLabels` on or off;
    - 0–4 annotations, Y lines, Y bands and X lines, labelled or not;
    - `includeLegend` true or false;
    - Light, Dark or Midnight, in either finish.
  - Mixed with a column series, it gives the column chart no X annotations, since those are refused (`ChartValidation.cs:152–153`).

- [ ] **Step 1: Write the failing unit tests:**
  - **"Legend 0.53.0: four short series flow onto one row"**:
    - a 340 × 300 line chart with series `You`, `Leader`, `Field`, `Best`, no gradient;
    - with `LegendFlow` the legend's `text` elements all share one `y`, `Height + 10`;
    - the `viewBox` height is 66 less than without the switch (322 against 388).
  - **"Legend 0.53.0: a 40-character name wraps to two lines"**:
    - at 340, series `Midlands Women Masters Mountain Marathon` (40 characters, 277.2 units by `Wide`, over the refined room of 272 and the classic one of 276) and `You`;
    - in both finishes it is written on two `text` lines 13 apart, every word kept (the two lines joined by a space equal the name);
    - each line's `x + Wide(line) <= 316`;
    - the row is 35 tall: the next row's baseline is 35 lower.
  - **"Legend 0.53.0: mixed names keep series order"**: `A`, the 40-character name, `B`, `C`. The entries' text, read in document order, is `A`, the two lines, `B`, `C`; each entry's `x` grows within its row.
  - **"Legend 0.53.0: a ring's labels with their values flow"**: three rings at 340. Each label reads `Name: v of g unit`, whole, with no `…`.
  - **"Legend 0.53.0: both finishes keep their keys"**: the refined keys equal `Key`'s shapes (a line key `<line`, a scatter dot `<circle`), and the classic keys are `rect`s with `width='9' height='9' rx='2'`. Only their `x`/`y` move.
  - **"Legend 0.53.0: refused where no legend is drawn"**: a donut, a heatmap and a sparkline line chart, each with the message containing `LegendFlow`. A one-series histogram renders.
  - **"Legend 0.53.0: without the legend the switch does nothing"**: `Render(spec with { LegendFlow = true }, includeLegend: false)` equals `Render(spec, includeLegend: false)`.
  - **"Legend 0.53.0: the switch is hashed only when set"**: on a line chart with a `Gradient` series, `ChartSvg.IdPrefix(spec)` equals `IdPrefix(spec with { LegendFlow = false })` and differs from `IdPrefix(spec with { LegendFlow = true })`.
  - **Review Focus 1**: at 320, a series named with 40 letters and no space (`Wide` 272.8, over 256) wraps between its letters; both lines are inside `Width − 24`, and `Outside52` (13123) finds nothing.
  - **Review Focus 2**: a column chart with `CellWidth = 40` and 14 categories, `Width = 340`, four series, `LegendFlow`. The rows run past 340, up to the drawn width less 24, and `Outside52` finds nothing.
- [ ] **Step 2: Add the baseline rows** in the new `// 0.53.0` block, both through `Render`, so the classic run draws them classic:
  - `legend/flow-short`: `line with { Title = "Flowed legend", Width = 340, Height = 300, LegendFlow = true, Series = ["You", "Leader", "Field", "Best"] }`, each series `line`'s 12 points offset by its index times 6;
  - `legend/flow-long`: the same at 340 with series `You`, `Midlands Women Masters Mountain Marathon`, `Leader`, `Invented Provincial Mountain Bike Marathon` (42 characters, 278.5 units) and `Best`, so two names wrap.
- [ ] **Step 3: Build and run the unit tests, and check the new ones fail.** Run `dotnet run -c Release --project tests/Lumen.Charts.Tests`. Expect: they FAIL to compile until `LegendFlow` exists; stub the property first to see the behaviour failures.
- [ ] **Step 4: Implement** the property, the refusal, the hash line and sentence, `LegendFlowLayout` and `Render`'s branch.
- [ ] **Step 5: Run the probe** on this branch and on `v0.52.0`, as Global Constraints says. Expected: the two files are equal, line for line.
- [ ] **Step 6: Run the unit tests and both baselines.**
  - The unit tests all pass.
  - `dotnet run -c Release` and `dotnet run -c Release -- classic` in `tests/Lumen.Charts.Baseline` each write 435 rows.
  - Diffed against `reference/*.txt`, the only changes are the two added rows.
  - Write `svg-out` and look at the two rows in Playwright at 320, 340 and 360 with a real viewport: nothing runs outside the drawing, and no two entries overlap.
- [ ] **Step 7: Commit.** Stage `src/Lumen.Charts/Models.cs`, `src/Lumen.Charts/ChartValidation.cs`, `src/Lumen.Charts/ChartSvg.cs`, `tests/Lumen.Charts.Tests/Program.cs`, `tests/Lumen.Charts.Baseline/Program.cs` and `tests/Lumen.Charts.Baseline/Probe.cs`, then:

  ```bash
  git commit -m "0.53.0 legend flow: LegendFlow sets the static legend's entries along rows and wraps a long name, one layout for the height and the drawing"
  ```

### Task 2: End notes on a second line

**Files:**
- Modify `src/Lumen.Charts/Models.cs`: add `WrapEndNotes` after `LegendFlow`.
- Modify `src/Lumen.Charts/ChartValidation.cs`: one refusal after Task 1's.
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - `Unfitted`: one line;
  - `Ending` (1213–1219) and its `<summary>` (1209–1212);
  - `Spread` (1234–1252) and its `<summary>` (1228–1233);
  - `Ends` (1261–1323): the gap choice (1286), the leave-out (1288), the spread (1289), and the cut and the writing (1291–1323); its `<summary>` (1253–1260).
- Modify `tests/Lumen.Charts.Tests/Program.cs`: a `// 0.53.0: end notes` block. Keep a verbatim copy of 0.52.0's `Spread` (`ChartSvg.cs:1234–1252`) in it as `Spread52`, for the comparison test.
- Modify `tests/Lumen.Charts.Baseline/Program.cs`: two rows in the 0.53.0 block.

**Interfaces:**
- **Produces** `public bool WrapEndNotes { get; init; }` on `ChartSpec`.
- **Refusal:**
  - `if (spec.WrapEndNotes && (spec.Sparkline || spec.Kind is ChartKind.Donut or ChartKind.Radar or ChartKind.Heatmap or ChartKind.Histogram or ChartKind.Box or ChartKind.Violin or ChartKind.Gauge or ChartKind.Ring or ChartKind.Calendar or ChartKind.Timeline or ChartKind.Strip)) throw new ArgumentException("WrapEndNotes sets an end label's note on a second line, so it applies to charts whose series can end in a line, an area or scatter points; a sparkline writes no end labels, and donut, radar, heatmap, histogram, box, violin, gauge, ring, calendar, timeline and strip charts take none.");`
  - The design cites "the marks `Ended` accepts, `:661`". That is `ChartValidation.cs:661`, in `Ended` (653–668), not `ChartSvg.cs`.
- **The hash:** `WrapEndNotes` serialized only when `true`.
- **`Spread` becomes `internal static double[] Spread(double[] wanted, double[] offsets, double tail, double low, double high)`:**
  - `offsets[i]` is label `i`'s distance below the first;
  - `tail` is 14 where the last label has two lines, else 0.
  - The pools take `wanted[i] - offsets[i]`.
  - Each centre is `Math.Clamp(sum / count, low, high - (offsets[^1] + tail)) + offsets[at]`.
  - With `offsets[i] = i * gap` and `tail = 0` this is today's arithmetic, expression for expression. The offsets are sums of the integers 12 and 14, so they are exact.
- **Offsets:** `offsets[i] = i * gap + 14 * (two-line labels above i)`. The extra line is 14: the note line's distance under its label, so blocks keep `gap` between a note line and the next label.
- **Produces** `private static double EndLines(int count, double gap, double[] extras)`: the span the group needs, `offsets[^1] + tail`.
  - **The gap choice (1286):** `EndLine` where the span at `EndLine` is at most `high − low`, else `EndTight`.
  - **The leave-out (1288):** keep the longest prefix of `sorted` whose span at `gap` is at most `high − low`.
  - With every label on one line both equal today's tests: `(n − 1) * gap <= high − low` and `Take(Floor((high − low) / gap) + 1)`.
- **The passes, per group, under the switch only:**
  1. `two` is an empty set of indices into `set`.
  2. Run the gap choice, the leave-out and `Spread` with `two`'s extras.
  3. For each kept label, work out `centre`, `moved`, `x` and `room` exactly as 1294–1297. Add it to `two` where `note is not null && Broad(full) > room + 1e-6`.
  4. Repeat from step 2 until a pass adds nothing. `two` only grows, so the loop ends within one pass a label plus one.

  Without the switch, `two` stays empty and the loop runs once, so the arithmetic is today's.
- **Writing a two-line label:**
  - The label line is `label`, cut with `Clipped(label, room)` where `Broad(label) > room + 1e-6`, as today.
  - The note line is `note`, or `Clipped(note, room)` where `Broad(note) > room + 1e-6`.
  - In both the halo copy and the words, the note is a `<tspan x='{N(x)}' dy='14' font-weight='400'>…</tspan>` (the words' copy adds `{noted}`, 1282). The halo copy therefore carries both lines.
  - The words' `text` is written as a cut one is: `role='img' aria-label='{full}'` with `<title>{full}</title>` (1318–1320).
  - A one-line label is written exactly as today.
- **`Ending` under the switch:** for each series with an end label:
  - `one = Lead + EndShift + Broad(EndText(series)) + EndPad`;
  - the series asks `one` where `one <= cap`, otherwise `Lead + EndShift + Math.Max(Broad(label), Broad(note ?? "")) + EndPad`;
  - `cap = s.Width - left - s.Width / 2d`, unchanged, and the result is `Math.Min(need, cap)`.

  **Correction to the design's example.** With the Y axis on the left at 320, `cap = 320 − 76 − 160 = 84`. "Rider name" alone asks `8 + 6 + 70.6 + 4 = 88.6`, so it still meets the cap and that plot is as wide as today. The plot widens only where the label alone asks less than the cap. The tests below pin both.

- [ ] **Step 1: Write the failing unit tests:**
  - **"End notes 0.53.0: at 320 the note goes whole on a second line"**:
    - a 320 × 300 line chart, two series ending far apart: `Rider name` with `+12.3s`, and `Other` with `+4.1s`;
    - the first label's `text` has `role='img'` and `aria-label='Rider name +12.3s'`, and a `tspan` with `dy='14'` reading exactly `+12.3s`;
    - `Outside52` finds nothing.
  - **"End notes 0.53.0: the plot widens where the label alone fits under the cap"**: at 320 with `Rider` and `+12.3s`, the plot's right edge (the clip `svg`'s `x + width`) is greater with the switch than without. With `Rider name`, it is equal.
  - **"End notes 0.53.0: a chart with nothing cut is byte-identical"**: the first test's chart at 640, no gradient, renders equal with and without the switch.
  - **"End notes 0.53.0: crowded two-line labels keep a line between blocks"**:
    - 340 × 300, five series ending within 10 units of each other, three with notes that are cut at that width;
    - for each pair of neighbouring blocks, the next label's baseline minus the previous block's last baseline is at least 12;
    - every baseline minus 4 lies within the plot's `top − 1` and `bottom + 1`.
  - **"End notes 0.53.0: a note wider than its line is cut with …"**: a 24-character note at 320. The note `tspan` ends `…`, and `x + Broad(text) <= 316`.
  - **"End notes 0.53.0: a second pass marks another label"**: two labels near one another at 320. The first is cut on one line; the second fits on one line where it stands, but moves more than 3 units once the first takes two lines, steps right by 6 and is then cut. Both are written with a `dy='14'` `tspan`. Pin the Y values from your run, and say in the test's comment why the second needs the second pass.
  - **"End notes 0.53.0: Spread with one-line offsets equals 0.52.0's"**:
    - 10,000 random sets from `new Random(53)`: 1–12 labels, sorted wanted centres, gap 12 or 14, `low` and `high` that hold them;
    - `ChartSvg.Spread(wanted, offsets: i * gap, tail: 0, low, high)` equals `Spread52(wanted, gap, low, high)` element for element, with `==` on the doubles.
  - **"End notes 0.53.0: refused where there are no end labels"**: a donut and a sparkline line chart, each message containing `WrapEndNotes`.
  - **"End notes 0.53.0: the switch is hashed only when set"**: as Task 1's hash test.
  - **Review Focus 3**:
    - a line chart with one `Panes` entry, three labelled series in pane 1 ending at that pane's bottom, two of them two-line;
    - every note line's baseline minus 4 is at most pane 1's `bottom + 1`, read from its clip `svg`;
    - `Outside52` finds nothing.
- [ ] **Step 2: Add the baseline rows**, through `Render`:
  - `ends/note-below-320`: two riders at 320 × 300, `Rider name` / `+12.3s` and `Other rider` / `+48.0s`, `WrapEndNotes = true`;
  - `ends/note-below-crowded`: five end labels at 340 × 300, three of whose notes are cut there, so three take two lines.
- [ ] **Step 3: Build and run the unit tests, and check the new ones fail.**
- [ ] **Step 4: Implement** the property, the refusal, the hash line, `Spread`'s offsets, `EndLines`, the passes, `Ending` and the two-line writing. Rewrite the `<summary>`s of `Ending`, `Spread` and `Ends` to say what is now true, with "(0.53.0)".
- [ ] **Step 5: Run `probe lines`** on this branch and on `v0.52.0`. Expected: the two files are equal.
- [ ] **Step 6: Run the unit tests and both baselines:**
  - 437 rows each, with only the four added rows changed;
  - look at the two `ends/*` rows in Playwright at 320, 340 and 360.
- [ ] **Step 7: Commit.** Stage `src/Lumen.Charts/Models.cs`, `src/Lumen.Charts/ChartValidation.cs`, `src/Lumen.Charts/ChartSvg.cs`, `tests/Lumen.Charts.Tests/Program.cs` and `tests/Lumen.Charts.Baseline/Program.cs`, then:

  ```bash
  git commit -m "0.53.0 end notes: WrapEndNotes sets a note that would be cut on a second line, Spread by offsets, passes settle the two-line labels"
  ```

### Task 3: Annotation labels clear of the marks

**Files:**
- Modify `src/Lumen.Charts/Models.cs`: add `AnnotationLabelsAvoidMarks` after `WrapEndNotes`.
- Modify `src/Lumen.Charts/ChartValidation.cs`: one refusal after Task 2's. It uses `Annotated` (873–875).
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - `Unfitted`: one line;
  - the pane loop in `Cartesian` (842–1190):
    - two lists beside `written` (853);
    - the `Place` call at 937, made conditional;
    - a second call before the front references (1185);
    - recording at the marks: line and area pieces after `Trace` (1012); markers in the marker loop (1019–1042); range bars are not recorded; columns and bars at the category branch (1142–1145) and the slot branch (1154–1163); scatter and bubble circles (1164–1180);
  - `Place` (2172–2224) and its `<summary>` (2163–2171).
- No change to the timeline's `Place` call (2338), which the switch refuses.
- Modify `tests/Lumen.Charts.Tests/Program.cs`: a `// 0.53.0: annotation labels` block.
- Modify `tests/Lumen.Charts.Baseline/Program.cs`: two rows.

**Interfaces:**
- **Produces** `public bool AnnotationLabelsAvoidMarks { get; init; }` on `ChartSpec`. Its doc says it acts in the refined finish only and is accepted, doing nothing, in the classic one.
- **Refusal:**
  - `if (spec.AnnotationLabelsAvoidMarks && (spec.Sparkline || !Annotated(spec.Kind) || spec.Kind == ChartKind.Timeline)) throw new ArgumentException("AnnotationLabelsAvoidMarks sets a reference's label clear of the data's marks, so it applies to charts drawn on an X and Y axis; a sparkline writes no annotation labels, a timeline's spans are not marks it avoids, a gauge and a calendar label their annotations in their own way, and donut, radar, heatmap, histogram, box, violin, ring and strip charts take no annotations.");`
  - Horizontal bar charts are accepted.
- **The hash:** serialized only when `true`.
- **Recording**, only where `w.Refined && s.AnnotationLabelsAvoidMarks`. Otherwise the lists are `null` and nothing is added, so the classic finish and the switch off run today's code.
  - `List<(double X1, double X2, double Y1, double Y2)>? boxes`: each mark's box.
    - **A line's visible marker:** the last point's ring `r = 10`, a highlight `r = 5.5`, a hollow or filled marker `r` (4, or 2 past 80 points), each `cx ± (r + 2)`. Not recorded: `MarkerStyle.None` (1033), and the refined `Auto` marker that shows on hover when `length > 1` (1035). A point between two gaps (`length == 1`) is visible and recorded.
    - **A scatter or bubble circle:** `cx ± radius`; a scatter highlight `± (5.5 + 2)`.
    - **A column, bar or stacked segment:** its `rx, ry, rw, rh` rectangle, and a zero hairline's 1-unit rect.
  - `List<(double X1, double Y1, double X2, double Y2)>? pieces`: for a `Line` or `Area` mark, each pair of neighbouring points in `run` (1001), `(X(p.X), At(p.Y))` to the next. A curve is taken by its chord.
  - **Value and gap labels** are `written` (853), passed to `Place` as boxes too.
  - **Not recorded:** bands, area fills, ranges, candles, OHLC bars, blocks, density cells (design §2.3). Bar tracks (1131–1138) and trend lines (1182) are not recorded either: the design does not list them, so this plan leaves them out.
- **The calls:**
  - At 937: `if (w.Refined && boxes is null) Place(references, left, right, top, bottom);`, so without the switch it runs here, as today.
  - Before 1185: `if (boxes is not null) Place(references, left, right, top, bottom, [.. boxes, .. written], pieces!);`.
- **`Place(List<Reference> references, double left, double right, double top, double bottom, IReadOnlyList<(double X1, double X2, double Y1, double Y2)>? boxes = null, IReadOnlyList<(double X1, double Y1, double X2, double Y2)>? pieces = null)`:**
  - With `boxes` null, it is today's code.
  - Otherwise it keeps the order (level lines, level bands, upright; 2222) and the constants (`ascent 9, descent 3, inset 4, gap 2, row 14`; 2175).
  - Each label tries its spots in turn and takes the first whose box clears the labels already set **and** every box and piece. Each spot keeps its own in-plot test.
    - **Level line**, all anchored as given:
      - right above: `x = right - inset - gap`, `Near - 6`, `end`;
      - right below: `x = right - inset - gap`, `Near + 4 + ascent`, `end`;
      - left above: `x = left + inset + gap`, `Near - 6`, `start`, needing `x + width <= right - inset`;
      - left below: `x = left + inset + gap`, `Near + 4 + ascent`, `start`.
    - **Level band:** at the right, `y` from `from + pad + ascent` down in steps of `row` (14) while `y + descent <= to - pad`; then the same rows at the left, `x = left + inset + gap`, `start`. The design says "each row"; this plan settles a row as Place's 14.
    - **Upright line:** each row (`top + inset + ascent`, step 14) on the side today's rule picks (2209–2211), then each row on the other side where its text fits there.
    - **Upright band:** today's rows inside the band.
  - Where no spot clears the marks, it runs today's rule for that label, clear of labels only. Where that finds nothing either, `Shown = false`, as today.
  - **A spot is hit** when a box meets the label's box widened by `gap` on every side, or when a piece crosses it.
- **Produces** `internal static bool Crosses(double x1, double y1, double x2, double y2, (double X1, double X2, double Y1, double Y2) box)`: Liang–Barsky. The segment is clipped to the box, and the result is true where any part of it lies inside, ends included.

- [ ] **Step 1: Write the failing unit tests:**
  - **"Annotation labels 0.53.0: Crosses clips the segment, not its bounding box"**:
    - a diagonal from (0, 0) to (100, 100) misses the box `(80, 100, 0, 10)`, though its bounding box covers it;
    - it crosses `(40, 60, 40, 60)`;
    - a segment ending on an edge crosses;
    - a vertical and a horizontal segment.
  - **"Annotation labels 0.53.0: a level label clears the markers at the line's right end"**:
    - a 340 × 300 refined line chart, `Markers = MarkerStyle.Filled`, its last four points within 6 units of a `Y = 255` line labelled `CP`;
    - with the switch, the label's box (from its `x`, `y`, `text-anchor` and `Wide(text)`, 9 up and 3 down) meets no visible `circle` (`cx ± (r + 2)`) and crosses no piece of the series' `path` (read its `d` pairs);
    - the box lies inside the plot;
    - without the switch, the box meets a marker (the test's own precondition).
  - **"Annotation labels 0.53.0: where every spot is blocked the label keeps today's"**: a dense series running along the line across the whole plot. The label's `x`/`y` equal those without the switch.
  - **"Annotation labels 0.53.0: an upright label clears a column"**: a `Line` chart with a series of `Kind = ChartKind.Column` on a linear X and an X reference whose label today's rule sets over the tallest column. With the switch the label's box meets no column `rect`.
  - **"Annotation labels 0.53.0: the classic finish is byte-identical"**: the first test's chart in the classic finish, no gradient, renders equal with and without the switch.
  - **"Annotation labels 0.53.0: refused where it cannot apply"**: a heatmap, a gauge, a timeline and a sparkline line chart, each message containing `AnnotationLabelsAvoidMarks`.
  - **"Annotation labels 0.53.0: the switch is hashed only when set"**: as Task 1's hash test.
  - **Review Focus 4**: a horizontal bar chart at 340 with a value reference whose label today's rule sets over a bar. With the switch it meets no bar `rect` and lies inside the plot.
  - **Review Focus 5**: a line chart with `ValueLabels` where a value label stands at the reference's right-above spot. With the switch the reference label is written at right-below (`y == Near + 13`).
- [ ] **Step 2: Add the baseline rows**, through `Render`:
  - `annotations/avoid-marks`: a 340 × 300 power line, `Markers = MarkerStyle.Filled`, whose last points crowd a level `Y = 255` reference labelled `CP` (so it reads `CP: 255`), `AnnotationLabelsAvoidMarks = true`. The filled markers matter: the refined default `Auto` markers show only on hover (1035) and are not recorded.
  - `annotations/avoid-marks-upright`: a `Line` chart on a linear X with one series drawn as columns (`Kind = ChartKind.Column`) and an X reference among them.

  **Correction to the design.** It asks for "columns with an X reference", but a column chart refuses X annotations (`ChartValidation.cs:152–153`). A line chart's column series draws in slots (`ChartSvg.cs:1154–1163`), so it is the nearest valid chart.

  Neither row draws a gradient. In the classic run, each row's hash must equal the same spec's without the switch. Check that once by hand (render both in the classic run) and say so in the report. The design's reason, "stays out of the hash", is not the mechanism: a `true` switch is hashed, and the rows simply write no ID.
- [ ] **Step 3: Build and run the unit tests, and check the new ones fail.**
- [ ] **Step 4: Implement** the property, the refusal, the hash line, the recording, the two calls, `Place`'s spots and `Crosses`. Rewrite `Place`'s `<summary>` to describe both rules, with "(0.53.0)".
- [ ] **Step 5: Run `probe lines`** on this branch and on `v0.52.0`. Expected: the two files are equal.
- [ ] **Step 6: Run the unit tests and both baselines:**
  - 439 rows each, with only the six added rows changed;
  - look at the two `annotations/*` rows in Playwright at 320, 340 and 360.
- [ ] **Step 7: Commit.** Stage `src/Lumen.Charts/Models.cs`, `src/Lumen.Charts/ChartValidation.cs`, `src/Lumen.Charts/ChartSvg.cs`, `tests/Lumen.Charts.Tests/Program.cs` and `tests/Lumen.Charts.Baseline/Program.cs`, then:

  ```bash
  git commit -m "0.53.0 annotation labels: AnnotationLabelsAvoidMarks records the marks a pane draws and places refined reference labels clear of them"
  ```

### Task 4: Padded fitted cells and the HTTP checks

**Files:**
- Modify `src/Lumen.Charts/Models.cs`: add `PadFittedCells` after `AnnotationLabelsAvoidMarks`.
- Modify `src/Lumen.Charts/ChartValidation.cs`: one refusal after Task 3's.
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - `Unfitted`: one line;
  - `HeatmapRowNeeds` (3236–3241) and its `<summary>` (3233–3235);
  - `HeatmapRow`'s `<summary>` (3183–3184).
- No change to `FittedHeight` (1765–1773), `HeatmapRows` (3247–3266), `Heatmap` (3358–3400), `CellBlock` (3593–3625), `HeatmapNameBand` or `LumenChart.razor`. All read `HeatmapRowNeeds`.
- Modify `tests/Lumen.Charts.Tests/Program.cs`: a `// 0.53.0: padded fitted cells` block.
- Modify `tests/Lumen.Charts.Baseline/Program.cs`: two rows.
- Modify `tests/verify-api.ps1`: eight checks before the summary line (675).

**Interfaces:**
- **Produces** `public bool PadFittedCells { get; init; }` on `ChartSpec`.
- **Refusal:**
  - `if (spec.PadFittedCells && (spec.Kind != ChartKind.Heatmap || !spec.FitHeight)) throw new ArgumentException("PadFittedCells adds room round the words in a fitted heatmap's cells, so it applies to heatmaps with FitHeight only; the other kinds have no cells, and without FitHeight a heatmap's rows take the Height they are given.");`
  - The message names both `PadFittedCells` and `FitHeight`.
- **The hash:** serialized only when `true`.
- **`HeatmapRowNeeds`:** `CellNeed(s, p, cw) + (s.PadFittedCells ? 12 : 4)` in place of `CellNeed(s, p, cw) + 4` (3240), still at least `HeatmapRow`.
  - The expected rows: a one-line cell's row stays 36 (25 < 36), a two-line cell's goes from 36 to 37, and a row of four lines from 53 to 61.
  - `CellBlock`'s `tall = ch - 4` (3597) is unchanged.

- [ ] **Step 1: Write the failing tests.**
  - **Unit tests:**
    - **"Padded cells 0.53.0: every fitted row is its need plus 12"**:
      - the baseline's `noted` table shape (`CategoryGrid` with a points note in every cell), `CellWidth = 90, FitHeight = true, CellNotes = true, PadFittedCells = true`;
      - each `HeatmapRowNeeds` entry is `Math.Max(36, max CellNeed + 12)`;
      - each row's rect height plus 2 equals its row's height, read from `HeatmapRows`;
      - the same with `FitEachRow = true`, where the rows differ.
    - **"Padded cells 0.53.0: the rows grow as the design says"**: one-, two- and four-line cells give rows of 36, 37 and 61.
    - **"Padded cells 0.53.0: the frozen band matches the drawing"**: with `FitEachRow`, `HeatmapNameBand`'s name `y`s equal the drawing's.
    - **"Padded cells 0.53.0: one-line cells draw the same rows"**: cells that write one line each (values only) give rect `y`/`height` attributes and name `y`s equal with and without the switch.
    - **"Padded cells 0.53.0: refused off a fitted heatmap"**:
      - a line chart is refused, and the message names `PadFittedCells`;
      - a heatmap without `FitHeight` is refused, and the message names `FitHeight`.
    - **"Padded cells 0.53.0: the switch is hashed only when set"**: on a heatmap with `FitHeight`, `IdPrefix` with `PadFittedCells = false` equals the default and differs with `true`.
  - **HTTP checks** in `verify-api.ps1`, eight in all:
    - `/api/charts/svg` returns 200 for each of:
      - a three-series line chart with `"legendFlow":true`;
      - a line chart whose series has `"endLabel"` and `"endNote"`, with `"wrapEndNotes":true`;
      - a line chart with an annotation and `"annotationLabelsAvoidMarks":true`;
      - a heatmap with `"cellText":true,"fitHeight":true,"padFittedCells":true`;
    - and 400 with the option's name in the body for each of:
      - `"legendFlow":true` on a donut;
      - `"wrapEndNotes":true` on a donut;
      - `"annotationLabelsAvoidMarks":true` on a heatmap;
      - `"padFittedCells":true` on a line chart.
- [ ] **Step 2: Add the baseline rows**, through `Render`:
  - `heatmap-cells/padded`: `noted with { CellWidth = 90, FitHeight = true, PadFittedCells = true }`;
  - `heatmap-cells/padded-each-row`: `noted with { CellWidth = 90, FitHeight = true, PadFittedCells = true, FitEachRow = true }`.
- [ ] **Step 3: Build and run the unit tests, and check the new ones fail.**
- [ ] **Step 4: Implement** the property, the refusal, the hash line and the expression. Rewrite the two `<summary>`s, with "(0.53.0)".
- [ ] **Step 5: Run `probe heatmaps`** (`Probe.cs:39–118`, `Heatmaps` at 43, unchanged) on this branch and on `v0.52.0`. Expected: every line is equal, the `touched` ones included, since 0.53.0 changes no heatmap by default. Run `probe lines` once more as well.
- [ ] **Step 6: Run the full suites:**
  - the unit tests;
  - both baselines: 441 rows each, with only the eight added rows changed;
  - `verify-api.ps1` against the gallery: 338 checks;
  - look at the two `heatmap-cells/padded*` rows in Playwright at 320, 340 and 360: the value's cap tops stand about 6.5 under a dashed top edge.
- [ ] **Step 7: Commit.** Stage `src/Lumen.Charts/Models.cs`, `src/Lumen.Charts/ChartValidation.cs`, `src/Lumen.Charts/ChartSvg.cs`, `tests/Lumen.Charts.Tests/Program.cs`, `tests/Lumen.Charts.Baseline/Program.cs` and `tests/verify-api.ps1`, then:

  ```bash
  git commit -m "0.53.0 padded cells: PadFittedCells gives a fitted heatmap row 8 more units round its words; HTTP checks for the four switches"
  ```

### Task 5: The raster recipe, docs and the version

**Files:**
- Modify `tests/Lumen.Charts.Recipes/Lumen.Charts.Recipes.csproj`: a `PackageReference` to `Svg.Skia`, test-only, at its current release, pinned exactly. Check its API against that release before you write the recipe.
- Modify `tests/Lumen.Charts.Recipes/check.py`:
  - after the render loop it writes, for each recipe variable declared `var (\w+Png) = `, that `byte[]` to `obj/out/<name>.png`;
  - this is its only change.
- Modify `integrations/claude-code/lumen-charts/references/recipes-race-face.md`:
  - **A new section, "## A share card as a PNG on the server"**, before "## Rendering notes" (1102):
    - a `using SkiaSharp;` / `using Svg.Skia;` block of its own, as the file's first block is (5–8);
    - the recipe: `byte[] SharePng(ChartSpec card)` renders `ChartSvg.Render(card with { Width = 360, Height = 450 })`, loads it into `SKSvg` and saves the picture as a PNG at a scale of 3;
    - then `var shareCardPng = SharePng(seasonArc);`, using the season-arc recipe's `seasonArc` (457).
    - `check.py` hoists every line that starts `using ` into the program's directives, so any `using var` in the recipe stays indented inside `SharePng`'s body.
    - **The prose** follows design §2.5:
      - the packages, and Lumen's packages referencing neither;
      - the 360 × 450 layout at 3×, and why;
      - the three things to check in the PNG: `var(--lumen-grid)` / `var(--lumen-muted)` (`ChartSvg.cs:413–417`) and a heatmap cell's `stroke='var(--lumen-muted)'` (3390), `fill='currentColor'` from the root's `color` (413), and `paint-order='stroke'` on refined reference labels (2160);
      - the host replacing the variables with the style's `Grid`, `Muted` and `Text` itself;
      - fonts in a Linux container: `fontconfig`, the style's first `FontFamily`, `fc-cache`, and why a fallback font misfits;
      - the browser's PNG at 2×, capped at 8,192 (`lumen.js:762–763`, `LumenChart.razor:311`).
  - **"Rendering notes"' Static bullet (1104):** point to the new section in place of "an SVG library of your choice".
  - **The Phone width bullet (13):** for Race Face's cards set `LegendFlow`, `WrapEndNotes` and `AnnotationLabelsAvoidMarks`, and `PadFittedCells` on a fitted heatmap, each where it applies.
- Modify `integrations/claude-code/lumen-charts/references/api.md`: the four options in the `ChartSpec` table, beside `FitHeight`/`FitEachRow` (71–73) and `EndLabel` (104), each with its JSON name and where it is refused.
- Modify `integrations/claude-code/lumen-charts/SKILL.md`: one body line after 104, "**Look changes** (0.53.0): …", naming the four switches and their JSON names, and the raster recipe. The frontmatter stays byte-identical.
- Modify `README.md`:
  - **"## 0.53.0 additions"** above `## 0.52.0 additions` (1535);
  - **one sentence each:**
    - the legend footer sentence (1949): `LegendFlow`;
    - the end labels bullet (1014): `WrapEndNotes`;
    - "### Annotations" (826): `AnnotationLabelsAvoidMarks`, refined only;
    - "### Heatmap tables" (531): `PadFittedCells`;
  - **"Exports and tooltips":** the raster paragraph (1121–1123) and the list line at 1521 ("there is no server-side PNG"), both pointing to the recipe.
- Modify `Directory.Build.props:7` to `0.53.0`.
- Modify `docs/VERIFICATION.md`: a 0.53.0 paragraph after 7, and the counts at 100–104.
- Modify `tests/Lumen.Charts.Baseline/README.md`: the reference is v0.53.0 with 441 rows, the eight added rows named, and the `probe lines` mode described.
- Replace `tests/Lumen.Charts.Baseline/reference/refined.txt` and `classic.txt` with the verified run.
- Modify `HANDOVER.md`:
  - the release lines (7–9);
  - the Race Face table's #19 row (55): the recipe;
  - the feedback list (69–84): the end-label (71), annotation-label (72) and legend (74) items done in 0.53.0, the rasteriser item (82) answered by the recipe, and #16b done.

- [ ] **Step 1: Write the recipe and run the recipe check.**
  - Run `python check.py` and `dotnet run -c Release` in `tests/Lumen.Charts.Recipes`.
  - Every recipe compiles and renders.
  - `obj/out/shareCardPng.png` is 1080 × 1350. Look at it: its words are drawn, the gridlines and muted text have colour, and the reference halos do not hide the glyphs. Report what Svg.Skia missed, if anything, and say so in the recipe's "check" list.
- [ ] **Step 2: Make the doc edits.** Every count (unit tests, HTTP checks, browser and WebAssembly checks, recipes, baseline rows) comes from your own runs.
- [ ] **Step 3: Check the diff:**
  - `git diff --check` is clean;
  - `git diff integrations/claude-code/lumen-charts/SKILL.md` touches no line between the `---` fences;
  - the description stays at 1,024 characters or fewer;
  - `git diff --stat src/Lumen.Charts.Blazor` is empty, and no `src/*.csproj` gains a package.
- [ ] **Step 4: Run everything:**
  - the Release build: 0 warnings;
  - the unit tests;
  - both baselines: 441 rows each, the 433 identical to `reference/*.txt` before you replace it;
  - `verify-api.ps1`;
  - the gallery browser suite and the WebAssembly suite, unchanged, both passing;
  - the recipe check.
- [ ] **Step 5: Commit.** Stage each changed file by its path, then:

  ```bash
  git commit -m "0.53.0: raster recipe, README, api.md, SKILL, VERIFICATION, HANDOVER, baseline references and the version"
  ```
