# Fitness and training charts

Research into the charts endurance and wellness apps draw, done in October 2026 to decide what Lumen builds next. It covers TrainingPeaks and WKO5, Strava, Gentler Streak, Bevel, WHOOP, Garmin Connect, Apple Fitness and Health, Oura, intervals.icu, Runna, Zwift, Polar Flow, COROS and Athlytic, with Golden Cheetah where it originated a chart.

Three sources were used, and each was checked against the others:

- **Vendor documentation**, found through Perplexity and read on the vendors' own help pages. Every claim about what an app computes or shows below comes from there. TrainingPeaks, Garmin and COROS help pages refused direct fetching, so their claims rest on search excerpts of those pages.
- **Mobbin**, for how the apps actually draw the charts on a phone. Its captures come from new demo accounts, so many charts are sparse, and it has no TrainingPeaks, Zwift, Polar, COROS or Athlytic screens. Nothing from it is reproduced here; the conventions are described.
- **The owner's library** (KnowledgeDock), for the science behind the numbers. Allen and Coggan, *Training and Racing with a Power Meter* (2nd edition, VeloPress) is the primary source; Bailey, *Science of Cycling*; Schneider et al., "Training and Athlete Monitoring" in Memmert (ed.), *Digitalization and Innovation in Sport and Sports Science*; and Raval et al., *Sports Data Analytics*. Formulas are paraphrased, not quoted.

## What the apps draw

Ranked by how many of the fourteen apps use the chart and how central it is to them. "Lumen today" says what the library can already draw; "Missing" is what stands in the way.

| Chart | Apps | What it is | Lumen today | Missing |
|---|---|---|---|---|
| Time in zone | 10 of 14 | One bar per zone, coloured on an ordered ramp, value at the bar end, the zone's range beside it | Horizontal bars, one colour per series | Per-category colours from a zone scale; time-in-zone computation |
| Activity stream | 9+ | Heart rate, power, pace, cadence and elevation over elapsed time or distance; zones shaded behind; line coloured by zone; scrubbing | Lines, areas, secondary axis, reference bands | Elapsed-duration axis; inverted pace axis; zone bands; zone-coloured line; muted context series behind; synchronised panels |
| Load against a target range | ~10 | Daily or weekly load as columns or a line inside a shaded band whose edges move with time | Columns, bands | Columns and a band in one chart |
| Score gauge | 8 | Arc or ring with a track, value in the centre, coloured by banded thresholds, sometimes a target arc or overflow | None | A radial gauge |
| HRV and resting-heart-rate trend | 7 | Daily points inside a rolling baseline band, coloured by whether they fall inside it | Lines, bands | Rolling mean and deviation; colour by threshold on markers |
| Weekly volume | 5–6 | Columns by week or day with goal or average lines, sometimes stacked by type | Columns, stacked columns, reference lines | Rounded bars (styling only) |
| Performance management (fitness, fatigue, form) | 4–5, central to serious athletes | Fitness and fatigue as lines, form as bars or a filled area on a second axis, daily load as dots, form zones, a projected future | Lines, secondary axis | The load model; lines and bars in one chart; projected-segment style |
| Calendar and streaks | 6–7 | Month grid or weeks-by-weekdays grid, cells coloured by value or carrying a ring or bubble | Heatmap (category grid) | A calendar layout |
| Sleep hypnogram | 5 | Ordered stage lanes over clock time, each period a rounded segment, connectors between levels | None | A state timeline over categories |
| Power–duration curve | 4, central to cycling | Best average power for every duration on a logarithmic duration axis, one curve per period, a model overlaid | Lines on a log axis | Duration-labelled log ticks (1s, 5s, 1m, 5m, 20m, 1h); mean-maximal computation; critical-power fit |
| Splits and laps | 5 | One bar per split, pace on an inverted mm:ss axis, average line; Strava sizes each lap's width by its length | Columns, reference lines | Pace format; inverted axis; variable-width columns |
| Elevation profile | 6 | Filled area over distance, sometimes coloured by grade | Area | Colour by a derived value along the line |
| Zone distribution over time | 6 | Time in each zone stacked per day or week | Stacked columns | Zone colours |
| Best efforts and personal-best progression | 4–5 | Pace or heart-rate duration curves; a step-shaped record envelope over time | Lines, scatter | Step lines; inverted axis |
| Structured workout profile | 3 | Each step a block whose width is its duration and height its target, coloured by zone | None | Variable-width blocks |
| Activity rings | 1, but iconic | Concentric radial progress bars that overlap themselves past the goal | Donut | Built with the gauge |
| Plan adherence | 4 | Deviation from target as dots around zero, coloured by compliance band | Scatter, reference lines | Colour by threshold |
| Intraday state timeline | 2 | Readings over the day coloured by state (rest, stress, activity) with a level line on top | Scatter, line | Colour by category on points or columns; lines and columns together |
| Gentler Streak Activity Path | 1, distinctive | A three-tone band whose width follows activity level, with each workout a dot at its acute-to-chronic load ratio | Band, scatter | Stacked band layers; band and points in one chart |
| W′ balance | 1 plus Golden Cheetah | Remaining anaerobic capacity as a line on a second axis beside power | Lines, secondary axis | The W′ balance model |

