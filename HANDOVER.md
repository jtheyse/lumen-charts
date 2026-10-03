# Handover

The state of Lumen.Charts and how work on it is done, for whoever picks it up next — a person or a new Claude Code session. Updated 3 October 2026, night. The THEBRAIN brain `@LumenCharts` holds the same state plus every design in detail: recall it first (`brain_recall`, intent `current_state`).

## Where things stand

- **Released:** v0.36.0, *reading a chart day by day* (Race Face chart #8): every `<LumenChart>` is one tab stop whose arrow keys walk the points; `ChartSpec.SharedReadout` reads every series at one X through all panes (component only; `ChartSvg.Readout` gives hosts the table); `YSymmetric` on a chart or pane; `ValueFormat.Signed`. Before it, v0.35.0 brought how the field finished and text that fits, v0.34.0 sparklines and v0.33.0 race results on a line. GitHub: https://github.com/jtheyse/lumen-charts — public, MIT. Every release carries the three `.nupkg` files.
- **Next:** **0.37.0, ride channels** (Race Face #7), from the brain's task "Race Face P2 plan (charts #7–#14)". The cross-pane readout it planned already shipped in 0.36.0, so what is left is: per-pane auto scale without labels, bucket-average sampling beside MinMax, `XFormat` Duration for time into a ride, and perhaps a readout shared across a group of charts. Run an Explore pass first, since that plan has not been checked against the code. Start with `git status`: uncommitted changes to `src/`, `samples/`, `tests/`, the docs or the skill are a delegated agent's unreleased work. Also open: the library's range-annotation labels fall under 4.5:1 on their band (the brain task "Range-annotation labels fail 4.5:1 on their band").
- **Counts at v0.36.0:** 490 unit assertions, 213 HTTP checks, 55 browser checks on the gallery (27 on the WebAssembly host), 325 hashed renderings, 28 recipe charts. Release build at 0 warnings.
- **NuGet:** not published. The owner chose "skip nuget for now"; packages ship as GitHub release assets, and the Claude Code skill's install scripts download them into a local feed.

## The map

| Path | What it is |
|---|---|
| `src/Lumen.Charts` | Core: `ChartSpec` → SVG (`ChartSvg.Render`), CSV, validation, axes, statistics, training metrics. net8.0. |
| `src/Lumen.Charts.Blazor` | `<LumenChart>`, `<LumenGraph>`, `<LumenBrand>`; `wwwroot/lumen.js`, `lumen.css`. |
| `src/Lumen.Charts.AspNetCore` | `app.MapLumenCharts()` HTTP API. |
| `samples/Lumen.Gallery` | Interactive Server gallery: `/` chart explorer, `/sports` Sports & performance dashboard (`SportsData.cs`, one simulated athlete). |
| `samples/Lumen.Wasm` | Standalone WebAssembly host (outside the solution; needs the `wasm-tools` workload). |
| `tests/Lumen.Charts.Tests` | Executable assertion suite (`Test`/`Check`/`Reject`), `dotnet run`. |
| `tests/verify-api.ps1` | HTTP checks against a running gallery on port 5188. |
| `tests/Lumen.Charts.BrowserTests` | Playwright suite (gallery on 5188, WebAssembly host on 5199), with axe sweeps in light, dark and Midnight. |
| `tests/Lumen.Charts.Baseline` | The rendering-hash harness and the v0.36.0 reference hashes (325 rows). See its README. |
| `tests/Lumen.Charts.Recipes` | Compiles every recipe in the skill's `sports.md` and `recipes-race-face.md` together, as written, and renders each chart (`python check.py`, then `dotnet run -c Release`). See its README. |
| `integrations/claude-code/lumen-charts` | The Claude Code skill: `SKILL.md`, `references/` (API, sports recipes, HTTP), `scripts/install.sh` and `install.ps1`. |
| `docs/FITNESS.md` | The sports-charts research and the eight-step build order this work has followed. |
| `docs/RESEARCH.md` | The original roadmap. `docs/VERIFICATION.md` the verification record. `docs/PERFORMANCE.md` measured cost. |
| `.github/workflows/verify.yml` | CI on push and on tags: build, unit, HTTP, browser; WebAssembly build. Runs on **Linux**. |
| `.brain` | Points THEBRAIN at the `@LumenCharts` brain. |

## The Race Face program

The owner's other product, Race Face (a race-results app for riders who are mostly children), is moving its ~20 hand-built charts onto Lumen. The owner's brief is `lumen-charts-race-face-brief.md` in the repository root. It is **untracked and must stay so**: commit with `git add -u` or explicit paths, never `git add .`. Work its charts in its priority order (P1 #1–#6 first), each as the smallest general feature any app would use, never a Race Face special case, with a recipe in `integrations/claude-code/lumen-charts/references/recipes-race-face.md` and a gallery entry. **Invented data only**: no real rider's name, number, age or results in the repository, tests, samples, release notes or the brain; scan a release's diff for anything from the owner's screenshots before committing. **Never colour alone**: a state shown by colour also reaches the tooltip and accessible name. **Drawn text clears 4.5:1.**

Where it stands (3 October 2026):

| Race Face chart | Lumen release | State in Race Face |
|---|---|---|
| #1 position & points, #2 season by round | 0.33.0 | live |
| #3 season strip | stays HTML (its ▲/▼ rule is in the recipe) | — |
| #4 PB sparklines, #6 growth sparklines | 0.34.0 | committed in RaceSenseNet, not yet deployed (the owner deploys) |
| #5 finish-time histogram | 0.35.0 | committed in RaceSenseNet (5f69ee7d), not yet deployed |
| #8 fitness & form | 0.36.0 | committed in RaceSenseNet (e24e7d1d), not yet deployed |
| #7, #9–#14 (P2) | 0.37.0–0.40.0, planned in the brain (task "Race Face P2 plan") | — |
| #15–#20 (P3) | after P2 | — |

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
- **features**:
  - a way to leave a chart's background unpainted, for cards on another surface colour (contrast is still checked against `Background`);
  - a way to keep the title, and perhaps the description, as the accessible name without drawing it, for pages that own their heading (and docs saying exactly what `Render`'s `includeTitles` covers);
  - a `<LumenChart>` parameter to hide the toolbar (and perhaps the status line) on small phone cards (0.36.0 feedback);
- **keys (0.36.0)**: an interactive chart has two tab stops, the scrolling viewport and the roving point, while the docs say one; fix the wording or drop the viewport's stop when nothing scrolls. Static `Render` output keeps one stop per mark (363 for a 90-day chart);
- **race lines** carry each race's own name, and the longest names get the least room when labels step down or drop;
- **legend**: long series names are truncated ("…MTB Le…"); consider wrapping them;
- **recipe and doc notes**:
  - use the app's own PB flag and bins where it has them;
  - a sparkline's root is `role='group'` named by title and description;
  - Lumen writes single-quoted attributes;
  - put a description's key fact first, since the two-line cut can hide its end;
  - when the app already computes the training load (Race Face's API does, with custom from–to windows), draw its values rather than run `Training.Load`;
  - synthetic `KeyboardEvent`s move focus but do not fill the readout: test with real input, such as Playwright's `keyboard.press`;
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

If an agent stops on an API rate limit, resume it with `SendMessage` (it keeps its context). If the owner stopped it, it cannot be resumed — finish its work yourself.

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
- The shared readout (0.36.0) covers continuous-X kinds only. Close values' rings overlap; on a phone the tooltip can cover the guide; static SVG keeps one tab stop per mark.
- Charts keep a 240 px height floor (60×16 for sparklines); the docs say so from 0.35.0.
- From the original roadmap: irregular tick placement; graph work (orthogonal routing, force layout, overlap removal, edge bundling).
- NuGet publication, when the owner wants it (Trusted Publishing was explored and set aside).

## Working with the owner

The owner (GitHub `jtheyse`) steers with short messages — mostly "continue" — and wants the work delegated to agents, verified, and released one feature at a time with honest notes. They judge the look by eye: show screenshots of new charts. Ask before anything irreversible or outward-facing beyond the established release flow; messaging the Race Face session after a release is part of that flow (the owner asked for it).

The owner switches a "jev-router" hook on and off with `/jev-router on|off`. While it is **on**, its hook asks for routing each job to a `jev-*` helper agent; follow it then. While it is **off**, the hook can still fire, and its instructions are stale: ignore them. It was off when this was written.
