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
    // The first point of the first series is the chart's one tab stop until another is focused.
    Check(await Marks().First.GetAttributeAsync("tabindex") == "0" && await chart.Locator(".lumen-datum[tabindex='0']").CountAsync() == 1);
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

// 0.36.0: every chart is one tab stop, and the arrow keys move between its marks. The drawing itself keeps every mark at tabindex 0, so
// the script is what makes them one; the words that name the keys are the viewport's description.
const string active = "() => { const a = document.activeElement; return a?.dataset?.series + ':' + a?.dataset?.point; }";
await Test("Every chart on the page is one tab stop, its keys named in its viewport's description, and Tab leaves it after one mark", async () =>
{
    var stops = await page.EvaluateAsync<int[][]>(@"() => [...document.querySelectorAll('.lumen-chart')].filter(c => c.querySelector('.lumen-datum')).map(c => {
        const marks = [...c.querySelectorAll('.lumen-datum')], v = c.querySelector(':scope > .lumen-viewport');
        const id = v.getAttribute('aria-describedby') || v.querySelector('svg')?.getAttribute('aria-describedby');
        return [marks.filter(m => m.getAttribute('tabindex') === '0').length, marks.filter(m => m.getAttribute('tabindex') === '-1').length, marks.length,
            (document.getElementById(id)?.textContent || '').startsWith('Arrow keys') && document.getElementById(id).hidden ? 1 : 0]; })");
    Check(stops.Length > 0 && stops.All(s => s[0] == 1 && s[1] == s[2] - 1 && s[3] == 1), string.Join(" | ", stops.Select(s => string.Join(",", s))));
    await chart.Locator(".lumen-viewport").FocusAsync();
    await page.Keyboard.PressAsync("Tab");
    Check(await page.EvaluateAsync<bool>("() => !!document.activeElement?.matches('.lumen-chart .lumen-datum[tabindex=\"0\"]')"), "Tab from the viewport did not reach the chart's tab stop");
    await page.Keyboard.PressAsync("Tab");
    Check(await chart.EvaluateAsync<bool>("c => !c.querySelector(':scope > .lumen-viewport').contains(document.activeElement)"), "a second Tab stayed among the chart's marks");
    await page.Keyboard.PressAsync("Shift+Tab");
});

// The first chart's first series has no gaps, so its points are its marks in order.
await Test("The arrow keys move along a series and to the series beside it, Home and End to its ends and Page Up and Page Down ten points, the tab stop following", async () =>
{
    var last = await chart.EvaluateAsync<int>("c => Math.max(...[...c.querySelectorAll('.lumen-datum[data-series=\"0\"]')].map(m => Number(m.dataset.point)))");
    await chart.Locator(".lumen-datum[data-series='0'][data-point='0']").FocusAsync();
    (string Key, string Expected)[] moves = [("ArrowRight", "0:1"), ("ArrowRight", "0:2"), ("ArrowLeft", "0:1"), ("End", $"0:{last}"), ("Home", "0:0"), ("ArrowLeft", "0:0"),
        ("PageDown", $"0:{Math.Min(10, last)}"), ("PageUp", "0:0"), ("ArrowUp", "0:0"), ("ArrowDown", "1:0"), ("ArrowRight", "1:1"), ("ArrowUp", "0:1")];
    foreach (var (key, expected) in moves)
    {
        await page.Keyboard.PressAsync(key);
        var now = await page.EvaluateAsync<string>(active);
        Check(now == expected, $"{key} reached {now}, not {expected}");
    }
    Check(await chart.Locator(".lumen-datum[tabindex='0']").CountAsync() == 1 && await chart.Locator(".lumen-datum[data-series='0'][data-point='1']").GetAttributeAsync("tabindex") == "0", "the tab stop did not follow the focus");
    await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    Check(await tooltip.TextContentAsync() == await chart.Locator(".lumen-datum[data-series='0'][data-point='1']").GetAttributeAsync("aria-label"), "the tooltip does not read the point the keys reached");
    await page.Keyboard.PressAsync("Escape");
    await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    await chart.Locator(".lumen-datum[data-series='0'][data-point='0']").FocusAsync();
});

// A missing value has no mark, so Left and Right step over it. A host whose first chart has no gap says SKIP.
var gap = await chart.EvaluateAsync<int[]?>(@"c => { for (const s of new Set([...c.querySelectorAll('.lumen-datum[data-series]')].map(m => m.dataset.series))) {
    const points = [...c.querySelectorAll(`.lumen-datum[data-series=""${s}""]`)].map(m => Number(m.dataset.point)).sort((a, b) => a - b);
    for (let i = 1; i < points.length; i++) if (points[i] > points[i - 1] + 1) return [Number(s), points[i - 1], points[i]]; } return null; }");
if (gap is { } hole)
    await Test("Left and Right step over a missing value, which has no mark", async () =>
    {
        await chart.Locator($".lumen-datum[data-series='{hole[0]}'][data-point='{hole[1]}']").FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        Check(await page.EvaluateAsync<string>(active) == $"{hole[0]}:{hole[2]}", $"Right from {hole[0]}:{hole[1]} reached {await page.EvaluateAsync<string>(active)}");
        await page.Keyboard.PressAsync("ArrowLeft");
        Check(await page.EvaluateAsync<string>(active) == $"{hole[0]}:{hole[1]}", $"Left reached {await page.EvaluateAsync<string>(active)}");
        await page.Keyboard.PressAsync("Escape");
        await chart.Locator(".lumen-datum[data-series='0'][data-point='0']").FocusAsync();
        await page.Keyboard.PressAsync("Escape");
    });
else Console.WriteLine("SKIP gap check: this host's first chart has no missing value");

