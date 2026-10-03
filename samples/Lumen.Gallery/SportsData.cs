using System.Globalization;
using Lumen.Charts;

namespace Lumen.Gallery;

public enum Sport { Run, Ride }

/// <summary>What a run recorded, one sample every <see cref="SportsData.RunSample"/> seconds from its start: heart rate in bpm,
/// pace in seconds per kilometre, the distance covered so far in metres, and the elevation in metres.</summary>
public sealed record Track(IReadOnlyList<double> HeartRate, IReadOnlyList<double> Pace, IReadOnlyList<double> Distance, IReadOnlyList<double> Elevation);

/// <summary>One workout. Its stress is <see cref="Training.StressScore"/> of its power record, rounded, and its time in zone
/// <see cref="Training.TimeInZone"/> of its heart rate. A ride keeps its mean-maximal power; a run keeps its samples and the
/// fastest 5 km inside it.</summary>
public sealed record Session(DateOnly Day, Sport Sport, string Name, double Seconds, double Metres, double Stress,
    IReadOnlyList<double> TimeInZone, IReadOnlyList<(double Seconds, double Value)> PowerCurve, double? Best5k, Track? Track);

/// <summary>The sessions trained, the stress planned after them, <see cref="Training.Load"/> over both, and one overnight HRV
/// reading a night from four weeks before the season to its last day, so that the first night has a baseline.</summary>
public sealed record Season(IReadOnlyList<Session> Sessions, IReadOnlyList<(DateOnly Day, double Stress)> Planned,
    IReadOnlyList<LoadDay> Load, IReadOnlyList<double> Hrv);

