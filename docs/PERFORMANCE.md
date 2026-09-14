# Measured performance

Measured 14 September 2026 with `tests/Lumen.Charts.Profile`, on an AMD Ryzen 9 7940HS (8 cores, 16 threads, 31 GB) running Windows 11, against headless Chromium 151.0.7922.34 in a 1400x900 viewport with a chart 1200 pixels wide. Each figure is the median of five runs. These are one machine's numbers, not a benchmark against another library.

```powershell
dotnet run --project tests/Lumen.Charts.Profile -c Release
```

The profiler asserts nothing and does not run in continuous integration: a timing gate on a shared runner fails for reasons that have nothing to do with the code.

## Results

| Chart | Input points | Budget | Rendered marks | SVG generation | SVG size | Browser render | DOM nodes |
|---|---:|---:|---:|---:|---:|---:|---:|
| Line | 1,000 | 1,200 | 1,000 | 2.8 ms | 240 KB | 33.3 ms | 3,021 |
| Line | 10,000 | 1,200 | 1,200 | 4.8 ms | 292 KB | 33.6 ms | 3,621 |
| Line | 100,000 | 1,200 | 1,200 | 26.8 ms | 295 KB | 33.3 ms | 3,621 |
| Line | 100,000 | 5,000 | 5,000 | 39.3 ms | 1,224 KB | 38.9 ms | 15,021 |
| Line | 100,000 | 16 | 16 | 12.3 ms | 6 KB | 33.0 ms | 69 |
| Scatter | 1,000 | 1,200 | 1,000 | 1.2 ms | 248 KB | 33.6 ms | 3,020 |
| Scatter | 10,000 | 1,200 | 10,000 | 9.3 ms | 2,499 KB | 83.1 ms | 30,020 |
| Scatter | 50,000 | 1,200 | 50,000 | 64.4 ms | 12,619 KB | 394.6 ms | 150,019 |
| Scatter | 100,000 | 1,200 | 100,000 | 131.5 ms | 25,270 KB | 818.9 ms | 300,020 |

Replacing a rendered 1,200-mark chart in place, which is what a zoom, a pan or a series toggle does: **32 ms**.

**The browser column has a floor of about 33 ms**, because the measurement waits two animation frames to be sure the frame was painted. Read the numbers above that floor as the marginal cost: a 10,000-point scatter costs roughly 50 ms of real work, a 50,000-point scatter roughly 360 ms, and a 100,000-point scatter roughly 790 ms. Generation timings vary more between runs than render timings; the 100,000-point line values moved between 12 ms and 55 ms across runs while the render column stayed within a few percent.

## What the numbers say

**Sampling does its job.** A line chart costs the same in the browser whether it is fed 1,000 points or 100,000, because min/max sampling holds the rendered marks at the budget. Only generation grows, and 27 ms for 100,000 points is not a problem for a server that renders once. Raising the budget to 5,000 costs a megabyte of markup and 15,000 DOM nodes for a chart most eyes cannot distinguish from the 1,200-mark version.

**Scatter and bubble are the exposure.** They render every point by design, because thinning a cloud changes what the reader sees. That is defensible up to roughly 10,000 points. Beyond it the cost is not the drawing but the DOM: 50,000 points means 150,000 elements and 12 MB of markup, and 100,000 points means 300,000 elements and 25 MB. At that size the page is slow to load, slow to hover and expensive to keep in memory, and it will be far worse on a phone than on this machine.

## On a second renderer

Roadmap item 4 asked whether a Canvas or WebGL renderer is justified. On this evidence, **not yet**, and not for the reason that was assumed.

The families that were expected to need it — line and area over large series — are already flat, because sampling caps the work. Adding a second renderer for them would buy nothing and would cost a second implementation of every axis, label, tooltip and accessibility affordance, all of which come free while marks are real DOM elements a screen reader and the keyboard can reach.

The families that do degrade are the unsampled point clouds, and the cheaper fix there is to reduce what is drawn rather than to change how it is drawn: density binning, or drawing a sampled cloud with an honest note about how many observations it represents. That keeps one renderer, one accessibility story and one export path. A Canvas renderer only becomes the right answer if a specific application needs every one of 50,000 individual points to stay individually visible and hoverable, which no use here has yet required.

Until then the honest statement is the one in the README: scatter and bubble render every point, comfortable to about 10,000, and larger clouds need aggregation first.
