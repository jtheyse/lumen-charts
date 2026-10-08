# Heatmap Follow-ups (0.46.1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close Race Face's ten 0.46.0 heatmap gaps: longer notes, "did not race" cells, reasons written in cells, a pinnable scale, labels on top, full and frozen row names, fitted height, a separate "View data" switch, the grid table's order, and standalone table margins.

**Architecture:**
- **Validation and model:** all in Lumen.Charts' `ChartValidation` and `Models`.
- **Drawing:** `ChartSvg.Heatmap` gains:
  - a measured name column (`HeatmapLeftOf`);
  - a vertical layout expressed from the grid's top and bottom (`HeatmapBelow`), so labels can move to the top and `FitHeight` can size the rows;
  - a word-wrapping writer for a cell's words (`CellReason`);
  - a scale that honours `YMin`, `YMax` and `IncludeZero` in the refined finish.
- **Component:** `<LumenChart>` overlays a names-only drawing of the same band (`ChartSvg.HeatmapNameBand`) in a `position:sticky` layer, and gains `ShowDataButton`.

**Tech Stack:** C# / .NET 8, Blazor (Razor components), plain CSS and JS, an executable test runner (`tests/Lumen.Charts.Tests`, `Test`/`Check`/`Reject`), Playwright browser tests, a rendering-hash baseline harness.

**Spec:** `docs/superpowers/specs/2026-10-08-heatmap-followups-design.md`. Read it with this plan; where they differ, the spec wins.

## Global Constraints

**Workspace**
- Work in the worktree `D:/CHATGPT/wt461`, branch `feature/heatmap-0461`, made from master.
- Commit with explicit paths. Never stage or open `lumen-charts-race-face-brief.md`.
- Keep every file's line endings as they are (several are CRLF). Edit with the Edit tool or a Python script, not `sed -i`.
- `Lumen.Gallery.exe` locks the build: `taskkill //IM Lumen.Gallery.exe //F` before building. Stop every other process you start by PID. Never kill `dotnet.exe` by name.
- `tests/Lumen.Charts.BrowserTests` and `samples/Lumen.Wasm` are outside the solution: build them explicitly before running them.

**Drawing rules**
- **Classic finish:** `ChartFinish.Classic` stays 0.23.0 byte for byte for every spec 0.23.0 could draw. The pinned scale (1.4) is refined only.
- **Gradient-ID hash:** a new spec property stays out of the hash at its default. `ColumnLabelsOnTop` is serialized only when true, as `CellText` is.
- **Contrast:** never colour alone, and every drawn word clears 4.5:1. Cell words use `CellInk`.
- **Invented data only** in tests, samples, baseline rows, docs and recipes. No real rider, team or event names.
- **Public members:** new public members need XML doc comments (the build fails without them). Match the surrounding comment style: plain sentences that say what and why.

**Verification (every task)**
- `dotnet build Lumen.Charts.slnx -c Release` → 0 warnings.
- `dotnet run --project tests/Lumen.Charts.Tests -c Release` → all pass.

**Baseline**
- Compare CRLF-blind: `diff <(tr -d '\r' < reference/refined.txt) <(tr -d '\r' < baseline.txt)`, from `tests/Lumen.Charts.Baseline`. Use `classic.txt` for `-- classic`.
- Only the rows this plan names may change or be added.

## Review Focus

1. **Hiding rows in the component.** A heatmap whose rows the reader hides through the legend: the frozen band must be built from the rows shown, so names and rows stay aligned. Tested in Task 4.
2. **A single long word.** A not-rated reason with no spaces (24 characters) in a narrow cell must be cut by characters with "…" and never overflow the cell, at any `CellWidth` from 24 to 120. Tested in Task 3.
3. **Pinned ends beyond the data.** `YMin` above every rated value, or `YMax` below every one, must draw without an exception, every cell taking the nearer end's colour. Tested in Task 3.
4. **Focus under the frozen names.** A cell moved to with the keyboard after the reader scrolled right must not stop hidden under the frozen names: the viewport keeps a scroll padding of the band's width. Tested in Task 5, browser.
5. **Long names and the size cap.** A heatmap whose long names widen the name column past what the 4096 check allowed at 130 must be refused with the measured width in the message. Tested in Task 2.

---

### Task 1: Model and validation

**Files:**
- Modify: `src/Lumen.Charts/Models.cs` (ChartSpec: add `ColumnLabelsOnTop`; doc comments on `FitHeight`, `ChartPoint.ValueNote` and `ChartPoint.GapLabel`)
- Modify: `src/Lumen.Charts/ChartValidation.cs:21-22` (FitHeight), `:111-112` (heatmap-only options), `:267-268` (ValueNote cap), `:664-679` (GapLabel)
- Modify: `src/Lumen.Charts/ChartSvg.cs:96-114` (the `Unfitted` hash modifier and its comment)
- Test: `tests/Lumen.Charts.Tests/Program.cs`. Append after the last 0.46.0 test, in a block headed `// 0.46.1: heatmap follow-ups.`

**Interfaces:**
- Produces: `bool ChartSpec.ColumnLabelsOnTop { get; init; }`.
- Produces: heatmap cells accept `GapLabel` (with `Y == null`) and a `ValueNote` of up to 40 characters.
- Produces: `FitHeight` is accepted on `ChartKind.Heatmap`.

- [ ] **Step 1: Write the failing tests**

```csharp
// 0.46.1: heatmap follow-ups.
ChartSpec Noted(ChartSpec s,string note)=>s with{Series=[s.Series[0] with{Points=[s.Series[0].Points[0] with{ValueNote=note},s.Series[0].Points[1]]},s.Series[1]]};
ChartSpec Raced(ChartSpec s,ChartPoint first)=>s with{Series=[s.Series[0] with{Points=[first,s.Series[0].Points[1]]},s.Series[1]]};
Test("Heatmap follow-ups: heatmap cells take a 40-character note, a gap label where they have no value, labels on top and FitHeight",()=>{
    var forty=" · "+new string('9',37);
    Check(forty.Length==40,"forty");
    ChartValidation.Validate(Noted(HeatGrid(),forty));
    ChartValidation.Validate(Raced(HeatGrid(),new ChartPoint(0,null,"2025"){SubLabel="/0 starts",GapLabel="did not race"}));
    ChartValidation.Validate(HeatGrid(s=>s with{ColumnLabelsOnTop=true,FitHeight=true}));
});
Test("Heatmap follow-ups: a longer note, a gap label with a value or beside a reason, and labels on top or FitHeight elsewhere are refused",()=>{
    var fortyOne=" · "+new string('9',38);
    Reject(()=>ChartValidation.Validate(Noted(HeatGrid(),fortyOne)));
    try{ChartValidation.Validate(Noted(HeatGrid(),fortyOne));}
    catch(ArgumentException e){Check(e.Message.StartsWith("A value note is at most 20 characters (40 on a heatmap cell, where it is never drawn)"),e.Message);}
    // Every other kind keeps 20.
    Reject(()=>ChartValidation.Validate(Spec(ChartKind.Column) with{Series=[new("S",[new ChartPoint(0,1,"A"){ValueNote=new string('x',21)}])]}));
    Reject(()=>ChartValidation.Validate(Raced(HeatGrid(),new ChartPoint(0,2.8,"2025"){GapLabel="did not race"})));
    Reject(()=>ChartValidation.Validate(Raced(HeatGrid(),new ChartPoint(0,null,"2025"){GapLabel="did not race",NotRated="no starts"})));
    try{ChartValidation.Validate(Raced(HeatGrid(),new ChartPoint(0,null,"2025"){GapLabel="did not race",NotRated="no starts"}));}
    catch(ArgumentException e){Check(e.Message.StartsWith("A heatmap cell is either not rated or has a gap label, not both"),e.Message);}
    Reject(()=>ChartValidation.Validate(Spec(ChartKind.Column) with{ColumnLabelsOnTop=true}));
    Reject(()=>ChartValidation.Validate(Spec(ChartKind.Line) with{FitHeight=true}));
});
Test("Heatmap follow-ups: labels on top left false change nothing a heatmap draws",()=>{
    Check(ChartSvg.Render(HeatGrid())==ChartSvg.Render(HeatGrid(s=>s with{ColumnLabelsOnTop=false})),"false is the default");
});
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release`
Expected: the build fails because `ChartSpec` has no `ColumnLabelsOnTop`.

