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
// A chart set to FitWidth is drawn at its spec's width until its script measures its box, and then again at the box's width. A
// check that switches to another chart and then measures or focuses its marks waits for that second drawing first; a chart that
// does not fit is settled at once.
Task Settled() => page.WaitForFunctionAsync(@"() => { const c = document.querySelector('.lumen-chart'), s = c?.querySelector(':scope > .lumen-viewport > svg');
    return !!s && (!c.classList.contains('lumen-fit') || Number(s.getAttribute('viewBox').split(' ')[2]) === Math.max(320, c.querySelector(':scope > .lumen-viewport').clientWidth)); }");

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

// The refined finish hides a line's or area's markers until their point is hovered or focused. A host whose first chart
// shows its markers says SKIP.
var concealed = chart.Locator(".lumen-datum[data-point]:has(circle.lumen-marker)");
if (await concealed.CountAsync() > 0)
{
    await Test("A hidden marker shows with its tooltip on hover, and with its focus ring when Tab reaches it", async () =>
    {
        var mark = concealed.First;
        var marker = mark.Locator("circle.lumen-marker");
        // The checks before this one leave a mark focused and the pointer over it, which show its marker as they should.
        await page.EvaluateAsync("() => document.activeElement?.blur()");
        await page.Mouse.MoveAsync(1, 1);
        Check(await marker.EvaluateAsync<string>("c => getComputedStyle(c).opacity") == "0", "the marker shows before its point is reached");
        var label = await mark.GetAttributeAsync("aria-label");
        await mark.HoverAsync();
        await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Check(await tooltip.TextContentAsync() == label, "the tooltip does not read the hidden marker's point");
        Check(await marker.EvaluateAsync<string>("c => getComputedStyle(c).opacity") == "1", "hovering the point does not show its marker");
        // Pointing away hides it again; reached from the chart by Tab, a point shows its marker inside the focus ring.
        await page.Mouse.MoveAsync(1, 1);
        await page.WaitForFunctionAsync("c => getComputedStyle(c).opacity === '0'", await marker.ElementHandleAsync());
        await chart.Locator(".lumen-viewport").FocusAsync();
        for (var i = 0; i < 40 && !await page.EvaluateAsync<bool>("() => !!document.activeElement?.matches('.lumen-datum[data-point]')"); i++)
            await page.Keyboard.PressAsync("Tab");
        var focused = chart.Locator(".lumen-datum[data-point]:focus circle.lumen-marker");
        Check(await focused.CountAsync() == 1, "Tab did not reach a point with a hidden marker");
        var shown = await focused.EvaluateAsync<string[]>("c => [getComputedStyle(c).opacity, getComputedStyle(c).strokeWidth]");
        Check(shown[0] == "1" && shown[1] == "3px", $"a focused point does not show its marker with the focus ring: {string.Join(", ", shown)}");
        await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await page.Keyboard.PressAsync("Escape");
    });
}
else Console.WriteLine("SKIP hidden-marker check: this host's first chart shows its markers");

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

