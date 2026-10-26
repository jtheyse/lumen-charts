# Counts per day follow-ups — design (0.47.1)

## Purpose

0.47.0 shipped counts per day: `DailyCountsChart.Build`, fixed-pitch columns and `<LumenChart ScrollToEnd>`. Its final review left follow-ups. 0.47.1 fixes the ones a reader on a phone, or a host such as Race Face, would actually hit. It adds no new chart features.

The owner chose this scope on 24 October 2026 ("continue with your suggestions"). Everything below marked *my call* is Claude's choice and the owner can overturn it at review.

### Success criteria

- On a chart that opens at its end, Tab lands on the latest bar, and the chart does not scroll back to its first day.
- Export PNG produces an image for any drawing, however wide.
- `<LumenChart>` draws no series legend under a heatmap.
- A zero bar on a track writes its `0` in the muted colour, as other zero bars do.
- The three home-page graph tests that have skipped since 0.41.0 run.
- A browser test proves that `ScrollToEnd` returns to the end when a redraw changes the drawn width.
- The docs say how to keep a title in view on a chart that opens at its end.
- Nothing else moves.

### Non-goals

These stay as documented limits, unchanged:
- "View data" shows the oldest 500 days of a long range.
- A zero day's hairline is a 1-unit tap target.
- The right-hand Y title stands apart from its ticks under the 320 floor.
- `ColumnMargins` repeats `Framed`'s margin rule (an internal refactor with no reader-visible effect).
- Counts of a million or more write `1E+6` on the axis and over the bars in the default `ValueFormat.Number` (see §4).

### Anti-goals

- No byte of static SVG moves for any spec 0.47.0 could draw, except where §5 says so. §5 moves nothing: no existing baseline row draws a bar track with `MarkZero`.
- The classic finish stays byte for byte 0.23.0.
- No new spec property, so the gradient-ID hash is untouched.
- The owner's 0.47.0 calls stand: the builder keeps pinning Light, and a range starting on 31 December keeps its documented label limit.

## 1. The tab stop on a chart that opens at its end (lumen.js)

Today `settle()` gives the chart's one tab stop to the mark last focused, or else to the first point of the first series. On a `ScrollToEnd` chart that is the oldest day, so a reader who tabs in is scrolled back to the start.

- **Rule.** When the component asks for `ScrollToEnd`, the default stop is the **last** point of the first series: the lowest series, at the highest point index. A chart without it keeps the first point. The mark last focused still wins while it exists.
- **A new span resets the stop.** When a `ScrollToEnd` drawing's width differs from the drawing before it (the same test that decides to scroll to the end), the remembered stop is cleared before `settle` runs, so the new span's stop is its last bar. A drawing of the same width (a theme or brand change) keeps the reader's stop, as it keeps their scroll. This needs the width computed before `settle()` in `state.drawn`.
- **Static SVG is unchanged:** every mark keeps `tabindex="0"`, and the script settles the stop.
- **The comments and docs** that say "the first point of the first series" gain "or, with `ScrollToEnd`, the last": lumen.js (the `rove`/`settle` comment), README (the Blazor integration paragraph and the keyboard section), and api.md (the Blazor components part).

## 2. Export PNG for any width (lumen.js `png`)

Today the scale is `Math.max(1, Math.min(scale || 2, 8192 / Math.max(width, height)))`. The `Math.max(1, …)` floor means a drawing wider than 8,192 gets a canvas as wide as itself. Past the browser's canvas limit (about 32,767 px in Chromium; about 1,090 days at a pitch of 30) the export fails with "Export failed".

- **Rule.** Drop the floor: `factor = Math.min(scale || 2, 8192 / Math.max(width, height))`. The longest edge is then at most 8,192 pixels for every drawing, which is what README already says.
  - Up to 4,096: unchanged, still at scale 2.
  - 4,096 to 8,192: as today, between 1 and 2.
  - Wider: scaled below 1, so a 4,000-day chart (about 120,106 by 320) exports at 8,192 by about 22. Its words become too small to read.
