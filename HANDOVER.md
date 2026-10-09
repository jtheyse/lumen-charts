# Handover

The state of Lumen.Charts and how work on it is done, for whoever picks it up next — a person or a new Claude Code session. Updated 9 October 2026. The THEBRAIN brain `@LumenCharts` holds the same state plus every design in detail: recall it first (`brain_recall`, intent `current_state`).

## Where things stand

- **Released:** v0.46.2, *heatmap cells show it all* (commit cff8a7a, tag v0.46.2, 9 Oct 2026): the three gaps that kept Race Face's #16 swap unmerged, each cell showing at least what its hand-built table drew. `ChartSpec.CellNotes` draws a cell's `ValueNote` under its sub-label (the separator trimmed, broken after commas first, a too-wide word cut with "…" and the words after it kept); `ChartSpec.NotRatedKeepsValue` makes a not-rated cell write its value (muted where `Style.Muted` clears 4.5:1), sub-label, note and reason last; with `FitHeight` a heatmap's rows grow to the tallest cell's words (12 a line, 13 for a value, plus 4, at least 36). Inside, one block writer (`CellBlock`, measured by `CellNeed` for `FittedHeight`) replaces `CellWords`/`CellReason`. Built subagent-driven (5 tasks, Opus task reviews, fix rounds on Tasks 2 and 5, an Opus whole-branch review that rendered 10,000 random heatmaps byte-identical to 0.46.1 without `FitHeight`, one final fix round). Accepted limits: a fitted reason below `CellWidth` ~43 loses words, and without `FitHeight` `NotRatedKeepsValue` can show less in a short fixed row. Spec `docs/superpowers/specs/2026-10-09-heatmap-cells-design.md` (amended twice), plan `docs/superpowers/plans/2026-10-09-heatmap-cells.md`. Before it: v0.46.1, *heatmap follow-ups* (commit c7429e0, tag v0.46.1): the ten gaps Race Face found in 0.46.0 — `ValueNote` up to 40 characters on heatmap cells; `GapLabel` on an empty heatmap cell ("did not race", drawn dashed and unshaded, column kept); with `CellText` a not-rated cell writes its reason (wrapped by `CellReason`) instead of its value; refined `YMin`/`YMax`/`IncludeZero` pin the colour scale's ends exactly; `ChartSpec.ColumnLabelsOnTop`; a refined name column measured from the names (130–240; classic keeps 130 and the 17-character cut); `FitHeight` on heatmaps (36 a row, 240 floor); `<LumenChart>` freezes a `CellWidth` heatmap's row names (`ChartSvg.HeatmapNameBand` in a sticky `.lumen-freeze` layer that takes the pointer, covers the label row, with scroll padding); `ShowDataButton`; `HtmlTable` cells read value · sub-label · note; `.lumen-table` margins only inside the component. Built subagent-driven (6 tasks, 6 fix rounds, an Opus whole-branch review whose probe found ~2,400 random classic heatmaps byte-identical to 0.46.0). Spec `docs/superpowers/specs/2026-10-08-heatmap-followups-design.md` (amended four times), plan `docs/superpowers/plans/2026-10-08-heatmap-followups.md`. Before it: v0.46.0 *heatmap tables* (commit 9f0c7a8; Race Face #16): `ChartSpec.CellText` writes each heatmap cell's value and per-cell `SubLabel` (style text or background where either clears 4.5:1, else black or white; sub-label dropped first, then value, when they don't fit), `ChartPoint.NotRated` draws an unshaded dashed cell left out of the colour scale and named `…, not rated: {words}`, `ChartSpec.CellWidth` fixes each column's width (165 + columns × width, 320–4096) and `<LumenChart FitWidth>` shows such a heatmap at exactly that width and scrolls it (`lumen-fixed`, `--lumen-drawn`), `ChartExport.HtmlTable` gives a real grid table (and is "View data" for heatmaps), CSV gains `NotRated`; heatmaps take `YFormat`/`YUnit`. Refined finish only: the colour scale reads `{min} low to {max} high` (then `{min} – {max}`, then cut) and Dark gets its own ramp in the renderer (`#22304A`→`#6E9BFF`; the preset is unchanged). Every chart's open data table is now a focusable region named "Chart data". Spec `docs/superpowers/specs/2026-10-07-heatmap-table-design.md` (amended three times during the build), plan `docs/superpowers/plans/2026-10-07-heatmap-table.md`. Before it: v0.45.0 *the places and points chart* (commit 3a0974d; owner's request via Race Face, 6 Oct 2026): `PlacingsChart.Build(results, options)` in Lumen.Charts (`src/Lumen.Charts/Placings.cs`: `Placing`, `PlacingsOptions`) returns an ordinary two-pane `ChartSpec` — places reversed, first at the top, one joined line per series judged only within the series, change words in every name, "/field" notes, points in a pane (an unscored race of a scoring series is a gap; a never-scoring series is left out), default colours from the palette minus anything close to Rising/Falling, `XTicks = PointLabels` — or null when nothing is placed; `<LumenPlacings>` (Lumen.Charts.Blazor) draws it interactive, static or an `Empty` slot, with `Adjust` as the host's last word. Spec `docs/superpowers/specs/2026-10-06-placings-chart-design.md` (amended during the build), plan `docs/superpowers/plans/2026-10-06-placings-chart.md`. Then v0.44.0 the interactive season planner `<LumenPlanner>`, v0.43.0 the static season planner, v0.42.0 words at a missing value (#15), v0.41.0 *fits a card*, v0.40.0 gradient columns and sub-labels (#11, #13), v0.39.0 strips, tracks and undrawn titles (#12, #14), v0.38.0 set ticks, units and end labels (#9, #10), v0.37.0 ride channels (#7), v0.36.0 arrow keys and the shared readout (#8). GitHub: https://github.com/jtheyse/lumen-charts — public, MIT. Every release carries the three `.nupkg` files.
- **Next:** **Race Face's report on 0.46.2.** The swap message went to "RACEFACE RUNNING EXPANSION 2" on 9 Oct 2026: it is rebuilding #16 on today's Race Face main as branch `lumen/0.46.2-heatmap` with `CellNotes`, `NotRatedKeepsValue` and `FitHeight`, checking each cell against the hand-built table at 360 px, and will report back; its owner merges only when each cell shows at least what the old table did. Record its findings in the brain task "Race Face feedback on 0.46.0 (#16 category heatmap)". Then **0.47.0, Race Face #18 "Registrations per day"** with the #20 course-profile recipe: both design sections are approved (brain decisions `Columns.FixedPitch.Design` and `Columns.DailyCounts.Design`), and **the spec, `docs/superpowers/specs/2026-10-09-registrations-per-day-design.md` (9c13a7b), awaits the owner's review**; it was written before 0.46.2 jumped the queue. Note in it: the #20 recipe draws a line, not an area, because an area refuses `YMinSpan`. On the owner's yes: the plan, a subagent-driven build, the release. Follow-ups waiting for the owner's word: `<LumenChart>` draws its series legend on heatmaps (four swatch rows on the gallery's Category card that carry nothing; `LumenChart.razor:15` skips only strips); 0.46.2's two accepted limits; 0.45.1 (parked). #17 stays HTML; #19 waits for the owner's word on a raster package. Other sessions left unmerged branches `fix/null-title-source`, `fix/null-graph-title` and `fix/null-description` (the brain's `ChartSpec.NullText.Policy`), and a worktree `.claude/worktrees/lumen-interaction-state` whose build will have to merge on top of 0.46.1's `<LumenChart>` changes: the owner's call. Race Face's own planner page needs its own spec with the owner. Still open for the owner: the band-label contrast fix would move renderings in Classic too. Start with `git status`: uncommitted changes to `src/`, `samples/`, `tests/`, the docs or the skill are a delegated agent's unreleased work.
- **Counts at v0.46.2:** 724 unit assertions, 300 HTTP checks, 112 browser checks on the gallery (62 on the WebAssembly host), 402 hashed renderings, 39 recipe charts. Release build at 0 warnings.
- **NuGet:** not published. The owner chose "skip nuget for now"; packages ship as GitHub release assets, and the Claude Code skill's install scripts download them into a local feed.

## The map

| Path | What it is |
|---|---|
| `src/Lumen.Charts` | Core: `ChartSpec` → SVG (`ChartSvg.Render`), CSV, validation, axes, statistics, training metrics. net8.0. The season planner (0.43.0): `Planner.cs` (public types), `PlannerValidation.cs` (refusals and caps), `PlannerCalendar.cs` (months, filters, long weekends, lanes), `PlannerSvg.cs` (year, month, day, narrow layouts, table). |
| `src/Lumen.Charts.Blazor` | `<LumenChart>`, `<LumenGraph>`, `<LumenBrand>`, `<LumenPlanner>` (0.44.0), `<LumenPlacings>` (0.45.0); `wwwroot/lumen.js` (the planner's script is `attachPlanner`/`plannerDrawn`), `lumen.css`. |
| `src/Lumen.Charts.AspNetCore` | `app.MapLumenCharts()` HTTP API. |
| `samples/Lumen.Gallery` | Interactive Server gallery: `/` chart explorer, `/sports` Sports & performance dashboard (`SportsData.cs`, one simulated athlete). The home page's `#planner` section draws an invented season (`PlannerData.cs`) at three widths picked by container query. |
| `samples/Lumen.Wasm` | Standalone WebAssembly host (outside the solution; needs the `wasm-tools` workload). |
| `tests/Lumen.Charts.Tests` | Executable assertion suite (`Test`/`Check`/`Reject`), `dotnet run`. |
| `tests/verify-api.ps1` | HTTP checks against a running gallery on port 5188. |
| `tests/Lumen.Charts.BrowserTests` | Playwright suite (gallery on 5188, WebAssembly host on 5199), with axe sweeps in light, dark and Midnight. |
| `tests/Lumen.Charts.Baseline` | The rendering-hash harness and the v0.46.2 reference hashes (402 rows). See its README. |
| `tests/Lumen.Charts.Recipes` | Compiles every recipe in the skill's `sports.md` and `recipes-race-face.md` together, as written, and renders each chart (`python check.py`, then `dotnet run -c Release`). See its README. |
| `integrations/claude-code/lumen-charts` | The Claude Code skill: `SKILL.md`, `references/` (API, sports recipes, HTTP), `scripts/install.sh` and `install.ps1`. |
| `docs/FITNESS.md` | The sports-charts research and the eight-step build order this work has followed. |
| `docs/RESEARCH.md` | The original roadmap. `docs/VERIFICATION.md` the verification record. `docs/PERFORMANCE.md` measured cost. |
| `.github/workflows/verify.yml` | CI on push and on tags: build, unit, HTTP, browser; WebAssembly build. Runs on **Linux**. |
| `.brain` | Points THEBRAIN at the `@LumenCharts` brain. |

## The Race Face program

The owner's other product, Race Face (a race-results app for riders who are mostly children), is moving its ~20 hand-built charts onto Lumen. The owner's brief is `lumen-charts-race-face-brief.md` in the repository root. It is **untracked and must stay so**: commit with `git add -u` or explicit paths, never `git add .`. Work its charts in its priority order (P1 #1–#6 first), each as the smallest general feature any app would use, never a Race Face special case, with a recipe in `integrations/claude-code/lumen-charts/references/recipes-race-face.md` and a gallery entry. **Invented data only**: no real rider's name, number, age or results in the repository, tests, samples, release notes or the brain; scan a release's diff for anything from the owner's screenshots before committing. **Never colour alone**: a state shown by colour also reaches the tooltip and accessible name. **Drawn text clears 4.5:1.**

Where it stands (4 October 2026):

| Race Face chart | Lumen release | State in Race Face |
|---|---|---|
| #1 position & points, #2 season by round | 0.33.0 | live |
| #3 season strip | stays HTML (its ▲/▼ rule is in the recipe) | — |
| #4 PB sparklines, #6 growth sparklines | 0.34.0 | live (production deployed 4 Oct 2026, 20:05, from b20c3359 on Lumen 0.39) |
| #5 finish-time histogram | 0.35.0 | live (same deploy) |
| #8 fitness & form | 0.36.0 | live (same deploy) |
| #7 ride channels | 0.37.0 | live (same deploy) |
| #9 season arc, #10 gap to the leader | 0.38.0 | live (same deploy) |
| #12 effort zones, #14 score bars | 0.39.0 | live (same deploy) |
| #11 best efforts, #13 heart rate per lap | 0.40.0 | live on production API and Web (c39bfa83, 4 Oct 2026 22:14, Lumen 0.42) and Perform's FitLab (894f498f, Lumen 0.42) |
| #15 team rider share | 0.42.0 | live on production Web (c39bfa83, 4 Oct 2026 22:14) |
| Season planner (owner's request, 5 Oct 2026) | 0.43.0 static; 0.44.0 interactive | not started in Race Face (needs its own spec) |
| Position & points by race, reusable (owner's request, 6 Oct 2026) | 0.45.0 (`PlacingsChart`, `<LumenPlacings>`) | Race Face to switch `PositionPointsChart.razor` to it |
| #16 category heatmap | 0.46.0 (`CellText`, `NotRated`, `CellWidth`, `ChartExport.HtmlTable`); 0.46.1 follow-ups; 0.46.2 notes in cells and kept values | being rebuilt on branch lumen/0.46.2-heatmap (not merged) after 0.46.2 (9 Oct 2026) closed gaps A–C; its report awaited |
| #18 registrations per day, #20 course profile | 0.47.0 (designing) | — |
| #17 round strips | stays HTML | — |
| #19 PDF/PNG | waits for the owner's word on a raster package | — |
Last confirmed production (5 Oct 2026): API and Web c39bfa83 and Perform 894f498f, all on Lumen 0.42; Web has been deployed again since by other sessions. Race Face's local master moved to Lumen 0.43.0 at 63391ce2 (package-only, not pushed or deployed). Perform consumes the shared `RaceSense.Components` project, so it moves with every Lumen bump; deploying it stays the owner's call.

**After each release** the owner wants the Claude session **"RACEFACE RUNNING EXPANSION 2"** told to upgrade: `SendMessage` to that name (check `ListAgents` first). The message gives the release URL, what it adds for which charts, and steps:
1. Refresh the skill: `git -C "D:/CHATGPT/.NET GRAPH API" archive vX.Y.Z integrations/claude-code/lumen-charts | tar -x --strip-components=2 -C ~/.claude/skills`.
2. Run the skill's `scripts/install.ps1` from the Race Face root.
3. Bump the `Lumen.Charts*` packages and delete the old `.nupkg` files.
4. Swap the charts the release covers, per `recipes-race-face.md`.
5. Check them at 340–360 px, static and interactive.
6. Commit `local-packages/` and `nuget.config`.
7. Report back.

That session replies with what changed and where the recipes missed real data. Record its findings in the brain and fold them into the next release.

Race Face feedback still open (the brain's "Race Face feedback" tasks hold the detail):
- **keys (0.36.0)**: an interactive chart has two tab stops, the scrolling viewport and the roving point (the docs say so from 0.37.0); consider dropping the viewport's stop when nothing scrolls. Static `Render` output keeps one stop per mark (363 for a 90-day chart);
- **end labels (0.38.0)**: real surnames with a gap note run past the margin at 320 px; since 0.40.0 the note is cut before the name. A second line for the note, or a wider margin when only one or two labels would be cut, is still open;
- **annotation labels (0.41.0)**: a reference's label ("CP: 255") can sit on markers near the line's end at 340, since annotation labels don't avoid marks;
- **height floor (0.42.0)**: a small fixed-axis line chart with its titles undrawn (220 tall) is refused by the 240 floor; consider a lower floor for line charts with `DrawTitles = false`;
- **legend**: short entries stack one per row in the static legend (four rows, ~90 px under a 300 px chart at 340); lay short names out in one row. Long series names are truncated ("…MTB Le…"); consider wrapping them;
- **recipe and doc notes**:
  - use the app's own PB flag and bins where it has them;
  - a sparkline's root is `role='group'` named by title and description;
  - Lumen writes single-quoted attributes;
  - put a description's key fact first, since the two-line cut can hide its end;
  - per-lap heart rate arrives with 0 for a lap without one: map it to null (a gap); past four laps write the sub-label's number alone; an app that stores its own power-curve durations should draw those, not `MeanMaximal` (0.41.0 feedback);
  - team rider: a round ridden but not rateable (no field size, or a place past the field) is a plain null gap with no `GapLabel`, so "absent" never says a child didn't ride; a share that arrives as 0–1 is scaled ×100; host CSS that targets `svg text[font-size]` also restyles Lumen's text (0.42.0 feedback);
  - the 1080×1350 card needs an SVG rasteriser on the server, which Lumen does not ship (Race Face's API draws its PNGs with ImageSharp).

## How a release is done

The owner drives with "continue". Each release is one feature, built by a delegated agent and verified independently before it ships.

1. **Find the facts first.** Before designing, send a read-only Explore agent to report how the code does the relevant things today (file:line), then **brief a builder** (general-purpose, background) with decided design rules, not open questions. Every brief carries:
   - no git commands at all (not even `git status`); no packing;
   - kill only `Lumen.Gallery.exe` by image name, and everything else by PID;
   - subagents cannot write report files, so the report comes back as text;
   - delete any `.playwright-mcp` folder;
   - keep the skill's frontmatter description ≤ 1024 characters with no unquoted `: `;
   - new public members need XML doc comments (the build fails without them);
   - new spec properties stay out of the gradient-ID hash at their defaults;
   - never colour alone; drawn text clears 4.5:1; invented data only;
   - save screenshots outside the repo and list them.

   If the builder's report shows a defect (it has three times: colour alone, label contrast, a graph shrunk instead of scrolled), send it back with `SendMessage` to its id — it keeps its context.
2. **Verify it yourself** — never ship on the agent's word:
   - `taskkill //IM Lumen.Gallery.exe //F` (it locks the build output), then `dotnet build Lumen.Charts.slnx -c Release` → 0 warnings.
   - `dotnet run --project tests/Lumen.Charts.Tests -c Release`.
   - Rendering: run `tests/Lumen.Charts.Baseline` in both finishes and compare with the previous release's hashes — only added rows are allowed unless the release is meant to change a look, in which case the classic finish must still match.
   - Start the gallery (`dotnet run --project samples/Lumen.Gallery -c Release --no-build --urls http://localhost:5188`), run `pwsh -File tests/verify-api.ps1 -BaseUrl http://localhost:5188` and `dotnet run --project tests/Lumen.Charts.BrowserTests -c Release`.
   - When the component, its CSS or its script changed, also run the WebAssembly host: `dotnet build samples/Lumen.Wasm -c Release`, `dotnet run --project samples/Lumen.Wasm -c Release --no-build --urls http://localhost:5199`, then the browser suite with the argument `http://localhost:5199`. Stop it by PID (`netstat -ano` → the PID listening on 5199 → `taskkill //PID <pid> //F`).
   - Look at the new charts. For phone widths use Playwright with a real viewport; headless Edge with `--window-size` will not go below its minimum width and gives misleading screenshots.
   - Compile every recipe **together, as written**: `tests/Lumen.Charts.Recipes` (`python check.py`, then `dotnet run -c Release`). This has caught recipes that reused one another's variable names.
   - Read every new public file in full before committing (the repository is public), and scan the diff for real data from the owner's screenshots.
3. **Pack** the three projects into `artifacts/packages`, install them into a fresh console app from a local `nuget.config`, and render something with the new feature. If a version was packed before, clear `~/.nuget/packages/lumen.charts*/<version>` first.
4. **Commit** (explicit paths if anything unrelated is in the tree), push, and wait for CI with `gh run watch`. CI is Linux: it catches platform assumptions (CSV newlines, maths-library rounding).
5. **Tag** the verified commit (`git tag -a vX.Y.Z <sha>`), push the tag, `gh release create` with the three `.nupkg` files and honest notes (what is new, worth knowing, verification).
6. **Package the skill** from the released commit (`git archive <sha> integrations/claude-code/lumen-charts`) with the skill-creator's `package_skill` and send it to the owner.

If an agent stops on an API rate limit, or because the session ended ("didn't finish before the previous session ended"), first check its worktree (`git log` from the task's base, `git status` for uncommitted edits), then resume it with `SendMessage` to its id, telling it the exact state and to commit in steps (it keeps its context). If the owner stopped it, it cannot be resumed — finish its work yourself. Repeat in every reviewer dispatch that may start a server: stop it by PID, never by image name (a 0.46.1 reviewer ran `taskkill /IM python.exe`).

The owner follows each release on a private Artifact, "Lumen Release Progress" (https://claude.ai/artifact/4N4wZFXgauRDfeDsz7ceDj). It reads the db document `release/current`; update it at each milestone with `ArtifactData set` from a local JSON state file (steps with state and weight, a "now" panel, a log), pinning `if_version` (last written: 24, 0.46.2 released). `release/v0461` keeps 0.46.1's record and `release/v047` 0.47.0's state as it stood when 0.46.2 jumped the queue: restore it into `release/current` when 0.47.0 resumes. The brain procedure "The owner's release progress page and running SDD across session restarts" has the details.

## Gotchas that have bitten

- `Lumen.Gallery.exe` locks the build: kill it first.
- Bash heredocs mangle backslashes (`\\` → `\`, `\r\n` → a real newline) in patch scripts. Write scripts with the Write tool and run the file.
- `Training.razor` hides `Lumen.Charts.Training` (CS0117): name a page `TrainingPage.razor`.
- Annotation labels print their value (`Target: 55`).
- A `nuget.config` inside a web project folder is published with it; put it at the repository root.
- Charts keep a 640 px minimum width unless `FitWidth="true"` (0.25.0) or the container lifts it.
- Hashes are platform-sensitive in the last decimal; the classic-equals-0.23.0 unit test runs on Windows only.
- Playwright MCP writes screenshots and logs only inside the repo (`.playwright-mcp`); copy them out and delete the folder.
- Windows refuses deep worktree paths: use short ones such as `D:/CHATGPT/wt…`, and remove with `Remove-Item -LiteralPath '\\?\<path>' -Recurse -Force`.
- The skill's frontmatter description has a hard 1024-character limit and is YAML: no unquoted `: `.
- `tests/Lumen.Charts.BrowserTests` and `samples/Lumen.Wasm` are outside the solution: building `Lumen.Charts.slnx` does not rebuild them, and `dotnet run --no-build` then runs a stale binary. Build both explicitly before running them.
- `sed -i` in Git Bash rewrites CRLF files as LF; `.gitattributes` is `eol=lf`, so commits are unaffected, but edit with the Edit tool or a Python script where line endings matter.
- `git add -u` stages tracked files only: a release's **new** files (a new recipe file, a new test folder) must be added by explicit path, and the untracked Race Face brief must never be.
- `brain_recall` cuts every item to about 100–160 characters, so a long procedure such as a release design cannot be read back whole. To read one in full, pull the `brain_remember` call out of the session transcript that wrote it (`~/.claude/projects/D--CHATGPT--NET-GRAPH-API/<session>.jsonl`), with a short Python script that matches its title.
- Axe sweeps do not check SVG text contrast: a value label at 4.12:1 passed them. Check drawn text against 4.5:1 yourself.
- A fitted drawing's `viewBox` is not what the reader sees: compare it with the SVG's rendered width (`getBoundingClientRect().width`) and the box's `scrollWidth`, which caught a graph scaled down where it should scroll. Resizing the Playwright MCP page to 375 keeps a desktop scrollbar (a 360-pixel page, a 322-pixel card); for a phone, open a context with `isMobile` and `hasTouch` through `browser_run_code_unsafe`.

## Decisions worth knowing before changing them

- **SVG renderer**, not Canvas or WebGL: measured cost does not justify a second renderer, and SVG keeps accessibility, exports and server rendering (`docs/PERFORMANCE.md`).
- **Refined finish by default** since 0.24.0 (thin non-scaling strokes, markers on hover or focus, dotted grid) because the owner found the old look "painted"; `ChartFinish.Classic` reproduces 0.23.0's charts byte for byte. Layout fixes apply in both finishes: 0.31.0 moved network graphs in classic too, rather than keep their labels colliding.
- **Fit in the space the chart draws**: trend lines and densities are computed in the space the chart draws in, so they stay straight on log axes and continuous across skipped weekends.
- **Training metrics follow published sources only** (Allen & Coggan, TrainingPeaks); no vendor scores, hrTSS, rTSS, TRIMP, grade-adjusted pace or W′ balance without a source.
- **Gradient IDs** are a hash of the chart's spec; every new spec property is left out of that hash at its default so existing IDs never move.
- **Never colour alone** (WCAG 1.4.1): change colours, highlights, status dots and a highlighted bin all say their state in the tooltip and accessible name too (`ValueNote`, change words). Verification turned back a 0.32.0 gallery chart that broke this.
- **Value labels clear 4.5:1** (0.33.0): a label takes its point's colour only where that colour clears 4.5:1 against the background, and the text colour otherwise.
- **Interactivity never changes the static SVG**: keyboard behaviour and readouts live in the component and its script, so `ChartSvg.Render` output, which servers, PDFs and static pages use, stays complete and byte-stable.

## What is left

- Blocks (0.29.0) leave out sloped ramps (a ramp is drawn at its average), stacked blocks and text on a block; reference lines sit behind blocks, as behind columns, so a lap chart's average line is hidden behind faster laps while its label stays on top.
- Graph labels after 0.31.0: an edge keeps out of its *own* nodes' labels, but a long chord or a bend can still cross *another* node's label (the crowded test graph shows it); an edge label avoids nodes, node labels and other edge labels but not other edges' lines (on the crowded circle "nightly" sits on the "live" edge); where no place is free a label falls back to the middle of its edge and may overlap.
- `GraphEngine.Fit` ignores edge labels. When a layered graph fits neither direction it turns top to bottom at its narrowest width, even where left to right would need less; it never grows a left-to-right graph's height for a full level.
- No gallery page shows a calendar without `YZones` (the 0.30.0 ramp); its tiers are what the explorer and the Sports & performance page draw.
- Screen-reader conformance (NVDA, JAWS, VoiceOver) — needs a person with the hardware; never run.
- On the default Light and Dark presets `Rising` and `Falling` are the same colours as palette series 2 and 5, so a chart using `ChangeColors` should pick its other series' colours to avoid them (the gallery's race points are grey). X-axis tick labels are not moved in from the plot's edges, as value labels are: index charts set `XMin = -0.5` and `XMax = count - 0.5`. The component's status line shows a point's note but not its change words (the mark's name and tooltip carry them). Lumen keeps a fixed bottom margin, which leaves empty space under a short chart's axis at phone sizes.
- Sparklines (0.34.0) refuse panes, value labels, bands, ranges and blocks; at 60×16 a `HighlightLast` ring is clipped at the edge. `YMinSpan` applies to the left-hand axis only.
- Range-annotation labels (an X annotation with `To`, drawn as a band) are written in the muted colour over the band fill, 4.24:1 in Light. This predates 0.36.0. The gallery's *Planned* band keeps its label undrawn until the library is fixed; the fix moves hash rows, and Classic's byte-for-byte promise needs a decision first.
- Season planner (0.43.0): view only; render `Width` equal to the box's CSS width or the 10–12 unit text shrinks; caps of 400 days, 2000 events, 1000 periods, 500 regions, 200 characters in a title, description or name and 120 in a note, but no length caps on an event's Id, Code, Category, Audience or Url; at most three stripes a day in the year, then "+N" naming at most 20 hidden events ("and N more"); a narrow month agenda lists at most 20 events a day; the narrow year draws no school bands; static SVG gives a cross-month event one tab stop per month; Relevance is the host's call, Lumen never computes clashes; cancelled events never count as clash or close.
- Planner component (0.44.0): a view the host sets outside the period throws `ArgumentException` (a Blazor Server circuit ends); the Blazor package calls Lumen.Charts' internal `PlannerCalendar.Name`, so the packages must be released together; on a 375 px phone the toolbar and chips take about 450 px above the drawing (`ShowFilters="false"` is the way out); after a key or click C# answers without a redraw, the next unrelated redraw may scroll to the reader's own focused cell; a chip in a day view moves the roving stop back to the first event; screen-reader conformance of the focusable `role=group` days is untested by a person.
- Heatmap cells (0.46.2): a fitted reason below `CellWidth` ~43 still loses words (0.46.1's wrap: `too` / `few…` at 34–43, `fe…` at 32–33, `t…` at 24); without `FitHeight` the drop order never adds a line back, so `NotRatedKeepsValue` can show less in a short fixed row (a 40-unit row writes only the two reason lines); a fitted heatmap's height is not capped (`Height`'s 240–2160 is checked before fitting); rows are one height, so one long note makes every row tall. Deferred test minors are in the brain fact "Known limits, rulings and loose ends after 0.46.2".
- Heatmap follow-ups (0.46.1): static SVG does not freeze row names; with `PaintBackground = false` the frozen layer is still an opaque background patch; with narrow columns (`CellWidth` ~24–60) the band clips the first label's overhang at rest; under keyboard `:focus-visible` a 2 px strip at the layer's left lets the pointer through; a reason whose first word is wider than a line is cut and ends there; `FitHeight` rows are 36 except at the 240 floor; the gallery's half Category heatmap card scrolls at 1400 px (owner may make it Wide); `svg:not(.lumen-chart svg)` needs Selectors Level 4. Deferred minors and two browser flakes (the Midnight axe sweep's `Hovered()`, and once the 1280 tab-stop test on a freshly started gallery) are in the brain fact "Known limits and loose ends after 0.46.1".
- Heatmap tables (0.46.0): at the narrowest widths a colour scale with six-digit values and a unit still cuts its top value (the names and grid table carry it); two charts with their data open both name their region "Chart data"; a heatmap cell's own `stroke` beats the `.lumen-datum:focus` ring (true for rated cells before 0.46.0, and now for dashed not-rated cells); the dark ramp's low end is 1.26:1 against Dark's background (the spec asks for it close), so the dashes and words, not the fill, tell a not-rated cell from the lowest rated one; a lone value or equal values pad the scale's ends as `LinearScale` does; no HTTP endpoint for `HtmlTable` and no sub-label column in CSV; a null element inside a series' `Points` still throws a `NullReferenceException` in the component (a null `Points` now gives the validation message).
- Places and points (0.45.0): a host re-render rebuilds the chart and `<LumenChart>` resets a drag-zoom; `Static` draws at the spec's width (340); an `Adjust` returning null shows `Empty`; change colours draw the lines while the legend shows series colours; more than 32 lines is refused; a host that sets `XTickLabels = Bounds` on the returned spec is refused (the spec uses `PointLabels`); on a 375 px phone the gallery card's long title is cut and two of six dates show; verify-api counts `lumen-value` page-wide on /sports (20), coupling the race-results and places checks.
- Gap labels (0.42.0): one line of at most 12 characters, only at the foot of the plot (the top on a reversed axis) whatever the values nearby; colliding words are thinned, never moved (two missed rounds side by side at a phone's width write the first word only, the second keeping an invisible 10-unit mark at its X); CSV leaves the value empty; a gap-labelled X becomes a shared-readout column; the WebAssembly host has no gap-label chart. While a drawing fits, the component's viewport is `tabindex=-1` (click-focusable, not a Tab stop), and a page without the script keeps the viewport's stop from the prerendered markup.
- Fits a card (0.41.0): `FitHeight` is for horizontal bars only, with a fixed row pitch; `PaintBackground` can't detect the surface (set `Style.Background` to it); `AverageOf` is words only, and Lumen's own averaging wording wins where it averaged.
- Lap columns (0.40.0): column charts without sub-labels still thin names one per 65 units (four columns at 340 show two names); columns stand on zero, so a gradient's first stop belongs at 0; gradient stops have no contrast check; sub-labels are cut past 12 characters under columns and don't reach CSV.
- Strips and meters (0.39.0): a strip holds one whole, at most 24 parts, an 18-unit bar with no setting; tracks run 0 to `YMax` with no target tick; at the 240-unit floor three or four meters leave large row gaps; `DrawTitles` shifts the body a fixed 50 units and graphs always draw their title.
- End labels (0.38.0): about 10 characters fit at 340 px; the lowest labels drop from the drawing when even tight rows don't fit; they don't avoid value or reference labels inside the plot; refused beside a right-hand axis. Hand-set ticks aren't thinned. Histogram, box and violin take neither set ticks nor a unit.
- The Sports & performance page is heavy (27 charts, a ~1 MB ride chart): CI's Midnight redraw passed 15 s, so the browser test waits a minute there (0.38.0).
- Ride channels (0.37.0): Y stays fitted to the whole series when zoomed, and host-built headers keep the whole series' numbers; six channels at 600 points make about 1 MB of SVG, resent by Blazor Server on each redraw; averages carry no change words; drag-to-zoom is mouse and pen only.
- The shared readout (0.36.0) covers continuous-X kinds only. Close values' rings overlap; on a phone the tooltip can cover the guide; static SVG keeps one tab stop per mark.
- Charts keep a 240 px height floor (60×16 for sparklines); the docs say so from 0.35.0.
- From the original roadmap: irregular tick placement; graph work (orthogonal routing, force layout, overlap removal, edge bundling).
- NuGet publication, when the owner wants it (Trusted Publishing was explored and set aside).

## Working with the owner

The owner (GitHub `jtheyse`) steers with short messages — mostly "continue" — and wants the work delegated to agents, verified, and released one feature at a time with honest notes. They judge the look by eye: show screenshots of new charts. Ask before anything irreversible or outward-facing beyond the established release flow; messaging the Race Face session after a release is part of that flow (the owner asked for it).

The owner switches a "jev-router" hook on and off with `/jev-router on|off`. While it is **on**, its hook asks for routing each job to a `jev-*` helper agent; follow it then. While it is **off**, the hook can still fire, and its instructions are stale: ignore them. It was on when this was written (9 October 2026). It also fires on subagents' hand-backs; during a subagent-driven build the controller keeps dispatching, fix rounds and the ledger itself (a helper cannot resume the build's agents) and routes only real decisions, such as triaging a review's findings, to a `jev-*` helper.
