# Heatmap table — design (0.46.0)

Date: 7 October 2026. Status: approved in conversation (approach, section 1, section 2, the classic-look decision); awaiting the owner's review of this written spec.

## Purpose

Race Face's chart #16 is a category heatmap: rows are categories, columns are seasons, each cell is points per rider-start with "/12 starts" under the number, and a cell with too few starts is marked "not rated" instead of being coloured as if it were a result. It must read on a phone (the grid scrolls sideways rather than squeezing) and for a screen reader (a real table, rows by columns).

Today's heatmap (`ChartKind.Heatmap`) draws coloured cells only: no text in cells, no "not rated" state, a grid that squeezes to slivers under `FitWidth` on a phone, a flat Series | Category | Value list as its data table, a colour-scale line whose words ("light … dark") are wrong on Midnight and on any style whose low end is darker than its high end, and a Dark preset that reuses Light's heatmap colours (so its brightest cell is the lowest value).

0.46.0 extends the existing heatmap. Nothing in it is specific to racing or Race Face.

### Decisions taken in the design conversation

| Question | Decision |
|---|---|
| Roadmap | 0.46.0 (owner, 6 Oct 2026); 0.45.1 (places and points follow-ups) is parked with its design in the brain, to be built on the owner's word. |
| Approach | Extend `ChartKind.Heatmap` with options that are off by default (not a separate heatmap-table type). |
| Legend fix scope | The colour-scale wording fix and Dark's own heatmap pair apply to the **refined finish only**; `ChartFinish.Classic` keeps reproducing 0.23.0 byte for byte (owner, 7 Oct 2026). |

### Success criteria

- A heatmap with `CellText` writes each cell's value and second line, every word at 4.5:1 against its own cell, or no word at all; the cell's name always carries everything.
- A not-rated cell can never be mistaken for a low score: unshaded, dashed, said in words, and outside the colour scale.
- On a 375 px phone a heatmap with `CellWidth` scrolls sideways at a readable cell size instead of squeezing.
- A screen reader and a static page get a real grid table (`ChartExport.HtmlTable`, and the component's "View data").
- Existing renderings: only the four refined heatmap baseline rows move (the legend wording and, for Dark, the colours), by design; the four classic heatmap rows and every other row stay identical, and the unit tests' classic hash pins stay.

## 1. The cells

### `ChartSpec.CellText` (`bool`, default `false`)

Heatmap only; refused on every other kind with a reason.

- Each drawn cell writes its value on one line and, when the point has a `SubLabel`, the sub-label on a second line beneath it, both centred in the cell.
- `ChartPoint.SubLabel` is now allowed on heatmaps (same limits as today: at most 16 characters, not blank, no line breaks). The category rule that all series' sub-labels in one category agree (column, bar and stacked column charts write one sub-label under the category's name) does **not** apply to heatmaps: each cell's sub-label is its own, such as "/12 starts" and "/4 starts" in one season's column.
- `YFormat` and `YUnit` are now allowed on heatmaps; they format the cell text, the cell's name, the colour-scale line and the HTML table. Default (Number, no unit) leaves every existing name unchanged.
- **Text colour, per cell:** whichever of `Style.Text` and `Style.Background` has the higher contrast with that cell's fill; if neither reaches 4.5:1, pure black `#000000` or pure white `#FFFFFF`, whichever contrasts more (one of them always reaches at least 4.58:1), so every cell writes its value. (Amended during the build by the owner: about a quarter of mid-ramp cells on the default colours would otherwise have written nothing.)
- **Fit:** using the library's text-width estimate, if the sub-label does not fit the cell's width or the two lines do not fit its height, the sub-label is dropped; if the value then does not fit, the value is dropped too. Value text is 11 px, sub-label 10 px.
- `ValueNote` stays name-only (never drawn in the cell).

### `ChartPoint.NotRated` (`string?`, 1–24 characters)

Heatmap only; refused elsewhere with a reason; blank or control characters refused.