// FitWidth draws a chart at the width its container gives it. The checks run on a host's first fitted chart: the gallery has
// its fitted charts on the Sports & performance page, where they run below, and a page without one says SKIP.
async Task FitChecks(IPage target, ILocator fitted, string where)
{
    var handle = await fitted.ElementHandleAsync();
    // The width it is drawn at, the width it is shown at, its box's width, its title's height on screen, and how far it scrolls.
    async Task<double[]> Measure() => await fitted.EvaluateAsync<double[]>(@"c => { const v = c.querySelector(':scope > .lumen-viewport'), s = v.querySelector(':scope > svg');
        return [Number(s.getAttribute('viewBox').split(' ')[2]), s.getBoundingClientRect().width, v.clientWidth, s.querySelector('text').getBoundingClientRect().height, v.scrollWidth - v.clientWidth]; }");
    const string drawn = "Number(c.querySelector(':scope > .lumen-viewport > svg').getAttribute('viewBox').split(' ')[2])";
    async Task Settle() => await target.WaitForFunctionAsync($"c => {drawn} === Math.max(320, c.querySelector(':scope > .lumen-viewport').clientWidth)", handle);
    double[] desktop = [];

    await Test($"A FitWidth chart is drawn at the width of its container, its text at its own size ({where})", async () =>
    {
        await Settle();
        desktop = await Measure();
        Check(Math.Abs(desktop[1] - desktop[0]) < 1.5, $"drawn {desktop[0]} wide and shown {desktop[1]:0.#} wide");
        Check(desktop[4] <= 0, "the chart scrolls sideways");
    });

    await Test($"A FitWidth chart redraws when its container is resized, and its text keeps its size ({where})", async () =>
    {
        var before = (await Measure())[0];
        await fitted.EvaluateAsync("c => c.parentElement.style.maxWidth = '480px'");
        await target.WaitForFunctionAsync($"c => {drawn} < {before}", handle);
        await Settle();
        var narrow = await Measure();
        Check(narrow[0] <= 480 && Math.Abs(narrow[1] - narrow[0]) < 1.5, $"in a 480-pixel container it is drawn {narrow[0]} wide and shown {narrow[1]:0.#} wide");
        Check(Math.Abs(narrow[3] - desktop[3]) < .5, $"its title is {narrow[3]:0.#} pixels high, {desktop[3]:0.#} before");
        await fitted.EvaluateAsync("c => c.parentElement.style.maxWidth = ''");
        await target.WaitForFunctionAsync($"c => {drawn} === {before}", handle);
    });

    await Test($"On a 375-pixel phone a FitWidth chart fits the screen, its text the size it is on a desktop ({where})", async () =>
    {
        await target.SetViewportSizeAsync(375, 800);
        await target.WaitForFunctionAsync($"c => {drawn} <= 375", handle);
        await Settle();
        // Anything else on the page that follows the width settles too, before the next check zooms.
        await target.WaitForTimeoutAsync(600);
        var phone = await Measure();
        Check(phone[0] <= 375 && Math.Abs(phone[1] - phone[0]) < 1.5 && phone[4] <= 0, $"drawn {phone[0]} wide, shown {phone[1]:0.#} wide, scrolling {phone[4]}");
        Check(Math.Abs(phone[3] - desktop[3]) < .5, $"its title is {phone[3]:0.#} pixels high on a phone and {desktop[3]:0.#} on a desktop");
    });

    await Test($"Zoom and the SVG and PNG exports work on a fitted chart, at the width it is drawn ({where})", async () =>
    {
        ILocator Button(string name) => fitted.Locator(".lumen-tools button", new() { HasTextString = name });
        var width = (await Measure())[0];
        var zoom = fitted.Locator(".lumen-tools button[aria-label='Zoom in']");
        if (await zoom.CountAsync() > 0)
        {
            var labels = async () => string.Join("|", await fitted.Locator(".lumen-viewport > svg text").AllTextContentsAsync());
            var before = await labels();
            await zoom.ClickAsync();
            await target.WaitForTimeoutAsync(500);
            Check(await labels() != before, "zoom did not change the axis");
            Check((await Measure())[0] == width, "zooming changed the width the chart is drawn at");
            await Button("Reset view").ClickAsync();
            await target.WaitForTimeoutAsync(500);
            Check(await labels() == before, "reset did not restore the axis");
        }
        var svg = await target.RunAndWaitForDownloadAsync(async () => await Button("Export SVG").ClickAsync());
        var exported = Regex.Match(await File.ReadAllTextAsync((await svg.PathAsync())!), "viewBox='0 0 ([0-9.]+) ").Groups[1].Value;
        Check(exported == width.ToString(CultureInfo.InvariantCulture), $"the SVG export is {exported} wide and the chart {width}");
        var png = await File.ReadAllBytesAsync((await (await target.RunAndWaitForDownloadAsync(async () => await Button("Export PNG").ClickAsync())).PathAsync())!);
        var pixels = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        Check(pixels == 2 * width, $"the PNG export is {pixels} pixels wide, not twice the chart's {width}");
    });

    await target.SetViewportSizeAsync(1400, 1000);
    try { await Settle(); } catch (TimeoutException) { }
}

var firstFitted = page.Locator(".lumen-chart.lumen-fit").First;
if (await firstFitted.CountAsync() > 0) await FitChecks(page, firstFitted, "first page");
else Console.WriteLine("SKIP FitWidth checks on the first page: this host's first page has no FitWidth chart");

// The gallery's chart explorer fits its card, so on a phone neither the explorer nor the page around it scrolls sideways. A host
// without a fitted explorer says SKIP.
if (await page.Locator("#playground .lumen-chart.lumen-fit").CountAsync() > 0)
{
    await Test("On a 375-pixel phone the home page's chart explorer fits the screen, and the page does not scroll sideways", async () =>
    {
        const string explorer = "document.querySelector('#playground .lumen-viewport > svg')";
        await page.SetViewportSizeAsync(375, 812);
        await page.WaitForFunctionAsync($"() => {{ const s = {explorer}; const w = s.getBoundingClientRect().width; return w <= 375 && Math.abs(Number(s.getAttribute('viewBox').split(' ')[2]) - w) < 1.5; }}");
        await page.WaitForTimeoutAsync(600);
        var overflow = await page.EvaluateAsync<int[]>("() => [document.documentElement.scrollWidth, ...[...document.querySelectorAll('#playground .lumen-viewport')].map(v => v.scrollWidth - v.clientWidth)]");
        Check(overflow[0] <= 375, $"the page is {overflow[0]} pixels wide");
        Check(overflow.Length == 2 && overflow[1] <= 0, $"the explorer scrolls sideways: {string.Join(", ", overflow.Skip(1))}");
        await page.SetViewportSizeAsync(1400, 1000);
        await page.WaitForFunctionAsync($"() => Number({explorer}.getAttribute('viewBox').split(' ')[2]) > 375");
    });
}
else Console.WriteLine("SKIP home explorer phone check: this host has no fitted chart explorer");

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

