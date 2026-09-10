namespace Lumen.Charts;

public static class Sampling
{
    /// <summary>Min/max buckets preserve extrema, endpoints and their original point indices. Use on a contiguous segment.</summary>
    public static IReadOnlyList<int> MinMax(IReadOnlyList<ChartPoint> points, int budget)
    {
        if (budget < 4) throw new ArgumentOutOfRangeException(nameof(budget));
        if (points.Count <= budget) return Enumerable.Range(0, points.Count).ToArray();
        var result = new SortedSet<int> { 0, points.Count - 1 };
        var buckets = (budget - 2) / 2;
        for (var b = 0; b < buckets; b++)
        {
            var start = 1 + (int)((long)b * (points.Count - 2) / buckets);
            var end = 1 + (int)((long)(b + 1) * (points.Count - 2) / buckets);
            var lo = start; var hi = start;
            for (var i = start + 1; i < end; i++)
            {
                if (points[i].Y < points[lo].Y) lo = i;
                if (points[i].Y > points[hi].Y) hi = i;
            }
            result.Add(lo); result.Add(hi);
        }
        return result.ToArray();
    }
}
