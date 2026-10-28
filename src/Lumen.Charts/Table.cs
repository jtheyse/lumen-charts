namespace Lumen.Charts;

/// <summary>What a table column holds, which decides which field of each cell is read and how it is written and sorted.</summary>
public enum TableColumnKind
{
    /// <summary>Words, read from <see cref="TableCell.Text"/>. This is the default.</summary>
    Text,
    /// <summary>A number, read from <see cref="TableCell.Number"/> and written in the column's format and unit.</summary>
    Number,
    /// <summary>A calendar date, read from <see cref="TableCell.Date"/>.</summary>
    Date,
    /// <summary>A change, from <see cref="TableCell.Previous"/> to <see cref="TableCell.Number"/>, written in words and an arrow.</summary>
    Change,
    /// <summary>A number drawn as a bar as well as written, read from <see cref="TableCell.Number"/>.</summary>
    Bar,
    /// <summary>A small trend line, read from <see cref="TableCell.Trend"/>. It cannot be sorted.</summary>
    Sparkline
}

/// <summary>How a column's cells line up.</summary>
public enum TableColumnAlign
{
    /// <summary>Right for <see cref="TableColumnKind.Number"/>, <see cref="TableColumnKind.Change"/> and <see cref="TableColumnKind.Bar"/>, left for the rest. This is the default.</summary>
    Auto,
    /// <summary>Against the left edge.</summary>
    Left,
    /// <summary>Against the right edge.</summary>
    Right,
    /// <summary>In the middle.</summary>
    Center
}

/// <summary>One column of a table: its header and what its cells hold.</summary>
/// <param name="Header">The column's name, 1 to 60 characters.</param>
public sealed record TableColumn(string Header)
{
    /// <summary>What the column holds. The default is text.</summary>
    public TableColumnKind Kind { get; init; } = TableColumnKind.Text;
    /// <summary>How numbers, and the two ends of a change, are written. The default is plain numbers.</summary>
    public ValueFormat Format { get; init; } = ValueFormat.Number;
    /// <summary>A unit written after each number, as an axis writes it.</summary>
    public string? Unit { get; init; }
    /// <summary>A .NET date format for a <see cref="TableColumnKind.Date"/> column, at most 40 characters. The default is <c>d MMM yyyy</c>.</summary>
    public string? DateFormat { get; init; }
    /// <summary>How the cells line up. The default is automatic.</summary>
    public TableColumnAlign Align { get; init; }
    /// <summary>Whether the reader may sort by this column. The default is true; a sparkline column is never sorted.</summary>
    public bool Sortable { get; init; } = true;
    /// <summary>Which direction of change is an improvement; it also decides which way a first sort of the column runs.</summary>
    public Better Better { get; init; } = Better.Neither;
    /// <summary>The value a bar column's bars end at; it must be greater than 0. The default is the largest value in the column.</summary>
    public double? BarMax { get; init; }
    /// <summary>Marks the column whose cells name their row. At most one column is marked; with none marked, the first is the row header.</summary>
    public bool RowHeader { get; init; }
}

/// <summary>One cell of a table. Which fields it uses follows its column's <see cref="TableColumn.Kind"/>; a field left null is a missing value.</summary>
public sealed record TableCell
{
    /// <summary>The words of a text cell, at most 200 characters. A footer cell may hold text in any column.</summary>
    public string? Text { get; init; }
    /// <summary>The number of a number, bar or change cell.</summary>
    public double? Number { get; init; }
    /// <summary>The value a change cell is compared with.</summary>
    public double? Previous { get; init; }
    /// <summary>The date of a date cell.</summary>
    public DateOnly? Date { get; init; }
    /// <summary>The values of a sparkline cell: at most 400, at least 2 not null.</summary>
    public IReadOnlyList<double?>? Trend { get; init; }
    /// <summary>A muted second line under the cell, at most 80 characters.</summary>
    public string? Note { get; init; }
    /// <summary>A link for the cell: an http, https or mailto address or a relative one.</summary>
    public string? Href { get; init; }

    /// <summary>A text cell.</summary>
    public static TableCell Of(string text) => new() { Text = text };
    /// <summary>A number cell, missing when <paramref name="number"/> is null.</summary>
    public static TableCell Of(double? number) => new() { Number = number };
    /// <summary>A date cell, missing when <paramref name="date"/> is null.</summary>
    public static TableCell Of(DateOnly? date) => new() { Date = date };
}

/// <summary>One row of a table.</summary>
/// <param name="Cells">One cell for each column, matched by position.</param>
public sealed record TableRow(IReadOnlyList<TableCell> Cells);

/// <summary>A read-only table: typed columns, rows, and how it is sorted and paged.</summary>
/// <param name="Columns">From 1 to 32 columns.</param>
/// <param name="Rows">At most 100,000 rows; without <see cref="PageSize"/>, at most 5,000.</param>
public sealed record TableSpec(IReadOnlyList<TableColumn> Columns, IReadOnlyList<TableRow> Rows)
{
    /// <summary>What the table is, 1 to 120 characters. It names the table and its scrolling region.</summary>
    public required string Caption { get; init; }
    /// <summary>A paragraph shown above the table, at most 300 characters.</summary>
    public string? Description { get; init; }
    /// <summary>The index of the column the table opens sorted by; the spec's row order is kept when it is null.</summary>
    public int? SortBy { get; init; }
    /// <summary>Whether the opening sort runs from the largest down.</summary>
    public bool SortDescending { get; init; }
    /// <summary>Rows to a page, from 10 to 500; null writes every row.</summary>
    public int? PageSize { get; init; }
    /// <summary>Opens on the last page, for a log whose newest rows come last.</summary>
    public bool StartAtEnd { get; init; }
    /// <summary>A totals row: one cell for each column, text in any column or the column kind's own field.</summary>
    public IReadOnlyList<TableCell>? Footer { get; init; }
    /// <summary>How closely the table is packed. The default is regular.</summary>
    public Density Density { get; init; }
    /// <summary>The look of the table; the chart default when null.</summary>
    public ChartStyle? Style { get; init; }
}

/// <summary>The body of <c>POST table/html</c>: a table and the page to write (the page it opens on when null).</summary>
/// <param name="Spec">The table.</param>
/// <param name="Page">The zero-based page; null for the page the table opens on.</param>
public sealed record TableRequest(TableSpec Spec, int? Page = null);
