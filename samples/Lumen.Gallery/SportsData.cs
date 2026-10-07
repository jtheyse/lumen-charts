using System.Globalization;
using Lumen.Charts;

namespace Lumen.Gallery;

public enum Sport { Run, Ride }

/// <summary>What a run recorded, one sample every <see cref="SportsData.RunSample"/> seconds from its start: heart rate in bpm,
/// pace in seconds per kilometre, the distance covered so far in metres, and the elevation in metres.</summary>
public sealed record Track(IReadOnlyList<double> HeartRate, IReadOnlyList<double> Pace, IReadOnlyList<double> Distance, IReadOnlyList<double> Elevation);

/// <summary>One workout. Its stress is <see cref="Training.StressScore"/> of its power record, rounded, and its time in zone
/// <see cref="Training.TimeInZone"/> of its heart rate. A ride keeps its mean-maximal power; a run keeps its samples and the
/// fastest 5 km inside it. Both keep the highest heart rate they recorded.</summary>
public sealed record Session(DateOnly Day, Sport Sport, string Name, double Seconds, double Metres, double Stress,
    IReadOnlyList<double> TimeInZone, IReadOnlyList<(double Seconds, double Value)> PowerCurve, double? Best5k, Track? Track, double PeakHeartRate)
{
    /// <summary>The steps the session was planned in, in order: how long each should take at its target, in seconds — a run's
    /// length at threshold speed times its effort; its target power in watts, a ramp's at its average; and, on a run, which is
    /// planned by distance, its length in metres.</summary>
    public IReadOnlyList<(double Seconds, double Watts, double Metres)> Steps { get; init; } = [];
    /// <summary>The threshold power the session was planned against, in watts: a ride's FTP, or a run's from its threshold pace.</summary>
    public double Ftp { get; init; }
}

/// <summary>One night's sleep, named by the morning it ends. Bedtime and waking are seconds since the midnight before the
/// night, so 22:48 is 82080 and 06:35 the next morning 110100, and so is each stage's start and end, in order from bedtime to
/// waking. The lowest heart rate is the night's.</summary>
public sealed record Night(DateOnly Morning, double Bedtime, double Wake, IReadOnlyList<(string Stage, double From, double To)> Stages, double LowestHeartRate);

/// <summary>The sessions trained, the stress planned after them, <see cref="Training.Load"/> over both, and one overnight HRV
/// reading a night from four weeks before the season to its last day, so that the first night has a baseline.</summary>
public sealed record Season(IReadOnlyList<Session> Sessions, IReadOnlyList<(DateOnly Day, double Stress)> Planned,
    IReadOnlyList<LoadDay> Load, IReadOnlyList<double> Hrv)
{
    /// <summary>The sessions planned after the season, in order, whose stress is <see cref="Planned"/>.</summary>
    public IReadOnlyList<Session> Upcoming { get; init; } = [];
}

/// <summary>An invented ride as a bike computer records it, one sample a second: heart rate in bpm, missing where the strap dropped
/// out; power in watts; cadence in rpm, 0 while coasting; speed in metres per second; elevation in metres; and temperature in °C.</summary>
public sealed record Ride(IReadOnlyList<double?> HeartRate, IReadOnlyList<double> Power, IReadOnlyList<double> Cadence, IReadOnlyList<double> Speed,
    IReadOnlyList<double> Elevation, IReadOnlyList<double> Temperature);

/// <summary>A chart on the Sports &amp; performance page, with the plain title above it and a one-line note, in which code is
/// set between backticks.</summary>
public sealed record SportsCard(string Section, string Id, string Title, string Note, bool Wide, ChartSpec Spec)
{
    /// <summary>A second chart drawn beside the first in the same wide card, each half its width, as the training calendar's month
    /// stands beside its season.</summary>
    public ChartSpec? Beside { get; init; }
    /// <summary>Sparklines the card draws instead of one chart, each with a line of words beside it; the first is its
    /// <see cref="SportsCard.Spec"/>.</summary>
    public IReadOnlyList<SportsLine>? Lines { get; init; }
    /// <summary>Whether the chart's component draws its legend: off for a chart whose lines are named at their ends.</summary>
    public bool ShowLegend { get; init; } = true;
    /// <summary>Whether the chart's component draws its toolbar: off for a card drawn as a phone app would, its status line kept out of sight.</summary>
    public bool ShowToolbar { get; init; } = true;
    /// <summary>Results this card draws with <c>&lt;LumenPlacings&gt;</c> instead of its own spec.</summary>
    public IReadOnlyList<Placing>? Placings { get; init; }
}

/// <summary>A sparkline on the Sports &amp; performance page and the words written beside it: what it measures, in bold, and what
/// it shows, worked out from the same numbers it draws.</summary>
public sealed record SportsLine(ChartSpec Spec, string Name, string Text);

/// <summary>
/// One simulated athlete who runs and rides: sixteen weeks of training from 8 June 2026 and two planned weeks tapering to a
/// 10 km race. Every session is simulated sample by sample from a fixed seed, so the season is the same on every run, and every
/// chart on the page is drawn from it: the activity stream is the last session, its time in zone is counted from the samples
/// the stream draws, and the weekly charts add up the days the performance chart draws.
/// </summary>
public static class SportsData
{
    public static readonly DateOnly Start = new(2026, 6, 8);
    public const int Weeks = 16, PlannedWeeks = 2, BaselineNights = 28;
    public const double RunSample = 10, ThresholdHeartRate = 170, SeedFitness = 60;
    /// <summary>The 5 km record the athlete brings into the season, in seconds.</summary>
    public const double PriorRecord = 1272;
    /// <summary>Threshold running pace in seconds per kilometre at the start of the season and at its end.</summary>
    public const double PaceBefore = 266, PaceAfter = 250;
    /// <summary>The pace the 10 km race is planned at, seconds per kilometre.</summary>
    public const double GoalPace = 245;
    /// <summary>The athlete's body mass in kilograms, which a run's active calories are reckoned from at one kilocalorie per
    /// kilogram per kilometre.</summary>
    public const double BodyMass = 68;
    // Short enough to stay inside a chart drawn at a phone's width.
    public const string Source = "Source: simulated athlete · not real training data";
    public static DateOnly Today => Start.AddDays(Weeks * 7 - 1);
    public static DateOnly Race => Start.AddDays((Weeks + PlannedWeeks) * 7 - 1);

    /// <summary>An invented season of five races, apart from the simulated training: each race's day, the place it finished, the size
    /// of its field and the series points it earned. A race without points would be a gap in the points, and one without a field
    /// size would write its place alone.</summary>
    public static readonly IReadOnlyList<(DateOnly Day, int Position, int? Field, int? Points)> Races =
        [(new(2026, 4, 11), 31, 50, 40), (new(2026, 5, 16), 24, 48, 52), (new(2026, 7, 4), 27, 51, 47), (new(2026, 8, 8), 21, 49, 58), (new(2026, 9, 19), 19, 52, 61)];

    /// <summary>An invented season of ten races in three disciplines and one other, apart from the simulated training: each race's day, its
    /// discipline, the place it finished, null for a race not finished, and the size of its field.</summary>
    public static readonly IReadOnlyList<(DateOnly Day, string Discipline, int? Place, int Field)> SeasonRaces =
        [(new(2026, 2, 7), "XCO", 18, 40), (new(2026, 2, 21), "XCC", 9, 32), (new(2026, 3, 14), "XCO", 12, 44), (new(2026, 3, 28), "XCM", 31, 60),
         (new(2026, 4, 18), "XCO", null, 41), (new(2026, 5, 9), "XCC", 6, 30), (new(2026, 5, 30), "XCO", 7, 42), (new(2026, 6, 20), "Enduro", 22, 55),
         (new(2026, 7, 11), "XCM", 19, 58), (new(2026, 8, 1), "XCO", 5, 40)];

    /// <summary>An invented season in two series, a league and an open race, for the places and points card: the league's points are
    /// earned, the open races score none.</summary>
    public static readonly IReadOnlyList<Placing> Placings =
    [
        new(18) { Field = 40, Points = 33, Date = new(2026, 3, 14), Series = "Invented League" },
        new(6) { Field = 22, Date = new(2026, 4, 4), Series = "Invented Open" },
        new(14) { Field = 42, Points = 37, Date = new(2026, 4, 25), Series = "Invented League" },
        new(9) { Field = 25, Date = new(2026, 5, 23), Series = "Invented Open" },
        new(16) { Field = 41, Points = 35, Date = new(2026, 6, 20), Series = "Invented League" },
        new(11) { Field = 44, Points = 40, Date = new(2026, 8, 15), Series = "Invented League" },
    ];

    /// <summary>The places and points card's size and words, on top of what <see cref="PlacingsChart.Build"/> draws.</summary>
    public static ChartSpec PlacesLook(ChartSpec spec) => spec with { Width = 1100, Height = 400, Title = "Places and points, in two series", Source = Source };

    /// <summary>The first of the four seasons in <see cref="CategoryHistory"/>.</summary>
    public const int FirstCategorySeason = 2023;

    /// <summary>The fewest starts a category and season need to be rated. A cell with fewer starts is marked not rated instead of being coloured as a result.</summary>
    public const int RatedStarts = 10;

    /// <summary>An invented history for the category heatmap card: each category's four seasons from <see cref="FirstCategorySeason"/>,
    /// each the points its riders earned, the starts they came from and the riders who made them.</summary>
    public static readonly IReadOnlyList<(string Category, (int Points, int Starts, int Riders)[] Seasons)> CategoryHistory =
    [
        ("Sprint", [(34, 12, 5), (41, 14, 6), (45, 13, 6), (52, 15, 7)]),
        ("Middle distance", [(27, 11, 5), (30, 12, 5), (38, 14, 6), (36, 13, 6)]),
        ("Long distance", [(8, 4, 3), (24, 10, 4), (19, 11, 4), (31, 12, 5)]),
        ("Relay", [(0, 0, 0), (20, 10, 5), (14, 9, 6), (25, 10, 5)]),
    ];

    /// <summary>An invented race of eight riders over six laps, apart from the simulated training: each rider's name, the athlete last as
    /// "You", and their lap times in seconds.</summary>
    public static readonly IReadOnlyList<(string Rider, double[] Laps)> LapRace =
        [("Rider A", [300.5, 290.0, 299.5, 297.3, 290.7, 301.7]), ("Rider B", [293.9, 298.5, 302.8, 292.4, 302.0, 299.6]),
         ("Rider C", [297.5, 306.7, 296.2, 301.0, 305.1, 294.8]), ("Rider D", [308.6, 301.6, 300.0, 309.1, 298.5, 303.5]),
         ("Rider E", [307.5, 300.1, 311.0, 303.9, 302.5, 311.5]), ("Rider F", [302.1, 311.1, 309.8, 302.5, 313.5, 306.2]),
         ("Rider G", [310.0, 315.1, 304.5, 313.5, 312.1, 305.0]), ("You", [307.8, 301.6, 298.6, 298.5, 293.3, 288.6])];

    /// <summary>Each rider of <see cref="LapRace"/>'s gap to whoever led at the end of each lap, in seconds, lap 0 the start.</summary>
    public static double[][] LapGaps()
    {
        var clocks = LapRace.Select(r => r.Laps.Aggregate(new List<double> { 0 }, (clock, lap) => { clock.Add(Math.Round(clock[^1] + lap, 1)); return clock; }).ToArray()).ToArray();
        return clocks.Select(clock => clock.Select((time, lap) => Math.Round(time - clocks.Min(other => other[lap]), 1)).ToArray()).ToArray();
    }

    /// <summary>An invented field for the last of <see cref="Races"/>: its 52 finishers' times in seconds, fastest first, the athlete's
    /// own, 40:12, the 19th, as its place has it. Most finish within a few minutes of 41, the slower tail the longer, as a field does;
    /// one rider finishes just ahead of the rest, and one behind.</summary>
    public static readonly IReadOnlyList<double> Field =
        [2025, 2060, 2141, 2168, 2207, 2225, 2249, 2272, 2283, 2298, 2316, 2335, 2342, 2354, 2367, 2380, 2398, 2406, 2412, 2425, 2433, 2448, 2457,
         2464, 2475, 2482, 2496, 2504, 2519, 2529, 2541, 2558, 2566, 2575, 2591, 2606, 2620, 2638, 2657, 2673, 2691, 2708, 2730, 2752, 2774, 2801,
         2839, 2870, 2907, 2976, 3015, 3080];