Route maps appear in most of these apps and are out of scope for a charting library. What a library can offer the host's map is the hover and selection events, which Lumen already raises.

## What the charts have in common

These capabilities come up across many chart types at once, so they are worth more than any single new chart kind.

1. **Duration and pace on axes.** Elapsed time as mm:ss and h:mm:ss; pace as mm:ss per km or mile; logarithmic duration ticks labelled 1s, 5s, 1m, 5m, 20m, 1h; compact numbers (1.2k). Pace axes are usually inverted so that faster is higher: intervals.icu documents it, and the Strava, Nike Run Club and Runna screens on Mobbin show it.
2. **A zone scale.** Ordered thresholds, each with a name and a colour, used to shade bands behind a line, to colour a line's segments by the zone their value falls in, to colour bars and markers, and to compute time in zone. Every app with heart-rate or power data has one.
3. **Several marks in one chart.** Lines over columns (performance management, load against target), a muted area behind a line (elevation behind pace), points over a band (baselines, the Activity Path). Today a Lumen chart has one kind for all its series.
4. **Training computation.** Normalized power, intensity factor, training stress, the fitness–fatigue–form model, time in zone, mean-maximal curves, critical power, rolling baselines. These are what most of the charts above draw, and they are well defined.
5. **New radial and timeline forms.** Gauges and rings; state timelines over categories; a calendar layout; variable-width blocks.
6. **Phone conventions.** Charts about a quarter to a third of the screen tall; the headline number above and the averages below; the Y axis on the right with three or four ticks or only its minimum and maximum; sparse X labels; scrubbing with a vertical rule and a pinned tooltip; a range selector (7D, 1M, 3M, 6M, YTD, 1Y); rounded bars and capsules; dark themes common. Much of this is styling and host layout rather than chart structure.

## The numbers behind the charts

Test values are given where a source provides them, because each is something the implementation can be checked against.

**Normalized power.** Take a 30-second rolling average of power, raise each value to the fourth power, average those, and take the fourth root (Allen and Coggan, ch. 7; TrainingPeaks). TrainingPeaks advises against reading it for efforts much under ten minutes. Neither source says how to treat gaps or recording rates other than one second.

**Intensity factor and training stress.** Intensity factor is normalized power divided by functional threshold power (FTP). Training stress (TSS) is hours × IF² × 100, so an hour at threshold is 100. Allen and Coggan's figure 11.1 — 7:09:27 at NP 198 W and IF 0.859 — gives 528.5, and the formula reproduces it within rounding; their figure 11.3 does not reproduce and should not be used as a test.

