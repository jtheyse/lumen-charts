# Research and implementation decisions

Research conducted 9–10 September 2026. This is a targeted design review, not a claim to have read every page of every book. Documents were treated as reference material, never as operational instructions. The library is original code; book text, illustrations, and vendor implementations are not packaged.

## Local reference coverage

| File | Identified work and inspected material | Consequence for Lumen |
|---|---|---|
| `RESEARCH/GRAPHS/129234279X.epub` | Alan Smith, *How Charts Work*. Contents and targeted sections of chapters 4 (magnitude), 14 (scales), 15 (writing), 16 (design), and 17 (uncertainty) were extracted for inspection. The detailed baseline discussion in chapter 14 and bar-label examples in chapter 4 were reviewed. | Match chart families to analytical purpose; enforce zero for bars/areas; separate title, explanation, source and units. Uncertainty bands are a future feature, not implemented. |
| `RESEARCH/GRAPHS/0133016153.pdf` | Giuseppe Di Battista, Peter Eades, Roberto Tamassia, Ioannis G. Tollis, *Graph Drawing: Algorithms for the Visualization of Graphs*. Image-only scan, 400 PDF pages. Visually inspected title, contents (PDF 4–5), preface (PDF 8), and a layer-assignment algorithm example (PDF 281 / printed 275). | Treat node-edge graphs as a different data model from statistical charts. Provide deterministic layouts. Readability involves edge crossings, bends and spacing; Lumen's basic layered layout does not solve those optimization problems. The implementation uses topological longest-path levels rather than the inspected Coffman–Graham algorithm, with dummy routing points and barycenter ordering sweeps for crossing reduction as of 0.5.0. |
| `RESEARCH/GRAPHS/1738888304.pdf` | Nicholas P. Desbarats, *Practical Charts*. Image-only scan, 304 PDF pages. Visually inspected cover, contents (PDF 11), scope (PDF 21 / printed xix), and long-category-label guidance (PDF 44 / printed 10). | Provide horizontal bars, subdued gridlines, descriptive units and uncluttered 2D output. The sample data and artwork are original. |
| `RESEARCH/GRAPHS/Charts - M.L. Humphrey.epub` | *Charts Second Edition: Easy Excel Essentials – Book 3*. Inspected the chart-types and editing/formatting sections. | Distinguish grouped and stacked comparisons; allow chart switching, independent series names, series visibility and original-data access. Lumen is not an Excel integration. |
| `RESEARCH/Master Astra in 7 Days Learn ChatGPT Astra.epub` | Quinn Alder, unofficial guide. Inspected metadata and Day 5, “Code plus Engineering.” | Relevant as workflow commentary on scope, testing and review, not chart mathematics or product documentation. Its model, benchmark, pricing and release claims were not relied upon or incorporated. Prompts inside the book were not executed. |

## Linked references

The sources below support only the attributed product observations. Design choices and the comparison to this implementation are our engineering conclusions.