    /// <summary>The bins a race's finish times are drawn in, by the rule a results page uses: the narrowest width of 1, 2, 5, 10, 15, 30
    /// or 60 minutes that sets the 1st to the 99th percentile in at most 20 bins, each starting on a whole multiple of the width; and
    /// how many finishers fall off the chart, faster and slower.</summary>
    public static ((double From, double To, int Count)[] Bins, int Faster, int Slower) FinishBins(IReadOnlyList<double> seconds)
    {
        var sorted = seconds.Order().ToArray();
        double low = Statistics.Quantile(sorted, .01), high = Statistics.Quantile(sorted, .99);
        var width = new[] { 60d, 120, 300, 600, 900, 1800, 3600 }.FirstOrDefault(w => Math.Floor(high / w) - Math.Floor(low / w) < 20, 3600);
        double from = Math.Floor(low / width) * width, to = (Math.Floor(high / width) + 1) * width;
        var bins = Enumerable.Range(0, (int)Math.Round((to - from) / width)).Select(i => (From: from + i * width, To: from + (i + 1) * width))
            .Select(bin => (bin.From, bin.To, Count: sorted.Count(t => t >= bin.From && t < bin.To))).ToArray();
        return (bins, sorted.Count(t => t < from), sorted.Count(t => t >= to));
    }

    /// <summary>An invented weigh-in each Monday of the season, in kilograms, steady about <see cref="BodyMass"/>: a few hundred grams
    /// either way, as a scale reads from week to week.</summary>
    public static readonly IReadOnlyList<double> WeighIns = [68.2, 68.0, 68.3, 67.9, 68.1, 68.0, 67.8, 68.1, 67.9, 68.0, 67.8, 67.9, 67.7, 67.9, 67.8, 67.8];

    /// <summary>Coggan's five heart-rate levels at a threshold of 170 bpm, under the short names the apps use.</summary>
    public static ZoneScale HeartZones { get; } = new(ZoneScale.CogganHeartRate(ThresholdHeartRate).Zones
        .Zip(new[] { "Recovery", "Endurance", "Tempo", "Threshold", "VO2max" }, (zone, name) => zone with { Name = name }).ToArray());

    public static Season Season => season.Value;
    private static readonly Lazy<Season> season = new(Simulate);

    /// <summary>How long the invented long ride lasts, in seconds, one sample each.</summary>
    public const int RideSeconds = 7200;
    /// <summary>The second the heart-rate strap drops out at, and how long it stays out.</summary>
    public const int DropoutAt = 3720, DropoutSeconds = 45;
    /// <summary>The road's height in metres a given second into the long ride: long rolling climbs with shorter rises on them.</summary>
    private static double RideHeight(int second) => 140 + 55 * Math.Sin(second / 380.0 - 1.3) + 18 * Math.Sin(second / 116.0 + .4);
    /// <summary>
    /// An invented two-hour ride, apart from the simulated season, worked out from sines so it is the same on every run: ten minutes'
    /// warm-up, then a steady effort that rises on each climb and stops on the steeper descents, where cadence falls to 0 and speed
    /// rises; heart rate follows the power a minute behind; the air warms through the morning; and the heart-rate strap drops out for
    /// <see cref="DropoutSeconds"/> seconds a little after the hour, leaving those samples missing, never zero.
    /// </summary>
    public static Ride LongRide()
    {
        var heart = new double?[RideSeconds];
        double[] power = new double[RideSeconds], cadence = new double[RideSeconds], speed = new double[RideSeconds],
            elevation = new double[RideSeconds], temperature = new double[RideSeconds];
        var pulse = 92d;
        for (var t = 0; t < RideSeconds; t++)
        {
            var climb = RideHeight(t + 1) - RideHeight(t);
            var effort = t < 600 ? 120 + t / 6d : 215;
            var watts = climb < -.2 ? 0 : Math.Max(0, effort + 450 * climb + 28 * Math.Sin(t * 1.7) + 16 * Math.Sin(t * .31 + 1));
            power[t] = Math.Round(watts);
            cadence[t] = watts == 0 ? 0 : Math.Round(85 + (watts - 215) / 25 + 3 * Math.Sin(t * .83));
            speed[t] = Math.Round(Math.Clamp(8.6 - 9 * climb + .4 * Math.Sin(t / 50d), 3.5, 16), 2);
            elevation[t] = Math.Round(RideHeight(t), 1);
            temperature[t] = Math.Round(17.5 + 5.5 * t / RideSeconds + .6 * Math.Sin(t / 700d), 1);
            pulse += (88 + .3 * watts - pulse) / 60;
            heart[t] = t >= DropoutAt && t < DropoutAt + DropoutSeconds ? null : Math.Round(pulse);
        }
        return new(heart, power, cadence, speed, elevation, temperature);
    }

    // Heart rate an effort settles at, as a fraction of threshold heart rate, against the effort as a fraction of threshold power.
    private static readonly (double Effort, double Heart)[] HeartCurve =
        [(0, .5), (.55, .66), (.75, .81), (.9, .92), (1, .99), (1.1, 1.04), (1.6, 1.08), (3.2, 1.1)];
    private static double Rolling(double metres) => 30 + 6 * Math.Sin(metres / 2300 * Math.Tau) + 3 * Math.Sin(metres / 870 * Math.Tau + 1.3);
    private static double Hills(double metres) =>
        42 + 22 * Math.Sin(metres / 6800 * Math.Tau - 1.2) + 10 * Math.Sin(metres / 2100 * Math.Tau + .4) + 4 * Math.Sin(metres / 760 * Math.Tau + 2.1);
    private static double Flat(double metres) => 12 + 1.5 * Math.Sin(metres / 1200 * Math.Tau);

    internal static Season Simulate()
    {
        var random = new Random(2026);
        double Noise(double deviation) => deviation * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(Math.Tau * random.NextDouble());
        // The athlete gets fitter through the weeks trained, and the planned weeks keep the fitness the season reached.
        double Progress(DateOnly day) => Math.Clamp((day.DayNumber - Start.DayNumber) / (Weeks * 7 - 1d), 0, 1);
        double Ftp(DateOnly day) => 246 + 22 * Progress(day);
        double Pace(DateOnly day) => PaceBefore - (PaceBefore - PaceAfter) * Progress(day);
        double Heart(double effort, double seconds, double sport) => ThresholdHeartRate * sport * Interpolate(effort, HeartCurve) + seconds / 60 * .05;
        double Lag(double step) => 1 - Math.Exp(-step / 28);

        // A ride is recorded every second. Terrain and wind move a steady effort about, a hard one is held, and a long ride coasts
        // down its descents. Heart rate runs a little lower on the bike than on foot for the same effort.
        Session Ride(DateOnly day, string name, (double Seconds, double From, double To)[] plan, bool coasts)
        {
            var ftp = Ftp(day);
            var total = (int)plan.Sum(step => step.Seconds);
            var watts = new double[total]; var heart = new double[total];
            double phase = random.NextDouble() * 1000, beats = 88, gust = 0, alpha = Lag(1);
            var t = 0;
            foreach (var (seconds, from, to) in plan)
                for (var i = 0; i < seconds; i++, t++)
                {
                    var effort = from + (to - from) * i / seconds;
                    gust = .8 * gust + Noise(.03);
                    var terrain = effort < 1 ? .07 * Math.Sin((t + phase) / 61) + .03 * Math.Sin((t + phase) / 17) : 0;
                    var e = Math.Max(0, effort * (1 + gust + terrain));
                    if (coasts && effort < .8 && Math.Sin((t + phase) / 173) > .93) e = 0;
                    watts[t] = Math.Round(ftp * e);
                    beats += (Heart(e, t, .95) - beats) * alpha;
                    heart[t] = Math.Round(beats + Noise(.8));
                }
            var np = Training.NormalizedPower(watts) ?? watts.Average();
            return new(day, Sport.Ride, name, total, 0, Math.Round(Training.StressScore(total, np, ftp)),
                Training.TimeInZone(heart, HeartZones), Training.MeanMaximal(watts, Training.StandardDurations), null, null, heart.Max())
            { Steps = plan.Select(step => (step.Seconds, ftp * (step.From + step.To) / 2, 0d)).ToArray(), Ftp = ftp };
        }

        // A run is planned by distance and recorded every ten seconds. A climb costs effort and pace both, and its stress comes
        // from a running power meter, whose threshold rises with threshold pace.
        Session Run(DateOnly day, string name, (double Metres, double From, double To)[] plan, Func<double, double> route)
        {
            var threshold = 1000 / Pace(day);
            var ftp = 75 * threshold;
            var total = plan.Sum(step => step.Metres);
            List<double> heart = [], pace = [], distance = [], elevation = [], watts = [];
            double metres = 0, beats = 88, gust = 0, alpha = Lag(RunSample);
            while (metres < total)
            {
                var grade = (route(metres + 10) - route(metres - 10)) / 20;
                gust = .7 * gust + Noise(.012);
                var e = Effort(plan, metres) * (1 + .6 * grade + gust);
                var speed = threshold * e / (1 + (grade > 0 ? 3.3 : 1.8) * grade);
                beats += (Heart(e, heart.Count * RunSample, 1) - beats) * alpha;
                heart.Add(Math.Round(beats + Noise(.8)));
                pace.Add(Math.Round(1000 / speed + Noise(3)));
                distance.Add(Math.Round(metres, 1));
                elevation.Add(Math.Round(route(metres), 1));
                watts.Add(Math.Round(ftp * e));
                metres += speed * RunSample;
            }
            var seconds = heart.Count * RunSample;
            var np = Training.NormalizedPower(watts, RunSample) ?? watts.Average();
            var track = new Track(heart, pace, distance, elevation);
            metres = Math.Round(metres, 1);
            return new(day, Sport.Run, name, seconds, metres, Math.Round(Training.StressScore(seconds, np, ftp)),
                Training.TimeInZone(heart, HeartZones, RunSample), [], Fastest(track, metres, 5000), track, heart.Max())
            { Steps = plan.Select(step => (step.Metres / (threshold * (step.From + step.To) / 2), ftp * (step.From + step.To) / 2, step.Metres)).ToArray(), Ftp = ftp };
        }

        static (double, double, double)[] Repeat(int count, (double, double, double)[] steps) => Enumerable.Repeat(steps, count).SelectMany(step => step).ToArray();

        // Each week builds on the last, and every few weeks a lighter one ends in tests: twenty minutes all out on the bike and a
        // 5 km time trial. The two planned weeks taper, and the second ends with the race.
        IEnumerable<Session> Week(int week, double volume)
        {
            var monday = Start.AddDays(7 * week);
            var even = week % 2 == 0;
            if (week is 3 or 7 or 12)
            {
                yield return Run(monday.AddDays(1), "Easy run", [(6000, .75, .75)], Rolling);
                yield return Ride(monday.AddDays(2), "Easy ride", [(600, .5, .62), (3000, .62, .62)], false);
                yield return Run(monday.AddDays(3), "Easy run", [(5000, .74, .74)], Rolling);
                yield return Ride(monday.AddDays(5), "20-minute test", [(1200, .5, .78), .. Repeat(3, [(60, 1.1, 1.1), (60, .55, .55)]), (240, .55, .55), (1200, 1.05, 1.05), (900, .5, .45)], false);
                yield return Run(monday.AddDays(6), "5 km time trial", [(2000, .72, .78), (5000, 1.06, 1.06), (1500, .66, .66)], Flat);
                yield break;
            }
            var repeats = even ? Repeat(Math.Max(3, (int)Math.Round(6 * volume)), [(800, 1.08, 1.08), (400, .62, .62)])
                : Repeat(Math.Max(2, (int)Math.Round(4 * volume)), [(1600, 1.02, 1.02), (400, .62, .62)]);
            yield return Run(monday.AddDays(1), even ? "800 m repeats" : "1600 m repeats", [(2000, .74, .78), .. repeats, (1500, .7, .7)], Rolling);
            var efforts = even ? Repeat(Math.Max(3, (int)Math.Round(5 * volume)), [(240, 1.15, 1.15), (240, .55, .55)])
                : Repeat(Math.Max(3, (int)Math.Round(6 * volume)), [(180, 1.23, 1.23), (180, .55, .55)]);
            var steady = Math.Max(600, Math.Round(4500 * volume - 1200 - efforts.Sum(step => step.Item1)));
            yield return Ride(monday.AddDays(2), even ? "Four-minute efforts" : "Three-minute efforts", [(900, .55, .72), .. efforts, (steady, .68, .68), (300, .5, .5)], false);
            yield return Run(monday.AddDays(3), "Easy run", [(Math.Round(80 * volume) * 100, .75, .75)], Rolling);
            yield return Ride(monday.AddDays(4), "Easy spin with sprints", [(600, .55, .6), .. Repeat(3, [(10, 3, 3), (50, .5, .5), (540, .6, .6)]), (Math.Max(300, Math.Round(3300 * volume - 2400)), .58, .58)], false);
            if (week == Weeks + PlannedWeeks - 1)
            {
                yield return Ride(monday.AddDays(5), "Openers", [(1200, .62, .62), .. Repeat(3, [(60, 1.1, 1.1), (120, .55, .55)]), (600, .6, .6)], false);
                yield return Run(monday.AddDays(6), "10 km race", [(2000, .72, .78), (10000, 1.02, 1.02)], Flat);
                yield break;
            }
            var main = even ? Repeat(2, [(1200, 1, 1), (600, .6, .6)]) : Repeat(3, [(600, 1.07, 1.07), (300, .6, .6)]);
            (double, double, double)[] climb = even ? [(60, 1.7, 1.7)] : [(300, 1.17, 1.17)];
            yield return Ride(monday.AddDays(5), even ? "Long ride with 2 × 20 minutes" : "Long ride with 3 × 10 minutes",
                [(1200, .55, .7), (Math.Round(1200 * volume), .7, .7), .. main, (Math.Round(900 * volume), .72, .72), .. climb, (120, .5, .5),
                 (Math.Round(1800 * volume), .86, .86), (Math.Round(1500 * volume), .7, .7), (600, .55, .5)], true);
            yield return week == Weeks - 1
                ? Run(monday.AddDays(6), "Hilly progression run", [(4000, .76, .76), (4000, .84, .84), (3000, .92, .92), (3000, 1, 1)], Hills)
                : Run(monday.AddDays(6), "Long run", [(Math.Round(14 * volume) * 1000, .76, .76)], Hills);
        }

        double[] volumes = [.90, .96, 1.02, .60, .96, 1.02, 1.08, .60, 1.00, 1.06, 1.12, 1.18, .60, 1.08, 1.14, 1.20, .70, .45];
        var sessions = new List<Session>();
        var planned = new List<(DateOnly Day, double Stress)>();
        var upcoming = new List<Session>();
        for (var week = 0; week < Weeks + PlannedWeeks; week++)
            foreach (var session in Week(week, volumes[week]))
                if (week < Weeks) sessions.Add(session); else { planned.Add((session.Day, session.Stress)); upcoming.Add(session); }
        // The first Monday is a rest day, entered so the load model starts on it. The athlete comes in at a typical daily stress
        // for both fitness and fatigue, which starts form at zero.
        var load = Training.Load(sessions.Select(s => (s.Day, s.Stress)).Prepend((Start, 0)).Concat(planned), SeedFitness, SeedFitness);
        // Overnight HRV sits lower while form is low and the night after a hard day; before the season form was level.
        var hrv = new List<double>();
        for (var night = -BaselineNights; night < Weeks * 7; night++)
            hrv.Add(Math.Round(64 + (night < 0 ? 0 : .3 * load[night].Form - (night > 0 && load[night - 1].Stress >= 150 ? 4 : 0)) + Noise(3.2)));
        return new(sessions, planned, load, hrv) { Upcoming = upcoming };
    }

