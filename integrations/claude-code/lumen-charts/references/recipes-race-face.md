# Race results recipes

Recipes for a race-results app's charts: a rider's season of finishing places and points, sparklines of finish times getting faster and of a growth log, how a race's whole field finished, a season's arc through its fields, a race's gaps to the leader, a ride's effort zones as a strip of shares, a race's scores on meter bars, a ride's best efforts, a race's heart rate lap by lap, a team rider's season with a missed round written in, a category-by-season heatmap table with a cell for too few starts to rate, and an organizers' season planner, on a dark brand style built from design tokens, at a phone card's width. Every example uses invented data. Each is a plain `ChartSpec`; render it with `ChartSvg.Render` for a static page, an API or an image, or put it in `<LumenChart Spec="…" FitWidth="true" />` on an interactive page. They compile against Lumen.Charts 0.46.0, together with the recipes in `sports.md`.

```csharp
using System.Globalization;
using Lumen.Charts;
```

## What every chart here meets

- **Static output is complete.** `ChartSvg.Render` writes every label, colour, tooltip (`<title>`) and accessible name into the SVG; nothing needs JavaScript, and nothing is fetched from a CDN.
- **Phone width.** For SVG rendered on the server set `Width` to the width the card shows it at, `Width = 340` here: drawn at the width it is shown, every label is 11 px or larger (axis ticks 12, value labels and notes 11, the title 17), and none needs the 8 to 9 px text a stretched 340-unit viewBox gives. A card a little narrower, 320 px, still shows 10.4 px. On an interactive page `FitWidth="true"` redraws the chart at its card's width instead. A chart is at least 240 units tall (a sparkline at least 60 wide and 16 tall), and keeps its `Height` at any width, so choose a height that reads at a phone's width; a strip (0.39.0) is drawn as tall as its bar and key instead, and a bar chart with `FitHeight = true` (0.41.0) as tall as its rows.
- **Text that fits.** From 0.35.0 a description or a source too wide for the card, by the library's generous estimate of its width, goes on over a second line, between its ` · ` clauses where both lines then fit and otherwise as evenly as its words allow, and the plot gives up 14 units for it; past two lines the second ends in `…`. A title stays one line, cut at a word with `…`. The whole of each stays in the drawing's `<title>`, `<desc>` and accessible name, so a long description written for a desktop no longer runs off a 340-pixel card.
- **Accessible.** `Title` and `Description` are the drawing's accessible name; each mark is focusable and named, `Position: 16-05-2026, 24/48, better than the previous`, and the same words are its tooltip.
- **Missing data is a gap, never a zero.** A race without a position or without points is a `null` Y: its line breaks there and no mark is drawn, unless a `GapLabel` (0.42.0) writes a word such as `absent` there, which also makes it a named mark. A race without a field size writes its place alone.
- **Never colour alone.** A place coloured by its change also says the change in words, in its tooltip and its accessible name.
- **Keys.** From 0.36.0 each `<LumenChart>` has one roving point among its marks; from 0.42.0 that is its only tab stop while its drawing fits its box, and a drawing wider than its box adds the scrollable viewport (a `role=region`) before it, so the chart has two. From the roving point the arrow keys move — Left and Right along a series, skipping its gaps (a gap with a gap label is a mark they stop on), Up and Down to the series before or after it, Home and End to its ends, Page Up and Page Down ten points — with the keys named in the chart's description. The static SVG keeps every mark focusable on its own. Test the keys with real input, such as Playwright's `keyboard.press`: a `KeyboardEvent` dispatched from script can move focus without bringing up the shared readout or its status line.

## A dark brand style from design tokens

| Token | Value | `ChartStyle` member |
|---|---|---|
| card (s1) | `#161618` | `Background` — the chart is drawn on the card; the page round it is `#0A0A0A` |
| hi | `#F5F6F7` | `Text` |
| low | `#80858E` | `Muted`: axis ticks, captions, value notes (`mid` `#B7BCC4` reads stronger) |
| line | `rgba(255,255,255,.10)` | `Grid = "#2D2D2F"`, the line over the card, since a style takes `#RRGGBB` |
| effort-text | `#FF5A54` | `Series[0]`: red for thin lines and small text |
| effort | `#E30613` | fills only: a column's or a point's `Color` |
| data | `#D7DDE5` | `Series[1]`: the steel second series |
| improved / worse | `#34d399` / `#f87171` | `Rising` / `Falling`, which change colours use |
| Inter | | `FontFamily` |

```csharp
var raceFace = new ChartStyle
{
    Background = "#161618", Text = "#F5F6F7", Muted = "#80858E", Grid = "#2D2D2F", Edge = "#80858E",
    Series = ["#FF5A54", "#D7DDE5", "#F5B642", "#3FD17A", "#C2C6D2", "#CD7F46"],   // red, steel, gold, good, silver, bronze
    Zones = ["#80858E", "#D7DDE5", "#3FD17A", "#F5B642", "#F2545B"],               // five zones; a longer scale names its own colours
    Rising = "#34d399", Falling = "#f87171",
    HeatmapLow = "#1E1F22", HeatmapHigh = "#E30613",
    FontFamily = "Inter, Segoe UI, Arial, sans-serif"
};
```

`raceFace.ContrastIssues()` is empty: text `#F5F6F7` stands 16.70:1 on the card, low `#80858E` 4.87:1 (5.34:1 on the page), steel 13.22:1, `#FF5A54` 5.89:1, improved 9.40:1 and worse 6.53:1. One token fails a use: race red `#E30613` stands 3.70:1 on the card, which clears 3:1 for a filled mark but not 4.5:1 for small text. A line's value labels take its colour only where it clears 4.5:1, and the text colour otherwise, so a line in `#E30613` would be labelled in white rather than red. So the style's red is `#FF5A54`, as the tokens say for small text and thin lines, and `#E30613` belongs on fills, where a `ChartPoint.Color` or a column series' own colour puts it. `ChartStyle.Midnight` is the ready-made alternative, near-black with vivid colours.

**A card on another surface.** Every chart here paints its own background, the card colour `#161618`. Where a chart stands on a surface of another colour, a raised panel say, set `PaintBackground = false` (0.41.0) so the panel shows through, and set `Background` to that panel's colour: `Style = raceFace with { Background = "#1E1F22" }, PaintBackground = false`. Lumen still checks every contrast against `Background`, and draws its halos and separators in it (a value label's halo, an end label's, a reference drawn in front), so a `Background` that is not the panel's colour would show those as patches. Check the panel with `ContrastIssues()` too: on `#1E1F22` low `#80858E` stands only 4.44:1, just under the 4.5:1 small text needs, so a chart on that panel writes its captions in `mid` `#B7BCC4` (8.64:1) as its `Muted`.

## Mapping the app's records

Each race is one point per measure, placed by its index so the races stand evenly, labelled with its date, its place's note the size of its field:

```csharp
// races: the app's records in date order. An invented season:
(DateOnly Date, int? Position, int? FieldSize, int? Points)[] races = [
    (new(2026, 4, 11), 31, 50, 40), (new(2026, 5, 16), 24, 48, 52), (new(2026, 7, 4), 27, 51, 47),
    (new(2026, 8, 8), 21, 49, 58), (new(2026, 9, 19), 19, 52, 61)];
var raceDates = races.Select(r => r.Date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)).ToArray();
// The place, its change against the race before coloured and named (fewer places is better), its field written after it in the
// muted colour as "/50"; and the points the race earned.
ChartSeries Placings() => new("Pos/field", races.Select((r, i) => new ChartPoint(i, r.Position, raceDates[i])
    { ValueNote = r.FieldSize is int field ? $"/{field}" : null }).ToArray(), "#F5F6F7")
    { ChangeColors = ChangeColors.LowerIsBetter, ValueLabels = true, Markers = MarkerStyle.Filled };
ChartSeries RacePoints() => new("Pts", races.Select((r, i) => new ChartPoint(i, r.Points, raceDates[i])).ToArray(), "#D7DDE5")
    { ValueLabels = true, Markers = MarkerStyle.Filled };
```

- `X` is the race's index, and `XMin = -0.5`, `XMax = races.Length - 0.5` below put each race in the middle of its slot, so the first and last dates and values stand clear of the card's edges.
- `r.Position` of `null` is a gap in the line and no mark; `r.Points` of `null` likewise; a `null` field size writes the place without a note. A season with no positions at all is better shown as the app's own empty state.
- `ChangeColors = ChangeColors.LowerIsBetter` draws a place better than the race before in `Rising`, worse in `Falling`, and the first place, or one level with the last, in the series colour, hi. The marker and the segment arriving at it take the colour. It compares with the nearest earlier race that has a place, across a gap. Each mark's name ends `, better than the previous`, `, worse than the previous` or `, level with the previous`.
- `ChangeColors` compares a place with the one before it **in the same series**, so put each competition in a series of its own: a rider who rides league rounds and open races gets a `League` series and an `Open` series of placings, both on the races' shared X (each race's index in the one season), each with `ChangeColors` of its own. A 12th in an open race of 80 is then never called worse than a 4th in a league round of 30. A race of the other competition is simply absent from a series, not a gap in it: leave its point out rather than give it a `null` Y, so the line joins that series' races.
- `ValueLabels = true` writes `24` in the point's colour at weight 600 above its marker — every colour in this style clears 4.5:1 on the card, so each label keeps its point's colour; a colour that did not would write it in the text colour — and its `ValueNote`, `/48`, straight after it in the muted colour. A label moves in from the plot's sides rather than being cut, drops below its marker where above would leave the plot or meet another label, and is left out where neither has room; its value stays in the mark's name.
- The dates label the axis: up to 24 labelled points do on a linear axis. At 340 px every other date is left out so none touch; each race keeps its date in its tooltip.

## Position and points by race, as the hand-built chart draws it

Both measures on one scale built from every place and every points total, padded 12 % of their range top and bottom, with no gridlines:

```csharp
// The values run from 19 to 61, a range of 42, so 0.12 × 42 = 5.04 goes on each end and the axis runs from 13.96 to 66.04.
var raceValues = races.SelectMany(r => new[] { r.Position, r.Points }).OfType<int>().ToArray();
double raceLow = raceValues.Min(), raceHigh = raceValues.Max();
var racePad = Math.Max(0.12 * (raceHigh - raceLow), 1);        // a season of one value still gets an axis
var faithful = new ChartSpec {
    Title = "Position & points by race", Description = "Place over field size, and points, race by race",
    Kind = ChartKind.Line, Width = 340, Height = 300, Style = raceFace with { Gridlines = GridLine.Hidden },
    XMin = -0.5, XMax = races.Length - 0.5, YMin = raceLow - racePad, YMax = raceHigh + racePad,
    Series = [Placings(), RacePoints()]
};
string faithfulSvg = ChartSvg.Render(faithful);
```

The axis keeps its tick labels with the gridlines hidden. The place that is best is the one lowest on the chart: position 1 would sit at the bottom, under every points total, which is why the next recipe is recommended.

## Position and points by race, recommended: two panes

Places on a reversed axis, first at the top, and the points beneath in a pane of their own, both on the races' shared X. From 0.45.0 one call builds it from the races themselves, each a `Placing` with its place, the size of its field, its points and its date, with Race Face's own words and colours as options:

```csharp
// The invented races of "Mapping the app's records", each as a Placing; a race with no Position is left out, one with no Points has no points mark.
var placeResults = races.Select(r => new Placing(r.Position) { Field = r.FieldSize, Points = r.Points, Date = r.Date }).ToArray();
var placeWords = new PlacingsOptions
{
    PlaceName = "Pos/field", PointsName = "Pts", DateFormat = "dd-MM-yyyy", Unlabelled = "R{0}",
    PlaceColors = [raceFace.Text, raceFace.Series[2], raceFace.Series[4], raceFace.Series[5]],   // hi, gold, silver, bronze: one for each series, in turn
    PointsColor = raceFace.Series[1],                                                            // steel
    Style = raceFace
};
// Build returns null when no race has a place: show the app's own empty state then. The title and axis label are the host's words.
var placeChart = PlacingsChart.Build(placeResults, placeWords)! with { Title = "Position & points by race", YLabel = "Position" };
string placeSvg = ChartSvg.Render(placeChart);
```

In a Blazor page, with an interactive render mode, the component takes the same results and options (a razor block, not compiled by the recipe check):

```razor
<LumenPlacings Results="placeResults" Options="placeWords" Adjust="Adjust">
    <Empty><p>No races yet.</p></Empty>
</LumenPlacings>

@code {
    // placeResults and placeWords are built above; Adjust is the host's last word on the chart the builder makes.
    ChartSpec Adjust(ChartSpec spec) => spec with { Title = "Position & points by race", YLabel = "Position" };
}
```

