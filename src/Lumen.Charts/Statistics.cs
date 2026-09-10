namespace Lumen.Charts;

/// <summary>Quartiles with Tukey whiskers. Outliers are observations beyond 1.5 interquartile ranges from the box.</summary>
public sealed record BoxSummary(double Q1, double Median, double Q3, double LowerWhisker, double UpperWhisker, IReadOnlyList<double> Outliers)
{
    public double InterquartileRange => Q3 - Q1;
}

public sealed record HistogramBin(double Start, double End, int Count);

public static class Statistics
{
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

    /// <summary>Equal-width bins. Freedman–Diaconis chooses the count when <paramref name="count"/> is null, falling back to Sturges.</summary>
    public static IReadOnlyList<HistogramBin> Bins(IReadOnlyList<double> values, int? count = null)
    {
        if (values.Count == 0) throw new ArgumentException("A histogram requires at least one observation.");
        if (count is < 1 or > MaxBins) throw new ArgumentException($"Bin counts must be between 1 and {MaxBins}.");
        var sorted = values.OrderBy(v => v).ToArray();
        double low = sorted[0], high = sorted[^1];
        var identical = low == high;
        if (identical) { low -= .5; high += .5; }
        var bins = count ?? (identical ? 1 : Auto(sorted, high - low));
        var width = (high - low) / bins;
        var counts = new int[bins];
        foreach (var value in sorted)
        {
            // The final bin includes its upper edge so the maximum observation is counted.
            var index = Math.Clamp((int)((value - low) / width), 0, bins - 1);
            counts[index]++;
        }
        return Enumerable.Range(0, bins).Select(i => new HistogramBin(low + i * width, low + (i + 1) * width, counts[i])).ToArray();
    }

    private static int Auto(double[] sorted, double range)
    {
        var iqr = Quantile(sorted, .75) - Quantile(sorted, .25);
        var width = 2 * iqr / Math.Cbrt(sorted.Length);
        var bins = width > 0 ? (int)Math.Ceiling(range / width) : (int)Math.Ceiling(Math.Log2(sorted.Length)) + 1;
        return Math.Clamp(bins, 1, MaxBins);
    }
}
