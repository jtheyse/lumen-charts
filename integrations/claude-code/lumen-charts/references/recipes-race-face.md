# Race results recipes

Recipes for a race-results app's charts: a rider's season of finishing places and points, sparklines of finish times getting faster and of a growth log, on a dark brand style built from design tokens, at a phone card's width. Every example uses invented data. Each is a plain `ChartSpec`; render it with `ChartSvg.Render` for a static page, an API or an image, or put it in `<LumenChart Spec="…" FitWidth="true" />` on an interactive page. They compile against Lumen.Charts 0.34.0, together with the recipes in `sports.md`.

```csharp
using System.Globalization;
using Lumen.Charts;
```

## What every chart here meets

- **Static output is complete.** `ChartSvg.Render` writes every label, colour, tooltip (`<title>`) and accessible name into the SVG; nothing needs JavaScript, and nothing is fetched from a CDN.
- **Phone width.** For SVG rendered on the server set `Width` to the width the card shows it at, `Width = 340` here: drawn at the width it is shown, every label is 11 px or larger (axis ticks 12, value labels and notes 11, the title 17), and none needs the 8 to 9 px text a stretched 340-unit viewBox gives. A card a little narrower, 320 px, still shows 10.4 px. On an interactive page `FitWidth="true"` redraws the chart at its card's width instead.
- **Accessible.** `Title` and `Description` are the drawing's accessible name; each mark is focusable and named, `Position: 16-05-2026, 24/48, better than the previous`, and the same words are its tooltip.
- **Missing data is a gap, never a zero.** A race without a position or without points is a `null` Y: its line breaks there and no mark is drawn. A race without a field size writes its place alone.
- **Never colour alone.** A place coloured by its change also says the change in words, in its tooltip and its accessible name.

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

Places on a reversed axis, first at the top, in a pane of their own, and the points beneath on theirs, both on the races' shared X:

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

## Rendering notes

- **Static:** `ChartSvg.Render(spec)` at `Width = 340` (or the card's own width) is the whole chart: labels, colours, tooltips and names, with no script. Write it into the page, or rasterise it on the server with an SVG library of your choice; Lumen ships no PNG or PDF renderer. Each line's value label stands on a copy of itself stroked in the background colour, not on SVG 2's `paint-order`, so rasterisers without it draw it the same.
- **Interactive:** `<LumenChart Spec="recommended" FitWidth="true" />` measures its card and redraws at that width, never below 320 px; before it is interactive it is drawn at `Width` and scaled to fit. `Height` is kept, so choose one that reads at a phone's width.
- **Gaps:** a race with no place or no points is `null`, never zero: the line breaks, no mark or label is drawn, and the next race's change still compares with the last race that had a place. A race with no field size draws its place without a note.
- **CSV:** `ChartExport.Csv(spec)` adds a `Note` column whenever a point carries a note, so the export keeps each field size beside its place.
- **Sparklines:** `ChartSvg.Render` draws a sparkline at its own size, `width:120px;max-width:100%`, rather than the width of its box. In the component, `<LumenChart Spec="pb" />` draws the drawing alone with its tooltips — no legend, toolbar, zoom or data table — and a tooltip stands just above the drawing, as wide as its words. A sparkline may be as small as 60 by 16; other kinds than line, area, scatter and column, panes and value labels are refused.
