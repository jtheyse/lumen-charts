# Parked follow-ups (0.52.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the follow-ups that are still open on master and change no existing drawing:
- the places and points options parked for 0.45.1;
- the heatmap gaps from Race Face: words in narrow fitted cells, kept values in short rows, row names in text ink, and per-row heights;
- a 190 floor for untitled charts;
- null text read as empty;
- the recipe notes Race Face's sessions left.

**Architecture:**
- **Places and points** stays in `PlacingsChart.Build`: two new options, a new date order, and one points line per scoring series.
- **Cell words** are a flag on the shared `CellLines` and a new give-up step in `CellBlock`.
- **Heatmap rows** are two `ChartSpec` switches. A new internal helper, `HeatmapRows`, gives every row's top, middle and height. The drawing, the frozen name band and the fitted height all read it, so the band cannot drift from the drawing.
- **The floor** is a parameter on `ChartValidation.Dimensions`.
- **Null text** is replaced by `""` at the renderer's entry points, before anything measures or hashes the spec.

**Tech stack:**
- C# / .NET 8 for the libraries and .NET 10 for the gallery and tests.
- ASP.NET Core minimal API.
- Blazor Server and WebAssembly.
- Playwright for .NET with axe-core.

**Spec:** `docs/superpowers/specs/2026-11-01-parked-follow-ups-design.md`. It is the binding authority; where this plan settles one of its ambiguities, the plan says so.

## Global Constraints

- **Baseline:** the 426 existing baseline rows stay byte-identical in both finishes. Only the seven named rows are added, so the total becomes 433.
- **Public API:** every new public member has an XML doc comment, and the Release build has 0 warnings.
- **Validation:** errors are `ArgumentException`, in `ChartValidation`'s voice: a plain sentence that names the option and says why.
- **Data:** invented data only.
- **SKILL.md:** the frontmatter stays byte-identical.
- **The brief:** never open `lumen-charts-race-face-brief.md`.
- **Accessibility:** never colour alone, and drawn text clears 4.5:1.
- **Git:** stage explicit file paths only.
- **Start point:** a parallel branch is making 0.51.1 (a WebAssembly Components page; it touches README, VERIFICATION, `Directory.Build.props` and the browser tests). Build 0.52.0 from master after 0.51.1 merges. Every count in the docs comes from your own runs.

## Review Focus

1. **`FitEachRow` where every row needs the same height** draws its rows and names exactly where `FitHeight` alone puts them. The helper falls through to today's equal split, so a uniform table never moves by a rounding bit. Test: Task 3.
2. **The frozen band matches the drawing** when the source wraps to two lines and the column labels stand under the grid. In that layout `HeatmapFrame`'s top and bottom both move. Test: Task 3.
3. **An undated race in a chart with two scoring series** moves to the front, and each points line starts at its own series' first race. A points line whose first race has no points starts with a gap, and it is never drawn as zero. Test: Task 1.
4. **An untitled line chart at 190 with a two-line source and a six-series legend** keeps every mark and text inside the drawing. The legend adds rows under `Height`, and the source takes 14 from the body. Test: Task 4.
5. **A sparkline with a null `Title`** gets the same `aria-label`, `<title>` and IDs as one with `""`, because a sparkline's title is its only name. Test: Task 4.

---

### Task 1: Places and points

**Files:**
- Modify `src/Lumen.Charts/Placings.cs`:
  - the option records at 25–42;
  - `Build` at 58–112: the `PointsName` check at 63, the date order at 73, the palette check and the 32-line limit at 79–85, the description at 99–100, and the points line at 104–111;
  - the XML docs on `Placing.Points` (11–13) and `Placing.Date` (14), and `Build`'s `<summary>` and `<exception>` (49–57).
- Modify `tests/Lumen.Charts.Tests/Program.cs`:
  - add a `// 0.52.0: places and points` block before `string ChartMarkup(` (12888);
  - update three 0.45.0 tests whose behaviour this design changes on purpose (§7.3). Name each in your report:
    - **9597** "races are ordered by date when all have one, else kept as given". Its `mixed` case becomes `{5, 4, 3}`, labelled `#1`, `Final`, `15 Mar 2027`. Rename the test to say undated races come first.
    - **9663**. The 32 scoring series now draw 64 lines, so the message names 64 and 32.
    - **9682–9687** "empty style palette is ok when not needed". With two scoring series, `PointsColor` no longer colours anything. Keep the case, and assert that each points line takes its places line's colour.
- Modify `tests/Lumen.Charts.Baseline/Program.cs`: add two rows in a new `// 0.52.0` block after the events rows (just before `if (args.FirstOrDefault() == "dump-finish")`, 986).

