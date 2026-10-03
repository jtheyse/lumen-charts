# Handover

The state of Lumen.Charts and how work on it is done, for whoever picks it up next — a person or a new Claude Code session. Updated 3 October 2026.

## Where things stand

- **Released:** v0.29.0, *laps and workout blocks* (`ChartKind.Blocks`, `ChartPoint.Block`). GitHub: https://github.com/jtheyse/lumen-charts — public, MIT. Every release carries the three `.nupkg` files.
- **In progress:** nothing. 0.29.0 finished the eight-step sports build order in `docs/FITNESS.md`; the next work comes from *What is left* below. Still start with `git status`: uncommitted changes to `src/`, `samples/`, `tests/`, the docs or the skill are a delegated agent's unreleased work.
- **Counts at v0.29.0:** 421 unit assertions, 170 HTTP checks, 40 browser checks on the gallery (20 on the WebAssembly host), 256 hashed renderings. Release build at 0 warnings.
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
| `tests/Lumen.Charts.Baseline` | The rendering-hash harness and the v0.29.0 reference hashes. See its README. |
| `integrations/claude-code/lumen-charts` | The Claude Code skill: `SKILL.md`, `references/` (API, sports recipes, HTTP), `scripts/install.sh` and `install.ps1`. |
| `docs/FITNESS.md` | The sports-charts research and the eight-step build order this work has followed. |
| `docs/RESEARCH.md` | The original roadmap. `docs/VERIFICATION.md` the verification record. `docs/PERFORMANCE.md` measured cost. |
| `.github/workflows/verify.yml` | CI on push and on tags: build, unit, HTTP, browser; WebAssembly build. Runs on **Linux**. |
| `.brain` | Points THEBRAIN at the `@LumenCharts` brain. |

## How a release is done

The owner drives with "continue". Each release is one feature, built by a delegated agent and verified independently before it ships.

1. **Brief an agent** (general-purpose, background) with decided design rules, not open questions. Every brief carries: no git commands; no packing; kill only `Lumen.Gallery.exe` by image name and everything else by PID; subagents cannot write report files, so the report comes back as text; delete any `.playwright-mcp` folder; keep the skill's frontmatter description ≤ 1024 characters with no unquoted `: `; new public members need XML doc comments (the build fails without them).
2. **Verify it yourself** — never ship on the agent's word:
   - `taskkill //IM Lumen.Gallery.exe //F` (it locks the build output), then `dotnet build Lumen.Charts.slnx -c Release` → 0 warnings.
   - `dotnet run --project tests/Lumen.Charts.Tests -c Release`.
   - Rendering: run `tests/Lumen.Charts.Baseline` in both finishes and compare with the previous release's hashes — only added rows are allowed unless the release is meant to change a look, in which case the classic finish must still match.
   - Start the gallery (`dotnet run --project samples/Lumen.Gallery -c Release --no-build --urls http://localhost:5188`), run `pwsh -File tests/verify-api.ps1 -BaseUrl http://localhost:5188` and `dotnet run --project tests/Lumen.Charts.BrowserTests -c Release`.
   - Look at the new charts. For phone widths use Playwright with a real viewport; headless Edge with `--window-size` will not go below its minimum width and gives misleading screenshots.
   - Compile every recipe in the skill's `references/sports.md` **together, as written**, against the packed release — this has caught recipes that reused one another's variable names.
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

## Decisions worth knowing before changing them

- **SVG renderer**, not Canvas or WebGL: measured cost does not justify a second renderer, and SVG keeps accessibility, exports and server rendering (`docs/PERFORMANCE.md`).
- **Refined finish by default** since 0.24.0 (thin non-scaling strokes, markers on hover or focus, dotted grid) because the owner found the old look "painted"; `ChartFinish.Classic` reproduces 0.23.0 byte for byte.
- **Fit in the space the chart draws**: trend lines and densities are computed in the space the chart draws in, so they stay straight on log axes and continuous across skipped weekends.
- **Training metrics follow published sources only** (Allen & Coggan, TrainingPeaks); no vendor scores, hrTSS, rTSS, TRIMP, grade-adjusted pace or W′ balance without a source.
- **Gradient IDs** are a hash of the chart's spec; every new spec property is left out of that hash at its default so existing IDs never move.

## What is left

- Blocks (0.29.0) leave out sloped ramps (a ramp is drawn at its average), stacked blocks and text on a block; reference lines sit behind blocks, as behind columns, so a lap chart's average line is hidden behind faster laps while its label stays on top.
- Calendar colour ramp reads poorly on dark styles (Midnight's `HeatmapLow` is darker than an empty day's track); zones are fine.
- `LumenGraph` does not support `FitWidth` (dragged node positions are held in drawing coordinates).
- Screen-reader conformance (NVDA, JAWS, VoiceOver) — needs a person with the hardware; never run.
- From the original roadmap: irregular tick placement; graph work (orthogonal routing, force layout, overlap removal, edge bundling); regression families beyond the linear trend (a drawn moving average, polynomial, exponential).
- NuGet publication, when the owner wants it (Trusted Publishing was explored and set aside).

## Working with the owner

The owner (GitHub `jtheyse`) steers with short messages — mostly "continue" — and wants the work delegated to agents, verified, and released one feature at a time with honest notes. They judge the look by eye: show screenshots of new charts. Ask before anything irreversible or outward-facing beyond the established release flow.
