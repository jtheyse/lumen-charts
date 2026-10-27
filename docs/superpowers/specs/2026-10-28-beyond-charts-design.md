# Beyond charts: tiles, tables and timelines

Design, 28 October 2026. Status: proposed, waiting for the owner's approval.

The owner asked for Lumen to offer components beyond charts and graphs, "tiles, tables, timelines and the like", as the commercial libraries Lumen was first measured against do (Infragistics Ignite UI, SciChart, ApexCharts, ScottPlot; Syncfusion and Telerik are in the same market). The owner skipped the questions and asked for a proposal written straight away, so every choice below is Claude's call unless it says otherwise. Each is open to change when the owner reads this.

## 1. Intent

A results page, a team dashboard or a season summary is rarely charts alone. Next to the charts there are a row of headline numbers, a table of results and a list of what happened when. Today a host writes those by hand. Race Face did, for its season shape strip (#3) and round strips (#17), which stayed HTML, and for the race-plan timeline in its "Not charts" list. Hand-built pieces do not share Lumen's colours, number formats, change words, contrast checks or keyboard behaviour, so the page reads as two products.

These components close that gap. They are the **data display** pieces that sit beside charts and speak the same language: the same `ChartStyle` and branding, the same value formats (`ValueFormat.Duration`, `Compact`, `Signed`, units), the same "better / worse" words and colours, the same rules that drawn text clears 4.5:1 and that colour never carries meaning alone, and the same output from the core library for servers, static pages and PDFs as from the Blazor component.

## 2. What the commercial libraries offer, and where Lumen stands

| Family | Ignite UI / Syncfusion / Telerik | Lumen today |
|---|---|---|
| Charts, sparklines, gauges, rings, heatmaps, calendars | yes | yes (22+ kinds) |
| Network graphs | partly | yes (`<LumenGraph>`) |
| Scheduler / season calendar | yes | yes (`<LumenPlanner>`, 0.43–0.44) |
| KPI / stat cards | Syncfusion and Telerik dashboards; Ignite UI through cards | **no** |
| Data grid / table | yes, with editing, grouping, pivot | heatmap grid (`ChartExport.HtmlTable`) and the "View data" table only |
| Event timeline (vertical history) | Syncfusion Timeline, Telerik Timeline | **no** (`ChartKind.Timeline` is a state lane chart, a different thing) |
| Maps | yes | no |
| Gantt, Kanban, spreadsheets, inputs, dialogs, navigation | yes | no |

The three gaps worth closing are the ones a data page needs beside its charts: **stat tiles**, a **data table** and an **event timeline**. The rest are either general UI (which MudBlazor, already used by Race Face, covers) or a different product (maps need tile servers; spreadsheets and Kanban are applications).

## 3. Goals, non-goals and anti-goals

**Goals**

- Three components, one per minor release: stat tiles (0.48.0), a data table (0.49.0), an event timeline (0.50.0).
- Each one has a core renderer in `Lumen.Charts` that returns HTML, a Blazor component in `Lumen.Charts.Blazor` that adds the interaction, and an HTTP endpoint in `Lumen.Charts.AspNetCore`, as charts and the planner do.
- They look and read as one family with the charts on the same page, in every preset (Light, Dark, Midnight), in the refined and classic finishes, and under `<LumenBrand>`.
- They work on a 360 px phone, a tablet and a desktop, prerendered and interactive, on Blazor Server and WebAssembly.
- Every one passes the axe WCAG A/AA sweep, works from the keyboard alone, and says everything it shows in words.

**Non-goals (not now; each could be its own later request)**

- Editing, grouping, pivoting, column filters, column resizing or reordering, row selection or virtual scrolling in the table. Hosts filter and group their rows before passing them in.
- A horizontal timeline, a Gantt chart, a Kanban board.
- Maps.
- Email clients. The HTML is for browsers, static pages and PDF renderers that read a `<style>` block.

**Anti-goals (would make this a failure even if it worked)**

- Lumen turning into a second general UI kit: no buttons, inputs, dialogs, menus, navigation or forms beyond the few controls a component needs for itself (sort headers, page buttons, "show more").
- Anything that changes an existing chart's drawing. The 409 baseline renderings must stay byte for byte as they are.
- Global CSS: no resets, no element selectors outside a `lumen-` root class. The components must sit inside a MudBlazor or Bootstrap page without changing that page or being changed by it.
- Third-party code, a JavaScript framework or a CDN.
- Colour carrying meaning alone, or text under 4.5:1.

## 4. Shared foundation (built in 0.48.0, used by all three)

**HTML, not SVG.** These components are mostly words and numbers: they should wrap, reflow, select, translate and be read by screen readers as text. The core renders them as HTML strings, the way `ChartExport.HtmlTable` does today. A sparkline inside a tile or a table cell is the existing sparkline SVG, drawn by `ChartSvg.Render`, embedded in that HTML.

**One renderer.** The Blazor components write the core's HTML (as `<LumenChart>` writes the core's SVG) and keep only their state (sort, page, "show more") themselves, asking the core to render again when it changes. So prerendered, interactive, static and server output are the same markup for the same state.