- [ ] **Step 3: Add the property to `ChartSpec` in `Models.cs`, next to `CellText` and `CellWidth`**

```csharp
/// <summary>Writes a heatmap's column labels above its grid, under its title and description, as a table's header row reads, instead of
/// under it; the grid takes the room they leave below (0.46.1). Heatmaps only, refused elsewhere; false by default, and left out of the
/// gradient-ID hash while false.</summary>
public bool ColumnLabelsOnTop { get; init; }
```

Update the existing doc comments:
- `FitHeight`: it now also applies to heatmaps, 36 units a row, and with it an empty `Source` reserves no room.
- `ChartPoint.ValueNote`: up to 40 characters on a heatmap cell, which never draws it.
- `ChartPoint.GapLabel`: also on a heatmap cell with no value, drawn unshaded and dashed and named with the word.

- [ ] **Step 4: Change the validation in `ChartValidation.cs`**

Replace lines 21-22:

```csharp
        if (spec.FitHeight && spec.Kind is not (ChartKind.Bar or ChartKind.Heatmap))
            throw new ArgumentException("FitHeight works out a chart's height from its rows, a horizontal bar chart's one a category and a heatmap's 36 units each, so it applies to bar charts and heatmaps only; a strip is drawn as tall as its content already, and the other kinds lay their marks out in the Height they are given.");
```

Replace lines 111-112:

```csharp
        if (spec.Kind != ChartKind.Heatmap && (spec.CellText || spec.CellWidth is not null || spec.ColumnLabelsOnTop))
            throw new ArgumentException("Cell text writes in a heatmap's cells, cell width sets the width of its columns and ColumnLabelsOnTop moves its column labels above its grid, so they apply to heatmap charts only; the other kinds draw other marks.");
```

Replace the note-length check at lines 267-268:

```csharp
                    if (p.ValueNote.Length > (mark == ChartKind.Heatmap ? 40 : 20))
                        throw new ArgumentException("A value note is at most 20 characters (40 on a heatmap cell, where it is never drawn), such as /48 after a finishing position for the size of its field; longer words belong in the point's label.");
```

In `GapLabel(...)`:
- Allow heatmaps: `if (mark is not (ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Heatmap))`. Extend that message: "…so it applies to series drawn as lines, areas or scatter points, and to heatmap cells, …".
- Before the `p.Y.HasValue` check, add:

```csharp
        if (p.NotRated is not null)
            throw new ArgumentException("A heatmap cell is either not rated or has a gap label, not both: NotRated says why a result is not read as a score, and GapLabel names a cell with no result, such as did not race.");
```

- [ ] **Step 5: Leave `ColumnLabelsOnTop` out of the hash while false**

In `ChartSvg.cs`, `Unfitted`, after the `CellText` line, add:

```csharp
            else if (property.Name == nameof(ChartSpec.ColumnLabelsOnTop)) property.ShouldSerialize = (_, top) => top is true;
```

Extend the comment above `Unfitted` with: "…as are a heatmap's labels on top, from 0.46.1, false by default."

- [ ] **Step 6: Run the tests, fix any older test that pins a changed message, and run again**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release`
Expected: all pass.

Grep the tests for the old messages:
- "FitHeight works out a horizontal bar chart's height";
- "Cell text writes in a heatmap's cells and cell width";
- "A value note follows a value on the chart";
- "A gap label is written where a line".

Update each pin to the new wording. Change nothing else.

- [ ] **Step 7: Commit**

```bash
git add src/Lumen.Charts/Models.cs src/Lumen.Charts/ChartValidation.cs src/Lumen.Charts/ChartSvg.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Accept longer notes, gap labels, labels on top and FitHeight on heatmaps"
```

---

### Task 2: Heatmap layout — measured name column, labels on top, fitted height, the frame

**Files:**
- Modify: `src/Lumen.Charts/ChartSvg.cs`. Change `Render` (lines ~240-244: the order of the FitHeight and CellWidth substitutions), the heatmap helpers (lines ~3085-3101), `FittedHeight` (~1678) and `Heatmap` (~3147-3214).
- Modify: `src/Lumen.Charts/ChartValidation.cs:342-343`, so the 4096 check uses the measured column.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, in the 0.46.1 block.

**Interfaces:**
- Consumes: `ChartSpec.ColumnLabelsOnTop`, and `FitHeight` on heatmaps (Task 1).
- Produces, as internal static members of `ChartSvg`:
  - `double HeatmapLeftOf(ChartSpec s)`: the name column's width, 130 to 240.
  - `double HeatmapWidth(ChartSpec s, int columns, double cellWidth)`: replaces the two-argument overload.
  - `double HeatmapBelow(ChartSpec s)`: the room under the grid.
  - `(double Left, double Top, double Bottom) HeatmapFrame(ChartSpec spec)`: for a spec as given to `Render`, the name column's width and the grid's top and bottom in drawing units, after the CellWidth and FitHeight substitutions. Task 4 uses it.
  - `ChartSpec Drawn(ChartSpec spec)`: the CellWidth and FitHeight substitutions `Render` applies.
  - `const double HeatmapRow = 36`.

- [ ] **Step 1: Write the failing tests**

```csharp
ChartSpec Named(ChartSpec s,string name)=>s with{Series=[s.Series[0] with{Name=name},s.Series[1]]};
Test("Heatmap follow-ups: a row name wider than 118 widens the name column and is written whole; names that fit keep today's drawing",()=>{
    // "Junior 18/19 Girls" is 10.52 em, 126.2 units at 12 px: the column becomes ceil(126.2 + 18) = 145, its names ending at 133.
    var wide=Named(HeatGrid(s=>s with{CellWidth=72}),"Junior 18/19 Girls");
    Check(ChartSvg.HeatmapLeftOf(wide)==145,"left "+ChartSvg.HeatmapLeftOf(wide));
    var svg=ChartSvg.Render(wide);
    Check(svg.Contains(">Junior 18/19 Girls<"),"whole");
    Check(svg.Contains("<text x='133'"),"names end 12 short of the grid");
    Check(svg.Contains("x='146'"),"the first cell starts one past the column");
    Check(ChartSvg.DrawnWidth(wide)==145+35+2*72,"width "+ChartSvg.DrawnWidth(wide));
    // Today's names fit 118: the column stays 130, and the drawing is unchanged.
    Check(ChartSvg.HeatmapLeftOf(HeatGrid())==130,"stays 130");
    Check(ChartSvg.Render(HeatGrid()).Contains("<text x='118'"),"names at 118");
    // A very long name is held to 240, and cut by width with an ellipsis to the 222 left.
    var longest=Named(HeatGrid(),"An invented category with a very long name indeed");
    Check(ChartSvg.HeatmapLeftOf(longest)==240,"cap");
    var cut=Regex.Match(ChartSvg.Render(longest),"<text x='228'[^>]*>([^<]*)<").Groups[1].Value;
    Check(cut.EndsWith("…")&&ChartSvg.Broad(cut)<=222,"cut by width: "+cut);
});
Test("Heatmap follow-ups: the 4096 check measures the name column",()=>{
    // 54 columns of 72 are 3888: 130 + 35 + 3888 = 4053 fits, but a 240 column makes 4163, past 4096.
    var cols=Enumerable.Range(0,54).Select(i=>new ChartPoint(i,i+1,$"S{i}")).ToArray();
    var fits=new ChartSpec{Title="Wide",Kind=ChartKind.Heatmap,CellWidth=72,Series=[new("Sprint",cols)]};
    ChartValidation.Validate(fits);
    var refused=fits with{Series=[new("An invented category with a very long name indeed",cols)]};
    Reject(()=>ChartValidation.Validate(refused));
    try{ChartValidation.Validate(refused);}catch(ArgumentException e){Check(e.Message.Contains("4163 pixels wide"),e.Message);}
});
Test("Heatmap follow-ups: labels on top are written above the grid, which takes the room they leave below",()=>{
    var top=ChartSvg.Render(HeatGrid(s=>s with{ColumnLabelsOnTop=true}));
    var below=ChartSvg.Render(HeatGrid());
    // Title and a one-line description: the grid starts at 80, and labels on top stand at 70.
    Check(Regex.IsMatch(top,"<text x='[^']*' y='70'[^>]*>2025<"),"2025 on top");
    Check(!Regex.IsMatch(top,"y='258'[^>]*>2025<"),"not at the foot");
    // Two rows in 320: below the grid 80 - 26 = 54, so a row is (320 - 80 - 54) / 2 = 93 and a cell 91 tall; at the foot 78.
    Check(top.Contains("height='91'")&&below.Contains("height='78'"),"the grid grows into the room");
    // The scale line stays 36 above the foot either way.
    Check(Regex.IsMatch(top,"y='284'[^>]*>Color scale")&&Regex.IsMatch(below,"y='284'[^>]*>Color scale"),"scale line in place");
});
Test("Heatmap follow-ups: FitHeight gives each row 36 units, drops an empty source's room, and keeps the 240 floor",()=>{
    var six=HeatGrid(s=>s with{FitHeight=true,Series=Enumerable.Range(0,6).Select(i=>new ChartSeries($"Row {i}",[new ChartPoint(0,i+1,"2025"),new ChartPoint(1,i+2,"2026")])).ToArray()});
    // 80 above the grid, 6 x 36, then 80 - 24 below with no source: 352.
    Check(ChartSvg.Render(six).Contains("viewBox='0 0 600 352'"),"no source: "+Regex.Match(ChartSvg.Render(six),"viewBox='[^']*'").Value);
    Check(ChartSvg.Render(six).Contains("height='34'"),"rows of 36");
    Check(ChartSvg.Render(six with{Source="Invented"}).Contains("viewBox='0 0 600 376'"),"a source keeps its 24");
    Check(ChartSvg.Render(six with{ColumnLabelsOnTop=true}).Contains("viewBox='0 0 600 326'"),"labels on top free 26");
    Check(ChartSvg.Render(HeatGrid(s=>s with{FitHeight=true})).Contains("viewBox='0 0 600 240'"),"two rows keep the floor");
    // With a cell width the source wraps across the drawn width, and the height follows it.
    var frame=ChartSvg.HeatmapFrame(six);
    Check(frame.Left==130&&frame.Top==80&&frame.Bottom==80+6*36,"frame "+frame);
});
```

`ChartSvg.Broad` is internal. The tests project already reads internals (see `ChartSvg.CellInk` in the 0.46.0 tests). If `Broad` is `private`, make it `internal`.

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release`
Expected: the build fails because `HeatmapLeftOf`, `HeatmapFrame` and others do not exist yet.

