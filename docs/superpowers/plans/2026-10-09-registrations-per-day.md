# Registrations per Day (0.47.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let any app draw counts per day as fixed-pitch, sideways-scrolling columns with a visible zero, two-level date labels and a named, plural-aware tooltip per bar. The pieces are general column features, a general `ChartPoint.Name`, `<LumenChart ScrollToEnd>`, and a `DailyCountsChart.Build` builder.

**Architecture:**
- **Column features:** column charts gain a fixed pitch (`CellWidth`), a static drawn width, drawn-only `AxisLabel`s with measured thinning, a `MarkZero` hairline, and value labels sized to the pitch. All of it is opt-in in `ChartSvg`/`ChartValidation`.
- **Names:** `ChartPoint.Name` overrides a mark's name in `Datum` and in the component's status line.
- **Scrolling:** `ScrollToEnd` is one script call.
- **Builder:** a new `DailyCounts.cs` turns `DayCount`s into an ordinary `ChartSpec` using those pieces.
- **Recipes:** the #20 course profile is a recipe only.

**Tech Stack:** C# / .NET 8, Blazor, Playwright.
- Unit assertion suite: `tests/Lumen.Charts.Tests` (`Test`/`Check`/`Reject`, `dotnet run`).
- Baseline hash harness: `tests/Lumen.Charts.Baseline`.
- Browser suite: `tests/Lumen.Charts.BrowserTests`.
- HTTP checks: `tests/verify-api.ps1`.
- Recipes: `tests/Lumen.Charts.Recipes`.

**Spec:** `docs/superpowers/specs/2026-10-09-registrations-per-day-design.md` (approved; amended 09e9869). Read it before starting any task.

## Global Constraints

- **Invented data only:** no real rider's, member's or account's name, number, age or result anywhere (code, tests, samples, docs, commit messages).
- **Never colour alone:** a zero day is said in its name ("0 registrations") and in its "0" label. The muted colour is emphasis only.
- **Drawn text clears 4.5:1:** the muted "0" uses `Style.Muted` only where `Contrast.Ratio(Style.Muted, Style.Background) >= 4.5`, else the text colour.
- **The classic finish stays byte for byte 0.23.0** for every spec 0.23.0 could draw. New features are opt-in and drawn in both finishes.
- **New spec properties stay out of the gradient-ID hash at their defaults.** Null properties drop out by themselves; a new `bool` needs a `ShouldSerialize` rule.
- **What may move:** without the new properties, every spec renders byte for byte as in 0.46.2, except the static root style of a `CellWidth` drawing (§1.2). Exactly six baseline rows move, in both finishes: `heatmap-table/cell-width`, `heatmap-table/reasons`, `heatmap-cells/notes`, `heatmap-cells/notes-fixed`, `heatmap-cells/keeps-value`, `heatmap-cells/both-dark`.
- **XML docs:** every new public member needs an XML doc comment; the build fails without one. A Release build shows 0 warnings.
- **Processes:**
  - Stop `Lumen.Gallery.exe` only by image name: `taskkill //IM Lumen.Gallery.exe //F`, before building, since it locks the output.
  - Stop every other process only by PID: `netstat -ano` → PID → `taskkill //PID <pid> //F`. Never `taskkill /IM` anything else.
- **The Race Face brief:** never open, read or search `lumen-charts-race-face-brief.md` in the repo root. Exclude it from every recursive search; search `src/`, `tests/`, `samples/`, `docs/` and `integrations/` only.
- **Git:**
  - Commit with explicit paths, never `git add .` or `-A`.
  - Never push, merge, rebase or switch branches.
  - Allowed: `git add <paths>`, `commit`, `status`, `diff`, `log`.
