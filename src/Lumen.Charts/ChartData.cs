namespace Lumen.Charts;

/// <summary>The words a chart's data is read in, shared by the component's status line and <see cref="ChartExport.DataTable"/>.</summary>
internal static class ChartData
{
    // A timeline's spans reach to their ends.
    public static Axis XAxisOf(ChartSpec spec)=>Axis.Create(spec.XAxis,spec.Series.SelectMany(s=>s.Points).SelectMany(p=>p.XEnd is {} end?new[]{p.X,end}:new[]{p.X}), min:spec.XMin, max:spec.XMax, zone:TimeAxis.Zone(spec.TimeZone),
        weekends:spec.SkipWeekends, skips:spec.TimeSkips.Count>0?spec.TimeSkips:null) with {ValueFormat=spec.XFormat};
    // How a value reads depends only on the axis kind and format, so the bounds here are nominal. A series in a pane below
    // the main plot reads through that pane's axes.
    public static Axis Measure(ChartSpec spec,ChartSeries series)
    {
        var pane=series.Pane>0&&series.Pane<=spec.Panes.Count?spec.Panes[series.Pane-1]:null;
        return series.Secondary ? new(pane?.Y2Axis ?? spec.Y2Axis,1,10){ValueFormat=pane?.Y2Format ?? spec.Y2Format} : new(pane?.YAxis ?? spec.YAxis,1,10){ValueFormat=pane?.YFormat ?? spec.YFormat,Unit=pane is null ? spec.YUnit : pane.YUnit};
    }
    // A point's category or X as its mark's name says it: its label, and on a category chart its category's sub-label after it, the first
    // any series gives it.
    public static string Category(ChartSpec spec,Axis x,ChartPoint point)=>(point.Label ?? x.Format(point.X))
        +(spec.Kind is ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn&&spec.Series.SelectMany(s=>s.Points).FirstOrDefault(p=>p.X==point.X&&p.SubLabel is not null)?.SubLabel is {} sub?" · "+sub:"");
    public static string Value(ChartSpec spec,Axis x,ChartSeries series,ChartPoint point)
    {
        var y=Measure(spec,series);
        return (series.Kind ?? spec.Kind) switch
        {
            ChartKind.Candlestick or ChartKind.Ohlc=>$"O {y.Format(point.Open ?? 0)} · H {y.Format(point.High ?? 0)} · L {y.Format(point.Low ?? 0)} · C {y.Format(point.Close ?? 0)}",
            ChartKind.Ring when point.Y.HasValue=>$"{y.Format(point.Y.Value)} of {y.Format(series.Goal ?? 100)}",
            // A timeline span reads its start in the X column, and how long it runs to its end here.
            ChartKind.Timeline when point.XEnd is {} end=>$"to {x.Format(end)}",
            // A block reads its height and the span it covers.
            ChartKind.Blocks when point.XEnd is {} end&&point.Y.HasValue=>$"{y.Format(point.Y.Value)}{point.ValueNote} from {x.Format(point.X)} to {x.Format(end)}",
            ChartKind.Range when point.Y is null&&point.Low.HasValue&&point.High.HasValue=>$"{y.Format(point.Low.Value)} to {y.Format(point.High.Value)}",
            _ when point.Y.HasValue&&point.Low.HasValue&&point.High.HasValue=>$"{y.Format(point.Y.Value)}{point.ValueNote} ({y.Format(point.Low.Value)} to {y.Format(point.High.Value)})",
            // A value's note follows it, as it does in the mark's name, and a missing value's gap label and note stand in its place.
            _=>point.Y.HasValue?y.Format(point.Y.Value)+point.ValueNote:point.GapLabel is {} gap?gap+point.ValueNote:"Missing"
        };
    }
}