// A graph set to FitWidth is drawn at the width of its box, holds a dragged node in proportion when the box changes, and lets it
// go when a narrow box turns the graph top to bottom. A host whose graph does not fit its box says SKIP.
var fittedGraph = page.Locator(".lumen-chart.lumen-fit:has([data-node])").First;
if (await fittedGraph.CountAsync() > 0)
{
    var box = await fittedGraph.ElementHandleAsync();
    const string graphDrawn = "c => [...c.querySelector(':scope > .lumen-viewport > svg').getAttribute('viewBox').split(' ').slice(2).map(Number), Math.max(320, c.querySelector(':scope > .lumen-viewport').clientWidth)]";
    async Task<double[]> Drawing() => await fittedGraph.EvaluateAsync<double[]>(graphDrawn);
    async Task SettleGraph() => await page.WaitForFunctionAsync($"c => {{ const d = ({graphDrawn})(c); return d[0] === d[2]; }}", box);
    async Task<double[]> Position(string id) => (await fittedGraph.Locator($"[data-node='{id}']").GetAttributeAsync("data-position"))!.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();

    await Test("A dragged graph node keeps its place in proportion when the graph's box narrows", async () =>
    {
        await SettleGraph();
        var node = fittedGraph.Locator("[data-node]").Nth(2);
        var id = (await node.GetAttributeAsync("data-node"))!;
        await node.ScrollIntoViewIfNeededAsync();
        var circle = await node.Locator("circle").BoundingBoxAsync();
        await page.Mouse.MoveAsync(circle!.X + circle.Width / 2, circle.Y + circle.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(circle.X + circle.Width / 2 + 45, circle.Y + circle.Height / 2 + 30, new() { Steps = 10 });
        await page.Mouse.UpAsync();
        await page.WaitForFunctionAsync("c => c.querySelector('.lumen-status')?.textContent.startsWith('Moved')", box);
        var (wide, before) = (await Drawing(), await Position(id));
        // Narrowed to 900 pixels the graph still stands left to right, so only its width changes.
        await fittedGraph.EvaluateAsync("c => c.parentElement.style.maxWidth = '900px'");
        await page.WaitForFunctionAsync($"c => ({graphDrawn})(c)[0] < {wide[0]}", box);
        await SettleGraph();
        var (narrow, after) = (await Drawing(), await Position(id));
        Check(narrow[1] == wide[1], $"the graph's height changed from {wide[1]} to {narrow[1]}");
        Check(Math.Abs(after[0] - before[0] * narrow[0] / wide[0]) < .01 && Math.Abs(after[1] - before[1]) < .01,
            $"the node moved from {before[0]},{before[1]} in {wide[0]} to {after[0]},{after[1]} in {narrow[0]}");
    });

    await Test("A graph whose box turns it top to bottom lets its dragged nodes go and says so", async () =>
    {
        await fittedGraph.EvaluateAsync("c => c.parentElement.style.maxWidth = '420px'");
        await page.WaitForFunctionAsync($"c => {{ const d = ({graphDrawn})(c); return d[0] === d[2] && d[1] > d[0]; }}", box);
        await page.WaitForFunctionAsync("c => c.querySelector('.lumen-status')?.textContent.includes('top to bottom')", box);
        Check(await fittedGraph.Locator(".lumen-tools button", new() { HasTextString = "Reset layout" }).IsDisabledAsync(), "the dragged node was kept");
        Check((await fittedGraph.Locator("svg desc").First.TextContentAsync())!.Contains("top to bottom"), "the drawing does not say it runs top to bottom");
        await fittedGraph.EvaluateAsync("c => c.parentElement.style.maxWidth = ''");
        await page.WaitForFunctionAsync($"c => {{ const d = ({graphDrawn})(c); return d[0] === d[2] && d[1] < d[0]; }}", box);
    });

    // A box narrower than the narrowest a graph is drawn, 320 pixels, shows the drawing at its own size and scrolls, rather than
    // scaling it down and its text with it.
    await Test("A fitted graph wider than its box scrolls in the box at its own size", async () =>
    {
        await fittedGraph.EvaluateAsync("c => c.parentElement.style.maxWidth = '280px'");
        await page.WaitForFunctionAsync("c => c.querySelector(':scope > .lumen-viewport').clientWidth < 300", box);
        await page.WaitForFunctionAsync($"c => {{ const d = ({graphDrawn})(c); return d[0] === d[2]; }}", box);
        var shown = await fittedGraph.EvaluateAsync<double[]>(@"c => { const v = c.querySelector(':scope > .lumen-viewport'), s = v.querySelector('svg');
            return [Number(s.getAttribute('viewBox').split(' ')[2]), s.getBoundingClientRect().width, v.scrollWidth - v.clientWidth]; }");
        Check(Math.Abs(shown[1] - shown[0]) < 1, $"the {shown[0]}-pixel drawing is shown {shown[1]} pixels wide");
        Check(shown[2] > 0, "the box does not scroll");
        await fittedGraph.EvaluateAsync("c => c.parentElement.style.maxWidth = ''");
        await page.WaitForFunctionAsync($"c => {{ const d = ({graphDrawn})(c); return d[0] === d[2] && d[1] < d[0]; }}", box);
    });

    // On a phone, with its overlay scrollbars, touch and a device scale of 2, the page and its graph fit the screen in each layout the
    // page offers, no two of the graph's labels overlap, no edge label lies on a node's label and no edge runs through one.
    await Test("On a 375-pixel phone the home page does not scroll sideways with its graph in either layout, the graph's labels do not overlap and no edge runs through a node's label", async () =>
    {
        await using var phone = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 });
        var tab = await phone.NewPageAsync();
        tab.SetDefaultTimeout(15_000);
        await tab.GotoAsync(address, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
        await tab.WaitForSelectorAsync(".lumen-tooltip", new() { State = WaitForSelectorState.Attached, Timeout = 120_000 });
        var shown = tab.Locator(".lumen-chart.lumen-fit:has([data-node])").First;
        var layouts = tab.Locator("#network .segmented button");
        var names = await layouts.CountAsync() > 0 ? (await layouts.AllTextContentsAsync()).ToArray() : new[] { "" };
        foreach (var name in names)
        {
            if (name.Length > 0)
            {
                await tab.Locator("#network .segmented button", new() { HasTextString = name }).ClickAsync();
                await tab.WaitForFunctionAsync("n => document.querySelector('#network .segmented .selected')?.textContent === n", name);
            }
            await tab.WaitForFunctionAsync($"c => {{ const d = ({graphDrawn})(c); return d[0] === d[2]; }}", await shown.ElementHandleAsync());
            await tab.WaitForTimeoutAsync(400);
            var measured = await shown.EvaluateAsync<double[]>(@"c => { const v = c.querySelector(':scope > .lumen-viewport');
                const boxes = [...c.querySelectorAll('[data-node]')].map(g => g.querySelectorAll('text')[1]).concat([...v.querySelectorAll('svg > text[font-size=""10""]')]).map(t => t.getBoundingClientRect());
                let overlaps = 0;
                for (let i = 0; i < boxes.length; i++) for (let j = i + 1; j < boxes.length; j++) { const a = boxes[i], b = boxes[j]; if (a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom) overlaps++; }
                return [document.documentElement.scrollWidth, v.scrollWidth - v.clientWidth, overlaps, boxes.length]; }");
            Check(measured[0] <= 375, $"{name}: the page is {measured[0]} pixels wide");
            Check(measured[1] <= 0, $"{name}: the graph scrolls sideways by {measured[1]} pixels");
            Check(measured[2] == 0, $"{name}: {measured[2]} pairs of the graph's {measured[3]} labels overlap");
            // Each drawn edge walked a pixel at a time, mapped to the screen, against each node label's box as the browser lays it out;
            // and each edge label's box against each node label's.
            var crossed = await shown.EvaluateAsync<string[]>(@"c => { const svg = c.querySelector(':scope > .lumen-viewport > svg'), screen = svg.getScreenCTM();
                const words = [...c.querySelectorAll('[data-node]')].map(g => ({ name: g.getAttribute('aria-label'), box: g.querySelectorAll('text')[1].getBoundingClientRect() }));
                const inside = (p, b) => p.x > b.left && p.x < b.right && p.y > b.top && p.y < b.bottom;
                const found = [];
                for (const path of svg.querySelectorAll(':scope > path[stroke-width=""1.5""]')) {
                    const length = path.getTotalLength(), hit = new Set();
                    for (let at = 0; at <= length; at++) { const p = path.getPointAtLength(at).matrixTransform(screen); for (const w of words) if (inside(p, w.box)) hit.add(w.name); }
                    for (const name of hit) found.push(`an edge runs through ${name}`);
                }
                for (const t of svg.querySelectorAll(':scope > text[font-size=""10""]')) { const b = t.getBoundingClientRect();
                    for (const w of words) if (b.left < w.box.right && w.box.left < b.right && b.top < w.box.bottom && w.box.top < b.bottom) found.push(`${t.textContent} lies on ${w.name}`); }
                return found; }");
            Check(crossed.Length == 0, $"{name}: {string.Join("; ", crossed)}");
        }
    });
}
else Console.WriteLine("SKIP fitted graph checks: this host's graph does not set FitWidth");

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
    await Settled();

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