- **What the call does.** It drops a race without a place, draws the races in date order when every one has a date (else as given), labels each by its `Label`, else its date in `DateFormat`, else `R1`, `R2`; and it writes each place with its field, `24/48`, and names it by its change, `Pos/field: 16-05-2026, 24/48, better than the previous`. The points are one line in a pane labelled `Pts`, with a mark at each race that has points: a race without points is never a zero, a gap where its series scores at other races and no mark (the line joins across it) where its series never scores. With no points at all there is no pane, and the chart is 260 tall instead of 380. It is 340 wide with `YReversed`, and its description reads `Finishing place out of the field, first at the top, and points. Best: 19.`
- **One line for each series.** Give each result a `Series` ("League", "Open") and each gets its own line, named `Pos/field · League`, holding points only at its own races, so it draws joined across the others'; a place is better or worse only than the previous race of its own series, which is what the League and Open advice in "Mapping the app's records" asks for by hand. A race with no series is a line of its own. `PlaceColors` are used in turn, one for each series, and cycle; leave it null and the style's series colours are used in order, less any near its `Rising` or `Falling` colour, with the points taking the next one. A race with no series keeps the plain name `Pos/field` among several lines.
- **Colours.** With change colours the lines are drawn in the style's `Rising` and `Falling` colours by change, as in the hand-built chart; in the interactive component the legend shows each series' own colour, hi for the first series and gold for the second. A host that does not pass `PlaceColors` takes `raceFace.Series` in order, less any colour near its `Rising` or `Falling`, which this style keeps for thin lines and small text.
- **Refusals.** A blank `PlaceName`, `PointsName` or `DateFormat`, an `Unlabelled` without `{0}`, an empty `PlaceColors` and a null result throw an `ArgumentException` naming the option.
- **Re-rendering.** `<LumenPlacings>` rebuilds its chart whenever its parameters are set, and `<LumenChart>` returns to its whole view when its spec is set, so a host that renders again resets the zoom the reader made. `Static="true"` writes the plain SVG at the spec's own width, 340 unless `Adjust` changes it, and an `Adjust` that returns null shows `Empty`.

The hand-built spec follows, as the explanation of what the builder writes for you: the two series, the pane, the reversed axis and the size are the same.

```csharp
var recommended = new ChartSpec {
    Title = "Position & points by race", Description = "Place over field size, first at the top, and points",
    Kind = ChartKind.Line, Width = 340, Height = 380, Style = raceFace,
    XMin = -0.5, XMax = races.Length - 0.5, YReversed = true, YLabel = "Position",
    Panes = [new ChartPane { Label = "Points", Weight = 1 }],
    Series = [Placings(), RacePoints() with { Pane = 1 }]
};
string recommendedSvg = ChartSvg.Render(recommended);
// Interactive: <LumenChart Spec="recommended" FitWidth="true" />
```

Two measures on one scale put position 1 at the bottom, so a better race reads as a drop, and the two lines cross for no reason in the data; each pane here reads up as better, and each has the axis its own numbers need. `YReversed` is the main pane's; a pane's own `ChartPane.YReversed` reverses that pane instead. Hiding a series in the component's legend closes its pane.

## Your season, round by round

A rider's place in each round on a time axis, first at the top, on Midnight:

```csharp
static double Noon(DateOnly day) => TimeAxis.Value(new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(2)));   // noon, Johannesburg
// rounds: (DateOnly Date, string Name, int? Position), each round's name as the app writes it. An invented season:
(DateOnly Date, string Name, int? Position)[] rounds = [
    (new(2026, 4, 11), "Round 1 · Hilltop Classic", 31), (new(2026, 5, 16), "Round 2 · River Valley", 24),
    (new(2026, 7, 4), "Round 3 · Quarry Loop", 27), (new(2026, 8, 8), "Round 4 · Forest Sprint", 21), (new(2026, 9, 19), "Round 5 · Final Ridge", 19)];
var seasonByRound = new ChartSpec {
    Title = "Your season, round by round", Description = "Your place in each round, first at the top",
    Kind = ChartKind.Line, XAxis = AxisKind.Time, TimeZone = "Africa/Johannesburg", XTicks = TickSource.Axis,
    YReversed = true, IncludeZero = false, Width = 340, Height = 260, Style = ChartStyle.Midnight,
    Series = [new("Position", rounds.Select(r => new ChartPoint(Noon(r.Date), r.Position, r.Name)).ToArray(), "#FF5A54")
        { Markers = MarkerStyle.Filled, HighlightLast = true }]
};
string seasonSvg = ChartSvg.Render(seasonByRound);
```

The X ticks are dates, `13 Apr`, `8 Jun`, `3 Aug`, read in Johannesburg, and each round's name stays in its point's tooltip and accessible name, `Position: Round 3 · Quarry Loop, 27`. Before 0.33.0 the names took the ticks' place and were cut at twelve characters, `Round 1 · H…`. From 0.33.0 a time axis does this on its own, `TickSource.Auto`, whenever a point's label would be cut; `XTicks = TickSource.Axis` keeps the dates even for names short enough to fit, such as `Round 3`, and `TickSource.PointLabels` puts the names back, thinned and cut. `HighlightLast` rings the latest round.

## Season strip

The strip of round tiles stays HTML: it is a grid of facts, not a chart. Its ▲ and ▼ follow `ChangeColors.LowerIsBetter`, so the strip and the chart never disagree: each round against the nearest earlier round that has a place, whether or not that round has a tile of its own, fewer places being better.

```csharp
int? lastPlace = null;
foreach (var (date, name, position) in rounds)
{
    if (position is not int place) continue;                               // no place: no tile, and nothing for the next to compare with
    int? gained = lastPlace is int before ? before - place : null;         // places gained on the round before
    var arrow = gained switch { > 0 => $"▲ {gained}", < 0 => $"▼ {-gained}", 0 => "no change", _ => "" };   // the first round has none
    lastPlace = place;
}
```

A ▲ round is drawn in `Rising` on the chart and a ▼ round in `Falling`, so the strip can take the same two colours, `#34d399` and `#f87171`.

## Getting faster? Personal-best sparklines

One sparkline per distance raced at least twice: the finish times oldest to newest, placed by index, faster higher, each personal best ringed in race red. `Sparkline = true` (0.34.0) draws the data alone — no axes, gridlines, legend, title or any other text — in a plot that fills the 120 by 32 drawing but for the room its largest ring needs; the words go in the page beside it.

```csharp
// The app's results for one distance, oldest first. An invented run of six 5 km races:
(DateOnly Day, string Race, double Seconds)[] fiveKm = [
    (new(2026, 3, 14), "Harbour Parkway 5", 1450), (new(2026, 4, 18), "Quarry Loop 5", 1432), (new(2026, 5, 23), "River Mile 5", 1445),
    (new(2026, 6, 27), "Hilltop 5", 1411), (new(2026, 8, 1), "Forest Run 5", 1420), (new(2026, 9, 12), "Final Ridge 5", 1367)];
// Seconds written as the chart writes them, 24:10 or 1:02:05.
static string Clock(double seconds) => new Axis(AxisKind.Linear, 0, 1) { ValueFormat = ValueFormat.Duration }.Format(seconds);
ChartSpec PbSparkline(string distance, (DateOnly Day, string Race, double Seconds)[] times)
{
    // A personal best is faster than every time before it; the first race only sets the time to beat.
    bool Best(int i) => i > 0 && times.Take(i).All(before => times[i].Seconds < before.Seconds);
    return new ChartSpec {
        // "5 km: 24:10 to 22:47 over 6 races", worked out here: the drawing writes no words of its own.
        Title = $"{distance}: {Clock(times[0].Seconds)} to {Clock(times[^1].Seconds)} over {times.Length} races",
        Description = "Each race's finish time, oldest first, faster higher, personal bests marked",
        Kind = ChartKind.Line, Width = 120, Height = 32, Sparkline = true, Style = raceFace,
        YReversed = true, YFormat = ValueFormat.Duration,
        Series = [new(distance, times.Select((t, i) => new ChartPoint(i, t.Seconds, $"{t.Day.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} · {t.Race}")
            { Highlight = Best(i) ? "#E30613" : null, ValueNote = Best(i) ? " · PB" : null }).ToArray(), "#B7BCC4") { StrokeWidth = 2 }]
    };
}
string pbSvg = ChartSvg.Render(PbSparkline("5 km", fiveKm));
```

- `X` is the race's index, so the races stand evenly however far apart they were run; the line runs from the first at the left edge of the plot to the last at the right.
- `YReversed = true` puts the fastest time at the top, and `YFormat = ValueFormat.Duration` writes each time as `22:47` in its mark's name.
- The line is `mid`, `#B7BCC4`, 9.47:1 on the card: neutral, so it says nothing good or bad by itself.
- **Never colour alone.** `ChartPoint.Highlight` (0.34.0) rings a personal best with an enlarged marker in `effort` red, `#E30613`, outlined in the card colour, whatever the series' markers; the line keeps its colour. Red stands 3.70:1 on the card, which a mark needs. The ring is only for those who see it, so each best also carries `ValueNote = " · PB"`, which its tooltip and accessible name read after the time: `5 km: 18 Apr 2026 · Quarry Loop 5, 23:52 · PB`. Pair them always: a highlight says *look here*, the note says *why*.
- The title is the drawing's accessible name and `<title>`, the description its `<desc>`; every race is a focusable mark named as above, with the same words as its native tooltip, so the static SVG reads race by race with no script.
- The padding is just enough for a ring at any edge: here 6.5 units, the ring's 5.5 radius and half its outline, so the plot runs from 6.5 to 113.5 across and 6.5 to 25.5 down. `HighlightLast` would need 10.

Beside it, the page writes the numbers in words, as the app's timeline does: `<p><b>5 km</b> 24:10 → 22:47 over 6 races · best 22:47</p>`. A sparkline is shown at its own width, 120 pixels, never wider than its box; give its box room, `display:flex; gap:12px; align-items:center`, and it sits on the line beside its words.

## Growth log sparklines

A child's logged weight (or height) in log order, on an axis that spans at least 8 kg, centred on the data, so a 0.3 kg wobble reads as the steady line it is rather than as a cliff. `ChartSpec.YMinSpan` (0.34.0) does it: when the data's range is less than the span, the axis runs from the data's middle less half the span to its middle plus half; when it is wider, the axis fits the data as before.

```csharp
// The app's log of one measure, in log order. An invented log of four weights in kilograms:
double[] weights = [37.9, 37.8, 38.2, 38.1];
// The lightest, 37.8, and the heaviest, 38.2, have their middle at (37.8 + 38.2) / 2 = 38.0 and a range of 0.4, less than 8,
// so the axis runs from 38.0 − 8 / 2 = 34 to 38.0 + 8 / 2 = 42. The library works the same out; the caption says it in words.
double lightest = weights.Min(), heaviest = weights.Max();
double middle = (lightest + heaviest) / 2, span = Math.Max(8, heaviest - lightest);
string scaleCaption = string.Create(CultureInfo.InvariantCulture, $"scale {middle - span / 2:0.#}–{middle + span / 2:0.#} kg");          // "scale 34–42 kg"
string weightSummary = string.Create(CultureInfo.InvariantCulture, $"Weight: {weights.Length} measurements, from {weights[0]:0.0} to {weights[^1]:0.0} kg.");   // "Weight: 4 measurements, from 37.9 to 38.1 kg."
var growth = new ChartSpec {
    Title = weightSummary.TrimEnd('.'), Description = $"Logged weight in log order, {scaleCaption}",
    Kind = ChartKind.Line, Width = 270, Height = 54, Sparkline = true, Style = raceFace, YMinSpan = 8,
    Series = [new("Weight", weights.Select((kg, i) => new ChartPoint(i, kg, $"Measurement {i + 1}") { ValueNote = " kg" }).ToArray(), "#D7DDE5") { StrokeWidth = 2 }]
};
string growthSvg = ChartSvg.Render(growth);
```

- **Neutral colour only.** Weight and height are sensitive data about children's bodies. A good or bad colour, a target line or a healthy band would judge a child's body, so the line is the steel `data` colour, `#D7DDE5`, 13.22:1 on the card, no point is highlighted, and no zone or annotation is drawn. Do not add them.
- `YMinSpan = 8` keeps the axis 8 kg tall about the weights; for height, a span in centimetres. It is refused beside `YMin` or `YMax`, on a logarithmic axis and on an axis that must include zero (`IncludeZero`, columns, areas).
- The caption and the summary are the page's words, worked out above from the same numbers: write them in HTML beside the drawing, which carries no text, as `<figure>` `@((MarkupString)growthSvg)` `<figcaption>scale 34–42 kg</figcaption>` `</figure>` and `<p>Weight: 4 measurements, from 37.9 to 38.1 kg.</p>`. Each measurement stays a focusable mark named `Weight: Measurement 2, 37.8 kg`.
- At 270 by 54 the plot stands 4 units in, room for a marker shown on hover, so the line runs 46 units tall: 34 kg at the bottom, 42 at the top, the weights a band of 2.3 units through the middle.

