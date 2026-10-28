using System.Globalization;
using System.Text;

namespace Lumen.Charts;

/// <summary>Writes a data table as an HTML fragment: the same markup in a prerender, a circuit, a static page or an HTTP response.</summary>
public static class TableHtml
{
    private const string MissingMarkup = "<span aria-hidden='true'>—</span><span class='lumen-sr'> none</span>";

    /// <summary>The fragment for one page of <paramref name="spec"/> in the spec's sort. A null <paramref name="page"/> is the page the table
    /// opens on: the first, or the last with <see cref="TableSpec.StartAtEnd"/>. The fragment holds its own style block, scoped under
    /// <c>.lumen-datatable</c>, and no <c>id</c> attribute. Throws <see cref="ArgumentException"/> for a spec outside its limits or a page
    /// that does not exist.</summary>
    public static string Render(TableSpec spec, int? page = null) => Render(spec, page, null, false, false);

    /// <summary>The fragment as <see cref="Render(TableSpec, int?)"/> writes it, sorted by <paramref name="sortBy"/> when given. With
    /// <paramref name="controls"/> it also holds the sort and paging buttons the component wires up.</summary>
    internal static string Render(TableSpec spec, int? page, int? sortBy, bool descending, bool controls)
    {
        var view = TableView.Of(spec, sortBy, descending);
        if (sortBy is null) { sortBy = spec.SortBy; descending = spec.SortDescending; }
        var current = page ?? view.DefaultPage;
        var (first, last) = view.RowsOn(current);

        var style = spec.Style ?? ChartStyle.Light;
        var compact = spec.Density == Density.Compact;
        var sb = new StringBuilder();
        sb.Append("<div class='lumen-datatable lumen-datatable-").Append(compact ? "compact" : "regular")
          .Append("' style='").Append(LumenHtml.RootStyle(style)).Append("'>");
        sb.Append("<style>").Append(Css(compact)).Append("</style>");
        if (!string.IsNullOrWhiteSpace(spec.Description)) sb.Append("<p class='lumen-datatable-desc'>").Append(LumenHtml.E(spec.Description)).Append("</p>");

        sb.Append("<div class='lumen-datatable-scroll' role='region' tabindex='0' aria-label='").Append(LumenHtml.E(spec.Caption)).Append("'>");
        sb.Append("<table><caption>").Append(LumenHtml.E(spec.Caption)).Append("</caption><thead><tr>");
        var rowHeader = RowHeaderColumn(spec);
        for (var c = 0; c < spec.Columns.Count; c++) Header(sb, spec.Columns[c], c, sortBy, descending, controls, c == rowHeader);
        sb.Append("</tr></thead><tbody>");

        var bars = spec.Columns.Select((col, c) => col.Kind == ColumnKind.Bar ? BarScale(spec, col, c) : default).ToArray();
        // The rows of a page are the rows at its positions in the sorted order, never a slice of the spec's own order.
        for (var p = first; p <= last; p++)
        {
            sb.Append("<tr>");
            Cells(sb, spec.Rows[view.Order[p]].Cells, spec, style, rowHeader, bars, footer: false);
            sb.Append("</tr>");
        }
        sb.Append("</tbody>");
        if (spec.Footer is { } footer)
        {
            sb.Append("<tfoot><tr>");
            Cells(sb, footer, spec, style, rowHeader, bars, footer: true);
            sb.Append("</tr></tfoot>");
        }
        sb.Append("</table></div>");

        if (spec.PageSize is not null) Paging(sb, spec, view, current, first, last, controls);
        return sb.Append("</div>").ToString();
    }

    private static int RowHeaderColumn(TableSpec spec)
    {
        for (var c = 0; c < spec.Columns.Count; c++) if (spec.Columns[c].RowHeader) return c;
        return 0;
    }

    private static string Align(TableColumn column) => column.Align switch
    {
        ColumnAlign.Left => "left",
        ColumnAlign.Right => "right",
        ColumnAlign.Center => "center",
        _ => column.Kind is ColumnKind.Number or ColumnKind.Change or ColumnKind.Bar ? "right" : "left"
    };

