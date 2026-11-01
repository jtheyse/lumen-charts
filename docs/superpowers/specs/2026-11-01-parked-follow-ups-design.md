# Parked follow-ups (0.52.0)

Design, 1 November 2026. The owner asked for the long-parked follow-ups to be dealt with ("do all using agents"). This release takes the ones that are still open on master, need no ruling, and change no existing drawing:
- the places and points follow-ups designed for 0.45.1;
- the heatmap gaps Race Face found after 0.46.2;
- 0.46.2's two accepted limits;
- a lower height floor for charts without drawn titles;
- null text read as empty (the work left on the `fix/null-*` branches);
- the recipe notes Race Face's sessions left.

The details below are Claude's calls for the owner to check; §7 lists them. What needs the owner's word is left out and listed in §3.

## 1. Intent

Race Face's category heatmap (#16) shipped on 0.46.2 with three nice-to-haves open:
- row names are drawn grey, where its own table had them in full ink;
- every row is as tall as the tallest cell;
- the top padding is too small. This one needs a ruling, since it moves fitted rows.

0.46.2 also accepted two limits:
- a fitted reason in a cell narrower than about 43 loses words;
- in a short fixed row, a not-rated cell that keeps its value writes only its reason.

The places and points chart (0.45.0) has follow-ups that were designed and parked on 7 October; the owner had already chosen one points line per series. A small line chart without its titles, 220 tall, is refused by the 240 floor, though its body has the room of a 270-tall titled chart. JSON's `"title": null`, `"source": null` or `"description": null` makes rendering throw. And the Race Face recipes miss several notes that its sessions gave.

## 2. What changes