## How the field finished

A race's finish times as a histogram on a continuous time axis: the app supplies the bins already counted, each drawn as a block from where it starts to where it ends and as tall as its count; the bin that holds the reader's time is race red and says so; the median is a dashed line over the bins; and the finishers off the chart are counted under it.

The app chooses the bins. Its rule, as caller code: the narrowest width of 1, 2, 5, 10, 15, 30 or 60 minutes that sets the 1st to the 99th percentile of the times in at most 20 bins, each starting on a whole multiple of the width, and the finishers outside those bins, faster and slower. `Statistics.Quantile` interpolates between order statistics, as Excel's `PERCENTILE.INC` does.

```csharp
static ((double From, double To, int Count)[] Bins, int Faster, int Slower) FinishBins(IReadOnlyList<double> seconds)
{
    var sorted = seconds.Order().ToArray();
    double low = Statistics.Quantile(sorted, .01), high = Statistics.Quantile(sorted, .99);
    // The first width whose bins, from the one holding the 1st percentile to the one holding the 99th, number 20 or fewer.
    var width = new[] { 60d, 120, 300, 600, 900, 1800, 3600 }.FirstOrDefault(w => Math.Floor(high / w) - Math.Floor(low / w) < 20, 3600);
    double from = Math.Floor(low / width) * width, to = (Math.Floor(high / width) + 1) * width;
    var bins = Enumerable.Range(0, (int)Math.Round((to - from) / width)).Select(i => (From: from + i * width, To: from + (i + 1) * width))
        .Select(bin => (bin.From, bin.To, Count: sorted.Count(t => t >= bin.From && t < bin.To))).ToArray();
    return (bins, sorted.Count(t => t < from), sorted.Count(t => t >= to));
}
```

What the page is given for one race, as the app's API sends it — an invented race of 312 finishers in 5-minute bins from 35:00 to 1:20:00, three faster and twelve slower off the chart, its median and the reader's own time:

```csharp
(double FromSeconds, double ToSeconds, int Count)[] fieldBins = [
    (2100, 2400, 18), (2400, 2700, 64), (2700, 3000, 88), (3000, 3300, 57), (3300, 3600, 33),
    (3600, 3900, 18), (3900, 4200, 10), (4200, 4500, 8), (4500, 4800, 1)];
int finishers = 312, offFaster = 3, offSlower = 12;
double medianSeconds = 2832, yourSeconds = 3160;     // 47:12, and 52:40 in the 50:00 to 55:00 bin
bool Yours((double FromSeconds, double ToSeconds, int Count) bin) => yourSeconds >= bin.FromSeconds && yourSeconds < bin.ToSeconds;
var fieldChart = new ChartSpec {
    Title = "How the field finished", Description = $"{finishers} finishers · median {Clock(medianSeconds)}",
    Source = $"Off the chart: {offFaster} faster and {offSlower} slower",
    Kind = ChartKind.Blocks, Width = 340, Height = 240, Style = raceFace with { BarRadius = 2 },
    IncludeZero = true, XFormat = ValueFormat.Duration, XTickLabels = TickLabels.Bounds, YTickLabels = TickLabels.Bounds,
    Annotations = [new(AnnotationAxis.X, medianSeconds) { Label = "median", ShowValue = false, InFront = true, Color = "#F5F6F7" }],
    Series = [new("Finishers", fieldBins.Select(bin => ChartPoint.Block(bin.FromSeconds, bin.ToSeconds, bin.Count)
        with { Color = Yours(bin) ? "#E30613" : null, ValueNote = Yours(bin) ? " · you" : null }).ToArray(), "#80858E")]
};
string fieldSvg = ChartSvg.Render(fieldChart, includeLegend: false);
// Interactive: <LumenChart Spec="fieldChart" FitWidth="true" />
```

- **Bins.** Each bin is `ChartPoint.Block(from, to, count)`: it covers its span of time exactly and stands on the bottom of the plot. Bins that touch are parted by a hairline, as a series' blocks are, and `BarRadius = 2` rounds their tops by 2 units. The bars are the token `low`, `#80858E`, neutral: a bin is not good or bad.
- **Heights.** `IncludeZero = true` runs the axis from exactly 0 to the most in a bin, 88, so each block's height is its count. A block above the bottom of the axis is drawn at least 2 units tall (0.35.0), so the last bin's one finisher of 312, 0.98 units at this size, still shows; a bin of none draws nothing and keeps its name.
- **Axes.** `XFormat = ValueFormat.Duration` writes times as `35:00` and `1:20:00`. `XTickLabels = TickLabels.Bounds` labels only the axis's two ends, at their exact values: the first bin's start, starting at the plot's left edge, and the last bin's end, ending at its right edge. `YTickLabels = TickLabels.Bounds` labels `0` and `88`. The gridlines stay at round counts.
- **Never colour alone.** The reader's bin takes race red, `#E30613`, 3.70:1 on the card, which a filled mark needs, and the note ` · you`, which its tooltip and accessible name read after its count: `Finishers: 50:00 to 55:00, 57 · you`. Every bin is a focusable mark named the same way, `Finishers: 45:00 to 50:00, 88`.
- **Median.** An X annotation, dashed as annotations are by default. `ShowValue = false` draws its label as `median` alone, since the description and the axis already give the time; its tooltip and accessible name still read `median: 47:12`. `InFront = true` draws the line over the bins, which would otherwise hide it, on a halo of the card colour so it shows over a bar of any colour. Its colour is the token `hi`, `#F5F6F7`, 16.70:1, so its label clears 4.5:1.
- **Words.** `Description` holds the summary sentence and `Source` the finishers off the chart, in the muted `low`, 4.87:1. Both stay one line here; on a narrower card either goes on over a second line rather than running off it. Leave `Source` empty when nobody is off the chart.
- **One series.** `includeLegend: false` leaves out the legend row a lone series would otherwise get; its name, `Finishers`, leads each bin's name.

The 1080 × 1350 social card is the same chart drawn large, every bar race red, with no median and no reader's bin:

```csharp
var fieldCard = fieldChart with {
    Width = 1080, Height = 1350, Annotations = [],
    Series = [fieldChart.Series[0] with { Color = "#E30613", Points = fieldChart.Series[0].Points.Select(p => p with { Color = null, ValueNote = null }).ToArray() }]
};
string fieldCardSvg = ChartSvg.Render(fieldCard, includeLegend: false);
```

Its words keep their sizes, 11 to 17 units, so on a 1080-pixel card they read small: set the card's headline in its own display type round the chart, or draw the card at 360 × 450 and rasterise it at three times.

## Fitness & form

A rider's fitness (CTL), fatigue (ATL) and form (TSB) from each day's training stress, read day by day: the stress as muted columns under fitness and fatigue, and form beneath in a pane of its own, on an axis held symmetric about zero and written with its sign, so fresh (+) and tired (−) stand either side of its middle. Race days are dashed lines through both panes. On an interactive page the shared readout (0.36.0) reads all four at the day under a finger, the pointer or the focused point, and the arrow keys step it a day at a time. The app offers the range — 7, 28, 90 or 365 days — and gives Lumen only those days.

```csharp
// The app's daily training stress (TSS), oldest first, a day without training as 0. An invented year: a rest day each week, two hard
// days, and blocks of heavier and lighter weeks.
var stressLog = Enumerable.Range(0, 365).Select(i => (Day: new DateOnly(2025, 9, 20).AddDays(i),
    Stress: Math.Round((0.8 + 0.3 * Math.Sin(i / 30.0)) * ((i % 7) switch { 0 => 0, 2 => 95 + i % 5 * 8, 5 => 140 + i % 3 * 15, _ => 45 + i % 4 * 9 })))).ToArray();
// Race days, the app's own records. Invented:
DateOnly[] raceDays = [new(2026, 4, 11), new(2026, 5, 16), new(2026, 7, 4), new(2026, 8, 8), new(2026, 9, 19)];
static double UtcDay(DateOnly day) => TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
// The model runs over the whole log, so the first day of a short range starts from the fitness the days before it built. It is seeded
// with the rider's typical daily stress.
var fitnessLoad = Training.Load(stressLog, fitness: 50, fatigue: 50);
var formWords = new Axis(AxisKind.Linear, 0, 1) { ValueFormat = ValueFormat.Signed };   // writes +5, −5 and 0, as the form axis does
ChartSpec FitnessAndForm(int rangeDays)
{
    // The range buttons are the app's: each takes the last 7, 28, 90 or 365 days of the model and draws the same chart.
    var shown = fitnessLoad.TakeLast(rangeDays).ToArray();
    var today = shown[^1];
    return new ChartSpec {
        Title = string.Create(CultureInfo.InvariantCulture, $"Fitness {Math.Round(today.Fitness)} · form {formWords.Format(Math.Round(today.Form))}"),
        Description = $"Last {rangeDays} days · fitness, fatigue and daily stress above, form below",
        Kind = ChartKind.Line, XAxis = AxisKind.Time, Width = 340, Height = 420, Style = raceFace, SharedReadout = true, YLabel = "TSS",
        Panes = [new ChartPane { Label = "Form", Weight = .6, YSymmetric = 10, YFormat = ValueFormat.Signed }],
        Annotations = raceDays.Where(d => d >= shown[0].Day && d <= today.Day)
            .Select(d => new ChartAnnotation(AnnotationAxis.X, UtcDay(d)) { Label = "Race", ShowValue = false, Color = "#a78bfa" }).ToArray(),
        Series = [
            ChartSeries.From("Fitness", shown, d => UtcDay(d.Day), d => Math.Round(d.Fitness, 1)) with { Color = "#38bdf8" },
            ChartSeries.From("Fatigue", shown, d => UtcDay(d.Day), d => Math.Round(d.Fatigue, 1)) with { Color = "#f87171" },
            ChartSeries.From("Form", shown, d => UtcDay(d.Day), d => Math.Round(d.Form, 1)) with { Color = "#f59e0b", Pane = 1 },
            ChartSeries.From("TSS", shown, d => UtcDay(d.Day), d => d.Stress) with { Kind = ChartKind.Column, Color = "#80858E" }]
    };
}
var fitnessWeek = FitnessAndForm(7);
var fitnessMonth = FitnessAndForm(28);
var fitnessQuarter = FitnessAndForm(90);
var fitnessYear = FitnessAndForm(365);
string fitnessSvg = ChartSvg.Render(fitnessMonth);
// Interactive: <LumenChart Spec="fitnessMonth" FitWidth="true" />, with the app's own 7 / 28 / 90 / 365 buttons above it.
```

