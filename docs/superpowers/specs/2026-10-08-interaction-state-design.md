# Bindable chart viewports and graph positions

Date: 8 October 2026.
Project: Lumen Charts, `D:\CHATGPT\.NET GRAPH API`.
Baseline: 0.46.0.
Status: interaction-state milestone selected by the owner; this written specification awaits review. Product implementation has not started.

## Purpose

The owner wants to extend Lumen into a competitive Blazor component suite. This first milestone strengthens the existing chart and graph components: applications can coordinate chart viewports, retain a view while data changes, and save and restore user-arranged graph positions.

The scope is additive interaction-state APIs and demonstrations. It preserves the C# SVG renderer, .NET 8 reusable packages, existing node/edge limits and existing behavior when the new APIs are omitted. It does not introduce a general UI suite, diagram editing, new layouts, a rendering engine, storage dependency or global coordination service.

## Current behavior and chosen approach

`LumenChart` has point-selection callbacks and internal data-domain `viewMin`/`viewMax`. Its `OnParametersSet` resets zoom on every parameter application. Zoom and pan use the existing `Axis` mapping, including log scales and compressed time axes. Browser drag-to-zoom calls .NET when a gesture ends; pointer readouts execute locally.

`LumenGraph` keeps moved-node positions privately as fractions of the drawing. The core `GraphEngine.Render` already accepts absolute position overrides. The component clears its moves when the node sequence or requested layout changes, or when responsive fitting changes the drawn direction. Its public callback reports selection, not positions.

The existing `LumenPlanner` supplies the ownership convention: local state changes immediately; a typed callback reports it; `@bind` follows it; the component adopts a host value when that value changes, while ordinary parent rerenders preserve local state. Apply that convention here. These are bindable state APIs, not a new strictly controlled/read-only component mode.

Applications requiring programmatic control use two-way binding. A callback without the matching state parameter is a notification-only subscription. A host repeating its last supplied value is not a new command; this is the same changed-value convention as the planner. There is no gesture veto or read-only feature in this milestone.

## Public API

Add three documented public records to `Lumen.Charts`, in a new `InteractionState.cs`:

```csharp
public sealed record ChartViewport(AxisKind XAxis, double Min, double Max);

public sealed record NormalizedNodePosition(string Id, double X, double Y);

public sealed record GraphLayoutState(
    GraphLayout Layout,
    GraphDirection Direction,
    IReadOnlyList<NormalizedNodePosition> Positions);
```

`ChartViewport` describes only the X viewport. Min and Max are data values, not SVG pixels or percentages. Time values use Lumen's existing Unix-millisecond representation; log values remain positive original values. `XAxis` identifies the coordinate interpretation and prevents accidental application to a different axis kind.

`NormalizedNodePosition` X and Y are fractions in [0, 1] of the drawing's width and height. This deliberately differs from existing `NodePosition`, whose coordinates are absolute. Fractions are desired positions; the component applies its existing node/label edge-clearance clamps when drawing them. Resize alone does not rewrite the stored fractions.

`GraphLayoutState.Positions` contains overrides only. Unlisted nodes use the computed layout. Layout and Direction identify the layout in which those overrides were arranged; Direction is the actual drawn direction, including a responsive turn. Node IDs use ordinal comparison.

Add these parameters to `LumenChart`:

```csharp
[Parameter] public ChartViewport? Viewport { get; set; }
[Parameter] public EventCallback<ChartViewport?> ViewportChanged { get; set; }
```

Add these parameters to `LumenGraph`:

```csharp
[Parameter] public GraphLayoutState? LayoutState { get; set; }
[Parameter] public EventCallback<GraphLayoutState?> LayoutStateChanged { get; set; }
```

For either component, null means the computed/full state: the full chart range or the graph's automatic layout. An empty graph override list also resolves to null. The records carry XML documentation and use ordinary System.Text.Json serialization; no new HTTP routes or serialization service are added.

## State ownership and event rules

