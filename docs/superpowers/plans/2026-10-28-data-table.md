# Data table (0.49.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `<LumenTable>`, a read-only table of typed columns built on 0.48.0's HTML foundation. It sorts, pages, keeps its header and row-header column sticky, and writes bar, change and sparkline cells, with CSV export.

**Architecture:**
- **Core (`Lumen.Charts`):**
  - public records `TableColumn`, `TableCell`, `TableRow` and `TableSpec`, with a validator;
  - an internal `TableView` that sorts and pages;
  - `TableHtml.Render`, which writes the HTML fragment;
  - `TableExport.Csv`.
- **Blazor:** `<LumenTable>` keeps the sort and the page as state and asks the core to render again whenever either changes. Header and paging buttons sit inside the core's fragment, so a small `lumen.js` delegate turns their clicks into .NET calls.
- **AspNetCore:** two endpoints.
- **Gallery:** two cards on the Components page.

**Tech Stack:** C# / .NET 8 (libraries), .NET 10 (gallery, tests), Blazor Server and WebAssembly, browser JavaScript (an ES module), ASP.NET Core minimal APIs, Playwright for .NET with axe-core.

**Spec:** `docs/superpowers/specs/2026-10-28-beyond-charts-design.md`: §4 (the shared foundation), §6 (the data table) and §8 (constraints). The owner approved it on 28 Oct 2026, and it is the binding authority. Where this plan pins something §6 leaves open, the plan says so. §7 (the timeline) is 0.50.0; do not build it.

## Global Constraints

- **No third-party code**, no framework and no CDN. Everything lives in the existing three packages.
- **Scoped CSS.** Every CSS rule sits under the root class `.lumen-datatable`. Do not use `.lumen-table`, which `<LumenChart>`'s "View data" wrapper already uses. The root and the table reset their own margins and padding, and nothing may change a MudBlazor or Bootstrap page around them.
- **Existing output is untouched.** The 416 baseline rows stay byte-identical in both finishes: the charts, and the five `tiles/*` rows too, even after the sparkline helper is shared. Rows for the table may only be added.
- **Contrast.** Every text colour clears 4.5:1 against its background, through `LumenHtml.Readable`.
- **Never colour alone.** Every arrow, bar, colour and sparkline also exists in words, either visible or visually hidden (`lumen-sr`, through `LumenHtml.HiddenRule(".lumen-datatable")`).
- **Escaping and links.** Every text is written through `LumenHtml.E`, and every link must pass `LumenHtml.SafeLink` and is written encoded. The fragment has no `id` attribute.
- **Formats.**
  - Numbers go through `ChangeWords.Value(value, format, unit)`.
  - Changes go through `ChangeWords.Of(...)`.
  - Dates are formatted with `CultureInfo.InvariantCulture` and the column's `DateFormat`, which defaults to `d MMM yyyy`.
- **API hygiene.**
  - Every new public member has an XML doc comment, and the Release build shows 0 warnings. A Razor component's class doc goes in a `.razor.cs` partial (CS1591 is suppressed for Razor).
  - Validation throws `ArgumentException` in `ChartValidation`'s voice.
- **Invented data only.**
- **Processes, git and leftovers.** Follow `how-to-run.md` in the plan's SDD workspace. The controller writes it, with the same rules as 0.48.0. Never open `lumen-charts-race-face-brief.md`. Keep the SKILL.md frontmatter byte-identical.

## Review Focus

1. **Sorting puts missing values last in both directions**, and is stable: equal keys keep the spec's row order whether ascending or descending. Test: Task 1.
2. **`StartAtEnd` on a log of 400 rows with `PageSize` 25** opens on rows 376–400, both statically and in the component. A page past the end is refused. Test: Tasks 1 and 3.
3. **A bar column with negative values** draws from a zero line, and its words still give the signed value. Test: Task 2.
4. **Focus after a sort or page click.** The control the reader pressed keeps keyboard focus after the re-render, even though the fragment is rewritten. Test: Task 4.
5. **A 360 px phone.** The table scrolls sideways inside its region, the header row and the row-header column stay in view, and the page itself never scrolls sideways. Test: Task 4.

---

### Task 1: The table model, validation, sorting, paging and CSV

