namespace Lumen.Charts;

/// <summary>Quartiles, whiskers and outliers. <see cref="Statistics.Summarize"/> uses Tukey whiskers, with outliers
/// beyond 1.5 interquartile ranges from the box; a summary supplied on <see cref="ChartSeries.Summary"/> may use any rule.</summary>
public sealed record BoxSummary(double Q1, double Median, double Q3, double LowerWhisker, double UpperWhisker, IReadOnlyList<double> Outliers)
{
    public double InterquartileRange => Q3 - Q1;
}

public sealed record HistogramBin(double Start, double End, int Count);

/// <summary>A least-squares line and the share of the variance in Y it accounts for.</summary>
public sealed record LinearFit(double Slope, double Intercept, double R2, int Count)
{
    public double Predict(double x) => Intercept + Slope * x;
}

/// <summary>The mean and sample standard deviation of the values present in one trailing window, and how many there were.</summary>
public sealed record RollingWindow(double Mean, double Deviation, int Count);

public static class Statistics
{
    /// <summary>
    /// Fits <c>y = a + bx</c> by least squares. Returns null when there are fewer than two observations
    /// or they all share one X, which leaves no line to draw. X is centred first, so a time axis in Unix
    /// milliseconds keeps its precision. Observations that share one Y are explained perfectly by a flat
    /// line, so their R squared is 1 rather than undefined.
    /// </summary>
    public static LinearFit? Fit(IEnumerable<(double X, double Y)> points)
    {
        var data = points.ToArray();
        if (data.Length < 2) return null;
        double meanX = data.Average(p => p.X), meanY = data.Average(p => p.Y);
        double sxx = 0, sxy = 0, syy = 0;
        foreach (var (x, y) in data)
        {
            double dx = x - meanX, dy = y - meanY;
            sxx += dx * dx; sxy += dx * dy; syy += dy * dy;
        }
        if (sxx == 0) return null;
        var slope = sxy / sxx;
        return new(slope, meanY - slope * meanX, syy == 0 ? 1 : Math.Clamp(sxy * sxy / (sxx * syy), 0, 1), data.Length);
    }

