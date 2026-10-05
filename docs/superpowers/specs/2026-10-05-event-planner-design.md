# Event planner — design

Date: 5 October 2026. Status: approved in conversation, awaiting review of this written spec.

## Purpose

Race organizers need to choose dates for their events without clashing with other organizers' events. They plan a year ahead and want to see, at a glance, which weekends are free in their part of the country, which days are public or school holidays, and which other events compete for the same riders.

Lumen gets a general, data-agnostic **planner**: a year that zooms to a month and to a day, with weekends, holidays and other organizers' events, filtered by region and category. Race Face (the first consumer) supplies the data and decides how strongly each event clashes with the viewing organizer's plans. Nothing in Lumen is specific to racing.

### What the owner asked for

- A year view, broken into months, with stripes showing what is scheduled in each month.
- Zooming into a month shows its days and a detailed view of the events on each day; several events on one day are shown underneath each other.
- Holidays shown, weekends highlighted, other organizers' events shown.
- Country and region (province) taken into account, since events far away do not affect an organizer's choice.

### Decisions taken in the design conversation

| Question | Decision |
|---|---|
| Scope | Design the whole feature; build Lumen's general planner first, then Race Face's side as a second sub-project with its own spec. |
| Clash logic | Race Face decides each event's relevance (`Clash`, `Near`, `Other`) for the viewing organizer; Lumen only draws it. |
| Devices | Desktop first, phone usable; every zoom level also renders as static SVG. |
| Interaction | View only: Lumen never creates, moves or saves events; it raises "event selected" and "day selected". |
| Approach | A new `PlannerSpec` with its own SVG renderer and a `<LumenPlanner>` component, as `GraphSpec` and `<LumenGraph>` are; no existing chart kind changes. |

### Success criteria

- An organizer can see a whole year and spot weekends that hold no clash or close event in their region, without zooming.
- Zooming into a month shows every event on every day, stacked, with its region, discipline, audience, status and relevance in words.
- Every state shown by colour is also shown by a pattern or weight and said in words, in the tooltip and the accessible name.
- Every drawn word clears 4.5:1 against what is behind it; every meaningful non-text mark (event stripe, holiday symbol, pattern) clears 3:1.
- The year, month and day views render as static SVG on a server, byte-stable for the same spec.
- No existing Lumen rendering moves.

## 1. Data: `PlannerSpec`

Public records in `src/Lumen.Charts/Planner.cs`, each with XML documentation.

### Period and week

- `From`, `To` (`DateOnly`): the period shown. A `Year` convenience (e.g. `PlannerSpec.ForYear(2027)`) sets 1 January to 31 December. A season that crosses a year end (September to August) is allowed. Refused: `To` before `From`; a period longer than 400 days.
- `WeekStart` (`DayOfWeek`, default Monday).
- `Weekend` (set of `DayOfWeek`, default Saturday and Sunday); some countries differ.
- `Title`, `Description` (required title, as charts), `Style` (a `ChartStyle`), `Width`, `DrawTitles` and `PaintBackground` as charts have them.

### Regions

- `Regions`: `IReadOnlyList<PlannerRegion>`, `PlannerRegion(string Code, string Name, string? Parent)`, e.g. `ZA` "South Africa"; `ZA-GP` "Gauteng", parent `ZA`. Any depth (country → province → district).
- Refused: duplicate codes, an unknown parent, a parent chain that loops, blank names.
- A region "includes" itself and every region below it. A period or event set for `ZA` applies to, and is shown under, every province of `ZA`.

### Special days

- `Periods`: `IReadOnlyList<PlannerPeriod>`, `PlannerPeriod(DateOnly From, DateOnly? To, string Name, PeriodKind Kind, string? Region)`.
- `PeriodKind`: `PublicHoliday`, `SchoolHoliday`, `Other` (exam weeks, a large external event that draws the same people).
- No region means everywhere; a region code applies to that region and every region below it.
- **Long weekends** are derived by Lumen: a run of days made of weekend days and public holidays, at least three long, that contains at least one public holiday. They are not passed in.
- Refused: `To` before `From`, an unknown region, blank names.

### Events

- `Events`: `IReadOnlyList<PlannerEvent>`, `PlannerEvent` with:
  - `Id` (string, unique), `Name`, `Start` (`DateOnly`), `End` (`DateOnly?`, for multi-day events), `Region` (code, optional: an event with no region is shown under every region);
  - `Category` (free text, e.g. a discipline), `Audience` (free text, e.g. an age group or level) — free text so the library stays general;
  - `Status`: `Confirmed`, `Provisional`, `Cancelled`;
  - `Relevance`: `Clash`, `Near`, `Other` — decided by the host for the person viewing;
  - `Mine` (bool): the viewer's own event;
  - `Note` (optional, short), `Url` (optional link for the host to open).
- Refused: duplicate ids, `End` before `Start`, an unknown region, blank names, an event wholly outside the period (refused, so the host notices). An event partly inside the period is drawn for the days inside it and named with its full dates.

### Selection

- `PlannerView`: `Year`, `Month(int year, int month)`, `Day(DateOnly)`, the level rendered.
- `PlannerFilter` (optional on the spec, used by the static renderer and set by the component): regions (codes; a region includes its descendants), categories, audiences, statuses, relevances. Empty means everything. Periods are filtered by region only.

## 2. Views

### Year view (desktop, the starting level)

