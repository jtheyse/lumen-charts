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
    /// <see cref="ChartSpec.YFormat"/> with <see cref="ChartSpec.YUnit"/>, then <c> · </c> and its <see cref="ChartPoint.SubLabel"/>,
    /// then its <see cref="ChartPoint.ValueNote"/> as written, then <c>, not rated: </c> and the reason of a not-rated cell (from 0.46.1;
    /// 0.46.0 put the note before the sub-label). A cell with no value starts with <c>—</c> when it is not rated and with its
    /// <see cref="ChartPoint.GapLabel"/> word when it has one, and its note follows a value or that word, never a bare dash. A cell is
    /// empty when it has no point, or has no value, no gap label and no not-rated reason.
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
    /// <summary>
    /// A chart's original observations as a <see cref="TableSpec"/>, for <c>LumenTable</c> or <c>POST table/html</c>: a Series column, an
    /// X / Category column that names each row, and a Value column, one row for each point, series by series, points in their order.
    /// The X and Value words are the ones the component's data table and status line write. When every series draws a plain Y (no
    /// candles, OHLC, rings, timeline spans, blocks, ranges or bands) and all read in one value format and unit, Value is a sortable
    /// Number column in that format and unit, a point's <see cref="ChartPoint.ValueNote"/> is the cell's note, and a missing value
    /// is last in a sort, with its <see cref="ChartPoint.GapLabel"/> as its note; otherwise Value is a Text column that is not sortable.
    /// The caption is the chart's title and <c> — original data</c> (<c>Chart data — original data</c> for an untitled chart), cut with
    /// <c>…</c> to 120 characters, never through a surrogate pair. With more than 100 rows the table pages at 100; <see cref="TableSpec.StartAtEnd"/> is left false for
    /// the caller to set. A cell's words are cut with <c>…</c> to the table's 200 characters, and a note to its 80. The table takes the
    /// chart's style, or its theme's.
    /// </summary>
    /// <exception cref="ArgumentException">The spec is not valid, or is a heatmap, whose data is a grid (see <see cref="HtmlTable"/>).</exception>
    public static TableSpec DataTable(ChartSpec spec)
    {
        ChartValidation.Validate(spec);
        if(spec.Kind==ChartKind.Heatmap)throw new ArgumentException("A heatmap's data is a grid; use HtmlTable for it.");
        var x=ChartData.XAxisOf(spec);
        var plain=spec.Series.All(s=>(s.Kind ?? spec.Kind) is not (ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Ring or ChartKind.Timeline or ChartKind.Blocks or ChartKind.Range)
            &&s.Points.All(p=>p.Low is null&&p.High is null&&p.Open is null&&p.Close is null&&p.XEnd is null));
        var axes=spec.Series.Select(s=>ChartData.Measure(spec,s)).ToArray();
        // A log axis writes a duration as a span and the table writes a number as a clock, so only linear axes make a Number column.
        var measures=axes.Select(a=>(a.ValueFormat,a.Unit)).Distinct().ToArray();
        var number=plain&&measures.Length<=1&&axes.All(a=>a.Kind==AxisKind.Linear);
        var (format,unit)=measures.Length==1?measures[0]:(ValueFormat.Number,(string?)null);
        var value=number?new TableColumn("Value"){Kind=TableColumnKind.Number,Format=format,Unit=unit}:new TableColumn("Value"){Sortable=false};
        var rows=new List<TableRow>();
        foreach(var s in spec.Series)
            foreach(var p in s.Points)
            {
                var category=TableCell.Of(Cut(ChartData.Category(spec,x,p),200));
                rows.Add(new([TableCell.Of(Cut(s.Name,200)),category,number
                    ?new(){Number=p.Y,Note=(p.Y.HasValue?p.ValueNote:p.GapLabel is {} gap?gap+p.ValueNote:p.ValueNote) is {} note?Cut(note,80):null}
                    :TableCell.Of(Cut(ChartData.Value(spec,x,s,p),200))]));
            }
        var caption=(string.IsNullOrWhiteSpace(spec.Title)?"Chart data":spec.Title)+" — original data";
        return new([new TableColumn("Series"),new TableColumn("X / Category"){RowHeader=true,Sortable=false},value],rows)
        {
            Caption=Cut(caption,120),
            PageSize=rows.Count>100?100:null,
            Style=ChartSvg.ResolveStyle(spec)
        };
    }
    // Words cut with an ellipsis to a table's limit, as the table would refuse them whole.
    private static string Cut(string text,int limit)=>text.Length<=limit?text:text[..(char.IsHighSurrogate(text[limit-2])?limit-2:limit-1)]+"…";
    // A grid cell's words, which are encoded one by one so that the separators between them stay as they are written: the value, or the
    // gap label's word where there is none, then " · " and the sub-label, then the note as written, then ", not rated: " and the reason.
    // The note is written where the cell's name writes it, after a value or a gap label's word, and not after a bare dash. A cell with no
    // point, or with no value, no gap label and no not-rated reason, is empty.
    private static string GridCell(ChartPoint? p,Axis values)
    {
        if(p is null||!p.Y.HasValue&&p.NotRated is null&&p.GapLabel is null)return "";
        var cell=WebUtility.HtmlEncode(p.Y.HasValue?values.Format(p.Y.Value):p.GapLabel??"—");
        if(p.SubLabel is not null)cell+=" · "+WebUtility.HtmlEncode(p.SubLabel);
        if(p.ValueNote is not null&&(p.Y.HasValue||p.GapLabel is not null))cell+=WebUtility.HtmlEncode(p.ValueNote);
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
