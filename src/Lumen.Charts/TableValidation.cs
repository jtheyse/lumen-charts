using System.Globalization;

namespace Lumen.Charts;

/// <summary>The checks a table spec passes before it is drawn or exported, in the voice of <see cref="ChartValidation"/>.</summary>
internal static class TableValidation
{
    internal static void Spec(TableSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Columns is null || spec.Columns.Count is 0 or > 32) throw new ArgumentException("A table needs between 1 and 32 columns.");
        if (spec.Rows is null) throw new ArgumentException("A table needs rows, which may be none.");
        if (spec.Rows.Count > 100_000) throw new ArgumentException("A table holds at most 100,000 rows.");
        if (spec.PageSize is null && spec.Rows.Count > 5_000)
            throw new ArgumentException("A table without PageSize writes every row, so it takes at most 5,000; set PageSize (10 to 500) to page a longer one.");
        if (string.IsNullOrWhiteSpace(spec.Caption) || spec.Caption.Length > 120)
            throw new ArgumentException("A caption is required and may be at most 120 characters.");
        if (spec.Description is { Length: > 300 }) throw new ArgumentException("A description may be at most 300 characters.");
        if (spec.PageSize is { } size && size is < 10 or > 500) throw new ArgumentException("A page size may be left out, or be from 10 to 500.");
        if (!Enum.IsDefined(spec.Density)) throw new ArgumentException("Unknown table density.");
        ChartValidation.Style(spec.Style);

        var headers = 0;
        for (var c = 0; c < spec.Columns.Count; c++)
        {
            Column(spec.Columns[c], c + 1);
            if (spec.Columns[c].RowHeader) headers++;
        }
        if (headers > 1) throw new ArgumentException("At most one column may be the row header.");

        SortColumn(spec, spec.SortBy);

        for (var r = 0; r < spec.Rows.Count; r++)
        {
            var row = spec.Rows[r];
            if (row?.Cells is null) throw new ArgumentException($"Row {r + 1} cannot be null.");
            Cells(spec, row.Cells, $"Row {r + 1}", footer: false);
        }
        if (spec.Footer is { } footerCells) Cells(spec, footerCells, "The footer", footer: true);
    }

    /// <summary>A sort column must exist, be sortable and not be a sparkline.</summary>
    internal static void SortColumn(TableSpec spec, int? sortBy)
    {
        if (sortBy is not { } index) return;
        if (index < 0 || index >= spec.Columns.Count) throw new ArgumentException($"SortBy {index} is not a column; the columns are 0 to {spec.Columns.Count - 1}.");
        var column = spec.Columns[index];
        if (!column.Sortable || column.Kind == ColumnKind.Sparkline)
            throw new ArgumentException($"Column {index + 1} ({column.Header}) cannot be sorted: it is not sortable or is a sparkline.");
    }

    private static void Column(TableColumn column, int number)
    {
        var at = $"Column {number}: ";
        if (column is null) throw new ArgumentException($"Column {number} cannot be null.");
        if (string.IsNullOrWhiteSpace(column.Header) || column.Header.Length > 60)
            throw new ArgumentException(at + "a header is required and may be at most 60 characters.");
        if (!Enum.IsDefined(column.Kind)) throw new ArgumentException(at + "unknown column kind.");
        if (!Enum.IsDefined(column.Format)) throw new ArgumentException(at + "unknown value format.");
        if (!Enum.IsDefined(column.Align)) throw new ArgumentException(at + "unknown alignment.");
        if (!Enum.IsDefined(column.Better)) throw new ArgumentException(at + "unknown Better.");
        if (column.BarMax is { } max && (!ChartValidation.Finite(max) || max <= 0))
            throw new ArgumentException(at + "BarMax must be finite and greater than 0.");
        if (column.DateFormat is { } format)
        {
            if (format.Length > 40) throw new ArgumentException(at + "a date format may be at most 40 characters.");
            try { new DateOnly(2026, 1, 31).ToString(format, CultureInfo.InvariantCulture); }
            catch (FormatException) { throw new ArgumentException(at + "the date format is not a valid .NET date format."); }
        }
    }

    private static void Cells(TableSpec spec, IReadOnlyList<TableCell> cells, string where, bool footer)
    {
        if (cells.Count != spec.Columns.Count)
            throw new ArgumentException($"{where} has {cells.Count} cells but the table has {spec.Columns.Count} columns; each needs one cell for each column.");
        for (var c = 0; c < cells.Count; c++)
            Cell(spec.Columns[c], cells[c], $"{where}, column {c + 1} ({spec.Columns[c].Header}): ", footer);
    }

    private static void Cell(TableColumn column, TableCell cell, string at, bool footer)
    {
        if (cell is null) throw new ArgumentException(at + "a cell cannot be null; use an empty cell for a missing value.");
        var kind = column.Kind;
        var text = kind == ColumnKind.Text || footer;
        var number = kind is ColumnKind.Number or ColumnKind.Bar or ColumnKind.Change;
        if (cell.Text is not null && !text) throw new ArgumentException(at + $"a {kind} column does not take Text.");
        if (cell.Number is not null && !number) throw new ArgumentException(at + $"a {kind} column does not take Number.");
        if (cell.Previous is not null && kind != ColumnKind.Change) throw new ArgumentException(at + $"a {kind} column does not take Previous.");
        if (cell.Date is not null && kind != ColumnKind.Date) throw new ArgumentException(at + $"a {kind} column does not take Date.");
        if (cell.Trend is not null && kind != ColumnKind.Sparkline) throw new ArgumentException(at + $"a {kind} column does not take Trend.");

        if (cell.Text is { Length: > 200 }) throw new ArgumentException(at + "text may be at most 200 characters.");
        if (cell.Note is { Length: > 80 }) throw new ArgumentException(at + "a note may be at most 80 characters.");
        if (cell.Number is { } n && !ChartValidation.Finite(n)) throw new ArgumentException(at + "a number must be finite.");
        if (cell.Previous is { } p && !ChartValidation.Finite(p)) throw new ArgumentException(at + "a previous value must be finite.");
        if (cell.Trend is { } trend)
        {
            if (trend.Count > 400) throw new ArgumentException(at + "a trend may hold at most 400 values.");
            if (trend.Any(v => v is { } x && !ChartValidation.Finite(x))) throw new ArgumentException(at + "trend values must be finite.");
            if (trend.Count(v => v is not null) < 2) throw new ArgumentException(at + "a trend needs at least 2 values that are not null.");
        }
        if (cell.Href is not null && !LumenHtml.SafeLink(cell.Href))
            throw new ArgumentException(at + "a link may be an http, https or mailto address or a relative one.");
    }
}