- **Two panes.** The main plot holds TSS, fitness and fatigue, all in training stress per day, so they share one axis; form, the difference of fitness and fatigue, gets the pane below and its own axis. `Weight = .6` makes the form pane 0.6 of the main plot's height. Every pane shares the X axis, and an X annotation runs through each.
- **Symmetric form.** `ChartPane.YSymmetric = 10` (0.36.0) holds the form axis symmetric about zero, at least −10 to +10, and as far as the data reaches either way, so a form of −24 runs it from −24 to +24 and zero stays in the middle: +4 and −4 stand equally far from it, and a quiet stretch near zero still reads as near zero. It is refused beside `YMin`, `YMax` or `YMinSpan` and on a logarithmic axis. `ChartSpec.YSymmetric` does the same for the main plot.
- **Signs.** `ValueFormat.Signed` (0.36.0) writes the form axis's ticks, its points' names and the readout as `+12`, `−8` (a true minus, U+2212) and `0`. `formWords` above writes the title's form the same way.
- **Colours.** Every colour here is checked against the card, `#161618`: fitness `#38bdf8` 8.44:1, fatigue `#f87171` 6.53:1, form `#f59e0b` 8.41:1, the race lines `#a78bfa` 6.64:1, so each clears 4.5:1 and the race label keeps its colour; TSS is the token `low`, `#80858E`, 4.87:1, neutral, since a day's stress is not good or bad. These are colours for a dark card: on a white one they stand only 2.14, 2.77, 2.15 and 2.72 to 1, short even of the 3:1 a line needs. A light theme takes darker ones, `ChartStyle.Light`'s series for instance; and a colour that carries text, as an annotation's label does, should clear 4.5:1 — test it with `Contrast.Ratio(color, style.Background) >= 4.5` and pass `Color = null`, the muted colour, where it falls short, as value labels fall back to the text colour on their own.
- **Never colour alone.** The series are told apart by colour in the drawing, and by name everywhere else: each point's tooltip and accessible name, `Form: 14 Sep 2026, −8.6`, and the readout, which names every series. A race line's name reads `Race: 4 Jul 2026`. At 340 units a year's races stand about 25 units apart, so their labels step down a row where they would touch, and one with no room left is left out, its name kept.
- **The shared readout.** `SharedReadout = true` changes nothing in the SVG: `ChartSvg.Render` draws the same chart with it or without it. In `<LumenChart>` a guide runs through both panes at the day nearest the pointer, a tap or the focused point, each series' point there is ringed, and one tooltip reads the day and then each series in legend order: `4 Jul 2026`, `Fitness 61.2`, `Fatigue 70.4`, `Form −9.1`, `TSS 0`. Left and Right step a day, Up and Down move between the series, Home and End go to the first and last day, Page Up and Page Down ten days, and Escape hides it; the status line reads the same words for a screen reader. `ChartSvg.Readout(spec)` gives the same table to a host that draws its own.
- **The app's own load.** When the app already computes fitness, fatigue and form, as an API with custom from–to windows does, draw its values rather than run `Training.Load`: `ChartSeries.From("Fitness", apiDays, d => UtcDay(d.Day), d => d.Fitness)` and the same for the others, so the chart and the app never disagree.
- **Race names.** A race line may carry the race's own name, `Label = race.Name`, rather than `Race`; its tooltip and accessible name read it whole. Where race lines stand close, labels step down a row and one with no room left is left out, and a long name needs the most room, so it is the first to be left out: keep the label short, the date or an abbreviation, and leave the full name to the tooltip or the page.
- **Phone cards.** On a small card `<LumenChart Spec="fitnessMonth" FitWidth="true" ShowToolbar="false" />` (0.38.0) leaves out the zoom, export and data-table buttons, which wrap onto several rows under a phone chart; the status line stays in the page, out of sight, so a screen reader still hears the readout. Keep the legend here, since four series share the plots; `ShowLegend="false"` suits a chart whose series are named in the drawing. Hiding the toolbar takes away the keyboard's way to zoom and the only way to the data table, so keep it where those matter.
- **Ranges.** Slice the model's days, not the stress before the model, so a 7-day chart's first fitness is the one the whole year built. Fitness takes about six weeks to build, so seed `Training.Load` with the rider's typical daily stress, or start the log six weeks before the first day shown.

## Ride channels

A ride's channels as a bike computer records them, one sample a second, each in a plot of its own over one elapsed-time axis: heart rate, power, cadence, speed, elevation and temperature, six plots of equal height. Each plot is named on one line above it with its average, highest and lowest, so no plot needs tick labels up the side; each channel's two hours are drawn as 600 averages of 12 seconds, so the lines read as the ride rather than as its noise; and on an interactive page the shared readout reads all six at the second under a finger, the pointer or the focused point, and a drag across the plots with a mouse zooms to the stretch it covers (0.37.0).

```csharp
// The app's ride as recorded, one sample a second: heart rate (null where the strap dropped out), power, cadence, speed in m/s, elevation
// and temperature. An invented two-hour ride, worked out from sines so it is the same on every run.
const int channelSeconds = 7200;
static double RoadHeight(int t) => 140 + 55 * Math.Sin(t / 380.0 - 1.3) + 18 * Math.Sin(t / 116.0 + 0.4);
var rideHeart = new double?[channelSeconds];
double[] ridePower = new double[channelSeconds], rideCadence = new double[channelSeconds], rideSpeed = new double[channelSeconds],
    rideElevation = new double[channelSeconds], rideTemperature = new double[channelSeconds];
var pulse = 92.0;
for (var t = 0; t < channelSeconds; t++)
{
    var gained = RoadHeight(t + 1) - RoadHeight(t);                                        // metres gained this second
    var effort = t < 600 ? 120 + t / 6.0 : 215;                                           // ten minutes' warm-up, then steady
    var watts = gained < -0.2 ? 0 : Math.Max(0, effort + 450 * gained + 28 * Math.Sin(t * 1.7) + 16 * Math.Sin(t * 0.31 + 1));
    ridePower[t] = Math.Round(watts);                                                     // 0 while coasting down the steeper descents
    rideCadence[t] = watts == 0 ? 0 : Math.Round(85 + (watts - 215) / 25 + 3 * Math.Sin(t * 0.83));
    rideSpeed[t] = Math.Round(Math.Clamp(8.6 - 9 * gained + 0.4 * Math.Sin(t / 50.0), 3.5, 16), 2);
    rideElevation[t] = Math.Round(RoadHeight(t), 1);
    rideTemperature[t] = Math.Round(17.5 + 5.5 * t / channelSeconds + 0.6 * Math.Sin(t / 700.0), 1);
    pulse += (88 + 0.3 * watts - pulse) / 60;                                             // heart rate follows the power a minute behind
    rideHeart[t] = t is >= 3720 and < 3765 ? null : Math.Round(pulse);                     // the strap drops out for 45 seconds
}
// Each channel's header, worked out with plain LINQ: a missing sample is left out, never counted as zero.
string ChannelHeader(string name, IEnumerable<double?> values, string unit, string format = "0")
{
    var present = values.OfType<double>().ToArray();
    string Text(double value) => value.ToString(format, CultureInfo.InvariantCulture);
    return $"{name} · avg {Text(present.Average())} · max {Text(present.Max())} · min {Text(present.Min())} {unit}";
}
static IEnumerable<double?> Readings(IEnumerable<double> values) => values.Select(value => (double?)value);
ChartSeries Channel(string name, IEnumerable<double?> values, string color, int pane) =>
    new(name, values.Select((value, t) => new ChartPoint(t, value)).ToArray(), color) { Pane = pane, Markers = MarkerStyle.None, StrokeWidth = 1.5 };
var rideKmh = rideSpeed.Select(metres => (double?)Math.Round(metres * 3.6, 1)).ToArray();   // stored in m/s, drawn in km/h
var rideChannels = new ChartSpec {
    Title = string.Create(CultureInfo.InvariantCulture, $"{rideSpeed.Sum() / 1000:0.0} km in {Clock(channelSeconds)}"),
    Description = "Six channels, one sample a second, each averaged over 12 seconds",
    Kind = ChartKind.Line, Width = 340, Height = 640, Style = raceFace, XFormat = ValueFormat.Duration, XLabel = "Elapsed time",
    Sampling = SamplingMethod.Average, MaxRenderedPoints = 600, YTickLabels = TickLabels.None, PaneTitles = PaneTitlePlacement.Above, SharedReadout = true,
    YLabel = ChannelHeader("HR", rideHeart, "bpm"),
    Panes = [
        new ChartPane { Label = ChannelHeader("Power", Readings(ridePower), "W"), Weight = 1 },
        new ChartPane { Label = ChannelHeader("Cadence", Readings(rideCadence), "rpm"), Weight = 1 },
        new ChartPane { Label = ChannelHeader("Speed", rideKmh, "km/h"), Weight = 1 },
        new ChartPane { Label = ChannelHeader("Elevation", Readings(rideElevation), "m"), Weight = 1 },
        new ChartPane { Label = ChannelHeader("Temp", Readings(rideTemperature), "°C", "0.0"), Weight = 1 }],
    Series = [
        Channel("Heart rate", rideHeart, "#e24b4a", 0), Channel("Power", Readings(ridePower), "#7048e8", 1),
        Channel("Cadence", Readings(rideCadence), "#1098ad", 2), Channel("Speed", rideKmh, "#0ca678", 3),
        Channel("Elevation", Readings(rideElevation), "#868e96", 4), Channel("Temperature", Readings(rideTemperature), "#e8950c", 5)]
};
string rideSvg = ChartSvg.Render(rideChannels, includeLegend: false);
// Interactive: <LumenChart Spec="rideChannels" FitWidth="true" />
```

- **Six plots.** `Panes` takes five from 0.37.0, so the main plot and five panes, each `Weight = 1`, share the height equally: at 340 by 640 each plot is about 53 units tall. Every pane shares the X axis, `XFormat = ValueFormat.Duration` writing `30:00` and `1:30:00`.
- **Headers, not tick labels.** `PaneTitles = PaneTitlePlacement.Above` writes the main plot's `YLabel` and each pane's `Label` as one line over the plot's left edge, in the text colour, `hi` `#F5F6F7`, 16.70:1 on the card. Put the key fact first and keep it short, the channel's short name and then its numbers, `HR · avg 147 · max 191 · min 89 bpm`: all six here stand whole at 340, and at 320. A header the card is too narrow for is cut at a word with `…`, its end lost from the drawing, though not from its tooltip or its accessible name, which keep the whole of it. Speed's header is written in whole km/h to fit; its points keep their tenths. `YTickLabels = TickLabels.None` writes no tick label in any plot, its gridlines staying; a pane may set its own `ChartPane.YTickLabels`, `Ends` say, to put its two ends back. With nothing written up the left, the plots run from 30 units in rather than 76.
- **Averaged slices.** `Sampling = SamplingMethod.Average` with `MaxRenderedPoints = 600` cuts the two hours into 600 slices of 12 seconds, the same slices for every channel, and draws each channel's 12 samples in a slice as one point at their mean, written as precisely as the channel's own samples, to at most two places, and named `Power: 1:00:06, 266, average of 12 points`; channels recorded at the same seconds line up slice for slice, and the readout reads all six at each, saying once what they average: `1:00:06 · average of 12 s`, `Heart rate 166`, `Power 266`, `Speed 27.7`. `MinMax`, the default, would keep each bucket's lowest and highest second instead, a band of spikes at this size. Zoomed in far enough, to 10 minutes say, every second in view is drawn as it was recorded.
- **Missing is a gap.** The strap's 45 missing seconds are `null`: the heart-rate line breaks there, its slices on either side average only the seconds they hold, and the readout reads `Heart rate missing` in the gap. The headers leave them out, `OfType<double>()`, never count them as zero. A real zero, the power and cadence while coasting, is a value and is drawn.
- **Never colour alone.** Each plot holds one channel, named in its header and in every point's name and the readout, so the colours only tell the lines apart. Against the card, `#161618`, the lines need 3:1: heart rate `#e24b4a` 4.59:1, power `#7048e8` 3.25:1, cadence `#1098ad` 5.26:1, speed `#0ca678` 5.80:1, elevation `#868e96` 5.44:1 and temperature `#e8950c` 7.52:1. On a white card heart rate stands 3.93:1, power 5.55:1, cadence 3.43:1, speed 3.12:1 and elevation 3.32:1, but temperature only 2.40:1: there take `#e8590c`, 3.58:1 on white and 5.05:1 on the card, and a light style such as `ChartStyle.Light`, whose text colour writes the headers at 12.80:1.
- **Interactive.** `SharedReadout = true` reads all six at the slice nearest the pointer, a tap or the focused point, and the arrow keys step it slice by slice. With a mouse or a pen, a drag across the plots of 8 pixels or more draws a band through all six and zooms to it; Escape lets it go, Reset view draws the whole ride again, and on a phone a tap still reads the chart. The zoom and pan buttons do the same from the keyboard.
- **Buckets of your own.** If the app already buckets its channels, to 600 points say, pass those points and leave `Sampling` alone: a series within the budget is drawn as given. Lumen then averages nothing, so it says nothing about averages, unless each channel says what its points are: `AverageOf = "12 s"` on each series (0.41.0) names every mark `Power: 1:00:06, 266, average of 12 s`, and brings back the readout's `1:00:06 · average of 12 s` once for the column, its entries reading their values alone, as Lumen's own averages read. A series whose span differs from the others says it after its own value instead. `includeLegend: false` leaves out the legend, since each header names its channel.
- **Keep the decimals in the points.** A channel recorded or stored in whole units, a speed in whole km/h say, draws a staircase: at this size each step of one unit is a visible jump. Keep the tenths in the points, as `rideKmh` does, and round only in the header.
- **Phone cards.** Each header names its channel, so the component's legend adds nothing: `<LumenChart Spec="rideChannels" FitWidth="true" ShowLegend="false" ShowToolbar="false" />` (0.38.0) leaves out the legend and the toolbar, which together took about 250 pixels under a phone chart. The status line stays, out of sight, for a screen reader; drag to zoom and the arrow keys still work, but the keyboard loses zoom, pan and reset and the data table, so keep the toolbar where those matter.
- **A large spec on an interactive island.** A spec passed as a parameter into an `InteractiveServer` component travels in SignalR's circuit-start message. Six channels of 600 points went past SignalR's default 32 KB `MaximumReceiveMessageSize`, and the circuit closed with only a console error, leaving the chart static: no readout, no zoom. Raise the limit where the server is set up, `builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddHubOptions(o => o.MaximumReceiveMessageSize = 256 * 1024);`, or let the island load its own data, passing it an ID rather than the spec.

