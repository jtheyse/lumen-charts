# Heatmap follow-ups — design (0.46.1)

Date: 8 October 2026. Status: approved in conversation (scope, the frozen-column approach, section 1, section 2); awaiting the owner's review of this written spec.

## Purpose

Race Face adopted 0.46.0's heatmap for its chart #16 on a branch, then kept its hand-built category table, because ten things still read worse than the table on a phone. Race Face's owner will switch once a release covers three of them:
- **gap 1:** a points total longer than `ValueNote` allows;
- **gap 3:** the not-rated reason written in the cell;
- **gap 6:** full row names that stay in view while the cells scroll.

0.46.1 covers all ten. Nothing in it is specific to Race Face.

### Decisions taken in the design conversation

| Question | Decision |
|---|---|
| Order | 0.46.1 before 0.47.0 (registrations per day, #18). 0.47.0's first section is approved and parked in the brain (`Columns.FixedPitch.Design`). |
| Scope | All ten gaps from Race Face's 0.46.0 report, plus a docs note on host CSS. |
| Frozen column | A sticky, cropped copy of the drawing in the component. No scroll script, and no split drawing. |
| Classic finish | Unchanged for every spec 0.23.0 could draw. The pinned scale (item 4) applies in the refined finish only. |

### Success criteria

- A heatmap cell's points total of up to 40 characters reaches its name and the grid table.
- A not-rated cell writes its reason in the cell when the cell can hold it, and its name and the grid table always say it in full.
- A season nobody raced can be a column of "did not race" cells. It is never mistaken for a low score or for "not rated".
- On a 375 px phone, a fixed-width heatmap's row names stay at the same screen position while the cells scroll, and they are written whole up to the 240-unit name column.
- A host can pin the colour scale at zero, put the column labels on top, fit the height to the rows, and show "View data" without the toolbar.
- Existing renderings: only the eight `heatmap-table/*` baseline rows move (item 3), by design. Every other row is identical in both finishes.

## 1. The drawing (Lumen.Charts)

### 1.1 Longer notes on heatmap cells (gap 1)

- On a heatmap cell, `ChartPoint.ValueNote` may be up to **40** characters, since it is never drawn in the cell. Every other kind keeps 20.
- The refusal message names both limits: "A value note is at most 20 characters (40 on a heatmap cell, where it is never drawn), …".

### 1.2 "Did not race" cells: `GapLabel` on heatmaps (gap 2)

- A heatmap point whose `Y` is null may set `ChartPoint.GapLabel`, with the existing limits: 1–12 characters, one line, not blank.
- The cell is drawn like a not-rated cell:
  - fill `Style.Background`;
  - a dashed outline (`stroke-dasharray='3 2'`) in `Style.Muted` at full opacity;
  - left out of the colour scale.
- With `CellText`, it writes the word in place of a value (10 px, ink by `CellInk` against the background), then its sub-label if there is room.
- Its name: `{row}: {column} · {sub}, {word}{ValueNote}`. The word stands where "missing" would, as on lines. Example: `Relay: 2025 · /0 starts, did not race`.
- Its column exists like any other. A season every row missed is a column of such cells.
- **Refused:**
  - `GapLabel` together with `NotRated` on one point ("A heatmap cell is either not rated or has a gap label, not both.");
  - `GapLabel` on a heatmap point that has a value.
- **CSV:** the value is empty, as for lines. **`HtmlTable`:** the word, then the sub-label and note (see 2.3).

### 1.3 The not-rated reason in the cell (gap 3)

With `CellText`, a not-rated cell writes its `NotRated` reason **in place of its value**:
- **Wrapping:** at word breaks, onto as many 10 px lines (12 units apart) as fit the cell's height less 4, each within the cell's width less 6, by the library's text-width estimate.
- **Then:** its sub-label, if a line remains.
- **Too long:**
  - A reason too long for the lines is cut at a word with "…".
  - A first word wider than a line is cut by characters with "…".
  - If not even "…" fits, the cell writes nothing.
- **Ink:** `CellInk` against `Style.Background`, as 0.46.0 already does for not-rated cells.
- **Block:** the lines are centred in the cell as one block.
- **Name and table:** the value, if any, and the full reason stay in the name and the grid table.

"too few starts to rate" fits whole on two lines at a `CellWidth` of 90 or more; the docs say so.

This changes 0.46.0's drawing of not-rated cells (value and sub-label), so the eight `heatmap-table/*` baseline rows move.

### 1.4 Pinning the colour scale (item 4, refined finish only)

- In the refined finish, a heatmap's `YMin`, `YMax` and `IncludeZero` set the colour scale's ends.
  - `YMin` sets the low end, and `YMax` the high end.
  - `IncludeZero` extends the rated cells' range to include 0.
  - The ends are used as given, not rounded.
- A rated value beyond an end takes that end's colour (clamped).
- No exception is thrown when the ends exclude every rated value.
- The legend line reads the ends: `Color scale: 0 pts low to 3.5 pts high`.
- With no rated cell, it still says `no rated cells`.
- The classic finish keeps ignoring all three, to stay 0.23.0 byte for byte.
- The existing bounds checks still apply: finite, and min below max.

### 1.5 Column labels on top (item 5)

- `ChartSpec.ColumnLabelsOnTop` (`bool`, default `false`, heatmaps only; refused elsewhere with a reason).
- **Placement:** the column labels are written above the grid, under the title and description, centred on their columns, 12 px. Same thinning and cut as at the foot.
- **Room:** the grid takes the room the labels left below it.
- **Hash:** left out of the gradient-ID hash when false.

### 1.6 Full row names (gap 6, the drawing part)

- Row names are measured at 12 px.
- **Name column:**
  - The column stays at today's 130 units, with names ending at x = 118, while every name fits 118.
  - Otherwise it widens to fit the longest name plus 18, up to **240**.
- **Cutting:** names wider than their room (the column less 12, or less 18 once widened) are cut by measured width with "…", no longer at 17 characters.
- **Width:**
  - The drawing's width follows the column: `left + 35 + columns × CellWidth` with `CellWidth`.
  - Without it, the cells share what is left of `Width`.
  - The 4096 size check uses the measured column.
- **Nothing moves:** names that fit today keep today's drawing, so no baseline row moves.

### 1.7 Fitted height (item 8)

- `ChartSpec.FitHeight` now applies to heatmaps as well as bars; it is refused on the other kinds as today.
- **Height:** the drawing's height is worked out from its rows: the room above the grid, 36 units per row, then the room below it.
- **Room below the grid:**
  - It leaves out the source line's 24 units when `Source` is empty.
  - It leaves out the column labels' room when `ColumnLabelsOnTop` is set.
- **Height floor:** charts' usual 240-unit floor still applies.
- **Without FitHeight:** heatmaps keep today's layout.

## 2. Component, export and CSS

### 2.1 Frozen row names in `<LumenChart>` (gap 6, the component part)

For a heatmap with `CellWidth`, `<LumenChart>` adds a second copy of the drawing inside `.lumen-viewport`.

- **What the copy shows:** the copy's `viewBox` is cropped to the row-name band, the name column from the top of the grid to its bottom. It sits in a layer with `position:sticky; left:0`, painted in the style's background, with a 1 px `Style.Grid` hairline on its right edge, so cells read as passing under it.
- **Accessibility:** the copy is `aria-hidden="true"` and `inert`. It is never read, focused or clicked. Marks, focus order, tooltips, keys and "View data" all stay on the one real drawing.
- **What scrolls:** the title, description, scale line and column labels lie outside the band, so they scroll with the drawing.
- **When nothing scrolls:** the copy sits exactly over the real names and nothing visible changes.
- **Renderer:** the renderer exposes the band (name-column width, grid top, grid bottom) as an internal helper, beside `DrawnWidth`.
- **Static SVG:** it has no freezing. The docs say so and point to "View data".

### 2.2 "View data" without the toolbar (item 7)

- `<LumenChart ShowDataButton="…">` (`bool?`). Unset, it follows `ShowToolbar`, so today's behaviour is unchanged.
- `ShowToolbar="false" ShowDataButton="true"` keeps only the "View data" button in the tools row.

### 2.3 The grid table's order (item 9)

- **Order:** a cell of `ChartExport.HtmlTable` reads value, then ` · {sub-label}`, then the `ValueNote` as written, then `, not rated: {reason}`.
  - Example: `2.8 · /12 starts · 34 pts, 5 riders`.
  - `ValueNote` brings its own separator, as in 0.46.0.
- **Special cells:**
  - A not-rated cell without a value starts with `—`.
  - A `GapLabel` cell starts with its word.
- **Unchanged:** cell names keep the column-chart order.

### 2.4 Table margins (item 10)

`.lumen-table`'s 24 px side margins apply only to the component's own data table (`.lumen-chart > .lumen-table`). A `.lumen-table` used on its own, as the docs show for `HtmlTable`, has none.

### 2.5 Host CSS (docs)

The README and the skill gain a note:
- **The problem:** a host rule such as `svg{max-width:100% !important}` overrides a fixed-width chart's own sizing, and stretches or squeezes it.
- **The fix:** exclude Lumen's drawings from the rule, for example with `:not(.lumen-chart svg)`.

## 3. Tests, gallery, docs

### Unit tests

- Every item above, including:
  - the 40-character heatmap note, and 20 elsewhere;
  - a `GapLabel` cell's drawing and name;
  - the refusals: `GapLabel` with `NotRated`, `GapLabel` with a value, and `ColumnLabelsOnTop` on other kinds;
  - reason wrapping, cutting and its sub-label;
  - the pinned scale in refined, with classic unchanged;
  - the measured name column and width cut;
  - `FitHeight` arithmetic, with and without a source and labels on top;
  - the frozen band helper;
  - `ShowDataButton`;
  - the `HtmlTable` order;
  - the CSS rule.
- Existing pins change only where this spec changes behaviour.

### Baseline

- **Changed by design:** the eight `heatmap-table/*` rows, both finishes.
- **Added:** labels on top, `FitHeight`, a long row name, a "did not race" season, a scale pinned at 0, and reasons in cells.
- **Every other row:** identical.

### Browser

On a 375 px phone (touch, scale 2):
- the Category heatmap's row names stay at the same screen x after its cells scroll;
- the copy is `aria-hidden`, `inert` and holds no focusable element;
- axe is clean in light, dark and Midnight, also with "View data" open;
- `ShowDataButton` shows only "View data".

### HTTP

The Sports page's prerender of the card.

### Gallery

The Sports & performance page's "Category heatmap" card becomes table-style, with invented data only:
- labels on top;
- `FitHeight`;
- reasons in cells at `CellWidth` 90;
- one "did not race" season;
- one long invented category name.

### Docs

- README: the heatmap section, the 0.46.1 additions, limits, and the host-CSS note.
- `references/api.md` and `SKILL.md` (frontmatter description ≤ 1024 characters).
- The "Category heatmap" recipe, compiled by `tests/Lumen.Charts.Recipes`.
- `docs/VERIFICATION.md`.
- Version 0.46.1.

## 4. Release

The handover's steps:
1. Delegated build.
2. Independent verification on master.
3. Pack **after** the release commit.
4. Refresh `artifacts/SHA256.json`.
5. Push, CI, tag.
6. GitHub release with the three `.nupkg`.
7. Package the skill.
8. Message "RACEFACE RUNNING EXPANSION 2" with the swap steps for #16.

## Coordination

Another session committed `docs/superpowers/specs/2026-10-08-interaction-state-design.md` (3ef1e7e, bindable chart and graph interaction state), which is likely to touch `<LumenChart>` too. The 0.46.1 build touches `LumenChart.razor` (the frozen copy and `ShowDataButton`), `lumen.css` and `ChartSvg.Heatmap`. Whichever lands second merges the other's changes; the owner decides the order.

## Out of scope

- Freezing in static SVG, or a sticky header row.
- "Did not race" on other kinds; `GapLabel` keeps its line, area and scatter rules there.
- A row-height setting other than `FitHeight`'s 36 units.
- Any change to the classic finish.
- 0.45.1 (places and points follow-ups) and 0.47.0 (#18), which are separate.
