namespace Lumen.Charts;

/// <summary>Lays out and draws network graphs. The layout is deterministic: one graph always lays out the same way.</summary>
public static class GraphEngine
{
    private const double Radius = 23, Trim = 25;
    // A node's label hangs under it, its baseline 42 below the centre, so top to bottom an edge that runs below a node meets it
    // 50 below the centre, at the foot of the label, rather than through the words. A row then holds a node, its label and an
    // edge long enough to carry its own label and arrowhead clear of both.
    private const double Foot = 50, Row = 110;

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
    /// reader dragged them; their edges follow, and the other nodes keep the layout's places.</summary>
    public static string Render(GraphSpec graph, IReadOnlyDictionary<string, GraphPoint>? positions = null)
    {
        var layout = Layout(graph).ToDictionary(p => p.Id, p => positions is not null && positions.TryGetValue(p.Id, out var moved) ? moved : new GraphPoint(p.X, p.Y), StringComparer.Ordinal);
        var routes = Routes(graph);
        var w = new SvgWriter { Style = graph.Style ?? ChartSvg.Preset(graph.Theme) };
        var down = Down(graph);
        var crossings = graph.Layout == GraphLayout.Circular ? "" : $" · {Crossings(graph)} edge crossings";
        ChartSvg.Begin(w, graph.Width, graph.Height, graph.Title,
            $"{graph.Nodes.Count} nodes · {graph.Edges.Count} directed connections · {graph.Layout} layout{(down ? " top to bottom" : "")}{crossings}", wrap: true);
        // An edge leaves and reaches a node trimmed clear of its circle; top to bottom, one that runs on below the node meets it at
        // the foot of its label instead, so that it never runs through the words.
        GraphPoint End(GraphPoint node, GraphPoint towards) => down && towards.Y > node.Y + Foot ? new(node.X, node.Y + Foot) : Shift(node, towards, Trim);
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
            var start = End(points[0], points[1]);
            // A straight edge that leaves from under a label arrives aimed from there.
            var end = End(points[^1], down && points.Length == 2 ? start : points[^2]);
            points[0] = start; points[^1] = end;
            w.Add($"<path d='{Path(points)}' fill='none' stroke='{w.Style.Edge}' stroke-width='1.5'{w.Fixed}/>");
            var direction = Unit(points[^2], end);
            w.Add($"<path d='M{N(end.X)},{N(end.Y)} L{N(end.X - direction.X * 9 - direction.Y * 4)},{N(end.Y - direction.Y * 9 + direction.X * 4)} L{N(end.X - direction.X * 9 + direction.Y * 4)},{N(end.Y - direction.Y * 9 - direction.X * 4)} Z' fill='{w.Style.Edge}'/>");
            if (edge.Label is not null)
            {
                var middle = points[points.Length / 2];
                var previous = points[points.Length / 2 - 1];
                var text = ChartSvg.Short(edge.Label, 20);
                if (down)
                {
                    // Top to bottom an edge runs down, so its label stands beside it, at the middle of its middle stretch, on its
                    // right unless that would take it past the drawing's edge.
                    double x = (middle.X + previous.X) / 2, y = (middle.Y + previous.Y) / 2;
                    var right = x + 6 + ChartSvg.Wide(text) * 10 / 11 <= graph.Width - 4;
                    w.Text(right ? x + 6 : x - 6, y + 3.5, text, $"text-anchor='{(right ? "start" : "end")}' class='lumen-muted' font-size='10'");
                }
                else w.Text((middle.X + previous.X) / 2, (middle.Y + previous.Y) / 2 - 9, text, "text-anchor='middle' class='lumen-muted' font-size='10'");
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
    /// circular graph keeps its circle's arithmetic and grows taller, up to 2,160 pixels, until neighbouring nodes stand that far
    /// apart. Nodes at one height, which no height can part, need room for their two labels and their circles side by side at
    /// least, and a width too narrow for that becomes the narrowest that has it. A graph is never drawn shorter than its own
    /// <see cref="GraphSpec.Height"/>.
    /// </summary>
    public static GraphSpec Fit(GraphSpec graph, int width)
    {
        Validate(graph);
        width = Math.Clamp(width, 320, 4096);
        if (graph.Nodes.Count == 0) return Sized(graph, width, graph.Height, graph.Direction);
        // A label is drawn 12 pixels high, cut to 22 characters.
        static double Label(GraphNode node) => ChartSvg.Wide(ChartSvg.Short(node.Label, 22)) * 12 / 11;
        var room = Math.Max(graph.Nodes.Max(Label) + 16, 2 * Radius + 24);
        if (graph.Layout == GraphLayout.Circular)
        {
            var n = graph.Nodes.Count;
            var labels = graph.Nodes.Select(Label).ToArray();
            // A node and its mirror image across the circle stand at one height whatever the height, so only the width parts them,
            // by twice the circle's half-width times this sine.
            for (var i = 1; i < n - i; i++)
            {
                var apart = 2 * Math.Abs(Math.Sin(2 * Math.PI * i / n));
                var need = Math.Max((labels[i] + labels[n - i]) / 2, 2 * Radius);
                width = Math.Max(width, (int)Math.Min(4096, Math.Ceiling(200 + 2 * need / apart)));
            }
            // Neighbours round the circle stand further apart as it grows taller, unless they stand at one height.
            var across = width / 2d - 100;
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

    private static IReadOnlyList<NodePosition> Circle(GraphSpec graph) =>
        graph.Nodes.Select((n, i) => new NodePosition(n.Id,
            graph.Width / 2d + (graph.Width / 2d - 100) * Math.Cos(2 * Math.PI * i / graph.Nodes.Count - Math.PI / 2),
            (graph.Height + 45) / 2d + (graph.Height / 2d - 100) * Math.Sin(2 * Math.PI * i / graph.Nodes.Count - Math.PI / 2))).ToArray();

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