- [ ] **Step 3: Replace the heatmap width helpers (ChartSvg.cs ~3085-3101)**

```csharp
    /// <summary>Where a heatmap's grid starts while every row name fits the 118 units its names end at: 130 units in.</summary>
    internal const double HeatmapLeft = 130;
    /// <summary>The widest a heatmap's name column grows for long row names (0.46.1).</summary>
    internal const double HeatmapWidest = 240;
    /// <summary>The height <see cref="ChartSpec.FitHeight"/> gives each of a heatmap's rows (0.46.1).</summary>
    internal const double HeatmapRow = 36;
    /// <summary>Where a heatmap's grid starts: 130 units in while every row name, at 12 px, fits the 118 its names end at; otherwise as far
    /// in as its widest name and 18 more, up to <see cref="HeatmapWidest"/>, so a long name is written whole rather than cut at 17
    /// characters (0.46.1).</summary>
    internal static double HeatmapLeftOf(ChartSpec s)
    {
        var widest = s.Series.Count == 0 ? 0 : s.Series.Max(series => Broad(series.Name));
        return widest <= HeatmapLeft - 12 ? HeatmapLeft : Math.Min(HeatmapWidest, Math.Ceiling(widest + 18));
    }
    /// <summary>How wide a heatmap is whose <paramref name="columns"/> take <paramref name="cellWidth"/> units each: its name column, its
    /// columns and 35 units on the right. Validation holds it within <see cref="MaxWidth"/>.</summary>
    internal static double HeatmapWidth(ChartSpec s, int columns, double cellWidth) => HeatmapLeftOf(s) + 35 + columns * cellWidth;
```

Keep `MinWidth` and `MaxWidth`. Delete `HeatmapMargin` and the two-argument `HeatmapWidth`, and update every caller:
- `DrawnWidth` becomes `HeatmapWidth(spec, distinctX, cell)`.
- `ChartValidation.cs:342-343` becomes `ChartSvg.HeatmapWidth(spec, categories, columnWidth)`, keeping its message.
- `Models.cs`'s `CellWidth` doc: "165 plus" becomes "the name column, at least 130, plus 35, plus".

- [ ] **Step 4: Add the vertical layout, `Drawn`, the frame, and the heatmap branch of `FittedHeight`**

```csharp
    /// <summary>The room under a heatmap's grid: its column labels, 26, unless they stand on top; its colour scale's line; and its source
    /// line, 24, which a heatmap fitted to its rows leaves out when it has no source (0.46.1).</summary>
    internal static double HeatmapBelow(ChartSpec s) => 80 - (s.ColumnLabelsOnTop ? 26 : 0) - (s.FitHeight && string.IsNullOrWhiteSpace(s.Source) ? 24 : 0);

    /// <summary>The spec as <see cref="Render"/> draws it: a heatmap with a cell width as wide as its columns make it, and then a chart fitted
    /// to its rows as tall as they make it, so a source wrapped across the drawn width is counted in the height.</summary>
    internal static ChartSpec Drawn(ChartSpec spec)
    {
        if (spec.CellWidth is not null) spec = spec with { Width = DrawnWidth(spec) };
        if (spec.FitHeight) spec = spec with { Height = FittedHeight(spec) };
        return spec;
    }

    /// <summary>Where a heatmap's rows stand in its drawing: its name column's width and its grid's top and bottom, for a spec as given to
    /// <see cref="Render"/>. The component freezes the names in that band (0.46.1).</summary>
    internal static (double Left, double Top, double Bottom) HeatmapFrame(ChartSpec spec)
    {
        var s = Drawn(spec);
        var foot = 14 * Math.Max(0, Wrap(s.Source, s.Width - 48).Length - 1);
        return (HeatmapLeftOf(s), 80 + Headroom(s), s.Height - foot - HeatmapBelow(s));
    }
```

At the top of `FittedHeight`, add:

```csharp
        if (s.Kind == ChartKind.Heatmap)
            return Math.Max(240, (int)Math.Ceiling(80 + Headroom(s) + s.Series.Count * HeatmapRow + HeatmapBelow(s) + 14 * Math.Max(0, Wrap(s.Source, s.Width - 48).Length - 1)));
```

In `Render`, replace the two substitution lines with `spec = Drawn(spec);`. Bars cannot have a cell width, so their height is unchanged.

