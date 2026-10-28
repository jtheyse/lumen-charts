# Counts per day limits (0.47.2)

Design, 28 October 2026. The owner approved shipping this before 0.48.0 ("Approved. Do 0.47.2 first, then plan 0.48.0 stat tiles", with `docs/superpowers/specs/2026-10-28-beyond-charts-design.md` §9). The owner skipped the brainstorm questions, so the details below are Claude's calls. §6 lists them.

## 1. Intent

0.47.0's final review left four limits, recorded in the README's 0.47.1 "Limits" paragraph and in the brain's `Roadmap.Next`. Three are things a reader meets on a long counts chart. The fourth makes the code say its margin rule once. 0.47.2 removes all four. It adds no spec property and no public member.

## 2. Changes

### 2.1 "View data" shows the latest days when the chart opens at its end

**Today:** `<LumenChart>`'s flat data table writes `series.Points.Take(500)` for each series, under the note "Table shows up to 500 observations per series. CSV contains all original observations." (`LumenChart.razor` 50–57). A 4,000-day counts chart opens at its newest bar with `ScrollToEnd`, but its table lists the oldest 500 days.

**Change:** with `ScrollToEnd="true"`, each series writes its **last** 500 points, in their order, so the table ends where the chart opens. The note then reads "Table shows the last 500 observations per series. CSV contains all original observations." Without `ScrollToEnd`, both the rows and the note are unchanged. The caption, which counts every observation, is unchanged. The heatmap grid table is unchanged, since a heatmap is never `ScrollToEnd`'s concern.

### 2.2 A zero day can be tapped like a short bar

**Today:** a `MarkZero` zero is drawn as a 1-unit hairline (`ChartSvg.cs` 1119–1121 for category columns, 1137–1140 for columns on a continuous X). That rect is the only shape inside its `lumen-datum` group, so the pointer target is 1 unit high.

**Change:** a zero drawn as a hairline also gets a hit area inside its `lumen-datum` group, after the hairline:

- `<rect … fill='transparent' stroke='none'/>`, as wide as the bar;
- 24 units high and inside the plot: it rises 24 units from the baseline, and where the plot's top is closer than that (a chart whose values are all 0 or below has its baseline at the top), it is moved down so that it still spans 24 units without leaving the plot, or the plot's whole height where the plot is shorter than 24;
- `stroke='none'`, so `.lumen-datum:focus`'s inherited stroke rings the hairline as it does today, not the hit area;
- invisible at rest, on hover (the brightness filter has nothing to darken) and on focus.

The value label `0` above the hairline did not have `pointer-events='none'`, so a tap on it would have been taken by the label. 0.47.2 gives a hairline zero's `0` label on plain columns `pointer-events='none'`, so a tap on it reaches the hit area. The label of a zero on a column track stands above the track, clear of the hit area, and needs nothing. The bar's name, its `<title>` and its keyboard behaviour are unchanged. The same applies on a column track (`BarTrack`), where the hit area also stands on the baseline.

Why 24: the WCAG 2.2 minimum target size (2.5.8) is 24 CSS pixels. A counts chart with a `CellWidth` is drawn one unit to one pixel. The width stays the bar's own (72% of the pitch) so that neighbouring days' targets never overlap. At pitch 30 a target is about 21.6 by 24. Narrow pitches keep narrow targets, which WCAG's spacing exception covers while the targets do not overlap.

**This moves baseline rows by design:** every row whose drawing has a `MarkZero` zero, in both finishes. The builder lists them; they are expected to include `columns/mark-zero`, `columns/track-mark-zero` and the `daily-counts/*` rows that hold a zero day. Every other row stays identical. The classic finish's promise is untouched, because 0.23.0 had no `MarkZero`.

### 2.3 A right-hand axis title stands beside its ticks under the 320 floor

