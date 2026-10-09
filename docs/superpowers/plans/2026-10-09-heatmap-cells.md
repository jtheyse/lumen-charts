# Heatmap Cells Show It All (0.46.2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a heatmap cell draw everything Race Face's hand-built table drew: the points note (`CellNotes`), a not-rated cell's value beside its reason (`NotRatedKeepsValue`), and fitted rows tall enough for every cell's words.

**Architecture:**
- **Model and validation:** two opt-in `bool`s on `ChartSpec`, validated and kept out of the gradient-ID hash at `false`.
- **One block writer:** in `ChartSvg`, `CellBlock` replaces `CellWords` and `CellReason`. Without the switches it reproduces their output exactly. The reason wrap moves into a shared `CellLines`.
- **Fitted height:** `FittedHeight` measures every cell's block with the same parts (`CellNeed`), so fitted rows grow to fit the tallest cell.

**Tech Stack:** C# / .NET 8. The executable assertion suite is `tests/Lumen.Charts.Tests` (`Test`/`Check`/`Reject`, run with `dotnet run`). The baseline hash harness is `tests/Lumen.Charts.Baseline`. The browser suite is Playwright, `tests/Lumen.Charts.BrowserTests`. The gallery is `samples/Lumen.Gallery`. HTTP checks are `tests/verify-api.ps1`.

**Spec:** `docs/superpowers/specs/2026-10-09-heatmap-cells-design.md` (commit 44e2260). Read it before starting any task.

## Global Constraints

- **Invented data only.** No real rider's name, number, age or result anywhere: code, tests, samples, docs, commit messages.
- **Never colour alone:** a not-rated cell still says so in its dashed outline, its reason and its name. The muted value is emphasis only.
- **Drawn text clears 4.5:1.** The muted value uses `Style.Muted` only where `Contrast.Ratio(Style.Muted, Style.Background) >= 4.5`, else the cell's ink.
- **The classic finish stays byte for byte 0.23.0** for every spec 0.23.0 could draw. Cell text is newer, so these changes apply in both finishes.
- **The gradient-ID hash:** new spec properties stay out of it at their defaults (`false`).
- **Unchanged without the switches:** without `CellNotes` and `NotRatedKeepsValue`, a heatmap without `FitHeight` draws exactly as in 0.46.1.
- **XML docs:** every new public member needs an XML doc comment; the build fails without one. A Release build must show 0 warnings.
- **Processes:**
  - Stop `Lumen.Gallery.exe` by image name only: `taskkill //IM Lumen.Gallery.exe //F`, which you need before building, since it locks the output.
  - Stop every other process by PID only: `netstat -ano`, find the PID, then `taskkill //PID <pid> //F`.
  - Never `taskkill /IM` anything else.
