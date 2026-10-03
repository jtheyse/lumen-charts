namespace Lumen.Charts;

/// <summary>Quartiles, whiskers and outliers. <see cref="Statistics.Summarize"/> uses Tukey whiskers, with outliers
/// beyond 1.5 interquartile ranges from the box; a summary supplied on <see cref="ChartSeries.Summary"/> may use any rule.</summary>
/// <param name="Q1">The first quartile, the bottom of the box.</param>
/// <param name="Median">The median, the line across the box.</param>
/// <param name="Q3">The third quartile, the top of the box.</param>
/// <param name="LowerWhisker">Where the lower whisker ends.</param>
/// <param name="UpperWhisker">Where the upper whisker ends.</param>
/// <param name="Outliers">The values drawn beyond the whiskers.</param>
public sealed record BoxSummary(double Q1, double Median, double Q3, double LowerWhisker, double UpperWhisker, IReadOnlyList<double> Outliers)
{
    /// <summary>The height of the box: <see cref="Q3"/> minus <see cref="Q1"/>.</summary>
    public double InterquartileRange => Q3 - Q1;
}

/// <summary>One histogram bin: the observations from <paramref name="Start"/> up to <paramref name="End"/>, and how many there
/// are. A value on the edge between two bins counts in the upper one, and the last bin also counts its upper edge.</summary>
/// <param name="Start">The bin's lower edge.</param>
/// <param name="End">The bin's upper edge.</param>
/// <param name="Count">How many observations fall in it.</param>
public sealed record HistogramBin(double Start, double End, int Count);

/// <summary>A least-squares line and the share of the variance in Y it accounts for.</summary>
/// <param name="Slope">How much Y rises for each unit of X.</param>
/// <param name="Intercept">The fitted Y where X is zero.</param>
/// <param name="R2">The share of the variance in Y the line accounts for, from 0 to 1.</param>
/// <param name="Count">How many observations it was fitted to.</param>
public sealed record LinearFit(double Slope, double Intercept, double R2, int Count)
{
    /// <summary>The fitted Y at <paramref name="x"/>.</summary>
    public double Predict(double x) => Intercept + Slope * x;
}

/// <summary>A least-squares polynomial and the share of the variance in Y it accounts for.</summary>
/// <param name="Coefficients">From the constant term upward, in the caller's X: <c>y = c₀ + c₁x + c₂x² + …</c>, one more than the
/// degree.</param>
/// <param name="R2">The share of the variance in Y the curve accounts for, from 0 to 1.</param>
/// <param name="Count">How many observations it was fitted to.</param>
public sealed record PolynomialFit(IReadOnlyList<double> Coefficients, double R2, int Count)
{
    // The fit as it was solved, in X centred and scaled to run from −1 to 1. Far from zero, as Unix milliseconds are, the terms of
    // the coefficients in the caller's X cancel in all but their last digits, so a fit predicts from here while it still carries
    // the coefficients it was solved with.
    private readonly (IReadOnlyList<double> For, double Centre, double Scale, double[] Scaled)? solved;
    internal PolynomialFit(IReadOnlyList<double> coefficients, double r2, int count, double centre, double scale, double[] scaled)
        : this(coefficients, r2, count) => solved = (coefficients, centre, scale, scaled);
    /// <summary>The fitted Y at <paramref name="x"/>. A fit from <see cref="Statistics.Polynomial"/> is evaluated about the middle of
    /// its observations, where it was solved, so X in Unix milliseconds keeps its precision; one built from coefficients alone is
    /// evaluated from them.</summary>
    public double Predict(double x) => solved is { } s && ReferenceEquals(s.For, Coefficients) ? Horner(s.Scaled, (x - s.Centre) / s.Scale) : Horner(Coefficients, x);
    internal static double Horner(IReadOnlyList<double> coefficients, double x)
    {
        var y = 0d;
        for (var k = coefficients.Count - 1; k >= 0; k--) y = y * x + coefficients[k];
        return y;
    }
}

/// <summary>An exponential <c>y = A·e^(B·x)</c> fitted to the logarithms of the positive values, and the share of their variance
/// it accounts for.</summary>
/// <param name="A">The fitted Y where X is zero. Far from zero, as Unix milliseconds are, it can be smaller than a double holds and
/// read 0; <see cref="Predict"/> still holds there.</param>
/// <param name="B">The growth rate: Y is multiplied by e^B for each unit of X, so it rises where B is positive.</param>
/// <param name="R2">The share of the variance in the logarithm of Y the fit accounts for, from 0 to 1, as Excel reports an
/// exponential trend's.</param>
/// <param name="Count">How many positive observations it was fitted to.</param>
public sealed record ExponentialFit(double A, double B, double R2, int Count)
{
    // Where the fit was solved: the observations' mean X and the fitted logarithm there, so a fit far from zero predicts from it
    // while it still carries the A and B it was solved with.
    private readonly (double A, double B, double X, double Log)? solved;
    internal ExponentialFit(double a, double b, double r2, int count, double x, double log) : this(a, b, r2, count) => solved = (a, b, x, log);
    /// <summary>The fitted Y at <paramref name="x"/>. A fit from <see cref="Statistics.Exponential"/> is evaluated from the middle of
    /// its observations, so it holds where <see cref="A"/> reads 0; one built from A and B alone is evaluated from them.</summary>
    public double Predict(double x) => solved is { } s && s.A.Equals(A) && s.B.Equals(B) ? Math.Exp(s.Log + B * (x - s.X)) : A * Math.Exp(B * x);
}

