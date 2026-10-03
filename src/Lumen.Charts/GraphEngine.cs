namespace Lumen.Charts;

/// <summary>Lays out and draws network graphs. The layout is deterministic: one graph always lays out the same way.</summary>
public static class GraphEngine
{
    private const double Radius = 23, Trim = 25;
    // A node's label hangs under it, its baseline 42 below the centre, so top to bottom an edge that runs below a node meets it
    // 50 below the centre, at the foot of the label, rather than through the words. A row then holds a node, its label and an
    // edge long enough to carry its own label and arrowhead clear of both.
    private const double Foot = 50, Row = 110;
    // Every candidate place for an edge's label, as a fraction of the drawn edge's length, from its middle outwards.
    private static readonly double[] Fractions = [.5, .4, .6, .3, .7, .25, .75];

    /// <summary>Node coordinates. Layered graphs order each level to reduce edge crossings.</summary>
    public static IReadOnlyList<NodePosition> Layout(GraphSpec graph)
    {
        Validate(graph);
        if (graph.Nodes.Count == 0) return [];
        if (graph.Layout == GraphLayout.Circular) return Circle(graph);
        return Arrange(graph).Nodes;
    }

    /// <summary>Edge polylines. Layered routes bend through one point per level a long edge spans; a self-loop keeps both endpoints on its node.</summary>
    public static IReadOnlyList<EdgeRoute> Routes(GraphSpec graph)
    {
        Validate(graph);
        if (graph.Nodes.Count == 0) return [];
        if (graph.Layout != GraphLayout.Circular) return Arrange(graph).Routes;
        var positions = Circle(graph).ToDictionary(p => p.Id, p => new GraphPoint(p.X, p.Y), StringComparer.Ordinal);
        return graph.Edges.Select((e, i) => new EdgeRoute(i, [positions[e.Source], positions[e.Target]])).ToArray();
    }

