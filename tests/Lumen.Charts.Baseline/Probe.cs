using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lumen.Charts;

public static class Probe
{
    public static void Run(string[] args)
    {
        if (args.Length >= 3 && args[1] == "heatmaps") { Heatmaps(args[2]); return; }
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
}
