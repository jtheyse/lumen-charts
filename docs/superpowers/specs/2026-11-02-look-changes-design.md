# Look changes (0.53.0)

Design, 2 November 2026. The owner ruled on four look changes Race Face asked for and that 0.52.0 left open (§3 of its design), and on #19's raster package. Each look change is an opt-in, so every existing drawing stays byte-identical by default:
- the static legend's flow layout, with long names wrapped instead of cut;
- an end label's note on a second line at 320 px instead of cut;
- annotation labels set clear of the series' marks;
- more room round the words in a fitted heatmap cell (#16b).

#19 becomes a recipe: the host turns Lumen's SVG into a PNG with Svg.Skia in its own code. Lumen ships no raster package and gains no dependency.

The details below are Claude's calls for the owner to check; §7 lists them. Line numbers are master at b2fe044.

## 1. Intent

What Race Face sees at phone widths today:
- **Legend.** The static legend lays its entries out in fixed columns, 180 units wide (220 on a ring), `legendColumns = (Width − 48) / 180` (`ChartSvg.cs:260`). At 340 that is one column, so four short series take four rows, 88 units under a 300-unit chart (`:264–269`, 22 a row). A name past 24 characters (30 on a ring) is cut by `Short` (`:303`, `:307`), so "Gauteng MTB League Elite" reads "…MTB Le…".
- **End labels.** An end label with a gap note, "Rider name +12.3s", is wider than the right margin at 320. The margin is capped so the plot keeps half the drawing (`Ending`, `:1213–1218`). Since 0.40.0 the note is cut first, then left out, then the name is cut (`:1301–1312`). So at 320 the reader loses the gap, which is the point of the note.
- **Annotation labels.** A reference line's label ("CP: 255") is set by `Place` (`:2172`). `Place` is called at `:937`, before any mark is drawn. It avoids other labels and the plot's edges, but not marks, so near the line's right end it can sit on the markers of a series that runs along the line.
- **Fitted cells (#16b).** A fitted row is its tallest cell's need plus 4 (`HeatmapRowNeeds`, `:3236–3241`; `CellNeed` is 12 a line plus 1 for a value, `:3632–3641`). The cell's rectangle is inset 1 (`:3384`). The block is centred on the row (`CellBlock`, first baseline `cy − 6n + 9`, `:3616`). So a cell whose lines fill its row has its value's cap tops about 2.5 units under the dashed or hairline top edge, and its last line as close to the bottom.
- **#19.** The 1080×1350 share card needs a PNG made on the server. Race Face's API draws its PNGs with ImageSharp, which does not read SVG. The browser's "Export PNG" works only in an interactive page.

## 2. What changes

Four new `ChartSpec` properties, all `bool`, all `false` by default. Each one:
- is left out of the gradient-ID hash while false, by a line in `Unfitted` (`:110–125`), whose summary gains a sentence for 0.53.0;
- is taken by the JSON API under its camel-case name;
- has an XML doc comment that says what it does, where it applies, that it is false by default and that it stays out of the hash while false;
- is refused, with its reason, where it cannot apply. The other rules in `ChartValidation` (`:129–132` for 0.52.0's heatmap switches) are the model.

They are bools because each has one alternative to today's look and the owner asked for no third; a bool reads the same as `FitEachRow` and `RowNamesInText` beside them. An enum (`LegendLayout.Columns | Flow`) would add a public type for one choice.

### 2.1 `ChartSpec.LegendFlow` (JSON `legendFlow`)

- **What it does.** The static legend that `Render` draws under the chart (`:295–308`) sets its entries left to right, in series order, each as wide as it measures, and starts a new row only where the next entry does not fit:
  - An entry is its key, its label and a 16-unit space after it. The label starts at `x + 20` in the refined finish and `x + 16` in the classic one, as today. Its width is `Wide(label)`, the library's generous estimate for 11 px text (`:1464`).
  - Rows start at x = 24. An entry goes to a new row where `x + entry > Width − 24` and the row already holds an entry.
  - Names are never cut by `Short`. A label wider than a whole row (`Width − 48` less the key) is set by `Wrap(label, room)` (`:467`) on two lines, the second line 13 units under the first. `Wrap` cuts only where two lines cannot hold it, with "…".
  - A row is 22 units tall, or 35 where one of its entries takes two lines. The legend's height is the sum of its rows. That replaces `legendRows * 22` in the drawing's height (`:269`). The first row's baseline stays at `Height + 10`.
  - One internal helper, such as `LegendFlowLayout(spec, ring)`, returns each entry's x, y and lines and the total height. `Render` uses it for both the height and the drawing, so the two cannot disagree.
- At 340 the four short series of §1 ("You", "Leader", "Field", "Best") take one row: 22 units in place of 88.
- **Where it applies.** It applies where `Render` draws a legend: the condition at `:264–265`. It is refused on sparklines and on the kinds that never draw this legend: donut, heatmap, box, violin, gauge, calendar and strip. A one-series histogram draws no legend but is accepted, since a second series gives it one. With `includeLegend: false` it does nothing.
- **Both finishes.** The classic square key and the refined mark-shaped key are kept as they are; only their positions move.
- **`<LumenChart>`.** The component writes its own HTML legend of toggle buttons (`LumenChart.razor:15–24`). That legend already flows (`flex-wrap`, `lumen.css:1`) and writes each name whole, and the component renders its drawing with `includeLegend: false` (`LumenChart.razor:196`). So the component needs no change. Its SVG and PNG exports call `ChartSvg.Render(VisibleSpec())` with the legend (`:311–312`), so they follow the spec's `LegendFlow`.

### 2.2 `ChartSpec.WrapEndNotes` (JSON `wrapEndNotes`)

- **What it does.** Where an end label and its note would be cut, the note goes on a second line under the label instead. The second line starts at the label's x, 14 units lower, and is written as the note is today: weight 400, muted where that clears 4.5:1, else the text colour (`noted`, `:1282`). The label line is unchanged.
- **Only where today's rule would cut.** A label that fits on one line stays on one line.
- **Which labels take two lines.** Whether a label is cut depends on whether it moved off its point (`moved`, `:1294`; `room`, `:1297`). That depends on `Spread`, which sets the labels one equal gap apart (`:1234`, `:1286`). A two-line label is taller, so that is circular. It is settled in passes:
  1. Spread the labels as today.
  2. Mark every label with a note that would be cut as two-line.
  3. Spread them again with each two-line label taking one more line.
  4. Repeat until no new label is marked. The set only grows, so it ends in at most one pass a label.
- **`Spread` generalised.** It takes each label's offset from the first (the sum of the gaps and the extra lines above it) in place of `i * gap`. Its upper bound keeps the last label's note line inside `high` as well. With every label on one line, the offsets are `i * gap`, so it gives today's numbers exactly. The rule that leaves the lowest labels out (`:1288`) counts the same offsets.
- **Margin.** Under the switch, `Ending` (`:1213`) asks for each label's one-line width where that fits under the cap, and otherwise for the wider of its label and its note. The cap is unchanged. So at 320, "Rider name +12.3s" asks only for "Rider name", and the plot is wider than today's capped one.
- **What is still cut.** A note wider than its line is cut by `Clipped` (`:1222`), as today. A label wider than its room is cut as today, and its note still goes under it.
- **Reading.** A two-line label is written as a cut one is: `role='img'`, with its whole words as its `aria-label` and `<title>` (`:1318–1320`). Its halo copy carries both lines.
- **Where it applies.** It is refused on sparklines (which refuse end labels, `ChartValidation.cs:495`) and on kinds whose series cannot be drawn as lines, areas or scatter points: donut, radar, heatmap, histogram, box, violin, gauge, ring, calendar, timeline and strip (the marks `Ended` accepts, `:661`). On a chart with no end note it does nothing.
- **Both finishes.** No component change.

### 2.3 `ChartSpec.AnnotationLabelsAvoidMarks` (JSON `annotationLabelsAvoidMarks`)

- **What it does.** In the refined finish, a reference's label is set where it clears the series' marks as well as the other labels, and it stays inside the plot.
- **Recording the marks.** While a pane's marks are drawn (`:956–1184`), the writer records the plot area each one covers:
  - each drawn marker and highlight ring, as its circle's box plus 2 for the halo;
  - each straight piece of a line or an area's top edge, between neighbouring drawn points (a curve by its chord);
  - each scatter and bubble circle;
  - each column, bar and stacked segment rectangle;
  - each value label and gap label box.

  Bands, area fills, ranges, candles, blocks and density cells are not recorded. A fill covers too much to avoid, and the others are left for later (§3). Recording happens only under the switch. An invisible hit circle (`MarkerStyle.None`, `:1033`, and the refined `Auto` marker that shows only on hover, `:1035`) is not a visible mark. Its line pieces are what is avoided.
- **Later placing.** Under the switch, `Place` is called after the marks, just before the front references and the labels are written (`:1185–1186`), not at `:937`. That is safe: in the refined finish, `Draw` writes only the shape (`:2149–2150`), and `Place` changes only the label's x, y, anchor and shown flag, which `Label` reads at `:1186`. Without the switch, the call stays at `:937`, so the output is today's to the byte.
- **The rule.** `Place` keeps its order (level lines, then level bands, then upright references) and its clearances (inset 4, gap 2). Each label tries these spots in turn and takes the first that clears the labels set before it and every recorded mark:
  - **level line:** right end above, right end below, left end above, left end below (left: `left + inset + gap`, anchor start);
  - **level band:** each row down the band at the right, then each row at the left;
  - **upright line:** each row down the plot on the side today's rule picks, then each row on the other side;
  - **upright band:** each row down the plot, inside the band.

  Where no spot clears the marks, the label takes the spot today's rule gives it (clear of labels only), so no label that shows today is left out. A label hit by a mark is still readable over its halo.
- A mark is hit when its box, or a line piece, meets the label's box widened by the 2-unit gap. Line pieces are tested by clipping the segment to the box (Liang–Barsky), not by its bounding box, so a long diagonal does not block the whole plot.
- **Classic finish.** The switch is accepted and does nothing. The classic finish writes a reference's label inside the reference's own group, behind the data, at `Measure`'s fixed positions (`:2085–2090`, `:2149–2150`), which are 0.23.0's look. It is not refused, because the finish often comes from the host's theme, not from the spec.
- **Where it applies.** It is refused on sparklines (which write no annotation labels), on the kinds that take no annotations (`ChartValidation.cs:140`), and on gauges, calendars and timelines. The gauge and calendar label their annotations in their own ways, and the timeline (`ChartSvg.cs:2338`) draws spans, which are not recorded. Horizontal bar charts are included, since they use the same pane loop and `Place`.
- **`<LumenChart>`.** No change: zoom and pan redraw through `Render`, which places again.

### 2.4 `ChartSpec.PadFittedCells` (JSON `padFittedCells`)

- **What it does.** A fitted heatmap row is its tallest cell's need plus 12, not plus 4, still at least `HeatmapRow` (36, `:3185`).
- **The change.** One expression in `HeatmapRowNeeds` (`:3240`): `CellNeed(s, p, cw) + (s.PadFittedCells ? 12 : 4)`. `FittedHeight`, `HeatmapRows`, the drawing and the frozen name band all read that helper (0.52.0), so they stay aligned with no other change.
- **Why 8 more.** The block stays centred, as `CellBlock` places it from the row's middle (`:3399`, `:3616`), so the 8 units are shared: 4 above and 4 below.
  - The value's cap tops move from about 2.5 to about 6.5 units under the cell's edge. Six units is what Lumen keeps between a reference label and the plot's edge (inset 4 plus gap 2, `:2175`).
  - Fewer (+2 a side) still reads tight against a dashed edge at 340. More would make a five-line cell 16 units taller than today for nothing the reader gains.
- **What moves.**
  - A one-line cell needs 25 with padding, so it stays at 36.
  - A two-line cell's row goes from 36 to 37.
  - A row of four lines (value, sub-label, note and one reason) goes from 53 to 61.

  `CellBlock`'s fitting test (`tall = ch − 4`, `:3597`) is unchanged; the rows are only taller, so nothing that fitted is given up.
- **Where it applies.** Heatmaps only, and it needs `FitHeight`. It is refused elsewhere, as `FitEachRow` is (`ChartValidation.cs:129–132`). Without `CellText` every row is 36 and it does nothing. It works with and without `FitEachRow`, and in both finishes.
- **`<LumenChart>`.** The frozen name band reads `HeatmapRows`, so it needs no change.

### 2.5 #19: rasterising on the server (docs only)

A recipe in `references/recipes-race-face.md` and a paragraph in the README's "Exports and tooltips" (`README.md:1121`), which says today that there is no server-side PNG. The recipe covers:
- **The package.** The host adds `Svg.Skia` (MIT) to its own project, and on Linux `SkiaSharp.NativeAssets.Linux.NoDependencies`. Lumen's packages reference neither.
- **The code.** About ten lines in the host:
  1. `var svg = ChartSvg.Render(spec with { Width = 360, Height = 450 })`;
  2. load it into `SKSvg`;
  3. save the picture as a PNG at a scale of 3, so 360×450 becomes 1080×1350.

  The card is laid out at 360×450 so its words are phone-sized when the image is shown at phone width. Drawn at 1080 they would be a third of that size. The exact Svg.Skia calls are checked against its current release when task 5 writes the recipe, and compiled in `tests/Lumen.Charts.Recipes`.
- **What to check in the PNG.** A rasteriser is not a browser. The recipe names the features in Lumen's SVG that the host must test in its PNG:
  - the root's `<style>` uses CSS custom properties: `var(--lumen-grid)` and `var(--lumen-muted)` (`ChartSvg.cs:413–417`), and a heatmap cell's hairline is `stroke='var(--lumen-muted)'` (`:3390`);
  - text inherits `fill='currentColor'` from the root's `color` in its `style` attribute (`:413`);
  - refined reference labels are haloed with `paint-order='stroke'` (`:2160`), which an SVG 1.1 rasteriser draws as a stroke over the glyphs. Value labels use a copy instead, for this reason (`:882`).

  Where the rasteriser misses one, the host replaces the variables with the style's own `Grid`, `Muted` and `Text` colours before loading the SVG. It does that in its own code; Lumen adds no helper (§3).
- **Fonts in a Linux container.**
  - Skia draws with the fonts it finds through fontconfig. A slim image has almost none, so text falls back or disappears.
  - Install `fontconfig` and the style's font (for Race Face, the font its `ChartStyle.FontFamily` names first), or copy its files into `/usr/share/fonts` and run `fc-cache`.
  - Lumen fits its words by its own width estimate, not by the font, so a narrower fallback font leaves gaps and a wider one can overrun. Use the named font.
- **The browser's PNG.** "Export PNG" in `<LumenChart>` draws at twice the chart's size, capped so the longest edge is at most 8,192 pixels (`lumen.js:762–763`, `LumenChart.razor:311`). So a 360×450 chart saves at 720×900. It is not the 1080×1350 card, and it needs an interactive page.

### 2.6 Version 0.53.0

New public options, so a minor release.

## 3. Non-goals

- No existing drawing moves. The 433 baseline rows stay byte-identical in both finishes.
- No new dependency and no raster package. No `ChartSvg` helper that flattens CSS variables for rasterisers: that is a host step in the recipe, and the owner can rule on a helper later.
- No change to the default legend, end-label, annotation or cell layout. The 0.40.0 cut stays the default.
- Not in this release:
  - annotation labels avoiding bands, area fills, ranges, candles, blocks, density cells or timeline spans;
  - avoiding marks in the classic finish;
  - a third legend line;
  - a legend at the side of the chart;
  - the wider margin for one or two cut end labels (HANDOVER's other end-label idea);
  - changing `paint-order` halos to copies, which would move refined reference labels in every drawing.

## 4. Tests

- **Unit tests:**
  - **Legend:**
    - At 340, four short series flow onto one row, and the drawing is 66 units shorter than without the switch.
    - A 40-character name at 340 takes two lines inside `Width − 24`, every word kept, and its row is 35 tall.
    - Mixed short and long names keep series order.
    - A ring's labels with values flow.
    - Both finishes keep their keys.
    - It is refused on a donut, a heatmap and a sparkline, naming `LegendFlow`.
    - With `includeLegend: false` the output equals the output without the switch.
  - **End notes:**
    - At 320, "Rider name" with "+12.3s" writes the note whole on a second line, inside the drawing, with `role='img'` and the whole words as its name.
    - The same chart at 640 is byte-identical with and without the switch.
    - Four crowded labels with two-line notes keep at least one line between blocks, inside the plot bounds.
    - A note wider than its line is cut with "…".
    - The pass loop ends on a chart where a second spread moves another label.
    - `Spread` with all one-line offsets equals today's on 10,000 random sets.
    - It is refused on a donut and a sparkline.
  - **Annotation labels:**
    - On a 340 line chart whose markers sit near the right end of a level reference, the label's box meets no recorded mark, and it lies inside the plot.
    - Where every spot is blocked, the label takes today's spot.
    - An upright reference's label clears a column.
    - The classic finish is byte-identical with and without the switch.
    - It is refused on a heatmap, a gauge, a timeline and a sparkline.
  - **Fitted cells:**
    - With `PadFittedCells` every fitted row is `CellNeed + 12`, at least 36, with and without `FitEachRow`.
    - The frozen band's rows match the drawing's.
    - A heatmap whose cells write one line draws the same 36-unit rows with and without the switch.
    - It is refused on a line chart and without `FitHeight`.
  - **Hash:** each switch at false leaves the gradient ID of a chart with gradients unchanged; at true it changes it.
- **Baseline:**
  - The 433 rows are unchanged in both finishes. As in 0.52.0, 10,000 random heatmaps and 10,000 random end-label charts without the switches are byte-identical to 0.52.0.
  - Eight new rows in each finish (441 in all):
    - `legend/flow-short`: four short series at 340;
    - `legend/flow-long`: a 340 legend mixing short names and two that wrap;
    - `ends/note-below-320`: two riders with gap notes at 320;
    - `ends/note-below-crowded`: five end labels at 340, three of them two-line;
    - `annotations/avoid-marks`: a 340 power line with a level `CP: 255` reference its markers crowd at the right end;
    - `annotations/avoid-marks-upright`: columns with an X reference;
    - `heatmap-cells/padded`: the 0.52.0 `noted` heatmap with `FitHeight` and `PadFittedCells`;
    - `heatmap-cells/padded-each-row`: the same with `FitEachRow`.
  - In the classic finish, the two `annotations/*` rows equal the same specs without the switch, since the switch does nothing there and stays out of the hash.
- **HTTP:** `/api/charts/svg` takes `legendFlow`, `wrapEndNotes`, `annotationLabelsAvoidMarks` and `padFittedCells` (200), and refuses each on a kind it does not apply to (400, naming the option): eight checks.
- **Browser:** no component change, so the suites run unchanged in both hosts. The new rows are looked at in Playwright at 320, 340 and 360 with a real viewport, from `svg-out`.
- **Recipes:** the raster recipe compiles with every other recipe in `tests/Lumen.Charts.Recipes`. It needs `Svg.Skia` there as a test-only reference; Lumen's packages do not take it. It renders the 360×450 card at 3× to a 1080×1350 PNG, which is looked at.
- **Accessibility:**
  - Never colour alone: legend keys keep their shapes and every name keeps its words.
  - A two-line note keeps 4.5:1 through `noted`, and its name says the whole label.
  - A moved annotation label keeps its ink and halo.
  - Every new text stays inside the drawing at 320–360: the legend wraps inside `Width − 24`, notes are cut to their room, and labels stay inside the plot.

## 5. Docs

- **README:**
  - a "0.53.0 additions" section;
  - one sentence each in the legend, end-label (`:995`), annotation (`:826`) and heatmap (`:531`) sections;
  - the raster paragraph in "Exports and tooltips" (`:1121`, `:1123`), and the list line at `:1521`.
- **`api.md`:** the four options with their JSON names.
- **`recipes-race-face.md`:** the raster recipe; and the phone notes say to set the four switches for Race Face's cards.
- **SKILL.md:** one body line, the description kept at 1,024 characters or fewer.
- **VERIFICATION:** the counts.
- **HANDOVER:** the Race Face list. The legend, end-label and annotation items are done; the recipe item's rasteriser is done; #16b is done.

## 6. Order of work

1. **Legend flow.** The layout helper, `Render`'s height and drawing, validation, hash, unit tests, and the two `legend/*` rows.
2. **End notes.** `Spread` with offsets, the pass loop, `Ending` under the switch, the two-line writing, validation, hash, unit tests, the random probe, and the two `ends/*` rows.
3. **Annotation labels.** Mark recording in the pane loop, the later `Place` call, the new spot order, validation, hash, unit tests, and the two `annotations/*` rows.
4. **Fitted cells.** `HeatmapRowNeeds`, validation, hash, unit tests, the random-heatmap probe, the two `heatmap-cells/padded*` rows, and the eight HTTP checks for all four switches.
5. **Raster recipe and docs.** The recipe compiled and rendered, README, `api.md`, SKILL, the version, VERIFICATION and HANDOVER.

## 7. Calls made (Claude's, for the owner to check)

1. Four `bool`s, not enums, named `LegendFlow`, `WrapEndNotes`, `AnnotationLabelsAvoidMarks` and `PadFittedCells`. Each is refused where it cannot apply, as 0.52.0's heatmap switches are.
2. `LegendFlow` is refused on the kinds that never draw the static legend, but accepted on a one-series histogram.
3. A flowed legend name that is wider than a row wraps to two lines, then is cut with "…". There is no third line.
4. `WrapEndNotes` moves a note only where today's rule would cut it. Under it, the margin asks for the narrower two-line width where the one-line width would meet the cap, so the plot widens at 320.
5. The end-label passes settle which labels take two lines; the set only grows.
6. Annotation labels avoid markers, line pieces, scatter circles, columns and bars, and value and gap labels. They do not avoid fills, ranges, candles, blocks or spans. Where no spot clears the marks, a label keeps today's spot rather than being left out.
7. The level-line spot order is right above, right below, left above, left below.
8. `AnnotationLabelsAvoidMarks` does nothing in the classic finish, and is not refused there. It is refused on timelines, gauges and calendars.
9. Fitted cells get 8 more units, shared above and below (about 6.5 clear of the edge), not top-only padding. A top-only shift would move `CellBlock`'s centred placing for one switch.
10. The raster recipe names Svg.Skia's CSS-variable, `currentColor` and `paint-order` gaps as things to check, and has the host substitute colours itself. Lumen adds no flattening helper, and `paint-order` halos stay as they are.
11. The share card is laid out at 360×450 and saved at 3×.
12. Version 0.53.0.
