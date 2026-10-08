# Registrations per day — design (0.47.0)

Date: 9 October 2026. Status: approved in conversation (approach 1 and section 1 on 7 October, section 2 on 9 October); awaiting the owner's review of this written spec.

## Purpose

Race Face's chart #18 counts new accounts per day on an admin page. Today it is a hand-built SVG:
- **Span:** the reader chooses it: 7, 30, 90 or 365 days, or any From–To. It can run to thousands of days.
- **Bars:** one bar a day at a fixed pitch. The drawing grows with the days and scrolls sideways instead of squeezing.
- **Counts:** a count above every bar, muted for zero. A zero day is a 1 px hairline.
- **Dates:** the day number under every bar, and the month under the first bar and each 1st.
- **Tooltip:** each bar says its date, its count with a singular or plural noun, and the running total so far.

Lumen gets small, general column features (section 1), one general primitive (`ChartPoint.Name`), and a builder that turns daily counts into the chart (section 2). Nothing in them is specific to Race Face: any app that counts sign-ups, orders or entries per day calls the builder with its own nouns. The release also carries the #20 course-profile recipe, which needs no library change.

### Decisions taken in the design conversation

| Question | Decision |
|---|---|
| Approach | A builder on top of general column features (approach 1, owner, 7 Oct 2026). Not a new chart kind and not a recipe only. |
| Section 1 | The column features: fixed pitch, static drawn width, axis labels, a visible zero, value labels at a fixed pitch, scroll to end (7 Oct 2026). |
| Section 2 | `DailyCountsChart.Build`, plus the general `ChartPoint.Name` (9 Oct 2026). |
| #20 course profile | A recipe only. It draws a **line**, not an area (see §3). |
| Classic finish | Every spec 0.23.0 could draw stays byte for byte. The new features are opt-in and drawn in both finishes. |

### Success criteria

- Race Face can replace its hand-built SVG with one call to `DailyCountsChart.Build` and `<LumenChart FitWidth ScrollToEnd>`, and keep everything listed above.
- 4,000 days draw at their pitch without squeezing.
- Never colour alone: a zero day is said in its name ("0 registrations") and in its "0" label, not only by the muted colour.
- Every drawn word clears 4.5:1, including the muted "0".
- Existing renderings: only the two `heatmap-table/*` rows that set `CellWidth` move (§1.2), by design, in both finishes. Every other row is identical, and rows are added.
- Gradient IDs don't move: every new property is left out of the hash at its default.

## 1. Column features (Lumen.Charts)

### 1.1 Fixed pitch: `CellWidth` on column charts

- `ChartSpec.CellWidth` now applies to `ChartKind.Column` as well as heatmaps. On a column chart it is the width of every category: finite and at least **8**. Heatmaps keep their 24.
- **Layout:**
  - The plot is exactly `categories × CellWidth` wide and starts at the frame's left margin (76, or 30 when the frame puts no axis labels there).
  - The drawing's width is the left margin, plus the plot, plus the right margin, rounded up, and never below 320. When the floor applies, the extra room sits at the right, so the pitch never changes.
  - The spec's `Width` is ignored, as it is on a heatmap.
- **Bars** keep their 72% share of the category.
- **Caps:**
  - With `CellWidth`, a column chart takes up to **4,000** categories, against 100 without.
  - The 4,096-unit width limit does not apply to such a chart. Heatmaps keep it.
- **Refused:**
  - `CellWidth` on any kind other than heatmap and column, as today.
  - `CellWidth` on a sparkline.
  - `CellText` and `ColumnLabelsOnTop` stay heatmap-only.
- **Messages:** the combined 0.46.0 message splits in two:
  - one for `CellText` and `ColumnLabelsOnTop` (heatmaps only);
  - one for `CellWidth`: "CellWidth sets the width of a heatmap's columns or a column chart's categories, so it applies to heatmap and column charts only; …".
  - The floor message names the kind: "A column chart's category is at least 8 pixels wide, …".
  - The 100-category message mentions the 4,000 allowed with `CellWidth`.
