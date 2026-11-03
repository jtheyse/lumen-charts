using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lumen.Charts;

public static class Probe
{
    public static void Run(string[] args)
    {
        if (args.Length >= 3 && args[1] == "heatmaps") { Heatmaps(args[2]); return; }
        if (args.Length >= 3 && args[1] == "lines") { Lines(args[2]); return; }
        if (args.Length >= 3 && args[1] == "ends") { Ends(args[2]); return; }
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var kolkata = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        static double At(int year, int month, int day, int hour = 0) =>
            TimeAxis.Value(new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero));

        void Show(string name, Axis axis)
        {
            var ticks = axis.Ticks(6);
            Console.WriteLine($"{name}:");
            foreach (var (value, label) in ticks)
            {
                var utc = TimeAxis.Moment(value).UtcDateTime;
                var local = axis.Zone is null ? utc : TimeZoneInfo.ConvertTimeFromUtc(utc, axis.Zone);
                Console.WriteLine($"   {label,-14} local {local:yyyy-MM-dd HH:mm}  utc {utc:yyyy-MM-dd HH:mm}");
            }
        }

        Show("UTC, four days", new Axis(AxisKind.Time, At(2026, 1, 5), At(2026, 1, 9)));
        Show("New York, four days", new Axis(AxisKind.Time, At(2026, 1, 5), At(2026, 1, 9)) { Zone = newYork });
        // Clocks go forward on 8 March 2026 in the United States.
        Show("New York across a spring change", new Axis(AxisKind.Time, At(2026, 3, 6), At(2026, 3, 11)) { Zone = newYork });
        Show("Kolkata, twelve hours", new Axis(AxisKind.Time, At(2026, 5, 4, 3), At(2026, 5, 4, 15)) { Zone = kolkata });
        Show("New York, one year", new Axis(AxisKind.Time, At(2026, 1, 1), At(2026, 12, 31)) { Zone = newYork });
        Console.WriteLine("Format in New York: " + new Axis(AxisKind.Time, At(2026, 1, 5), At(2026, 1, 5, 12)) { Zone = newYork }.Format(At(2026, 1, 5, 4)));
        Console.WriteLine("Format in UTC:      " + new Axis(AxisKind.Time, At(2026, 1, 5), At(2026, 1, 5, 12)).Format(At(2026, 1, 5, 4)));
    }

    // "heatmaps <out.txt>" draws 10,000 random heatmaps from a fixed seed with 0.51.0's API only and writes "i hash" for each, so the same file
    // from 0.51.0's code and from newer code can be compared line by line. A spec that throws writes "i refused". A spec that could meet a
    // 0.52.0 change (cell words kept with FitHeight in narrow cells; a kept value in a short fixed row), by a cautious rule using public
    // options only, has " touched" after its hash: only those lines may differ.
    static void Heatmaps(string path)
    {
        var random = new Random(52);
        bool Flip() => random.Next(2) == 0;
        string[] reasons = ["too few starts to rate", "injured", "no data", "withdrew"];
        string[] gaps = ["did not race", "DNF"];
        var output = new StringBuilder();
        for (var i = 0; i < 10000; i++)
        {
            var rows = random.Next(1, 9);
            var columns = random.Next(1, 13);
            var cellNotes = Flip();
            var keeps = Flip();
            var fit = Flip();
            var spec = new ChartSpec
            {
                Kind = ChartKind.Heatmap,
                Title = "Points per start",
                Description = "Invented categories by season",
                Width = random.Next(320, 1201),
                Height = random.Next(240, 601),
                CellText = Flip() || cellNotes || keeps,
                FitHeight = fit,
                CellNotes = cellNotes,
                NotRatedKeepsValue = keeps,
                ColumnLabelsOnTop = Flip(),
                IncludeZero = Flip(),
                YUnit = Flip() ? " pts" : null,
                CellWidth = Flip() ? null : random.Next(24, 121),
                Source = random.Next(3) switch { 0 => "", 1 => "Invented results", _ => string.Concat(Enumerable.Repeat("Invented results from a made-up season. ", 12)) },
            };
            var anySaid = false;
            var keptWithValue = false;
            var series = new List<ChartSeries>();
            for (var r = 0; r < rows; r++)
            {
                var points = new List<ChartPoint>();
                for (var c = 0; c < columns; c++)
                {
                    var point = new ChartPoint(c, Math.Round(1 + random.NextDouble() * 4, 1), $"{2000 + c}");
                    var kind = random.Next(3);
                    if (kind == 1)
                    {
                        point = point with { NotRated = reasons[random.Next(reasons.Length)] };
                        if (Flip()) point = point with { Y = null };
                        anySaid = true;
                        if (point.Y is not null) keptWithValue = true;
                    }
                    else if (kind == 2)
                    {
                        point = point with { Y = null, GapLabel = gaps[random.Next(gaps.Length)] };
                        anySaid = true;
                    }
                    if (Flip()) point = point with { SubLabel = $"/{random.Next(1, 30)} starts" };
                    if (Flip()) point = point with { ValueNote = $" · {random.Next(1, 90)} pts, {random.Next(1, 20)} riders" };
                    points.Add(point);
                }
                series.Add(new ChartSeries($"Row {r}", points.ToArray()));
            }
            var theme = random.Next(3);
            var style = (theme switch { 0 => ChartStyle.Light, 1 => ChartStyle.Dark, _ => ChartStyle.Midnight }) with { Finish = Flip() ? ChartFinish.Classic : ChartFinish.Refined };
            spec = spec with { Series = series, Style = style };
            var touched = (fit && (spec.CellWidth is null or < 60) && anySaid) || (keeps && !fit && keptWithValue);
            try
            {
                var svg = ChartSvg.Render(spec);
                output.Append(i).Append(' ').Append(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(svg)))[..16]);
                if (touched) output.Append(" touched");
            }
            catch (Exception)
            {
                output.Append(i).Append(" refused");
            }
            output.Append('\n');
        }
        File.WriteAllText(path, output.ToString());
    }

    // "lines <out.txt>" draws 10,000 random line, area, scatter and one-series column charts from a fixed seed with 0.52.0's API only and writes
    // "i hash" for each, so the same file from 0.52.0's code and from newer code can be compared line by line: the options 0.53.0 adds are
    // off in every chart, so every line must be equal. A spec that throws writes "i refused" (both runs must agree).
    static void Lines(string path)
    {
        var random = new Random(53);
        bool Flip() => random.Next(2) == 0;
        string[] words = ["Midlands", "Women", "Masters", "Mountain", "Marathon", "Invented", "Provincial", "Open", "Club", "Leader", "Field", "You", "Best", "Rider", "Series", "Cup", "Trail", "Ridge"];
        string Name()
        {
            var length = random.Next(1, 41);
            var text = new StringBuilder();
            while (text.Length < length)
            {
                if (text.Length > 0) text.Append(' ');
                text.Append(words[random.Next(words.Length)]);
            }
            return text.ToString()[..length].TrimEnd();
        }
        var output = new StringBuilder();
        for (var i = 0; i < 10000; i++)
        {
            var choice = random.Next(4);
            var column = choice == 3;
            var kind = choice switch { 0 => ChartKind.Line, 1 => ChartKind.Area, 2 => ChartKind.Scatter, _ => ChartKind.Column };
            var count = random.Next(1, 9);
            var length = random.Next(2, 31);
            var endLabels = random.Next(3);
            var notes = random.Next(3);
            var markers = random.Next(4);
            var highlight = Flip();
            var valueLabels = Flip();
            var series = new List<ChartSeries>();
            for (var s = 0; s < count; s++)
            {
                var name = Name();
                var points = Enumerable.Range(0, length).Select(x => new ChartPoint(x, random.Next(6) == 0 ? null : Math.Round(5 + random.NextDouble() * 40, 1), x % 4 == 0 ? $"P{x}" : null)).ToArray();
                var mark = column ? (s == 0 ? ChartKind.Line : ChartKind.Column) : kind;
                var lined = mark is ChartKind.Line or ChartKind.Area;
                var item = new ChartSeries(name, points)
                {
                    Markers = lined || mark == ChartKind.Scatter ? (MarkerStyle)(mark == ChartKind.Scatter && markers == 1 ? 0 : markers) : MarkerStyle.Auto,
                    HighlightLast = lined && highlight,
                    ValueLabels = mark != ChartKind.Area && valueLabels,
                };
                if (column && s == 0) item = item with { Kind = ChartKind.Line };
                if (mark != ChartKind.Column && (endLabels == 2 || (endLabels == 1 && Flip())))
                {
                    item = item with { EndLabel = new string('E', random.Next(1, 13)) };
                    if (notes == 2 || (notes == 1 && Flip())) item = item with { EndNote = $"+{random.Next(0, 100)}.{random.Next(10)}s" };
                }
                series.Add(item);
            }
            var annotations = new List<ChartAnnotation>();
            for (var a = random.Next(5); a > 0; a--)
            {
                var label = Flip() ? $"Mark {a}" : null;
                var at = random.Next(0, length);
                annotations.Add(random.Next(column ? 3 : 4) switch
                {
                    0 => new ChartAnnotation(AnnotationAxis.Y, 5 + random.Next(40)) { Label = label },
                    1 => new ChartAnnotation(AnnotationAxis.Y, 5 + random.Next(20)) { To = 30 + random.Next(15), Label = label },
                    2 => new ChartAnnotation(AnnotationAxis.Y, 10 + random.Next(30)) { Label = label },
                    _ => new ChartAnnotation(AnnotationAxis.X, at) { Label = label },
                });
            }
            var style = (random.Next(3) switch { 0 => ChartStyle.Light, 1 => ChartStyle.Dark, _ => ChartStyle.Midnight }) with { Finish = Flip() ? ChartFinish.Classic : ChartFinish.Refined };
            var spec = new ChartSpec
            {
                Kind = kind,
                Title = "Invented results",
                Description = "A made-up season",
                Width = random.Next(320, 1201),
                Height = random.Next(240, 601),
                Series = series,
                Annotations = annotations,
                Style = style,
            };
            var includeLegend = Flip();
            try
            {
                var svg = ChartSvg.Render(spec, includeLegend);
                output.Append(i).Append(' ').Append(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(svg)))[..16]);
            }
            catch (Exception)
            {
                output.Append(i).Append(" refused");
            }
            output.Append((char)10);
        }
        File.WriteAllText(path, output.ToString());
    }

    // "ends <out.txt>" draws 10,000 random line, area, scatter and column charts from a fixed seed with 0.53.0's API only, WrapEndNotes on in every
    // one, an end label of 1 to 24 letters on every series that can have one, and writes "i hash" for each, with " touched" after the hash where
    // the 0.53.1 rule can apply: the chart has no CellWidth and some end label's name asks (lead, step, name, pad) more than the half cap of an
    // upright chart with a left axis, 76 here since no chart has a right axis or a secondary series. So the same file from v0.53.0's code and
    // from newer code can be compared line by line: every line without " touched" must be equal; touched lines may differ. A spec that throws
    // writes "i refused" (both runs must agree).
    static void Ends(string path)
    {
        var random = new Random(531);
        bool Flip() => random.Next(2) == 0;
        const string letters = "EaRmw ";
        string Name()
        {
            var length = random.Next(1, 25);
            var text = new StringBuilder();
            for (var c = 0; c < length; c++)
                text.Append(c == 0 || c == length - 1 ? letters[random.Next(letters.Length - 1)] : letters[random.Next(letters.Length)]);
            return text.ToString();
        }
        // A copy of ChartSvg.Broad: .3 an em for . , : and a space, .36 for -, .9 for m M w W, .62 otherwise, at 12 px.
        static double B(string text) => text.Sum(c => c is '.' or ',' or ':' or ' ' ? .3 : c == '-' ? .36 : c is 'm' or 'M' or 'w' or 'W' ? .9 : .62) * 12;
        var output = new StringBuilder();
        for (var i = 0; i < 10000; i++)
        {
            var choice = random.Next(4);
            var column = choice == 3;
            var kind = choice switch { 0 => ChartKind.Line, 1 => ChartKind.Area, 2 => ChartKind.Scatter, _ => ChartKind.Column };
            var count = random.Next(1, 9);
            var length = random.Next(2, 31);
            var notes = random.Next(3);
            var markers = random.Next(4);
            var highlight = Flip();
            var valueLabels = Flip();
            var cellWidth = column && random.Next(4) == 0 ? 30 + random.Next(30) : (int?)null;
            var series = new List<ChartSeries>();
            var asks = new List<double>();
            for (var s = 0; s < count; s++)
            {
                var points = Enumerable.Range(0, length).Select(x => new ChartPoint(x, random.Next(6) == 0 ? null : Math.Round(5 + random.NextDouble() * 40, 1), x % 4 == 0 ? $"P{x}" : null)).ToArray();
                var mark = column ? (s == 0 ? ChartKind.Line : ChartKind.Column) : kind;
                var lined = mark is ChartKind.Line or ChartKind.Area;
                var item = new ChartSeries($"Series {s}", points)
                {
                    Markers = lined || mark == ChartKind.Scatter ? (MarkerStyle)(mark == ChartKind.Scatter && markers == 1 ? 0 : markers) : MarkerStyle.Auto,
                    HighlightLast = lined && highlight,
                    ValueLabels = mark != ChartKind.Area && valueLabels,
                };
                if (column && s == 0) item = item with { Kind = ChartKind.Line };
                if (mark != ChartKind.Column)
                {
                    var name = Name();
                    item = item with { EndLabel = name };
                    asks.Add((item.HighlightLast ? 14 : 8) + 6 + B(name) + 4);
                    if (notes == 2 || (notes == 1 && Flip())) item = item with { EndNote = $"+{random.Next(0, 100)}.{random.Next(10)}s" };
                }
                series.Add(item);
            }
            var style = (random.Next(3) switch { 0 => ChartStyle.Light, 1 => ChartStyle.Dark, _ => ChartStyle.Midnight }) with { Finish = Flip() ? ChartFinish.Classic : ChartFinish.Refined };
            var width = random.Next(320, 701);
            var spec = new ChartSpec
            {
                Kind = kind,
                Title = "Invented results",
                Description = "A made-up season",
                Width = width,
                Height = random.Next(240, 601),
                Series = series,
                Style = style,
                WrapEndNotes = true,
                CellWidth = cellWidth,
            };
            var touched = cellWidth is null && asks.Any(ask => ask > width - 76 - width / 2d);
            var includeLegend = Flip();
            try
            {
                var svg = ChartSvg.Render(spec, includeLegend);
                output.Append(i).Append(' ').Append(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(svg)))[..16]);
                if (touched) output.Append(" touched");
            }
            catch (Exception)
            {
                output.Append(i).Append(" refused");
            }
            output.Append((char)10);
        }
        File.WriteAllText(path, output.ToString());
    }
}