**Check `Headroom(s)` equals the `w.Head` that `Begin` sets.** Use a title, a one-line description, a two-line description, and `DrawTitles = false`. If `Headroom` differs, use whatever `Begin` uses, so that `HeatmapFrame(spec).Top` equals the drawn grid top in every case. Add a check for the two-line description and `DrawTitles = false` to the FitHeight test.

- [ ] **Step 5: Use the layout in `Heatmap`**

In `Heatmap(SvgWriter w, ChartSpec s)`:

```csharp
        var left = HeatmapLeftOf(s);
        double top = 80 + w.Head, bottom = s.Height - w.Foot - HeatmapBelow(s);
        var cw = s.CellWidth ?? (s.Width - left - 35) / cats.Length; var ch = (bottom - top) / s.Series.Count;
```

- **Row name:**
  - Replace the name line with `w.Text(left - 12, top + (si + .5) * ch + 4, Fitted(series.Name, left == HeatmapLeft ? left - 12 : left - 18), "text-anchor='end' class='lumen-muted'");`.
  - `Fitted` is a new private helper: the whole text if `Broad(text) <= room`, else `Short(text, keep)` with the largest `keep` whose `Broad` fits, as the scale line already cuts.
- **Cells:** `HeatmapLeft` becomes `left` in the cell x and the column-label x, and `80+w.Head` becomes `top`.
- **Column labels:** at `y = s.ColumnLabelsOnTop ? top - 10 : bottom + 18`.
- **Scale line:** at `y = bottom + (s.ColumnLabelsOnTop ? 18 : 44)`, x `left`, and room `s.Width - 12 - left`.

With the defaults these are today's numbers (`bottom + 18 = H − Foot − 62`, `bottom + 44 = H − Foot − 36`). The 0.46.0 tests and the baseline prove it: run the baseline harness and expect no row to change.

- [ ] **Step 6: Run the tests and the baseline**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release` → all pass.

Run, from `tests/Lumen.Charts.Baseline`:
- `dotnet run -c Release`, then the CRLF-blind diff against `reference/refined.txt` → no output;
- `dotnet run -c Release -- classic`, then the diff against `reference/classic.txt` → no output.

- [ ] **Step 7: Commit**

```bash
git add src/Lumen.Charts/ChartSvg.cs src/Lumen.Charts/ChartValidation.cs src/Lumen.Charts/Models.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Measure a heatmap's name column, put its labels on top on request, and fit its height to its rows"
```

---

### Task 3: Heatmap cells — gap-label cells, reasons in cells, a pinned scale

**Files:**
- Modify: `src/Lumen.Charts/ChartSvg.cs`:
  - `HeatmapCellName` (~3134-3145);
  - `Heatmap`'s scale and cell loop (~3147-3184);
  - a new `CellReason` beside `CellWords` (~3216);
  - `Mix`'s caller, to clamp.
- Test: `tests/Lumen.Charts.Tests/Program.cs`, in the 0.46.1 block.

**Interfaces:**
- Consumes: `Raced`, `HeatGrid` and the layout from Task 2. `HeatmapRow` is 36.
- Produces: a not-rated or gap-label cell writes its words through `CellReason`. `HeatmapCellName` names a gap-label cell with its word.

- [ ] **Step 1: Write the failing tests**

```csharp
// Six rows, so FitHeight's rows are 36 tall rather than raised to the 240 floor: a cell 34 tall holds two 10 px lines.
ChartSpec Six(ChartSpec s)=>s with{Series=[..s.Series,..Enumerable.Range(2,4).Select(i=>new ChartSeries($"Row {i}",[new ChartPoint(0,i,"2025"),new ChartPoint(1,i+1,"2026")]))]};
Test("Heatmap follow-ups: a not-rated cell writes its reason, wrapped, instead of its value",()=>{
    // HeatGrid's Long distance 2025 is not rated "too few starts to rate", with the value 1.2.
    var ninety=ChartSvg.Render(Six(HeatGrid(s=>s with{CellWidth=90,FitHeight=true})));
    Check(ninety.Contains(">too few starts<")&&ninety.Contains(">to rate<"),"two whole lines at 90");
    Check(!ninety.Contains(">1.2<"),"the value is not written");
    Check(ninety.Contains("1.2, not rated: too few starts to rate"),"the name keeps the value and the reason");
    // At 72 a line holds 66: "too few", then "starts to", and "rate" is left over, so the second line ends with an ellipsis.
    var seventy=ChartSvg.Render(Six(HeatGrid(s=>s with{CellWidth=72,FitHeight=true})));
    Check(seventy.Contains(">too few<")&&seventy.Contains(">starts to…<"),"cut at a word at 72");
    // A taller cell takes more lines and then the sub-label: two rows are raised to the 240 floor, 52 a row, four lines.
    var tall=ChartSvg.Render(HeatGrid(s=>s with{CellWidth=72,FitHeight=true}));
    Check(tall.Contains(">too few<")&&tall.Contains(">starts to<")&&tall.Contains(">rate<")&&Regex.IsMatch(tall,"font-size='10'[^>]*>/4 starts<"),"three lines and the sub-label");
});
Test("Heatmap follow-ups: no cell's words run past its cell at any width, a reason of one long word included",()=>{
    var word=new string('w',24);
    foreach(var width in new double[]{24,30,48,72,90,120})
    {
        var spec=Raced(HeatGrid(s=>s with{CellWidth=width,FitHeight=true}),new ChartPoint(0,null,"2025"){NotRated=word});
        foreach(Match m in Regex.Matches(ChartSvg.Render(spec),"<text[^>]*font-size='10'[^>]*>([^<]*)<"))
            Check(ChartSvg.Wide(m.Groups[1].Value)*10/11<=width-6+1e-9,$"{width}: {m.Groups[1].Value}");
    }
});
Test("Heatmap follow-ups: a gap-label cell is drawn like a not-rated cell, writes its word and keeps its column",()=>{
    var raced=Raced(HeatGrid(s=>s with{CellWidth=90,FitHeight=true}),new ChartPoint(0,null,"2025"){SubLabel="/0 starts",GapLabel="did not race"});
    var svg=ChartSvg.Render(raced);
    Check(svg.Contains("Sprint: 2025 · /0 starts, did not race"),"named with its word");
    Check(svg.Contains(">did not race<"),"written");
    Check(Regex.Matches(svg,"stroke-dasharray='3 2'").Count==2,"dashed, beside the not-rated cell");
    // A season every row missed is still a column.
    var missed=HeatGrid(s=>s with{Series=[new("Sprint",[new ChartPoint(0,null,"2023"){GapLabel="did not race"},new ChartPoint(1,3.1,"2024")]),new("Relay",[new ChartPoint(0,null,"2023"){GapLabel="did not race"},new ChartPoint(1,2.0,"2024")])]});
    Check(Regex.IsMatch(ChartSvg.Render(missed),">2023<"),"its column is labelled");
});
Test("Heatmap follow-ups: YMin, YMax and IncludeZero set a refined heatmap's scale ends; the classic finish ignores them",()=>{
    Check(ChartSvg.Render(HeatGrid(s=>s with{IncludeZero=true})).Contains("Color scale: 0 low to 3.1 high"),"zero");
    Check(ChartSvg.Render(HeatGrid(s=>s with{YMin=0,YMax=5})).Contains("Color scale: 0 low to 5 high"),"set ends");
    // A value past an end takes the end's colour: on Light the default ramp's high end, #4069D0.
    Check(ChartSvg.Render(HeatGrid(s=>s with{YMax=2})).Contains("fill='#4069D0'"),"clamped high");
    // Ends that leave out every value still draw.
    ChartSvg.Render(HeatGrid(s=>s with{YMin=10}));
    ChartSvg.Render(HeatGrid(s=>s with{YMax=-1}));
    var classic=new ChartStyle{Finish=ChartFinish.Classic};
    Check(ChartSvg.Render(HeatGrid(s=>s with{Style=classic,IncludeZero=true,YMin=0,YMax=5}))==ChartSvg.Render(HeatGrid(s=>s with{Style=classic})),"classic unchanged");
});
```

`ChartSvg.Wide` must be internal for the test. If it is `private`, make it `internal`.

The exact strings `0 low to 3.1 high` and `fill='#4069D0'` assume `HeatmapValues` writes 0 as `0`, and that the Light style's default high end is `#4069D0`. If the code writes them otherwise, pin what it writes, and say so in the report.

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release`
Expected: the five new tests FAIL. The reason is not written, the gap cell is skipped, and the scale ignores the ends.

- [ ] **Step 3: Name gap-label cells**

In `HeatmapCellName`, the branch without a value becomes:

```csharp
            : $"{series.Name}: {point.Label ?? columns.Format(point.X)}{Under(point.SubLabel)}" + (point.GapLabel is { } word ? $", {word}{point.ValueNote}" : "");
