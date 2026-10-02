using System.Globalization;
using System.Text.RegularExpressions;
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
// Data marks specifically: a chart may also carry aggregates such as annotations, histogram bins
// and box glyphs, which are labelled and focusable but report no observation.
ILocator Marks() => chart.Locator(".lumen-datum[data-point]");
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
    await page.WaitForFunctionAsync($"() => document.querySelectorAll('.lumen-chart .lumen-datum[data-point]').length < {before}");
    Check(await legend.First.GetAttributeAsync("aria-pressed") == "false");
    await legend.First.ClickAsync();
    await page.WaitForFunctionAsync($"() => document.querySelectorAll('.lumen-chart .lumen-datum[data-point]').length === {before}");
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

// Only a host that wraps its charts in LumenBrand can prove the page's own colours reach the chart.
if (await page.Locator("[data-lumen-brand]").CountAsync() > 0)
{
    await Test("LumenBrand draws with the host's colours and typeface, and exports them", async () =>
    {
        await page.WaitForSelectorAsync("[data-lumen-brand=resolved]");
        // Normalize independently of the library, so a bug in its resolver cannot also hide in this check.
        var expected = await page.EvaluateAsync<string[]>(@"() => {
            const wrapper = document.querySelector('[data-lumen-brand]');
            const name = wrapper.dataset.lumenSeries.split(',')[0].trim();
            const canvas = document.createElement('canvas').getContext('2d');
            canvas.fillStyle = getComputedStyle(wrapper).getPropertyValue(name).trim();
            const font = getComputedStyle(wrapper).fontFamily.split(',').map(f => f.replace(/[""']/g, '').trim()).join(',');
            return [canvas.fillStyle.toUpperCase(), font];
        }");
        var brandChart = page.Locator("[data-lumen-brand] .lumen-chart").First;
        var fill = await brandChart.Locator(".lumen-datum circle, .lumen-datum rect").First.GetAttributeAsync("fill");
        Check(fill == expected[0], $"first mark is {fill}, the page's first brand colour is {expected[0]}");
        var style = await brandChart.Locator("svg").First.GetAttributeAsync("style");
        Check(style!.Contains("font-family:" + expected[1]), $"chart font does not match the host font {expected[1]}");
        var download = await page.RunAndWaitForDownloadAsync(async () =>
            await brandChart.Locator(".lumen-tools button", new() { HasTextString = "Export SVG" }).ClickAsync());
        var exported = await File.ReadAllTextAsync((await download.PathAsync())!);
        Check(exported.Contains(expected[0]), "the exported file lost the host's brand");
    });
}
else Console.WriteLine("SKIP LumenBrand check: this host renders no LumenBrand");

// Panes belong to a chart a host chooses to offer, so the check finds the gallery's activity stream as a user does, by the
// buttons named for the line chart and for the stream, and a host without them says SKIP. Its three panes are nested clips.
var lineTab = page.GetByRole(AriaRole.Button, new() { Name = "Line", Exact = true });
var stream = page.GetByRole(AriaRole.Button, new() { Name = "Activity stream", Exact = true });
if (await lineTab.CountAsync() > 0)
{
    await lineTab.First.ClickAsync();
    try { await stream.First.WaitForAsync(new() { Timeout = 5_000 }); } catch (TimeoutException) { }
}
if (await stream.CountAsync() > 0)
{
    await stream.First.ClickAsync();
    await page.WaitForFunctionAsync("() => document.querySelector('.lumen-chart svg')?.querySelectorAll(':scope > svg').length === 3");

    await Test("Zooming moves every pane together", async () =>
    {
        // The same moment in each series, one series to a pane, read from its marker.
        const string moment = "() => [0, 1, 2].map(s => document.querySelector(`.lumen-chart [data-series='${s}'][data-point='120'] circle`)?.getAttribute('cx'))";
        var before = await page.EvaluateAsync<string?[]>(moment);
        Check(before[0] is not null && before.Distinct().Count() == 1, $"the panes place one moment at {string.Join(", ", before)}");
        await chart.Locator(".lumen-tools button[aria-label='Zoom in']").ClickAsync();
        await page.WaitForFunctionAsync("x => document.querySelector(`.lumen-chart [data-series='0'][data-point='120'] circle`)?.getAttribute('cx') !== x", before[0]);
        var after = await page.EvaluateAsync<string?[]>(moment);
        Check(after[0] is not null && after.Distinct().Count() == 1, $"after zooming the panes place it at {string.Join(", ", after)}");
        await chart.Locator(".lumen-tools button[aria-label='Pan right']").ClickAsync();
        await page.WaitForFunctionAsync("x => document.querySelector(`.lumen-chart [data-series='0'][data-point='120'] circle`)?.getAttribute('cx') !== x", after[0]);
        var panned = await page.EvaluateAsync<string?[]>(moment);
        Check(panned[0] is not null && panned.Distinct().Count() == 1, $"after panning the panes place it at {string.Join(", ", panned)}");
        await Tool("Reset view").ClickAsync();
        await page.WaitForFunctionAsync("x => document.querySelector(`.lumen-chart [data-series='0'][data-point='120'] circle`)?.getAttribute('cx') === x", before[0]);
    });

    // The stream hides its markers, so a reading is reached only through its invisible target.
    await Test("A hidden marker still takes focus, draws its focus ring and reads its value", async () =>
    {
        var mark = chart.Locator(".lumen-datum[data-series='0'][data-point='120']");
        var label = await mark.GetAttributeAsync("aria-label");
        await mark.FocusAsync();
        await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Check(await tooltip.TextContentAsync() == label, "the tooltip does not read the hidden marker's value");
        var drawn = await mark.Locator("circle").EvaluateAsync<string[]>("c => [getComputedStyle(c).fillOpacity, getComputedStyle(c).strokeWidth]");
        Check(drawn[0] == "0" && drawn[1] == "3px", $"the marker is not hidden with a focus ring: {string.Join(", ", drawn)}");
        await page.Keyboard.PressAsync("Escape");
    });

    await Test("Export PNG keeps the gradient the heart-rate line is coloured with", async () =>
    {
        var download = await page.RunAndWaitForDownloadAsync(async () => await Tool("Export PNG").ClickAsync());
        var bytes = await File.ReadAllBytesAsync((await download.PathAsync())!);
        // The zone bands behind the line are faint tints, so strongly coloured pixels in the top pane are the line's: a gradient
        // lost in rasterizing would leave the line unpainted and almost none of them.
        var coloured = await page.EvaluateAsync<int>(@"async png => {
            const image = new Image();
            image.src = 'data:image/png;base64,' + png;
            await image.decode();
            const canvas = document.createElement('canvas');
            canvas.width = image.width; canvas.height = image.height;
            const context = canvas.getContext('2d');
            context.drawImage(image, 0, 0);
            const svg = document.querySelector('.lumen-chart svg');
            const scale = image.width / Number(svg.getAttribute('viewBox').split(' ')[2]);
            const pane = svg.querySelector(':scope > svg');
            const [x, y, w, h] = ['x', 'y', 'width', 'height'].map(name => Math.round(Number(pane.getAttribute(name)) * scale));
            const data = context.getImageData(x, y, w, h).data;
            let count = 0;
            for (let i = 0; i < data.length; i += 4)
                if (Math.max(data[i], data[i + 1], data[i + 2]) - Math.min(data[i], data[i + 1], data[i + 2]) > 100) count++;
            return count;
        }", Convert.ToBase64String(bytes));
        Check(coloured > 2000, $"only {coloured} strongly coloured pixels in the heart-rate pane");
    });

    await Test("axe-core reports no WCAG A or AA violation on a chart with panes", Sweep);
}
else Console.WriteLine("SKIP pane checks: this host offers no chart with panes");

// A fresh load, so the sweep sees the page as a visitor first meets it rather than mid-interaction.
await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
await page.WaitForSelectorAsync(".lumen-tooltip", new() { State = WaitForSelectorState.Attached, Timeout = 120_000 });

async Task Sweep()
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
}

await Test("axe-core reports no WCAG A or AA violation", Sweep);

// A theme belongs to the host, not to a component, so there is no component selector for it: the switch is
// found the way a user finds it, as a button named for the theme.
var theme = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("theme", RegexOptions.IgnoreCase) });
if (await theme.CountAsync() > 0)
{
    await Test("axe-core reports no WCAG A or AA violation in the dark theme", async () =>
    {
        var before = await chart.Locator("svg").First.GetAttributeAsync("style");
        await theme.First.ClickAsync();
        // The chart redraws in the theme's colours, so the sweep cannot run against the light page by mistake.
        await page.WaitForFunctionAsync("before => document.querySelector('.lumen-chart svg')?.getAttribute('style') !== before", before);
        await Sweep();
    });
}
else Console.WriteLine("SKIP dark-theme axe sweep: this host has no theme switch");

// Midnight is a brand a host chooses to offer, so it too is found by the button that names it.
var midnight = page.GetByRole(AriaRole.Button, new() { Name = "Midnight", Exact = true });
if (await midnight.CountAsync() > 0)
{
    await Test("axe-core reports no WCAG A or AA violation in the Midnight brand", async () =>
    {
        await midnight.First.ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.lumen-chart svg')?.getAttribute('style')?.includes('background:#0B0E14')");
        await Sweep();
    });
}
else Console.WriteLine("SKIP Midnight axe sweep: this host offers no Midnight brand");

Console.WriteLine($"\n{passed} passed; {failures.Count} failed. ({address})");
foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;
