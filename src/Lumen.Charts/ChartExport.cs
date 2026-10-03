using System.Globalization;
using System.Text;

namespace Lumen.Charts;

/// <summary>A chart's data in a form other programs read.</summary>
public static class ChartExport
{
    /// <summary>Original observations, not sampled display points. Text is protected against spreadsheet formulas.</summary>
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
        var columns=(prices?",Open,High,Low,Close":band?",Low,High":rings?",Goal":"")+(spans?",XEnd":"");
        var result=new StringBuilder($"Series,X,{(time?"XTime,":"")}Y,Label,Size{columns}\r\n");
        foreach(var s in spec.Series)
            foreach(var p in s.Points)
                // CSV ends every record with CRLF (RFC 4180), on every platform, as the header does.
                result.Append($"{Cell(s.Name)},{Number(p.X)},{(time?TimeAxis.Moment(p.X).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ",CultureInfo.InvariantCulture)+",":"")}{Number(p.Y)},{Cell(p.Label ?? "")},{Number(p.Size)}{(prices?$",{Number(p.Open)},{Number(p.High)},{Number(p.Low)},{Number(p.Close)}":band?$",{Number(p.Low)},{Number(p.High)}":rings?$",{Number(s.Goal ?? 100)}":"")}{(spans?$",{Number(p.XEnd)}":"")}").Append("\r\n");
        return result.ToString();
    }
    private static string Number(double? value)=>value?.ToString("R",CultureInfo.InvariantCulture) ?? "";
    private static string Cell(string value)
    {
        if(value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@')value="'"+value;
        return "\""+value.Replace("\"","\"\"")+"\"";
    }
}