    private static double Effort((double Metres, double From, double To)[] plan, double metres)
    {
        foreach (var (length, from, to) in plan)
        {
            if (metres < length) return from + (to - from) * metres / length;
            metres -= length;
        }
        return plan[^1].To;
    }

    private static double Interpolate(double x, (double X, double Y)[] curve)
    {
        if (x <= curve[0].X) return curve[0].Y;
        for (var i = 1; i < curve.Length; i++)
            if (x <= curve[i].X) return curve[i - 1].Y + (curve[i].Y - curve[i - 1].Y) * (x - curve[i - 1].X) / (curve[i].X - curve[i - 1].X);
        return curve[^1].Y;
    }

    /// <summary>When a run reached <paramref name="at"/> metres, in seconds from its start, interpolated between samples.</summary>
    public static double TimeAt(Track track, double metres, double at)
    {
        var i = IndexAt(track.Distance, at);
        var next = i + 1 < track.Distance.Count ? track.Distance[i + 1] : metres;
        return (i + (at - track.Distance[i]) / (next - track.Distance[i])) * RunSample;
    }

    /// <summary>The elevation of a run at <paramref name="at"/> metres, interpolated between samples and held after the last.</summary>
    public static double ElevationAt(Track track, double at)
    {
        var i = IndexAt(track.Distance, at);
        if (i + 1 >= track.Distance.Count) return track.Elevation[i];
        return track.Elevation[i] + (track.Elevation[i + 1] - track.Elevation[i]) * (at - track.Distance[i]) / (track.Distance[i + 1] - track.Distance[i]);
    }

    /// <summary>The fewest seconds a run took over <paramref name="length"/> metres starting at any sample, or null for a run
    /// shorter than that.</summary>
    public static double? Fastest(Track track, double metres, double length)
    {
        if (metres < length) return null;
        var best = double.PositiveInfinity;
        for (var i = 0; i < track.Distance.Count && track.Distance[i] + length <= metres; i++)
            best = Math.Min(best, TimeAt(track, metres, track.Distance[i] + length) - i * RunSample);
        return Math.Round(best);
    }