## Season arc

Every race of a season in date order, each at its index along X, placed by where the rider finished in its field: 0 % at the front, 100 % at the back, on a reversed axis so the front is at the top, with three ticks set by hand, `Front`, `Mid` and `Back`. One line per discipline joins only that discipline's races, over the races of the others between them, so a rider's cross-country form reads as one line however many marathons sit between its races (0.38.0).

```csharp
// The app's races of the season in date order: its day, its discipline, the rider's place (null for a race started but not finished)
// and the size of its field. An invented season:
(DateOnly Day, string Discipline, int? Place, int Field)[] seasonRaces = [
    (new(2026, 2, 7), "XCO", 18, 40), (new(2026, 2, 21), "XCC", 9, 32), (new(2026, 3, 14), "XCO", 12, 44), (new(2026, 3, 28), "XCM", 31, 60),
    (new(2026, 4, 18), "XCO", null, 41), (new(2026, 5, 9), "XCC", 6, 30), (new(2026, 5, 30), "XCO", 7, 42), (new(2026, 6, 20), "Enduro", 22, 55),
    (new(2026, 7, 11), "XCM", 19, 58), (new(2026, 8, 1), "XCO", 5, 40)];
// One line per discipline, each in its colour on the card; any discipline not listed is drawn as Other.
(string Name, string Color)[] arcDisciplines = [("XCC", "#38bdf8"), ("XCO", "#34d399"), ("XCM", "#f59e0b"), ("Other", "#a78bfa")];
string ArcGroup(string discipline) => arcDisciplines.Any(d => d.Name == discipline) ? discipline : "Other";
// Where a place stands in its field, 0 for first and 100 for last, rounded to a whole per cent; a race without a place is null, a gap.
static double? FieldPercent(int? place, int field) => place is int p && field > 1 ? Math.Round(100.0 * (p - 1) / (field - 1)) : null;
var seasonArc = new ChartSpec {
    Title = "Season arc", Description = "Each race's place in its field, front at the top, by discipline",
    Kind = ChartKind.Line, Width = 340, Height = 300, Style = raceFace,
    XMin = -0.5, XMax = seasonRaces.Length - 0.5,
    YReversed = true, YMin = 0, YMax = 100, YUnit = "%", YTickValues = [new(0, "Front"), new(50, "Mid"), new(100, "Back")],
    Series = arcDisciplines.Select(d => new ChartSeries(d.Name, seasonRaces.Select((race, i) => (Race: race, Index: i))
            .Where(t => ArcGroup(t.Race.Discipline) == d.Name)
            .Select(t => new ChartPoint(t.Index, FieldPercent(t.Race.Place, t.Race.Field), t.Race.Day.ToString("d MMM", CultureInfo.InvariantCulture))
                { ValueNote = t.Race.Place is int p ? $" · P{p}/{t.Race.Field}" : null }).ToArray(), d.Color) { Markers = MarkerStyle.Filled })
        .Where(series => series.Points.Count > 0).ToArray()
};
string seasonArcSvg = ChartSvg.Render(seasonArc);
// Interactive: <LumenChart Spec="seasonArc" FitWidth="true" />
```

- **One season, one X.** Each race's `X` is its index in the whole season, whatever its discipline, so the disciplines share one axis and `XMin = -0.5`, `XMax = seasonRaces.Length - 0.5` stand each race in the middle of its slot. Each date labels the axis, thinned at 340 so none touch.
- **Joined over the others, broken by its own gap.** A discipline's series holds only its own races: the races of the other disciplines between two of its races are simply absent, so its line joins them. A race of its own the rider did not finish is a `null` Y, a gap: the XCO line above breaks at 18 Apr and starts again. Never give an absent race a `null`, or every discipline breaks at every race it did not ride.
- **Ticks set by hand.** `YTickValues` (0.38.0) puts a gridline and a label exactly at 0, 50 and 100 and nowhere else; a tick's `Label` is written as given, and a tick without one writes its value in the axis's format. A value outside the axis is left out, never stretching it, so `YMin = 0` and `YMax = 100` hold the axis to the field; with `YReversed`, 0 stands exactly at the top. `YTickLabels` still chooses which of them are written.
- **The words a point says.** `YUnit = "%"` (0.38.0) writes the percentage after every value the axis writes and the place in its field follows as the `ValueNote`, so each mark's name and tooltip read `XCO: 1 Aug, 10% · P5/40`; the `Front`, `Mid` and `Back` labels are written as given, without the unit. The note is at most 20 characters, room for a field in the thousands.
- **Never colour alone.** The disciplines are told apart by colour on the line and by name everywhere else: the legend under the chart, each point's name and tooltip, and the shared readout if it is on. Against the card, `#161618`, XCC `#38bdf8` stands 8.44:1, XCO `#34d399` 9.40:1, XCM `#f59e0b` 8.41:1 and Other `#a78bfa` 6.64:1. On a white card they stand only 2.14, 1.92, 2.15 and 2.72 to 1, short of the 3:1 a line needs: there take `#0284c7` (4.10:1), `#047857` (5.48:1), `#b45309` (5.02:1) and `#7c3aed` (5.70:1).
- **A single race.** A discipline raced once, Other here, is one marker and no line; `Markers = MarkerStyle.Filled` keeps every race visible, since a refined line otherwise shows its markers only on hover.

## Gap to the lap leader

An invented race's eight riders lap by lap: each rider's time behind whoever led at the end of that lap, the leader at the top, each line named at its end instead of in a legend, and the reader's own line, "You", drawn over the others in a colour of its own and wider (0.38.0).

```csharp
// The app's lap times in seconds, one row a rider, the reader's own last. An invented race of eight riders over six laps:
string[] gapRiders = ["Rider A", "Rider B", "Rider C", "Rider D", "Rider E", "Rider F", "Rider G", "You"];
double[][] lapSeconds = [
    [300.5, 290.0, 299.5, 297.3, 290.7, 301.7], [293.9, 298.5, 302.8, 292.4, 302.0, 299.6], [297.5, 306.7, 296.2, 301.0, 305.1, 294.8],
    [308.6, 301.6, 300.0, 309.1, 298.5, 303.5], [307.5, 300.1, 311.0, 303.9, 302.5, 311.5], [302.1, 311.1, 309.8, 302.5, 313.5, 306.2],
    [310.0, 315.1, 304.5, 313.5, 312.1, 305.0], [307.8, 301.6, 298.6, 298.5, 293.3, 288.6]];
// Each rider's race clock at the end of each lap, lap 0 the start at 0, and how far behind whoever led at that lap.
double[][] lapClock = lapSeconds.Select(laps => laps.Aggregate(new List<double> { 0 }, (clock, lap) => { clock.Add(Math.Round(clock[^1] + lap, 1)); return clock; }).ToArray()).ToArray();
double[][] lapGaps = lapClock.Select(clock => clock.Select((time, lap) => Math.Round(time - lapClock.Min(other => other[lap]), 1)).ToArray()).ToArray();
var gapWords = new Axis(AxisKind.Linear, 0, 1) { ValueFormat = ValueFormat.Signed, Unit = "s" };   // writes +9.5s, as the axis does
string[] riderGreys = ["#D7DDE5", "#C2C6D2", "#B7BCC4", "#A9AEB7", "#9AA0A9", "#8D939C", "#80858E"];
ChartSeries GapLine(int rider)
{
    var you = rider == gapRiders.Length - 1;
    var behind = lapGaps[rider][^1];
    return new(gapRiders[rider], lapGaps[rider].Select((gap, lap) => new ChartPoint(lap, gap, lap == 0 ? "Start" : $"Lap {lap}")).ToArray(), you ? "#34d399" : riderGreys[rider])
    {
        StrokeWidth = you ? 3.2 : 2, Markers = MarkerStyle.None,
        EndLabel = you ? "You" : gapRiders[rider][^1..],                       // "A" to "G" fit a phone card; the series name says "Rider A"
        EndNote = behind == 0 ? "leader" : gapWords.Format(behind)
    };
}
var gapToLeader = new ChartSpec {
    Title = "Gap to the leader", Description = "Seconds behind the leader at each lap, the leader at the top",
    Kind = ChartKind.Line, Width = 340, Height = 320, Style = raceFace, XLabel = "Lap",
    YReversed = true, YMin = 0, YFormat = ValueFormat.Signed, YUnit = "s",
    Series = Enumerable.Range(0, gapRiders.Length).Select(GapLine).ToArray()   // "You" is listed last, so it is drawn over the others
};
string gapSvg = ChartSvg.Render(gapToLeader, includeLegend: false);
var gapLive = gapToLeader with { SharedReadout = true };
// Interactive: <LumenChart Spec="gapLive" FitWidth="true" ShowLegend="false" />
```

- **Gaps.** A rider's gap at a lap is their race clock less the least clock of any rider at that lap, so whoever leads stands at 0 and the lead can change hands, Rider B leading after lap 1 and Rider A after that. Lap 0, the start, is 0 for everyone. `YReversed = true` with `YMin = 0` puts 0 exactly at the top, the leader's place, and the axis fits the furthest behind.
- **Seconds with their sign.** `YFormat = ValueFormat.Signed` with `YUnit = "s"` (0.38.0) writes the ticks `0s`, `+25s`, `+50s` and every value said, `You: Lap 3, +18s`; `gapWords` writes the end notes the same way. A unit is written exactly as given, so `" bpm"` keeps its space.
- **End labels instead of a legend.** `EndLabel` and `EndNote` (0.38.0) write each rider's name and gap just right of the line's last point, in the line's colour where it clears 4.5:1 on the card and in the text colour where it does not, the note in the muted colour, `low` `#80858E` 4.87:1. Labels whose points end close together are moved apart, as little as they can be, 14 units a line, and a label moved off its point is joined to it by a short line in its colour. The right margin grows to hold the widest, up to half the drawing: at 340 that is about 10 characters, so the labels here are the riders' letters and "You", and a longer one is cut with `…`, its whole kept as its tooltip and accessible name. Each label and note is also said in its line's last point's name, `You: Lap 6, +8.7s, labelled You · +8.7s`. With every line labelled, `includeLegend: false` drops the legend; in the component, `ShowLegend="false"`.
- **Never colour alone.** The other riders are greys from `#D7DDE5` (13.22:1) down to `low` `#80858E` (4.87:1), which only tell the lines apart; "You" is the token `improved` `#34d399`, 9.40:1, and 3.2 units wide to their 2, and it is named "You" at its end and in every point's name, so neither the colour nor the width carries it alone. On a white card take `ChartStyle.Light`'s first series colour for "You" and its zone grey for the others.
- **Interactive.** `SharedReadout = true` reads all eight riders at the lap under a finger, the pointer or the focused point, `Lap 3 · Rider A 0s · Rider B +5.2s · … · You +18s`, and the arrow keys step lap by lap. `ShowLegend="false"` leaves out the component's legend of toggle buttons; with it, a reader can no longer hide a rider.
- **The readout uses series names.** The shared readout, the tooltips and each point's name read a rider by the series' `Name`, not its end label: name the reader's own series `"You"`, or tag it, `"Sam (you)"`, so the readout says whose line it is. The end label can still say just `You`.
- **Riders who stop mid-race.** A rider who did not finish has fewer laps: give that series only the laps they rode, rather than `null` for the rest. The line ends early, and its end label stands at its last point, mid-plot, moved apart only from labels whose lines end close by; its last point's name says the label, so the reader hears where it stopped.
- **A moving lead.** Each lap's gaps are measured from whoever led that lap, so the rider at `0s` at a lap is that lap's leader, who need not be the eventual winner: Rider B leads after lap 1 above and finishes behind Rider A. Say so where the page names a winner, and write the winner's own note, `leader` here, from the last lap only.

## Effort zones

An invented ride's time in four heart-rate zones, counted from its samples by the share of the rider's maximum heart rate each falls in, drawn as one strip of shares: each zone as long as its share of the ride, a gap in the card colour between each two, and a key under the bar of every zone and its whole percentage (0.39.0). The card writes its own heading, so the chart draws neither its title nor its description, which stay its accessible name.