- **Component:** `<LumenChart>` already takes any chart with `CellWidth` down the fixed path (`lumen-fixed`, `--lumen-drawn`). `ChartSvg.DrawnWidth` learns the column width, so a column chart is shown at exactly its drawn width and scrolls sideways when wider than its box.
- **No frozen band:** columns have no frozen band. The Y axis scrolls with the bars, and the count on every bar carries the values.

### 1.2 Static SVG at its drawn width

- Today the root `<svg>`'s style says `width:100%`. A drawing with `CellWidth`, heatmap or column, now says `width:{drawn}px` instead, with no `max-width`. A host's `overflow-x:auto` box then scrolls it instead of squeezing it.
- Section 1 said a `width` attribute; the root already sizes itself through its style, as a sparkline's `width:{w}px` does, so the style carries it.
- Other drawings are unchanged.
- Inside `<LumenChart>`, the `lumen-fixed` rules already hold the drawing at that width, so nothing visible changes there.
- **Baseline:** `heatmap-table/cell-width` and `heatmap-table/reasons` move in both finishes, by design: only their root style changes.

### 1.3 Axis labels: `ChartPoint.AxisLabel`

- `string?`, default null. It is for points on a column chart (`ChartKind.Column`), whatever series they belong to.
- **Form:**
  - One or two lines, split at `\n`.
  - Each line has 1–12 characters, is not blank, and has no `\r` or `\t`.
  - Two points of one category that give different axis labels are refused, as sub-labels are.
- **Drawing:** written under the column in place of its category name:
  - the first line where the name goes (12 px, muted, as names are);
  - the second line 14 units lower (11 px, muted, as sub-labels are).
  - When any label has a second line, the room below the plot grows by 14, and the X axis title moves down with it, as with sub-labels.
- **Drawn only:** the category's `Label` still names it in the mark's name, the tooltip, the data table and CSV.
- **Refused beside `SubLabel`:** a column chart takes `AxisLabel` or `SubLabel`, not both, since both use the line under the name ("A column chart writes either axis labels or sub-labels under its columns, not both.").
- **Thinning**, for column charts with `CellWidth` or with any `AxisLabel`. Each label is measured by its wider line, and "clear" is today's rule: two labels' centres at least half their widths' sum plus 8 apart.
  1. Two-line labels are placed first, left to right. Each is kept unless it collides with the previous kept two-line label; the first is always kept.
  2. Then one-line labels, left to right. Each is kept where it is clear of the kept labels on either side.
  - Other column charts keep today's thinning.

### 1.4 A zero you can see: `ChartSeries.MarkZero`

- `bool`, default `false`. It is for a series drawn as columns: on a column chart, or as a column series on a continuous-X chart.
- **Refused** on other series: lines, areas, bars, stacked columns and the rest. Message: "MarkZero draws a zero column as a hairline, so it applies to series drawn as columns only."
- **The hairline:** a point whose value is exactly 0 draws a bar 1 unit high, standing on the baseline, the column's width, in `Style.Muted`. It has no radius, no gradient and no fade. It replaces today's zero-height rect, and it stays the focusable, named mark ("…, 0").
- **The "0" label:** with `ValueLabels`, a zero's label is written in `Style.Muted` where that clears 4.5:1 against the background, and in the text colour otherwise. Other value labels keep the text colour.
- **Hash:** left out of the gradient-ID hash at `false`, with a `ShouldSerialize` rule like `CellText`'s. The test that pins the default JSON gains the new property.
- **Never colour alone:** the name and the written "0" say it. Muted is emphasis only.

### 1.5 Value labels at a fixed pitch

- With `CellWidth` on a column chart, a column's value label is kept while it is no wider than `CellWidth − 2`, not the bar's width. At the default pitch of 30, "1,234" fits.
- Neighbouring labels stay at least 2 units apart.
- The other rules are unchanged: a label that would fall outside the plot is still dropped.

### 1.6 A mark's own name: `ChartPoint.Name`

- `string?`, default null, meaning Lumen builds the name. When set it must be 1–200 characters on one line, not blank, with no control characters.
- **Where it applies:** when set, it is the point's mark's whole name:
  - the `aria-label`;
  - the `<title>` in static output;
  - the component's tooltip and keyboard announcements, which read the `aria-label`;
  - the component's status line when the point is selected.
