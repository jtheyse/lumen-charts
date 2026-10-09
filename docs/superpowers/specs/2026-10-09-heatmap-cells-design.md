# Heatmap cells show it all — design (0.46.2)

Date: 9 October 2026. Status: approved by the owner 9 October 2026; amended during the build.

## Purpose

Race Face swapped its chart #16 (the team "compare seasons" category heatmap) to 0.46.1 on a branch. It reads better than the hand-built table almost everywhere: frozen row names, seasons on top, a pinned scale, "did not race" cells, a fitted height, "View data" on its own, the grid table's order. Race Face's owner merges only when each cell shows at least what the old table drew. Three gaps remain at 360 px:

- **A. The note is never drawn.** The old table draws three lines in a cell: the value, "/N starts" and "N pts, M athletes". Lumen puts the note only in the cell's name and the grid table.
- **B. A not-rated cell loses its value.** The old table draws the value (muted), "/N", the note, then "too few starts to rate". 0.46.1 writes the reason in place of the value.
- **C. The sub-label falls out of not-rated cells.** In 36-unit rows a two-line reason takes both lines, so "/N starts" is dropped. The old table always draws it.

0.46.2 closes all three with two opt-in switches and fitted rows that fit their words. Nothing in it is specific to Race Face. 0.47.0 (registrations per day, spec 9c13a7b) follows.

### Decisions taken in the design conversation

| Question | Decision |
|---|---|
| Order | 0.46.2 before 0.47.0 (owner, 9 Oct 2026). |
| Notes in cells | Opt-in: `ChartSpec.CellNotes`. |
| A not-rated cell's value | Opt-in: `ChartSpec.NotRatedKeepsValue`. Off keeps 0.46.1. |
| Fitted rows | `FitHeight` rows grow to fit the tallest cell's words, at least 36. This applies to every fitted heatmap, so fitted renderings whose cells were short of room move. |
| Classic finish | Unchanged for every spec 0.23.0 could draw. Cell text is newer than 0.23.0, so these changes apply in both finishes. |

### Success criteria

- At `CellWidth` 90, with both switches and `FitHeight`, every cell of Race Face's chart draws everything its old table drew:
  - a rated cell: value, "/N starts" and the points note;
  - a not-rated cell: the value, muted, then "/N starts", the note and the reason.
- No word is cut in a fitted heatmap, except a single word wider than its cell.
- Never colour alone: a not-rated cell still says so in its dashed outline, its reason and its name. The muted value is emphasis only.
- Every drawn word clears 4.5:1, including the muted value.
- Existing renderings: without the new switches, a heatmap without `FitHeight` draws exactly as in 0.46.1. Fitted heatmaps move only where a cell's words needed more than its 36-unit row.

## 1. The drawing (Lumen.Charts)

### 1.1 Notes in cells: `ChartSpec.CellNotes`

- `bool`, default `false`, for heatmaps with `CellText` on.
- **Refused** on other kinds or without `CellText`: "CellNotes and NotRatedKeepsValue write in a heatmap's cells, so they apply to heatmaps with CellText only."
- **Which cells:** each cell writes its `ValueNote` under its sub-label, where the cell's name and the grid table carry the note: a cell with a value, or a `GapLabel` cell.
- **The separator:** a note brings its own separator for the name, e.g. `" · 34 pts, 5 riders"`. When drawn, its leading spaces are trimmed, then one leading `·` or `,` and the spaces after it, so it reads `34 pts, 5 riders`. A note that is empty after trimming draws nothing.
- **Wrapping:** the note is written in 10 px, in the cell's ink (the colour its value and sub-label take), wrapped onto as many lines as it needs, each within the cell's width less 6 by the library's text-width estimate.
  - It breaks first after its commas.
  - A clause wider than a line is broken at word breaks.
  - A single word wider than a line is cut by characters with "…", as a reason's first word is.
  - Example: at `CellWidth` 90, `1840 pts, 12 athletes` reads `1840 pts,` / `12 athletes`.
- **Line breaks refused:** while `CellNotes` is on, a heatmap note containing `\n`, `\r` or `\t` is refused: "With CellNotes a heatmap cell's value note is drawn in the cell, where Lumen wraps it, so it takes no line breaks or tabs."
- **Length:** 40 characters, as in 0.46.1. The message no longer says "never drawn": "A value note is at most 20 characters (40 on a heatmap cell, where CellNotes draws it on lines of its own), …".
- **Unchanged:** the cell's name, the grid table, CSV.