```csharp
// The app's heart-rate samples for one ride, one a second (NaN where the strap dropped out), and the rider's maximum heart rate. An
// invented hour, worked out from sines so it is the same on every run:
const double effortMaxHeart = 192;
var effortHeart = Enumerable.Range(0, 3600).Select(t => t is >= 2100 and < 2130 ? double.NaN
    : Math.Round(118 + 28 * Math.Sin(t / 600.0 - 1.4) + 38 * Math.Pow(Math.Max(0, Math.Sin(t / 280.0)), 3) + 5 * Math.Sin(t / 37.0))).ToArray();
// Zones by the share of the maximum: Easy below 60 %, Moderate 60–74 %, Hard 74–87 % and Very hard from 87 %. Heart rate is in whole
// beats, so each zone holds every beat below its upper share: Easy to 115 at a maximum of 192, Moderate to 142, Hard to 167.
double BelowShare(double share) => Math.Ceiling(share * effortMaxHeart) - 1;
var effortZones = new ZoneScale([new("Easy", BelowShare(.60), "#3FD17A"), new("Moderate", BelowShare(.74), "#D7DDE5"),
    new("Hard", BelowShare(.87), "#F5B642"), new("Very hard", double.PositiveInfinity, "#E30613")]);
var effortSeconds = Training.TimeInZone(effortHeart, effortZones);          // seconds in each zone; the dropout counts in none
var effortStrip = new ChartSpec {
    Title = "Effort zones", Description = "Time in each heart-rate zone, by the share of your maximum",
    Kind = ChartKind.Strip, Width = 340, Style = raceFace, DrawTitles = false, YFormat = ValueFormat.Duration,
    Series = [new("Time in zone", effortZones.Zones.Select((zone, i) => new ChartPoint(i, effortSeconds[i], zone.Name) { Color = zone.Color }).ToArray())]
};
string effortSvg = ChartSvg.Render(effortStrip);
// Interactive: <LumenChart Spec="effortStrip" FitWidth="true" />
```

- **One whole.** `ChartKind.Strip` takes one series whose points are the parts in order: each point's `Label` names its zone and its `Y` is its seconds, zero or more. X is only their order. Each zone is as long as its share of the total, so the strip always runs the card's width less 24 units each side, the four zones together.
- **The key.** Under the bar each zone is written with its swatch and its share, `Easy 32%`, in whole percentages that add up to exactly 100: each share is rounded down, and the points left over go to the largest remainders, so a ride of three equal thirds reads 33, 33 and 34, never 99 or 101. The entries flow left to right and wrap where the card is too narrow: at 340 the four take two rows. A zone with no time keeps its entry, `Hard 0%`, and draws no part.
- **Height.** A strip is drawn as tall as its content, the bar 18 units thick and 20 units a row of the key, so `Height` is not used; with `DrawTitles = false` the bar stands 14 units from the top and two rows of the key make the drawing 92 units tall. With its title and description drawn it is 50 units taller.
- **The words a part says.** Each part drawn is a focusable mark named, and tooltipped, `Moderate: 44%, 26:15`: its share, and its seconds in `YFormat`, `ValueFormat.Duration`. The arrow keys step from part to part. A zone of no time has no mark; its key entry names it.
- **Pass amounts, not shares.** The key's share is of the strip's total, rounded to whole percentages that add up to exactly 100, so pass the raw amounts, the seconds, rather than shares the app has already rounded. Rounded shares that add up to 99 or 101, such as 33, 33 and 33, are shared out again over their own total and can differ from the amounts by a point: the key reads `34%` where the amount said 33. Where amounts are shares that add up to 100, `YUnit = "%"` with `YFormat` left as a number, a part whose amount writes exactly as its share is named once, `Moderate: 30%`, not `Moderate: 30%, 30%` (0.40.0); one that differs keeps both, `Hard: 34%, 33%`.
- **Never colour alone.** Neighbouring zones stand close in lightness, Easy `#3FD17A` against Moderate `#D7DDE5` 1.45:1, Moderate against Hard `#F5B642` 1.32:1 and Hard against Very hard `#E30613` 2.71:1, so each part is parted from the next by a 2-unit gap in the card colour, against which every zone clears 3:1: Easy 9.13:1, Moderate 13.22:1, Hard 10.03:1, and Very hard 3.70:1, which a filled part needs but small text would not, so the red is a fill only. The key names every zone in words, in the text colour, `hi` `#F5F6F7` 16.70:1, and each part's name says its zone.
- **The heading.** `DrawTitles = false` (0.39.0) draws neither the title nor the description, and the bar moves up into their room; both stay the drawing's `<title>`, `<desc>` and accessible name, so the page's own heading and the chart agree. `Render`'s `includeTitles: false` is something else: it leaves out the native tooltip in each mark.
- **The ends.** Both outer ends are rounded by the style's `BarRadius`, or 6 units unless the style sets one, clamped to half the bar's thickness, so `BarRadius = 9999` draws a capsule.

A cadence split is the same strip of two parts: the seconds pedalling, cadence above zero, and the seconds coasting.

```csharp
// The app's cadence samples for the same ride, one a second, 0 while coasting. Invented: coasting on every descent of the road.
var effortCadence = Enumerable.Range(0, 3600).Select(t => Math.Sin(t / 210.0) < -0.55 ? 0 : Math.Round(86 + 6 * Math.Sin(t / 47.0))).ToArray();
var cadenceSplit = new ChartSpec {
    Title = "Cadence split", Description = "Time pedalling and time coasting",
    Kind = ChartKind.Strip, Width = 340, Style = raceFace, DrawTitles = false, YFormat = ValueFormat.Duration,
    Series = [new("Cadence", [new(0, effortCadence.Count(rpm => rpm > 0), "Pedalling") { Color = "#D7DDE5" },
        new(1, effortCadence.Count(rpm => rpm == 0), "Coasting") { Color = "#80858E" }])]
};
string cadenceSvg = ChartSvg.Render(cadenceSplit);
```

Pedalling is the steel `data` token, 13.22:1 on the card, and coasting the `low` grey, 4.87:1; the two stand 2.71:1 apart, and the gap between them parts them.

## Score bars

Four invented scores out of 100, each a bar on a track that runs to 100, its value written just past the track's end, as a meter reads (0.39.0). What each score means is in its name and its number; the bars are all one colour.

```csharp
// The app's scores for one race, each out of 100. Invented:
(string Name, double Score)[] raceScores = [("Execution", 82), ("Improvement", 64), ("Effort", 91), ("Consistency", 58)];
var scoreBars = new ChartSpec {
    Title = "Race scores", Description = "Execution, improvement, effort and consistency, each out of 100",
    Kind = ChartKind.Bar, Width = 340, Height = 240, Style = raceFace with { Gridlines = GridLine.Hidden }, DrawTitles = false,
    YMin = 0, YMax = 100, BarTrack = true, YTickLabels = TickLabels.None, FitHeight = true,
    Series = [new("Score", raceScores.Select((s, i) => new ChartPoint(i, s.Score, s.Name)).ToArray(), "#D7DDE5") { ValueLabels = true }]
};
string scoreSvg = ChartSvg.Render(scoreBars, includeLegend: false);
// Interactive: <LumenChart Spec="scoreBars" FitWidth="true" ShowLegend="false" />
```

- **Tracks.** `BarTrack = true` (0.39.0) draws a track behind each bar from zero to `YMax`, in the style's `Grid` colour, `#2D2D2F`, rounded as the bar is, so 64 reads as a bar filled 64 % of the way along its track. It needs `YMax`, an axis from zero (`YMin` unset or 0, no value below zero) and every series on the left-hand axis; a score above `YMax` is drawn at the track's end and its name says so, `Score: Effort, 104, above the scale, drawn at 100`.
- **Layout.** A bar on a track is at most 18 units thick, centred in its row. The left margin fits the widest name, `Improvement` here, rather than the fixed 160 a bar chart keeps, up to 45 % of the width; a longer name is cut with `…`, its whole in its bar's name. The right margin grows to hold the widest value label. `YTickLabels = TickLabels.None` with no `YLabel` writes nothing under the plot, so its bottom margin narrows from 76 to 24, and `DrawTitles = false` moves the plot up 50 units.
- **No spare room.** `FitHeight = true` (0.41.0) draws the card as tall as its rows need instead of `Height`: 36 units a row on tracks, 28 above them with the titles undrawn and 24 under them with nothing written there, so these four scores are 28 + 4 × 36 + 24 = 196 units tall, and three would be 160, where `Height = 240` gives three rows about 63 units each, an 18-unit bar in each, and leaves the card mostly air. The height is worked out the same way every time, so a card of three scores and one of four line up row for row. `Height` is still checked but not used, and the 240-unit floor every other chart keeps does not apply. A sub-label under any name makes every row 38.
- **Never colour alone.** Every bar is the steel `data` token, `#D7DDE5`, 10.05:1 against its track, so the fill's end reads plainly; the track stands only 1.31:1 on the card, which is fine, since it only shows how far the scale runs and the value written past its end says the number. The value labels are written in the text colour, `hi`, 16.70:1. A score that is good or bad is said by its name and number; colour it only beside words that say why, as a personal best's ring is paired with ` · PB`.
- **Columns.** On a `ChartKind.Column` chart the tracks stand upright, from zero to the top of the plot, and each value label stands above its track.

## Best efforts

A ride's best average power at five durations, 5 seconds to an hour, as columns: each its watts above it and its watts per kilogram on a second line under its duration (0.40.0). It is the power–duration curve read at five points; the curve itself is "Power–duration curve with critical power" in `sports.md`, and a Race Face version of it follows.

```csharp
// The app's power samples for one ride, one a second, and the rider's weight in kilograms. An invented ride of an hour and a half:
// steady riding with a sprint every ten minutes, a one-minute and a five-minute effort, and a twenty-minute block.
const double bestEffortKg = 52;
var bestEffortWatts = Enumerable.Range(0, 5400).Select(t => Math.Round(
    t % 600 < 5 ? 640 + t / 30.0                              // a sprint every ten minutes, the last the strongest
    : t is >= 900 and < 960 ? 420                              // one minute hard
    : t is >= 1500 and < 1800 ? 290                            // five minutes
    : t is >= 2400 and < 3600 ? 238 + 6 * Math.Sin(t / 90.0)   // twenty minutes at threshold
    : 170 + 25 * Math.Sin(t / 300.0) + 10 * Math.Sin(t / 23.0))).ToArray();
// The durations the card shows, with the names written under each column.
(double Seconds, string Name)[] bestEffortDurations = [(5, "5s"), (60, "1m"), (300, "5m"), (1200, "20m"), (3600, "60m")];
var bestEffortCurve = Training.MeanMaximal(bestEffortWatts, bestEffortDurations.Select(d => d.Seconds));   // a ride shorter than an hour leaves 60m out
var bestEfforts = new ChartSpec {
    Title = "Best efforts", Description = "Best average power at five durations, in watts, with watts per kilogram under each",
    Kind = ChartKind.Column, Width = 340, Height = 260, Style = raceFace with { BarRadius = 3, Gridlines = GridLine.Hidden }, DrawTitles = false,
    YTickLabels = TickLabels.None, XLabel = "W/kg under each duration",
    Series = [new("Best power", bestEffortCurve.Select((effort, i) => new ChartPoint(i, Math.Round(effort.Value), bestEffortDurations.First(d => d.Seconds == effort.Seconds).Name)
        { SubLabel = (effort.Value / bestEffortKg).ToString("0.0", CultureInfo.InvariantCulture) }).ToArray(), "#38bdf8") { ValueLabels = true }]
};
string bestEffortsSvg = ChartSvg.Render(bestEfforts, includeLegend: false);
// Interactive: <LumenChart Spec="bestEfforts" FitWidth="true" ShowLegend="false" />
```

- **The numbers.** `Training.MeanMaximal` finds, for each duration, the best average over any stretch of the ride that long, so `5s` is the best sprint and `20m` the twenty-minute block. Pass the durations the card shows; a duration longer than the ride is left out of the curve, which is why each point finds its name by its seconds rather than by its place. When the app already stores its best efforts, draw those instead, so the card and the app agree.
- **Sub-labels.** `ChartPoint.SubLabel` (0.40.0) writes a second line under each column's name, at 11 px in the muted colour, `low` `#80858E` 4.87:1 on the card, and each mark's name and tooltip read it after the name: `Best power: 5s · 15.4, 800`. A sub-label is thinned with its name, the wider of the two keeping neighbouring columns apart, and at 340 five columns stand about 47 units apart, room for a number such as `15.4` but not for `15.4 W/kg`, which would leave every other column's words out. So the unit is written once, in the axis title `W/kg under each duration`, and the description says it for a screen reader. Three columns or fewer keep `15.4 W/kg` whole.
- **Columns.** The columns are the dashboard's sky blue, `#38bdf8`, 8.44:1 on the card, and `BarRadius = 3` rounds their tops by 3 units. `ValueLabels = true` writes each column's watts above it in the text colour, `hi`, 16.70:1; a label wider than its column is left out, its value kept in the mark's name, so a sprint over 999 W still fits at 340, four digits standing 27 units wide in a column 34 wide.
- **Axis.** A column's length is its value, so a column chart's axis always starts at zero: `YMin` above zero is refused. With the values written on the columns, `YTickLabels = TickLabels.None` and hidden gridlines leave the plot to the columns.
- **Never colour alone.** One colour for every column: the duration under it and the number on it say which is which.

