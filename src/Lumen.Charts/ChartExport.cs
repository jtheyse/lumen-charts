using System.Globalization;
using System.Text;

namespace Lumen.Charts;

public static class ChartExport
{
    /// <summary>Original observations, not sampled display points. Text is protected against spreadsheet formulas.</summary>
    public static string Csv(ChartSpec spec)
    {
        ChartValidation.Validate(spec);
        var time=spec.XAxis==AxisKind.Time;
        var columns=spec.Kind switch{ChartKind.Candlestick=>",Open,High,Low,Close",ChartKind.Band=>",Low,High",_=>""};
        var result=new StringBuilder($"Series,X,{(time?"XTime,":"")}Y,Label,Size{columns}\r\n");
        foreach(var s in spec.Series)
            foreach(var p in s.Points)
                result.AppendLine($"{Cell(s.Name)},{Number(p.X)},{(time?TimeAxis.Moment(p.X).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ",CultureInfo.InvariantCulture)+",":"")}{Number(p.Y)},{Cell(p.Label ?? "")},{Number(p.Size)}{spec.Kind switch{ChartKind.Candlestick=>$",{Number(p.Open)},{Number(p.High)},{Number(p.Low)},{Number(p.Close)}",ChartKind.Band=>$",{Number(p.Low)},{Number(p.High)}",_=>""}}");
        return result.ToString();
    }
    private static string Number(double? value)=>value?.ToString("R",CultureInfo.InvariantCulture) ?? "";
    private static string Cell(string value)
    {
        if(value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@')value="'"+value;
        return "\""+value.Replace("\"","\"\"")+"\"";
    }
}
