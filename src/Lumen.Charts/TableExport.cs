using System.Globalization;
using System.Text;

namespace Lumen.Charts;

/// <summary>A table's data in a form other programs read.</summary>
public static class TableExport
{
    /// <summary>Every row, on every page, in the spec's sort, with the headers first and the footer last when there is one. Numbers are
    /// written in invariant round-trip form, dates as <c>yyyy-MM-dd</c>, a sparkline as its values joined by <c>;</c>, and a missing
    /// value as an empty field. Text that starts with <c>=</c>, <c>+</c>, <c>-</c> or <c>@</c> is prefixed with <c>'</c> so a spreadsheet does not run it, as <see cref="ChartExport.Csv"/> does; numbers and dates are not. A field holding a comma, a quote or a line break is quoted, with its quotes doubled. Lines end in CRLF.</summary>
    /// <exception cref="ArgumentException">The spec is not valid.</exception>
    public static string Csv(TableSpec spec)
    {
        var view = TableView.Of(spec, null, false);
        var result = new StringBuilder();
        Line(result, spec.Columns.Select(c => Guard(c.Header)));
        foreach (var i in view.Order) Line(result, spec.Columns.Select((c, k) => Field(c, spec.Rows[i].Cells[k])));
        if (spec.Footer is { } footer) Line(result, spec.Columns.Select((c, k) => Field(c, footer[k])));
        return result.ToString();
    }

    private static void Line(StringBuilder result, IEnumerable<string> fields) =>
        result.Append(string.Join(",", fields.Select(Quote))).Append("\r\n");

    private static string Field(TableColumn column, TableCell cell)
    {
        if (cell.Text is not null) return Guard(cell.Text);
        return column.Kind switch
        {
            ColumnKind.Number or ColumnKind.Bar or ColumnKind.Change => Number(cell.Number),
            ColumnKind.Date => cell.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            ColumnKind.Sparkline => cell.Trend is { } trend ? string.Join(";", trend.Select(Number)) : "",
            _ => ""
        };
    }

    // Text that a spreadsheet would read as a formula is prefixed with a quote, as ChartExport.Csv does; numbers and dates stay bare.
    private static string Guard(string text) =>
        text.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' ? "'" + text : text;

    private static string Number(double? value) => value?.ToString("R", CultureInfo.InvariantCulture) ?? "";

    private static string Quote(string field) =>
        field.IndexOfAny([',', '"', '\r', '\n']) < 0 ? field : "\"" + field.Replace("\"", "\"\"") + "\"";
}