    /// <summary>
    /// A trailing window over a series with gaps, for a baseline band or a moving average. Entry i summarises the
    /// <paramref name="window"/> entries ending at i, skipping missing ones, and is null wherever fewer than
    /// <paramref name="minimum"/> values are present, or fewer than the whole window when no minimum is given. The
    /// deviation is the sample standard deviation, dividing by n − 1, and zero for a single value. Totals are kept
    /// relative to the first value, so a large level with a small spread keeps its precision.
    /// </summary>
    public static IReadOnlyList<RollingWindow?> Rolling(IReadOnlyList<double?> values, int window, int? minimum = null)
    {
        if (window < 1) throw new ArgumentOutOfRangeException(nameof(window));
        var least = minimum ?? window;
        if (least < 1 || least > window) throw new ArgumentOutOfRangeException(nameof(minimum));
        var origin = values.FirstOrDefault(v => v.HasValue) ?? 0;
        double sum = 0, squares = 0;
        var count = 0;
        var result = new RollingWindow?[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is { } added)
            {
                if (!double.IsFinite(added)) throw new ArgumentException("Values must be finite; leave a missing one null.");
                sum += added - origin; squares += (added - origin) * (added - origin); count++;
            }
            if (i >= window && values[i - window] is { } removed)
            {
                sum -= removed - origin; squares -= (removed - origin) * (removed - origin); count--;
            }
            if (count < least) continue;
            var mean = sum / count;
            result[i] = new(origin + mean, count > 1 ? Math.Sqrt(Math.Max(0, (squares - sum * mean) / (count - 1))) : 0, count);
        }
        return result;
    }

    public const int MaxBins = 100;

    /// <summary>Linear interpolation between order statistics, matching NumPy's default and Excel's PERCENTILE.INC.</summary>
    public static double Quantile(IReadOnlyList<double> sorted, double probability)
    {
        if (sorted.Count == 0) throw new ArgumentException("Quantiles require at least one observation.");
        if (probability is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(probability));
        var position = (sorted.Count - 1) * probability;
        var lower = (int)Math.Floor(position);
        var fraction = position - lower;
        return lower + 1 < sorted.Count ? sorted[lower] + fraction * (sorted[lower + 1] - sorted[lower]) : sorted[lower];
    }

    public static BoxSummary Summarize(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0) throw new ArgumentException("A box summary requires at least one observation.");
        var q1 = Quantile(sorted, .25);
        var q3 = Quantile(sorted, .75);
        var fence = 1.5 * (q3 - q1);
        var inside = sorted.Where(v => v >= q1 - fence && v <= q3 + fence).ToArray();
        return new(q1, Quantile(sorted, .5), q3,
            inside.Length == 0 ? q1 : inside[0], inside.Length == 0 ? q3 : inside[^1],
            sorted.Where(v => v < q1 - fence || v > q3 + fence).ToArray());
    }

    /// <summary>
    /// A kernel density estimate over the observed range, for a violin's outline. The kernel is Gaussian
    /// and the bandwidth is Silverman's rule of thumb, taking the smaller of the standard deviation and
    /// the interquartile range so one long tail cannot smooth the shape away. The grid runs from the
    /// smallest observation to the largest and no further, so the drawing claims no values the data
    /// never had. Returns nothing for fewer than two observations or for a set with no spread.
    /// </summary>
    public static IReadOnlyList<(double Value, double Density)> Density(IReadOnlyList<double> values, int samples = 64)
    {
        if (values.Count < 2 || samples < 2) return [];
        var sorted = values.OrderBy(v => v).ToArray();
        double low = sorted[0], high = sorted[^1];
        if (high <= low) return [];
        var mean = sorted.Average();
        var deviation = Math.Sqrt(sorted.Sum(v => (v - mean) * (v - mean)) / (sorted.Length - 1));
        var spread = Quantile(sorted, .75) - Quantile(sorted, .25);
        var width = .9 * (spread > 0 ? Math.Min(deviation, spread / 1.349) : deviation) * Math.Pow(sorted.Length, -.2);
        if (width <= 0) return [];
        var scale = 1 / (sorted.Length * width * Math.Sqrt(2 * Math.PI));
        var estimate = new (double, double)[samples];
        for (var i = 0; i < samples; i++)
        {
            var at = low + (high - low) * i / (samples - 1);
            var sum = 0d;
            foreach (var v in sorted) { var z = (at - v) / width; sum += Math.Exp(-.5 * z * z); }
            estimate[i] = (at, sum * scale);
        }
        return estimate;
    }

    /// <summary>Equal-width bins. Freedman–Diaconis chooses the count when <paramref name="count"/> is null, falling back to Sturges.</summary>
    public static IReadOnlyList<HistogramBin> Bins(IReadOnlyList<double> values, int? count = null) => SharedBins([values], count)[0];

    /// <summary>
    /// One set of equal-width bins for several sets of observations, so a bin covers the same range in every
    /// set. The edges are chosen from the pooled observations exactly as <see cref="Bins"/> chooses them for one
    /// set, and each set is then counted against them; a set with no observations counts zero in every bin.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<HistogramBin>> SharedBins(IReadOnlyList<IReadOnlyList<double>> sets, int? count = null)
    {
        var sorted = sets.SelectMany(set => set).OrderBy(v => v).ToArray();
        if (sorted.Length == 0) throw new ArgumentException("A histogram requires at least one observation.");
        if (count is < 1 or > MaxBins) throw new ArgumentException($"Bin counts must be between 1 and {MaxBins}.");
        double low = sorted[0], high = sorted[^1];
        var identical = low == high;
        if (identical) { low -= .5; high += .5; }
        var bins = count ?? (identical ? 1 : Auto(sorted, high - low));
        var width = (high - low) / bins;
        return sets.Select(set =>
        {
            var counts = new int[bins];
            // The final bin includes its upper edge so the maximum observation is counted.
            foreach (var value in set) counts[Math.Clamp((int)((value - low) / width), 0, bins - 1)]++;
            return (IReadOnlyList<HistogramBin>)Enumerable.Range(0, bins).Select(i => new HistogramBin(low + i * width, low + (i + 1) * width, counts[i])).ToArray();
        }).ToArray();
    }

    private static int Auto(double[] sorted, double range)
    {
        var iqr = Quantile(sorted, .75) - Quantile(sorted, .25);
        var width = 2 * iqr / Math.Cbrt(sorted.Length);
        var bins = width > 0 ? (int)Math.Ceiling(range / width) : (int)Math.Ceiling(Math.Log2(sorted.Length)) + 1;
        return Math.Clamp(bins, 1, MaxBins);
    }
}
