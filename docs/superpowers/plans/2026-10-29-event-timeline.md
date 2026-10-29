# Event timeline (0.50.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `<LumenEvents>`, a vertical list of dated events. It can be grouped by day, month or year, and can carry a "Now" marker and status words. It shows a window of events with buttons to show earlier and later ones. It is the last of the three components beyond charts, and it is built on the shared HTML foundation.

**Architecture:**
- **Core (`Lumen.Charts`):** the public records `TimelineEvent`, `EventTimelineSpec` and `EventTimelineRequest` with a validator, an internal `EventView` that orders the events, places Now and cuts the window, and `EventTimelineHtml.Render`.
- **Blazor:** `<LumenEvents>` keeps its window as state. A `lumen.js` delegate turns clicks on the core's "Show earlier" and "Show later" buttons into .NET calls, as `<LumenTable>` does.
- **AspNetCore:** one endpoint.
- **Gallery:** one card.

**Tech Stack:** C# / .NET 8 (libraries), .NET 10 (gallery, tests), Blazor Server and WebAssembly, an ES-module script, ASP.NET Core minimal APIs, Playwright for .NET with axe-core.

**Spec:** `docs/superpowers/specs/2026-10-28-beyond-charts-design.md`, §4 (the shared foundation), §7 (the event timeline) and §8 (constraints), approved by the owner on 28 Oct 2026. The spec is the binding authority. Where this plan pins something §7 leaves open, the plan says so.

## Global Constraints