- **The Race Face brief:** never open, read or search `lumen-charts-race-face-brief.md` in the repo root. Exclude it from every recursive search (`--exclude=lumen-charts-race-face-brief.md`, or search `src/`, `tests/`, `samples/`, `docs/`, `integrations/` only).
- **Git:** commit with explicit paths, never `git add .` or `git add -A`. Never push, merge, rebase or switch branches.
- **Leftovers:** delete any `.playwright-mcp` folder you create. Save screenshots outside the repo (`%TEMP%\lumen0462\`) and list them in your report.
- **The skill's frontmatter description** stays at 1,024 characters or fewer, with no unquoted `: `.

## Review Focus

1. **A separator-only note** (`" · "`, `", "`, `"   "`) trims to nothing. Expect no empty `<text>` and no blank line; the block is as if there were no note. *Task 2.*
2. **A not-rated cell with no value** under `NotRatedKeepsValue` writes no value line, no "—" and no empty text: only its sub-label and reason, and no note, since there is no value for it to follow. *Task 2.*
3. **Narrow cells** (`CellWidth` 24) with both switches, `FitHeight` and a long note: no drawn word runs past its cell (`Wide(text)*10/11 <= cw-6`, or `Wide(value) <= cw-6` for the 11 px value), and a single too-wide word is cut with "…", not wrapped one character per line. *Task 2 and Task 3.*
4. **Byte-identity without the switches:** every `heatmap-table/*` baseline row that does not set `FitHeight` hashes as in 0.46.1, in both finishes. *Task 3 runs the baseline.*
5. **A custom style whose `Muted` falls under 4.5:1** against its background: the kept value is written in the cell's ink, never in the faint muted colour. *Task 2.*

---

### Task 1: Model, validation and hash

**Files:**
- Modify: `src/Lumen.Charts/Models.cs`:
  - add `CellNotes` and `NotRatedKeepsValue` after `FitHeight` (~803);
  - update the doc comments of `ValueNote` (306-312), `NotRated` (354-357), `CellText` (725-729) and `FitHeight` (788-802).
- Modify: `src/Lumen.Charts/ChartValidation.cs`:
  - add the switch check after line 116;
  - add the note line-break check and change the 40-character message (267-270).
- Modify: `src/Lumen.Charts/ChartSvg.cs`: the hash modifier `Unfitted` (105-116) and its doc comment (98-104).
- Test: `tests/Lumen.Charts.Tests/Program.cs`:
  - the default-JSON pin (4241-4245);
  - the 40-character message pin (10098);
  - a new `// 0.46.2` block inserted right after the test that ends at line 10540, before the `ChartMarkup` helper.

**Interfaces:**
- Produces: `public bool ChartSpec.CellNotes { get; init; }` and `public bool ChartSpec.NotRatedKeepsValue { get; init; }`, declared in that order after `FitHeight`. Tasks 2–5 read them.

- [ ] **Step 1: Write the failing tests.** Insert this block after the `});` that ends the test "Heatmap follow-ups: the stylesheet freezes the layer…" (line 10540), before `// Renders LumenChart with any parameters…`:

```csharp
// 0.46.2: heatmap cells show it all.
Test("Heatmap cells: CellNotes and NotRatedKeepsValue are accepted on a heatmap with cell text and refused elsewhere",()=>{
    ChartValidation.Validate(HeatGrid(s=>s with{CellNotes=true,NotRatedKeepsValue=true}));
    foreach(var bad in new[]{HeatGrid(s=>s with{CellText=false,CellNotes=true}),HeatGrid(s=>s with{CellText=false,NotRatedKeepsValue=true}),
        Spec(ChartKind.Column) with{CellNotes=true},Spec(ChartKind.Line) with{NotRatedKeepsValue=true}})
    {
        Reject(()=>ChartValidation.Validate(bad));
        try{ChartValidation.Validate(bad);}catch(ArgumentException e){Check(e.Message.StartsWith("CellNotes and NotRatedKeepsValue write in a heatmap's cells, so they apply to heatmaps with CellText only."),e.Message);}
    }
});
Test("Heatmap cells: with CellNotes a heatmap note takes no line break or tab; without it, as before, it may",()=>{
    foreach(var note in new[]{" · 34 pts\n5 riders"," · 34 pts\r","34\tpts"})
    {
        ChartValidation.Validate(Noted(HeatGrid(),note));
        var drawn=Noted(HeatGrid(s=>s with{CellNotes=true}),note);
        Reject(()=>ChartValidation.Validate(drawn));
        try{ChartValidation.Validate(drawn);}catch(ArgumentException e){Check(e.Message.StartsWith("With CellNotes a heatmap cell's value note is drawn in the cell, where Lumen wraps it, so it takes no line breaks or tabs."),e.Message);}
    }
});
Test("Heatmap cells: the two switches left false change nothing a heatmap draws, and set they tell its gradient IDs apart",()=>{
    Check(ChartSvg.Render(HeatGrid())==ChartSvg.Render(HeatGrid(s=>s with{CellNotes=false,NotRatedKeepsValue=false})),"false is the default");
    var plain=HeatGrid();
    Check(ChartSvg.IdPrefix(plain)==ChartSvg.IdPrefix(plain with{CellNotes=false,NotRatedKeepsValue=false}),"false is left out of the hash");
    Check(ChartSvg.IdPrefix(plain)!=ChartSvg.IdPrefix(plain with{CellNotes=true}),"CellNotes counts when set");
    Check(ChartSvg.IdPrefix(plain)!=ChartSvg.IdPrefix(plain with{NotRatedKeepsValue=true}),"NotRatedKeepsValue counts when set");
});
```

- [ ] **Step 2: Update the two pins this task moves.**
  - **The 40-character message** (line 10098): change the expected start to `"A value note is at most 20 characters (40 on a heatmap cell, where CellNotes draws it on lines of its own)"`.
  - **The default-JSON pin:**
    - In its comment (4240-4241), add: `and since 0.46.2 its cell notes and kept not-rated values, written after its fitted height.`
    - In `defaults` (4245), replace the tail `\"FitHeight\":false}` with `\"FitHeight\":false,\"CellNotes\":false,\"NotRatedKeepsValue\":false}`.

- [ ] **Step 3: Run the tests and see them fail.**
  - Run: `taskkill //IM Lumen.Gallery.exe //F` (ignore "not found"), then `dotnet build Lumen.Charts.slnx -c Release`.
  - Expected: build FAIL, `'ChartSpec' does not contain a definition for 'CellNotes'`.

- [ ] **Step 4: Add the properties** to `src/Lumen.Charts/Models.cs`, after `public bool FitHeight { get; init; }`:

```csharp
    /// <summary>
    /// On a heatmap with <see cref="CellText"/>, writes each cell's <see cref="ChartPoint.ValueNote"/> in the cell, under its sub-label
    /// (0.46.2): 10 px, in the cell's ink, without the separator it starts with for its name (leading spaces, then one <c>·</c> or <c>,</c>
    /// and the spaces after it), so <c>" · 1840 pts, 12 athletes"</c> reads <c>1840 pts, 12 athletes</c>. It wraps onto as many lines as it
    /// needs, breaking after its commas first and then between words; a single word wider than the cell is cut with <c>…</c>. A cell with a
    /// value, or with a <see cref="ChartPoint.GapLabel"/>, writes its note, as its name says it. Where the cell is too short for every line,
    /// the note is the first to go, whole; with <see cref="FitHeight"/> the rows grow so it never has to. A note with a line break or a tab
    /// is refused while it is set. Heatmaps with cell text only; false by default, and left out of the gradient-ID hash while false.
    /// </summary>
    public bool CellNotes { get; init; }
    /// <summary>
    /// On a heatmap with <see cref="CellText"/>, a not-rated cell keeps its value (0.46.2): it writes its value, 11 px and weight 600, in
    /// the style's <see cref="ChartStyle.Muted"/> colour where that clears 4.5:1 against <see cref="ChartStyle.Background"/>, and in the
    /// cell's ink otherwise; then its sub-label; then its note, with <see cref="CellNotes"/>; then its <see cref="ChartPoint.NotRated"/>
    /// reason, wrapped, as its last lines. The dashed outline, the reason and the cell's name still say it is not rated, so the muted
    /// colour is never the only cue. Off, a not-rated cell writes its reason in place of its value, as 0.46.1 does. Heatmaps with cell
    /// text only; false by default, and left out of the gradient-ID hash while false.
    /// </summary>
    public bool NotRatedKeepsValue { get; init; }
```

- [ ] **Step 5: Update the four doc comments** in `Models.cs`. Each must still read as one sentence flow and keep its existing facts.
  - **`ValueNote`:** replace `or 40 on a heatmap cell, which never draws it, from 0.46.1,` with `or 40 on a heatmap cell, from 0.46.1, which draws it in the cell only with <see cref="ChartSpec.CellNotes"/>, from 0.46.2,`.
  - **`NotRated`:** after `then its sub-label if a line is left.` add `With <see cref="ChartSpec.NotRatedKeepsValue"/> it keeps its value instead, muted, and writes the reason as its last lines (0.46.2).`
  - **`CellText`:** after `in place of a value, from 0.46.1.` add `<see cref="CellNotes"/> adds each cell's note, and <see cref="NotRatedKeepsValue"/> keeps a not-rated cell's value (0.46.2).`
  - **`FitHeight`:** replace `A heatmap takes 36 units a row,` with `A heatmap's rows are as tall as the tallest cell's written words need, 12 units a line and 13 for a value, plus 4, and at least 36 (0.46.2),`.

- [ ] **Step 6: Validation** in `src/Lumen.Charts/ChartValidation.cs`.
  - **After line 116** (the `CellWidth < 24` check):

```csharp
        if ((spec.CellNotes || spec.NotRatedKeepsValue) && (spec.Kind != ChartKind.Heatmap || !spec.CellText))
            throw new ArgumentException("CellNotes and NotRatedKeepsValue write in a heatmap's cells, so they apply to heatmaps with CellText only.");
```

  - **In the `p.ValueNote is not null` block**, replace the length check (269-270) with:

```csharp
                    if (p.ValueNote.Length > (mark == ChartKind.Heatmap ? 40 : 20))
                        throw new ArgumentException("A value note is at most 20 characters (40 on a heatmap cell, where CellNotes draws it on lines of its own), such as /48 after a finishing position for the size of its field; longer words belong in the point's label.");
                    if (spec.CellNotes && mark == ChartKind.Heatmap && p.ValueNote.IndexOfAny(['\n', '\r', '\t']) >= 0)
                        throw new ArgumentException("With CellNotes a heatmap cell's value note is drawn in the cell, where Lumen wraps it, so it takes no line breaks or tabs.");
```

- [ ] **Step 7: The hash** in `src/Lumen.Charts/ChartSvg.cs`.
  - **In `Unfitted`**, after the `ColumnLabelsOnTop` branch (115):

```csharp
            else if (property.Name == nameof(ChartSpec.CellNotes)) property.ShouldSerialize = (_, notes) => notes is true;
            else if (property.Name == nameof(ChartSpec.NotRatedKeepsValue)) property.ShouldSerialize = (_, kept) => kept is true;
```

  - **In its doc comment**, end the last sentence with `…, false by default, as are a heatmap's cell notes and kept not-rated values, from 0.46.2.`

- [ ] **Step 8: Run the build and the tests, and see them pass.**
  - Run: `dotnet build Lumen.Charts.slnx -c Release`. Expected: `0 Warning(s)`, `0 Error(s)`.
  - Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build`. Expected: the three new tests PASS, `0 failed` overall, and a passed count 3 above the starting count (711).

- [ ] **Step 9: Commit.**

```bash
git add src/Lumen.Charts/Models.cs src/Lumen.Charts/ChartValidation.cs src/Lumen.Charts/ChartSvg.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Add CellNotes and NotRatedKeepsValue to heatmap specs, validated and kept out of the hash at false"
```

---

### Task 2: One block writer for a cell's words

**Files:**
- Modify: `src/Lumen.Charts/ChartSvg.cs`:
  - `Heatmap()`'s cell-text call (3248-3252);
  - replace `CellWords` (3319-3332) and `CellReason` (3334-3383) with `CellLines`, `CellNote`, `NoteLines`, `CellParts`/`PartsOf` and `CellBlock`.
- Test: `tests/Lumen.Charts.Tests/Program.cs`: the `// 0.46.2` block from Task 1.

**Interfaces:**
- Consumes: `ChartSpec.CellNotes`, `ChartSpec.NotRatedKeepsValue` (Task 1).
- Produces, for Task 3:
  - `private sealed record CellParts(string? Value, bool Kept, string? Sub, List<string> Note, string? Said, bool ReasonFirst, bool Rated)`;
  - `private static CellParts PartsOf(ChartSpec s, ChartPoint p, double cw)`;
  - `private static List<string> CellLines(string said, double room, int most)`.
- Produces, for tests: `internal static string CellNote(string note)`.

- [ ] **Step 1: Write the failing tests.** Append to the `// 0.46.2` block.

```csharp
// The Sprint 2025 cell with a points note, and the Long distance 2025 cell (not rated, 1.2, "/4 starts") with one.
ChartSpec Notes(ChartSpec s,string sprint=" · 34 pts, 5 riders",string distance=" · 8 pts, 3 riders")=>
    s with{Series=[s.Series[0] with{Points=[s.Series[0].Points[0] with{ValueNote=sprint},s.Series[0].Points[1]]},s.Series[1] with{Points=[s.Series[1].Points[0] with{ValueNote=distance},s.Series[1].Points[1]]}]};
// The words written in one cell, top to bottom: the 10 and 11 px texts whose x is the cell's centre, in drawing order.
string[] WordsAt(string svg,double cx)=>Regex.Matches(svg,$"<text x='{cx.ToString(CultureInfo.InvariantCulture)}' y='[^']*'[^>]*font-size='1[01]'[^>]*>([^<]*)<").Select(m=>m.Groups[1].Value).ToArray();
Test("Heatmap cells: a drawn note loses the separator its name needs, breaks after its commas first, then between words",()=>{
    Check(ChartSvg.CellNote(" · 34 pts, 5 riders")=="34 pts, 5 riders"&&ChartSvg.CellNote(", 3 riders")=="3 riders"&&ChartSvg.CellNote("/48")=="/48"&&ChartSvg.CellNote(" · ")==""&&ChartSvg.CellNote("   ")=="","trimmed");
    // 90 wide: a line holds 84 at 10 px. "34 pts, 5 riders" is 86.4, so it breaks after its comma, not between "5" and "riders".
    var svg=ChartSvg.Render(Notes(HeatGrid(s=>s with{CellWidth=90,Height=400,CellNotes=true})));
    Check(WordsAt(svg,175).Take(4).SequenceEqual(["2.8","/12 starts","34 pts,","5 riders"]),string.Join(" | ",WordsAt(svg,175)));
    // A clause wider than a line wraps at its words: "twelve invented starters" has no comma.
    var words=ChartSvg.Render(Notes(HeatGrid(s=>s with{CellWidth=90,Height=400,CellNotes=true})," · twelve invented starters"));
    Check(WordsAt(words,175).Take(5).SequenceEqual(["2.8","/12 starts","twelve","invented","starters"]),string.Join(" | ",WordsAt(words,175)));
    // A note that is only a separator writes nothing: the cell is as it was.
    var bare=ChartSvg.Render(Notes(HeatGrid(s=>s with{CellWidth=90,Height=400,CellNotes=true})," · "));
    Check(WordsAt(bare,175).Take(2).SequenceEqual(["2.8","/12 starts"])&&!Regex.IsMatch(bare,"font-size='10'[^>]*></text>"),string.Join(" | ",WordsAt(bare,175)));
});
Test("Heatmap cells: without the switches a not-rated cell in 36-unit rows writes 0.46.1's reason, cut at a word, in place of its value",()=>{
    // CellBlock replaces CellWords and CellReason. The 0.46.0 and 0.46.1 cell-text tests and the unchanged baseline prove the rest; this pins
    // 0.46.1's cut at a fixed height, since FitHeight's rows now grow (Task 3).
    var seventy=ChartSvg.Render(Six(HeatGrid(s=>s with{CellWidth=72,Height=376})));
    Check(seventy.Contains(">too few<")&&seventy.Contains(">starts to…<")&&!seventy.Contains(">1.2<"),"0.46.1's reason, cut at a word, in place of the value");
});
Test("Heatmap cells: a not-rated cell keeps its value, muted, then its sub-label, its note and its reason last",()=>{
    // Long distance 2025 is row 1 at x 130..220, its centre 175. At Height 290 a row is 65, room for all five lines: 13 + 4 x 12 = 61.
    var spec=Notes(HeatGrid(s=>s with{CellWidth=90,Height=290,CellNotes=true,NotRatedKeepsValue=true}));
    var svg=ChartSvg.Render(spec);
    var lines=WordsAt(svg,175);
    Check(lines.Skip(lines.Length-5).SequenceEqual(["1.2","/4 starts","8 pts, 3 riders","too few starts","to rate"]),string.Join(" | ",lines));
    var value=Regex.Match(svg,"<text x='175' y='([^']*)'[^>]*font-size='11' font-weight='600' fill='([^']*)'[^>]*>1.2<");
    Check(value.Success&&value.Groups[2].Value==ChartStyle.Light.Muted,"the kept value is muted: "+value.Value);
    Check(Contrast.Ratio(ChartStyle.Light.Muted,ChartStyle.Light.Background)>=4.5,"Light's muted clears 4.5:1");
    Check(HeatNames(spec).Contains("Long distance: 2025 · /4 starts, 1.2 · 8 pts, 3 riders, not rated: too few starts to rate"),string.Join(" | ",HeatNames(spec)));
    // The five lines are 12 apart, centred on the row's middle, 145 + 65/2 = 177.5: the first at 177.5 - 30 + 9 = 156.5.
    Check(value.Groups[1].Value=="156.5","first baseline "+value.Groups[1].Value);
    // A style whose muted colour falls under 4.5:1 writes the value in the cell's ink instead.
    var faint=ChartStyle.Light with{Muted="#B8B8B8"};
    var inked=ChartSvg.Render(spec with{Style=faint});
    Check(Regex.IsMatch(inked,$"font-weight='600' fill='{faint.Text}'[^>]*>1.2<"),"ink when muted is too faint");
    // Without NotRatedKeepsValue the reason leads, then the sub-label, then the note, as 0.46.1 orders a not-rated cell.
    var led=WordsAt(ChartSvg.Render(spec with{NotRatedKeepsValue=false}),175);
    Check(led.Skip(led.Length-4).SequenceEqual(["too few starts","to rate","/4 starts","8 pts, 3 riders"]),string.Join(" | ",led));
    // A not-rated cell with no value writes no value line and no note: its sub-label, then its reason.
    var empty=WordsAt(ChartSvg.Render(FirstCell(spec,new ChartPoint(0,null,"2025"){SubLabel="/2 starts",NotRated="too few starts to rate",ValueNote=" · 2 pts"})),175);
    Check(empty.Take(3).SequenceEqual(["/2 starts","too few starts","to rate"])&&!empty.Contains("2 pts")&&!empty.Contains("—"),string.Join(" | ",empty));
    // A gap-label cell writes its word, its sub-label and its note.
    var gap=WordsAt(ChartSvg.Render(FirstCell(spec,new ChartPoint(0,null,"2025"){SubLabel="/0 starts",GapLabel="did not race",ValueNote=" · 0 pts"})),175);
    Check(gap.Take(3).SequenceEqual(["did not race","/0 starts","0 pts"]),string.Join(" | ",gap));
});
Test("Heatmap cells: a cell too short for its block gives up its note, then its sub-label, then its value, and its reason last",()=>{
    var spec=Notes(HeatGrid(s=>s with{CellWidth=90,CellNotes=true,NotRatedKeepsValue=true}));
    string[] Last(int height,int count){var l=WordsAt(ChartSvg.Render(spec with{Height=height}),175);return l.Skip(Math.Max(0,l.Length-count)).ToArray();}
    // Two rows: a row is (Height - 160) / 2 and the block has that less 4.
    Check(Last(288,4).SequenceEqual(["1.2","/4 starts","too few starts","to rate"]),"60: the note goes first: "+string.Join(" | ",Last(288,4)));
    Check(Last(264,3).SequenceEqual(["1.2","too few starts","to rate"]),"48: then the sub-label: "+string.Join(" | ",Last(264,3)));
    Check(Last(240,2).SequenceEqual(["too few starts","to rate"]),"36: then the value: "+string.Join(" | ",Last(240,2)));
    // Six rows of (320 - 160) / 6 = 26.7: one line, so the reason is cut at a word.
    var one=WordsAt(ChartSvg.Render(Six(spec)),175);
    Check(one.Contains("too few…")&&!one.Contains("1.2"),string.Join(" | ",one));
    // A rated cell with a note: value, sub-label and two note lines need 49; at 48 the note goes whole, never half of it.
    Check(WordsAt(ChartSvg.Render(spec with{Height=266}),175).Take(4).SequenceEqual(["2.8","/12 starts","34 pts,","5 riders"]),"49 fits");
    Check(WordsAt(ChartSvg.Render(spec with{Height=264}),175).Take(3).SequenceEqual(["2.8","/12 starts","1.2"]),"48: the note goes whole, and the next cell down follows");
});
Test("Heatmap cells: at every width, with both switches, no word runs past its cell, and a single long word is cut rather than broken letter by letter",()=>{
    var word=new string('w',26);
    foreach(var width in new double[]{24,30,48,72,90,120})
    {
        var spec=Notes(HeatGrid(s=>s with{CellWidth=width,Height=600,CellNotes=true,NotRatedKeepsValue=true})," · "+word+", 5 riders");
        var svg=ChartSvg.Render(spec);
        foreach(Match m in Regex.Matches(svg,"<text[^>]*font-size='10'[^>]*>([^<]*)<"))
            Check(ChartSvg.Wide(m.Groups[1].Value)*10/11<=width-6+1e-9,$"{width}: {m.Groups[1].Value}");
        foreach(Match m in Regex.Matches(svg,"<text[^>]*font-size='11' font-weight='600'[^>]*>([^<]*)<"))
            Check(ChartSvg.Wide(m.Groups[1].Value)<=width-6+1e-9,$"{width}: value {m.Groups[1].Value}");
        Check(!Regex.IsMatch(svg,"font-size='10'[^>]*>w<"),$"{width}: a word broken letter by letter");
    }
});
```

- [ ] **Step 2: Run the tests and see them fail.**
  - Run: `dotnet build Lumen.Charts.slnx -c Release`.
  - Expected: build FAIL, `'ChartSvg' does not contain a definition for 'CellNote'`.

- [ ] **Step 3: Replace `CellWords` and `CellReason`** (ChartSvg.cs 3319-3383) with the following. `CellLines` is `CellReason`'s wrap, moved as it is.

```csharp
    /// <summary>The lines a not-rated cell's reason, a gap label's word or a note's clause takes in a cell <paramref name="room"/> wide at
    /// 10 px, at most <paramref name="most"/> of them: wrapped at word breaks; words that do not fit cut at a word with "…", and a first word
    /// wider than a line by its characters. A word after the first line that is wider than a line ends the block before it, with "…" after the
    /// line above, and a cut first word is the whole block, so the text is always the words' start with at most one "…". Empty where not even
    /// "…" fits (0.46.1; shared from 0.46.2).</summary>
    private static List<string> CellLines(string said, double room, int most)
    {
        static double Width(string text) => Wide(text) * 10 / 11;
        string Fit(string text)
        {
            if (Width(text) <= room) return text;
            var keep = text.Length;
            while (keep > 1 && Width(Short(text, keep)) > room) keep--;
            return keep > 1 ? Short(text, keep) : Width("…") <= room ? "…" : "";
        }
        var words = said.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        if (most < 1) return lines;
        var i = 0;
        while (i < words.Length && lines.Count < most)
        {
            // A word wider than a line is cut by its characters only as the first word, and then nothing follows it: after the first line
            // the block ends before it, as it does for words that run out of lines, so the text is only ever the words' start.
            if (lines.Count > 0 && Width(words[i]) > room) break;
            var line = words[i++];
            while (i < words.Length && Width(line + " " + words[i]) <= room) line += " " + words[i++];
            lines.Add(line);
            if (Width(line) > room) break;
        }
        if (i < words.Length && lines.Count > 0)
        {
            // Words are left over: the last line gives up words until an ellipsis fits after it.
            var last = lines[^1];
            while (Width(last + "…") > room && last.Contains(' ')) last = last[..last.LastIndexOf(' ')];
            lines[^1] = Width(last + "…") <= room ? last + "…" : Fit(last + "…");
        }
        for (var k = 0; k < lines.Count; k++) lines[k] = Fit(lines[k]);
        lines.RemoveAll(line => line.Length == 0);
        return lines;
    }

    /// <summary>A heatmap cell's note as <see cref="ChartSpec.CellNotes"/> draws it: without the spaces it starts with, and then one leading
    /// <c>·</c> or <c>,</c> and the spaces after it, which part it from the value in the cell's name, so <c>" · 34 pts, 5 riders"</c> is
    /// drawn <c>34 pts, 5 riders</c> (0.46.2).</summary>
    internal static string CellNote(string note)
    {
        var text = note.TrimStart();
        if (text.Length > 0 && text[0] is '·' or ',') text = text[1..].TrimStart();
        return text.TrimEnd();
    }

    /// <summary>The lines a drawn note takes in a cell <paramref name="room"/> wide at 10 px, as many as it needs: its clauses, each up to and
    /// including its comma, joined on one line while they fit, and a clause wider than a line wrapped at its words as a reason is (0.46.2).</summary>
    private static List<string> NoteLines(string note, double room)
    {
        static double Width(string text) => Wide(text) * 10 / 11;
        var parts = note.Split(", ");
        var lines = new List<string>();
        for (var k = 0; k < parts.Length; k++)
        {
            var clause = (k < parts.Length - 1 ? parts[k] + "," : parts[k]).Trim();
            if (clause.Length == 0) continue;
            if (lines.Count > 0 && Width(lines[^1] + " " + clause) <= room) lines[^1] += " " + clause;
            else if (Width(clause) <= room) lines.Add(clause);
            else lines.AddRange(CellLines(clause, room, int.MaxValue));
        }
        return lines;
    }

    /// <summary>What a heatmap cell with <see cref="ChartSpec.CellText"/> may write, before any of it is given up for height (0.46.2):
    /// <list type="bullet">
    /// <item><description><c>Value</c>: its value in its format and unit; for a not-rated cell only with
    /// <see cref="ChartSpec.NotRatedKeepsValue"/>, when <c>Kept</c> is true; null where the cell writes none or it is wider than the cell
    /// less 6.</description></item>
    /// <item><description><c>Sub</c>: its sub-label where it fits that width.</description></item>
    /// <item><description><c>Note</c>: its note's lines with <see cref="ChartSpec.CellNotes"/>, for a cell with a value or a gap
    /// label.</description></item>
    /// <item><description><c>Said</c>: a not-rated cell's reason or a gap label's word, which leads the block (<c>ReasonFirst</c>) unless a
    /// not-rated cell keeps its value.</description></item>
    /// <item><description><c>Rated</c>: neither, a cell whose value is its words: when that value does not fit, it writes nothing, as in
    /// 0.46.0.</description></item>
    /// </list></summary>
    private sealed record CellParts(string? Value, bool Kept, string? Sub, List<string> Note, string? Said, bool ReasonFirst, bool Rated);

    private static CellParts PartsOf(ChartSpec s, ChartPoint p, double cw)
    {
        var room = cw - 6;
        var said = p.NotRated ?? p.GapLabel;
        var kept = p.NotRated is not null && s.NotRatedKeepsValue;
        var value = p.Y is { } y && (said is null || kept) ? HeatmapValues(s).Format(y) : null;
        if (value is not null && Wide(value) > room) value = null;
        var sub = p.SubLabel is { } line && Wide(line) * 10 / 11 <= room ? line : null;
        var note = s.CellNotes && p.ValueNote is { } written && (p.Y.HasValue || p.GapLabel is not null) && CellNote(written) is { Length: > 0 } text
            ? NoteLines(text, room) : [];
        return new(value, kept, sub, note, said, said is not null && !kept, said is null);
    }

    /// <summary>Writes a heatmap cell's words, centred on (<paramref name="cx"/>, <paramref name="cy"/>) in a cell <paramref name="cw"/> by
    /// <paramref name="ch"/>, as one block of lines 12 units apart within 6 of its width and 4 of its height (0.46.2):
    /// <list type="bullet">
    /// <item><description>a rated cell: its value, 11 px and weight 600, then its sub-label, then its note's lines;</description></item>
    /// <item><description>a not-rated cell that keeps its value: its value, muted where <see cref="ChartStyle.Muted"/> clears 4.5:1
    /// against the background, then its sub-label, its note and its reason;</description></item>
    /// <item><description>any other not-rated or gap-label cell: its reason or word, then its sub-label, then its note.</description></item>
    /// </list>
    /// Where the cell is too short for them all it gives up its note, whole, then its sub-label, then its value, and last its reason's lines
    /// from the end, the last kept ending in "…". The block's first line stands at <c>cy − 6 × lines + 9</c>, and a value alone at
    /// <c>cy + 4</c>, where 0.46.0 and 0.46.1 wrote them, so a cell without the 0.46.2 switches is drawn as before. The cell's name says all
    /// of it, so none of it is read.</summary>
    private static void CellBlock(SvgWriter w, ChartSpec s, ChartPoint p, double cx, double cy, double cw, double ch, string ink)
    {
        var parts = PartsOf(s, p, cw);
        if (parts.Rated && parts.Value is null) return;
        double room = cw - 6, tall = ch - 4;
        string? value = parts.Value, sub = parts.Sub;
        var note = parts.Note;
        var reason = parts.Said is { } said ? CellLines(said, room, int.MaxValue) : [];
        int Count() => (value is null ? 0 : 1) + (sub is null ? 0 : 1) + note.Count + reason.Count;
        bool Fits() => 12 * Count() + (value is null ? 0 : 1) <= tall;
        if (!Fits()) note = [];
        if (!Fits()) sub = null;
        if (!Fits()) value = null;
        if (!Fits() && parts.Said is { } cut) reason = CellLines(cut, room, (int)Math.Floor(tall / 12));
        if (Count() == 0 || !Fits()) return;
        var block = new List<(string Text, bool IsValue)>();
        if (parts.ReasonFirst) block.AddRange(reason.Select(line => (line, false)));
        if (value is not null) block.Add((value, true));
        if (sub is not null) block.Add((sub, false));
        block.AddRange(note.Select(line => (line, false)));
        if (!parts.ReasonFirst) block.AddRange(reason.Select(line => (line, false)));
        var first = block is [{ IsValue: true }] ? cy + 4 : cy - block.Count * 6 + 9;
        var valueInk = parts.Kept && Contrast.Ratio(w.Style.Muted, w.Style.Background) >= 4.5 ? w.Style.Muted : ink;
        const string unread = "pointer-events='none' aria-hidden='true'";
        for (var k = 0; k < block.Count; k++)
            w.Text(cx, first + 12 * k, block[k].Text, block[k].IsValue
                ? $"text-anchor='middle' font-size='11' font-weight='600' fill='{valueInk}' {unread}"
                : $"text-anchor='middle' font-size='10' fill='{ink}' {unread}");
    }
```

- [ ] **Step 4: Call it** in `Heatmap()`. Replace lines 3248-3252 with:

```csharp
                if (s.CellText) CellBlock(w, s, p, x + cw / 2, y + ch / 2, cw, ch, ink);
```

  Note the `Fits()` rule: a block needs `12 × lines`, plus 1 when it holds a value. That reproduces 0.46.0's `13 <= tall` for a value alone and `13 + 12 <= tall` for a value and sub-label. It also reproduces 0.46.1's `floor(tall / 12)` lines for a reason, with its sub-label only where a line is left. A rated cell writes its value only with its sub-label or alone, and nothing when the value is too wide, as `CellWords` did.

- [ ] **Step 5: Run the build and the tests, and see them pass.**
  - Run: `dotnet build Lumen.Charts.slnx -c Release` (expect 0 warnings), then `dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build`.
  - Expected: every new test PASSES, and **every 0.46.0 and 0.46.1 heatmap test passes unchanged**.
  - If an old cell-text test fails, `CellBlock` has diverged from `CellWords`/`CellReason`. Fix `CellBlock`; never the old test.
  - Exception: tests that set `FitHeight` are Task 3's. They still pass here, because `FittedHeight` is unchanged in this task.

- [ ] **Step 6: Run the baseline in both finishes** and compare with 0.46.1.

```bash
cd tests/Lumen.Charts.Baseline
dotnet run -c Release
dotnet run -c Release -- classic
diff <(tr -d '\r' < reference/refined.txt) <(tr -d '\r' < baseline.txt)
diff <(tr -d '\r' < reference/classic.txt) <(tr -d '\r' < classic.txt)
```

  Expected: no difference at all. This task must not move any rendering. Delete `baseline.txt` and `classic.txt` afterwards; they are not committed.

- [ ] **Step 7: Commit.**

```bash
git add src/Lumen.Charts/ChartSvg.cs tests/Lumen.Charts.Tests/Program.cs
git commit -m "Write a heatmap cell's words as one block: notes, a kept not-rated value, and the drop order"
```

---

### Task 3: Fitted rows fit their words, and the reference renderings

**Files:**
- Modify: `src/Lumen.Charts/ChartSvg.cs`:
  - the `HeatmapRow` doc (3094-3095);
  - the `FittedHeight` heatmap branch and its doc (1676-1684);
  - add `CellNeed` next to `CellBlock`.
- Modify: `tests/Lumen.Charts.Tests/Program.cs`:
  - the 0.46.1 tests whose fitted rows grow (at least 10245; the frozen-band pins 10427-10514 where their cells need more than 32 units);
  - new tests in the `// 0.46.2` block.
- Modify: `tests/Lumen.Charts.Baseline/Program.cs`: a `// 0.46.2` block after line 880.
- Modify: `tests/Lumen.Charts.Baseline/reference/refined.txt` and `reference/classic.txt`: regenerated.

**Interfaces:**
- Consumes: `PartsOf`, `CellParts`, `CellLines` (Task 2).
- Produces: `internal static double CellNeed(ChartSpec s, ChartPoint p, double cw)`.

- [ ] **Step 1: Write the failing tests.** Append to the `// 0.46.2` block.

```csharp
Test("Heatmap cells: FitHeight rows are as tall as the tallest cell's words, at least 36",()=>{
    // Six rows of rated values alone keep 36: 80 + 6 x 36 + 56 = 352, as in 0.46.1.
    var six=HeatGrid(s=>s with{FitHeight=true,Series=Enumerable.Range(0,6).Select(i=>new ChartSeries($"Row {i}",[new ChartPoint(0,i+1,"2025"),new ChartPoint(1,i+2,"2026")])).ToArray()});
    Check(ChartSvg.Render(six).Contains("viewBox='0 0 600 352'"),"36 a row");
    // A not-rated cell at 90 needs its two reason lines and its sub-label: 4 + 3 x 12 = 40, so 80 + 6 x 40 + 56 = 376, and "/4 starts" is written.
    var reasons=ChartSvg.Render(Six(HeatGrid(s=>s with{CellWidth=90,FitHeight=true})));
    Check(reasons.Contains("viewBox='0 0 345 376'")&&reasons.Contains("height='38'"),"40 a row: "+Regex.Match(reasons,"viewBox='[^']*'").Value);
    Check(Regex.IsMatch(reasons,"font-size='10'[^>]*>/4 starts<"),"the sub-label shows under the reason");
    // Both switches with a two-line note on the not-rated cell: 4 + 13 + 5 x 12 = 77, so 80 + 6 x 77 + 56 = 598.
    var full=Six(Notes(HeatGrid(s=>s with{CellWidth=90,FitHeight=true,CellNotes=true,NotRatedKeepsValue=true}),distance:" · 14 pts, 6 riders"));
    var drawn=ChartSvg.Render(full);
    Check(drawn.Contains("viewBox='0 0 345 598'"),"77 a row: "+Regex.Match(drawn,"viewBox='[^']*'").Value);
    Check(WordsAt(drawn,175).Skip(4).Take(6).SequenceEqual(["1.2","/4 starts","14 pts,","6 riders","too few starts","to rate"]),string.Join(" | ",WordsAt(drawn,175)));
    // The frame, the row names and the frozen band follow the fitted row.
    var frame=ChartSvg.HeatmapFrame(full);
    Check(frame.Top==80&&frame.Bottom==80+6*77,"frame "+frame);
    Check(drawn.Contains($"<text x='118' y='{(80+1.5*77+4).ToString(CultureInfo.InvariantCulture)}'"),"the second row's name in the middle of its row");
    var band=ChartSvg.HeatmapNameBand(full)!.Value;
    Check(band.Top==80&&band.Height==6*77+24,"band "+band.Top+" "+band.Height);
    // Two rows stay at the 240 floor, where they share its height as before.
    Check(ChartSvg.Render(HeatGrid(s=>s with{FitHeight=true})).Contains("viewBox='0 0 600 240'"),"the floor");
});
Test("Heatmap cells: fitted rows never hide a word, with both switches and any note, at every width",()=>{
    foreach(var width in new double[]{24,48,72,90,120})
    {
        var spec=Six(Notes(HeatGrid(s=>s with{CellWidth=width,FitHeight=true,CellNotes=true,NotRatedKeepsValue=true}),distance:" · 1840 pts, 12 athletes, 153 pts a rider"));
        var lines=WordsAt(ChartSvg.Render(spec),130+width/2);
        // With room for every line, nothing is given up for height: no ellipsis but where a single word is wider than the cell.
        Check(lines.All(l=>!l.EndsWith("…")||!l.Contains(' ')),$"{width}: {string.Join(" | ",lines)}");
        Check(width<72||lines.Contains("/4 starts")&&lines.Contains("/12 starts"),$"{width}: the sub-labels: {string.Join(" | ",lines)}");
    }
});
```

- [ ] **Step 2: Run the tests and see them fail.**
  - Run: `dotnet build Lumen.Charts.slnx -c Release && dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build`.
  - Expected: the two new tests FAIL. For example: `40 a row: viewBox='0 0 345 352'`.

- [ ] **Step 3: Measure a cell.** Add `CellNeed` after `CellBlock` in `ChartSvg.cs`:

```csharp
    /// <summary>The height a heatmap cell's words take with every line kept, as <see cref="CellBlock"/> writes them in a cell tall enough: 12
    /// units a line and 1 more where it writes a value; 0 where it writes nothing, without <see cref="ChartSpec.CellText"/> or where a
    /// rated cell's value is wider than the cell. <see cref="FittedHeight"/> sizes a heatmap's rows by its tallest cell (0.46.2).</summary>
    internal static double CellNeed(ChartSpec s, ChartPoint p, double cw)
    {
        if (!s.CellText) return 0;
        var parts = PartsOf(s, p, cw);
        if (parts.Rated && parts.Value is null) return 0;
        var reason = parts.Said is { } said ? CellLines(said, cw - 6, int.MaxValue).Count : 0;
        var lines = (parts.Value is null ? 0 : 1) + (parts.Sub is null ? 0 : 1) + parts.Note.Count + reason;
        return 12 * lines + (parts.Value is null ? 0 : 1);
    }
```

- [ ] **Step 4: Fit the rows.** In `FittedHeight`, replace the heatmap branch (1683-1684) with the code below.

```csharp
        if (s.Kind == ChartKind.Heatmap)
        {
            // Every row is as tall as the tallest cell's words, at the cells' width as Heatmap draws them, and at least HeatmapRow.
            var cats = HeatmapColumns(s);
            var cw = cats.Length == 0 ? 0 : s.CellWidth ?? (s.Width - HeatmapLeftOf(s) - 35) / cats.Length;
            var row = Math.Max(HeatmapRow, s.Series.SelectMany(series => series.Points).Select(p => CellNeed(s, p, cw) + 4).DefaultIfEmpty(0).Max());
            return Math.Max(240, (int)Math.Ceiling(80 + Headroom(s) + s.Series.Count * row + HeatmapBelow(s) + 14 * Math.Max(0, Wrap(s.Source, s.Width - 48).Length - 1)));
        }
```

  Then update two doc comments:
  - **`FittedHeight`:** `A heatmap is drawn at the height of its rows, each as tall as its tallest cell's written words need (CellNeed, plus 4), at least HeatmapRow, with HeatmapBelow under them, and no less than 240 (0.46.1; rows fitted to their words from 0.46.2).`
  - **`HeatmapRow`:** `The least height ChartSpec.FitHeight gives each of a heatmap's rows (0.46.1); a row grows past it to hold its tallest cell's words (0.46.2).`

  Every term is a whole number, so `ch` in `Heatmap()` and `HeatmapFrame` is exactly `row`, and the names and the frozen band follow it.

- [ ] **Step 5: Update the 0.46.1 tests whose fitted rows now grow.** Run the suite and read every failure. For each, check that the new number follows from `CellNeed`, then update the expectation with a comment that says why.
  - **Line 10244-10245, the "seventy" case:** at 72 the reason now has room for three lines and its sub-label (52 a row), so it is no longer cut. Keep the cut test by fixing the height to 0.46.1's 36-unit rows without `FitHeight`:
    `var seventy=ChartSvg.Render(Six(HeatGrid(s=>s with{CellWidth=72,Height=376})));`, with the comment `// 36-unit rows, set by the height: a line holds 66 …`.
  - **Line 10239, "ninety":** add `Check(Regex.IsMatch(ninety,"font-size='10'[^>]*>/4 starts<"),"rows of 40 now hold the sub-label under the reason");`.
  - **The frozen-band pins (10427-10514)** and any other `FitHeight` pin over a heatmap whose not-rated or gap cells hold a two-line reason plus a sub-label: their rows go from 36 to 40.
  - Never loosen a check to make it pass.

- [ ] **Step 6: Add the reference rows.** In `tests/Lumen.Charts.Baseline/Program.cs`, after line 880:

```csharp
// 0.46.2: heatmap cells show it all, on the same invented table with a points note in every cell: notes drawn with rows fitted to them,
// notes in the fixed height that gives them up first, not-rated cells keeping their value, and both switches on the Dark preset.
var noted = table with { Series = table.Series.Select((row, r) => row with { Points = row.Points.Select((p, c) => p with { ValueNote = $" · {12 + r * 7 + c * 3} pts, {3 + (r + c) % 4} riders" }).ToArray() }).ToArray() };
lines.Add($"heatmap-cells/notes {Hash(Render(noted with { CellWidth = 90, FitHeight = true, CellNotes = true }))}");
lines.Add($"heatmap-cells/notes-fixed {Hash(Render(noted with { CellWidth = 90, CellNotes = true }))}");
lines.Add($"heatmap-cells/keeps-value {Hash(Render(noted with { CellWidth = 90, FitHeight = true, NotRatedKeepsValue = true }))}");
lines.Add($"heatmap-cells/both-dark {Hash(Render(CategoryGrid(ChartTheme.Dark, null, 4, 90) with { FitHeight = true, CellNotes = true, NotRatedKeepsValue = true, Series = noted.Series }))}");
```

- [ ] **Step 7: Run the baseline in both finishes and compare.** Commands as in Task 2, Step 6.
  - Expected: 402 rows per finish, 398 + 4 added.
  - **Changed by design**, in both finishes: `heatmap-table/fit` and `heatmap-table/reasons`. Their not-rated cells hold a two-line reason plus "/4 starts" and "/2 starts", so their rows grow to 40.
  - **Every other one of the 398 rows:** identical.
  - If any other row moved, stop and report it: it is a defect.
  - Then copy `baseline.txt` over `reference/refined.txt`, and `classic.txt` over `reference/classic.txt`. Delete the two run files.

- [ ] **Step 8: Look at the drawings.**
  - Write the SVGs with `dotnet run -c Release -- svg-out %TEMP%\lumen0462\svg`. Open `heatmap-cells/notes`, `keeps-value` and `both-dark` (`names.txt` gives each one's index), and check by eye:
    - nothing overlaps;
    - the block is centred;
    - the muted value reads.
  - List the files in your report. Do not commit them.

- [ ] **Step 9: Run the build and the tests, and see them pass.**
  - Run: `dotnet build Lumen.Charts.slnx -c Release` (expect 0 warnings) and `dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build`.
  - Expected: `0 failed`. Gallery tests that pin the Category heatmap card's drawing may fail here, since its rows grow too. If so, leave them to Task 4 and list them in your report; Task 4 owns the card.

- [ ] **Step 10: Commit.**

```bash
git add src/Lumen.Charts/ChartSvg.cs tests/Lumen.Charts.Tests/Program.cs tests/Lumen.Charts.Baseline/Program.cs tests/Lumen.Charts.Baseline/reference/refined.txt tests/Lumen.Charts.Baseline/reference/classic.txt
git commit -m "Fit a heatmap's rows to its tallest cell's words, and record 0.46.2's reference renderings"
```

---

### Task 4: The gallery card and its unit, HTTP and browser checks

**Files:**
- Modify: `samples/Lumen.Gallery/SportsData.cs`:
  - the `categories` spec (~1096-1107) sets `CellNotes = true, NotRatedKeepsValue = true`;
  - update the comment above it (~1090-1095) and the card's note text at ~1176, which says the note goes only in the name and the grid table.
- Modify: `tests/Lumen.Charts.Tests/Program.cs`: the Category heatmap gallery test (7598-7632).
- Modify: `tests/verify-api.ps1`: the category heatmap prerender check (line 206).
- Modify: `tests/Lumen.Charts.BrowserTests/Program.cs`: the Category heatmap tests (1715-1890).

**Interfaces:**
- Consumes: `ChartSpec.CellNotes`, `ChartSpec.NotRatedKeepsValue`, and the drawing from Tasks 2–3.

- [ ] **Step 1: Set the switches on the card.** In `SportsData.cs`, add `CellNotes = true, NotRatedKeepsValue = true` to the `categories` heatmap's initializer, next to `FitHeight = true`. Reword the comment and the card's note so they say each cell writes its value, its starts and its points, and a thin cell keeps its value, muted, above its reason. Keep the backtick style the note already uses.

- [ ] **Step 2: Work out the drawing, and write the numbers into the test before running it.** The invented data is `CategoryHistory` (SportsData.cs 131-137); each note is `" · {points} pts, {riders} riders"`. At `CellWidth` 90 a 10 px line holds 84.
  - **Note lines:** `CellNote` drops the `" · "`. "34 pts, 5 riders" (86.4) takes two lines, "34 pts," and "5 riders". "8 pts, 3 riders" (80.2) takes one. Compute each of the 15 cells that have a value with `ChartSvg.Wide(text)*10/11`. The gap cell has no note.
  - **The rows:** the tallest cell is the 2025 relay (not rated: value, "/9 starts", "14 pts," / "6 riders", "too few starts" / "to rate") at `4 + 13 + 5 × 12 = 77`. So the drawing is `80 + 4 × 77 + 54 = 442` tall: labels on top, so 80 − 26 below, and the source line kept. It stays 580 wide.
  - **Text counts:**
    - 11 px values: 13 rated plus the 2 kept, muted = 15.
    - 10 px lines starting with `/`: 16, since every cell now keeps its starts (13 + 2 + 1).
    - Reason lines: 4. "did not race": 1.
    - Note lines: their sum from your computation.
  - Run `dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build` once the gallery builds, and confirm each count by reading the SVG, not by copying what the test prints.

- [ ] **Step 3: Update the unit test** "Sports page: the Category heatmap closes the Racing section…" (7600-7632):
  - The card check (7602) adds `CellNotes:true,NotRatedKeepsValue:true` to the property pattern.
  - The comment (7616-7619) describes the new drawing.
  - The `viewBox` check becomes `"0 0 580 442"`.
  - Line 7623-7624 checks the counts from Step 2. Add a check that the two kept values are written in Light's `Muted` colour, and one that no note line contains `·`.

- [ ] **Step 4: Update the HTTP check.** In `tests/verify-api.ps1` line 206, keep everything that is checked today and add:
  - the prerendered card contains `>34 pts,<` and `>5 riders<`, the Sprint 2023 cell's note on two lines;
  - it contains `font-weight='600' fill='#63718A'`, the Light preset's muted colour on a kept value. Confirm that colour against `ChartStyle.Light.Muted` in `src/Lumen.Charts/ChartStyle.cs` first, and use whatever it is.

  Name it: "…and each cell writes its points under its starts, a thin cell keeping its value, muted, above its reason".

- [ ] **Step 5: Update the browser tests** in `tests/Lumen.Charts.BrowserTests/Program.cs`.
  - **Line 1741:** expect the Step 2 counts (15 values, 16 starts, and the non-`/` 10 px lines: 4 reason + 1 "did not race" + the note lines).
  - **In the 375-pixel phone test (1785),** after the cells scroll, add:
    - **The rows:** every frozen name's `y` equals its row's middle plus 4. Read the names from `.lumen-freeze svg text` and the cells from `g.lumen-datum > rect` (y + height / 2 + 4, ± 0.5).
    - **The muted value:** each kept value's computed `fill` clears 4.5:1 against the card background. Read `getComputedStyle(text).fill` and the drawing's background, and compute the WCAG ratio in the page with the same formula as `Contrast.Ratio`.
    - **The width:** no `text[aria-hidden="true"]` inside a cell is wider than its cell less 6. Use `getBBox().width` against its cell rect's width, + 0.5 tolerance.
  - **Themes:** run the muted-value contrast check in light, dark and Midnight. The suite already switches themes for its axe sweeps; reuse that switch, as the other per-theme checks do. The existing axe sweeps cover the card in all three.

- [ ] **Step 6: Build and run everything this task touches.**
  - Stop the gallery by name (`taskkill //IM Lumen.Gallery.exe //F`). Build: `dotnet build Lumen.Charts.slnx -c Release` and `dotnet build tests/Lumen.Charts.BrowserTests -c Release`, which is outside the solution.
  - Run: `dotnet run --project tests/Lumen.Charts.Tests -c Release --no-build`. Expected: 0 failed.
  - Start the gallery: `dotnet run --project samples/Lumen.Gallery -c Release --no-build --urls http://localhost:5188`, in the background.
  - Run: `pwsh -File tests/verify-api.ps1 -BaseUrl http://localhost:5188`. Expected: every check passes; the count stays 300.
  - Run: `dotnet run --project tests/Lumen.Charts.BrowserTests -c Release --no-build`. Expected: every check passes.
  - **Known flakes:** the Midnight axe sweep's `Hovered()` NullReferenceException, and the 1280 tab-stop test on a freshly started gallery. Re-run once and note it.
  - Take a 375 px screenshot of the card with Playwright (touch, scale 2) into `%TEMP%\lumen0462\card-375.png`, and a 1400 px one.
  - Stop the gallery: `taskkill //IM Lumen.Gallery.exe //F`.
  - The WebAssembly host has no Sports page and no component change, so it is not run here; Task 5 and the release verification run it.

- [ ] **Step 7: Commit.**

```bash
git add samples/Lumen.Gallery/SportsData.cs tests/Lumen.Charts.Tests/Program.cs tests/verify-api.ps1 tests/Lumen.Charts.BrowserTests/Program.cs
git commit -m "Gallery: the Category heatmap draws every cell's points and keeps its thin cells' values"
```

---

### Task 5: Docs, recipe and version

**Files:**
- Modify: `README.md`:
  - "### Heatmap tables" (526-587): 550 (words in the cells), 570 (what changes), 575 ("never drawn"), 577 (the reason in the cell), 581 (a height fitted to the rows), 586 (limits);
  - "## Supported behavior and limits" (~1267-1268);
  - a new "## 0.46.2 additions" before "## 0.46.1 additions" (1273).
- Modify: `integrations/claude-code/lumen-charts/references/api.md`:
  - the spec-table rows (68-71), adding `CellNotes` and `NotRatedKeepsValue` rows after `ColumnLabelsOnTop`;
  - the ChartPoint paragraph (105);
  - the heatmap paragraph (147).
- Modify: `integrations/claude-code/lumen-charts/SKILL.md` (93, 96-97, 125): body text only; leave the frontmatter description untouched.
- Modify: `integrations/claude-code/lumen-charts/references/recipes-race-face.md`:
  - the header's "compile against Lumen.Charts 0.46.1" (~3) becomes 0.46.2;
  - the "Category heatmap" recipe (702-750) sets both switches;
  - its bullets 737 ("ValueNote is never drawn"), 739, 742 (36 units a row).
- Modify: `docs/VERIFICATION.md`: a 0.46.2 entry above the 0.46.1 one (line 7), with the counts you measured.
- Modify: `tests/Lumen.Charts.Baseline/README.md` (12): v0.46.2's reference rows.
- Modify: `Directory.Build.props:7`: `<Version>0.46.2</Version>`.

- [ ] **Step 1: README.** In "### Heatmap tables":
  - **Words in the cells (550):** with `CellNotes` each cell writes its note under its sub-label. Say how it is trimmed and wrapped, with `" · 1840 pts, 12 athletes"` → `1840 pts,` / `12 athletes` at 90. The note is no longer "never written in a cell" when the switch is on.
  - **The reason in the cell (577):** with `NotRatedKeepsValue` the value stays, muted, with the reason last.
  - **A height fitted to the rows (581):** rows are as tall as the tallest cell's words need, at least 36.
  - **What changes for an existing heatmap (564-573):** add the spec's §3 item.
  - **Limits (586):** "rows of 36 units" becomes "rows at least 36 units, all one height". Add the spec's accepted limits.
  - **"## 0.46.2 additions":** the two switches and the fitted rows, in the 0.46.1 additions' format. Give the baseline rows: 398 reproduced except `heatmap-table/fit` and `heatmap-table/reasons`, four added, 402 in all.

- [ ] **Step 2: The skill's references and SKILL.md**, as listed under Files.
  - Every claim must match the spec and the code: 12 units a line, 13 for a value, plus 4, at least 36; notes break after commas first; the muted value only where it clears 4.5:1.
  - Check the frontmatter description is byte-for-byte unchanged: `git diff integrations/claude-code/lumen-charts/SKILL.md` must show no change above the closing `---`.

- [ ] **Step 3: The recipe.**
  - In the "Category heatmap" recipe's code, add `CellNotes = true, NotRatedKeepsValue = true` beside `FitHeight = true`.
  - Rewrite its bullets "Each cell", "Not rated" and "Width and height" to match.
  - Its `ValueNote` (726) is longer: `" · {Points} pts, {Riders} riders, {pts} pts a rider"`. At `CellWidth` 90 it takes three lines, and the "Width and height" bullet says so.
  - Compile it with every other recipe:

```bash
cd tests/Lumen.Charts.Recipes
python check.py
dotnet run -c Release
```

  Expected: both succeed, and the recipe count stays 39.

- [ ] **Step 4: VERIFICATION.md and the baseline README.**
  - In `docs/VERIFICATION.md`, add a 0.46.2 entry in the 0.46.1 entry's format: unit count, HTTP 300, browser counts, 402 hashes per finish (two changed by design, four added), 39 recipes. Use the numbers you measure in Step 6, never estimates.
  - In the baseline README, add 0.46.2's sentence at the front of its history: "`reference/refined.txt` and `reference/classic.txt` are the hashes of **v0.46.2**, 402 rows: v0.46.1's 398 rows, 396 reproduced exactly in both finishes and two changed by design (`heatmap-table/fit` and `heatmap-table/reasons`, whose fitted rows now hold their not-rated cells' sub-label under the reason), with 0.46.2's four `heatmap-cells/*` rows added." Then keep the existing v0.46.1 sentence as history ("v0.46.1 recorded 398 rows: …").

- [ ] **Step 5: Version.** In `Directory.Build.props`, `<Version>0.46.1</Version>` becomes `<Version>0.46.2</Version>`.

- [ ] **Step 6: Full verification.**
  - `taskkill //IM Lumen.Gallery.exe //F`.
  - `dotnet build Lumen.Charts.slnx -c Release`: expect 0 warnings.
  - Run the unit tests.
  - Run the baseline in both finishes against the new reference: no difference.
  - Run the gallery, `verify-api.ps1` and the browser suite as in Task 4, Step 6.
  - Build the WebAssembly host: `dotnet build samples/Lumen.Wasm -c Release`. Run it on 5199 (`dotnet run --project samples/Lumen.Wasm -c Release --no-build --urls http://localhost:5199`), run the browser suite with the argument `http://localhost:5199`, then stop it by PID (`netstat -ano`, the PID listening on 5199).
  - Write every count into VERIFICATION.md.

- [ ] **Step 7: Commit.**

```bash
git add README.md integrations/claude-code/lumen-charts/references/api.md integrations/claude-code/lumen-charts/SKILL.md integrations/claude-code/lumen-charts/references/recipes-race-face.md docs/VERIFICATION.md tests/Lumen.Charts.Baseline/README.md Directory.Build.props
git commit -m "Document 0.46.2: notes in heatmap cells, kept not-rated values, rows fitted to their words"
```