- One row per month, aligned **by weekday**: each month's first day stands under its weekday column, so the grid is 37 columns (`WeekStart` first) and every weekend day lines up in vertical bands down the year — free weekends read at a glance.
- Weekends: a light band (`Style.Grid`), named in each day's accessible text ("Saturday").
- Public holidays: a symbol on the day (a small filled diamond, ≥ 3:1), named.
- Long weekends: a bracket under the run of days.
- School holidays: a thin band along the top of their days, with a distinct pattern from other periods.
- Events: short stripes under their day (multi-day events as one stripe across their days), stacked up to three per day; beyond that a "+N" mark that names the rest.
- Relevance by weight and pattern, not colour alone: `Clash` solid and bold, `Near` medium and dashed, `Other` thin and muted. `Provisional` hatched; `Cancelled` struck through; `Mine` outlined.
- Busy weeks: a small count under each week of the clash and close events it holds, said in words in the week's name.
- Month names on the left; the weekday initials along the top.

### Month view

- A month grid: 7 columns from `WeekStart`, 5 or 6 weeks.
- Each day: the date, its holiday names at the top, then its events stacked underneath each other, each a line with name, region and a relevance word ("clash", "close"); a "+N more" line when they do not fit, naming the rest.
- School holidays and other periods as bands across the weeks they cover; multi-day events as one bar across their days.
- Readable as a table: rows are weeks, columns weekdays, cells the day's holidays and events.

### Day view

- A list of every event that day with region (full name), category, audience, status, relevance, note; the day's periods at the top.

### Phone (narrow) layouts

- Year: twelve compact month bars, each with weekend dots and the busy-week counts.
- Month: an agenda list of only the days that have events or periods.
- The component chooses the narrow layout when its box is narrower than 640 px; the static renderer takes the layout as an option (`PlannerLayout.Wide` or `Narrow`).

### Words and accessibility

- Every event is a focusable mark named in full: "Hilltop XCO, Saturday 13 March, Gauteng, XCO, kids, provisional, clash". Its tooltip says the same.
- Every day says its weekday, date, holidays, period names and event count.
- A legend in words explains every pattern, symbol and weight.
- Text contrast 4.5:1, non-text marks 3:1, checked in tests for Light, Dark and Midnight.

## 3. Build

### Library (`Lumen.Charts`)

- `Planner.cs`: the records and enums above.
- `PlannerSvg.cs`: `PlannerSvg.Render(PlannerSpec spec, PlannerView view, PlannerLayout layout = Wide)` → SVG string; `PlannerSvg.Table(spec, month)` → the month as an HTML table for static pages.
- Validation alongside `ChartValidation`, with a reason for every refusal.
- Rendering is deterministic: same spec, same bytes.

### Component (`Lumen.Charts.Blazor`)

- `<LumenPlanner Spec="…" />` with:
  - zoom: click a month for its month view, a day for its day view; Escape or a Back button zooms out; previous and next buttons step months or days;
  - keys: arrows move between days (month view) or months (year view), Enter zooms in, Escape out;
  - filters: chips for region (as a tree, country then province), category, audience, status, relevance; filtering re-renders;
  - `FitWidth` and the narrow layout on phones;
  - `EventSelected` and `DaySelected` callbacks;
  - status line read by screen readers.
- Interactivity never changes the static SVG of a view.

### HTTP (`Lumen.Charts.AspNetCore`)

- `POST /api/planner/svg` with a spec and a view, beside the chart and graph endpoints; JSON enums as strings, as elsewhere.

### Tests

- Unit: weekday alignment of every month; weekend bands; long-weekend derivation (a Monday holiday, a Friday holiday, a holiday on a weekend day); region inheritance; filters; stacking and "+N"; multi-day spans across weeks and months; status and relevance words; every refusal; contrast of every drawn word and mark in Light, Dark and Midnight; byte-stability.
- Baseline: new hashed rows for the year, a month and a day, in Light, Dark and Midnight, wide and narrow; no existing row moves.
- HTTP: the endpoint renders and refuses as the library does.
- Browser (gallery and WebAssembly host): zoom in and out, keys, filters, callbacks, a 375 px phone, axe in Light, Dark and Midnight.
- Gallery demo and a skill recipe with invented organizers and events; real South African province names and public-holiday dates are public facts and may be used.

### Releases

1. **0.43.0** — the planner drawn statically: data, year/month/day SVG views, validation, the HTTP endpoint, the month table, gallery demo and recipe.
2. **0.44.0** — the interactive `<LumenPlanner>`: zoom, keys, filters, narrow layout, callbacks.
3. **0.45.0** — the category heatmap (Race Face #16), moved back one release.

Each release follows the handover's ritual: built by a delegated agent, verified independently, released with honest notes and screenshots.

## 4. Race Face sub-project (separate spec, after the Lumen releases)

Outline only; its own design and spec come later:

- An events store with region codes and the fields above.
- Holidays: South African public holidays computed from the Public Holidays Act (fixed dates, Easter-based days, a holiday on a Sunday moving to the Monday), and school terms entered or imported each year.
- Clash rules that set `Relevance` for the viewing organizer from region and distance, category, audience and event size.
- Who may see provisional dates.
- The planner page in Race Face Web, using `<LumenPlanner>`.

## Out of scope (first version)

- Creating, moving or saving events in the planner (view only).
- Lumen computing clash levels.
- Map views, travel-time calculation, notifications, calendar export (iCal) — possible later, in Race Face.
