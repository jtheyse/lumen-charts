namespace Lumen.Charts;

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
public sealed record ReadoutEntry(int Series, int Point, double? Position, string Text, string Color);

public static partial class ChartSvg
{
    /// <summary>
    /// What a shared readout reads across <paramref name="spec"/> — the X values its series have points at, left to right, each with
    /// where it stands in the drawing, how it reads and each series' value there in legend order — so a host can draw a guide and rings
    /// over the SVG <see cref="Render"/> draws for the same spec, and say the same words. The Blazor component reads it when
    /// <see cref="ChartSpec.SharedReadout"/> is set. Series drawn as lines, areas, bands, scatter points, bubbles, columns, ranges and
    /// candles are read at each X of their own points, and blocks at the X they cover; a series is read at an X where it has a point
    /// within half the closest spacing of the X values, and a missing value there reads <c>missing</c>. A chart without a continuous X
    /// axis, or without data, reads <see cref="ChartReadout.Empty"/>. Checks the spec as <see cref="Render"/> does.
    /// </summary>
    public static ChartReadout Readout(ChartSpec spec)
    {
        ChartValidation.Validate(spec);
        if (spec.Kind is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range
            or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Blocks) || !HasData(spec)) return ChartReadout.Empty;
        var style = ResolveStyle(spec);
        // Laid out as Render lays it out: a description or a source on two lines moves the body, and a sparkline has neither.
        var bare = spec.Sparkline;
        var head = bare ? 0 : 14 * (Wrap(spec.Description, spec.Width - 48d).Length - 1);
        var foot = bare ? 0 : 14 * Math.Max(0, Wrap(spec.Source, spec.Width - 48).Length - 1);
        var frame = Framed(spec, bare ? Padding(spec, style.Finish == ChartFinish.Refined) : 0, head, foot);
        var xs = frame.Xs;
        bool Shown(ChartPoint p) => p.X >= xs.Min && p.X <= xs.Max;
        bool Valued(ChartKind mark, ChartPoint p) => mark == ChartKind.Range ? p.Low.HasValue && p.High.HasValue : p.Y.HasValue;
        var marks = spec.Series.Select(series => Mark(spec, series)).ToArray();
        // A density scatter draws cells rather than points, so it has none to read.
        var read = Enumerable.Range(0, spec.Series.Count).Where(i => marks[i] is not ChartKind.Blocks && !(marks[i] == ChartKind.Scatter && spec.DensityCells is not null)).ToArray();
        var blocked = Enumerable.Range(0, spec.Series.Count).Where(i => marks[i] == ChartKind.Blocks).ToArray();
        // The X values read are those the series have values at; blocks alone are read where each starts.
        var values = read.SelectMany(i => spec.Series[i].Points.Where(p => Valued(marks[i], p) && Shown(p))).Select(p => p.X).Distinct().Order().ToArray();
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
            string Entry(int pi)
            {
                var p = series.Points[pi];
                if (!Valued(mark, p)) return $"{series.Name} missing";
                var value = mark switch
                {
                    ChartKind.Candlestick or ChartKind.Ohlc => $"open {scale.Format(p.Open ?? p.Y!.Value)}, high {scale.Format(p.High ?? p.Y!.Value)}, low {scale.Format(p.Low ?? p.Y!.Value)}, close {scale.Format(p.Close ?? p.Y!.Value)}",
                    ChartKind.Range => $"{scale.Format(p.Low!.Value)} to {scale.Format(p.High!.Value)}{(p.Y is { } typical ? $", average {scale.Format(typical)}" : "")}",
                    _ => scale.Format(p.Y!.Value) + p.ValueNote
                        + (series.Zones is { } zones ? $", {zones.Zones[zones.IndexOf(p.Y!.Value)].Name}" : "")
                        + changes[pi] switch { > 0 => ", better than the previous", < 0 => ", worse than the previous", 0 => ", level with the previous", _ => "" }
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
                    entries[c].Add(new(si, pi, Height(series.Points[pi]), Entry(pi), color));
                    if (series.Points[pi].X == values[c]) labels[c] ??= series.Points[pi].Label;
                }
                continue;
            }
            // The series' points the plot shows, a missing one included, by where they stand.
            var placed = Enumerable.Range(0, series.Points.Count).Where(i => Shown(series.Points[i])).Select(i => (At: frame.X(series.Points[i].X), Index: i)).OrderBy(p => p.At).ToArray();
            if (placed.Length == 0) continue;
            var ats = placed.Select(p => p.At).ToArray();
            for (var c = 0; c < values.Length; c++)
            {
                var found = Array.BinarySearch(ats, positions[c]);
                var near = found >= 0 ? found : ~found;
                // The nearer of the points either side of where the column would stand.
                if (found < 0 && (near == ats.Length || near > 0 && positions[c] - ats[near - 1] <= ats[near] - positions[c])) near--;
                if (Math.Abs(ats[near] - positions[c]) > half + 1e-9) continue;
                var pi = placed[near].Index;
                entries[c].Add(new(si, pi, Height(series.Points[pi]), Entry(pi), color));
                if (series.Points[pi].X == values[c]) labels[c] ??= series.Points[pi].Label;
            }
        }
        var columns = values.Select((x, c) => new ReadoutColumn(x, positions[c], labels[c] ?? xs.Format(x), entries[c])).Where(column => column.Entries.Count > 0).ToArray();
        return new(top, bottom, frame.Left, frame.Right, columns);
    }
}