1. **Places and points** (the 0.45.1 design, in `Placings.cs`):
   - **`PlacingsOptions.PaneLabel`** (string?; null means `PointsName`) labels the points pane, so the legend can say "Pts" while the pane says "Points". A blank label is refused.
   - **`PlacingsOptions.Description`** (string?; null means today's sentence) is the chart's description. `{best}` is replaced by the best place, written in the invariant culture.
   - **Date order.** Undated results come first, in the order given, then dated results by date (stable). Today, one missing date turns date ordering off for every result. `Unlabelled` still numbers by drawn position.
   - **One points line per scoring series**, drawn only at that series' own races in pane 1:
     - A race of that series without points is a gap, and a series that never scores has no points line.
     - With one points line, nothing changes: it is named `PointsName` and coloured `PointsColor`, or the next default colour.
     - With several, each is named `{PointsName} · {series}` (`PointsName` for the blank series) and coloured as its places line.
     - The 32-line limit counts every points line.
   - Every existing chart with at most one scoring series is byte-identical, including the three `placings/*` rows. `<LumenPlacings>` needs no change.
2. **Heatmap cell words** (`ChartSvg.cs`: `CellLines`, `PartsOf`, `CellBlock`, `CellNeed`):
   - **Fitted reasons keep every word.** With `FitHeight`, a not-rated reason or a gap label wraps as a note does:
     - A word wider than a line takes a line of its own, cut with "…", and the words after it continue on the next line, instead of the reason ending there.
     - The rows grow for the extra lines, since `CellNeed` uses the same lines.
     - At `CellWidth` 34, "too few starts to rate" reads `too` / `few` / `sta…` / `to rate`, not `too` / `few…`.
     - Without `FitHeight`, reasons wrap exactly as today.
   - **A kept value stays in a short row.** For a not-rated cell with `NotRatedKeepsValue`:
     - Things are given up in this order: its note, then its sub-label, then its reason is cut to the lines left under the value, ending "…".
     - The value goes only when not even one reason line fits under it. Then the reason is cut to the whole cell, as today.
     - In a 40-unit row the cell writes its value and one reason line, where today it writes two reason lines.
     - Other cells are unchanged.
3. **Heatmap rows.** Two new options that apply to heatmaps only and are refused on other kinds. They are left out of the gradient-ID hash at their defaults, and the JSON API takes them.
   - **`ChartSpec.RowNamesInText`** (bool, false). Row names are drawn in the style's `Text` colour instead of `Muted`, both in the drawing and in `<LumenChart>`'s frozen name band, in both finishes.
   - **`ChartSpec.FitEachRow`** (bool, false; needs `FitHeight`, and is refused without it):
     - Each row is as tall as its own tallest cell's words (`CellNeed` + 4, at least 36), not as tall as the tallest in the chart.
     - The chart's height is the sum of the rows plus the usual room. Where that falls under the 240 floor, the extra height is shared equally between the rows.
     - Row names stand in the middle of their own rows.
     - One internal helper gives every row's top and height. `Heatmap`, `HeatmapNames`, `HeatmapNameBand` and `FittedHeight` all use it, so the band stays aligned.
     - Without the switch, rows are computed exactly as today.
4. **A lower floor for untitled charts** (`ChartValidation.Dimensions`):
   - With `DrawTitles = false`, a chart may be as low as 190 (240 less `ChartSvg.Untitled`, 50), so its body has the same room as a 240-tall titled chart.
   - The error message for titled charts is unchanged; an untitled chart's says 190–2160.
   - Sparklines, strips, graphs and the planner keep their own rules, and a fitted heatmap keeps its 240 floor.
5. **Null text reads as empty:**
   - `ChartSvg.Render` (and the readout and plot paths) treat a null `Title`, `Source` or `Description` as `""`.
   - `GraphEngine` treats a null graph `Title` as `""`.
   - Today a null from JSON (`"title": null`) throws a `NullReferenceException`.
   - The planner keeps refusing a null `Description` (its own design, tested).
   - This redoes the `fix/null-title-source`, `fix/null-description` and `fix/null-graph-title` work on master; those branches are 167 commits behind.
6. **Recipe and doc notes**, in the skill's `references/recipes-race-face.md` and `api.md`, each checked against the code before writing:
   - use the app's own PB flag and bins;
   - a sparkline's root is `role='group'`, named by its title and description;
   - Lumen writes single-quoted attributes;
   - put a description's key fact first, since a two-line cut can hide its end;
   - a lap's heart rate of 0 is a null gap, and past four laps the sub-label writes its number alone;
   - an app's own power-curve durations go before `MeanMaximal`;
   - a team rider's round that cannot be rated is a plain null with no `GapLabel`;
   - a share given as 0–1 is scaled ×100;
   - host CSS on `svg text[font-size]` restyles Lumen's text.

   The places recipe drops its `Adjust` description and its own undated-first sorting, and uses `Description` with `{best}`.
7. **Version 0.52.0.** This release adds public options and two behaviour changes (date order, and kept values in short rows), so it is a minor release.

## 3. Non-goals

- No existing drawing changes. The 426 baseline rows stay byte-identical in both finishes.
- Left for the owner's ruling:
  - more top padding in fitted cells (#16b), which moves fitted rows;
  - end labels at 320 (a second line for the note, or a wider margin);
  - annotation labels that avoid marks;
  - the static legend's flow layout and the wrapping of long names;
  - #19's raster package.
- Not in this release:
  - changing the default row-name colour;
  - per-row heights without `FitHeight`;
  - a lower floor for fitted heatmaps, the planner or graphs;
  - an HTTP endpoint for places and points;
  - the interaction-state spec (2026-10-08), which stays a spec.

## 4. Tests

- **Unit tests:**
  - **Places:**
    - `PaneLabel` sets the pane's label and leaves the legend's name alone; a blank one is refused.
    - `Description` with `{best}` is filled in, and its null default equals today's sentence.
    - Mixed dated and undated results come undated first, then by date, with stable ties and `Unlabelled` numbered by position.
    - Two scoring series give two points lines with the names, colours and gaps described.
    - One scoring series is byte-identical to 0.51.0's builder for the baseline's `league` and `placed` data.
    - The 32-line limit counts the points lines.
  - **Cell words:**
    - At `CellWidth` 24, 34 and 43 with `FitHeight`, every word of "too few starts to rate" appears, and the row height equals `CellNeed` + 4.
    - A 40-unit fixed row with `NotRatedKeepsValue` writes the value and one reason line ending "…".
    - Without the switches, the output equals 0.51.0's on the baseline heatmaps.
  - **Rows:**
    - `RowNamesInText` writes the names and the band's names in `Text`.
    - `FitEachRow` gives each row its own height, with centred names, and the band matches the drawing. Under the floor, the rows share the extra height.
    - Both options are refused on a line chart, and `FitEachRow` is refused without `FitHeight`.
    - Neither option appears in the gradient hash at its default.
  - **Floor:**
    - An untitled line chart renders at 190 and at 220. At 189 it is refused, and a titled chart at 239 is still refused with today's message.
    - One untitled chart of each `ChartKind` renders at 190 with every text and mark inside the drawing.
  - **Null text:**
    - Every chart kind, titled or not, renders with a null `Title`, `Source` and `Description` the same as with `""`, including the readout, the plot and a gradient ID.
    - A graph with a null `Title` renders the same as with `""`, for layered, top-to-bottom, circular, empty, classic and dragged graphs.
    - JSON `null` works for each.
- **Baseline:**
  - The 426 rows are unchanged in both finishes.
  - New rows: `placings/two-scoring`, `placings/undated-first`, `heatmap-cells/row-ink`, `heatmap-cells/each-row`, `heatmap-cells/narrow-fit`, `heatmap-cells/keeps-value-short` and `titles/undrawn-190`.
  - As in 0.46.2's review, 10,000 random heatmaps without the new options are byte-identical to 0.51.0.
- **HTTP:**
  - `/api/charts/svg` takes `rowNamesInText` and `fitEachRow`.
  - An untitled chart at height 190 returns 200 (it returned 400).
  - `"title": null` returns 200.
- **Browser:**
  - On the gallery's Category heatmap card with `FitEachRow`, the frozen names line up with their rows while scrolling at 360 and 1280 px.
  - Axe passes in Light, Dark and Midnight.
- **Recipes:** every recipe compiles and renders.

## 5. Docs

- README:
  - a "0.52.0 additions" section;
  - the 0.46.2 limits, rewritten;
  - the places section;
  - the floor sentences.
- `api.md`: the new options.
- SKILL.md: one body line.
- VERIFICATION: the counts.
- HANDOVER: the Race Face feedback list, marking the keys item done (it has been done since 0.42.0: the viewport is `tabindex=-1` while the drawing fits).

## 6. Order of work

1. Places and points, with their tests and two baseline rows.
2. Heatmap cell words, with their tests, two baseline rows and the random-heatmap probe.
3. Heatmap rows, with their tests, two baseline rows, the HTTP check, the gallery card and its browser test.
4. The 190 floor and null text, with the each-kind tests, one baseline row and the HTTP checks.
5. Recipe and doc notes, README, `api.md`, the version, VERIFICATION and HANDOVER.

## 7. Calls made (Claude's, for the owner to check)

1. 0.45.1 is built as parked, with "do all" taken as the owner's word.
2. With several points lines, each takes its series' place-line colour and `PointsColor` is ignored; it still colours a lone points line.
3. Undated-first ordering changes the order of existing mixed-date charts.
4. Row names in text ink and per-row heights are opt-ins, not defaults, so no drawing moves.
5. Fitted reasons keep every word by cutting over-wide words with "…", not by breaking them across lines.
6. A kept value is kept before a second reason line in a short row.
7. The untitled floor is 190 for every chart kind that renders cleanly there.
8. Null text reads as empty everywhere except the planner's `Description`.
9. #16b's padding waits for a ruling, since it moves fitted rows.
10. Version 0.52.0.
