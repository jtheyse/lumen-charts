# Room for long end-label names (0.53.1)

Design, 3 November 2026. In 0.53.0, `WrapEndNotes` moves an end label's note to a second line where it would be cut, but the right margin is still capped so the plot keeps half the drawing. So at 320 a real surname is still cut: "Rider name" draws as "Rider na…" with its note under it. The owner ruled "Yes, follow-up": under `WrapEndNotes`, give a long name more room at 320, so the name shows whole and its note goes under it. With the switch off nothing changes, to the byte.

Two small follow-ups ride along:
- the recipe check compiles the recipes with nullable analysis off, because it writes them to a `.g.cs` file;
- the raster recipe gives the wrong legend threshold for the 190 floor.

The details below are Claude's calls for the owner to check; §7 lists them. Line numbers are master at 8027260.

## 1. Intent

What a 320 card shows today with `WrapEndNotes`:
- **The margin.** `Ending` (`ChartSvg.cs:1289–1300`) asks for each label's one-line width where that fits under the cap, and otherwise for the wider of its name and its note (`:1297`). The cap is `Width − left − Width / 2` (`:1292`), so the plot keeps half the drawing. At 320, with the Y axis on the left (`left` = 76, `Margins`, `:3402`), the cap is 320 − 76 − 160 = 84.
- **The room.** A label starts `Lead` (8, or 14 beside a highlighted last point, `:1279`) right of its last point, and 6 more once it has moved off it (`Slot`, `:1395–1402`). It is cut where it would pass `Width − 4`. So a label that has not moved has `margin − 12`, and one that moved has `margin − 18`. At 320 that is 72 and 66.
- **The name.** "Rider name" is 73.92 by `Broad` (`:1603`; .62 em a letter, .3 a space, .9 for `m`, at 12 px). It asks `8 + 6 + 73.92 + 4 = 91.92`, meets the cap of 84, and is cut by `Clipped` (`:1303`) to "Rider na…" (63.12; "Rider nam…" is 73.92, over 72). Its note goes whole under it (`:1439–1444`).
- **From 336 up** the cap (`Width / 2 − 76`) is at least 91.92, so "Rider name" is already whole at 340 and 360. The problem is a 320 card, and any name longer than the half cap allows at 340 or 360.

## 2. What changes

### 2.1 The margin under `WrapEndNotes`

**The rule.** Under the switch, the cap rises to the widest end label's own ask, the name alone, and stops where the plot would keep less than two fifths of the drawing. Only names drive the rise; a note never does, since it can wrap.

In `Ending`, after the loop that today ends at `:1298`:
- `names` is the largest `Lead(series) + EndShift + Broad(series.EndLabel) + EndPad` over the series with an end label. It counts `EndShift`, so a label that moves off its point still has its whole name.
- `wide = Width − left − 2 × Width / 5` is the margin that leaves the plot two fifths of the drawing. It is written `2d * s.Width / 5` so it is exact at whole widths: 128, 136 and 144 at 320, 340 and 360.
- Without the switch it returns `Math.Min(need, cap)`, as today.
- With it, it returns `Math.Min(need, Math.Max(cap, Math.Min(names, wide)))`.

**Why this is byte-identical where no name is over the half cap.** Every series' ask is at least its name's ask, so `need ≥ names`.
- Where `names ≤ cap`, `Math.Min(names, wide) ≤ cap`, so `Math.Max` returns `cap` itself, and the result is 0.53.0's `Math.Min(need, cap)`, the same double.
- Where `names > cap`, the result is `Math.Min(names, wide)`. It is the name's need, bounded by the floor, and no note reaches it.

**Why names only.** A note wider than its name goes on the second line, where it is cut to its line by `Clipped` (`:1443`), as in 0.53.0. Letting notes raise the margin would give the plot's width to words that already have a place.

