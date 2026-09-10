namespace Lumen.Charts;

public static class GraphEngine
{
    private const double Radius = 23, Trim = 25;

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

    public static string Render(GraphSpec graph, IReadOnlyDictionary<string, GraphPoint>? positions = null)
    {
        var layout = Layout(graph).ToDictionary(p => p.Id, p => positions is not null && positions.TryGetValue(p.Id, out var moved) ? moved : new GraphPoint(p.X, p.Y), StringComparer.Ordinal);
        var routes = Routes(graph);
        var w = new SvgWriter();
        var crossings = graph.Layout == GraphLayout.Circular ? "" : $" · {Crossings(graph)} edge crossings";
        ChartSvg.Begin(w, graph.Width, graph.Height, graph.Title,
            $"{graph.Nodes.Count} nodes · {graph.Edges.Count} directed connections · {graph.Layout} layout{crossings}", graph.Theme);
        for (var i = 0; i < graph.Edges.Count; i++)
        {
            var edge = graph.Edges[i];
            var a = layout[edge.Source];
            if (edge.Source == edge.Target)
            {
                w.Add($"<path d='M{N(a.X - 12)},{N(a.Y - 17)} C{N(a.X - 65)},{N(a.Y - 75)} {N(a.X + 65)},{N(a.Y - 75)} {N(a.X + 12)},{N(a.Y - 17)}' fill='none' stroke='#8090AD'><title>{SvgWriter.E(edge.Label ?? "Self-loop")}</title></path>");
                continue;
            }
            // Dragged endpoints replace the layout's own; the bends between them stay where the layout put them.
            var points = routes[i].Points.ToArray();
            points[0] = a; points[^1] = layout[edge.Target];
            var start = Shift(points[0], points[1], Trim);
            var end = Shift(points[^1], points[^2], Trim);
            points[0] = start; points[^1] = end;
            w.Add($"<path d='{Path(points)}' fill='none' stroke='#8090AD' stroke-width='1.5'/>");
            var direction = Unit(points[^2], end);
            w.Add($"<path d='M{N(end.X)},{N(end.Y)} L{N(end.X - direction.X * 9 - direction.Y * 4)},{N(end.Y - direction.Y * 9 + direction.X * 4)} L{N(end.X - direction.X * 9 + direction.Y * 4)},{N(end.Y - direction.Y * 9 - direction.X * 4)} Z' fill='#8090AD'/>");
            if (edge.Label is not null)
            {
                var middle = points[points.Length / 2];
                var previous = points[points.Length / 2 - 1];
                w.Text((middle.X + previous.X) / 2, (middle.Y + previous.Y) / 2 - 9, ChartSvg.Short(edge.Label, 20), "text-anchor='middle' class='lumen-muted' font-size='10'");
            }
        }
        for (var i = 0; i < graph.Nodes.Count; i++)
        {
            var n = graph.Nodes[i]; var p = layout[n.Id]; var color = n.Color ?? ChartSvg.Palette[i % ChartSvg.Palette.Count];
            w.Add($"<g class='lumen-node' tabindex='0' role='button' data-node='{SvgWriter.E(n.Id)}' data-position='{N(p.X)},{N(p.Y)}' aria-label='{SvgWriter.E(n.Label)}'><title>{SvgWriter.E(n.Label)}</title><circle cx='{N(p.X)}' cy='{N(p.Y)}' r='{N(Radius)}' fill='{color}' fill-opacity='.15' stroke='{color}' stroke-width='2'/>");
            w.Text(p.X, p.Y + 5, (i + 1).ToString(), "text-anchor='middle' font-weight='600'");
            w.Text(p.X, p.Y + 42, ChartSvg.Short(n.Label, 22), "text-anchor='middle'"); w.Add("</g>");
        }
        w.Add("</svg>");
        return w.ToString();
    }

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

        foreach (var layer in layers)
            foreach (var slot in layer)
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
        if (!Enum.IsDefined(g.Layout) || !Enum.IsDefined(g.Theme)) throw new ArgumentException("Unknown layout or theme.");
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
