using System.Globalization;
using System.Net;
using System.Text;

namespace Lumen.Charts;

/// <summary>A chart's data in a form other programs read.</summary>
public static class ChartExport
{
    /// <summary>Original observations, not sampled display points. Text is protected against spreadsheet formulas. A chart whose
    /// points carry a <see cref="ChartPoint.ValueNote"/> adds a <c>Note</c> column, and a heatmap with a not-rated cell adds a
    /// <c>NotRated</c> column, the last of all, holding each cell's <see cref="ChartPoint.NotRated"/> reason, empty where the cell is
    /// rated.</summary>
    public static string Csv(ChartSpec spec)
    {
        ChartValidation.Validate(spec);
        var time=spec.XAxis==AxisKind.Time;
        var prices=spec.Kind is ChartKind.Candlestick or ChartKind.Ohlc;
        // A band or range bar drawn by any series carries its edges into the file, whatever kind the chart is.
        var band=!prices&&spec.Series.Any(s=>ChartSvg.Mark(spec,s) is ChartKind.Band or ChartKind.Range);
        // A ring is measured against its series' goal, which the file carries beside its value.
        var rings=spec.Kind==ChartKind.Ring;
        // A timeline's spans and a series of blocks carry where each ends.
        var spans=spec.Series.Any(s=>s.Points.Any(p=>p.XEnd.HasValue));
        // A value's note, such as the size of a finishing position's field, is carried beside it, in a column of its own.
        var notes=spec.Series.Any(s=>s.Points.Any(p=>p.ValueNote is not null));
        // A heatmap cell that is not rated carries why, in a column of its own, last.
        var rated=spec.Series.Any(s=>s.Points.Any(p=>p.NotRated is not null));
        var columns=(prices?",Open,High,Low,Close":band?",Low,High":rings?",Goal":"")+(spans?",XEnd":"")+(notes?",Note":"")+(rated?",NotRated":"");
        var result=new StringBuilder($"Series,X,{(time?"XTime,":"")}Y,Label,Size{columns}\r\n");
        foreach(var s in spec.Series)
            foreach(var p in s.Points)
                // CSV ends every record with CRLF (RFC 4180), on every platform, as the header does.
                result.Append($"{Cell(s.Name)},{Number(p.X)},{(time?TimeAxis.Moment(p.X).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ",CultureInfo.InvariantCulture)+",":"")}{Number(p.Y)},{Cell(p.Label ?? "")},{Number(p.Size)}{(prices?$",{Number(p.Open)},{Number(p.High)},{Number(p.Low)},{Number(p.Close)}":band?$",{Number(p.Low)},{Number(p.High)}":rings?$",{Number(s.Goal ?? 100)}":"")}{(spans?$",{Number(p.XEnd)}":"")}{(notes?$",{Cell(p.ValueNote ?? "")}":"")}{(rated?$",{Cell(p.NotRated ?? "")}":"")}").Append("\r\n");
        return result.ToString();
    }
    /// <summary>
    /// A heatmap as a real HTML grid table, for a page that reads its data as a table rather than as a drawing: a table of class
    /// <c>lumen-grid-table</c> with the chart's title as its caption, its columns across the head, each labelled as the heatmap writes it
    /// but whole, not cut, and a row for each series, named by a row header. A cell holds its value as the cells' names write it, in
    /// <see cref="ChartSpec.YFormat"/> with <see cref="ChartSpec.YUnit"/> and followed by its <see cref="ChartPoint.ValueNote"/>, then
    /// <c> · </c> and its <see cref="ChartPoint.SubLabel"/>, then <c>, not rated: </c> and the reason of a not-rated cell, which has
    /// <c>—</c> in place of a value it does not have. A cell is empty when it has no point, or has neither a value nor a not-rated reason.
    /// Every word is HTML-encoded, and the table is styled by <c>lumen.css</c>, which the Blazor package serves. The spec is validated
    /// first, and a chart that is not a heatmap has no grid and is refused.
    /// </summary>
    /// <exception cref="ArgumentException">The spec is not valid, or is not a heatmap.</exception>
    public static string HtmlTable(ChartSpec spec)
    {
        ChartValidation.Validate(spec);
        if(spec.Kind!=ChartKind.Heatmap)
            throw new ArgumentException("An HTML grid table reads a heatmap's rows by its columns; other kinds have no grid — use the component's data table or Csv.");
        var values=ChartSvg.HeatmapValues(spec);
        var columns=ChartSvg.HeatmapColumns(spec);
        var table=new StringBuilder($"<table class='lumen-grid-table'><caption>{WebUtility.HtmlEncode(spec.Title)}</caption><thead><tr><td></td>");
        foreach(var at in columns)table.Append($"<th scope='col'>{WebUtility.HtmlEncode(ChartSvg.HeatmapColumn(spec,at))}</th>");
        table.Append("</tr></thead><tbody>");
        foreach(var series in spec.Series)
        {
            table.Append($"<tr><th scope='row'>{WebUtility.HtmlEncode(series.Name)}</th>");
            foreach(var at in columns)table.Append("<td>").Append(GridCell(series.Points.FirstOrDefault(point=>point.X==at),values)).Append("</td>");
            table.Append("</tr>");
        }
        return table.Append("</tbody></table>").ToString();
    }
    // A grid cell's words, which are encoded one by one so that the separators between them stay as they are written. A cell with no point,
    // or with neither a value nor a not-rated reason, is empty.
    private static string GridCell(ChartPoint? p,Axis values)
    {
        if(p is null||!p.Y.HasValue&&p.NotRated is null)return "";
        var cell=WebUtility.HtmlEncode(p.Y.HasValue?values.Format(p.Y.Value)+p.ValueNote:"—");
        if(p.SubLabel is not null)cell+=" · "+WebUtility.HtmlEncode(p.SubLabel);
        if(p.NotRated is not null)cell+=", not rated: "+WebUtility.HtmlEncode(p.NotRated);
        return cell;
    }
    private static string Number(double? value)=>value?.ToString("R",CultureInfo.InvariantCulture) ?? "";
    private static string Cell(string value)
    {
        if(value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@')value="'"+value;
        return "\""+value.Replace("\"","\"\"")+"\"";
    }
}