**Interfaces:**
- **Produces** `PlacingsOptions.PaneLabel` (`string?`, default null): the points pane's label. Null means `PointsName`. A non-null blank is refused with `ArgumentException("A places chart's PaneLabel names its points pane; leave it null to use PointsName, not blank.")`.
- **Produces** `PlacingsOptions.Description` (`string?`, default null): the chart's description.
  - Every `{best}` is replaced by the best place, `best.ToString(CultureInfo.InvariantCulture)`.
  - Null gives today's two sentences, exactly as at 99–100.
  - Text without `{best}` is used as given, and `""` is allowed.
- **Date order:** the undated results first, in the order given, then the dated ones by date, stable: `placed.Where(r => r.Date is null).Concat(placed.Where(r => r.Date is not null).OrderBy(r => r.Date!.Value))`. `Unlabelled` still numbers by drawn position (`i + 1` at 75, unchanged).
- **Points lines:**
  - Let `scoring` be the keys of series with any points, in `keys` order. Each scoring key gets one line in pane 1, with a point at each of that series' races, `new ChartPoint(i, r.Points, labels[i])`, so a race without points is a gap.
  - **One scoring key:** named `PointsName`, coloured `PointsColor ?? palette[lines.Count % palette.Count]`. This is today's output byte for byte, since today's filter at 108 picks exactly that series' races.
  - **Several scoring keys:** named `$"{PointsName} · {key}"` (`PointsName` for the blank key), coloured `Color(k)` with `k` the key's places line index. `PointsColor` is ignored (§7.2).
  - **Series order:** `[.. lines, .. pointsLines]`.
- **Palette and limit:**
  - The palette is needed when `PlaceColors is null || (PointsColor is null && scoring.Count == 1)`. That settles 79–84 for several points lines, which no longer need it.
  - The limit counts `keys.Count + scoring.Count`. Its message becomes "A places chart draws a line for each series and one for each series that scores: {drawn} lines is more than the 32 a chart can hold."
- **The pane:** `new ChartPane { Label = o.PaneLabel ?? o.PointsName, Weight = 1 }`.
- `<LumenPlacings>` is unchanged.

- [ ] **Step 1: Write the failing unit tests:**
  - **"Placings 0.52.0: PaneLabel labels the pane and leaves the line's name"**:
    - with `PointsName = "Pts", PaneLabel = "Points"`, `Panes[0].Label == "Points"` and the points series is named `Pts`;
    - `PaneLabel = " "` is refused, and the message contains `PaneLabel`.
  - **"Placings 0.52.0: Description fills {best} and defaults to today's sentence"**:
    - `"Best {best} of the season"` gives `Best 3 of the season`;
    - null gives `Finishing place out of the field, first at the top, and points. Best: 3.`, and without points `… first at the top. Best: 3.`
  - **"Placings 0.52.0: undated races come first, then dated by date"**:
    - input `[dated w2, undated A, dated w0, undated B (Label "Final"), dated w0 again]`;
    - Y order `A, Final, w0, w0-again, w2`, so same-date ties keep their order;
    - labels `#1`, `Final`, then the dates.
  - **"Placings 0.52.0: two scoring series draw two points lines"**:
    - League and Open both score, and one Open race has no points;
    - the points series are named `Points · League` and `Points · Open`, each `Pane == 1`, each `Color` equals its places line's `Color`, and `PointsColor = "#123456"` appears on neither;
    - each points line's X values are exactly its own series' race indexes;
    - the Open race without points is a `Y == null` point, not 0;
    - a blank-series scoring line is named `Points`.
  - **"Placings 0.52.0: one scoring series is byte-identical to 0.51.0"**: render the baseline's `league` and `placed` data exactly as `placings/points`, `placings/no-points` and `placings/two-series` do. The SHA-256 prefix (16 hex digits, as the baseline's `Hash`) equals each row's hash in `tests/Lumen.Charts.Baseline/reference/refined.txt`.
  - **"Placings 0.52.0: the 32-line limit counts every points line"**:
    - 16 scoring series is 32 lines and builds;
    - 17 is refused naming 34.
  - **Review Focus 3**: two scoring series, the first-given race undated, Open, without points. It is drawn first (X 0), and `Points · Open` starts with `Y == null` at X 0.
- [ ] **Step 2: Add the baseline rows**, invented data on the 0.45.0 `placed` set:
  - `placings/two-scoring`: `placed` with the two Open races given `Points = 12` and `Points = 15`;
  - `placings/undated-first`: `league` with its third result's `Date = null`.

  Both use `new() { Style = Finished(null, ChartTheme.Light) }`, as the 0.45.0 rows do.