    private static void Header(StringBuilder sb, TableColumn column, int index, int? sortBy, bool descending, bool controls, bool corner)
    {
        // The row-header column's own header is the corner: it stays put in both directions.
        sb.Append("<th scope='col' class='lumen-datatable-").Append(Align(column)).Append(corner ? " lumen-datatable-corner" : "").Append('\'');
        if (sortBy == index) sb.Append(" aria-sort='").Append(descending ? "descending" : "ascending").Append('\'');
        sb.Append('>');
        if (controls && column.Sortable && column.Kind != ColumnKind.Sparkline)
            sb.Append("<button type='button' class='lumen-datatable-sort' data-lumen-sort='").Append(index.ToString(CultureInfo.InvariantCulture)).Append("'>")
              .Append(LumenHtml.E(column.Header)).Append("</button>");
        else sb.Append(LumenHtml.E(column.Header));
        sb.Append("</th>");
    }

    private static void Paging(StringBuilder sb, TableSpec spec, TableView view, int page, int first, int last, bool controls)
    {
        sb.Append("<div class='lumen-datatable-pages' role='group' aria-label='Pages of ").Append(LumenHtml.E(spec.Caption)).Append("'>");
        if (controls)
        {
            PageButton(sb, "First", "First page", 0, page == 0);
            PageButton(sb, "Previous", "Previous page", Math.Max(0, page - 1), page == 0);
        }
        sb.Append("<span class='lumen-datatable-range'>");
        // An empty table has no rows to number: RowsOn(0) is (0,-1), which would read "Rows 1–0 of 0".
        if (view.Order.Count == 0) sb.Append("No rows");
        else
            sb.Append("Rows ").Append((first + 1).ToString("N0", CultureInfo.InvariantCulture)).Append('–').Append((last + 1).ToString("N0", CultureInfo.InvariantCulture))
              .Append(" of ").Append(view.Order.Count.ToString("N0", CultureInfo.InvariantCulture));
        sb.Append("</span>");
        if (controls)
        {
            PageButton(sb, "Next", "Next page", Math.Min(view.Pages - 1, page + 1), page == view.Pages - 1);
            PageButton(sb, "Last", "Last page", view.Pages - 1, page == view.Pages - 1);
        }
        sb.Append("</div>");
    }

    private static void PageButton(StringBuilder sb, string label, string name, int target, bool disabled)
    {
        sb.Append("<button type='button' data-lumen-page='").Append(target.ToString(CultureInfo.InvariantCulture)).Append("' aria-label='").Append(name).Append('\'');
        // aria-disabled, not disabled, so a button that stops moving keeps keyboard focus; the component ignores a press on it.
        if (disabled) sb.Append(" aria-disabled='true'");
        sb.Append('>').Append(label).Append("</button>");
    }

    private static void Cells(StringBuilder sb, IReadOnlyList<TableCell> cells, TableSpec spec, ChartStyle style, int rowHeader, (double Lo, double Hi)[] bars, bool footer)
    {
        for (var c = 0; c < cells.Count; c++)
        {
            var column = spec.Columns[c];
            var tag = c == rowHeader ? "th" : "td";
            sb.Append('<').Append(tag);
            if (c == rowHeader) sb.Append(" scope='row'");
            sb.Append(" class='lumen-datatable-").Append(Align(column)).Append("'>");
            Cell(sb, column, cells[c], style, bars[c], footer);
            sb.Append("</").Append(tag).Append('>');
        }
    }

