namespace Lumen.Gallery;

public enum AxisDemo { Numeric, Time, Log }

public static class DemoData
{
    public static bool TimeCapable(Lumen.Charts.ChartKind kind)=>kind is Lumen.Charts.ChartKind.Line or Lumen.Charts.ChartKind.Area or Lumen.Charts.ChartKind.Scatter or Lumen.Charts.ChartKind.Bubble;
    public static bool LogCapable(Lumen.Charts.ChartKind kind)=>kind is Lumen.Charts.ChartKind.Line or Lumen.Charts.ChartKind.Scatter or Lumen.Charts.ChartKind.Bubble;
    public static readonly string[] Months=["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];
    public static Lumen.Charts.ChartSpec Create(Lumen.Charts.ChartKind kind,Lumen.Charts.ChartTheme theme,int revision=0,AxisDemo axis=AxisDemo.Numeric)
    {
        var random=new Random(42+revision);
        Lumen.Charts.ChartSeries Make(string name,double baseline) => new(name,Enumerable.Range(0,12).Select(i=>new Lumen.Charts.ChartPoint(i,Math.Round(baseline+i*2+random.NextDouble()*18,1),Months[i],10+random.Next(80))).ToArray());
        var series=new[]{Make("Workspace",35),Make("Enterprise",20),Make("Community",10)};
        var xKind=Lumen.Charts.AxisKind.Linear; var title="A clearer view of growth"; var desc="Monthly activity across three product plans";var x="Month index";var y="Active accounts (thousands)";
        if(kind==Lumen.Charts.ChartKind.Donut)
        {
            series=[new("Acquisition",[new(0,42,"Organic"),new(1,28,"Direct"),new(2,18,"Referral"),new(3,12,"Campaigns")])];
            title="Where our audience finds us";desc="Acquisition mix · share of 100 sample accounts";
        }
        if(kind==Lumen.Charts.ChartKind.Radar)
        {
            string[] labels=["Clarity","Speed","Coverage","Access","Control","Export"];
            series=[new("Current",labels.Select((l,i)=>new Lumen.Charts.ChartPoint(i,55+random.Next(40),l)).ToArray()),new("Previous",labels.Select((l,i)=>new Lumen.Charts.ChartPoint(i,30+random.Next(35),l)).ToArray())];
            title="Product quality at a glance";desc="Illustrative evaluation · shared 0–100 units";
        }
        if(kind==Lumen.Charts.ChartKind.Heatmap)
        {
            series=Enumerable.Range(0,7).Select(d=>new Lumen.Charts.ChartSeries(new[]{"Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday"}[d],Enumerable.Range(0,12).Select(h=>new Lumen.Charts.ChartPoint(h,random.Next(5,100),$"{h+8}:00")).ToArray())).ToArray();
            title="Find the busiest moments";desc="Activity by day and hour · darker cells mean higher activity";
        }
        if(kind==Lumen.Charts.ChartKind.Candlestick)
        {
            var open=118.0;var candles=new List<Lumen.Charts.ChartPoint>();var day=new DateTimeOffset(2026,3,2,0,0,0,TimeSpan.Zero);
            for(var i=0;i<30;i++)
            {
                var close=Math.Round(open*(1+(random.NextDouble()-.47)*.06),2);
                candles.Add(Lumen.Charts.ChartPoint.Candle(Lumen.Charts.TimeAxis.Value(day.AddDays(i)),open,
                    Math.Round(Math.Max(open,close)*(1+random.NextDouble()*.018),2),
                    Math.Round(Math.Min(open,close)*(1-random.NextDouble()*.018),2),close));
                open=close;
            }
            series=[new("ACME",candles)];
            title="Follow the market's mood";desc="Simulated daily prices · the body spans open to close, the wick the full range";
            x="Trading day (UTC)";y="Price (ZAR)";
            xKind=Lumen.Charts.AxisKind.Time;
        }
        if(kind==Lumen.Charts.ChartKind.Band)
        {
            series=[new("Projected demand",Enumerable.Range(0,12).Select(i=>{
                var value=Math.Round(42+i*3.4+random.NextDouble()*4,1);var spread=Math.Round(3+i*.9,1);
                return Lumen.Charts.ChartPoint.Interval(i,value,Math.Round(value-spread,1),Math.Round(value+spread,1),Months[i]);}).ToArray())];
            title="Say how sure you are";desc="Projection with a widening confidence interval";x="Month";y="Units (thousands)";
        }
        if(kind==Lumen.Charts.ChartKind.Histogram)
        {
            series=[new("Response time",Enumerable.Range(0,320).Select(i=>
                new Lumen.Charts.ChartPoint(i,Math.Round(38-Math.Log(1-random.NextDouble())*24,1))).ToArray())];
            title="Where the response times land";desc="320 sampled requests · bin count chosen from the data";
            x="Response time (ms)";y="Requests";
        }
        if(kind==Lumen.Charts.ChartKind.Box)
        {
            Lumen.Charts.ChartSeries Spread(string name,double center,double width,int count)=>new(name,
                Enumerable.Range(0,count).Select(i=>new Lumen.Charts.ChartPoint(i,
                    Math.Round(center+(random.NextDouble()+random.NextDouble()-1)*width+(i%17==0?width*2.4:0),1))).ToArray());
            series=[Spread("Europe",210,40,45),Spread("Africa",260,70,38),Spread("Americas",180,30,52)];
            title="Compare the spread, not just the average";desc="Latency samples by region · box shows quartiles, whiskers reach 1.5 interquartile ranges";
            x="Region";y="Latency (ms)";
        }
        if(kind==Lumen.Charts.ChartKind.Bar) {title="Compare plans without the clutter";x="Month";}
        if(kind==Lumen.Charts.ChartKind.Scatter || kind==Lumen.Charts.ChartKind.Bubble)
        {
            title="Explore the relationship";desc="Account engagement and retention · illustrative observations";x="Engagement score";y="Retention score";
            series=series.Select(s=>s with {Points=s.Points.Select(p=>p with {X=p.X*8+random.Next(6)}).ToArray()}).ToArray();
        }
        var spec=new Lumen.Charts.ChartSpec{Kind=kind,Theme=theme,XAxis=xKind,Title=title,Description=desc,Series=series,XLabel=x,YLabel=y,Source="Source: deterministic demonstration data · not business results",Height=420};
        if(axis==AxisDemo.Time&&TimeCapable(kind))
        {
            var start=new DateTimeOffset(2026,1,5,0,0,0,TimeSpan.Zero);
            spec=spec with{XAxis=Lumen.Charts.AxisKind.Time,XLabel="Week beginning (UTC)",Description="Weekly activity across three product plans",
                Series=series.Select(s=>s with{Points=s.Points.Select((p,i)=>p with{X=Lumen.Charts.TimeAxis.Value(start.AddDays(i*7)),Label=null}).ToArray()}).ToArray()};
        }
        if(axis==AxisDemo.Log&&LogCapable(kind))
            spec=spec with{YAxis=Lumen.Charts.AxisKind.Log,YLabel="Requests per minute (log scale)",Description="Traffic spanning several orders of magnitude",
                Series=spec.Series.Select((s,si)=>s with{Points=s.Points.Select((p,i)=>p with{Y=Math.Round(Math.Pow(10,si*.6+i*.3)+random.Next(1,9),2)}).ToArray()}).ToArray()};
        return spec;
    }
    public static Lumen.Charts.GraphSpec Graph(Lumen.Charts.GraphLayout layout,Lumen.Charts.ChartTheme theme)=>new()
    {
        Title="From source to insight",Layout=layout,Theme=theme,
        Nodes=[new("sources","Sources"),new("ingest","Ingestion"),new("validate","Validation"),new("transform","Transform"),new("charts","Charts"),new("api","Chart API"),new("reports","Reports")],
        Edges=[new("sources","ingest"),new("ingest","validate"),new("validate","transform"),new("transform","charts"),new("transform","api"),new("charts","reports"),new("api","reports"),new("ingest","reports","audit trail")]
    };
}
