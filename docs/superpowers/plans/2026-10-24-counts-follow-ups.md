# Counts per day follow-ups (0.47.1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the 0.47.0 follow-ups a phone reader or a host would hit. The changes: Tab starts at the latest bar on a `ScrollToEnd` chart; PNG export works at any width; no series legend under a heatmap in `<LumenChart>`; a zero on a bar track is muted; the graph tests run again; a gallery range switch with a width-change check; the `DrawTitles` tip.

**Architecture:** Script changes in `lumen.js` (`settle`, `state.drawn`, `png`). One branch in `ChartSvg.cs` (the column track's value label). One condition in `LumenChart.razor` (the legend). A `days` parameter on the gallery's `DemoData.SignUps` with two toggle buttons on its card. Tests in the unit suite, the baseline harness and the Playwright suite. Docs last.

**Tech Stack:** C# / .NET 8 (library), .NET 10 (gallery, tests), Blazor Server, browser JavaScript, Playwright for .NET.

**Spec:** `docs/superpowers/specs/2026-10-24-counts-follow-ups-design.md` (8386def, approved 25 Oct 2026). It is the binding authority. Read its section for your task.

## Global Constraints

- Invented data only.
- Never colour alone: a zero is said in its name and its `0` label.
- Drawn text clears 4.5:1. The track's muted `0` uses `Style.Muted` only where `Contrast.Ratio(Style.Muted, Style.Background) >= 4.5`; otherwise the text colour.
- The classic finish stays byte for byte 0.23.0.
- No new spec property, so the gradient-ID hash does not move.
- Baseline: every one of the 408 rows stays identical in both finishes. Exactly one row is added, `columns/track-mark-zero`, making 409.
- Static SVG is unchanged by the script work: every mark keeps `tabindex="0"`.
- Every new public member needs an XML doc comment. A Release build shows 0 warnings.
- **Processes:**
  - Stop `Lumen.Gallery.exe` only by image name (`taskkill //IM Lumen.Gallery.exe //F`), and do it before every build, since it locks the output.
  - Stop every other process only by PID: `netstat -ano`, then `taskkill //PID <pid> //F`.
- **The Race Face brief:** never open, read or search `lumen-charts-race-face-brief.md` in the repo root. Search only `src/`, `tests/`, `samples/`, `docs/` and `integrations/`.
- **Git:** commit with explicit paths, never `git add .` or `-A`. Never push, merge, rebase or switch branches.
- **Line endings:** keep LF in every file (check with `git diff --check` and `git ls-files --eol`). Python edits use `newline="\n"` or `write_bytes`.
- **Culture:** the machine culture is en-ZA, so format with `CultureInfo.InvariantCulture`.
- **Leftovers:** delete any `.playwright-mcp` folder you create, and save screenshots under `%TEMP%\lumen0471\`.
- **SKILL.md:** its frontmatter stays byte-identical.

## Review Focus

1. **The reader's stop through a same-width redraw.** A reader moves the tab stop with the arrow keys, then a theme switch redraws the card at the same width: the stop stays on the reader's bar. Test: Task 1, step 1.
2. **A new span on a chart whose reader had focused a bar.** The stop moves to the new span's last bar; it does not stay on an index that is now another day. Test: Task 3, step 1.
3. **PNG of an extreme aspect ratio.** A 20,000-by-3 drawing gives a PNG at least 1 pixel tall, not a zero-height canvas that fails. Test: Task 1, step 1.
4. **A heatmap with `ShowLegend="true"` set explicitly** still draws no legend, while a line chart with it does. Test: Task 2, step 1.
5. **A track's zero under a custom `Muted` that fails 4.5:1** is written in the text colour. Test: Task 2, step 1.

---

### Task 1: The tab stop at the end, and PNG at any width (lumen.js)

**Files:**
- Modify: `src/Lumen.Charts.Blazor/wwwroot/lumen.js`:
  - the `rove`/`settle` comment and `settle` (about 119-132);
  - `state.drawn` (about 323-347);
  - `png` (about 714-724).
- Test: `tests/Lumen.Charts.Tests/Program.cs`, a `// 0.47.1` block after the 0.47.0 tests and before `string ChartMarkup(`.
- Test: `tests/Lumen.Charts.BrowserTests/Program.cs`:
  - inside the `#sign-ups` block (about 1704-1792);
  - next to "Export PNG rasterizes through the canvas" (about 289).

**Interfaces:**
- Produces: `settle(end)`. A truthy `end` makes the default stop the last point of the lowest series. Task 3's width-change check relies on the reset rule below.

- [ ] **Step 1: Write the failing browser tests**, in the suite's style, with SKIP lines where a host lacks the card.
  - **"With ScrollToEnd the Sign-ups per day card's one tab stop is its latest bar, Tab reaches it without scrolling the chart back, and a redraw at the same width keeps the reader's stop".** In a 1280×900 context through `Fresh41`, after the card's `.lumen-tooltip` is attached and its script has taken the drawing:
    - exactly one `#sign-ups .lumen-datum[tabindex="0"]`, and its `aria-label` starts `"Mon 2 Nov 2026: "`;
    - focusing it (`FocusAsync`) leaves the viewport at its end (`scrollLeft + clientWidth >= scrollWidth - 1`);
    - press `ArrowLeft` twice: the stop's `aria-label` starts `"Sat 31 Oct 2026: "`;
    - click the theme switch (the `NameRegex` "theme" button the hidden-card test uses) and wait for the dark background and the taken drawing: the stop still starts `"Sat 31 Oct 2026: "`.
  - **"Export PNG keeps the longest edge within 8,192 pixels at any width".** On the home page:
    - import the component's module (`await import('/_content/Lumen.Charts.Blazor/lumen.js')`);
    - call `png(svg, 'wide.png', 2)` with a self-contained SVG string `viewBox='0 0 20000 300'` (one `<rect>`), inside `RunAndWaitForDownloadAsync`;
    - read the PNG's IHDR (big-endian width at bytes 16-19, height at 20-23): 8192 by 123;
    - repeat with `viewBox='0 0 20000 3'`: 8192 wide and at least 1 tall.
- [ ] **Step 2: Run them; they fail.**
  - Build: `taskkill //IM Lumen.Gallery.exe //F`, then `dotnet build Lumen.Charts.slnx -c Release` and `dotnet build tests/Lumen.Charts.BrowserTests -c Release`.
  - Start the gallery in the background: `dotnet run --project samples/Lumen.Gallery -c Release --no-build --urls http://localhost:5188`.
  - Run: `dotnet run --project tests/Lumen.Charts.BrowserTests -c Release --no-build`.
  - Expected: the tab-stop test fails (the stop is "Thu 20 Aug 2026"), and the PNG test fails (20000 wide, or "Export failed" / a rejected promise).
- [ ] **Step 3: `settle(end)`.** With `end`, the default stop is the point with the lowest series and, within it, the highest point index. Without `end`, it is the first point, as today. The mark last focused still wins while it exists. Update the comment above `rove`: "…or else the first point of the first series (the last, on a chart asked to open at its end)…".
- [ ] **Step 4: `state.drawn` resets the stop for a new span.**
  - Compute `width` (`drawing()?.viewBox?.baseVal?.width ?? 0`) before `settle`.
  - When `end` is set and `width !== (state.endPending ?? state.endWidth)`, set `state.current = null`.
  - Then call `settle(end)`.
  - The existing end-pending block keeps its rule and its order (`region()`, then the pending test). Do not change `toEnd` or `region`.
- [ ] **Step 5: `png` drops the floor.**
  - `const factor = Math.min(scale || 2, 8192 / Math.max(width, height));`
  - Canvas edges `Math.max(1, Math.round(width * factor))` and `Math.max(1, Math.round(height * factor))`.
- [ ] **Step 6: Unit text checks** in the `// 0.47.1` block. The script contains:
  - `Math.min(scale || 2, 8192 / Math.max(width, height))`;
  - no `Math.max(1, Math.min(scale`.
  The existing pins at about 7011, 7310, 8418 and 11006 still pass unchanged.
- [ ] **Step 7: Run everything this task touches.**
  - Unit: `dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build`, 0 failed.
  - Browser on 5188: 0 failed, including the existing phone, hidden-card and smooth-scroll checks and "Export PNG rasterizes through the canvas".
  - Known flakes: the Midnight axe sweep's `Hovered()` and the 1280 tab-stop test on a fresh gallery. Re-run once and say so.
  - Stop the gallery by name.
- [ ] **Step 8: Commit.**

```bash
git add src/Lumen.Charts.Blazor/wwwroot/lumen.js tests/Lumen.Charts.Tests/Program.cs tests/Lumen.Charts.BrowserTests/Program.cs
git commit -m "Give a chart that opens at its end its last bar as its tab stop, and keep a PNG's longest edge within 8,192 at any width"
```

### Task 2: A muted zero on a track, and no legend under a heatmap

**Files:**
- Modify: `src/Lumen.Charts/ChartSvg.cs`, the column track's value label (about 1127): `else if (s.BarTrack) Above(…)`.
- Modify: `src/Lumen.Charts.Blazor/LumenChart.razor:15`, the legend condition.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, the `// 0.47.1` block.
- Baseline: `tests/Lumen.Charts.Baseline/Program.cs`, one row after `daily-counts/year` (about 900), and both `reference/*.txt`.

**Interfaces:**
- Consumes: the existing `Above(double x, double w, double y, bool up, string text, string? note, double? room = null, string? fill = null)` and `ChartMarkup(ChartSpec, Dictionary<string,object?>)` (test helper).

- [ ] **Step 1: Write the failing unit tests** (Test/Check style):
  - **A track's zero.** `new ChartSpec { Kind = ChartKind.Column, Title = "Zero on a track", YMax = 100, BarTrack = true, Series = [new("Score", [new(0, 82, "A"), new(1, 0, "B"), new(2, 64, "C")]) { ValueLabels = true, MarkZero = true }] }`:
    - its SVG writes the `0` text with `fill='{ChartStyle.Light.Muted}'`;
    - the `82` and `64` labels keep the text colour;
    - the hairline is there: `height='1' fill='{Muted}'`.
    - With `Style = ChartStyle.Light with { Muted = "#B0B8C4" }` (under 4.5:1 on white), the `0` is written in the text colour.
  - **No heatmap legend.** `ChartMarkup(Sample(ChartKind.Heatmap), new(){{"ShowLegend",true}})` has no `lumen-legend`, and `ChartMarkup(Sample(ChartKind.Line), new(){{"ShowLegend",true}})` has one.
- [ ] **Step 2: Run them; they fail.** Run `dotnet build Lumen.Charts.slnx -c Release`, then the unit suite. Expected: the muted-zero check and the heatmap-legend check fail.
- [ ] **Step 3: The track branch.** It passes the plain branch's argument: `fill: hairline && Contrast.Ratio(w.Style.Muted, w.Style.Background) >= 4.5 ? w.Style.Muted : null`. The horizontal track line is unchanged.
- [ ] **Step 4: The legend.** The condition is `ShowLegend && Spec.Kind is not (ChartKind.Strip or ChartKind.Heatmap)`. Update a comment there if one names the kinds.
- [ ] **Step 5: Baseline row.** Add it after `daily-counts/year`:
  - the row: `lines.Add($"columns/track-mark-zero {Hash(Render(<the step 1 spec>))}");`
  - run both finishes from `tests/Lumen.Charts.Baseline`: `dotnet run -c Release` and `dotnet run -c Release -- classic`;
  - diff each against its reference with CR stripped: rows 1-408 identical, one row added (409);
  - regenerate both references and delete the run files.
- [ ] **Step 6: Run the unit suite and a Release build:** 0 failed, 0 warnings. Name any existing test you had to update (none is expected: no test counts heatmap legends).
- [ ] **Step 7: Commit.**

```bash
git add src/Lumen.Charts/ChartSvg.cs src/Lumen.Charts.Blazor/LumenChart.razor tests/Lumen.Charts.Tests/Program.cs tests/Lumen.Charts.Baseline/Program.cs tests/Lumen.Charts.Baseline/reference/refined.txt tests/Lumen.Charts.Baseline/reference/classic.txt
git commit -m "Mute a zero's 0 on a column track as on a plain column, and draw no series legend under a heatmap"
```

### Task 3: The gallery's range switch, the width-change check, and the graph tests

**Files:**
- Modify: `samples/Lumen.Gallery/DemoData.cs`, `SignUps` (about 411-417).
- Modify: `samples/Lumen.Gallery/Components/Pages/Home.razor`, the `#sign-ups` card (60) and its `@code`.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, the 0.47.0 gallery test (about 11075-11093) or the `// 0.47.1` block.
- Test: `tests/Lumen.Charts.BrowserTests/Program.cs`:
  - the `#sign-ups` block;
  - the graph tests (about 525-561).

**Interfaces:**
- Consumes: Task 1's `settle(end)` and its new-span reset.
- Produces: `public static Lumen.Charts.ChartSpec? SignUps(Lumen.Charts.ChartTheme theme, int days = 75)` (the XML doc updated). For `days < 75` it passes `From = new DateOnly(2026, 11, 2).AddDays(-(days - 1))`.

- [ ] **Step 1: Write the failing tests.**
  - **Unit.** `DemoData.SignUps(ChartTheme.Light, 30)`:
    - 30 bars, the first named `"Sun 4 Oct 2026: …"` with axis label `"4\nOct"`, the last starting `"Mon 2 Nov 2026: "`;
    - `ChartSvg.DrawnWidth` is `76 + 30 * 30 + 30` (1006);
    - `SignUps(ChartTheme.Light)` still has 75 bars and 2356.
  - **Browser: "Picking Last 30 days redraws the Sign-ups per day card 1,006 wide at its end with its last bar the tab stop, and All 75 days brings back 2,356 at its end".** On the 1280×900 home page:
    - set the viewport's `scrollLeft` to 240;
    - click the card's "Last 30 days" button: it becomes `aria-pressed="true"` and "All 75 days" `"false"`;
    - wait for the drawing's `viewBox` width 1006 and the script to take it: the viewport stands at its end, and the one `tabindex="0"` mark starts `"Mon 2 Nov 2026: "`;
    - click "All 75 days": 2356, at its end.
  - **Browser, the graph tests.**
    - Replace `page.Locator(".lumen-chart").Nth(1)` with `page.Locator("#network .lumen-chart")`.
    - Replace the two `document.querySelectorAll('.lumen-chart')[1]` with `document.querySelector('#network .lumen-chart')`.
    - Add an `else Console.WriteLine("SKIP graph checks: this host's first page has no network graph");`.
- [ ] **Step 2: Run them.** The unit check and the range test fail (no `days` parameter, no buttons). The graph tests now run: say whether each passes. If one fails for a reason in the product rather than the test, stop and report it with the failing output (spec §6). Do not change `LumenGraph` in this task.
- [ ] **Step 3: `SignUps(theme, days = 75)`.** Build from the same 75 invented days, passing `From` for `days < 75`. Then shed `Style` and the series `Color` as today.
- [ ] **Step 4: The card's switch.**
  - Above the chart inside `#sign-ups`, two buttons in the page's toggle style (`class="@(… ? "selected" : "")" aria-pressed="@(… ? "true" : "false")"`): "Last 30 days" and "All 75 days".
  - State: `int signUpDays = 75;`. The chart is `DemoData.SignUps(Theme, signUpDays)!`.
  - Add one clause to the card's `fit-note`: the switch redraws a shorter span, and it opens at its end again.
  - The default markup must keep `verify-api.ps1:15` passing (`id="sign-ups"`, `lumen-fixed`, `--lumen-drawn:2356px`, `aria-label='Thu 20 Aug 2026: `).
- [ ] **Step 5: Run everything this task touches.**
  - Build: the solution and the browser tests.
  - Unit: 0 failed.
  - Gallery on 5188, then `pwsh -File tests/verify-api.ps1 -BaseUrl http://localhost:5188`: 302.
  - Browser suite: 0 failed. Report the count; the graph tests now count.
  - Screenshots of the card at 375 and 1400 px, in both ranges.
  - Stop the gallery by name.
- [ ] **Step 6: Commit.**

```bash
git add samples/Lumen.Gallery/DemoData.cs samples/Lumen.Gallery/Components/Pages/Home.razor tests/Lumen.Charts.Tests/Program.cs tests/Lumen.Charts.BrowserTests/Program.cs
git commit -m "Gallery: a 30- or 75-day switch on the sign-ups card, checked to open each span at its end; find the network graph by its card"
```

### Task 4: Docs, version and full verification

**Files:**
- Modify:
  - `README.md`;
  - `integrations/claude-code/lumen-charts/references/api.md`;
  - `integrations/claude-code/lumen-charts/SKILL.md` (body only);
  - `integrations/claude-code/lumen-charts/references/recipes-race-face.md`;
  - `docs/VERIFICATION.md`;
  - `tests/Lumen.Charts.Baseline/README.md`;
  - `Directory.Build.props`.

- [ ] **Step 1: README.** Write what the code does, from the code and spec §7:
  - "## 0.47.1 additions" before "## 0.47.0 additions": the tab stop, PNG, the heatmap legend, the track's muted zero, and the gallery switch;
  - the Blazor integration paragraph (about 99) and the keyboard section (about 1281): "the first point of the first series, or with `ScrollToEnd` the last";
  - the PNG sentence (about 1120): longest edge within 8,192 at any width; a wider drawing is scaled down and its words shrink; Export SVG keeps every detail;
  - the `DrawTitles` tip in "Counts per day" (its Limits paragraph, about 643);
  - the E-notation limit (spec §4);
  - the header's preview version.
- [ ] **Step 2: api.md, SKILL.md body and the recipe.**
  - api.md: the tab stop (about 249), the PNG cap, and the heatmap legend.
  - The `DrawTitles` tip in api.md's `ScrollToEnd` bullet (about 252), in SKILL.md's "Counts per day" bullet (about 99), and in the "Registrations per day" recipe (about 810).
  - Check that SKILL.md's frontmatter is unchanged with `git diff`.
- [ ] **Step 3: Version, baseline README and VERIFICATION.md.**
  - `Directory.Build.props`: `0.47.0` → `0.47.1`.
  - Baseline README: v0.47.1's references are 409 rows, all 408 of 0.47.0's identical in both finishes, plus `columns/track-mark-zero`.
  - VERIFICATION.md: a 0.47.1 entry in the 0.47.0 entry's format, with the counts measured in step 4.
- [ ] **Step 4: Full verification**, as 0.47.0's Task 7 did:
  - build at 0 warnings;
  - unit;
  - baseline in both finishes against the references, no difference;
  - the gallery, `verify-api.ps1` and the browser suite;
  - the WebAssembly host: build `samples/Lumen.Wasm`, run it on 5199, run the browser suite with `http://localhost:5199`, then stop it by PID;
  - the recipes: `python check.py`, then `dotnet run -c Release`, 41 charts.
  Write every count into VERIFICATION.md.
- [ ] **Step 5: Commit.**

```bash
git add README.md integrations/claude-code/lumen-charts/references/api.md integrations/claude-code/lumen-charts/SKILL.md integrations/claude-code/lumen-charts/references/recipes-race-face.md docs/VERIFICATION.md tests/Lumen.Charts.Baseline/README.md Directory.Build.props
git commit -m "Document 0.47.1: the tab stop at the end, PNG at any width, no heatmap legend, a muted zero on a track, and the DrawTitles tip"
```