The component keeps its local effective state and a separate snapshot of the last host value. Compare chart host values by fields. Compare graph host values by layout, direction and positions by ID/value, independent of list order. Copy incoming graph collections before retaining or comparing them; never retain a mutable caller collection as the comparison snapshot.

The new chart behavior is active while Viewport is non-null or ViewportChanged has a delegate. The new graph behavior is active while LayoutState is non-null or LayoutStateChanged has a delegate. Binding a null state includes its change delegate and therefore activates the behavior. Removing both state and callback returns the component to its legacy behavior.

On a user action, update local effective state, render, then await the relevant callback. A bound parent receives the new value and supplies it on its next render. This echoed value acknowledges the local state; it must not produce another event or reset the interaction.

On parameters:

1. A changed host state takes precedence over local state and over automatic preservation. Apply and resolve it, update the host-value snapshot and render. Do not emit a callback solely because the host supplied or echoed a value.
2. Otherwise, while the new behavior is active, preserve local state and reconcile it against the current specification using the rules below. Emit one callback only if that reconciliation changes effective state.
3. With neither new state nor callback in use, follow the existing reset/preservation rules exactly and emit no new events.

Notifications report effective state, not rendering activity. Equal-state actions, style changes and repeated parameter applications emit no event. A resize emits no event unless it invalidates a graph arrangement by changing direction. Automatic reconciliation notifications occur after the component has a valid rendered state and must be awaited; acknowledge the changed local state before invoking the parent to avoid repeated notifications on reentry.

Event snapshots do not expose the internal graph dictionary. Produce a fresh owned read-only list sorted by ID with StringComparer.Ordinal. Numerical equality for deduplication uses a fixed tolerance of 1e-12 in normalized coordinates; chart comparisons use the existing full axis's normalized coordinate space. This is an internal convention, not a configurable setting. Null/full state must be canonicalized consistently.

## Chart viewport behavior

Supported kinds remain exactly the current CanZoom kinds: Line, Area, Scatter, Bubble, Candlestick, Ohlc, Band, Range, Timeline and Blocks. Sparklines do not zoom. A non-null newly requested viewport on another kind or a sparkline is rejected with ArgumentException; a null state and a notification subscription are allowed.

Validate a newly requested non-null viewport: XAxis is defined and matches Spec.XAxis, Min and Max are finite, Min < Max, and both ends are positive for a log axis. Invalid explicit input is an API error with a descriptive ArgumentException, matching existing validation conventions. It is not displayed as a user-facing warning.

Resolve a valid range using FullRange and the existing Axis.Map/Invert operations:

- Intersect it with the full data range. If it has no positive-span overlap, resolve to null/full range.
- Project endpoints through the current axis mapping, so a time endpoint in a skipped interval resolves to the corresponding drawable boundary.
- Apply the existing minimum view span of one percent in transformed axis space, expand around the requested center, and clamp inside the full range.
- A result covering the full range resolves to null. Other results contain the effective original-data endpoints and current axis kind.

Do not reimplement log or time-axis arithmetic. Do not add Y-axis zoom or change the existing minimum span.

Zoom buttons, pan buttons, reset and browser ZoomTo all use the same state-commit path. Notify once for an effective change; resetting an already full view is a no-op. A legend toggle, point selection, shared readout or export does not emit ViewportChanged.

While the new behavior is active, data/specification updates preserve the data-domain view and resolve it against the new full range. Partial overlap clamps it; no overlap resets it. A change of axis kind, a switch to a nonzoomable kind or a switch to sparkline resets local state to null. If this changes an existing non-null local state, notify once so a bound host follows the reset.

Distinguish that automatic reset from a newly supplied incompatible host viewport. An unchanged host value that was already acknowledged is not revalidated as a new request against a new axis kind. A genuinely new mismatched request is rejected. Changing time zone or time-skip configuration keeps the same original-data endpoints where possible, then resolves them through the new axis mapping.