```

- [ ] **Step 4: The scale**

Replace `LinearScale? scale = values.Length > 0 ? LinearScale.Create(values) : null;` with `LinearScale? scale = values.Length > 0 ? HeatmapScale(s, w.Refined, values) : null;` and add:

```csharp
    /// <summary>A heatmap's colour scale over its rated values. In the refined finish YMin and YMax set its ends and IncludeZero widens it
    /// to 0, used as given; ends that leave out every value still make a scale, which those values take the nearer end of. The classic
    /// finish builds it from the values alone, as 0.23.0 did (0.46.1).</summary>
    private static LinearScale HeatmapScale(ChartSpec s, bool refined, double[] values)
    {
        if (!refined || s.YMin is null && s.YMax is null && !s.IncludeZero) return LinearScale.Create(values);
        double low = values.Min(), high = values.Max();
        if (s.IncludeZero) { low = Math.Min(low, 0); high = Math.Max(high, 0); }
        if (s.YMin is { } min) low = min;
        if (s.YMax is { } max) high = max;
        return LinearScale.Create(low < high ? [low, high] : [low]);
    }
```

**Check that `LinearScale.Create([low, high])` keeps `low` and `high` as its `Min` and `Max`.** If it rounds them to nice numbers, build the scale so the legend reads the ends as given. Say how in the report.

In the cell loop, clamp: `Mix(low, high, Math.Clamp(scale!.Value.Map(p.Y!.Value, 0, 1), 0, 1))`. Without set ends every value lies within the scale, so the clamp changes no existing drawing.

- [ ] **Step 5: Draw gap-label cells and write reasons**

In the cell loop:
- Skip only `if (!p.Y.HasValue && p.NotRated is null && p.GapLabel is null) continue;`.
- A cell is shaded only when `p.NotRated is null && p.GapLabel is null`; otherwise it takes the existing dashed branch.
- The words become:

```csharp
                if (s.CellText)
                {
                    if ((p.NotRated ?? p.GapLabel) is { } said) CellReason(w, x + cw / 2, y + ch / 2, cw, ch, said, p.SubLabel, ink);
                    else CellWords(w, x + cw / 2, y + ch / 2, cw, ch, words.Format(p.Y!.Value), p.SubLabel, ink);
                }
```

Add beside `CellWords`:

```csharp
    /// <summary>Writes a heatmap cell's words where it shows no value: a not-rated cell's reason or a gap-label cell's word, 10 px in
    /// <paramref name="ink"/>, wrapped at word breaks onto as many lines, 12 units apart, as fit 4 inside the cell's height, each within 6
    /// of its width, then its sub-label on a line of its own where one is left. Words that do not fit are cut at a word with "…", and a word
    /// wider than a line by its characters; where not even "…" fits, nothing is written. The block is centred in the cell, its two lines
    /// where the value and sub-label of <see cref="CellWords"/> stand. The cell's name says all of it, so none of it is read (0.46.1).</summary>
    private static void CellReason(SvgWriter w, double cx, double cy, double cw, double ch, string said, string? sub, string ink)
    {
        double room = cw - 6;
        var most = (int)Math.Floor((ch - 4) / 12);
        if (most < 1) return;
        static double Width(string text) => Wide(text) * 10 / 11;
        string Fit(string text)
        {
            if (Width(text) <= room) return text;
            var keep = text.Length;
            while (keep > 1 && Width(Short(text, keep)) > room) keep--;
            return keep > 1 || Width("…") <= room ? Short(text, keep) : "";
        }
        var words = said.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var i = 0;
        while (i < words.Length && lines.Count < most)
        {
            var line = words[i++];
            while (i < words.Length && Width(line + " " + words[i]) <= room) line += " " + words[i++];
            lines.Add(line);
        }
        if (i < words.Length)
        {
            // Words are left over: the last line gives up words until an ellipsis fits after it.
            var last = lines[^1];
            while (Width(last + "…") > room && last.Contains(' ')) last = last[..last.LastIndexOf(' ')];
            lines[^1] = Width(last + "…") <= room ? last + "…" : Fit(last + "…");
        }
        for (var k = 0; k < lines.Count; k++) lines[k] = Fit(lines[k]);
        lines.RemoveAll(line => line.Length == 0);
        if (lines.Count == 0) return;
        var withSub = sub is not null && lines.Count < most && Width(sub) <= room;
        var count = lines.Count + (withSub ? 1 : 0);
        var first = cy - count * 6 + 9;
        const string unread = "pointer-events='none' aria-hidden='true'";
        for (var k = 0; k < lines.Count; k++) w.Text(cx, first + 12 * k, lines[k], $"text-anchor='middle' font-size='10' fill='{ink}' {unread}");
        if (withSub) w.Text(cx, first + 12 * lines.Count, sub, $"text-anchor='middle' font-size='10' fill='{ink}' {unread}");
    }