/// <summary>A chart on the Sports &amp; performance page, with the plain title above it and a one-line note, in which code is
/// set between backticks.</summary>
public sealed record SportsCard(string Section, string Id, string Title, string Note, bool Wide, ChartSpec Spec);

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
    // Short enough to stay inside a chart drawn at a phone's width.
    public const string Source = "Source: simulated athlete · not real training data";
    public static DateOnly Today => Start.AddDays(Weeks * 7 - 1);
    public static DateOnly Race => Start.AddDays((Weeks + PlannedWeeks) * 7 - 1);

    /// <summary>Coggan's five heart-rate levels at a threshold of 170 bpm, under the short names the apps use.</summary>
    public static ZoneScale HeartZones { get; } = new(ZoneScale.CogganHeartRate(ThresholdHeartRate).Zones
        .Zip(new[] { "Recovery", "Endurance", "Tempo", "Threshold", "VO2max" }, (zone, name) => zone with { Name = name }).ToArray());

    public static Season Season => season.Value;
    private static readonly Lazy<Season> season = new(Simulate);

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
                Training.TimeInZone(heart, HeartZones), Training.MeanMaximal(watts, Training.StandardDurations), null, null);
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
                Training.TimeInZone(heart, HeartZones, RunSample), [], Fastest(track, metres, 5000), track);
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
        for (var week = 0; week < Weeks + PlannedWeeks; week++)
            foreach (var session in Week(week, volumes[week]))
                if (week < Weeks) sessions.Add(session); else planned.Add((session.Day, session.Stress));
        // The first Monday is a rest day, entered so the load model starts on it. The athlete comes in at a typical daily stress
        // for both fitness and fatigue, which starts form at zero.
        var load = Training.Load(sessions.Select(s => (s.Day, s.Stress)).Prepend((Start, 0)).Concat(planned), SeedFitness, SeedFitness);
        // Overnight HRV sits lower while form is low and the night after a hard day; before the season form was level.
        var hrv = new List<double>();
        for (var night = -BaselineNights; night < Weeks * 7; night++)
            hrv.Add(Math.Round(64 + (night < 0 ? 0 : .3 * load[night].Form - (night > 0 && load[night - 1].Stress >= 150 ? 4 : 0)) + Noise(3.2)));
        return new(sessions, planned, load, hrv);
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

    /// <summary>Each week's training stress, from the days the load model counts.</summary>
    public static IReadOnlyList<double> WeeklyLoad(Season season) =>
        Enumerable.Range(0, Weeks).Select(week => season.Load.Skip(7 * week).Take(7).Sum(day => day.Stress)).ToArray();

    /// <summary>Each week's seconds in each heart-rate zone, over every session that week.</summary>
    public static IReadOnlyList<IReadOnlyList<double>> WeeklyZones(Season season) => Enumerable.Range(0, Weeks)
        .Select(week => (IReadOnlyList<double>)Enumerable.Range(0, HeartZones.Zones.Count)
            .Select(zone => season.Sessions.Where(s => (s.Day.DayNumber - Start.DayNumber) / 7 == week).Sum(s => s.TimeInZone[zone])).ToArray())
        .ToArray();

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

        // Performance management, the planned weeks shaded and dashed from the first planned day, and the daily stress in the
        // ramp's neutral grey so that the lines read over it.
        var planned = When(Today.AddDays(1));
        var end = load[^1];
        var performance = Chart(wide, 460) with
        {
            Kind = ChartKind.Line, XAxis = AxisKind.Time,
            Title = $"Fitness {Text(end.Fitness)} on race day, form {Signed(end.Form)}", Description = "Sixteen weeks trained and two planned, shaded",
            XLabel = "Day (UTC)", YLabel = "Training stress per day", Y2Label = "Form",
            Annotations = [new(AnnotationAxis.X, planned) { To = When(end.Day), Label = "Planned" }],
            Series = [
                ChartSeries.From("Fitness", load, d => When(d.Day), d => Math.Round(d.Fitness, 1)) with { ProjectedFrom = planned, HighlightLast = true },
                ChartSeries.From("Fatigue", load, d => When(d.Day), d => Math.Round(d.Fatigue, 1)) with { ProjectedFrom = planned },
                ChartSeries.From("Form", load, d => When(d.Day), d => Math.Round(d.Form, 1)) with { Kind = ChartKind.Area, Secondary = true, ProjectedFrom = planned },
                ChartSeries.From("Daily stress", load, d => When(d.Day), d => d.Stress) with { Kind = ChartKind.Column, Color = zones[0] }]
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

        // Each night against the mean and standard deviation of the nights before it.
        var nights = season.Hrv;
        var rolling = Statistics.Rolling(nights.Select(v => (double?)v).ToArray(), BaselineNights);
        var inside = new List<ChartPoint>(); var below = new List<ChartPoint>(); var above = new List<ChartPoint>(); var band = new List<ChartPoint>();
        for (var night = BaselineNights; night < nights.Count; night++)
        {
            var x = When(Start.AddDays(night - BaselineNights));
            var window = rolling[night - 1]!;
            var (floor, ceiling) = (Math.Round(window.Mean - window.Deviation, 1), Math.Round(window.Mean + window.Deviation, 1));
            band.Add(ChartPoint.Interval(x, Math.Round(window.Mean, 1), floor, ceiling));
            (nights[night] < floor ? below : nights[night] > ceiling ? above : inside).Add(new(x, nights[night]));
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
                new("Inside baseline", inside, zones[2]) { Kind = ChartKind.Scatter, Markers = MarkerStyle.Filled },
                new("Below baseline", below, zones[4]) { Kind = ChartKind.Scatter, Markers = MarkerStyle.Filled },
                new("Above baseline", above, zones[1]) { Kind = ChartKind.Scatter, Markers = MarkerStyle.Filled }]
        };

        return [
            new("load", "performance", "Performance management", "Daily stress as columns, fitness and fatigue as lines and form as an area on the right axis, all from `Training.Load`; `ProjectedFrom` dashes the planned weeks and `HighlightLast` rings race-day fitness.", true, performance),
            new("load", "weekly-load", "Weekly load against a target", "Each week's stress in capsule columns over a `Band` series from 80 to 130 % of the four weeks before, whose centre line is their average.", false, weeklyLoad),
            new("load", "weekly-zones", "Weekly zone distribution", "Every session's `Training.TimeInZone` added up by week and stacked in the zone colours.", false, distribution),
            new("session", "stream", "Activity stream", "Heart rate coloured by zone over its `YZones` bands, pace on a reversed duration axis and elevation as a faded area, in three `Panes` on one elapsed-time axis.", true, stream),
            new("session", "time-in-zone", "Time in zone", "`Training.TimeInZone` over the same heart-rate samples the stream draws, one bar per zone in its colour.", false, timeInZone),
            new("session", "splits", "Pace by kilometre", "Each kilometre's split on a reversed duration axis, with a `Trend` line and the race's goal pace.", false, pace),
            new("session", "elevation", "Elevation coloured by grade", "The same route as an area, each 100 m segment taking a point `Color` from its grade band.", true, elevation),
            new("fitness", "power-curve", "Power–duration curve", "`Training.MeanMaximal` over this month's rides against last month's on a logarithmic duration axis, with the `Training.CriticalPower` fit as a reference line.", false, power),
            new("fitness", "records", "5 km record progression", "Each week's fastest 5 km inside a run, and the record as a `LineCurve.Step` envelope on a reversed axis, so faster is higher.", false, best),
            new("fitness", "hrv", "HRV against its baseline", "Each night's HRV, coloured by where it falls against a band of the mean ± one standard deviation of the 28 nights before, from `Statistics.Rolling`.", true, hrv)];
    }
}