**Today:** the main Y title on the right (`YTitle`, `ChartSvg.cs` 1375–1379) and a pane's Y2 title (`ChartSvg.cs` 804) stand at `s.Width − 16`. Where a column chart with `CellWidth` is narrower than the 320 floor, the plot ends at `left + days × CellWidth`, the tick labels stand at the plot's right + 12, and the title stays at the drawing's edge, apart from them.

**Change:** on a column chart with `CellWidth`, both titles stand at the **plot's right edge + 60**. That chart's right-hand margin beside a right-hand axis is 76, so where the floor does not apply `right + 60` equals `s.Width − 16` and nothing moves; under the floor, the title follows its ticks. Every other chart keeps `s.Width − 16` exactly, so renderers whose right margin is not 76 cannot move. The builder adds a baseline row for each title (the main Y on the right, and Y2 if a fixed-pitch column chart can have a secondary axis; if validation refuses that pairing, one row is enough). On a column chart the Y2 title is the spec-level `Y2Label`, since panes are refused there. No existing row is expected to move. If one does, the builder reports it rather than accepting it.

### 2.4 One margin rule

**Today:** `Framed` (`ChartSvg.cs` 581) works out an upright chart's left and right margins, and `ColumnMargins` (`ChartSvg.cs` 3183–3191), which `DrawnWidth` uses, repeats the same rule. The only difference is that it caps the end labels' room by `MaxWidth` instead of the spec's `Width`.

**Change:** one private function holds the rule for upright charts on X and Y axes. `Framed` and `DrawnWidth` both call it, each passing the width that caps the end labels' room, so the rule is written once. Output is byte for byte unchanged: this item alone moves no baseline row.

## 3. Tests

- **Unit** (`tests/Lumen.Charts.Tests`, a `// 0.47.2` block):
  - **2.1:** prerender a `ScrollToEnd` chart of 600 points with the data open, and check that its first table row is point 101 and the note says "last 500". Without `ScrollToEnd`, check that the first row is point 1 and the note is the old one.
  - **2.2:** check that a zero's group holds the hairline and then a transparent, unstroked rect of the bar's width and 24 high on the baseline. Check that it runs down from the baseline where the baseline is at the plot's top (every value 0 or below), that neighbouring zeros' areas do not overlap, and that a non-zero bar has no hit area.
  - **2.3:** check the title's X for a fixed-pitch column chart under the floor with `YAxisSide.Right`, and for Y2 where allowed. Check that a non-floor chart's title is still at `Width − 16`.
  - **2.4** is covered by the baseline.
- **Baseline:** in both finishes, only the 2.2 rows move, and the new 2.3 rows are added. The builder states the final row count.
- **Browser** (gallery, then the WebAssembly host where a counts chart exists):
  - On the Sign-ups per day card in a 375 px context opened with `isMobile` and `hasTouch`, tap 12 units above a zero day's baseline and check that the zero's readout or tooltip shows, its name included.
  - Check that with View data open, the card's last row is its last day (2 Nov 2026).

## 4. Docs

- The README:
  - a "0.47.2 additions" section, in the style of 0.47.1's;
  - the 0.47.1 "Limits" paragraph rewritten to drop the three limits that are gone, with any that remain kept honestly (for example, a table without `ScrollToEnd` still shows the first 500);
  - the Counts per day and Blazor sections, where they describe these behaviours.
- The skill's references, where they mention View data or the zero bar.
- `docs/VERIFICATION.md`: counts.
- `HANDOVER.md` at release.

## 5. Non-goals

- Paging, sorting or showing every row in "View data". That comes with the 0.49.0 table component (beyond-charts design §6).
- Wider targets than the bar, or a hit area for non-zero short bars.
- Any change to the static SVG beyond the hit area and the moved title.

## 6. Calls made (Claude's, for the owner to check)

1. The last 500 rows apply only with `ScrollToEnd`; other charts keep the first 500.
2. The hit area is 24 units high, as wide as the bar, and has no focus stroke.
3. The right-hand titles of a fixed-pitch column chart stand 60 units right of its plot; every other chart's title stays where it is.
4. The margin refactor must be byte-identical.