The same efforts as a curve on a logarithmic duration axis, every standard duration from a second to the ride's length, in the same style:

```csharp
var bestEffortCurveChart = new ChartSpec {
    Title = "Power–duration curve", Description = "Best average power for every duration, 1 second to 90 minutes",
    Kind = ChartKind.Line, Width = 340, Height = 260, Style = raceFace, DrawTitles = false,
    XAxis = AxisKind.Log, XFormat = ValueFormat.Duration, YLabel = "Power (W)",
    Series = [new("Best power", Training.MeanMaximal(bestEffortWatts, Training.StandardDurations)
        .Select(effort => new ChartPoint(effort.Seconds, Math.Round(effort.Value))).ToArray(), "#38bdf8") { Markers = MarkerStyle.Filled }]
};
string bestEffortCurveSvg = ChartSvg.Render(bestEffortCurveChart, includeLegend: false);
```

The ticks read `1s`, `10s`, `1m`, `10m` and `1h`, a decade apart, so the sprint and the hour each get room. Add `Training.CriticalPower` as a reference line where the ride has efforts of 3 to 20 minutes, as `sports.md` does.

## Heart rate per lap

An invented race's laps as columns of their average heart rate, each filled by value from amber at its base towards red, the hardest lap reaching full red, its lap's name under it and its heart rate under that (0.40.0).

```csharp
// The app's laps for one race: each lap's average heart rate in whole bpm. An invented race of four laps:
double[] raceLapHeart = [152, 161, 168, 174];
var lapHeartColumns = new ChartSpec {
    Title = "Heart rate per lap", Description = "Average heart rate in each lap, from 152 to 174 bpm",
    Kind = ChartKind.Column, Width = 340, Height = 260, Style = raceFace with { Gridlines = GridLine.Hidden }, DrawTitles = false,
    YTickLabels = TickLabels.None,
    Series = [new("Heart rate", raceLapHeart.Select((bpm, lap) => new ChartPoint(lap, bpm, $"L{lap + 1}")
        { SubLabel = bpm.ToString("0", CultureInfo.InvariantCulture) + " bpm" }).ToArray())
        { Gradient = [new(0, "#f59e0b"), new(raceLapHeart.Max(), "#f87171")] }]
};
string lapHeartSvg = ChartSvg.Render(lapHeartColumns, includeLegend: false);
// Interactive: <LumenChart Spec="lapHeartColumns" FitWidth="true" ShowLegend="false" />
```

- **Filled by value.** `ChartSeries.Gradient` (0.40.0, on columns) lays one gradient along the value axis, shared by every column: its first stop, amber `#f59e0b`, stands at 0, the columns' base, and its last, red `#f87171`, at 174, the hardest lap, and past either end the stop's colour carries on. So each column takes, at each height, the colour of the heart rate drawn there: every column shades from amber at its base towards red all the way up, the hardest lap reaching full red at its top and the gentlest stopping short of it, a little more orange, so a taller column reaches further along the colours as it does along the axis. Put the first stop at 0, where the columns stand: a first stop above 0, at the gentlest lap say, leaves every column's lower part one flat colour, here amber up to 152 bpm, and the change only in a thin cap at the top, which reads as a rendering fault rather than as effort. Put the last stop at the hardest lap, worked out from the laps as here, or at a heart rate that means something, such as the top of the rider's zones; the stops must rise strictly, so the hardest lap must be above 0.
- **From zero.** A column's length is its value, so a column chart's axis starts at zero, and `YMin` above zero is refused: 152 and 174 bpm differ by 14 %, and the columns show that. An axis from 140 would draw the hardest lap nearly three times as tall as the gentlest, which is what a truncated axis does. The colour and the `bpm` under each lap say which laps were hard without misstating how much harder.
- **Two lines under each lap.** `L1` is the point's `Label`; `SubLabel` writes `152 bpm` under it, in the muted colour at 11 px, `low` `#80858E`, 4.87:1 on the card. The plot gives up 14 units at its foot for the second line. At 340 four laps keep `152 bpm` whole; a race of more laps leaves every other lap's words out where two would touch, so write the number alone, `152`, which fits about eight, and say `bpm` in the description.
- **Never colour alone.** The colour repeats what the height and the words say: each mark's name and tooltip read `Heart rate: L3 · 168 bpm, 168`. Against the card the amber stands 8.41:1 and the red 6.53:1, and every blend between them at least 6.53:1, well past the 3:1 a filled mark needs. On a white card take darker stops, `ChartStyle.Light`'s zone gold `#A88200` (3.58:1) and red `#DD4B45` (4.06:1).
- **No value labels.** The sub-labels already write each lap's heart rate, so `ValueLabels` is left off and `YTickLabels = TickLabels.None` with hidden gridlines leaves the plot to the columns. With value labels on, each would be written above its column in the text colour.

## Team rider

An invented team rider's season, round by round: in each round the rider rode, the share of their category they finished ahead of, their place and the field after it; a round they were entered in and missed is written `absent` at the foot of the plot rather than left blank (0.42.0).

```csharp
// The app's results for one team rider: each held round's number, the rider's place, null for a round the rider missed, and the size
// of the category's field. Round 4 was not held, so it is not here at all. An invented season:
(int Round, int? Place, int Field)[] teamRounds = [(1, 9, 38), (2, 14, 41), (3, 5, 37), (5, 7, 40), (6, null, 39), (7, 3, 36), (8, 6, 42)];
// The share of the category the rider finished ahead of: the riders behind over everyone else in the field, 100 for first and 0 for last.
static double TeamShare(int place, int field) => Math.Round(100d * (field - place) / (field - 1), MidpointRounding.AwayFromZero);
static string TeamOrdinal(int place) => place + (place % 100 is 11 or 12 or 13 ? "th" : (place % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
var teamRider = new ChartSpec {
    Title = "Team rider", Description = "Share of the category finished ahead of, round by round; round 4 was not held",
    Kind = ChartKind.Line, Width = 340, Height = 260, Style = raceFace, DrawTitles = false,
    XMin = -0.5, XMax = teamRounds.Length - 0.5,
    YMin = 0, YMax = 100, YUnit = "%", YTickValues = [new(0, "0%"), new(50, "50%"), new(100, "100%")],
    Series = [new("Share", teamRounds.Select((r, i) => r.Place is int place
        ? new ChartPoint(i, TeamShare(place, r.Field), $"Round {r.Round}") { ValueNote = $" · {TeamOrdinal(place)} of {r.Field}" }
        : new ChartPoint(i, null, $"Round {r.Round}") { GapLabel = "absent", Color = "#e0a800" }).ToArray(), "#22d3ee")
        { StrokeWidth = 2, Markers = MarkerStyle.Filled }]
};
string teamRiderSvg = ChartSvg.Render(teamRider);
// Interactive: <LumenChart Spec="teamRider" FitWidth="true" />
```

- **Missed, or not held.** A round the rider was entered in and did not ride is a missing value: a point with a `null` Y. The line breaks there, since nothing was ridden to join, and `GapLabel = "absent"` writes the word where the value would be. A round that was not held at all is not a missing value, so it has no point: X counts the held rounds, round 5 stands next to round 3, and the line joins them. Give a cancelled round a `null` Y instead and the line would break for a round nobody rode; give a missed round no point and the season would read as if the rider had ridden every round.
- **The word.** `GapLabel` (0.42.0), 1 to 12 characters on one line, is written at 11 px and weight 600 at its round's X, centred on it and moved in from the plot's sides so it is never cut, just inside the plot beside the value axis's start: above the bottom edge here, or below the top edge on a reversed axis. It stands on a copy of itself stroked in the card's colour, as a value label does, so the `0%` gridline never cuts it, and it keeps clear of the labels written before it; where it has no room it is left out and the word stays in its mark's name. Lines, areas and scatter series take it, on a point whose Y is `null`; sparklines and other kinds refuse it.
- **Its colour.** The word takes the point's `Color` where that clears 4.5:1 on the card, else the series colour where that does, else the text colour, `hi`. Gold `#e0a800` stands 8.41:1 on the card `#161618`, so the word is gold; the cyan line `#22d3ee` stands 10.00:1, and its markers are filled.
- **Named and reached.** The absent round is a focusable mark like the others, an invisible box round its word, named and tooltipped `Share: Round 6, absent`, the word in place of `missing`, with the point's `ValueNote` after it if it has one. The arrow keys stop on it, the component's shared readout reads it `Share absent`, and its status line and data table say it. CSV leaves its value empty.
- **Values and names.** `YUnit = "%"` writes `89%` after each value, and `ValueNote` adds the place and field, so a round reads `Share: Round 3, 89% · 5th of 37`. `YTickValues` labels 0, 50 and 100 as given. At 340 the axis writes every other round's name so none touch (`Round 1`, `Round 3`, `Round 6`, `Round 8`), and every round keeps its name in its mark; label the points `R3` instead and all seven fit, at the cost of names that read `Share: R3, 89% · 5th of 37`.
- **Never colour alone.** `absent` is a word, not only a gold mark, and the series is named in its legend and in every mark's name.
- **Keys.** From 0.42.0 a card whose drawing fits it is one tab stop, the roving point; its viewport takes a stop of its own, as a `role=region`, only while the drawing is wider than the card and scrolls.

## Category heatmap

An invented history of four categories over four seasons as a table of colour: a row for each category, a column for each season, each cell its points per rider-start with its starts under it, and a cell with too few starts marked "not rated" instead of coloured as if it were a result (0.46.0).

```csharp
// The app's results by category and season: the points its riders earned, the starts they came from and the riders who made them.
// An invented history in which the relay did not run in 2023:
(string Category, (int Points, int Starts, int Riders)[] Seasons)[] categoryHistory = [
    ("Sprint", [(34, 12, 5), (41, 14, 6), (45, 13, 6), (52, 15, 7)]),
    ("Middle distance", [(27, 11, 5), (30, 12, 5), (38, 14, 6), (36, 13, 6)]),
    ("Long distance", [(8, 4, 3), (24, 10, 4), (19, 11, 4), (31, 12, 5)]),
    ("Relay", [(0, 0, 0), (20, 10, 5), (14, 9, 6), (25, 10, 5)])];
const int categoryFirstSeason = 2023, categoryRatedStarts = 10;      // a cell with fewer starts than this is not rated
var categoryHeatmap = new ChartSpec {
    Title = "Points per start, by category", Description = "Points per rider-start in each category and season; a dashed cell has too few starts to rate",
    Kind = ChartKind.Heatmap, Height = 320, Style = raceFace, CellText = true, CellWidth = 72,
    Series = categoryHistory.Select(category => new ChartSeries(category.Category, category.Seasons.Select((season, i) =>
        new ChartPoint(i, season.Starts > 0 ? Math.Round((double)season.Points / season.Starts, 1) : null, $"{categoryFirstSeason + i}")
        {
            SubLabel = $"/{season.Starts} starts",
            ValueNote = season.Starts > 0 ? $" · {season.Points} pts, {season.Riders} riders" : null,   // brings its own separator
            NotRated = season.Starts < categoryRatedStarts ? "too few starts to rate" : null
        }).ToArray())).ToArray()
};
string categorySvg = ChartSvg.Render(categoryHeatmap);           // 453 wide: 165 + four seasons of 72
string categoryTable = ChartExport.HtmlTable(categoryHeatmap);   // the same cells as a table, rows by columns, for a static page and screen readers
// Interactive: <LumenChart Spec="categoryHeatmap" FitWidth="true" />   ("View data" shows the table)
```

