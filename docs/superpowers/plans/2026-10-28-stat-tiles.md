# Stat tiles (0.48.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `<LumenStats>`, a row or grid of headline numbers that speaks Lumen's language. That means Lumen's value formats, "better / worse" change words with colour only where it reads, an optional trend sparkline and target meter, and links. Ship with it the shared HTML foundation that the 0.49.0 table and the 0.50.0 timeline will reuse.

**Architecture:**
- **Core (`Lumen.Charts`):** an internal HTML writer, a scoped style block carrying `ChartStyle` as CSS custom properties, and an internal value-and-change words helper. The public `StatTile` and `StatTilesSpec` records come with a validator and `StatTilesHtml.Render`, which returns an HTML fragment.
- **Blazor:** `<LumenStats>` writes that fragment.
- **AspNetCore:** `POST …/stats/html` returns it.
- **Gallery:** a new Components page shows it.

**Tech Stack:** C# / .NET 8 (libraries), .NET 10 (gallery, tests), Blazor Server and WebAssembly, ASP.NET Core minimal APIs, Playwright for .NET with axe-core.

**Spec:** `docs/superpowers/specs/2026-10-28-beyond-charts-design.md`, §1–§5 and §8, approved by the owner on 28 Oct 2026. It is the binding authority. §6 (table) and §7 (timeline) are later releases; do not build them.

## Global Constraints

- **Dependencies:** no third-party code, no JavaScript framework, no CDN. Everything lives in the existing three packages.
- **CSS scope:**
  - No global CSS: every rule sits under the `.lumen-stats` root class.
  - Class names start with `lumen-`.
  - Nothing may change a MudBlazor or Bootstrap page around the component.
- **Charts are untouched.** The 411 baseline rows stay byte-identical in both finishes. Tile rows may only be added.
- **Contrast and colour:**
  - Every drawn text colour clears 4.5:1 against the tile background: a colour is used only where `Contrast.Ratio(colour, background) >= 4.5`, else `Style.Text`.
  - Never colour alone: every arrow, colour, meter and sparkline also exists in words, visible or visually hidden.
- **Escaping and links:**
  - Every text is HTML-encoded (`WebUtility.HtmlEncode`).
  - Links accept only `http:`, `https:`, `mailto:` and relative addresses. A scheme is matched case-insensitively after trimming, so `JavaScript:` and ` javascript:` are refused.
- **Formats:** use `CultureInfo.InvariantCulture`. Values are formatted with the existing `Axis` formatting: `new Axis(AxisKind.Linear, 0, 1) { ValueFormat = …, Unit = … }.Format(v)`.
- **API surface:**
  - Every new public member has an XML doc comment (the build fails without one), and the Release build shows 0 warnings.
  - Validation throws `ArgumentException` with a plain message, in `ChartValidation`'s voice.
- **Invented data only**, in the gallery, recipes and tests.
- **Processes:**
  - Stop `Lumen.Gallery.exe` only by image name (`taskkill //IM Lumen.Gallery.exe //F`), and do it before every build.
  - Stop every other process by PID only: `netstat -ano`, then `taskkill //PID <pid> //F`.
  - `tests/Lumen.Charts.BrowserTests` and `samples/Lumen.Wasm` are outside the solution: build them explicitly.
- **The Race Face brief:** never open, read or search `lumen-charts-race-face-brief.md`.
- **Git:**
  - Commit with explicit paths only.
  - Never push, merge, rebase, stash, reset or switch branches.
  - Keep LF line endings (`git diff --check`).
- **Leftovers:** delete any `.playwright-mcp` folder, and save screenshots under `%TEMP%\lumen0480\`.
- **SKILL.md:** its frontmatter stays byte-identical.

## Review Focus

1. **Two identical tile sets on one page.** Nothing collides, because the markup uses no `id` attributes. Test: Task 2.
2. **Hostile words.** A label, note or title holding `<script>` or quotes is written escaped. Test: Task 2.
3. **Disguised links.** `JavaScript:alert(1)`, ` javascript:`, `data:` and `vbscript:` are refused, while `/riders/7`, `riders/7`, `https://…` and `mailto:…` pass. Test: Task 2.
4. **A percentage of zero.** `Previous = 0` with `ChangeAs.Percent` falls back to the difference. A negative `Previous` takes its percentage of `|Previous|`. Test: Task 1.
5. **A custom style whose `Rising` fails 4.5:1** writes "better" in the text colour, and the words still say "better". Test: Task 1.