- **Docs.** The README sentence becomes true, and it says that a drawing wider than 8,192 is scaled down to fit, so its words shrink, and that "Export SVG" keeps every detail at any width.

## 3. No series legend under a heatmap (LumenChart.razor)

Today `@if(ShowLegend&&Spec.Kind!=ChartKind.Strip)` draws one legend button per heatmap row, each with the same low-to-high swatch and the row's name. The rows are already named in the grid, and the static SVG leaves the legend out for heatmaps.

- **Rule (my call).** The component also skips the legend for `ChartKind.Heatmap`: `Spec.Kind is not (ChartKind.Strip or ChartKind.Heatmap)`.
- **Cost.** Clicking a legend button was the only built-in way to hide a heatmap row, and that goes. A host that wants rows hidden filters its own spec. The grid table and every name are unchanged.
- **Not changed:** the other kinds whose static SVG has no legend (donut, box, violin, gauge, calendar, one-series histogram) keep their component legend. Each needs its own look, and none is in scope.
- **Gallery:** the Sports & performance page's Category heatmap card loses its four legend rows. No HTTP or browser check counts them. Any test that does is updated and named in the report.

## 4. Tick labels: left as they are (my call)

An earlier draft had the builder label its ticks with `N0`, so a million would read `1,000,000` and not `1E+6`. The builder check found two problems:
- **The labels would disagree.** The count over each bar goes through the axis's own format (`LinearScale.Label`). Above 999 the axis would read `2,000` over a bar labelled `2000`, and a wide bar of 1,400,000 would still read `1.4E+6`.
- **Bar labels can't follow.** Making them match needs a new property, which this release doesn't add.

A million registrations a day is not a real case for this builder, so the ticks stay as 0.47.0 draws them. The limit is documented: with the default `ValueFormat.Number`, counts of a million or more read in E-notation on the axis and over a bar wide enough to hold the label. `spec with { YFormat = ValueFormat.Compact }` writes them `1M`. Amended at release: an earlier sentence here said "as on every Lumen axis", which `ValueFormat.Compact` disproves.

## 5. A zero on a track is muted (ChartSvg.cs)

Today the bar-track branch writes a column's value label at the track's top through `Above(rx, rw, At(scale.Max), true, …)` with no `fill`. The plain column branch passes `fill: hairline && Contrast.Ratio(Style.Muted, Style.Background) >= 4.5 ? Style.Muted : null`. So a column with `BarTrack` and `MarkZero` draws the zero's hairline but writes its `0` in the text colour.

- **Rule.** The vertical track branch passes the same `fill` as the plain branch. Horizontal tracks are bars, which refuse `MarkZero`, so they are unchanged.
- **Baseline:** no existing row draws a track with a zero and `MarkZero`, so nothing moves. One row is added in both finishes, `columns/track-mark-zero`: a column chart with `BarTrack`, `YMax`, `ValueLabels` and `MarkZero`, one of its values 0. That makes 409 rows.

## 6. Tests that run, and a width-change check

### The graph tests

In tests/Lumen.Charts.BrowserTests, three home-page tests (drag a node, then Reset layout, then select a node) find the graph with `.lumen-chart` at index 1. Since 0.41.0 that is the meters card, and they skip without a word.
- They find the graph by its node marks instead: `.lumen-chart:has([data-node])`. Only the graph writes `data-node`.
- Amended at release: an earlier draft used `#network .lumen-chart` and said the WebAssembly host has no graph. It has one, in a card without `#network`, so that locator would have skipped it.
- A host with no graph prints one SKIP line, as the suite's other host-dependent tests do.
- They have not run for many releases. If one fails for a reason in the product rather than the test, the implementer stops and reports it with evidence. The fix is then ruled on and is not assumed to be in scope.

### A range switch on the gallery card, and its check