    private static void Cell(StringBuilder sb, TableColumn column, TableCell cell, ChartStyle style, (double Lo, double Hi) bar, bool footer)
    {
        var main = new StringBuilder();
        // A footer cell may hold text in any column; it is written as written.
        if (footer && cell.Text is not null) main.Append(LumenHtml.E(cell.Text));
        else switch (column.Kind)
        {
            case ColumnKind.Text:
                main.Append(cell.Text is null ? MissingMarkup : LumenHtml.E(cell.Text));
                break;
            case ColumnKind.Number:
                main.Append(cell.Number is { } n ? LumenHtml.E(ChangeWords.Value(n, column.Format, column.Unit)) : MissingMarkup);
                break;
            case ColumnKind.Date:
                main.Append(cell.Date is { } d ? LumenHtml.E(d.ToString(column.DateFormat ?? "d MMM yyyy", CultureInfo.InvariantCulture)) : MissingMarkup);
                break;
            case ColumnKind.Change:
                if (cell.Number is { } now && cell.Previous is { } before) Change(main, column, now, before, style);
                else main.Append(MissingMarkup);
                break;
            case ColumnKind.Bar:
                if (cell.Number is { } v)
                {
                    // A footer total is a number in words, with no bar drawn against the column's scale.
                    if (footer) main.Append(LumenHtml.E(ChangeWords.Value(v, column.Format, column.Unit)));
                    else Bar(main, column, v, bar);
                }
                else main.Append(MissingMarkup);
                break;
            case ColumnKind.Sparkline:
                if (cell.Trend is { } trend)
                {
                    var (svg, summary) = LumenHtml.Sparkline(trend, column.Header, column.Format, column.Unit, style, 96, 24);
                    main.Append("<span class='lumen-datatable-trend' aria-hidden='true'>").Append(svg).Append("</span><span class='lumen-sr'>").Append(summary).Append("</span>");
                }
                else main.Append(MissingMarkup);
                break;
        }
        var missing = main.ToString() == MissingMarkup;
        if (cell.Href is not null && !missing && LumenHtml.SafeLink(cell.Href)) sb.Append("<a href='").Append(LumenHtml.E(cell.Href)).Append("'>").Append(main).Append("</a>");
        else sb.Append(main);
        if (!string.IsNullOrEmpty(cell.Note)) sb.Append("<span class='lumen-datatable-note'>").Append(LumenHtml.E(cell.Note)).Append("</span>");
    }

    private static void Change(StringBuilder sb, TableColumn column, double now, double before, ChartStyle style)
    {
        var (text, colour) = ChangeWords.Of(now, before, column.Better, ChangeAs.Difference, null, column.Format, column.Unit, style);
        // The arrow and the size are seen; the verdict that follows is kept for readers, so the colour is never the only carrier.
        var cut = text.IndexOf(" · ", StringComparison.Ordinal);
        sb.Append("<span class='lumen-datatable-change' style='color:").Append(colour).Append("'>");
        if (cut < 0) sb.Append(LumenHtml.E(text));
        else sb.Append(LumenHtml.E(text[..cut])).Append("<span class='lumen-sr'>").Append(LumenHtml.E(text[cut..])).Append("</span>");
        sb.Append("</span>");
    }

    /// <summary>The ends of a bar column's scale over the whole column, every page included: from 0 or the smallest value if lower,
    /// to <see cref="TableColumn.BarMax"/> or the largest value, and at least 0.</summary>
    private static (double Lo, double Hi) BarScale(TableSpec spec, TableColumn column, int index)
    {
        var values = spec.Rows.Select(r => r.Cells[index].Number).Where(v => v is not null).Select(v => v!.Value).ToArray();
        var lo = Math.Min(0, values.Length == 0 ? 0 : values.Min());
        var hi = Math.Max(column.BarMax ?? (values.Length == 0 ? 0 : values.Max()), 0);
        return (lo, hi);
    }

    private static void Bar(StringBuilder sb, TableColumn column, double value, (double Lo, double Hi) scale)
    {
        var range = scale.Hi - scale.Lo;
        var zero = range > 0 ? (0 - scale.Lo) / range * 100 : 0;
        // A negative value fills from the zero line leftwards; a positive one from the zero line rightwards, to at most the scale's end.
        var from = value < 0 ? (range > 0 ? (value - scale.Lo) / range * 100 : 0) : zero;
        var width = range > 0 ? Math.Min(Math.Abs(value), value < 0 ? -scale.Lo : scale.Hi) / range * 100 : 0;
        string P(double x) => Math.Round(x, 2).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        sb.Append("<span class='lumen-datatable-bar' aria-hidden='true'>");
        sb.Append("<span class='lumen-datatable-fill' style='left:").Append(P(from)).Append(";width:").Append(P(width)).Append("'></span>");
        if (scale.Lo < 0) sb.Append("<span class='lumen-datatable-zero' style='left:").Append(P(zero)).Append("'></span>");
        sb.Append("</span>");
        sb.Append("<span class='lumen-datatable-value'>").Append(LumenHtml.E(ChangeWords.Value(value, column.Format, column.Unit))).Append("</span>");
    }