- **It replaces everything Lumen would say:** series, label, value, note, change words, zone, "projected", "above the scale". The docs say plainly that the host then says everything the mark needs, including any state shown by colour.
- **Unchanged:** the drawing, value labels, the data table, CSV, `HtmlTable`, and the shared readout, which lists every series' value at an X in its own words.
- **Marks that summarise points:**
  - Where several points share one mark, that mark keeps Lumen's name. A line thinned by sampling into an averaged mark is the case.
  - On histogram, box, violin and calendar charts, whose marks always summarise points, `Name` is refused.
- **Hash:** null, so not hashed.

### 1.7 Opening at the latest day: `<LumenChart ScrollToEnd>`

- `bool`, default `false`. It applies when the drawing is wider than its box, as a `CellWidth` chart can be.
- **Opening:** the viewport opens scrolled to its right end.
- **Redraws:**
  - After a redraw whose drawn width changed, such as a new span of days, it goes to the end again.
  - Otherwise the reader's scroll position is kept.
- **Interactive only:** it runs in the component's script. Prerendered markup sits at the left until the script attaches. A static page scrolls its own box.
- Keyboard behaviour is unchanged.

## 2. The builder: `DailyCountsChart` (Lumen.Charts)

New file `src/Lumen.Charts/DailyCounts.cs`, shaped like `Placings.cs`.

```csharp
public sealed record DayCount(DateOnly Date, int Count);

public sealed record DailyCountsOptions
{
    public string Singular { get; init; } = "registration";
    public string Plural { get; init; } = "registrations";
    public DateOnly? From { get; init; }               // null: the earliest date given
    public DateOnly? To { get; init; }                 // null: the latest date given
    public double CellWidth { get; init; } = 30;       // about a 22-unit bar
    public string DateFormat { get; init; } = "ddd d MMM yyyy";   // invariant culture
    public bool RunningTotal { get; init; } = true;
    public string? Title { get; init; }                // null: "{Plural, capitalised} per day"
    public string? Color { get; init; }                // null: the style's first series colour
    public ChartStyle? Style { get; init; }            // null: the Light preset
}
```

### `DailyCountsChart.Build(IEnumerable<DayCount> counts, DailyCountsOptions? options = null)` → `ChartSpec?`

1. **Bad input:**
   - A null `counts` throws `ArgumentNullException`. Null `options` means the defaults.
   - Each of these throws `ArgumentException`, saying why:
     - a null entry;
     - a negative `Count`;
     - a blank `Singular` or `Plural`;
     - a `DateFormat` that can't format a sample date (probed as `Placings` does);
     - `From` after `To`;
     - a range longer than 4,000 days, with the message naming the day count.
   - What the chart can't draw, such as a `CellWidth` below 8 or a bad colour, is refused by chart validation when the spec is rendered, as with `Placings`.
2. **The range:**
   - It runs from `From`, or else the earliest date given, to `To`, or else the latest date given.
   - With no dates given and neither bound set, there is no range, and `Build` returns null.
   - Counts outside the range are ignored. Counts on one date are summed.
   - Days in the range with no count are 0.
3. **Nothing to draw:** when every day in the range is 0, `Build` returns null, and the host shows its own empty state.
4. **One point per day, in date order:**
   - `X` = 0, 1, 2 …; `Y` = the day's count.
   - `Label` = the date in `DateFormat`, in the invariant culture, e.g. `Mon 6 Oct 2026`. It names the category in the table and CSV.
   - `AxisLabel` and `Name` as below.
5. **Axis labels:**
   - The day of the month under every bar.
   - A second line on the first day and on each 1st: the month as `MMM`.
   - When the range covers more than one calendar year, the first day and each 1 January read `MMM yyyy` instead.
   - Example across a year end: `28` / `Dec 2026`, `29`, `30`, `31`, `1` / `Jan 2027`, … `1` / `Feb`.
