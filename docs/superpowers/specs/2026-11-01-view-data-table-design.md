# View data as a table (0.51.0)

Design, 1 November 2026. The owner chose this release ("View data as a table") from the candidates listed after 0.50.0. The details below are Claude's calls for the owner to check; §7 lists them.

## 1. Intent

`<LumenChart>`'s "View data" today is a hand-written `<table>` that lists at most 500 observations per series. With `ScrollToEnd` (0.47.2) those are the last 500; otherwise the first 500. A long chart, such as 4,000 days of counts, cannot show the rest. 0.49.0 shipped `<LumenTable>`, which can. It pages, sorts, keeps its header row in view, reads well on a phone, and is tested with axe.

This release draws the flat "View data" table with `<LumenTable>`, so every observation is reachable and the 500 cut is gone. The heatmap's grid table (`ChartExport.HtmlTable`) is unchanged.

## 2. What changes

1. **A public builder.** `ChartExport.DataTable(ChartSpec spec)` returns a `TableSpec` holding the chart's observations, so static pages and servers get the same table. It validates the chart first. A heatmap is refused, as `Csv` and `HtmlTable` refuse what they can't write; a heatmap's data is the grid table.
2. **The columns:**
   - **Series** (Text). It writes the series' name and is sortable (A to Z).
   - **X / Category** (Text). It writes the label or the formatted X exactly as the component does today, including a category's sub-label after " · ". It is the row header (`<th scope=row>`) and is **not sortable**, because its text (dates, times, categories) does not sort as text. The rows' own order is the chart's order: series by series, points in their order.
   - **Value.** Two cases:
     - **Plain charts.** When every series reads a plain Y (no candles, OHLC, rings, timeline spans, blocks, ranges or bands) and every series shares one value format and unit, Value is a **Number** column with that `Format` and `Unit`. It is sortable, with missing values last. A point's `ValueNote` becomes the cell's muted note, and a `GapLabel` becomes the cell's note on a missing value.
     - **Everything else.** Value is a **Text** column holding exactly today's words (for example `O 12 · H 14 · L 11 · C 13`, `52 to 168`, `12 of 100`, `to 07:30`), and it is not sortable.
3. **Caption.** `{Title} — original data` (`Chart data — original data` for an untitled chart), cut with "…" to the table's 120-character limit. The caption is visible, as for every `<LumenTable>`.
4. **Paging.** With more than 100 rows, the table pages at 100 rows. Its `StartAtEnd` is the chart's `ScrollToEnd`, so a chart that opens at its end opens its table on the last page. With 100 rows or fewer, every row is shown and there are no paging buttons. The 500-row cut and its sentence are gone; the CSV export is unchanged.
5. **Style.** The table takes the chart's resolved style (spec, cascading brand, or Light), so it matches the chart.
6. **Markup.** `<LumenChart>` writes `<div class="lumen-table">` as a plain wrapper (no role or tabindex) around `<LumenTable>`, so a host's CSS hook on `.lumen-table` still applies. The focusable, named region is now `<LumenTable>`'s own, named by the caption instead of "Chart data". The heatmap's grid table keeps today's "Chart data" region.
7. **Version 0.51.0.** The View data markup and its words change, so this is a minor release.

## 3. Non-goals

- Changing the heatmap grid table, the CSV, or the chart drawing. No baseline row may move.
- Sorting by X. Its text does not sort meaningfully, and the rows already come in chart order.
- Hiding series the reader turned off in the legend. Today's table lists every series, and that is kept.

## 4. Tests

- **Unit tests:**
  - `DataTable` for a line chart of 600 points: one column of each kind as described, a Number value with the chart's format and unit, notes, the caption, `PageSize` 100, and `StartAtEnd` following `ScrollToEnd`.
  - A candlestick, a range and a timeline chart get a Text value.
  - Mixed formats (a secondary axis with another format) give a Text value.
  - A heatmap is refused.
  - A 30-row chart has no `PageSize`.
  - The component's prerendered View data equals `<LumenTable>`'s markup for `DataTable(spec)`.
- **Browser tests:**
  - On the Sign-ups per day card, View data opens on its last page with the latest day in the last row, and "Previous page" works.
  - On a long chart, every observation is reachable by paging.
  - Axe passes with the data open in Light and Dark.
  - Existing tests that read the old table are updated to the new markup.
- **Baseline:** the 426 rows are unchanged.

## 5. Docs

README: the "View data" text in the Blazor section, the 0.47.2 limit lines (the 500 cut is gone), a "0.51.0 additions" section, `api.md` (`ChartExport.DataTable`), SKILL.md (one line), and VERIFICATION.

## 6. Order of work

1. `ChartExport.DataTable`, with its tests.
2. `<LumenChart>` uses `<LumenTable>`, with the unit and browser tests updated and added.
3. Docs and the version.

## 7. Calls made (Claude's, for the owner to check)

1. A public `ChartExport.DataTable(spec)`, so servers and static pages get the same table.
2. X is the row header and is not sortable; Value is sortable only when it is a plain number in one format.
3. The table pages at 100 rows and opens on its last page when the chart opens at its end.
4. The `.lumen-table` wrapper stays as a plain div, and the focusable region becomes the table's own, named by its caption.
5. Every series is listed, even ones hidden in the legend, as today.
6. Version 0.51.0.
