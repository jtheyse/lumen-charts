namespace Lumen.Charts;

/// <summary>A named training zone. <paramref name="Upper"/> is inclusive: the zone holds every value above the previous
/// zone's upper bound up to and including its own. The top zone of a scale is unbounded, so its upper bound is
/// <see cref="double.PositiveInfinity"/>.</summary>
public sealed record Zone(string Name, double Upper);

/// <summary>
/// Ordered zones that between them hold every number. Published tables give whole-percent ranges with a gap between
/// them — 56–75 % and then 76–90 % — and so do not say where a value in the gap belongs. Here each zone runs from above
/// the previous zone's upper bound up to and including its own, which is the rule that reproduces Allen and Coggan's
/// worked example: at an FTP of 290 W the bounds, rounded half up to whole watts, are 160, 218, 261, 305 and 348 W.
/// The bounds are kept exact rather than rounded, so active recovery at that FTP ends at 159.5 W, which an integer
/// table prints as 160.
/// </summary>
public sealed record ZoneScale(IReadOnlyList<Zone> Zones)
{
    public IReadOnlyList<Zone> Zones { get; } = Checked(Zones);

    /// <summary>The first zone whose upper bound is at least <paramref name="value"/>, or -1 for NaN, which no zone holds.</summary>
    public int IndexOf(double value)
    {
        for (var i = 0; i < Zones.Count; i++)
            if (value <= Zones[i].Upper) return i;
        return -1;
    }

    /// <summary>Coggan's seven power levels as fractions of functional threshold power: active recovery to 55 %,
    /// endurance to 75 %, tempo to 90 %, lactate threshold to 105 %, VO2max to 120 %, anaerobic capacity to 150 %,
    /// and neuromuscular above that.</summary>
    public static ZoneScale CogganPower(double ftp) => Scaled(Training.Positive(ftp, nameof(ftp)),
        [("Active recovery", .55), ("Endurance", .75), ("Tempo", .90), ("Lactate threshold", 1.05), ("VO2max", 1.20),
         ("Anaerobic capacity", 1.50), ("Neuromuscular", double.PositiveInfinity)]);

    /// <summary>Coggan's five heart-rate levels as fractions of threshold heart rate: active recovery to 68 %,
    /// endurance to 83 %, tempo to 94 %, lactate threshold to 105 %, and VO2max above that.</summary>
    public static ZoneScale CogganHeartRate(double thresholdHeartRate) => Scaled(Training.Positive(thresholdHeartRate, nameof(thresholdHeartRate)),
        [("Active recovery", .68), ("Endurance", .83), ("Tempo", .94), ("Lactate threshold", 1.05), ("VO2max", double.PositiveInfinity)]);

    private static ZoneScale Scaled(double threshold, (string Name, double Fraction)[] levels) =>
        new(levels.Select(level => new Zone(level.Name, level.Fraction * threshold)).ToArray());

    private static IReadOnlyList<Zone> Checked(IReadOnlyList<Zone> zones)
    {
        if (zones is null || zones.Count == 0) throw new ArgumentException("A zone scale needs at least one zone.");
        for (var i = 0; i < zones.Count; i++)
        {
            if (zones[i] is null || string.IsNullOrWhiteSpace(zones[i].Name)) throw new ArgumentException("Every zone needs a name.");
            var upper = zones[i].Upper;
            if (i < zones.Count - 1 ? !double.IsFinite(upper) : upper != double.PositiveInfinity)
                throw new ArgumentException("Zone upper bounds must be finite, except the last zone's, which is positive infinity.");
            if (i > 0 && upper <= zones[i - 1].Upper) throw new ArgumentException("Zone upper bounds must increase strictly.");
        }
        return zones.ToArray();
    }
}

/// <summary>Fitness, fatigue and form for one day. Form is yesterday's fitness minus yesterday's fatigue.</summary>
public sealed record LoadDay(DateOnly Day, double Stress, double Fitness, double Fatigue, double Form);

/// <summary>Monod's critical-power model fitted to maximal efforts: <see cref="CriticalPower"/> in watts and
/// <see cref="WPrime"/>, the work available above it, in joules.</summary>
public sealed record CriticalPowerFit(double CriticalPower, double WPrime, double R2, int Count);