    /// <summary>Edge crossings in the produced drawing: layer-by-layer for layered graphs, interleaved chords for circular ones.</summary>
    public static int Crossings(GraphSpec graph)
    {
        Validate(graph);
        if (graph.Nodes.Count == 0) return 0;
        if (graph.Layout != GraphLayout.Circular) return Arrange(graph).Crossings;
        var index = graph.Nodes.Select((n, i) => (n.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        var chords = graph.Edges.Where(e => e.Source != e.Target).Select(e => (A: index[e.Source], B: index[e.Target])).ToArray();
        var total = 0;
        for (var i = 0; i < chords.Length; i++)
            for (var j = i + 1; j < chords.Length; j++)
            {
                var (a, b) = chords[i]; var (c, d) = chords[j];
                if (a == c || a == d || b == c || b == d) continue;
                if (Inside(c, a, b) != Inside(d, a, b)) total++;
            }
        return total;
        static bool Inside(int value, int from, int to) => from < to ? value > from && value < to : value > from || value < to;
    }

    /// <summary>Draws a graph as SVG. <paramref name="positions"/> moves nodes, by id, to centres of their own, such as where a
    /// reader dragged them; their edges follow, and the other nodes keep the layout's places. An edge whose straight run from a
    /// node would cross that node's label meets the node at the foot of the label instead, in every layout, and an edge's label
    /// stands at the first place along the edge, from its middle outwards, that keeps off every node, every node's label and
    /// the edge labels drawn before it.</summary>
    public static string Render(GraphSpec graph, IReadOnlyDictionary<string, GraphPoint>? positions = null)
    {
        var layout = Layout(graph).ToDictionary(p => p.Id, p => positions is not null && positions.TryGetValue(p.Id, out var moved) ? moved : new GraphPoint(p.X, p.Y), StringComparer.Ordinal);
        var routes = Routes(graph);
        var w = new SvgWriter { Style = graph.Style ?? ChartSvg.Preset(graph.Theme) };
        var down = Down(graph);
        var crossings = graph.Layout == GraphLayout.Circular ? "" : $" · {Crossings(graph)} edge crossings";
        ChartSvg.Begin(w, graph.Width, graph.Height, graph.Title,
            $"{graph.Nodes.Count} nodes · {graph.Edges.Count} directed connections · {graph.Layout} layout{(down ? " top to bottom" : "")}{crossings}", wrap: true);
        // Every node's label box, which edges keep out of and edge labels keep off, as do the labels placed before them.
        var labels = graph.Nodes.ToDictionary(n => n.Id, n => LabelBox(n, layout[n.Id]), StringComparer.Ordinal);
        var placed = new List<Box>();
        // An edge leaves and reaches a node trimmed clear of its circle. One whose straight run from the node towards its next point
        // would cross the node's label meets the node at the foot of the label instead, and so, top to bottom, does one that runs on
        // below the node, so that no edge runs through its own node's words.
        bool Under(string id, GraphPoint node, GraphPoint towards) => down && towards.Y > node.Y + Foot || Crosses(node, towards, labels[id]);
        GraphPoint End(GraphPoint node, GraphPoint towards, bool under) => under ? new(node.X, node.Y + Foot) : Shift(node, towards, Trim);
        for (var i = 0; i < graph.Edges.Count; i++)
        {
            var edge = graph.Edges[i];
            var a = layout[edge.Source];
            if (edge.Source == edge.Target)
            {
                // A loop stands on top of its node; top to bottom, where edges arrive from above, it stands at the node's right.
                var loop = down
                    ? $"M{N(a.X + 17)},{N(a.Y - 12)} C{N(a.X + 75)},{N(a.Y - 65)} {N(a.X + 75)},{N(a.Y + 65)} {N(a.X + 17)},{N(a.Y + 12)}"
                    : $"M{N(a.X - 12)},{N(a.Y - 17)} C{N(a.X - 65)},{N(a.Y - 75)} {N(a.X + 65)},{N(a.Y - 75)} {N(a.X + 12)},{N(a.Y - 17)}";
                w.Add($"<path d='{loop}' fill='none' stroke='{w.Style.Edge}'{w.Fixed}><title>{SvgWriter.E(edge.Label ?? "Self-loop")}</title></path>");
                continue;
            }
            // Dragged endpoints replace the layout's own; the bends between them stay where the layout put them.
            var points = routes[i].Points.ToArray();
            points[0] = a; points[^1] = layout[edge.Target];
            var straight = points.Length == 2;
            var leaves = Under(edge.Source, points[0], points[1]);
            var start = End(points[0], points[1], leaves);
            // A straight edge that leaves from under a label, or runs top to bottom, arrives aimed from where it leaves; one that
            // arrives under a label leaves aimed at it there. A straight edge meets only one of its nodes under its label: the node
            // whose label its run crosses stands higher than the other by more than the label's top, 30.6 pixels below it, so the
            // other's run climbs away from its own label.
            var from = straight && (down || leaves) ? start : points[^2];
            var arrives = Under(edge.Target, points[^1], from);
            var end = End(points[^1], from, arrives);
            if (arrives && straight) start = End(points[0], end, leaves);
            points[0] = start; points[^1] = end;
            w.Add($"<path d='{Path(points)}' fill='none' stroke='{w.Style.Edge}' stroke-width='1.5'{w.Fixed}/>");
            var direction = Unit(points[^2], end);
            w.Add($"<path d='M{N(end.X)},{N(end.Y)} L{N(end.X - direction.X * 9 - direction.Y * 4)},{N(end.Y - direction.Y * 9 + direction.X * 4)} L{N(end.X - direction.X * 9 + direction.Y * 4)},{N(end.Y - direction.Y * 9 - direction.X * 4)} Z' fill='{w.Style.Edge}'/>");
            if (edge.Label is not null)
            {
                var text = ChartSvg.Short(edge.Label, 20);
                var wide = ChartSvg.Wide(text) * 10 / 11;
                // A label 9 pixels above the edge, or as far below it; top to bottom, where an edge runs down, 6 pixels beside it.
                (double X, double Y, string Anchor)[] Places(GraphPoint at) => down
                    ? [(at.X + 6, at.Y + 3.5, "start"), (at.X - 6, at.Y + 3.5, "end")]
                    : [(at.X, at.Y - 9, "middle"), (at.X, at.Y + 16, "middle")];
                bool Free(Box box)
                {
                    var padded = box.Padded(4);
                    return padded.Left >= 0 && padded.Top >= 0 && padded.Right <= graph.Width && padded.Bottom <= graph.Height
                        && !layout.Values.Any(p => padded.Touches(p, Radius)) && !labels.Values.Any(padded.Overlaps) && !placed.Any(padded.Overlaps);
                }
                // The first free place along the edge, from its middle outwards, on the side tried first before the other, keeps off
                // every node and its label, the labels placed before it and the drawing's edge, 4 pixels to spare. Where none is
                // free the label stands where it always has: at the middle of the edge's middle stretch, above it, or top to bottom
                // beside it, on its right unless that would take it past the drawing's edge.
                var line = Flatten(points);
                var place = Fractions.SelectMany(f => Places(Along(line, f))).Select(p => (p, Box: TextBox(p.X, p.Y, p.Anchor, wide))).FirstOrDefault(c => Free(c.Box)).p;
                if (place.Anchor is null)
                {
                    var middle = points[points.Length / 2];
                    var previous = points[points.Length / 2 - 1];
                    double x = (middle.X + previous.X) / 2, y = (middle.Y + previous.Y) / 2;
                    var right = x + 6 + wide <= graph.Width - 4;
                    place = !down ? (x, y - 9, "middle") : right ? (x + 6, y + 3.5, "start") : (x - 6, y + 3.5, "end");
                }
                placed.Add(TextBox(place.X, place.Y, place.Anchor, wide));
                w.Text(place.X, place.Y, text, $"text-anchor='{place.Anchor}' class='lumen-muted' font-size='10'");
            }
        }
        for (var i = 0; i < graph.Nodes.Count; i++)
        {
            var n = graph.Nodes[i]; var p = layout[n.Id]; var color = n.Color ?? w.Style.SeriesColor(i);
            w.Add($"<g class='lumen-node' tabindex='0' role='button' data-node='{SvgWriter.E(n.Id)}' data-position='{N(p.X)},{N(p.Y)}' aria-label='{SvgWriter.E(n.Label)}'><title>{SvgWriter.E(n.Label)}</title><circle cx='{N(p.X)}' cy='{N(p.Y)}' r='{N(Radius)}' fill='{color}' fill-opacity='.15' stroke='{color}' stroke-width='2'{w.Fixed}/>");
            w.Text(p.X, p.Y + 5, (i + 1).ToString(), "text-anchor='middle' font-weight='600'");
            w.Text(p.X, p.Y + 42, ChartSvg.Short(n.Label, 22), "text-anchor='middle'"); w.Add("</g>");
        }
        w.Add("</svg>");
        return w.ToString();
    }

    /// <summary>
    /// The graph as it should be drawn in a box <paramref name="width"/> pixels wide, clamped to 320 to 4,096, so that it fills
    /// the box with its text at its own size: the graph itself when it already fits at its own width and direction, and otherwise
    /// a copy at that width. Neighbouring nodes need room for the widest node label as drawn, cut to 22 characters, and 16 pixels
    /// more, or for a node's diameter and 24 pixels more, whichever is the larger. A layered graph whose levels cannot stand that
    /// far apart side by side turns <see cref="GraphDirection.TopToBottom"/>, as one already set so stays, and grows as tall as
    /// its rows need, up to 2,160 pixels; when its fullest level, counting the bends of longer edges that pass through it, cannot
    /// stand that far apart across the width either, it takes the narrowest width that holds it instead, wider than the box,
    /// which a fitted LumenGraph scrolls. A
    /// circular graph keeps its circle's arithmetic, which stands the circle in from either side by half its widest label, or a
    /// node's radius if that is more, and 24 pixels, and grows taller, up to 2,160 pixels, until neighbouring nodes stand that far
    /// apart. Nodes at one height, which no height can part, need the same room for their two labels, 16 pixels apart, and their
    /// circles, 24 apart, and a width too narrow for that becomes the narrowest that has it. A graph is never drawn shorter than
    /// its own <see cref="GraphSpec.Height"/>.
    /// </summary>
    public static GraphSpec Fit(GraphSpec graph, int width)
    {
        Validate(graph);
        width = Math.Clamp(width, 320, 4096);
        if (graph.Nodes.Count == 0) return Sized(graph, width, graph.Height, graph.Direction);
        var room = Math.Max(graph.Nodes.Max(Label) + 16, 2 * Radius + 24);
        if (graph.Layout == GraphLayout.Circular)
        {
            var n = graph.Nodes.Count;
            var labels = graph.Nodes.Select(Label).ToArray();
            var margin = Margin(graph);
            // A node and its mirror image across the circle stand at one height whatever the height, so only the width parts them,
            // by twice the circle's half-width times this sine, and they need the room neighbours get: their labels 16 pixels
            // apart and their circles 24.
            for (var i = 1; i < n - i; i++)
            {
                var apart = 2 * Math.Abs(Math.Sin(2 * Math.PI * i / n));
                var need = Math.Max((labels[i] + labels[n - i]) / 2 + 16, 2 * Radius + 24);
                width = Math.Max(width, (int)Math.Min(4096, Math.Ceiling(2 * margin + 2 * need / apart)));
            }
            // Neighbours round the circle stand further apart as it grows taller, unless they stand at one height.
            var across = width / 2d - margin;
            var radius = graph.Height / 2d - 100;
            for (var i = 0; i < n && n > 1; i++)
            {
                double a = 2 * Math.PI * i / n - Math.PI / 2, b = 2 * Math.PI * ((i + 1) % n) / n - Math.PI / 2;
                var dx = across * Math.Abs(Math.Cos(b) - Math.Cos(a));
                var rise = Math.Abs(Math.Sin(b) - Math.Sin(a));
                if (rise > 1e-9 && dx < room) radius = Math.Max(radius, Math.Sqrt(room * room - dx * dx) / rise);
            }
            return Sized(graph, width, (int)Math.Clamp(Math.Ceiling(2 * (radius + 100)), graph.Height, 2160), graph.Direction);
        }
        var levels = Levels(graph);
        var depth = levels.Values.Max();
        if (graph.Direction == GraphDirection.LeftToRight && (depth == 0 || (width - 180d) / depth >= room))
            return Sized(graph, width, graph.Height, GraphDirection.LeftToRight);
        // Top to bottom, a level's nodes and the bends of the longer edges passing through it share the width.
        var slots = new int[depth + 1];
        foreach (var level in levels.Values) slots[level]++;
        foreach (var edge in graph.Edges)
            for (var level = levels[edge.Source] + 1; level < levels[edge.Target]; level++) slots[level]++;
        var narrowest = (int)Math.Min(4096, Math.Ceiling(48 + slots.Max() * room));
        return Sized(graph, Math.Max(width, narrowest), (int)Math.Clamp(Math.Ceiling(180 + depth * Row), graph.Height, 2160), GraphDirection.TopToBottom);
    }

    // The graph itself when nothing changes, so a graph that fits is drawn exactly as it was.
    private static GraphSpec Sized(GraphSpec graph, int width, int height, GraphDirection direction) =>
        width == graph.Width && height == graph.Height && direction == graph.Direction ? graph : graph with { Width = width, Height = height, Direction = direction };

    private static bool Down(GraphSpec graph) => graph.Layout == GraphLayout.Layered && graph.Direction == GraphDirection.TopToBottom;

    private static string N(double value) => SvgWriter.N(value);

    private static string Path(GraphPoint[] points)
    {
        if (points.Length == 2) return $"M{N(points[0].X)},{N(points[0].Y)} L{N(points[1].X)},{N(points[1].Y)}";
        // Quadratic segments through each bend, ending at the midpoint towards the next one, keep long edges smooth.
        var path = $"M{N(points[0].X)},{N(points[0].Y)}";
        for (var i = 1; i < points.Length - 1; i++)
            path += $" Q{N(points[i].X)},{N(points[i].Y)} {N((points[i].X + points[i + 1].X) / 2)},{N((points[i].Y + points[i + 1].Y) / 2)}";
        return path + $" L{N(points[^1].X)},{N(points[^1].Y)}";
    }

    private static GraphPoint Unit(GraphPoint from, GraphPoint to)
    {
        var dx = to.X - from.X; var dy = to.Y - from.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        return length == 0 ? new(0, 0) : new(dx / length, dy / length);
    }
    private static GraphPoint Shift(GraphPoint point, GraphPoint towards, double distance)
    {
        var unit = Unit(point, towards);
        return new(point.X + unit.X * distance, point.Y + unit.Y * distance);
    }

    // A node's label is drawn 12 pixels high, cut to 22 characters, and is as wide as the library's generous estimate.
    private static double Label(GraphNode node) => ChartSvg.Wide(ChartSvg.Short(node.Label, 22)) * 12 / 11;

    // A circle stands in from either side by half its widest label, or a node's radius if that is more, and 24 pixels, so that
    // the labels at its sides keep inside the drawing however long they are.
    private static double Margin(GraphSpec graph) => Math.Max(Radius, graph.Nodes.Max(Label) / 2) + 24;

    private static IReadOnlyList<NodePosition> Circle(GraphSpec graph)
    {
        var across = graph.Width / 2d - Margin(graph);
        return graph.Nodes.Select((n, i) => new NodePosition(n.Id,
            graph.Width / 2d + across * Math.Cos(2 * Math.PI * i / graph.Nodes.Count - Math.PI / 2),
            (graph.Height + 45) / 2d + (graph.Height / 2d - 100) * Math.Sin(2 * Math.PI * i / graph.Nodes.Count - Math.PI / 2))).ToArray();
    }

    private readonly record struct Box(double Left, double Top, double Right, double Bottom)
    {
        public Box Padded(double by) => new(Left - by, Top - by, Right + by, Bottom + by);
        public bool Overlaps(Box other) => Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;
        // A circle reaches into the box when its centre is nearer the box than its radius.
        public bool Touches(GraphPoint centre, double radius)
        {
            double dx = Math.Max(0, Math.Max(Left - centre.X, centre.X - Right)), dy = Math.Max(0, Math.Max(Top - centre.Y, centre.Y - Bottom));
            return dx * dx + dy * dy < radius * radius;
        }
    }

    // A label's box is its estimated width by its line, 1.2 em, about the middle of its letters, .35 em above the baseline: a
    // node's 12-pixel label hangs 42 below the node's centre, so its box runs from 30.6 to 45 below it, clear of the foot at 50.
    private static Box LabelBox(GraphNode node, GraphPoint at)
    {
        var half = Label(node) / 2;
        return new(at.X - half, at.Y + 42 - 4.2 - 7.2, at.X + half, at.Y + 42 - 4.2 + 7.2);
    }

    // An edge's 10-pixel label, anchored at x and standing on the baseline y.
    private static Box TextBox(double x, double y, string anchor, double wide)
    {
        var left = anchor == "start" ? x : anchor == "end" ? x - wide : x - wide / 2;
        return new(left, y - 3.5 - 6, left + wide, y - 3.5 + 6);
    }

    // Whether the segment from a to b passes through the inside of the box: clipped to the box's four sides in turn, some of it is
    // left. A segment that only grazes a side or a corner does not cross.
    private static bool Crosses(GraphPoint a, GraphPoint b, Box box)
    {
        double low = 0, high = 1, dx = b.X - a.X, dy = b.Y - a.Y;
        foreach (var (p, q) in new[] { (-dx, a.X - box.Left), (dx, box.Right - a.X), (-dy, a.Y - box.Top), (dy, box.Bottom - a.Y) })
        {
            if (p == 0) { if (q <= 0) return false; continue; }
            if (p < 0) low = Math.Max(low, q / p); else high = Math.Min(high, q / p);
            if (low >= high) return false;
        }
        return true;
    }

    // The edge as Path draws it, as a polyline: a straight edge as it is, and each quadratic segment of a bent one in sixteen pieces.
    private static GraphPoint[] Flatten(GraphPoint[] points)
    {
        if (points.Length == 2) return points;
        var line = new List<GraphPoint> { points[0] };
        var from = points[0];
        for (var i = 1; i < points.Length - 1; i++)
        {
            var to = new GraphPoint((points[i].X + points[i + 1].X) / 2, (points[i].Y + points[i + 1].Y) / 2);
            for (var k = 1; k <= 16; k++)
            {
                double t = k / 16d, u = 1 - t;
                line.Add(new(u * u * from.X + 2 * u * t * points[i].X + t * t * to.X, u * u * from.Y + 2 * u * t * points[i].Y + t * t * to.Y));
            }
            from = to;
        }
        line.Add(points[^1]);
        return line.ToArray();
    }

    // The point a fraction of a polyline's length along it. Half way along a straight edge is its midpoint, worked out as the
    // label's place always has been, so that a label that stays there is drawn exactly as before.
    private static GraphPoint Along(GraphPoint[] line, double fraction)
    {
        if (line.Length == 2 && fraction == .5) return new((line[1].X + line[0].X) / 2, (line[1].Y + line[0].Y) / 2);
        var left = fraction * Enumerable.Range(1, line.Length - 1).Sum(i => Distance(line[i - 1], line[i]));
        for (var i = 1; i < line.Length; i++)
        {
            var piece = Distance(line[i - 1], line[i]);
            if (piece > 0 && left <= piece) return new(line[i - 1].X + (line[i].X - line[i - 1].X) * left / piece, line[i - 1].Y + (line[i].Y - line[i - 1].Y) * left / piece);
            left -= piece;
        }
        return line[^1];
        static double Distance(GraphPoint a, GraphPoint b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
    }

    private sealed record Arrangement(IReadOnlyList<NodePosition> Nodes, IReadOnlyList<EdgeRoute> Routes, int Crossings);

    /// <summary>Longest-path levels, routing points for edges that span more than one level, then barycenter ordering sweeps.</summary>
    private static Arrangement Arrange(GraphSpec graph)
    {
        var levels = Levels(graph);
        var depth = levels.Values.Max();
        var layers = Enumerable.Range(0, depth + 1).Select(_ => new List<Slot>()).ToList();
        var slots = new Dictionary<string, Slot>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            var slot = new Slot { Node = node.Id, Level = levels[node.Id] };
            slots[node.Id] = slot; layers[slot.Level].Add(slot);
        }
        var chains = new Slot[graph.Edges.Count][];
        for (var e = 0; e < graph.Edges.Count; e++)
        {
            var edge = graph.Edges[e];
            if (edge.Source == edge.Target) { chains[e] = [slots[edge.Source], slots[edge.Source]]; continue; }
            var chain = new List<Slot> { slots[edge.Source] };
            for (var level = levels[edge.Source] + 1; level < levels[edge.Target]; level++)
            {
                var bend = new Slot { Level = level };
                layers[level].Add(bend); chain.Add(bend);
            }
            chain.Add(slots[edge.Target]);
            for (var i = 1; i < chain.Count; i++) { chain[i - 1].Below.Add(chain[i]); chain[i].Above.Add(chain[i - 1]); }
            chains[e] = chain.ToArray();
        }

        Renumber(layers);
        var best = layers.Select(layer => layer.ToArray()).ToArray();
        var fewest = Count(layers);
        for (var pass = 0; pass < 8 && fewest > 0; pass++)
        {
            var downward = pass % 2 == 0;
            var order = downward ? Enumerable.Range(1, depth) : Enumerable.Range(0, depth).Reverse();
            foreach (var level in order) Sweep(layers[level], downward);
            Renumber(layers);
            var crossings = Count(layers);
            if (crossings < fewest) { fewest = crossings; best = layers.Select(layer => layer.ToArray()).ToArray(); }
        }
        for (var level = 0; level < layers.Count; level++) { layers[level].Clear(); layers[level].AddRange(best[level]); }
        Renumber(layers);

        var down = Down(graph);
        foreach (var layer in layers)
            foreach (var slot in layer)
                if (down)
                {
                    // Top to bottom mirrors left to right: the levels run between the same 90-pixel ends, and each level's slots
                    // share the width in equal bands, as they share the height across, inside the 24-pixel margins the title keeps.
                    slot.X = 24 + (graph.Width - 48d) * (slot.Order + .5) / layer.Count;
                    slot.Y = depth == 0 ? graph.Height / 2d : 90 + (graph.Height - 180d) * slot.Level / depth;
                }
                else
                {
                    slot.X = depth == 0 ? graph.Width / 2d : 90 + (graph.Width - 180d) * slot.Level / depth;
                    slot.Y = 90 + (graph.Height - 130d) * (slot.Order + .5) / layer.Count;
                }
        return new(
            graph.Nodes.Select(n => new NodePosition(n.Id, slots[n.Id].X, slots[n.Id].Y)).ToArray(),
            chains.Select((chain, i) => new EdgeRoute(i, chain.Select(s => new GraphPoint(s.X, s.Y)).ToArray())).ToArray(),
            fewest);
    }

    private sealed class Slot
    {
        public string? Node;
        public int Level, Order;
        public double X, Y;
        public readonly List<Slot> Above = [], Below = [];
    }

    private static void Renumber(List<List<Slot>> layers)
    {
        foreach (var layer in layers)
            for (var i = 0; i < layer.Count; i++) layer[i].Order = i;
    }

    private static void Sweep(List<Slot> layer, bool downward)
    {
        // Slots without a neighbour in the reference layer keep their current position.
        var keyed = layer.Select((slot, index) =>
        {
            var neighbours = downward ? slot.Above : slot.Below;
            return (slot, index, key: neighbours.Count == 0 ? slot.Order : neighbours.Average(n => (double)n.Order));
        }).OrderBy(entry => entry.key).ThenBy(entry => entry.index).Select(entry => entry.slot).ToArray();
        layer.Clear(); layer.AddRange(keyed);
    }

    private static int Count(List<List<Slot>> layers)
    {
        var total = 0;
        foreach (var layer in layers)
        {
            var segments = layer.SelectMany(slot => slot.Below.Select(below => (slot.Order, Target: below.Order))).ToArray();
            for (var i = 0; i < segments.Length; i++)
                for (var j = i + 1; j < segments.Length; j++)
                    if ((segments[i].Order - segments[j].Order) * (segments[i].Target - segments[j].Target) < 0) total++;
        }
        return total;
    }

    private static Dictionary<string, int> Levels(GraphSpec graph)
    {
        var outgoing = graph.Nodes.ToDictionary(n => n.Id, _ => new List<string>(), StringComparer.Ordinal);
        var indegrees = graph.Nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        var levels = graph.Nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        foreach (var e in graph.Edges.Where(e => e.Source != e.Target)) { outgoing[e.Source].Add(e.Target); indegrees[e.Target]++; }
        var queue = new Queue<string>(graph.Nodes.Where(n => indegrees[n.Id] == 0).Select(n => n.Id));
        var visited = 0;
        while (queue.TryDequeue(out var id))
        {
            visited++;
            foreach (var target in outgoing[id])
            {
                levels[target] = Math.Max(levels[target], levels[id] + 1);
                if (--indegrees[target] == 0) queue.Enqueue(target);
            }
        }
        if (visited != graph.Nodes.Count) throw new ArgumentException("Layered layout requires an acyclic directed graph. Use Circular for cycles.");
        return levels;
    }

    private static void Validate(GraphSpec g)
    {
        ArgumentNullException.ThrowIfNull(g); ChartValidation.Dimensions(g.Width, g.Height); ChartValidation.Text(g.Title);
        if (!Enum.IsDefined(g.Layout) || !Enum.IsDefined(g.Direction) || !Enum.IsDefined(g.Theme)) throw new ArgumentException("Unknown layout, direction or theme.");
        ChartValidation.Style(g.Style);
        if (g.Nodes is null || g.Edges is null || g.Nodes.Count > 250 || g.Edges.Count > 2000) throw new ArgumentException("Graphs support at most 250 nodes and 2000 edges.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in g.Nodes)
        {
            if (n is null || n.Label is null || string.IsNullOrWhiteSpace(n.Id) || !ids.Add(n.Id)) throw new ArgumentException("Node IDs must be nonempty and unique.");
            ChartValidation.Text(n.Id); ChartValidation.Text(n.Label); ChartValidation.Color(n.Color);
        }
        foreach (var e in g.Edges)
        {
            if (e is null || !ids.Contains(e.Source) || !ids.Contains(e.Target)) throw new ArgumentException("Every edge endpoint must reference a node.");
            ChartValidation.Text(e.Label);
        }
    }
}