    private static string Css(bool compact)
    {
        var r = compact ? ".lumen-datatable.lumen-datatable-compact" : ".lumen-datatable.lumen-datatable-regular";
        var pad = compact ? "4px 8px" : "8px 12px";
        return $"{r}{{display:block;box-sizing:border-box;margin:0;padding:0;background:var(--lumen-bg);color:var(--lumen-text);font-family:var(--lumen-font);font-size:{(compact ? 13 : 14)}px;line-height:1.3}}"
            + $"{r} *{{box-sizing:border-box}}"
            + $"{r} p{{margin:0}}"
            + $"{r} .lumen-datatable-desc{{margin:0 0 8px;font-size:13px;color:var(--lumen-muted)}}"
            + $"{r} .lumen-datatable-scroll{{position:relative;overflow:auto;max-height:var(--lumen-datatable-height,none);margin:0;padding:0;border:1px solid var(--lumen-grid);border-radius:8px;background:var(--lumen-bg)}}"
            + $"{r} .lumen-datatable-scroll:focus-visible,{r} button:focus-visible{{outline:2px solid var(--lumen-accent);outline-offset:2px}}"
            + $"{r} table{{border-collapse:separate;border-spacing:0;margin:0;padding:0;width:100%}}"
            + $"{r} caption{{margin:0;padding:{pad};text-align:left;font-weight:600;color:var(--lumen-text)}}"
            + $"{r} th,{r} td{{margin:0;padding:{pad};border-bottom:1px solid var(--lumen-grid);background:var(--lumen-bg);color:var(--lumen-text);vertical-align:top;font-weight:400}}"
            + $"{r} thead th{{position:sticky;top:0;z-index:2;font-weight:600;white-space:nowrap}}"
            + $"{r} thead th.lumen-datatable-corner{{left:0;z-index:3;background:var(--lumen-bg)}}"
            + $"{r} tbody th,{r} tfoot th{{position:sticky;left:0;z-index:1;font-weight:600;text-align:left}}"
            + $"{r} tfoot td,{r} tfoot th{{border-top:2px solid var(--lumen-grid);font-weight:600}}"
            + $"{r} .lumen-datatable-left{{text-align:left}}"
            + $"{r} .lumen-datatable-right{{text-align:right}}"
            + $"{r} .lumen-datatable-center{{text-align:center}}"
            + $"{r} thead th[aria-sort=ascending]::after{{content:\" ▲\";content:\" ▲\" / \"\"}}"
            + $"{r} thead th[aria-sort=descending]::after{{content:\" ▼\";content:\" ▼\" / \"\"}}"
            + $"{r} .lumen-datatable-sort{{margin:0;padding:0;border:0;background:none;color:inherit;font:inherit;font-weight:600;cursor:pointer}}"
            + $"{r} .lumen-datatable-change{{font-weight:600;white-space:nowrap}}"
            + $"{r} .lumen-datatable-bar{{position:relative;display:inline-block;width:64px;height:8px;margin-right:8px;vertical-align:middle;border-radius:4px;background:var(--lumen-grid);overflow:hidden}}"
            + $"{r} .lumen-datatable-fill{{position:absolute;top:0;bottom:0;background:var(--lumen-accent)}}"
            + $"{r} .lumen-datatable-zero{{position:absolute;top:0;bottom:0;width:1px;background:var(--lumen-text)}}"
            + $"{r} .lumen-datatable-trend{{display:inline-block;line-height:0}}"
            + $"{r} .lumen-datatable-note{{display:block;font-size:12px;font-weight:400;color:var(--lumen-muted);overflow-wrap:anywhere}}"
            + $"{r} a{{color:inherit}}"
            + $"{r} .lumen-datatable-pages{{display:flex;flex-wrap:wrap;align-items:center;gap:8px;margin:8px 0 0;padding:0;font-size:13px;color:var(--lumen-muted)}}"
            + $"{r} .lumen-datatable-pages button{{margin:0;padding:4px 10px;border:1px solid var(--lumen-grid);border-radius:6px;background:var(--lumen-bg);color:var(--lumen-text);font:inherit;cursor:pointer}}"
            + $"{r} .lumen-datatable-pages button[aria-disabled='true']{{color:var(--lumen-muted);cursor:default}}"
            + LumenHtml.HiddenRule(".lumen-datatable");
    }
}