// The shared readout belongs to a chart a host chooses to set it on, so the suite finds one by the words that name its keys, and a page
// without one says SKIP. It reads every series in legend order at one X, draws a guide through every pane and a ring round each point
// there, moves by X with the keyboard, reads the same words in the status line, and hides on Escape.
const string readoutIndex = "() => [...document.querySelectorAll('.lumen-chart')].findIndex(c => (c.querySelector(':scope > .lumen-keys')?.textContent || '').startsWith('Arrow keys read'))";
async Task ReadoutChecks(IPage target, string where)
{
    var index = await target.EvaluateAsync<int>(readoutIndex);
    if (index < 0) { Console.WriteLine($"SKIP shared readout checks: no chart on the {where} sets SharedReadout"); return; }
    var card = target.Locator(".lumen-chart").Nth(index);
    var tip = card.Locator(".lumen-tooltip");
    async Task<string[]> Lines() => ((await tip.TextContentAsync()) ?? "").Split('\n');
    await card.ScrollIntoViewIfNeededAsync();
    await target.WaitForFunctionAsync("c => { const v = c.querySelector(':scope > .lumen-viewport'), s = v.querySelector(':scope > svg'); return !c.classList.contains('lumen-fit') || Number(s.getAttribute('viewBox').split(' ')[2]) === Math.max(320, v.clientWidth); }", await card.ElementHandleAsync());
    const string drawnReadout = @"c => { const s = c.querySelector(':scope > .lumen-viewport > svg'), g = s.querySelector(':scope > g.lumen-readout'), line = g?.querySelector('line');
        const clips = [...s.querySelectorAll(':scope > svg')], first = clips[0], last = clips[clips.length - 1];
        return line ? [Number(line.getAttribute('y1')), Number(line.getAttribute('y2')), Number(first.getAttribute('y')), Number(last.getAttribute('y')) + Number(last.getAttribute('height')),
            clips.length, g.querySelectorAll('circle').length, Number(line.getAttribute('x1'))] : [0, 0, 0, 0, clips.length, g ? g.querySelectorAll('circle').length : 0, 0]; }";

    await Test($"The shared readout reads every series in legend order at the X under the pointer, a guide through every pane and a ring round each point ({where})", async () =>
    {
        var box = (await card.Locator(".lumen-viewport > svg").BoundingBoxAsync())!;
        await target.Mouse.MoveAsync(box.X + box.Width * .45f, box.Y + box.Height * .5f);
        await tip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var legend = (await card.Locator(".lumen-legend button").AllTextContentsAsync()).Select(t => t.Trim()).ToArray();
        var lines = await Lines();
        Check(lines.Length == legend.Length + 1 && lines.Skip(1).Select((line, i) => line.StartsWith(legend[i] + " ")).All(b => b), $"the readout reads {string.Join(" / ", lines)} for {string.Join(", ", legend)}");
        var drawn = await card.EvaluateAsync<double[]>(drawnReadout);
        var placed = lines.Skip(1).Count(line => !line.EndsWith(" missing"));
        Check(drawn[0] >= drawn[2] && drawn[0] <= drawn[2] + 12 && drawn[1] <= drawn[3] && drawn[1] >= drawn[3] - 12, $"the guide runs from {drawn[0]} to {drawn[1]}, the plots from {drawn[2]} to {drawn[3]}");
        Check(drawn[5] == 2 * placed, $"{drawn[5]} circles for {placed} points");
        // The guide stands at the X nearest the pointer, within half the spacing of the X values.
        var pointer = await card.EvaluateAsync<double>("(c, x) => { const s = c.querySelector(':scope > .lumen-viewport > svg'); return new DOMPoint(x, 0).matrixTransform(s.getScreenCTM().inverse()).x; }", (double)(box.X + box.Width * .45f));
        var spacing = await card.EvaluateAsync<double>("c => { const xs = [...new Set([...c.querySelectorAll('.lumen-datum[data-series=\"0\"] circle')].map(e => Number(e.getAttribute('cx'))))].sort((a, b) => a - b); return Math.min(...xs.slice(1).map((x, i) => x - xs[i])); }");
        Check(Math.Abs(drawn[6] - pointer) <= spacing / 2 + .01, $"the guide stands at {drawn[6]}, the pointer at {pointer}, the spacing {spacing}");
        // The guide is the muted text colour and each ring is outlined in the text colour, so both clear 3:1 on the chart's background, and
        // the tooltip's words clear 4.5:1 on its own.
        var contrast = await card.EvaluateAsync<double[]>(@"c => {
            const rgb = s => s.match(/[\d.]+/g).slice(0, 3).map(Number), lum = s => { const [r, g, b] = rgb(s).map(v => { v /= 255; return v <= .03928 ? v / 12.92 : Math.pow((v + .055) / 1.055, 2.4); }); return .2126 * r + .7152 * g + .0722 * b; };
            const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + .05) / (Math.min(x, y) + .05); };
            const s = c.querySelector(':scope > .lumen-viewport > svg'), g = s.querySelector('g.lumen-readout'), ground = getComputedStyle(s).backgroundColor, t = getComputedStyle(c.querySelector('.lumen-tooltip'));
            return [ratio(getComputedStyle(g.querySelector('line')).stroke, ground), ratio(getComputedStyle(g.querySelector('circle')).stroke, ground), ratio(t.color, t.backgroundColor)]; }");
        Check(contrast[0] >= 3 && contrast[1] >= 3 && contrast[2] >= 4.5, $"the guide {contrast[0]:0.00}:1, a ring {contrast[1]:0.00}:1, the tooltip {contrast[2]:0.00}:1");
        await target.Mouse.MoveAsync(1, 1);
        await tip.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        Check(await card.EvaluateAsync<int>("c => c.querySelectorAll('g.lumen-readout > *').length") == 0, "the guide stayed when the pointer left");
    });

    await Test($"With a shared readout the keys step from one X to the next, Up and Down move between its series, the status line reads it, and Escape hides it ({where})", async () =>
    {
        await card.Locator(".lumen-viewport").FocusAsync();
        await target.Keyboard.PressAsync("Tab");
        await target.Keyboard.PressAsync("Home");
        await tip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var first = await Lines();
        await target.Keyboard.PressAsync("ArrowRight");
        var second = await Lines();
        Check(second[0] != first[0] && await target.EvaluateAsync<string>(active) == "0:1", $"Right read {second[0]} after {first[0]}, at {await target.EvaluateAsync<string>(active)}");
        await target.WaitForFunctionAsync("([c, text]) => c.querySelector('.lumen-status')?.textContent === text", new object[] { await card.ElementHandleAsync(), string.Join(" · ", second) });
        await target.Keyboard.PressAsync("ArrowDown");
        Check(await target.EvaluateAsync<string>(active) == "1:1" && (await Lines())[0] == second[0], $"Down reached {await target.EvaluateAsync<string>(active)}");
        await target.Keyboard.PressAsync("End");
        var end = await Lines();
        await target.Keyboard.PressAsync("PageUp");
        var back = await Lines();
        await target.Keyboard.PressAsync("Home");
        Check(end[0] != second[0] && back[0] != end[0] && (await Lines())[0] == first[0] && (await target.EvaluateAsync<string>(active)).StartsWith("1:"), $"End read {end[0]}, Page Up {back[0]}, and Home kept to the second series at {await target.EvaluateAsync<string>(active)}");
        Check(await card.EvaluateAsync<int>("c => c.querySelectorAll('g.lumen-readout line').length") == 1, "no guide while the keys read the chart");
        await target.Keyboard.PressAsync("Escape");
        await tip.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        Check(await card.EvaluateAsync<int>("c => c.querySelectorAll('g.lumen-readout > *').length") == 0, "Escape left the guide");
    });

    // At a phone's width, with touch, and at a 1280-pixel desktop: a tap or a pointer brings the readout up inside the screen, and the keys
    // still step it.
    foreach (var (width, phone) in new[] { (375, true), (1280, false) })
        await Test($"At {width} pixels{(phone ? " on a phone, a tap" : ", the pointer")} brings the readout up inside the screen, and the keys step it ({where})", async () =>
        {
            await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = phone ? 812 : 900 }, IsMobile = phone, HasTouch = phone, DeviceScaleFactor = phone ? 2 : 1 });
            var tab = await context.NewPageAsync();
            tab.SetDefaultTimeout(15_000);
            await tab.GotoAsync(target.Url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
            await tab.WaitForSelectorAsync(".lumen-tooltip", new() { State = WaitForSelectorState.Attached, Timeout = 120_000 });
            var shown = tab.Locator(".lumen-chart").Nth(await tab.EvaluateAsync<int>(readoutIndex));
            await shown.ScrollIntoViewIfNeededAsync();
            await tab.WaitForFunctionAsync("c => { const v = c.querySelector(':scope > .lumen-viewport'), s = v.querySelector(':scope > svg'); return Number(s.getAttribute('viewBox').split(' ')[2]) === Math.max(320, v.clientWidth); }", await shown.ElementHandleAsync(), new() { Timeout = 60_000 });
            await tab.WaitForTimeoutAsync(400);
            var box = (await shown.Locator(".lumen-viewport > svg").BoundingBoxAsync())!;
            if (phone) await tab.Touchscreen.TapAsync(box.X + box.Width * .7f, box.Y + box.Height * .45f);
            else await tab.Mouse.MoveAsync(box.X + box.Width * .7f, box.Y + box.Height * .45f);
            var tipped = shown.Locator(".lumen-tooltip");
            await tipped.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            var placed = await tipped.EvaluateAsync<double[]>("t => { const b = t.getBoundingClientRect(); return [b.left, b.right, document.documentElement.clientWidth, document.documentElement.scrollWidth]; }");
            Check(placed[0] >= 0 && placed[1] <= placed[2] && placed[3] <= width, $"the tooltip stands from {placed[0]:0.#} to {placed[1]:0.#} on a {placed[2]}-pixel screen, the page {placed[3]} wide");
            Check(await shown.EvaluateAsync<int>("c => c.querySelectorAll('g.lumen-readout line').length") == 1, "no guide");
            var before = ((await tipped.TextContentAsync()) ?? "").Split('\n')[0];
            await shown.Locator(".lumen-datum[tabindex='0']").FocusAsync();
            await tab.Keyboard.PressAsync("End");
            await tab.Keyboard.PressAsync("ArrowLeft");
            var after = ((await tipped.TextContentAsync()) ?? "").Split('\n')[0];
            Check(after.Length > 0 && after != before, $"the keys read {after} after {before}");
            await tab.Keyboard.PressAsync("Escape");
            await tipped.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        });
}
await ReadoutChecks(page, "first page");

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
    // The first chart's own marks are counted, since the page may show other charts.
    await legend.First.ClickAsync();
    await page.WaitForFunctionAsync($"() => document.querySelector('.lumen-chart').querySelectorAll('.lumen-datum[data-point]').length < {before}");
    Check(await legend.First.GetAttributeAsync("aria-pressed") == "false");
    await legend.First.ClickAsync();
    await page.WaitForFunctionAsync($"() => document.querySelector('.lumen-chart').querySelectorAll('.lumen-datum[data-point]').length === {before}");
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

