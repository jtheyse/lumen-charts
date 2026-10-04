namespace Lumen.Charts;

/// <summary>
/// Where a chart drawn on a continuous X axis stands in the drawing <see cref="ChartSvg.Render"/> draws for it, as <see cref="ChartSvg.Plot"/>
/// works it out, in the drawing's own units, the units of its <c>viewBox</c>: so a host can draw over the plots or turn a position back
/// into an X, as the Blazor component's drag to zoom does.
/// </summary>
/// <param name="Left">Where the X axis's lowest value stands across the drawing, the plots' left edge, or in from it by half a range bar's
/// slot on a chart that draws range bars.</param>
/// <param name="Right">Where its highest value stands.</param>
/// <param name="Top">The top of the first plot.</param>
/// <param name="Bottom">The bottom of the last plot.</param>
/// <param name="X">The X axis as drawn: <see cref="Axis.Map"/> with <paramref name="Left"/> and <paramref name="Right"/> places an X
/// as the marks are placed, and <see cref="Axis.Invert"/> reads one back.</param>
public sealed record ChartPlot(double Left, double Right, double Top, double Bottom, Axis X);

/// <summary>
/// What a shared readout reads across a chart, as <see cref="ChartSvg.Readout"/> works it out: every X the chart's series have a point
/// at, where each stands in the drawing, and what each series reads there. Positions are in the drawing's own units, the units of its
/// <c>viewBox</c>, so a host can draw a guide and rings over the SVG <see cref="ChartSvg.Render"/> draws for the same spec.
/// </summary>
/// <param name="Top">The top of the first plot, where a guide through every pane starts.</param>
/// <param name="Bottom">The bottom of the last plot, where the guide ends.</param>
/// <param name="Left">The plots' left edge.</param>
/// <param name="Right">The plots' right edge.</param>
/// <param name="Columns">Each X read, left to right.</param>
public sealed record ChartReadout(double Top, double Bottom, double Left, double Right, IReadOnlyList<ReadoutColumn> Columns)
{
    /// <summary>A chart with nothing to read: one without a continuous X axis, or without data.</summary>
    public static ChartReadout Empty { get; } = new(0, 0, 0, 0, []);
}

/// <summary>One X of a <see cref="ChartReadout"/>: where it stands, how it reads, and what each series reads there.</summary>
/// <param name="X">The X value, in the axis's units: Unix milliseconds on a time axis.</param>
/// <param name="Position">Where it stands across the drawing.</param>
/// <param name="Label">How it reads: the label of a point there, or the X in the axis's format, such as <c>3 Jun 2026</c>.</param>
/// <param name="Entries">Each series read there, in legend order.</param>
public sealed record ReadoutColumn(double X, double Position, string Label, IReadOnlyList<ReadoutEntry> Entries)
{
    /// <summary>The column in words on one line: its label, then each entry, parted by <c> · </c>, as
    /// <c>3 Jun 2026 · Fitness 52.3 · Fatigue 61 · Form −8.7</c>.</summary>
    public string Text => string.Join(" · ", Entries.Select(entry => entry.Text).Prepend(Label));
}

/// <summary>What one series reads at one X of a <see cref="ChartReadout"/>.</summary>
/// <param name="Series">The series' index in <see cref="ChartSpec.Series"/>.</param>
/// <param name="Point">The point's index in its series' <see cref="ChartSeries.Points"/>.</param>
/// <param name="Position">Where the point stands down the drawing, a ring's centre; null for a missing value, which has no place.</param>
/// <param name="Text">The series' name and its value in its axis's format, with the value's note, its zone and how it changed where it
/// has them, as <c>Fitness 52.3</c> or <c>Form −8.7, worse than the previous</c>; <c>Form missing</c> for a missing value.</param>
/// <param name="Color">The series' colour, a <c>#RRGGBB</c> colour for its ring.</param>
public sealed record ReadoutEntry(int Series, int Point, double? Position, string Text, string Color)
{
    /// <summary>How many of the series' points the entry stands for: one, or the number an average of <see cref="SamplingMethod.Average"/>
    /// takes in, which its column's label says once for the column.</summary>
    public int Count { get; init; } = 1;
}