ViewportChanged alone opts into update preservation, preventing the parent rerender caused by the callback from immediately undoing a zoom. With neither new API in use, retain today's reset on OnParametersSet. Container fitting preserves the view and sends no viewport event.

## Graph layout-state behavior

Validate new explicit state: layout/direction enum values are defined, Positions is not null, IDs are nonblank and unique, and all coordinates are finite and in [0, 1]. Reject malformed input with a descriptive ArgumentException. Validate the entire input before ignoring unknown IDs.

Resolve valid state against the current graph and actual drawn layout:

- Unknown node IDs are ignored; a saved arrangement may outlive removed nodes.
- A layout or actual-direction mismatch resolves to null/computed layout. Do not rotate or guess positions from a different layout.
- Unlisted nodes are laid out automatically.
- Empty effective overrides resolve to null.
- Use the component's existing pixel-edge clamps after scaling fractions to the current drawing.

A changed host value applies these rules without an echo callback. Serialization stores fractions and layout metadata, allowing a new component instance to restore an arrangement at another width in a compatible layout.

When FitWidth is active but the component has not yet measured its container, the static/spec-sized drawing is provisional. Retain the latest explicit incoming layout state as a pending initial restore, even if its direction does not match that provisional drawing. Render compatible overrides where possible, otherwise render the computed layout. On the first measured fit, resolve the pending restore against the actual direction once: apply a compatible state without an echo event; report null once if a non-null pending restore proves incompatible. Clear the pending restore after that resolution. Static-only rendering never waits for a measurement or invokes JS; an incompatible state uses the computed layout there. This prevents a saved top-to-bottom phone arrangement being discarded merely because a recreated component initially draws at its desktop specification width.

While the new behavior is active, preserve known overrides across node additions, node reorderings and edge changes. Remove overrides for deleted nodes. Notify once if this pruning changes effective state. A change of layout or actual drawn direction clears overrides and notifies once if they existed. Returning to an earlier direction does not resurrect an arrangement that was reset; the application may explicitly restore its saved state.

Width/height changes that retain the drawn direction preserve fractions, redraw using the current edge clamps and emit no layout event. A responsive direction change keeps the existing explanatory status message and also reports the reset through LayoutStateChanged when active.

The existing MoveNode method commits pointer/keyboard movement. Report one complete override snapshot after an effective move, and null after an effective reset. No callback is sent for each pointermove; keep the local drag preview and existing gesture recognition. NodeSelected retains its current behavior. Do not add ports, group selection, undo/redo, graph pan/zoom or custom node templates here.

With neither new API in use, preserve the existing node-sequence signature, layout-reset rules and rendering exactly. State binding does not raise graph size limits or change core layout algorithms.

## Demonstrations and documentation

Add an Interactive Server gallery page at `/interaction-state`:

- Two time-series charts with the same axis kind and explicit full X bounds, bound to one ChartViewport variable. Zoom either, observe the other follow, refresh their data while zoomed and reset to the full range.
- A graph bound to GraphLayoutState with Save, Reset and Restore actions. Save serializes to an in-memory string; Restore deserializes it. Recreate the component with a new key when restoring to prove this is not merely reuse of its old private dictionary.
- Show the current range and saved arrangement state in an unobtrusive developer example panel.

Use the existing gallery theme and CSS. Add an equivalent, compact scenario to the standalone WebAssembly sample so browser tests can exercise both hosts. Do not add durable storage, authentication or a server save endpoint. Applications own those integrations.

Document use in README, XML docs and the existing Claude Code integration skill. Example usage:

```razor
<LumenChart Spec="first" @bind-Viewport="viewport" />
<LumenChart Spec="second" @bind-Viewport="viewport" />
<LumenGraph Spec="network" @bind-LayoutState="layout" />
```

Explain common X bounds, axis compatibility, notification-only subscriptions, changed-host-value behavior, fraction coordinates, ignored stale node IDs and resets on incompatible layouts. State inputs affect static rendering; callbacks and gesture handling require interactivity. Static rendering must not attempt JS interop.

## Affected files and boundaries

