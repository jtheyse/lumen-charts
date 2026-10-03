namespace Lumen.Gallery;

public enum AxisDemo { Numeric, Time, Log, PowerCurve, Pace, Zones, Performance, Target, Stream }
public enum BrandDemo { Lumen, Harbour, PageCss, Midnight }
/// <summary>The theme and brand a visitor picked, kept for the circuit so they carry from one gallery page to the next.</summary>
public sealed class GalleryLook { public bool Dark { get; set; } public BrandDemo Brand { get; set; } }

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
    public static bool StreamCapable(Lumen.Charts.ChartKind kind)=>kind is Lumen.Charts.ChartKind.Line;
    public static readonly string[] Months=["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];
    public static Lumen.Charts.ChartSpec Create(Lumen.Charts.ChartKind kind,Lumen.Charts.ChartTheme theme,int revision=0,AxisDemo axis=AxisDemo.Numeric)
    {
        var random=new Random(42+revision);
        Lumen.Charts.ChartSeries Make(string name,double baseline) => new(name,Enumerable.Range(0,12).Select(i=>new Lumen.Charts.ChartPoint(i,Math.Round(baseline+i*2+random.NextDouble()*18,1),Months[i],10+random.Next(80))).ToArray());
        var series=new[]{Make("Workspace",35),Make("Enterprise",20),Make("Community",10)};
        var xKind=Lumen.Charts.AxisKind.Linear; var weekends=false; IReadOnlyList<Lumen.Charts.TimeSkip> holidays=[];
        IReadOnlyList<Lumen.Charts.ChartPane> panes=[]; var height=420;
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
            // Volume runs higher on the days the price moves most, and the average is over the last five closes.
            var volume=candles.Select(c=>new Lumen.Charts.ChartPoint(c.X,Math.Round((1_200_000+Math.Abs(c.Close!.Value-c.Open!.Value)/c.Open.Value*60_000_000+random.Next(0,300_000))/1000)*1000)).ToArray();
            var average=Lumen.Charts.Statistics.Rolling(candles.Select(c=>c.Close).ToArray(),5);
            series=[new("ACME",candles),new("Volume",volume){Kind=Lumen.Charts.ChartKind.Column,Pane=1},
                new("Five-day average",candles.Select((c,i)=>new Lumen.Charts.ChartPoint(c.X,average[i] is {} window?Math.Round(window.Mean,2):null)).ToArray()){Kind=Lumen.Charts.ChartKind.Line}];
            panes=[new(){Label="Volume (shares)",Weight=.4,YFormat=Lumen.Charts.ValueFormat.Compact}];height=520;
            title=kind==Lumen.Charts.ChartKind.Candlestick?"Follow the market's mood":"The same prices, bar by bar";
            desc=kind==Lumen.Charts.ChartKind.Candlestick
                ? "Simulated daily prices with their five-day average, and the volume traded beneath · 30 trading days, with the weekends and Good Friday left out of the axis"
                : "The same 30 trading days as the candlestick, with its average and volume · the tick on the left is the open, the one on the right the close";
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
        // The radial kinds take the zone ramp's red, gold, green and blue, which clear 3:1 on every preset's background.
        var palette=Lumen.Charts.ChartStyle.Light.Zones;
        if(kind==Lumen.Charts.ChartKind.Gauge)
        {
            series=[new("Recovery",[new(0,Math.Round(58+random.NextDouble()*30),"Recovery")])];
            title="How recovered is the body today?";desc="A recovery score in WHOOP-like tiers · red to 33 %, yellow to 66 %, green above · the tick is the seven-day average";x="";y="%";
        }
        if(kind==Lumen.Charts.ChartKind.Ring)
        {
            series=[new("Move",[new(0,Math.Round(420+random.NextDouble()*160),"kcal")],palette[5]){Goal=600},
                new("Exercise",[new(0,Math.Round(36+random.NextDouble()*20),"min")],palette[2]){Goal=30},
                new("Stand",[new(0,7+random.Next(4),"h")],palette[1]){Goal=12}];
            title="Close your rings";desc="Today's move, exercise and stand against their goals · exercise is past its goal, so its ring runs on over itself";x="";y="";
        }
        if(kind==Lumen.Charts.ChartKind.Timeline)
        {
            // One simulated night in cycles of light, deep and light sleep and REM, deep sleep giving way to REM as the night goes
            // on, with a little waking between cycles. A stage that follows itself lengthens the span before.
            var at=Lumen.Charts.TimeAxis.Value(new DateTimeOffset(2026,9,26,22,40+random.Next(0,20),0,TimeSpan.FromHours(2)));
            string[] stages=["Awake","REM","Light","Deep"];
            var spans=stages.ToDictionary(stage=>stage,_=>new List<Lumen.Charts.ChartPoint>());
            var last="";
            void Add(string stage,double minutes)
            {
                var to=at+Math.Round(minutes)*60_000;
                if(stage==last)spans[stage][^1]=spans[stage][^1] with{XEnd=to};else spans[stage].Add(Lumen.Charts.ChartPoint.Span(at,to));
                (at,last)=(to,stage);
            }
            Add("Awake",8+random.Next(8));
            for(var cycle=0;cycle<5;cycle++)
            {
                Add("Light",22+random.Next(10));
                if(cycle<4)Add("Deep",Math.Max(4,38-cycle*10+random.Next(-4,5)));
                Add("Light",10+random.Next(10));
                Add("REM",10+cycle*7+random.Next(6));
                if(cycle<4&&random.NextDouble()<.5)Add("Awake",1+random.Next(4));
            }
            Add("Awake",4+random.Next(6));
            series=stages.Select((stage,i)=>new Lumen.Charts.ChartSeries(stage,spans[stage],new[]{palette[4],palette[1],palette[0],palette[6]}[i])).ToArray();
            title="See how the night went";desc="One simulated night, stage by stage · each stage a lane, joined where sleep moves from one to the next";x="Time (Johannesburg)";y="";
            xKind=Lumen.Charts.AxisKind.Time;
        }
        if(kind==Lumen.Charts.ChartKind.Range)
        {
            // A fortnight of simulated heart rate: the lowest overnight, the highest in the day's training or an ordinary day's
            // walk, and the day's average above the lowest.
            var first=new DateOnly(2026,9,14);
            series=[new("Heart rate",Enumerable.Range(0,14).Select(d=>{
                var low=46+random.Next(8);var high=d%7 is 1 or 3 or 5?150+random.Next(30):105+random.Next(25);var day=first.AddDays(d);
                return Lumen.Charts.ChartPoint.Interval(Lumen.Charts.TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero)),low+18+random.Next(8)+(high>140?6:0),low,high,
                    day.ToString("d MMM",System.Globalization.CultureInfo.InvariantCulture));}).ToArray(),palette[5])];
            title="Read a day's heart rate at a glance";desc="Lowest to highest heart rate each day for a fortnight · the dot is the day's average";x="Day";y="Heart rate (bpm)";
            xKind=Lumen.Charts.AxisKind.Time;
        }
        if(kind==Lumen.Charts.ChartKind.Calendar)
        {
            // Sixteen weeks of simulated training from a Monday: a rest day, a run, a ride, an easy run, a short spin or another rest,
            // a long ride and a long run, building each week except every fourth, which eases, and now and then a day missed.
            var first=new DateOnly(2026,6,8);
            series=[new("Training stress",Enumerable.Range(0,16*7).Select(d=>{
                var week=d/7;var scale=week%4==3?.6:1+week*.03;
                var day=(d%7) switch{0=>0d,1=>45+random.Next(45),2=>60+random.Next(70),3=>30+random.Next(30),4=>random.NextDouble()<.5?0:30+random.Next(20),5=>150+random.Next(80),_=>70+random.Next(50)};
                var stress=day>0&&random.NextDouble()<.06?0:Math.Round(day*scale);
                return new Lumen.Charts.ChartPoint(Lumen.Charts.TimeAxis.Value(new DateTimeOffset(first.AddDays(d).ToDateTime(TimeOnly.MinValue),TimeSpan.Zero)),stress);}).ToArray())];
            title="See a season at a glance";desc="Simulated daily training stress, in tiers";x="";y="";
            xKind=Lumen.Charts.AxisKind.Time;
        }
        if(kind==Lumen.Charts.ChartKind.Blocks)
        {
            // A structured threshold ride at an FTP of 250 W: a warm-up, a build, three eight-minute intervals with four minutes
            // between them and a cool-down, each step a block as long as it lasts and as high as its target. The ride over it is
            // sampled every 15 seconds, closing most of the gap to each target and wandering a little about it.
            const double ftp=250;
            (string Name,double Minutes,double Fraction)[] steps=[("Warm-up",10,.55),("Build",5,.75),("Interval 1",8,1),("Recovery",4,.5),("Interval 2",8,1),("Recovery",4,.5),("Interval 3",8,1),("Cool-down",8,.45)];
            var plan=new List<Lumen.Charts.ChartPoint>();var at=0d;
            foreach(var (name,minutes,fraction) in steps){plan.Add(Lumen.Charts.ChartPoint.Block(at,at+minutes*60,Math.Round(ftp*fraction),name));at+=minutes*60;}
            var ride=new List<Lumen.Charts.ChartPoint>();var watts=110d;
            for(var t=0d;t<=at;t+=15){watts+=(plan.Last(step=>step.X<=t).Y!.Value-watts)*.45;ride.Add(new(t,Math.Round(watts+random.Next(-14,15))));}
            // The ride is drawn in the palette's rose, which none of the levels a threshold workout reaches is drawn in.
            series=[new("Plan",plan){Zones=Lumen.Charts.ZoneScale.CogganPower(ftp)},new("Power",ride,Lumen.Charts.ChartStyle.Light.Series[4]){Kind=Lumen.Charts.ChartKind.Line}];
            title="Hold the plan";desc="A simulated threshold ride in Coggan's power levels";x="Elapsed time";y="Power (W)";
        }
        if(kind==Lumen.Charts.ChartKind.Bar) {title="Compare plans without the clutter";x="Month";}
        if(kind==Lumen.Charts.ChartKind.Scatter || kind==Lumen.Charts.ChartKind.Bubble)
        {
            title="Explore the relationship";desc="Account engagement and retention · illustrative observations"+(kind==Lumen.Charts.ChartKind.Scatter?" with a least-squares trend per series":"");x="Engagement score";y="Retention score";
            series=series.Select(s=>s with {Points=s.Points.Select(p=>p with {X=p.X*8+random.Next(6)}).ToArray(),Trend=kind==Lumen.Charts.ChartKind.Scatter}).ToArray();
        }
        var spec=new Lumen.Charts.ChartSpec{Kind=kind,Theme=theme,XAxis=xKind,SkipWeekends=weekends,TimeSkips=holidays,Title=title,Description=desc,Series=series,XLabel=x,YLabel=y,Source="Source: deterministic demo data · not real results",Height=height,Panes=panes};
        if(kind==Lumen.Charts.ChartKind.Timeline) spec=spec with{TimeZone="Africa/Johannesburg"};
        // The workout's power rises from zero, as Zwift and TrainingPeaks draw it, and the threshold is a reference line.
        if(kind==Lumen.Charts.ChartKind.Blocks)
            spec=spec with{XFormat=Lumen.Charts.ValueFormat.Duration,IncludeZero=true,Annotations=[new(Lumen.Charts.AnnotationAxis.Y,250){Label="FTP"}]};
        // The tiers are the Sports & performance page's, in the zone ramp's blue, green, gold and red, and today is the last day.
        if(kind==Lumen.Charts.ChartKind.Calendar)
            spec=spec with{YZones=SportsData.StressTiers(palette),Annotations=[new(Lumen.Charts.AnnotationAxis.X,series[0].Points[^1].X){Label="Today"}]};
        if(kind==Lumen.Charts.ChartKind.Gauge)
            spec=spec with{YZones=new([new("Low",33,palette[5]),new("Moderate",66,palette[3]),new("Good",double.PositiveInfinity,palette[2])]),
                Annotations=[new(Lumen.Charts.AnnotationAxis.Y,Math.Round(52+random.NextDouble()*16)){Label="7-day average"}]};
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
                Series=[Lumen.Charts.ChartSeries.From("Fitness",load,d=>When(d.Day),d=>Math.Round(d.Fitness,1)) with{ProjectedFrom=planned,HighlightLast=true},
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
        if(axis==AxisDemo.Stream&&StreamCapable(kind))
        {
            // An hour's run every 10 seconds: a warm-up, five intervals of four minutes hard and three easy, and a cool-down, over
            // rolling ground. Heart rate closes a fifth of the gap to the effort each sample, and pace half of it.
            var heart=Lumen.Charts.ZoneScale.CogganHeartRate(170);var beats=new double[361];var paces=new double[361];var climb=new double[361];
            double current=96,stride=390;
            for(var i=0;i<361;i++)
            {
                var t=i*10;var hard=t>=600&&t<3000&&(t-600)%420<240;
                current+=((t<600?100+t*.075:t<3000?hard?185:132:118)-current)*.2;beats[i]=Math.Round(current+random.Next(-2,3));
                stride+=((t<600?390-t*.1:t<3000?hard?248:345:380)-stride)*.5;paces[i]=Math.Round(stride+random.Next(-4,5));
                climb[i]=Math.Round(20+14*Math.Sin(t/700.0)+5*Math.Sin(t/190.0),1);
            }
            Lumen.Charts.ChartPoint[] Over(double[] values)=>values.Select((v,i)=>new Lumen.Charts.ChartPoint(i*10,v)).ToArray();
            // The line takes each zone's colour from the middle of its band, and blends between them.
            var ramp=Lumen.Charts.ChartStyle.Light.Zones;
            Lumen.Charts.ColorStop[] effort=[new(105,ramp[0]),new(128,ramp[1]),new(150,ramp[2]),new(169,ramp[3]),new(185,ramp[4])];
            spec=spec with{XFormat=Lumen.Charts.ValueFormat.Duration,Height=640,Title="See the whole run at once",
                Description="Heart rate, pace and climb through a simulated interval run · three panes share the elapsed time, so zooming moves them together · heart rate coloured by its value",
                XLabel="Elapsed time",YLabel="Heart rate (bpm)",YZones=heart,Annotations=[new(Lumen.Charts.AnnotationAxis.X,600){To=3000,Label="Intervals"}],
                Panes=[new(){Label="Pace (min/km)",Weight=.6,YFormat=Lumen.Charts.ValueFormat.Duration,YReversed=true},new(){Label="Climb (m)",Weight=.4}],
                Series=[new("Heart rate",Over(beats)){Gradient=effort,Markers=Lumen.Charts.MarkerStyle.None},new("Pace",Over(paces)){Pane=1,Markers=Lumen.Charts.MarkerStyle.None},
                    new("Climb",Over(climb)){Pane=2,Kind=Lumen.Charts.ChartKind.Area,Curve=Lumen.Charts.LineCurve.Smooth,Fill=Lumen.Charts.AreaFill.Fade,Markers=Lumen.Charts.MarkerStyle.None}]};
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