    // The last sample at or before a distance.
    private static int IndexAt(IReadOnlyList<double> distance, double at)
    {
        int low = 0, high = distance.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (distance[middle] <= at) low = middle; else high = middle - 1;
        }
        return low;
    }

    /// <summary>The kilometre splits of a run, in seconds, whole kilometres only.</summary>
    public static IReadOnlyList<double> Splits(Session run) =>
        Enumerable.Range(1, (int)(run.Metres / 1000)).Select(k => Math.Round(TimeAt(run.Track!, run.Metres, k * 1000) - TimeAt(run.Track!, run.Metres, (k - 1) * 1000))).ToArray();

    /// <summary>A run's laps, one for each step it was planned in, the last ending where the run did: each a block from where it
    /// starts to where it ends, in kilometres, as high as its pace in seconds per kilometre, rounded to the second.</summary>
    public static IReadOnlyList<ChartPoint> Laps(Session run)
    {
        var laps = new List<ChartPoint>();
        var start = 0d;
        for (var i = 0; i < run.Steps.Count; i++)
        {
            var end = i == run.Steps.Count - 1 ? run.Metres : start + run.Steps[i].Metres;
            var seconds = TimeAt(run.Track!, run.Metres, end) - TimeAt(run.Track!, run.Metres, start);
            laps.Add(ChartPoint.Block(start / 1000, Math.Round(end / 1000, 2), Math.Round(seconds / (end - start) * 1000), $"Lap {i + 1}"));
            start = end;
        }
        return laps;
    }

    /// <summary>A run's average heart rate in each of its laps, the laps <see cref="Laps"/> draws, in whole bpm: the mean of the samples
    /// recorded from where the lap starts to where it ends.</summary>
    public static IReadOnlyList<double> LapHeartRates(Session run)
    {
        var track = run.Track!;
        return Laps(run).Select(lap =>
        {
            double from = TimeAt(track, run.Metres, lap.X * 1000), to = TimeAt(track, run.Metres, lap.XEnd!.Value * 1000);
            return Math.Round(Enumerable.Range(0, track.HeartRate.Count).Where(i => i * RunSample >= from && i * RunSample < to).Average(i => track.HeartRate[i]));
        }).ToArray();
    }

    /// <summary>A planned session as a structured workout: each step a block from where it starts to where it ends, in whole seconds
    /// at its target, as high as its target power in whole watts, named for its place in the session — the warm-up, each repeat at
    /// or above threshold, the recoveries between them and the cool-down — and on a run for its length.</summary>
    public static IReadOnlyList<ChartPoint> Workout(Session session)
    {
        var blocks = new List<ChartPoint>();
        var (at, repeat) = (0d, 0);
        for (var i = 0; i < session.Steps.Count; i++)
        {
            var (seconds, watts, metres) = session.Steps[i];
            var name = i == 0 ? "Warm-up" : i == session.Steps.Count - 1 ? "Cool-down" : watts >= session.Ftp ? $"Repeat {++repeat}" : "Recovery";
            if (session.Sport == Sport.Run) name += metres >= 1000 ? $", {Text(metres / 1000, "0.#")} km" : $", {Text(metres)} m";
            blocks.Add(ChartPoint.Block(Math.Round(at), Math.Round(at + seconds), Math.Round(watts), name));
            at += seconds;
        }
        return blocks;
    }

    /// <summary>The best average power for each standard duration over every ride in a month.</summary>
    public static IReadOnlyList<(double Seconds, double Value)> MonthBest(Season season, int month) => Training.StandardDurations
        .Select(duration => (duration, Rides: season.Sessions.Where(s => s.Sport == Sport.Ride && s.Day.Month == month)
            .SelectMany(s => s.PowerCurve).Where(p => p.Seconds == duration).Select(p => p.Value).ToArray()))
        .Where(d => d.Rides.Length > 0).Select(d => (d.duration, Math.Round(d.Rides.Max()))).ToArray();

    /// <summary>The 5 km record over the season: the prior record on the first day, each run that beat it, and today.</summary>
    public static IReadOnlyList<(DateOnly Day, double Seconds)> Records(Season season)
    {
        var record = PriorRecord;
        var records = new List<(DateOnly, double)> { (Start, record) };
        foreach (var run in season.Sessions)
            if (run.Best5k < record) records.Add((run.Day, record = run.Best5k!.Value));
        if (records[^1].Item1 != Today) records.Add((Today, record));
        return records;
    }

    /// <summary>The run with each week's fastest 5 km.</summary>
    public static IReadOnlyList<Session> WeeklyBests(Season season) => season.Sessions.Where(s => s.Best5k is not null)
        .GroupBy(s => (s.Day.DayNumber - Start.DayNumber) / 7).Select(week => week.MinBy(s => s.Best5k)!).ToArray();

    /// <summary>The fastest kilometre inside each session of repeats, the 800 m and the 1600 m, in seconds, oldest first.</summary>
    public static IReadOnlyList<(Session Run, double Seconds)> RepeatBests(Season season) => season.Sessions
        .Where(s => s.Sport == Sport.Run && s.Name.EndsWith("repeats", StringComparison.Ordinal)).Select(s => (s, Fastest(s.Track!, s.Metres, 1000)!.Value)).ToArray();

    /// <summary>Which of a run of times, oldest first, are personal bests: faster than every time before them. The first time only
    /// sets the mark to beat.</summary>
    public static IReadOnlyList<bool> Bests(IReadOnlyList<double> seconds) =>
        seconds.Select((time, i) => i > 0 && seconds.Take(i).All(before => time < before)).ToArray();

    /// <summary>Each week's training stress, from the days the load model counts.</summary>
    public static IReadOnlyList<double> WeeklyLoad(Season season) =>
        Enumerable.Range(0, Weeks).Select(week => season.Load.Skip(7 * week).Take(7).Sum(day => day.Stress)).ToArray();

    /// <summary>Each week's seconds in each heart-rate zone, over every session that week.</summary>
    public static IReadOnlyList<IReadOnlyList<double>> WeeklyZones(Season season) => Enumerable.Range(0, Weeks)
        .Select(week => (IReadOnlyList<double>)Enumerable.Range(0, HeartZones.Zones.Count)
            .Select(zone => season.Sessions.Where(s => (s.Day.DayNumber - Start.DayNumber) / 7 == week).Sum(s => s.TimeInZone[zone])).ToArray())
        .ToArray();

    /// <summary>
    /// An illustrative readiness score for each day of the season, from 0 to 100, and no vendor's, whose formulas are
    /// unpublished: 60, plus 10 for each standard deviation the night before's HRV sits above the mean of the 28 nights before
    /// it — the baseline the HRV chart draws — plus half the day's form, rounded and held between 0 and 100.
    /// </summary>
    public static IReadOnlyList<double> Readiness(Season season)
    {
        var rolling = Statistics.Rolling(season.Hrv.Select(v => (double?)v).ToArray(), BaselineNights);
        return Enumerable.Range(0, Weeks * 7).Select(day =>
        {
            var night = day + BaselineNights;
            var window = rolling[night - 1]!;
            return Math.Clamp(Math.Round(60 + 10 * (season.Hrv[night] - window.Mean) / window.Deviation + .5 * season.Load[day].Form), 0, 100);
        }).ToArray();
    }

    /// <summary>Three illustrative scores for a run, no vendor's, each out of 100 and rounded: pacing, 100 less twice the spread of its
    /// kilometre splits as a percentage of their mean; effort, 100 less its training stress's distance from 60, the stress the progression is
    /// planned at; and steadiness, 100 less twice the spread of its heart rate as a percentage of its mean.</summary>
    public static IReadOnlyList<(string Name, double Score)> SessionScores(Session run)
    {
        static double Spread(IReadOnlyList<double> values) { var mean = values.Average(); return Math.Sqrt(values.Average(v => (v - mean) * (v - mean))) / mean * 100; }
        static double Score(double value) => Math.Round(Math.Clamp(value, 0, 100));
        var splits = Splits(run);
        return [("Pacing", Score(100 - 2 * Spread(splits))), ("Effort", Score(100 - Math.Abs(run.Stress - 60))), ("Steadiness", Score(100 - 2 * Spread(run.Track!.HeartRate)))];
    }

    /// <summary>Readiness in three tiers, as WHOOP colours recovery: low to 33, moderate to 66 and good above, in the red, gold
    /// and green of <paramref name="zones"/>, a brand's zone ramp.</summary>
    public static ZoneScale ReadinessZones(IReadOnlyList<string> zones) =>
        new([new("Low", 33, zones[5]), new("Moderate", 66, zones[3]), new("Good", double.PositiveInfinity, zones[2])]);

    /// <summary>A day's training stress in four illustrative tiers, no vendor's: easy to 50, moderate to 100, hard to 150 and very
    /// hard above, in the blue, green, gold and red of <paramref name="zones"/>, a brand's zone ramp.</summary>
    public static ZoneScale StressTiers(IReadOnlyList<string> zones) =>
        new([new("Easy", 50, zones[1]), new("Moderate", 100, zones[2]), new("Hard", 150, zones[3]), new("Very hard", double.PositiveInfinity, zones[5])]);

    /// <summary>The day's training as three rings: active calories from its runs at <see cref="BodyMass"/>, against 1000; its
    /// minutes, against 45; and its training stress, against yesterday's fitness, which is the athlete's average day.</summary>
    public static IReadOnlyList<(string Name, double Value, string Unit, double Goal)> Activity(Season season, DateOnly day)
    {
        var sessions = season.Sessions.Where(s => s.Day == day).ToArray();
        var fitness = season.Load.Single(d => d.Day == day.AddDays(-1)).Fitness;
        return [("Move", Math.Round(sessions.Where(s => s.Sport == Sport.Run).Sum(s => s.Metres) / 1000 * BodyMass), "kcal", 1000),
            ("Exercise", Math.Round(sessions.Sum(s => s.Seconds) / 60), "min", 45),
            ("Stress", season.Load.Single(d => d.Day == day).Stress, "TSS", Math.Round(fitness))];
    }

    /// <summary>How many nights the sleep charts show, the last of the season.</summary>
    public const int SleepNights = 14;
    /// <summary>The sleep stages, in the order their lanes stand, awake at the top.</summary>
    public static readonly IReadOnlyList<string> SleepStages = ["Awake", "REM", "Light", "Deep"];

    /// <summary>
    /// The last <see cref="SleepNights"/> nights of the season, each simulated from a seed of its own so the training is untouched.
    /// A night reads the HRV the HRV chart draws for it, measured as the readiness score measures it, in standard deviations from
    /// the mean of the 28 nights before: each one above it adds a tenth to every cycle's deep sleep and takes a beat and a half
    /// off the night's lowest heart rate. A day of 150 or more training stress sends the athlete to bed a quarter of an hour
    /// early, and a Friday or Saturday night twenty minutes late. Sleep runs in cycles of about ninety minutes, each light, deep,
    /// light again and REM, the deep sleep shrinking and the REM growing through the night, with a few minutes awake falling
    /// asleep, now and then between cycles, and before rising. Every stage starts and ends on a whole minute.
    /// </summary>
    public static IReadOnlyList<Night> Nights(Season season)
    {
        var rolling = Statistics.Rolling(season.Hrv.Select(v => (double?)v).ToArray(), BaselineNights);
        var nights = new List<Night>();
        for (var day = Weeks * 7 - SleepNights; day < Weeks * 7; day++)
        {
            var random = new Random(7000 + day);
            double Noise(double deviation) => deviation * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(Math.Tau * random.NextDouble());
            static double Minute(double seconds) => Math.Round(seconds / 60) * 60;
            var morning = Start.AddDays(day);
            var window = rolling[day + BaselineNights - 1]!;
            var z = (season.Hrv[day + BaselineNights] - window.Mean) / window.Deviation;
            var hard = season.Load[day - 1].Stress >= 150;
            var late = morning.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var bedtime = Minute(82200 + Noise(900) - (hard ? 900 : 0) + (late ? 1200 : 0));
            var wake = Minute(109800 + Noise(720) + (morning.DayOfWeek == DayOfWeek.Saturday ? 1800 : 0));
            var stages = new List<(string Stage, double From, double To)>();
            var at = bedtime;
            // A stage runs on from where the last one ended; one that follows itself lengthens it.
            void Add(string stage, double until)
            {
                var to = Minute(until);
                if (to <= at) return;
                if (stages.Count > 0 && stages[^1].Stage == stage) stages[^1] = (stage, stages[^1].From, to);
                else stages.Add((stage, at, to));
                at = to;
            }
            Add("Awake", at + 360 + random.NextDouble() * 600);
            var rise = wake - Minute(180 + random.NextDouble() * 420);
            var start = at;
            var cycles = Math.Max(3, (int)Math.Round((rise - start) / 5400));
            for (var c = 0; c < cycles; c++)
            {
                var end = c == cycles - 1 ? rise : start + (rise - start) * (c + 1) / cycles;
                if (c > 0 && random.NextDouble() < .45) Add("Awake", at + 60 + random.NextDouble() * 150);
                var deep = Math.Max(0, .36 - .1 * c) * Math.Clamp(1 + .1 * z, .6, 1.4);
                var rem = Math.Min(.1 + .07 * c, .38);
                var light = 1 - deep - rem;
                var room = end - at;
                Add("Light", at + room * light * .55);
                Add("Deep", at + room * deep);
                Add("Light", at + room * light * .45);
                Add("REM", end);
            }
            Add("Awake", wake);
            nights.Add(new(morning, bedtime, wake, stages, Math.Round(47 - 1.5 * z + (hard ? 2 : 0) + Noise(1))));
        }
        return nights;
    }

    /// <summary>The heart rate of each day the sleep charts show: its lowest the night before's, its highest the peak of the day's
    /// sessions or, on a day without one, an ordinary day's 105 to 125, and its average 17 to 21 beats above the lowest, and more
    /// for every hour trained.</summary>
    public static IReadOnlyList<(DateOnly Day, double Low, double High, double Average)> HeartRates(Season season, IReadOnlyList<Night> nights) =>
        nights.Select(night =>
        {
            var random = new Random(9000 + night.Morning.DayNumber);
            var sessions = season.Sessions.Where(s => s.Day == night.Morning).ToArray();
            var high = Math.Max(105 + random.Next(0, 21), sessions.Length == 0 ? 0 : sessions.Max(s => s.PeakHeartRate));
            var trained = sessions.Sum(s => s.Seconds) / 3600;
            return (night.Morning, night.LowestHeartRate, high, Math.Round(night.LowestHeartRate + 17 + random.Next(0, 5) + trained * 4));
        }).ToArray();

    /// <summary>Seconds as hours and minutes, the way a sleep app writes them: <c>7 h 21 min</c>, or <c>48 min</c> under an hour.</summary>
    public static string HoursMinutes(double seconds)
    {
        var minutes = Math.Round(seconds / 60);
        return minutes < 60 ? $"{Text(minutes)} min" : $"{Text(Math.Floor(minutes / 60))} h {Text(minutes % 60)} min";
    }

    /// <summary>Seconds since a midnight as the clock reads them, the way the sleep-timing chart writes them.</summary>
    public static string ClockTime(double seconds) => new Axis(AxisKind.Linear, 0, 1) { ValueFormat = ValueFormat.TimeOfDay }.Format(seconds);

    /// <summary>The headline numbers above the dashboard, each a value and what it is.</summary>
    public static IReadOnlyList<(string Value, string Label)> Facts()
    {
        var today = Season.Load[Weeks * 7 - 1];
        return [(Text(today.Fitness), "Fitness today"), (Signed(today.Form), "Form today"),
            (Clock(Records(Season)[^1].Seconds), "5 km record"), ($"{Text(Training.CriticalPower(MonthBest(Season, 9))!.CriticalPower)} W", "Critical power")];
    }

    /// <summary>The latest session in a line: when, how far, how long, how fast, how hard.</summary>
    public static string SessionSummary()
    {
        var run = Season.Sessions[^1];
        return $"{run.Day.ToString("dddd d MMMM", CultureInfo.InvariantCulture)} · {Text(run.Metres / 1000, "0.0")} km in {Clock(run.Seconds)} · "
            + $"{Clock(run.Seconds / run.Metres * 1000)} per km · {Text(run.Track!.HeartRate.Average())} bpm average · stress {Text(run.Stress)}";
    }

    public static double When(DateOnly day) => TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
    private static string Day(DateOnly day) => day.ToString("d MMM", CultureInfo.InvariantCulture);
    private static string Text(double value, string format = "0") => value.ToString(format, CultureInfo.InvariantCulture);
    private static string Signed(double value) => Math.Round(value) switch { > 0 and var v => "+" + Text(v), < 0 and var v => "−" + Text(-v), _ => "0" };
    /// <summary>Seconds as the charts write them: m:ss, or h:mm:ss from an hour.</summary>
    public static string Clock(double seconds) => new Axis(AxisKind.Linear, 0, 1) { ValueFormat = ValueFormat.Duration }.Format(seconds);

    /// <summary>
    /// Every chart on the page, in the order it appears, drawn on <paramref name="theme"/>. Marks the page colours itself — the
    /// time-in-zone bars, the weekly zones, the HRV statuses and the grades, and the daily stress, the HRV baseline and the
    /// elevation fill in its first, neutral grey — take <paramref name="zones"/>, which should be the zone ramp of the brand they
    /// are drawn in, so they match the zone colours the library draws and clear contrast on its background. A wide chart is
    /// drawn 1100 units across and the others 540, a desktop's widths, until the page fits each to its card. The activity
    /// stream marks where the progression steps up when <paramref name="markers"/> is set.
    /// </summary>
    public static IReadOnlyList<SportsCard> Cards(ChartTheme theme, IReadOnlyList<string> zones, bool markers = true)
    {
        const int wide = 1100, half = 540;
        var season = Season;
        var load = season.Load;
        var run = season.Sessions[^1];
        var track = run.Track!;
        ChartSpec Chart(int width, int height) => new() { Theme = theme, Width = width, Height = height, Source = Source };
        string WeekOf(int week) => Day(Start.AddDays(7 * week));

        // This morning's readiness on a gauge tinted by its tiers, with the average of the 28 days before as a tick, and the day's
        // training as rings, each in a zone colour of the brand: move red, exercise green and stress blue.
        var scores = Readiness(season);
        var tiers = ReadinessZones(zones);
        var readiness = Chart(half, 360) with
        {
            Kind = ChartKind.Gauge,
            Title = $"Readiness {Text(scores[^1])}, {tiers.Zones[tiers.IndexOf(scores[^1])].Name.ToLowerInvariant()}", Description = "Last night's HRV against its baseline, and today's form",
            YZones = tiers, Annotations = [new(AnnotationAxis.Y, Math.Round(scores.SkipLast(1).TakeLast(BaselineNights).Average())) { Label = "28-day average" }],
            Series = [new("Readiness", [new(0, scores[^1], "Readiness")])]
        };
        var activity = Activity(season, Today);
        var rings = Chart(half, 360) with
        {
            Kind = ChartKind.Ring,
            Title = $"{activity.Count(a => a.Value >= a.Goal)} of {activity.Count} rings closed", Description = $"{run.Name} · the day's training against its goals",
            Series = activity.Select((a, i) => new ChartSeries(a.Name, [new(0, a.Value, a.Unit)], zones[new[] { 5, 2, 1 }[i]]) { Goal = a.Goal }).ToArray()
        };

        // Performance management in two panes: fitness and fatigue over the daily stress, in the ramp's neutral grey so that the lines
        // read over it, and form beneath on an axis held symmetric about zero and written with its sign, so fresh and tired stand either
        // side of its middle. The planned weeks are shaded through both panes and dashed from the first planned day, ending on race day, their
        // band labelled without its dates, which are too wide for it, and the shared readout reads every series at the day under the pointer or the focused point.
        var planned = When(Today.AddDays(1));
        var end = load[^1];
        var performance = Chart(wide, 500) with
        {
            Kind = ChartKind.Line, XAxis = AxisKind.Time, SharedReadout = true,
            Title = $"Fitness {Text(end.Fitness)} on race day, form {Signed(end.Form)}", Description = "Fitness, fatigue and daily stress above, form below · two planned weeks shaded",
            XLabel = "Day (UTC)", YLabel = "Training stress per day",
            Panes = [new() { Label = "Form", Weight = .5, YSymmetric = 10, YFormat = ValueFormat.Signed }],
            Annotations = [new(AnnotationAxis.X, planned) { To = When(end.Day), Label = "Planned" }],
            Series = [
                ChartSeries.From("Fitness", load, d => When(d.Day), d => Math.Round(d.Fitness, 1)) with { ProjectedFrom = planned, HighlightLast = true },
                ChartSeries.From("Fatigue", load, d => When(d.Day), d => Math.Round(d.Fatigue, 1)) with { ProjectedFrom = planned },
                ChartSeries.From("Form", load, d => When(d.Day), d => Math.Round(d.Form, 1)) with { Pane = 1, ProjectedFrom = planned },
                ChartSeries.From("Daily stress", load, d => When(d.Day), d => d.Stress) with { Kind = ChartKind.Column, Color = zones[0] }]
        };

        // The first session planned after today, as the structured workout it was planned as: each step as long as it should take
        // and as high as its target power, in Coggan's levels at the threshold it is planned against, so its repeats stand out. Its
        // stress is the column the performance chart projects for its day.
        var next = season.Upcoming[0];
        var steps = Workout(next);
        var repeats = steps.Where(step => step.Label!.StartsWith("Repeat")).ToArray();
        var workout = Chart(wide, 360) with
        {
            Kind = ChartKind.Blocks, XFormat = ValueFormat.Duration, IncludeZero = true,
            Title = $"{next.Day.ToString("dddd", CultureInfo.InvariantCulture)}: {next.Name.ToLowerInvariant()}, stress {Text(next.Stress)}",
            Description = $"{HoursMinutes(steps[^1].XEnd!.Value)} · {repeats.Length} × {(next.Sport == Sport.Run ? repeats[0].Label!.Split(", ")[1] : HoursMinutes(repeats[0].XEnd!.Value - repeats[0].X))} at {Text(repeats[0].Y!.Value)} W, threshold {Text(next.Ftp)} W",
            XLabel = "Planned time", YLabel = "Target power (W)",
            Annotations = [new(AnnotationAxis.Y, next.Ftp) { Label = "Threshold" }],
            Series = [new("Plan", steps) { Zones = ZoneScale.CogganPower(next.Ftp) }]
        };

        // Weekly load inside a band of 80 to 130 % of the four weeks before, the weeks before the season counted at the seed.
        var weekly = WeeklyLoad(season);
        double Prior(int week) => Enumerable.Range(week - 4, 4).Average(k => k < 0 ? SeedFitness * 7 : weekly[k]);
        var (low, high) = (Prior(Weeks - 1) * .8, Prior(Weeks - 1) * 1.3);
        var weeklyLoad = Chart(half, 360) with
        {
            Kind = ChartKind.Column,
            Title = $"{Text(weekly[^1])} this week, {(weekly[^1] > high ? "above" : weekly[^1] < low ? "below" : "inside")} its range",
            Description = "Stress per week against the four weeks before",
            XLabel = "Week beginning", YLabel = "Training stress per week",
            Series = [
                new("Weekly load", weekly.Select((stress, week) => new ChartPoint(week, stress, WeekOf(week))).ToArray()),
                new("Four-week average", Enumerable.Range(0, Weeks).Select(week => ChartPoint.Interval(week, Math.Round(Prior(week)),
                    Math.Round(Prior(week) * .8), Math.Round(Prior(week) * 1.3), WeekOf(week))).ToArray()) { Kind = ChartKind.Band }]
        };

        // The season's days as a contribution grid, each in its tier of training stress, and this month's runs as bubbles sized by
        // their distance on a grid of the whole month, today outlined in both.
        var trained = load.Take(Weeks * 7).ToArray();
        var calendar = Chart(half, 360) with
        {
            Kind = ChartKind.Calendar, XAxis = AxisKind.Time,
            Title = $"{Text(trained.Count(d => d.Stress > 0))} days trained, {Text(trained.Count(d => d.Stress > 100))} of them hard",
            Description = "Each day's training stress, in tiers",
            YZones = StressTiers(zones), Annotations = [new(AnnotationAxis.X, When(Today)) { Label = "Today" }],
            Series = [ChartSeries.From("Training stress", trained, d => When(d.Day), d => (double?)d.Stress)]
        };
        var first = new DateOnly(Today.Year, Today.Month, 1);
        var runs = season.Sessions.Where(s => s.Sport == Sport.Run && s.Day >= first && s.Day <= Today).ToArray();
        var month = Chart(half, 360) with
        {
            Kind = ChartKind.Calendar, XAxis = AxisKind.Time, CalendarLayout = CalendarLayout.Months, CalendarCell = CalendarCell.Bubble,
            XMin = When(first), XMax = When(first.AddMonths(1).AddDays(-1)),
            Title = $"{Text(runs.Sum(r => r.Metres) / 1000)} km run in {first.ToString("MMMM", CultureInfo.InvariantCulture)}, {runs.Length} runs",
            Description = "Each day's running in km, the longest run filling its day",
            YZones = new([new("Run", double.PositiveInfinity, zones[1])]), Annotations = [new(AnnotationAxis.X, When(Today)) { Label = "Today" }],
            Series = [new("Distance (km)", runs.Select(r => new ChartPoint(When(r.Day), Math.Round(r.Metres / 1000, 1), r.Name)).ToArray())]
        };

        // Time in each zone per week, stacked from recovery up.
        var weeklyZones = WeeklyZones(season);
        var easy = weeklyZones.Sum(week => week[0] + week[1]) / weeklyZones.Sum(week => week.Sum());
        var distribution = Chart(half, 360) with
        {
            Kind = ChartKind.StackedColumn, YFormat = ValueFormat.Duration,
            Title = $"{Text(easy * 100)} % of the season below tempo", Description = "Time in each heart-rate zone, every session",
            XLabel = "Week beginning", YLabel = "Time per week",
            Series = HeartZones.Zones.Select((zone, z) => new ChartSeries(zone.Name,
                Enumerable.Range(0, Weeks).Select(week => new ChartPoint(week, weeklyZones[week][z], WeekOf(week))).ToArray(), zones[z])).ToArray()
        };

        // The last session, in three panes over its elapsed time, marked where the progression steps up unless the page finds the
        // chart too narrow for the markers' labels to stand clear of the zones'.
        ChartPoint[] Over(IReadOnlyList<double> values) => values.Select((value, i) => new ChartPoint(i * RunSample, value)).ToArray();
        var stream = Chart(wide, 640) with
        {
            Kind = ChartKind.Line, XFormat = ValueFormat.Duration,
            Title = $"{Text(run.Metres / 1000, "0.0")} km in {Clock(run.Seconds)}, {Text(track.HeartRate.Average())} bpm average",
            Description = $"{run.Day.ToString("dddd d MMMM", CultureInfo.InvariantCulture)} · {run.Name.ToLowerInvariant()}",
            XLabel = "Elapsed time", YLabel = "Heart rate (bpm)", YZones = HeartZones,
            Annotations = !markers ? [] : new[] { 4, 8, 11 }.Select(km => new ChartAnnotation(AnnotationAxis.X, Math.Round(TimeAt(track, run.Metres, km * 1000))) { Label = $"{km} km" }).ToArray(),
            Panes = [new() { Label = "Pace (min/km)", Weight = .6, YFormat = ValueFormat.Duration, YReversed = true }, new() { Label = "Elevation (m)", Weight = .4 }],
            Series = [
                new("Heart rate", Over(track.HeartRate)) { Zones = HeartZones, Markers = MarkerStyle.None },
                new("Pace", Over(track.Pace)) { Pane = 1, Markers = MarkerStyle.None },
                new("Elevation", Over(track.Elevation)) { Pane = 2, Kind = ChartKind.Area, Curve = LineCurve.Smooth, Fill = AreaFill.Fade, Markers = MarkerStyle.None }]
        };

        // Time in zone over the samples the stream draws, each zone labelled with its whole-bpm range.
        var seconds = Training.TimeInZone(track.HeartRate, HeartZones, RunSample);
        string Range(int z) => z == 0 ? $"{HeartZones.Zones[0].Name} ≤{Text(Math.Floor(HeartZones.Zones[0].Upper))}"
            : z == HeartZones.Zones.Count - 1 ? $"{HeartZones.Zones[z].Name} ≥{Text(Math.Floor(HeartZones.Zones[z - 1].Upper) + 1)}"
            : $"{HeartZones.Zones[z].Name} {Text(Math.Floor(HeartZones.Zones[z - 1].Upper) + 1)}–{Text(Math.Floor(HeartZones.Zones[z].Upper))}";
        var most = Enumerable.Range(0, seconds.Count).MaxBy(z => seconds[z]);
        // Room past the longest bar, in whole ten minutes, for the value written at its end.
        var timeInZone = Chart(half, 360) with
        {
            Kind = ChartKind.Bar, YFormat = ValueFormat.Duration, YMax = Math.Ceiling(seconds[most] * 1.2 / 600) * 600,
            Title = $"{Text(seconds[most] / run.Seconds * 100)} % of the run in {HeartZones.Zones[most].Name.ToLowerInvariant()}",
            Description = "The same run's heart rate, counted by zone",
            XLabel = "Zone (bpm)", YLabel = "Time in zone",
            Series = [new("Time in zone", HeartZones.Zones.Select((_, z) => new ChartPoint(z, seconds[z], Range(z)) { Color = zones[z] }).ToArray()) { ValueLabels = true }]
        };

        // The same time in zone as one strip of shares, each zone in its colour with its whole percentage in the key under it.
        var zoneStrip = Chart(half, 360) with
        {
            Kind = ChartKind.Strip, YFormat = ValueFormat.Duration,
            Title = $"{Text(seconds[most] / run.Seconds * 100)} % in {HeartZones.Zones[most].Name.ToLowerInvariant()}", Description = "The same run's heart rate, as shares of its time",
            Series = [new("Time in zone", HeartZones.Zones.Select((zone, z) => new ChartPoint(z, seconds[z], zone.Name) { Color = zones[z] }).ToArray())]
        };

        // Three illustrative scores for the same run, no vendor's, each out of 100 on a track: how evenly it was paced, how close it ran
        // to its plan's effort, and how steadily the heart rate held within each step.
        var scored = SessionScores(run);
        var sessionScores = Chart(half, 240) with
        {
            // No ticks, so no gridlines cross the tracks in any brand, and nothing written under the plot.
            Kind = ChartKind.Bar, YMin = 0, YMax = 100, BarTrack = true, YTickLabels = TickLabels.None, YTickValues = [], DrawTitles = false,
            Title = $"Session scores: {string.Join(", ", scored.Select(s => $"{s.Name.ToLowerInvariant()} {Text(s.Score)}"))}", Description = "Illustrative scores for the same run, each out of 100",
            Series = [new("Score", scored.Select((s, i) => new ChartPoint(i, s.Score, s.Name)).ToArray()) { ValueLabels = true }]
        };

        // Kilometre splits, faster higher.
        var splits = Splits(run);
        var pace = Chart(half, 360) with
        {
            Kind = ChartKind.Line, YFormat = ValueFormat.Duration, YReversed = true,
            Title = $"From {Clock(splits[0])} to {Clock(splits[^1])} per km", Description = "The same run, kilometre by kilometre",
            XLabel = "Kilometre", YLabel = "Split (min per km)",
            Annotations = [new(AnnotationAxis.Y, GoalPace) { Label = "10 km goal pace" }],
            Series = [new("Split", splits.Select((split, k) => new ChartPoint(k + 1, split, $"km {k + 1}")).ToArray()) { Trend = true, Markers = MarkerStyle.Filled }]
        };

        // The same run in its laps, one for each step of the progression, ending where the stream marks the steps: each as wide as
        // it is long and as high as its pace on a reversed axis, faster higher, with the run's average pace across them. It shares
        // its distance axis with the elevation below it, so a slow lap can be read against its climb.
        var laps = Laps(run);
        var lapChart = Chart(wide, 300) with
        {
            Kind = ChartKind.Blocks, YFormat = ValueFormat.Duration, YReversed = true,
            Title = $"{laps.Count} laps from {Clock(laps[0].Y!.Value)} to {Clock(laps[^1].Y!.Value)} per km", Description = "The same run, lap by lap, each as wide as it is long",
            XLabel = "Distance (km)", YLabel = "Pace (min per km)",
            Annotations = [new(AnnotationAxis.Y, run.Seconds / run.Metres * 1000) { Label = "Average" }],
            Series = [new("Laps", laps)]
        };

        // The same laps as columns of their average heart rate, filled by value from the ramp's gold at their base to its red at the hardest
        // lap, so each column shades towards red as it rises and the hardest reaches it; each lap's pace is written under its name.
        var lapHearts = LapHeartRates(run);
        var lapColumns = Chart(half, 360) with
        {
            Kind = ChartKind.Column,
            Title = $"{Text(lapHearts[0])} to {Text(lapHearts[^1])} bpm over {laps.Count} laps", Description = "The same run, lap by lap, each lap's pace under its name",
            XLabel = "Lap, its pace per km under it", YLabel = "Average heart rate (bpm)",
            Series = [new("Heart rate", lapHearts.Select((bpm, i) => new ChartPoint(i, bpm, $"Lap {i + 1}") { SubLabel = Clock(laps[i].Y!.Value) }).ToArray())
                { ValueLabels = true, Gradient = [new(0, zones[3]), new(lapHearts.Max(), zones[5])] }]
        };

        // The route every 100 m, each stretch in the colour of its grade: descending, level, climbing and steep. The fill is the
        // ramp's neutral grey, so it is not read as a grade.
        var marks = Enumerable.Range(0, (int)(run.Metres / 100) + 1).Select(k => k * 100d).ToArray();
        var heights = marks.Select(at => Math.Round(ElevationAt(track, at), 1)).ToArray();
        double Grade(int k) => k + 1 < heights.Length ? (heights[k + 1] - heights[k]) / 100 : (heights[k] - heights[k - 1]) / 100;
        string Steepness(double grade) => grade < -.02 ? zones[1] : grade < .02 ? zones[2] : grade < .05 ? zones[3] : zones[5];
        var climbing = heights.Zip(heights.Skip(1), (a, b) => Math.Max(0, b - a)).Sum();
        var elevation = Chart(wide, 300) with
        {
            Kind = ChartKind.Area,
            Title = $"{Text(climbing)} m of climbing over {Text(run.Metres / 1000, "0.0")} km", Description = "The same route, each 100 m in the colour of its grade",
            XLabel = "Distance (km)", YLabel = "Elevation (m)",
            Series = [new("Elevation", marks.Select((at, k) => new ChartPoint(at / 1000, heights[k], $"{Text(at / 1000, "0.0")} km, grade {Text(Grade(k) * 100, "+0.0;−0.0;0.0")} %")
                { Color = Steepness(Grade(k)) }).ToArray(), zones[0]) { Fill = AreaFill.Fade }]
        };

        // The invented long ride, channel by channel: six plots of equal height on one elapsed-time axis, each named above it with its
        // average, highest and lowest, worked out here with a missing sample left out, never counted as zero, and no tick labels up the
        // side. The headers are short, their key fact first, so each stands whole on a phone. Each channel's 7,200 seconds are drawn as 600 averages of about 12 seconds, so the lines read as the ride rather than as
        // its noise, and the shared readout reads all six at once. Speed is recorded in metres per second and drawn in km/h.
        var ride = LongRide();
        string Header(string name, IEnumerable<double?> values, string unit, string format = "0")
        {
            var present = values.OfType<double>().ToArray();
            return $"{name} · avg {Text(present.Average(), format)} · max {Text(present.Max(), format)} · min {Text(present.Min(), format)} {unit}";
        }
        ChartSeries Channel(string name, IEnumerable<double?> values, int pane) =>
            new(name, values.Select((value, t) => new ChartPoint(t, value)).ToArray()) { Pane = pane, Markers = MarkerStyle.None, StrokeWidth = 1.5 };
        IEnumerable<double?> Present(IEnumerable<double> values) => values.Select(value => (double?)value);
        var kmh = ride.Speed.Select(metres => (double?)Math.Round(metres * 3.6, 1)).ToArray();
        var channels = Chart(wide, 760) with
        {
            Kind = ChartKind.Line, XFormat = ValueFormat.Duration, Sampling = SamplingMethod.Average, MaxRenderedPoints = 600,
            YTickLabels = TickLabels.None, PaneTitles = PaneTitlePlacement.Above, SharedReadout = true,
            Title = $"{Text(ride.Speed.Sum() / 1000, "0.0")} km in {Clock(RideSeconds)}, {Text(ride.Power.Average())} W average",
            Description = "An invented long ride, one sample a second, each channel averaged over 12 seconds",
            XLabel = "Elapsed time", YLabel = Header("HR", ride.HeartRate, "bpm"),
            Panes = [
                new() { Label = Header("Power", Present(ride.Power), "W"), Weight = 1 },
                new() { Label = Header("Cadence", Present(ride.Cadence), "rpm"), Weight = 1 },
                new() { Label = Header("Speed", kmh, "km/h"), Weight = 1 },
                new() { Label = Header("Elevation", Present(ride.Elevation), "m"), Weight = 1 },
                new() { Label = Header("Temp", Present(ride.Temperature), "°C", "0.0"), Weight = 1 }],
            Series = [Channel("Heart rate", ride.HeartRate, 0), Channel("Power", Present(ride.Power), 1), Channel("Cadence", Present(ride.Cadence), 2),
                Channel("Speed", kmh, 3), Channel("Elevation", Present(ride.Elevation), 4), Channel("Temperature", Present(ride.Temperature), 5)]
        };

        // Power for every duration over the rides of each month, and the critical-power fit to this month's.
        var september = MonthBest(season, 9);
        var august = MonthBest(season, 8);
        var fit = Training.CriticalPower(september)!;
        var change = fit.CriticalPower - Training.CriticalPower(august)!.CriticalPower;
        ChartSeries Curve(string name, IReadOnlyList<(double Seconds, double Value)> curve) => ChartSeries.From(name, curve, p => p.Seconds, p => (double?)p.Value);
        var power = Chart(half, 360) with
        {
            Kind = ChartKind.Line, XAxis = AxisKind.Log, XFormat = ValueFormat.Duration,
            Title = $"Critical power {Text(fit.CriticalPower)} W, {(change >= 0 ? "up" : "down")} {Text(Math.Abs(change))} W",
            Description = "Best power over every ride, September and August",
            XLabel = "Duration (log scale)", YLabel = "Power (W)",
            Annotations = [new(AnnotationAxis.Y, Math.Round(fit.CriticalPower)) { Label = "Critical power" }],
            Series = [Curve("September", september), Curve("August", august)]
        };

        // This month's best efforts at five durations as columns, each its watts above it and its watts per kilogram of the athlete's body
        // mass under its name: the power curve's points read one by one.
        string Lasting(double seconds) => seconds < 60 ? $"{Text(seconds)}s" : $"{Text(seconds / 60)}m";
        var efforts = september.Where(p => p.Seconds is 5 or 60 or 300 or 1200 or 3600).ToArray();
        var twenty = efforts.Single(p => p.Seconds == 1200).Value;
        var bestEfforts = Chart(half, 360) with
        {
            Kind = ChartKind.Column,
            Title = $"{Text(twenty)} W for 20 min, {Text(twenty / BodyMass, "0.0")} W/kg", Description = "September's best average power at five durations, watts per kilogram under each",
            XLabel = "Duration, W/kg under it", YLabel = "Power (W)",
            Series = [new("Best power", efforts.Select((p, i) => new ChartPoint(i, p.Value, Lasting(p.Seconds)) { SubLabel = Text(p.Value / BodyMass, "0.0") }).ToArray()) { ValueLabels = true }]
        };

        // The 5 km record as a step held until it falls, over each week's fastest 5 km inside a run, on the day it was run.
        var records = Records(season);
        var setOn = records.First(r => r.Seconds == records[^1].Seconds).Day;
        // The axis starts a whole minute faster than the record, so a labelled tick stands near the record line.
        var best = Chart(half, 360) with
        {
            Kind = ChartKind.Line, XAxis = AxisKind.Time, YFormat = ValueFormat.Duration, YReversed = true, YMin = Math.Floor(records[^1].Seconds / 60) * 60 - 60,
            Title = $"5 km record {Clock(records[^1].Seconds)}, set {(setOn == Start ? "before the season" : Day(setOn))}", Description = "Each week's fastest 5 km in a run, faster higher",
            XLabel = "Day (UTC)", YLabel = "5 km time",
            Series = [
                ChartSeries.From("5 km record", records, r => When(r.Day), r => (double?)r.Seconds) with { Curve = LineCurve.Step, HighlightLast = true },
                ChartSeries.From("Week's fastest 5 km", WeeklyBests(season), s => When(s.Day), s => s.Best5k) with { Kind = ChartKind.Scatter }]
        };

        // Getting faster? Each week's fastest 5 km inside a run, the records chart's dots, and the fastest kilometre of each session of
        // repeats, as sparklines by index, oldest first on a reversed duration axis so faster is higher, in the ramp's neutral grey. Each
        // time faster than every one before it is ringed in the ramp's red and noted as a best, so its tooltip says why it is ringed.
        SportsLine Faster(string name, string unit, IReadOnlyList<(Session Run, double Seconds)> times)
        {
            var taken = times.Select(t => t.Seconds).ToArray();
            var bests = Bests(taken);
            var fastest = times[Array.IndexOf(taken, taken.Min())];
            var spark = new ChartSpec
            {
                Theme = theme, Kind = ChartKind.Line, Sparkline = true, Width = 120, Height = 32, YReversed = true, YFormat = ValueFormat.Duration,
                Title = $"{name}: {Clock(taken[0])} to {Clock(taken[^1])} over {taken.Length} {unit}", Description = "Oldest first, faster higher, each personal best ringed",
                Series = [new(name, times.Select((t, i) => new ChartPoint(i, t.Seconds, $"{Day(t.Run.Day)} · {t.Run.Name}")
                    { Highlight = bests[i] ? zones[5] : null, ValueNote = bests[i] ? " · PB" : null }).ToArray(), zones[0]) { StrokeWidth = 2 }]
            };
            return new(spark, name, $"{Clock(taken[0])} to {Clock(taken[^1])} over {taken.Length} {unit} · best {Clock(fastest.Seconds)} on {Day(fastest.Run.Day)} · {bests.Count(b => b)} personal bests, ringed");
        }
        var fiveK = Faster("5 km", "weeks", WeeklyBests(season).Select(s => (s, s.Best5k!.Value)).ToArray());
        var kilometre = Faster("1 km", "sessions", RepeatBests(season));
        // An invented weigh-in each Monday, in grey alone, never good or bad, on an axis at least 8 kg tall centred on the weights, so a
        // few hundred grams read as the steady weight they are: the lightest 67.7 and the heaviest 68.3 kg centre it on 68.0, and the
        // axis runs 4 kg either side, from 64 to 72.
        var (lightest, heaviest) = (WeighIns.Min(), WeighIns.Max());
        var span = Math.Max(8, heaviest - lightest);
        var weighed = new SportsLine(new ChartSpec
        {
            Theme = theme, Kind = ChartKind.Line, Sparkline = true, Width = 270, Height = 54, YMinSpan = 8,
            Title = $"Body mass: {Text(WeighIns[0], "0.0")} to {Text(WeighIns[^1], "0.0")} kg over {WeighIns.Count} weeks", Description = "An invented weigh-in each Monday, on a scale at least 8 kg tall",
            Series = [new("Body mass", WeighIns.Select((kg, i) => new ChartPoint(i, kg, Day(Start.AddDays(7 * i))) { ValueNote = " kg" }).ToArray(), zones[0]) { StrokeWidth = 2 }]
        }, "Body mass", $"{Text(WeighIns[0], "0.0")} to {Text(WeighIns[^1], "0.0")} kg over {WeighIns.Count} weigh-ins, never under {Text(lightest, "0.0")} or over {Text(heaviest, "0.0")} · scale {Text((lightest + heaviest) / 2 - span / 2)}–{Text((lightest + heaviest) / 2 + span / 2)} kg");

        // Each night against the mean and standard deviation of the nights before it, in the ramp's green inside the band, orange
        // below it and blue above it. A night's note names its status after its value, so its tooltip and accessible name say what
        // its colour shows, and the seven-night moving average runs through them in the ramp's purple, which no night is drawn in.
        var nights = season.Hrv;
        var rolling = Statistics.Rolling(nights.Select(v => (double?)v).ToArray(), BaselineNights);
        var nightly = new List<ChartPoint>(); var band = new List<ChartPoint>();
        for (var night = BaselineNights; night < nights.Count; night++)
        {
            var x = When(Start.AddDays(night - BaselineNights));
            var window = rolling[night - 1]!;
            var (floor, ceiling) = (Math.Round(window.Mean - window.Deviation, 1), Math.Round(window.Mean + window.Deviation, 1));
            band.Add(ChartPoint.Interval(x, Math.Round(window.Mean, 1), floor, ceiling));
            var (status, ink) = nights[night] < floor ? ("below", zones[4]) : nights[night] > ceiling ? ("above", zones[1]) : ("inside", zones[2]);
            nightly.Add(new(x, nights[night]) { Color = ink, ValueNote = $" {status} baseline" });
        }
        var last = nights[^1];
        var hrv = Chart(wide, 360) with
        {
            Kind = ChartKind.Line, XAxis = AxisKind.Time,
            Title = $"{Text(last)} ms last night, {(last < band[^1].Low ? "below" : last > band[^1].High ? "above" : "inside")} the baseline",
            Description = $"Overnight HRV against the {BaselineNights} nights before each",
            XLabel = "Night (UTC)", YLabel = "HRV, rMSSD (ms)",
            Series = [
                new("Baseline", band, zones[0]) { Kind = ChartKind.Band },
                new("Nightly HRV", nightly, zones[6]) { Kind = ChartKind.Scatter, Markers = MarkerStyle.Filled, Trend = true, TrendFit = TrendFit.MovingAverage }]
        };

        // The race season in two panes on one race-by-race axis, each race in the middle of its slot so its date is never cut at the
        // card's edge: the place each finished on a reversed axis, first at the top, coloured by whether it finished higher or lower
        // than the race before and written with the size of its field, and beneath it the points each earned, in the ramp's neutral
        // grey so they are not read as better or worse.
        string Ordinal(int n) => n + (n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
        var raced = Races.Select(r => r.Day.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)).ToArray();
        var highest = Races.MinBy(r => r.Position);
        var results = Chart(wide, 400) with
        {
            Kind = ChartKind.Line, XMin = -.5, XMax = Races.Count - .5, YReversed = true,
            Title = $"Up {Races[0].Position - Races[^1].Position} places this season", Description = $"Five invented races · best {Ordinal(highest.Position)} of {highest.Field}",
            XLabel = "Race", YLabel = "Position",
            Panes = [new() { Label = "Points", Weight = 1 }],
            Series = [
                new("Position", Races.Select((r, i) => new ChartPoint(i, r.Position, raced[i]) { ValueNote = r.Field is { } field ? $"/{field}" : null }).ToArray())
                    { ChangeColors = ChangeColors.LowerIsBetter, ValueLabels = true, Markers = MarkerStyle.Filled },
                new("Points", Races.Select((r, i) => new ChartPoint(i, r.Points, raced[i])).ToArray(), zones[0]) { Pane = 1, ValueLabels = true, Markers = MarkerStyle.Filled }]
        };

        // How the field of the last race finished: its times in bins by the results page's rule, each a block from where it starts to
        // where it ends, as tall as its count, in the ramp's neutral grey; the athlete's bin in its red and noted, so its name says why;
        // the median a dashed line over the bins, labelled without its time, which the axis reads; and the finishers off the chart
        // counted in the source line.
        var (bins, faster, slower) = FinishBins(Field);
        var (place, mine, median) = (Races[^1].Position, Field[Races[^1].Position - 1], Statistics.Quantile(Field, .5));
        var finish = Chart(wide, 300) with
        {
            Kind = ChartKind.Blocks, IncludeZero = true, XFormat = ValueFormat.Duration, XTickLabels = TickLabels.Bounds, YTickLabels = TickLabels.Bounds,
            Title = $"{Ordinal(place)} of {Field.Count} in {Clock(mine)}", Description = $"{Field.Count} finishers · median {Clock(median)}",
            Source = $"Off the chart: {faster} faster and {slower} slower · an invented field", XLabel = "Finish time", YLabel = "Finishers",
            Annotations = [new(AnnotationAxis.X, median) { Label = "median", ShowValue = false, InFront = true }],
            Series = [new("Finishers", bins.Select(b => ChartPoint.Block(b.From, b.To, b.Count) with
                { Color = mine >= b.From && mine < b.To ? zones[5] : null, ValueNote = mine >= b.From && mine < b.To ? " · you" : null }).ToArray(), zones[0])]
        };

        // An invented season's arc: each race at its index in the season, as far back in its field as it finished, 0 % at the front and 100 %
        // at the back on a reversed axis with three ticks set by hand. One line a discipline joins only its own races, over the others' between
        // them, and breaks at a race it did not finish. The disciplines take the brand's series colours and the legend names them.
        string[] arcKinds = ["XCC", "XCO", "XCM", "Other"];
        string ArcKind(string discipline) => arcKinds.Contains(discipline) ? discipline : "Other";
        var arc = Chart(half, 360) with
        {
            Kind = ChartKind.Line, XMin = -.5, XMax = SeasonRaces.Count - .5, YReversed = true, YMin = 0, YMax = 100, YUnit = "%",
            YTickValues = [new(0, "Front"), new(50, "Mid"), new(100, "Back")],
            Title = $"Top quarter in {SeasonRaces.Count(r => r.Place is { } p && (p - 1.0) / (r.Field - 1) < .25)} of {SeasonRaces.Count}",
            Description = "An invented season · each race's place in its field, front at the top", XLabel = "Race",
            Series = arcKinds.Select(kind => new ChartSeries(kind, SeasonRaces.Select((r, i) => (Race: r, Index: i)).Where(t => ArcKind(t.Race.Discipline) == kind)
                .Select(t => new ChartPoint(t.Index, t.Race.Place is { } p ? Math.Round(100.0 * (p - 1) / (t.Race.Field - 1)) : null, Day(t.Race.Day))
                    { ValueNote = t.Race.Place is { } q ? $" · P{q}/{t.Race.Field}" : null }).ToArray()) { Markers = MarkerStyle.Filled }).ToArray()
        };
        // An invented race's gaps to the leader lap by lap, the leader at the top: every rider in the ramp's neutral grey but the athlete,
        // in its green and wider, listed last so it is drawn over the rest; each line named at its end with its last gap, so it needs no legend.
        var gapsBehind = LapGaps();
        var signedSeconds = new Axis(AxisKind.Linear, 0, 1) { ValueFormat = ValueFormat.Signed, Unit = "s" };
        var you = LapRace.Count - 1;
        var gap = Chart(half, 360) with
        {
            Kind = ChartKind.Line, YReversed = true, YMin = 0, YFormat = ValueFormat.Signed, YUnit = "s", XLabel = "Lap",
            Title = $"You finished {signedSeconds.Format(gapsBehind[you][^1])} back",
            Description = "An invented race · seconds behind the leader at each lap",
            Series = LapRace.Select((r, k) => new ChartSeries(r.Rider, gapsBehind[k].Select((g, lap) => new ChartPoint(lap, g, lap == 0 ? "Start" : $"Lap {lap}")).ToArray(), k == you ? zones[2] : zones[0])
            {
                StrokeWidth = k == you ? 3.2 : 2, Markers = MarkerStyle.None, EndLabel = k == you ? "You" : r.Rider[^1..],
                EndNote = gapsBehind[k][^1] == 0 ? "leader" : signedSeconds.Format(gapsBehind[k][^1])
            }).ToArray()
        };

        // An invented history of four categories over four seasons as a heatmap table: each cell writes its points per start and, under it,
        // its starts as its sub-label; a cell with too few starts to rate is dashed, said in words and out of the colour scale; and each
        // season takes 72 pixels, enough for "/15 starts" under its value, so on a phone the grid scrolls sideways rather than squeezing.
        // The note after each value names the points and riders behind it, in the cell's name and in the grid table, with its own separator.
        var categories = Chart(half, 340) with
        {
            Kind = ChartKind.Heatmap, YUnit = " pts", CellText = true, CellWidth = 72,
            Title = "Points per start, by category", Description = "Invented seasons · a dashed cell has too few starts",
            Series = CategoryHistory.Select(category => new ChartSeries(category.Category, category.Seasons.Select((s, i) =>
                new ChartPoint(i, s.Starts > 0 ? Math.Round((double)s.Points / s.Starts, 1) : null, $"{FirstCategorySeason + i}")
                {
                    SubLabel = $"/{s.Starts} starts", ValueNote = s.Starts > 0 ? $" · {s.Points} pts, {s.Riders} riders" : null,
                    NotRated = s.Starts < RatedStarts ? "too few starts to rate" : null
                }).ToArray())).ToArray()
        };

        // Last night's stages, one lane each, on the clock: awake in the ramp's orange, REM blue, light sleep its neutral grey and
        // deep sleep purple. Each night runs from the midnight before it, so its seconds are added to that midnight.
        var sleep = Nights(season);
        var lastNight = sleep[^1];
        double OnClock(Night n, double seconds) => When(n.Morning.AddDays(-1)) + seconds * 1000;
        string[] stageInks = [zones[4], zones[1], zones[0], zones[6]];
        double Stage(string stage) => lastNight.Stages.Where(s => s.Stage == stage).Sum(s => s.To - s.From);
        var hypnogram = Chart(wide, 360) with
        {
            Kind = ChartKind.Timeline, XAxis = AxisKind.Time,
            Title = $"{HoursMinutes(lastNight.Wake - lastNight.Bedtime - Stage("Awake"))} asleep, {HoursMinutes(Stage("Deep"))} deep",
            Description = $"Into {lastNight.Morning.ToString("dddd d MMMM", CultureInfo.InvariantCulture)}, HRV {Text(nights[^1])} ms",
            XLabel = "Time (UTC)",
            Series = SleepStages.Select((stage, i) => new ChartSeries(stage, lastNight.Stages.Where(s => s.Stage == stage)
                .Select(s => ChartPoint.Span(OnClock(lastNight, s.From), OnClock(lastNight, s.To))).ToArray(), stageInks[i])).ToArray()
        };
        // Two weeks of bedtimes on a reversed time-of-day axis, earlier higher, from the three-hour mark before the earliest
        // bedtime to the one after the latest waking, so both ends are labelled.
        var timing = Chart(half, 360) with
        {
            Kind = ChartKind.Range, XAxis = AxisKind.Time, YFormat = ValueFormat.TimeOfDay, YReversed = true,
            YMin = Math.Floor(sleep.Min(n => n.Bedtime) / 10800) * 10800, YMax = Math.Ceiling(sleep.Max(n => n.Wake) / 10800) * 10800,
            Title = $"In bed at {ClockTime(Math.Round(sleep.Average(n => n.Bedtime) / 60) * 60)} on average, up at {ClockTime(Math.Round(sleep.Average(n => n.Wake) / 60) * 60)}",
            Description = $"Bedtime to waking, the last {SleepNights} nights, earlier higher",
            XLabel = "Night ending", YLabel = "Clock time (UTC)",
            Series = [new("Sleep", sleep.Select(n => ChartPoint.Interval(When(n.Morning), null, n.Bedtime, n.Wake, Day(n.Morning))).ToArray(), zones[1])]
        };
        // Each day's heart rate from its lowest, overnight, to its highest, the dot its average.
        var rates = HeartRates(season, sleep);
        var heartRange = Chart(half, 360) with
        {
            Kind = ChartKind.Range, XAxis = AxisKind.Time,
            Title = $"{Text(rates[^1].Low)} to {Text(rates[^1].High)} bpm today, {Text(rates[^1].Average)} on average",
            Description = "Each day's lowest and highest heart rate, the dot its average",
            XLabel = "Day", YLabel = "Heart rate (bpm)",
            Series = [new("Heart rate", rates.Select(r => ChartPoint.Interval(When(r.Day), r.Average, r.Low, r.High, Day(r.Day))).ToArray(), zones[5])]
        };

        return [
            new("today", "readiness", "Readiness", "An illustrative score, no vendor's: 60, plus 10 for each standard deviation last night's HRV sits above its 28-night baseline, plus half of today's form, on a `Gauge` whose `YZones` tint the track; the tick is the 28-day average.", false, readiness),
            new("today", "activity", "Today's activity", "The run's active calories at 1 kcal per kg per km, its minutes and its training stress, each a `Ring` series against its `Goal` — stress against fitness, the athlete's average day. Past 100 % a ring runs on over itself.", false, rings),
            new("load", "performance", "Performance management", "Daily stress as columns under fitness and fatigue, all from `Training.Load`, and form in a pane of its own beneath, its axis held symmetric about zero by `YSymmetric` and written +5 and −5 by `ValueFormat.Signed`; `ProjectedFrom` dashes the planned weeks, `HighlightLast` rings race-day fitness, and the planned weeks are shaded through both panes. `SharedReadout` reads every series at the day under the pointer or the focused point; the arrow keys step day by day.", true, performance),
            new("load", "next-session", "Next session, as planned", "The first planned session as a `ChartKind.Blocks` workout: each step a `ChartPoint.Block` as long as it should take and as high as its target power, coloured by `ZoneScale.CogganPower` at the threshold it is planned against; its stress is the performance chart's projected column for its day.", true, workout),
            new("load", "weekly-load", "Weekly load against a target", "Each week's stress in capsule columns over a `Band` series from 80 to 130 % of the four weeks before, whose centre line is their average.", false, weeklyLoad),
            new("load", "weekly-zones", "Weekly zone distribution", "Every session's `Training.TimeInZone` added up by week and stacked in the zone colours.", false, distribution),
            new("load", "training-calendar", "Training calendar", "The season's daily stress as a `ChartKind.Calendar` contribution grid, each day in its tier from `YZones` — illustrative tiers, no vendor's — and this month's runs on a `CalendarLayout.Months` grid of `CalendarCell.Bubble` days sized by distance; an X annotation outlines today in both.", true, calendar) { Beside = month },
            new("session", "stream", "Activity stream", "Heart rate coloured by zone over its `YZones` bands, pace on a reversed duration axis and elevation as a faded area, in three `Panes` on one elapsed-time axis.", true, stream),
            new("session", "time-in-zone", "Time in zone", "`Training.TimeInZone` over the same heart-rate samples the stream draws, one bar per zone in its colour.", false, timeInZone),
            new("session", "zone-strip", "Time in zone, as shares", "The same `Training.TimeInZone` as a `ChartKind.Strip`: one bar whose parts are the zones, each as long as its share of the run and parted by a gap, with a key under it of every zone and its whole percentage, adding up to exactly 100.", false, zoneStrip),
            new("session", "scores", "Session scores", "Three illustrative scores, no vendor's, out of 100 on `BarTrack` meters: each bar stands on a track to `YMax`, its value past the track's end, its name beside it, and an empty `YTickValues` with `TickLabels.None` leaves out the gridlines and ticks. `DrawTitles = false` leaves the heading to this card, the chart's title still its accessible name.", false, sessionScores) { ShowLegend = false },
            new("session", "splits", "Pace by kilometre", "Each kilometre's split on a reversed duration axis, with a `Trend` line and the race's goal pace.", false, pace),
            new("session", "laps", "Laps", "One `ChartPoint.Block` per lap of the progression, as wide as the lap is long and as high as its pace on a reversed duration axis, with the run's average pace as a reference line; its distance axis is the elevation's below.", true, lapChart),
            new("session", "lap-heart", "Heart rate by lap", "The same laps as columns of their average heart rate, each filled by a `Gradient` laid along the value axis from gold at 0, the columns' base, to red at the hardest lap, so every column shades towards red as it rises and a taller one reaches further into it; each lap's pace is its `SubLabel`, a second line under its name, said in its tooltip too.", false, lapColumns),
            new("session", "elevation", "Elevation coloured by grade", "The same route as an area, each 100 m segment taking a point `Color` from its grade band.", true, elevation),
            new("ride", "ride-channels", "Ride channels", "Six `Panes` of equal height on one elapsed-time axis, each named above its plot by `PaneTitles.Above` with its average, high and low, and no tick labels up the side, `TickLabels.None`; `SamplingMethod.Average` draws each channel's 7,200 seconds as 600 averages, the heart-rate strap's dropout stays a gap, and `SharedReadout` reads all six at the second under the pointer. Drag across the plots to zoom in on a stretch; Reset view draws the whole ride.", true, channels),
            new("fitness", "power-curve", "Power–duration curve", "`Training.MeanMaximal` over this month's rides against last month's on a logarithmic duration axis, with the `Training.CriticalPower` fit as a reference line.", false, power),
            new("fitness", "best-efforts", "Best efforts", "The power curve read at five durations, 5 seconds to an hour, as columns with their watts above them and each one's watts per kilogram as its `SubLabel` under its duration, said in its tooltip after the duration.", false, bestEfforts),
            new("fitness", "records", "5 km record progression", "Each week's fastest 5 km inside a run, and the record as a `LineCurve.Step` envelope on a reversed axis, so faster is higher.", false, best),
            new("fitness", "getting-faster", "Getting faster?", "Word-sized `Sparkline` charts, no axes, beside the numbers they draw: each week's fastest 5 km and each session's fastest kilometre, faster higher, every time faster than all before it ringed by a `Highlight` and noted `· PB` so its tooltip says why; and an invented weekly weigh-in in grey alone, its axis held at least 8 kg tall by `YMinSpan` so a few hundred grams read as steady.", true, fiveK.Spec)
                { Lines = [fiveK, kilometre, weighed] },
            new("fitness", "hrv", "HRV against its baseline", "Each night's HRV in green inside a band of the mean ± one standard deviation of the 28 nights before, from `Statistics.Rolling`, orange below it and blue above it, its `ValueNote` naming that status in its tooltip; the dashed purple line is a seven-night `TrendFit.MovingAverage`.", true, hrv),
            new("racing", "race-results", "Race results", "Five invented races in two `Panes` on one race-by-race axis: the place each finished on a reversed axis, first at the top, `ChangeColors.LowerIsBetter` drawing a race that finished higher than the one before in the style's rising colour and one that finished lower in its falling colour, and saying so in its tooltip, and `ValueLabels` writing each place with its field as a muted `ValueNote`; beneath, the points each race earned.", true, results),
            new("racing", "field", "How the field finished", "An invented field for the last race, its finish times in bins the page works out, each a `ChartPoint.Block` on an axis that `IncludeZero`; the athlete's bin in red with the `ValueNote` `· you`, so its tooltip says why, a bin of one kept 2 pixels tall, `TickLabels.Bounds` labelling only the ends of each axis, and the median a dashed X annotation `InFront` of the bins, its label drawn without its time by `ShowValue = false`; the finishers off the chart are counted in the source line.", true, finish),
            new("racing", "season-arc", "Season arc", "An invented season of ten races in three disciplines, each at its index in the season and as far back in its field as it finished, on a reversed axis whose `YTickValues` set `Front`, `Mid` and `Back` by hand; `YUnit` writes `%` after every value, each discipline's line joins only its own races, and a race not finished is a gap.", false, arc),
            new("racing", "gap", "Gap to the leader", "An invented race's eight riders lap by lap, `YReversed` from 0, the leader, written `+9.5s` by `ValueFormat.Signed` and `YUnit`; each line is named at its end by `EndLabel` and `EndNote`, moved apart where lines end close together, so the component's legend is off, and its toolbar too, as on a phone card, its status line kept for screen readers; `SharedReadout` reads every rider at the lap under the pointer. The athlete's line is green and wider and named \"You\".", false, gap with { SharedReadout = true }) { ShowLegend = false, ShowToolbar = false },
            new("racing", "places-points", "Places and points", "The same invented kind of season in two series, built by one call, `PlacingsChart.Build`, and drawn by `<LumenPlacings>`: each series its own line, so a place is better or worse only than the previous race of the same series, the field after each place, and the league's points in a pane beneath, the open races, which score none, have no points mark, never a zero, so the league's points join across them.", true,
                PlacesLook(PlacingsChart.Build(Placings, new() { Style = theme == ChartTheme.Dark ? ChartStyle.Dark : ChartStyle.Light })!)) { Placings = Placings },
            new("racing", "category-heatmap", "Category heatmap", "Invented categories by season as a `ChartKind.Heatmap` table: `CellText` writes each cell's points per start with its starts under it as a `SubLabel`; `NotRated` marks a cell with fewer than ten starts, dashed and out of the colour scale, said in its name and, when it has no value, as a dash; `CellWidth` keeps every season 72 pixels wide, wide enough for its starts under the value, so on a phone the grid scrolls sideways and stays readable; `YUnit` writes ` pts`, and each cell's `ValueNote` names the points and riders behind it. \"View data\" reads the grid as a table, a row for each category.", false, categories),
            new("sleep", "hypnogram", "Last night's sleep stages", "A `ChartKind.Timeline`: one series per stage, each period a `ChartPoint.Span`, joined where the stage changes; the higher the HRV sits above its baseline, the more deep sleep.", true, hypnogram),
            new("sleep", "sleep-timing", "Sleep timing", "Bedtime to waking as `ChartKind.Range` bars on a reversed `ValueFormat.TimeOfDay` axis, its seconds running past 24 hours so a night never crosses zero.", false, timing),
            new("sleep", "heart-range", "Daily heart rate", "Each day's lowest and highest heart rate as `ChartPoint.Interval` range bars, the dot its average; today's highest is the run's.", false, heartRange)];
    }
}