There is a second reason. Which labels take two lines is settled in `Ends`' passes (`:1403–1426`), after the margin is fixed in `Framed` (`:644`) or `Margins` (`:3404`). A margin driven by names alone depends on nothing the passes decide, so there is no new circle.

### 2.2 The floor: two fifths of the drawing

- **An absolute 160 at 320 gives nothing.** It is today's cap at 320 (half of 320), so "Rider name" would stay cut.
- **The ceiling.** For "Rider name" to be whole at 320, the plot must give up 7.92: 320 − 76 − 91.92 = 152.08, 47.5 % of the drawing. Any floor at or under that holds it.
- **Two fifths** is the largest round share that also holds a 14-letter surname whole from 340, which a 45 % floor does not (`0.55 × 340 − 76 = 111 < 125.52`). At 320 it leaves a plot of 128 units. That is room for three or four X tick labels at `Apart`'s 8-unit gap (`:1588–1600`), with a margin of 116, which holds a 12-letter name whole.
- **Whole at 320 every time?** A 14-letter name at 320 would need the plot cut to 118.48, 37 %. The plot would then be narrower than the gutter beside it plus the margin on its left. That is the point where the chart stops being a chart. So the floor stays at two fifths, and a name that long is cut at 320.
- **A share, not a number.** The old cap is a share of the drawing (half), so a share keeps the rule's form. It matters only at narrow widths: where the half cap is already wide, every name under 24 characters fits under it.

### 2.3 The numbers

`left` is 76, and the series' last point stands at the plot's right edge, with `Lead` 8. "Vandermeulenko" is an invented 14-letter surname with one `m`, `Broad` 107.52, ask 125.52. Every note here is "+12.3s", 40.8.

| Width | half cap | `wide` | Name | 0.53.0 margin | 0.53.0 label | 0.53.1 margin | 0.53.1 label | Plot |
|---|---|---|---|---|---|---|---|---|
| 320 | 84 | 116 | Rider name | 84 | `Rider na…` (room 72) | **91.92** | `Rider name` whole (room 79.92; 73.92 once moved) | 152.08 |
| 340 | 94 | 128 | Rider name | 91.92 | whole | 91.92 (unchanged) | whole | 172.08 |
| 360 | 104 | 140 | Rider name | 91.92 | whole | 91.92 (unchanged) | whole | 192.08 |
| 320 | 84 | 116 | Vandermeulenko | 84 | `Vanderme…` (room 72) | **116** (the floor) | `Vandermeulen…` (room 104; `Vandermeule…` once moved, room 98) | 128 |
| 340 | 94 | 128 | Vandermeulenko | 94 | `Vandermeu…` (room 82) | **125.52** | whole (room 113.52; 107.52 once moved) | 138.48 |
| 360 | 104 | 140 | Vandermeulenko | 104 | `Vandermeul…` (room 92) | **125.52** | whole | 158.48 |

In every row the note goes whole on the second line: 40.8 is under each room.

**What is still cut:**
- at 320, a name whose ask passes 116, so a `Broad` over 98, about 13 letters (12 beside a highlighted last point, whose `Lead` is 14);
- at 340, a `Broad` over 110; at 360, over 122;
- a 24-character name (178.56 at .62 em) until about 455 units.

A cut name keeps its whole label as its accessible name and `<title>`, as today (`:1460–1462`).

### 2.4 How it meets the rest of `Ends`

- **The passes and the two-line marking** (`:1403–1426`) are unchanged. They read the room through `Slot` (`:1395–1402`), and a wider margin gives each label more room. So the set of two-line labels can only stay the same or shrink. A label whose name and note now fit together stays on one line. Example: "Other +4.1s" (74.16) beside "Rider name" at 320 now fits the 79.92 its label has.
  - The passes still end: the set still only grows inside one call.