/// <summary>
/// The numbers endurance-training charts draw, as Allen and Coggan's <i>Training and Racing with a Power Meter</i>
/// and TrainingPeaks define them. Power is in watts and time in seconds. Samples are one uniformly spaced series:
/// the sources say nothing about gaps or a changing recording rate, so fill or cut gaps before passing one in.
/// </summary>
public static class Training
{
    /// <summary>The durations a power–duration curve is usually read at, from one second to four hours.</summary>
    public static IReadOnlyList<double> StandardDurations { get; } =
        Array.AsReadOnly(new double[] { 1, 5, 10, 15, 30, 60, 120, 180, 300, 600, 1200, 1800, 3600, 5400, 7200, 10800, 14400 });

    /// <summary>
    /// Normalized power: a 30-second rolling average of power, each average raised to the fourth power, the mean of
    /// those, and its fourth root. The window is 30 seconds in whole samples, rounded half up and at least one, and the
    /// result is null when the record is shorter than one window. The sources do not define gaps or non-uniform
    /// sampling, so <paramref name="watts"/> must be uniformly sampled every <paramref name="sampleSeconds"/>.
    /// TrainingPeaks advises against reading it for efforts much under ten minutes.
    /// </summary>
    public static double? NormalizedPower(IReadOnlyList<double> watts, double sampleSeconds = 1)
    {
        var window = Samples(30, Positive(sampleSeconds, nameof(sampleSeconds)));
        double sum = 0, total = 0;
        for (var i = 0; i < watts.Count; i++)
        {
            if (!double.IsFinite(watts[i])) throw new ArgumentException("Power samples must be finite.");
            sum += watts[i];
            if (i >= window) sum -= watts[i - window];
            if (i >= window - 1) total += Math.Pow(sum / window, 4);
        }
        return watts.Count < window ? null : Math.Pow(total / (watts.Count - window + 1), .25);
    }

    /// <summary>Intensity factor: normalized power as a fraction of functional threshold power (FTP).</summary>
    public static double IntensityFactor(double normalizedPower, double ftp) =>
        NonNegative(normalizedPower, nameof(normalizedPower)) / Positive(ftp, nameof(ftp));

    /// <summary>Training stress score: seconds × NP × IF ÷ (FTP × 3600) × 100, which is hours × IF² × 100, so an hour
    /// at threshold scores 100.</summary>
    public static double StressScore(double seconds, double normalizedPower, double ftp) =>
        NonNegative(seconds, nameof(seconds)) * normalizedPower * IntensityFactor(normalizedPower, ftp) / (ftp * 3600) * 100;

    /// <summary>
    /// Fitness, fatigue and form by TrainingPeaks' published daily recurrence, which descends from Banister's
    /// impulse-response model. Each day fitness moves 1/<paramref name="fitnessDays"/> of the way from yesterday's value
    /// toward the day's stress and fatigue 1/<paramref name="fatigueDays"/> of the way; form is yesterday's fitness
    /// minus yesterday's fatigue, which on the first day are the seeds <paramref name="fitness"/> and
    /// <paramref name="fatigue"/>. Entries on one day are added together, and every day from the first entry to the
    /// last is returned, a day without an entry counting as zero stress, so planned workouts are simply later entries.
    /// Allen and Coggan suggest keeping fitness at 42 days but tuning fatigue between about 4 and 12, and seeding an
    /// athlete with no history at their typical daily stress for both, which starts form at zero.
    /// </summary>
    public static IReadOnlyList<LoadDay> Load(IEnumerable<(DateOnly Day, double Stress)> days, double fitness = 0, double fatigue = 0,
        double fitnessDays = 42, double fatigueDays = 7)
    {
        if (!double.IsFinite(fitness) || !double.IsFinite(fatigue)) throw new ArgumentException("Starting fitness and fatigue must be finite.");
        Positive(fitnessDays, nameof(fitnessDays));
        Positive(fatigueDays, nameof(fatigueDays));
        var totals = new Dictionary<DateOnly, double>();
        foreach (var (day, stress) in days)
        {
            if (!double.IsFinite(stress) || stress < 0) throw new ArgumentException("Training stress must be finite and nonnegative.");
            totals[day] = totals.GetValueOrDefault(day) + stress;
        }
        if (totals.Count == 0) return [];
        var first = totals.Keys.Min();
        var load = new LoadDay[totals.Keys.Max().DayNumber - first.DayNumber + 1];
        for (var i = 0; i < load.Length; i++)
        {
            var day = first.AddDays(i);
            var stress = totals.GetValueOrDefault(day);
            var form = fitness - fatigue;
            fitness += (stress - fitness) / fitnessDays;
            fatigue += (stress - fatigue) / fatigueDays;
            load[i] = new(day, stress, fitness, fatigue, form);
        }
        return load;
    }