**Files:**
- Create: `src/Lumen.Charts/Table.cs`, holding the public `ColumnKind`, `ColumnAlign`, `TableColumn`, `TableCell`, `TableRow` and `TableSpec`.
- Create: `src/Lumen.Charts/TableValidation.cs`, holding `internal static class TableValidation`.
- Create: `src/Lumen.Charts/TableView.cs`, holding `internal sealed class TableView`.
- Create: `src/Lumen.Charts/TableExport.cs`, holding the public `TableExport.Csv`.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, a `// 0.49.0` block before `string ChartMarkup(`.

**Interfaces:**
- Produces:
  - `public enum ColumnKind { Text, Number, Date, Change, Bar, Sparkline }`.
  - `public enum ColumnAlign { Auto, Left, Right, Center }`. `Auto` means right for `Number`, `Change` and `Bar`, and left for the rest.
  - `public sealed record TableColumn(string Header)`, with `init` properties:
    - `ColumnKind Kind` (default `Text`);
    - `ValueFormat Format` (default `Number`), `string? Unit` and `string? DateFormat`;
    - `ColumnAlign Align`;
    - `bool Sortable = true`;
    - `Better Better` (default `Neither`);
    - `double? BarMax`;
    - `bool RowHeader`.
  - `public sealed record TableCell`, with `init` properties `string? Text`, `double? Number`, `double? Previous`, `DateOnly? Date`, `IReadOnlyList<double?>? Trend`, `string? Note` and `string? Href`. Static helpers make the common cases short to write: `TableCell.Of(string text)`, `TableCell.Of(double? number)` and `TableCell.Of(DateOnly? date)`.
  - `public sealed record TableRow(IReadOnlyList<TableCell> Cells)`.
  - `public sealed record TableSpec(IReadOnlyList<TableColumn> Columns, IReadOnlyList<TableRow> Rows)`, with:
    - `string Caption` (required);
    - `string? Description`;
    - `int? SortBy` and `bool SortDescending`;
    - `int? PageSize`, `bool StartAtEnd`;
    - `IReadOnlyList<TableCell>? Footer`;
    - `Density Density` (0.48.0's enum);
    - `ChartStyle? Style`.
  - `internal sealed class TableView`:
    - built by `TableView.Of(TableSpec spec, int? sortBy, bool descending)`, which validates the spec first;
    - `IReadOnlyList<int> Order`: row indexes in sorted order;
    - `int Pages`: 1 without `PageSize`;
    - `int DefaultPage`: `Pages − 1` with `StartAtEnd`, else 0;
    - `(int First, int Last) RowsOn(int page)`: zero-based, inclusive. It throws `ArgumentException` for a page outside `0..Pages−1`.
  - `public static class TableExport` with `public static string Csv(TableSpec spec)`.

**Validation (exact):**
- Columns: 1–32.
- Rows: at most 100,000. Without `PageSize`, at most 5,000, refused with this message: `"A table without PageSize writes every row, so it takes at most 5,000; set PageSize (10 to 500) to page a longer one."`
- Every row, and `Footer` when set, has exactly as many cells as there are columns.
- `Header`: 1–60 characters, not blank. `Caption`: 1–120 characters, not blank. `Description`: at most 300.
- `Text`: at most 200 characters. `Note`: at most 80. `Href`: must pass `SafeLink`.
- Numbers and `Previous` are finite. `Trend` has at most 400 values, of which at least 2 are not null.
- `BarMax`: finite and > 0. `DateFormat`: at most 40 characters, and must format `2026-01-31` without throwing.
- `PageSize`: 10–500. `SortBy`: a valid index of a `Sortable` column that is not a `Sparkline` column.
- At most one `RowHeader` column. When none is marked, the first column is the row header.
- **Cell fields must match the column kind:**
  - `Text` uses `Text`;
  - `Number` and `Bar` use `Number`;
  - `Date` uses `Date`;
  - `Change` uses `Number` and `Previous`;
  - `Sparkline` uses `Trend`;
  - any cell may also carry `Note` and `Href`;
  - another kind's field is refused, naming the column and the row;
  - a cell whose own field is null is a missing value.
- **`Footer`:** its cells may be `Text` in any column, or the column kind's own field.

**Sorting (exact):**
- **Keys by column kind:** `Text` sorts by `Text` (`StringComparer.InvariantCultureIgnoreCase`); `Number` and `Bar` by `Number`; `Date` by `Date`; `Change` by `Number − Previous`, which is missing when either is null.
- **Missing values** go last whether ascending or descending.
- **Stability:** ties keep the spec's row order in both directions. Do not sort ascending and then reverse.
- **Direction:**
  - `TableView.Of(spec, sortBy, descending)` uses its arguments. Without them it falls back to `spec.SortBy` and `spec.SortDescending`, and with no `SortBy` at all the spec's order is kept.
  - A column's natural first direction (used by the component, Task 3) is descending for `Better.Higher` and ascending otherwise. Expose it as `internal static bool NaturalDescending(TableColumn c)`.

**CSV (exact):**
- The first line holds the headers. Then every row follows in the view's order (all pages), then the footer when set.
- Each field by kind:
  - `Text`: as written;
  - `Number` and `Bar`: the number in `"R"` invariant;
  - `Date`: `yyyy-MM-dd`;
  - `Change`: the number;
  - `Sparkline`: its values in `"R"`, joined by `;`, with an empty slot for a null.
- A missing value is an empty field.
- Quote a field containing `,`, `"`, `\r` or `\n` the way `ChartExport.Csv` does: read its quoting helper and reuse it, or match it exactly. Lines end in `\r\n`, as there.

- [ ] **Step 1: Write the failing unit tests**, one `Test(...)` for each of the following, named in the file's style with `0.49.0: …`:
  - **Each validation rule** at its boundary and one past it, including the exact `PageSize` message, a mismatched field in a cell (for example, `Text` in a `Number` column), the cell count, and `SortBy` on a sparkline column.
  - **Review Focus 1:**
    - rows with numbers `[3, null, 1, 3, 2]` sort ascending to row order `[2, 4, 0, 3, 1]`, and descending to `[0, 3, 4, 2, 1]`: the null is last both times, and the two 3s keep their order;
    - text compares ignoring case;
    - a `Change` column sorts by its difference.
  - **Review Focus 2:** 400 rows with `PageSize` 25 and `StartAtEnd` give `Pages == 16`, `DefaultPage == 15` and `RowsOn(15) == (375, 399)`. `RowsOn(16)` throws `ArgumentException`.
  - **CSV:**
    - its exact text for a 3-row spec with one column of each kind and a footer;
    - a text containing a comma and a quote is quoted;
    - the rows come in the sort order.
  - **`NaturalDescending`:** true for `Better.Higher`, false for `Lower` and `Neither`.
- [ ] **Step 2: Run the tests.** Expected: the build fails because the types do not exist, which is the RED.
- [ ] **Step 3: Implement** the four files.
- [ ] **Step 4: Run the unit tests.** Expected: all pass, 0 warnings.
- [ ] **Step 5: Commit.**

### Task 2: The HTML renderer, and the sparkline helper shared with the tiles

**Files:**
- Create: `src/Lumen.Charts/TableHtml.cs`, holding `public static class TableHtml`.
- Modify: `src/Lumen.Charts/Html.cs`. Add `internal static (string Svg, string Summary) Sparkline(IReadOnlyList<double?> values, string name, ValueFormat format, string? unit, ChartStyle style, int width, int height)`. It holds the tile's present sparkline code, unchanged: the `ChartSpec`, `includeLegend: false`, `includeTitles: false`, the stripped root `<title>`, the stripped `tabindex='0'`, and the summary `"{n} values, lowest {min}, highest {max}, last {last}"`.
- Modify: `src/Lumen.Charts/StatTiles.cs`, so that `Tile()` calls the new helper with 120×28.
- Modify: `tests/Lumen.Charts.Tests/Program.cs` and `tests/Lumen.Charts.Baseline/Program.cs`, and both references (added rows only).

**Interfaces:**
- Consumes: Task 1's types and `TableView`.
- Produces:
  - `public static string TableHtml.Render(TableSpec spec, int? page = null)`. Here `null` means the view's `DefaultPage`. This pins what §6 left as `page = 0`, so that static output honours `StartAtEnd`.
  - `internal static string TableHtml.Render(TableSpec spec, int? page, int? sortBy, bool descending, bool controls)`. With `controls`, the fragment also holds the buttons that the component (Task 3) wires up.

**Markup (exact structure):**
- **Root:** `<div class='lumen-datatable lumen-datatable-{regular|compact}' style='{RootStyle}'>`, then `<style>…</style>` (with `HiddenRule(".lumen-datatable")`), then `<p class='lumen-datatable-desc'>` when there is a `Description`.
- **Scrolling region:** `<div class='lumen-datatable-scroll' role='region' tabindex='0' aria-label='{Caption}'>`, holding the `<table>`, which starts with `<caption>{Caption}</caption>`.
- **Header row:** `<thead><tr>`, then one `<th scope='col' class='lumen-datatable-{align}'>` per column.
  - The sorted column's `<th>` carries `aria-sort='ascending'` or `'descending'`; no other header has `aria-sort`.
  - With `controls`, a sortable header's text sits in `<button type='button' class='lumen-datatable-sort' data-lumen-sort='{index}'>{Header}</button>`. Without them, the header is plain text.
- **Body:** `<tbody>` holds the rows of the page, in the view's order. Each row is a `<tr>`; the row-header column's cell is `<th scope='row'>`, and the others are `<td>`.
- **Footer:** `<tfoot>` when there is a `Footer`.
- **Paging line:** with `PageSize`, after the region comes `<div class='lumen-datatable-pages'><span class='lumen-datatable-range'>Rows {first+1}–{last+1} of {total N0}</span></div>`. With `controls`, add before the span the buttons First and Previous, and after it Next and Last, each `<button type='button' data-lumen-page='{n}'>`, disabled where they would not move.
- **Missing value:** `<span aria-hidden='true'>—</span><span class='lumen-sr'> none</span>`.
- **Cells:**
  - `Text`: as written.
  - `Number`: `ChangeWords.Value`.
  - `Date`: the formatted date.
  - `Change`: compute the full words with `ChangeWords.Of(number, previous, column.Better, ChangeAs.Difference, since: null, …)`, for example `▲ 2 · better`. Write the part before `" · "` visibly (`▲ 2`), the rest hidden (`<span class='lumen-sr'> · better</span>`), in the returned colour. `= same` is written whole.
  - `Bar`: `<span class='lumen-datatable-bar' aria-hidden='true'>` holding a track in `--lumen-grid` and a fill in series colour 0, then the value as text.
    - The scale runs from `min(0, smallest)` to `max(BarMax ?? largest, 0)`, over the whole column, every page included.
    - A negative value fills from the zero line leftwards. The words keep the sign.
  - `Sparkline`: the shared helper at 96×24, `<span class='lumen-datatable-trend' aria-hidden='true'>{svg}</span><span class='lumen-sr'>{summary}</span>`.
  - `Note`: `<span class='lumen-datatable-note'>` as a muted second line. `Href`: wraps the cell's main text in an encoded `<a>`.
- **Sticky CSS:** `thead th{position:sticky;top:0}`, and the row-header cells `{position:sticky;left:0}`, each with the background `var(--lumen-bg)` and a z-index above the body cells. The region has `overflow:auto` and a `max-height` only through a CSS custom property, `--lumen-datatable-height`, that a host may set; there is no default height. The focus ring on the region and the buttons is `2px solid var(--lumen-accent)`.

- [ ] **Step 1: Write the failing tests:**
  - The structure, checked by parsing with `XElement`: the caption, `scope`s, `aria-sort` on the sorted column only, the row header, the footer and the missing words.
  - The `controls` markup, against the static markup.
  - **Review Focus 3:** a bar column `[-5, 10]` without `BarMax` puts its zero line at one third of the track, and writes `-5` and `10` as text.
  - **The change cell:** its visible and hidden parts, and its `Readable` colour.
  - **The sparkline cell:** 96×24, no `<title>`, no `tabindex`, and the summary text.
  - **Paging text:** `Rows 376–400 of 400` from the default page with `StartAtEnd`.
  - No `id` anywhere, and hostile text escaped.
  - **The tiles are untouched:** the five `tiles/*` baseline rows still hash the same after the helper move. Check this by running the baseline harness.
- [ ] **Step 2: Run them.** Expected: they fail.
- [ ] **Step 3: Implement** `TableHtml` and the sparkline helper move.
- [ ] **Step 4: Run the unit tests and both baselines.** Expected: all pass, and the 416 existing rows are identical.
- [ ] **Step 5: Add the baseline rows** after the last row:
  - `table/results-light`: 12 invented riders, with columns place, name, club, time (Duration, `Better.Lower`), change, points (Bar) and season (Sparkline), plus a footer;
  - `table/results-dark`: the same in `ChartStyle.Dark`;
  - `table/log-last-page`: 400 days, `PageSize` 25, `StartAtEnd`;
  - `table/compact-sorted`: `Density.Compact`, `SortBy` time, descending;
  - `table/negative-bars`.

  Both finishes give 421 rows. Update both references with the added rows only.
- [ ] **Step 6: Commit.**

### Task 3: `<LumenTable>`, its script and the HTTP endpoints

**Files:**
- Create: `src/Lumen.Charts.Blazor/LumenTable.razor` and `LumenTable.razor.cs` (the class XML doc).
- Modify: `src/Lumen.Charts.Blazor/wwwroot/lumen.js`, adding `export function attachTable(root, dotnet)` and `export function tableFocus(root, selector)`.
- Modify: `src/Lumen.Charts.AspNetCore/ChartEndpoints.cs`, adding `POST /table/html` (body `TableRequest`) and `POST /table/csv` (body `TableSpec`). Both answer 400 with the title "Invalid table", and CSV is `text/csv`.
- Create: `public sealed record TableRequest(TableSpec Spec, int? Page)`, next to wherever `PlannerRequest` is defined. Follow that record.
- Modify: `tests/verify-api.ps1` and `tests/Lumen.Charts.Tests/Program.cs`.

**Interfaces:**
- Consumes: Task 2's internal `TableHtml.Render(spec, page, sortBy, descending, controls: true)` and Task 1's `TableView` and `TableExport`.
- Produces `<LumenTable Spec="…" ShowExport="false"/>`:
  - **Parameters:** `[Parameter, EditorRequired] TableSpec Spec`, `[Parameter] bool ShowExport`, and `[CascadingParameter] ChartStyle? CascadingStyle`. The style is resolved as `Spec.Style ?? CascadingStyle ?? ChartStyle.Light`, as `<LumenStats>` does.
  - **State:** `sortBy`, `descending` and `page`. They start from the spec (`SortBy`, `SortDescending` and the view's `DefaultPage`), and are reset whenever a new `Spec` instance is set.
  - **Markup:** `<div class='lumen-datatable-host' @ref=root>`, then the fragment as a `MarkupString`, then the export button when `ShowExport` is on (`<button type='button' @onclick=Export>Export CSV</button>`, which downloads `TableExport.Csv` through lumen.js's existing download path; find what `<LumenChart>`'s "Export CSV" uses), then `<span class='lumen-status' role='status'>@status</span>`.
  - **Sorting:** a `[JSInvokable] Sort(int column)`.
    - Pressing a column that is not the current sort column sorts it in its `NaturalDescending` direction; pressing it again reverses the direction.
    - The page returns to 0, or to `DefaultPage` with `StartAtEnd`.
    - The status becomes `Sorted by {Header}, {words}`. The words are `highest first` or `lowest first` for `Better.Higher`, `fastest first` or `slowest first` for a `Duration` with `Better.Lower`, `best first` or `worst first` for a `Better.Lower` number, and `A to Z` or `Z to A` for text. Dates are `oldest first` or `newest first`.
  - **Paging:** a `[JSInvokable] Page(int page)`. The status becomes `Rows {first}–{last} of {total}`.
  - **After a re-render:** call `tableFocus(root, '[data-lumen-sort="{i}"]')` or `'[data-lumen-page="{n}"]'`, matching the control that was pressed. If that page button is now disabled (Last, on the last page), focus the range span's region instead.
- **`attachTable`:** one delegated `click` listener on the root, which calls `dotnet.invokeMethodAsync('Sort', i)` or `('Page', n)` for clicks inside `[data-lumen-sort]` or `[data-lumen-page]`. Dispose it as `<LumenChart>` disposes its module. Before the circuit starts (prerender), the buttons do nothing.

- [ ] **Step 1: Write the failing tests:**
  - **Unit:**
    - the prerendered `<LumenTable>` markup contains the fragment that `Render(..., controls: true)` writes for the spec's default state;
    - the cascading style applies;
    - a Razor-level sort: the component's `Sort` method, called through reflection or an instance from the renderer, changes `aria-sort` and the order, then reverses it, with the exact status words for a `Duration` with `Lower`;
    - **Review Focus 2:** the component's first render, with `StartAtEnd`, shows `Rows 376–400 of 400`.
  - **verify-api:**
    - `/table/html` returns 200 and contains `lumen-datatable`, and its `page` parameter works;
    - `/table/csv` returns 200 `text/csv` with the header line;
    - 33 columns gives 400 "Invalid table";
    - a `javascript:` href gives 400.
- [ ] **Step 2:** Run the tests, and check that they fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4:** Run the unit tests and verify-api. Expected: all pass.
- [ ] **Step 5: Commit.**

### Task 4: Gallery cards and browser checks

**Files:**
- Modify:
  - `samples/Lumen.Gallery/Components/Pages/Components.razor`: two cards after the tiles, "Results" and "Training log", plus the "On this page" links;
  - `samples/Lumen.Gallery/DemoData.cs`: `ResultsTable(ChartStyle style)` and `TrainingLog(ChartStyle style)`, with invented data;
  - the nav count for "Beyond charts" (2 → 4) on every page;
  - `tests/verify-api.ps1`: `/components` contains `Results` and `Training log`;
  - `tests/Lumen.Charts.BrowserTests/Program.cs`.

**Content (invented):**
- **"Results":** 40 riders, with columns:
  - Place (Number);
  - Name (Text, `RowHeader`, with an `Href` of `#rider-{n}` on 5 riders);
  - Club (Text);
  - Time (Number, `Duration`, `Better.Lower`);
  - Change (Change, `Better.Lower`, its previous race's place);
  - Points (Bar);
  - Season (Sparkline, 8 rounds).

  It is sorted by Place, with `ShowExport` on.
- **"Training log":** 400 days ending 28 Oct 2026, with columns Date (Date, `RowHeader`), Session (Text), Distance (Number, unit `km`), Time (Duration) and Load (Bar). It has `PageSize` 25 and `StartAtEnd`, and a footer of totals.

- [ ] **Step 1: Write the failing browser tests**, in the suite's style. Each prints a SKIP line where the host has no "Beyond charts" link:
  - **axe** on the page in Light, Dark and Midnight, with both tables shown.
  - **Sorting by click:** clicking the Time header sorts ascending (`aria-sort='ascending'`, the first row is the fastest), with the status `Sorted by Time, fastest first`, and focus stays on that header button. A second click sorts descending.
  - **Paging by keyboard** (Review Focus 4): Tab to "Previous" on the log and press Enter. The range reads `Rows 351–375 of 400`, the status says the same, and `document.activeElement` is still the Previous button.
  - **`StartAtEnd`:** the log opens on `Rows 376–400 of 400`.
  - **Review Focus 5**, at 360 px with `isMobile` and `hasTouch`:
    - the results region's `scrollWidth` is greater than its `clientWidth`;
    - after `region.scrollLeft = 200`, the first body row's `<th>` still has `getBoundingClientRect().left` within 1 px of the region's left;
    - after scrolling the region vertically, `thead th` stays at the region's top;
    - the page's `scrollWidth` is not more than its `clientWidth`.
  - **Export:** clicking "Export CSV" downloads a file named for the caption, whose first line is the header row. Follow the suite's existing download tests.
- [ ] **Step 2:** Build, run the new tests, and check that they fail.
- [ ] **Step 3: Implement** the cards and the data.
- [ ] **Step 4:** Run the gallery browser suite, verify-api, and the WebAssembly host's suite (lumen.js changed). Expected: all pass, with the new tests printing SKIP on the WebAssembly host.
  - Take screenshots at 1280 (Light and Dark) and at 360 (Light), and save them under `%TEMP%\lumen0490\`.
- [ ] **Step 5: Commit.**

### Task 5: Docs, the skill and the version

**Files:**
- Modify:
  - `Directory.Build.props` to `0.49.0`;
  - `README.md`:
    - a "### Data tables" section under Blazor integration, covering the model, the cell kinds, sorting and paging, sticky headers, CSV, the HTTP endpoints, the JSON enums note (the same as the tiles') and the limits;
    - a "## 0.49.0 additions" section in 0.48.0's style;
    - the Limits.
  - `integrations/claude-code/lumen-charts/references/api.md`: tables for the records and the component's line.
  - `SKILL.md`: one body bullet.
  - `references/recipes-race-face.md`: a "## Results table" recipe that compiles as written, using unique variable names.
  - `docs/VERIFICATION.md` and `tests/Lumen.Charts.Baseline/README.md`.

- [ ] **Step 1:** Make the edits. Every count comes from your own runs.
- [ ] **Step 2:** Run `git diff --check`, and confirm the SKILL.md frontmatter is unchanged.
- [ ] **Step 3:** Run everything:
  - the Release build (0 warnings);
  - the unit tests;
  - both baselines (421 rows each, identical to the references);
  - verify-api;
  - the gallery browser suite;
  - the WebAssembly host's suite;
  - the recipes.

  Report each count.
- [ ] **Step 4: Commit.**