- **The thin-pane guard** (`:1419–1421`) is unchanged. In a pane too thin for a second line, a label is cut on one line as before, but against the wider room. So "Rider name +12.3s" in such a pane at 320 now reads "Rider name" (its note left out, the whole kept as its name), not "Rider na…".
- **Panes.** The margin is one for the whole drawing. `Ending` reads every series, whichever pane it is in, so a long name in a lower pane widens the margin for every pane, as 0.53.0's ask already does. `Ends` is still called per pane with its own bounds (`:1261`).
- **The 190 untitled floor** (`Models.cs:802`) is vertical. The margin rule is horizontal and does not read `Height`. A short chart meets only the thin-pane guard and the leave-out (`:1409`), both unchanged.
- **Column charts with `CellWidth`.** `DrawnWidth` and `Framed` reach `Ending` through `Margins` with `Width = MaxWidth` (4096) (`:3404`; summary `:1286–1288`). There the half cap is 1972 and every name fits under it, so `Math.Max` returns the cap, and these charts draw as in 0.53.0.
- **Horizontal bars** call `Ending` with `left` = 160 (`:643–644`). Their series cannot carry end labels (`ChartValidation.cs:230–231`, `:669–670`), so `need` and `names` are 0 and the margin is 30, as today.
- **A right-hand axis or a secondary series** fixes the margin at 76 and does not call `Ending` (`:644`, `:3404`). It takes no end labels anyway (`ChartValidation.cs:673–674`).
- **No Y labels on the left** (`Unlabelled`, `:1924`), `left` = 30. The half cap at 320 is 130, over both names above, so nothing changes there. The rule rises only where `left` is 76 and the width is narrow.

### 2.5 The recipe check under nullable analysis

`tests/Lumen.Charts.Recipes/check.py:37` writes the program to `obj/Recipes.g.cs`. The compiler treats a `*.g.cs` file as generated code and turns nullable analysis off in it, whatever the project's `<Nullable>enable</Nullable>` says (`Lumen.Charts.Recipes.csproj:5`). So a recipe that dereferences a maybe-null value compiles today.

- `check.py:26` begins the program with `'#nullable enable\n'`, before the `using` lines.
- With warnings as errors (`csproj:5`), any nullable warning in a recipe then fails the check.
- **Fixes.** Each failing recipe is fixed only where the fix is trivially correct and keeps the recipe's meaning:
  - a `?` on a declaration that can hold null;
  - a `!` after a call whose own docs say it is not null where the recipe calls it, such as a builder given points;
  - a null test the recipe's prose already implies.
- **Anything else is reported to the owner, not changed:** a warning that shows a recipe could really throw on some input. The design cannot list the failures ahead of the run.

### 2.6 The raster recipe's threshold

`references/recipes-race-face.md:1133`, in the share-card prose, says that past the 240 floor "(190 with `DrawTitles` off; the legend is then taller than 210, about ten one-line rows of 22)". 210 is the threshold for the 240 floor only:
- `450 − L < 240` ⇒ `L > 210`, about ten rows;
- `450 − L < 190` ⇒ `L > 260`, about twelve rows.

The sentence gives both: "taller than 210, about ten one-line rows of 22, or 260, about twelve, with `DrawTitles` off". The code at `:1122` is right and does not change.

### 2.7 Version 0.53.1

**A patch.**
- It adds no public API.
- It changes no drawing with `WrapEndNotes` off, and none with it on unless some end label's name asks more than the half cap.
- What it changes is an opt-in released yesterday, whose intent was a whole name with its note under it. The Race Face recipe's phone notes already say the switch keeps "a long end label whole" (`recipes-race-face.md:13`).
- **Precedent.** 0.46.1 changed how heatmaps with long row names draw (`HeatmapLeftOf`, `ChartSvg.cs:3380–3388`; README "0.46.1 additions", `README.md:1679`), in a patch.

**The case for 0.54.0.** A host that pinned a 0.53.0 drawing with the switch on and a long name sees that drawing change. One day after release, with Race Face the only known user and asking for this change, a patch is the right call.

## 3. Non-goals