/// <summary>The mean and sample standard deviation of the values present in one trailing window, and how many there were.</summary>
/// <param name="Mean">The mean of the values present.</param>
/// <param name="Deviation">Their sample standard deviation, dividing by n − 1; zero for a single value.</param>
/// <param name="Count">How many values were present.</param>
public sealed record RollingWindow(double Mean, double Deviation, int Count);

/// <summary>The numbers behind the statistical charts and trend lines, for a host that wants them without the drawing.</summary>
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
    /// Fits <c>y = c₀ + c₁x + … + cₙxⁿ</c> of <paramref name="degree"/> 1 to 4 by least squares. Returns null when fewer than
    /// degree + 1 distinct X values leave no one curve to draw. X is centred and scaled to run from −1 to 1, and Y centred, before the
    /// normal equations are solved by elimination with partial pivoting, so a time axis in Unix milliseconds keeps its precision;
    /// the coefficients are then given in the caller's X. Observations that share one Y are explained perfectly by a flat curve, so
    /// their R squared is 1 rather than undefined.
    /// </summary>
    public static PolynomialFit? Polynomial(IEnumerable<(double X, double Y)> points, int degree)
    {
        if (degree is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(degree), "A polynomial's degree is from 1 to 4.");
        var data = points.ToArray();
        if (data.Select(p => p.X).Distinct().Take(degree + 1).Count() <= degree) return null;
        double low = data.Min(p => p.X), high = data.Max(p => p.X), centre = (low + high) / 2, scale = (high - low) / 2, meanY = data.Average(p => p.Y);
        var size = degree + 1;
        // The normal equations: sums of the powers of the scaled X, and of those powers times the centred Y.
        var sums = new double[2 * degree + 1];
        var moments = new double[size];
        foreach (var (x, y) in data)
        {
            double u = (x - centre) / scale, power = 1;
            for (var k = 0; k < sums.Length; k++)
            {
                sums[k] += power;
                if (k < size) moments[k] += power * (y - meanY);
                power *= u;
            }
        }
        var a = new double[size, size + 1];
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++) a[r, c] = sums[r + c];
            a[r, size] = moments[r];
        }
        for (var column = 0; column < size; column++)
        {
            var pivot = column;
            for (var r = column + 1; r < size; r++) if (Math.Abs(a[r, column]) > Math.Abs(a[pivot, column])) pivot = r;
            if (a[pivot, column] == 0) return null;
            for (var c = column; c <= size; c++) (a[column, c], a[pivot, c]) = (a[pivot, c], a[column, c]);
            for (var r = column + 1; r < size; r++)
            {
                var factor = a[r, column] / a[column, column];
                for (var c = column; c <= size; c++) a[r, c] -= factor * a[column, c];
            }
        }
        var scaled = new double[size];
        for (var r = size - 1; r >= 0; r--)
        {
            var sum = a[r, size];
            for (var c = r + 1; c < size; c++) sum -= a[r, c] * scaled[c];
            scaled[r] = sum / a[r, r];
        }
        scaled[0] += meanY;
        double residual = 0, total = 0;
        foreach (var (x, y) in data)
        {
            var miss = y - PolynomialFit.Horner(scaled, (x - centre) / scale);
            residual += miss * miss; total += (y - meanY) * (y - meanY);
        }
        // Each power of (x − centre) / scale expanded by the binomial theorem gives the coefficients in the caller's X.
        var coefficients = new double[size];
        for (var k = 0; k < size; k++)
        {
            var term = scaled[k] / Math.Pow(scale, k);
            for (var j = 0; j <= k; j++) coefficients[j] += term * Choose(k, j) * Math.Pow(-centre, k - j);
        }
        return new(Array.AsReadOnly(coefficients), total == 0 ? 1 : Math.Clamp(1 - residual / total, 0, 1), data.Length, centre, scale, scaled);
        static double Choose(int n, int k) => k == 0 ? 1 : Choose(n - 1, k - 1) * n / k;
    }

    /// <summary>
    /// Fits <c>y = A·e^(B·x)</c> by least squares on the logarithm of Y, as <see cref="Fit"/> fits a line, leaving out every
    /// observation whose Y is zero or negative, which has no logarithm. Returns null when fewer than two positive observations
    /// remain or they all share one X. R squared is measured on the logarithms, as Excel reports it for an exponential trend, not on
    /// Y itself.
    /// </summary>
    public static ExponentialFit? Exponential(IEnumerable<(double X, double Y)> points)
    {
        var logs = points.Where(p => p.Y > 0).Select(p => (p.X, Math.Log(p.Y))).ToArray();
        if (Fit(logs) is not { } line) return null;
        // A least-squares line passes through its means, so the fitted logarithm at the mean X is the mean logarithm.
        return new(Math.Exp(line.Intercept), line.Slope, line.R2, line.Count, logs.Average(p => p.X), logs.Average(p => p.Item2));
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

    /// <summary>The most bins a histogram takes.</summary>
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

    /// <summary>
    /// The box a box chart draws: quartiles by <see cref="Quantile"/>, whiskers at the most extreme observations within 1.5
    /// interquartile ranges of the box, which is Tukey's rule, and every observation beyond them as an outlier. Throws
    /// <see cref="ArgumentException"/> when there are no values.
    /// </summary>
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