### 1.2 Not-rated cells keep their value: `ChartSpec.NotRatedKeepsValue`

- `bool`, default `false`, for heatmaps with `CellText` on. It is refused as `CellNotes` is.
- **What a not-rated cell writes**, from the top:
  1. its value, if it has one, at 11 px and weight 600 as values are, in `Style.Muted` where that clears 4.5:1 against `Style.Background` (the not-rated cell's fill), and in the cell's ink otherwise;
  2. its sub-label;
  3. its note, with `CellNotes`, if it has a value;
  4. its reason, wrapped as 0.46.1 wraps it, as the last lines.
- **Off:** 0.46.1's drawing stays: the reason in place of the value, then the sub-label, then (with `CellNotes`) the note.
- **Never colour alone:** the dashed outline, the reason and the name ("…, 50.8, not rated: too few starts to rate") say it is not rated. The muted colour adds emphasis only.

### 1.3 A cell's lines

Every cell with `CellText` writes one block of lines:

| Cell | Lines, top to bottom |
|---|---|
| Rated | value, sub-label, note lines |
| Not rated, `NotRatedKeepsValue` | value (muted), sub-label, note lines, reason lines |
| Not rated, without it | reason lines, sub-label, note lines |
| `GapLabel` | the word, sub-label, note lines |

- **Spacing:** lines are 12 units apart. The block is centred in the cell as 0.46.1 centres a reason: the first baseline at `cy − 6 × lines + 9`. A value written alone stays at `cy + 4`, as today.
- **Room:** a block needs `12 × lines`, plus 1 when its first line is a value, within the cell's height less 4. A value alone needs 13 and a value with its sub-label 25, as 0.46.0's rule says.
- **Width:**
  - A sub-label wider than the cell less 6 is dropped, whole, as today.
  - A rated cell whose value is wider than the cell less 6 writes nothing at all, as in 0.46.0: its sub-label and note are not written without it. A not-rated cell's kept value that is too wide is dropped alone.
  - Note and reason lines wrap, as above.
- **Short of room** (no `FitHeight`, or a height floor), lines are given up in this order until the block fits:
  1. the whole note;
  2. the sub-label;
  3. the value;
  4. the reason's lines from the end, the last kept line ending in "…", as 0.46.1 cuts it.
  - The reason, or a `GapLabel`'s word, is the last to go, since it says what the cell is.
- **Compatibility:** with neither switch, every cell draws exactly as 0.46.1 draws it. The rules above reproduce 0.46.0's value and sub-label and 0.46.1's reason, line for line.

### 1.4 Fitted rows fit their words (`FitHeight`)

- With `FitHeight`, every row is as tall as the tallest cell's block needs, plus 4 for the padding, and never less than 36.
- **Measuring:** each cell's block is measured at its own width with every line kept, except what is dropped for width as above (a sub-label too wide, or a rated cell whose value is too wide).
- **Example:** at `CellWidth` 90, a not-rated cell with both switches, a value, "/9 starts", a two-line note and the two-line reason needs `4 + 13 + 12 × 5 = 77`, so every row is 77.
- **The rest of the height:** the room above the grid, the room below it and the 240 floor are as in 0.46.1. Where the floor binds, rows share its height as today.
- **What follows the row height:** the row names and the frozen band in `<LumenChart>` use the fitted row height, as they use 36 today. The 4,096 size check is width-only and does not follow it.
- **What moves:** a 0.46.1 fitted heatmap whose not-rated cells hold a two-line reason plus a sub-label now has 40-unit rows, so the sub-label shows. Fitted heatmaps whose blocks fit 32 units keep 36-unit rows and don't move.

### 1.5 Hash

`CellNotes` and `NotRatedKeepsValue` are declared after `FitHeight`, and are left out of the gradient-ID hash at `false`, with the `ShouldSerialize` rule `CellText` uses. The test that pins the default spec JSON gains both at its end.

## 2. Tests, gallery, docs

### Unit tests

- **Validation:**
  - both switches refused on other kinds and without `CellText`;
  - a note with a line break refused with `CellNotes`, and allowed without it;
  - the new 40-character message.
- **Notes:**
  - the separator trimmed (`" · 34 pts"`, `", 3 riders"`, `"/48"` kept as is);
  - the comma-first wrap, the word wrap and the first-word cut;
  - an empty note after trimming;
  - a not-rated cell without a value writing no note.
- **Not-rated cells:**
  - the line order with and without `NotRatedKeepsValue`;
  - the muted value's colour, and the cell's ink when a custom `Muted` falls under 4.5:1;
  - a not-rated cell without a value.
- **Room:** each step of the drop order, and the reason cut last with "…".
- **Compatibility:** without the switches and without `FitHeight`, the 0.46.0 and 0.46.1 cell-text tests pass unchanged.
- **Fitted rows:**
  - the row arithmetic for 36, 40 and 77;
  - the 240 floor;
  - the frozen band's height and the row names following the fitted row.
- **Hash:** IDs unchanged at the defaults; the default-JSON pin updated.

### Baseline

- **Changed by design:** fitted `heatmap-table/*` rows whose cells needed more than 32 units of words. The build reports exactly which; `heatmap-table/reasons` is expected.
- **Added, in both finishes:**
  - notes drawn, fitted;
  - notes drawn in a fixed height, so the drop order shows;
  - `NotRatedKeepsValue`, fitted;
  - both switches on the Dark preset (the muted value).
- **Every other row:** identical.

### Gallery

- The Sports page's "Category heatmap" card sets `CellNotes` and `NotRatedKeepsValue`. With its invented data it then draws every cell's value, starts and points note, and its not-rated cells keep their value.
- Its height follows its rows.
- The pinned counts and heights in the unit, HTTP and browser checks move with it.

### Browser

On a 375 px phone (touch, scale 2), the Category heatmap card:
- writes a value, a sub-label and a note in every rated cell, and a muted value, a sub-label, a note and the reason in each not-rated cell, with no word past its cell;
- keeps its frozen names level with their rows after the cells scroll;
- writes the muted value at 4.5:1 or more, measured in light, dark and Midnight;
- passes axe in all three.

### HTTP

The Sports page's prerender of the card, with a drawn note.

### Docs

- **README:** the heatmap section: notes in cells, the kept value, rows that fit their words in place of "36 units a row", the limits; "0.46.2 additions"; what changes for an existing heatmap (§3).
- **Models:** the doc comments of `ValueNote`, `NotRated`, `CellText`, `FitHeight` and the two switches.
- **The skill:** `references/api.md` and `SKILL.md`, keeping the frontmatter description at 1,024 characters or fewer.
- **The "Category heatmap" recipe** in `recipes-race-face.md` sets both switches. `tests/Lumen.Charts.Recipes` compiles it.
- `docs/VERIFICATION.md`, and version 0.46.2.

## 3. What changes for an existing heatmap

- A fitted heatmap (`FitHeight`) whose cells' words needed more than its 36-unit rows now has taller rows: every row as tall as the tallest cell's words need. A not-rated cell's sub-label then shows under its two-line reason.
- Nothing else changes until a host sets `CellNotes` or `NotRatedKeepsValue`.

## 4. Release

The handover's steps:
1. Delegated build.
2. Independent verification on master.
3. Pack **after** the release commit.
4. Refresh `artifacts/SHA256.json`.
5. Push, CI, tag.
6. GitHub release with the three `.nupkg`.
7. Package the skill.
8. Message "RACEFACE RUNNING EXPANSION 2": set `CellNotes` and `NotRatedKeepsValue` on #16 (`FitHeight` already set), check at 340–360 px, then the merge is its owner's call.

## Accepted limits

- **Uniform rows:** rows are one height, so one long note makes every row tall. A wider `CellWidth` keeps notes to one line.
- **Without `FitHeight`,** a short cell gives up its note first. The name and the grid table always carry it.
- **Static SVG** still doesn't freeze names, as in 0.46.1.
- **No height cap on a fitted heatmap:** `Height`'s 240–2160 is checked on the spec as given, before the rows are fitted, as in 0.46.1, so many rows of long notes can now draw taller than 2160.
- **Narrow fitted reasons:** a not-rated reason in a fitted heatmap still wraps as 0.46.1 wraps it. A later word wider than a line ends the reason there with "…", and the rows do not grow for the rest. Between a `CellWidth` of about 34 and 43, "too few starts to rate" reads "too" and "few…".
- **The kept value in a short fixed row:** without `FitHeight`, a cell short of room gives up its lines in a fixed order and never adds one back. So in a short fixed row `NotRatedKeepsValue` can show less: in a 40-unit row a kept-value cell writes only its two reason lines, where the same cell without the switch writes the reason and its sub-label. With `FitHeight` nothing is given up.

## Out of scope

- Per-row heights.
- Notes drawn on other kinds' cells or marks (column value labels already draw a note after the value).
- A strike-through value (muted was chosen).
- 0.47.0, which follows.