- No change with `WrapEndNotes` off: the 433 rows of 0.52.0 and every 0.53.0 row without the switch stay byte-identical in both finishes.
- No change to the one-line rule, the default margin, the passes, `Spread`, the thin-pane guard or the two-line writing. Only the margin's ceiling moves, and only under the switch.
- No note-driven margin, and no third line.
- No new option or constant in the public API. The two-fifths floor is an internal constant.
- No wider margin for the plain (non-wrapped) end labels. That stays HANDOVER's other idea.
- No change to the raster recipe's code (`:1122`).

## 4. Tests

- **Unit tests:**
  - **New, for the rule** (one series each, so the margin is that label's ask, its point at the plot's right edge, never moved). Label `x` is the plot's right edge plus 8.
    - At 320, "Rider name" / "+12.3s" is written whole, with its note on a `dy='14'` line. The plot's right edge is 7.92 left of the switch-off one's. The label's `x` is 236.08.
    - At 340 and 360, "Rider name" draws with the same plot as 0.53.0. With the switch on and off the plot's right edges differ by the 0.53.0 amounts: 340 − 91.92 against 340 − 94, and 360 − 91.92 against 360 − 104.
    - "Vandermeulenko" / "+12.3s":
      - at 320 it reads `Vandermeulen…`, with `x` 212 (the margin is the floor, 116);
      - at 340 it is whole, with `x` 222.48;
      - at 360 it is whole, with `x` 242.48;
      - each note is whole under it.
    - A 24-character name at 320: the plot is exactly two fifths of the drawing. Its right edge is 32 left of the switch-off one's (the margin is 116 against 84).
    - Notes never raise it past the half cap: "Rider name" with a 24-character note at 320 gets the same margin as "Rider name" with "+1s" (the name's 91.92), and "Ann" with a 24-character note gets the half cap with the switch on or off, as in 0.53.0 (a note still raises the margin up to the half cap, as it always did).
    - With `HighlightLast`, "Rider name" at 320 starts 14 right of its point and is whole: the margin is 97.92.
    - In a two-pane chart, a long name in pane 1 widens pane 0's plot by the same amount: the clips' right edges are equal.
    - In a pane too thin for a second line (0.53.0's thin-pane chart), "Rider name" now reads whole on one line, its note left out.
  - **0.53.0's end-note tests that change with the rule**, each changed to say what is now true:
    - **"at 320 the note goes whole on a second line".** "Other +4.1s" now fits on one line, so one label takes two lines, not both.
    - **"the plot widens where the label alone fits under the cap".** With "Rider name" the plot is now narrower than with the switch off (the margin is 91.92, not 84), not equal. The "Rider" half is unchanged.
    - **"a second pass marks another label".** "Other rider" now asks a margin of 96, so a label has 84 where it stands and 78 once moved. "Ann +12.3s" (66.72) fits both. The second label becomes "Anna" / "+123.4s", 81.6, which fits 84 but not 78, so the test still needs a second pass. The unmoved `x` is 232, and the moved one 238.
    - The other 0.53.0 end-note tests, the pane test included, keep their assertions; the run confirms them.
- **Probes:**
  - `probe lines`, unchanged, run here and in a `v0.53.0` worktree: every line equal. The switch is off in every chart it draws.
  - A new `probe ends` mode: 10,000 random charts with `WrapEndNotes` on and end labels of 1–24 characters, widths 320–700, some of them column charts with `CellWidth`. It writes `i hash`, and ` touched` after the hash where some end label's ask passes the half cap of an upright chart with `left` = 76. Run here and in a `v0.53.0` worktree:
    - every untouched line is equal;
    - the touched lines may differ, and at least one does;
    - no `CellWidth` chart is touched.
- **Baseline:**
  - 441 rows in each finish, as in 0.53.0. 440 are byte-identical to `v0.53.0`'s reference.
  - **`ends/note-below-320`** changes. Its "Rider name" (73.92) and "Other rider" (78) both ask more than 84 at 320, so the margin becomes 96, and both names are whole with their notes under them. The row keeps its name and its spec: it still pins `WrapEndNotes` at 320, and the drawing it pins is the one the switch now gives.
  - **`ends/note-below-crowded` does not change.** Its names (Ann, Bo, Cy, Di, Ed) ask at most 40.32, under the 94 half cap at 340, so the rule returns 0.53.0's margin. The row stays as proof that short names are untouched. This corrects the brief, which expected both `ends/*` rows to change.
- **HTTP:** no change. The `wrapEndNotes` check (200) stands. The script stays at 338.
- **Browser:** no component change. The changed row is looked at in Playwright at 320, 340 and 360 from `svg-out`.
- **Recipes:** `python check.py` and `dotnet run -c Release` with `#nullable enable`. Every recipe compiles and renders, after the trivial fixes of §2.5.
- **Accessibility:** a whole name needs no tooltip. A cut name keeps `role='img'` with the whole label as its name. Every end label stays inside `Width − 4`.

## 5. Docs

The sentence that says the cap is still half the drawing, and that "Rider name" is still cut, is restated wherever it was stated:
- `README.md:1018` (the end-label bullet) and `README.md:1544` (0.53.0 additions, which gains a pointer to 0.53.1);
- `api.md:107` (the `WrapEndNotes` row);
- `HANDOVER.md:7` (the release line), `:8` (the open question and the two follow-ups, now done) and `:71` (the feedback item);
- `docs/VERIFICATION.md:7`.

Also:
- **README:** a "0.53.1 additions" section above "0.53.0 additions" (`README.md:1539`).
- **VERIFICATION:** a 0.53.1 paragraph and the counts (`:102–106`).
- **Baseline README** (`tests/Lumen.Charts.Baseline/README.md:10–22`): the reference is v0.53.1, `ends/note-below-320` changed, and the `probe ends` mode.
- **Recipes README:** the check runs with nullable analysis on.
- **SKILL.md:** no change. Its 0.53.0 line (`:105`) does not state the limit.
- `recipes-race-face.md:1133` as §2.6. The phone notes (`:13`) already say the switch keeps a long end label whole.
- `Models.cs:878–884`: `WrapEndNotes`' doc comment, the margin sentence.
- `Directory.Build.props:7`: 0.53.1.

## 6. Order of work

1. **The margin rule.** `Ending` and its summary, the `WrapEndNotes` doc comment, the new and the changed unit tests, `probe ends`, and the `ends/note-below-320` row checked against `v0.53.0`.
2. **The recipe follow-ups.** `#nullable enable` in `check.py`, the recipes it then fails fixed or reported, the 260 sentence, and the Recipes README.
3. **Docs and the version.** README, `api.md`, HANDOVER, VERIFICATION, the Baseline README and its reference files, and `Directory.Build.props`.

## 7. Calls made (Claude's, for the owner to check)

1. Under `WrapEndNotes`, the margin's cap rises to the widest name's own ask (lead, step and pad included). Notes never raise it.
2. The floor is a share of the drawing: the plot keeps at least two fifths (`2 × Width / 5`). "Rider name" is whole at 320, and a 14-letter name from 340. A name past about 13 letters is still cut at 320.
3. Not 160 at 320: that is today's cap and changes nothing.
4. The rule returns 0.53.0's double wherever no name asks more than the half cap. So the crowded row and every chart with short names are byte-identical with the switch on, too.
5. No change to the passes, the thin-pane guard or `Spread`. They read the wider room and mark fewer labels.
6. Only `ends/note-below-320` changes, and it keeps its name. `ends/note-below-crowded` is unchanged. The brief expected both rows to change.
7. A `probe ends` mode proves the change touches only charts with a name over the half cap.
8. The recipe check writes `#nullable enable`. Failing recipes are fixed only where the fix is trivially correct; the rest are reported.
9. The raster recipe gives 210 for the 240 floor and 260 for the 190 floor.
10. Version 0.53.1, not 0.54.0.