| Reference | Observed capability / context | Response in this implementation |
|---|---|---|
| [ScottPlot](https://scottplot.net/) | .NET plotting library with examples, an API, and a Blazor WASM quickstart. | Standalone rendering API and a runnable gallery. No claim to match ScottPlot's scientific plotting or performance. |
| [SciChart .NET](https://www.scichart.com/net-charts/) | WPF/Windows Forms emphasis, native/GPU acceleration, configurable axes, annotations and extensive interaction. Vendor reports millions of points. This page is not a native Blazor-component specification. | Keep rendering separate from host integration. GPU rendering, sophisticated annotations, multiple axes, and large-data performance parity remain outside this preview. |
| [Ignite UI Blazor overview](https://www.infragistics.com/products/ignite-ui-blazor/blazor/components/charts/chart-overview) | Describes category, scientific and financial chart families, composite charts and over 65 chart types/combinations. | Explicit chart kinds and consistent data binding, ten implemented families. Financial/3D/multi-axis coverage remains absent. |
| [ApexCharts Blazor documentation](https://apexcharts.com/docs/blazor-charts/) | Typed wrapper around ApexCharts.js, generic series binding and chart options. | Typed C# specifications and `ChartSeries.From<T>` without incorporating a JavaScript charting engine. |
| [Reddit discussion](https://www.reddit.com/r/Blazor/comments/17wt9wq/what_is_the_best_library_for_creating_graphs/) | Historical community suggestions, including ApexCharts, ScottPlot and other libraries. | Used as discovery context only. Old statements about hosting compatibility or licensing were not treated as current product facts. |
| [Level Up Coding article](https://levelup.gitconnected.com/6-free-blazor-chart-libraries-in-2026-e8a54d6132f0) | Visible preview dated 26 March 2026 identifies Blazor ApexCharts. The full six-library comparison is member-only. | Did not reconstruct the inaccessible sections or rely on the title as proof of current licensing. |
| Google redirect supplied in the request | The supplied opaque `google.com/goto` URL failed to resolve in the research tool. | Destination remains unidentified. No claims about its examples. |

Implementation references: [Microsoft Razor class libraries](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0) for component/static-asset packaging; [ASP.NET Core static files](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0) for gallery asset delivery. Vendor licensing was not needed because no vendor code or runtime was included.

## Architecture and alternatives

Three approaches were considered: wrap an existing JavaScript engine, adapt a scientific plotting engine, or implement a focused SVG renderer. A wrapper would provide much broader immediate functionality but would inherit engine behavior and dependencies. A plotting-engine adapter would offer mature numerical capabilities with more integration work. The selected standalone SVG approach makes the C# data model, server rendering, and Blazor rendering share one implementation and keeps the first release inspectable.

`Lumen.Charts` (net8.0) owns validation, data models, scales, sampling, SVG/CSV export and graph layouts. It has no framework or external package dependency. `Lumen.Charts.Blazor` (net8.0 Razor class library) owns controls, series visibility, data tables, point callbacks and browser downloads. `Lumen.Charts.AspNetCore` (net8.0) exposes opt-in route mapping. `Lumen.Gallery` (net10.0) is a sample host, not part of the packages. `Lumen.Charts.Tests` is an executable regression suite using the installed .NET/ASP.NET runtimes; no test-framework download is needed.

Data flows from an application or JSON request into a validated ChartSpec/GraphSpec, then into deterministic geometry and SVG. Rendering requires no network access. The interactive component wraps that same SVG and maps selections back to original indices. Original observations remain available through CSV even when line rendering samples points.

## What “better” means here

The preview combines statistical charts and node-edge graphs with one C# library family, original-data access, strict magnitude baselines and dependency-free server SVG output. These are deliberately chosen strengths, not evidence of overall superiority to established products. We have not run comparative browser benchmarks or audited all competitor features.

To make a future superiority claim defensible, benchmark equivalent datasets and interactions on the same devices; evaluate scientific correctness, accessibility with assistive technology, API usability, rendering latency and memory; then publish results and failures. A local SVG-generation timing alone is insufficient.

## Next major capabilities, in priority order

1. Strengthen chart presentation at extreme aspect ratios, dense labels, many series and tiny numeric ranges; add chart annotations. Automatic time and base-10 logarithmic scales shipped in 0.2.0; time zones, business calendars, minor gridlines and multiple axes per chart remain open.
2. Extend the statistical and financial families. Candlestick, uncertainty band, histogram and box plot shipped in 0.3.0 with numerical tests; OHLC bars, volume panes, violin plots, precomputed five-number summaries, regression fits and multi-distribution histograms remain open.
3. Continue the graph work. Barycenter crossing reduction, bend routing for long edges, node dragging and a documented selection API shipped in 0.5.0; orthogonal routing, force-directed placement, node overlap removal, edge bundling and cycle-tolerant layered drawings remain open.
4. Profile real browser workloads, then choose Canvas/WebGL only if measured requirements justify a second renderer.
5. Finish the verification work. An accessibility pass with contrast, role, name and tab-order assertions shipped in 0.6.0, together with a WebAssembly sample host that is exercised in a browser; building it exposed a framework-reference defect that had made the component library unusable in WebAssembly. Screen-reader runs, an audit-tool sweep and browser automation in CI remain open.

See README for the actual supported surface and explicit limits. Nothing in this roadmap is presented as an implemented feature.