---

### Task 1: The shared foundation (HTML writer, scoped style, change words)

**Files:**
- Create: `src/Lumen.Charts/Html.cs`. It holds the internal static class `LumenHtml`: escaping, link checking, the root style attribute, the `lumen-sr` visually-hidden rule and `Readable`.
- Create: `src/Lumen.Charts/Change.cs`. It holds the public enums `Better` and `ChangeAs`, and the internal static class `ChangeWords`.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, a new `// 0.48.0` block before `string ChartMarkup(`.

**Interfaces:**
- Produces:
  - `public enum Better { Neither, Higher, Lower }`, each value with an XML doc.
  - `public enum ChangeAs { Difference, Percent, Both }`.
  - `internal static class LumenHtml`:
    - `static string E(string? text)`: HTML-encoded text, `""` for null.
    - `static bool SafeLink(string href)`: true for `http:`, `https:` and `mailto:` and for relative addresses (no scheme before the first `/`, `?` or `#`). It is case-insensitive and ignores leading and trailing white space.
    - `static string Readable(string colour, ChartStyle style)`: `colour` where `Contrast.Ratio(colour, style.Background) >= 4.5`, else `style.Text`.
    - `static string RootStyle(ChartStyle style)`: the inline `style` value `--lumen-bg:…;--lumen-text:…;--lumen-muted:…;--lumen-grid:…;--lumen-rising:…;--lumen-falling:…;--lumen-accent:…;--lumen-font:…`. Here `--lumen-muted` is `Readable(style.Muted, style)` and `--lumen-accent` is `style.SeriesColor(0)`; every value is CSS-safe as `ChartValidation.Style` already guarantees.
    - `const string HiddenRule`: the CSS for `.lumen-sr`, the usual visually-hidden pattern (`position:absolute; width:1px; height:1px; overflow:hidden; clip:rect(0 0 0 0); white-space:nowrap`).
  - `internal static class ChangeWords`:
    - `static string Value(double value, ValueFormat format, string? unit)`: formatted as the axis formats it.
    - `static (string Text, string Colour) Of(double value, double previous, Better better, ChangeAs changeAs, string? since, ValueFormat format, string? unit, ChartStyle style)`.

