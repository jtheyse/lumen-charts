# Places and points chart — design (0.45.0)

Date: 6 October 2026. Status: approved in conversation (approach, section 1, section 2); awaiting the owner's review of this written spec.

## Purpose

Race Face draws a "Position & points by race" chart on the rider page and the season dashboards (web and phone) from one hand-built `ChartSpec` (`RaceSense.Components/PositionPointsChart.razor`, behaviour pinned by `RaceSense.Web.Tests/PositionPointsChartTests.cs`). The owner asked for it to become a reusable Lumen feature, so Race Face — and any app that shows finishing places over a sequence of events — calls one builder instead of hand-building the spec.

Lumen gets a general, data-agnostic builder: a list of results (a place, optionally a field size, points, a date, a label and a series) becomes the two-pane chart Lumen's skill already recommends (recipe "Position and points by race, recommended: two panes", 0.33.0), plus a thin Blazor component. Nothing in it is specific to racing or to Race Face; Race Face passes its own words and colours as options.

### Decisions taken in the design conversation

| Question | Decision |
|---|---|
| Roadmap | 0.45.0 (owner, 6 Oct 2026); the category heatmap (Race Face #16) moves to 0.46.0. |
| Approach | A static builder returning an ordinary `ChartSpec` plus a thin component (not a new chart kind, not a recipe only). No existing rendering changes. |
| Input shape | The builder orders the races and writes their X labels (owner's choice), from optional dates and labels. |
| HTTP | No endpoint in 0.45.0; it can be added later without changes. |

### Success criteria

- Race Face can replace its `Spec(...)` body with one call to `PlacingsChart.Build` and its razor markup with `<LumenPlacings>`, and its seven chart tests still hold (apart from the mixed-dates order noted under rule 2).
- Every state shown by colour is also said in words (the change against the previous race of the same series reaches each place's name and tooltip, as Lumen's change colours already do).
- Every drawn word clears 4.5:1 and every meaningful mark 3:1, as Lumen's renderer already ensures for value labels and lines.
- No existing rendering moves: the baseline only gains rows.

## 1. The builder (`Lumen.Charts`)

New file `src/Lumen.Charts/Placings.cs`.

### Input

```csharp
public sealed record Placing(int? Place)
{
    public int? Field { get; init; }      // field size, written after the place as "/48"
    public double? Points { get; init; }  // null = no points (a gap, never 0)
    public DateOnly? Date { get; init; }  // orders the races and labels them
    public string? Label { get; init; }   // the X label; wins over the date
    public string? Series { get; init; }  // a place compares only within its series
}
```

### Options

```csharp
public sealed record PlacingsOptions
{
    public string PlaceName { get; init; } = "Place";          // Race Face: "Pos/field"
    public string PointsName { get; init; } = "Points";        // Race Face: "Pts"
    public string DateFormat { get; init; } = "d MMM yyyy";    // Race Face: "dd-MM-yyyy"; invariant culture
    public string Unlabelled { get; init; } = "#{0}";          // Race Face: "R{0}"; {0} is the race's 1-based number
    public IReadOnlyList<string>? PlaceColors { get; init; }   // null: the style's palette, in order
    public string? PointsColor { get; init; }                  // null: the palette colour after the place lines
    public ChartStyle? Style { get; init; }                    // null: the Light preset
}
```

The title, description, Y label, width and height are not options: they are set on the returned spec, and a host changes them with `spec with { … }`.

### `PlacingsChart.Build(IEnumerable<Placing> results, PlacingsOptions? options = null)` → `ChartSpec?`

1. **Unplaced races are dropped.** A result whose `Place` is null or 0 or less is dropped, with its points. If none is left, `Build` returns `null` (the page shows its own empty state).
2. **Order.** If every remaining result has a `Date`, the races are sorted by date, stably. Otherwise they keep the order given. (Race Face puts undated races first when dates are mixed; this is the one behaviour that differs.)
3. **X labels.** Each race is labelled by its `Label`, else its `Date` in `DateFormat` (invariant culture), else `string.Format(Unlabelled, n)` with its 1-based number in the drawn order. Races stand at X = 0, 1, 2 … with `XMin = -0.5` and `XMax = n - 0.5`.
4. **One line per series.** A blank or null `Series` is its own group. Groups are taken in the order they first occur (after ordering). With one group the line is named `PlaceName`; with several, each is named `"{PlaceName} · {Series}"`, and the blank group `PlaceName` alone. A line holds points only at its own races (at their X), so it is drawn joined across the other series' races, and better or worse is judged only against the previous race of the same series. (Amended during the build: null points at the other series' races would have broken each line into isolated markers.) Each line: `ChangeColors = LowerIsBetter`, `ValueLabels = true`, `Markers = Filled`, and on each point `ValueNote = "/{Field}"` when `Field > 0`. Colours: line `k` (0-based) takes `PlaceColors[k % count]`, or by default `Style.Series[k % Style.Series.Count]`. (Amended during the build: the first line no longer defaults to the style's text colour, which vanishes from an HTML legend when a dark style stands on a light page; a host that wants a "hi" line passes `PlaceColors`.)
5. **Points.** One line named `PointsName` in pane 1, with a point only at each race that has points (a race without points has no mark and never a zero, and the line is drawn joined across it), `ValueLabels = true`, `Markers = Filled`, colour `PointsColor` or by default `Style.Series[lines % Style.Series.Count]`, the palette colour after the place lines. (Amended during the build: null points broke the line into isolated dots wherever a series scores no points.) The pane is labelled `PointsName`. If no race has points, there is no points line and no pane.
6. **The spec.** `Kind = Line`, `Width = 340`, `Height = 380` with points and `260` without, `YReversed = true`, `YLabel = "Place"`, `Title = "Places and points"`, `Style` = the options' style, and `Description` = `"Finishing place out of the field, first at the top, and points. Best: {best}."` (`"Finishing place out of the field, first at the top. Best: {best}."` without points), `best` being the smallest place. The X point labels are the races' labels.
7. **Bad input.** A null `results` throws `ArgumentNullException`; null `options` means the defaults. A blank `PlaceName`, `PointsName` or `DateFormat`, an `Unlabelled` without `{0}`, or an empty `PlaceColors` throws `ArgumentException` saying why. Values Lumen cannot draw (non-finite points) are refused by the existing chart validation when the spec is rendered.

## 2. The component (`Lumen.Charts.Blazor`)

New file `src/Lumen.Charts.Blazor/LumenPlacings.razor`.

```razor
<LumenPlacings Results="results" Options="raceFace" Adjust="s => s with { Title = "Position & points by race", YLabel = "Position" }">
    <Empty><RfEmptyState … /></Empty>
</LumenPlacings>
```

- `Results` (`IEnumerable<Placing>`, required) and `Options` (`PlacingsOptions?`) go to the builder.
- `Adjust` (`Func<ChartSpec, ChartSpec>?`): the host's last word on the spec (title, axis label, a raised-card style).
- `Static` (`bool`): true writes `ChartSvg.Render(spec)` as plain markup, no script (a static page); false renders `<LumenChart Spec FitWidth="true" ShowToolbar="false">`.
- `Empty` (`RenderFragment?`): shown when the builder returns null; nothing is drawn without it.
- A cascaded `ChartStyle` (from `<LumenBrand>` or a `CascadingValue`) is used when `Options.Style` is null.

## 3. Tests, gallery, docs

- **Unit (builder):** one test per rule above, with invented data: change words in the places' names ("better than the previous", "worse than the previous"), "24/48", a place without a field written alone, a race without points as a gap not 0, two series compared only with themselves, no pane without points, `null` when nothing is placed, date order versus given order, label precedence, default and custom colours, every refusal.
- **Unit (component):** prerender draws the chart; the `Empty` slot when nothing is placed; `Static` writes SVG with no component markup; a cascaded style is used.
- **Baseline:** three added rows (with points, without points, two series); no existing row moves.
- **Gallery:** a "Places and points" card on the Sports & performance page with invented races; a browser check that it renders with its change words; the existing axe sweeps (light, dark, Midnight) cover it. **HTTP:** one check that the page prerenders the card.
- **Docs:** README and `references/api.md` describe the builder and the component; the skill's Race Face recipe "Position and points by race, recommended: two panes" becomes the one-call version, with the hand-built spec kept as the explanation; `tests/Lumen.Charts.Recipes` compiles it. `docs/VERIFICATION.md`, version 0.45.0.

## 4. Release

The handover's ritual: built by delegated agents, verified independently on master, packed, pushed, CI, tagged, GitHub release with the three `.nupkg`, the skill packaged; then a message to "RACEFACE RUNNING EXPANSION 2" with the version and what to replace in `PositionPointsChart.razor`.

## Out of scope

- An HTTP endpoint for the builder.
- Any change to existing chart rendering.
- Race Face's own adoption (its session does that after the release).
- The category heatmap (0.46.0).