The home page cannot change the card's drawn width today, so the width-change rule of `ScrollToEnd` has no test.
- **The card (my call):** the "Sign-ups per day" card gains two buttons above the chart, "Last 30 days" and "All 75 days", in the page's `aria-pressed` toggle style. "All 75 days" is pressed by default.
- **Its data:** `DemoData.SignUps(theme, days = 75)`. For `days` below 75 it passes `From` = 2 Nov 2026 − (days − 1) to the builder, so 30 days run from 4 Oct to 2 Nov and draw 76 + 30 × 30 + 30 = 1,006 wide.
- **Unchanged at the default:** prerender, the HTTP check and the existing browser checks. The new buttons' markup is the only addition to the card.
- **The browser check:** the reader scrolls the 75-day chart to 240, then picks "Last 30 days". The viewport's `scrollWidth` is about 1,006, and it stands at its end (`scrollLeft + clientWidth >= scrollWidth − 1`). The tab stop is the last bar (`aria-label` starting "Mon 2 Nov 2026: "). Picking "All 75 days" again gives about 2,356, at its end.
- **The tab-stop check (§1):** on the 75-day card, the one `tabindex="0"` mark is the last bar. Focusing it with the keyboard leaves the viewport at its end.

### PNG

A browser test imports the component's script module and calls `png` on a synthetic SVG 20,000 by 300. It catches the download, reads the PNG's header, and finds 8,192 by 123. The existing checks of scale 2 on fitted charts (no wider than 4,096) still pass.

### Unit tests

- The muted `0` on a track: muted where `Style.Muted` clears 4.5:1, text colour under a custom `Muted` that does not.
- The component draws no legend for a heatmap and still draws one for a line chart.
- Text checks on lumen.js where a rule is plain in the source:
  - the `png` factor without the floor;
  - `settle` choosing the last point when asked to open at the end.

## 7. Docs

- **The `DrawTitles` tip.** A chart that opens at its end has its title and description at the drawing's left, so they scroll away. In the host's own heading, write the chart's title with `DrawTitles = false`. Put it in:
  - README's "Counts per day" (its Limits paragraph);
  - api.md's `ScrollToEnd` bullet;
  - SKILL.md's "Counts per day" bullet;
  - the "Registrations per day" recipe.
- **README "## 0.47.1 additions"**, before "## 0.47.0 additions", for §§1–3, §5 and the gallery's range switch. Also the header's preview version.
- **The limits** keep the non-goals as written, including the E-notation limit (§4).
- **api.md and SKILL.md (body only):** the tab stop, the PNG cap and the heatmap legend. The SKILL.md frontmatter stays byte-identical.
- **docs/VERIFICATION.md:** a 0.47.1 entry in the 0.47.0 entry's format, with the measured counts.
- **tests/Lumen.Charts.Baseline/README.md:** 409 rows, with `columns/track-mark-zero` added and every other row identical.
- **`Directory.Build.props`:** `0.47.1`.

## 8. Constraints (binding on every task)

- Invented data only.
- Never colour alone: a zero is still said in its name and its label.
- Drawn text clears 4.5:1.
- The classic finish stays byte for byte 0.23.0.
- No new spec property. The gradient-ID hash does not move.
- Every baseline row is identical in both finishes except the one added (§5).
- Every new public member has an XML doc comment, and the Release build shows 0 warnings.
- **Processes:** stop `Lumen.Gallery.exe` by image name only, and every other process only by PID.
- **The Race Face brief:** never open, read or search `lumen-charts-race-face-brief.md`.
- **Git:** commit with explicit paths. Never push, merge, rebase or switch branches.

## 9. Release

The controller does these after the owner's merge call, as for 0.47.0. They are not plan tasks: the tasks stop at the verified release commit, and §8 binds them. The HANDOVER ritual:
1. Verify on master.
2. Pack after the release commit.
3. Push and wait for CI.
4. Tag, and publish the GitHub release with the three `.nupkg` files.
5. Package the skill.
6. Send Race Face a short note covering: the tab stop, PNG export, the heatmap legend gone from `<LumenChart>`, and the `DrawTitles` tip for #18. As after every release, this is part of the established flow.
7. Update the brain, HANDOVER.md and the progress page.
