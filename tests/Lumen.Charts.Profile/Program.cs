using System.Diagnostics;
using System.Text.Json;
using Lumen.Charts;
using Microsoft.Playwright;

// Measures the two halves of the cost separately: producing the SVG in .NET, and parsing, laying
// out and painting it in a browser. Reports medians of repeated runs; it asserts nothing, because
// timings belong in a record rather than in a pass-or-fail gate.
const int repeats = 5;

var random = new Random(12);
ChartSpec Spec(ChartKind kind, int points, int budget) => new()
{
    Kind = kind,
    Title = $"{kind} with {points:N0} points",
    MaxRenderedPoints = budget,
    Series = [new("Signal", Enumerable.Range(0, points)
        .Select(i => new ChartPoint(i, Math.Sin(i * .01) * 50 + random.NextDouble() * 10)).ToArray())]
};

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
await page.SetContentAsync("<div id='host' style='width:1200px'></div>");

static double Median(List<double> values)
{
    values.Sort();
    return values[values.Count / 2];
}

Console.WriteLine("| Chart | Input points | Budget | Rendered marks | SVG generation | SVG size | Browser render | DOM nodes |");
Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|");

foreach (var (kind, points, budget) in new (ChartKind, int, int)[]
{
    (ChartKind.Line, 1_000, 1200), (ChartKind.Line, 10_000, 1200), (ChartKind.Line, 100_000, 1200),
    (ChartKind.Line, 100_000, 5000), (ChartKind.Line, 100_000, 16),
    (ChartKind.Scatter, 1_000, 1200), (ChartKind.Scatter, 10_000, 1200), (ChartKind.Scatter, 50_000, 1200),
    (ChartKind.Scatter, 100_000, 1200)
})
{
    var spec = Spec(kind, points, budget);
    ChartSvg.Render(spec); // warm the code paths before timing
    var generation = new List<double>();
    string svg = "";
    for (var run = 0; run < repeats; run++)
    {
        var timer = Stopwatch.StartNew();
        svg = ChartSvg.Render(spec);
        timer.Stop();
        generation.Add(timer.Elapsed.TotalMilliseconds);
    }

    var render = new List<double>();
    var measurement = new { marks = 0, nodes = 0, ms = 0d };
    for (var run = 0; run < repeats; run++)
    {
        var result = await page.EvaluateAsync<JsonElement>(@"async (svg) => {
            const host = document.getElementById('host');
            host.innerHTML = '';
            await new Promise(r => requestAnimationFrame(r));
            const start = performance.now();
            host.innerHTML = svg;
            // Force layout, then wait for the frame that paints it.
            host.firstElementChild.getBoundingClientRect();
            await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
            const elapsed = performance.now() - start;
            return { ms: elapsed, marks: host.querySelectorAll('.lumen-datum').length, nodes: host.querySelectorAll('*').length };
        }", svg);
        render.Add(result.GetProperty("ms").GetDouble());
        measurement = new { marks = result.GetProperty("marks").GetInt32(), nodes = result.GetProperty("nodes").GetInt32(), ms = result.GetProperty("ms").GetDouble() };
    }

    Console.WriteLine($"| {kind} | {points:N0} | {budget:N0} | {measurement.marks:N0} | {Median(generation):0.0} ms | {svg.Length / 1024.0:N0} KB | {Median(render):0.0} ms | {measurement.nodes:N0} |");
}

// Re-rendering is what an interactive chart does on every zoom, pan or series toggle.
var interactive = Spec(ChartKind.Line, 100_000, 1200);
var svgText = ChartSvg.Render(interactive);
var updates = new List<double>();
for (var run = 0; run < repeats; run++)
{
    var result = await page.EvaluateAsync<double>(@"async (svg) => {
        const host = document.getElementById('host');
        host.innerHTML = svg;
        await new Promise(r => requestAnimationFrame(r));
        const start = performance.now();
        host.innerHTML = svg;
        host.firstElementChild.getBoundingClientRect();
        await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
        return performance.now() - start;
    }", svgText);
    updates.Add(result);
}
Console.WriteLine($"\nReplacing a rendered 1,200-mark chart in place: {Median(updates):0.0} ms median of {repeats}.");
Console.WriteLine($"Browser: {browser.Version}");