// 0.37.0: drag to zoom. With a mouse or a pen, pressing on the plots and dragging 8 pixels or more across them draws a translucent band
// through every pane, and letting go zooms to the X it covers through the component; Escape lets a drag go without zooming, a shorter drag
// is a click, and Reset view brings the whole range back. A chart without zoom says SKIP.
async Task DragChecks(IPage target, ILocator card, string where, bool windowed = false)
{
    if (await card.Locator(".lumen-tools button[aria-label='Zoom in']").CountAsync() == 0) { Console.WriteLine($"SKIP drag checks: the {where} offers no zoom"); return; }
    async Task<string> Labels() => string.Join("|", await card.Locator(".lumen-viewport > svg > text").AllTextContentsAsync());
    // The plots on screen: the first pane's clip's top and left to the last one's bottom and right, kept 12 pixels in from each edge.
    async Task<float[]> Plots() => await card.EvaluateAsync<float[]>(@"c => { const clips = [...c.querySelectorAll(':scope > .lumen-viewport > svg > svg')],
        a = clips[0].getBoundingClientRect(), b = clips[clips.length - 1].getBoundingClientRect(); return [a.left + 12, a.top + 12, a.right - 12, b.bottom - 12]; }");
    const string band = @"c => { const r = c.querySelector('g.lumen-brush rect'), clips = [...c.querySelectorAll(':scope > .lumen-viewport > svg > svg')], last = clips[clips.length - 1];
        return r ? [Number(r.getAttribute('width')), Number(r.getAttribute('y')), Number(r.getAttribute('height')), Number(clips[0].getAttribute('y')), Number(last.getAttribute('y')) + Number(last.getAttribute('height')),
            c.querySelectorAll('g.lumen-readout > *').length, c.querySelector(':scope > .lumen-tooltip').hidden ? 1 : 0, c.querySelectorAll('g.lumen-brush line').length] : []; }";
    async Task<double[]> Band() => await card.EvaluateAsync<double[]>(band);
    var tool = card.Locator(".lumen-tools button", new() { HasTextString = "Reset view" });

    await Test($"Dragging across the plots with a mouse draws a band through every pane, hides the readout and zooms to the band; Reset view restores the whole range ({where})", async () =>
    {
        await card.ScrollIntoViewIfNeededAsync();
        await target.Mouse.MoveAsync(1, 1);
        var before = await Labels();
        var plots = await Plots();
        float y = (plots[1] + plots[3]) / 2, from = plots[0] + (plots[2] - plots[0]) * .3f, to = plots[0] + (plots[2] - plots[0]) * .6f;
        await target.Mouse.MoveAsync(from, y);
        await target.Mouse.DownAsync();
        await target.Mouse.MoveAsync(from + 20, y, new() { Steps = 4 });
        await target.Mouse.MoveAsync(to, y, new() { Steps = 8 });
        var drawn = await Band();
        Check(drawn.Length == 8 && drawn[0] > 0 && drawn[1] >= drawn[3] && drawn[1] <= drawn[3] + 12 && drawn[1] + drawn[2] <= drawn[4] && drawn[1] + drawn[2] >= drawn[4] - 12 && drawn[7] == 2,
            $"the band: {string.Join(", ", drawn)}");
        Check(drawn[5] == 0 && drawn[6] == 1, "the readout or a tooltip shows while the band is drawn");
        // The band is the text colour at a tenth, edged in the muted colour, which clears 3:1 on the background and on the band.
        var contrast = await card.EvaluateAsync<double[]>(@"c => {
            const rgb = s => s.match(/[\d.]+/g).slice(0, 3).map(Number), lum = v => { const [r, g, b] = v.map(x => { x /= 255; return x <= .03928 ? x / 12.92 : Math.pow((x + .055) / 1.055, 2.4); }); return .2126 * r + .7152 * g + .0722 * b; };
            const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + .05) / (Math.min(x, y) + .05); };
            const s = c.querySelector(':scope > .lumen-viewport > svg'), ground = rgb(getComputedStyle(s).backgroundColor), ink = rgb(getComputedStyle(s).color), edge = rgb(getComputedStyle(c.querySelector('g.lumen-brush line')).stroke);
            const tint = ground.map((v, i) => v + (ink[i] - v) * .1);
            return [ratio(edge, ground), ratio(edge, tint)]; }");
        Check(contrast[0] >= 3 && contrast[1] >= 3, $"the band's edge stands {contrast[0]:0.00}:1 on the background and {contrast[1]:0.00}:1 on the band");
        await target.Mouse.UpAsync();
        await target.WaitForFunctionAsync("([c, before]) => [...c.querySelectorAll(':scope > .lumen-viewport > svg > text')].map(t => t.textContent).join('|') !== before && !c.querySelector('g.lumen-brush')",
            new object[] { await card.ElementHandleAsync(), before });
        // Every pane draws the zoomed stretch alone.
        var spans = await card.EvaluateAsync<int[][]>("c => { const by = new Map(); for (const m of c.querySelectorAll('.lumen-datum[data-point]')) { const s = m.dataset.series; if (!by.has(s)) by.set(s, []); by.get(s).push(Number(m.dataset.point)); } return [...by.values()].map(v => [Math.min(...v), Math.max(...v)]); }");
        // A long line is thinned over the window shown, so each of its panes draws the zoomed stretch alone.
        if (windowed) Check(spans.Length > 0 && spans.All(span => span[0] > 0), $"a pane still draws its first point: {string.Join(" | ", spans.Select(span => string.Join("-", span)))}");
        await tool.ClickAsync();
        await target.WaitForFunctionAsync("([c, before]) => [...c.querySelectorAll(':scope > .lumen-viewport > svg > text')].map(t => t.textContent).join('|') === before", new object[] { await card.ElementHandleAsync(), before });
    });

    await Test($"Escape during a drag lets it go without zooming, and a drag shorter than 8 pixels is a click ({where})", async () =>
    {
        await target.Mouse.MoveAsync(1, 1);
        var before = await Labels();
        var plots = await Plots();
        float y = (plots[1] + plots[3]) / 2, from = plots[0] + (plots[2] - plots[0]) * .4f;
        await target.Mouse.MoveAsync(from, y);
        await target.Mouse.DownAsync();
        await target.Mouse.MoveAsync(from + 60, y, new() { Steps = 6 });
        Check((await Band()).Length == 8, "no band");
        await target.Keyboard.PressAsync("Escape");
        Check((await Band()).Length == 0, "Escape left the band");
        await target.Mouse.UpAsync();
        await target.WaitForTimeoutAsync(700);
        Check(await Labels() == before, "Escape zoomed");
        // A mark pressed and moved 3 pixels is selected, as a click selects it, and nothing zooms.
        var mark = card.Locator(".lumen-datum[data-point]").Nth(3);
        var box = (await mark.BoundingBoxAsync())!;
        var shown = card.Locator(".lumen-status");
        var was = await shown.TextContentAsync();
        await target.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await target.Mouse.DownAsync();
        await target.Mouse.MoveAsync(box.X + box.Width / 2 + 3, box.Y + box.Height / 2, new() { Steps = 2 });
        Check((await Band()).Length == 0, "a 3-pixel drag drew a band");
        await target.Mouse.UpAsync();
        await target.WaitForFunctionAsync("([c, was]) => { const t = c.querySelector('.lumen-status')?.textContent || ''; return t !== was && (t.includes(':') || t.includes(' · ')); }", new object[] { await card.ElementHandleAsync(), was ?? "" });
        await target.WaitForTimeoutAsync(500);
        Check(await Labels() == before, "a click zoomed");
        await target.Mouse.MoveAsync(1, 1);
    });
}
await DragChecks(page, chart, "first page's first chart");

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
// 0.39.0: a strip draws its parts as one bar, with a key of whole percentages inside its drawing and no legend of toggle buttons. The suite
// finds one by the button that names its kind, or else as a chart that draws a strip's key, and a host without one says SKIP.
async Task<ILocator> StripOn(IPage tab)
{
    var button = tab.GetByRole(AriaRole.Button, new() { Name = "Strip", Exact = true });
    if (await button.CountAsync() > 0) await button.First.ClickAsync();
    var found = tab.Locator(".lumen-chart:has(g.lumen-strip-key)").First;
    await found.WaitForAsync();
    return found;
}
if (await page.GetByRole(AriaRole.Button, new() { Name = "Strip", Exact = true }).CountAsync() > 0 || await page.Locator(".lumen-chart:has(g.lumen-strip-key)").CountAsync() > 0)
{
    await Test("A strip draws no legend of toggle buttons, its key's percentages add up to 100, and the arrow keys step from part to part, Enter reading each in the status line", async () =>
    {
        var strip = await StripOn(page);
        await strip.ScrollIntoViewIfNeededAsync();
        await page.WaitForFunctionAsync("c => { const v = c.querySelector(':scope > .lumen-viewport'), s = v.querySelector(':scope > svg'); return !c.classList.contains('lumen-fit') || Number(s.getAttribute('viewBox').split(' ')[2]) === Math.max(320, v.clientWidth); }", await strip.ElementHandleAsync());
        var keys = await strip.Locator("g.lumen-strip-key > text").AllTextContentsAsync();
        var shares = keys.Select(k => int.Parse(Regex.Match(k, @"(\d+)%$").Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
        Check(await strip.Locator(".lumen-legend").CountAsync() == 0 && keys.Count >= 2 && shares.Sum() == 100, string.Join(" | ", keys));
        var marks = strip.Locator(".lumen-datum[data-point]");
        await marks.First.FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        var second = await marks.Nth(1).GetAttributeAsync("aria-label");
        await page.WaitForFunctionAsync("n => document.activeElement?.getAttribute('aria-label') === n", second);
        Check(Regex.IsMatch(second!, @"^.+: \d+%, .+$"), second!);
        await page.Keyboard.PressAsync("Enter");
        var line = strip.Locator(".lumen-status");
        await page.WaitForFunctionAsync("([s, t]) => s.textContent === t", new object[] { await line.ElementHandleAsync(), second! });
        await page.Keyboard.PressAsync("Escape");
        await SweepOf(page);
    });
    foreach (var (width, phone) in new[] { (375, true), (1280, false) })
        await Test($"At {width} pixels{(phone ? ", on a phone," : "")} a strip's key stands whole inside its drawing, its words in the text colour, and the drawing as tall as its content", async () =>
        {
            await using var context = await browser.NewContextAsync(phone ? new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 }
                : new() { ViewportSize = new() { Width = width, Height = 900 } });
            var other = await context.NewPageAsync();
            other.SetDefaultTimeout(15_000);
            await other.GotoAsync(address, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
            await other.WaitForSelectorAsync(".lumen-tooltip", new() { State = WaitForSelectorState.Attached, Timeout = 120_000 });
            var strip = await StripOn(other);
            await strip.ScrollIntoViewIfNeededAsync();
            await other.WaitForFunctionAsync("c => { const v = c.querySelector(':scope > .lumen-viewport'), s = v.querySelector(':scope > svg'); return Number(s.getAttribute('viewBox').split(' ')[2]) === Math.max(320, v.clientWidth); }", await strip.ElementHandleAsync());
            var measured = await strip.EvaluateAsync<double[]>(@"c => { const s = c.querySelector(':scope > .lumen-viewport > svg'), box = s.getBoundingClientRect();
                const keys = [...s.querySelectorAll('g.lumen-strip-key > text')], boxes = keys.map(t => t.getBoundingClientRect());
                const outside = boxes.filter(b => b.width === 0 || b.left < box.left - .5 || b.right > box.right + .5 || b.top < box.top - .5 || b.bottom > box.bottom + .5).length;
                const lowest = Math.max(...boxes.map(b => b.bottom));
                return [keys.length, outside, box.bottom - lowest, document.documentElement.scrollWidth, getComputedStyle(keys[0]).fill === getComputedStyle(s).color ? 1 : 0]; }");
            // Under the key stand at most the room the drawing keeps and a source line of two lines.
            Check(measured[0] >= 2 && measured[1] == 0 && measured[2] >= 0 && measured[2] < 60 && measured[3] <= width && measured[4] == 1, string.Join(", ", measured));
        });
}
else Console.WriteLine("SKIP strip checks: this host offers no strip");
// 0.38.0: a chart drawn without its legend or toolbar, as a phone card is, keeps its status line out of sight and reading; its lines are
// named at their ends, each label inside the drawing on a phone and on a desktop; and its shared readout reads every line. The checks
// find such a chart by the class its toolbar takes, so they run on any host that draws one.
async Task QuietChecks(IPage tab, ILocator quiet, string url, string where)
{
    await Test($"{where}: a chart without its legend and toolbar draws neither, and its status line, out of sight, reads the readout the arrow keys move", async () =>
    {
        await quiet.ScrollIntoViewIfNeededAsync();
        var shape = await quiet.EvaluateAsync<double[]>(@"c => { const s = c.querySelector('.lumen-status'), b = s.getBoundingClientRect();
            return [c.querySelectorAll('.lumen-legend').length, c.querySelectorAll('button').length, c.querySelectorAll('.lumen-status[role=status]').length, b.width, b.height,
                c.querySelectorAll('.lumen-viewport g.lumen-end').length]; }");
        Check(shape[0] == 0 && shape[1] == 0 && shape[2] == 1 && shape[3] <= 1 && shape[4] <= 1 && shape[5] >= 5, string.Join(", ", shape));
        var status = quiet.Locator(".lumen-status");
        var marks = quiet.Locator(".lumen-datum[data-point]");
        await marks.First.FocusAsync();
        await tab.WaitForFunctionAsync("s => s.textContent.length > 0", await status.ElementHandleAsync());
        var before = await status.TextContentAsync();
        await tab.Keyboard.PressAsync("ArrowRight");
        await tab.WaitForFunctionAsync("([s, b]) => s.textContent !== b && s.textContent.length > 0", new object[] { await status.ElementHandleAsync(), before! });
        var after = (await status.TextContentAsync())!;
        Check(after.Contains(" · You ") && after.Split(" · ").Skip(1).All(e => e.EndsWith('s')), $"the status line reads \"{after}\"");
        await tab.Keyboard.PressAsync("Escape");
    });
    await Test($"{where}: the shared readout reads every line, each with its unit, in legend order", async () =>
    {
        var box = (await quiet.Locator(".lumen-viewport > svg").BoundingBoxAsync())!;
        await tab.Mouse.MoveAsync(box.X + box.Width * .5f, box.Y + box.Height * .5f);
        var tip = quiet.Locator(".lumen-tooltip");
        await tip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var lines = ((await tip.TextContentAsync()) ?? "").Split('\n');
        var series = await quiet.EvaluateAsync<int>("c => new Set([...c.querySelectorAll('.lumen-datum[data-series]')].map(m => m.dataset.series)).size");
        Check(lines.Length == series + 1 && lines[^1].StartsWith("You ") && lines.Skip(1).All(l => l.EndsWith('s')), string.Join(" / ", lines));
        await tab.Mouse.MoveAsync(1, 1);
    });
    foreach (var (width, phone) in new[] { (375, true), (1280, false) })
        await Test($"{where}: at {width} pixels{(phone ? ", on a phone," : "")} every end label stands whole inside the drawing, none overlapping another", async () =>
        {
            await using var context = await browser.NewContextAsync(phone ? new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 }
                : new() { ViewportSize = new() { Width = width, Height = 900 } });
            var other = await context.NewPageAsync();
            other.SetDefaultTimeout(15_000);
            await other.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
            await other.WaitForSelectorAsync(".lumen-tooltip", new() { State = WaitForSelectorState.Attached, Timeout = 120_000 });
            var card = other.Locator(".lumen-chart:has(.lumen-quiet)").First;
            await card.ScrollIntoViewIfNeededAsync();
            await other.WaitForFunctionAsync("c => { const v = c.querySelector(':scope > .lumen-viewport'), s = v.querySelector(':scope > svg'); return Number(s.getAttribute('viewBox').split(' ')[2]) === Math.max(320, v.clientWidth); }", await card.ElementHandleAsync());
            var measured = await card.EvaluateAsync<double[]>(@"c => { const s = c.querySelector(':scope > .lumen-viewport > svg'), box = s.getBoundingClientRect();
                const labels = [...s.querySelectorAll('g.lumen-end > text:last-child')].map(t => t.getBoundingClientRect());
                const outside = labels.filter(b => b.left < box.left - .5 || b.right > box.right + .5 || b.top < box.top - .5 || b.bottom > box.bottom + .5).length;
                /* A text's box is its font's whole line, a third taller than its letters: boxes 14 apart meet by a pixel or two while the letters stand clear. */ let overlaps = 0;
                for (let i = 0; i < labels.length; i++) for (let j = i + 1; j < labels.length; j++) { const a = labels[i], b = labels[j];
                    if (a.left < b.right && b.left < a.right && a.top < b.bottom - 3 && b.top < a.bottom - 3) overlaps++; }
                const cut = [...s.querySelectorAll('g.lumen-end > text:last-child')].filter(t => t.textContent.includes('…')).length;
                return [labels.length, outside, overlaps, cut, box.width, document.documentElement.scrollWidth]; }");
            Check(measured[0] >= 5 && measured[1] == 0 && measured[2] == 0 && measured[3] == 0 && measured[5] <= width, string.Join(", ", measured));
        });
}
var quietHere = page.Locator(".lumen-chart:has(.lumen-quiet)");
if (await quietHere.CountAsync() > 0) await QuietChecks(page, quietHere.First, address, "This host");
else Console.WriteLine("SKIP quiet chart checks: this host's first page draws no chart without its toolbar");
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
        return svgs.length === 31 && svgs.every(s => Math.abs(Number(s.getAttribute('viewBox').split(' ')[2]) - s.getBoundingClientRect().width) < 1.5); }";

    await Test("The Sports & performance page renders its thirty-one charts, each live and drawn at the width it is shown", async () =>
    {
        Check(await charts.CountAsync() == 31, $"the page shows {await charts.CountAsync()} charts");
        for (var i = 0; i < 31; i++)
            Check(await charts.Nth(i).Locator(".lumen-datum[data-point]").CountAsync() > 0, $"chart {i + 1} drew no marks");
        // Each chart's script adds its tooltip, so thirty-one of them prove every chart, sparklines included, is interactive.
        await sports.WaitForFunctionAsync("() => document.querySelectorAll('.lumen-chart > .lumen-tooltip').length === 31");
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
            await tab.WaitForFunctionAsync("() => document.querySelectorAll('.lumen-chart > .lumen-tooltip').length === 31", null, new() { Timeout = 120_000 });
            await Sparklines(tab, "phone");
        });
    }
    else Console.WriteLine("SKIP sparkline checks: this host's Sports & performance page has no Getting faster? card");

    await ReadoutChecks(sports, "Sports & performance page");

    if (await sports.Locator("#gap .lumen-chart:has(.lumen-quiet)").CountAsync() > 0)
        await QuietChecks(sports, sports.Locator("#gap .lumen-chart"), sportsUrl.ToString(), "Gap to the leader");
    else Console.WriteLine("SKIP Gap to the leader checks: this host's Sports & performance page has no such card");

    // 0.39.0: the latest session's time in zone as a strip of shares and its scores on tracks, each word inside its drawing on a phone and on
    // a desktop; the scores' card writes no title, which stays the drawing's name.
    if (await sports.Locator("#zone-strip .lumen-chart").CountAsync() > 0 && await sports.Locator("#scores .lumen-chart").CountAsync() > 0)
        foreach (var (width, phone) in new[] { (375, true), (1280, false) })
            await Test($"Time in zone, as shares, and Session scores at {width} pixels{(phone ? ", on a phone" : "")}: every percentage and score stands inside its drawing, the percentages adding up to 100", async () =>
            {
                await using var context = await browser.NewContextAsync(phone ? new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 }
                    : new() { ViewportSize = new() { Width = width, Height = 900 } });
                var tab = await context.NewPageAsync();
                tab.SetDefaultTimeout(15_000);
                await tab.GotoAsync(sportsUrl.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
                await tab.WaitForFunctionAsync("() => document.querySelectorAll('.lumen-chart > .lumen-tooltip').length === 31", null, new() { Timeout = 120_000 });
                await tab.WaitForFunctionAsync(drawnToFit, null, new() { Timeout = 60_000 });
                const string inside = @"(s, texts) => { const box = s.getBoundingClientRect();
                    return texts.filter(t => { const b = t.getBoundingClientRect(); return b.width === 0 || b.left < box.left - .5 || b.right > box.right + .5 || b.top < box.top - .5 || b.bottom > box.bottom + .5; }).length; }";
                var strip = tab.Locator("#zone-strip .lumen-chart");
                await strip.ScrollIntoViewIfNeededAsync();
                var keys = await strip.Locator("g.lumen-strip-key > text").AllTextContentsAsync();
                var outsideKeys = await strip.EvaluateAsync<int>($"c => ({inside})(c.querySelector('.lumen-viewport > svg'), [...c.querySelectorAll('g.lumen-strip-key > text')])");
                Check(keys.Count == 5 && keys.Sum(k => int.Parse(Regex.Match(k, @"(\d+)%$").Groups[1].Value, CultureInfo.InvariantCulture)) == 100 && outsideKeys == 0
                    && await strip.Locator(".lumen-legend").CountAsync() == 0 && await strip.Locator(".lumen-datum[data-point]").CountAsync() >= 3, $"{string.Join(" | ", keys)}; {outsideKeys} outside");
                var scores = tab.Locator("#scores .lumen-chart");
                await scores.ScrollIntoViewIfNeededAsync();
                var measured = await scores.EvaluateAsync<double[]>($@"c => {{ const s = c.querySelector('.lumen-viewport > svg'), texts = [...s.querySelectorAll('text')];
                    return [s.querySelectorAll('.lumen-bar-track').length, ({inside})(s, texts.filter(t => t.textContent.length > 0)), s.querySelectorAll('text[font-size=""17""]').length,
                        s.getAttribute('aria-label').startsWith('Session scores: ') ? 1 : 0, c.querySelectorAll('.lumen-legend').length, document.documentElement.scrollWidth]; }}");
                Check(measured[0] == 3 && measured[1] == 0 && measured[2] == 0 && measured[3] == 1 && measured[4] == 0 && measured[5] <= width, string.Join(", ", measured));
            });
    else Console.WriteLine("SKIP time in zone and session scores checks: this host's Sports & performance page has no such cards");

    // 0.40.0: the laps' heart rates as columns filled by value and the best efforts with their watts per kilogram, each lap's or effort's name
    // and its sub-label inside its drawing on a phone and on a desktop, the sub-labels clearing 4.5:1 and every column 3:1 at its fill's ends,
    // and a mark chosen with the keys reading its sub-label in the status line.
    if (await sports.Locator("#lap-heart .lumen-chart").CountAsync() > 0 && await sports.Locator("#best-efforts .lumen-chart").CountAsync() > 0)
    {
        foreach (var (width, phone) in new[] { (375, true), (1280, false) })
            await Test($"Heart rate by lap and Best efforts at {width} pixels{(phone ? ", on a phone" : "")}: every name and sub-label stands inside its drawing, the columns filled by one gradient or one colour", async () =>
            {
                await using var context = await browser.NewContextAsync(phone ? new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 }
                    : new() { ViewportSize = new() { Width = width, Height = 900 } });
                var tab = await context.NewPageAsync();
                tab.SetDefaultTimeout(15_000);
                await tab.GotoAsync(sportsUrl.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
                await tab.WaitForFunctionAsync("() => document.querySelectorAll('.lumen-chart > .lumen-tooltip').length === 31", null, new() { Timeout = 120_000 });
                await tab.WaitForFunctionAsync(drawnToFit, null, new() { Timeout = 60_000 });
                foreach (var (id, count) in new[] { ("#lap-heart", 4), ("#best-efforts", 5) })
                {
                    var card = tab.Locator($"{id} .lumen-chart");
                    await card.ScrollIntoViewIfNeededAsync();
                    var measured = await card.EvaluateAsync<double[]>(@"c => {
                        const rgb = s => s.match(/[\d.]+/g).slice(0, 3).map(Number), lum = v => { const [r, g, b] = v.map(x => { x /= 255; return x <= .03928 ? x / 12.92 : Math.pow((x + .055) / 1.055, 2.4); }); return .2126 * r + .7152 * g + .0722 * b; };
                        const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + .05) / (Math.min(x, y) + .05); };
                        const hex = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
                        const s = c.querySelector(':scope > .lumen-viewport > svg'), box = s.getBoundingClientRect(), ground = rgb(getComputedStyle(s).backgroundColor);
                        const under = [...s.querySelectorAll(':scope > text[text-anchor=""middle""].lumen-muted')].filter(t => t.textContent.length > 0 && Number(t.getAttribute('y')) > 0);
                        const subs = under.filter(t => t.getAttribute('font-size') === '11');
                        const outside = under.filter(t => { const b = t.getBoundingClientRect(); return b.width === 0 || b.left < box.left - .5 || b.right > box.right + .5 || b.top < box.top - .5 || b.bottom > box.bottom + .5; }).length;
                        const fills = [...s.querySelectorAll('.lumen-datum[data-point] > rect, .lumen-datum[data-point] > path')].map(r => r.getAttribute('fill'));
                        const stops = [...s.querySelectorAll('linearGradient stop')].map(t => hex(t.getAttribute('stop-color')));
                        const fill = getComputedStyle(s.querySelector('.lumen-datum[data-point] > rect, .lumen-datum[data-point] > path')).fill;
                        const inks = stops.length ? stops : [rgb(fill)];
                        return [subs.length, outside, Math.min(...subs.map(t => ratio(rgb(getComputedStyle(t).fill), ground))), new Set(fills).size, fills[0]?.startsWith('url(') ? 1 : 0,
                            Math.min(...inks.map(i => ratio(i, ground))), document.documentElement.scrollWidth, Number(s.getAttribute('viewBox').split(' ')[2]), box.width]; }");
                    var gradient = id == "#lap-heart";
                    Check(measured[0] == count && measured[1] == 0 && measured[2] >= 4.5 && measured[3] == 1 && measured[4] == (gradient ? 1 : 0) && measured[5] >= 3 && measured[6] <= width
                        && Math.Abs(measured[7] - measured[8]) < 1.5, $"{id}: {string.Join(", ", measured.Select(m => m.ToString("0.##", CultureInfo.InvariantCulture)))}");
                }
            });
        await Test("Heart rate by lap: a lap chosen with the keys reads its pace after its name in the status line, as its mark's name does", async () =>
        {
            var card = sports.Locator("#lap-heart .lumen-chart");
            await card.ScrollIntoViewIfNeededAsync();
            var marks = card.Locator(".lumen-datum[data-point]");
            await marks.First.FocusAsync();
            await sports.Keyboard.PressAsync("ArrowRight");
            var second = (await marks.Nth(1).GetAttributeAsync("aria-label"))!;
            await sports.WaitForFunctionAsync("n => document.activeElement?.getAttribute('aria-label') === n", second);
            await sports.Keyboard.PressAsync("Enter");
            var line = card.Locator(".lumen-status");
            await sports.WaitForFunctionAsync("s => s.textContent.includes(' · ')", await line.ElementHandleAsync());
            var read = (await line.TextContentAsync())!;
            Check(Regex.IsMatch(second, @"^Heart rate: Lap 2 · \d+:\d\d, \d+$") && read.StartsWith(second[..second.LastIndexOf(',')] + " = "), $"the mark \"{second}\", the status line \"{read}\"");
            await sports.Keyboard.PressAsync("Escape");
        });
    }
    else Console.WriteLine("SKIP heart rate by lap and best efforts checks: this host's Sports & performance page has no such cards");

    // 0.37.0: the Ride channels card reads six channels at once, zooms by a drag through all six panes, takes a tap on a phone as the
    // readout, never a band, and keeps every word inside its drawing there.
    if (await sports.Locator("#ride-channels .lumen-chart").CountAsync() > 0)
    {
        var ride = sports.Locator("#ride-channels .lumen-chart");
        await Test("Ride channels: the shared readout reads all six channels in legend order, saying once that each is an average of a 12-second slice", async () =>
        {
            await ride.ScrollIntoViewIfNeededAsync();
            await sports.WaitForFunctionAsync(drawnToFit);
            var box = (await ride.Locator(".lumen-viewport > svg").BoundingBoxAsync())!;
            await sports.Mouse.MoveAsync(box.X + box.Width * .45f, box.Y + box.Height * .5f);
            var tip = ride.Locator(".lumen-tooltip");
            await tip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            var lines = ((await tip.TextContentAsync()) ?? "").Split('\n');
            var legend = (await ride.Locator(".lumen-legend button").AllTextContentsAsync()).Select(t => t.Trim()).ToArray();
            Check(legend.Length == 6 && lines.Length == 7 && lines[0].EndsWith(" · average of 12 s") && lines.Skip(1).Select((line, i) => line.StartsWith(legend[i] + " ") && !line.Contains("average")).All(b => b), string.Join(" / ", lines));
            // Every header stands whole, none cut with an ellipsis.
            Check(await ride.EvaluateAsync<int>("c => [...c.querySelectorAll('text.lumen-pane-title')].filter(t => t.textContent.endsWith('…')).length") == 0, "a header was cut");
            Check(await ride.EvaluateAsync<int>("c => c.querySelectorAll('g.lumen-readout circle').length") == 12 && await ride.Locator(".lumen-datum[data-point]").CountAsync() <= 6 * 600, "the rings or the marks");
            await sports.Mouse.MoveAsync(1, 1);
        });
        await DragChecks(sports, ride, "Ride channels card", windowed: true);
        await Test("Ride channels on a 375-pixel phone: a tap shows the readout and draws no band, and every header and word stands inside the drawing", async () =>
        {
            await using var phone = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 });
            var tab = await phone.NewPageAsync();
            tab.SetDefaultTimeout(15_000);
            await tab.GotoAsync(sportsUrl.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
            await tab.WaitForFunctionAsync("() => document.querySelectorAll('.lumen-chart > .lumen-tooltip').length === 31", null, new() { Timeout = 120_000 });
            await tab.WaitForFunctionAsync(drawnToFit, null, new() { Timeout = 60_000 });
            var card = tab.Locator("#ride-channels .lumen-chart");
            await card.ScrollIntoViewIfNeededAsync();
            await tab.WaitForTimeoutAsync(400);
            var box = (await card.Locator(".lumen-viewport > svg").BoundingBoxAsync())!;
            await tab.Touchscreen.TapAsync(box.X + box.Width * .6f, box.Y + box.Height * .5f);
            var tip = card.Locator(".lumen-tooltip");
            await tip.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            Check(((await tip.TextContentAsync()) ?? "").Split('\n').Length == 7 && await card.EvaluateAsync<int>("c => c.querySelectorAll('g.lumen-brush').length") == 0, "the tap");
            var measured = await card.EvaluateAsync<double[]>(@"c => { const s = c.querySelector(':scope > .lumen-viewport > svg'), box = s.getBoundingClientRect(), texts = [...s.querySelectorAll('text')];
                const outside = texts.filter(t => { const b = t.getBoundingClientRect(); return b.width > 0 && (b.left < box.left - .5 || b.right > box.right + .5 || b.top < box.top - .5 || b.bottom > box.bottom + .5); }).length;
                return [s.querySelectorAll('text.lumen-pane-title').length, outside, texts.length, Number(s.getAttribute('viewBox').split(' ')[2]), box.width, document.documentElement.scrollWidth]; }");
            Check(measured[0] == 6 && measured[1] == 0 && measured[3] <= 375 && Math.Abs(measured[4] - measured[3]) < 1.5 && measured[5] <= 375, string.Join(", ", measured));
            Check(await card.EvaluateAsync<int>("c => [...c.querySelectorAll('text.lumen-pane-title')].filter(t => t.textContent.endsWith('…')).length") == 0, "a header was cut on the phone");
        });
    }
    else Console.WriteLine("SKIP Ride channels checks: this host's Sports & performance page has no Ride channels card");

    // Each sweep runs with the performance chart's shared readout showing, its guide, rings and tooltip included.
    async Task Hovered(IPage target)
    {
        var shown = target.Locator("#performance .lumen-chart");
        if (await shown.CountAsync() == 0) return;
        await shown.ScrollIntoViewIfNeededAsync();
        var box = (await shown.Locator(".lumen-viewport > svg").BoundingBoxAsync())!;
        await target.Mouse.MoveAsync(box.X + box.Width * .5f, box.Y + box.Height * .5f);
        await shown.Locator(".lumen-tooltip").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60000 });
    }
    await Test("axe-core reports no WCAG A or AA violation on the Sports & performance page", async () => { await Hovered(sports); await SweepOf(sports); });

    await Test("axe-core reports no WCAG A or AA violation on the Sports & performance page in the dark theme", async () =>
    {
        var before = await charts.First.Locator(".lumen-viewport > svg").GetAttributeAsync("style");
        await sports.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("theme", RegexOptions.IgnoreCase) }).First.ClickAsync();
        await sports.WaitForFunctionAsync("before => document.querySelector('.lumen-viewport > svg')?.getAttribute('style') !== before", before);
        await Hovered(sports);
        await SweepOf(sports);
    });

    await Test("axe-core reports no WCAG A or AA violation on the Sports & performance page in the Midnight brand", async () =>
    {
        await sports.GetByRole(AriaRole.Button, new() { Name = "Midnight", Exact = true }).ClickAsync();
        // Every chart on the page is redrawn on the server, the ride's six channels among them, which takes longer than the default
        // wait on a slow runner.
        await sports.WaitForFunctionAsync("() => [...document.querySelectorAll('.lumen-viewport > svg')].every(s => s.getAttribute('style')?.includes('background:#0B0E14'))", null, new() { Timeout = 60000 });
        await Hovered(sports);
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
    // On the same phone every word of How the field finished stands inside its drawing as the browser lays it out, and so does every
    // chart's title, description and source, which are written from the left: a description or a source too wide for its card goes on
    // over a second line, and a title too wide is cut, rather than running past the drawing's edge.
    if (await sports.Locator("#field .lumen-chart").CountAsync() > 0)
        await Test("On a 375-pixel phone no word of How the field finished, and no chart's title, description or source on the Sports & performance page, runs outside its drawing", async () =>
        {
            await using var phone = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 375, Height = 812 }, IsMobile = true, HasTouch = true, DeviceScaleFactor = 2 });
            var tab = await phone.NewPageAsync();
            tab.SetDefaultTimeout(15_000);
            await tab.GotoAsync(sportsUrl.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
            await tab.WaitForFunctionAsync("() => document.querySelectorAll('.lumen-chart > .lumen-tooltip').length === 31", null, new() { Timeout = 120_000 });
            await tab.WaitForFunctionAsync(drawnToFit, null, new() { Timeout = 60_000 });
            var card = tab.Locator("#field .lumen-chart");
            await card.ScrollIntoViewIfNeededAsync();
            var measured = await tab.EvaluateAsync<double[]>(@"() => {
                const outside = (s, texts) => { const box = s.getBoundingClientRect();
                    return texts.filter(t => { const b = t.getBoundingClientRect(); return b.width > 0 && (b.left < box.left - .5 || b.right > box.right + .5 || b.top < box.top - .5 || b.bottom > box.bottom + .5); }).length; };
                const field = document.querySelector('#field .lumen-viewport > svg');
                const charts = [...document.querySelectorAll('.lumen-chart:not(.lumen-spark) .lumen-viewport > svg')];
                const written = charts.map(s => [...s.querySelectorAll(':scope > text[x=""24""]')]);
                return [field.querySelectorAll('text').length, outside(field, [...field.querySelectorAll('text')]), field.querySelectorAll(':scope > text[x=""24""]').length,
                    charts.length, written.reduce((n, texts) => n + texts.length, 0), charts.reduce((n, s, i) => n + outside(s, written[i]), 0),
                    Number(field.getAttribute('viewBox').split(' ')[2]), field.getBoundingClientRect().width, document.documentElement.scrollWidth]; }");
            Check(measured[0] > 10 && measured[1] == 0, $"{measured[1]} of the card's {measured[0]} texts run outside its drawing");
            // Its title, its description and its source on two lines, the card drawn at the width it is shown and the page not scrolling sideways.
            Check(measured[2] == 4 && measured[6] <= 375 && Math.Abs(measured[7] - measured[6]) < 1.5 && measured[8] <= 375, $"{measured[2]} lines written from the left, drawn {measured[6]} wide and shown {measured[7]:0.#}, the page {measured[8]} wide");
            Check(measured[3] == 28 && measured[4] >= 82 && measured[5] == 0, $"{measured[5]} of the {measured[4]} titles, descriptions and sources of {measured[3]} charts run outside their drawings");
        });
    else Console.WriteLine("SKIP field phone check: this host's Sports & performance page has no How the field finished");
    await sports.CloseAsync();
}
else Console.WriteLine("SKIP Sports & performance checks: this host has no Sports & performance page");

Console.WriteLine($"\n{passed} passed; {failures.Count} failed. ({address})");
foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;