- **Each cell.** `CellText = true` writes the value, 11 px at weight 600, and under it the point's `SubLabel`, 10 px, centred. Every word is in the style's text colour here, `hi` `#F5F6F7`, which clears 4.5:1 on every cell, 4.51:1 at the least on the brightest red `#E30613`; where neither the text nor the background colour reaches 4.5:1 on a cell, Lumen writes it in black or white instead, so no cell is left without its number. A sub-label that does not fit the cell is dropped first, then the value, and the cell's name always says both. `ValueNote` is never drawn in a cell: it is in the cell's name and in the table.
- **Names.** Each cell is a focusable mark named as a column chart's is: its row, its column, then ` · ` and its sub-label, then its value and its `ValueNote` as written, `Sprint: 2023 · /12 starts, 2.8 · 34 pts, 5 riders`. The note brings its own separator, so write it (`" · 34 pts, 5 riders"`); a note without one would run on from the value. A not-rated cell adds `, not rated: ` and its reason.
- **Not rated.** Whether a cell is rated is the app's rule, here fewer than ten starts: `NotRated = "too few starts to rate"` (1 to 24 characters, no line breaks). Three cells are not rated. Long distance in 2023 has four starts and a value, `Long distance: 2023 · /4 starts, 2 · 8 pts, 3 riders, not rated: too few starts to rate`; Relay in 2025 has nine starts; and Relay in 2023 had none, so its `Y` is `null`: the cell is still drawn, writes `—`, and is named without a value, `Relay: 2023 · /0 starts, not rated: too few starts to rate`. A `null` `Y` without `NotRated` draws no cell at all. A not-rated cell is unshaded (the card's colour), outlined with a dash in `Muted` at full opacity (4.87:1 on the card), and left out of the colour scale: `Color scale: 1.7 low to 3.5 high` reads the thirteen rated cells, and the 1.6 of Relay in 2025 does not pull it lower.
- **Never colour alone.** The numbers say what the colours say, and the dashes and the words say what the colour of a not-rated cell cannot. The style's ramp starts at the panel colour `#1E1F22`, which stands only 1.10:1 off the card `#161618`, so the lowest cells, Long distance in 2025 here, are told apart by their hairline outline and their number, and a not-rated cell from them by its dashes. The scale line is words too: the refined finish writes `{min} low to {max} high`, which is right on any ramp, where 0.45.0 wrote `(light) to (dark)`.
- **Width.** `CellWidth = 72` gives each season 72 pixels, enough for `/15 starts` under its value (by the library's estimate the words need 66, and a column of 64 drops them), so the drawing is 165 + 4 × 72 = 453 wide whatever `Width` says (it is not used, so only `Height` is set; the rows share what is left, about 34 units each here, and a row needs about 29 for both lines). A column too narrow for its words drops the sub-label first and then the value; 24 is the least `CellWidth`, and more than 4096 in all is refused with the number of columns and pixels. `<LumenChart>` shows the drawing at exactly its own size, with or without `FitWidth`: in a card narrower than 453 pixels it scrolls sideways (its viewport is then a `role=region` stop, as any scrolling chart's is) and it is never squeezed or stretched. A page that draws the static SVG must keep it from squeezing itself, since the root is `width:100%`: put it in a box that scrolls and give the inner one the drawn width, `<div style="overflow-x:auto"><div style="min-width:453px">…svg…</div></div>`; in a test page 375 pixels wide with 8 pixels of padding each side the bare SVG shrank to 359 and the wrapped one kept 453 and scrolled. On a phone the row names scroll away with the cells, which is what the table is for.
- **The table.** `ChartExport.HtmlTable(spec)` returns a `<table class='lumen-grid-table'>` of the same cells, the seasons across the head and a row for each category, each `<th scope='row'>`, and each cell the value, the `ValueNote`, ` · ` and the sub-label, then `, not rated: …` where it applies: `2.8 · 34 pts, 5 riders · /12 starts`, `— · /0 starts, not rated: too few starts to rate`. Every word is HTML-encoded, so the `·` in a note is written `&#183;`. It is a bare table: on a static page wrap it in the region `<LumenChart>`'s "View data" uses, so a table taller than its box can be scrolled from the keyboard, and link `lumen.css`, which styles the wrapper and the grid and gives the wrapper its focus ring:

```html
<div class="lumen-table" tabindex="0" role="region" aria-label="Chart data">…ChartExport.HtmlTable(categoryHeatmap)…</div>
```

- **Data.** `ChartExport.Csv(spec)` writes a `Note` column, since cells carry notes, and after it a `NotRated` column, since some cell is not rated, empty for the rated ones; it has no column for sub-labels. The invented history above stands for the app's own: Lumen does not work out starts, riders or points per start, and does not decide which cells are rated.

## Season planner

Organizers choosing a date see the year at a glance: months aligned by weekday so the weekends line up down the page, public holidays, school holidays and long weekends marked, and other organizers' events drawn by how much they compete with yours — the app decides that, Lumen only draws it. Zoom by drawing a month or a day. Every event is invented here. The planner is for PCs and tablets, so the wide layout is the main one (0.43.0). In a Blazor app `<LumenPlanner>` (0.44.0) is the interactive version: the reader zooms from the year to a month to a day, steps and filters, and it chooses its own layout and width; the static calls below are for pages without it, for email and for the server.

```csharp
// The app's regions, holidays and events. The relevance is the app's own rule, worked out for the organizer viewing:
// same day, same province and same discipline or audience is a clash; an adjacent weekend or a neighbouring province is close.
PlannerRegion[] seasonRegions = [new("ZA", "South Africa"), new("ZA-GP", "Gauteng", "ZA"), new("ZA-WC", "Western Cape", "ZA")];
PlannerPeriod[] seasonDays =
[
    new(new(2027, 4, 27), null, "Freedom Day", PeriodKind.PublicHoliday, "ZA"),
    new(new(2027, 6, 26), new DateOnly(2027, 7, 18), "Invented school holiday", PeriodKind.SchoolHoliday, "ZA")
];
PlannerEvent[] seasonEvents =
[
    new("e1", "Hilltop XCO", new(2027, 3, 13)) { Region = "ZA-GP", Category = "XCO", Audience = "Kids", Relevance = PlannerRelevance.Clash },
    new("e2", "Coast Stage Race", new(2027, 3, 12)) { End = new DateOnly(2027, 3, 14), Region = "ZA-WC", Category = "Stage", Status = PlannerStatus.Provisional },
    new("mine", "Our Spring Enduro", new(2027, 9, 18)) { Region = "ZA-GP", Category = "Enduro", Mine = true }
];
var seasonPlanner = PlannerSpec.ForYear(2027) with
{
    Title = "Season planner", Description = "Gauteng and the country's holidays", Style = raceFace, Width = 1100,
    Regions = seasonRegions, Periods = seasonDays, Events = seasonEvents,
    Filter = new() { Regions = ["ZA-GP"] }   // the organizer's province; the country's holidays still show, Western Cape's events drop out
};
string seasonYear = PlannerSvg.Render(seasonPlanner, PlannerView.WholePeriod);
string seasonMarch = PlannerSvg.Render(seasonPlanner, PlannerView.Month(2027, 3));
string seasonDay = PlannerSvg.Render(seasonPlanner, PlannerView.Day(new(2027, 3, 13)));
string seasonPhone = PlannerSvg.Render(seasonPlanner with { Width = 340 }, PlannerView.WholePeriod, PlannerLayout.Narrow);
string seasonTable = PlannerSvg.Table(seasonPlanner, 2027, 3);   // the month for screen readers and static pages
```

The same planner in a Blazor page with an interactive render mode, beside those static calls (a razor block, not compiled by the recipe check):

```razor
<LumenPlanner Spec="seasonPlanner" EventSelected="OnEvent" DaySelected="OnDay" />

@code {
    // seasonPlanner is the PlannerSpec built above; the component draws it at its box's width and zooms it itself.
    void OnEvent(PlannerEvent e) { /* open the event's page, e.Url or e.Id */ }
    void OnDay(DateOnly day) { /* offer the day for a new event */ }
}
```

- **Relevance is yours to decide.** `Clash`, `Near` and `Other` are drawn by weight and dash, not colour: a clash bold and solid, a close event dashed, any other thin and muted. Each is said in words in the event's name and tooltip, `Hilltop XCO, Saturday 13 March 2027, Gauteng, XCO, Kids, clash`, and a `Near` one says `close`. The planner never computes relevance, so the rule can change without a Lumen release. A cancelled event counts as neither clash nor close in any busy count (the year's weekly counts, a phone's month slots and summaries), though it is still drawn and named as cancelled.
- **The year.** Twelve rows, one per month, aligned by weekday on 37 columns so every Saturday and Sunday stands in one column down the page; the month and its year are written on two lines. Weekends are bands, public holidays diamonds, school holidays a band along the top of their days, other periods (`PeriodKind.Other`: exams, a large outside event) a dotted band under it, and a long weekend a bracket under the stripes (derived: weekend days and public holidays joined, three days or more, at least one a holiday). Each event is a stripe across its days, in the lowest of three lanes free on all of them; a day with more writes `+N`, which names the ones it hid, and each week writes one number, its clashes and close events counted together, each named apart in words for a screen reader.
- **The month and the day.** A month is a grid of weeks with each day's events under it as lines of words, `Name · word · day N of M · Region code`: a multi-day event is listed under every day it covers, its line ending `day 2 of 3`; a line too long for its cell is cut at its end with `…`, so the region code goes first, the whole text staying in its tooltip and name, and a full day writes `+N more` naming the rest. A day is a list of its holidays and events with their region, category, audience, status and relevance in words.
- **Regions.** A filter on a province still shows the country's holidays and the events set for the whole country or for no region; events in other provinces drop out. Filter by `Categories`, `Audiences`, `Statuses` and `Relevances` the same way, and leave `Filter` null to see everything.
- **Phones.** `<LumenPlanner>` draws the narrow layout by itself in a box under 640 pixels and the wide one from there; on a 375 px phone its toolbar and filter chips stand above the drawing, so `ShowFilters="false"` with the host's own filters set through `Spec.Filter` gives the drawing more room. Without the component, for a box under 640 pixels draw `PlannerLayout.Narrow` at the box's width (`Width = 340` here): the year becomes a bar of weekend slots per month, a weekend across a month's end shown in both bars and named by its full span, with the month's clash and close events counted once each in words; a month becomes an agenda of only the days that hold something.
- **Sizes.** The drawing's text is 10 to 12 units, so for readable text on screen the drawing's width must equal the box's CSS width. `<LumenPlanner>` measures its box and draws at that width, from 320 to 4096 pixels (it has no `FitWidth`; it always fits), and is drawn at `Spec.Width` and scaled only until it is interactive. For the static calls, render `Width` equal to the box's CSS width.
- **Not colour.** Provisional is an outline with hatching, cancelled an outline with a strike line, yours an outline in the text colour, and every one is also said in words. On the card, words clear 4.5:1 (`hi` 16.70:1, `low` 4.87:1) and every mark 3:1, including on a weekend band, where `hi` stands 12.70:1 and `low` 3.71:1; check a style of your own with `ContrastIssues()`.
- **Limits.** View only, no editing, dragging or adding; at most 400 days, 2000 events, 1000 periods and 500 regions, and 200 characters in a title, description or name; at most three stripes a day in the year; a month's lines cut long names; `PlannerSvg.Table` is the month's words for a static page. Interactive zoom has arrived with `<LumenPlanner>` (0.44.0), which needs an interactive render mode, and a keyboard reaches an event in the year or a month by opening its day; a page without the component draws the view it asks for.

## Rendering notes

- **Static:** `ChartSvg.Render(spec)` at `Width = 340` (or the card's own width) is the whole chart: labels, colours, tooltips and names, with no script. Write it into the page, or rasterise it on the server with an SVG library of your choice; Lumen ships no PNG or PDF renderer. Each line's value label stands on a copy of itself stroked in the background colour, not on SVG 2's `paint-order`, so rasterisers without it draw it the same.
- **Interactive:** `<LumenChart Spec="recommended" FitWidth="true" />` measures its card and redraws at that width, never below 320 px; before it is interactive it is drawn at `Width` and scaled to fit. `Height` is kept, so choose one that reads at a phone's width.
- **Gaps:** a race with no place or no points is `null`, never zero: the line breaks, no mark or label is drawn, and the next race's change still compares with the last race that had a place. A `GapLabel` (0.42.0) writes a word there instead, `absent`, and names the point; a race that was not held has no point at all. A race with no field size draws its place without a note.
- **CSV:** `ChartExport.Csv(spec)` adds a `Note` column whenever a point carries a note, so the export keeps each field size beside its place, and a heatmap with a not-rated cell adds a `NotRated` column after it (0.46.0).
- **Sparklines:** `ChartSvg.Render` draws a sparkline at its own size, `width:120px;max-width:100%`, rather than the width of its box. In the component, `<LumenChart Spec="pb" />` draws the drawing alone with its tooltips — no legend, toolbar, zoom or data table — and a tooltip stands just above the drawing, as wide as its words. A sparkline may be as small as 60 by 16; other kinds than line, area, scatter and column, panes and value labels are refused.