**Style carried with the markup.** The rendered fragment's root element sets the style's colours and font as CSS custom properties (`--lumen-bg`, `--lumen-text`, `--lumen-muted`, `--lumen-grid`, `--lumen-rising`, `--lumen-falling`, `--lumen-accent`, `--lumen-font`) from `ChartStyle`. The rules that use them come in a `<style>` block inside the fragment, scoped under the component's root class, as an SVG carries its own `<style>`. The core is the only source of those rules, so the Blazor stylesheet does not repeat them and cannot drift from them. Builder's choice: whether the `<style>` block is written once per fragment, or once per page by the component with a flag such as `includeStyle: false` for the second and later fragments.

**Theming.** Each spec takes a `ChartStyle? Style`. Each component takes the cascading `ChartStyle` from `<LumenBrand>` exactly as `<LumenChart>` does, so a page branded once brands its tiles, tables and timelines too. The `ChartFinish` is honoured where it changes something visible, such as the hairline weight of rules. These components have no classic look to preserve, so both finishes start from the same new design.

**Words, formats and change.** One shared internal helper turns a value into text and a pair of values into change words, using the existing `ValueFormat` and unit rules (`Axis` / `LinearScale` formatting, invariant culture), so a tile, a table cell and a chart's tooltip write "1:12:05" or "1.2k" the same way. Change words:

- `Better` is `Higher`, `Lower` or `Neither` (the default).
- The change is written as an arrow and words: "▲ 12% · better than last season", "▼ 0:42 · worse than last season", or "= same as last season". With `Neither` the words are "up" and "down".
- It is shown as a difference, a percentage, or both (`ChangeAs`). A percentage of a previous value of 0 falls back to the difference.
- Its colour is `Rising` for better and `Falling` for worse, used only where that colour clears 4.5:1 against the background, and the text colour otherwise. A `Neither` change and no change are in the muted colour where it clears 4.5:1.

**Accessibility rules, for all three.**

- Semantic HTML: lists are lists, tables are tables with a caption and scoped headers, and dates are `<time datetime>`.
- Every visual cue (arrow, colour, bar, status chip, sparkline) also exists in the text, either visibly or as visually hidden text.
- Focus rings use `--lumen-accent` and follow the viewport ring Lumen already draws.
- A scrolling area is a focusable `role=region` with a name, like the chart viewport and the "Chart data" table.

**Validation.** Each spec has a validator that throws `ArgumentException` with a plain message, as `ChartValidation` and `PlannerValidation` do, and the HTTP endpoints turn it into a 400. Text is HTML-escaped. Links accept `http`, `https`, relative and `mailto` addresses only.