- **Files outside the repo:**
  - Delete any `.playwright-mcp` folder you create.
  - Screenshots go outside the repo (`%TEMP%\lumen0470\`); list them in your report.
- **The skill's frontmatter description** stays ≤ 1,024 characters with no unquoted `: `.

## Review Focus

1. **A fixed-pitch column chart narrower than the 320 floor**, e.g. 3 days at 30. The plot keeps its pitch (its right edge at `left + 3 × 30`), the extra room sits at the right, and no axis or value label is cut. *Task 2.*
2. **`Name` with series hidden in the component.** `<LumenChart>` draws a spec with hidden series removed, so `Datum`'s series index is an index into the drawn spec. The `Name` lookup must use the drawn spec (`w.Spec`), never the host's. *Task 4.*
3. **A range of exactly one day, and of exactly 4,000 days** (4,001 refused). One day gives one bar, "1 day" in the description and a two-line first label. *Task 5.*
4. **Negative values beside `MarkZero`.** Only a value of exactly 0 becomes a hairline. A negative column is drawn as today, and its label stays in the text colour. *Task 3.*
5. **`ScrollToEnd` on a chart that does not scroll,** or with the script not yet attached. Nothing throws, nothing moves, and prerendered markup is unchanged apart from nothing. *Task 4 (unit), Task 6 (browser).*

---

### Task 1: Model and validation

**Files:**
- Modify `src/Lumen.Charts/Models.cs`:
  - `ChartPoint`: add `AxisLabel` and `Name` after `NotRated` (~359).
  - `ChartSeries`: add `MarkZero` as its last property, after `AverageOf` (~505).
  - `ChartSpec.CellWidth` doc (~733-736).
- Modify `src/Lumen.Charts/ChartValidation.cs`:
  - the combined check at 111-118;
  - the point loop (~252-260);
  - the category block (~339-349);
  - a new `AxisLabel` helper beside `SubLabel` (~645);
  - a new `PointName` helper;
  - the series check beside `ValueLabels` (~722).
- Modify `src/Lumen.Charts/ChartSvg.cs`: the `Unfitted` hash modifier (~105-118) and its doc.
- Test: `tests/Lumen.Charts.Tests/Program.cs`:
  - the pins of the combined message (~9733) and of CellWidth floors and caps (~9720-9755);
  - the default-JSON pin (~4241-4251);
  - a new `// 0.47.0` block after the last 0.46.2 test, before the `ChartMarkup` helper.

**Interfaces:**
- Produces:
  - `public string? ChartPoint.AxisLabel`;
  - `public string? ChartPoint.Name`;
  - `public bool ChartSeries.MarkZero`;
  - `internal const int ChartValidation.MaxFixedCategories = 4000`.

  All later tasks read these.

- [ ] **Step 1: Write the failing tests.** Add this block:

```csharp
// 0.47.0: registrations per day.
ChartSpec Days(int count,double? cell=30)=>new(){Title="Sign-ups",Kind=ChartKind.Column,CellWidth=cell,Series=[new("Sign-ups",Enumerable.Range(0,count).Select(i=>new ChartPoint(i,i%4,$"D{i}")).ToArray())]};
Test("Daily counts: CellWidth takes column charts at a pitch of 8 or more and up to 4,000 categories, and is refused on other kinds and sparklines",()=>{
    ChartValidation.Validate(Days(10,8));
    ChartValidation.Validate(Days(4000,8));
    foreach(var bad in new[]{Days(10,7.9),Days(10,double.NaN),Days(4001,8),Days(101,null) with{CellWidth=null},Spec(ChartKind.Bar) with{CellWidth=30},Spec(ChartKind.StackedColumn) with{CellWidth=30},Days(10) with{Sparkline=true,Width=120,Height=40}})
        Reject(()=>ChartValidation.Validate(bad));
    string Why(ChartSpec s){try{ChartValidation.Validate(s);}catch(ArgumentException e){return e.Message;}return "";}
    Check(Why(Days(10,7)).StartsWith("A column chart's category is at least 8 pixels wide"),Why(Days(10,7)));
    Check(Why(Spec(ChartKind.Line) with{CellWidth=30}).StartsWith("CellWidth sets the width of a heatmap's columns or a column chart's categories, so it applies to heatmap and column charts only;"),Why(Spec(ChartKind.Line) with{CellWidth=30}));
    Check(Why(Spec(ChartKind.Line) with{CellText=true}).StartsWith("Cell text writes in a heatmap's cells and ColumnLabelsOnTop moves its column labels above its grid, so they apply to heatmap charts only;"),Why(Spec(ChartKind.Line) with{CellText=true}));
    Check(Why(Days(101,null)).Contains("4,000 on a column chart with CellWidth"),Why(Days(101,null)));
    Check(Why(Days(4001,8)).StartsWith("A column chart with CellWidth takes at most 4,000 categories"),Why(Days(4001,8)));
    // The 4,096-unit width cap stays a heatmap's: 4,000 columns at 30 are far wider and accepted.
    ChartValidation.Validate(Days(4000,30));
});
Test("Daily counts: an axis label is one or two short lines under a column, one per category, and never beside a sub-label",()=>{
    ChartSpec Labelled(params string?[] labels)=>Days(labels.Length) with{Series=[new("Sign-ups",labels.Select((l,i)=>new ChartPoint(i,i,$"D{i}"){AxisLabel=l}).ToArray())]};
    ChartValidation.Validate(Labelled("6","7\nOct","8",null));
    ChartValidation.Validate(Labelled("31","1\nJan 2027"));
    foreach(var bad in new[]{Labelled("1\nOct\n2026"),Labelled("1\n"),Labelled(" "),Labelled("Thirteen chrs"),Labelled("1\nOct\t"),Labelled("1\r\nOct")})
        Reject(()=>ChartValidation.Validate(bad));
    Reject(()=>ChartValidation.Validate(Spec(ChartKind.Line) with{Series=[new("S",[new ChartPoint(0,1,"A"){AxisLabel="1"}])]}));
    // Two series may repeat a category's axis label, never give different ones.
    var two=Days(2) with{Series=[new("A",[new ChartPoint(0,1,"D0"){AxisLabel="1"}]),new("B",[new ChartPoint(0,2,"D0"){AxisLabel="1"}])]};
    ChartValidation.Validate(two);
    Reject(()=>ChartValidation.Validate(two with{Series=[two.Series[0],new("B",[new ChartPoint(0,2,"D0"){AxisLabel="2"}])]}));
    var both=Days(2) with{Series=[new("A",[new ChartPoint(0,1,"D0"){AxisLabel="1"},new ChartPoint(1,1,"D1"){SubLabel="x"}])]};
    try{ChartValidation.Validate(both);Check(false,"axis and sub-labels together were accepted");}catch(ArgumentException e){Check(e.Message.StartsWith("A column chart writes either axis labels or sub-labels under its columns, not both."),e.Message);}
});
Test("Daily counts: MarkZero is for series drawn as columns, and a point's Name is one line of up to 200 characters on marks of its own",()=>{
    ChartValidation.Validate(Days(3) with{Series=[Days(3).Series[0] with{MarkZero=true}]});
    ChartValidation.Validate(Spec(ChartKind.Line) with{Series=[new("S",[new ChartPoint(0,0),new ChartPoint(1,2)]){Kind=ChartKind.Column,MarkZero=true}]});
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Bar,ChartKind.StackedColumn,ChartKind.Area})
    {
        var s=Spec(kind) with{Series=[Spec(kind).Series[0] with{MarkZero=true}]};
        try{ChartValidation.Validate(s);Check(false,$"{kind} took MarkZero");}catch(ArgumentException e){Check(e.Message.StartsWith("MarkZero draws a zero column as a hairline, so it applies to series drawn as columns only."),e.Message);}
    }
    ChartValidation.Validate(Spec(ChartKind.Line) with{Series=[new("S",[new ChartPoint(0,1,"A"){Name="Tue 6 Oct 2026: 1 registration"},new ChartPoint(1,2,"B")])]});
    ChartValidation.Validate(HeatGrid(s=>s with{Series=[s.Series[0] with{Points=[s.Series[0].Points[0] with{Name="Sprint 2025, 2.8 points a start"},s.Series[0].Points[1]]},s.Series[1]]}));
    foreach(var bad in new[]{""," ",new string('x',201),"two\nlines","tab\there"})
        Reject(()=>ChartValidation.Validate(Spec(ChartKind.Line) with{Series=[new("S",[new ChartPoint(0,1,"A"){Name=bad}])]}));
    foreach(var kind in new[]{ChartKind.Histogram,ChartKind.Box,ChartKind.Violin})
        Reject(()=>ChartValidation.Validate(Sample(kind) with{Series=[Sample(kind).Series[0] with{Points=[Sample(kind).Series[0].Points[0] with{Name="one"},..Sample(kind).Series[0].Points.Skip(1)]}]}));
    Reject(()=>ChartValidation.Validate(Sample(ChartKind.Calendar) with{Series=[Sample(ChartKind.Calendar).Series[0] with{Points=[Sample(ChartKind.Calendar).Series[0].Points[0] with{Name="one"},..Sample(ChartKind.Calendar).Series[0].Points.Skip(1)]}]}));
});
Test("Daily counts: the new properties left at their defaults change no drawing and no gradient ID, and set they count",()=>{
    var plain=Days(5);
    Check(ChartSvg.IdPrefix(plain)!=ChartSvg.IdPrefix(plain with{Series=[plain.Series[0] with{MarkZero=true}]}),"MarkZero counts when set");
    Check(ChartSvg.IdPrefix(plain)!=ChartSvg.IdPrefix(plain with{Series=[plain.Series[0] with{Points=[plain.Series[0].Points[0] with{AxisLabel="1"},..plain.Series[0].Points.Skip(1)]}]}),"AxisLabel counts when set");
    Check(ChartSvg.IdPrefix(plain)!=ChartSvg.IdPrefix(plain with{Series=[plain.Series[0] with{Points=[plain.Series[0].Points[0] with{Name="one"},..plain.Series[0].Points.Skip(1)]}]}),"Name counts when set");
});
```

`Sample(ChartKind.Calendar)` already exists in the helper at the top of the file. Check that it gives a calendar spec. If not, build a two-day calendar spec inline instead.

- [ ] **Step 2: Update the pins this task moves.**
  - **The combined-message pin (~9733):** replace it with two pins.
    - `CellText` on Line and Calendar: the new message starts `"Cell text writes in a heatmap's cells and ColumnLabelsOnTop moves its column labels above its grid, so they apply to heatmap charts only;"`.
    - `CellWidth=30` on Line and Calendar: it starts `"CellWidth sets the width of a heatmap's columns or a column chart's categories, so it applies to heatmap and column charts only;"`.
  - **The other pins (~9720-9755):** keep the heatmap messages ("A heatmap's cell width is a finite number of pixels, at least 24…", "A heatmap's cell is at least 24 pixels wide…" and the 4,096 messages). They are unchanged for heatmaps.
  - **The default-JSON pin (~4241-4251):**
    - In the comment, add `and since 0.47.0 each series' zero hairline, written last in the series`.
    - Add `Check(json.Contains(",\"MarkZero\":false"),"the series' MarkZero")`.
    - In the `before` reconstruction, add `.Replace(",\"MarkZero\":false","")`, so the 0.25.0 hash still matches.

- [ ] **Step 3: Run and see it fail.**
  - Run `taskkill //IM Lumen.Gallery.exe //F`, then `dotnet build Lumen.Charts.slnx -c Release`.
  - Expected: the build fails with `'ChartPoint' does not contain a definition for 'AxisLabel'`.

- [ ] **Step 4: The model** (`Models.cs`).
  - **`ChartPoint`, after `NotRated`:**

```csharp
    /// <summary>
    /// Drawn-only words under a column, in place of its category's name (0.47.0): one or two lines split at <c>\n</c>, each 1 to 12
    /// characters, such as <c>6</c> or <c>1\nOct</c>. The first line stands where the name would, 12 px and muted; the second 14 units
    /// under it, 11 px and muted, and the room under the plot grows 14 for it, as for a sub-label. The category's <see cref="Label"/> still
    /// names it in the mark's name, its tooltip, the data table and CSV. A category takes the axis label its points give it: two different
    /// ones are refused. With axis labels, or with <see cref="ChartSpec.CellWidth"/>, a column chart's labels are thinned by their measured
    /// widths, two-line labels kept first and one-line labels written where they stand clear. Column charts only, and not beside
    /// <see cref="SubLabel"/>s, which take the same line.
    /// </summary>
    public string? AxisLabel { get; init; }
    /// <summary>
    /// This point's mark's whole spoken name and tooltip, in place of the one Lumen builds (0.47.0): its <c>aria-label</c>, its native
    /// tooltip in static SVG, the component's tooltip and keyboard announcement, and the status line when it is selected. It replaces
    /// everything Lumen would say, the series, label, value, note, change words, zone and "projected" included, so a host that sets it says
    /// every state the mark shows, colour included. The drawing, value labels, the data table, CSV, the grid table and the shared readout are
    /// unchanged. 1 to 200 characters on one line. A mark that stands for several points, such as a line thinned by averaging, keeps Lumen's
    /// name; histogram, box, violin and calendar charts, whose marks always summarise points, refuse it. Null, the default, keeps Lumen's.
    /// </summary>
    public string? Name { get; init; }
```

  - **`ChartSeries`, last property, after `AverageOf`:**

```csharp
    /// <summary>
    /// Draws a value of exactly 0 as a hairline (0.47.0): a bar 1 unit high on the baseline, the column's width, in the style's
    /// <see cref="ChartStyle.Muted"/> colour, where an empty column would draw nothing, so a day with none reads as zero rather than as
    /// missing. With <see cref="ValueLabels"/> its <c>0</c> is written in the muted colour where that clears 4.5:1 against the background,
    /// and in the text colour otherwise. The mark stays focusable and named. Series drawn as columns only; false by default, and left out of
    /// the gradient-ID hash while false.
    /// </summary>
    public bool MarkZero { get; init; }
```

  - **`ChartSpec.CellWidth` doc:** replace it with:

```csharp
    /// <summary>On a heatmap, the width of every column in pixels, at least 24: the drawing grows to the name column, at least 130, plus 35,
    /// plus the columns times this width, and never narrower than 320, the room left over at its right, instead of squeezing into
    /// <see cref="Width"/>. On a column chart, from 0.47.0, the width of every category, at least 8: the plot is exactly the categories times
    /// this width, the drawing its margins and the plot, never narrower than 320, and up to 4,000 categories (100 without it). Either way the
    /// static SVG is written at that width, so a host's <c>overflow-x:auto</c> box scrolls it, and in <c>&lt;LumenChart&gt;</c> it scrolls
    /// sideways when wider than its box. Heatmaps and column charts only, not sparklines; null by default.</summary>
```

- [ ] **Step 5: Validation** (`ChartValidation.cs`).
  - **Lines 111-118:** replace the combined `CellText`/`CellWidth`/`ColumnLabelsOnTop` check and the two `CellWidth` checks with:

```csharp
        if (spec.Kind != ChartKind.Heatmap && (spec.CellText || spec.ColumnLabelsOnTop))
            throw new ArgumentException("Cell text writes in a heatmap's cells and ColumnLabelsOnTop moves its column labels above its grid, so they apply to heatmap charts only; the other kinds draw other marks.");
        if (spec.CellWidth is not null && spec.Kind is not (ChartKind.Heatmap or ChartKind.Column))
            throw new ArgumentException("CellWidth sets the width of a heatmap's columns or a column chart's categories, so it applies to heatmap and column charts only; the other kinds lay their marks out across Width.");
        if (spec.CellWidth is not null && spec.Sparkline)
            throw new ArgumentException("A sparkline is drawn at its own small Width, so it takes no CellWidth.");
        var columnPitch = spec.Kind == ChartKind.Column;
        if (spec.CellWidth is { } cellWidth && !double.IsFinite(cellWidth))
            throw new ArgumentException(columnPitch ? "A column chart's cell width is a finite number of pixels, at least 8, so a bar and its focus ring fit."
                : "A heatmap's cell width is a finite number of pixels, at least 24, so its text and focus ring fit.");
        if (spec.CellWidth < (columnPitch ? 8 : 24))
            throw new ArgumentException(columnPitch ? "A column chart's category is at least 8 pixels wide, so a bar and its focus ring fit."
                : "A heatmap's cell is at least 24 pixels wide, so its text and focus ring fit.");
```

    Keep the 0.46.2 `CellNotes`/`NotRatedKeepsValue` check where it is, after these.
  - **The constant**, beside `MaxSubLabel`: `internal const int MaxFixedCategories = 4000;` with a `/// <summary>The most categories a column chart with a cell width takes (0.47.0).</summary>`.
  - **The category block (~343-349):** replace the 100-category check and the 4,096 check with the code below. Keep the sub-label check after it.

```csharp
            var fixedPitch = spec.Kind == ChartKind.Column && spec.CellWidth is not null;
            if (fixedPitch && categories > MaxFixedCategories)
                throw new ArgumentException($"A column chart with CellWidth takes at most 4,000 categories, and this one has {categories.ToString("N0", CultureInfo.InvariantCulture)}; aggregate first.");
            if (!fixedPitch && categories > 100)
                throw new ArgumentException("Category charts support at most 100 categories, or 4,000 on a column chart with CellWidth; aggregate first.");
            if (spec.Kind == ChartKind.Heatmap && spec.CellWidth is { } columnWidth && ChartSvg.HeatmapWidth(spec, categories, columnWidth) > ChartSvg.MaxWidth)
                throw new ArgumentException(FormattableString.Invariant($"At {columnWidth} pixels a column, {categories} {(categories == 1 ? "column makes" : "columns make")} a drawing {ChartSvg.HeatmapWidth(spec, categories, columnWidth)} pixels wide, past the {ChartSvg.MaxWidth} a chart may be; narrow the cells or show fewer columns."));
            var columnPoints = spec.Kind == ChartKind.Column ? spec.Series.SelectMany(s => s.Points).ToArray() : [];
            if (columnPoints.Where(p => p.AxisLabel is not null).GroupBy(p => p.X).Any(category => category.Select(p => p.AxisLabel).Distinct().Count() > 1))
                throw new ArgumentException("A category's axis label is written once under its column, so the points of several series in one category may repeat it or leave it null, but not give different ones.");
            if (columnPoints.Any(p => p.AxisLabel is not null) && columnPoints.Any(p => p.SubLabel is not null))
                throw new ArgumentException("A column chart writes either axis labels or sub-labels under its columns, not both.");
```

    Add `using System.Globalization;` if the file lacks it.
  - **The point loop (~257-259)**, after `if (p.SubLabel is not null) SubLabel(spec, p.SubLabel);`:

```csharp
                if (p.AxisLabel is not null) AxisLabel(spec, p.AxisLabel);
                if (p.Name is not null) PointName(mark, p.Name);
```

  - **The helpers**, after `SubLabel`:

```csharp
    /// <summary>The most characters a line of an axis label takes (0.47.0).</summary>
    private const int MaxAxisLine = 12;

    /// <summary>An axis label is drawn under a column in place of its category's name: one or two short lines (0.47.0).</summary>
    private static void AxisLabel(ChartSpec spec, string label)
    {
        Text(label);
        if (spec.Kind != ChartKind.Column)
            throw new ArgumentException("An axis label is written under a column in place of its category's name, so it applies to column charts only; the other kinds name their categories or ticks themselves.");
        if (spec.Sparkline)
            throw new ArgumentException("A sparkline draws its data alone, with no words, so its points take no axis labels; write them in the words beside it.");
        var lines = label.Split('\n');
        if (lines.Length > 2 || lines.Any(line => string.IsNullOrWhiteSpace(line) || line.Length > MaxAxisLine || line.Any(c => c is '\r' or '\t')))
            throw new ArgumentException($"An axis label is one or two lines under a column, split at \\n, each 1 to {MaxAxisLine} characters with no other line breaks or tabs, such as 6 or 1\\nOct.");
    }

    /// <summary>The most characters a point's own name takes (0.47.0).</summary>
    private const int MaxPointName = 200;

    /// <summary>A point's own name replaces its mark's, so it needs a mark of its own and one line of words (0.47.0).</summary>
    private static void PointName(ChartKind mark, string name)
    {
        if (mark is ChartKind.Histogram or ChartKind.Box or ChartKind.Violin or ChartKind.Calendar)
            throw new ArgumentException("A point's Name names its own mark, and histogram, box, violin and calendar charts draw marks that stand for several points, so they take none; their marks keep the names Lumen gives them.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxPointName || name.Any(char.IsControl))
            throw new ArgumentException($"A point's Name is its mark's whole spoken name and tooltip: 1 to {MaxPointName} characters on one line, not blank. Leave it null for the name Lumen gives.");
    }
```

  - **The series check**, after the `ValueLabels` check (~722-723):

```csharp
        if (series.MarkZero && mark != ChartKind.Column)
            throw new ArgumentException("MarkZero draws a zero column as a hairline, so it applies to series drawn as columns only.");
```

    If `mark` is a calendar's, it is `spec.Kind`; check the loop's variable name there. In the point loop it is `mark`.
- [ ] **Step 6: The hash** (`ChartSvg.cs` `Unfitted`). In the `ChartSeries` branch, after the `AverageOf` rule, add:

```csharp
                else if (property.Name == nameof(ChartSeries.MarkZero)) property.ShouldSerialize = (_, zero) => zero is true;
```

  Make it the `else if` of that loop's `if`. At the end of the doc comment, add: "A series' zero hairline, from 0.47.0, is false by default and left out unless set; a point's axis label and name are null unless set."
- [ ] **Step 7: Build and test.**
  - Run `dotnet build Lumen.Charts.slnx -c Release`. Expect 0 warnings.
  - Run `dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build`. Expect the four new tests to pass and 0 failed overall (724 at the start, so 728).
  - Run the baseline in both finishes from `tests/Lumen.Charts.Baseline`:
    - `dotnet run -c Release`, then `dotnet run -c Release -- classic`;
    - `diff <(tr -d '\r' < reference/refined.txt) <(tr -d '\r' < baseline.txt)`, and the same for classic.

    Expect no difference: this task draws nothing new. Delete the run files.
- [ ] **Step 8: Commit.**

```bash
git add src/Lumen.Charts/Models.cs src/Lumen.Charts/ChartValidation.cs src/Lumen.Charts/ChartSvg.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Accept CellWidth on column charts, axis labels, MarkZero and a point's own Name, validated and out of the hash at their defaults"
```

---

### Task 2: Fixed pitch and the static drawn width

**Files:**
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - `DrawnWidth` (~3128);
  - a new `ColumnMargins` helper beside it;
  - `Framed` (~564-588);
  - `SvgWriter` (add `Drawn`);
  - `Render`'s writer initializer (~250);
  - `Begin`'s `shown` (~402);
  - `Above` (~798-802) and the category column branch's value-label call (~1078).
- Test: `tests/Lumen.Charts.Tests/Program.cs` (the `// 0.47.0` block, plus every existing pin of `;width:100%;` on a `CellWidth` drawing).
- Baseline: `tests/Lumen.Charts.Baseline/Program.cs` (a `// 0.47.0` block after the 0.46.2 rows) and both reference files.

**Interfaces:**
- Consumes: `ChartValidation.MaxFixedCategories` (Task 1).
- Produces:
  - `ChartSvg.DrawnWidth(spec)` gives a column chart its drawn width;
  - `SvgWriter.Drawn`;
  - the `Days` test helper's drawings scroll in `<LumenChart>`, which Task 6 relies on.

- [ ] **Step 1: Write the failing tests** (append to the `// 0.47.0` block).

```csharp
Test("Daily counts: a column chart with CellWidth draws its plot at exactly that pitch, its margins beside it, and keeps the 320 floor's room at its right",()=>{
    // 90 days at 30: left 76, plot 2700, right 30 = 2806.
    var wide=Days(90);
    Check(ChartSvg.DrawnWidth(wide)==76+90*30+30,"drawn "+ChartSvg.DrawnWidth(wide));
    var svg=ChartSvg.Render(wide);
    Check(svg.Contains("viewBox='0 0 2806 "),Regex.Match(svg,"viewBox='[^']*'").Value);
    // Each bar is 72% of the pitch: 21.6, the first one 76 + 30 x .14 = 80.2 in.
    Check(svg.Contains("x='80.2'")&&svg.Contains("width='21.6'"),"bar geometry");
    // Three days at 30 make 196, under the floor: the drawing is 320 and the plot still ends at 76 + 90 = 166.
    var three=Days(3);
    Check(ChartSvg.DrawnWidth(three)==320,"floor "+ChartSvg.DrawnWidth(three));
    Check(ChartSvg.Render(three).Contains("x='140.2'"),"the third bar at 76 + 2 x 30 + 4.2");
    // If the style rounds bars (BarRadius), Bar writes a path rather than a rect: check the path's start x instead, and say so.
    // Without CellWidth nothing moves.
    Check(ChartSvg.DrawnWidth(Days(10,null))==900,"plain");
});
Test("Daily counts: a CellWidth drawing writes its drawn width into its root style, so a host's box scrolls it; every other drawing still fills its box",()=>{
    Check(ChartSvg.Render(Days(90)).Contains(";width:2806px;height:auto;"),"column");
    Check(ChartSvg.Render(HeatGrid(s=>s with{CellWidth=90})).Contains($";width:{ChartSvg.DrawnWidth(HeatGrid(s=>s with{CellWidth=90}))}px;height:auto;"),"heatmap");
    Check(ChartSvg.Render(Days(10,null)).Contains(";width:100%;height:auto;"),"plain column");
    Check(ChartSvg.Render(Spec()).Contains(";width:100%;height:auto;"),"line");
});
Test("Daily counts: at a fixed pitch a value label is kept while it fits the category less 2, not just the bar",()=>{
    // At 30 a bar is 21.6 wide and "1234" is 27.28: kept at the pitch, dropped without it.
    var big=Days(3) with{Series=[new("Sign-ups",[new ChartPoint(0,1234,"D0"),new ChartPoint(1,5,"D1"),new ChartPoint(2,9,"D2")]){ValueLabels=true}]};
    Check(ChartSvg.Render(big).Contains(">1234<"),"kept at 30");
    Check(!ChartSvg.Render(big with{CellWidth=24}).Contains(">1234<"),"dropped at 24: 27.28 > 22");
    Check(!ChartSvg.Render(big with{CellWidth=null,Width=320}).Contains(">1234<"),"dropped across a bar without a pitch");
});
Test("Daily counts: the component shows a column chart with CellWidth at its drawn width",()=>{
    var html=ChartMarkup(Days(90),new(){{"FitWidth",true}});
    Check(html.Contains("lumen-fixed")&&html.Contains("--lumen-drawn:2806px"),"fixed");
});
```

  `ChartMarkup` is defined after the block, near the file's end. That's fine, because it is a local function in top-level code.

- [ ] **Step 2: Run and see it fail.** Build, then run the tests. Expected: `drawn 900` and similar failures.
- [ ] **Step 3: The margins and the drawn width.** Add beside `DrawnWidth`:

```csharp
    /// <summary>The margins a column chart's frame keeps left and right of its plot, as <see cref="Framed"/> sets them: 76 on the left, or 30
    /// with the Y axis on the right or no labels up the left; 30 on the right, or 76 beside a right-hand axis, or as wide as its end labels
    /// need (0.47.0).</summary>
    private static (double Left, double Right) ColumnMargins(ChartSpec s)
    {
        var flipped = s.YAxisSide == AxisSide.Right;
        var secondary = s.Series.Any(series => series.Secondary);
        var left = flipped || Unlabelled(s) ? 30d : 76d;
        return (left, secondary || flipped ? 76d : Math.Max(30d, Ending(s, left)));
    }
```

  Then replace `DrawnWidth` with:

```csharp
    /// <summary>The width a chart is drawn at: a heatmap or, from 0.47.0, a column chart with a <see cref="ChartSpec.CellWidth"/> as wide as
    /// its columns and margins make it, rounded up to a whole unit and never narrower than <see cref="MinWidth"/>, the room left over standing
    /// at its right, whatever its Width; any other chart its Width.</summary>
    internal static int DrawnWidth(ChartSpec spec)
    {
        if (spec.CellWidth is not { } cell || spec.Kind is not (ChartKind.Heatmap or ChartKind.Column)) return spec.Width;
        var columns = spec.Series.SelectMany(s => s.Points).Select(p => p.X).Distinct().Count();
        if (spec.Kind == ChartKind.Heatmap) return Math.Max(MinWidth, (int)Math.Ceiling(HeatmapWidth(spec, columns, cell)));
        var (left, right) = ColumnMargins(spec);
        return Math.Max(MinWidth, (int)Math.Ceiling(left + columns * cell + right));
    }
```

- [ ] **Step 4: The plot at its pitch.** In `Framed`, keep the existing `left`/`right` line. Right after `var cats = …`, add:

```csharp
        // A column chart with a cell width lays its categories out at exactly that pitch from its left margin; the drawing is at least that
        // wide, and where the 320 floor makes it wider, the room left over stands at its right (0.47.0).
        if (s.Kind == ChartKind.Column && s.CellWidth is { } pitch) right = left + cats.Length * pitch;
```

  `right` is declared with `var` in the line above, and is assigned again here.
- [ ] **Step 5: The static width.**
  - `SvgWriter`: add `/// <summary>The drawing has a cell width, so its root is written at its drawn width rather than filling its box (0.47.0).</summary> public bool Drawn { get; init; }`, next to `Bare`.
  - `Render`'s initializer (~250): add `Drawn = spec.CellWidth is not null`. Here `spec` is the `AsDrawn` spec.
  - `Begin` (~402):

```csharp
        // A chart fills the width of its box, except one drawn at its own width: a sparkline, as a word is, never wider than its box; and a
        // drawing with a cell width, at exactly its drawn width, which a host's overflow box scrolls (0.47.0).
        var shown = w.Bare ? $"width:{width}px;max-width:100%" : w.Drawn ? $"width:{width}px" : "width:100%";
```

- [ ] **Step 6: Value labels at the pitch.**
  - Give `Above` an optional room:

```csharp
            // A column's label fits across the column, so it never runs into its neighbours'; at a fixed pitch with one series of columns it
            // may take the category's width less 2 (0.47.0).
            void Above(double x, double width, double far, bool up, string text, string? note, double? room = null)
            {
                if (Wide(text + note) > (room ?? width) || x + width / 2 < left || x + width / 2 > right || far < top - .5 || far > bottom + .5) return;
                Name(x + width / 2, up ? far - 5 : far + 12, text, "middle", note);
            }
```

  - The category branch's plain-column call (~1078):

```csharp
                            else Above(rx, rw, At(y), y >= 0, scale.Format(y), p.ValueNote, s.CellWidth is { } pitch && columns.Length == 1 ? pitch - 2 : null);
```

- [ ] **Step 7: Update the pins** that expected `;width:100%;` on a `CellWidth` drawing, if any. Search the tests for `width:100%` near `CellWidth`; the 0.46.x frozen-band and component tests are the likely ones. Change each to the drawn width, with a comment.
- [ ] **Step 8: The reference rows.** In `tests/Lumen.Charts.Baseline/Program.cs`, after the 0.46.2 block:

```csharp
// 0.47.0: registrations per day. Ninety invented days of sign-ups at a fixed pitch of 30, with plain labels.
var signups = Enumerable.Range(0, 90).Select(i => new ChartPoint(i, (i * 7 + 3) % 11 == 0 ? 0 : (i * 5 + 2) % 14, $"D{i + 1}")).ToArray();
ChartSpec SignUps(ChartTheme theme) => new() { Kind = ChartKind.Column, Theme = theme, Title = "Sign-ups per day", Description = "Invented sign-ups", Width = 320, Height = 320, CellWidth = 30, Series = [new("Sign-ups", signups) { ValueLabels = true }] };
lines.Add($"columns/cell-width {Hash(Render(SignUps(ChartTheme.Light)))}");
```

- [ ] **Step 9: Run the baseline in both finishes and compare.**
  - Expected: 403 rows per finish, the 402 plus `columns/cell-width`.
  - Exactly six rows change in both finishes, their root style only: `heatmap-table/cell-width`, `heatmap-table/reasons`, `heatmap-cells/notes`, `heatmap-cells/notes-fixed`, `heatmap-cells/keeps-value` and `heatmap-cells/both-dark`.
  - Confirm "root style only" for one of them: render it with `svg-out` from this build and from the reference, and diff the two SVGs.
  - Any other moved row is a defect: stop and report it.
  - Then copy the run files over `reference/refined.txt` and `reference/classic.txt`, and delete the run files.
- [ ] **Step 10: Build and test.** 0 warnings and 0 failed. Unit tests that pin the gallery's Category heatmap (static `Svg(...)`) may need the new width. Update them only where they pin `width:100%`, and say so.
- [ ] **Step 11: Commit.**

```bash
git add src/Lumen.Charts/ChartSvg.cs tests/Lumen.Charts.Tests/Program.cs tests/Lumen.Charts.Baseline/Program.cs tests/Lumen.Charts.Baseline/reference/refined.txt tests/Lumen.Charts.Baseline/reference/classic.txt
git commit -m "Lay column charts out at a fixed pitch with CellWidth, and write any CellWidth drawing at its drawn width"
```

---

### Task 3: Axis labels and the zero hairline

**Files:**
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - `Subbed` (~596);
  - the category X-label block in `Cartesian` (~686-715) and the axis title line (~746);
  - `Name` (~790, add an optional fill);
  - the category and continuous column branches (~1071-1088).
- Test: the `// 0.47.0` block.
- Baseline: two rows and both references.

**Interfaces:**
- Consumes: `ChartPoint.AxisLabel`, `ChartSeries.MarkZero` (Task 1), and the fixed pitch (Task 2).

- [ ] **Step 1: Write the failing tests.**

```csharp
// A month of invented days: the day number under each, the month under the first and each 1st.
ChartSpec Month(int days,double? cell=30)=>new(){Title="Sign-ups",Kind=ChartKind.Column,CellWidth=cell,Series=[new("Sign-ups",Enumerable.Range(0,days).Select(i=>{var day=new DateOnly(2026,9,28).AddDays(i);return new ChartPoint(i,i%3,day.ToString("d MMM",CultureInfo.InvariantCulture)){AxisLabel=i==0||day.Day==1?$"{day.Day}\n{day:MMM}":$"{day.Day}"};}).ToArray()){ValueLabels=true,MarkZero=true}]};
Test("Daily counts: axis labels write their first line where the name goes and their second 14 under it, the room under the plot growing 14",()=>{
    var svg=ChartSvg.Render(Month(10));
    // The plot's bottom: Height 420 less a floor of 90 = 330; first lines at 351, second at 365.
    Check(Regex.IsMatch(svg,"<text x='[^']*' y='351' text-anchor='middle' class='lumen-muted'>28<"),"first line");
    Check(Regex.IsMatch(svg,"<text x='[^']*' y='365' text-anchor='middle' class='lumen-muted' font-size='11'>Sep<"),"second line");
    Check(Regex.IsMatch(svg,"y='365'[^>]*>Oct<"),"the 1st's month");
    // The label is drawn only: the mark is named by its Label.
    Check(HeatNames(Month(10)).Any(n=>n.StartsWith("Sign-ups: 28 Sep, ")),string.Join(" | ",HeatNames(Month(10)).Take(2)));
    Check(!ChartExport.Csv(Month(10)).Contains("\n1\nOct"),"CSV keeps the Label");
});
Test("Daily counts: at a fixed pitch two-line labels are kept first and one-line labels only where they stand clear of them",()=>{
    // At 8 a pitch, "28"/"Sep" and "1"/"Oct" are kept and day numbers between them only where clear.
    var svg=ChartSvg.Render(Month(40,8));
    var firsts=Regex.Matches(svg,"y='351' text-anchor='middle' class='lumen-muted'>([^<]*)<").Select(m=>m.Groups[1].Value).ToList();
    Check(firsts[0]=="28"&&firsts.Contains("1"),string.Join(",",firsts));
    var kept=Regex.Matches(svg,"<text x='([^']*)' y='351' text-anchor='middle' class='lumen-muted'>([^<]*)<").Select(m=>(X:double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture),T:m.Groups[2].Value)).ToList();
    // Kept neighbours stand at least half their first lines' widths and 8 apart (their second lines only widen the rule).
    for(var i=1;i<kept.Count;i++) Check(kept[i].X-kept[i-1].X>=(ChartSvg.Broad(kept[i-1].T)+ChartSvg.Broad(kept[i].T))/2+8-1e-9,$"labels {kept[i-1]} and {kept[i]} too close");
    // At 30 every day number stands clear.
    Check(Regex.Matches(ChartSvg.Render(Month(40)),"y='351' text-anchor='middle' class='lumen-muted'>").Count==40,"all 40 at 30");
});
Test("Daily counts: MarkZero draws a zero as a 1-unit muted bar on the baseline and writes its 0 muted, and changes no other bar",()=>{
    var svg=ChartSvg.Render(Month(4));
    // Day 0 (28 Sep) is 0: a hairline from 329 to 330, the bar's width, in Light's muted colour.
    Check(Regex.IsMatch(svg,$"<rect x='80.2' y='329' width='21.6' height='1' fill='{ChartStyle.Light.Muted}'/>"),"hairline");
    Check(Regex.IsMatch(svg,$"<text[^>]*fill='{ChartStyle.Light.Muted}'[^>]*>0<"),"the muted 0");
    Check(Lumen.Charts.Contrast.Ratio(ChartStyle.Light.Muted,ChartStyle.Light.Background)>=4.5,"clears 4.5:1");
    Check(HeatNames(Month(4)).Contains("Sign-ups: 28 Sep, 0"),"named");
    // A faint muted colour falls back to the text colour.
    var faint=Month(4) with{Style=ChartStyle.Light with{Muted="#C8C8C8"}};
    Check(!Regex.IsMatch(ChartSvg.Render(faint),"<text[^>]*fill='#C8C8C8'[^>]*>0<"),"faint muted not used for the 0");
    // Without MarkZero a zero is a zero-height bar, as before, and its 0 in the text colour.
    var plain=Month(4) with{Series=[Month(4).Series[0] with{MarkZero=false}]};
    Check(!ChartSvg.Render(plain).Contains("height='1' fill="),"no hairline");
    // A negative column is drawn as before.
    var negative=Days(3) with{Series=[new("S",[new ChartPoint(0,-2,"A"),new ChartPoint(1,0,"B"),new ChartPoint(2,3,"C")]){MarkZero=true,ValueLabels=true}]};
    Check(Regex.Matches(ChartSvg.Render(negative),"height='1' fill=").Count==1,"only the zero");
});
```

  Check the plot's bottom of 330 against the code before trusting it: `Height` 420 less `Floor` 90 when a second line exists. Adjust the numbers only from the code, and say how in your report.

- [ ] **Step 2: Run and see it fail.**
- [ ] **Step 3: Room under the plot.** Extend `Subbed`:

```csharp
    /// <summary>Whether a column or stacked column chart writes a second line under its categories' names, a sub-label or, from 0.47.0, an
    /// axis label's second line, which takes a line more under the plot.</summary>
    private static bool Subbed(ChartSpec s) => s.Kind is ChartKind.Column or ChartKind.StackedColumn
        && s.Series.Any(series => series.Points.Any(p => p.SubLabel is not null || p.AxisLabel?.Contains('\n') == true));
```

  At the axis title line (~746), replace `subs.Count > 0` with `Subbed(s)`.
- [ ] **Step 4: The labels.** In the category branch (~686-715), the non-horizontal case: before the existing `measured`/`step` thinning, add the code below, and keep the existing code as the `else` for every other chart.

```csharp
                    // With a cell width, or any axis label, a column chart's labels are its axis labels or names, thinned by their measured
                    // widths: two-line labels first, each kept unless it meets the last two-line label kept, then one-line labels where they
                    // stand clear of those on either side (0.47.0).
                    string? AxisWords(double x) => points.FirstOrDefault(p => p.X == x && p.AxisLabel is not null)?.AxisLabel;
                    if (!horizontal && s.Kind == ChartKind.Column && (s.CellWidth is not null || points.Any(p => p.AxisLabel is not null)))
                    {
                        string[] Lines(int i) => AxisWords(cats[i]) is { } words ? words.Split('\n')
                            : Sub(cats[i]) is { } under ? [Short(points.First(p => p.X == cats[i]).Label ?? LinearScale.Label(cats[i]), 12), Short(under, 12)]
                            : [Short(points.First(p => p.X == cats[i]).Label ?? LinearScale.Label(cats[i]), 12)];
                        (double At, double Width) Box(int i) { var l = Lines(i); return (X(cats[i]), Math.Max(Broad(l[0]), l.Length > 1 ? Wide(l[1]) : 0)); }
                        var kept = new List<int>();
                        foreach (var i in Enumerable.Range(0, cats.Length).Where(i => Lines(i).Length > 1))
                            if (kept.Count == 0 || Clear([Box(kept[^1]), Box(i)])) kept.Add(i);
                        var twoLine = kept.ToList();
                        foreach (var i in Enumerable.Range(0, cats.Length).Where(i => Lines(i).Length == 1))
                        {
                            var before = kept.Where(k => k < i).DefaultIfEmpty(-1).Max();
                            var after = kept.Where(k => k > i).DefaultIfEmpty(-1).Min();
                            if ((before < 0 || Clear([Box(before), Box(i)])) && (after < 0 || Clear([Box(i), Box(after)]))) { kept.Add(i); kept.Sort(); }
                        }
                        foreach (var i in kept)
                        {
                            var l = Lines(i);
                            w.Text(X(cats[i]), bottom + 21, l[0], "text-anchor='middle' class='lumen-muted'");
                            if (l.Length > 1) w.Text(X(cats[i]), bottom + 21 + SubLine, l[1], "text-anchor='middle' class='lumen-muted' font-size='11'");
                        }
                    }
                    else
                    {
                        // … the existing measured/step thinning and drawing, unchanged …
                    }
```

  `after` is computed with `DefaultIfEmpty(-1).Min()`, which returns −1 when there is none. Guard it with `after < 0` as shown. Note that `Min` over `-1` would be wrong if real indexes exist: compute it as `kept.Where(k => k > i).Select(k => (int?)k).FirstOrDefault()` and test for null instead. Write it whichever way is correct, and test it.

  `twoLine` is unused: delete it if the compiler warns. `Clear` takes `IEnumerable<(double At, double Width)>`.
- [ ] **Step 5: The hairline and the muted 0.**
  - Give `Name` an optional fill:

```csharp
            void Name(double x, double y, string text, string anchor, string? note, string? fill = null)
            {
                var width = Wide(text + note);
                var x1 = anchor == "middle" ? x - width / 2 : anchor == "end" ? x - width : x;
                written.Add((x1, x1 + width, y - 9, y + 3));
                named.Append($"<text x='{N(x)}' y='{N(y)}' text-anchor='{anchor}' font-size='11'{(fill is null ? "" : $" fill='{fill}'")} aria-hidden='true'>{Noted(text, note)}</text>");
            }
```

    Thread `fill` through `Above(…, double? room = null, string? fill = null)` to `Name`.
  - In the category branch, replace the `Datum(…, Bar(w, rx, ry, rw, rh, end, Ink(p), series.Fill, outermost))` call (~1071) with:

```csharp
                        // A zero drawn as a hairline: a bar 1 unit high on the baseline, in the muted colour, focusable and named as any (0.47.0).
                        var hairline = series.MarkZero && !horizontal && y == 0;
                        var shape = hairline ? $"<rect x='{N(rx)}' y='{N(At(0) - 1)}' width='{N(rw)}' height='1' fill='{w.Style.Muted}'/>" : Bar(w, rx, ry, rw, rh, end, Ink(p), series.Fill, outermost);
                        Datum(w, si, pi, PointLabel(series,p,xs,scale,sub: Sub(p.X)) + Of(series, p) + over, shape);
```

  - The plain-column value label (`else Above(…)`) passes `fill: hairline && Contrast.Ratio(w.Style.Muted, w.Style.Background) >= 4.5 ? w.Style.Muted : null`.
  - Do the same in the continuous-axis column branch (~1086-1087): there the bar is `x`, `width` and `At(0)`.
- [ ] **Step 6: Reference rows**, after `columns/cell-width`:

```csharp
// Axis labels without a pitch (a fortnight across a month end) and a zero hairline at the pitch.
ChartPoint[] Fortnight(int days) => Enumerable.Range(0, days).Select(i => { var day = new DateOnly(2026, 9, 24).AddDays(i); return new ChartPoint(i, (i * 3 + 1) % 5, day.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture)) { AxisLabel = i == 0 || day.Day == 1 ? $"{day.Day}\n{day:MMM}" : $"{day.Day}" }; }).ToArray();
lines.Add($"columns/axis-labels {Hash(Render(new ChartSpec { Kind = ChartKind.Column, Title = "Sign-ups per day", Description = "Invented sign-ups", Width = 640, Height = 320, Series = [new("Sign-ups", Fortnight(14)) { ValueLabels = true }] }))}");
lines.Add($"columns/mark-zero {Hash(Render(SignUps(ChartTheme.Light) with { Series = [new("Sign-ups", signups) { ValueLabels = true, MarkZero = true }] }))}");
```

- [ ] **Step 7: Baseline in both finishes:** 405 rows, the 403 plus these two, and no other row moves. Look at both new drawings (`svg-out` to `%TEMP%\lumen0470\svg`): labels clear of each other and the hairline visible. Regenerate the references and delete the run files.
- [ ] **Step 8: Build and test:** 0 warnings, 0 failed.
- [ ] **Step 9: Commit.**

```bash
git add src/Lumen.Charts/ChartSvg.cs tests/Lumen.Charts.Tests/Program.cs tests/Lumen.Charts.Baseline/Program.cs tests/Lumen.Charts.Baseline/reference/refined.txt tests/Lumen.Charts.Baseline/reference/classic.txt
git commit -m "Draw axis labels under columns, thinned by their measured widths, and a zero as a muted hairline"
```

---

### Task 4: A point's own name, and opening at the end

**Files:**
- Modify `src/Lumen.Charts/ChartSvg.cs`:
  - `Datum` (~507-510);
  - the line/area `Datum` call (~967), so an averaged mark keeps Lumen's name.
- Modify `src/Lumen.Charts.Blazor/LumenChart.razor`:
  - a `ScrollToEnd` parameter;
  - `SelectPoint` (~252-286);
  - the `drawn` call in `OnAfterRenderAsync` (~232).
- Modify `src/Lumen.Charts.Blazor/wwwroot/lumen.js`: `drawn` (~338) and `state.drawn` (~309).
- Test: the `// 0.47.0` block. Baseline: one row and both references.

**Interfaces:**
- Consumes: `ChartPoint.Name` (Task 1).
- Produces: `<LumenChart ScrollToEnd>`. Task 6's gallery card and browser checks use it.

- [ ] **Step 1: Write the failing tests.**

```csharp
Test("Daily counts: a point's Name is its mark's whole name and tooltip on any kind that draws a mark for it; the table and CSV keep their words",()=>{
    var line=Spec(ChartKind.Line) with{Series=[new("S",[new ChartPoint(0,1,"A"){Name="Tue 6 Oct 2026: 1 registration, 1 so far"},new ChartPoint(1,2,"B")])]};
    var svg=ChartSvg.Render(line);
    Check(svg.Contains("aria-label='Tue 6 Oct 2026: 1 registration, 1 so far'")&&svg.Contains("<title>Tue 6 Oct 2026: 1 registration, 1 so far</title>"),"line mark");
    Check(svg.Contains("aria-label='S: B, 2"),"the other mark keeps Lumen's name");
    Check(!ChartExport.Csv(line).Contains("registration"),"CSV unchanged");
    var column=Days(2) with{Series=[new("S",[new ChartPoint(0,0,"D0"){Name="Zero day"},new ChartPoint(1,3,"D1")]){MarkZero=true}]};
    Check(ChartSvg.Render(column).Contains("aria-label='Zero day'"),"column");
    var cell=HeatGrid(s=>s with{Series=[s.Series[0] with{Points=[s.Series[0].Points[0] with{Name="Sprint 2025 cell"},s.Series[0].Points[1]]},s.Series[1]]});
    Check(ChartSvg.Render(cell).Contains("aria-label='Sprint 2025 cell'"),"heatmap cell");
    // The component's status line reads it when the mark is selected.
    Check(Operate(column,c=>c.SelectPoint(0,0)).Contains("Zero day"),"status line");
});
Test("Daily counts: a mark that averages several points keeps Lumen's name, whatever its points' Names",()=>{
    var many=Spec(ChartKind.Line) with{MaxRenderedPoints=10,Sampling=SamplingMethod.Average,Series=[new("S",Enumerable.Range(0,100).Select(i=>new ChartPoint(i,i%7){Name=$"own {i}"}).ToArray())]};
    var names=Regex.Matches(ChartSvg.Render(many),"aria-label='([^']*)'").Select(m=>m.Groups[1].Value).Where(n=>n.StartsWith("S: ")||n.StartsWith("own ")).ToList();
    Check(names.Count>0&&names.All(n=>n.StartsWith("S: ")),string.Join(" | ",names.Take(3)));
});
Test("Daily counts: ScrollToEnd reaches the script with each drawing, and the prerendered markup is the same with it",()=>{
    var js=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Lumen.Charts.Blazor/wwwroot/lumen.js"));
    Check(js.Contains("export function drawn(root, readout, plot, end)")&&js.Contains("viewport.scrollLeft = viewport.scrollWidth"),"script");
    Check(ChartMarkup(Days(90),new(){{"FitWidth",true},{"ScrollToEnd",true}})==ChartMarkup(Days(90),new(){{"FitWidth",true}}),"prerender unchanged");
});
```

  `Operate(spec, act, fit)` is the existing helper at ~1902 that drives the component. Check its signature, and adapt the call shape (`c=>c.SelectPoint(0,0)` and how it returns the status) to it. Change nothing else.

- [ ] **Step 2: Run and see it fail.**
- [ ] **Step 3: `Datum`.**

```csharp
    /// <summary>A focusable, labelled data mark. <paramref name="attributes"/> are presentation attributes on the group, which
    /// its shapes inherit and the focus rule overrides, so a hollow marker still shows the focus ring. A mark that stands for one point
    /// takes that point's own <see cref="ChartPoint.Name"/> in place of <paramref name="label"/> where it has one (0.47.0); one that stands
    /// for several, <paramref name="single"/> false, keeps Lumen's.</summary>
    private static void Datum(SvgWriter w, int series, int index, string label, string shape, string attributes = "", bool single = true)
    {
        if (single && w.Spec is { } drawn && series >= 0 && series < drawn.Series.Count && index >= 0 && index < drawn.Series[series].Points.Count
            && drawn.Series[series].Points[index].Name is { } own) label = own;
        w.Add($"<g class='lumen-datum' tabindex='0' role='button' data-series='{series}' data-point='{index}' aria-label='{SvgWriter.E(label)}'{attributes}>{(w.Titles ? $"<title>{SvgWriter.E(label)}</title>" : "")}{shape}</g>");
    }
```

  At the line/area call (~967), pass `single: d.Count <= 1`. `w.Spec` is the spec as drawn: the component's spec with its hidden series removed, so the indexes agree.
- [ ] **Step 4: The status line.** In `SelectPoint`, after the averaged branch and before `status=Spec.Kind is …`, add the code below. Here `p` is the host's point, with the host's `index`, so its `Name` is the host's.

```csharp
        // A point that names itself reads as its mark does (0.47.0).
        if(p.Name is {} own){status=own;await PointSelected.InvokeAsync(new(index,point));await InvokeAsync(StateHasChanged);return;}
```

- [ ] **Step 5: `ScrollToEnd`.**
  - The parameter, after `ShowDataButton`:

```csharp
    /// <summary>Opens a chart that is wider than its box, such as one with a <see cref="ChartSpec.CellWidth"/>, scrolled to its right end,
    /// where its latest days are, and goes back there whenever a new drawing has a different width; otherwise the reader's scroll is kept.
    /// Needs an interactive render mode: prerendered and static markup stands at the left (0.47.0).</summary>
    [Parameter] public bool ScrollToEnd { get; set; }
```

  - In `OnAfterRenderAsync`: `await module.InvokeVoidAsync("drawn",root,Told,Brushed,ScrollToEnd);`.
  - `lumen.js`, the export:

```js
export function drawn(root, readout, plot, end) {
    handlers.get(root)?.state?.drawn(readout, plot, end);
}
```

  - In `attach`, change `state.drawn = (data, plot) => {` to `state.drawn = (data, plot, end) => {`. At its end, after the drawing is taken, add:

```js
        // Asked to open at its end, a drawing whose width is new scrolls its viewport to the right end, where the latest days stand (0.47.0).
        const viewport = root.querySelector(':scope > .lumen-viewport');
        const width = drawing()?.viewBox?.baseVal?.width ?? 0;
        if (end && viewport && width !== state.endWidth) {
            state.endWidth = width;
            requestAnimationFrame(() => { viewport.scrollLeft = viewport.scrollWidth; });
        }
        if (!end) state.endWidth = null;
```

    Add `endWidth: null` to the `state` object literal.
- [ ] **Step 6: Reference row**, after `columns/mark-zero`:

```csharp
lines.Add($"point-name/line {Hash(Render(new ChartSpec { Kind = ChartKind.Line, Title = "Named points", Description = "Invented values", Width = 640, Height = 320, Series = [new("Value", Enumerable.Range(0, 6).Select(i => new ChartPoint(i, i * 2 % 5, $"P{i}") { Name = i % 2 == 0 ? $"Point {i}: {i * 2 % 5} units" : null }).ToArray())] }))}");
```

- [ ] **Step 7: Baseline in both finishes:** 406 rows, no other row moves. Regenerate the references and delete the run files.
- [ ] **Step 8: Build and test:** 0 warnings, 0 failed.
- [ ] **Step 9: Commit.**

```bash
git add src/Lumen.Charts/ChartSvg.cs src/Lumen.Charts.Blazor/LumenChart.razor src/Lumen.Charts.Blazor/wwwroot/lumen.js tests/Lumen.Charts.Tests/Program.cs tests/Lumen.Charts.Baseline/Program.cs tests/Lumen.Charts.Baseline/reference/refined.txt tests/Lumen.Charts.Baseline/reference/classic.txt
git commit -m "Name a mark by its point's own Name, and open a scrolling chart at its end with ScrollToEnd"
```

---

### Task 5: The builder, `DailyCountsChart`

**Files:**
- Create: `src/Lumen.Charts/DailyCounts.cs`.
- Test: the `// 0.47.0` block.
- Baseline: two rows and both references.

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces:
  - `public sealed record DayCount(DateOnly Date, int Count)`;
  - `public sealed record DailyCountsOptions`;
  - `public static class DailyCountsChart { public const int MaxDays = 4000; public static ChartSpec? Build(IEnumerable<DayCount> counts, DailyCountsOptions? options = null); }`.

- [ ] **Step 1: Write the failing tests.**

```csharp
DayCount Count(int y,int m,int d,int n)=>new(new DateOnly(y,m,d),n);
Test("Daily counts: the builder fills missing days with 0, sums a day's counts, ignores counts outside the range, and returns null when there is nothing to draw",()=>{
    var spec=DailyCountsChart.Build([Count(2026,10,4,3),Count(2026,10,6,2),Count(2026,10,6,1),Count(2026,9,1,9)],new(){From=new DateOnly(2026,10,4)})!;
    var p=spec.Series[0].Points;
    Check(p.Count==3&&p.Select(x=>x.Y).SequenceEqual(new double?[]{3,0,3}),string.Join(",",p.Select(x=>x.Y)));
    Check(DailyCountsChart.Build([])==null,"no counts");
    Check(DailyCountsChart.Build([Count(2026,10,4,0),Count(2026,10,5,0)])==null,"all zero");
    // An unset end stretches to the set one: From after every date gives From..From, nothing in it, so null.
    Check(DailyCountsChart.Build([Count(2026,10,4,3)],new(){From=new DateOnly(2026,10,9)})==null,"From after the data");
    Check(DailyCountsChart.Build([Count(2026,10,4,3)],new(){To=new DateOnly(2026,10,1)})==null,"To before the data");
    Check(DailyCountsChart.Build([],new(){From=new DateOnly(2026,10,1),To=new DateOnly(2026,10,3)})==null,"a range with no counts");
});
Test("Daily counts: the builder labels, names and describes each day, singular for one",()=>{
    var spec=DailyCountsChart.Build([Count(2026,10,5,12),Count(2026,10,6,1),Count(2026,10,7,0)])!;
    var p=spec.Series[0].Points;
    Check(p[0].Label=="Mon 5 Oct 2026"&&p[0].AxisLabel=="5\nOct"&&p[1].AxisLabel=="6","labels: "+p[0].Label+" / "+p[0].AxisLabel);
    Check(p[0].Name=="Mon 5 Oct 2026: 12 registrations, 12 so far"&&p[1].Name=="Tue 6 Oct 2026: 1 registration, 13 so far"&&p[2].Name=="Wed 7 Oct 2026: 0 registrations, 13 so far",string.Join(" | ",p.Select(x=>x.Name)));
    Check(spec.Title=="Registrations per day"&&spec.Description=="13 registrations across 3 days","title and description: "+spec.Description);
    Check(spec is{Kind:ChartKind.Column,CellWidth:30,Width:320,Height:320}&&spec.Series[0] is{Name:"Registrations",ValueLabels:true,MarkZero:true},"the spec");
    var plain=DailyCountsChart.Build([Count(2026,10,5,1)],new(){Singular="order",Plural="orders",RunningTotal=false,Title="Orders",DateFormat="d MMM"})!;
    Check(plain.Series[0].Points[0].Name=="5 Oct: 1 order"&&plain.Title=="Orders"&&plain.Description=="1 order across 1 day",plain.Series[0].Points[0].Name+" / "+plain.Description);
    // Thousands are separated in names and descriptions.
    var many=DailyCountsChart.Build([Count(2026,10,5,1200),Count(2026,10,6,34)])!;
    Check(many.Series[0].Points[1].Name!.EndsWith("1,234 so far")&&many.Description=="1,234 registrations across 2 days",many.Description!);
});
Test("Daily counts: across a year end the first day and each 1 January carry the year",()=>{
    var p=DailyCountsChart.Build([Count(2026,12,30,1),Count(2027,1,2,2)])!.Series[0].Points;
    Check(p.Select(x=>x.AxisLabel).SequenceEqual(["30\nDec 2026","31","1\nJan 2027","2"]),string.Join(" | ",p.Select(x=>x.AxisLabel)));
    var later=DailyCountsChart.Build([Count(2027,1,30,1),Count(2027,2,2,2)])!.Series[0].Points;
    Check(later[0].AxisLabel=="30\nJan"&&later[2].AxisLabel=="1\nFeb","one year: no year on the 1sts");
});
Test("Daily counts: whole-number ticks with a step's headroom over the tallest day",()=>{
    foreach(var (largest,top,ticks) in new[]{(1,2d,"0,1,2"),(3,4d,"0,1,2,3,4"),(4,5d,"0,1,2,3,4,5"),(5,6d,"0,2,4,6"),(12,15d,"0,5,10,15"),(15,20d,"0,5,10,15,20"),(140,150d,"0,50,100,150")})
    {
        var spec=DailyCountsChart.Build([Count(2026,10,5,largest),Count(2026,10,6,0)])!;
        Check(spec.YMax==top&&string.Join(",",spec.YTickValues!.Select(t=>t.Value))==ticks,$"{largest}: {spec.YMax} {string.Join(",",spec.YTickValues!.Select(t=>t.Value))}");
        ChartValidation.Validate(spec);
    }
});
Test("Daily counts: the builder refuses what it cannot build, and draws 4,000 days but not 4,001",()=>{
    Reject(()=>DailyCountsChart.Build([Count(2026,10,5,-1)]));
    Reject(()=>DailyCountsChart.Build([Count(2026,10,5,1)],new(){Singular=" "}));
    Reject(()=>DailyCountsChart.Build([Count(2026,10,5,1)],new(){Plural=""}));
    Reject(()=>DailyCountsChart.Build([Count(2026,10,5,1)],new(){DateFormat="%"}));
    Reject(()=>DailyCountsChart.Build([Count(2026,10,5,1)],new(){From=new DateOnly(2026,10,9),To=new DateOnly(2026,10,1)}));
    Reject(()=>DailyCountsChart.Build([null!]));
    try{DailyCountsChart.Build(null!);Check(false,"null counts");}catch(ArgumentNullException){}
    var start=new DateOnly(2020,1,1);
    var four=DailyCountsChart.Build([new DayCount(start,1)],new(){From=start,To=start.AddDays(3999)})!;
    Check(four.Series[0].Points.Count==4000,"4,000 days");
    ChartValidation.Validate(four);
    try{DailyCountsChart.Build([new DayCount(start,1)],new(){From=start,To=start.AddDays(4000)});Check(false,"4,001 accepted");}
    catch(ArgumentException e){Check(e.Message.Contains("4,001"),e.Message);}
});
```

- [ ] **Step 2: Run and see it fail:** `'DailyCountsChart' does not exist`.
- [ ] **Step 3: Write `src/Lumen.Charts/DailyCounts.cs`.**

```csharp
using System.Globalization;

namespace Lumen.Charts;

/// <summary>How many things happened on one day, for <see cref="DailyCountsChart"/> (0.47.0).</summary>
/// <param name="Date">The day.</param>
/// <param name="Count">How many, 0 or more; several counts on one day are added up.</param>
public sealed record DayCount(DateOnly Date, int Count);

/// <summary>The words, range and look of a <see cref="DailyCountsChart"/> (0.47.0).</summary>
public sealed record DailyCountsOptions
{
    /// <summary>The word for one, said in a bar's name for a count of 1: <c>registration</c> by default.</summary>
    public string Singular { get; init; } = "registration";
    /// <summary>The word for any other count, and capitalised the series' name and the title's start: <c>registrations</c> by default.</summary>
    public string Plural { get; init; } = "registrations";
    /// <summary>The first day drawn; null, the earliest date given, or <see cref="To"/> where that is earlier.</summary>
    public DateOnly? From { get; init; }
    /// <summary>The last day drawn; null, the latest date given, or <see cref="From"/> where that is later.</summary>
    public DateOnly? To { get; init; }
    /// <summary>Each day's width in pixels, at least 8: 30 by default, a bar about 22 wide.</summary>
    public double CellWidth { get; init; } = 30;
    /// <summary>How a day is written in its bar's name, the data table and CSV, in the invariant culture: <c>ddd d MMM yyyy</c> by default.</summary>
    public string DateFormat { get; init; } = "ddd d MMM yyyy";
    /// <summary>Says how many so far after each day's count in its name, the default.</summary>
    public bool RunningTotal { get; init; } = true;
    /// <summary>The chart's title; null, the plural capitalised with "per day".</summary>
    public string? Title { get; init; }
    /// <summary>The bars' <c>#RRGGBB</c> colour; null, the style's first series colour.</summary>
    public string? Color { get; init; }
    /// <summary>The chart's style; null, the Light preset.</summary>
    public ChartStyle? Style { get; init; }
}

/// <summary>
/// Builds a chart of counts per day, such as sign-ups or orders, as an ordinary <see cref="ChartSpec"/> (0.47.0): one column a day at a fixed
/// pitch (<see cref="ChartSpec.CellWidth"/>), so a long span scrolls sideways rather than squeezing; missing days drawn as 0, each as a muted
/// hairline (<see cref="ChartSeries.MarkZero"/>); the count above every bar; the day number under each, with the month under the first and
/// each 1st, and the year where the span crosses one (<see cref="ChartPoint.AxisLabel"/>); and each bar named with its date, its count in
/// the singular or the plural, and the total so far (<see cref="ChartPoint.Name"/>). Show it with
/// <c>&lt;LumenChart FitWidth="true" ScrollToEnd="true" ShowLegend="false"&gt;</c>, and change anything else with <c>spec with { … }</c>.
/// </summary>
public static class DailyCountsChart
{
    /// <summary>The most days one chart draws: the most categories a column chart with a cell width takes.</summary>
    public const int MaxDays = ChartValidation.MaxFixedCategories;

    /// <summary>The chart of <paramref name="counts"/>, or null when there is no day to draw or every day in the range is 0, so the page shows
    /// its own empty state. Counts outside the range are left out.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="counts"/> is null.</exception>
    /// <exception cref="ArgumentException">A count is null or negative, a word is blank, the date format cannot format a date, From is after To,
    /// or the range is longer than <see cref="MaxDays"/> days.</exception>
    public static ChartSpec? Build(IEnumerable<DayCount> counts, DailyCountsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var o = options ?? new DailyCountsOptions();
        if (string.IsNullOrWhiteSpace(o.Singular) || string.IsNullOrWhiteSpace(o.Plural))
            throw new ArgumentException("A daily-counts chart needs a word for one and a word for several (Singular and Plural), such as registration and registrations.");
        try { new DateOnly(2027, 1, 1).ToString(o.DateFormat, CultureInfo.InvariantCulture); }
        catch (FormatException) { throw new ArgumentException("DateFormat must be a valid format string for DateOnly.ToString."); }
        if (o.From is { } setFrom && o.To is { } setTo && setFrom > setTo)
            throw new ArgumentException(FormattableString.Invariant($"From, {setFrom:yyyy-MM-dd}, is after To, {setTo:yyyy-MM-dd}."));
        var list = counts.ToList();
        if (list.Any(c => c is null)) throw new ArgumentException("A daily count cannot be null.");
        if (list.Any(c => c.Count < 0)) throw new ArgumentException("A day's count cannot be negative.");
        if (list.Count == 0 && o.From is null && o.To is null) return null;
        // An end that is not set stretches to take in the one that is.
        DateOnly? earliest = list.Count > 0 ? list.Min(c => c.Date) : null, latest = list.Count > 0 ? list.Max(c => c.Date) : null;
        var from = o.From ?? new[] { earliest, o.To }.Where(d => d.HasValue).Min()!.Value;
        var to = o.To ?? new[] { latest, o.From }.Where(d => d.HasValue).Max()!.Value;
        var days = to.DayNumber - from.DayNumber + 1;
        if (days > MaxDays)
            throw new ArgumentException(FormattableString.Invariant($"A daily-counts chart draws at most {MaxDays:N0} days, and {from:yyyy-MM-dd} to {to:yyyy-MM-dd} is {days:N0}; narrow the range."));
        var totals = new long[days];
        foreach (var c in list)
            if (c.Date >= from && c.Date <= to) totals[c.Date.DayNumber - from.DayNumber] += c.Count;
        if (totals.All(n => n == 0)) return null;
        var style = o.Style ?? ChartStyle.Light;
        if (o.Color is null && style.Series.Count == 0)
            throw new ArgumentException("The style has no series colours to draw the bars in; give Color or a style with a palette.");
        string Noun(long n) => n == 1 ? o.Singular : o.Plural;
        static string Number(long n) => n.ToString("N0", CultureInfo.InvariantCulture);
        static string Capital(string word) => char.ToUpperInvariant(word[0]) + word[1..];
        var crosses = from.Year != to.Year;
        var points = new ChartPoint[days];
        long running = 0;
        for (var i = 0; i < days; i++)
        {
            var day = from.AddDays(i);
            running += totals[i];
            var label = day.ToString(o.DateFormat, CultureInfo.InvariantCulture);
            var month = i == 0 || day.Day == 1 ? day.ToString(crosses && (i == 0 || day.Month == 1) ? "MMM yyyy" : "MMM", CultureInfo.InvariantCulture) : null;
            var number = day.Day.ToString(CultureInfo.InvariantCulture);
            points[i] = new ChartPoint(i, totals[i], label)
            {
                AxisLabel = month is null ? number : $"{number}\n{month}",
                Name = $"{label}: {Number(totals[i])} {Noun(totals[i])}" + (o.RunningTotal ? $", {Number(running)} so far" : "")
            };
        }
        // Whole-number ticks, at most five steps, one step above the tallest day so its count stands clear of the description.
        var largest = totals.Max();
        long step = 1;
        for (var k = 0; largest / step + 1 > 5; k++) step = k % 3 == 1 ? step * 5 / 2 : step * 2;
        var top = step * (largest / step + 1);
        var ticks = new List<AxisTick>();
        for (var v = 0L; v <= top; v += step) ticks.Add(new AxisTick(v));
        var total = totals.Sum();
        return new ChartSpec
        {
            Kind = ChartKind.Column, CellWidth = o.CellWidth, Width = 320, Height = 320, Style = style,
            Title = o.Title ?? $"{Capital(o.Plural)} per day",
            Description = $"{Number(total)} {Noun(total)} across {Number(days)} {(days == 1 ? "day" : "days")}",
            YMax = top, YTickValues = ticks,
            Series = [new ChartSeries(Capital(o.Plural), points) { Color = o.Color ?? style.Series[0], ValueLabels = true, MarkZero = true }]
        };
    }
}
```

  Check three things against the code:
  - **`ChartStyle.Series`'s type**, for `.Count`/indexing, and whether `ChartStyle.Light` is the preset's name (Placings.cs uses it).
  - **`DateOnly` format strings work in `FormattableString.Invariant`.** If they don't, write the dates with `.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)`.
  - **`ChartPoint.Name` is a single line.** The tests keep it under 200 characters with the default format. A host format that makes it longer is refused by validation when rendered, which is acceptable. Say so in the XML doc of `DateFormat` if you agree.

- [ ] **Step 4: Run the tests** until all pass.
- [ ] **Step 5: Reference rows**, after `point-name/line`:

```csharp
// The builder: forty invented days across a month end, and twelve across a year end, some of them zero.
DayCount[] Invented(DateOnly start, int days) => Enumerable.Range(0, days).Select(i => new DayCount(start.AddDays(i), (i * 7 + 2) % 9 == 0 ? 0 : (i * 5 + 3) % 13)).ToArray();
lines.Add($"daily-counts/month {Hash(Render(DailyCountsChart.Build(Invented(new DateOnly(2026, 9, 10), 40))!))}");
lines.Add($"daily-counts/year {Hash(Render(DailyCountsChart.Build(Invented(new DateOnly(2026, 12, 24), 12))!))}");
```

- [ ] **Step 6: Baseline in both finishes:** 408 rows, no other row moves. Look at both drawings:
  - month and year labels;
  - the thinning around `Jan 2027`;
  - hairlines;
  - counts above bars, clear of the description.

  Regenerate the references and delete the run files.
- [ ] **Step 7: Build and test:** 0 warnings, 0 failed.
- [ ] **Step 8: Commit.**

```bash
git add src/Lumen.Charts/DailyCounts.cs tests/Lumen.Charts.Tests/Program.cs tests/Lumen.Charts.Baseline/Program.cs tests/Lumen.Charts.Baseline/reference/refined.txt tests/Lumen.Charts.Baseline/reference/classic.txt
git commit -m "Add DailyCountsChart.Build: counts per day as fixed-pitch columns with day and month labels and named bars"
```

---

### Task 6: The gallery card and its checks

**Files:**
- Modify `samples/Lumen.Gallery/Components/Pages/Home.razor` (a card beside the other single charts, e.g. near the `FitHeight`/`PaintBackground`/`GapLabel` cards ~56-59).
- Modify `samples/Lumen.Gallery/DemoData.cs` (an invented 75-day series and the builder call).
- Modify `tests/Lumen.Charts.Tests/Program.cs` (a gallery test; and any home-page count pins).
- Modify `tests/verify-api.ps1` (the home page's prerender of the card).
- Modify `tests/Lumen.Charts.BrowserTests/Program.cs` (a phone test).

**Interfaces:**
- Consumes: `DailyCountsChart.Build`, `<LumenChart ScrollToEnd>`.

- [ ] **Step 1: The data and card.**
  - **`DemoData`:** add `public static ChartSpec? SignUps(ChartTheme theme)`. It calls `DailyCountsChart.Build` on 75 invented days starting 20 Aug 2026, with a few zero days, the counts made from a formula, and the gallery theme's style the way neighbouring `DemoData` methods take it.
  - **Words:** `Singular = "sign-up"`, `Plural = "sign-ups"`, with the default `Title` ("Sign-ups per day").
  - **`Home.razor`:** add a card wrapped in `@Branded(...)` like its neighbours: `<LumenChart Spec="DemoData.SignUps(Theme)!" FitWidth="true" ScrollToEnd="true" ShowLegend="false"/>`. Give it a `fit-note` that names `DailyCountsChart.Build`, `CellWidth`, `AxisLabel`, `MarkZero`, `Name` and `ScrollToEnd` in the page's style.
  - **The section id:** give the card's `chart-card` an `id="sign-ups"` so the tests can find it.
- [ ] **Step 2: Unit test.**
  - **The spec:** `DemoData.SignUps(ChartTheme.Light)` builds, validates and has 75 bars. At least three of them are zero and named "…: 0 sign-ups, … so far".
  - **The labels:** its first `AxisLabel` is `"20\nAug"` and the 1 September one is `"1\nSep"`.
  - **The drawing:** its static SVG is `76 + 75 × 30 + 30 = 2356` wide.
  - **Pins:** update any test that pins the home page's chart count; search the tests for the home page's counts.
- [ ] **Step 3: HTTP.** Add one check to `verify-api.ps1`: the home page prerenders the card, i.e. `id="sign-ups"`, `lumen-fixed`, `--lumen-drawn:2356px` and `aria-label='Thu 20 Aug 2026: `. Name it in the file's style. The count goes from 300 to 301; say so.
- [ ] **Step 4: Browser.** Add one test, written like the Category heatmap's phone test (BrowserTests ~1785). In a 375×812 context (`IsMobile`, `HasTouch`, scale 2), on the home page, after the card's chart is interactive:
  - the viewport's `scrollWidth` is about the drawn width, 2356;
  - the page itself does not scroll sideways;
  - the viewport is scrolled to its end: `scrollLeft + clientWidth >= scrollWidth - 1`, waited for with `WaitForFunctionAsync`;
  - the last bar's `aria-label` starts `"Mon 2 Nov 2026: "` (20 Aug 2026 plus 74 days) and ends `" so far"`;
  - a zero bar's label is `"…: 0 sign-ups, … so far"`;
  - each muted `0` value label's computed fill clears 4.5:1 against the drawing's background.

  The suite's axe sweeps already cover the home page in light, dark and Midnight. Run the muted-0 contrast check in each, as Task 4 of 0.46.2 did for the kept values (the `KeptValuesClear` pattern), so it measures under each theme.
- [ ] **Step 5: Run everything.**
  - Stop the gallery by name, then build the solution and `tests/Lumen.Charts.BrowserTests`.
  - Run unit, start the gallery on 5188 in the background, run `verify-api.ps1` (301) and the browser suite.
  - Take 375 px and 1400 px screenshots of the card into `%TEMP%\lumen0470\`.
  - Stop the gallery by name.
  - Known flakes: the Midnight axe sweep's `Hovered()` and the 1280 tab-stop test on a fresh gallery. Re-run once and say so.
- [ ] **Step 6: Commit.**

```bash
git add samples/Lumen.Gallery/Components/Pages/Home.razor samples/Lumen.Gallery/DemoData.cs tests/Lumen.Charts.Tests/Program.cs tests/verify-api.ps1 tests/Lumen.Charts.BrowserTests/Program.cs
git commit -m "Gallery: sign-ups per day, drawn by DailyCountsChart and opened at its latest day"
```

---

### Task 7: Docs, recipes and version

**Files:**
- Modify `README.md`:
  - a "### Counts per day" section near "### Heatmap tables";
  - the column and `ChartPoint` reference parts;
  - "## Supported behavior and limits";
  - a new "## 0.47.0 additions" before "## 0.46.2 additions";
  - the header's preview version.
- Modify `integrations/claude-code/lumen-charts/references/api.md`:
  - `CellWidth` row;
  - the ChartPoint paragraph (`AxisLabel`, `Name`);
  - the ChartSeries part (`MarkZero`);
  - the Blazor part (`ScrollToEnd`);
  - a `DailyCountsChart` paragraph beside `PlacingsChart`'s.
- Modify `integrations/claude-code/lumen-charts/SKILL.md`: body only.
- Modify `integrations/claude-code/lumen-charts/references/recipes-race-face.md`:
  - the header's version;
  - a "Registrations per day" recipe and a "Course profile" recipe.
- Modify `tests/Lumen.Charts.Recipes/check.py` (render a builder's chart).
- Modify `docs/VERIFICATION.md`, `tests/Lumen.Charts.Baseline/README.md` and `Directory.Build.props` (`<Version>0.47.0</Version>`).

- [ ] **Step 1: README and the skill.** Write what the code does, from the code and the spec. Every number must be true:
  - pitch ≥ 8;
  - 4,000 categories;
  - the 320 floor with room at the right;
  - `width:{drawn}px`;
  - the 72% bar and `CellWidth − 2` labels;
  - axis lines ≤ 12, with the second line 14 under the first;
  - the hairline 1 unit;
  - `Name` ≤ 200;
  - the builder's rules.

  In "What changes for an existing chart", say that a `CellWidth` heatmap's static SVG is now written at its drawn width and needs an `overflow-x:auto` box on a static page. In the limits, add:
  - the Y axis scrolls away;
  - the redraw cost of long ranges;
  - `ScrollToEnd` is interactive only;
  - `Name` owns the whole name;
  - year labels thin their neighbours.

  Keep the SKILL.md frontmatter byte-identical, and check it with `git diff`.
- [ ] **Step 2: The recipes.**
  - **"Registrations per day":**
    - the host's own counts (invented in the recipe) become `DayCount`s;
    - the reader's free From–To is clamped to `DailyCountsChart.MaxDays`;
    - `DailyCountsChart.Build` is called with Race Face's nouns (`registration`/`registrations`);
    - the empty state is shown for null;
    - the markup is `<LumenChart Spec FitWidth ScrollToEnd ShowLegend="false">`;
    - static use goes in a `div` with `overflow-x:auto`.
  - **"Course profile":**
    - a line chart of elevation over distance (invented km and m);
    - `ChartSeries.Gradient` stops that each clear 3:1;
    - `YMinSpan` (e.g. 60), `YUnit = " m"`, an `XFormat`/label for km, and no markers;
    - a bullet saying why it is not an area: an area stands on zero and refuses `YMinSpan`.
  - **`check.py`:** it renders `var x = <Something>Chart.Build(...)!` lines, as it renders `new ChartSpec` lines, so both recipes compile and render. Run `python check.py` and `dotnet run -c Release` in `tests/Lumen.Charts.Recipes`. Expect 41 charts, the 39 plus these two; if the count differs, say why.
- [ ] **Step 3: VERIFICATION.md and the baseline README.**
  - **VERIFICATION.md:** add a 0.47.0 entry in the 0.46.2 entry's format, with the counts you measure in Step 5.
  - **Baseline README:** add the sentence that v0.47.0's references are 408 rows: v0.46.2's 402, 396 reproduced exactly in both finishes and six changed by design (the root style of every `CellWidth` drawing), with six rows added (`columns/cell-width`, `columns/axis-labels`, `columns/mark-zero`, `point-name/line`, `daily-counts/month`, `daily-counts/year`).
- [ ] **Step 4: Version.** `Directory.Build.props`: `0.46.2` → `0.47.0`.
- [ ] **Step 5: Full verification.**
  - `taskkill //IM Lumen.Gallery.exe //F`; build at 0 warnings.
  - Unit; baseline in both finishes against the references, with no difference.
  - Gallery and `verify-api.ps1`; the gallery browser suite.
  - The WebAssembly host:
    - build `samples/Lumen.Wasm`;
    - run it on 5199;
    - run the browser suite with `http://localhost:5199`;
    - stop it by PID.
  - Recipes.
  - Write every count into VERIFICATION.md.
- [ ] **Step 6: Commit.**

```bash
git add README.md integrations/claude-code/lumen-charts/references/api.md integrations/claude-code/lumen-charts/SKILL.md integrations/claude-code/lumen-charts/references/recipes-race-face.md tests/Lumen.Charts.Recipes/check.py docs/VERIFICATION.md tests/Lumen.Charts.Baseline/README.md Directory.Build.props
git commit -m "Document 0.47.0: counts per day, fixed-pitch columns, axis labels, MarkZero, Name, ScrollToEnd, and the course-profile recipe"
```