    /// <summary>Seconds in each zone of <paramref name="zones"/>, in the scale's order. Each finite sample counts
    /// <paramref name="sampleSeconds"/>; NaN and infinite samples, such as dropouts, count in no zone.</summary>
    public static IReadOnlyList<double> TimeInZone(IReadOnlyList<double> samples, ZoneScale zones, double sampleSeconds = 1)
    {
        Positive(sampleSeconds, nameof(sampleSeconds));
        var counts = new int[zones.Zones.Count];
        foreach (var sample in samples)
            if (double.IsFinite(sample)) counts[zones.IndexOf(sample)]++;
        return counts.Select(count => count * sampleSeconds).ToArray();
    }

    /// <summary>
    /// The mean-maximal curve: for each duration, the best average over any run of consecutive samples that long.
    /// Durations are in seconds and rounded half up to whole samples; one shorter than a sample or longer than the
    /// record is left out. The curve is not forced to fall with duration, because the data need not: when one
    /// duration is not a whole multiple of another the longer one can score higher, as 5 seconds hard, 5 easy and
    /// 5 hard does over 15 seconds against any 10. Each duration is one pass over running totals.
    /// </summary>
    public static IReadOnlyList<(double Seconds, double Value)> MeanMaximal(IReadOnlyList<double> samples, IEnumerable<double> durations, double sampleSeconds = 1)
    {
        Positive(sampleSeconds, nameof(sampleSeconds));
        var totals = new double[samples.Count + 1];
        for (var i = 0; i < samples.Count; i++)
        {
            if (!double.IsFinite(samples[i])) throw new ArgumentException("Samples must be finite.");
            totals[i + 1] = totals[i] + samples[i];
        }
        var curve = new List<(double, double)>();
        foreach (var duration in durations)
        {
            if (double.IsNaN(duration)) throw new ArgumentException("Durations must be numbers.");
            if (duration < sampleSeconds || duration > samples.Count * sampleSeconds) continue;
            var width = Samples(duration, sampleSeconds);
            var best = double.NegativeInfinity;
            for (var end = width; end <= samples.Count; end++) best = Math.Max(best, totals[end] - totals[end - width]);
            curve.Add((duration, best / width));
        }
        return curve;
    }

    /// <summary>
    /// Monod's critical-power model, total work = W′ + CP × time, fitted by least squares to work against time over
    /// the efforts lasting 3 to 20 minutes (180 to 1200 seconds inclusive); the rest are ignored. The slope is CP and
    /// the intercept W′. Returns null without two different durations in that window. Pass maximal efforts, such as
    /// points from <see cref="MeanMaximal"/>. The model overestimates what can be held for short efforts, which is why
    /// the window starts at three minutes, and work rises with time almost regardless of the athlete, so R2 is near 1
    /// for any plausible set and says little about the fit.
    /// </summary>
    public static CriticalPowerFit? CriticalPower(IEnumerable<(double Seconds, double Watts)> efforts)
    {
        var work = new List<(double, double)>();
        foreach (var (seconds, watts) in efforts)
        {
            if (!double.IsFinite(seconds) || !double.IsFinite(watts)) throw new ArgumentException("Efforts must be finite.");
            if (seconds is >= 180 and <= 1200) work.Add((seconds, watts * seconds));
        }
        return Statistics.Fit(work) is { } fit ? new(fit.Slope, fit.Intercept, fit.R2, fit.Count) : null;
    }

    internal static double Positive(double value, string name) =>
        double.IsFinite(value) && value > 0 ? value : throw new ArgumentOutOfRangeException(name, value, "Must be positive and finite.");

    private static double NonNegative(double value, string name) =>
        double.IsFinite(value) && value >= 0 ? value : throw new ArgumentOutOfRangeException(name, value, "Must be nonnegative and finite.");

    private static int Samples(double seconds, double sampleSeconds) =>
        Math.Max(1, (int)Math.Round(seconds / sampleSeconds, MidpointRounding.AwayFromZero));
}