- [ ] **Step 3: Run and check they fail.** Build the tests, then run `dotnet run -c Release --project tests/Lumen.Charts.Tests`. Expect: the new tests FAIL, because `PaneLabel` and `Description` do not compile until Step 4; stub them first if you need to see the behaviour failures.
- [ ] **Step 4: Implement the Interfaces above in `Placings.cs`.** Rewrite the touched XML docs so they say what is now true: date order, one points line per scoring series, `PointsName` versus `PaneLabel`, and `PointsColor` for a lone points line only.
- [ ] **Step 5: Run the unit tests and both baselines.**
  - The unit tests all pass.
  - `dotnet run -c Release` and `dotnet run -c Release -- classic` in `tests/Lumen.Charts.Baseline` each write 428 rows. Diffed against `reference/*.txt` (the README's `diff` command), the only changes are the two added rows.
- [ ] **Step 6: Commit.** Stage `src/Lumen.Charts/Placings.cs`, `tests/Lumen.Charts.Tests/Program.cs` and `tests/Lumen.Charts.Baseline/Program.cs`, then:

  ```bash
  git commit -m "0.52.0 places and points: PaneLabel, Description with {best}, undated races first, a points line per scoring series"
  ```

### Task 2: Heatmap cell words

**Files:**
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - `CellLines` (3426–3460) and its `<summary>` (3420–3425);
  - `CellBlock` (3541–3570) and its `<summary>` (3530–3540);
  - `CellNeed` (3576–3585).

  `PartsOf` (3516–3528) is unchanged: the reason's lines are worked out in `CellBlock` and `CellNeed`, not in `PartsOf`.
- Modify `tests/Lumen.Charts.Tests/Program.cs`: add a `// 0.52.0: heatmap cell words` block.
- Modify `tests/Lumen.Charts.Baseline/Program.cs`: add two rows to the 0.52.0 block.
- Modify `tests/Lumen.Charts.Baseline/Probe.cs`: add a `heatmaps <out.txt>` mode, run as `dotnet run -c Release -- probe heatmaps out.txt`. `Program.cs:5` passes `args` through.

**Interfaces:**
- **`CellLines(string said, double room, int most, bool keepWords = false)`.**
  - **With `keepWords`**, a word wider than a line, first or later, takes a line of its own, cut by its characters with `Fit`. The words after it continue on the next line; the loop does not `break` at 3446 or 3449.
  - Joining, the leftover-words ellipsis (3451–3457) and the final `Fit` pass are unchanged. A reason with no over-wide word therefore wraps exactly as today.
  - **Without the flag**, nothing changes.
  - `NoteLines`' call (3492) is unchanged.
- **The flag's callers:**
  - `CellBlock` passes `s.FitHeight` at both of its calls (3548 and 3554).
  - `CellNeed` passes `s.FitHeight` at 3581, so fitted rows grow for the extra lines.
  - Expected at `CellWidth` 34 with `FitHeight`: "too few starts to rate" gives `too` / `few` / `sta…` / `to rate`. Pin the cut word from your run if the estimate gives another prefix, but it must end "…" and be followed by `to rate`.
- **A kept value stays.** In `CellBlock`, after the note and the sub-label are given up (3550–3551) and before the value is (3552), apply this to a cell with `parts.Kept && value is not null && parts.Said is { } said` that still does not fit:
  - `under = (int)Math.Floor((tall - 13) / 12)`, the lines left under an 11 px value line (12 + 1);
  - if `under >= 1`, `reason = CellLines(said, room, under, s.FitHeight)` and the value is kept;
  - otherwise the existing steps run: drop the value, then cut the reason to `Math.Floor(tall / 12)` lines, as today.

  Rated cells and not-kept not-rated cells take exactly today's path.
- **The probe:**
  - It writes `i hash` for 10,000 heatmaps from a fixed seed (`new Random(52)`), using only 0.51.0 API. The random choices:
    - rows 1–8 and columns 1–12;
    - `CellText`, `FitHeight`, `CellNotes`, `NotRatedKeepsValue`, `ColumnLabelsOnTop` and `IncludeZero` on or off;
    - `CellWidth` null or 24–120;
    - `Width` 320–1200 and `Height` 240–600;
    - a source of zero, one or two lines;
    - cells rated, not rated ("too few starts to rate", "injured", "no data", "withdrew") or gaps ("did not race", "DNF"), with or without a value, sub-label and note;
    - Light, Dark or Midnight, in either finish.
  - With `NotRatedKeepsValue` or `CellNotes` on, it turns `CellText` on, so every spec validates. If a spec still throws, write `i refused`; the two runs must agree.
  - It also writes `touched` after a spec's hash when the spec could meet a 0.52.0 change, by this conservative, public test: `FitHeight` with `CellWidth` null or under 60 and any `NotRated` or `GapLabel`; or `NotRatedKeepsValue` without `FitHeight` and any not-rated cell with a value. (The design's "without the new options" cannot hold for these, since §2.2's changes take no switch; this settles it.)

- [ ] **Step 1: Write the failing unit tests:**
  - **"Heatmap 0.52.0: a fitted reason keeps every word"**:
    - one row of not-rated cells reading "too few starts to rate", plus seven rated rows so the chart clears the 240 floor;
    - at `CellWidth` 24, 34 and 43 with `FitHeight`, read the cell's 10 px lines in order;
    - every one of the five words appears either whole or as a prefix ending "…";
    - none is wider than `CellWidth − 6` by `ChartSvg.Wide(text) * 10 / 11`;
    - the row's rect height plus 2 equals `ChartSvg.CellNeed(spec, point, cw) + 4`.
  - **"Heatmap 0.52.0: at 34 the reason reads too, few, sta…, to rate"**: the exact lines, pinned.
  - **"Heatmap 0.52.0: without FitHeight a reason wraps as 0.51.0"**: the same cells at `CellWidth` 34 without `FitHeight` write `too` and `few…`.
  - **"Heatmap 0.52.0: a 40-unit row keeps the kept value and one reason line"**:
    - `CategoryGrid`'s data (four rows in `Height` 320, so `ch` = 40) with `CellWidth = 90, NotRatedKeepsValue = true`;
    - the not-rated cell with a value writes exactly two lines: its 11 px value, then one 10 px line starting `too few` and ending `…`;
    - the not-rated cell without a value writes its reason as before.
  - **"Heatmap 0.52.0: kept values give way when not one reason line fits"**: in a 24-unit row (`tall` 20, so `under` is 0), the cell writes its reason cut to one line and no value, as 0.51.0 does.
- [ ] **Step 2: Add the baseline rows:**
  - `heatmap-cells/narrow-fit`: `table with { CellWidth = 34, FitHeight = true }`;
  - `heatmap-cells/keeps-value-short`: `noted with { CellWidth = 90, NotRatedKeepsValue = true }`.
- [ ] **Step 3: Run the tests and check they fail.**
- [ ] **Step 4: Implement** `CellLines`' flag, its three callers and the kept-value step. Rewrite the `<summary>`s of `CellLines`, `CellBlock` and `CellNeed` to say what is now true, with "(0.52.0)".
- [ ] **Step 5: Run the probe against 0.51.0.**
  - Create a worktree for the old code with `git worktree add <short path> v0.51.0`, and copy `tests/Lumen.Charts.Baseline` into it, as the Baseline README says.
  - Run `dotnet run -c Release -- probe heatmaps old.txt` there and `… new.txt` here.
  - Expected: every line not marked `touched` is identical in both files.
  - Report the count of `touched` specs, and how many of them differ.
  - Remove the worktree afterwards.
- [ ] **Step 6: Run the unit tests and both baselines.** All tests pass. Each finish has 430 rows, and the only changes are the four added rows.
- [ ] **Step 7: Commit.** Stage `src/Lumen.Charts/ChartSvg.cs`, `tests/Lumen.Charts.Tests/Program.cs`, `tests/Lumen.Charts.Baseline/Program.cs` and `tests/Lumen.Charts.Baseline/Probe.cs`, then:

  ```bash
  git commit -m "0.52.0 heatmap cell words: fitted reasons keep every word, a kept value stays in a short row"
  ```

### Task 3: Heatmap rows

**Files:**
- Modify `src/Lumen.Charts/Models.cs`: add two properties after `NotRatedKeepsValue` (857), before `ChartSpec`'s closing brace (858).
- Modify `src/Lumen.Charts/ChartValidation.cs`: two refusals beside the 0.46.2 one at 127–128.
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - `Unfitted` (109–122) and its `<summary>` (100–108);
  - the heatmap branch of `FittedHeight` (1761–1770);
  - `Heatmap` (3313–3382): names at 3326–3331, and the cell `y` and `ch` at 3325 and 3336;
  - `HeatmapNames` (3384–3389);
  - `HeatmapNameBand` (3402–3419), including the name fill at 3416.
- No change to `src/Lumen.Charts.Blazor/LumenChart.razor`. Its band (line 11, `FreezeStyle` at 95, `HeatmapNameBand` at 200) draws whatever `HeatmapNameBand` returns.
- Modify `samples/Lumen.Gallery/SportsData.cs`:
  - the Category heatmap spec (1097–1108) gets `FitEachRow = true, RowNamesInText = true`;
  - its card text (1178) names both;
  - its comment (1090–1096) says the rows are each as tall as their own words.

  Turning `RowNamesInText` on as well is this plan's call: the card shows Race Face's table, whose names were in full ink.
- Modify `tests/Lumen.Charts.Tests/Program.cs`:
  - add a `// 0.52.0: heatmap rows` block;
  - update the gallery card test at 7613–7652. Its `viewBox` `0 0 580 442` and its comment's "each of the four rows is 77 tall" become each row's own height. Compute them from `CellNeed`, write the arithmetic in the comment, and pin the new `viewBox`.
- Modify `tests/Lumen.Charts.BrowserTests/Program.cs`:
  - update the 375-px phone test at 2163–2215. Its `rowNames[3] ≈ 77` check (2202) assumes equal rows: check instead that each name stands at its own row's middle plus 4 (the existing `rowNames[2]` check), and that the rows' heights are not all equal;
  - add the new test below.
- Modify `tests/verify-api.ps1`: add the HTTP checks before the summary line (657).
- Modify `tests/Lumen.Charts.Baseline/Program.cs`: add two rows to the 0.52.0 block.

**Interfaces:**
- **Produces** on `ChartSpec`, each with an XML doc in the style of `NotRatedKeepsValue`'s, and each saying it is "left out of the gradient-ID hash while false":
  - **`public bool RowNamesInText { get; init; }`**: the row names are drawn in `ChartStyle.Text` instead of `Muted`, in the drawing and in the frozen band, in both finishes. Heatmaps only.
  - **`public bool FitEachRow { get; init; }`**: each row is as tall as its own tallest cell's words. It needs `FitHeight`. Heatmaps only.
- **Refusals**, in `ChartValidation.Validate` after 128:
  - `if ((spec.RowNamesInText || spec.FitEachRow) && spec.Kind != ChartKind.Heatmap) throw new ArgumentException("RowNamesInText and FitEachRow set how a heatmap writes and sizes its rows, so they apply to heatmaps only; the other kinds have no rows of cells.");`
  - `if (spec.FitEachRow && !spec.FitHeight) throw new ArgumentException("FitEachRow makes each of a heatmap's rows as tall as its own cells' words, so it needs FitHeight, which draws the heatmap as tall as its rows.");`
- **The hash:** in `Unfitted`, `RowNamesInText` and `FitEachRow` are serialized only when `true`, as `CellNotes` is at 120.
- **Produces** `private static double[] HeatmapRowNeeds(ChartSpec s)`. For each series, `Math.Max(HeatmapRow, max over its points of CellNeed(s, p, cw) + 4)`, with `cw` worked out as at 1766–1767. `FittedHeight` uses it:
  - without `FitEachRow`, the row is the maximum of the needs, which equals today's 1768;
  - with `FitEachRow`, the sum of the needs replaces `s.Series.Count * row` at 1769;
  - the 240 floor stays.
- **Produces** `internal static (double Top, double Middle, double Height)[] HeatmapRows(ChartSpec s, double top, double bottom)`, where `s` is the spec as drawn and `top`/`bottom` are the grid's:
  - **Without `FitEachRow`, or when every need is equal (Review Focus 1):** `ch = (bottom - top) / n`, `Top = top + si * ch`, `Middle = top + (si + .5) * ch`, `Height = ch`. These are today's exact expressions (3336 and 3389), so no row moves by a rounding bit.
  - **Otherwise:** `extra = (bottom - top - Σneeds) / n`. Then `Height = need + extra`, `Top` is `top` plus the heights above, and `Middle = Top + Height / 2`.
- **The users of `HeatmapRows`:**
  - `Heatmap` draws each row's cells at its `Top` with its `Height` in place of `ch`, for the box and for `CellBlock`'s `cy` and `ch`.
  - `HeatmapNames(ChartSpec s, bool refined, double left, (double Top, double Middle, double Height)[] rows)` writes each name at `rows[si].Middle + 4`. It replaces the `top`/`ch` parameters.
  - `HeatmapNameBand` passes `HeatmapRows(s, top, bottom)` in place of `ch` (3410, used at 3415).
- **Row ink:**
  - In `Heatmap`, a name is written with `"text-anchor='end'"` alone when `s.RowNamesInText`, so it takes the root's `currentColor`, `Style.Text`, and with `"text-anchor='end' class='lumen-muted'"` otherwise, as today.
  - In the band, `fill='{(s.RowNamesInText ? style.Text : style.Muted)}'`.

- [ ] **Step 1: Write the failing tests.**
  - **Unit tests:**
    - **"Heatmap 0.52.0: RowNamesInText writes the names in Text"**:
      - with the switch, no row name has `class='lumen-muted'`, in both finishes, and the band's names have `fill` equal to `ChartStyle.Light.Text`;
      - without it, both read as before;
      - `Contrast.Ratio(Text, Background) >= 4.5` for Light, Dark and Midnight.
    - **"Heatmap 0.52.0: FitEachRow gives each row its own height"**:
      - `noted` with `CellWidth = 90, FitHeight = true, CellNotes = true, NotRatedKeepsValue = true, FitEachRow = true`;
      - each row's rect height plus 2 equals that row's `HeatmapRowNeeds` entry, and the rows are not all equal;
      - the drawing's height is `80 + Σneeds + HeatmapBelow`;
      - each name's `y` is its row's middle plus 4, and the band's name `y`s equal the drawing's.
    - **"Heatmap 0.52.0: under the floor the rows share the extra height"**: two short rows, needs 36 and 49. Each row's height minus its need is the same, and the heights add up to the grid's span.
    - **Review Focus 1**: four rows with equal needs. `FitEachRow` and `FitHeight` alone give the same rect `y`/`height` attributes and the same name `y`s.
    - **Review Focus 2**: `FitEachRow` with a two-line `Source` and `ColumnLabelsOnTop = false`. The band's name `y`s equal the drawing's, and every band name lies inside the band's `viewBox`.
    - **"Heatmap 0.52.0: the row switches are refused where they cannot apply"**:
      - each switch on a line chart is refused, and the message names it;
      - `FitEachRow` without `FitHeight` is refused, and the message names `FitHeight`.
    - **"Heatmap 0.52.0: neither switch is hashed at its default"**: `ChartSvg.IdPrefix` of `noted` equals that of `noted with { RowNamesInText = false, FitEachRow = false }`, and differs once either is true.
  - **Browser test** **"The Category heatmap with FitEachRow keeps its frozen names on their rows while the cells scroll"**:
    - widths 360 and 1280;
    - at each, scroll the viewport by 200 and back to 0, and at both positions check that each `.lumen-freeze svg text`'s screen `top + height/2` is within 1 px of its row's cells' screen middle;
    - then switch Light, Dark and Midnight by the theme buttons, as the `keptValues` test (2051–2070) does, and run `SweepOf` in each.
  - **HTTP checks** in `verify-api.ps1`:
    - a heatmap posted with `"rowNamesInText":true,"fitHeight":true,"fitEachRow":true,"cellText":true` returns 200 with no `class='lumen-muted'` on its `text-anchor='end'` names;
    - `"fitEachRow":true` without `fitHeight` returns 400 with `FitHeight` in the body;
    - `"rowNamesInText":true` on a line chart returns 400.
- [ ] **Step 2: Add the baseline rows:**
  - `heatmap-cells/row-ink`: `noted with { CellWidth = 90, FitHeight = true, CellNotes = true, RowNamesInText = true }`;
  - `heatmap-cells/each-row`: `noted with { CellWidth = 90, FitHeight = true, CellNotes = true, NotRatedKeepsValue = true, FitEachRow = true }`.
- [ ] **Step 3: Build and run the unit tests, and check the new ones fail.**
- [ ] **Step 4: Implement** the properties, the refusals, the hash, `HeatmapRowNeeds`, `HeatmapRows` and its users, the row ink, and the gallery card. Update the two existing tests named under Files. Rewrite the `<summary>`s of `FittedHeight`, `HeatmapNames` and `HeatmapNameBand`.
- [ ] **Step 5: Run the full suites:**
  - the unit tests;
  - both baselines: 432 rows each, with only the six added rows changed;
  - `verify-api.ps1` against the gallery;
  - the gallery browser suite;
  - the WebAssembly suite, since the band is drawn in the component.

  Stop `Lumen.Gallery` before the first build (it locks the build).
- [ ] **Step 6: Commit.** Stage `src/Lumen.Charts/Models.cs`, `src/Lumen.Charts/ChartValidation.cs`, `src/Lumen.Charts/ChartSvg.cs`, `samples/Lumen.Gallery/SportsData.cs`, `tests/Lumen.Charts.Tests/Program.cs`, `tests/Lumen.Charts.BrowserTests/Program.cs`, `tests/verify-api.ps1` and `tests/Lumen.Charts.Baseline/Program.cs`, then:

  ```bash
  git commit -m "0.52.0 heatmap rows: RowNamesInText and FitEachRow, one row helper for the drawing, the band and the fitted height"
  ```

### Task 4: The 190 floor and null text

**Files:**
- Modify `src/Lumen.Charts/ChartValidation.cs`: `Dimensions` (956–964) and its call at 18.
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - add `WithText`;
  - call it first in `AsDrawn` (3232–3237), which `Render` (245, `AsDrawn` at 250), `HeatmapFrame` (3241) and `HeatmapNameBand` (3406) all go through before `Begin` (266), the source's `Wrap` (269), `Headroom` (446), `FittedHeight` (1769, 1773) and `Stripped` (3078, 3092) read the text;
  - the `IdPrefix` that `SvgWriter` takes from `w.Spec` (60) then hashes the replaced spec.
- Modify `src/Lumen.Charts/Readout.cs`: `spec = WithText(spec)` after the validation in `Readout` (80–82, ahead of 88–89) and in `Plot` (215–217, ahead of 219–220).
- Modify `src/Lumen.Charts/GraphEngine.cs:65`: `graph.Title ?? ""`, as `24a14b8` did.
- Modify `src/Lumen.Charts/Models.cs`: the XML docs of `ChartSpec.Title`, `Description` and `Source` (551–560) and of `GraphSpec.Title` (986) say that null is read as `""`.
- Modify `tests/Lumen.Charts.Tests/Program.cs`, `tests/verify-api.ps1` and `tests/Lumen.Charts.Baseline/Program.cs` (one row).
- **Reference only** (read, never merge): `git show 07760c5`, `git show 24a14b8`, and `git -C ".claude/worktrees/modest-yonath-e5d74e" diff`. Those branches normalised inside `Render` before `FitHeight`. `AsDrawn` came later (0.46.1), so the entry point is now `AsDrawn` plus `Readout` and `Plot`.

**Interfaces:**
- **Produces** `internal static ChartSpec WithText(ChartSpec spec)`: the same instance when `Title`, `Description` and `Source` are all non-null; otherwise `spec with { … }` with each null one set to `""`.
- **`Dimensions(int width, int height, bool sparkline = false, bool strip = false, bool untitled = false)`.** The final line's floor is `untitled ? 240 - ChartSvg.Untitled : 240`, which is 190.
  - Titled message: "Dimensions must be {MinWidth}–{MaxWidth} by 240–2160.", unchanged.
  - Untitled message: "An untitled chart's dimensions must be {MinWidth}–{MaxWidth} by 190–2160."
  - The call at 18 passes `untitled: !spec.DrawTitles`.
  - The graph's call (`GraphEngine.cs:458`) and the planner keep their rules.
  - A fitted heatmap keeps its drawn 240 floor in `FittedHeight` (1769). The given `Height` is only validated (the design's "keeps its 240 floor" read as the drawn height, since `FitHeight` replaces the given one).
  - **If the each-kind test finds a kind that cannot draw cleanly at 190** (§7.7: "every chart kind that renders cleanly there"), `Dimensions` takes the kind and keeps 240 for it. Add that kind to the message, and report it.
- **The planner** still refuses a null `Description` (`PlannerValidation.cs:27`).

- [ ] **Step 1: Write the failing tests.**
  - **Unit tests:**
    - **"Floor 0.52.0: an untitled line chart renders at 190 and 220"**:
      - both render;
      - 189 untitled is refused with `190–2160` in the message;
      - 239 titled is refused with exactly the 0.51.0 message.
    - **"Floor 0.52.0: every kind draws inside an untitled 190"**:
      - for each `ChartKind` (strip aside, since it draws at its own height), the baseline's `Spec(kind, Light)` shape with `DrawTitles = false, Height = 190, Width = 340`, with `FitHeight` off;
      - every `text` `x`/`y`, `rect`, `circle` (`c ± r`) and `line` endpoint, and every numeric pair in a `path`'s `d` (arc radii aside, as `Part39` reads them), lies inside the `viewBox`.
    - **Review Focus 4**: a line chart with six series, `Source` long enough to wrap to two lines, untitled at 190, with every mark and text inside, the legend's rows included.
    - **"Null text 0.52.0: every kind draws a null title, source and description as empty ones"**:
      - each `ChartKind`, with `DrawTitles` true and false, and a heatmap with `CellWidth` and `FitHeight`;
      - `Render` of `spec with { Title = null!, Source = null!, Description = null! }` equals `Render` of the same with `""`. That covers the gradient IDs, which come from the hash;
      - so do `Readout` (on a line chart) and `Plot` (on a line and a timeline), and `HeatmapNameBand`.
    - **Review Focus 5**: a sparkline with a null `Title` renders equal to one with `""`.
    - **"Null text 0.52.0: a graph's null title draws as an empty one"**: graphs that are layered, top-to-bottom, circular, empty (no nodes), classic, and dragged (`positions` given), each null equal to `""`.
    - **"Null text 0.52.0: JSON null reads as empty"**: `JsonSerializer.Deserialize<ChartSpec>("{\"title\":null,\"source\":null,\"description\":null,…}", web options)` renders equal to the `""` spec, and likewise a `GraphSpec`.
    - **"Null text 0.52.0: the planner still refuses a null Description"**: the existing message, unchanged.
  - **HTTP checks:**
    - an untitled line chart (`"drawTitles":false,"height":190`) returns 200; it returned 400;
    - `"height":189` untitled returns 400;
    - `"title":null` (and `"source":null,"description":null`) on `/api/charts/svg` returns 200;
    - `"title":null` on `/api/charts/graph/svg` returns 200.
- [ ] **Step 2: Add the baseline row** `titles/undrawn-190`: `Render(line with { Title = "Not drawn", Description = "Named, not written", DrawTitles = false, Height = 190 })`, beside the 0.39.0 `titles/undrawn` row's shape.
- [ ] **Step 3: Run the tests and check they fail.**
- [ ] **Step 4: Implement** `WithText` and its four calls, the graph title, `Dimensions`' parameter, and the docs.
- [ ] **Step 5: Run the full suites:**
  - the unit tests;
  - both baselines: 433 rows each, with only the seven added rows changed from the 426;
  - `verify-api.ps1`.
- [ ] **Step 6: Commit.** Stage `src/Lumen.Charts/ChartValidation.cs`, `src/Lumen.Charts/ChartSvg.cs`, `src/Lumen.Charts/Readout.cs`, `src/Lumen.Charts/GraphEngine.cs`, `src/Lumen.Charts/Models.cs`, `tests/Lumen.Charts.Tests/Program.cs`, `tests/verify-api.ps1` and `tests/Lumen.Charts.Baseline/Program.cs`, then:

  ```bash
  git commit -m "0.52.0 a 190 floor for untitled charts, and null title, source and description read as empty"
  ```

### Task 5: Recipe notes, docs and the version

**Files:**
- Modify `integrations/claude-code/lumen-charts/references/recipes-race-face.md`:
  - **The places recipe (105–133):**
    - `placeWords` gains `Description = "Place over field size, and points. Best: {best}."` and `PaneLabel = "Points"`;
    - the "What the call does" bullet says that undated races come first, then dated by date, with one points line per series that scores, named `Pts · League`, in its places line's colour.
    - The design asks to drop "its `Adjust` description and its own undated-first sorting", but the recipe has neither: `Adjust` (124) sets only `Title` and `YLabel`, and there is no sort. So nothing is dropped, and the `Adjust` stays.
  - **The notes of design §2.6**, each in the recipe it belongs to, each checked against the code first. Name the line you checked in your report.
    - **The app's own PB flag and bins:** the PB and finish-time recipes (about 200 and 282).
    - **A sparkline's root is `role='group'`,** named by its title and description: `Begin`, `ChartSvg.cs:410`.
    - **Lumen writes single-quoted attributes.**
    - **Put a description's key fact first,** since `Wrap` cuts at two lines (`ChartSvg.cs:464`).
    - **Laps:** a lap's heart rate of 0 is a null gap, and past four laps the sub-label writes its number alone (the heart-rate-per-lap recipe, about 652).
    - **Power curve:** an app's own durations go before `MeanMaximal` (about 633).
    - **Team rider:** a round that cannot be rated is a plain null with no `GapLabel`, and a 0–1 share is scaled ×100 (about 681).
    - **Host CSS** on `svg text[font-size]` restyles Lumen's text.
- Modify `integrations/claude-code/lumen-charts/references/api.md`:
  - the `Height` row (37): 190 when untitled;
  - beside `FitHeight` (71): `RowNamesInText` and `FitEachRow`;
  - the `PlacingsOptions` text near 232: `PaneLabel`, `Description`, the date order and the points lines;
  - the null text.
- Modify `integrations/claude-code/lumen-charts/SKILL.md`: one body line after 103, "**Parked follow-ups** (0.52.0): …". The frontmatter stays byte-identical.
- Modify `README.md`:
  - **a "## 0.52.0 additions" section** above `## 0.51.0 additions` (1535);
  - **the heatmap Limits paragraph (596):**
    - the 0.46.2 sentence on reasons "below a `CellWidth` of about 43" now says a fitted reason keeps every word;
    - the 40-unit kept-value sentence now says the value and one reason line;
    - "there is no row-height setting and no per-row height" now names `FitEachRow`;
    - the JSON list adds `rowNamesInText` and `fitEachRow`;
  - **the places section (491–):** `PaneLabel`, `Description`, the date order and the points lines;
  - **the floor sentence (130):** an untitled chart is at least 190;
  - **the `FitHeight` bullet (1077):** `FitEachRow`.
- Modify `Directory.Build.props:7` to `0.52.0`.
- Modify `docs/VERIFICATION.md` with a 0.52.0 paragraph and the counts.
- Modify `tests/Lumen.Charts.Baseline/README.md`: the reference is v0.52.0 with 433 rows, the seven added rows named.
- Replace `tests/Lumen.Charts.Baseline/reference/refined.txt` and `classic.txt` with the verified run.
- Modify `HANDOVER.md`, in the Race Face feedback list (69–81):
  - mark **keys (0.36.0)** (70) done, since 0.42.0: the viewport is `tabindex=-1` while the drawing fits;
  - mark the recipe-note items that this release writes (80–81) done in 0.52.0;
  - update the release line (7).

- [ ] **Step 1: Make the edits.** Every count (unit tests, HTTP checks, browser and WebAssembly checks, recipes, baseline rows) comes from your own runs on the branch, rebased on master after 0.51.1.
- [ ] **Step 2: Check the diff:**
  - `git diff --check` is clean;
  - `git diff integrations/claude-code/lumen-charts/SKILL.md` touches no line between the `---` fences.
- [ ] **Step 3: Run everything:**
  - the Release build: 0 warnings;
  - the unit tests;
  - both baselines: 433 rows each, the 426 identical;
  - `verify-api.ps1`;
  - the gallery browser suite;
  - the WebAssembly suite;
  - the recipes (`tests/Lumen.Charts.Recipes`): every recipe compiles and renders.
- [ ] **Step 4: Commit.** Stage each changed file by its path, then:

  ```bash
  git commit -m "0.52.0: recipe notes, README, api.md, SKILL, VERIFICATION, HANDOVER and the version"
  ```