- The cell is drawn even when `Y` is null: fill = `Style.Background`, a dashed outline (`stroke-dasharray='3 2'`) in `Style.Muted`, which must clear 3:1 against the background (the existing style checks guarantee Muted's contrast; the dashed outline uses full opacity).
- A not-rated cell's value (if any) is **left out of the colour scale's minimum and maximum**.
- With `CellText`, it writes its value (if any) and sub-label in `Style.Text`, or "—" when it has no value.
- Its name: `"{row}: {column}, {value}{sub}, not rated: {NotRated}"`, with the value part omitted when there is none, e.g. `Long distance: 2025, 2.8 · /4 starts, not rated: too few starts to rate`. (The sub-label is named the way column charts already name it, `" · /4 starts"`.)
- It is focusable like any other cell.
- A null `Y` without `NotRated` still draws no cell, as today.

## 2. Legend, Dark pair, width, table

### Colour-scale line (refined finish only)

- Reads `Color scale: {min} low to {max} high`, the values formatted with `YFormat` and `YUnit`.
- If the line would run past the drawing's right edge (text-width estimate), the `Color scale: ` prefix is dropped; if still too wide, it is cut with "…".
- The classic finish keeps `Color scale: {min} (light) to {max} (dark)` exactly.

### A dark heatmap pair on dark backgrounds (refined finish only)

- The Dark preset itself is **not** changed: its `HeatmapLow`/`HeatmapHigh` feed the calendar ramp (`CalendarLow`, the calendar legend) and the spec hash that names gradients, so changing them would move every Dark calendar and the IDs of other Dark charts.
- Instead the refined heatmap renderer resolves its pair: when the style's pair is the default `#E4EDFC` → `#4069D0` **and** the style's background is dark (relative luminance below 0.2), it uses a dark pair — low close to the background, high clearing 3:1 against it — chosen and verified in the build (a unit test measures it on Dark's `#171E2E`). Any other pair, and every pair on a light background, is used as given. The heatmap's legend keys (`ChartSvg` legend swatches for heatmap rows) use the same resolved pair.
- The classic finish never resolves: it draws the style's pair as given, so Dark's four classic rows stay identical.

### `ChartSpec.CellWidth` (`double?`, at least 24)

Heatmap only; refused elsewhere and below 24.

- Each column takes that width; the drawing's width becomes `165 + columns × CellWidth` (the spec's `Width` is ignored for a heatmap with `CellWidth`; the result must stay within the size limits, else refused with a reason).
- In `<LumenChart FitWidth="true">`, a heatmap with `CellWidth` is drawn at that width and, when wider than its box, scrolls sideways (the component sets `--lumen-drawn`, as `<LumenGraph>` does); it is never squeezed.
- Column labels are thinned so that no two overlap (text-width estimate against the cell width), in place of the fixed "every n past 12 columns" rule — only when `CellWidth` is set, so existing heatmaps do not move.

### `ChartExport.HtmlTable(ChartSpec spec)` → `string`

Heatmaps only (an `ArgumentException` saying so for other kinds); validates the spec first.

- `<table class='lumen-grid-table'><caption>{title}</caption>`, a header row with an empty corner cell and one `<th scope='col'>` per column label, then one row per series with `<th scope='row'>{series name}</th>` and one `<td>` per column: the value (formatted), then `" · {sub-label}"` when present, then `", not rated: {words}"` when not rated; `—` for a not-rated cell without a value; empty for no point / null value.
- Single-quoted attributes and HTML-encoded text, like `PlannerSvg.Table`.

### Component and CSV

- `<LumenChart>`'s "View data" for a heatmap shows the `HtmlTable` grid instead of the flat list (other kinds unchanged).
- `ChartExport.Csv` gains a `NotRated` column (after `Note`) when any point has `NotRated`; otherwise unchanged.

## 3. Tests, gallery, docs

- **Unit:** text colour per cell (light and dark fills, a fill where neither colour clears 4.5:1 → black or white text), fit-and-drop (sub-label first, then value), `YFormat`/`YUnit` on heatmaps, not-rated drawing (dashed outline, background fill, out of the scale, "—"), names with sub-label and not-rated words, `CellWidth` width and refusals, column thinning with `CellWidth`, the refined legend wording and its fitting at 340 wide, the classic legend unchanged, Dark's pair contrast, `HtmlTable` structure (scopes, caption, encoding, refusal for non-heatmaps), CSV `NotRated` column, the component's grid table, every refusal (`CellText`/`CellWidth`/`NotRated` on other kinds, `CellWidth` < 24, `NotRated` length).
- **Baseline:** added rows — cell text in Light, Dark and Midnight; not-rated cells; `CellWidth`. Changed by design — the four refined `Heatmap/*` rows (the legend wording in all four; the dark pair in the two Dark rows). Unchanged — the four classic `Heatmap/*` rows and every other row.
- **Gallery:** a "Category heatmap" card on the Sports & performance page (Racing section) with invented categories and seasons, `CellText`, sub-labels, not-rated cells and `CellWidth`; browser checks (cells' names, the grid table in "View data", sideways scroll at a 375 px phone with readable cells) and the existing axe sweeps in light, dark and Midnight; one HTTP check of the card's prerender.
- **Docs:** README (a "Heatmap tables" section; the legend fix; 0.46.0 additions; limits), `references/api.md`, `SKILL.md`, the Race Face recipe "Category heatmap" (#16) with invented data (rows = categories, columns = seasons, value = points per rider-start, `SubLabel` "/12 starts", `ValueNote` such as "34 pts, 5 riders", `NotRated` below 10 starts, `CellText`, `CellWidth`, `ChartExport.HtmlTable`), compiled by `tests/Lumen.Charts.Recipes`; `docs/VERIFICATION.md`; version 0.46.0.

## 4. Release

The handover's ritual: delegated build, independent verification on master, pack **after** the release commit (so the packages carry the tagged commit), refresh `artifacts/SHA256.json`, push, CI, tag, GitHub release with the three `.nupkg`, the skill packaged; then a message to "RACEFACE RUNNING EXPANSION 2".

## Out of scope

- The band-label contrast fix (still the owner's decision).
- A gradient legend key; an HTTP endpoint for `HtmlTable`.
- Any change to the classic finish.
- The places and points follow-ups (0.45.1, parked).