**Fitness, fatigue and form.** TrainingPeaks publishes the daily recurrence: fitness (CTL) moves a forty-second of the way from yesterday's value toward today's TSS, and fatigue (ATL) a seventh of the way. Form (TSB) is yesterday's fitness minus yesterday's fatigue. Days without training count as zero. The model descends from Banister's impulse-response model. Allen and Coggan recommend leaving fitness at 42 days but tuning fatigue between about 4 and 12 days, and seeding an athlete with no history by setting both to their typical daily stress so form starts at zero. intervals.icu also expresses form as a percentage of fitness. Two errors in the book's text are worth knowing: it states the fitness time constant must be shorter than the fatigue one, contradicting its own defaults, and it says form rises to equal fitness where its figure shows it returning to zero.

**Training zones.** Coggan's seven power levels, as a percentage of FTP: active recovery below 55, endurance 56–75, tempo 76–90, threshold 91–105, VO2max 106–120, anaerobic capacity 121–150, neuromuscular above that. The published ranges leave one-percent gaps, so an implementation has to choose a rule: treating each level as everything above the previous level's upper bound up to its own reproduces the book's worked example for FTP 290 W (160, 218, 261, 305 and 348 W as upper bounds, rounding half up). The heart-rate equivalents use threshold heart rate: below 68, 69–83, 84–94, 95–105, above 106 percent. Vendors differ: Garmin and Apple use five heart-rate zones, WHOOP uses heart-rate reserve, Zwift uses six power zones with fixed colours, and Gentler Streak's documentation is inconsistent about five or six.

**Mean-maximal curves and critical power.** The best average for every duration, plotted on a logarithmic duration axis because most of what changes happens between one second and thirty minutes. Critical power comes from Monod's model, total work = W′ + CP × time, fitted to maximal efforts between about three and twenty minutes; it overestimates short efforts. Terms collide: Friel uses "critical power" for the mean-maximal curve itself.

**Baselines.** Wearables compare a short recent average with a longer one: Oura a 14-day against a 3-month, Bevel and Athlytic against 60 days, Polar a 28-day mean with multiples of its standard deviation. Schneider et al. give the measurement-science view: a change is only real when it exceeds about twice the typical measurement error, and the composite readiness scores are opaque. They are drawn as the score; they should not be recomputed by a charting library.

**Not covered by any source used here**, and so not to be implemented without one: heart-rate and running TSS (TRIMP, hrTSS, rTSS), grade-adjusted pace, Friel zones, and the vendors' own load, strain, recovery and readiness formulas, which are unpublished.

## Build order

Cross-cutting work comes first, because each piece unlocks several charts. Each release is independently useful and is checked the same way as the rest of the library: executable assertions, the hashed rendering baseline, HTTP checks and the browser suite.

1. **Training metrics** — normalized power, intensity factor, training stress, the fitness–fatigue–form model, time in zone, mean-maximal curves, the critical-power fit, rolling baselines, and a zone scale with Coggan's power and heart-rate levels. Computation only, tested against the values above. Shipped in 0.18.0.
2. **Axis formats** — elapsed duration, pace, duration-labelled logarithmic ticks, compact numbers, and inverted axes. Unlocks the power curve, pace streams, splits and record progressions. Shipped in 0.19.0.
3. **Zones on charts** — zone bands behind a line, a line coloured by zone, and bars and markers coloured from a zone scale. Unlocks the activity stream, time in zone, grade-coloured elevation and baseline trends.
4. **Several marks in one chart** — lines, areas, columns and points together, and a band whose edges are series. Unlocks performance management, load against target, elevation behind pace and the Activity Path. The volume pane on the existing roadmap is the same need with panels stacked instead of overlaid, so synchronised panels belong here too.
5. **Gauges and rings.**
6. **State timelines and range columns** — hypnograms, intraday states, sleep timing and minimum–maximum bars.
7. **Calendar layout.**
8. **Variable-width blocks** — laps sized by their length, and structured workout profiles.

The moving average planned under regression families arrives with the rolling baselines in the first release.