**Data in the repository.** Gallery cards, recipes and tests use invented data only (the Race Face constraint).

## 5. Stat tiles (0.48.0)

A row or grid of headline numbers: "Races 12", "Best finish 3rd ▲ 2 places · better than last season", "Total time 14:12:30", each with an optional trend line and target.

**Core API.** The names are this design's calls. The builder may adjust them where the codebase's conventions say otherwise.

- `StatTile(string Label, double? Value)`, with:
  - `Format` (a `ValueFormat`) and `Unit`;
  - `Previous` (double?), `Better`, `ChangeAs` and `Since` ("last season"), which together make the change words of §4;
  - `ChangeText`, which replaces those words outright for a change Lumen should not compute (such as "new best");
  - `Missing` (1–24 characters, written when `Value` is null, such as "No race yet"; default "—" with the visually hidden word "none");
  - `Note`, one muted line of at most 60 characters;
  - `Trend` (up to 400 `double?` values, nulls as gaps), drawn as a sparkline under the value, with a visually hidden summary ("12 values, lowest 34, highest 41, last 41");
  - `Target` (double?), drawn as a thin meter under the value with the words "82% of target 50";
  - `Href`, which makes the whole tile a link.
- `StatTilesSpec(IReadOnlyList<StatTile> Tiles)`, with:
  - `Title` and `Description` (optional, written as a heading and a paragraph);
  - `Columns` (null for automatic: as many tiles of at least 150 px as fit, two on a 360 px phone; or 1–6 fixed);
  - `Density` (`Regular` or `Compact`; Compact fits Race Face's round-by-round strip);
  - `Style`.
- `StatTilesHtml.Render(StatTilesSpec spec)` returns the HTML fragment.
- Limits: 1–48 tiles; a label of at most 40 characters.

**Markup and reading order.** A `<section>` named by its title, or by `aria-label` from the description, holds a `<ul>` of tiles. Each tile reads, in order, label, value, change, note, target, trend summary. The value is the largest text, then the label, then the change. Exact sizes are the builder's, within the 4.5:1 rule and an 11 px floor, which the charts share.

**Component.** `<LumenStats Spec="…"/>` writes the core's HTML and adds nothing interactive. Link tiles are ordinary links. Its value is the cascading brand and the same markup in prerender and on the circuit.

**HTTP.** `POST …/stats/html` takes a `StatTilesSpec` and returns `text/html`.

**Gallery.** A "Season at a glance" card on a new Components page: six tiles with invented data, among them a duration with lower-is-better, a place, a count with a trend and a target. Next to it, a compact strip of eight rounds standing in for Race Face's #3 and #17.

## 6. Data table (0.49.0)

A read-only table of rows with typed columns, sorting, paging, sticky headers and in-cell bars and sparklines: results, standings, a club's riders, a training log.

**Core API.**

- `TableColumn(string Header)`, with:
  - `Kind`: `Text`, `Number`, `Date`, `Change`, `Bar` or `Sparkline`. Durations and places are `Number` with a format.
  - `Format`, `Unit` and `DateFormat`.
  - `Align` (automatic: numbers, changes and bars to the right, the rest to the left).
  - `Sortable` (default true, false for sparklines).
  - `Better`, for `Change` columns and for sorting a column "best first".
  - `BarMax` (the bar column's scale end; default the largest value in the column).
  - `RowHeader` (exactly one column, the first by default, is written as `<th scope=row>`).
- `TableRow(IReadOnlyList<TableCell> Cells)`, cells matched to columns by position.
- `TableCell`, with `Text`, `Number` (double?), `Previous` (for `Change`), `Date`, `Trend` (for `Sparkline`), `Note` (a muted second line) and `Href`.
- `TableSpec(IReadOnlyList<TableColumn> Columns, IReadOnlyList<TableRow> Rows)`, with:
  - `Caption` (required: it names the table and its scrolling region);
  - `Description`;
  - `SortBy` (a column index) and `SortDescending`;
  - `PageSize` (null for every row; or 10–500);
  - `StartAtEnd` (open on the last page: the newest days of a long log, the same need 0.47.0's `ScrollToEnd` met for charts);
  - `Footer` (optional cells for a totals row);
  - `Style`.
- `TableHtml.Render(TableSpec spec, int page = 0)` returns the HTML for one page in the spec's sort. `TableExport.Csv(TableSpec spec)` writes every row in the spec's sort.
- Limits: 1–32 columns; at most 100,000 rows; without `PageSize`, at most 5,000 rows (more is refused with a message that names `PageSize`).

**Sorting.** Sorting is stable. Missing values go last in both directions. Text compares with the invariant culture, ignoring case. Dates and numbers compare by value. A `Change` column sorts by its difference. The header of the sorted column carries `aria-sort`.

**Cells.**

- A number is written in its format and unit.
- A change cell writes §4's arrow and words in a short form ("▲ 12%"), with the full words visually hidden.
- A bar cell writes its value as text and draws the bar behind or beside it, hidden from screen readers, in series colour 1 on a `Grid` track. A negative value runs left of a zero line.
- A sparkline cell is a sparkline 96 units wide by 24 high, with a visually hidden summary.

**Layout.** The table sits in a focusable `role=region` named by its caption that scrolls both ways. The header row and the row-header column stay put while it scrolls (sticky), which is how a 360 px phone reads a wide table. Builder's choice: the row height and padding, and the exact `Compact` and `Regular` densities, if both are offered.

**Component.** `<LumenTable Spec="…"/>`:

- Column headers become buttons that sort: the first press sorts in the column's natural direction ("best first" with `Better`, else ascending), the next reverses it.
- Paging appears under the table when `PageSize` is set: First, Previous, "Rows 51–100 of 4,000", Next, Last.
- An "Export CSV" button is offered where `ShowExport` is on.
- A `role=status` line announces "Sorted by Finish time, fastest first" and "Rows 51–100 of 4,000".
- A sort or a page change keeps keyboard focus on the control that made it.

**HTTP.** `POST …/table/html` (with an optional `page`) and `POST …/table/csv`.

**Gallery.** A results table of 40 invented riders with place, name, club, a finish-time duration (lower is better), a change on the previous race, a bar of points and a sparkline of the season. Plus a training log of 400 invented days with `PageSize` 25 and `StartAtEnd`.

**Follow-on, not part of 0.49.0.** `<LumenChart>`'s "View data" table could later be drawn by this component, which would fix the 0.47.2 "oldest 500 days" limit with paging. Until then, 0.47.2 can fix that limit on its own (see §9).

## 7. Event timeline (0.50.0)

A vertical list of dated events: a rider's race history, a race-plan timeline with a "now" line, a club's season of news, an order of the day. It is named `EventTimeline` and `<LumenEvents>` to keep it apart from `ChartKind.Timeline`, the state lane chart.

**Core API.**

- `TimelineEvent(DateTimeOffset When, string Title)`, with:
  - `End` (a span, written "Sat 4 – Sun 5 Oct");
  - `AllDay` (dates only, no times);
  - `Note`, at most 200 characters;
  - `Value` (a short figure such as "3rd · 1:12:05", at most 40 characters);
  - `Status`: `None`, `Done`, `Current`, `Upcoming` or `Cancelled`, written as a word with a glyph (✓ Done, ● Now, ○ Upcoming, ✕ Cancelled) and coloured with the accent for `Current`, never colour alone;
  - `Category` (a short chip, at most 24 characters);
  - `Href`.
- `EventTimelineSpec(IReadOnlyList<TimelineEvent> Events)`, with:
  - `Title` and `Description`;
  - `NewestFirst` (default false);
  - `GroupBy`: `None`, `Day`, `Month` or `Year`, written as headings;
  - `Now` (DateTimeOffset?, which writes a "Now · Tue 28 Oct 2026" marker between the past and the future);
  - `TimeZone`, as `ChartSpec.TimeZone` is read;
  - `DateFormat` and `TimeFormat`;
  - `Style`.
- `EventTimelineHtml.Render(EventTimelineSpec spec, int shown = int.MaxValue)` returns the HTML fragment.
- Limits: at most 2,000 events; a title of at most 120 characters.

**Markup.** An `<ol>` of groups, each with its heading and its own `<ol>` of events. Each event has a `<time datetime>`, its title (a link with `Href`), value, status, category and note, in that reading order. The rail and dots down the left are decoration, hidden from screen readers. The now marker is a list item with its words, not decoration.

**Component.** `<LumenEvents Spec="…" PageSize="20"/>` shows the first 20 events, or the 20 round `Now` when `Now` is set, with "Show 20 more" buttons before and after, and announces how many are shown. Without a page size it writes them all.

**HTTP.** `POST …/timeline/html`.

**Gallery.** An invented rider's season: past races done with their results, the next race current, the rest upcoming, one cancelled, grouped by month with a now marker.

## 8. Constraints

- `net8.0` for the libraries; the solution, CI and release ritual as in `HANDOVER.md`. Every new public member has an XML doc comment (the build fails without one).
- No new package. All three live in the existing three packages, and the packages are released together as now.
- Existing renderings must not move: the 409 baseline rows stay identical in both finishes. The new renderers get their own baseline rows, which may only be added to.
- Nothing in these components may change `ChartSvg.Render` output for an existing spec, including the gradient-ID hash.
- Browser checks for each release: the axe sweep in Light, Dark and Midnight; keyboard-only use; a 375 px phone opened with `isMobile` and `hasTouch` (see `HANDOVER.md`'s gotchas); prerender equals the first interactive render; and the WebAssembly host where the component's script or CSS changed.
- Contrast is checked by the unit tests against 4.5:1 for every text colour each component can choose, because axe misses SVG text.

## 9. Order of work

1. **0.47.2 first (small, already designed in chat, Claude's recommendation).** Three of its follow-ups are independent of this design:
   - a zero day's tap target;
   - the right-hand Y title under the 320 floor;
   - `ColumnMargins` sharing `Framed`'s rule.

   The fourth, "View data shows the oldest 500 days", can be fixed there now by showing the latest 500 when `ScrollToEnd` is on. Or it can wait for the table component (§6). That is the owner's call.
2. **0.48.0, stat tiles**, with the shared foundation of §4.
3. **0.49.0, the data table.**
4. **0.50.0, the event timeline.**

Each release goes through the usual flow: its own short spec section if this one needs detail added, a plan the owner approves, a subagent-driven build with Opus reviews, independent verification, the owner's merge call and the release ritual.

## 10. Left to the builder

- The exact CSS (sizes, spacing, radii, the hover and focus states) within the rules above, and its class names below each component's `lumen-` root class.
- The internal shape of the shared HTML writer and format helper, and the file layout.
- Whether `Compact` density exists for the table in 0.49.0.
- The wording of validation messages, in the style `ChartValidation` uses.
- Test names and how the new baseline rows are grouped.

## 11. Calls made in this design (for the owner to check)

1. The set is three components (stat tiles, a data table, an event timeline). Maps, Gantt, Kanban and general UI are left out, and MudBlazor stays the host's general UI kit.
2. The order is tiles, then table, then timeline: tiles are smallest and lay the shared foundation.
3. They render HTML, not SVG, from the core, with the style carried as CSS custom properties and a scoped `<style>` block.
4. The table is read-only: no editing, grouping, filtering or virtual scrolling.
5. They are named `<LumenStats>`, `<LumenTable>` and `<LumenEvents>`, the timeline being `EventTimeline` to keep it apart from `ChartKind.Timeline`.
6. No new NuGet package; all three ship inside the existing three.
7. 0.47.2 ships before 0.48.0.