6. **Names:**
   - The form is `{Label}: {count} {noun}`, then `, {running} so far` when `RunningTotal` is on. `running` includes that day.
   - Numbers are written with thousands separators, in the invariant culture. The noun is `Singular` for a count of 1 and `Plural` otherwise.
   - Examples: `Mon 6 Oct 2026: 12 registrations, 140 so far`, `Tue 7 Oct 2026: 1 registration, 141 so far`, `Wed 8 Oct 2026: 0 registrations, 141 so far`.
7. **Whole-number ticks.** The builder sets the Y axis itself, so it never writes "0.5 registrations":
   - The step is the smallest of 1, 2, 5, 10, 20, 50 … with `ceil(largest / step) ≤ 5`.
   - `YMax = step × ceil(largest / step)`, and `YTickValues` are 0, step, … `YMax`.
   - Examples: largest 1 gives 0 and 1; largest 3 gives 0–3; largest 12 gives 0, 5, 10, 15; largest 140 gives 0, 50, 100, 150.
8. **The spec:**
   - `Kind = Column`, `CellWidth` from the options.
   - `Width = 320` (ignored for the drawn width), `Height = 260`.
   - `Title` from the options, or the plural capitalised plus " per day" ("Registrations per day").
   - `Description = "{total} {noun} across {days} days"`, with "1 day" for a single day, e.g. `1,234 registrations across 90 days`.
   - `YLabel` empty, and `Style` from the options or Light.
   - One series, named the plural capitalised, with colour `Color` or the style's first series colour, `ValueLabels = true` and `MarkZero = true`.
   - A host changes anything else with `spec with { … }`.

There is no wrapper component. The host writes:

```razor
<LumenChart Spec="spec" FitWidth="true" ScrollToEnd="true" ShowLegend="false" />
```

## 3. The course-profile recipe (#20), docs only

- **The chart:** a line chart of elevation over distance. One series, coloured by height with `ChartSeries.Gradient` (stops that each clear 3:1).
- **Scale:** `YMinSpan` keeps a gentle course from filling the plot, and `YUnit` is `" m"`. No markers or value labels.
- **Not an area:** an area stands on zero, and Lumen refuses `YMinSpan` beside one (`ChartValidation.cs:489`). A profile filled down to the axis floor would need a new feature; it is out of scope.
- **Never colour alone:** the height is on the axis and in each point's name, so the colour carries nothing on its own.
- It sits in `recipes-race-face.md`, next to the existing "Elevation coloured by grade" in `sports.md`, and `tests/Lumen.Charts.Recipes` compiles it.

## 4. Tests, gallery, docs

### Unit tests

- **§1.1:**
  - the width arithmetic, including the 320 floor with the extra room on the right;
  - the bar width at 72% of the pitch;
  - 4,000 categories accepted with `CellWidth` and 4,001 refused; 101 refused without it;
  - the floor of 8;
  - the refusals on other kinds and on a sparkline, and the split messages;
  - the heatmap's 4,096 check, unchanged;
  - the component taking a column chart down the `lumen-fixed` path.
- **§1.2:** `width:{drawn}px` for `CellWidth` drawings, and `width:100%` otherwise.
- **§1.3:**
  - the lines and their positions, and the room below growing by 14;
  - names, the data table and CSV unchanged;
  - every refusal;
  - thinning: two-line labels kept, one-line labels only where clear, and the 8-unit pitch.
- **§1.4:**
  - the hairline's geometry and colour;
  - the muted "0", and the text colour when a custom `Muted` falls under 4.5:1;
  - non-zero bars unchanged;
  - the refusals;
  - the hash unchanged at `false`, and the default-JSON pin updated.
- **§1.5:** a label kept up to `CellWidth − 2`.
- **§1.6:**
  - the `aria-label` and `<title>` replaced on line, column, heatmap and gauge marks;
  - an averaged mark keeping Lumen's name;
  - the refusals, by kind, by length, when blank, and for control characters;
  - the status line;
  - the table and CSV unchanged.
- **§1.7:** the parameter reaches the component's script. Its behaviour is checked in the browser.
- **§2:** every builder rule, with invented data:
  - filling and summing, and counts outside the range ignored;
  - both null cases;
  - singular and plural, and the running total off;
  - year-crossing labels;
  - a one-day description;
  - the default and custom titles;
  - the ticks for largest 1, 3, 12 and 140;
  - every refusal, and 4,000 against 4,001 days.