```

- [ ] **Step 6: Run the tests, and the baseline expecting exactly the eight `heatmap-table/*` rows to change**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release` → all pass.

Some 0.46.0 tests pinned a not-rated cell's value or "—" in the cell. Update only those pins, and list them in the report.

Baseline, refined and classic: only `heatmap-table/light`, `/dark`, `/midnight` and `/cell-width` may differ. They carry a not-rated cell with a value and one without, so both change. Every other row must be identical.

- [ ] **Step 7: Commit**

```bash
git add src/Lumen.Charts/ChartSvg.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Write a not-rated reason or a gap label's word in its heatmap cell, and pin a refined heatmap's scale"
```

---

### Task 4: Export, component and CSS — the grid table's order, the frozen names, the data button, the table's margins

**Files:**
- Modify: `src/Lumen.Charts/ChartExport.cs:68-74` (`GridCell`)
- Modify: `src/Lumen.Charts/ChartSvg.cs` (new `HeatmapNameBand`, sharing the name-drawing code with `Heatmap`)
- Modify: `src/Lumen.Charts.Blazor/LumenChart.razor` (the viewport markup at line 11, the tools row at 24-40, the parameters after `ShowToolbar` at ~88, and `Render()` at ~193-207)
- Modify: `src/Lumen.Charts.Blazor/wwwroot/lumen.css` (lines 1 and 3)
- Test: `tests/Lumen.Charts.Tests/Program.cs`, in the 0.46.1 block.

**Interfaces:**
- Consumes: `ChartSvg.HeatmapFrame(ChartSpec)`, `ChartSvg.HeatmapLeftOf(ChartSpec)` and `ChartSvg.Drawn(ChartSpec)` from Task 2.
- Produces:
  - `internal static (string Svg, double Left, double Top, double Height)? HeatmapNameBand(ChartSpec spec)` on `ChartSvg`.
  - `[Parameter] public bool? ShowDataButton { get; set; }` on `LumenChart`.

- [ ] **Step 1: Write the failing tests**

```csharp
Test("Heatmap follow-ups: a grid table cell reads value, sub-label, note, then why it is not rated; a gap cell its word",()=>{
    var noted=Noted(HeatGrid()," · 34 pts, 5 riders");
    var table=ChartExport.HtmlTable(noted);
    // The separator the table writes before a sub-label is a plain ·; a · inside a note or a sub-label is encoded with the rest of it.
    Check(table.Contains("<td>2.8 · /12 starts &#183; 34 pts, 5 riders</td>"),table);
    Check(table.Contains("<td>1.2 · /4 starts, not rated: too few starts to rate</td>"),"not rated");
    var raced=ChartExport.HtmlTable(Raced(HeatGrid(),new ChartPoint(0,null,"2025"){SubLabel="/0 starts",GapLabel="did not race"}));
    Check(raced.Contains("<td>did not race · /0 starts</td>"),raced);
});
Test("Heatmap follow-ups: the name band holds the drawing's row names, where it draws them, and nothing else",()=>{
    var spec=Named(HeatGrid(s=>s with{CellWidth=72,FitHeight=true}),"Junior 18/19 Girls");
    var band=ChartSvg.HeatmapNameBand(spec)!.Value;
    var frame=ChartSvg.HeatmapFrame(spec);
    Check(band.Left==frame.Left&&band.Top==frame.Top&&band.Height==frame.Bottom-frame.Top,"the frame");
    var drawn=ChartSvg.Render(spec);
    foreach(var name in new[]{"Junior 18/19 Girls","Long distance"})
    {
        var at=Regex.Match(drawn,$"<text x='([^']*)' y='([^']*)'[^>]*>{Regex.Escape(name)}<");
        Check(at.Success&&band.Svg.Contains($"<text x='{at.Groups[1].Value}' y='{at.Groups[2].Value}'")&&band.Svg.Contains($">{name}<"),name);
    }
    Check(band.Svg.Contains($"viewBox='0 {band.Top} {band.Left} {band.Height}'"),"cropped to the band");
    Check(band.Svg.Contains("aria-hidden='true'")&&!band.Svg.Contains("lumen-datum")&&!band.Svg.Contains("tabindex"),"no marks, not read");
    Check(ChartSvg.HeatmapNameBand(HeatGrid())is null&&ChartSvg.HeatmapNameBand(Spec())is null,"only for a heatmap with a cell width");
});
Test("Heatmap follow-ups: the component freezes a fixed-width heatmap's names, from the rows shown, and pads its scrolling by them",()=>{
    var spec=Named(HeatGrid(s=>s with{CellWidth=72}),"Junior 18/19 Girls");
    var html=Operate(spec,async c=>{await c.Fit(340);},fit:true);
    Check(html.Contains("<div class=\"lumen-freeze\" aria-hidden=\"true\" inert"),"the layer");
    Check(Regex.IsMatch(html,"<div class=\"lumen-viewport\"[^>]*scroll-padding-left:145px"),"scroll padding "+Regex.Match(html,"<div class=\"lumen-viewport\"[^>]*>").Value);
    // A hidden row leaves the band as it leaves the drawing.
    var hidden=Operate(spec,async c=>{typeof(LumenChart).GetMethod("Toggle",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(c,[0]);await Task.CompletedTask;},fit:true);
    var layer=Regex.Match(hidden,"<div class=\"lumen-freeze\".*?</div>",RegexOptions.Singleline).Value;
    Check(!layer.Contains("Junior 18/19 Girls")&&layer.Contains("Long distance"),"shown rows only");
    Check(!Operate(HeatGrid(),async c=>{await c.Fit(340);},fit:true).Contains("lumen-freeze"),"no layer without a cell width");
});
Test("Heatmap follow-ups: ShowDataButton keeps View data with the toolbar off, and follows the toolbar when unset",()=>{
    string Tools(Dictionary<string,object?> p)=>Regex.Match(ChartMarkup(Spec(),p),"<div class=\"lumen-tools[^\"]*\">.*?</div>",RegexOptions.Singleline).Value;
    var only=Tools(new(){{"ShowToolbar",false},{"ShowDataButton",true}});
    Check(only.Contains("View data")&&!only.Contains("Export SVG"),only);
    Check(!Tools(new(){{"ShowToolbar",false}}).Contains("View data"),"follows the toolbar");
    Check(Tools(new()).Contains("View data")&&Tools(new()).Contains("Export SVG"),"default");
});
Test("Heatmap follow-ups: the stylesheet freezes the layer, and keeps 24-pixel table margins for the component's own table only",()=>{
    var css=File.ReadAllText(Path.Combine(Root,"src","Lumen.Charts.Blazor","wwwroot","lumen.css"));
    Check(css.Contains(".lumen-freeze{position:sticky;left:0"),"sticky");
    Check(css.Contains(".lumen-table{overflow:auto;max-height:320px;margin:10px 0;")&&css.Contains(".lumen-chart>.lumen-table{margin:10px 24px}"),"margins");
});
```

The tests file has its own helpers for component markup and the repository root. `Operate` takes a spec and an action, and `fit` is the `FitWidth` flag. Use them under whatever names they have; add `ChartMarkup(spec, parameters)` only if no helper renders `LumenChart` with arbitrary parameters. Toggle a row the way the existing legend tests do, if they have a way; reflection on `Toggle` is the fallback.

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release`
Expected: the build fails because `HeatmapNameBand` and `ShowDataButton` do not exist yet.

- [ ] **Step 3: Reorder the grid cell (`ChartExport.cs`)**

```csharp
    private static string GridCell(ChartPoint? p,Axis values)
    {
        if(p is null||!p.Y.HasValue&&p.NotRated is null&&p.GapLabel is null)return "";
        var cell=WebUtility.HtmlEncode(p.Y.HasValue?values.Format(p.Y.Value):p.GapLabel??"—");
        if(p.SubLabel is not null)cell+=" · "+WebUtility.HtmlEncode(p.SubLabel);
        if(p.ValueNote is not null)cell+=WebUtility.HtmlEncode(p.ValueNote);
        if(p.NotRated is not null)cell+=", not rated: "+WebUtility.HtmlEncode(p.NotRated);
        return cell;
    }
```

Update its comment to the new order. Update the 0.46.0 test pins that read the old order (value, note, sub-label), and list them in the report.

- [ ] **Step 4: Add the name band (`ChartSvg.cs`)**

Move the row-name drawing out of `Heatmap` into a private helper both use:

```csharp
    /// <summary>A heatmap's row names as it draws them: each at the name column's edge, less 12, in the middle of its row, cut to its room.</summary>
    private static IEnumerable<(double X, double Y, string Text)> HeatmapNames(ChartSpec s, double left, double top, double ch) =>
        s.Series.Select((series, si) => (left - 12, top + (si + .5) * ch + 4, Fitted(series.Name, left == HeatmapLeft ? left - 12 : left - 18)));
```

In `Heatmap`, loop over `HeatmapNames(s, left, top, ch)` and write each with `w.Text(x, y, text, "text-anchor='end' class='lumen-muted'")` before its row's cells. The order of the markup must not change, so the baseline does not move: write the name for row `si` just before that row's cells, as today.

Then add:

```csharp
    /// <summary>The row names of a heatmap with a cell width, as <see cref="Render"/> draws them, in a drawing of their own cropped to
    /// the name column beside the grid's rows, painted in the chart's background. The component lays it over the scrolling drawing, held
    /// at the left, so the names stay in view as the cells scroll (0.46.1). It holds no marks and is hidden from reading, since the
    /// drawing beneath says it all. Null for any other chart.</summary>
    internal static (string Svg, double Left, double Top, double Height)? HeatmapNameBand(ChartSpec spec)
    {
        if (spec.Kind != ChartKind.Heatmap || spec.CellWidth is null) return null;
        ChartValidation.Validate(spec);
        var s = Drawn(spec);
        var style = ResolveStyle(s);
        var (left, top, bottom) = HeatmapFrame(spec);
        var ch = (bottom - top) / s.Series.Count;
        var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 {N(top)} {N(left)} {N(bottom - top)}' width='{N(left)}' height='{N(bottom - top)}' aria-hidden='true' focusable='false' font-family='{FontFamily}' font-size='12'>");
        svg.Append($"<rect x='0' y='{N(top)}' width='{N(left)}' height='{N(bottom - top)}' fill='{style.Background}'/>");
        foreach (var (x, y, text) in HeatmapNames(s, left, top, ch))
            svg.Append($"<text x='{N(x)}' y='{N(y)}' text-anchor='end' fill='{style.Muted}'>{WebUtility.HtmlEncode(text)}</text>");
        return (svg.Append("</svg>").ToString(), left, top, bottom - top);
    }
```

Match the root's font to what `Begin` writes: use its font family and size, whatever the constant or attribute is called. Match the name text colour to what `class='lumen-muted'` resolves to in the drawing, normally `Style.Muted`. Use the drawing's own text encoding for the text (`w.Text` encodes; reuse its encoder). The test compares `x`, `y` and the text with the drawing's, so the encoding of a name with `&` or `<` must match too.

- [ ] **Step 5: The component (`LumenChart.razor`)**

Parameters, after `ShowToolbar`:

```csharp
    /// <summary>Draws the button that shows the data table. Unset, it follows <see cref="ShowToolbar"/>; set true with the toolbar off,
    /// the tools row keeps this one button, so a small card can drop zoom and exports and keep its data a click away (0.46.1).</summary>
    [Parameter] public bool? ShowDataButton { get; set; }
    private bool DataButton => ShowDataButton ?? ShowToolbar;
    // A fixed-width heatmap's row names, drawn again in a layer held at the viewport's left while its cells scroll under it (0.46.1).
    private (string Svg, double Left, double Top, double Height)? band;
    private string? FreezeStyle => band is { } b ? FormattableString.Invariant($"margin-top:{b.Top}px;width:{b.Left}px;height:{b.Height}px;--lumen-freeze-bg:{Style.Background};--lumen-freeze-line:{Style.Grid}") : null;
```

`DrawnStyle` gains the scroll padding: `drawnWidth is { } drawn ? $"--lumen-drawn:{drawn}px" + (band is { } b ? FormattableString.Invariant($";scroll-padding-left:{b.Left}px") : "") : null`.

Viewport (line 11): after `@((MarkupString)svg)` add:

```razor
@if(band is { } frozen){<div class="lumen-freeze" aria-hidden="true" inert style="@FreezeStyle">@((MarkupString)frozen.Svg)</div>}
```

`Render()`: after `drawnWidth=…`, add `band=ChartSvg.HeatmapNameBand(shown);`.

Tools row:
- The class becomes `ShowToolbar ? "lumen-tools" : DataButton ? "lumen-tools lumen-hush" : "lumen-tools lumen-quiet"`.
- Keep every toolbar button inside `@if(ShowToolbar)` except "View data".
- Write "View data" under `@if(DataButton)`, in the same place, so the default markup is byte for byte what 0.46.0 wrote. An existing test checks the toolbar markup; it must still pass unchanged.

- [ ] **Step 6: The stylesheet (`lumen.css`)**

Line 1:
- `.lumen-table{overflow:auto;max-height:320px;margin:10px 24px;font-size:12px}` becomes `.lumen-table{overflow:auto;max-height:320px;margin:10px 0;font-size:12px}.lumen-chart>.lumen-table{margin:10px 24px}`.
- `.lumen-quiet .lumen-status{…}` becomes `.lumen-quiet .lumen-status,.lumen-hush .lumen-status{…}` with the same declarations.

Line 3: append:

```css
.lumen-fixed>.lumen-viewport{display:grid}.lumen-fixed>.lumen-viewport>svg,.lumen-freeze{grid-area:1/1}.lumen-freeze{position:sticky;left:0;z-index:1;align-self:start;justify-self:start;background:var(--lumen-freeze-bg);box-shadow:1px 0 0 var(--lumen-freeze-line)}.lumen-freeze>svg{display:block}
```

- [ ] **Step 7: Run the tests and the baseline**

Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release` → all pass.

Baseline: unchanged from Task 3. The band is not hashed, and `Heatmap`'s markup is unchanged.

- [ ] **Step 8: Commit**

```bash
git add src/Lumen.Charts/ChartExport.cs src/Lumen.Charts/ChartSvg.cs src/Lumen.Charts.Blazor/LumenChart.razor src/Lumen.Charts.Blazor/wwwroot/lumen.css tests/Lumen.Charts.Tests/Program.cs
git commit -m "Freeze a fixed-width heatmap's row names, keep View data without the toolbar, and read grid cells value, sub-label, note"
```

---

### Task 5: Baseline rows, the gallery card, browser and HTTP checks

**Files:**
- Modify: `tests/Lumen.Charts.Baseline/Program.cs`. Add six rows per finish after the four `heatmap-table/*` rows (~854-871).
- Modify: `samples/Lumen.Gallery/SportsData.cs` (the Category heatmap card ~119-134 and its spec ~1091-1103) and the page that hosts it.
- Modify: `tests/Lumen.Charts.BrowserTests/Program.cs` (the category-heatmap tests ~1732-1790 and the sweeps ~1973-2021)
- Modify: `tests/verify-api.ps1` (the category-heatmap check ~206)
- Test: the browser suite, verify-api and the baseline.

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces: the final counts Task 6 records.

- [ ] **Step 1: Add the baseline rows**

Each is built from the existing `heatmap-table` data in the harness (invented categories and seasons). Use the names below verbatim:

```csharp
// 0.46.1: heatmap follow-ups.
("heatmap-table/top",       table with { ColumnLabelsOnTop = true }),
("heatmap-table/fit",       table with { FitHeight = true, Source = null }),
("heatmap-table/long-name", table with { Series = [table.Series[0] with { Name = "Junior mixed team relay" }, .. table.Series.Skip(1)] }),
("heatmap-table/no-race",   /* the table with one season every row missed, each cell GapLabel = "did not race" and Y = null */),
("heatmap-table/zero",      table with { IncludeZero = true }),
("heatmap-table/reasons",   table with { CellWidth = 90, FitHeight = true }),
```

Follow the harness's own row syntax and its `Render`/`Finished` helper; `table` is whatever the four existing rows are built from. For `no-race`, write the spec out in full, adding one season X at the end with `new ChartPoint(x, null, "2027") { SubLabel = "/0 starts", GapLabel = "did not race" }` in every row.

- [ ] **Step 2: Run the baseline and check that exactly the planned rows moved**

From `tests/Lumen.Charts.Baseline`, run `dotnet run -c Release` and `dotnet run -c Release -- classic`, each with its CRLF-blind diff.

Expected in each finish:
- the four `heatmap-table/{light,dark,midnight,cell-width}` rows change;
- the six rows above are added;
- 398 rows in all;
- no other line differs.

- [ ] **Step 3: Update the gallery card (invented data only)**

The Category heatmap card on the Sports & performance page:
- **Spec:** `CellWidth = 90`, `ColumnLabelsOnTop = true`, `FitHeight = true`.
- **Data:**
  - Rename the invented "Relay" row "Junior mixed team relay", so the name column widens.
  - Make its 2023 cell `GapLabel = "did not race"` with `Y = null` (no `NotRated`).
  - Keep the other not-rated cells' `NotRated = "too few starts to rate"`.
  - Keep `ValueNote` with its own separator.
- **Card:** draw it with `<LumenChart FitWidth ShowToolbar="false" ShowDataButton="true">`.

Update every check that reads the card's names, widths, texts or table. The drawn width becomes `HeatmapLeftOf + 35 + 4 × 90`; work it out from the code and write it into the checks. The chart count (33) and the fitted count (30) stay.

- [ ] **Step 4: Browser checks (`tests/Lumen.Charts.BrowserTests/Program.cs`)**

**Phone.** In the category-heatmap phone test (375 px, touch, scale 2), add:

```csharp
// The row names stay where they are while the cells scroll under them.
var before=await sports.EvaluateAsync<double[]>(@"() => { const c=document.querySelector('#category-heatmap'); const b=c.querySelector('.lumen-freeze'); const s=c.querySelector('.lumen-viewport > svg'); return [b.getBoundingClientRect().left, s.getBoundingClientRect().left]; }");
await sports.EvaluateAsync("() => document.querySelector('#category-heatmap .lumen-viewport').scrollBy(200,0)");
var after=await sports.EvaluateAsync<double[]>(@"() => { const c=document.querySelector('#category-heatmap'); const b=c.querySelector('.lumen-freeze'); const s=c.querySelector('.lumen-viewport > svg'); return [b.getBoundingClientRect().left, s.getBoundingClientRect().left]; }");
Check(Math.Abs(after[0]-before[0])<1,"names held: "+string.Join(",",before)+" -> "+string.Join(",",after));
Check(before[1]-after[1]>150,"cells scrolled");
// The layer is never read, focused or pointed at through.
var layer=await sports.EvaluateAsync<string[]>(@"() => { const b=document.querySelector('#category-heatmap .lumen-freeze'); return [b.getAttribute('aria-hidden'), String(b.inert), String(b.querySelectorAll('[tabindex],a,button').length)]; }");
Check(layer.SequenceEqual(new[]{"true","true","0"}),string.Join(",",layer));
```

**Keyboard.** Add a test that focuses the first cell, presses End to move to the last cell of its row, then presses Home. After Home, the focused cell's `getBoundingClientRect().left` must be at least the frozen layer's right edge less 1. This is Review Focus 4.

**View data.** The test opens "View data" through the only button in the card's tools row: the row holds exactly one button, "View data". Update the expected cell texts to the new order: value · sub-label · note.

**Sweeps.** Keep the axe sweeps (light, dark, Midnight, and with the data open) and make sure they still pass. The page-width and drawn-at-viewBox checks still skip exactly one `.lumen-fixed` chart.

- [ ] **Step 5: The HTTP check (`tests/verify-api.ps1`)**

Update the category-heatmap prerender check to:
- the new name: the whole "Junior mixed team relay";
- `did not race` in a cell's name;
- `not rated: too few starts to rate` three times, or as the card's data gives it;
- `lumen-freeze`;
- the new `--lumen-drawn` width.

Keep the count of HTTP checks the same unless a new `Verify` is truly needed; if one is, record the new total.

- [ ] **Step 6: Run everything**

- Build `tests/Lumen.Charts.BrowserTests` and `samples/Lumen.Wasm` explicitly.
- Run the unit tests and the baseline as in Step 2.
- Start the gallery (`dotnet run --project samples/Lumen.Gallery -c Release --no-build --urls http://localhost:5188`), run `pwsh -File tests/verify-api.ps1 -BaseUrl http://localhost:5188`, and run `dotnet run --project tests/Lumen.Charts.BrowserTests -c Release --no-build`.
- Start the WebAssembly host on 5199, run the browser suite with `http://localhost:5199`, and stop it by PID.