public static partial class ChartSvg
{
    /// <summary>
    /// What a shared readout reads across <paramref name="spec"/> — the X values its series have points at, left to right, each with
    /// where it stands in the drawing, how it reads and each series' value there in legend order — so a host can draw a guide and rings
    /// over the SVG <see cref="Render"/> draws for the same spec, and say the same words. The Blazor component reads it when
    /// <see cref="ChartSpec.SharedReadout"/> is set. Series drawn as lines, areas, bands, scatter points, bubbles, columns, ranges and
    /// candles are read at each X of their own points, and blocks at the X they cover; a series is read at an X where it has a point
    /// within half the closest spacing of the X values, and a missing value there reads <c>missing</c>. A line or an area is read at the
    /// points it draws: thinned to <see cref="ChartSpec.MaxRenderedPoints"/> over the X range shown, as <see cref="ChartSpec.Sampling"/>
    /// thins it, so a long ride reads one X for each mark drawn rather than one for each second. Where averages are read, the column's
    /// label says so once, after the X: <c>1:02:30 · average of 12 s</c> on a duration or time axis, which gives the width of a slice, or
    /// <c>average of 12 points</c> on another, and each entry reads its average alone, <c>Heart rate 152</c>; an entry that stands for
    /// another number of points, at a gap's edge, says its own, <c>, average of 3 points</c>. Series whose points the app averaged, by
    /// <see cref="ChartSeries.AverageOf"/>, say so the same way: once in the label, <c>1:02:30 · average of 12 s</c>, where every entry of the
    /// column says the same and none is Lumen's own average, and otherwise after each such entry's value, <c>Power 212, average of 12 s</c>.
    /// A chart without a continuous X axis, or without data, reads
    /// <see cref="ChartReadout.Empty"/>. Checks the spec as <see cref="Render"/> does.
    /// </summary>
    public static ChartReadout Readout(ChartSpec spec)
    {
        ChartValidation.Validate(spec);
        if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range
            or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Blocks) || !HasData(spec)) return ChartReadout.Empty;
        var style = ResolveStyle(spec);
        // Laid out as Render lays it out: a description or a source on two lines moves the body, and a sparkline has neither.
        var bare = spec.Sparkline;
        var head = Headroom(spec);
        var foot = bare ? 0 : 14 * Math.Max(0, Wrap(spec.Source, spec.Width - 48).Length - 1);
        var frame = Framed(spec, bare ? Padding(spec, style.Finish == ChartFinish.Refined) : 0, head, foot);
        var xs = frame.Xs;
        bool Shown(ChartPoint p) => p.X >= xs.Min && p.X <= xs.Max;
        bool Valued(ChartKind mark, ChartPoint p) => mark == ChartKind.Range ? p.Low.HasValue && p.High.HasValue : p.Y.HasValue;
        var marks = spec.Series.Select(series => Mark(spec, series)).ToArray();
        // What each series offers the readout, by index: a line's or an area's marks as it draws them, with its missing values; every other
        // series' own points. Each is a point, its index in its series and how many points it stands for.
        var offered = spec.Series.Select((series, si) => marks[si] is ChartKind.Line or ChartKind.Area
            ? Traces(spec, series, marks[si], xs, series.HighlightLast ? Enumerable.Range(0, series.Points.Count).LastOrDefault(i => series.Points[i].Y.HasValue, -1) : -1)
                .SelectMany(run => run.Path.Concat(run.Apart)).Concat(Enumerable.Range(0, series.Points.Count).Where(i => !series.Points[i].Y.HasValue).Select(i => new Drawn(i, series.Points[i], 1)))
                .OrderBy(d => d.Index).ToArray()
            : series.Points.Select((p, i) => new Drawn(i, p, 1)).ToArray()).ToArray();
        // A density scatter draws cells rather than points, so it has none to read.
        var read = Enumerable.Range(0, spec.Series.Count).Where(i => marks[i] is not ChartKind.Blocks && !(marks[i] == ChartKind.Scatter && spec.DensityCells is not null)).ToArray();
        var blocked = Enumerable.Range(0, spec.Series.Count).Where(i => marks[i] == ChartKind.Blocks).ToArray();
        // The X values read are those the series have values at; blocks alone are read where each starts.
        var values = read.SelectMany(i => offered[i].Select(d => d.Point).Where(p => Valued(marks[i], p) && Shown(p))).Select(p => p.X).Distinct().Order().ToArray();
        if (values.Length == 0) values = blocked.SelectMany(i => spec.Series[i].Points.Where(Shown)).Select(p => p.X).Distinct().Order().ToArray();
        var (top, bottom) = (frame.Plots[0].Top, frame.Plots[^1].Bottom);
        if (values.Length == 0) return new(top, bottom, frame.Left, frame.Right, []);
        var positions = values.Select(frame.X).ToArray();
        // A point is read at the X nearest it within half the closest spacing of two X values on screen.
        var half = double.PositiveInfinity;
        for (var i = 1; i < positions.Length; i++)
            if (positions[i] - positions[i - 1] is > 0 and var gap) half = Math.Min(half, gap / 2);
        var entries = values.Select(_ => new List<ReadoutEntry>()).ToArray();
        var labels = new string?[values.Length];
        foreach (var si in Enumerable.Range(0, spec.Series.Count))
        {
            var series = spec.Series[si];
            var mark = marks[si];
            if (!read.Contains(si) && !blocked.Contains(si)) continue;
            var (_, paneTop, paneBottom, ys, ys2, _) = frame.Plots[series.Pane];
            var scale = series.Secondary ? ys2 : ys;
            double At(double y) => scale.Map(y, paneBottom, paneTop);
            var color = SeriesColor(series, si, style);
            var changes = Changes(series);
            string Entry(Drawn d)
            {
                var (pi, p) = (d.Index, d.Point);
                if (!Valued(mark, p)) return $"{series.Name} missing";
                var value = mark switch
                {
                    ChartKind.Candlestick or ChartKind.Ohlc => $"open {scale.Format(p.Open ?? p.Y!.Value)}, high {scale.Format(p.High ?? p.Y!.Value)}, low {scale.Format(p.Low ?? p.Y!.Value)}, close {scale.Format(p.Close ?? p.Y!.Value)}",
                    ChartKind.Range => $"{scale.Format(p.Low!.Value)} to {scale.Format(p.High!.Value)}{(p.Y is { } typical ? $", average {scale.Format(typical)}" : "")}",
                    _ => scale.Format(p.Y!.Value) + p.ValueNote
                        + (series.Zones is { } zones ? $", {zones.Zones[zones.IndexOf(p.Y!.Value)].Name}" : "")
                        + (d.Count > 1 ? null : changes[pi]) switch { > 0 => ", better than the previous", < 0 => ", worse than the previous", 0 => ", level with the previous", _ => "" }
                        + (series.ProjectedFrom is { } from && p.X >= from ? ", projected" : "")
                        + (mark == ChartKind.Band && p.Low.HasValue && p.High.HasValue ? $" ({scale.Format(p.Low.Value)} to {scale.Format(p.High.Value)})" : "")
                };
                return $"{series.Name} {value}";
            }
            double? Height(ChartPoint p) => !Valued(mark, p) ? null
                : mark == ChartKind.Range && p.Y is null ? At((p.Low!.Value + p.High!.Value) / 2)
                : mark == ChartKind.Blocks ? Math.Min(At(p.Y!.Value), paneBottom) : At(p.Y!.Value);
            if (mark == ChartKind.Blocks)
            {
                // A block is read at every X it covers, from its start up to its end.
                for (var c = 0; c < values.Length; c++)
                {
                    var pi = Enumerable.Range(0, series.Points.Count).FirstOrDefault(i => series.Points[i].X <= values[c] && values[c] < series.Points[i].XEnd!.Value, -1);
                    if (pi < 0) continue;
                    entries[c].Add(new(si, pi, Height(series.Points[pi]), Entry(new(pi, series.Points[pi], 1)), color));
                    if (series.Points[pi].X == values[c]) labels[c] ??= series.Points[pi].Label;
                }
                continue;
            }
            // The series' points the plot shows, a missing one included, by where they stand.
            var placed = offered[si].Where(d => Shown(d.Point)).Select(d => (At: frame.X(d.Point.X), Drawn: d)).OrderBy(p => p.At).ToArray();
            if (placed.Length == 0) continue;
            var ats = placed.Select(p => p.At).ToArray();
            for (var c = 0; c < values.Length; c++)
            {
                var found = Array.BinarySearch(ats, positions[c]);
                var near = found >= 0 ? found : ~found;
                // The nearer of the points either side of where the column would stand.
                if (found < 0 && (near == ats.Length || near > 0 && positions[c] - ats[near - 1] <= ats[near] - positions[c])) near--;
                if (Math.Abs(ats[near] - positions[c]) > half + 1e-9) continue;
                var d = placed[near].Drawn;
                entries[c].Add(new(si, d.Index, Height(d.Point), Entry(d), color) { Count = d.Count });
                if (d.Point.X == values[c]) labels[c] ??= d.Point.Label;
            }
        }
        // A column of averages says what they average once, by the count most of its averages share; an entry that stands for another
        // count says its own. The width of a slice is said only for a column of whole slices, those of the count most averages across the
        // chart hold; a slice a gap cuts short says its points.
        var typical = entries.SelectMany(column => column).Where(e => e.Count > 1).GroupBy(e => e.Count).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).FirstOrDefault()?.Key ?? 0;
        string Label(int c)
        {
            var label = labels[c] ?? xs.Format(values[c]);
            var counts = entries[c].Where(e => e.Count > 1).Select(e => e.Count).ToArray();
            // Averages the app made say what they average: once in the label where every entry in the column says the same and Lumen
            // averaged none of them, and otherwise after each such entry's value. A missing value says nothing, and an entry Lumen averaged
            // keeps Lumen's words.
            var given = entries[c].Select(e => e.Count > 1 ? null : spec.Series[e.Series].AverageOf).ToArray();
            var once = counts.Length == 0 && given.Length > 0 && given.All(of => of is not null && of == given[0]) ? given[0] : null;
            if (once is null)
                for (var k = 0; k < entries[c].Count; k++)
                    if (given[k] is { } of && entries[c][k].Position is not null) entries[c][k] = entries[c][k] with { Text = $"{entries[c][k].Text}, average of {of}" };
            if (counts.Length == 0) return once is null ? label : $"{label} · average of {once}";
            var shared = counts.GroupBy(n => n).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
            for (var k = 0; k < entries[c].Count; k++)
                if (entries[c][k] is { Count: > 1 } e && e.Count != shared) entries[c][k] = e with { Text = e.Text + Averaged(e.Count) };
            return $"{label} · average of {(shared == typical ? Slice(shared) : $"{Count(shared)} points")}";
        }
        // A slice's width on a duration or time axis, else the points it holds.
        string Slice(int count)
        {
            var seconds = spec.XAxis == AxisKind.Time ? (xs.Max - xs.Min) / spec.MaxRenderedPoints / 1000
                : spec.XAxis == AxisKind.Linear && spec.XFormat == ValueFormat.Duration ? (xs.Max - xs.Min) / spec.MaxRenderedPoints : double.NaN;
            string Whole(double value, string unit) => $"{Math.Round(value, MidpointRounding.AwayFromZero).ToString(System.Globalization.CultureInfo.InvariantCulture)} {unit}";
            return double.IsNaN(seconds) ? $"{Count(count)} points" : seconds < 90 ? Whole(seconds, "s") : seconds < 5400 ? Whole(seconds / 60, "min")
                : seconds < 129_600 ? Whole(seconds / 3600, "h") : Whole(seconds / 86_400, "days");
        }
        var columns = values.Select((x, c) => new ReadoutColumn(x, positions[c], Label(c), entries[c])).Where(column => column.Entries.Count > 0).ToArray();
        return new(top, bottom, frame.Left, frame.Right, columns);
    }

    /// <summary>
    /// Where the plots of <paramref name="spec"/> stand in the drawing <see cref="Render"/> draws for it, and the X axis they share, for a
    /// chart drawn on a continuous X axis — line, area, scatter, bubble, band, range, candlestick, OHLC, blocks and timeline charts, panes
    /// included — or null for the other kinds, a sparkline and a chart without data. Checks the spec as <see cref="Render"/> does.
    /// </summary>
    public static ChartPlot? Plot(ChartSpec spec)
    {
        ChartValidation.Validate(spec);
        if (spec.Sparkline || !HasData(spec)) return null;
        var head = Headroom(spec);
        var foot = 14 * Math.Max(0, Wrap(spec.Source, spec.Width - 48).Length - 1);
        if (spec.Kind == ChartKind.Timeline)
        {
            var (left, right, top, bottom, xs) = Lanes(spec, head, foot);
            return new(left, right, top, bottom, xs);
        }
        if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range
            or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Blocks)) return null;
        var frame = Framed(spec, 0, head, foot);
        return new(frame.Left + frame.Inset, frame.Right - frame.Inset, frame.Plots[0].Top, frame.Plots[^1].Bottom, frame.Xs);
    }
}