- **Classic:** the classic-equals-0.23.0 test is unchanged.

### Baseline

- **Changed by design:** `heatmap-table/cell-width` and `heatmap-table/reasons`, in both finishes (root style only).
- **Added, in both finishes:**
  - a fixed-pitch column chart with plain labels;
  - axis labels without `CellWidth`;
  - `MarkZero`;
  - a daily-counts chart across a month end;
  - a daily-counts chart across a year end;
  - a line chart with `Name` on its points.
- **Every other row:** identical.

### Browser

On a 375 px phone (touch, scale 2), the gallery's "Registrations per day" card:
- scrolls sideways at its drawn width, and the page does not;
- opens scrolled to its right end (`scrollLeft + clientWidth ≥ scrollWidth − 1`);
- names its last bar with its `Name`, and a zero day as "…: 0 registrations, … so far";
- writes its muted "0" at 4.5:1 or more, measured in light, dark and Midnight;
- passes axe in all three themes.

The existing check that the page has one `.lumen-fixed` viewport now expects two.

### HTTP

- The Sports page prerenders the card: two `lumen-fixed` charts, the card's `--lumen-drawn`, and a named bar.
- A column spec with `CellWidth`, posted to the API, renders with `width:{drawn}px`.

### Gallery

- A "Registrations per day" card on the Sports & performance page, after "Category heatmap", wide, with invented counts:
  - about 75 days across a month end;
  - a few zero days;
  - `ScrollToEnd`.
- The pinned counts that follow from it move: the sidebar's 33, the unit tests on the card ids and counts, and `verify-api.ps1`'s fixed-chart count.

### Docs

- **README:** the column features, `ChartPoint.Name`, `ScrollToEnd` and `DailyCountsChart`; "0.47.0 additions"; the limits; and what changes for existing charts (§6).
- **The skill:** `references/api.md` and `SKILL.md`, keeping the frontmatter description at 1,024 characters or fewer.
- **Recipes:** in `recipes-race-face.md`, "Registrations per day" (the builder) and "Course profile" (§3). `tests/Lumen.Charts.Recipes` compiles both and renders the builder's chart: `check.py` learns to render `var x = …Chart.Build(…)!`.
- `docs/VERIFICATION.md`, and version 0.47.0.

## 5. Release

The handover's steps:
1. Delegated build.
2. Independent verification on master.
3. Pack **after** the release commit.
4. Refresh `artifacts/SHA256.json`.
5. Push, CI, tag.
6. GitHub release with the three `.nupkg`.
7. Package the skill.
8. Message "RACEFACE RUNNING EXPANSION 2" with the swap steps for #18 and the #20 recipe.

## 6. What changes for existing charts

- A heatmap with `CellWidth` rendered statically is now written at its drawn width instead of 100%. On a static page, put it in an `overflow-x:auto` box. Inside `<LumenChart>` nothing changes.
- Every other spec renders byte for byte as in 0.46.1.

## Accepted limits

- **The Y axis scrolls away** with the bars. There is no frozen axis; each bar's count carries its value.
- **Size:** 4,000 days at the default pitch make a drawing about 120,000 units wide with 4,000 marks. Static SVG keeps one tab stop per mark, as it does today.
- **`Name` owns the whole name:** a host that sets it must say any state its colours show.
- **`ScrollToEnd`** is interactive only.

## Coordination

The worktree `lumen-interaction-state` (spec 3ef1e7e) is likely to touch `<LumenChart>` too. 0.47.0 adds `ScrollToEnd` and a status-line branch for `Name` to `LumenChart.razor`, and the scroll call to `lumen.js`. Whichever lands second merges the other's changes; the owner decides the order.

## Out of scope

- A frozen Y axis.
- An area that stands on its axis floor instead of zero.
- `CellWidth` on bars or stacked columns.
- A wrapper component, and an HTTP endpoint for the builder.
- 0.45.1 (places and points follow-ups), #17 (stays HTML) and #19 (waits for the owner's word on a raster package).