- **Dependencies:** no third-party code, framework or CDN. Everything lives in the existing three packages.
- **CSS scoping:** every rule sits under `.lumen-events.lumen-events-{regular|compact}`, except the hiding rule, which is `LumenHtml.HiddenRule(".lumen-events")`. Reset the root's and the lists' margin, padding and `list-style`. Write no global CSS and no `id` attributes.
- **Baseline:** the 421 baseline rows stay byte-identical in both finishes. Rows may only be added.
- **Colour and contrast:** every text colour clears 4.5:1 through `LumenHtml.Readable`. Colour never carries meaning alone: every status has a word, the rail and dots are decoration (`aria-hidden`), and the Now marker is words.
- **Escaping and links:** all text goes through `LumenHtml.E`. Links must pass `LumenHtml.SafeLink` and are written encoded.
- **Dates and times:** format with `CultureInfo.InvariantCulture`. Convert times into `TimeZone` with the existing `TimeAxis.Zone(id)`, the zone `ChartSpec.TimeZone` uses; null means UTC.
- **Docs and validation:** every new public member has an XML doc (a Razor component's class doc goes in a `.razor.cs` partial), and the Release build shows 0 warnings. Validation throws `ArgumentException` in `ChartValidation`'s voice.
- **Data:** invented data only.
- **Process:** the processes, git, leftovers and report contract are those of `how-to-run.md` in the plan's SDD workspace (as for 0.49.0). Never open `lumen-charts-race-face-brief.md`. The SKILL.md frontmatter stays byte-identical.

## Review Focus

1. **Equal times keep the spec's order**, in both directions (`NewestFirst`). Test: Task 1.
2. **Now before every event, after every event, and exactly at an event's time.** The marker goes first, last, or after that event (an event at Now counts as reached). With grouping, the marker sits in Now's group, which is created when no event falls in it. Test: Task 1.
3. **A window at either end.** "Show earlier" at the first event cannot move. The button stays focusable (`aria-disabled`), reads "No earlier events", and focus stays on it. Test: Tasks 2 and 4.
4. **Time zones across midnight.** An event at 23:30 UTC on 31 Oct, in `Africa/Johannesburg`, groups under 1 Nov and November. Test: Task 1.
5. **An all-day span crossing a month or a year** is written in full at both ends ("Fri 31 Oct – Sat 1 Nov 2026", "Wed 31 Dec 2026 – Thu 1 Jan 2027"). Test: Task 2.

---

### Task 1: The model, validation, ordering, Now and the window

**Files:**
- Create: `src/Lumen.Charts/EventTimeline.cs`. It holds `EventStatus`, `EventGrouping`, `TimelineEvent`, `EventTimelineSpec` and `EventTimelineRequest`.
- Create: `src/Lumen.Charts/EventTimelineValidation.cs`.
- Create: `src/Lumen.Charts/EventView.cs`.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, in a `// 0.50.0` block placed before `string ChartMarkup(`.

**Interfaces:**
- Produces:
  - `public enum EventStatus { None, Done, Current, Upcoming, Cancelled }`.
  - `public enum EventGrouping { None, Day, Month, Year }`. This name keeps it apart from LINQ's `GroupBy`; the spec property is still `GroupBy`.
  - `public sealed record TimelineEvent(DateTimeOffset When, string Title)`, with init properties:
    - `DateTimeOffset? End`, `bool AllDay`;
    - `string? Note`, `string? Value`;
    - `EventStatus Status`, `string? Category`, `string? Href`.
  - `public sealed record EventTimelineSpec(IReadOnlyList<TimelineEvent> Events)`, with:
    - `string? Title`, `string? Description`, `int TitleLevel = 2`;
    - `bool NewestFirst`, `EventGrouping GroupBy`;
    - `DateTimeOffset? Now`, `string? TimeZone`;
    - `string? DateFormat` (default `"ddd d MMM yyyy"`), `string? TimeFormat` (default `"HH:mm"`);
    - `Density Density`, `ChartStyle? Style`.
  - `public sealed record EventTimelineRequest(EventTimelineSpec Spec, int? Shown = null)`.
  - `internal sealed class EventView`, built by `EventView.Of(EventTimelineSpec spec)` (which validates first), with:
    - `IReadOnlyList<int> Order`: event indexes in reading order;
    - `int? NowAt`: the reading position the marker stands before, `Order.Count` meaning last, null without `Now`;
    - `(int First, int Last) Window(int shown)`: the default window of `shown` reading positions;
    - `(int First, int Last) Grow((int First, int Last) window, bool earlier, int by)`;
    - `string GroupKey(int index)` and `string GroupLabel(int index)`;
    - `string NowGroupKey` and `string NowGroupLabel`.

**Validation (exact limits):**
- `Events`: at most 2,000.
- `Title`: 1–120 characters and not blank.
- `Note`: at most 200. `Value`: at most 40. `Category`: at most 24.
- `Href`: must pass `SafeLink`.
- `End`: at or after `When`.
- Enums: defined values only.
- Spec `Title`: at most 120. `Description`: at most 300.
- `TitleLevel`: 2–5. Group headings take `TitleLevel + 1`.
- `TimeZone`: must be known to `TimeAxis.Zone`.
- `DateFormat` and `TimeFormat`: at most 40 characters each, and each must format a sample without throwing.
- `Style`: through `ChartValidation.Style`.
- Null events are refused.
- `EventTimelineRequest.Shown`: when set, at least 1.

**Ordering, Now and the window (exact):**
- **Order:** chronological by `When`, stable on ties. `NewestFirst` reverses the chronology but keeps ties in spec order (Review Focus 1).
- **Now (`NowAt`):** the marker stands after every event whose `When` is at or before `Now`. Chronologically that is before the first event later than Now; in `NewestFirst` reading order it is placed accordingly (Review Focus 2).
- **Groups:**
  - Group keys and labels come from the time converted into `TimeZone`:
    - Day: `yyyy-MM-dd`, labelled `ddd d MMM yyyy`;
    - Month: `yyyy-MM`, labelled `MMMM yyyy`;
    - Year: `yyyy`, labelled `yyyy`.
  - `NowGroupKey` and `NowGroupLabel` come from Now in the same way.
- **The default `Window(shown)`:**
  - Without `Now`, it is the first `shown` positions.
  - With `Now`, it is the `shown` positions nearest the marker: up to ⌈shown/2⌉ before the marker and ⌊shown/2⌋ after it, topped up from the other side when one side runs short, and clamped to the list.
- **`Grow`** extends the earlier or the later side by `by`, clamped. In reading order, "earlier" means the start of the list. Let the renderer word it, since with `NewestFirst` that end is newer.

- [ ] **Step 1: Write the failing unit tests** (`0.50.0: …`):
  - every limit at its boundary and one past;
  - Review Focus 1, 2 and 4, each with exact positions or keys;
  - `Window` with no Now, with Now in the middle, at the start and at the end (topped up);
  - `Grow` clamping at both ends.
- [ ] **Step 2: Run the tests.** The build fails, which is the RED.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the unit tests.** All pass, with 0 warnings.
- [ ] **Step 5: Commit.**

### Task 2: The renderer

**Files:**
- Create: `src/Lumen.Charts/EventTimelineHtml.cs`.
- Modify: the tests, `tests/Lumen.Charts.Baseline/Program.cs`, and both references (added rows only).

**Interfaces:**
- Consumes: Task 1.
- Produces:
  - `public static string EventTimelineHtml.Render(EventTimelineSpec spec, int? shown = null)`. Null means every event. A number means `EventView.Window(shown)`. This pins §7's `int shown = int.MaxValue`, so that a static page can show the window round Now.
  - `internal static string Render(EventTimelineSpec spec, EventView view, (int First, int Last) window, bool controls)`.

**Markup (exact structure):**
- The root:
  - `<section class='lumen-events lumen-events-{regular|compact}' style='{RootStyle}' aria-label='{Title, else Description, else "Events"}'>`;
  - then `<style>`;
  - then, with a title, `<h{TitleLevel} class='lumen-events-title'>`;
  - then, with a description, `<p class='lumen-events-desc'>`.
- **With `GroupBy`:** `<ol class='lumen-events-groups'>`. Each group with any shown event, plus Now's group when the marker is in the window, is `<li class='lumen-events-group'><h{TitleLevel+1} class='lumen-events-heading'>{label}</h…><ol class='lumen-events-list'>…</ol></li>`.
- **Without `GroupBy`:** a single `<ol class='lumen-events-list'>`.
- **Each event** is `<li class='lumen-event lumen-event-{none|done|current|upcoming|cancelled}'>`, with these children in this order:
  1. `<span class='lumen-event-dot' aria-hidden='true'></span>`;
  2. `<time datetime='{ISO 8601 with offset, or yyyy-MM-dd when AllDay}'>{when text}</time>`;
  3. the title, as `<span class='lumen-event-title'>`, or as an encoded `<a>` with `Href`;
  4. `<span class='lumen-event-value'>`;
  5. the status: `<span class='lumen-event-status'><span aria-hidden='true'>{glyph}</span> {word}</span>`, using ✓ Done, ● Now, ○ Upcoming and ✕ Cancelled. `Current` is coloured `Readable(accent)`. `Cancelled` also strikes the title through. `None` writes no status;
  6. `<span class='lumen-event-category'>`, a chip with a 1 px `--lumen-grid` border;
  7. `<p class='lumen-event-note'>`, muted.
- **The when text:**
  - AllDay: `DateFormat`.
  - Otherwise: `DateFormat · TimeFormat`, converted into `TimeZone`.
  - With `End`, a same-day timed span is `{date} · {t1}–{t2}`. An all-day span in the same month is `ddd d – ddd d MMM yyyy`, in the same year `ddd d MMM – ddd d MMM yyyy`, and otherwise both dates in full (Review Focus 5). A multi-day timed span is both dates in full, each with its time, separated by `–` with spaces.
- **The Now marker:** `<li class='lumen-events-now'><span class='lumen-event-dot' aria-hidden='true'></span>Now · {Now in DateFormat}</li>`, at `NowAt` inside Now's group.
- **With `controls`, a button before the list and one after:**
  - Each is `<button type='button' class='lumen-events-more' data-lumen-more='earlier|later'>`.
  - The label is `Show {k} earlier` or `Show {k} later`, where k is the smaller of the page size and what remains. With `NewestFirst` the labels become `Show {k} newer` and `Show {k} older`.
  - When nothing remains, the button reads `No earlier events` (or `later`, `newer`, `older`) and carries `aria-disabled='true'`. Never use `disabled`.
  - Without `controls`, a static window shows no buttons, only a muted `<p class='lumen-events-range'>Showing {a}–{b} of {n} events</p>`.
- **The rail:** a 2 px line in `--lumen-grid` down the left, behind the dots, written as CSS only.

- [ ] **Step 1: Write the failing tests:**
  - the XML structure, order and reading order of an event's parts;
  - status words and glyphs, and the colours through `Readable`;
  - links;
  - the Now marker inside the right group;
  - the when text for each form, including Review Focus 5;
  - the window and the controls markup, including Review Focus 3's `aria-disabled` label;
  - no `id`, and hostile text escaped.
- [ ] **Step 2: Run the tests.** Expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the unit tests and both baselines.** The 421 existing rows must be identical.
- [ ] **Step 5: Add baseline rows** after `table/negative-bars`:
  - `events/season-light`: 16 invented events grouped by month, with Now;
  - `events/season-dark`;
  - `events/grouped-day-newest`;
  - `events/window-now`: `shown` 6;
  - `events/spans-allday`.

  That makes 426 rows in both finishes. Update the references with the added rows only.
- [ ] **Step 6: Commit.**

### Task 3: `<LumenEvents>`, its script and the endpoint

**Files:**
- Create: `src/Lumen.Charts.Blazor/LumenEvents.razor` and `LumenEvents.razor.cs`.
- Modify: `src/Lumen.Charts.Blazor/wwwroot/lumen.js`, adding `export function attachEvents(root, dotnet)`. Reuse `tableFocus`, or generalise it as `focusIn(root, selector)` while keeping `tableFocus` working.
- Modify: `src/Lumen.Charts.AspNetCore/ChartEndpoints.cs`, adding `POST /timeline/html` (body `EventTimelineRequest`), which answers 400 "Invalid timeline".
- Modify: `tests/verify-api.ps1` and the unit tests.

**Interfaces:**
- Consumes: Task 2's internal `Render(spec, view, window, controls: true)`.
- Produces:
  - `<LumenEvents Spec="…" PageSize="null"/>`, with `[Parameter] int? PageSize` (1–200) and the cascading style as the other components have.
  - The component builds one `EventView` for each new `Spec` instance. With `PageSize` its window starts at `Window(PageSize)`; without, it shows everything and writes no buttons.
  - `[JSInvokable] More(string side)` accepts only `earlier` or `later`, grows the window by `PageSize`, and sets the status to `Showing {a}–{b} of {n} events`.
  - After a re-render, focus returns to `[data-lumen-more='{side}']`, which always exists (Review Focus 3).
  - The disposal-race guard and the module lifecycle follow `<LumenTable>`.

- [ ] **Step 1: Write the failing tests:**
  - **Unit tests:**
    - prerendered markup equals the internal `Render` for the default window;
    - the cascading style applies;
    - `More("earlier")` grows the window, and an unknown side is ignored;
    - the view is reused across `More` and same-`Spec` re-renders.
  - **verify-api:**
    - `/timeline/html` answers 200 with `lumen-events`, and honours `Shown`;
    - more than 2,000 events gives 400;
    - a `javascript:` href gives 400;
    - `{"spec":null}` gives 400.
- [ ] **Step 2: Run the tests.** Expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the unit tests and verify-api.** All pass.
- [ ] **Step 5: Commit.**

### Task 4: The gallery card and browser checks

**Files:**
- Modify:
  - `Components.razor`: a "Season" card after the tables, with its "On this page" link;
  - `DemoData.cs`: `RiderSeason(ChartStyle style)`, invented;
  - the "Beyond charts" nav count on every page, 4 → 5;
  - `tests/verify-api.ps1`: `/components` contains `Season`;
  - `tests/Lumen.Charts.BrowserTests/Program.cs`.

**Content (invented):** one rider's 2026 season of 16 events, grouped by month, with `Now` = 29 Oct 2026 12:00 in `Africa/Johannesburg`:
- past races `Done`, with values such as `3rd · 1:12:05`;
- the next race `Current`;
- the later races `Upcoming`;
- one race `Cancelled`, with the note "Called off for weather";
- one two-day all-day stage race;
- categories `XCO`, `Marathon` and `Road`;
- `Href`s on three of the races;
- `PageSize` 6.

- [ ] **Step 1: Write the failing browser tests.** Each prints SKIP where there is no "Beyond charts" link:
  - axe in Light, Dark and Midnight;
  - Tab to "Show … earlier", press Enter: more events appear, the status updates, and focus stays on the button;
  - repeat until it reads "No earlier events", is `aria-disabled`, and keeps focus (Review Focus 3);
  - the Now marker's text is visible and sits between the last done race and the current race;
  - at 360 px with `isMobile`: no page-wide sideways scroll;
  - the prerendered card equals the interactive card.
- [ ] **Step 2: Build and run the tests.** Expect the new ones to fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the full suites:** the gallery suite, verify-api, and the WebAssembly suite (`lumen.js` changed). Take screenshots at 1280 (Light and Dark) and 360, saved under `%TEMP%\lumen0500\`.
- [ ] **Step 5: Commit.**

### Task 5: Docs, the skill and the version

**Files:**
- Modify:
  - `Directory.Build.props`: 0.50.0;
  - README: a "### Event timelines" section, a "## 0.50.0 additions" section and the limits;
  - `api.md`;
  - SKILL.md: a body bullet only;
  - `recipes-race-face.md`: a "## Race history" recipe that compiles as written, with unique variable names;
  - VERIFICATION and the Baseline README.

- [ ] **Step 1: Make the edits.** Every count must come from your own runs.
- [ ] **Step 2: Check the diff.** Run `git diff --check` and confirm the SKILL frontmatter is unchanged.
- [ ] **Step 3: Run everything:**
  - the Release build (0 warnings);
  - the unit tests;
  - both baselines (426 rows each, identical);
  - verify-api;
  - the gallery suite;
  - the WebAssembly suite;
  - the recipes.

  Report each count.
- [ ] **Step 4: Commit.**