Task Sweep() => SweepOf(page);
async Task SweepOf(IPage target)
{
    var result = await target.RunAxe(new AxeRunOptions
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

// Gauges and rings belong to the chart kinds a host offers, so the suite finds them by the buttons that name them, and a host
// without them says SKIP. Each score and each ring is one mark: it takes focus with its focus ring, shows its tooltip, and
// Enter selects it, which the gallery reports under the chart.
var gaugeTab = page.GetByRole(AriaRole.Button, new() { Name = "Gauge", Exact = true });
var ringsTab = page.GetByRole(AriaRole.Button, new() { Name = "Rings", Exact = true });
if (await gaugeTab.CountAsync() > 0 && await ringsTab.CountAsync() > 0)
{
    await Test("A gauge and a ring take focus, show their tooltip and raise selection, with no zoom to offer", async () =>
    {
        foreach (var (tab, group, series, selected) in new[] { (gaugeTab, "lumen-gauge", 0, "Selected series 1, observation 1"), (ringsTab, "lumen-rings", 1, "Selected series 2, observation 1") })
        {
            await tab.First.ClickAsync();
            // The chart before it has marks of the same series and point, so the check waits for the radial drawing itself.
            var mark = chart.Locator($"g.{group} .lumen-datum[data-series='{series}'][data-point='0']");
            await mark.WaitForAsync();
            await Settled();
            Check(await chart.Locator(".lumen-tools button[aria-label='Zoom in']").CountAsync() == 0, "a radial chart offers zoom");
            var label = await mark.GetAttributeAsync("aria-label");
            await mark.FocusAsync();
            await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            Check(await tooltip.TextContentAsync() == label, $"the tooltip reads \"{await tooltip.TextContentAsync()}\", the mark \"{label}\"");
            var ring = await mark.EvaluateAsync<string>("g => getComputedStyle(g.querySelector(':scope > path[fill=none]')).strokeWidth");
            Check(ring == "3px", $"the focused mark's outline is {ring} wide");
            await page.Keyboard.PressAsync("Enter");
            await page.WaitForFunctionAsync("text => document.querySelector('.under-chart')?.textContent.includes(text)", selected);
            // The status line reads the mark as its legend does: the name the tooltip starts with, and the value.
            await page.WaitForFunctionAsync("() => document.querySelector('.lumen-status')?.textContent.trim().length > 0");
            var reported = (await status.TextContentAsync())!.Trim();
            Check(label!.StartsWith(reported), $"the status line reads \"{reported}\" for \"{label}\"");
            await page.Keyboard.PressAsync("Escape");
        }
    });
}
else Console.WriteLine("SKIP gauge and ring check: this host offers no gauge or rings");

// State timelines and range bars belong to the chart kinds a host offers too, so the suite finds them by the buttons that name
// them, and a host without them says SKIP. A span and a bar each take focus with the focus ring, show their tooltip and raise
// selection through Enter; and zooming a timeline narrows its X axis, so a span is drawn twice as wide until the view is reset.
var timelineTab = page.GetByRole(AriaRole.Button, new() { Name = "Timeline", Exact = true });
var rangeTab = page.GetByRole(AriaRole.Button, new() { Name = "Range", Exact = true });
if (await timelineTab.CountAsync() > 0 && await rangeTab.CountAsync() > 0)
{
    await Test("A hypnogram span and a range bar take focus, show their tooltip and raise selection, and zooming a timeline narrows its X", async () =>
    {
        foreach (var (tab, shape, series, point) in new[] { (timelineTab, "lumen-span", 1, 0), (rangeTab, "lumen-range", 0, 3) })
        {
            await tab.First.ClickAsync();
            // The mark is found by its own shape, so the check waits for the new kind's drawing rather than the chart before it.
            var mark = chart.Locator($".lumen-datum[data-series='{series}'][data-point='{point}']:has(rect.{shape})");
            await mark.WaitForAsync();
            await Settled();
            Check(await chart.Locator(".lumen-tools button[aria-label='Zoom in']").CountAsync() == 1, $"the {shape} chart offers no zoom");
            var label = await mark.GetAttributeAsync("aria-label");
            await mark.FocusAsync();
            await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            Check(await tooltip.TextContentAsync() == label, $"the tooltip reads \"{await tooltip.TextContentAsync()}\", the mark \"{label}\"");
            var ring = await mark.EvaluateAsync<string>("g => getComputedStyle(g.querySelector('rect')).strokeWidth");
            Check(ring == "3px", $"the focused {shape}'s outline is {ring} wide");
            await page.Keyboard.PressAsync("Enter");
            await page.WaitForFunctionAsync("text => document.querySelector('.under-chart')?.textContent.includes(text)", $"Selected series {series + 1}, observation {point + 1}");
            await page.WaitForFunctionAsync("() => document.querySelector('.lumen-status')?.textContent.includes(':')");
            await page.Keyboard.PressAsync("Escape");
        }
        await timelineTab.First.ClickAsync();
        const string middle = ".lumen-chart .lumen-datum[data-series='2'][data-point='1'] rect.lumen-span";
        await page.WaitForSelectorAsync(middle);
        await Settled();
        double Width(string value) => double.Parse(value, CultureInfo.InvariantCulture);
        var before = Width((await page.GetAttributeAsync(middle, "width"))!);
        await chart.Locator(".lumen-tools button[aria-label='Zoom in']").ClickAsync();
        await page.WaitForFunctionAsync("([s, w]) => Number(document.querySelector(s)?.getAttribute('width')) > w", new object[] { middle, before });
        var after = Width((await page.GetAttributeAsync(middle, "width"))!);
        Check(Math.Abs(after / before - 2) < .01, $"zooming in drew a span {after} wide, {before} before");
        await Tool("Reset view").ClickAsync();
        await page.WaitForFunctionAsync("([s, w]) => Number(document.querySelector(s)?.getAttribute('width')) === w", new object[] { middle, before });
    });
}
else Console.WriteLine("SKIP timeline and range check: this host offers no timeline or range bars");

// Calendars belong to the chart kinds a host offers too, so the suite finds one by the button that names it, and a host without one
// says SKIP. A day with activity takes focus with the focus ring, shows its tooltip and raises selection through Enter, which the
// status line reads as the day's date and total; a rest day is an empty cell outside every mark; and a calendar offers no zoom.
var calendarTab = page.GetByRole(AriaRole.Button, new() { Name = "Calendar", Exact = true });
if (await calendarTab.CountAsync() > 0)
{
    await Test("A calendar day takes focus, shows its tooltip and raises selection, a rest day takes none, and there is no zoom", async () =>
    {
        await calendarTab.First.ClickAsync();
        var days = chart.Locator(".lumen-datum[data-point]:has(.lumen-day)");
        await days.First.WaitForAsync();
        await Settled();
        Check(await chart.Locator(".lumen-tools button[aria-label='Zoom in']").CountAsync() == 0, "a calendar offers zoom");
        Check(await chart.Locator(".lumen-track").CountAsync() > 0 && await chart.Locator(".lumen-datum .lumen-track").CountAsync() == 0, "the rest days are not empty cells outside the marks");
        var mark = days.Nth(5);
        var label = (await mark.GetAttributeAsync("aria-label"))!;
        await mark.FocusAsync();
        await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Check(await tooltip.TextContentAsync() == label, $"the tooltip reads \"{await tooltip.TextContentAsync()}\", the day \"{label}\"");
        var ring = await mark.EvaluateAsync<string>("g => getComputedStyle(g.querySelector('.lumen-day')).strokeWidth");
        Check(ring == "3px", $"the focused day's outline is {ring} wide");
        var point = int.Parse((await mark.GetAttributeAsync("data-point"))!, CultureInfo.InvariantCulture);
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("text => document.querySelector('.under-chart')?.textContent.includes(text)", $"Selected series 1, observation {point + 1}");
        // A day's name reads "Tue 9 Jun 2026: 78, Moderate"; the status line reads its date and its total.
        await page.WaitForFunctionAsync("() => document.querySelector('.lumen-status')?.textContent.includes(' = ')");
        var reported = (await status.TextContentAsync())!.Trim();
        var (date, total) = (label.Split(": ")[0], label.Split(": ")[1].Split(',')[0]);
        Check(reported.EndsWith($": {date} = {total}"), $"the status line reads \"{reported}\" for \"{label}\"");
        await page.Keyboard.PressAsync("Escape");
    });
}
else Console.WriteLine("SKIP calendar check: this host offers no calendar");

// Blocks belong to the chart kinds a host offers too, so the suite finds them by the button that names them, and a host without them
// says SKIP. A block takes focus with the focus ring, shows its tooltip and raises selection through Enter, which the status line
// reads as the block's span and height; and zooming in halves the X axis about its middle, so a block there, which gives up half a
// hairline to each neighbour, is drawn twice as wide with its hairlines kept, until the view is reset.
var blocksTab = page.GetByRole(AriaRole.Button, new() { Name = "Blocks", Exact = true });
if (await blocksTab.CountAsync() > 0)
{
    await Test("A block takes focus, shows its tooltip and raises selection, and zooming narrows X across the blocks", async () =>
    {
        await blocksTab.First.ClickAsync();
        var mark = chart.Locator(".lumen-datum[data-series='0'][data-point='2']:has(path.lumen-block)");
        await mark.WaitForAsync();
        await Settled();
        Check(await chart.Locator(".lumen-tools button[aria-label='Zoom in']").CountAsync() == 1, "the blocks offer no zoom");
        var label = (await mark.GetAttributeAsync("aria-label"))!;
        await mark.FocusAsync();
        await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Check(await tooltip.TextContentAsync() == label, $"the tooltip reads \"{await tooltip.TextContentAsync()}\", the block \"{label}\"");
        var ring = await mark.EvaluateAsync<string>("g => getComputedStyle(g.querySelector('path.lumen-block')).strokeWidth");
        Check(ring == "3px", $"the focused block's outline is {ring} wide");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("text => document.querySelector('.under-chart')?.textContent.includes(text)", "Selected series 1, observation 3");
        await page.WaitForFunctionAsync("() => document.querySelector('.lumen-status')?.textContent.includes(' = ')");
        // A block's name reads "Interval 1: 15:00 to 23:00, 250, Lactate threshold"; the status line its series, name, span and height.
        var (name, rest) = (label.Split(": ")[0], label.Split(": ")[1].Split(", "));
        var reported = (await status.TextContentAsync())!.Trim();
        Check(reported.EndsWith($": {name}, {rest[0]} = {rest[1]}"), $"the status line reads \"{reported}\" for \"{label}\"");
        await page.Keyboard.PressAsync("Escape");
        const string middle = ".lumen-chart .lumen-datum[data-series='0'][data-point='2'] path.lumen-block";
        async Task<double> Width() => double.Parse(await page.EvaluateAsync<string>("s => String(document.querySelector(s).getBBox().width)", middle), CultureInfo.InvariantCulture);
        var before = await Width();
        await chart.Locator(".lumen-tools button[aria-label='Zoom in']").ClickAsync();
        await page.WaitForFunctionAsync("([s, w]) => document.querySelector(s)?.getBBox().width > w + 1", new object[] { middle, before });
        var after = await Width();
        Check(Math.Abs((after + 1) / (before + 1) - 2) < .01, $"zooming in drew a block {after} wide, {before} before");
        await Tool("Reset view").ClickAsync();
        await page.WaitForFunctionAsync("([s, w]) => Math.abs(document.querySelector(s)?.getBBox().width - w) < .01", new object[] { middle, before });
    });
}
else Console.WriteLine("SKIP blocks check: this host offers no blocks");
// The Sports & performance page belongs to the gallery, so the suite finds it as a visitor does, by the link that names it, and a
// host without one says SKIP. It opens in a page of its own, so it starts in the light theme and the Lumen brand.
var sportsLink = page.GetByRole(AriaRole.Link, new() { Name = "Sports & performance" });
if (await sportsLink.CountAsync() > 0)
{
    var sports = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 1000 } });
    sports.SetDefaultTimeout(15_000);
    var sportsUrl = new Uri(new Uri(address.TrimEnd('/') + "/"), await sportsLink.First.GetAttributeAsync("href"));
    await sports.GotoAsync(sportsUrl.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
    await sports.WaitForSelectorAsync(".lumen-tooltip", new() { State = WaitForSelectorState.Attached, Timeout = 120_000 });
    var charts = sports.Locator(".lumen-chart");
    // Every chart sets FitWidth, which draws it at the width it is shown once the page is interactive, so the checks wait until it has.
    const string drawnToFit = @"() => { const svgs = [...document.querySelectorAll('.lumen-chart .lumen-viewport > svg')];
        return svgs.length === 23 && svgs.every(s => Math.abs(Number(s.getAttribute('viewBox').split(' ')[2]) - s.getBoundingClientRect().width) < 1.5); }";

    await Test("The Sports & performance page renders its twenty-three charts, each live and drawn at the width it is shown", async () =>
    {
        Check(await charts.CountAsync() == 23, $"the page shows {await charts.CountAsync()} charts");
        for (var i = 0; i < 23; i++)
            Check(await charts.Nth(i).Locator(".lumen-datum[data-point]").CountAsync() > 0, $"chart {i + 1} drew no marks");
        // Each chart's script adds its tooltip, so twenty-three of them prove every chart, sparklines included, is interactive.
        await sports.WaitForFunctionAsync("() => document.querySelectorAll('.lumen-chart > .lumen-tooltip').length === 23");
        await sports.WaitForFunctionAsync(drawnToFit);
    });

    await Test("Hovering a mark on the Sports & performance page shows its tooltip", async () =>
    {
        var bars = sports.Locator("#time-in-zone .lumen-chart");
        var mark = bars.Locator(".lumen-datum[data-point]").Nth(2);
        var label = await mark.GetAttributeAsync("aria-label");
        await mark.HoverAsync();
        var tip = bars.Locator(".lumen-tooltip");
        await tip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Check(await tip.TextContentAsync() == label, $"the tooltip reads \"{await tip.TextContentAsync()}\", the mark \"{label}\"");
        await sports.Mouse.MoveAsync(1, 1);
    });

    // The Getting faster? card draws three sparklines, each at its own size with no words inside it and none of a chart's controls, every
    // personal best ringed and named so, and a mark's tooltip, as wide as its words, standing above the drawing rather than over the line.
    if (await sports.Locator("#getting-faster").CountAsync() > 0)
    {
        async Task Sparklines(IPage tab, string where)
        {
            var card = tab.Locator("#getting-faster");
            await card.ScrollIntoViewIfNeededAsync();
            var shown = await card.EvaluateAsync<double[][]>(@"c => [...c.querySelectorAll('.lumen-chart.lumen-spark > .lumen-viewport > svg')].map(s => { const b = s.getBoundingClientRect();
                return [Number(s.getAttribute('viewBox').split(' ')[2]), b.width, s.querySelectorAll('text').length, s.closest('.lumen-chart').querySelectorAll('button, .lumen-legend, .lumen-tools, .lumen-table').length]; })");
            Check(shown.Length == 3 && shown.Select(s => s[0]).SequenceEqual([120d, 120, 270]) && shown.All(s => Math.Abs(s[1] - s[0]) < .5 && s[2] == 0 && s[3] == 0), $"{where}: {string.Join(" | ", shown.Select(s => string.Join(",", s)))}");
            var bests = await card.Locator(".lumen-chart").Nth(0).Locator(".lumen-datum").EvaluateAllAsync<string[]>("ms => ms.map(m => m.getAttribute('aria-label')).filter(n => n.endsWith(' · PB'))");
            Check(bests.Length == 4 && bests.All(n => Regex.IsMatch(n, @"^5 km: \d{1,2} [A-Z][a-z]{2} · .+, \d\d:\d\d · PB$")), $"{where}: {string.Join(" | ", bests)}");
            var first = card.Locator(".lumen-chart").First;
            var mark = first.Locator(".lumen-datum[data-point='12']");
            var label = await mark.GetAttributeAsync("aria-label");
            if (where.Contains("phone")) await mark.FocusAsync(); else await mark.HoverAsync();
            var tip = first.Locator(".lumen-tooltip");
            await tip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            var placed = await first.EvaluateAsync<double[]>(@"c => { const t = c.querySelector('.lumen-tooltip').getBoundingClientRect(), s = c.querySelector('svg').getBoundingClientRect();
                return [t.left, t.right, t.bottom, s.top, t.width, t.height, document.documentElement.clientWidth]; }");
            Check(await tip.TextContentAsync() == label && label!.EndsWith(" · PB"), $"{where}: the tooltip reads {await tip.TextContentAsync()}");
            Check(placed[2] <= placed[3] && placed[4] > 120 && placed[5] < 40 && placed[0] >= 15.5 && placed[1] <= placed[6] - 15.5, $"{where}: the tooltip stands at {string.Join(", ", placed.Select(v => Math.Round(v, 1)))}");
            await tab.Mouse.MoveAsync(1, 1);
        }
        await Test("The Getting faster? card draws three sparklines at their own sizes with no words or controls in them, each best named, its tooltip above the drawing", () => Sparklines(sports, "desktop"));
        await Test("On a 375-pixel phone the Getting faster? sparklines keep their sizes and a focused best's tooltip stands above the drawing, inside the screen", async () =>
        {
            await using var phone = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 });
            var tab = await phone.NewPageAsync();
            tab.SetDefaultTimeout(15_000);
            await tab.GotoAsync(sportsUrl.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
            await tab.WaitForFunctionAsync("() => document.querySelectorAll('.lumen-chart > .lumen-tooltip').length === 23", null, new() { Timeout = 120_000 });
            await Sparklines(tab, "phone");
        });
    }
    else Console.WriteLine("SKIP sparkline checks: this host's Sports & performance page has no Getting faster? card");

    await Test("axe-core reports no WCAG A or AA violation on the Sports & performance page", () => SweepOf(sports));

    await Test("axe-core reports no WCAG A or AA violation on the Sports & performance page in the dark theme", async () =>
    {
        var before = await charts.First.Locator(".lumen-viewport > svg").GetAttributeAsync("style");
        await sports.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("theme", RegexOptions.IgnoreCase) }).First.ClickAsync();
        await sports.WaitForFunctionAsync("before => document.querySelector('.lumen-viewport > svg')?.getAttribute('style') !== before", before);
        await SweepOf(sports);
    });

    await Test("axe-core reports no WCAG A or AA violation on the Sports & performance page in the Midnight brand", async () =>
    {
        await sports.GetByRole(AriaRole.Button, new() { Name = "Midnight", Exact = true }).ClickAsync();
        await sports.WaitForFunctionAsync("() => [...document.querySelectorAll('.lumen-viewport > svg')].every(s => s.getAttribute('style')?.includes('background:#0B0E14'))");
        await SweepOf(sports);
    });

    await FitChecks(sports, sports.Locator("#stream .lumen-chart"), "Sports & performance page");

    await Test("On a 375-pixel phone the Sports & performance page and its charts fit the screen", async () =>
    {
        await sports.SetViewportSizeAsync(375, 800);
        await sports.WaitForFunctionAsync(drawnToFit);
        var overflow = await sports.EvaluateAsync<int[]>("() => [document.documentElement.scrollWidth, ...[...document.querySelectorAll('.lumen-viewport')].map(v => v.scrollWidth - v.clientWidth)]");
        Check(overflow[0] <= 375, $"the page is {overflow[0]} pixels wide");
        Check(overflow.Skip(1).All(extra => extra <= 0), $"a chart scrolls sideways: {string.Join(", ", overflow.Skip(1))}");
    });
    // On a phone, with its overlay scrollbars, touch and a device scale of 2, the race results card is drawn at the width it is shown,
    // every value label stands inside the drawing as the browser lays it out, and each race's place reads its field size and how it
    // changed from the race before, in the words its colour stands for.
    if (await sports.Locator("#race-results .lumen-chart").CountAsync() > 0)
        await Test("On a 375-pixel phone no value label of the race results runs outside its drawing, and each place reads its field and its change", async () =>
        {
            await using var phone = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 });
            var tab = await phone.NewPageAsync();
            tab.SetDefaultTimeout(15_000);
            await tab.GotoAsync(sportsUrl.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
            await tab.WaitForSelectorAsync(".lumen-tooltip", new() { State = WaitForSelectorState.Attached, Timeout = 120_000 });
            var card = tab.Locator("#race-results .lumen-chart");
            await card.ScrollIntoViewIfNeededAsync();
            await tab.WaitForFunctionAsync("c => { const v = c.querySelector(':scope > .lumen-viewport'), s = v.querySelector(':scope > svg'); return Number(s.getAttribute('viewBox').split(' ')[2]) === Math.max(320, v.clientWidth); }", await card.ElementHandleAsync());
            var measured = await card.EvaluateAsync<double[]>(@"c => { const v = c.querySelector(':scope > .lumen-viewport'), s = v.querySelector(':scope > svg'), box = s.getBoundingClientRect();
                const labels = [...s.querySelectorAll(':scope > g.lumen-value > text')].map(t => t.getBoundingClientRect());
                const outside = labels.filter(b => b.left < box.left - .5 || b.right > box.right + .5 || b.top < box.top - .5 || b.bottom > box.bottom + .5).length;
                return [Number(s.getAttribute('viewBox').split(' ')[2]), box.width, labels.length, outside, v.scrollWidth - v.clientWidth, document.documentElement.scrollWidth]; }");
            Check(measured[0] <= 375 && Math.Abs(measured[1] - measured[0]) < 1.5, $"drawn {measured[0]} wide and shown {measured[1]:0.#} wide");
            Check(measured[2] == 20 && measured[3] == 0, $"{measured[3]} of the drawing's {measured[2]} value label texts run outside it");
            Check(measured[4] <= 0 && measured[5] <= 375, $"the card scrolls by {measured[4]}, the page is {measured[5]} wide");
            var names = await card.Locator(".lumen-datum[data-series='0']").EvaluateAllAsync<string[]>("ms => ms.map(m => m.getAttribute('aria-label'))");
            Check(names.Length == 5 && names.All(n => System.Text.RegularExpressions.Regex.IsMatch(n, @"^Position: \d\d-\d\d-2026, \d+/\d+")), string.Join(" | ", names));
            Check(names.Count(n => n.EndsWith(", better than the previous")) == 3 && names.Count(n => n.EndsWith(", worse than the previous")) == 1 && !names[0].Contains("previous"), string.Join(" | ", names));
            // The tooltip a reader sees on a touch is the mark's name.
            var mark = card.Locator(".lumen-datum[data-series='0'][data-point='2']");
            await mark.FocusAsync();
            var tip = card.Locator(".lumen-tooltip");
            await tip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            Check(await tip.TextContentAsync() == await mark.GetAttributeAsync("aria-label") && (await tip.TextContentAsync())!.EndsWith("27/51, worse than the previous"), $"the tooltip reads {await tip.TextContentAsync()}");
        });
    else Console.WriteLine("SKIP race results phone check: this host's Sports & performance page has no race results");
    await sports.CloseAsync();
}
else Console.WriteLine("SKIP Sports & performance checks: this host has no Sports & performance page");

Console.WriteLine($"\n{passed} passed; {failures.Count} failed. ({address})");
foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;