**The exact change words** (the spec's §4 copy, pinned here):
- **Arrow:** `▲` when the value rose, `▼` when it fell, `=` when equal. The arrow follows the value, whatever `Better` says.
- **Amount:**
  - Difference: `Value(|value − previous|, …)`.
  - Percent: `|value − previous| / |previous| × 100`, written with one decimal under 10 and whole from 10 up, with a trailing `.0` dropped and a `%` added (`8.3%`, `12%`, `4%`).
  - Both: `{difference} ({percent})`.
  - When `previous == 0`, Percent and Both write the difference only.
- **Word:**
  - With `Higher`: rising is "better" and falling is "worse".
  - With `Lower`: rising is "worse" and falling is "better".
  - With `Neither`: "up" and "down".
- **Text:**
  - The text is `"{arrow} {amount} · {word}"`, then:
    - for better and worse, `" than {since}"`;
    - for up and down, `" from {since}"`;
    - nothing when `since` is null or blank.
  - Equal values write `"= same as {since}"`, or `"= same"` when `since` is null or blank.
  - Examples: `▲ 2 · better than last season`, `▼ 0:42 · better than last season` (a duration with `Lower`), `▲ 12 (8%) · up from last week`, `= same as last season`.
- **Colour:**
  - Better is `Readable(style.Rising, style)` and worse is `Readable(style.Falling, style)`.
  - Up, down and same take `Readable(style.Muted, style)`.

- [ ] **Step 1: Write the failing unit tests** in the `// 0.48.0` block:
  - **"0.48.0: change words follow the value's direction and the judgement Better gives it".** One `Check` per example above, plus:
    - `Lower` with a rise writes `▲ 3 · worse than last race`;
    - `Neither` with a fall writes `▼ 5 · down from last week`;
    - with `since` null, `▲ 2 · better`.
  - **"0.48.0: a percentage is of the previous value's size, one decimal under 10, and falls back to the difference from zero" (Review Focus 4).** It checks:
    - 108 against 100 is `8%`;
    - 112 against 100 is `12%`;
    - 103.3 against 100 is `3.3%`;
    - −90 against −100 with `Neither` writes `▲ 10%`, because the percentage is of `|previous|`;
    - 5 against 0 with `Percent` writes `▲ 5`.
  - **"0.48.0: colours: Rising for better, Falling for worse, muted for up, down and same, each only where it clears 4.5:1" (Review Focus 5).** With `ChartStyle.Light`, better is `Light.Rising` where that clears 4.5:1 against `Light.Background` (compute it, do not assume), else `Light.Text`. With a style whose `Rising = "#FFFFFF"` on a white background, the colour is `Text` and the words still say "better".
  - **"0.48.0: links: only http, https, mailto and relative addresses" (Review Focus 3's list).** True for `https://x.test/a`, `HTTP://x.test`, `mailto:a@x.test`, `/riders/7`, `riders/7`, `?page=2` and `#top`. False for `javascript:alert(1)`, `JavaScript:alert(1)`, `  javascript:x`, `data:text/html,x`, `vbscript:x` and `ftp://x`.
  - **"0.48.0: the root style carries the style's colours and font as custom properties".** `RootStyle(ChartStyle.Dark)` contains `--lumen-bg:#171E2E` and `--lumen-text:#E8ECF6`.
- [ ] **Step 2: Run the tests.** Run `taskkill //IM Lumen.Gallery.exe //F`, `dotnet build Lumen.Charts.slnx -c Release`, then `dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build`. Expected: the build fails (the types do not exist yet), which counts as RED.
- [ ] **Step 3: Implement `Html.cs` and `Change.cs`.** The test project reaches internal members through the existing `InternalsVisibleTo`; check that it covers `LumenHtml` and `ChangeWords`.
- [ ] **Step 4: Run the unit tests.** Expected: all pass, 0 warnings.
- [ ] **Step 5: Commit** both new files and the tests.

### Task 2: Stat tiles in the core

**Files:**
- Create: `src/Lumen.Charts/StatTiles.cs`, holding `StatTile`, `StatTilesSpec`, `TileDensity` and `StatTilesHtml`.
- Create: `src/Lumen.Charts/StatTilesValidation.cs`, holding `internal static class StatTilesValidation`.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, in the `// 0.48.0` block.
- Modify: `tests/Lumen.Charts.Baseline/Program.cs` (add `tiles/*` rows after the last row) and both `reference/*.txt` files (added rows only).

**Interfaces:**
- Consumes: Task 1's `LumenHtml`, `ChangeWords`, `Better` and `ChangeAs`.
- Produces:
  - `public sealed record StatTile(string Label, double? Value)`, with these `init` properties:
    - `ValueFormat Format` (default `Number`) and `string? Unit`;
    - `double? Previous`, `Better Better` (default `Neither`), `ChangeAs ChangeAs` (default `Difference`) and `string? Since`;
    - `string? ChangeText` (it wins over computed words, written in the muted colour);
    - `string Missing` (default `"—"`), `string? Note`, `IReadOnlyList<double?>? Trend`, `double? Target` and `string? Href`.
  - `public enum TileDensity { Regular, Compact }`.
  - `public sealed record StatTilesSpec(IReadOnlyList<StatTile> Tiles)`, with `string? Title`, `string? Description`, `int TitleLevel` (default 2), `int? Columns`, `TileDensity Density` and `ChartStyle? Style` (null means `ChartStyle.Light`).
  - `public static class StatTilesHtml` with `public static string Render(StatTilesSpec spec)`. It validates first.

**Validation (exact limits):**
- Tiles: 1–48.
- `Label`: 1–40 characters and not blank.
- `Value`, `Previous` and every `Trend` value: finite.
- `Missing`: 1–24 characters.
- `Note`, `Since` and `ChangeText`: at most 60, 40 and 40 characters.
- `Trend`: at most 400 values, of which at least 2 are not null.
- `Target`: finite and > 0.
- `Href`: `LumenHtml.SafeLink`.
- `Title`: at most 120 characters; `Description`: at most 300.
- `TitleLevel`: 2–6. `Columns`: null or 1–6.
- `Style`: through `ChartValidation.Style`.
- Messages say what is wrong and what is allowed.

**Markup (exact structure; the CSS values are the builder's, within the constraints):**
- **The root:**
  - `<section class='lumen-stats lumen-stats-{regular|compact}' style='{RootStyle}' aria-label='{Title or Description or "Statistics"}'>`.
  - Then `<style>` holding the scoped rules and `HiddenRule`, written in every fragment.
  - Then `<h{TitleLevel} class='lumen-stats-title'>` when there is a title, and `<p class='lumen-stats-desc'>` when there is a description.
  - **No `id` attribute anywhere** (Review Focus 1).
- **The grid:** `<ul class='lumen-stats-grid' role='list'>`. Its CSS grid is `repeat(auto-fill,minmax(150px,1fr))`, or `minmax(96px,1fr)` when Compact, or `repeat({Columns},minmax(0,1fr))` when `Columns` is set.
- **Each tile:** `<li class='lumen-stat'>`, whose content is wrapped in `<a class='lumen-stat-link' href='…'>` when `Href` is set. It reads in this order:
  1. `<p class='lumen-stat-label'>` (muted);
  2. `<p class='lumen-stat-value'>` (the largest text, 26 px, or 20 px Compact), holding the formatted value, or `Missing` followed by `<span class='lumen-sr'> none</span>` when it is the default dash;
  3. `<p class='lumen-stat-change' style='color:{colour}'>`, when `Previous` or `ChangeText` is set and `Value` is not null;
  4. `<p class='lumen-stat-note'>` (muted);
  5. when `Target` is set: `<div class='lumen-stat-meter' aria-hidden='true'><span style='width:{min(100,pct)}%'></span></div>`, then `<p class='lumen-stat-target'>{pct}% of target {Value(target)}</p>`, with pct a whole number;
  6. when `Trend` is set: `<div class='lumen-stat-trend' aria-hidden='true'>{sparkline svg}</div><span class='lumen-sr'>{n} values, lowest {min}, highest {max}, last {last}</span>`, where n counts the non-null values and last is the last non-null one.
- **The sparkline:** `ChartSvg.Render(new ChartSpec { Kind = Line, Sparkline = true, Width = 120, Height = 28, Style = style, Series = [new(label, points)] }, includeLegend: false)`, where the points are `(index, value)` and null values are gaps. Use the spec's own builder conventions for a missing point.
- **Font sizes:** no text under 11 px.
- **The focus ring:** `.lumen-stat-link:focus-visible{outline:2px solid var(--lumen-accent);outline-offset:2px}`.

- [ ] **Step 1: Write the failing unit tests:**
  - **"0.48.0: tiles render label, value, change, note, target and trend in reading order, all escaped".** Parse the fragment's tiles with a regex or `XElement`, after wrapping it in a root.
  - **"0.48.0: a missing value writes its words and a hidden 'none'"**, for the default and for `Missing = "No race yet"` (which gets no hidden word).
  - **Review Focus 1:** "0.48.0: a fragment has no id, so two on a page cannot collide". It checks that `!html.Contains(" id=")`.
  - **Review Focus 2:** "0.48.0: hostile words are escaped". A label `<script>x</script>` and a note with `'` and `"` appear only encoded.
  - **Review Focus 3:** "0.48.0: a tile's link must be http, https, mailto or relative". `JavaScript:alert(1)` throws `ArgumentException`, and `/riders/7` renders `<a class='lumen-stat-link' href='/riders/7'>`.
  - **"0.48.0: validation limits".** One `Reject` or `Check(throws)` per limit above, at the boundary and one past it.
  - **"0.48.0: the change takes Readable colours and ChangeText wins"**, against Task 1's rules.
  - **"0.48.0: the trend is the existing sparkline, hidden, with its words".** The fragment contains `class='lumen-svg` inside `lumen-stat-trend`, and its hidden summary is exact for `[34, null, 36, 41]`: `3 values, lowest 34, highest 41, last 41`.
  - **"0.48.0: the target meter caps at 100% and says its percentage"**: 41 of 50 is `82% of target 50`, and 60 of 50 is a meter width of `100%` with the words `120% of target 50`.
- [ ] **Step 2: Run the tests and check they fail** (the build fails, which counts as RED).
- [ ] **Step 3: Implement** `StatTiles.cs` and `StatTilesValidation.cs`.
- [ ] **Step 4: Run the unit tests.** Expected: all pass.
- [ ] **Step 5: Baseline.**
  - Add rows that hash `StatTilesHtml.Render(...)`:
    - `tiles/regular-light`: six invented tiles with every part;
    - `tiles/regular-dark` (`ChartStyle.Dark`);
    - `tiles/compact-strip`: eight round tiles with `Columns = null`;
    - `tiles/missing-and-link`;
    - `tiles/fixed-columns` (`Columns = 3`).
  - Run both finishes. Expected: the 411 existing rows are identical, and 5 are added (416), the same in both finishes. Update the references with the added rows only.
- [ ] **Step 6: Commit.**

### Task 3: `<LumenStats>` and the HTTP endpoint

**Files:**
- Create: `src/Lumen.Charts.Blazor/LumenStats.razor`.
- Modify: `src/Lumen.Charts.AspNetCore/ChartEndpoints.cs`, adding `group.MapPost("/stats/html", …)` with a 400 Problem titled `"Invalid stat tiles"`, following `Render`'s pattern.
- Modify: `tests/verify-api.ps1`.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, in the `// 0.48.0` block.

**Interfaces:**
- Consumes: Task 2's `StatTilesSpec` and `StatTilesHtml.Render`.
- Produces:
  - `<LumenStats Spec="…"/>`, with `[Parameter, EditorRequired] public StatTilesSpec Spec` and `[CascadingParameter] public ChartStyle? CascadingStyle`.
  - It renders `(MarkupString)StatTilesHtml.Render(Spec with { Style = Spec.Style ?? CascadingStyle ?? ChartStyle.Light })` and nothing else.
  - XML docs on the class and both parameters.

- [ ] **Step 1: Write the failing tests:**
  - A unit test that prerenders `<LumenStats>` with `HtmlRenderer`, as `ChartMarkup` does, and checks that it equals `StatTilesHtml.Render(spec)` for a spec with a `Style`.
  - A unit test that, under a cascading `ChartStyle.Dark`, the markup carries `--lumen-bg:#171E2E`.
  - In `verify-api.ps1`, in its style:
    - POST a valid spec to `/stats/html`: 200, `text/html`, a body containing `class='lumen-stats`;
    - POST 49 tiles: 400;
    - POST a tile with `Href = "JavaScript:alert(1)"`: 400.
- [ ] **Step 2: Run them.** Expected: the build fails or the tests fail.
- [ ] **Step 3: Implement** the component and the endpoint.
- [ ] **Step 4: Run** the unit tests, then start the gallery and run `verify-api.ps1`. Expected: all pass. Report the HTTP count.
- [ ] **Step 5: Commit.**

### Task 4: The gallery's Components page and browser checks

**Files:**
- Create: `samples/Lumen.Gallery/Components/Pages/Components.razor` (`@page "/components"`). Follow `Sports.razor` for the layout, the theme switch and `GalleryLook`.
- Modify:
  - `samples/Lumen.Gallery/DemoData.cs`: `SeasonTiles(ChartStyle style)` and `RoundStrip(ChartStyle style)`, with invented data;
  - the nav in `Home.razor` and `Sports.razor`: a "Beyond charts" item linking to `components`;
  - `tests/Lumen.Charts.BrowserTests/Program.cs`;
  - `tests/verify-api.ps1`: GET `/components` returns 200 and contains `Season at a glance`.

**Interfaces:**
- Consumes: Task 3's `<LumenStats>`.

**Content (invented):**
- **"Season at a glance":** six tiles.
  - Races: 12, against 9 last season, `Higher`, with a trend over 12 rounds.
  - Best finish: `Value = 3`, `Note = "place"`, `Previous = 5`, `Better = Lower` (writes `▼ 2 · better than last season`).
  - Total time: `Duration`, `Lower`.
  - Podiums: 2, with `Target = 4`.
  - Points: 148, `Higher`, `ChangeAs = Both`.
  - Next race: `Value = null`, `Missing = "No race yet"`, `Href = "#next"`.
- **"Round by round":** a Compact strip of eight round tiles, each a place with its change on the round before.

- [ ] **Step 1: Write the failing browser tests**, in the suite's style, with a SKIP line where the host has no `/components` page (the WebAssembly host):
  - **"Components page: the tiles pass axe in Light, Dark and Midnight"**, using the suite's axe sweep.
  - **"Components page on a 375-pixel phone (`isMobile`, `hasTouch`)":** the season grid shows two tiles a row (the first two tiles share a top, the third is lower), and the page's `scrollWidth` is not more than its `clientWidth`.
  - **"Components page: Tab reaches the link tile and its focus ring is drawn"**, checking the computed `outline-style` is not `none`.
  - **"Components page: the prerendered tiles equal the interactive ones"**: the section's `outerHTML` from the first response is the same as after the circuit starts.
- [ ] **Step 2:** Build the gallery and the browser tests, run them, and check the new tests fail.
- [ ] **Step 3: Implement** the page, the data and the nav.
- [ ] **Step 4:** Run the gallery browser suite and verify-api. Expected: all pass; report the counts. Take screenshots at 1280 and 375 in Light and Dark, saved under `%TEMP%\lumen0480\` and listed in the report.
- [ ] **Step 5: Commit.**

### Task 5: Docs, the skill and the version

**Files:**
- Modify: `Directory.Build.props` to `0.48.0`.
- Modify: `README.md`, with:
  - a "### Stat tiles" section under Blazor integration: what it is, a code sample, the change words, links and limits;
  - a "## 0.48.0 additions" section above 0.47.2's, in its style;
  - the Limits.
- Modify: `integrations/claude-code/lumen-charts/references/api.md`, adding a `StatTile` and `StatTilesSpec` table and the `<LumenStats>` line.
- Modify: `integrations/claude-code/lumen-charts/SKILL.md`, the body only: one bullet.
- Modify: `integrations/claude-code/lumen-charts/references/recipes-race-face.md`, adding a "## Season at a glance" recipe before "## Rendering notes", with invented data, compiling as written.
- Modify: `tests/Lumen.Charts.Recipes` if its check needs the new recipe counted.
- Modify: `docs/VERIFICATION.md` and `tests/Lumen.Charts.Baseline/README.md`.

- [ ] **Step 1:** Make the edits. Every count you write comes from a run you made.
- [ ] **Step 2:** Run `git diff --check`, and confirm the SKILL.md frontmatter is unchanged.
- [ ] **Step 3:** Run everything:
  - the Release build (0 warnings);
  - the unit tests;
  - both baselines (416 rows each, identical to the references);
  - verify-api;
  - the gallery browser suite;
  - the WebAssembly host's suite;
  - the recipes (`python check.py`, then `dotnet run -c Release`).

  Report each count.
- [ ] **Step 4: Commit.**