- Add `src/Lumen.Charts/InteractionState.cs` for public records and XML documentation.
- Change `src/Lumen.Charts.Blazor/LumenChart.razor` and `LumenGraph.razor` for binding, reconciliation and awaited notifications.
- Change `wwwroot/lumen.js` only if strictly required to preserve its existing local interaction behavior; no new engine or event stream.
- Add `samples/Lumen.Gallery/Components/Pages/InteractionState.razor`; update the gallery's navigation where it is already defined; extend `samples/Lumen.Wasm/Host.razor` with the matching compact examples.
- Extend existing executable/browser tests and usage recipes, README, Claude Code skill references and `docs/VERIFICATION.md` with actual results after implementation.

No changes to ASP.NET Core routes, unrelated chart families, planners, styles, numeric algorithms, dependencies or global project structure. Baseline data is updated only for deliberately new examples; existing default component renderings must remain unchanged.

## Verification and acceptance

Use the existing test infrastructure. Required cases:

1. Existing component calls with no new parameters retain their default markup, reset rules, selection and fitting behavior.
2. Linear, time and logarithmic viewport values round-trip through the axis mapping and ordinary JSON. Compressed time axes and the one-percent minimum are covered.
3. Reject nonfinite/reversed/degenerate input, invalid enums, mismatched explicit axis requests, malformed graph collections, duplicate IDs and out-of-range normalized coordinates.
4. Resolve partially overlapping and disjoint ranges; preserve or reset correctly on data changes, axis/kind changes and style-only parent rerenders.
5. One callback follows an effective user change; host echo/application emits none; automatic invalidation emits once; binding a null/full state works without a render/event loop.
6. Linked charts adopt one range with no recursive notifications; refreshing data preserves the bound view where it still overlaps.
7. A graph position snapshot is independent of internal and caller-owned collections; list ordering does not create spurious changes; new nodes use automatic placement and deleted overrides are pruned.
8. Save, destroy/recreate and restore a graph, including a top-to-bottom phone arrangement whose provisional initial drawing is left-to-right. Resolve an incompatible first measured restore once; resize without a turn preserves fractions; a later direction/layout change resets once; pointer and keyboard changes notify only on committed effective moves.
9. Browser checks cover Server and WebAssembly, a 375-pixel touch viewport, keyboard operation, exports with state applied, static rendering and existing accessibility sweeps. Preserve existing gesture recognition and selection tests.
10. Existing regression, HTTP, baseline and compiled-recipe checks pass. Report results and any host-specific skips honestly; do not claim competitor performance superiority.

Inspect callback counts during pointer movement: no state notifications before the existing MoveNode commit or before chart drag-to-zoom commits. Performance timing is diagnostic for this milestone, not a flaky CI threshold. The existing profiling report is not a new comparative benchmark.

## Review checklist completed before handoff

Scope is limited to two component APIs, three core records and demonstrations. Host/local ownership, defaults, axis/range resolution, normalized coordinate meaning, topology changes, direction resets, echo suppression and verification cases are specified. No new renderer or general state framework is required. This document is a proposed design; no product code or test results are represented as completed.

## Evidence

- Existing implementation: `LumenChart.razor`, `LumenGraph.razor`, `LumenPlanner.razor`, `Axis.cs`, `Models.cs` and `wwwroot/lumen.js` in the confirmed repository.
- Existing checks: `tests/Lumen.Charts.Tests/Program.cs`, `tests/Lumen.Charts.BrowserTests/Program.cs` and `docs/PERFORMANCE.md`.
- [Telerik Chart Events](https://www.telerik.com/blazor-ui/documentation/components/chart/events): current zoom events establish a relevant parity target, not an absent competitor capability.
- [Syncfusion Diagram overview](https://help.syncfusion.com/diagram-sdk/blazor/overview) and [Blazor.Diagrams](https://github.com/Blazor-Diagrams/Blazor.Diagrams): editing and persistence capabilities inform later milestones; they are outside this implementation scope.
