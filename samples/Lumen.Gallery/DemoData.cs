namespace Lumen.Gallery;

public enum AxisDemo { Numeric, Time, Log, PowerCurve, Pace, Zones, Performance, Target }
public enum BrandDemo { Lumen, Harbour, PageCss }

public static class DemoData
{
    /// <summary>A brand written in C#: it renders the same in the page, in exports and through the HTTP API.</summary>
    public static readonly Lumen.Charts.ChartStyle Harbour = new()
    {
        Background="#F6F3EE",Text="#1F2A37",Muted="#4B5563",Grid="#E5DED3",Edge="#6B7280",
        Series=["#1D4E89","#B03A2E","#2E7D5B","#9A6A12"],Rising="#2E7D5B",Falling="#B03A2E",
        HeatmapLow="#EFE6D8",HeatmapHigh="#1D4E89",FontFamily="Georgia,Cambria,serif"
    };
    public static bool TimeCapable(Lumen.Charts.ChartKind kind)=>kind is Lumen.Charts.ChartKind.Line or Lumen.Charts.ChartKind.Area or Lumen.Charts.ChartKind.Scatter or Lumen.Charts.ChartKind.Bubble;
    public static bool LogCapable(Lumen.Charts.ChartKind kind)=>kind is Lumen.Charts.ChartKind.Line or Lumen.Charts.ChartKind.Scatter or Lumen.Charts.ChartKind.Bubble;
    public static bool DurationCapable(Lumen.Charts.ChartKind kind)=>kind is Lumen.Charts.ChartKind.Line;
    public static bool ZoneCapable(Lumen.Charts.ChartKind kind)=>kind is Lumen.Charts.ChartKind.Line or Lumen.Charts.ChartKind.Bar;
    public static bool PerformanceCapable(Lumen.Charts.ChartKind kind)=>kind is Lumen.Charts.ChartKind.Line;
    public static bool TargetCapable(Lumen.Charts.ChartKind kind)=>kind is Lumen.Charts.ChartKind.Column;
    public static readonly string[] Months=["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];
    public static Lumen.Charts.ChartSpec Create(Lumen.Charts.ChartKind kind,Lumen.Charts.ChartTheme theme,int revision=0,AxisDemo axis=AxisDemo.Numeric)
    {
        var random=new Random(42+revision);
        Lumen.Charts.ChartSeries Make(string name,double baseline) => new(name,Enumerable.Range(0,12).Select(i=>new Lumen.Charts.ChartPoint(i,Math.Round(baseline+i*2+random.NextDouble()*18,1),Months[i],10+random.Next(80))).ToArray());
        var series=new[]{Make("Workspace",35),Make("Enterprise",20),Make("Community",10)};
        var xKind=Lumen.Charts.AxisKind.Linear; var weekends=false; IReadOnlyList<Lumen.Charts.TimeSkip> holidays=[];
        var title="A clearer view of growth"; var desc="Monthly activity across three product plans";var x="Month index";var y="Active accounts (thousands)";
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
        if(kind is Lumen.Charts.ChartKind.Candlestick or Lumen.Charts.ChartKind.Ohlc)
        {
            var open=118.0;var candles=new List<Lumen.Charts.ChartPoint>();var day=new DateTimeOffset(2026,3,2,0,0,0,TimeSpan.Zero);
            var shut=new DateTime(2026,4,3);   // Good Friday, when the exchange does not open.
            while(candles.Count<30)
            {
                if(day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && day.Date!=shut)
                {
                    var close=Math.Round(open*(1+(random.NextDouble()-.47)*.06),2);
                    candles.Add(Lumen.Charts.ChartPoint.Candle(Lumen.Charts.TimeAxis.Value(day),open,
                        Math.Round(Math.Max(open,close)*(1+random.NextDouble()*.018),2),
                        Math.Round(Math.Min(open,close)*(1-random.NextDouble()*.018),2),close));
                    open=close;
                }
                day=day.AddDays(1);
            }
            series=[new("ACME",candles)];
            title=kind==Lumen.Charts.ChartKind.Candlestick?"Follow the market's mood":"The same prices, bar by bar";
            desc=kind==Lumen.Charts.ChartKind.Candlestick
                ? "Simulated daily prices · 30 trading days, with the weekends and Good Friday left out of the axis"
                : "The same 30 trading days as the candlestick · the tick on the left is the open, the one on the right the close";
            x="Trading day (UTC)";y="Price (ZAR)";
            xKind=Lumen.Charts.AxisKind.Time;weekends=true;holidays=[Lumen.Charts.TimeAxis.Day(shut)];
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
            // After the cache most requests are hits; the misses keep the old tail.
            series=[new("Before caching",Enumerable.Range(0,320).Select(i=>
                    new Lumen.Charts.ChartPoint(i,Math.Round(38-Math.Log(1-random.NextDouble())*24,1))).ToArray()),
                new("After caching",Enumerable.Range(0,320).Select(i=>
                    new Lumen.Charts.ChartPoint(i,Math.Round(i%10<7?12-Math.Log(1-random.NextDouble())*6:38-Math.Log(1-random.NextDouble())*24,1))).ToArray())];
            title="See where the response times moved";desc="320 sampled requests before and after a cache · one set of bins, chosen from both";
            x="Response time (ms)";y="Requests";
        }
        if(kind is Lumen.Charts.ChartKind.Box or Lumen.Charts.ChartKind.Violin)
        {
            Lumen.Charts.ChartSeries Spread(string name,double center,double width,int count)=>new(name,
                Enumerable.Range(0,count).Select(i=>new Lumen.Charts.ChartPoint(i,
                    Math.Round(center+(random.NextDouble()+random.NextDouble()-1)*width+(i%17==0?width*2.4:0),1))).ToArray());
            series=[Spread("Europe",210,40,45),Spread("Africa",260,70,38),Spread("Americas",180,30,52)];
            title="Compare the spread, not just the average";
            desc=kind==Lumen.Charts.ChartKind.Violin
                ? "Latency samples by region · the outline is a kernel density estimate, the bar the quartiles"
                : "Latency samples by region, and Asia as a five-number summary from the warehouse · whiskers reach 1.5 interquartile ranges";
            x="Region";y="Latency (ms)";
            // Asia arrives already summarised, so a violin, which needs the observations, leaves it out.
            if(kind==Lumen.Charts.ChartKind.Box) series=[..series,new("Asia",[]){Summary=new(205,228,252,160,318,[352,371])}];
        }
        if(kind==Lumen.Charts.ChartKind.Bar) {title="Compare plans without the clutter";x="Month";}
        if(kind==Lumen.Charts.ChartKind.Scatter || kind==Lumen.Charts.ChartKind.Bubble)
        {
            title="Explore the relationship";desc="Account engagement and retention · illustrative observations"+(kind==Lumen.Charts.ChartKind.Scatter?" with a least-squares trend per series":"");x="Engagement score";y="Retention score";
            series=series.Select(s=>s with {Points=s.Points.Select(p=>p with {X=p.X*8+random.Next(6)}).ToArray(),Trend=kind==Lumen.Charts.ChartKind.Scatter}).ToArray();
        }
        var spec=new Lumen.Charts.ChartSpec{Kind=kind,Theme=theme,XAxis=xKind,SkipWeekends=weekends,TimeSkips=holidays,Title=title,Description=desc,Series=series,XLabel=x,YLabel=y,Source="Source: deterministic demonstration data · not business results",Height=420};
        if(axis==AxisDemo.Time&&TimeCapable(kind))
        {
            var start=new DateTimeOffset(2026,1,5,0,0,0,TimeSpan.Zero);
            spec=spec with{XAxis=Lumen.Charts.AxisKind.Time,TimeZone="Africa/Johannesburg",XLabel="Week beginning (Johannesburg)",Description="Weekly activity across three product plans",
                Series=series.Select(s=>s with{Points=s.Points.Select((p,i)=>p with{X=Lumen.Charts.TimeAxis.Value(start.AddDays(i*7)),Label=null}).ToArray()}).ToArray()};
        }
        // Conversion runs in percent, so it is measured against the right-hand axis.
        if(axis==AxisDemo.Numeric&&kind==Lumen.Charts.ChartKind.Line)
        {
            var conversion=new Lumen.Charts.ChartSeries("Conversion",Enumerable.Range(0,12)
                .Select(i=>new Lumen.Charts.ChartPoint(i,Math.Round(2.4+i*.28+random.NextDouble()*.5,2),Months[i])).ToArray()){Secondary=true};
            spec=spec with{Series=[..spec.Series,conversion],Y2Label="Conversion (%)"};
        }
        // A target every plan is measured against, and the window a campaign ran in.
        if(axis==AxisDemo.Numeric&&kind is Lumen.Charts.ChartKind.Line or Lumen.Charts.ChartKind.Area)
            spec=spec with{Annotations=[new(Lumen.Charts.AnnotationAxis.Y,55){Label="Target"},
                new(Lumen.Charts.AnnotationAxis.X,7){To=9,Label="Campaign"}]};
        if(axis==AxisDemo.Numeric&&kind is Lumen.Charts.ChartKind.Column or Lumen.Charts.ChartKind.Bar)
            spec=spec with{Annotations=[new(Lumen.Charts.AnnotationAxis.Y,55){Label="Target"}]};
        // Thousands of requests read as 1k and 10k.
        if(axis==AxisDemo.Log&&LogCapable(kind))
            spec=spec with{YAxis=Lumen.Charts.AxisKind.Log,YFormat=Lumen.Charts.ValueFormat.Compact,MinorGridlines=true,YLabel="Requests per minute (log scale)",Description="Traffic spanning several orders of magnitude",
                Series=spec.Series.Select((s,si)=>s with{Points=s.Points.Select((p,i)=>p with{Y=Math.Round(Math.Pow(10,si*.6+i*.3)+random.Next(1,9),2)}).ToArray()}).ToArray()};
        if(axis==AxisDemo.PowerCurve&&DurationCapable(kind))
        {
            // Two simulated two-hour rides at one sample a second: a sprint every half hour, a 20-minute effort and a 5-minute one.
            double[] Ride(double ftp)=>Enumerable.Range(0,7200).Select(t=>Math.Round(ftp*(t%1800<20?2.5:t%1800<60?1.6:t>=1800&&t<3000?1.02:t>=4000&&t<4300?1.15:.72)+random.Next(-20,21))).ToArray();
            Lumen.Charts.ChartSeries Curve(string name,IReadOnlyList<(double Seconds,double Value)> curve)=>Lumen.Charts.ChartSeries.From(name,curve,p=>p.Seconds,p=>(double?)Math.Round(p.Value));
            var recent=Lumen.Charts.Training.MeanMaximal(Ride(255),Lumen.Charts.Training.StandardDurations);
            var earlier=Lumen.Charts.Training.MeanMaximal(Ride(240),Lumen.Charts.Training.StandardDurations);
            var fit=Lumen.Charts.Training.CriticalPower(recent);
            spec=spec with{XAxis=Lumen.Charts.AxisKind.Log,XFormat=Lumen.Charts.ValueFormat.Duration,Title="Find what you can hold",
                Description="Best average power for every duration · two simulated two-hour rides",XLabel="Duration (log scale)",YLabel="Power (W)",
                Series=[Curve("This month",recent),Curve("Last month",earlier)],
                Annotations=fit is null?[]:[new(Lumen.Charts.AnnotationAxis.Y,Math.Round(fit.CriticalPower)){Label="Critical power"}]};
        }
        if(axis==AxisDemo.Pace&&DurationCapable(kind))
        {
            // A 50-minute run every 30 seconds, in seconds per kilometre: a hilly middle and a faster finish.
            var pace=Enumerable.Range(0,101).Select(i=>new Lumen.Charts.ChartPoint(i*30,Math.Round(302+16*Math.Sin(i/8.0)-(i>80?18:0)+random.Next(-5,6)))).ToArray();
            spec=spec with{XFormat=Lumen.Charts.ValueFormat.Duration,YFormat=Lumen.Charts.ValueFormat.Duration,YReversed=true,Title="Faster is higher",
                Description="Pace through a simulated 50-minute run · the axis is reversed, so a quicker kilometre sits higher",XLabel="Elapsed time",YLabel="Pace (min per km)",
                Series=[new("Pace",pace){Trend=true}],Annotations=[new(Lumen.Charts.AnnotationAxis.Y,300){Label="Target"}]};
        }
        if(axis==AxisDemo.Zones&&ZoneCapable(kind))
        {
            // An hour's run every 10 seconds: a warm-up, five intervals of four minutes hard and three easy, and a cool-down.
            // Heart rate lags the effort, closing a fifth of the gap each sample.
            var heart=Lumen.Charts.ZoneScale.CogganHeartRate(170);var beats=new double[361];var current=96.0;
            for(var i=0;i<beats.Length;i++)
            {
                var t=i*10;var target=t<600?100+t*.075:t<3000?((t-600)%420<240?185:132):118;
                current+=(target-current)*.2;beats[i]=Math.Round(current+random.Next(-2,3));
            }
            if(kind==Lumen.Charts.ChartKind.Line)
                spec=spec with{XFormat=Lumen.Charts.ValueFormat.Duration,Title="Read the effort as it happened",
                    Description="Heart rate through a simulated interval run · coloured and shaded by Coggan's five heart-rate zones at a threshold of 170 bpm",
                    XLabel="Elapsed time",YLabel="Heart rate (bpm)",YZones=heart,Annotations=[],
                    Series=[new("Heart rate",beats.Select((b,i)=>new Lumen.Charts.ChartPoint(i*10,b)).ToArray()){Zones=heart}]};
            else
            {
                var seconds=Lumen.Charts.Training.TimeInZone(beats,heart,10);
                var style=theme==Lumen.Charts.ChartTheme.Dark?Lumen.Charts.ChartStyle.Dark:Lumen.Charts.ChartStyle.Light;
                spec=spec with{YFormat=Lumen.Charts.ValueFormat.Duration,Title="See where the hour went",
                    Description="Time in each heart-rate zone during the same simulated run · each bar in its zone's colour",
                    XLabel="Zone",YLabel="Time in zone",Annotations=[],
                    Series=[new("Time in zone",heart.Zones.Select((zone,i)=>new Lumen.Charts.ChartPoint(i,seconds[i],zone.Name){Color=zone.Color??style.Zones[i]}).ToArray())]};
            }
        }
        if(axis==AxisDemo.Performance&&PerformanceCapable(kind))
        {
            // Twelve weeks of simulated training and two planned. Each week has a rest day, two hard days and a long ride,
            // and builds on the last except every fourth, which eases; the planned weeks taper towards a race.
            var start=new DateOnly(2026,6,1);
            var days=Enumerable.Range(0,98).Select(i=>{
                var week=i/7;var scale=week>=12?(week==12?.75:.45):week%4==3?.6:1+week*.04;
                var day=(i%7) switch{0=>0d,1=>65,2=>100,3=>55,4=>115,5=>175,_=>80};
                return (start.AddDays(i),day==0?0:Math.Round(day*scale+random.Next(-12,13)));}).ToArray();
            var load=Lumen.Charts.Training.Load(days,fitness:60,fatigue:60);
            double When(DateOnly day)=>Lumen.Charts.TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero));
            var planned=When(start.AddDays(84));
            spec=spec with{XAxis=Lumen.Charts.AxisKind.Time,Title="Arrive fresh",
                Description="Fitness, fatigue and form from twelve weeks of simulated training · the last two weeks are planned, so they are drawn dashed",
                XLabel="Day (UTC)",YLabel="Training stress per day",Y2Label="Form",Annotations=[],
                Series=[Lumen.Charts.ChartSeries.From("Fitness",load,d=>When(d.Day),d=>Math.Round(d.Fitness,1)) with{ProjectedFrom=planned},
                    Lumen.Charts.ChartSeries.From("Fatigue",load,d=>When(d.Day),d=>Math.Round(d.Fatigue,1)) with{ProjectedFrom=planned},
                    Lumen.Charts.ChartSeries.From("Form",load,d=>When(d.Day),d=>Math.Round(d.Form,1)) with{Kind=Lumen.Charts.ChartKind.Area,Secondary=true,ProjectedFrom=planned},
                    Lumen.Charts.ChartSeries.From("Daily stress",load,d=>When(d.Day),d=>d.Stress) with{Kind=Lumen.Charts.ChartKind.Column}]};
        }
        if(axis==AxisDemo.Target&&TargetCapable(kind))
        {
            // Twelve weeks of simulated load, every fourth an easy week, against an illustrative range of 80 to 130 percent
            // of the four weeks before it; the first week is measured against an assumed 360.
            var weekly=Enumerable.Range(0,12).Select(i=>380d+i*22+(i%4==3?-170:0)+random.Next(-40,41)).ToArray();
            double Before(int i)=>i==0?360:weekly.Skip(Math.Max(0,i-4)).Take(i-Math.Max(0,i-4)).Average();
            string Week(int i)=>new DateOnly(2026,6,1).AddDays(i*7).ToString("d MMM",System.Globalization.CultureInfo.InvariantCulture);
            spec=spec with{Title="Build without spiking",Description="Simulated weekly training stress against a range of 80 to 130 % of the four weeks before",
                XLabel="Week beginning",YLabel="Training stress per week",Annotations=[],
                Series=[new("Weekly load",weekly.Select((w,i)=>new Lumen.Charts.ChartPoint(i,w,Week(i))).ToArray()),
                    new("Target range",Enumerable.Range(0,12).Select(i=>Lumen.Charts.ChartPoint.Interval(i,Math.Round(Before(i)),Math.Round(Before(i)*.8),Math.Round(Before(i)*1.3),Week(i))).ToArray()){Kind=Lumen.Charts.ChartKind.Band}]};
        }
        return spec;
    }
    public static Lumen.Charts.GraphSpec Graph(Lumen.Charts.GraphLayout layout,Lumen.Charts.ChartTheme theme)=>new()
    {
        Title="From source to insight",Layout=layout,Theme=theme,
        Nodes=[new("sources","Sources"),new("ingest","Ingestion"),new("validate","Validation"),new("transform","Transform"),new("charts","Charts"),new("api","Chart API"),new("reports","Reports")],
        Edges=[new("sources","ingest"),new("ingest","validate"),new("validate","transform"),new("transform","charts"),new("transform","api"),new("charts","reports"),new("api","reports"),new("ingest","reports","audit trail")]
    };
}