Expected: all pass. Write every count into the report.

Save screenshots of the card outside the repo, under `%TEMP%/lumen0461/`: 1400 light, dark and Midnight; 375 before and after scrolling. List them in the report.

- [ ] **Step 7: Commit**

```bash
git add tests/Lumen.Charts.Baseline/Program.cs samples/Lumen.Gallery/SportsData.cs tests/Lumen.Charts.BrowserTests/Program.cs tests/verify-api.ps1
git commit -m "Hash the heatmap follow-ups and show them on the Category heatmap card"
```

Also add the page that hosts the card if it changed, by explicit path.

---

### Task 6: Docs, recipe, version

**Files:**
- Modify: `Directory.Build.props` (version 0.46.1)
- Modify: `README.md` (the "Heatmap tables" section, a "0.46.1 additions" section above 0.46.0's, the limits, and the host-CSS note)
- Modify: `integrations/claude-code/lumen-charts/references/api.md`, `integrations/claude-code/lumen-charts/SKILL.md`
- Modify: `integrations/claude-code/lumen-charts/references/recipes-race-face.md` (the "Category heatmap" recipe)
- Modify: `docs/VERIFICATION.md`
- Test: `tests/Lumen.Charts.Recipes` (`python check.py`, then `dotnet run -c Release`)

**Interfaces:**
- Consumes: the behaviour and counts from Tasks 1–5.

- [ ] **Step 1: Bump the version**

In `Directory.Build.props`, set the version to `0.46.1`.

- [ ] **Step 2: README**

Every statement must match the code; copy messages and names from the unit tests.

- **Heatmap tables section:**
  - the 40-character note on heatmap cells;
  - `GapLabel` cells and their name;
  - reasons written in cells: two lines at `CellWidth` 90 fit "too few starts to rate";
  - `YMin`/`YMax`/`IncludeZero`, refined only;
  - `ColumnLabelsOnTop`;
  - the measured name column (130–240);
  - `FitHeight` at 36 a row;
  - the frozen names in `<LumenChart>` (static SVG does not freeze);
  - `ShowDataButton`;
  - the grid table's new order.
- **0.46.1 additions:** a short list, pointing at the section.
- **Limits:**
  - static SVG does not freeze;
  - no sticky header row;
  - `FitHeight` rows are always 36;
  - a reason longer than the cell is cut with "…".
- **Host-CSS note:** a rule like `svg{max-width:100% !important}` stretches or squeezes a fixed-width chart. Exclude Lumen's drawings, for example `svg:not(.lumen-chart svg)`.

- [ ] **Step 3: API reference and skill**

- **`references/api.md`:**
  - `ColumnLabelsOnTop`;
  - `FitHeight` on heatmaps;
  - `GapLabel` on heatmap cells;
  - the 40-character `ValueNote` on heatmap cells;
  - `ShowDataButton`;
  - `HtmlTable`'s order;
  - the frozen names;
  - the `.lumen-table` margins.
- **`SKILL.md`:** update the heatmap lines. Measure the frontmatter description: it must stay ≤ 1024 characters with no unquoted `: `.

- [ ] **Step 4: The recipe**

Update "Category heatmap" in `recipes-race-face.md`:
- Invented data.
- `CellWidth = 90`, `ColumnLabelsOnTop = true`, `FitHeight = true`.
- A "did not race" season through `GapLabel`.
- Reasons written in cells.
- A long invented category name.
- `ValueNote` with its own separator, up to 40 characters.
- The component call `<LumenChart Spec="…" FitWidth="true" ShowToolbar="false" ShowDataButton="true" />`.
- `ChartExport.HtmlTable` for a static page.

Run `python check.py` and `dotnet run -c Release` in `tests/Lumen.Charts.Recipes`. Both must pass. Record the recipe and chart counts.

- [ ] **Step 5: VERIFICATION.md**

Add 0.46.1's paragraph and results:
- unit, baseline (398 rows per finish, the four changed rows and six added), HTTP, browser (gallery and WebAssembly) and recipes;
- every count from your own run at your final commit.

- [ ] **Step 6: Commit**

```bash
git add Directory.Build.props README.md integrations/claude-code/lumen-charts/references/api.md integrations/claude-code/lumen-charts/SKILL.md integrations/claude-code/lumen-charts/references/recipes-race-face.md docs/VERIFICATION.md
git commit -m "Document the heatmap follow-ups and set the version to 0.46.1"
```
