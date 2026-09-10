using System.Globalization;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

// Drives a running host in a real browser. The selectors below belong to the components, not to a
// particular sample, so the same suite runs against the server gallery and the WebAssembly host.
var address = args.FirstOrDefault() ?? "http://localhost:5188";
var failures = new List<string>();
var passed = 0;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 1000 } });
page.SetDefaultTimeout(15_000);

await page.GotoAsync(address, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
// The tooltip element is created by the component's JavaScript module, so its presence proves
// interop is live: a Blazor Server circuit is connected, or the WebAssembly runtime has started.
await page.WaitForSelectorAsync(".lumen-tooltip", new() { State = WaitForSelectorState.Attached, Timeout = 120_000 });

void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
async Task Test(string name, Func<Task> action)
{
    try { await action(); Console.WriteLine($"PASS {name}"); passed++; }
    catch (Exception error) { failures.Add($"{name}: {error.Message}"); Console.WriteLine($"FAIL {name}: {error.Message}"); }
}

var chart = page.Locator(".lumen-chart").First;
var tooltip = chart.Locator(".lumen-tooltip");
var status = chart.Locator(".lumen-status");
ILocator Marks() => chart.Locator(".lumen-datum");
ILocator Tool(string name) => chart.Locator(".lumen-tools button", new() { HasTextString = name });

await Test("Chart renders focusable marks", async () =>
{
    Check(await Marks().CountAsync() > 0, "no marks rendered");
    Check(await Marks().First.GetAttributeAsync("tabindex") == "0");
    Check(!string.IsNullOrWhiteSpace(await Marks().First.GetAttributeAsync("aria-label")));
});

await Test("Hovering a mark shows its tooltip", async () =>
{
    var mark = Marks().First;
    var label = await mark.GetAttributeAsync("aria-label");
    await mark.HoverAsync();
    await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    Check(await tooltip.TextContentAsync() == label, "tooltip text does not match the accessible name");
});

await Test("Keyboard focus shows the tooltip and Escape hides it", async () =>
{
    await Marks().First.FocusAsync();
    await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    await page.Keyboard.PressAsync("Escape");
    await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
});

await Test("Selecting a mark reports the original observation", async () =>
{
    await Marks().Nth(1).ClickAsync();
    await page.WaitForFunctionAsync("() => document.querySelector('.lumen-status')?.textContent.trim().length > 0");
    Check((await status.TextContentAsync())!.Contains(':'), "status did not name the selected point");
});

await Test("Export SVG downloads the chart", async () =>
{
    var download = await page.RunAndWaitForDownloadAsync(async () => await Tool("Export SVG").ClickAsync());
    Check(download.SuggestedFilename == "chart.svg", download.SuggestedFilename);
});

await Test("Export CSV downloads the original observations", async () =>
{
    var download = await page.RunAndWaitForDownloadAsync(async () => await Tool("Export CSV").ClickAsync());
    Check(download.SuggestedFilename == "chart.csv", download.SuggestedFilename);
    var path = await download.PathAsync();
    var csv = await File.ReadAllTextAsync(path!);
    Check(csv.StartsWith("Series,X"), "unexpected CSV header");
});

await Test("Export PNG rasterizes through the canvas", async () =>
{
    var download = await page.RunAndWaitForDownloadAsync(async () => await Tool("Export PNG").ClickAsync());
    Check(download.SuggestedFilename == "chart.png", download.SuggestedFilename);
    var path = await download.PathAsync();
    var bytes = await File.ReadAllBytesAsync(path!);
    Check(bytes.Length > 5_000, $"PNG is only {bytes.Length} bytes");
    Check(bytes[0] == 0x89 && bytes[1] == 'P' && bytes[2] == 'N' && bytes[3] == 'G', "not a PNG signature");
});

await Test("Hiding a series removes its marks", async () =>
{
    var legend = chart.Locator(".lumen-legend button");
    if (await legend.CountAsync() < 2) return;
    var before = await Marks().CountAsync();
    await legend.First.ClickAsync();
    await page.WaitForFunctionAsync($"() => document.querySelectorAll('.lumen-chart .lumen-datum').length < {before}");
    Check(await legend.First.GetAttributeAsync("aria-pressed") == "false");
    await legend.First.ClickAsync();
    await page.WaitForFunctionAsync($"() => document.querySelectorAll('.lumen-chart .lumen-datum').length === {before}");
});

await Test("The data table lists observations with scoped headers", async () =>
{
    await Tool("View data").ClickAsync();
    var table = chart.Locator(".lumen-table table");
    await table.WaitForAsync();
    Check(await table.Locator("caption").CountAsync() == 1);
    Check(await table.Locator("th[scope=col]").CountAsync() == 3, "table headers are not scoped");
    Check(await table.Locator("tbody tr").CountAsync() > 0);
    await Tool("Hide data").ClickAsync();
});

await Test("Zooming narrows the axis and reset restores it", async () =>
{
    var zoom = chart.Locator(".lumen-tools button[aria-label='Zoom in']");
    if (await zoom.CountAsync() == 0) return;
    var labels = async () => string.Join("|", await chart.Locator("svg text").AllTextContentsAsync());
    var before = await labels();
    await zoom.ClickAsync();
    await page.WaitForTimeoutAsync(500);
    Check(await labels() != before, "zoom did not change the axis");
    await Tool("Reset view").ClickAsync();
    await page.WaitForTimeoutAsync(500);
    Check(await labels() == before, "reset did not restore the axis");
});

var graph = page.Locator(".lumen-chart").Nth(1);
if (await graph.CountAsync() > 0 && await graph.Locator("[data-node]").CountAsync() > 0)
{
    await Test("Dragging a graph node moves it and its edges", async () =>
    {
        var node = graph.Locator("[data-node]").Nth(2);
        var id = await node.GetAttributeAsync("data-node");
        var before = await node.GetAttributeAsync("data-position");
        // Aim at the painted circle: the group's own box spans the gap between the circle and its label.
        // The mouse works in viewport coordinates and does not scroll on its own, unlike ClickAsync.
        await node.ScrollIntoViewIfNeededAsync();
        var box = await node.Locator("circle").BoundingBoxAsync();
        await page.Mouse.MoveAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X + box.Width / 2 + 45, box.Y + box.Height / 2 - 60, new() { Steps = 10 });
        await page.Mouse.UpAsync();
        await page.WaitForFunctionAsync($"() => document.querySelector('[data-node=\"{id}\"]')?.dataset.position !== '{before}'");
        var after = await graph.Locator($"[data-node='{id}']").GetAttributeAsync("data-position");
        Check(after != before, "the node did not move");
        Check((await graph.Locator(".lumen-status").TextContentAsync())!.StartsWith("Moved"), "no move was reported");
    });

    await Test("Reset layout restores the computed positions", async () =>
    {
        await graph.Locator(".lumen-tools button", new() { HasTextString = "Reset layout" }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.lumen-chart')[1].querySelector('.lumen-status')?.textContent.includes('Layout reset')");
        Check(true);
    });

    await Test("Selecting a graph node reports it", async () =>
    {
        var node = graph.Locator("[data-node]").First;
        await node.ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.lumen-chart')[1].querySelector('.lumen-status')?.textContent.startsWith('Selected')");
        Check(true);
    });
}

// A fresh load, so the sweep sees the page as a visitor first meets it rather than mid-interaction.
await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
await page.WaitForSelectorAsync(".lumen-tooltip", new() { State = WaitForSelectorState.Attached, Timeout = 120_000 });

await Test("axe-core reports no WCAG A or AA violation", async () =>
{
    var result = await page.RunAxe(new AxeRunOptions
    {
        RunOnly = new RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] }
    });
    foreach (var violation in result.Violations)
    {
        Console.WriteLine($"  {violation.Impact} - {violation.Id}: {violation.Help} ({violation.Nodes.Length} node(s))");
        foreach (var node in violation.Nodes.Take(3)) Console.WriteLine($"      {node.Html}");
    }
    Check(result.Violations.Length == 0, string.Join("; ", result.Violations.Select(v => $"{v.Id} on {v.Nodes.Length} node(s)")));
});

Console.WriteLine($"\n{passed} passed; {failures.Count} failed. ({address})");
foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;
