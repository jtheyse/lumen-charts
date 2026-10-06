using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Lumen.Charts;
using Lumen.Charts.Blazor;
using Lumen.Gallery;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

var failures=new List<string>();var passed=0;
void Check(bool condition,string message="Assertion failed") { if(!condition)throw new Exception(message); }
void Test(string name,Action action) {try{action();Console.WriteLine($"PASS {name}");passed++;}catch(Exception e){failures.Add(name+": "+e.Message);Console.WriteLine($"FAIL {name}: {e.Message}");}}
void Reject(Action action) {try{action();}catch(ArgumentException){return;}throw new Exception("Expected ArgumentException");}
ChartSpec Spec(ChartKind kind=ChartKind.Line)=>new(){Title="Example",Kind=kind,Series=[new("Series",[new(0,2,"A"),new(1,5,"B"),new(2,3,"C")])]};
XNamespace ns="http://www.w3.org/2000/svg";
XDocument Svg(ChartSpec spec)=>XDocument.Parse(ChartSvg.Render(spec));
// 0.24.0 made the refined finish the default. Tests written against 0.23.0's look, such as 2.5-pixel strokes, visible line
// markers, solid gridlines, square legend keys and reference labels inside their groups, draw in the classic finish, where
// that look holds; the refined finish has tests of its own at the end.
ChartSpec Classic(ChartSpec spec)=>spec with{Style=ChartSvg.ResolveStyle(spec) with{Finish=ChartFinish.Classic}};
ChartSpec Sample(ChartKind kind)=>kind switch{
    ChartKind.Candlestick or ChartKind.Ohlc=>Spec(kind) with{Series=[new("Price",[ChartPoint.Candle(0,10,12,9,11),ChartPoint.Candle(1,11,13,10,10.5),ChartPoint.Candle(2,10.5,11,8,9)])]},
    ChartKind.Band=>Spec(kind) with{Series=[new("Forecast",[ChartPoint.Interval(0,2,1,3),ChartPoint.Interval(1,5,4,6),ChartPoint.Interval(2,3,2,4)])]},
    ChartKind.Histogram or ChartKind.Box or ChartKind.Violin=>Spec(kind) with{Series=[new("Sample",Enumerable.Range(0,40).Select(i=>new ChartPoint(i,i%7+1)).ToArray())]},
    ChartKind.Gauge=>Spec(kind) with{Series=[new("Recovery",[new(0,72,"Recovery")])]},
    ChartKind.Ring=>Spec(kind) with{Series=[new("Move",[new(0,540,"kcal")]){Goal=600},new("Exercise",[new(0,47,"min")]){Goal=30},new("Stand",[new(0,9,"h")]){Goal=12}]},
    ChartKind.Timeline=>Spec(kind) with{Series=[new("Awake",[ChartPoint.Span(0,10),ChartPoint.Span(60,65)]),new("Light",[ChartPoint.Span(10,40),ChartPoint.Span(50,60)]),new("Deep",[ChartPoint.Span(40,50)])]},
    ChartKind.Range=>Spec(kind) with{Series=[new("Heart rate",[ChartPoint.Interval(0,70,50,150,"A"),ChartPoint.Interval(1,null,55,130,"B"),ChartPoint.Interval(2,64,48,170,"C")])]},
    ChartKind.Calendar=>Spec(kind) with{XAxis=AxisKind.Time,Series=[new("Stress",[new(Utc(2026,9,14,12),40,"Easy run"),new(Utc(2026,9,15,12),0),new(Utc(2026,9,16,12),120)])]},
    ChartKind.Blocks=>Spec(kind) with{Series=[new("Plan",[ChartPoint.Block(0,2,140,"Warm-up"),ChartPoint.Block(2,5,250,"Interval"),ChartPoint.Block(5,6,120,"Recovery")])]},
    _=>Spec(kind)};
foreach(var kind in Enum.GetValues<ChartKind>())
{
    Test($"{kind}: valid SVG and accessible marks",()=>{
        var doc=Svg(Sample(kind));Check(doc.Root!.Name==ns+"svg");Check(doc.Descendants(ns+"title").Any());
        Check(doc.Descendants().Any(e=>(string?)e.Attribute("class")=="lumen-datum"));
        // Histogram bins, box glyphs and violins are aggregates: focusable and labelled, but not observation indices.
        Check(kind is ChartKind.Histogram or ChartKind.Box or ChartKind.Violin || doc.Descendants().Any(e=>e.Attribute("data-point") is not null));
        Check(!doc.ToString().Contains("NaN")&&!doc.ToString().Contains("Infinity"));
    });
    Test($"{kind}: empty data",()=>Check(ChartSvg.Render(new(){Kind=kind}).Contains("No data to display")));
}
Test("SVG export includes a visible series legend",()=>{
    var doc=Svg(Spec() with{Series=[new("Revenue",[new(0,3)]),new("Costs",[new(0,2)])]});
    Check(doc.Descendants(ns+"text").Any(e=>e.Value=="Revenue"));
    Check(doc.Descendants(ns+"text").Any(e=>e.Value=="Costs"));
});
Test("Bubble area uses a shared scale across series",()=>{
    var doc=Svg(Spec(ChartKind.Bubble) with{Series=[new("Small",[new(0,2,Size:10)]),new("Large",[new(1,3,Size:100)])]});
    var radii=doc.Descendants(ns+"g").Where(e=>e.Attribute("data-point") is not null).Select(e=>double.Parse(e.Element(ns+"circle")!.Attribute("r")!.Value,CultureInfo.InvariantCulture)).ToArray();
    Check(Math.Abs(radii[1]*radii[1]/(radii[0]*radii[0])-10)<1e-6);
});
Test("SVG styles are isolated from host styles and other themes",()=>{
    var doc=Svg(Spec() with{Theme=ChartTheme.Dark});
    var style=doc.Descendants(ns+"style").Single().Value;
    Check(style.Contains(".lumen-svg .lumen-grid"));
    Check(style.Contains("var(--lumen-grid)"));
    Check(doc.Root!.Attribute("style")!.Value.Contains("--lumen-grid:#303B50"));
});
Test("Zero scale is nondegenerate",()=>{var scale=LinearScale.Create([0,0],true);Check(scale.Max>scale.Min);Check(double.IsFinite(scale.Map(0,0,100)));});
Test("Linear scale preserves proportions",()=>{var scale=LinearScale.Create([0,100],true);Check(scale.Map(25,0,200)==50);});
Test("Negative-only bars include zero",()=>{var doc=Svg(Spec(ChartKind.Column) with{Series=[new("S",[new(0,-2),new(1,-4)])]});Check(doc.Descendants(ns+"text").Any(e=>e.Value=="0"));});
Test("Magnitude chart refuses truncated baseline",()=>Reject(()=>ChartSvg.Render(Spec(ChartKind.Column) with{YMin=1})));
Test("Reject NaN",()=>Reject(()=>ChartSvg.Render(Spec() with{Series=[new("S",[new(double.NaN,2)])]})));
Test("Reject infinity",()=>Reject(()=>ChartSvg.Render(Spec() with{Series=[new("S",[new(0,double.PositiveInfinity)])]})));
Test("Reject invalid bounds",()=>Reject(()=>ChartSvg.Render(Spec() with{XMin=2,XMax=1})));
Test("Reject one-sided bounds outside extent",()=>Reject(()=>ChartSvg.Render(Spec() with{YMin=10})));
Test("Reject unsorted line",()=>Reject(()=>ChartSvg.Render(Spec() with{Series=[new("S",[new(2,2),new(1,3)])]})));
Test("Reject negative donut",()=>Reject(()=>ChartSvg.Render(Spec(ChartKind.Donut) with{Series=[new("S",[new(0,-1)])]})));
Test("Donut zero total",()=>Check(ChartSvg.Render(Spec(ChartKind.Donut) with{Series=[new("S",[new(0,0)])]}).Contains("No positive values")));
Test("Single donut slice finite",()=>Svg(Spec(ChartKind.Donut) with{Series=[new("S",[new(0,5)])]}));
Test("Reject negative bubble size",()=>Reject(()=>ChartSvg.Render(Spec(ChartKind.Bubble) with{Series=[new("S",[new(0,1,Size:-1)])]})));
Test("Reject duplicate categories",()=>Reject(()=>ChartSvg.Render(Spec(ChartKind.Column) with{Series=[new("S",[new(0,1),new(0,2)])]})));
Test("Reject incomplete radar",()=>Reject(()=>ChartSvg.Render(Spec(ChartKind.Radar) with{Series=[Spec().Series[0],new("Missing",[new(0,1)])]})));
Test("Reject null series",()=>Reject(()=>ChartSvg.Render(Spec() with{Series=null!})));
Test("Reject unknown chart enum",()=>Reject(()=>ChartSvg.Render(Spec() with{Kind=(ChartKind)123})));
Test("Reject oversized dimensions",()=>Reject(()=>ChartSvg.Render(Spec() with{Width=10000})));
Test("Reject color injection",()=>Reject(()=>ChartSvg.Render(Spec() with{Series=[new("S",[new(0,1)],"red' onload='alert(1)")]})));
Test("Escape labels and title",()=>{
    var doc=Svg(Spec() with{Title="<script>alert('x')</script>",Series=[new("<img src=x onerror=alert(1)>",[new(0,2,"<svg onload='bad'>")])]});
    Check(!doc.Descendants(ns+"script").Any());Check(!doc.Descendants().Attributes().Any(a=>a.Name.LocalName.StartsWith("on")));
});
Test("Missing values split line paths",()=>{
    var doc=Svg(Classic(Spec() with{Series=[new("S",[new(0,2),new(1,3),new(2,null),new(3,1),new(4,2)])]}));
    Check(doc.Descendants(ns+"path").Count(e=>(string?)e.Attribute("stroke-width")=="2.5")==2);
    Check(doc.Descendants().Count(e=>e.Attribute("data-point") is not null)==4);
});
Test("Stacked positive and negative sums",()=>{
    var doc=Svg(Spec(ChartKind.StackedColumn) with{Series=[new("A",[new(0,4),new(1,-3)]),new("B",[new(0,6),new(1,-7)])]});
    var labels=doc.Descendants(ns+"text").Select(e=>e.Value).ToArray();Check(labels.Contains("10")&&labels.Contains("-10"));
    Check(doc.Descendants(ns+"rect").All(e=>double.Parse(e.Attribute("height")!.Value,CultureInfo.InvariantCulture)>=0));
});
Test("Chart captions group numbers invariantly",()=>{
    var previous=CultureInfo.CurrentCulture;
    try {
        CultureInfo.CurrentCulture=new("fr-FR");
        var doc=Svg(Spec(ChartKind.Scatter) with{DensityCells=40,Series=[new("Cloud",
            Enumerable.Range(0,5000).Select(i=>new ChartPoint(i%320,Math.Sin(i*.03)*40+i%17)).ToArray())]});
        // A host culture that groups with spaces must not change what the chart reads.
        Check(doc.Descendants(ns+"text").Any(t=>t.Value.Contains("5,000 observations aggregated into 40 cells")));
        Check(doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-datum")
            .All(e=>!e.Attribute("aria-label")!.Value.Contains(' ')&&!e.Attribute("aria-label")!.Value.Contains(' ')));
    } finally {CultureInfo.CurrentCulture=previous;}
});
Test("Invariant SVG decimal formatting",()=>{
    var previous=CultureInfo.CurrentCulture;try {CultureInfo.CurrentCulture=new("fr-FR");var doc=Svg(Spec());Check(doc.Descendants(ns+"circle").All(e=>!e.Attribute("cx")!.Value.Contains(',')));}finally{CultureInfo.CurrentCulture=previous;}
});
Test("Sampling retains spike, trough and endpoints",()=>{
    var data=Enumerable.Range(0,10000).Select(i=>new ChartPoint(i,i==1234?1000:i==4567?-1000:0)).ToArray();
    var sample=Sampling.MinMax(data,100);Check(sample.Count<=100);Check(sample.Contains(0)&&sample.Contains(9999)&&sample.Contains(1234)&&sample.Contains(4567));
});
Test("CSV quotes and formula protection",()=>{
    var csv=ChartExport.Csv(Spec() with{Series=[new("=CMD()",[new(0,null,"comma, and \"quote\""),new(1,2,"  @SUM(A1)")])]});
    Check(csv.Contains("\"'=CMD()\""));Check(csv.Contains("\"comma, and \"\"quote\"\"\""));Check(csv.Contains("\"'  @SUM(A1)\""));
});
Test("CSV preserves original data beyond sampling",()=>{
    var spec=Spec() with{MaxRenderedPoints=16,Series=[new("S",Enumerable.Range(0,2000).Select(i=>new ChartPoint(i,i)).ToArray())]};
    Check(Svg(spec).Descendants().Count(e=>e.Attribute("data-point") is not null)<=16);
    Check(ChartExport.Csv(spec).Split('\n',StringSplitOptions.RemoveEmptyEntries).Length==2001);
});
GraphSpec Graph()=>new(){Nodes=[new("a","Start"),new("b","Middle"),new("c","End")],Edges=[new("a","b"),new("b","c")]};
Test("Layered layout respects edge direction",()=>{var positions=GraphEngine.Layout(Graph()).ToDictionary(p=>p.Id);Check(positions["a"].X<positions["b"].X&&positions["b"].X<positions["c"].X);});
Test("Graph layout deterministic",()=>Check(GraphEngine.Layout(Graph()).SequenceEqual(GraphEngine.Layout(Graph()))));
Test("Graph endpoints validated",()=>Reject(()=>GraphEngine.Layout(Graph() with{Edges=[new("a","missing")]})));
Test("Duplicate node IDs rejected",()=>Reject(()=>GraphEngine.Layout(Graph() with{Nodes=[new("a","A"),new("a","B")]})));
Test("Layered cycles rejected",()=>Reject(()=>GraphEngine.Layout(Graph() with{Edges=[new("a","b"),new("b","a")]})));
Test("Circular cycles supported",()=>Check(GraphEngine.Layout(Graph() with{Layout=GraphLayout.Circular,Edges=[new("a","b"),new("b","a")]}).Count==3));
Test("Graph SVG valid",()=>Check(XDocument.Parse(GraphEngine.Render(Graph())).Descendants(ns+"circle").Count()==3));
Test("Empty graph supported",()=>Check(GraphEngine.Layout(new()).Count==0));
Test("Graph self loop has a curved path",()=>Check(XDocument.Parse(GraphEngine.Render(new(){Nodes=[new("a","A")],Edges=[new("a","a")],Layout=GraphLayout.Circular})).Descendants(ns+"path").Any()));
Test("100k line input bounded render",()=>{
    var spec=Spec() with{Series=[new("Large",Enumerable.Range(0,100000).Select(i=>new ChartPoint(i,Math.Sin(i*.01))).ToArray())]};
    var timer=Stopwatch.StartNew();var svg=ChartSvg.Render(spec);timer.Stop();
    Check(XDocument.Parse(svg).Descendants().Count(e=>e.Attribute("data-point") is not null)<=1200);
    Console.WriteLine($"  Diagnostic: 100k-point SVG generated in {timer.ElapsedMilliseconds} ms; {svg.Length:N0} characters (not a browser benchmark).");
});
Test("Blazor prerender includes controls and SVG",()=>{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        var html=renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<LumenChart>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",Spec()}}));return root.ToHtmlString();
        }).GetAwaiter().GetResult();
        Check(html.Contains("<svg")&&html.Contains("View data")&&html.Contains("Export SVG")&&html.Contains("aria-pressed=\"true\""));
        Check(html.Contains("Export PNG"));
        // The component supplies its own tooltips, so the prerendered marks carry labels without native titles.
        Check(html.Contains("aria-label='Series: A, 2'")&&!html.Contains("<title>Series: A, 2</title>"));
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
});
double Utc(int year,int month,int day,int hour=0)=>TimeAxis.Value(new DateTimeOffset(year,month,day,hour,0,0,TimeSpan.Zero));
ChartSpec TimeSpec(double start,double step,int count)=>Spec() with{XAxis=AxisKind.Time,Series=[new("Signal",Enumerable.Range(0,count).Select(i=>new ChartPoint(start+i*step,i+1)).ToArray())]};
Test("Time axis round trips a moment",()=>{
    var moment=new DateTimeOffset(2026,9,10,13,45,30,TimeSpan.Zero);
    Check(TimeAxis.Moment(TimeAxis.Value(moment))==moment);
});
Test("Time ticks inside a day fall on clock boundaries",()=>{
    var ticks=new Axis(AxisKind.Time,Utc(2026,9,10,8),Utc(2026,9,10,14)).Ticks();
    Check(ticks.Count>=3);
    Check(ticks.All(t=>t.Value%3600000==0));
    Check(ticks.All(t=>t.Label.Length==5&&t.Label[2]==':'));
});
Test("Time ticks across weeks label days",()=>{
    var ticks=new Axis(AxisKind.Time,Utc(2026,1,1),Utc(2026,1,20)).Ticks();
    Check(ticks.Count>=3);
    Check(ticks.All(t=>TimeAxis.Moment(t.Value).UtcDateTime.TimeOfDay==TimeSpan.Zero));
    Check(ticks.All(t=>t.Label.Contains("Jan")));
});
Test("Time ticks across a year fall on month starts",()=>{
    var ticks=new Axis(AxisKind.Time,Utc(2026,1,1),Utc(2026,12,31)).Ticks();
    Check(ticks.Count>=3);
    Check(ticks.All(t=>TimeAxis.Moment(t.Value).UtcDateTime.Day==1));
    Check(ticks.All(t=>t.Label.EndsWith("2026")));
});
Test("Time ticks across decades fall on January years",()=>{
    var ticks=new Axis(AxisKind.Time,Utc(2000,1,1),Utc(2026,1,1)).Ticks();
    Check(ticks.Count>=3);
    Check(ticks.All(t=>{var m=TimeAxis.Moment(t.Value).UtcDateTime;return m.Month==1&&m.Day==1;}));
    Check(ticks.All(t=>t.Label.Length==4&&int.Parse(t.Label)%5==0));
});
Test("Time labels are culture invariant",()=>{
    var previous=CultureInfo.CurrentCulture;
    try {
        CultureInfo.CurrentCulture=new("fr-FR");
        var axis=new Axis(AxisKind.Time,Utc(2026,1,1),Utc(2026,12,31));
        Check(axis.Ticks().Any(t=>t.Label=="Jan 2026"));
        Check(axis.Format(Utc(2026,3,5))=="5 Mar 2026");
    } finally {CultureInfo.CurrentCulture=previous;}
});
Test("Time axis chart renders date ticks and date tooltips",()=>{
    var doc=Svg(TimeSpec(Utc(2026,1,1),30*86400000d,12));
    Check(doc.Descendants(ns+"text").Count(e=>e.Value.Contains("2026"))>=3);
    var marks=doc.Descendants(ns+"g").Where(e=>e.Attribute("data-point") is not null).ToArray();
    Check(marks.Length==12&&marks.All(e=>e.Attribute("aria-label")!.Value.Contains("2026")));
});
Test("CSV adds an ISO timestamp column for time axes",()=>{
    var csv=ChartExport.Csv(TimeSpec(Utc(2026,9,10),3600000d,2));
    Check(csv.StartsWith("Series,X,XTime,Y,Label,Size"));
    Check(csv.Contains("2026-09-10T00:00:00.000Z")&&csv.Contains("2026-09-10T01:00:00.000Z"));
    Check(!ChartExport.Csv(Spec()).Contains("XTime"));
});
Test("Log axis spaces decades evenly",()=>{
    var axis=Axis.Create(AxisKind.Log,[1,1000]);
    Check(Math.Abs(axis.Map(1,0,3))<1e-9&&Math.Abs(axis.Map(10,0,3)-1)<1e-9&&Math.Abs(axis.Map(100,0,3)-2)<1e-9);
    Check(Math.Abs(axis.Invert(axis.Map(42,0,3),0,3)-42)<1e-9);
});
Test("Log ticks are powers of ten over wide ranges",()=>{
    var ticks=Axis.Create(AxisKind.Log,[1,10000]).Ticks();
    Check(ticks.Select(t=>t.Value).SequenceEqual([1d,10,100,1000,10000]));
});
Test("Log ticks add intermediate steps over short ranges",()=>{
    var values=Axis.Create(AxisKind.Log,[1,10]).Ticks().Select(t=>t.Value).ToArray();
    Check(values.Contains(2d)&&values.Contains(5d)&&values.Contains(10d));
});
Test("Log chart renders finite geometry",()=>{
    var doc=Svg(Classic(Spec(ChartKind.Scatter) with{YAxis=AxisKind.Log,Series=[new("S",[new(1,0.01),new(2,1),new(3,1000)])]}));
    Check(doc.Descendants(ns+"circle").All(e=>double.Parse(e.Attribute("cy")!.Value,CultureInfo.InvariantCulture) is >=0 and <=420));
});
Test("Reject nonpositive values on a log axis",()=>{
    Reject(()=>ChartSvg.Render(Spec() with{YAxis=AxisKind.Log,Series=[new("S",[new(1,1),new(2,0)])]}));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Scatter) with{XAxis=AxisKind.Log,Series=[new("S",[new(0,1)])]}));
});
Test("Reject log axes on magnitude and radial charts",()=>{
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Area) with{YAxis=AxisKind.Log}));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Column) with{YAxis=AxisKind.Log}));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Donut) with{XAxis=AxisKind.Time}));
});
Test("Reject a time axis on Y and unknown axis kinds",()=>{
    Reject(()=>ChartSvg.Render(Spec() with{YAxis=AxisKind.Time}));
    Reject(()=>ChartSvg.Render(Spec() with{XAxis=(AxisKind)9}));
});
Test("Reject log bounds and zero inclusion that a log axis cannot show",()=>{
    Reject(()=>ChartSvg.Render(Spec() with{YAxis=AxisKind.Log,YMin=0}));
    Reject(()=>ChartSvg.Render(Spec() with{YAxis=AxisKind.Log,IncludeZero=true}));
});
Test("Reject time values outside the representable range",()=>Reject(()=>ChartSvg.Render(TimeSpec(1e15,1000,3))));
Test("Quantiles interpolate between order statistics",()=>{
    double[] sorted=[1,2,3,4];
    Check(Statistics.Quantile(sorted,.25)==1.75&&Statistics.Quantile(sorted,.5)==2.5&&Statistics.Quantile(sorted,.75)==3.25);
    Check(Statistics.Quantile(sorted,0)==1&&Statistics.Quantile(sorted,1)==4);
    Check(Statistics.Quantile([7d],.5)==7);
});
Test("Summaries use Tukey fences for whiskers and outliers",()=>{
    var summary=Statistics.Summarize([1d,2,3,4,5,6,7,8,9,100]);
    Check(summary.Q1==3.25&&summary.Median==5.5&&summary.Q3==7.75);
    Check(summary.LowerWhisker==1&&summary.UpperWhisker==9);
    Check(summary.Outliers.SequenceEqual([100d]));
});
Test("Bins cover the range and count every observation",()=>{
    var values=Enumerable.Range(0,100).Select(i=>(double)i).ToArray();
    var bins=Statistics.Bins(values,10);
    Check(bins.Count==10&&bins[0].Start==0&&bins[^1].End==99);
    Check(bins.Sum(b=>b.Count)==100);
    Check(bins.Select(b=>Math.Round(b.End-b.Start,9)).Distinct().Count()==1);
});
Test("Automatic bin counts stay within the supported range",()=>{
    var bins=Statistics.Bins(Enumerable.Range(0,1000).Select(i=>(double)(i%50)).ToArray());
    Check(bins.Count is > 1 and <= Statistics.MaxBins);
    Check(bins.Sum(b=>b.Count)==1000);
});
Test("Identical observations still produce one bin",()=>{
    var bins=Statistics.Bins([5d,5,5],null);
    Check(bins.Count==1&&bins[0].Start==4.5&&bins[0].End==5.5&&bins[0].Count==3);
});
Test("Reject empty or out-of-range statistics input",()=>{
    Reject(()=>Statistics.Summarize([]));
    Reject(()=>Statistics.Bins([],null));
    Reject(()=>Statistics.Bins([1d,2],0));
    Reject(()=>Statistics.Bins([1d,2],Statistics.MaxBins+1));
});
ChartSpec Candles()=>Spec(ChartKind.Candlestick) with{Series=[new("Price",[
    ChartPoint.Candle(0,10,12.5,9.5,11.8),ChartPoint.Candle(1,11.8,12,9,9.4),ChartPoint.Candle(2,9.4,13,9.2,12.6)])]};
Test("Candlestick draws a wick and a directional body for each period",()=>{
    var doc=Svg(Candles());
    var marks=doc.Descendants(ns+"g").Where(e=>e.Attribute("data-point") is not null).ToArray();
    Check(marks.Length==3);
    Check(marks.All(m=>m.Elements(ns+"line").Count()==1&&m.Elements(ns+"rect").Count()==1));
    var fills=marks.Select(m=>(string?)m.Element(ns+"rect")!.Attribute("fill")).ToArray();
    Check(fills[0]==ChartSvg.RisingColor&&fills[1]==ChartSvg.FallingColor&&fills[2]==ChartSvg.RisingColor);
    Check(marks[0].Attribute("aria-label")!.Value.Contains("open 10")&&marks[0].Attribute("aria-label")!.Value.Contains("close 11.8"));
});
Test("Candlestick wicks span the full high-low range",()=>{
    var doc=Svg(Candles());
    var mark=doc.Descendants(ns+"g").First(e=>(string?)e.Attribute("data-point")=="2");
    var wick=mark.Element(ns+"line")!;var body=mark.Element(ns+"rect")!;
    double top=double.Parse(wick.Attribute("y1")!.Value,CultureInfo.InvariantCulture),bottom=double.Parse(wick.Attribute("y2")!.Value,CultureInfo.InvariantCulture);
    double bodyTop=double.Parse(body.Attribute("y")!.Value,CultureInfo.InvariantCulture);
    Check(top<bodyTop&&bodyTop<bottom);
});
Test("Candlestick accepts a time X axis and a log Y axis",()=>{
    var start=TimeAxis.Value(new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero));
    var doc=Svg(Candles() with{XAxis=AxisKind.Time,YAxis=AxisKind.Log,Series=[new("Price",[
        ChartPoint.Candle(start,10,12,9,11),ChartPoint.Candle(start+30*86400000d,11,140,10,130),ChartPoint.Candle(start+60*86400000d,130,1400,120,1200)])]});
    Check(doc.Descendants(ns+"text").Any(e=>e.Value.Contains("Jan")));
    Check(doc.Descendants(ns+"text").Any(e=>e.Value=="100"));
});
Test("Reject candles that are incomplete, inconsistent or duplicated",()=>{
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Candlestick) with{Series=[new("P",[new(0,1)])]}));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Candlestick) with{Series=[new("P",[ChartPoint.Candle(0,10,10.5,9,11)])]}));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Candlestick) with{Series=[new("P",[ChartPoint.Candle(0,10,12,10.5,11)])]}));
    Reject(()=>ChartSvg.Render(Candles() with{Series=[Candles().Series[0],Candles().Series[0]]}));
    Reject(()=>ChartSvg.Render(Candles() with{YAxis=AxisKind.Log,Series=[new("P",[ChartPoint.Candle(0,1,2,0,1.5)])]}));
});
Test("Candlestick CSV exports the four prices",()=>{
    var csv=ChartExport.Csv(Candles());
    Check(csv.StartsWith("Series,X,Y,Label,Size,Open,High,Low,Close"));
    Check(csv.Contains("10,12.5,9.5,11.8"));
});
ChartSpec Bands()=>Spec(ChartKind.Band) with{Series=[new("Forecast",[
    ChartPoint.Interval(0,10,8,12),ChartPoint.Interval(1,12,9,15),ChartPoint.Interval(2,11,7,16)])]};
Test("Band fills the interval and keeps the central line",()=>{
    var doc=Svg(Classic(Bands()));
    Check(doc.Descendants(ns+"path").Count(e=>(string?)e.Attribute("fill-opacity")==".16")==1);
    Check(doc.Descendants(ns+"path").Count(e=>(string?)e.Attribute("stroke-width")=="2.5")==1);
    Check(doc.Descendants(ns+"g").Where(e=>e.Attribute("data-point") is not null).All(e=>e.Attribute("aria-label")!.Value.Contains("band")));
});
Test("Missing band bounds split the interval",()=>{
    var doc=Svg(Bands() with{Series=[new("Forecast",[
        ChartPoint.Interval(0,10,8,12),ChartPoint.Interval(1,12,9,15),new(2,11),ChartPoint.Interval(3,9,7,11),ChartPoint.Interval(4,10,8,12)])]});
    Check(doc.Descendants(ns+"path").Count(e=>(string?)e.Attribute("fill-opacity")==".16")==2);
});
Test("Reject half-specified or inverted band bounds",()=>{
    Reject(()=>ChartSvg.Render(Bands() with{Series=[new("F",[new(0,1){Low=1}])]}));
    Reject(()=>ChartSvg.Render(Bands() with{Series=[new("F",[ChartPoint.Interval(0,1,5,2)])]}));
});
Test("Band CSV exports the interval bounds",()=>{
    var csv=ChartExport.Csv(Bands());
    Check(csv.StartsWith("Series,X,Y,Label,Size,Low,High"));
    Check(csv.Contains(",8,12"));
});
ChartSpec Distribution(int bins)=>Spec(ChartKind.Histogram) with{Bins=bins,Series=[new("Latency",
    Enumerable.Range(0,120).Select(i=>new ChartPoint(i,i%20+1)).ToArray())]};
Test("Histogram renders one bar per bin over a zero baseline",()=>{
    var doc=Svg(Distribution(6));
    Check(doc.Descendants(ns+"rect").Count()==6);
    Check(doc.Descendants(ns+"g").Count(e=>(string?)e.Attribute("class")=="lumen-datum")==6);
    Check(doc.Descendants(ns+"text").Any(e=>e.Value=="0"));
    Check(doc.Descendants(ns+"text").Any(e=>e.Value.Contains("120 observations in 6 equal-width bins")));
});
Test("Histogram bar heights follow the bin counts",()=>{
    var counts=Statistics.Bins(Enumerable.Range(0,120).Select(i=>(double)(i%20+1)).ToArray(),4).Select(b=>b.Count).ToArray();
    var doc=Svg(Distribution(4));
    var heights=doc.Descendants(ns+"rect").Select(e=>double.Parse(e.Attribute("height")!.Value,CultureInfo.InvariantCulture)).ToArray();
    Check(heights.Length==4);
    for(var i=1;i<4;i++)Check(Math.Sign(heights[i]-heights[i-1])==Math.Sign(counts[i]-counts[i-1]));
});
Test("Reject histogram specifications the binning cannot honor",()=>{
    Reject(()=>ChartSvg.Render(Distribution(6) with{Series=Enumerable.Repeat(Distribution(6).Series[0],5).ToArray()}));
    Reject(()=>ChartSvg.Render(Distribution(6) with{Bins=0}));
    Reject(()=>ChartSvg.Render(Distribution(6) with{YAxis=AxisKind.Log}));
    Reject(()=>ChartSvg.Render(Distribution(6) with{XAxis=AxisKind.Time}));
});
Test("Box draws quartiles, whiskers and indexed outliers",()=>{
    var doc=Svg(Spec(ChartKind.Box) with{Series=[new("Latency",[new(0,1),new(1,2),new(2,3),new(3,4),new(4,5),new(5,100)])]});
    var box=doc.Descendants(ns+"g").Single(e=>(string?)e.Attribute("class")=="lumen-datum"&&e.Attribute("data-point") is null);
    Check(box.Attribute("aria-label")!.Value.Contains("median 3.5")&&box.Attribute("aria-label")!.Value.Contains("1 outliers"));
    Check(box.Elements(ns+"line").Count()==4&&box.Elements(ns+"rect").Count()==1);
    var outlier=doc.Descendants(ns+"g").Single(e=>e.Attribute("data-point") is not null);
    Check((string?)outlier.Attribute("data-point")=="5"&&outlier.Attribute("aria-label")!.Value.Contains("outlier"));
});
Test("Box places one glyph per series with observation counts",()=>{
    var doc=Svg(Spec(ChartKind.Box) with{Series=[
        new("Alpha",Enumerable.Range(0,9).Select(i=>new ChartPoint(i,i+1)).ToArray()),
        new("Beta",Enumerable.Range(0,5).Select(i=>new ChartPoint(i,i*2+1)).ToArray()),
        new("Gamma",Enumerable.Range(0,7).Select(i=>new ChartPoint(i,i+10)).ToArray())]});
    Check(doc.Descendants(ns+"g").Count(e=>(string?)e.Attribute("class")=="lumen-datum")==3);
    Check(doc.Descendants(ns+"text").Any(e=>e.Value=="Alpha (n=9)")&&doc.Descendants(ns+"text").Any(e=>e.Value=="Beta (n=5)"));
});
Test("Box tolerates missing observations and log axes",()=>{
    var doc=Svg(Spec(ChartKind.Box) with{YAxis=AxisKind.Log,Series=[new("Latency",[new(0,1),new(1,null),new(2,10),new(3,100),new(4,1000)])]});
    Check(doc.Descendants(ns+"text").Any(e=>e.Value=="10"));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Box) with{XAxis=AxisKind.Log,Series=[new("L",[new(1,1)])]}));
});
Test("Marks drop native titles when the host draws tooltips",()=>{
    var doc=XDocument.Parse(ChartSvg.Render(Spec(),includeTitles:false));
    var marks=doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-datum").ToArray();
    Check(marks.Length==3&&marks.All(m=>!m.Elements(ns+"title").Any()));
    Check(marks.All(m=>!string.IsNullOrEmpty(m.Attribute("aria-label")!.Value)));
    // The chart title and description stay: they name the whole graphic.
    Check(doc.Root!.Elements(ns+"title").Single().Value=="Example"&&doc.Root!.Elements(ns+"desc").Any());
});
Test("Default rendering keeps a native title on every mark",()=>{
    var doc=Svg(Spec());
    var marks=doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-datum").ToArray();
    Check(marks.All(m=>m.Elements(ns+"title").Single().Value==m.Attribute("aria-label")!.Value));
    Check(doc.Descendants(ns+"title").Count()==marks.Length+1);
});
Test("Aggregate marks honor the title switch",()=>{
    foreach(var kind in (ChartKind[])[ChartKind.Histogram,ChartKind.Box])
    {
        var spec=Spec(kind) with{Series=[new("Sample",Enumerable.Range(0,40).Select(i=>new ChartPoint(i,i%7+1)).ToArray())]};
        var quiet=XDocument.Parse(ChartSvg.Render(spec,includeTitles:false));
        Check(quiet.Descendants(ns+"title").Count()==1);
        Check(XDocument.Parse(ChartSvg.Render(spec)).Descendants(ns+"title").Count()>1);
    }
});
Test("Exported SVG stays self-contained for rasterization",()=>{
    var doc=Svg(Spec(ChartKind.Bubble));
    Check(doc.Root!.Attribute("viewBox") is not null);
    var markup=doc.ToString();
    Check(!markup.Contains("http://",StringComparison.Ordinal)||markup.IndexOf("http://",StringComparison.Ordinal)==markup.IndexOf("http://www.w3.org/2000/svg",StringComparison.Ordinal));
    Check(!doc.Descendants(ns+"image").Any()&&!doc.Descendants(ns+"foreignObject").Any());
    Check(doc.Root!.Attribute("style")!.Value.Contains("background:"));
});
GraphSpec Crossing()=>new(){Nodes=[new("a","A"),new("b","B"),new("c","C"),new("d","D")],Edges=[new("a","d"),new("b","c")]};
GraphSpec Spanning()=>new(){Nodes=[new("a","A"),new("b","B"),new("c","C")],Edges=[new("a","b"),new("b","c"),new("a","c")]};
Test("Layered ordering removes an avoidable crossing",()=>{
    var spec=Crossing();
    Check(GraphEngine.Crossings(spec)==0);
    var positions=GraphEngine.Layout(spec).ToDictionary(p=>p.Id);
    // The second level is reordered so d sits opposite a instead of below c.
    Check(positions["d"].Y<positions["c"].Y);
    Check(positions["a"].Y<positions["b"].Y);
});
Test("Ordering leaves a graph without crossings in input order",()=>{
    var spec=new GraphSpec{Nodes=[new("a","A"),new("b","B"),new("c","C"),new("d","D")],Edges=[new("a","c"),new("b","d")]};
    var positions=GraphEngine.Layout(spec).ToDictionary(p=>p.Id);
    Check(GraphEngine.Crossings(spec)==0);
    Check(positions["a"].Y<positions["b"].Y&&positions["c"].Y<positions["d"].Y);
});
Test("Long edges bend once for every level they span",()=>{
    var routes=GraphEngine.Routes(Spanning());
    Check(routes.Count==3);
    Check(routes[0].Points.Count==2&&routes[1].Points.Count==2);
    var spanning=routes[2].Points;
    Check(spanning.Count==3);
    Check(spanning[0].X<spanning[1].X&&spanning[1].X<spanning[2].X);
    // The bend avoids the middle node rather than passing through it.
    var middle=GraphEngine.Layout(Spanning()).Single(p=>p.Id=="b");
    Check(Math.Abs(spanning[1].Y-middle.Y)>20);
});
Test("Routes and crossing counts are deterministic",()=>{
    Check(GraphEngine.Routes(Spanning()).SelectMany(r=>r.Points).SequenceEqual(GraphEngine.Routes(Spanning()).SelectMany(r=>r.Points)));
    Check(GraphEngine.Crossings(Crossing())==GraphEngine.Crossings(Crossing()));
});
Test("Self-loop routes keep both endpoints on their node",()=>{
    var spec=new GraphSpec{Nodes=[new("a","A"),new("b","B")],Edges=[new("a","b"),new("a","a")]};
    var route=GraphEngine.Routes(spec)[1];
    Check(route.Points.Count==2&&route.Points[0]==route.Points[1]);
    Check(route.Points[0]==new GraphPoint(GraphEngine.Layout(spec)[0].X,GraphEngine.Layout(spec)[0].Y));
});
Test("Circular crossings count interleaved chords",()=>{
    GraphSpec Ring(params GraphEdge[] edges)=>new(){Layout=GraphLayout.Circular,Nodes=[new("a","A"),new("b","B"),new("c","C"),new("d","D")],Edges=edges};
    Check(GraphEngine.Crossings(Ring(new("a","c"),new("b","d")))==1);
    Check(GraphEngine.Crossings(Ring(new("a","b"),new("c","d")))==0);
    Check(GraphEngine.Crossings(Ring(new("a","b"),new("b","c")))==0);
});
Test("Graph SVG exposes drag handles and the crossing count",()=>{
    var doc=XDocument.Parse(GraphEngine.Render(Spanning()));
    var nodes=doc.Descendants(ns+"g").Where(e=>e.Attribute("data-node") is not null).ToArray();
    Check(nodes.Length==3);
    Check(nodes.All(n=>(string?)n.Attribute("class")=="lumen-node"&&(string?)n.Attribute("tabindex")=="0"));
    Check(nodes.All(n=>n.Attribute("data-position")!.Value.Split(',').Length==2));
    Check(doc.Descendants(ns+"desc").Single().Value.Contains("0 edge crossings"));
    Check(!XDocument.Parse(GraphEngine.Render(Spanning() with{Layout=GraphLayout.Circular})).Descendants(ns+"desc").Single().Value.Contains("crossings"));
});
Test("Dragged positions move the node and its edge endpoints",()=>{
    var moved=new Dictionary<string,GraphPoint>{["b"]=new(400,300)};
    var doc=XDocument.Parse(GraphEngine.Render(Spanning(),moved));
    var circle=doc.Descendants(ns+"g").Single(e=>(string?)e.Attribute("data-node")=="b").Element(ns+"circle")!;
    Check(circle.Attribute("cx")!.Value=="400"&&circle.Attribute("cy")!.Value=="300");
    Check(doc.Descendants(ns+"g").Single(e=>(string?)e.Attribute("data-node")=="b").Attribute("data-position")!.Value=="400,300");
    // The edge into the moved node ends near it, allowing for the arrowhead gap.
    var path=doc.Descendants(ns+"path").First().Attribute("d")!.Value.Split(' ')[^1].TrimStart('L').Split(',');
    Check(Math.Abs(double.Parse(path[0],CultureInfo.InvariantCulture)-400)<40&&Math.Abs(double.Parse(path[1],CultureInfo.InvariantCulture)-300)<40);
});
Test("Layered graphs accept self-loops but not longer cycles",()=>{
    var spec=new GraphSpec{Nodes=[new("a","A"),new("b","B")],Edges=[new("a","b"),new("a","a")]};
    var positions=GraphEngine.Layout(spec).ToDictionary(p=>p.Id);
    Check(positions["a"].X<positions["b"].X);
    Check(XDocument.Parse(GraphEngine.Render(spec)).Descendants(ns+"path").Any(e=>e.Attribute("d")!.Value.Contains("C")));
    Reject(()=>GraphEngine.Layout(spec with{Edges=[new("a","b"),new("b","a")]}));
});
Test("Blazor graph prerender exposes nodes and a reset control",()=>{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        var html=renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<LumenGraph>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",Spanning()}}));return root.ToHtmlString();
        }).GetAwaiter().GetResult();
        Check(html.Contains("data-node='a'")&&html.Contains("data-position=")&&html.Contains("Reset layout"));
        Check(html.Contains("View connections"));
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
});
double Channel(int value){var c=value/255.0;return c<=0.03928?c/12.92:Math.Pow((c+.055)/1.055,2.4);}
double Luminance(string hex)=>.2126*Channel(Convert.ToInt32(hex.Substring(1,2),16))+.7152*Channel(Convert.ToInt32(hex.Substring(3,2),16))+.0722*Channel(Convert.ToInt32(hex.Substring(5,2),16));
double Contrast(string a,string b){double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
const string LightBackground="#FFFFFF",DarkBackground="#171E2E";
Test("Contrast helper matches published WCAG ratios",()=>{
    Check(Math.Abs(Contrast("#000000","#FFFFFF")-21)<.01);
    Check(Math.Abs(Contrast("#FFFFFF","#FFFFFF")-1)<.01);
    Check(Math.Abs(Contrast("#767676","#FFFFFF")-4.54)<.02);
});
Test("Series colors meet the 3:1 non-text contrast ratio in both themes",()=>{
    foreach(var color in ChartSvg.Palette.Concat([ChartSvg.RisingColor,ChartSvg.FallingColor]))
    {
        Check(Contrast(color,LightBackground)>=3,$"{color} on light is {Contrast(color,LightBackground):0.00}");
        Check(Contrast(color,DarkBackground)>=3,$"{color} on dark is {Contrast(color,DarkBackground):0.00}");
    }
});
Test("Chart text meets the 4.5:1 contrast ratio in both themes",()=>{
    Check(Contrast("#26324B",LightBackground)>=4.5);
    Check(Contrast("#63718A",LightBackground)>=4.5);
    Check(Contrast("#E8ECF6",DarkBackground)>=4.5);
    Check(Contrast("#AAB8CF",DarkBackground)>=4.5);
    // Tooltip colors are set in lumen.css and travel with the component.
    Check(Contrast("#F4F7FD","#1D2436")>=4.5);
});
Test("Graph edges stay visible against both backgrounds",()=>{
    Check(Contrast("#8090AD",LightBackground)>=3&&Contrast("#8090AD",DarkBackground)>=3);
});
Test("Every mark carries an accessible name and a supported role",()=>{
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        var doc=Svg(Sample(kind));
        var marks=doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-datum").ToArray();
        Check(marks.Length>0,$"{kind} drew no marks");
        Check(marks.All(m=>!string.IsNullOrWhiteSpace(m.Attribute("aria-label")?.Value)),$"{kind} mark without a name");
        Check(marks.All(m=>(string?)m.Attribute("role") is "button" or "img"),$"{kind} mark with an unsupported role");
        Check(marks.All(m=>(string?)m.Attribute("tabindex")=="0"),$"{kind} mark outside the tab order");
        Check(doc.Root!.Attribute("role")!.Value=="group"&&!string.IsNullOrWhiteSpace(doc.Root!.Attribute("aria-label")!.Value));
        Check(doc.Root!.Elements(ns+"title").Any()&&doc.Root!.Elements(ns+"desc").Any());
    }
});
Test("No element claims a positive tab index",()=>{
    foreach(var kind in Enum.GetValues<ChartKind>())
        Check(Svg(Sample(kind)).Descendants().Attributes("tabindex").All(a=>a.Value=="0"));
    Check(XDocument.Parse(GraphEngine.Render(Spanning())).Descendants().Attributes("tabindex").All(a=>a.Value=="0"));
});
Test("Graph nodes are named, focusable buttons",()=>{
    var doc=XDocument.Parse(GraphEngine.Render(Spanning()));
    var nodes=doc.Descendants(ns+"g").Where(e=>e.Attribute("data-node") is not null).ToArray();
    Check(nodes.All(n=>(string?)n.Attribute("role")=="button"&&(string?)n.Attribute("tabindex")=="0"));
    Check(nodes.Select(n=>n.Attribute("aria-label")!.Value).SequenceEqual(["A","B","C"]));
});
Test("Heatmap cells stay delineated against the chart background",()=>{
    var doc=Svg(Sample(ChartKind.Heatmap));
    var cells=doc.Descendants(ns+"rect").Where(e=>e.Attribute("stroke") is not null).ToArray();
    Check(cells.Length>0&&cells.All(c=>(string?)c.Attribute("stroke")=="var(--lumen-muted)"));
});
Test("Component markup exposes labelled controls and a live status region",()=>{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        var html=renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<LumenChart>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",Spec() with{Series=[Spec().Series[0]]}}}));
            return root.ToHtmlString();
        }).GetAwaiter().GetResult();
        Check(html.Contains("role=\"status\""));
        Check(html.Contains("aria-label=\"Chart series\"")&&html.Contains("aria-pressed=\"true\""));
        Check(html.Contains("role=\"region\"")&&html.Contains("aria-label=\"Scrollable chart\""));
        Check(html.Contains("aria-expanded=\"false\""));
        Check(html.Contains("aria-label=\"Zoom in\"")&&html.Contains("aria-label=\"Pan left\""));
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
});
Test("Published libraries carry no host-specific dependency",()=>{
    var core=typeof(ChartSpec).Assembly.GetReferencedAssemblies().Select(a=>a.Name!).ToArray();
    Check(core.All(name=>name.StartsWith("System.")||name=="netstandard"),"core references "+string.Join(", ",core));
    var components=typeof(LumenChart).Assembly.GetReferencedAssemblies().Select(a=>a.Name!).ToArray();
    Check(components.All(name=>name.StartsWith("System.")||name=="netstandard"||name=="Lumen.Charts"
        ||name=="Microsoft.AspNetCore.Components"||name=="Microsoft.AspNetCore.Components.Web"||name=="Microsoft.JSInterop"),
        "component references "+string.Join(", ",components));
    // Anything server-only would keep the component library out of a WebAssembly host.
    Check(!components.Any(name=>name.Contains("Server")||name.Contains("Http")||name.Contains("Hosting")));
});
Test("Marks on the first and last values are drawn whole",()=>{
    foreach(var kind in (ChartKind[])[ChartKind.Line,ChartKind.Area,ChartKind.Scatter])
    {
        var doc=Svg(Spec(kind));
        var clip=doc.Descendants(ns+"svg").Single(e=>e.Attribute("x") is not null);
        double left=double.Parse(clip.Attribute("x")!.Value,CultureInfo.InvariantCulture);
        double width=double.Parse(clip.Attribute("width")!.Value,CultureInfo.InvariantCulture);
        var circles=doc.Descendants(ns+"g").Where(e=>e.Attribute("data-point") is not null).Select(e=>e.Element(ns+"circle")!).ToArray();
        Check(circles.Length==3,$"{kind} drew {circles.Length} marks");
        foreach(var circle in circles)
        {
            double cx=double.Parse(circle.Attribute("cx")!.Value,CultureInfo.InvariantCulture);
            double r=double.Parse(circle.Attribute("r")!.Value,CultureInfo.InvariantCulture);
            // A mark clipped in half is half invisible and cannot be hovered at its own centre.
            Check(cx-r>=left&&cx+r<=left+width,$"{kind} mark at {cx} is clipped by the plot viewport");
        }
    }
});
ChartSpec Cloud(int points,int? cells)=>Spec(ChartKind.Scatter) with{DensityCells=cells,Series=[new("Cloud",
    Enumerable.Range(0,points).Select(i=>new ChartPoint(i%320,Math.Sin(i*.03)*40+i%17)).ToArray())]};
Test("Density cells replace one mark per observation",()=>{
    var every=Svg(Cloud(5000,null));
    var binned=Svg(Cloud(5000,40));
    Check(every.Descendants(ns+"g").Count(e=>(string?)e.Attribute("class")=="lumen-datum")==5000);
    var cells=binned.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-datum").ToArray();
    Check(cells.Length is >0 and <1000,$"{cells.Length} cells drawn");
    // Cells are aggregates, so they carry no observation index, exactly like histogram bins.
    Check(cells.All(c=>c.Attribute("data-point") is null&&(string?)c.Attribute("role")=="img"));
    Check(binned.Descendants(ns+"text").Any(t=>t.Value.Contains("5,000 observations aggregated into 40 cells")));
});
Test("Every observation lands in exactly one cell",()=>{
    var doc=Svg(Cloud(5000,40));
    var counted=doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-datum")
        .Select(e=>int.Parse(e.Attribute("aria-label")!.Value.Split(": ")[1].Split(' ')[0].Replace(",",""),CultureInfo.InvariantCulture)).Sum();
    Check(counted==5000,$"cells account for {counted} of 5000 observations");
});
Test("Density cells are square and shaded by count",()=>{
    var doc=Svg(Cloud(5000,40));
    var rects=doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-datum").Select(e=>e.Element(ns+"rect")!).ToArray();
    Check(rects.All(r=>r.Attribute("width")!.Value==r.Attribute("height")!.Value),"cells are not square");
    var weights=rects.Select(r=>double.Parse(r.Attribute("fill-opacity")!.Value,CultureInfo.InvariantCulture)).ToArray();
    Check(weights.Min()>=.2&&weights.Max()<=.9);
    Check(weights.Distinct().Count()>1,"every cell has the same shading");
});
Test("Density leaves the exported observations untouched",()=>{
    Check(ChartExport.Csv(Cloud(500,40)).Split('\n',StringSplitOptions.RemoveEmptyEntries).Length==501);
});
Test("Reject density cells where they would misrepresent the chart",()=>{
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Line) with{DensityCells=40}));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Bubble) with{DensityCells=40}));
    Reject(()=>ChartSvg.Render(Cloud(100,4)));
    Reject(()=>ChartSvg.Render(Cloud(100,400)));
});
ChartStyle Brand()=>new(){Background="#F6F3EE",Text="#1F2A37",Muted="#4B5563",Grid="#E5DED3",Edge="#6B7280",
    Series=["#1D4E89","#B03A2E","#2E7D5B"],Rising="#2E7D5B",Falling="#B03A2E",HeatmapLow="#EFE6D8",HeatmapHigh="#1D4E89",FontFamily="Georgia,serif"};
Test("Style presets reproduce the themes exactly",()=>{
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        Check(ChartSvg.Render(Sample(kind))==ChartSvg.Render(Sample(kind) with{Style=ChartStyle.Light}),$"{kind} light differs");
        Check(ChartSvg.Render(Sample(kind) with{Theme=ChartTheme.Dark})==ChartSvg.Render(Sample(kind) with{Style=ChartStyle.Dark}),$"{kind} dark differs");
    }
    Check(GraphEngine.Render(Spanning())==GraphEngine.Render(Spanning() with{Style=ChartStyle.Light}));
});
Test("A style replaces every themed colour and the typeface",()=>{
    var doc=Svg(Spec() with{Style=Brand(),Series=[new("A",[new(0,1),new(1,2)]),new("B",[new(0,2),new(1,3)])]});
    var root=doc.Root!.Attribute("style")!.Value;
    Check(root.Contains("background:#F6F3EE")&&root.Contains("color:#1F2A37")&&root.Contains("font-family:Georgia,serif"));
    Check(root.Contains("--lumen-grid:#E5DED3")&&root.Contains("--lumen-muted:#4B5563"));
    var fills=doc.Descendants(ns+"g").Where(e=>e.Attribute("data-point") is not null).Select(e=>(string?)e.Element(ns+"circle")!.Attribute("fill")).Distinct().ToArray();
    Check(fills.SequenceEqual(["#1D4E89","#B03A2E"]),string.Join(",",fills));
    Check(!doc.ToString().Contains("#5675E7"),"a default palette colour leaked into a styled chart");
});
Test("A series' own colour still wins over the style",()=>{
    var doc=Svg(Spec() with{Style=Brand(),Series=[new("A",[new(0,1)],"#123456")]});
    Check(doc.Descendants(ns+"circle").Any(c=>(string?)c.Attribute("fill")=="#123456"));
});
Test("Candles, heatmaps and graphs take their colours from the style",()=>{
    var candles=ChartSvg.Render(Candles() with{Style=Brand()});
    Check(candles.Contains("fill='#2E7D5B'")&&candles.Contains("fill='#B03A2E'"));
    var heat=Svg(Sample(ChartKind.Heatmap) with{Style=Brand()});
    var cells=heat.Descendants(ns+"rect").Select(r=>(string?)r.Attribute("fill")).ToArray();
    Check(cells.Contains("#EFE6D8")&&cells.Contains("#1D4E89"),"the ramp does not run between the style's heatmap colours");
    var graph=GraphEngine.Render(Spanning() with{Style=Brand()});
    Check(graph.Contains("stroke='#6B7280'")&&graph.Contains("stroke='#1D4E89'")&&!graph.Contains("#8090AD"));
});
Test("Reject styles that are incomplete or could escape the markup",()=>{
    Reject(()=>ChartSvg.Render(Spec() with{Style=Brand() with{Background="white"}}));
    Reject(()=>ChartSvg.Render(Spec() with{Style=Brand() with{Series=[]}}));
    Reject(()=>ChartSvg.Render(Spec() with{Style=Brand() with{Series=Enumerable.Repeat("#123456",33).ToArray()}}));
    Reject(()=>ChartSvg.Render(Spec() with{Style=Brand() with{Text=null!}}));
    foreach(var font in (string[])["Arial;background:url(x)","Arial'><script>","\"Segoe UI\", Arial","Arial}svg{fill:red",""])
        Reject(()=>ChartSvg.Render(Spec() with{Style=Brand() with{FontFamily=font}}));
    Reject(()=>GraphEngine.Render(Spanning() with{Style=Brand() with{Edge="#12345"}}));
});
Test("Font lists from CSS are reduced to what a style accepts",()=>{
    Check(ChartStyle.FontFamilyFrom("\"Trebuchet MS\", Verdana, sans-serif")=="Trebuchet MS,Verdana,sans-serif");
    Check(ChartStyle.FontFamilyFrom("'Inter var'  , system-ui")=="Inter var,system-ui");
    Check(ChartStyle.FontFamilyFrom("Arial;}<script>")=="Arialscript");
    Check(ChartStyle.FontFamilyFrom("  ")==null&&ChartStyle.FontFamilyFrom("\"\";,")==null);
    Check(ChartStyle.FontFamilyFrom(string.Join(",",Enumerable.Repeat("Longfontname",40)))!.Length<=200);
});
Test("Contrast findings name the failing pairs",()=>{
    Check(ChartStyle.Light.ContrastIssues().Count==0&&ChartStyle.Dark.ContrastIssues().Count==0,"a built-in preset is inaccessible");
    var issues=(ChartStyle.Light with{Series=["#FFD60A","#1D4E89"],Muted="#A0A0A0"}).ContrastIssues();
    Check(issues.Count==2);
    Check(issues.Any(i=>i.Element=="Series 1"&&i.Foreground=="#FFD60A"&&i.Required==3&&i.Ratio<2));
    Check(issues.Any(i=>i.Element=="Muted text"&&i.Required==4.5));
    Check(Math.Abs(Lumen.Charts.Contrast.Ratio("#000000","#FFFFFF")-21)<.01&&Math.Abs(Lumen.Charts.Contrast.Ratio("#767676","#FFFFFF")-4.54)<.02);
});
Test("A partial style from JSON keeps the defaults it does not name",()=>{
    var json="{\"kind\":\"Line\",\"style\":{\"series\":[\"#1D4E89\"],\"background\":\"#F6F3EE\"},\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1},{\"x\":1,\"y\":2}]}]}";
    var spec=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}})!;
    Check(spec.Style!.Background=="#F6F3EE"&&spec.Style.Text==ChartStyle.Light.Text&&spec.Style.FontFamily==ChartStyle.Light.FontFamily);
    Check(ChartSvg.Render(spec).Contains("fill='#1D4E89'"));
});
string RenderInside(ChartStyle? cascaded,ChartSpec spec)
{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        RenderFragment chart=b=>{b.OpenComponent<LumenChart>(0);b.AddAttribute(1,"Spec",spec);b.CloseComponent();};
        return renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<CascadingValue<ChartStyle>>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Value",cascaded},{"ChildContent",chart}}));
            return root.ToHtmlString();
        }).GetAwaiter().GetResult();
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
}
Test("A cascaded style brands the component, and a spec's own style wins",()=>{
    var branded=RenderInside(Brand() with{Finish=ChartFinish.Classic},Spec());
    Check(branded.Contains("background:#F6F3EE")&&branded.Contains("background:#1D4E89"),"cascaded style missing from the chart or its legend");
    var own=RenderInside(Brand(),Spec() with{Style=ChartStyle.Dark});
    Check(own.Contains("background:#171E2E")&&!own.Contains("#F6F3EE"),"the cascade overrode the spec's own style");
});
Test("LumenBrand renders its fallback until the page has been read",()=>{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        RenderFragment chart=b=>{b.OpenComponent<LumenChart>(0);b.AddAttribute(1,"Spec",Spec());b.CloseComponent();};
        var html=renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<LumenBrand>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Series","--brand-1"},{"Fallback",Brand()},{"ChildContent",chart}}));
            return root.ToHtmlString();
        }).GetAwaiter().GetResult();
        Check(html.Contains("data-lumen-brand=\"pending\"")&&html.Contains("data-lumen-series=\"--brand-1\""));
        Check(html.Contains("background:#F6F3EE")&&html.Contains("--lumen-accent:#1D4E89"));
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
});
ChartSpec Marked(params ChartAnnotation[] annotations)=>Spec() with{Annotations=annotations,
    Series=[new("S",[new(0,10),new(1,30),new(2,20),new(3,40)])]};
XElement Annotation(XDocument doc)=>doc.Descendants(ns+"g").Single(e=>(string?)e.Attribute("class")=="lumen-datum"&&e.Element(ns+"text") is not null);
Test("A reference line sits at its value and names itself",()=>{
    var doc=Svg(Classic(Marked(new ChartAnnotation(AnnotationAxis.Y,25){Label="Target"})));
    var group=Annotation(doc);
    Check(group.Attribute("aria-label")!.Value=="Target: 25");
    var lines=group.Elements(ns+"line").ToArray();
    // An invisible wider line first, so the dashes are not the only hoverable part.
    Check(lines.Length==2&&(string?)lines[0].Attribute("stroke-opacity")=="0"&&(string?)lines[0].Attribute("stroke-width")=="12");
    var line=lines[1];
    Check(line.Attribute("y1")!.Value==line.Attribute("y2")!.Value,"a Y reference is not horizontal");
    Check((string?)line.Attribute("stroke-dasharray")=="6 4");
    // Halfway between the 10 and 40 extremes of the data, so halfway down the plot.
    var y=double.Parse(line.Attribute("y1")!.Value,CultureInfo.InvariantCulture);
    Check(Math.Abs(y-(78+(420-76))/2.0)<12,$"the line is at {y}");
    Check(group.Element(ns+"text")!.Value=="Target: 25");
});
Test("A vertical reference uses the X axis and its formatting",()=>{
    var start=TimeAxis.Value(new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero));
    var doc=Svg(Classic(Spec() with{XAxis=AxisKind.Time,Annotations=[new(AnnotationAxis.X,start+86400000d*15){Label="Launch"}],
        Series=[new("S",Enumerable.Range(0,30).Select(i=>new ChartPoint(start+i*86400000d,i)).ToArray())]}));
    var group=Annotation(doc);
    Check(group.Attribute("aria-label")!.Value=="Launch: 16 Jan 2026",group.Attribute("aria-label")!.Value);
    var line=group.Elements(ns+"line").Last();
    Check(line.Attribute("x1")!.Value==line.Attribute("x2")!.Value,"an X reference is not vertical");
});
Test("A band covers the range it names",()=>{
    var doc=Svg(Classic(Marked(new ChartAnnotation(AnnotationAxis.Y,15){To=35,Label="Acceptable"})));
    var group=Annotation(doc);
    Check(group.Attribute("aria-label")!.Value=="Acceptable: 15 to 35");
    var rect=group.Element(ns+"rect")!;
    Check((string?)rect.Attribute("fill-opacity")==".12");
    var height=double.Parse(rect.Attribute("height")!.Value,CultureInfo.InvariantCulture);
    Check(height>50,$"the band is only {height} tall");
});
Test("Annotations render behind the data and inside the plot's clip",()=>{
    var markup=ChartSvg.Render(Classic(Marked(new ChartAnnotation(AnnotationAxis.Y,25))));
    var clip=markup.IndexOf("overflow='hidden'");
    var annotation=markup.IndexOf("stroke-dasharray");
    var firstMark=markup.IndexOf("data-point=");
    Check(clip<annotation&&annotation<firstMark,"an annotation is outside the clip or drawn over the data");
});
Test("An annotation takes the style's muted colour unless it names one",()=>{
    var branded=Svg(Marked(new ChartAnnotation(AnnotationAxis.Y,25)) with{Style=ChartStyle.Light with{Muted="#4B5563",Finish=ChartFinish.Classic}});
    Check((string?)Annotation(branded).Elements(ns+"line").Last().Attribute("stroke")=="#4B5563");
    var own=Svg(Classic(Marked(new ChartAnnotation(AnnotationAxis.Y,25){Color="#B03A2E",Dashed=false})));
    var line=Annotation(own).Elements(ns+"line").Last();
    Check((string?)line.Attribute("stroke")=="#B03A2E"&&line.Attribute("stroke-dasharray") is null);
});
Test("An annotation without a label still reads as a value",()=>{
    Check(Annotation(Svg(Classic(Marked(new ChartAnnotation(AnnotationAxis.Y,25))))).Attribute("aria-label")!.Value=="25");
});
Test("Reject annotations a chart cannot place honestly",()=>{
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Donut) with{Annotations=[new(AnnotationAxis.Y,1)]}));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Histogram) with{Series=[new("S",[new(0,1),new(1,2)])],Annotations=[new(AnnotationAxis.Y,1)]}));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Column) with{Annotations=[new(AnnotationAxis.X,1)]}));
    Reject(()=>ChartSvg.Render(Marked(new ChartAnnotation(AnnotationAxis.Y,10){To=10})));
    Reject(()=>ChartSvg.Render(Marked(new ChartAnnotation(AnnotationAxis.Y,double.NaN))));
    Reject(()=>ChartSvg.Render(Marked(new ChartAnnotation((AnnotationAxis)7,1))));
    Reject(()=>ChartSvg.Render(Marked(new ChartAnnotation(AnnotationAxis.Y,1){Color="red"})));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Scatter) with{YAxis=AxisKind.Log,Series=[new("S",[new(1,1),new(2,10)])],Annotations=[new(AnnotationAxis.Y,0)]}));
    Reject(()=>ChartSvg.Render(Marked(Enumerable.Range(0,33).Select(i=>new ChartAnnotation(AnnotationAxis.Y,i+1)).ToArray())));
});
Test("A column chart still takes a Y reference",()=>{
    var doc=Svg(Classic(Spec(ChartKind.Column) with{Annotations=[new(AnnotationAxis.Y,4){Label="Budget"}]}));
    Check(Annotation(doc).Attribute("aria-label")!.Value=="Budget: 4");
});
ChartSpec Paired()=>Spec() with{Y2Label="Rate (%)",Series=[
    new("Accounts",Enumerable.Range(0,6).Select(i=>new ChartPoint(i,100+i*80)).ToArray()),
    new("Conversion",Enumerable.Range(0,6).Select(i=>new ChartPoint(i,2+i*0.4)).ToArray()){Secondary=true}]};
double[] Heights(XDocument doc,int series)=>doc.Descendants(ns+"g")
    .Where(e=>(string?)e.Attribute("data-series")==series.ToString(CultureInfo.InvariantCulture))
    .Select(e=>double.Parse(e.Element(ns+"circle")!.Attribute("cy")!.Value,CultureInfo.InvariantCulture)).ToArray();
Test("A secondary series is measured against its own axis",()=>{
    var doc=Svg(Paired());
    double[] left=Heights(doc,0),right=Heights(doc,1);
    Check(left.Length==6&&right.Length==6);
    // Both series climb across the full plot even though one runs 100..500 and the other 2..4.
    Check(left[0]-left[^1]>150&&right[0]-right[^1]>150,$"left spans {left[0]-left[^1]}, right spans {right[0]-right[^1]}");
    // On one shared axis the small series would sit flat at the bottom; it does not.
    var shared=Svg(Paired() with{Series=[Paired().Series[0],Paired().Series[1] with{Secondary=false}]});
    Check(Heights(shared,1).Max()-Heights(shared,1).Min()<20,"the control chart did not flatten the small series");
});
Test("The right axis carries its own ticks and name",()=>{
    var doc=Svg(Paired());
    var labels=doc.Descendants(ns+"text").Select(t=>t.Value).ToArray();
    Check(labels.Contains("Rate (%)"),"the secondary axis is unnamed");
    Check(doc.Descendants(ns+"text").Any(t=>(string?)t.Attribute("transform") is string x&&x.StartsWith("rotate(90")),"the secondary name is not rotated on the right");
    var anchored=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="start"&&(string?)t.Attribute("class")=="lumen-muted").ToArray();
    Check(anchored.Length>=3,"the right axis has no ticks");
    // One grid to read, not two: a gridline per left tick and none for the right axis.
    var leftTicks=doc.Descendants(ns+"text").Count(t=>(string?)t.Attribute("text-anchor")=="end"&&(string?)t.Attribute("class")=="lumen-muted");
    Check(doc.Descendants(ns+"line").Count(l=>(string?)l.Attribute("class")=="lumen-grid")==leftTicks,"the gridlines do not match the left axis alone");
});
Test("The plot narrows to leave room for the right axis",()=>{
    double Width(ChartSpec spec)=>double.Parse(Svg(spec).Descendants(ns+"svg").Single(e=>e.Attribute("x") is not null).Attribute("width")!.Value,CultureInfo.InvariantCulture);
    Check(Width(Paired())<Width(Spec())-40,"the plot did not make room");
});
Test("A secondary point reads in its own units",()=>{
    var doc=Svg(Paired());
    var mark=doc.Descendants(ns+"g").First(e=>(string?)e.Attribute("data-series")=="1");
    Check(mark.Attribute("aria-label")!.Value=="Conversion: 0, 2",mark.Attribute("aria-label")!.Value);
});
Test("The secondary axis takes its own bounds and scale",()=>{
    var bounded=Svg(Paired() with{Y2Min=0,Y2Max=10});
    Check(bounded.Descendants(ns+"text").Any(t=>t.Value=="10"&&(string?)t.Attribute("text-anchor")=="start"));
    var logged=Svg(Spec(ChartKind.Scatter) with{Y2Axis=AxisKind.Log,Series=[
        new("Left",[new(0,1),new(1,2)]),
        new("Right",[new(0,1),new(1,1000)]){Secondary=true}]});
    var rightTicks=logged.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="start").Select(t=>t.Value).ToArray();
    Check(rightTicks.Contains("10")&&rightTicks.Contains("100"),string.Join(",",rightTicks));
});
Test("Reject a secondary axis where a chart cannot measure against two",()=>{
    ChartSpec With(ChartKind kind)=>Spec(kind) with{Series=[new("A",[new(0,1),new(1,2)]),new("B",[new(0,3),new(1,4)]){Secondary=true}]};
    Reject(()=>ChartSvg.Render(With(ChartKind.StackedColumn)));
    Reject(()=>ChartSvg.Render(With(ChartKind.Bar)));
    Reject(()=>ChartSvg.Render(With(ChartKind.Donut)));
    Reject(()=>ChartSvg.Render(With(ChartKind.Radar)));
    Reject(()=>ChartSvg.Render(Spec() with{Series=[new("Only",[new(0,1),new(1,2)]){Secondary=true}]}));
    Reject(()=>ChartSvg.Render(Paired() with{Y2Axis=AxisKind.Time}));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Column) with{Y2Axis=AxisKind.Log,Series=[new("A",[new(0,1)]),new("B",[new(1,2)]){Secondary=true}]}));
    Reject(()=>ChartSvg.Render(Paired() with{Y2Axis=AxisKind.Log,Series=[Paired().Series[0],new("Zeroed",[new(0,0),new(1,1)]){Secondary=true}]}));
    Reject(()=>ChartSvg.Render(Paired() with{Y2Min=5,Y2Max=1}));
});
TimeZoneInfo NewYork()=>TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
DateTime InZone(double value,TimeZoneInfo zone)=>TimeZoneInfo.ConvertTimeFromUtc(TimeAxis.Moment(value).UtcDateTime,zone);
Test("Day ticks fall on local midnight, not on the UTC one",()=>{
    var axis=new Axis(AxisKind.Time,Utc(2026,1,5),Utc(2026,1,9)){Zone=NewYork()};
    var ticks=axis.Ticks(6);
    Check(ticks.Count>=4);
    Check(ticks.All(t=>InZone(t.Value,NewYork()).TimeOfDay==TimeSpan.Zero),"a tick misses local midnight");
    // Eastern Standard Time is five hours behind, so local midnight is 05:00 in UTC.
    Check(ticks.All(t=>TimeAxis.Moment(t.Value).UtcDateTime.Hour==5),"the ticks are not offset from UTC");
    Check(new Axis(AxisKind.Time,Utc(2026,1,5),Utc(2026,1,9)).Ticks(6).All(t=>TimeAxis.Moment(t.Value).UtcDateTime.Hour==0),"UTC moved");
});
Test("Ticks hold local midnight across a clock change",()=>{
    // Clocks go forward in the United States on 8 March 2026.
    var ticks=new Axis(AxisKind.Time,Utc(2026,3,6),Utc(2026,3,11)){Zone=NewYork()}.Ticks(6);
    Check(ticks.All(t=>InZone(t.Value,NewYork()).TimeOfDay==TimeSpan.Zero),"a tick drifted off local midnight");
    var offsets=ticks.Select(t=>TimeAxis.Moment(t.Value).UtcDateTime.Hour).Distinct().OrderBy(h=>h).ToArray();
    Check(offsets.SequenceEqual([4,5]),"the ticks did not follow the clock change: "+string.Join(",",offsets));
});
Test("A zone with a half-hour offset keeps its own clock",()=>{
    var kolkata=TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    var ticks=new Axis(AxisKind.Time,Utc(2026,5,4,3),Utc(2026,5,4,15)){Zone=kolkata}.Ticks(6);
    Check(ticks.All(t=>InZone(t.Value,kolkata).Minute==0),"a tick misses the local hour");
    Check(ticks.All(t=>TimeAxis.Moment(t.Value).UtcDateTime.Minute==30),"the half-hour offset was lost");
});
Test("Months and years start where the zone starts them",()=>{
    var months=new Axis(AxisKind.Time,Utc(2026,1,1),Utc(2026,12,31)){Zone=NewYork()}.Ticks(6);
    Check(months.All(t=>{var local=InZone(t.Value,NewYork());return local.Day==1&&local.TimeOfDay==TimeSpan.Zero;}),"a month does not start at local midnight");
    var years=new Axis(AxisKind.Time,Utc(2020,1,1),Utc(2026,1,1)){Zone=NewYork()}.Ticks(6);
    Check(years.All(t=>{var local=InZone(t.Value,NewYork());return local is{Month:1,Day:1}&&local.TimeOfDay==TimeSpan.Zero;}),"a year does not start at local midnight");
});
Test("A time axis reads its values in its own zone",()=>{
    var axis=new Axis(AxisKind.Time,Utc(2026,1,5),Utc(2026,1,5,12)){Zone=NewYork()};
    Check(axis.Format(Utc(2026,1,5,4))=="4 Jan 2026 23:00",axis.Format(Utc(2026,1,5,4)));
    Check(new Axis(AxisKind.Time,Utc(2026,1,5),Utc(2026,1,5,12)).Format(Utc(2026,1,5,4))=="5 Jan 2026 04:00");
});
Test("A chart draws and reads its time axis in the spec's zone",()=>{
    var doc=Svg(TimeSpec(Utc(2026,1,5),3600000d,12) with{TimeZone="America/New_York"});
    var first=doc.Descendants(ns+"g").First(e=>e.Attribute("data-point") is not null).Attribute("aria-label")!.Value;
    // Midnight on 5 January in UTC is the previous evening in New York.
    Check(first.Contains("4 Jan 2026 19:00"),first);
    Check(doc.Descendants(ns+"text").Any(t=>t.Value.Contains(':')),"the axis lost its clock labels");
    Check(ChartSvg.Render(TimeSpec(Utc(2026,1,5),3600000d,12)).Contains("5 Jan 2026 00:00"),"UTC rendering moved");
});
Test("Reject a zone the host does not know, or one without a time axis",()=>{
    Reject(()=>ChartSvg.Render(TimeSpec(Utc(2026,1,5),3600000d,6) with{TimeZone="Mars/Olympus"}));
    Reject(()=>ChartSvg.Render(Spec() with{TimeZone="America/New_York"}));
});
int MinorLines(XDocument doc)=>doc.Descendants(ns+"line").Count(l=>(string?)l.Attribute("class")=="lumen-grid-minor");
Test("Minor lines divide each interval and stay off by default",()=>{
    var plain=Svg(Spec());
    Check(MinorLines(plain)==0&&!plain.ToString().Contains("lumen-grid-minor"),"a chart that asked for nothing carries minor lines or their rule");
    var divided=Svg(Spec() with{MinorGridlines=true});
    Check(MinorLines(divided)>0);
    var axis=new Axis(AxisKind.Linear,0,8);
    var minors=axis.MinorTicks();
    var majors=axis.Ticks().Select(t=>t.Value).ToArray();
    Check(minors.All(m=>m>0&&m<8)&&minors.Distinct().Count()==minors.Count,"a minor value repeats or sits outside the axis");
    Check(!minors.Any(m=>majors.Any(major=>Math.Abs(major-m)<1e-9)),"a labelled tick is repeated as a minor line");
    // Each interval is divided evenly; the wider gaps in the sequence are where a major tick sits.
    foreach(var major in majors.SkipLast(1))
    {
        var inside=minors.Where(m=>m>major&&m<major+2).OrderBy(m=>m).ToArray();
        Check(inside.Length==3,$"interval at {major} has {inside.Length} divisions");
        Check(inside.Zip(inside.Skip(1),(a,b)=>Math.Round(b-a,6)).Distinct().Count()==1,"the divisions are uneven");
    }
});
Test("The number of divisions follows the step",()=>{
    // A step of two reads best in quarters, a step of ten in fifths.
    Check(Math.Round(new Axis(AxisKind.Linear,0,8).MinorTicks().First(),6)==0.5,"a step of 2 should divide into four");
    var overForty=new Axis(AxisKind.Linear,0,40).MinorTicks();
    Check(Math.Round(overForty[1]-overForty[0],6)==2,"a step of 10 should divide into five");
    // A step of two and a half also divides into four, which lands on eighths of ten.
    Check(Math.Round(new Axis(AxisKind.Linear,0,10).MinorTicks().First(),6)==0.625);
});
Test("A log axis marks the mantissas between its decades",()=>{
    var minors=Axis.Create(AxisKind.Log,[1,1000]).MinorTicks();
    Check(minors.Contains(20)&&minors.Contains(50)&&minors.Contains(200),string.Join(",",minors.Take(12)));
    Check(!minors.Contains(10)&&!minors.Contains(100),"a decade is repeated as a minor line");
});
Test("A time axis takes no minor lines",()=>{
    Check(new Axis(AxisKind.Time,Utc(2026,1,1),Utc(2026,12,31)).MinorTicks().Count==0);
    var doc=Svg(TimeSpec(Utc(2026,1,1),86400000d,20) with{MinorGridlines=true});
    // The value axis still divides; the time axis does not.
    Check(MinorLines(doc)>0&&doc.Descendants(ns+"line").Where(l=>(string?)l.Attribute("class")=="lumen-grid-minor")
        .All(l=>l.Attribute("y1")!.Value==l.Attribute("y2")!.Value),"a time axis drew vertical minor lines");
});
Test("Minor lines are lighter than the labelled grid and sit behind the data",()=>{
    var markup=ChartSvg.Render(Spec() with{MinorGridlines=true});
    Check(markup.Contains(".lumen-grid-minor{stroke:var(--lumen-grid);stroke-width:1;stroke-opacity:.45}"));
    Check(markup.IndexOf("lumen-grid-minor'")<markup.IndexOf("data-point="),"minor lines are drawn over the data");
});
Test("Charts that derive their axis divide it too",()=>{
    foreach(var kind in (ChartKind[])[ChartKind.Histogram,ChartKind.Box])
    {
        var spec=Spec(kind) with{MinorGridlines=true,Series=[new("Sample",Enumerable.Range(0,40).Select(i=>new ChartPoint(i,i%7+1)).ToArray())]};
        Check(MinorLines(Svg(spec))>0,$"{kind} drew none");
    }
});
double Weekday(int offset)=>Utc(2026,1,5)+offset*86400000d;  // 5 January 2026 is a Monday.
double[] TradingDays(int span)=>Enumerable.Range(0,span).Select(Weekday)
    .Where(v=>TimeAxis.Moment(v).DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday).ToArray();
Test("A weekend axis puts the trading days side by side",()=>{
    var days=TradingDays(15);
    var axis=Axis.Create(AxisKind.Time,days,weekends:true);
    Check(axis.Skips.Count==2,$"{axis.Skips.Count} weekends over three weeks");
    var gaps=days.Zip(days.Skip(1),(a,b)=>Math.Round(axis.Map(b,0,1000)-axis.Map(a,0,1000),6)).Distinct().ToArray();
    Check(gaps.Length==1,"trading days are unevenly spaced: "+string.Join(",",gaps));
    var plain=Axis.Create(AxisKind.Time,days);
    Check(plain.Skips.Count==0&&days.Any(d=>Math.Abs(plain.Map(d,0,1000)-axis.Map(d,0,1000))>1),"the plain axis compressed too");
});
Test("A tick inside a skipped span is left out",()=>{
    var axis=Axis.Create(AxisKind.Time,TradingDays(15),weekends:true);
    var ticks=axis.Ticks(6);
    Check(ticks.Count>2,$"{ticks.Count} ticks left");
    Check(ticks.All(t=>!axis.Skips.Any(skip=>t.Value>=skip.From&&t.Value<skip.To)),"a tick landed inside a weekend");
    Check(ticks.All(t=>TimeAxis.Moment(t.Value).DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday),
        string.Join(",",ticks.Select(t=>t.Label)));
});
Test("A compressed axis inverts back to the moment it drew",()=>{
    var axis=Axis.Create(AxisKind.Time,TradingDays(15),weekends:true);
    foreach(var value in (double[])[Weekday(0),Weekday(1)+3600000d*9,Weekday(7),Weekday(14)])
        Check(Math.Abs(axis.Invert(axis.Map(value,40,900),40,900)-value)<1,"the round trip lost "+value);
    // A Saturday has no room of its own, so it sits where the weekend opens, and reading that
    // position back gives the Monday, which is the moment the axis resumes at.
    Check(Math.Abs(axis.Map(Utc(2026,1,10,12),0,1000)-axis.Map(Utc(2026,1,10),0,1000))<1e-9);
    Check(Math.Abs(axis.Invert(axis.Map(Utc(2026,1,10,12),0,1000),0,1000)-Weekday(7))<1);
});
Test("Weekends are counted where the market is",()=>{
    var york=TimeAxis.Zone("America/New_York");
    var skips=TimeAxis.Weekends(Weekday(0)+12*3600000d,Weekday(15),york);
    Check(skips.Count==2,$"{skips.Count} weekends");
    var sunday=TimeAxis.Weekends(Weekday(0),Weekday(15),york);
    Check(sunday.Count==3&&sunday[0].From==Weekday(0),"the tail of a New York weekend was dropped");
    // A New York Saturday begins at 05:00 UTC in January, not at midnight UTC.
    Check(TimeAxis.Moment(skips[0].From).UtcDateTime==new DateTime(2026,1,10,5,0,0),TimeAxis.Moment(skips[0].From).ToString("O"));
    Check(TimeAxis.Moment(skips[0].To).UtcDateTime==new DateTime(2026,1,12,5,0,0));
    Check(TimeAxis.Moment(TimeAxis.Weekends(Weekday(0),Weekday(15))[0].From).UtcDateTime==new DateTime(2026,1,10,0,0,0));
});
Test("Skipped spans are clipped, sorted and merged",()=>{
    var merged=TimeAxis.Normalise([new TimeSkip(50,80),new TimeSkip(10,30),new TimeSkip(25,40),new TimeSkip(200,300),new TimeSkip(-100,5)],0,250);
    // 10-30 and 25-40 overlap and become one; 50-80 stands apart.
    Check(string.Join(",",merged.Select(skip=>$"{skip.From}-{skip.To}"))=="0-5,10-40,50-80,200-250",
        string.Join(",",merged.Select(skip=>$"{skip.From}-{skip.To}")));
    Check(TimeAxis.Normalise([new TimeSkip(10,10),new TimeSkip(300,400)],0,250).Count==0,"an empty or outside span survived");
});
Test("A holiday leaves its day out",()=>{
    var axis=Axis.Create(AxisKind.Time,[Weekday(0),Weekday(4)],skips:[TimeAxis.Day(new DateTime(2026,1,7))]);
    Check(axis.Skips.Count==1);
    // Four days less the holiday leaves three, so a day is a third of the width.
    Check(Math.Abs(axis.Map(Weekday(3),0,1000)-axis.Map(Weekday(1),0,1000)-1000d/3)<1e-6,"the holiday still takes room");
    // The tick where the holiday opens shares a position with the day the market returns, so only one is drawn.
    Check(axis.Ticks(5).All(t=>t.Value!=Weekday(2)),string.Join(",",axis.Ticks(5).Select(t=>t.Label)));
});
Test("A chart leaves the weekends out of its axis and its labels",()=>{
    var days=TradingDays(15);
    var spec=Spec() with{XAxis=AxisKind.Time,SkipWeekends=true,
        Series=[new("Close",days.Select((v,i)=>new ChartPoint(v,100+i%5)).ToArray())]};
    var labels=Svg(spec).Descendants(ns+"text").Select(e=>e.Value).ToArray();
    Check(!labels.Contains("10 Jan")&&!labels.Contains("11 Jan"),"a weekend was labelled");
    Check(labels.Any(l=>l=="5 Jan"||l=="6 Jan"),string.Join("|",labels));
    Check(ChartSvg.Render(spec)!=ChartSvg.Render(spec with{SkipWeekends=false}),"the weekends made no difference");
});
Test("Skipped spans are rejected where they cannot apply",()=>{
    Reject(()=>ChartSvg.Render(Spec() with{SkipWeekends=true}));
    Reject(()=>ChartSvg.Render(Spec() with{TimeSkips=[new TimeSkip(1,2)]}));
    var time=TimeSpec(Weekday(0),86400000d,10);
    Reject(()=>ChartSvg.Render(time with{TimeSkips=[new TimeSkip(5,5)]}));
    Reject(()=>ChartSvg.Render(time with{TimeSkips=[new TimeSkip(0,TimeAxis.MaxValue+1)]}));
    Reject(()=>ChartSvg.Render(time with{TimeSkips=Enumerable.Range(0,401).Select(i=>new TimeSkip(i*10,i*10+5)).ToArray()}));
    Check(ChartSvg.Render(time with{SkipWeekends=true,TimeSkips=[TimeAxis.Day(new DateTime(2026,1,7))]}).Length>0);
});
Test("A least-squares line recovers the line it was given",()=>{
    var fit=Statistics.Fit(Enumerable.Range(0,20).Select(i=>((double)i,3d*i+2)))!;
    Check(Math.Abs(fit.Slope-3)<1e-9&&Math.Abs(fit.Intercept-2)<1e-9,$"{fit.Slope} {fit.Intercept}");
    Check(Math.Abs(fit.R2-1)<1e-12&&fit.Count==20);
    Check(Math.Abs(fit.Predict(100)-302)<1e-9);
});
Test("A fit needs two observations and some spread in X",()=>{
    Check(Statistics.Fit([])is null&&Statistics.Fit([(1d,1d)])is null,"a line was fitted through nothing");
    Check(Statistics.Fit([(5d,1d),(5d,9d)])is null,"a vertical line was fitted");
    // One Y throughout is explained perfectly by a flat line rather than being undefined.
    var flat=Statistics.Fit([(1d,4d),(2d,4d),(3d,4d)])!;
    Check(flat.Slope==0&&flat.R2==1&&Math.Abs(flat.Intercept-4)<1e-12);
});
Test("R squared falls as the observations scatter",()=>{
    double[] xs=[1,2,3,4,5,6];
    var tight=Statistics.Fit(xs.Select(x=>(x,2*x+(x%2==0?.1:-.1))))!;
    var loose=Statistics.Fit(xs.Select(x=>(x,2*x+(x%2==0?6d:-6d))))!;
    Check(tight.R2>.99&&loose.R2<.5,$"{tight.R2} {loose.R2}");
    Check(Statistics.Fit(xs.Select(x=>(x,-4*x+1)))!.Slope<0);
});
Test("A time axis keeps its precision when fitted",()=>{
    // Unix milliseconds are large enough that an uncentred fit loses the slope entirely.
    var day=86400000d;var start=Utc(2026,1,5);
    var fit=Statistics.Fit(Enumerable.Range(0,30).Select(i=>(start+i*day,100+i*.5)))!;
    Check(Math.Abs(fit.Slope*day-.5)<1e-9,$"slope per day {fit.Slope*day}");
    Check(Math.Abs(fit.Predict(start+29*day)-114.5)<1e-9);
});
Test("A trend line is drawn only when a series asks for it",()=>{
    var scatter=Spec(ChartKind.Scatter);
    Check(!ChartSvg.Render(scatter).Contains("lumen-trend"),"a trend appeared uninvited");
    var trended=scatter with{Series=[scatter.Series[0] with{Trend=true}]};
    var path=Svg(trended).Descendants(ns+"path").Single(e=>(string?)e.Attribute("class")=="lumen-trend");
    Check((string?)path.Attribute("stroke-dasharray")=="7 5"&&(string?)path.Attribute("fill")=="none");
    Check(path.Value.Contains("R squared")&&path.Value.Contains("Series trend"),path.Value);
    // Two stops on a plain axis, so the line is straight.
    Check(((string)path.Attribute("d")!).Count(c=>c=='M'||c=='L')==2,(string)path.Attribute("d")!);
});
Test("A trend line spans the plot and follows the data",()=>{
    var spec=Classic(Spec(ChartKind.Scatter) with{Series=[new("Rising",Enumerable.Range(0,10).Select(i=>new ChartPoint(i,i*2+1)).ToArray()){Trend=true}]});
    var d=(string)Svg(spec).Descendants(ns+"path").Single(e=>(string?)e.Attribute("class")=="lumen-trend").Attribute("d")!;
    var ends=d.Split(' ').Select(part=>part.TrimStart('M','L').Split(',').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray()).ToArray();
    Check(ends[0][0]<ends[1][0],"the line does not run left to right");
    Check(ends[0][1]>ends[1][1],"a rising series drew a falling line");
    // The marks sit on the line, because the observations are exactly linear.
    var marks=Svg(spec).Descendants(ns+"circle").Select(c=>(X:(double)c.Attribute("cx")!,Y:(double)c.Attribute("cy")!)).ToArray();
    var slope=(ends[1][1]-ends[0][1])/(ends[1][0]-ends[0][0]);
    Check(marks.All(m=>Math.Abs(ends[0][1]+slope*(m.X-ends[0][0])-m.Y)<.5),"the marks do not sit on the line");
});
Test("A logarithmic axis is fitted in the space it draws",()=>{
    // Powers of ten are a straight line on a log axis, and nothing else would be.
    var spec=Classic(Spec(ChartKind.Scatter) with{YAxis=AxisKind.Log,
        Series=[new("Growth",Enumerable.Range(1,5).Select(i=>new ChartPoint(i,Math.Pow(10,i))).ToArray()){Trend=true}]});
    var d=(string)Svg(spec).Descendants(ns+"path").Single(e=>(string?)e.Attribute("class")=="lumen-trend").Attribute("d")!;
    var ends=d.Split(' ').Select(part=>part.TrimStart('M','L').Split(',').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray()).ToArray();
    var marks=Svg(spec).Descendants(ns+"circle").Select(c=>(X:(double)c.Attribute("cx")!,Y:(double)c.Attribute("cy")!)).ToArray();
    var slope=(ends[1][1]-ends[0][1])/(ends[1][0]-ends[0][0]);
    Check(marks.All(m=>Math.Abs(ends[0][1]+slope*(m.X-ends[0][0])-m.Y)<.5),"the log fit missed its own points");
});
Test("A compressed axis takes one straight trend, not a jump per weekend",()=>{
    var days=TradingDays(15);
    var spec=Classic(Spec() with{Kind=ChartKind.Scatter,XAxis=AxisKind.Time,SkipWeekends=true,
        Series=[new("Close",days.Select((v,i)=>new ChartPoint(v,100+i)).ToArray()){Trend=true}]});
    var doc=Svg(spec);
    var d=(string)doc.Descendants(ns+"path").Single(e=>(string?)e.Attribute("class")=="lumen-trend").Attribute("d")!;
    Check(d.Count(c=>c=='M'||c=='L')==2,d);
    // The observations rise by one a day with the weekends left out, so they all sit on the line.
    var ends=d.Split(' ').Select(part=>part.TrimStart('M','L').Split(',').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray()).ToArray();
    var slope=(ends[1][1]-ends[0][1])/(ends[1][0]-ends[0][0]);
    var marks=doc.Descendants(ns+"circle").Select(c=>(X:(double)c.Attribute("cx")!,Y:(double)c.Attribute("cy")!)).ToArray();
    Check(marks.Length==days.Length&&marks.All(m=>Math.Abs(ends[0][1]+slope*(m.X-ends[0][0])-m.Y)<.5),"the marks do not sit on the line");
});
Test("A trend says which way it runs",()=>{
    string Label(bool up)=>Svg(Spec(ChartKind.Scatter) with{Series=[new("S",Enumerable.Range(0,6)
        .Select(i=>new ChartPoint(i,up?i*2+1:20-i*2)).ToArray()){Trend=true}]})
        .Descendants(ns+"path").Single(e=>(string?)e.Attribute("class")=="lumen-trend").Value;
    Check(Label(true).Contains("rising")&&Label(false).Contains("falling"),Label(true)+" | "+Label(false));
});
Test("A trend line is refused where marks sit by index",()=>{
    foreach(var kind in (ChartKind[])[ChartKind.Column,ChartKind.Bar,ChartKind.StackedColumn,ChartKind.Donut,ChartKind.Histogram,ChartKind.Box])
        Reject(()=>ChartSvg.Render(Sample(kind) with{Series=Sample(kind).Series.Select(series=>series with{Trend=true}).ToArray()}));
    var series=Spec().Series[0] with{Trend=true};
    Check(ChartSvg.Render(Spec() with{Series=[series]}).Contains("lumen-trend"));
});
Test("A series with no spread in X draws no trend",()=>{
    var spec=Spec(ChartKind.Scatter) with{Series=[new("Column",[new(4,1),new(4,9)]){Trend=true}]};
    Check(!ChartSvg.Render(spec).Contains("lumen-trend"),"a vertical trend was drawn");
});
double[] Normal(int count,double center,double spread,int seed)
{
    var random=new Random(seed);
    return Enumerable.Range(0,count).Select(_=>{
        double u1=1-random.NextDouble(),u2=random.NextDouble();
        return center+spread*Math.Sqrt(-2*Math.Log(u1))*Math.Cos(2*Math.PI*u2);}).ToArray();
}
Test("A density estimate covers the observed range and no more",()=>{
    var values=Normal(200,50,8,7);
    var estimate=Statistics.Density(values);
    Check(estimate.Count==64);
    Check(Math.Abs(estimate[0].Value-values.Min())<1e-9&&Math.Abs(estimate[^1].Value-values.Max())<1e-9,"the grid leaves the data");
    Check(estimate.All(point=>point.Density>=0));
    // The trapezoid over the observed range carries nearly all of the mass.
    var area=estimate.Zip(estimate.Skip(1),(a,b)=>(b.Value-a.Value)*(a.Density+b.Density)/2).Sum();
    Check(area>.9&&area<1.001,$"area {area}");
});
Test("A density estimate needs two observations and some spread",()=>{
    Check(Statistics.Density([]).Count==0&&Statistics.Density([4d]).Count==0);
    Check(Statistics.Density([4d,4d,4d]).Count==0,"a flat set produced a shape");
    Check(Statistics.Density([1d,2d,3d],1).Count==0,"one sample is not a curve");
});
Test("A density estimate finds the peak and the gap a box plot hides",()=>{
    var single=Statistics.Density(Normal(300,20,3,11));
    var peak=single.MaxBy(point=>point.Density).Value;
    Check(Math.Abs(peak-20)<2,$"peak at {peak}");
    // Two separated groups: the estimate dips between them, which is the whole point of a violin.
    var split=Statistics.Density([..Normal(200,10,1.5,3),..Normal(200,40,1.5,4)]);
    var middle=split.Where(point=>point.Value>20&&point.Value<30).Max(point=>point.Density);
    var modes=split.Where(point=>point.Value<20||point.Value>30).Max(point=>point.Density);
    Check(middle<modes/4,$"the valley is {middle} against peaks of {modes}");
});
Test("A violin mirrors its estimate about its own column",()=>{
    var spec=Spec(ChartKind.Violin) with{Series=[new("Europe",Normal(120,40,6,5).Select((v,i)=>new ChartPoint(i,v)).ToArray())]};
    var doc=Svg(spec);
    var outline=(string)doc.Descendants(ns+"path").Single(e=>((string?)e.Attribute("fill-opacity"))==".22").Attribute("d")!;
    var xs=outline.TrimStart('M').Split(' ').Where(pair=>pair!="Z").Select(pair=>double.Parse(pair.Split(',')[0],CultureInfo.InvariantCulture)).ToArray();
    Check(xs.Length==128,$"{xs.Length} points");
    var center=(xs.Min()+xs.Max())/2;
    // Every point on the right has its twin on the left.
    Check(xs.Take(64).Zip(xs.Skip(64).Reverse()).All(pair=>Math.Abs(pair.First-center-(center-pair.Second))<1e-6),"the sides do not match");
    Check(doc.Descendants(ns+"text").Any(e=>e.Value.StartsWith("Europe (n=120")),"the column is not named and counted");
});
Test("A violin carries its quartiles, median and count to a reader",()=>{
    var spec=Spec(ChartKind.Violin) with{Series=[new("Latency",Enumerable.Range(1,20).Select(i=>new ChartPoint(i,i*1d)).ToArray())]};
    var label=Svg(spec).Descendants(ns+"g").Single(e=>(string?)e.Attribute("class")=="lumen-datum").Attribute("aria-label")!.Value;
    Check(label.Contains("20 observations")&&label.Contains("median 10.5")&&label.Contains("quartiles 5.75 to 15.25")&&label.Contains("range 1 to 20"),label);
    var doc=Svg(spec);
    Check(doc.Descendants(ns+"rect").Any()&&doc.Descendants(ns+"line").Any(l=>(string?)l.Attribute("stroke-width")=="2.5"),"no quartile bar or median tick");
});
Test("Violins share one axis and stand in their own columns",()=>{
    var spec=Spec(ChartKind.Violin) with{Series=[
        new("Low",Normal(80,10,2,1).Select((v,i)=>new ChartPoint(i,v)).ToArray()),
        new("High",Normal(80,60,2,2).Select((v,i)=>new ChartPoint(i,v)).ToArray())]};
    var doc=Svg(spec);
    var outlines=doc.Descendants(ns+"path").Where(e=>((string?)e.Attribute("fill-opacity"))==".22").ToArray();
    Check(outlines.Length==2);
    double Center(XElement path){var xs=((string)path.Attribute("d")!).TrimStart('M').Split(' ').Where(pair=>pair!="Z")
        .Select(pair=>double.Parse(pair.Split(',')[0],CultureInfo.InvariantCulture)).ToArray();return (xs.Min()+xs.Max())/2;}
    Check(Center(outlines[0])<Center(outlines[1]),"the columns overlap");
    // A violin chart names its columns on the axis, so it needs no legend.
    Check(!doc.Descendants(ns+"text").Any(e=>e.Value=="Low"&&(string?)e.Parent!.Attribute("class")=="lumen-legend"));
});
Test("A logarithmic violin is shaped in logarithms",()=>{
    var values=Enumerable.Range(0,120).Select(i=>Math.Pow(10,1+i%3)).ToArray();
    var spec=Spec(ChartKind.Violin) with{YAxis=AxisKind.Log,Series=[new("Traffic",values.Select((v,i)=>new ChartPoint(i,v)).ToArray())]};
    var doc=Svg(spec);
    var d=(string)doc.Descendants(ns+"path").Single(e=>((string?)e.Attribute("fill-opacity"))==".22").Attribute("d")!;
    Check(!d.Contains("NaN")&&!d.Contains("Infinity"),d[..60]);
    // Three decades, evenly spaced on a log axis, give three evenly spaced bulges.
    var points=d.TrimStart('M').Split(' ').Take(64).Select(pair=>pair.Split(',').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray()).ToArray();
    // The widest places are the three decades; the outermost two are the ends of the grid, because
    // the smallest and largest observations are themselves modes.
    // Rounded coordinates make a peak two samples wide, so a plateau counts once, at its far end.
    var bulges=points.Where((pt,i)=>(i==0||pt[0]>=points[i-1][0])&&(i==points.Length-1||pt[0]>points[i+1][0])).ToArray();
    Check(bulges.Length==3,$"{bulges.Length} bulges in {string.Join(" ",points.Select(pt=>pt[0].ToString("0",CultureInfo.InvariantCulture)))}");
    Check(Math.Abs((bulges[0][1]-bulges[1][1])-(bulges[1][1]-bulges[2][1]))<6,"the decades are not evenly spaced");
});
Test("A violin refuses what it cannot draw",()=>{
    var spec=Sample(ChartKind.Violin);
    Reject(()=>ChartSvg.Render(spec with{Annotations=[new ChartAnnotation(AnnotationAxis.Y,3)]}));
    Reject(()=>ChartSvg.Render(spec with{XAxis=AxisKind.Time}));
    Reject(()=>ChartSvg.Render(spec with{Series=[spec.Series[0] with{Trend=true}]}));
    Reject(()=>ChartSvg.Render(spec with{Series=[spec.Series[0],spec.Series[0] with{Name="Second",Secondary=true}]}));
    Check(ChartSvg.Render(spec with{YAxis=AxisKind.Log,Series=[new("Positive",Enumerable.Range(1,30).Select(i=>new ChartPoint(i,i*3d)).ToArray())]}).Contains("lumen-datum"));
});
Test("A violin with one observation still reports it",()=>{
    var doc=Svg(Spec(ChartKind.Violin) with{Series=[new("Single",[new(0,5)])]});
    Check(!doc.ToString().Contains("fill-opacity='.22'"),"a single observation drew a shape");
    Check(doc.Descendants(ns+"g").Any(e=>(string?)e.Attribute("class")=="lumen-datum"),"the column vanished");
});
ChartSpec Ohlc()=>Candles() with{Kind=ChartKind.Ohlc};
XElement[] Marks(ChartSpec spec)=>Svg(spec).Descendants(ns+"g").Where(e=>e.Attribute("data-point") is not null).ToArray();
double Coord(XElement line,string name)=>(double)line.Attribute(name)!;
Test("An OHLC bar ticks the open to the left and the close to the right",()=>{
    var marks=Marks(Ohlc());
    Check(marks.Length==3);
    Check(marks.All(m=>m.Elements(ns+"line").Count()==3&&!m.Elements(ns+"rect").Any()),"an OHLC bar is three lines and no body");
    foreach(var mark in marks)
    {
        var lines=mark.Elements(ns+"line").ToArray();
        var range=lines.Single(l=>Coord(l,"x1")==Coord(l,"x2"));
        var centre=Coord(range,"x1");
        var open=lines.Single(l=>Coord(l,"x2")==centre&&Coord(l,"x1")<centre);
        var close=lines.Single(l=>Coord(l,"x1")==centre&&Coord(l,"x2")>centre);
        Check(Coord(open,"y1")==Coord(open,"y2")&&Coord(close,"y1")==Coord(close,"y2"),"a price tick is not horizontal");
        Check(centre-Coord(open,"x1")==Coord(close,"x2")-centre,"the ticks are not the same length");
        Check(Coord(range,"y1")<Coord(open,"y1")&&Coord(open,"y1")<Coord(range,"y2"),"the open sits outside the high-low range");
        Check(Coord(range,"y1")<Coord(close,"y1")&&Coord(close,"y1")<Coord(range,"y2"),"the close sits outside the high-low range");
    }
});
Test("An OHLC bar ticks the prices a candle body would start and end at",()=>{
    var bars=Marks(Ohlc());
    var bodies=Marks(Candles()).Select(m=>m.Element(ns+"rect")!)
        .Select(r=>(Top:Coord(r,"y"),Bottom:Coord(r,"y")+Coord(r,"height"))).ToArray();
    for(var i=0;i<bars.Length;i++)
    {
        var lines=bars[i].Elements(ns+"line").ToArray();
        var centre=Coord(lines.Single(l=>Coord(l,"x1")==Coord(l,"x2")),"x1");
        var open=lines.Single(l=>Coord(l,"x2")==centre&&Coord(l,"x1")<centre);
        var close=lines.Single(l=>Coord(l,"x1")==centre&&Coord(l,"x2")>centre);
        // A rising day closes above it opens, so its close is the top of the body and its open the bottom.
        var rising=(string?)close.Attribute("stroke")==ChartSvg.RisingColor;
        Check(Coord(close,"y1")==(rising?bodies[i].Top:bodies[i].Bottom),$"bar {i}: the close is not where the body ends");
        Check(Coord(open,"y1")==(rising?bodies[i].Bottom:bodies[i].Top),$"bar {i}: the open is not where the body starts");
    }
});
Test("An OHLC bar is coloured by direction, from the style",()=>{
    var colours=Marks(Ohlc()).Select(m=>m.Elements(ns+"line").Select(l=>(string?)l.Attribute("stroke")).Distinct().Single()).ToArray();
    Check(colours[0]==ChartSvg.RisingColor&&colours[1]==ChartSvg.FallingColor&&colours[2]==ChartSvg.RisingColor,string.Join(",",colours));
    var branded=ChartSvg.Render(Ohlc() with{Style=Brand()});
    Check(branded.Contains("stroke='#2E7D5B'")&&branded.Contains("stroke='#B03A2E'"),"a styled OHLC chart ignores the style's directions");
});
Test("An OHLC line spans the full high-low range",()=>{
    (double Top,double Bottom)[] Ranges(ChartSpec spec)=>Marks(spec)
        .Select(m=>m.Elements(ns+"line").Single(l=>Coord(l,"x1")==Coord(l,"x2")))
        .Select(l=>(Coord(l,"y1"),Coord(l,"y2"))).ToArray();
    var ranges=Ranges(Ohlc());
    Check(ranges.SequenceEqual(Ranges(Candles())),"the bars and the candles disagree about the range");
    // Pixel height follows the price range: 13 to 9.2 against 12.5 to 9.5.
    Check(Math.Abs((ranges[2].Bottom-ranges[2].Top)/(ranges[0].Bottom-ranges[0].Top)-3.8/3)<1e-9,"the line is not proportional to the range");
});
Test("An OHLC mark names all four prices",()=>{
    var label=Marks(Ohlc())[0].Attribute("aria-label")!.Value;
    Check(label.Contains("open 10")&&label.Contains("high 12.5")&&label.Contains("low 9.5")&&label.Contains("close 11.8"),label);
    Check(Marks(Ohlc())[0].Element(ns+"title")!.Value==label,"the native tooltip and the accessible name disagree");
});
Test("OHLC accepts a time X axis and a log Y axis",()=>{
    var start=TimeAxis.Value(new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero));
    var doc=Svg(Ohlc() with{XAxis=AxisKind.Time,YAxis=AxisKind.Log,Series=[new("Price",[
        ChartPoint.Candle(start,10,12,9,11),ChartPoint.Candle(start+30*86400000d,11,140,10,130),ChartPoint.Candle(start+60*86400000d,130,1400,120,1200)])]});
    Check(doc.Descendants(ns+"text").Any(e=>e.Value.Contains("Jan")));
    Check(doc.Descendants(ns+"text").Any(e=>e.Value=="100"));
    // Weekends left out of the axis, and a reference behind the bars, as the candlestick takes them.
    var trading=Svg(Ohlc() with{XAxis=AxisKind.Time,SkipWeekends=true,Annotations=[new(AnnotationAxis.Y,11){Label="Open"}],
        Series=[new("Price",Enumerable.Range(0,10).Select(i=>ChartPoint.Candle(start+i*86400000d,10,12,9,11)).ToArray())]});
    Check(!trading.Descendants(ns+"text").Any(e=>e.Value=="3 Jan"),"a Saturday was labelled");
    Check(trading.Descendants(ns+"text").Any(e=>e.Value=="Open: 11"));
});
Test("An OHLC chart refuses what a candlestick refuses",()=>{
    Func<ChartSpec,ChartSpec>[] broken=[
        s=>s with{Series=[new("P",[new(0,1)])]},
        s=>s with{Series=[new("P",[ChartPoint.Candle(0,10,10.5,9,11)])]},
        s=>s with{Series=[new("P",[ChartPoint.Candle(0,10,12,10.5,11)])]},
        s=>s with{Series=[s.Series[0],s.Series[0] with{Name="Second"}]},
        s=>s with{Series=[s.Series[0] with{Trend=true}]},
        s=>s with{Series=[s.Series[0],s.Series[0] with{Name="Second",Secondary=true}]},
        s=>s with{YAxis=AxisKind.Log,Series=[new("P",[ChartPoint.Candle(0,1,2,0,1.5)])]},
        s=>s with{Series=[new("P",[ChartPoint.Candle(2,10,12,9,11),ChartPoint.Candle(1,10,12,9,11)])]}];
    foreach(var kind in (ChartKind[])[ChartKind.Candlestick,ChartKind.Ohlc])
        foreach(var mutate in broken) Reject(()=>ChartSvg.Render(mutate(Sample(kind))));
});
Test("OHLC CSV exports the four prices",()=>{
    var csv=ChartExport.Csv(Ohlc());
    Check(csv.StartsWith("Series,X,Y,Label,Size,Open,High,Low,Close"));
    Check(csv.Contains("10,12.5,9.5,11.8"));
});
BoxSummary Warehouse()=>new(20,30,45,5,70,[90,2]);
ChartSpec Supplied(BoxSummary summary,params ChartPoint[] points)=>Spec(ChartKind.Box) with{Series=[new("Asia",points){Summary=summary}]};
XElement Glyph(XDocument doc)=>doc.Descendants(ns+"g").First(e=>(string?)e.Attribute("class")=="lumen-datum"&&e.Element(ns+"rect") is not null);
double[] Grid(XDocument doc)=>doc.Descendants(ns+"line").Where(l=>(string?)l.Attribute("class")=="lumen-grid").Select(l=>Coord(l,"y1")).ToArray();
Test("A supplied summary is drawn where its five numbers say",()=>{
    var summary=Warehouse();
    var doc=Svg(Supplied(summary));
    var glyph=Glyph(doc);
    var lines=glyph.Elements(ns+"line").ToArray();
    var stem=lines.Single(l=>Coord(l,"x1")==Coord(l,"x2"));
    var median=lines.Single(l=>(string?)l.Attribute("stroke-width")=="2.5");
    var box=glyph.Element(ns+"rect")!;
    // The whisker ends fix one map from value to pixel; every other number must land on it.
    double upper=Coord(stem,"y1"),lower=Coord(stem,"y2");
    double At(double value)=>lower+(value-summary.LowerWhisker)/(summary.UpperWhisker-summary.LowerWhisker)*(upper-lower);
    Check(Math.Abs(Coord(box,"y")-At(summary.Q3))<1e-6,"the box does not start at Q3");
    Check(Math.Abs(Coord(box,"y")+Coord(box,"height")-At(summary.Q1))<1e-6,"the box does not end at Q1");
    Check(Coord(median,"y1")==Coord(median,"y2")&&Math.Abs(Coord(median,"y1")-At(summary.Median))<1e-6,"the median line is misplaced");
    var caps=lines.Where(l=>l!=stem&&l!=median).Select(l=>Coord(l,"y1")).Order().ToArray();
    Check(caps.SequenceEqual([upper,lower]),"the whisker caps are not at the whisker ends");
    var marks=doc.Descendants(ns+"circle").Select(c=>Coord(c,"cy")).Order().ToArray();
    Check(marks.Length==2&&Math.Abs(marks[0]-At(90))<1e-6&&Math.Abs(marks[1]-At(2))<1e-6,"the outliers are misplaced");
    // The axis reaches the outliers: its grid matches a chart of observations spanning 2 to 90.
    var raw=Svg(Spec(ChartKind.Box) with{Series=[new("Asia",new[]{2d,5,20,30,45,70,90}.Select(ChartPoint.Observation).ToArray())]});
    Check(Grid(doc).SequenceEqual(Grid(raw)),"the axis does not reach the outliers");
});
Test("A supplied summary's outliers name no point to select",()=>{
    var doc=Svg(Supplied(Warehouse()));
    Check(!doc.Descendants().Any(e=>e.Attribute("data-point") is not null||e.Attribute("data-series") is not null),"a supplied outlier claims a point index");
    var outliers=doc.Descendants(ns+"g").Where(e=>e.Element(ns+"circle") is not null).ToArray();
    Check(outliers.Length==2);
    Check(outliers.All(o=>(string?)o.Attribute("class")=="lumen-datum"&&(string?)o.Attribute("tabindex")=="0"&&(string?)o.Attribute("role")=="img"),"an outlier is not a focusable aggregate");
    var labels=outliers.Select(o=>o.Attribute("aria-label")!.Value).Order().ToArray();
    Check(labels.SequenceEqual(["Asia outlier (supplied summary): 2","Asia outlier (supplied summary): 90"]),string.Join(" | ",labels));
    // Beside it, a series of observations still raises its own outlier.
    var mixed=Svg(Spec(ChartKind.Box) with{Series=[new("Asia",[]){Summary=Warehouse()},new("Europe",[new(0,1),new(1,2),new(2,3),new(3,4),new(4,5),new(5,100)])]});
    var indexed=mixed.Descendants(ns+"g").Single(e=>e.Attribute("data-point") is not null);
    Check((string?)indexed.Attribute("data-series")=="1"&&(string?)indexed.Attribute("data-point")=="5","the observed outlier lost its index");
});
Test("A supplied summary says so and claims no observation count",()=>{
    var doc=Svg(Spec(ChartKind.Box) with{Series=[new("Asia",[]){Summary=Warehouse()},new("Europe",Enumerable.Range(1,9).Select(i=>new ChartPoint(i,i*10d)).ToArray())]});
    var texts=doc.Descendants(ns+"text").Select(e=>e.Value).ToArray();
    Check(texts.Contains("Asia (summary)")&&!texts.Any(t=>t.Contains("Asia (n=")),"the summary column claims a count");
    Check(texts.Contains("Europe (n=9)"),"the observed column lost its count");
    var label=Glyph(doc).Attribute("aria-label")!.Value;
    Check(label=="Asia (supplied summary): median 30, quartiles 20 to 45, whiskers 5 to 70, 2 outliers",label);
    Check(RenderInside(Brand(),Supplied(Warehouse())).Contains("Asia (summary)"),"the component did not draw the summary");
});
Test("A supplied summary is drawn as given, whatever its whisker rule",()=>{
    // Minimum and maximum whiskers reach well past Tukey's fences, and an outlier may sit inside them.
    var doc=Svg(Supplied(new(20,30,40,0,200,[35])));
    Check(Glyph(doc).Attribute("aria-label")!.Value.Contains("whiskers 0 to 200, 1 outliers"));
    Check(doc.Descendants(ns+"circle").Count()==1);
    Check(Svg(Supplied(new(5,5,5,5,5,[]))).Descendants(ns+"rect").Any(),"a summary with no spread drew nothing");
});
Test("Reject a summary that cannot be drawn honestly",()=>{
    var good=Warehouse();
    Reject(()=>ChartSvg.Render(Supplied(good,new ChartPoint(0,25))));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Box) with{Series=[new("Asia",null!){Summary=good}]}));
    Reject(()=>ChartSvg.Render(Supplied(good with{Outliers=null!})));
    foreach(var bad in new[]{double.NaN,double.PositiveInfinity,1e101})
    {
        Reject(()=>ChartSvg.Render(Supplied(good with{LowerWhisker=bad})));
        Reject(()=>ChartSvg.Render(Supplied(good with{Q1=bad})));
        Reject(()=>ChartSvg.Render(Supplied(good with{Median=bad})));
        Reject(()=>ChartSvg.Render(Supplied(good with{Q3=bad})));
        Reject(()=>ChartSvg.Render(Supplied(good with{UpperWhisker=bad})));
        Reject(()=>ChartSvg.Render(Supplied(good with{Outliers=[90,bad]})));
    }
    Reject(()=>ChartSvg.Render(Supplied(good with{LowerWhisker=21})));
    Reject(()=>ChartSvg.Render(Supplied(good with{Q1=31})));
    Reject(()=>ChartSvg.Render(Supplied(good with{Median=46})));
    Reject(()=>ChartSvg.Render(Supplied(good with{Q3=71})));
    Reject(()=>ChartSvg.Render(Supplied(good with{UpperWhisker=44})));
    Check(ChartSvg.Render(Supplied(good)).Contains("Asia (summary)"));
});
Test("Only a box chart takes a supplied summary",()=>{
    foreach(var kind in Enum.GetValues<ChartKind>().Where(kind=>kind!=ChartKind.Box))
    {
        try { ChartSvg.Render(Spec(kind) with{Series=[new("Asia",[]){Summary=Warehouse()}]}); throw new Exception($"{kind} drew a summary"); }
        catch(ArgumentException error) { Check(error.Message.Contains("box charts only"),$"{kind}: {error.Message}"); }
    }
});
Test("A supplied summary on a log axis needs positive numbers and reaches its outliers",()=>{
    var spec=Supplied(new(40,90,300,8,900,[5000,2])) with{YAxis=AxisKind.Log};
    var doc=Svg(spec);
    Check(doc.Descendants(ns+"text").Any(e=>e.Value=="1000"),"the decades stop short of the outlier");
    Reject(()=>ChartSvg.Render(Supplied(new(40,90,300,0,900,[])) with{YAxis=AxisKind.Log}));
    Reject(()=>ChartSvg.Render(Supplied(new(40,90,300,8,900,[-1])) with{YAxis=AxisKind.Log}));
});
Test("A supplied summary survives a JSON round trip",()=>{
    var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    var spec=Supplied(Warehouse());
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(System.Text.Json.JsonSerializer.Serialize(spec,options),options)!;
    Check(ChartSvg.Render(back)==ChartSvg.Render(spec),"the summary changed in transit");
    // A host writing JSON by hand names the five numbers and leaves out what the record derives.
    var json="{\"title\":\"Example\",\"kind\":\"Box\",\"series\":[{\"name\":\"Asia\",\"points\":[],\"summary\":{\"q1\":20,\"median\":30,\"q3\":45,\"lowerWhisker\":5,\"upperWhisker\":70,\"outliers\":[90,2]}}]}";
    Check(ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,options)!)==ChartSvg.Render(spec),"hand-written JSON drew something else");
});
ChartSpec Compared(int? bins,params (string Name,double[] Values)[] sets)=>Spec(ChartKind.Histogram) with{Bins=bins,
    Series=sets.Select(set=>new ChartSeries(set.Name,set.Values.Select(ChartPoint.Observation).ToArray())).ToArray()};
XElement[] Bars(XDocument doc)=>doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-datum"&&e.Element(ns+"rect") is not null).ToArray();
double[] Early()=>Enumerable.Range(0,60).Select(i=>i*.8).ToArray();
double[] Late()=>Enumerable.Range(0,45).Select(i=>30+i*1.6).ToArray();
Test("Several histogram series share one set of bin edges",()=>{
    var sets=Statistics.SharedBins([Early(),Late()]);
    Check(sets.Count==2&&sets[0].Select(b=>(b.Start,b.End)).SequenceEqual(sets[1].Select(b=>(b.Start,b.End))),"the edges differ between series");
    var pooled=Statistics.Bins([..Early(),..Late()]);
    Check(pooled.Select(b=>(b.Start,b.End)).SequenceEqual(sets[0].Select(b=>(b.Start,b.End))),"the edges are not the pooled data's");
    Check(pooled.Select(b=>b.Count).SequenceEqual(sets[0].Zip(sets[1],(a,b)=>a.Count+b.Count)),"the series do not add up to the pooled counts");
    Check(sets[0].Sum(b=>b.Count)==60&&sets[1].Sum(b=>b.Count)==45);
    // The chart reads the same ranges for both series.
    var ranges=Bars(Svg(Compared(null,("Early",Early()),("Late",Late())))).Select(b=>b.Attribute("aria-label")!.Value)
        .GroupBy(l=>l.Split(", ")[0],l=>l.Split(", ")[1].Split(':')[0]).Select(g=>g.ToArray()).ToArray();
    Check(ranges.Length==2&&ranges[0].Length==pooled.Count&&ranges[0].SequenceEqual(ranges[1]),"the chart labels different ranges per series");
});
Test("Each histogram series counts its own observations in each shared bin",()=>{
    var doc=Svg(Compared(4,("A",[0,1,5,12,15,25,39]),("B",[8,18,19,22,28,31,40])));
    var bars=Bars(doc);
    // Counted by hand over edges 0, 10, 20, 30 and 40, the last bin closed at the top.
    string[] expected=["A, 0 to 10: 3 observations","B, 0 to 10: 1 observations","A, 10 to 20: 2 observations","B, 10 to 20: 2 observations",
        "A, 20 to 30: 1 observations","B, 20 to 30: 2 observations","A, 30 to 40: 1 observations","B, 30 to 40: 2 observations"];
    var labels=bars.Select(b=>b.Attribute("aria-label")!.Value).ToArray();
    Check(labels.SequenceEqual(expected),string.Join(" | ",labels));
    var heights=bars.Select(b=>Coord(b.Element(ns+"rect")!,"height")).ToArray();
    Check(Math.Abs(heights[0]-3*heights[1])<1e-6&&Math.Abs(heights[2]-heights[3])<1e-6,"bar heights do not follow the counts");
});
Test("Bars in one bin stand side by side inside it",()=>{
    double[] a=Early(),b=Late(),c=Enumerable.Range(0,30).Select(i=>10+i*2.5).ToArray();
    var bars=Bars(Svg(Compared(6,("A",a),("B",b),("C",c)))).Select(e=>e.Element(ns+"rect")!).ToArray();
    // The pooled observations as one series fill each bin with a single bar, which marks the bin.
    var whole=Bars(Svg(Compared(6,("All",[..a,..b,..c])))).Select(e=>e.Element(ns+"rect")!).ToArray();
    Check(bars.Length==18&&whole.Length==6);
    for(var i=0;i<6;i++)
    {
        var group=bars.Skip(i*3).Take(3).ToArray();
        double start=Coord(whole[i],"x"),end=start+Coord(whole[i],"width");
        double first=Coord(group[0],"x"),last=Coord(group[2],"x")+Coord(group[2],"width");
        Check(first>start&&last<end,$"bin {i}: the bars leave the bin or run into the next");
        Check(Math.Abs((first-start)-(end-last))<1e-6,$"bin {i}: the group is not centred in its bin");
        for(var k=0;k<2;k++)Check(Coord(group[k],"x")+Coord(group[k],"width")<=Coord(group[k+1],"x")+1e-6,$"bin {i}: bars {k} and {k+1} overlap");
        Check(group.All(r=>Coord(r,"width")>0),$"bin {i}: a bar has no width");
        Check(group.Select(r=>(string?)r.Attribute("fill")).SequenceEqual(ChartSvg.Palette.Take(3)),$"bin {i}: the bars are not in their series colours");
    }
});
Test("A histogram of one series keeps its labels and draws no legend",()=>{
    var doc=Svg(Distribution(6));
    Check((string?)doc.Root!.Attribute("viewBox")=="0 0 900 420","a single histogram grew a legend");
    Check(Bars(doc).All(b=>System.Text.RegularExpressions.Regex.IsMatch(b.Attribute("aria-label")!.Value,@"^[\d.]+ to [\d.]+: \d+ observations$")),"a single histogram renamed its bins");
    var values=Distribution(6).Series[0].Points.Select(p=>p.Y!.Value).ToArray();
    Check(Statistics.Bins(values,6).SequenceEqual(Statistics.SharedBins([values],6)[0]),"one set binned alone and as the only shared set disagree");
});
Test("A histogram of several series carries a legend and says what it pooled",()=>{
    var spec=Compared(null,("Before",Late()),("After",Early()));
    var doc=Svg(spec);
    Check((string?)doc.Root!.Attribute("viewBox")=="0 0 900 442","no legend row");
    Check(doc.Descendants(ns+"text").Any(e=>e.Value=="Before")&&doc.Descendants(ns+"text").Any(e=>e.Value=="After"),"the legend does not name both series");
    Check(doc.Descendants(ns+"text").Any(e=>e.Value.StartsWith("105 observations across 2 series in ")&&e.Value.EndsWith(" shared equal-width bins")),"the caption does not say the bins are shared");
    Check((string?)XDocument.Parse(ChartSvg.Render(spec,includeLegend:false)).Root!.Attribute("viewBox")=="0 0 900 420","a component legend was not left to the component");
});
Test("A histogram series with no observations keeps its place",()=>{
    var bars=Bars(Svg(Compared(3,("Full",[1,2,3,4,5,6]),("Empty",[]))));
    Check(bars.Length==6&&bars.Where((_,i)=>i%2==1).All(b=>b.Attribute("aria-label")!.Value.EndsWith(": 0 observations")),"the empty series lost its bars");
});
Test("A histogram takes at most four series",()=>{
    ChartSeries Set(int i)=>new($"S{i}",Enumerable.Range(0,20).Select(v=>ChartPoint.Observation(v+i*3)).ToArray());
    Check(Bars(Svg(Spec(ChartKind.Histogram) with{Bins=5,Series=Enumerable.Range(0,4).Select(Set).ToArray()})).Length==20);
    try { ChartSvg.Render(Spec(ChartKind.Histogram) with{Series=Enumerable.Range(0,5).Select(Set).ToArray()}); throw new Exception("five series were drawn"); }
    catch(ArgumentException error) { Check(error.Message.Contains("at most four"),error.Message); }
});
bool Near(double actual,double expected,double tolerance=1e-9)=>Math.Abs(actual-expected)<=tolerance*Math.Max(1,Math.Abs(expected));
DateOnly Day(int offset)=>new DateOnly(2026,3,2).AddDays(offset);
// An hour of varied riding: a slow swell, noise, and a 20-second surge every five minutes.
double[] Ride(){var random=new Random(7);return Enumerable.Range(0,3600).Select(i=>150+100*Math.Sin(i/40.0)+random.Next(0,120)+(i%300<20?400:0d)).ToArray();}
string[] power=["Active recovery","Endurance","Tempo","Lactate threshold","VO2max","Anaerobic capacity","Neuromuscular"];
Test("Coggan's power levels reproduce the worked example for an FTP of 290 W",()=>{
    var scale=ZoneScale.CogganPower(290);
    Check(scale.Zones.Select(z=>z.Name).SequenceEqual(power),"the levels are misnamed or misordered");
    var rounded=scale.Zones.Take(5).Select(z=>Math.Round(z.Upper,MidpointRounding.AwayFromZero)).ToArray();
    Check(rounded.SequenceEqual([160d,218,261,305,348]),string.Join(", ",rounded));
    Check(scale.Zones[5].Upper==435&&double.IsPositiveInfinity(scale.Zones[6].Upper),"anaerobic capacity or the top level is wrong");
});
Test("A zone holds its own upper bound and starts just above the previous one",()=>{
    var scale=ZoneScale.CogganPower(290);
    for(var i=0;i<6;i++)
    {
        var bound=scale.Zones[i].Upper;
        Check(scale.IndexOf(bound)==i,$"{bound} W left its own zone");
        Check(scale.IndexOf(Math.BitDecrement(bound))==i,$"just below {bound} W left the zone");
        Check(scale.IndexOf(Math.BitIncrement(bound))==i+1,$"just above {bound} W stayed in the zone");
    }
    Check(scale.IndexOf(-5)==0&&scale.IndexOf(0)==0&&scale.IndexOf(5000)==6&&scale.IndexOf(double.PositiveInfinity)==6);
    Check(scale.IndexOf(double.NaN)==-1,"NaN was put in a zone");
    // The bounds are exact, so 160 W, which an integer table prints as recovery's limit, is already endurance.
    Check(scale.IndexOf(159)==0&&scale.IndexOf(159.5)==0&&scale.IndexOf(160)==1&&scale.IndexOf(217)==1&&scale.IndexOf(218)==2);
});
Test("Coggan's heart-rate levels follow threshold heart rate",()=>{
    var scale=ZoneScale.CogganHeartRate(170);
    Check(scale.Zones.Select(z=>z.Name).SequenceEqual(power.Take(5)),"the levels are misnamed or misordered");
    double[] bounds=[115.6,141.1,159.8,178.5];
    for(var i=0;i<4;i++)Check(Near(scale.Zones[i].Upper,bounds[i]),$"level {i+1} ends at {scale.Zones[i].Upper}");
    Check(double.IsPositiveInfinity(scale.Zones[4].Upper));
    Check(scale.IndexOf(115)==0&&scale.IndexOf(116)==1&&scale.IndexOf(159)==2&&scale.IndexOf(160)==3&&scale.IndexOf(178)==3&&scale.IndexOf(179)==4);
});
Test("A zone scale refuses what it cannot order",()=>{
    var top=new Zone("Top",double.PositiveInfinity);
    Reject(()=>new ZoneScale([]));
    Reject(()=>new ZoneScale(null!));
    Reject(()=>new ZoneScale([new("Easy",100),new("Hard",100),top]));
    Reject(()=>new ZoneScale([new("Easy",200),new("Hard",100),top]));
    Reject(()=>new ZoneScale([new("",100),top]));
    Reject(()=>new ZoneScale([new(" ",100),top]));
    Reject(()=>new ZoneScale([null!,top]));
    Reject(()=>new ZoneScale([new("Easy",100),new("Hard",200)]));
    Reject(()=>new ZoneScale([new("Easy",double.NaN),top]));
    Reject(()=>new ZoneScale([new("Easy",double.NegativeInfinity),top]));
    Reject(()=>new ZoneScale([new("Easy",100),new("Hard",double.PositiveInfinity),top]));
    foreach(var threshold in new[]{0,-250,double.NaN,double.PositiveInfinity})
    {
        Reject(()=>ZoneScale.CogganPower(threshold));
        Reject(()=>ZoneScale.CogganHeartRate(threshold));
    }
    Check(new ZoneScale([top]).IndexOf(-1e300)==0,"a single unbounded zone does not hold everything");
});
Test("Normalized power of a steady effort is that effort",()=>{
    Check(Near(Training.NormalizedPower(Enumerable.Repeat(237.4,3600).ToArray())!.Value,237.4));
    Check(Near(Training.NormalizedPower(Enumerable.Repeat(200d,30).ToArray())!.Value,200),"one whole window is not enough");
});
Test("Normalized power averages over 30 seconds before taking the fourth power",()=>{
    // Every 30-second window of alternating 0 and 400 W holds fifteen of each, so every average is 200 W;
    // raising the samples themselves would give 400 / 2^(1/4), about 336 W.
    Check(Near(Training.NormalizedPower(Enumerable.Range(0,600).Select(i=>i%2==0?0d:400).ToArray())!.Value,200));
    // Every 10 seconds the window is three samples, so 100, 200, 300 and 400 W average to 200 and 300 W.
    Check(Near(Training.NormalizedPower([100d,200,300,400],10)!.Value,263.8975964004231));
});
Test("Normalized power is never below the plain mean of its rolling averages",()=>{
    var ride=Ride();
    var averages=Enumerable.Range(0,ride.Length-29).Select(i=>ride.Skip(i).Take(30).Average()).ToArray();
    var np=Training.NormalizedPower(ride)!.Value;
    Check(np>averages.Average()+1,$"NP {np} against a mean of {averages.Average()}");
    Check(Near(np,Math.Pow(averages.Average(a=>Math.Pow(a,4)),.25)),"the running window disagrees with averaging each window afresh");
});
Test("Normalized power needs one whole window, counted in samples",()=>{
    Check(Training.NormalizedPower([]) is null&&Training.NormalizedPower(Enumerable.Repeat(250d,29).ToArray()) is null);
    // Every 5 seconds the window is six samples; every 12, two and a half rounds up to three.
    Check(Training.NormalizedPower([100d,200,300,400,500],5) is null&&Near(Training.NormalizedPower([100d,200,300,400,500,600],5)!.Value,350));
    Check(Training.NormalizedPower([100d,200],12) is null&&Near(Training.NormalizedPower([100d,200,300],12)!.Value,200));
    // A sample longer than 30 seconds is a window of its own.
    Check(Near(Training.NormalizedPower([100d,300],60)!.Value,Math.Pow((Math.Pow(100,4)+Math.Pow(300,4))/2,.25)));
    Reject(()=>Training.NormalizedPower(Ride(),0));
    Reject(()=>Training.NormalizedPower(Ride(),-1));
    Reject(()=>Training.NormalizedPower(Ride(),double.NaN));
    Reject(()=>Training.NormalizedPower([..Ride(),double.NaN]));
    Reject(()=>Training.NormalizedPower([..Ride(),double.PositiveInfinity]));
});
Test("An hour at threshold scores 100 training stress",()=>{
    Check(Training.StressScore(3600,250,250)==100&&Training.IntensityFactor(250,250)==1);
    Check(Near(Training.StressScore(1800,250,250),50)&&Near(Training.StressScore(3600,275,250),121));
    foreach(var (seconds,np,ftp) in new[]{(5400d,210d,260d),(2700d,305d,280d),(600d,90d,300d)})
        Check(Near(Training.StressScore(seconds,np,ftp),seconds/3600*Math.Pow(np/ftp,2)*100),"training stress is not hours × IF² × 100");
    Reject(()=>Training.IntensityFactor(200,0));
    Reject(()=>Training.IntensityFactor(200,double.NaN));
    Reject(()=>Training.IntensityFactor(-1,250));
    Reject(()=>Training.IntensityFactor(double.PositiveInfinity,250));
    Reject(()=>Training.StressScore(-1,200,250));
    Reject(()=>Training.StressScore(double.NaN,200,250));
    Reject(()=>Training.StressScore(3600,200,-250));
});
Test("Allen and Coggan's 7:09:27 ride at NP 198 W and IF 0.859 scores 528.5 within rounding",()=>{
    var seconds=7*3600+9*60+27d;
    // FTP is NP / IF. An IF printed to three places lies between 0.8585 and 0.8595.
    double low=Training.StressScore(seconds,198,198/.8585),high=Training.StressScore(seconds,198,198/.8595);
    Check(low<=528.5&&528.5<=high,$"{low} to {high}");
    Check(Math.Abs(Training.StressScore(seconds,198,198/.859)-528.5)<.5);
});
Test("Fitness, fatigue and form follow the published recurrence day by day",()=>{
    var load=Training.Load([(Day(0),100),(Day(1),50),(Day(3),70)]);
    Check(load.Select(d=>d.Day).SequenceEqual([Day(0),Day(1),Day(2),Day(3)]),"the days are not consecutive");
    Check(load.Select(d=>d.Stress).SequenceEqual([100d,50,0,70]));
    Check(Near(load[0].Fitness,100/42d)&&Near(load[0].Fatigue,100/7d));
    // Worked by hand: fitness moves a 42nd of the way to the day's stress, fatigue a 7th, form is yesterday's difference.
    double[] fitness=[2.380952380952381,3.5147392290249435,3.431054961667207,5.016029843532273];
    double[] fatigue=[14.285714285714286,19.387755102040817,16.61807580174927,24.244064972927944];
    double[] form=[0,-11.904761904761905,-15.873015873015873,-13.187020840082063];
    for(var i=0;i<4;i++)Check(Near(load[i].Fitness,fitness[i])&&Near(load[i].Fatigue,fatigue[i])&&Near(load[i].Form,form[i]),$"day {i+1}: {load[i]}");
});
Test("Form is yesterday's fitness minus yesterday's fatigue",()=>{
    var load=Training.Load(Enumerable.Range(0,30).Select(i=>(Day(i),(double)(i*37%120))),fitness:40,fatigue:55);
    Check(load[0].Form==-15,"the first day's form is not the seeds'");
    for(var i=1;i<load.Count;i++)Check(load[i].Form==load[i-1].Fitness-load[i-1].Fatigue,$"day {i+1}");
    Check(Training.Load([(Day(0),60),(Day(1),500)])[1].Form==Training.Load([(Day(0),60),(Day(1),0)])[1].Form,"a day's own training moved its form");
});
Test("Days without training count as zero and one day's entries add up",()=>{
    var gap=Training.Load([(Day(0),80),(Day(5),60)]);
    Check(gap.Count==6&&gap.Skip(1).Take(4).All(d=>d.Stress==0),"the gap was not filled");
    Check(gap.SequenceEqual(Training.Load([(Day(0),80),(Day(1),0),(Day(2),0),(Day(3),0),(Day(4),0),(Day(5),60)])),"a filled day differs from a written zero");
    Check(gap.SequenceEqual(Training.Load([(Day(5),25),(Day(0),30),(Day(5),35),(Day(0),50)])),"entries on one day were not added, or order mattered");
    Check(Training.Load([]).Count==0);
});
Test("Steady training draws fitness and fatigue to it and form back to zero",()=>{
    var load=Training.Load(Enumerable.Range(0,400).Select(i=>(Day(i),80d)));
    for(var i=1;i<load.Count;i++)
        Check(load[i].Fitness>load[i-1].Fitness&&load[i].Fitness<80&&load[i].Fatigue>=load[i-1].Fatigue&&load[i].Fatigue<=80,$"day {i+1}: {load[i]}");
    Check(load.All(d=>d.Fatigue>d.Fitness)&&load.Skip(1).All(d=>d.Form<0),"fatigue did not lead fitness");
    Check(Math.Abs(load[^1].Fitness-80)<.01&&Math.Abs(load[^1].Fatigue-80)<1e-9&&Math.Abs(load[^1].Form)<.01,load[^1].ToString());
    Check(load[10].Form<load[100].Form&&load[100].Form<load[^1].Form,"form did not recover");
});
Test("Seeds and time constants change the load model as Allen and Coggan describe",()=>{
    // Seeded at the typical daily stress, an athlete holding it starts at zero form and stays there.
    Check(Training.Load(Enumerable.Range(0,60).Select(i=>(Day(i),80d)),fitness:80,fatigue:80).All(d=>d.Fitness==80&&d.Fatigue==80&&d.Form==0));
    var quick=Training.Load([(Day(0),100)],fatigueDays:4)[0];
    Check(Near(quick.Fatigue,25)&&Near(quick.Fitness,100/42d),quick.ToString());
    var custom=Training.Load([(Day(0),100)],fitness:50,fatigue:20,fitnessDays:28,fatigueDays:10)[0];
    Check(Near(custom.Fitness,51.785714285714285)&&Near(custom.Fatigue,28)&&custom.Form==30,custom.ToString());
    // After a hard week and a rest week, a short fatigue constant has shed more fatigue than a long one.
    var block=Enumerable.Range(0,14).Select(i=>(Day(i),i<7?150d:0)).ToArray();
    Check(Training.Load(block,fatigueDays:4)[^1].Form>Training.Load(block,fatigueDays:12)[^1].Form);
});
Test("The load model refuses what it cannot run",()=>{
    Reject(()=>Training.Load([(Day(0),double.NaN)]));
    Reject(()=>Training.Load([(Day(0),double.PositiveInfinity)]));
    Reject(()=>Training.Load([(Day(0),-1)]));
    Reject(()=>Training.Load([(Day(0),50)],fitness:double.NaN));
    Reject(()=>Training.Load([(Day(0),50)],fatigue:double.PositiveInfinity));
    Reject(()=>Training.Load([(Day(0),50)],fitnessDays:0));
    Reject(()=>Training.Load([(Day(0),50)],fitnessDays:double.PositiveInfinity));
    Reject(()=>Training.Load([(Day(0),50)],fatigueDays:-7));
    Reject(()=>Training.Load([(Day(0),50)],fatigueDays:double.NaN));
});
Test("Time in zone counts each finite sample once",()=>{
    // At FTP 200 the bounds are 110, 150, 180, 210, 240 and 300 W.
    double[] samples=[90,100,-5,120,130,140,160,200,205,230,260,400,double.NaN,double.PositiveInfinity,double.NegativeInfinity];
    var seconds=Training.TimeInZone(samples,ZoneScale.CogganPower(200),2);
    Check(seconds.SequenceEqual([6d,6,2,4,2,2,2]),string.Join(", ",seconds));
    var ride=Ride();
    var whole=Training.TimeInZone(ride,ZoneScale.CogganPower(250));
    Check(whole.Count==7&&whole.Sum()==3600,"an hour did not add up to an hour");
    Check(Training.TimeInZone([..ride,double.NaN,double.NaN],ZoneScale.CogganPower(250),.5).Sum()==1800,"dropouts were counted");
    Check(Training.TimeInZone(Enumerable.Repeat(150d,90).ToArray(),ZoneScale.CogganHeartRate(160)).SequenceEqual([0d,0,90,0,0]));
    Reject(()=>Training.TimeInZone(ride,ZoneScale.CogganPower(250),0));
    Reject(()=>Training.TimeInZone(ride,ZoneScale.CogganPower(250),double.NaN));
});
Test("A mean-maximal curve finds the best average for each duration",()=>{
    var ride=Ride();
    long[] chain=[1,5,10,30,60,120,600,1200,3600];
    var curve=Training.MeanMaximal(ride,chain.Select(d=>(double)d));
    Check(curve.Select(p=>p.Seconds).SequenceEqual(chain.Select(d=>(double)d)));
    foreach(var (seconds,value) in curve)
    {
        var width=(int)seconds;
        var best=Enumerable.Range(0,ride.Length-width+1).Max(i=>ride.Skip(i).Take(width).Average());
        Check(Near(value,best),$"{seconds} s: {value} against {best}");
    }
    // Each duration here is a multiple of the one before, and over such a chain the best average cannot rise.
    for(var i=1;i<curve.Count;i++)Check(curve[i].Value<=curve[i-1].Value,$"the curve rose from {curve[i-1].Seconds} to {curve[i].Seconds} s");
    Check(Near(curve[^1].Value,ride.Average()),"the whole record is not its own average");
});
Test("A mean-maximal curve can rise between durations that are not multiples",()=>{
    // Five seconds hard, five easy and five hard: every 10-second window holds five hard seconds, the 15-second one ten.
    double[] surges=[..Enumerable.Repeat(1000d,5),..Enumerable.Repeat(0d,5),..Enumerable.Repeat(1000d,5),..Enumerable.Repeat(0d,30)];
    var curve=Training.MeanMaximal(surges,[10,15]);
    Check(Near(curve[0].Value,500)&&Near(curve[1].Value,10000/15d),string.Join(", ",curve));
});
Test("A steady record is flat and a spike stays at the short durations",()=>{
    var flat=Training.MeanMaximal(Enumerable.Repeat(260.5,3600).ToArray(),Training.StandardDurations);
    Check(flat.Select(p=>p.Seconds).SequenceEqual(Training.StandardDurations.Where(d=>d<=3600)),"durations past the record were kept");
    Check(flat.All(p=>Near(p.Value,260.5)),"a steady record is not flat");
    var spiked=Enumerable.Repeat(200d,600).ToArray();
    for(var i=300;i<305;i++)spiked[i]=1000;
    var curve=Training.MeanMaximal(spiked,[1,5,10,60,600,601]).ToDictionary(p=>p.Seconds,p=>p.Value);
    Check(curve.Count==5&&curve[1]==1000&&curve[5]==1000,"the spike is missing at the short end");
    Check(Near(curve[10],600)&&Near(curve[60],(5000+55*200)/60d)&&Near(curve[600],(5000+595*200)/600d),"the spike spread too far or too little");
});
Test("A mean-maximal curve counts durations in whole samples",()=>{
    // Every 2 seconds: one second is shorter than a sample, three rounds half up to two samples, and the record is 20 seconds.
    double[] samples=[100,300,200,400,100,100,100,100,100,100];
    var curve=Training.MeanMaximal(samples,[1,2,3,4,20,21],2);
    Check(curve.Select(p=>p.Seconds).SequenceEqual([2d,3,4,20]),string.Join(", ",curve));
    Check(curve[0].Value==400&&curve[1].Value==300&&curve[2].Value==300&&Near(curve[3].Value,160));
    Check(Training.MeanMaximal([],Training.StandardDurations).Count==0);
    Reject(()=>Training.MeanMaximal(samples,[double.NaN]));
    Reject(()=>Training.MeanMaximal([100d,double.NaN],[1]));
    Reject(()=>Training.MeanMaximal(samples,[1],0));
    Reject(()=>Training.MeanMaximal(samples,[1],double.PositiveInfinity));
});
Test("Critical power recovers CP and W′ from efforts built from the model",()=>{
    double Model(double seconds)=>250+20000/seconds;
    var efforts=new[]{180d,300,600,1200}.Select(t=>(t,Model(t))).ToArray();
    var fit=Training.CriticalPower(efforts)!;
    Check(Near(fit.CriticalPower,250)&&Near(fit.WPrime,20000)&&fit.Count==4&&Near(fit.R2,1),fit.ToString());
    // Efforts outside 3 to 20 minutes are ignored, however far off the model they are; the window's ends are inside it.
    Check(Training.CriticalPower([..efforts,(5,1200),(60,600),(179.9,100),(1200.1,100),(3600,100)])==fit,"an effort outside the window moved the fit");
    var start=Training.CriticalPower([..efforts,(180,100)])!;var end=Training.CriticalPower([..efforts,(1200,100)])!;
    Check(start.Count==5&&end.Count==5&&start.CriticalPower!=250&&end.CriticalPower!=250,"an effort at the window's edge was ignored");
    // A mean-maximal curve feeds it directly: an hour that settles at 300 W after a hard start fits a CP near 300 W.
    var settled=Training.CriticalPower(Training.MeanMaximal(Enumerable.Range(0,3600).Select(i=>i<200?380d:300).ToArray(),Training.StandardDurations))!;
    Check(settled.Count==4&&Math.Abs(settled.CriticalPower-300)<5,settled.ToString());
});
Test("Critical power needs two different durations between 3 and 20 minutes",()=>{
    Check(Training.CriticalPower([]) is null);
    Check(Training.CriticalPower([(300,320)]) is null);
    Check(Training.CriticalPower([(300,320),(300,330)]) is null);
    Check(Training.CriticalPower([(60,500),(3600,220),(300,320)]) is null);
    Reject(()=>Training.CriticalPower([(300,double.NaN),(600,280)]));
    Reject(()=>Training.CriticalPower([(double.PositiveInfinity,200),(600,280)]));
});
bool Held(RollingWindow? window,double mean,double deviation,int count)=>window is not null&&Near(window.Mean,mean)&&Near(window.Deviation,deviation)&&window.Count==count;
Test("A rolling window gives the mean and sample deviation of the values present",()=>{
    var windows=Statistics.Rolling([2d,4,null,8,6],3,2);
    Check(windows[0] is null,"one value met a minimum of two");
    Check(Held(windows[1],3,Math.Sqrt(2),2)&&Held(windows[2],3,Math.Sqrt(2),2)&&Held(windows[3],6,Math.Sqrt(8),2)&&Held(windows[4],7,Math.Sqrt(2),2),string.Join(" | ",windows));
    var full=Statistics.Rolling([1d,2,3,4,5],3);
    Check(full[0] is null&&full[1] is null&&Held(full[2],2,1,3)&&Held(full[3],3,1,3)&&Held(full[4],4,1,3),string.Join(" | ",full));
    Check(Held(Statistics.Rolling([2d,4,4,4,5,5,7,9],8)[7],5,Math.Sqrt(32/7d),8),"the deviation does not divide by n - 1");
    // Precision survives a large level with a small spread.
    Check(Held(Statistics.Rolling([1e9+1,1e9+2,1e9+3],3)[2],1e9+2,1,3),"a large level swamped the spread");
});
Test("A rolling window skips missing values and waits for its minimum",()=>{
    Check(Statistics.Rolling([2d,4,null,8,6],3).All(w=>w is null),"a window with a gap met the default minimum of the whole window");
    var average=Statistics.Rolling([10d,20,30,40],2,1);
    Check(Held(average[0],10,0,1)&&Held(average[1],15,Math.Sqrt(50),2)&&Held(average[2],25,Math.Sqrt(50),2)&&Held(average[3],35,Math.Sqrt(50),2),string.Join(" | ",average));
    var random=new Random(11);
    var values=Enumerable.Range(0,500).Select(i=>random.Next(5)==0?(double?)null:50+random.NextDouble()*20).ToArray();
    var rolled=Statistics.Rolling(values,7,3);
    for(var i=0;i<values.Length;i++)
    {
        var present=values.Skip(Math.Max(0,i-6)).Take(Math.Min(7,i+1)).OfType<double>().ToArray();
        if(present.Length<3){Check(rolled[i] is null,$"entry {i} met the minimum with {present.Length}");continue;}
        var mean=present.Average();
        Check(Held(rolled[i],mean,Math.Sqrt(present.Sum(v=>(v-mean)*(v-mean))/(present.Length-1)),present.Length),$"entry {i}: {rolled[i]}");
    }
    Reject(()=>Statistics.Rolling([1d,2],0));
    Reject(()=>Statistics.Rolling([1d,2],3,0));
    Reject(()=>Statistics.Rolling([1d,2],3,4));
    Reject(()=>Statistics.Rolling([1d,double.NaN],2,1));
    Reject(()=>Statistics.Rolling([1d,double.PositiveInfinity],2,1));
});
Axis Seconds(double min,double max)=>new(AxisKind.Linear,min,max){ValueFormat=ValueFormat.Duration};
Axis LogSeconds(double min,double max)=>new(AxisKind.Log,min,max){ValueFormat=ValueFormat.Duration};
Axis Compacted(double min=0,double max=1)=>new(AxisKind.Linear,min,max){ValueFormat=ValueFormat.Compact};
string[] Labelled(IReadOnlyList<(double Value,string Label)> ticks)=>ticks.Select(t=>t.Label).ToArray();
Test("Durations read m:ss below an hour and h:mm:ss from an hour up",()=>{
    (double Value,string Label)[] cases=[(0,"0:00"),(5,"0:05"),(59.4,"0:59"),(59.5,"1:00"),(330,"5:30"),(3599,"59:59"),(3599.5,"1:00:00"),
        (3600,"1:00:00"),(3725,"1:02:05"),(86400,"24:00:00"),(90061,"25:01:01"),(-330,"-5:30"),(-3725,"-1:02:05"),(-0.4,"0:00")];
    foreach(var (value,label) in cases)Check(Seconds(0,1).Format(value)==label,$"{value} read {Seconds(0,1).Format(value)}, not {label}");
});
double[] DurationLadder=[1,2,5,10,15,30,60,120,300,600,900,1800,3600,7200,10800,21600,43200];
Test("Linear duration ticks are every multiple of the smallest round step that fits the count",()=>{
    var random=new Random(19);
    for(var trial=0;trial<400;trial++)
    {
        var min=Math.Round((random.NextDouble()-.3)*Math.Pow(10,random.Next(1,7)));
        var max=min+Math.Round(1+random.NextDouble()*Math.Pow(10,random.Next(1,7)));
        var count=random.Next(2,9);
        var ticks=Seconds(min,max).Ticks(count);
        double Fitted(double step)=>Math.Floor(max/step)-Math.Ceiling(min/step)+1;
        // The documented ladder and then every whole number of days, searched from the bottom.
        var step=DurationLadder.Concat(Enumerable.Range(1,100_000).Select(days=>days*86400d)).First(s=>Fitted(s)<=count);
        var expected=Enumerable.Range(0,(int)Fitted(step)).Select(i=>(Math.Ceiling(min/step)+i)*step).ToArray();
        Check(ticks.Count<=count,$"[{min}, {max}] drew {ticks.Count} ticks for {count}");
        Check(ticks.Select(t=>t.Value).SequenceEqual(expected),$"[{min}, {max}] at {count}: {string.Join(" ",ticks.Select(t=>t.Value))}, expected every {step} s");
        Check(ticks.All(t=>t.Label==Seconds(0,1).Format(t.Value)),"a tick reads differently from a tooltip");
    }
    Check(Labelled(Seconds(280,330).Ticks()).SequenceEqual(["4:45","5:00","5:15","5:30"]));
    Check(Labelled(Seconds(0,3000).Ticks()).SequenceEqual(["0:00","15:00","30:00","45:00"]));
    Check(Labelled(Seconds(0,9000).Ticks()).SequenceEqual(["0:00","1:00:00","2:00:00"]));
    Check(Labelled(Seconds(-300,300).Ticks()).SequenceEqual(["-4:00","-2:00","0:00","2:00","4:00"]));
    Check(Labelled(Seconds(0,10*86400).Ticks()).SequenceEqual(["0:00","72:00:00","144:00:00","216:00:00"]),"ten days did not step by three");
});
Test("Linear duration minor lines divide each step into round durations",()=>{
    Check(Seconds(280,330).MinorTicks().SequenceEqual([290d,295,305,310,320,325]),"15 seconds is not three fives");
    Check(Seconds(0,3000).MinorTicks().SequenceEqual([300d,600,1200,1500,2100,2400]),"15 minutes is not three fives");
    Check(Seconds(0,9000).MinorTicks().SequenceEqual([900d,1800,2700,4500,5400,6300,8100]),"an hour is not four quarters");
    Check(Seconds(0,3*86400).MinorTicks().Count==9&&Seconds(0,3*86400).MinorTicks().All(v=>v%21600==0&&v%86400!=0),"a day is not four six-hour parts");
});
Test("Logarithmic duration ticks are round durations inside the range, from the nearest at each end",()=>{
    double[] fixedLadder=[1,2,5,10,15,30,60,120,300,600,1200,1800,3600,7200,10800,14400,18000];
    var random=new Random(7);
    for(var trial=0;trial<400;trial++)
    {
        var min=Math.Pow(10,random.NextDouble()*4);var max=min*Math.Pow(10,.5+random.NextDouble()*2.5);
        var count=random.Next(2,9);
        var ticks=LogSeconds(min,max).Ticks(count).Select(t=>t.Value).ToArray();
        var inside=fixedLadder.Concat(Enumerable.Range(6,Math.Max(0,(int)(max/3600)-5)).Select(hours=>hours*3600d)).Where(v=>v>=min&&v<=max).ToArray();
        Check(ticks.Length<=count&&ticks.Length>=Math.Min(2,inside.Length),$"[{min}, {max}] drew {ticks.Length} ticks for {count}");
        Check(ticks.All(inside.Contains),$"[{min}, {max}]: {string.Join(" ",ticks)} leaves the ladder or the range");
        Check(ticks[0]==inside.Min()&&ticks[^1]==inside.Max(),$"[{min}, {max}]: {string.Join(" ",ticks)} does not reach the ends");
        Check(ticks.Zip(ticks.Skip(1)).All(p=>p.First<p.Second),"ticks repeat or run backwards");
    }
    Check(Labelled(LogSeconds(1,3600).Ticks()).SequenceEqual(["1s","10s","1m","10m","1h"]));
    Check(Labelled(LogSeconds(1,7200).Ticks()).SequenceEqual(["1s","10s","2m","10m","2h"]));
    Check(Labelled(LogSeconds(1,1000).Ticks()).SequenceEqual(["1s","5s","30s","2m","10m"]),"the range's last round duration is not kept");
    Check(Labelled(LogSeconds(7*3600,30*3600).Ticks()).SequenceEqual(["7h","10h","14h","21h","30h"]),"whole hours past five are not on the ladder");
    (double Value,string Label)[] spans=[(1,"1s"),(1.5,"1.5s"),(30,"30s"),(60,"1m"),(90,"1m30s"),(1200,"20m"),(3600,"1h"),(3725,"1h2m5s"),(9000,"2h30m"),(86400,"24h")];
    foreach(var (value,label) in spans)Check(LogSeconds(1,10).Format(value)==label,$"{value} read {LogSeconds(1,10).Format(value)}, not {label}");
    // Mantissas of 20 minutes are no duration anyone reads, so the duration axis has no minor lines; a plain one does.
    Check(LogSeconds(1,3600).MinorTicks().Count==0&&new Axis(AxisKind.Log,1,3600).MinorTicks().Count>0);
});
Test("Compact labels change unit at each magnitude boundary and keep the plain positions",()=>{
    (double Value,string Label)[] cases=[(0,"0"),(12.5,"12.5"),(999,"999"),(1000,"1k"),(1049,"1k"),(1050,"1.1k"),(999_949,"999.9k"),(999_950,"1M"),
        (999_999,"1M"),(1e6,"1M"),(3.45e6,"3.5M"),(1.5e9,"1.5B"),(2e12,"2T"),(-1234,"-1.2k"),(-1e6,"-1M")];
    foreach(var (value,label) in cases)Check(Compacted().Format(value)==label,$"{value} read {Compacted().Format(value)}, not {label}");
    var plain=new Axis(AxisKind.Linear,0,2.5e6);
    Check(Compacted(0,2.5e6).Ticks().Select(t=>t.Value).SequenceEqual(plain.Ticks().Select(t=>t.Value))&&Labelled(Compacted(0,2.5e6).Ticks()).SequenceEqual(["0","1M","2M"]));
    Check(Compacted(0,2.5e6).MinorTicks().SequenceEqual(plain.MinorTicks()));
    var log=new Axis(AxisKind.Log,1,1e7);
    Check(Labelled((log with{ValueFormat=ValueFormat.Compact}).Ticks()).SequenceEqual(["1","100","10k","1M"])&&(log with{ValueFormat=ValueFormat.Compact}).MinorTicks().SequenceEqual(log.MinorTicks()));
    var doc=Svg(Spec(ChartKind.Column) with{YFormat=ValueFormat.Compact,Series=[new("Views",[new(0,1500,"A"),new(1,2_400_000,"B")])]});
    Check(doc.Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")=="Views: A, 1.5k")&&doc.Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")=="Views: B, 2.4M"));
    Check(doc.Descendants(ns+"text").Any(t=>t.Value=="2M"),"the axis does not read compactly");
});
Test("Durations and compact numbers read the same in every culture",()=>{
    var previous=CultureInfo.CurrentCulture;
    try {
        foreach(var culture in (string[])["fr-FR","sv-SE"])
        {
            // Swedish writes its minus as U+2212 and both write a decimal comma.
            CultureInfo.CurrentCulture=new(culture);
            Check(Seconds(0,1).Format(-3725)=="-1:02:05"&&LogSeconds(1,10).Format(1.5)=="1.5s"&&LogSeconds(1,10).Format(9000)=="2h30m",culture);
            Check(Compacted().Format(1250)=="1.3k"&&Compacted().Format(-3.45e6)=="-3.5M",culture);
        }
    } finally {CultureInfo.CurrentCulture=previous;}
});
Test("A reversed axis maps the minimum to the top and reads back exactly",()=>{
    foreach(var kind in (AxisKind[])[AxisKind.Linear,AxisKind.Log])
    {
        var plain=new Axis(kind,10,1000);var reversed=plain with{Reversed=true};
        Check(reversed.Map(10,344,78)==78&&reversed.Map(1000,344,78)==344,$"{kind}: the minimum is not at the top");
        foreach(var value in (double[])[10,37.5,100,512,1000])
        {
            Check(Near(reversed.Map(value,344,78)+plain.Map(value,344,78),344+78),$"{kind}: {value} is not mirrored");
            Check(Near(reversed.Invert(reversed.Map(value,344,78),344,78),value),$"{kind}: {value} does not read back");
        }
        Check(reversed.Ticks().SequenceEqual(plain.Ticks())&&reversed.MinorTicks().SequenceEqual(plain.MinorTicks()),$"{kind}: reversal moved the ticks");
    }
});
// A plot spans 78 to 344 pixels, so on a reversed axis every Y position is 422 minus the plain one.
double Attr(XElement e,string name)=>double.Parse(e.Attribute(name)!.Value,CultureInfo.InvariantCulture);
Test("A reversed pace axis puts the fastest pace on top and mirrors every mark and annotation",()=>{
    var pace=Spec() with{YFormat=ValueFormat.Duration,Annotations=[new(AnnotationAxis.Y,300){Label="Target"},new(AnnotationAxis.Y,290){To=310,Label="Zone"}],
        Series=[new("Pace",[new(0,330),new(1,300),new(2,281),new(3,295)])]};
    XDocument plain=Svg(pace),reversed=Svg(pace with{YReversed=true});
    double TickY(XDocument doc,string label)=>Attr(doc.Descendants(ns+"text").First(t=>t.Value==label),"y");
    Check(TickY(reversed,"4:50")<TickY(reversed,"5:20")&&TickY(plain,"4:50")>TickY(plain,"5:20"),"faster pace is not higher");
    double[] Marks(XDocument doc)=>doc.Descendants(ns+"circle").Select(c=>Attr(c,"cy")).ToArray();
    Check(Marks(plain).Length==4&&Marks(plain).Zip(Marks(reversed)).All(p=>Near(p.First+p.Second,422)),"marks are not mirrored");
    XElement Reference(XDocument doc)=>doc.Descendants(ns+"line").First(l=>(string?)l.Attribute("stroke-dasharray")=="6 4");
    Check(Near(Attr(Reference(plain),"y1")+Attr(Reference(reversed),"y1"),422),"the target line is not mirrored");
    XElement Band(XDocument doc)=>doc.Descendants(ns+"rect").Single(r=>(string?)r.Attribute("fill-opacity")==".12");
    Check(Near(Attr(Band(reversed),"y"),422-Attr(Band(plain),"y")-Attr(Band(plain),"height"))&&Near(Attr(Band(reversed),"height"),Attr(Band(plain),"height")),"the zone band is not mirrored");
    var labels=reversed.Descendants().Select(e=>(string?)e.Attribute("aria-label")).OfType<string>().ToArray();
    Check(labels.Contains("Target: 5:00")&&labels.Contains("Zone: 4:50 to 5:10")&&labels.Contains("Pace: 2, 4:41"),string.Join(" | ",labels));
});
Test("Trend lines on a reversed axis name the direction of the data, not of the line",()=>{
    foreach(var reversed in (bool[])[false,true])
    {
        ChartSpec Trended(params double[] values)=>Spec(ChartKind.Scatter) with{YReversed=reversed,Series=[new("Pace",values.Select((y,i)=>new ChartPoint(i,y)).ToArray()){Trend=true}]};
        XElement Line(ChartSpec spec)=>Svg(spec).Descendants(ns+"path").Single(p=>(string?)p.Attribute("class")=="lumen-trend");
        Check(Line(Trended(300,310,320,330)).Attribute("aria-label")!.Value.Contains("trend: rising"),$"reversed {reversed}: a rising series is not called rising");
        Check(Line(Trended(330,320,310,300)).Attribute("aria-label")!.Value.Contains("trend: falling"),$"reversed {reversed}: a falling series is not called falling");
        // On screen a rising series climbs on a plain axis and descends on a reversed one.
        var ends=System.Text.RegularExpressions.Regex.Matches(Line(Trended(300,310,320,330)).Attribute("d")!.Value,@",(-?[\d.]+)").Select(m=>double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)).ToArray();
        Check(reversed?ends[1]>ends[0]:ends[1]<ends[0],$"reversed {reversed}: the line runs the wrong way on screen");
    }
});
Test("Candles, boxes and violins on a reversed axis keep their shapes, mirrored",()=>{
    foreach(var (kind,opacity) in ((ChartKind,string?)[])[(ChartKind.Candlestick,null),(ChartKind.Box,".18"),(ChartKind.Violin,".85")])
    {
        XElement[] Bodies(XDocument doc)=>doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("class")=="lumen-datum").SelectMany(g=>g.Elements(ns+"rect"))
            .Where(r=>(string?)r.Attribute("fill-opacity")==opacity).ToArray();
        XDocument plain=Svg(Sample(kind)),reversed=Svg(Sample(kind) with{YReversed=true});
        Check(Bodies(plain).Length>0&&Bodies(plain).Length==Bodies(reversed).Length,$"{kind}: bodies went missing");
        foreach(var (before,after) in Bodies(plain).Zip(Bodies(reversed)))
            Check(Near(Attr(after,"y"),422-Attr(before,"y")-Attr(before,"height"))&&Near(Attr(after,"height"),Attr(before,"height")),$"{kind}: a body is not mirrored");
    }
    // A density cell still reads its range from low to high when the low edge is at the top.
    var cells=Svg(Spec(ChartKind.Scatter) with{YReversed=true,DensityCells=10,Series=[new("Cloud",Enumerable.Range(0,400).Select(i=>new ChartPoint(i%37,i%23)).ToArray())]})
        .Descendants(ns+"g").Select(g=>(string?)g.Attribute("aria-label")).OfType<string>().Where(l=>l.StartsWith("Cloud:")).ToArray();
    Check(cells.Length>0&&cells.All(label=>{var ends=label.Split(", ")[^1].Split(" to ").Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray();return ends[0]<ends[1];}),
        string.Join(" | ",cells.Take(3)));
});
Test("Formats and reversal are refused where they cannot apply",()=>{
    string Refusal(ChartSpec spec){try{ChartSvg.Render(spec);}catch(ArgumentException error){return error.Message;}throw new Exception($"{spec.Kind} accepted a setting it cannot draw");}
    foreach(var format in (ValueFormat[])[ValueFormat.Duration,ValueFormat.Compact])
        Check(Refusal(TimeSpec(Utc(2026,1,1),3_600_000,5) with{XFormat=format}).Contains("time axis"),$"{format} on a time axis");
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        var spec=Sample(kind);
        if(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Timeline or ChartKind.Blocks)
            ChartSvg.Render(spec with{XFormat=ValueFormat.Duration});
        else Check(Refusal(spec with{XFormat=ValueFormat.Duration}).Contains(kind==ChartKind.Calendar?"writes its own calendar":"X format"),$"{kind}: X format");
        if(kind is ChartKind.Donut or ChartKind.Heatmap or ChartKind.Radar or ChartKind.Histogram)
        {
            Check(Refusal(spec with{YFormat=ValueFormat.Compact}).Contains("Y format"),$"{kind}: Y format");
            Check(Refusal(spec with{Y2Format=ValueFormat.Duration}).Contains("Y format"),$"{kind}: Y2 format");
        }
        // A timeline's lanes are states, not values on a Y axis.
        else if(kind==ChartKind.Timeline)
            Check(Refusal(spec with{YFormat=ValueFormat.Compact}).Contains("lanes")&&Refusal(spec with{Y2Format=ValueFormat.Duration}).Contains("lanes"),$"{kind}: Y format");
        // A calendar's Y format writes its days' values, and it has no secondary axis.
        else if(kind==ChartKind.Calendar)
        {
            ChartSvg.Render(spec with{YFormat=ValueFormat.Duration});
            Check(Refusal(spec with{Y2Format=ValueFormat.Duration}).Contains("no secondary axis"),$"{kind}: Y2 format");
        }
        else ChartSvg.Render(spec with{YFormat=ValueFormat.Duration,Y2Format=ValueFormat.Compact});
        if(kind is ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Area or ChartKind.Histogram)
        {
            Check(Refusal(spec with{YReversed=true}).Contains("zero baseline"),$"{kind}: reversed Y");
            Check(Refusal(spec with{Y2Reversed=true}).Contains("zero baseline"),$"{kind}: reversed Y2");
        }
        else if(kind is ChartKind.Donut or ChartKind.Heatmap or ChartKind.Radar or ChartKind.Gauge or ChartKind.Ring) Check(Refusal(spec with{YReversed=true}).Contains("no Y axis"),$"{kind}: reversed Y");
        else if(kind==ChartKind.Timeline) Check(Refusal(spec with{YReversed=true}).Contains("lanes"),$"{kind}: reversed Y");
        else if(kind==ChartKind.Strip) Check(Refusal(spec with{YReversed=true}).Contains("has no axes"),$"{kind}: reversed Y");
        else if(kind==ChartKind.Calendar) Check(Refusal(spec with{YReversed=true}).Contains("rather than measuring it on a Y axis")&&Refusal(spec with{Y2Reversed=true}).Contains("no secondary axis"),$"{kind}: reversed Y");
        else ChartSvg.Render(spec with{YReversed=true,Y2Reversed=true});
    }
    Reject(()=>ChartSvg.Render(Spec() with{YFormat=(ValueFormat)9}));
    Reject(()=>ChartSvg.Render(Spec() with{Y2Format=(ValueFormat)(-1)}));
});
string Operate(ChartSpec spec,Func<LumenChart,Task> act,bool fit=false)
{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        LumenChart? chart=null;
        RenderFragment content=b=>{b.OpenComponent<LumenChart>(0);b.AddAttribute(1,"Spec",spec);if(fit)b.AddAttribute(2,"FitWidth",true);b.AddComponentReferenceCapture(3,c=>chart=(LumenChart)c);b.CloseComponent();};
        return renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<CascadingValue<ChartStyle>>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Value",ChartStyle.Light},{"ChildContent",content}}));
            await act(chart!);
            await root.QuiescenceTask;
            return root.ToHtmlString();
        }).GetAwaiter().GetResult();
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
}
Test("Durations reach tooltips, the component's data table and status line; CSV keeps the seconds",()=>{
    var run=Spec() with{XFormat=ValueFormat.Duration,YFormat=ValueFormat.Duration,YReversed=true,Y2Format=ValueFormat.Compact,
        Series=[new("Pace",[new(0,301),new(600,295),new(1200,288.4)]),new("Climb",[new(0,1200),new(600,15_400),new(1200,15_900)]){Secondary=true}]};
    var labels=Svg(run).Descendants().Select(e=>(string?)e.Attribute("aria-label")).OfType<string>().ToArray();
    Check(labels.Contains("Pace: 10:00, 4:55")&&labels.Contains("Climb: 20:00, 15.9k"),string.Join(" | ",labels));
    var csv=ChartExport.Csv(run);
    Check(csv.Contains("\"Pace\",600,295,")&&csv.Contains("\"Pace\",1200,288.4,")&&csv.Contains("\"Climb\",600,15400,"),csv);
    var html=Operate(run,async chart=>{
        // The table opens on a click, which static rendering cannot send, so the test opens it directly.
        typeof(LumenChart).GetField("showData",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(chart,true);
        await chart.SelectPoint(0,1);
    });
    Check(html.Contains("<tr><td>Pace</td><td>10:00</td><td>4:55</td></tr>")&&html.Contains("<tr><td>Climb</td><td>20:00</td><td>15.9k</td></tr>"),"the table reads raw numbers");
    Check(html.Contains("Pace: 10:00 = 4:55"),"the status line reads raw numbers");
});
Test("Formats and reversal survive JSON as strings, and a request that names none keeps the defaults",()=>{
    var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    var spec=Spec() with{XAxis=AxisKind.Log,XFormat=ValueFormat.Duration,YFormat=ValueFormat.Compact,Y2Format=ValueFormat.Duration,YReversed=true,Y2Reversed=true,
        Series=[new("Power",[new(1,1200),new(60,420),new(1200,290)]),new("Pace",[new(1,300),new(60,290),new(1200,310)]){Secondary=true}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,options);
    Check(json.Contains("\"xFormat\":\"Duration\"")&&json.Contains("\"yFormat\":\"Compact\"")&&json.Contains("\"y2Format\":\"Duration\"")&&json.Contains("\"yReversed\":true")&&json.Contains("\"y2Reversed\":true"),json);
    Check(ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,options)!)==ChartSvg.Render(spec),"the settings changed in transit");
    var written="{\"title\":\"Example\",\"kind\":\"Line\",\"yFormat\":\"Duration\",\"yReversed\":true,\"series\":[{\"name\":\"Pace\",\"points\":[{\"x\":0,\"y\":301},{\"x\":1,\"y\":295}]}]}";
    var read=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,options)!;
    Check(read.YFormat==ValueFormat.Duration&&read.YReversed&&ChartSvg.Render(read).Contains(">5:00<"),"hand-written JSON lost its format");
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]}]}",options)!;
    Check(old.XFormat==ValueFormat.Number&&old.YFormat==ValueFormat.Number&&old.Y2Format==ValueFormat.Number&&!old.YReversed&&!old.Y2Reversed);
});
Test("An axis that asks for no format or reversal reads exactly as before",()=>{
    var axis=new Axis(AxisKind.Linear,0,1);
    Check(axis.ValueFormat==ValueFormat.Number&&!axis.Reversed&&new ChartSpec().XFormat==ValueFormat.Number&&!new ChartSpec().YReversed);
    var random=new Random(3);
    for(var i=0;i<2000;i++)
    {
        var value=(random.NextDouble()-.5)*Math.Pow(10,random.Next(-4,12));
        Check(axis.Format(value)==LinearScale.Label(value)&&new Axis(AxisKind.Log,1,10).Format(value)==LinearScale.Label(value),$"{value}");
    }
    var range=new Axis(AxisKind.Linear,-37,1234);
    Check(range.Ticks().SequenceEqual(new LinearScale(-37,1234).Ticks().Select(v=>(v,LinearScale.Label(v)))),"plain ticks moved");
});
Test("Axes at extreme magnitudes finish their ticks",()=>{
    // Near 1e21 a step can be too small to move the value it is added to; minor lines once looped for ever there.
    var work=Task.Run(()=>{
        foreach(var format in Enum.GetValues<ValueFormat>())
        {
            var axis=new Axis(AxisKind.Linear,1e21,1e21+131072){ValueFormat=format};
            Check(axis.Ticks().Count<=5);axis.MinorTicks();
        }
        LogSeconds(1e95,1e100).Ticks();
        ChartSvg.Render(Spec() with{MinorGridlines=true,YFormat=ValueFormat.Duration,YMin=1e21,YMax=1e21+131072,Series=[new("S",[new(0,1e21+65536)])]});
    });
    Check(work.Wait(TimeSpan.FromSeconds(20)),"an axis near 1e21 did not finish");
});
// 0.20.0: zones on charts. The plot runs from x 76 to 870 and y 344 to 78; with Y fixed at 100 to 200 a value v sits at PY(v).
ZoneScale Effort()=>new([new("Easy",120),new("Steady",140),new("Hard",160),new("Max",double.PositiveInfinity)]);
string[] Ramp=[..ChartStyle.Light.Zones];
double PY(double value)=>344-(value-100)/100*266;
ChartSpec Effortful(params double[] values)=>Spec() with{YMin=100,YMax=200,Series=[new("Heart rate",values.Select((v,i)=>new ChartPoint(i,v)).ToArray()){Zones=Effort()}]};
(string Ink,(double X,double Y)[] Points)[] Strokes(XDocument doc)=>doc.Descendants(ns+"path")
    .Where(p=>(string?)p.Attribute("fill")=="none"&&(string?)p.Attribute("stroke-width")=="2.5")
    .Select(p=>((string)p.Attribute("stroke")!,p.Attribute("d")!.Value.Split(' ').Select(c=>c[1..].Split(','))
        .Select(c=>(double.Parse(c[0],CultureInfo.InvariantCulture),double.Parse(c[1],CultureInfo.InvariantCulture))).ToArray())).ToArray();
void Matches((double X,double Y)[] actual,params (double X,double Y)[] expected)
{
    Check(actual.Length==expected.Length,$"{actual.Length} points where {expected.Length} were expected");
    for(var i=0;i<actual.Length;i++)
        Check(Math.Abs(actual[i].X-expected[i].X)<1e-6&&Math.Abs(actual[i].Y-expected[i].Y)<1e-6,$"({actual[i].X}, {actual[i].Y}) is not ({expected[i].X}, {expected[i].Y})");
}
XElement[] Points(XDocument doc)=>doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).Select(g=>g.Elements().Last()).ToArray();
string Tint(string colour,string background,double opacity)=>"#"+string.Concat(new[]{1,3,5}.Select(at=>
    ((int)Math.Round(Convert.ToInt32(background.Substring(at,2),16)*(1-opacity)+Convert.ToInt32(colour.Substring(at,2),16)*opacity)).ToString("X2")));
Test("A line crossing zone bounds is split exactly where it crosses each, and each piece takes its zone's colour",()=>{
    // 110 to 150 crosses 120 a quarter of the way along and 140 three quarters of the way.
    var up=Strokes(Svg(Classic(Effortful(110,150))));
    Check(up.Select(s=>s.Ink).SequenceEqual(Ramp[..3]),string.Join(",",up.Select(s=>s.Ink)));
    Matches(up[0].Points,(76,PY(110)),(274.5,PY(120)));
    Matches(up[1].Points,(274.5,PY(120)),(671.5,PY(140)));
    Matches(up[2].Points,(671.5,PY(140)),(870,PY(150)));
    var down=Strokes(Svg(Classic(Effortful(150,110))));
    Check(down.Select(s=>s.Ink).SequenceEqual(Ramp[..3].Reverse()));
    Matches(down[0].Points,(76,PY(150)),(274.5,PY(140)));
    Matches(down[1].Points,(274.5,PY(140)),(671.5,PY(120)));
    Matches(down[2].Points,(671.5,PY(120)),(870,PY(110)));
    // Crossing three bounds in one segment splits it into four pieces.
    Check(Strokes(Svg(Classic(Effortful(105,195)))).Select(s=>s.Ink).SequenceEqual(Ramp[..4]));
    // On a log axis the segment is straight on screen, not in the data, so the split is interpolated on screen:
    // 100 is half way from 10 to 1000 there, against a tenth of the way in the data.
    var log=Strokes(Svg(Classic(Spec() with{YAxis=AxisKind.Log,YMin=10,YMax=1000,Series=[new("S",[new(0,10),new(1,1000)]){Zones=new([new("Low",100),new("High",double.PositiveInfinity)])}]})));
    Check(log.Length==2);
    Matches(log[0].Points,(76,344),(473,211));
    Matches(log[1].Points,(473,211),(870,78));
});
Test("A value exactly on a bound takes the lower zone, as ZoneScale.IndexOf does",()=>{
    Check(Effort().IndexOf(120)==0&&Effort().IndexOf(Math.BitIncrement(120))==1);
    var doc=Svg(Classic(Effortful(110,120,130,120,110)));
    var strokes=Strokes(doc);
    // Rising to the bound and falling back to it stay below it; leaving it upwards starts the zone above at once.
    Check(strokes.Select(s=>s.Ink).SequenceEqual([Ramp[0],Ramp[1],Ramp[0]]),string.Join(",",strokes.Select(s=>s.Ink)));
    Matches(strokes[0].Points,(76,PY(110)),(274.5,PY(120)));
    Matches(strokes[1].Points,(274.5,PY(120)),(473,PY(130)),(671.5,PY(120)));
    Matches(strokes[2].Points,(671.5,PY(120)),(870,PY(110)));
    var marks=doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
    Check((string?)marks[1].Element(ns+"circle")!.Attribute("fill")==Ramp[0]&&marks[1].Attribute("aria-label")!.Value=="Heart rate: 1, 120, Easy");
    Check((string?)marks[2].Element(ns+"circle")!.Attribute("fill")==Ramp[1]&&marks[2].Attribute("aria-label")!.Value=="Heart rate: 2, 130, Steady");
});
Test("A point's own colour beats its zone's, which beats the series colour",()=>{
    var series=new ChartSeries("Effort",[new(0,110){Color="#ABCDEF"},new(1,130),new(2,150)],"#123456"){Zones=Effort()};
    var line=Svg(Classic(Spec() with{YMin=100,YMax=200,Series=[series]}));
    Check(Points(line).Select(m=>(string?)m.Attribute("fill")).SequenceEqual(["#ABCDEF",Ramp[1],Ramp[2]]));
    // The segment from the coloured point is drawn whole in its colour although it crosses 120; the next splits at 140.
    var strokes=Strokes(line);
    Check(strokes.Select(s=>s.Ink).SequenceEqual(["#ABCDEF",Ramp[1],Ramp[2]]),string.Join(",",strokes.Select(s=>s.Ink)));
    Matches(strokes[0].Points,(76,PY(110)),(473,PY(130)));
    foreach(var kind in (ChartKind[])[ChartKind.Area,ChartKind.Scatter,ChartKind.Bubble,ChartKind.Column,ChartKind.Bar])
    {
        var marks=Points(Svg(Spec(kind) with{Series=[series]}));
        Check(marks.Select(m=>(string?)m.Attribute("fill")).SequenceEqual(["#ABCDEF",Ramp[1],Ramp[2]]),$"{kind} fills");
        if(kind is ChartKind.Scatter or ChartKind.Bubble) Check(marks.Select(m=>(string?)m.Attribute("stroke")).SequenceEqual(["#ABCDEF",Ramp[1],Ramp[2]]),$"{kind} outlines");
    }
    // Without zones a point with no colour keeps the series colour, and a segment the colour of the point it starts from.
    var plain=Svg(Classic(Spec() with{Series=[series with{Zones=null,Points=[new(0,110),new(1,130){Color="#ABCDEF"},new(2,150),new(3,140){Color="#FEDCBA"}]}]}));
    Check(Points(plain).Select(m=>(string?)m.Attribute("fill")).SequenceEqual(["#123456","#ABCDEF","#123456","#FEDCBA"]));
    Check(Strokes(plain).Select(s=>s.Ink).SequenceEqual(["#123456","#ABCDEF","#123456"]));
    Check(Strokes(Svg(Classic(Spec() with{Series=[series with{Zones=null,Points=[new(0,1),new(1,2)]}]}))).Single().Ink=="#123456");
});
Test("An area keeps its fill in the series colour while its stroke follows the zones",()=>{
    var doc=Svg(Classic(Effortful(110,150) with{Kind=ChartKind.Area,YMin=0}));
    Check((string?)doc.Descendants(ns+"path").Single(p=>(string?)p.Attribute("fill-opacity")==".12").Attribute("fill")==ChartStyle.Light.Series[0]);
    Check(Strokes(doc).Select(s=>s.Ink).SequenceEqual(Ramp[..3]));
});
Test("A long zone-coloured line colours its sampled segments by the zone each piece lies in",()=>{
    var values=Enumerable.Range(0,6000).Select(i=>Math.Round(150+40*Math.Sin(i/37.0)+i%7,2)).ToArray();
    var doc=Svg(Classic(Effortful(values) with{MaxRenderedPoints=400}));
    var marks=doc.Descendants(ns+"g").Count(g=>g.Attribute("data-point") is not null);
    Check(marks is > 100 and <= 400,$"{marks} marks");
    var pieces=0;
    foreach(var (ink,points) in Strokes(doc))
        for(var i=1;i<points.Length;i++)
        {
            // A piece lies within one zone, so its midpoint names the zone.
            var value=100+(344-(points[i-1].Y+points[i].Y)/2)/266*100;
            Check(ink==Ramp[Effort().IndexOf(value)],$"a piece around {value:0.##} is drawn in {ink}");
            pieces++;
        }
    Check(pieces>marks,$"{pieces} pieces for {marks} sampled points");
});
Test("Zone bands are built from the scale, clamped to the plot, and leave the axis range alone",()=>{
    var data=Classic(Spec() with{Series=[new("S",[new(0,105),new(1,150),new(2,130)])]});
    string[] Ticks(XDocument doc)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="end"&&(string?)t.Attribute("class")=="lumen-muted").Select(t=>t.Value).ToArray();
    XDocument plain=Svg(data),zoned=Svg(data with{YZones=Effort()});
    Check(Ticks(plain).Length>=2&&Ticks(plain).SequenceEqual(Ticks(zoned)),"the bands moved the axis");
    Check(plain.Descendants(ns+"circle").Select(c=>(string?)c.Attribute("cy")).SequenceEqual(zoned.Descendants(ns+"circle").Select(c=>(string?)c.Attribute("cy"))));
    var doc=Svg(data with{YMin=100,YMax=150,YZones=Effort()});
    var bands=doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("role")=="img").ToArray();
    // The open bottom stops at the axis minimum and Hard at the maximum; Max lies wholly above the plot and is left out.
    // Each reads its zone's own range, not the clamp.
    Check(bands.Select(b=>b.Attribute("aria-label")!.Value).SequenceEqual(["Easy: up to 120","Steady: 120 to 140","Hard: 140 to 160"]),string.Join(" | ",bands.Select(b=>b.Attribute("aria-label")!.Value)));
    double Py(double v)=>344-(v-100)/50*266;
    (double From,double To)[] spans=[(100,120),(120,140),(140,150)];
    for(var i=0;i<3;i++)
    {
        var rect=bands[i].Element(ns+"rect")!;
        Check(Math.Abs((double)rect.Attribute("y")!-Py(spans[i].To))<1e-6&&Math.Abs((double)rect.Attribute("height")!-(Py(spans[i].From)-Py(spans[i].To)))<1e-6,$"band {i} spans the wrong values");
        Check((double)rect.Attribute("x")! ==76&&(double)rect.Attribute("width")! ==794&&(string?)rect.Attribute("fill")==Ramp[i]&&(string?)rect.Attribute("fill-opacity")==".12");
        Check((string?)bands[i].Element(ns+"text")!.Attribute("fill")==ChartStyle.Light.Text&&bands[i].Element(ns+"text")!.Value==bands[i].Attribute("aria-label")!.Value);
    }
    // The unbounded top zone runs to the top of the plot.
    var high=Svg(data with{YMin=100,YMax=200,YZones=Effort()}).Descendants(ns+"g").Single(g=>(string?)g.Attribute("aria-label")=="Max: above 160").Element(ns+"rect")!;
    Check((double)high.Attribute("y")! ==78&&Math.Abs((double)high.Attribute("height")!-(PY(160)-78))<1e-6);
    // Behind the data and behind any annotation.
    var labels=Svg(data with{YZones=Effort(),Annotations=[new(AnnotationAxis.Y,125){Label="Target"}]}).Descendants(ns+"g").Select(g=>(string?)g.Attribute("aria-label") ?? "").ToList();
    Check(labels.FindIndex(l=>l.StartsWith("Hard"))<labels.FindIndex(l=>l.StartsWith("Target"))&&labels.FindIndex(l=>l.StartsWith("Target"))<labels.FindIndex(l=>l.StartsWith("S:")));
    // On a reversed axis the lowest zone sits at the top.
    Check((double)Svg(data with{YMin=100,YMax=150,YReversed=true,YZones=Effort()}).Descendants(ns+"g").Single(g=>(string?)g.Attribute("aria-label")=="Easy: up to 120").Element(ns+"rect")!.Attribute("y")! ==78);
    // A horizontal bar chart measures along X, so its bands stand upright across the value axis.
    var bar=Svg(Spec(ChartKind.Bar) with{Series=[new("S",[new(0,5),new(1,30)])],YZones=new([new("Low",10),new("High",double.PositiveInfinity)])});
    var low=bar.Descendants(ns+"g").Single(g=>(string?)g.Attribute("aria-label")=="Low: up to 10").Element(ns+"rect")!;
    Check((double)low.Attribute("x")! ==160&&Math.Abs((double)low.Attribute("width")!-710/3d)<1e-6&&(double)low.Attribute("y")! ==78&&(double)low.Attribute("height")! ==266);
    // A bound a log axis cannot reach sits off it rather than breaking the drawing.
    var log=ChartSvg.Render(Spec() with{YAxis=AxisKind.Log,Series=[new("S",[new(0,10),new(1,1000)])],YZones=new([new("Below",-5),new("Small",100),new("Large",double.PositiveInfinity)])});
    Check(!log.Contains("NaN")&&!log.Contains("Below:")&&log.Contains("Small: -5 to 100")&&log.Contains("Large: above 100"));
    Check(ChartSvg.Render(Spec() with{YZones=new([new("All",double.PositiveInfinity)])}).Contains("All: every value"));
});
Test("Labels and tooltips name the zone after the value",()=>{
    var heart=ZoneScale.CogganHeartRate(170);
    var stream=Spec() with{XFormat=ValueFormat.Duration,Series=[new("Heart rate",[new(0,98),new(10,152),new(20,171)]){Zones=heart}]};
    foreach(var kind in (ChartKind[])[ChartKind.Line,ChartKind.Area,ChartKind.Scatter,ChartKind.Bubble])
    {
        var marks=Svg(stream with{Kind=kind}).Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
        Check(marks.Select(m=>m.Attribute("aria-label")!.Value).SequenceEqual(["Heart rate: 0:00, 98, Active recovery","Heart rate: 0:10, 152, Tempo","Heart rate: 0:20, 171, Lactate threshold"]),$"{kind}");
        Check(marks.All(m=>m.Element(ns+"title")!.Value==m.Attribute("aria-label")!.Value));
    }
    foreach(var kind in (ChartKind[])[ChartKind.Column,ChartKind.Bar])
        Check(ChartSvg.Render(stream with{Kind=kind,XFormat=ValueFormat.Number}).Contains("aria-label='Heart rate: 10, 152, Tempo'"),$"{kind}");
    // The component's own tooltips read the accessible name.
    Check(Operate(stream,_=>Task.CompletedTask).Contains("Heart rate: 0:10, 152, Tempo"));
    Check(!ChartSvg.Render(stream with{Series=[stream.Series[0] with{Zones=null}]}).Contains("Tempo"));
});
Test("A zone without a colour takes the style's ramp at its position, and one with a colour keeps it",()=>{
    var scale=new ZoneScale([new("A",10),new("B",20,"#123456"),new("C",double.PositiveInfinity)]);
    var spec=Spec(ChartKind.Column) with{Series=[new("S",[new(0,5),new(1,15),new(2,25)]){Zones=scale}]};
    string?[] Fills(ChartSpec chart)=>Points(Svg(chart)).Select(m=>(string?)m.Attribute("fill")).ToArray();
    Check(Fills(spec).SequenceEqual([Ramp[0],"#123456",Ramp[2]]));
    Check(Fills(spec with{Theme=ChartTheme.Dark}).SequenceEqual([ChartStyle.Dark.Zones[0],"#123456",ChartStyle.Dark.Zones[2]]));
    var brand=Brand() with{Zones=["#111111","#222222","#333333"]};
    Check(Fills(spec with{Style=brand}).SequenceEqual(["#111111","#123456","#333333"]));
    Check(RenderInside(brand,spec).Contains("fill='#333333'"),"a cascaded ramp did not reach the component");
});
Test("The zone ramp clears 3:1 on both presets, and its band labels 4.5:1 over their tint",()=>{
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark})
    {
        Check(style.Zones.Count>=7);
        foreach(var colour in style.Zones)
        {
            Check(Contrast(colour,style.Background)>=3,$"{colour} on {style.Background} is {Contrast(colour,style.Background):0.00}");
            // A band is its zone colour at .12 over the background, and its label is drawn in the text colour.
            Check(Contrast(style.Text,Tint(colour,style.Background,.12))>=4.5,$"a label over {colour} is {Contrast(style.Text,Tint(colour,style.Background,.12)):0.00}");
        }
        Check(!style.ContrastIssues().Any(i=>i.Element.StartsWith("Zone")));
    }
    Check(Contrast(Ramp[0],LightBackground)>=3&&Contrast(Ramp[0],DarkBackground)>=3);
    var issues=(ChartStyle.Light with{Zones=["#3F87D9","#FFD60A"]}).ContrastIssues();
    Check(issues.Count==1&&issues[0].Element=="Zone 2"&&issues[0].Foreground=="#FFD60A"&&issues[0].Required==3&&issues[0].Ratio<2);
});
Test("Point colours draw time in zone and colour by a derived value",()=>{
    var heart=ZoneScale.CogganHeartRate(170);
    var seconds=Training.TimeInZone(Enumerable.Range(0,600).Select(i=>100+i*.15).ToArray(),heart);
    var bars=Svg(Spec(ChartKind.Bar) with{YFormat=ValueFormat.Duration,
        Series=[new("Time in zone",heart.Zones.Select((zone,i)=>new ChartPoint(i,seconds[i],zone.Name){Color=zone.Color ?? ChartStyle.Light.Zones[i]}).ToArray())]});
    Check(Points(bars).Select(m=>(string?)m.Attribute("fill")).SequenceEqual(Ramp[..5]));
    Check(bars.Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")==$"Time in zone: Tempo, {new Axis(AxisKind.Linear,0,1){ValueFormat=ValueFormat.Duration}.Format(seconds[2])}"));
    var donut=Svg(Spec(ChartKind.Donut) with{Series=[new("Mix",[new(0,3,"A"){Color="#ABCDEF"},new(1,2,"B")])]});
    Check(Points(donut).Select(m=>(string?)m.Attribute("fill")).SequenceEqual(["#ABCDEF",ChartStyle.Light.Series[1]]));
    Check(donut.Descendants(ns+"circle").Select(c=>(string?)c.Attribute("fill")).SequenceEqual(["#ABCDEF",ChartStyle.Light.Series[1]]),"the donut's key lost the colour");
    // An elevation profile coloured by grade: each segment takes the colour of the point it starts from, the fill the series'.
    double[] grades=[0,2,6,9,4,-3];
    string Grade(double grade)=>grade>=6?"#DD4B45":grade>=2?"#DB6A1F":"#2E9B58";
    var profile=Svg(Classic(Spec(ChartKind.Area) with{Series=[new("Elevation",grades.Select((g,i)=>new ChartPoint(i,100+i*5){Color=Grade(g)}).ToArray())]}));
    Check(Strokes(profile).Select(s=>s.Ink).SequenceEqual(["#2E9B58","#DB6A1F","#DD4B45","#DB6A1F"]),string.Join(",",Strokes(profile).Select(s=>s.Ink)));
    Check((string?)profile.Descendants(ns+"path").Single(p=>(string?)p.Attribute("fill-opacity")==".12").Attribute("fill")==ChartStyle.Light.Series[0]);
});
Test("Zones and point colours are refused where colour already means something else",()=>{
    bool Accepts(ChartSpec chart){try{ChartSvg.Render(chart);return true;}catch(ArgumentException){return false;}}
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        var sample=Sample(kind);
        var zoned=sample with{Series=sample.Series.Select(s=>s with{Zones=Effort()}).ToArray()};
        var coloured=sample with{Series=sample.Series.Select(s=>s with{Points=s.Points.Select(p=>p with{Color="#ABCDEF"}).ToArray()}).ToArray()};
        Check(Accepts(zoned)==(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar or ChartKind.Blocks),$"zones on {kind}");
        Check(Accepts(coloured)==(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar or ChartKind.Range or ChartKind.Blocks or ChartKind.Donut or ChartKind.Strip),$"point colours on {kind}");
        // Zone bands go wherever a Y annotation goes, and nowhere else; a calendar's zones colour its days, and it has no Y axis
        // for a Y annotation.
        Check(Accepts(sample with{YZones=Effort()})==(kind==ChartKind.Calendar||Accepts(sample with{Annotations=[new(AnnotationAxis.Y,1)]})),$"zone bands on {kind}");
    }
    Check(!Accepts(Sample(ChartKind.Donut) with{YZones=Effort()})&&Accepts(Sample(ChartKind.StackedColumn) with{YZones=Effort()}));
    Reject(()=>ChartSvg.Render(Spec() with{Series=[new("S",[new(0,1){Color="red"}])]}));
    Reject(()=>ChartSvg.Render(Spec() with{Series=[new("S",[new(0,1){Color="#ABC' onload='x"}])]}));
    Reject(()=>ChartSvg.Render(Spec() with{Series=[new("S",[new(0,1)]){Zones=new([new("A",double.PositiveInfinity,"#12345")])}]}));
    Reject(()=>ChartSvg.Render(Spec() with{YZones=new([new("A",double.PositiveInfinity,"blue")])}));
    Reject(()=>ChartSvg.Render(Spec() with{YZones=new([new("Bad\u0001",double.PositiveInfinity)])}));
    Reject(()=>ChartSvg.Render(Spec() with{Style=ChartStyle.Light with{Zones=[]}}));
    Reject(()=>ChartSvg.Render(Spec() with{Style=ChartStyle.Light with{Zones=null!}}));
    Reject(()=>ChartSvg.Render(Spec() with{Style=ChartStyle.Light with{Zones=["#12345G"]}}));
    Reject(()=>ChartSvg.Render(Spec() with{Style=ChartStyle.Light with{Zones=Enumerable.Repeat("#123456",33).ToArray()}}));
    // A density scatter shades cells, so there is no mark for a zone or a point to colour.
    Reject(()=>ChartSvg.Render(Cloud(100,20) with{Series=[Cloud(100,20).Series[0] with{Zones=Effort()}]}));
    Reject(()=>ChartSvg.Render(Cloud(100,20) with{Series=[Cloud(100,20).Series[0] with{Points=[new(0,1){Color="#ABCDEF"}]}]}));
    // Zones past the end of the ramp need colours of their own rather than repeating one.
    var eight=new ZoneScale(Enumerable.Range(0,8).Select(i=>new Zone($"Z{i+1}",i<7?i*10:double.PositiveInfinity)).ToArray());
    Reject(()=>ChartSvg.Render(Spec() with{YZones=eight}));
    Reject(()=>ChartSvg.Render(Spec() with{Series=[Spec().Series[0] with{Zones=eight}]}));
    Check(Accepts(Spec() with{YZones=new(eight.Zones.Select((z,i)=>i==7?z with{Color="#123456"}:z).ToArray())}));
    Reject(()=>ChartSvg.Render(Spec() with{Style=ChartStyle.Light with{Zones=["#3F87D9"]},YZones=Effort()}));
    Reject(()=>ChartSvg.Render(Spec() with{YZones=new(Enumerable.Range(0,33).Select(i=>new Zone($"Z{i}",i<32?i:double.PositiveInfinity,"#123456")).ToArray())}));
});
Test("Zones, zone colours and point colours survive JSON, the top bound as Infinity",()=>{
    var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    var spec=Spec() with{YZones=Effort(),Style=ChartStyle.Light with{Zones=["#3F87D9","#2E9B58","#A88200","#DD4B45"]},
        Series=[new("Effort",[new(0,110){Color="#ABCDEF"},new(1,150)]){Zones=new([new("Low",130,"#123456"),new("High",double.PositiveInfinity)])}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,options);
    Check(json.Contains("\"yZones\":{\"zones\":[{\"name\":\"Easy\",\"upper\":120,\"color\":null}")&&json.Contains("\"upper\":\"Infinity\"")
        &&json.Contains("\"color\":\"#ABCDEF\"")&&json.Contains("{\"name\":\"Low\",\"upper\":130,\"color\":\"#123456\"}")&&json.Contains("\"zones\":[\"#3F87D9\""),json);
    Check(ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,options)!)==ChartSvg.Render(spec),"the zones changed in transit");
    var written="{\"kind\":\"Line\",\"yZones\":{\"zones\":[{\"name\":\"Easy\",\"upper\":120},{\"name\":\"Hard\",\"upper\":\"Infinity\"}]},"+
        "\"series\":[{\"name\":\"S\",\"zones\":{\"zones\":[{\"name\":\"Easy\",\"upper\":120},{\"name\":\"Hard\",\"upper\":\"Infinity\",\"color\":\"#123456\"}]},\"points\":[{\"x\":0,\"y\":110},{\"x\":1,\"y\":130,\"color\":\"#ABCDEF\"}]}]}";
    var read=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,options)!;
    Check(read.YZones!.Zones[1].Upper==double.PositiveInfinity&&read.Series[0].Zones!.Zones[1].Color=="#123456"&&read.Series[0].Points[1].Color=="#ABCDEF");
    var svg=ChartSvg.Render(read);
    Check(svg.Contains("Hard: above 120")&&svg.Contains("S: 1, 130, Hard")&&svg.Contains("fill='#ABCDEF'")&&svg.Contains("stroke='#848484'"),"hand-written JSON lost its zones");
    // A scale that breaks its own rules is invalid JSON, which a host answers as a bad request rather than a server error.
    foreach(var broken in (string[])["{\"zones\":[{\"name\":\"A\",\"upper\":5},{\"name\":\"B\",\"upper\":3}]}","{\"zones\":[]}","{}","{\"zones\":[{\"name\":\"A\",\"upper\":5}]}","{\"zones\":[null]}"])
    {
        try{System.Text.Json.JsonSerializer.Deserialize<ZoneScale>(broken,options);throw new Exception($"{broken} was accepted");}
        catch(System.Text.Json.JsonException){}
    }
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"style\":{\"background\":\"#FFFFFF\"},\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]}]}",options)!;
    Check(old.YZones is null&&old.Series[0].Zones is null&&old.Series[0].Points[0].Color is null&&old.Style!.Zones.SequenceEqual(ChartStyle.Light.Zones));
});
Test("A chart that asks for no zones or point colours draws as before",()=>{
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        var svg=ChartSvg.Render(Sample(kind));
        // The ramp reaches neither the stylesheet nor a mark until a zone asks for it.
        Check(svg==ChartSvg.Render(Sample(kind) with{Style=ChartStyle.Light with{Zones=["#010203"]}}),$"{kind} moved with the ramp");
        Check(!Ramp.Any(svg.Contains),$"{kind} drew a zone colour");
    }
    var doc=Svg(Classic(Spec() with{Series=[new("S",[new(0,2),new(1,3),new(2,null),new(3,1),new(4,2)])]}));
    Check(Strokes(doc).Select(s=>s.Ink).SequenceEqual([ChartStyle.Light.Series[0],ChartStyle.Light.Series[0]]),"a plain line no longer draws one stroke per run");
});
Test("A horizontal bar chart draws a value reference upright at its value, its label inside the plot",()=>{
    // The value axis runs along X, from 0 at x=160 to 40 at x=870, so its middle is at 20.
    var doc=Svg(Classic(Spec(ChartKind.Bar) with{Series=[new("S",[new(0,10,"A"),new(1,40,"B")])],Annotations=[
        new(AnnotationAxis.Y,10){Label="Floor"},new(AnnotationAxis.Y,35){Label="Target"},new(AnnotationAxis.Y,45){Label="Beyond"},
        new(AnnotationAxis.Y,5){To=15,Label="Low"},new(AnnotationAxis.Y,30){To=50,Label="Stretch"}]}));
    double Px(double value)=>160+value/40*710;
    XElement Reference(string name)=>doc.Descendants(ns+"g").Single(g=>(string?)g.Attribute("aria-label")==name);
    (double X,string? Anchor) Label(string name)=>((double)Reference(name).Element(ns+"text")!.Attribute("x")!,(string?)Reference(name).Element(ns+"text")!.Attribute("text-anchor"));
    foreach(var (name,value) in new[]{("Floor: 10",10d),("Target: 35",35d)})
    {
        var line=Reference(name).Elements(ns+"line").Last();
        Check((double)line.Attribute("x1")! ==Px(value)&&(double)line.Attribute("x2")! ==Px(value)&&(double)line.Attribute("y1")! ==78&&(double)line.Attribute("y2")! ==344,$"{name} is not upright at its value");
    }
    var low=Reference("Low: 5 to 15").Element(ns+"rect")!;
    Check((double)low.Attribute("x")! ==Px(5)&&Math.Abs((double)low.Attribute("width")!-(Px(15)-Px(5)))<1e-6&&(double)low.Attribute("y")! ==78&&(double)low.Attribute("height")! ==266,"the band does not span its values upright");
    // A label reads into the larger side of the plot: right of a reference in the left half, left of one in the right half.
    Check(Label("Floor: 10")==(Px(10)+6,"start")&&Label("Low: 5 to 15")==(Px(5)+6,"start")&&Label("Target: 35")==(Px(35)-6,"end"),"a label runs towards the nearer edge");
    // A band running off the plot labels the part the plot shows; a reference wholly off it turns its label away, to clip with it.
    Check(Label("Stretch: 30 to 50")==(870-6,"end")&&Label("Beyond: 45")==(Px(45)+6,"start"),"a label is placed off the part of the plot its reference covers");
    // X stays refused: a category chart places its bars by index, not at values.
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Bar) with{Annotations=[new(AnnotationAxis.X,1)]}));
});
// 0.21.0: several marks in one chart. Without a right-hand axis the plot runs from x 76 to 870, with one to 824, and y 344 to 78.
XElement[] Rects(XDocument doc,int series)=>doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null&&(string?)g.Attribute("data-series")==series.ToString(CultureInfo.InvariantCulture))
    .Select(g=>g.Element(ns+"rect")).OfType<XElement>().ToArray();
(double Left,double Right) Spans(XElement rect)=>(Attr(rect,"x"),Attr(rect,"x")+Attr(rect,"width"));
bool Close(double actual,double expected)=>Math.Abs(actual-expected)<1e-6;
(bool Dashed,string Ink,(double X,double Y)[] Points)[] Dashes(XDocument doc)=>doc.Descendants(ns+"path")
    .Where(p=>(string?)p.Attribute("fill")=="none"&&(string?)p.Attribute("stroke-width")=="2.5")
    .Select(p=>(p.Attribute("stroke-dasharray") is not null,(string)p.Attribute("stroke")!,p.Attribute("d")!.Value.Split(' ').Select(c=>c[1..].Split(','))
        .Select(c=>(double.Parse(c[0],CultureInfo.InvariantCulture),double.Parse(c[1],CultureInfo.InvariantCulture))).ToArray())).ToArray();
string[] Labels(XDocument doc)=>doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).Select(g=>(string)g.Attribute("aria-label")!).ToArray();
string[] Ticks(XDocument doc,string anchor)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")==anchor&&(string?)t.Attribute("class")=="lumen-muted").Select(t=>t.Value).ToArray();
Test("A column series on a continuous axis centres one bar on each X, 0.7 of the closest gap wide, rising from zero",()=>{
    var doc=Svg(Spec() with{YMin=-10,YMax=30,Series=[new("Level",[new(0,12),new(100,18)]),
        new("Load",[new(0,10),new(20,-5),new(25,20),new(60,15),new(100,8)]){Kind=ChartKind.Column}]});
    var bars=Rects(doc,1);
    double[] at=[0,20,25,60,100];
    Check(bars.Length==5,$"{bars.Length} bars");
    // X runs 0 to 100 across 794 pixels, so the closest two values, 20 and 25, are 39.7 pixels apart.
    for(var i=0;i<5;i++)
        Check(Close(Attr(bars[i],"width"),39.7*.7)&&Close(Attr(bars[i],"x")+Attr(bars[i],"width")/2,76+at[i]*7.94),$"bar {i} is not centred on its X at the width its gap allows");
    // With Y fixed at -10 to 30 zero sits at 277.5: a positive value rises from it and a negative one hangs below it.
    double At(double value)=>344-(value+10)/40*266;
    Check(Close(Attr(bars[0],"y"),At(10))&&Close(Attr(bars[0],"height"),At(0)-At(10)),"a positive column does not rise from zero");
    Check(Close(Attr(bars[1],"y"),At(0))&&Close(Attr(bars[1],"height"),At(-5)-At(0)),"a negative column does not hang from zero");
    Check(Labels(doc).Contains("Load: 25, 20"),"a column does not name its X and value");
    // Clamped as candle bodies are: at most 34 pixels and at least one, and 21 for a lone column.
    double[] Widths(params double[] columns)=>Rects(Svg(Spec() with{Series=[new("Level",[new(0,1),new(1000,2)]),
        new("Load",columns.Select(x=>new ChartPoint(x,1)).ToArray()){Kind=ChartKind.Column}]}),1).Select(b=>Attr(b,"width")).ToArray();
    Check(Widths(0,500,1000).All(width=>width==34),"a wide gap is not clamped to 34 pixels");
    Check(Widths(0,.5,1000).All(width=>width==1),"a narrow gap is not clamped to one pixel");
    Check(Widths(500).Single()==21,"a lone column is not 21 pixels wide");
});
Test("Column series on a continuous axis stand side by side in one slot per X and never overlap",()=>{
    var doc=Svg(Spec() with{Series=[new("A",[new(0,3),new(4,5),new(100,4)]){Kind=ChartKind.Column},new("Level",[new(0,1),new(100,2)]),
        new("B",[new(2,2),new(6,6),new(100,7)]){Kind=ChartKind.Column}]});
    XElement[] a=Rects(doc,0),b=Rects(doc,2);
    // The closest X values any column has are 2 apart, 15.88 pixels, so a slot is 0.7 of that and each of the two column
    // series takes half of it; the line takes no share. Slots sized from each series' own gap would overlap here.
    Check(a.Length==3&&b.Length==3&&a.Concat(b).All(r=>Close(Attr(r,"width"),15.88*.7/2)),"the slot is not shared by the two column series alone");
    var spans=a.Concat(b).Select(Spans).OrderBy(s=>s.Left).ToArray();
    for(var i=1;i<spans.Length;i++) Check(spans[i].Left>=spans[i-1].Right-1e-9,$"columns at {spans[i-1].Left} and {spans[i].Left} overlap");
    // Where both have a column they stand in series order, the pair centred on the X.
    Check(Close(Spans(a[2]).Right,Spans(b[2]).Left)&&Close((Spans(a[2]).Left+Spans(b[2]).Right)/2,870),"the pair at 100 is not centred on it");
});
Test("An axis that carries columns or an area includes zero, and one that carries only lines need not",()=>{
    var spec=Spec() with{Series=[new("Level",[new(0,100),new(1,120)]),new("Load",[new(0,50),new(1,60)]){Kind=ChartKind.Column,Secondary=true}]};
    var doc=Svg(spec);
    Check(Ticks(doc,"start").Contains("0")&&!Ticks(doc,"end").Contains("0"),"columns on the right did not bring zero to the right axis alone");
    // Each column is measured on its own axis: here the right one, from 0 at the foot of the plot to 100 at its top.
    var bars=Rects(Svg(spec with{Y2Min=0,Y2Max=100}),1);
    Check(Close(Attr(bars[0],"y"),344-.5*266)&&Close(Attr(bars[0],"height"),.5*266)&&Close(Attr(bars[1],"height"),.6*266),"a column on the right is not measured on the right-hand axis");
    var swapped=Svg(spec with{Series=[spec.Series[0] with{Kind=ChartKind.Area},spec.Series[1] with{Kind=null}]});
    Check(Ticks(swapped,"end").Contains("0")&&!Ticks(swapped,"start").Contains("0"),"an area on the left did not bring zero to the left axis alone");
    var scatter=Svg(Spec(ChartKind.Scatter) with{Series=[new("Dots",[new(0,100),new(1,120)]),new("Bars",[new(0,105),new(1,110)]){Kind=ChartKind.Column}]});
    Check(Ticks(scatter,"end").Contains("0"),"columns on a scatter chart float above zero");
});
Test("Bands are drawn first, then areas, columns, lines and points, each group in series order",()=>{
    var doc=Svg(Spec() with{Series=[
        new("Dots",[new(0,4),new(1,5)]){Kind=ChartKind.Scatter},
        new("Line one",[new(0,3),new(1,4)]),
        new("Bars",[new(0,2),new(1,3)]){Kind=ChartKind.Column},
        new("Fill",[new(0,1),new(1,2)]){Kind=ChartKind.Area},
        new("Line two",[new(0,6),new(1,7)]),
        new("Range",[ChartPoint.Interval(0,5,4,6),ChartPoint.Interval(1,6,5,7)]){Kind=ChartKind.Band}]});
    var drawn=doc.Descendants(ns+"g").Select(g=>(string?)g.Attribute("data-series")).OfType<string>().Distinct().ToArray();
    Check(drawn.SequenceEqual(["5","3","2","1","4","0"]),string.Join(",",drawn));
    // The band's fill is the first shape in the plot and the area's comes next, both before any column, and the columns before the first line.
    var shapes=doc.Descendants().Where(e=>e.Name==ns+"path"||e.Name==ns+"rect").ToArray();
    int First(Func<XElement,bool> match)=>Array.FindIndex(shapes,e=>match(e));
    int band=First(e=>(string?)e.Attribute("fill-opacity")==".16"),fill=First(e=>(string?)e.Attribute("fill-opacity")==".12"),
        column=First(e=>e.Name==ns+"rect"&&(string?)e.Parent!.Attribute("data-series")=="2"),stroke=First(e=>(string?)e.Attribute("stroke")==ChartStyle.Light.Series[1]&&(string?)e.Attribute("fill")=="none");
    Check(band==0&&band<fill&&fill<column&&column<stroke,$"band {band}, area {fill}, column {column}, line {stroke}");
});
Test("A projection dashes a line from the exact point where it reaches its X, and leaves the markers as they were",()=>{
    var plan=Classic(Spec() with{YMin=0,YMax=20,Series=[new("Plan",[new(0,10),new(4,20),new(8,0),new(10,10)]){ProjectedFrom=5}]});
    var unprojected=plan with{Series=[plan.Series[0] with{ProjectedFrom=null}]};
    // X runs 0 to 10 across 794 pixels and Y 0 to 20 across 266, so 4 to 8 reaches X 5 a quarter of the way along, at 15.
    double Px(double x)=>76+x*79.4; double Py(double y)=>344-y*13.3;
    var doc=Svg(plan);var pieces=Dashes(doc);
    Check(pieces.Select(p=>p.Dashed).SequenceEqual([false,true]),"the stroke is not one solid piece and one dashed");
    Matches(pieces[0].Points,(Px(0),Py(10)),(Px(4),Py(20)),(Px(5),Py(15)));
    Matches(pieces[1].Points,(Px(5),Py(15)),(Px(8),Py(0)),(Px(10),Py(10)));
    Check(doc.Descendants(ns+"path").Single(p=>p.Attribute("stroke-dasharray") is not null).Attribute("stroke-dasharray")!.Value=="6 4");
    // The markers stay where they were, and from the projection on each is named projected.
    Check(Points(doc).Select(c=>c.ToString()).SequenceEqual(Points(Svg(unprojected)).Select(c=>c.ToString())),"the markers moved");
    Check(Labels(doc).SequenceEqual(["Plan: 0, 10","Plan: 4, 20","Plan: 8, 0, projected","Plan: 10, 10, projected"]),string.Join(" | ",Labels(doc)));
    // On a point the stroke splits there; before the first everything is dashed; past the last nothing is, and the chart draws as without one.
    var at=Dashes(Svg(plan with{Series=[plan.Series[0] with{ProjectedFrom=4}]}));
    Matches(at[0].Points,(Px(0),Py(10)),(Px(4),Py(20)));Matches(at[1].Points,(Px(4),Py(20)),(Px(8),Py(0)),(Px(10),Py(10)));
    Check(Dashes(Svg(plan with{Series=[plan.Series[0] with{ProjectedFrom=-3}]})).Select(p=>p.Dashed).SequenceEqual([true]),"a projection before the data leaves a solid piece");
    Check(ChartSvg.Render(plan with{Series=[plan.Series[0] with{ProjectedFrom=11}]})==ChartSvg.Render(unprojected),"a projection past the data changed the chart");
    // On a logarithmic axis 10 lies half way between 1 and 100 on screen, so the split is half way along the drawn
    // segment, at 20, rather than at 11.8 where interpolating the data would put it.
    var curve=Dashes(Svg(Classic(Spec() with{XAxis=AxisKind.Log,YMin=0,YMax=40,Series=[new("Curve",[new(1,10),new(100,30)]){ProjectedFrom=10}]})));
    Matches(curve[0].Points,(76,344-10*6.65),(473,344-20*6.65));Matches(curve[1].Points,(473,344-20*6.65),(870,344-30*6.65));
    // An area keeps its fill whole.
    var area=plan with{Kind=ChartKind.Area};
    string Fill(ChartSpec spec)=>Svg(spec).Descendants(ns+"path").Single(p=>(string?)p.Attribute("fill-opacity")==".12").Attribute("d")!.Value;
    Check(Fill(area)==Fill(area with{Series=[area.Series[0] with{ProjectedFrom=null}]})&&Dashes(Svg(area)).Select(p=>p.Dashed).SequenceEqual([false,true]),"the area's fill moved or its stroke was not dashed");
    // With zones, the pieces past the projection are dashed in their own zone colours.
    var zoned=Dashes(Svg(Classic(Effortful(110,150) with{Series=[Effortful(110,150).Series[0] with{ProjectedFrom=.5}]})));
    Check(zoned.Select(p=>(p.Ink,p.Dashed)).SequenceEqual([(Ramp[0],false),(Ramp[1],false),(Ramp[1],true),(Ramp[2],true)]),string.Join(",",zoned.Select(p=>(p.Ink,p.Dashed))));
    Matches(zoned[1].Points,(76+794*.25,PY(120)),(473,PY(130)));Matches(zoned[2].Points,(473,PY(130)),(76+794*.75,PY(140)));
    // A category chart places categories by index, so a projection between two is interpolated between their centres.
    var weekly=Dashes(Svg(Classic(Spec(ChartKind.Column) with{Series=[new("Volume",[new(0,6),new(1,8),new(2,7),new(3,9)]),
        new("Plan",[new(0,6),new(1,7),new(2,7),new(3,8)]){Kind=ChartKind.Line,ProjectedFrom=1.5}]})));
    Check(weekly.Length==2&&Close(weekly[0].Points[^1].X,473)&&Close(weekly[1].Points[0].X,473),"the projection is not half way between the second and third categories");
});
Test("Lines, points and bands on a category chart mark the centre of each category, where its columns stand",()=>{
    var doc=Svg(Classic(Spec(ChartKind.Column) with{Series=[
        new("Volume",[new(0,6,"W1"),new(1,8,"W2"),new(2,7,"W3"),new(3,9,"W4")]),
        new("Average",[new(0,6,"W1"),new(1,7,"W2"),new(2,7,"W3"),new(3,7.5,"W4")]){Kind=ChartKind.Line},
        new("Last year",[new(0,5,"W1"),new(1,9,"W2"),new(2,8,"W3"),new(3,10,"W4")]),
        new("Races",[new(1,4,"W2"),new(3,3,"W4")]){Kind=ChartKind.Scatter},
        new("Range",[ChartPoint.Interval(0,6,5,7,"W1"),ChartPoint.Interval(1,7,6,8,"W2"),ChartPoint.Interval(2,7,6,8,"W3"),ChartPoint.Interval(3,8,7,9,"W4")]){Kind=ChartKind.Band}]}));
    // Four categories across 794 pixels: each is 198.5 wide and centred at 76 + 198.5 (i + 0.5).
    double Centre(double i)=>76+198.5*(i+.5);
    bool AtCentres(IEnumerable<double> xs,params double[] categories)=>xs.Count()==categories.Length&&xs.Zip(categories).All(p=>Close(p.First,Centre(p.Second)));
    double[] Cx(int series)=>doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("data-series")==series.ToString(CultureInfo.InvariantCulture)).Select(g=>Attr(g.Element(ns+"circle")!,"cx")).ToArray();
    Check(AtCentres(Cx(1),0,1,2,3)&&AtCentres(Dashes(doc).Single(p=>p.Ink==ChartStyle.Light.Series[1]).Points.Select(p=>p.X),0,1,2,3),"the line is off the centres");
    Check(AtCentres(Cx(3),1,3)&&AtCentres(Cx(4),0,1,2,3),"the points are off the centres");
    var edge=doc.Descendants(ns+"path").Single(p=>(string?)p.Attribute("fill-opacity")==".16").Attribute("d")!.Value.Split(' ').Where(c=>c!="Z").Select(c=>double.Parse(c[1..].Split(',')[0],CultureInfo.InvariantCulture));
    Check(AtCentres(edge.Distinct().Order(),0,1,2,3),"the band's edges are off the centres");
    // The two column series share each category's slot side by side, centred on it; the other marks take no share.
    XElement[] volume=Rects(doc,0),last=Rects(doc,2);
    for(var i=0;i<4;i++)
        Check(Close(Attr(volume[i],"width"),198.5*.72/2)&&Close(Spans(volume[i]).Right,Spans(last[i]).Left)&&Close((Spans(volume[i]).Left+Spans(last[i]).Right)/2,Centre(i)),$"category {i}'s columns are not side by side about its centre");
});
Test("Zones, point colours and trend lines follow the mark a series draws, not the chart's kind",()=>{
    // Columns on a line chart take their zones' colours and name their zones.
    var zoned=Svg(Spec() with{YMin=0,YMax=200,Series=[new("Heart rate",[new(0,110),new(2,150)]),new("Effort",[new(0,110),new(1,130),new(2,170)]){Kind=ChartKind.Column,Zones=Effort()}]});
    Check(Rects(zoned,1).Select(r=>(string?)r.Attribute("fill")).SequenceEqual([Ramp[0],Ramp[1],Ramp[3]]),"the columns ignore their zones");
    Check(Labels(zoned).Contains("Effort: 2, 170, Max"),"a column does not name its zone");
    // A line on a band chart splits its stroke at the bounds, as it would on a line chart; the band, drawn first, keeps its colour.
    var banded=Svg(Classic(Spec(ChartKind.Band) with{YMin=100,YMax=200,Series=[new("Range",[ChartPoint.Interval(0,130,120,140),ChartPoint.Interval(1,135,125,145)]),
        new("Heart rate",[new(0,110),new(1,150)]){Kind=ChartKind.Line,Zones=Effort()}]}));
    Check(Dashes(banded).Select(p=>p.Ink).SequenceEqual([ChartStyle.Light.Series[0],..Ramp[..3]]),string.Join(",",Dashes(banded).Select(p=>p.Ink)));
    // A point colour on that line is drawn; a band refuses one, and zones, wherever it is drawn.
    Check(ChartSvg.Render(Spec(ChartKind.Band) with{Series=[new("Range",[ChartPoint.Interval(0,1,0,2)]),new("Line",[new(0,1){Color="#123456"},new(1,2)]){Kind=ChartKind.Line}]}).Contains("stroke='#123456'"));
    Reject(()=>ChartSvg.Render(Spec() with{Series=[new("Range",[ChartPoint.Interval(0,1,0,2) with{Color="#123456"}]){Kind=ChartKind.Band}]}));
    Reject(()=>ChartSvg.Render(Spec() with{Series=[new("Range",[ChartPoint.Interval(0,1,0,2)]){Kind=ChartKind.Band,Zones=Effort()}]}));
    // A line on a column chart takes a trend, fitted through the category centres; a column on a line chart takes none.
    var trended=Svg(Spec(ChartKind.Column) with{Series=[new("Volume",[new(0,6),new(1,8),new(2,7)]),new("Average",[new(0,6),new(1,7),new(2,8)]){Kind=ChartKind.Line,Trend=true}]});
    var trend=trended.Descendants(ns+"path").Single(p=>(string?)p.Attribute("class")=="lumen-trend");
    var ends=trend.Attribute("d")!.Value.Split(' ').Select(c=>double.Parse(c[1..].Split(',')[1],CultureInfo.InvariantCulture)).ToArray();
    // 6, 7 and 8 lie on a line, so the fit passes the middle category's centre, the middle of the plot, at 7 on an axis from 0 to 8.
    Check(trend.Attribute("aria-label")!.Value=="Average trend: rising, R squared 1.00"&&Close((ends[0]+ends[1])/2,344-7/8d*266),"the trend is not fitted through the centres");
    Reject(()=>ChartSvg.Render(Spec() with{Series=[new("Bars",[new(0,1),new(1,2)]){Kind=ChartKind.Column,Trend=true}]}));
    Reject(()=>ChartSvg.Render(Spec() with{Series=[new("Range",[ChartPoint.Interval(0,1,0,2),ChartPoint.Interval(1,2,1,3)]){Kind=ChartKind.Band,Trend=true}]}));
    // A density scatter counts the points it aggregates, not a line drawn over them.
    var cloud=Cloud(400,20);
    Check(ChartSvg.Render(cloud with{Series=[..cloud.Series,new("Fit",[new(0,0),new(1,1)]){Kind=ChartKind.Line}]}).Contains($"{cloud.Series[0].Points.Count(p=>p.Y.HasValue)} observations aggregated"),"the density note counts the line");
});
Test("Marks beside others keep their own measure: a band's edges reach its axis, and bubbles are sized against bubbles alone",()=>{
    ChartSeries Range(bool secondary)=>new("Range",[ChartPoint.Interval(0,15,5,90),ChartPoint.Interval(1,16,6,95)]){Kind=ChartKind.Band,Secondary=secondary};
    var left=Svg(Spec() with{Series=[new("Level",[new(0,10),new(1,20)]),Range(false)]});
    var right=Svg(Spec() with{Series=[new("Level",[new(0,10),new(1,20)]),Range(true)]});
    Check(Ticks(left,"end").Contains("75")&&Ticks(right,"start").Contains("75")&&!Ticks(right,"end").Contains("75"),"a band's edges did not reach its own axis");
    // A scatter series on a bubble chart draws plain points, and its sizes do not shrink the bubbles.
    var bubbles=Spec(ChartKind.Bubble) with{Series=[new("Bubbles",[new(0,2,Size:10),new(1,3,Size:40)])]};
    var beside=bubbles with{Series=[..bubbles.Series,new("Dots",[new(0,1,Size:1000),new(1,4,Size:1000)]){Kind=ChartKind.Scatter}]};
    double[] Radii(ChartSpec spec,int series)=>Svg(spec).Descendants(ns+"g").Where(g=>(string?)g.Attribute("data-series")==series.ToString(CultureInfo.InvariantCulture)).Select(g=>Attr(g.Element(ns+"circle")!,"r")).ToArray();
    Check(Radii(beside,0).SequenceEqual(Radii(bubbles,0))&&Radii(bubbles,0)[1]==22,"the points' sizes shrank the bubbles");
    Check(Radii(beside,1).All(r=>r==4),"a scatter series on a bubble chart drew bubbles");
});
Test("CSV carries band edges whenever a series draws a band, and only then",()=>{
    string[] Rows(ChartSpec spec)=>ChartExport.Csv(spec).Split('\n').Select(r=>r.TrimEnd('\r')).Where(r=>r.Length>0).ToArray();
    var mixed=Rows(Spec() with{Series=[new("Level",[new(0,5),new(1,6)]),new("Range",[ChartPoint.Interval(0,5,4,6),ChartPoint.Interval(1,6,5,7)]){Kind=ChartKind.Band}]});
    Check(mixed.SequenceEqual(["Series,X,Y,Label,Size,Low,High","\"Level\",0,5,\"\",1,,","\"Level\",1,6,\"\",1,,","\"Range\",0,5,\"\",1,4,6","\"Range\",1,6,\"\",1,5,7"]),string.Join(" | ",mixed));
    var category=Rows(Spec(ChartKind.Column) with{Series=[new("Volume",[new(0,6)]),new("Range",[ChartPoint.Interval(0,6,5,7)]){Kind=ChartKind.Band}]});
    Check(category[0]=="Series,X,Y,Label,Size,Low,High"&&category[1]=="\"Volume\",0,6,\"\",1,,"&&category[2]=="\"Range\",0,6,\"\",1,5,7",string.Join(" | ",category));
    // A band chart whose series all draw as something else has no band to carry; one that overrides nothing keeps its edges.
    Check(Rows(Bands() with{Series=[Bands().Series[0] with{Kind=ChartKind.Line}]})[0]=="Series,X,Y,Label,Size");
    Check(Rows(Bands())[0]=="Series,X,Y,Label,Size,Low,High"&&Rows(Bands())[1]=="\"Forecast\",0,10,\"\",1,8,12");
    Check(Rows(Candles())[0]=="Series,X,Y,Label,Size,Open,High,Low,Close"&&Rows(Spec())[0]=="Series,X,Y,Label,Size");
});
Test("Series kinds and projections are refused where they cannot draw, each with its reason",()=>{
    string Refusal(ChartSpec spec){try{ChartSvg.Render(spec);}catch(ArgumentException error){return error.Message;}throw new Exception("a chart was accepted that should not be");}
    // Six kinds lay out X by value or by category and take a series' own kind; the rest draw every series one way.
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        var lined=Sample(kind) with{Series=Sample(kind).Series.Select(s=>s with{Kind=ChartKind.Line}).ToArray()};
        if(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Column or ChartKind.Range) ChartSvg.Render(lined);
        // A blocks chart's points end in XEnd, which a line refuses; a line beside the blocks is drawn.
        else if(kind==ChartKind.Blocks) Check(Refusal(lined).Contains("XEnd ends a span or a block")&&ChartSvg.Render(Sample(kind) with{Series=[..Sample(kind).Series,Spec().Series[0] with{Kind=ChartKind.Line}]}).Length>0,$"{kind}: {Refusal(lined)}");
        else Check(Refusal(lined).Contains(kind==ChartKind.Strip?"kind of its own":"own kind"),$"{kind}: {Refusal(lined)}");
    }
    // A series can be a line, area, column, scatter, band, range or blocks, and nothing else; a range series is drawn from its
    // bounds, and blocks from their spans.
    foreach(var mark in Enum.GetValues<ChartKind>().Append((ChartKind)99))
    {
        var spec=Spec() with{Series=[new("S",mark==ChartKind.Range?[ChartPoint.Interval(0,1,0,2),ChartPoint.Interval(1,2,1,3)]:mark==ChartKind.Blocks?[ChartPoint.Block(0,1,1),ChartPoint.Block(1,2,2)]:[new(0,1),new(1,2)]){Kind=mark}]};
        if(mark is ChartKind.Line or ChartKind.Area or ChartKind.Column or ChartKind.Scatter or ChartKind.Band or ChartKind.Range or ChartKind.Blocks) ChartSvg.Render(spec);
        else Check(Refusal(spec).Contains("can be drawn as"),$"{mark}: {Refusal(spec)}");
    }
    // Columns and areas draw from zero, on whichever axis measures them; the other axis stays free.
    var left=Spec() with{Series=[new("Level",[new(0,10),new(1,20)]),new("Load",[new(0,5),new(1,8)]){Kind=ChartKind.Column}]};
    var right=Spec() with{Series=[new("Level",[new(0,10),new(1,20)]),new("Form",[new(0,5),new(1,8)]){Kind=ChartKind.Area,Secondary=true}]};
    Check(Refusal(left with{YAxis=AxisKind.Log}).Contains("logarithmic")&&Refusal(right with{Y2Axis=AxisKind.Log}).Contains("logarithmic"));
    Check(Refusal(left with{YReversed=true}).Contains("reversed")&&Refusal(right with{Y2Reversed=true}).Contains("reversed"));
    Check(Refusal(left with{YMin=1}).Contains("zero baseline")&&Refusal(right with{Y2Max=-1}).Contains("zero baseline")&&Refusal(left with{YMax=-1}).Contains("zero baseline"));
    ChartSvg.Render(right with{YAxis=AxisKind.Log,YReversed=true,YMin=5});
    ChartSvg.Render(left with{Y2Reversed=true,Y2Min=3,Series=[..left.Series,new("Rate",[new(0,4),new(1,6)]){Secondary=true}]});
    // A projection dashes a stroke, so only lines and areas take one, and it must start somewhere on the X axis.
    foreach(var mark in (ChartKind[])[ChartKind.Column,ChartKind.Scatter,ChartKind.Band])
        Check(Refusal(Spec() with{Series=[new("S",[new(0,1),new(1,2)]){Kind=mark,ProjectedFrom=.5}]}).Contains("lines or areas"),$"{mark}");
    foreach(var kind in (ChartKind[])[ChartKind.Scatter,ChartKind.Bubble,ChartKind.Column,ChartKind.Bar,ChartKind.StackedColumn,ChartKind.Band,ChartKind.Candlestick])
        Check(Refusal(Sample(kind) with{Series=[Sample(kind).Series[0] with{ProjectedFrom=0}]}).Contains("lines or areas"),$"{kind}");
    foreach(var start in (double[])[double.NaN,double.PositiveInfinity,1e101])
        Check(Refusal(Spec() with{Series=[Spec().Series[0] with{ProjectedFrom=start}]}).Contains("finite"),$"{start}");
    Check(Refusal(Spec() with{XAxis=AxisKind.Log,Series=[new("S",[new(1,1),new(2,2)]){ProjectedFrom=0}]}).Contains("positive projection"));
    Check(Refusal(TimeSpec(Utc(2026,1,1),3_600_000,5) with{Series=[TimeSpec(Utc(2026,1,1),3_600_000,5).Series[0] with{ProjectedFrom=1e18}]}).Contains("year 9999"));
    // Two columns at one X would stand in one place.
    Check(Refusal(Spec() with{Series=[new("S",[new(0,1),new(0,2)]){Kind=ChartKind.Column}]}).Contains("unique X"));
    // Trends, zones, point colours, ordering and band bounds follow the mark.
    Check(Refusal(Spec() with{Series=[new("S",[new(0,1),new(1,2)]){Kind=ChartKind.Column,Trend=true}]}).Contains("trend line"));
    Check(Refusal(Spec() with{Series=[new("S",[new(0,1),new(1,2)]){Kind=ChartKind.Band,Zones=Effort()}]}).Contains("zones"));
    Check(Refusal(Spec() with{Series=[new("S",[new(0,1){Color="#123456"},new(1,2)]){Kind=ChartKind.Band}]}).Contains("Point colours"));
    Check(Refusal(Spec(ChartKind.Scatter) with{Series=[new("S",[new(1,1),new(0,2)]){Kind=ChartKind.Line}]}).Contains("ordered"));
    ChartSvg.Render(Spec() with{Series=[Spec().Series[0],new("S",[new(1,1),new(0,2)]){Kind=ChartKind.Scatter}]});
    Check(Refusal(Spec() with{Series=[new("S",[new(0,1){Low=2}]){Kind=ChartKind.Band}]}).Contains("both Low and High"));
    Check(Refusal(Spec() with{Y2Axis=AxisKind.Log,Series=[new("L",[new(0,1)]),new("S",[ChartPoint.Interval(0,1,-1,2)]){Kind=ChartKind.Band,Secondary=true}]}).Contains("positive band bounds"));
});
Test("A series that names its chart's own kind draws exactly as one that names none",()=>{
    foreach(var kind in (ChartKind[])[ChartKind.Line,ChartKind.Area,ChartKind.Scatter,ChartKind.Band,ChartKind.Column])
    {
        var named=Sample(kind) with{Series=Sample(kind).Series.Select(s=>s with{Kind=kind}).ToArray()};
        Check(ChartSvg.Render(named)==ChartSvg.Render(Sample(kind))&&ChartExport.Csv(named)==ChartExport.Csv(Sample(kind)),$"{kind}");
    }
    var paired=Paired() with{Series=Paired().Series.Select(s=>s with{Kind=ChartKind.Line}).ToArray()};
    Check(ChartSvg.Render(paired)==ChartSvg.Render(Paired()),"a secondary series that names its kind moved");
    Check(new ChartSeries("S",[]).Kind is null&&new ChartSeries("S",[]).ProjectedFrom is null);
});
Test("A performance management chart from the load model: fitness and fatigue over daily stress, form on the right, two planned weeks projected",()=>{
    var start=new DateOnly(2026,6,1);
    double When(DateOnly day)=>TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero));
    // Six weeks done and two planned, each a rest day, two hard days and easy ones between.
    var load=Training.Load(Enumerable.Range(0,56).Select(i=>(start.AddDays(i),(double)((i%7) switch{0=>0,2=>120,5=>150,_=>60}))),40,40);
    var planned=When(start.AddDays(42));
    var doc=Svg(Classic(new ChartSpec{Kind=ChartKind.Line,XAxis=AxisKind.Time,YLabel="Training stress",Y2Label="Form",Series=[
        ChartSeries.From("Fitness",load,d=>When(d.Day),d=>d.Fitness) with{ProjectedFrom=planned},
        ChartSeries.From("Fatigue",load,d=>When(d.Day),d=>d.Fatigue) with{ProjectedFrom=planned},
        ChartSeries.From("Form",load,d=>When(d.Day),d=>d.Form) with{Kind=ChartKind.Area,Secondary=true,ProjectedFrom=planned},
        ChartSeries.From("Daily stress",load,d=>When(d.Day),d=>d.Stress) with{Kind=ChartKind.Column}]}));
    // With the right-hand axis the plot runs from 76 to 824, so the 56 days are 748/55 pixels apart.
    double Px(int day)=>76+day*748/55d;
    var bars=Rects(doc,3);
    Check(bars.Length==56,$"{bars.Length} columns");
    for(var i=0;i<56;i++)
        Check(Close(Attr(bars[i],"width"),748/55d*.7)&&Close(Attr(bars[i],"x")+Attr(bars[i],"width")/2,Px(i)),$"day {i}'s column is not centred on its day");
    // Each column is its day's stress from zero on the left axis: one baseline, and height over stress one scale for every day.
    var scale=bars.Select((b,i)=>Attr(b,"height")/Math.Max(load[i].Stress,1)).Where((_,i)=>load[i].Stress>0).ToArray();
    Check(scale.All(s=>Math.Abs(s-scale[0])<1e-6)&&bars.Where((_,i)=>load[i].Stress==0).All(b=>Attr(b,"height")==0),"a column is not its day's stress");
    Check(bars.All(b=>Close(Attr(b,"y")+Attr(b,"height"),Attr(bars[0],"y")+Attr(bars[0],"height"))),"the columns do not share one baseline");
    // Form is an area from zero on the right axis, behind the columns, which stand behind the lines.
    Check(Ticks(doc,"start").Contains("0")&&Ticks(doc,"end").Contains("0"),"an axis leaves out zero");
    var order=doc.Descendants(ns+"g").Select(g=>(string?)g.Attribute("data-series")).OfType<string>().Distinct().ToArray();
    Check(order.SequenceEqual(["2","3","0","1"]),string.Join(",",order));
    // Fitness, fatigue and form are dashed from the first planned day to the last.
    foreach(var series in new[]{0,1,2})
    {
        var pieces=Dashes(doc).Where(p=>p.Ink==ChartStyle.Light.Series[series]).ToArray();
        Check(pieces.Select(p=>p.Dashed).SequenceEqual([false,true])&&Close(pieces[0].Points[^1].X,Px(42))&&Close(pieces[1].Points[0].X,Px(42))&&Close(pieces[1].Points[^1].X,Px(55)),
            $"series {series} is not dashed from the first planned day");
    }
    var labels=Labels(doc);
    Check(labels.Contains($"Fitness: 13 Jul 2026, {LinearScale.Label(load[42].Fitness)}, projected")&&labels.Contains($"Fitness: 12 Jul 2026, {LinearScale.Label(load[41].Fitness)}")
        &&labels.Contains($"Form: 13 Jul 2026, {LinearScale.Label(load[42].Form)}, projected")&&labels.Contains("Daily stress: 1 Jun 2026, 0")&&labels.Contains("Daily stress: 3 Jun 2026, 120"),"the marks are misnamed");
});
Test("The component's data table and status line read each series of a mixed chart in its own mark and units",()=>{
    double June(int day)=>Utc(2026,6,day);
    var spec=Spec() with{XAxis=AxisKind.Time,Y2Format=ValueFormat.Duration,Series=[
        new("Stress",[new(June(1),80),new(June(2),0),new(June(3),120),new(June(4),60)]){Kind=ChartKind.Column},
        new("Fitness",[new(June(1),40.5),new(June(2),39.6),new(June(3),41.2),new(June(4),41.6)]){ProjectedFrom=June(3)},
        new("Ride time",[new(June(1),3600),new(June(2),0),new(June(3),5400),new(June(4),2700)]){Kind=ChartKind.Area,Secondary=true},
        new("Target",[ChartPoint.Interval(June(1),40,30,50),ChartPoint.Interval(June(2),41,31,51),ChartPoint.Interval(June(3),42,32,52),ChartPoint.Interval(June(4),43,33,53)]){Kind=ChartKind.Band}]};
    var html=Operate(Classic(spec),async chart=>{
        typeof(LumenChart).GetField("showData",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(chart,true);
        await chart.SelectPoint(0,2);
    });
    Check(html.Contains("<tr><td>Stress</td><td>3 Jun 2026</td><td>120</td></tr>")&&html.Contains("<tr><td>Ride time</td><td>3 Jun 2026</td><td>1:30:00</td></tr>")
        &&html.Contains("<tr><td>Target</td><td>2 Jun 2026</td><td>41 (31 to 51)</td></tr>"),"the table does not read each series in its own units");
    Check(html.Contains("Stress: 3 Jun 2026 = 120"),"the status line does not read the column");
    Check(html.Contains("Fitness: 3 Jun 2026, 41.2, projected")&&html.Contains("stroke-dasharray='6 4'"),"the component lost the projection");
});
Test("Series kinds and projections survive JSON, and a request that names neither keeps the defaults",()=>{
    var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    var spec=Spec() with{Y2Label="Form",Series=[new("Fitness",[new(0,40),new(1,42),new(2,45)]){ProjectedFrom=1.5},
        new("Stress",[new(0,80),new(1,0),new(2,120)]){Kind=ChartKind.Column},new("Form",[new(0,-5),new(1,3),new(2,-1)]){Kind=ChartKind.Area,Secondary=true}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,options);
    Check(json.Contains("\"kind\":\"Column\"")&&json.Contains("\"kind\":\"Area\"")&&json.Contains("\"projectedFrom\":1.5"),json);
    Check(ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,options)!)==ChartSvg.Render(spec),"the marks changed in transit");
    var written="{\"kind\":\"Line\",\"style\":{\"finish\":\"Classic\"},\"series\":[{\"name\":\"Fitness\",\"projectedFrom\":1,\"points\":[{\"x\":0,\"y\":40},{\"x\":1,\"y\":42},{\"x\":2,\"y\":45}]},"+
        "{\"name\":\"Stress\",\"kind\":\"Column\",\"points\":[{\"x\":0,\"y\":80},{\"x\":1,\"y\":0},{\"x\":2,\"y\":120}]}]}";
    var read=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,options)!;
    var svg=ChartSvg.Render(read);
    Check(read.Series[1].Kind==ChartKind.Column&&read.Series[0].ProjectedFrom==1&&svg.Contains("stroke-dasharray='6 4'")&&svg.Contains("aria-label='Stress: 2, 120'><title>Stress: 2, 120</title><rect"),"hand-written JSON lost its marks");
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]}]}",options)!;
    Check(old.Series[0].Kind is null&&old.Series[0].ProjectedFrom is null);
});
// 0.22.0: panes. Each pane clips its marks in a nested SVG six pixels wider than it all round; the height between y 78 and
// Height - 76 is shared out by weight, the main plot weighing 1, after a gap of 24 pixels between each two panes.
XElement[] PaneClips(XDocument doc)=>doc.Root!.Elements(ns+"svg").ToArray();
(double Top,double Bottom) PaneSpan(XElement clip)=>(Attr(clip,"y")+6,Attr(clip,"y")+Attr(clip,"height")-6);
ChartSpec Stacked()=>Spec() with{Height=600,YLabel="Top",Panes=[new(){Label="Middle",Weight=.5},new(){Label="Bottom",Weight=1.5}],Series=[
    new("Top",[new(0,100),new(5,200),new(10,150)]),new("Middle",[new(0,1),new(5,5),new(10,3)]){Pane=1},new("Bottom",[new(0,-20),new(5,20),new(10,0)]){Pane=2}]};
double[] CxOf(XDocument doc,int series)=>doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("data-series")==series.ToString(CultureInfo.InvariantCulture)&&g.Element(ns+"circle") is not null)
    .Select(g=>Attr(g.Element(ns+"circle")!,"cx")).ToArray();
Test("Panes share the plot by weight, the main one weighing 1, a fixed gap apart and without overlapping",()=>{
    var clips=PaneClips(Svg(Stacked()));
    var spans=clips.Select(PaneSpan).ToArray();
    // 600 - 76 - 78 leaves 446 pixels; less two gaps of 24 that is 398, shared 1 : 0.5 : 1.5.
    Check(spans.Length==3,$"{spans.Length} panes");
    double[] heights=[398/3d,398/6d,398/2d];
    for(var i=0;i<3;i++) Check(Close(spans[i].Bottom-spans[i].Top,heights[i]),$"pane {i} is {spans[i].Bottom-spans[i].Top} high");
    Check(Close(spans[0].Top,78)&&Close(spans[2].Bottom,524)&&Close(spans[1].Top-spans[0].Bottom,24)&&Close(spans[2].Top-spans[1].Bottom,24),"the panes do not fill the plot a gap apart");
    Check(clips.Zip(clips.Skip(1)).All(p=>Attr(p.First,"y")+Attr(p.First,"height")<Attr(p.Second,"y")),"two clips overlap");
    // Weights are relative, so doubling every one, the main plot's included, would change nothing; doubling the panes' alone shrinks the main plot.
    var heavier=PaneClips(Svg(Stacked() with{Panes=Stacked().Panes.Select(p=>p with{Weight=p.Weight*2}).ToArray()})).Select(PaneSpan).ToArray();
    Check(Close(heavier[0].Bottom-heavier[0].Top,398/5d)&&Close(heavier[2].Bottom-heavier[2].Top,398*3/5d),"weights are not relative to the main plot's 1");
    Check(new ChartPane().Weight==.5&&PaneClips(Svg(Spec())).Select(PaneSpan).SequenceEqual([(78d,344d)]),"the defaults moved");
});
Test("Every pane places X through one mapping, the main plot's, on a trading axis and at any zoom",()=>{
    var friday=Utc(2026,3,6);
    var days=Enumerable.Range(0,12).Select(i=>friday+i*86_400_000d).Where(x=>TimeAxis.Moment(x).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToArray();
    ChartSeries Over(string name,int pane,double scale)=>new(name,days.Select((x,i)=>new ChartPoint(x,(i+1)*scale)).ToArray()){Pane=pane};
    var paned=Spec() with{XAxis=AxisKind.Time,SkipWeekends=true,Height=560,Panes=[new(){YReversed=true},new(){YAxis=AxisKind.Log}],Series=[Over("A",0,1),Over("B",1,100),Over("C",2,.01)]};
    foreach(var (from,to) in new (double?,double?)[]{(null,null),(days[2],days[5])})
    {
        var doc=Svg(paned with{XMin=from,XMax=to});
        var alone=CxOf(Svg(Spec() with{XAxis=AxisKind.Time,SkipWeekends=true,XMin=from,XMax=to,Series=[Over("A",0,1)]}),0);
        Check(CxOf(doc,0).Length==days.Length&&CxOf(doc,1).SequenceEqual(CxOf(doc,0))&&CxOf(doc,2).SequenceEqual(CxOf(doc,0))&&CxOf(doc,0).SequenceEqual(alone),$"the panes place X differently from {from} to {to}");
    }
    Check(!CxOf(Svg(paned),2).SequenceEqual(CxOf(Svg(paned with{XMin=days[2],XMax=days[5]}),2)),"zooming moved nothing");
});
Test("The X axis is labelled and titled once, under the bottom pane",()=>{
    var spec=Stacked() with{XLabel="Elapsed",XFormat=ValueFormat.Duration};
    XElement[] Below(XDocument doc)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="middle"&&t.Attribute("transform") is null&&(string?)t.Attribute("class")=="lumen-muted").ToArray();
    var doc=Svg(spec);
    var bottom=PaneSpan(PaneClips(doc)[2]).Bottom;
    var alone=Below(Svg(spec with{Panes=[],Series=[spec.Series[0]]})).Select(t=>t.Value).ToArray();
    Check(alone.Length>2&&Below(doc).Select(t=>t.Value).SequenceEqual(alone),"the panes label X differently from one plot");
    Check(Below(doc).All(t=>Close(Attr(t,"y"),bottom+(t.Value=="Elapsed"?44:21)))&&Below(doc).Count(t=>t.Value=="Elapsed")==1,"an X label is not under the bottom pane, or is drawn twice");
});
Test("Each pane's Y axes measure its own series and no other, and are titled beside it",()=>{
    var spec=Classic(Stacked() with{Panes=[Stacked().Panes[0] with{Y2Label="Rate"},Stacked().Panes[1]],Series=[..Stacked().Series,new("Rate",[new(0,.2),new(10,.4)]){Pane=1,Secondary=true}]});
    var doc=Svg(spec);
    var spans=PaneClips(doc).Select(PaneSpan).ToArray();
    string[] Within(string anchor,int pane)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")==anchor&&(string?)t.Attribute("class")=="lumen-muted"&&t.Attribute("transform") is null
        &&Attr(t,"y")-4>=spans[pane].Top-1e-6&&Attr(t,"y")-4<=spans[pane].Bottom+1e-6).Select(t=>t.Value).ToArray();
    // A series alone in one plot draws the ticks its pane should.
    string[] Alone(ChartSeries series)=>Ticks(Svg(Classic(Spec() with{Series=[series with{Pane=0,Secondary=false}]})),"end");
    for(var k=0;k<3;k++) Check(Within("end",k).SequenceEqual(Alone(spec.Series[k])),$"pane {k} reads {string.Join(",",Within("end",k))}");
    Check(Within("start",1).SequenceEqual(Alone(spec.Series[3]))&&Within("start",0).Length==0&&Within("start",2).Length==0,"the right-hand ticks are not pane 1's alone");
    // Titles stand at the middle of their own pane, the right-hand one beside the pane that has the axis.
    string[] Rotated(string title)=>((string)doc.Descendants(ns+"text").Single(t=>t.Value==title&&t.Attribute("transform") is not null).Attribute("transform")!).TrimEnd(')').Split(' ');
    for(var k=0;k<3;k++)
    {
        var turn=Rotated(new[]{"Top","Middle","Bottom"}[k]);
        Check(turn[0]=="rotate(-90"&&turn[1]=="20"&&Close(double.Parse(turn[2],CultureInfo.InvariantCulture),(spans[k].Top+spans[k].Bottom)/2),$"pane {k}'s title is not beside it");
    }
    var right=Rotated("Rate");
    Check(right[0]=="rotate(90"&&right[1]=="884"&&Close(double.Parse(right[2],CultureInfo.InvariantCulture),(spans[1].Top+spans[1].Bottom)/2),"the right-hand title is not beside its pane");
});
Test("Each pane clips its own marks",()=>{
    var spec=Stacked() with{Panes=[Stacked().Panes[0] with{YMax=4},Stacked().Panes[1]]};
    var clips=PaneClips(Svg(spec));
    for(var k=0;k<3;k++)
    {
        var series=clips[k].Descendants(ns+"g").Select(g=>(string?)g.Attribute("data-series")).OfType<string>().Distinct().ToArray();
        Check(series.SequenceEqual([k.ToString(CultureInfo.InvariantCulture)]),$"pane {k} holds series {string.Join(",",series)}");
        var span=PaneSpan(clips[k]);
        var box=((string)clips[k].Attribute("viewBox")!).Split(' ').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray();
        Check(Attr(clips[k],"x")==70&&Attr(clips[k],"width")==806&&(string?)clips[k].Attribute("overflow")=="hidden"
            &&box.SequenceEqual([70,Attr(clips[k],"y"),806,Attr(clips[k],"height")])&&Close(span.Bottom-span.Top,new[]{398/3d,398/6d,398/2d}[k]),$"pane {k} is clipped elsewhere");
    }
    // Pane 1 stops at 4, so its 5 is drawn above the pane, where only that pane's clip can hide it.
    Check(clips[1].Descendants(ns+"circle").Min(c=>Attr(c,"cy"))<PaneSpan(clips[1]).Top-6,"the value past the pane's maximum is not beyond its clip");
    // Columns in two panes each take the whole slot at their X, 34 pixels at most, rather than sharing it side by side.
    var columns=Svg(Stacked() with{Series=[Stacked().Series[0] with{Kind=ChartKind.Column},Stacked().Series[1] with{Kind=ChartKind.Column},Stacked().Series[2]]});
    foreach(var k in new[]{0,1})
        Check(Rects(columns,k).Zip(new[]{0d,5,10}).All(p=>Close(Attr(p.First,"width"),34)&&Close(Attr(p.First,"x")+17,76+p.Second/10*794)),$"pane {k}'s columns share a slot with another pane's");
});
Test("An X annotation runs through every pane and is named once; Y annotations and the main zones stay in the main plot, and a pane's own zones shade it",()=>{
    var spec=Classic(Stacked() with{YZones=new([new("Low",150),new("High",double.PositiveInfinity)]),Panes=[Stacked().Panes[0] with{YZones=new([new("Calm",2),new("Busy",double.PositiveInfinity)])},Stacked().Panes[1]],
        Annotations=[new(AnnotationAxis.X,2){To=4,Label="Window"},new(AnnotationAxis.Y,120){Label="Target"},new(AnnotationAxis.X,7){Label="Event"}]});
    var clips=PaneClips(Svg(spec));
    string[] Named(XElement clip)=>clip.Elements(ns+"g").Where(g=>(string?)g.Attribute("role")=="img").Select(g=>(string)g.Attribute("aria-label")!).ToArray();
    Check(Named(clips[0]).SequenceEqual(["Low: up to 150","High: above 150","Window: 2 to 4","Target: 120","Event: 7"]),string.Join(" | ",Named(clips[0])));
    Check(Named(clips[1]).SequenceEqual(["Calm: up to 2","Busy: above 2"])&&Named(clips[2]).Length==0,string.Join(" | ",Named(clips[1]).Concat(Named(clips[2]))));
    var muted=ChartStyle.Light.Muted;
    for(var k=0;k<3;k++)
    {
        var span=PaneSpan(clips[k]);
        var window=clips[k].Descendants(ns+"rect").Single(r=>(string?)r.Attribute("fill")==muted);
        Check(Close(Attr(window,"x"),76+2/10d*794)&&Close(Attr(window,"width"),2/10d*794)&&Close(Attr(window,"y"),span.Top)&&Close(Attr(window,"height"),span.Bottom-span.Top),$"the window does not span pane {k}");
        var lines=clips[k].Descendants(ns+"line").Where(l=>(string?)l.Attribute("stroke")==muted&&(string?)l.Attribute("stroke-width")=="1.5").ToArray();
        // The event stands upright through every pane; the target lies across the main plot alone.
        Check(lines.Count(l=>Close(Attr(l,"x1"),76+7/10d*794)&&Close(Attr(l,"x2"),76+7/10d*794)&&Close(Attr(l,"y1"),span.Top)&&Close(Attr(l,"y2"),span.Bottom))==1,$"the event does not stand through pane {k}");
        Check(lines.Count(l=>Attr(l,"y1")==Attr(l,"y2"))==(k==0?1:0),$"pane {k} has the wrong Y references");
        Check(clips[k].Descendants(ns+"text").Count()==Named(clips[k]).Length,$"pane {k} labels a reference it does not name");
    }
});
Test("A candlestick chart takes a moving average from Statistics.Rolling over its candles and volume as columns in a pane beneath",()=>{
    var monday=Utc(2026,3,2);
    var sessions=Enumerable.Range(0,28).Select(i=>monday+i*86_400_000d).Where(x=>TimeAxis.Moment(x).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToArray();
    var candles=sessions.Select((x,i)=>ChartPoint.Candle(x,50+i,53+i,48+i,i%3==0?49+i:52+i)).ToArray();
    var average=Statistics.Rolling(candles.Select(p=>p.Close).ToArray(),5);
    // The average comes first, so the candles are not series 0, and take their own index.
    var spec=Classic(new ChartSpec{Kind=ChartKind.Candlestick,XAxis=AxisKind.Time,SkipWeekends=true,Height=520,Panes=[new(){Label="Volume",YFormat=ValueFormat.Compact}],Series=[
        new("Five-day average",candles.Select((p,i)=>new ChartPoint(p.X,average[i]?.Mean)).ToArray()){Kind=ChartKind.Line},
        new("ACME",candles),
        new("Volume",candles.Select((p,i)=>new ChartPoint(p.X,1_000_000+i*50_000)).ToArray()){Kind=ChartKind.Column,Pane=1}]});
    var doc=Svg(spec);
    var clips=PaneClips(doc);
    // 520 - 154 leaves 366 pixels, less one gap 342, shared 1 : 0.5: the prices from 78 to 306 and the volume from 330 to 444.
    Check(clips.Select(PaneSpan).Zip(new[]{(78d,306d),(330d,444d)}).All(p=>Close(p.First.Top,p.Second.Item1)&&Close(p.First.Bottom,p.Second.Item2)),"the panes are not where their weights put them");
    // Prices and the average share the main plot's axis, from the lowest low, 48, to the highest high, 72.
    double Price(double value)=>306-(value-48)/24*228;
    var bars=clips[0].Elements(ns+"g").Where(g=>(string?)g.Attribute("data-series")=="1").ToArray();
    Check(sessions.Length==20&&bars.Length==20&&bars.Select((g,i)=>Attr(g.Element(ns+"line")!,"y1")-Price(candles[i].High!.Value)).All(d=>Math.Abs(d)<1e-6),"the candles are not on the main axis, or lost their index");
    var means=clips[0].Elements(ns+"g").Where(g=>(string?)g.Attribute("data-series")=="0").ToArray();
    Check(means.Length==16&&means.Select((g,i)=>(Cy:Attr(g.Element(ns+"circle")!,"cy"),Mean:average[i+4]!.Mean)).All(m=>Math.Abs(m.Cy-Price(m.Mean))<1e-6),"the average is not measured with the candles");
    Check(means.Zip(bars.Skip(4)).All(p=>Close(Attr(p.First.Element(ns+"circle")!,"cx"),Attr(p.Second.Element(ns+"line")!,"x1"))),"the average does not stand over its candles");
    // Only the candles' and a band's lows and highs reach the axis; points beside them are measured by their values alone.
    var spread=Svg(spec with{Series=[..spec.Series,new("Range",candles.Select(p=>ChartPoint.Interval(p.X,p.Close,0,500)).ToArray()){Kind=ChartKind.Scatter}]});
    Check(PaneClips(spread)[0].Elements(ns+"g").Where(g=>(string?)g.Attribute("data-series")=="1").Select((g,i)=>Attr(g.Element(ns+"line")!,"y1")-Price(candles[i].High!.Value)).All(d=>Math.Abs(d)<1e-6),"a scatter series' bounds widened the price axis");
    // The candles are drawn before the average, so it lies over them.
    var order=clips[0].Elements().ToList();
    Check(order.IndexOf(bars[^1])<order.FindIndex(e=>e.Name==ns+"path"&&(string?)e.Attribute("stroke-width")=="2.5"),"the average is drawn behind the candles");
    // Volume rises from zero at the foot of its own pane, one scale for every session, and reads in its pane's format.
    var volume=Rects(doc,2);
    Check(volume.Length==20&&volume.All(r=>Close(Attr(r,"y")+Attr(r,"height"),444))&&volume.Select((r,i)=>Attr(r,"height")/(1_000_000+i*50_000)).All(h=>Math.Abs(h-114/1_950_000d)<1e-9),"the volume is not drawn from zero on its own axis");
    Check(clips[1].Descendants(ns+"rect").Count()==20&&Labels(doc).Contains("Volume: 2 Mar 2026, 1M")&&Labels(doc).Contains($"Five-day average: 6 Mar 2026, {LinearScale.Label(average[4]!.Mean)}"),"the volume is misplaced or misnamed");
    // Exactly one series is the candles; the others draw as lines, areas, columns, points or bands.
    string Refusal(ChartSpec chart){try{ChartSvg.Render(chart);}catch(ArgumentException error){return error.Message;}throw new Exception("a chart was accepted that should not be");}
    Check(Refusal(spec with{Series=[..spec.Series,new("Second",candles)]}).Contains("exactly one series as candles"));
    Check(Refusal(spec with{Series=[spec.Series[0],spec.Series[2]]}).Contains("exactly one series as candles"));
    foreach(var mark in (ChartKind[])[ChartKind.Bubble,ChartKind.Candlestick,ChartKind.Ohlc,ChartKind.Bar])
        Check(Refusal(spec with{Series=[spec.Series[0] with{Kind=mark},spec.Series[1]]}).Contains("can be drawn as"),$"{mark}");
    Check(Refusal(spec with{Series=[spec.Series[0] with{Secondary=true},spec.Series[1]]}).Contains("secondary axis applies"));
    foreach(var mark in (ChartKind[])[ChartKind.Area,ChartKind.Scatter,ChartKind.Band,ChartKind.Column])
        ChartSvg.Render(spec with{Kind=ChartKind.Ohlc,Series=[spec.Series[1],new("Companion",candles.Select(p=>ChartPoint.Interval(p.X,p.Close,p.Low!.Value,p.High!.Value)).ToArray()){Kind=mark,Pane=1}]});
    // A logarithmic or reversed price axis leaves the volume pane's own axis free to draw from zero, but refuses columns of its own.
    ChartSvg.Render(spec with{YAxis=AxisKind.Log,YReversed=true});
    Check(Refusal(spec with{YAxis=AxisKind.Log,Series=[..spec.Series.Take(2),spec.Series[2] with{Pane=0}],Panes=[]}).Contains("logarithmic"));
    Check(Refusal(spec with{Panes=[spec.Panes[0] with{YAxis=AxisKind.Log}]}).Contains("logarithmic"));
    // Everything per series follows the mark: the average takes a trend, a projection and point colours, which the candles refuse.
    Check(ChartSvg.Render(spec with{Series=[spec.Series[0] with{Trend=true,ProjectedFrom=sessions[15]},..spec.Series.Skip(1)]}).Contains("stroke-dasharray='6 4'"));
    Check(Refusal(spec with{Series=[spec.Series[0],spec.Series[1] with{Points=[candles[0] with{Color="#123456"}]},spec.Series[2]]}).Contains("Point colours"));
    // Hidden from the component, the candles stay without their points and the average and volume are drawn alone.
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var hidden="";
    Operate(spec,chart=>{typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[1]);hidden=(string)typeof(LumenChart).GetField("svg",flags)!.GetValue(chart)!;return Task.CompletedTask;});
    var shown=XDocument.Parse(hidden);
    Check(PaneClips(shown).Length==2&&!hidden.Contains(": open ")&&Labels(shown).Count(l=>l.StartsWith("Five-day average"))==16&&Labels(shown).Count(l=>l.StartsWith("Volume"))==20,"hiding the candles lost their companions");
});
Test("A series below the main plot is coloured, dashed, fitted and named on its own pane's axes",()=>{
    var spec=Classic(Spec() with{Height=560,YMin=0,YMax=1000,XFormat=ValueFormat.Duration,Panes=[new(){YFormat=ValueFormat.Duration,YReversed=true,YMin=240,YMax=360}],Series=[
        new("Power",[new(0,200),new(600,300),new(1200,250)]),
        new("Pace",[new(0,330),new(600,300),new(1200,270)]){Pane=1,Trend=true,Zones=new([new("Fast",280),new("Steady",320),new("Easy",double.PositiveInfinity)])},
        new("Plan",[new(0,320),new(1200,260)]){Pane=1,ProjectedFrom=600}]});
    var doc=Svg(spec);
    // 560 - 154 leaves 406, less one gap 382, shared 1 : 0.5; pane 1 runs from 356.67 to 484 with 240 at its top.
    var span=PaneSpan(PaneClips(doc)[1]);
    Check(Close(span.Top,78+382/1.5+24)&&Close(span.Bottom,484));
    double Px(double x)=>76+x/1200*794; double Py(double v)=>span.Top+(v-240)/120*(span.Bottom-span.Top);
    var pace=Dashes(doc).Where(p=>!p.Dashed).Where(p=>p.Ink!=ChartStyle.Light.Series[0]&&p.Ink!=ChartStyle.Light.Series[2]).ToArray();
    Check(pace.Select(p=>p.Ink).SequenceEqual([Ramp[2],Ramp[1],Ramp[0]]),string.Join(",",pace.Select(p=>p.Ink)));
    Matches(pace[0].Points,(Px(0),Py(330)),(Px(200),Py(320)));
    Matches(pace[1].Points,(Px(200),Py(320)),(Px(600),Py(300)),(Px(1000),Py(280)));
    Matches(pace[2].Points,(Px(1000),Py(280)),(Px(1200),Py(270)));
    var plan=Dashes(doc).Where(p=>p.Ink==ChartStyle.Light.Series[2]).ToArray();
    Check(plan.Select(p=>p.Dashed).SequenceEqual([false,true]));
    Matches(plan[0].Points,(Px(0),Py(320)),(Px(600),Py(290)));Matches(plan[1].Points,(Px(600),Py(290)),(Px(1200),Py(260)));
    // The pace falls in a straight line, so its trend runs through every point on the pane's reversed axis, and reads falling.
    var trend=doc.Descendants(ns+"path").Single(p=>(string?)p.Attribute("class")=="lumen-trend");
    var ends=trend.Attribute("d")!.Value.Split(' ').Select(c=>c[1..].Split(',').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray()).ToArray();
    Check(Close(ends[0][1],Py(330))&&Close(ends[1][1],Py(270))&&trend.Attribute("aria-label")!.Value=="Pace trend: falling, R squared 1.00","the trend is not fitted on the pane's axis");
    Check(Labels(doc).Contains("Pace: 10:00, 5:00, Steady")&&Labels(doc).Contains("Plan: 20:00, 4:20, projected")&&Labels(doc).Contains("Power: 10:00, 300"),string.Join(" | ",Labels(doc)));
});
Test("Panes are refused where they cannot be drawn, each with its reason",()=>{
    string Refusal(ChartSpec spec){try{ChartSvg.Render(spec);}catch(ArgumentException error){return error.Message;}throw new Exception("a chart was accepted that should not be");}
    // Only the kinds that lay X out continuously take panes, whether named by a pane or by a series.
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        var below=Sample(kind) with{Panes=[new()],Series=[..Sample(kind).Series,Sample(kind).Series[0] with{Name="Below",Pane=1,Kind=kind is ChartKind.Candlestick or ChartKind.Ohlc?ChartKind.Line:null}]};
        var pointed=Sample(kind) with{Series=[Sample(kind).Series[0] with{Pane=1}]};
        if(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Range or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Blocks)
        {
            ChartSvg.Render(below);
            Check(Refusal(pointed).Contains("pane is 0"),$"{kind}: {Refusal(pointed)}");
        }
        // A timeline's lanes are its one plot, and a calendar's days its one grid.
        else if(kind==ChartKind.Strip)
            Check(Refusal(below).Contains("one whole")&&Refusal(pointed).Contains("no panes")&&Refusal(Sample(kind) with{Panes=[new()]}).Contains("no panes"),$"{kind}: {Refusal(below)}");
        else if(kind is ChartKind.Timeline or ChartKind.Calendar)
            Check(Refusal(below).Contains("no panes")&&Refusal(pointed).Contains("no panes")&&Refusal(Sample(kind) with{Panes=[new()]}).Contains("no panes"),$"{kind}: {Refusal(below)}");
        else Check(Refusal(below).Contains("Panes share")&&Refusal(pointed).Contains("Panes share")&&Refusal(Sample(kind) with{Panes=[new()]}).Contains("Panes share"),$"{kind}: {Refusal(below)}");
    }
    var spec=Stacked();
    Check(Refusal(spec with{Series=[..spec.Series,new("Lost",[new(0,1)]){Pane=3}]}).Contains("pane is 0"));
    Check(Refusal(spec with{Series=[..spec.Series,new("Lost",[new(0,1)]){Pane=-1}]}).Contains("pane is 0"));
    Check(Refusal(spec with{Series=[spec.Series[0],spec.Series[2]]}).Contains("pane 1 has none"));
    Check(Refusal(spec with{Series=spec.Series.Skip(1).ToArray()}).Contains("pane 0 has none"));
    // From 0.37.0 a chart takes six plots, the main one and five panes, and refuses a seventh.
    Check(Refusal(spec with{Panes=[new(),new(),new(),new(),new(),new()]}).Contains("at most six plots")&&Refusal(spec with{Panes=null!}).Contains("at most six plots"));
    ChartSvg.Render(spec with{Panes=[..spec.Panes,new()],Series=[..spec.Series,new("Fourth",[new(0,1)]){Pane=3}]});
    ChartSvg.Render(spec with{Panes=[..spec.Panes,new(),new(),new()],Series=[..spec.Series,new("Fourth",[new(0,1)]){Pane=3},new("Fifth",[new(0,1)]){Pane=4},new("Sixth",[new(0,1)]){Pane=5}]});
    foreach(var weight in new[]{0,-1,double.NaN,double.PositiveInfinity,1e101})
        Check(Refusal(spec with{Panes=[spec.Panes[0] with{Weight=weight},spec.Panes[1]]}).Contains("weight"),$"{weight}");
    Check(Refusal(spec with{Panes=[spec.Panes[0],null!]}).Contains("cannot be null"));
    // A pane's settings meet the rules the main plot's do.
    ChartPane Middle(Func<ChartPane,ChartPane> change)=>change(spec.Panes[0]);
    Check(Refusal(spec with{Panes=[Middle(p=>p with{YAxis=AxisKind.Time}),spec.Panes[1]]}).Contains("time axes"));
    Check(Refusal(spec with{Panes=[Middle(p=>p with{Y2Axis=(AxisKind)7}),spec.Panes[1]]}).Contains("time axes"));
    Check(Refusal(spec with{Panes=[Middle(p=>p with{YFormat=(ValueFormat)7}),spec.Panes[1]]}).Contains("Unknown value format"));
    Check(Refusal(spec with{Panes=[Middle(p=>p with{YMin=5,YMax=5}),spec.Panes[1]]}).Contains("Axis bounds"));
    Check(Refusal(spec with{Panes=[Middle(p=>p with{YAxis=AxisKind.Log,YMin=-1}),spec.Panes[1]]}).Contains("Log Y bounds"));
    Check(Refusal(spec with{Panes=[Middle(p=>p with{Y2Axis=AxisKind.Log,Y2Max=0}),spec.Panes[1]]}).Contains("Log secondary bounds"));
    Check(Refusal(spec with{IncludeZero=true,Panes=[Middle(p=>p with{YAxis=AxisKind.Log}),spec.Panes[1]]}).Contains("cannot include zero"));
    Check(Refusal(spec with{Panes=[Middle(p=>p with{YZones=new(Enumerable.Range(0,8).Select(i=>new Zone($"Z{i}",i==7?double.PositiveInfinity:i)).ToArray())}),spec.Panes[1]]}).Contains("zone ramp"));
    Check(Refusal(spec with{Panes=[Middle(p=>p with{Label=new string('x',2001)}),spec.Panes[1]]}).Contains("2000 characters"));
    Check(Refusal(spec with{Kind=ChartKind.Area,Panes=[Middle(p=>p with{YAxis=AxisKind.Log}),spec.Panes[1]]}).Contains("Log Y axes require"));
    Check(Refusal(spec with{Kind=ChartKind.Area,Panes=[Middle(p=>p with{YReversed=true}),spec.Panes[1]]}).Contains("reversed Y axis"));
    Check(Refusal(spec with{Kind=ChartKind.Area,Panes=[Middle(p=>p with{YMin=1}),spec.Panes[1]]}).Contains("zero baseline"));
    Check(Refusal(Sample(ChartKind.Candlestick) with{Panes=[new(){Y2Axis=AxisKind.Log}],Series=[..Sample(ChartKind.Candlestick).Series,new("Below",[new(0,1)]){Pane=1,Kind=ChartKind.Line}]}).Contains("logarithmic secondary axis"));
    // Each series meets its own pane's axes: a pane of secondary series alone has nothing on its left, and a log pane refuses zero.
    Check(Refusal(spec with{Series=[spec.Series[0],spec.Series[1] with{Secondary=true},spec.Series[2]]}).Contains("on the left of its pane"));
    // A null series beside a secondary one is refused, where counting the left-hand series once dereferenced it.
    Check(Refusal(Spec() with{Series=[new("S",[new(0,1)]){Secondary=true},null!]}).Contains("on the left"));
    Check(Refusal(spec with{Panes=[Middle(p=>p with{YAxis=AxisKind.Log}),spec.Panes[1]],Series=[spec.Series[0],spec.Series[1] with{Points=[new(0,0)]},spec.Series[2]]}).Contains("positive values"));
    ChartSvg.Render(spec with{YAxis=AxisKind.Log,Series=[spec.Series[0],spec.Series[1],spec.Series[2] with{Kind=ChartKind.Area}]});
    Check(Refusal(spec with{Panes=[spec.Panes[0],spec.Panes[1] with{YAxis=AxisKind.Log}],Series=[spec.Series[0],spec.Series[1],spec.Series[2] with{Points=[new(0,1),new(1,2)],Kind=ChartKind.Column}]}).Contains("logarithmic"));
    Check(Refusal(spec with{Panes=[spec.Panes[0],spec.Panes[1] with{YReversed=true}],Series=[spec.Series[0],spec.Series[1],spec.Series[2] with{Kind=ChartKind.Area}]}).Contains("reversed"));
    Check(Refusal(spec with{Panes=[spec.Panes[0],spec.Panes[1] with{YMin=-30,YMax=-1}],Series=[spec.Series[0],spec.Series[1],spec.Series[2] with{Kind=ChartKind.Column,Points=[new(0,-20),new(5,-5)]}]}).Contains("zero baseline"));
    Check(Refusal(spec with{Panes=[spec.Panes[0],spec.Panes[1] with{Y2Axis=AxisKind.Log}],Series=[..spec.Series,new("Edge",[ChartPoint.Interval(0,1,-1,2)]){Pane=2,Secondary=true,Kind=ChartKind.Band}]}).Contains("positive band bounds"));
});
Test("A chart that sets no panes draws in one plot, as before",()=>{
    Check(new ChartSpec().Panes.Count==0&&new ChartSeries("S",[]).Pane==0);
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        var sample=Sample(kind);
        var doc=Svg(sample);
        if(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Candlestick or ChartKind.Band or ChartKind.Ohlc or ChartKind.Range or ChartKind.Blocks)
            Check(PaneClips(doc).Select(PaneSpan).SequenceEqual([(78d,344d)])&&doc.Descendants(ns+"g").Where(g=>g.Attribute("data-series") is not null).All(g=>(string?)g.Attribute("data-series")=="0"),$"{kind} is not one plot");
        // A timeline's lanes share its one plot.
        else if(kind==ChartKind.Timeline) Check(PaneClips(doc).Select(PaneSpan).SequenceEqual([(78d,344d)]),$"{kind} is not one plot");
        else Check(PaneClips(doc).Length==0,$"{kind} drew a plot");
    }
});
Test("Panes survive JSON, and a request that names none keeps one plot",()=>{
    var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    var spec=Stacked() with{Panes=[Stacked().Panes[0] with{YFormat=ValueFormat.Compact,YReversed=true,YMin=0,YMax=6,Y2Label="Rate",Y2Axis=AxisKind.Log,Y2Format=ValueFormat.Duration,YZones=Effort()},Stacked().Panes[1]],
        Series=[..Stacked().Series,new("Rate",[new(0,60),new(10,3600)]){Pane=1,Secondary=true}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,options);
    Check(json.Contains("\"panes\":[{\"label\":\"Middle\",\"weight\":0.5,\"yAxis\":\"Linear\"")&&json.Contains("\"y2Axis\":\"Log\"")&&json.Contains("\"pane\":2"),json);
    Check(ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,options)!)==ChartSvg.Render(spec),"the panes changed in transit");
    var written="{\"kind\":\"Candlestick\",\"panes\":[{\"label\":\"Volume\",\"yFormat\":\"Compact\"}],\"series\":[{\"name\":\"ACME\",\"points\":[{\"x\":0,\"open\":10,\"high\":12,\"low\":9,\"close\":11},{\"x\":1,\"open\":11,\"high\":13,\"low\":10,\"close\":10.4}]},"+
        "{\"name\":\"Volume\",\"kind\":\"Column\",\"pane\":1,\"points\":[{\"x\":0,\"y\":1500000},{\"x\":1,\"y\":2100000}]}]}";
    var read=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,options)!;
    var svg=Svg(read);
    Check(read.Panes[0].Weight==.5&&read.Series[1].Pane==1&&PaneClips(svg).Length==2&&Labels(svg).Contains("Volume: 1, 2.1M")&&Labels(svg).Contains("0: open 10, high 12, low 9, close 11"),"hand-written JSON lost its pane");
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]}]}",options)!;
    Check(old.Panes.Count==0&&old.Series[0].Pane==0);
});
Test("The component reads a pane's series in its own formats, closes a pane its hidden series leave empty, and zooms every pane together",()=>{
    var spec=Stacked() with{XFormat=ValueFormat.Duration,Panes=[Stacked().Panes[0] with{YFormat=ValueFormat.Duration},Stacked().Panes[1] with{YFormat=ValueFormat.Compact}],
        Series=[Stacked().Series[0],Stacked().Series[1] with{Points=[new(0,61),new(5,125),new(10,3600)]},Stacked().Series[2] with{Points=[new(0,1500),new(5,-2500),new(10,0)]}]};
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    string Drawn(LumenChart chart)=>(string)typeof(LumenChart).GetField("svg",flags)!.GetValue(chart)!;
    void Call(LumenChart chart,string method,params object[] arguments)=>typeof(LumenChart).GetMethod(method,flags)!.Invoke(chart,arguments);
    string before="",zoomed="",closed="",promoted="";
    var html=Operate(spec,async chart=>{
        typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);
        await chart.SelectPoint(1,2);
        before=Drawn(chart);
        Call(chart,"Zoom",.5);zoomed=Drawn(chart);Call(chart,"ResetView");
        Call(chart,"Toggle",1);closed=Drawn(chart);
        Call(chart,"Toggle",0);promoted=Drawn(chart);
    });
    Check(html.Contains("<tr><td>Middle</td><td>0:05</td><td>2:05</td></tr>")&&html.Contains("<tr><td>Bottom</td><td>0:05</td><td>-2.5k</td></tr>")&&html.Contains("<tr><td>Top</td><td>0:05</td><td>200</td></tr>"),"the table does not read each pane in its own format");
    Check(html.Contains("Middle: 0:10 = 1:00:00"),"the status line does not read the pane's format");
    // Zooming moves the three panes as one: the same X stands at one place in each, and it moved.
    var (a,b)=(XDocument.Parse(before),XDocument.Parse(zoomed));
    Check(Enumerable.Range(1,2).All(k=>CxOf(b,k).SequenceEqual(CxOf(b,0)))&&!CxOf(b,0).SequenceEqual(CxOf(a,0))&&Close(CxOf(b,0)[1],473),"the panes did not zoom together");
    // Hiding the middle pane's only series closes it, and the bottom pane moves up with its own settings.
    var shut=XDocument.Parse(closed);
    Check(PaneClips(shut).Length==2&&Labels(shut).Contains("Bottom: 0:05, -2.5k")&&PaneClips(shut)[1].Descendants(ns+"g").Any(g=>(string?)g.Attribute("data-series")=="1"),"the emptied pane did not close");
    // Hiding the main plot's series too lets the bottom pane take its place, with its settings and without the main plot's references.
    var main=XDocument.Parse(promoted);
    Check(PaneClips(main).Select(PaneSpan).SequenceEqual([(78d,524d)])&&Labels(main).SequenceEqual(["Bottom: 0:00, 1.5k","Bottom: 0:05, -2.5k","Bottom: 0:10, 0"])&&Ticks(main,"end").Contains("-2k"),"the bottom pane did not take the main plot's place");
});
// 0.23.0: the finish. The plot runs from x 76 to 870 and y 344 to 78, so with Y fixed at 0 to 40 a value v sits at FY(v).
double FY(double value)=>344-value/40*266;
(char Op,double[] Args)[] Commands(string d)=>System.Text.RegularExpressions.Regex.Matches(d,"([MLCAZ])([^MLCAZ]*)")
    .Select(m=>(m.Groups[1].Value[0],m.Groups[2].Value.Split([' ',','],StringSplitOptions.RemoveEmptyEntries).Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray())).ToArray();
(double X0,double Y0,double X1,double Y1,double X2,double Y2,double X3,double Y3)[] Cubics(string d)
{
    var result=new List<(double,double,double,double,double,double,double,double)>();double x=0,y=0;
    foreach(var (op,a) in Commands(d))
    {
        if(op=='C')result.Add((x,y,a[0],a[1],a[2],a[3],a[4],a[5]));
        if(op is 'M' or 'L' or 'C'){x=a[^2];y=a[^1];}
    }
    return result.ToArray();
}
double Bezier(double a,double b,double c,double d,double t)=>(1-t)*(1-t)*(1-t)*a+3*(1-t)*(1-t)*t*b+3*(1-t)*t*t*c+t*t*t*d;
XElement StrokeOf(XDocument doc)=>doc.Descendants(ns+"path").Single(p=>(string?)p.Attribute("fill")=="none"&&p.Attribute("class") is null);
XElement[] MarksOf(XDocument doc,int series)=>doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("data-series")==series.ToString(CultureInfo.InvariantCulture)&&g.Attribute("data-point") is not null)
    .Select(g=>g.Elements().Last()).ToArray();
XElement[] GridStrokes(XDocument doc)=>doc.Descendants(ns+"line").Where(l=>((string?)l.Attribute("class"))?.StartsWith("lumen-grid")==true).ToArray();
(char Op,double[] Args)[] BarOf(XDocument doc,int series,int point)=>Commands(doc.Descendants(ns+"g")
    .Single(g=>(string?)g.Attribute("data-series")==series.ToString(CultureInfo.InvariantCulture)&&(string?)g.Attribute("data-point")==point.ToString(CultureInfo.InvariantCulture)).Element(ns+"path")!.Attribute("d")!.Value);
void Draws((char Op,double[] Args)[] actual,params (char Op,double[] Args)[] expected)
{
    Check(actual.Length==expected.Length,$"{actual.Length} commands where {expected.Length} were expected");
    for(var i=0;i<actual.Length;i++)
        Check(actual[i].Op==expected[i].Op&&actual[i].Args.Length==expected[i].Args.Length&&actual[i].Args.Zip(expected[i].Args).All(p=>Math.Abs(p.First-p.Second)<1e-6),
            $"{actual[i].Op}{string.Join(",",actual[i].Args)} is not {expected[i].Op}{string.Join(",",expected[i].Args)}");
}
(char,double[]) M(double x,double y)=>('M',[x,y]);
(char,double[]) L(double x,double y)=>('L',[x,y]);
(char,double[]) A(double r,double x,double y)=>('A',[r,r,0,0,1,x,y]);
(char,double[]) Z()=>('Z',[]);
var finishJson=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
Test("Without the finish a chart draws exactly as before: 2.5 px strokes, 2 px corners, solid gridlines, labels on the left and no IDs",()=>{
    foreach(var kind in Enum.GetValues<ChartKind>())
        foreach(var theme in Enum.GetValues<ChartTheme>())
        {
            var sample=Classic(Sample(kind) with{Theme=theme});
            var svg=ChartSvg.Render(sample);
            Check(!svg.Contains(" id=")&&!svg.Contains("<defs")&&!svg.Contains("url("),$"{kind} {theme} carries an ID");
            // Every new property set to its default is the same chart.
            var spelled=sample with{YAxisSide=AxisSide.Left,YTickLabels=TickLabels.All,Style=ChartSvg.ResolveStyle(sample) with{Gridlines=GridLine.Solid,BarRadius=null},
                Series=sample.Series.Select(s=>s with{Curve=LineCurve.Linear,Fill=AreaFill.Flat,Markers=MarkerStyle.Auto,StrokeWidth=null,Gradient=null,HighlightLast=false,ValueLabels=false}).ToArray()};
            Check(ChartSvg.Render(spelled)==svg,$"{kind} {theme} changes when its defaults are spelled out");
        }
    var line=Svg(Classic(Spec() with{MinorGridlines=true}));
    Check((string?)StrokeOf(line).Attribute("stroke-width")=="2.5"&&MarksOf(line,0).All(c=>(string?)c.Attribute("r")=="4"&&c.Attribute("fill-opacity") is null));
    Check(GridStrokes(line).Length>0&&GridStrokes(line).All(l=>l.Attribute("stroke-dasharray") is null),"a default gridline is not solid");
    Check(Ticks(line,"end").Length>0&&Ticks(line,"start").Length==0&&Attr(PaneClips(line).Single(),"x")==70);
    Check(Svg(Classic(Spec(ChartKind.Column))).Descendants(ns+"rect").Where(r=>r.Parent!.Attribute("data-point") is not null).All(r=>(string?)r.Attribute("rx")=="2"),"a default column lost its corners");
    Check(!Svg(Classic(Spec(ChartKind.Column))).Descendants(ns+"text").Any(t=>t.Attribute("aria-hidden") is not null),"a default column wrote its value");
});
Test("A smooth curve passes through every point and never leaves the range of the two points either side of it",()=>{
    double[] values=[0,10,10,0,5,100,0,0,50,49,51,3,3,3,80];
    double[] xs=[0,1,2,3,3.2,4,7,8,8.1,9,12,13,14,15,16];
    foreach(var reversed in new[]{false,true})
    {
        var doc=Svg(Spec() with{YReversed=reversed,Series=[new("S",values.Select((v,i)=>new ChartPoint(xs[i],v)).ToArray()){Curve=LineCurve.Smooth}]});
        var cubics=Cubics(StrokeOf(doc).Attribute("d")!.Value);
        var marks=MarksOf(doc,0);
        Check(cubics.Length==values.Length-1,$"{cubics.Length} curves");
        for(var i=0;i<cubics.Length;i++)
        {
            var c=cubics[i];
            Check(Close(c.X0,Attr(marks[i],"cx"))&&Close(c.Y0,Attr(marks[i],"cy"))&&Close(c.X3,Attr(marks[i+1],"cx"))&&Close(c.Y3,Attr(marks[i+1],"cy")),$"curve {i} does not run between its points");
            double low=Math.Min(c.Y0,c.Y3)-1e-9,high=Math.Max(c.Y0,c.Y3)+1e-9;
            for(var t=0d;t<=1;t+=1/512d)
            {
                var y=Bezier(c.Y0,c.Y1,c.Y2,c.Y3,t);
                Check(y>=low&&y<=high,$"curve {i} reaches {y}, outside {low} to {high}");
                // X moves evenly with t, so the curve is a function of X and cannot double back.
                Check(Close(Bezier(c.X0,c.X1,c.X2,c.X3,t),c.X0+t*(c.X3-c.X0)),$"curve {i} doubles back");
            }
        }
        // Two equal values in a row stay level between them, and a peak is level at its top: the curve invents neither.
        Check(Close(cubics[1].Y1,cubics[1].Y0)&&Close(cubics[1].Y2,cubics[1].Y3),"a level stretch bows");
        Check(Close(cubics[4].Y2,cubics[4].Y3)&&Close(cubics[5].Y1,cubics[5].Y0),"the peak at 100 is not level");
    }
    // Two points at one X are joined straight, and the curve carries on either side.
    var stepped=StrokeOf(Svg(Spec() with{Series=[new("S",[new(0,1),new(1,4),new(1,2),new(2,3),new(3,1)]){Curve=LineCurve.Smooth}]})).Attribute("d")!.Value;
    Check(Commands(stepped).Select(c=>c.Op).SequenceEqual(['M','C','L','C','C']),stepped);
    // Two points alone are joined by a straight line.
    var pair=Cubics(StrokeOf(Svg(Spec() with{Series=[new("S",[new(0,1),new(1,4)]){Curve=LineCurve.Smooth}]})).Attribute("d")!.Value).Single();
    Check(Close((pair.Y1-pair.Y0)/(pair.X1-pair.X0),(pair.Y3-pair.Y0)/(pair.X3-pair.X0))&&Close((pair.Y2-pair.Y0)/(pair.X2-pair.X0),(pair.Y3-pair.Y0)/(pair.X3-pair.X0)));
});
Test("A step line holds each value until the next point and rises or falls there, its area and zone colours with it",()=>{
    double X(double x)=>76+x*794/3;
    var spec=Classic(Spec() with{YMin=0,YMax=40,Series=[new("S",[new(0,10),new(1,20),new(2,20),new(3,5)]){Curve=LineCurve.Step}]});
    var doc=Svg(spec);
    Matches(Strokes(doc).Single().Points,(X(0),FY(10)),(X(1),FY(10)),(X(1),FY(20)),(X(2),FY(20)),(X(3),FY(20)),(X(3),FY(5)));
    // Each point sits where its hold begins.
    Check(MarksOf(doc,0).Select(c=>(Attr(c,"cx"),Attr(c,"cy"))).Zip(new[]{(X(0),FY(10)),(X(1),FY(20)),(X(2),FY(20)),(X(3),FY(5))}).All(p=>Close(p.First.Item1,p.Second.Item1)&&Close(p.First.Item2,p.Second.Item2)));
    var area=Svg(spec with{Kind=ChartKind.Area});
    var fill=area.Descendants(ns+"path").Single(p=>(string?)p.Attribute("fill-opacity")==".12");
    Check(fill.Attribute("d")!.Value==$"{StrokeOf(area).Attribute("d")!.Value} L870,344 L76,344 Z","the area does not follow the steps");
    // The rise at X(1) crosses 15 at X(1), and the fall at X(3) crosses it at X(3).
    var zoned=Strokes(Svg(spec with{Series=[spec.Series[0] with{Zones=new([new("Low",15),new("High",double.PositiveInfinity)])}]}));
    Check(zoned.Select(s=>s.Ink).SequenceEqual([Ramp[0],Ramp[1],Ramp[0]]),string.Join(",",zoned.Select(s=>s.Ink)));
    Matches(zoned[0].Points,(X(0),FY(10)),(X(1),FY(10)),(X(1),FY(15)));
    Matches(zoned[1].Points,(X(1),FY(15)),(X(1),FY(20)),(X(2),FY(20)),(X(3),FY(20)),(X(3),FY(15)));
    Matches(zoned[2].Points,(X(3),FY(15)),(X(3),FY(5)));
    // A projection splits a hold where it reaches its X.
    var dashed=Dashes(Svg(spec with{Series=[spec.Series[0] with{ProjectedFrom=1.5}]}));
    Check(dashed.Length==2&&!dashed[0].Dashed&&dashed[1].Dashed);
    Matches(dashed[1].Points,(X(1.5),FY(20)),(X(2),FY(20)),(X(3),FY(20)),(X(3),FY(5)));
    // A missing value breaks the steps as it breaks a line.
    Check(Svg(spec with{Series=[new("S",[new(0,1),new(1,2),new(2,null),new(3,2),new(4,3)]){Curve=LineCurve.Step}]}).Descendants(ns+"path").Count(p=>(string?)p.Attribute("fill")=="none")==2);
});
Test("A smooth zone-coloured line splits on the curve where it crosses each bound, and a projection splits it at its X",()=>{
    double[] values=[110,150,125,165,130,105];
    var plain=Classic(Effortful(values) with{Series=[Effortful(values).Series[0] with{Zones=null,Curve=LineCurve.Smooth}]});
    var cubics=Cubics(StrokeOf(Svg(plain)).Attribute("d")!.Value);
    double CurveAt(double x){var c=cubics.First(k=>x>=k.X0-1e-9&&x<=k.X3+1e-9);return Bezier(c.Y0,c.Y1,c.Y2,c.Y3,(x-c.X0)/(c.X3-c.X0));}
    var zoned=Strokes(Svg(Classic(Effortful(values) with{Series=[Effortful(values).Series[0] with{Curve=LineCurve.Smooth}]})));
    double[] bounds=[PY(120),PY(140),PY(160)];
    Check(zoned.Length==9,$"{zoned.Length} pieces");
    foreach(var (piece,next) in zoned.Zip(zoned.Skip(1)))
    {
        var split=piece.Points[^1];
        Check(Close(split.X,next.Points[0].X)&&Close(split.Y,next.Points[0].Y),"two pieces do not meet");
        Check(bounds.Any(b=>Close(split.Y,b)),$"a split at {split.Y} is not on a bound");
        Check(Math.Abs(CurveAt(split.X)-split.Y)<.25,$"the split at ({split.X}, {split.Y}) is {Math.Abs(CurveAt(split.X)-split.Y)} px off the curve");
    }
    // Every vertex lies on the curve, so the coloured stroke draws the curve the plain one does, each piece in its zone's colour.
    Check(zoned.SelectMany(p=>p.Points).All(p=>Math.Abs(CurveAt(p.X)-p.Y)<.25),"a piece leaves the curve");
    foreach(var piece in zoned)
    {
        var middle=(piece.Points[0].Y+piece.Points[1].Y)/2;
        Check(piece.Ink==Ramp[Effort().IndexOf(100+(344-middle)/266*100)],$"a piece in the wrong colour: {piece.Ink}");
    }
    var projected=Dashes(Svg(plain with{Series=[plain.Series[0] with{ProjectedFrom=2.5}]}));
    Check(projected.Length==2&&!projected[0].Dashed&&projected[1].Dashed);
    var at=projected[1].Points[0];
    Check(Close(at.X,76+2.5*794/5)&&Close(projected[0].Points[^1].X,at.X)&&Math.Abs(CurveAt(at.X)-at.Y)<.25,"the projection does not split the curve at its X");
    Check(projected.SelectMany(p=>p.Points).All(p=>Math.Abs(CurveAt(p.X)-p.Y)<.25),"the projected stroke leaves the curve");
    // A missing value breaks the curve, as it breaks a line.
    var gap=Svg(Spec() with{Series=[new("S",[new(0,1),new(1,3),new(2,null),new(3,2),new(4,5),new(5,4)]){Curve=LineCurve.Smooth}]});
    Check(gap.Descendants(ns+"path").Count(p=>(string?)p.Attribute("fill")=="none")==2&&MarksOf(gap,0).Length==5);
});
Test("A gradient stop lands at the exact height of its value on linear, logarithmic and reversed axes",()=>{
    void Lands(ChartSpec spec,int series)
    {
        var doc=Svg(spec);
        var gradient=doc.Descendants(ns+"linearGradient").Single();
        Check((string?)gradient.Attribute("gradientUnits")=="userSpaceOnUse"&&Attr(gradient,"x1")==0&&Attr(gradient,"x2")==0,"the gradient is not laid out up the plot");
        double y1=Attr(gradient,"y1"),y2=Attr(gradient,"y2");
        var stops=gradient.Elements(ns+"stop").ToArray();
        var own=spec.Series[series];
        Check(stops.Length==own.Gradient!.Count);
        var marks=MarksOf(doc,series);
        for(var i=0;i<stops.Length;i++)
        {
            var y=y1+Attr(stops[i],"offset")*(y2-y1);
            var at=Array.FindIndex(own.Points.ToArray(),p=>p.Y==own.Gradient[i].Value);
            Check(Math.Abs(y-Attr(marks[at],"cy"))<1e-4,$"stop {i} lands at {y}, its value at {Attr(marks[at],"cy")}");
            Check((string?)stops[i].Attribute("stop-color")==own.Gradient[i].Color);
        }
        var id=(string)gradient.Attribute("id")!;
        Check(doc.Descendants(ns+"path").Count(p=>(string?)p.Attribute("stroke")==$"url(#{id})")==1,"the stroke does not paint with the gradient");
        Check(marks.All(m=>(string?)m.Attribute("fill")==$"url(#{id})"),"the markers do not paint with the gradient");
    }
    ColorStop[] Three(double a,double b,double c)=>[new(a,"#2E9B58"),new(b,"#A88200"),new(c,"#DD4B45")];
    Lands(Spec() with{Series=[new("S",[new(0,100),new(1,140),new(2,130),new(3,180)]){Gradient=Three(100,130,180)}]},0);
    Lands(Spec() with{YAxis=AxisKind.Log,Series=[new("S",[new(0,1),new(1,31.6),new(2,1000),new(3,12)]){Gradient=Three(1,31.6,1000)}]},0);
    Lands(Spec() with{YReversed=true,YFormat=ValueFormat.Duration,Series=[new("Pace",[new(0,330),new(1,300),new(2,270),new(3,290)]){Gradient=Three(270,300,330)}]},0);
    // A secondary series is measured on its own axis, logarithmic here and reversed.
    Lands(Spec() with{Y2Axis=AxisKind.Log,Y2Reversed=true,Series=[new("Left",[new(0,0),new(1,1000)]),new("Right",[new(0,1),new(1,300),new(2,20)]){Secondary=true,Gradient=Three(1,20,300)}]},1);
    // Stops beyond the data still sit at their values' heights, and the end colours carry on past them.
    var beyond=Svg(Spec() with{YMin=0,YMax=40,Series=[new("S",[new(0,10),new(1,30)]){Gradient=[new(-10,"#2E9B58"),new(60,"#DD4B45")]}]}).Descendants(ns+"linearGradient").Single();
    Check(Close(Attr(beyond,"y1"),FY(-10))&&Close(Attr(beyond,"y2"),FY(60))&&beyond.Attribute("spreadMethod") is null);
    // Labels gain nothing: the value is already in each one.
    Check(Labels(Svg(Spec() with{Series=[new("S",[new(0,100),new(1,140)]){Gradient=Three(100,120,140)}]})).SequenceEqual(["S: 0, 100","S: 1, 140"]));
});
Test("A chart names its gradients after a hash of its spec: stable, unique within the chart and different between charts",()=>{
    ChartSpec Faded(double last)=>Spec(ChartKind.Area) with{Series=[new("S",[new(0,2),new(1,5),new(2,last)]){Fill=AreaFill.Fade,Curve=LineCurve.Smooth},
        new("T",[new(0,1),new(1,2),new(2,3)]){Kind=ChartKind.Line,Gradient=[new(1,"#2E9B58"),new(3,"#DD4B45")]}]};
    string[] Ids(string svg)=>XDocument.Parse(svg).Descendants().Select(e=>(string?)e.Attribute("id")).OfType<string>().ToArray();
    var first=ChartSvg.Render(Faded(3));
    var ids=Ids(first);
    Check(ids.Length==2&&ids.Distinct().Count()==2&&ids.All(id=>System.Text.RegularExpressions.Regex.IsMatch(id,"^lumen-[0-9a-f]{12}-[0-9]+$")),string.Join(",",ids));
    Check(ids.Select(id=>id[..18]).Distinct().Count()==1,"one chart's IDs do not share a prefix");
    var references=System.Text.RegularExpressions.Regex.Matches(first,"url\\(#([^)]+)\\)").Select(m=>m.Groups[1].Value).Distinct().ToArray();
    Check(references.Length==2&&references.All(ids.Contains),"a reference names no definition");
    Check(ChartSvg.Render(Faded(3))==first,"the same spec renders differently");
    Check(Ids(ChartSvg.Render(Faded(3) with{Series=[..Faded(3).Series]})).SequenceEqual(ids),"an equal spec built again is named differently");
    Check(Ids(ChartSvg.Render(Faded(3),includeLegend:false,includeTitles:false)).SequenceEqual(ids),"leaving out the legend or the titles renamed the gradients");
    // Two charts on one page differ in their spec, so their prefixes differ, by one value or by the title alone.
    Check(!Ids(ChartSvg.Render(Faded(4))).Intersect(ids).Any()&&!Ids(ChartSvg.Render(Faded(3) with{Title="Another"})).Intersect(ids).Any(),"two different charts share an ID");
    // A gradient asked for twice is defined once: three columns of one colour rising share one fade.
    var columns=Svg(Spec(ChartKind.Column) with{Series=[new("S",[new(0,1),new(1,2),new(2,3)]){Fill=AreaFill.Fade}]});
    Check(columns.Descendants(ns+"linearGradient").Count()==1&&columns.Descendants(ns+"defs").Count()==1);
});
Test("A chart with gradients stays self-contained for rasterization, its definitions ahead of the marks",()=>{
    var markup=ChartSvg.Render(Spec(ChartKind.Area) with{Series=[new("S",[new(0,2),new(1,5),new(2,3)]){Fill=AreaFill.Fade,Gradient=[new(2,"#2E9B58"),new(5,"#DD4B45")]}]});
    var doc=XDocument.Parse(markup);
    Check(doc.Root!.Elements(ns+"defs").Count()==1&&doc.Root.Elements(ns+"defs").Single().Elements(ns+"linearGradient").Count()==2,"the definitions are not one block at the top level");
    Check(System.Text.RegularExpressions.Regex.Matches(markup,"url\\(([^)]*)\\)").All(m=>m.Groups[1].Value.StartsWith('#')),"a paint refers outside the document");
    Check(!markup.Contains("href")&&markup.IndexOf("http://",StringComparison.Ordinal)==markup.LastIndexOf("http://",StringComparison.Ordinal)&&!doc.Descendants(ns+"image").Any()&&!doc.Descendants(ns+"foreignObject").Any());
    Check(markup.IndexOf("<defs>",StringComparison.Ordinal)<markup.IndexOf("url(#",StringComparison.Ordinal)&&markup.IndexOf("</style>",StringComparison.Ordinal)<markup.IndexOf("<defs>",StringComparison.Ordinal));
});
Test("A faded area shades from its colour at the top of the plot to nothing at its baseline; a faded column lightens towards its far end",()=>{
    var doc=Svg(Spec(ChartKind.Area) with{YMin=-20,YMax=60,Series=[new("S",[new(0,10),new(1,-10),new(2,50)]){Fill=AreaFill.Fade,Curve=LineCurve.Smooth}]});
    var gradient=doc.Descendants(ns+"linearGradient").Single();
    var fill=doc.Descendants(ns+"path").Single(p=>((string?)p.Attribute("fill"))?.StartsWith("url(")==true);
    Check((string?)fill.Attribute("fill")==$"url(#{(string)gradient.Attribute("id")!})"&&fill.Attribute("fill-opacity") is null);
    Check((string?)gradient.Attribute("gradientUnits")=="userSpaceOnUse"&&Attr(gradient,"y1")==78&&Attr(gradient,"y2")==344,"the fade does not span the plot");
    var stops=gradient.Elements(ns+"stop").Select(s=>(Attr(s,"offset"),(string)s.Attribute("stop-opacity")!,(string)s.Attribute("stop-color")!)).ToArray();
    // With Y from -20 to 60, zero sits three quarters of the way down: a fill below it fades the same way, away from zero.
    Check(stops.SequenceEqual([(0d,".35","#5675E7"),(.75,"0","#5675E7"),(1d,".35","#5675E7")]),string.Join(",",stops));
    Check(fill.Attribute("d")!.Value.StartsWith(StrokeOf(doc).Attribute("d")!.Value+" L"),"the fill does not follow the curve");
    var floor=Svg(Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,4)]){Fill=AreaFill.Fade}]}).Descendants(ns+"stop").Select(s=>(Attr(s,"offset"),(string)s.Attribute("stop-opacity")!)).ToArray();
    Check(floor.SequenceEqual([(0d,".35"),(1d,"0")]),string.Join(",",floor));
    // Columns fade on each bar's own box, one gradient for each colour and direction.
    var columns=Svg(Spec(ChartKind.Column) with{Series=[new("S",[new(0,3),new(1,-2),new(2,5)]){Fill=AreaFill.Fade}]});
    var fades=columns.Descendants(ns+"linearGradient").ToArray();
    Check(fades.Length==2&&fades.All(f=>f.Attribute("gradientUnits") is null),"columns do not fade on their own boxes");
    string Fill(int point)=>(string)columns.Descendants(ns+"g").Single(g=>(string?)g.Attribute("data-point")==point.ToString(CultureInfo.InvariantCulture)).Element(ns+"rect")!.Attribute("fill")!;
    XElement Fade(int point)=>fades.Single(f=>Fill(point)==$"url(#{(string)f.Attribute("id")!})");
    Check(Fill(0)==Fill(2)&&Fill(0)!=Fill(1));
    Check((Attr(Fade(0),"y1"),Attr(Fade(0),"y2"))==(1,0)&&(Attr(Fade(1),"y1"),Attr(Fade(1),"y2"))==(0,1),"a column does not fade from its baseline to its far end");
    Check(fades.All(f=>f.Elements(ns+"stop").Select(s=>((string?)s.Attribute("stop-color"),(string?)s.Attribute("stop-opacity"))).SequenceEqual([("#5675E7",null),("#5675E7",".6")])));
    // A zone or point colour fades in its own colour.
    var zoned=Svg(Spec(ChartKind.Column) with{Series=[new("S",[new(0,3),new(1,8){Color="#123456"}]){Fill=AreaFill.Fade,Zones=new([new("Low",5),new("High",double.PositiveInfinity)])}]});
    Check(zoned.Descendants(ns+"stop").Select(s=>(string?)s.Attribute("stop-color")).Distinct().SequenceEqual([Ramp[0],"#123456"]));
});
Test("Hidden markers keep every point a focusable, labelled mark with an invisible target",()=>{
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Area})
    {
        var doc=Svg(Spec(kind) with{Series=[new("Heart rate",[new(0,120),new(1,null),new(2,150),new(3,140)]){Markers=MarkerStyle.None}]});
        var groups=doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
        Check(groups.Select(g=>(string)g.Attribute("data-point")!).SequenceEqual(["0","2","3"]),"a point lost its mark");
        Check(groups.All(g=>(string?)g.Attribute("tabindex")=="0"&&(string?)g.Attribute("role")=="button"&&(string?)g.Attribute("class")=="lumen-datum"),"a hidden mark cannot be focused");
        Check(Labels(doc).SequenceEqual(["Heart rate: 0, 120","Heart rate: 2, 150","Heart rate: 3, 140"]),string.Join("|",Labels(doc)));
        Check(groups.All(g=>g.Element(ns+"title")?.Value==(string?)g.Attribute("aria-label")),"a hidden mark lost its tooltip");
        // The target is the size the marker would have been, painted but transparent, so it still takes the pointer, and
        // it sets no stroke, so the focus rule draws its ring.
        Check(MarksOf(doc,0).All(c=>c.Name==ns+"circle"&&(string?)c.Attribute("r")=="4"&&(string?)c.Attribute("fill-opacity")=="0"&&(string?)c.Attribute("fill")=="#5675E7"&&c.Attribute("stroke") is null),"a target is missing or visible");
    }
    Check(Svg(Spec()).Descendants(ns+"style").Single().Value.Contains(".lumen-svg .lumen-datum:focus{stroke:currentColor;stroke-width:3}"));
    // A hollow marker paints its ring on the group, which the focus rule overrides, over a disc of the background.
    var hollow=Svg(Spec() with{Theme=ChartTheme.Dark,Series=[new("S",[new(0,1),new(1,2)]){Markers=MarkerStyle.Hollow}]});
    var group=hollow.Descendants(ns+"g").First(g=>g.Attribute("data-point") is not null);
    Check((string?)group.Attribute("stroke")=="#5675E7"&&(string?)group.Attribute("stroke-width")=="2"&&(string?)group.Element(ns+"circle")!.Attribute("fill")==ChartStyle.Dark.Background&&group.Element(ns+"circle")!.Attribute("stroke") is null);
    // Scatter marks can be filled solid or hollow, but not hidden: a scatter series is its markers.
    Check(MarksOf(Svg(Spec(ChartKind.Scatter) with{Series=[new("S",[new(0,1),new(1,2)]){Markers=MarkerStyle.Filled}]}),0).All(c=>c.Attribute("fill-opacity") is null&&c.Attribute("stroke") is null&&(string?)c.Attribute("fill")=="#5675E7"));
    Check(Svg(Spec(ChartKind.Scatter) with{Series=[new("S",[new(0,1),new(1,2)]){Markers=MarkerStyle.Hollow}]}).Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).All(g=>(string?)g.Attribute("stroke")=="#5675E7"));
    Reject(()=>ChartSvg.Render(Spec(ChartKind.Scatter) with{Series=[new("S",[new(0,1)]){Markers=MarkerStyle.None}]}));
});
Test("The last reading of a line or area is drawn larger with a soft ring, and its pane leaves room for the ring",()=>{
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Area})
    {
        var doc=Svg(Spec(kind) with{Series=[new("S",[new(0,2),new(1,5),new(2,3),new(3,null)]){HighlightLast=true,Markers=MarkerStyle.None}]});
        var groups=doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
        var last=groups.Single(g=>(string?)g.Attribute("data-point")=="2").Elements(ns+"circle").ToArray();
        Check(last.Length==2&&Attr(last[0],"r")==10&&(string?)last[0].Attribute("fill-opacity")==".2"&&Attr(last[1],"r")==5.5&&last[1].Attribute("fill-opacity") is null,"the last reading is not ringed");
        Check(!doc.Descendants(ns+"filter").Any()&&!doc.Descendants().Any(e=>e.Attribute("filter") is not null),"the ring uses a filter");
        Check(groups.Where(g=>(string?)g.Attribute("data-point")!="2").All(g=>g.Elements(ns+"circle").Count()==1&&(string?)g.Element(ns+"circle")!.Attribute("fill-opacity")=="0"),"another point was highlighted");
        Check(Attr(PaneClips(doc).Single(),"x")==76-12&&Attr(PaneClips(doc).Single(),"y")==78-12,"the clip would cut the ring");
    }
    Check(Attr(PaneClips(Svg(Spec())).Single(),"y")==72,"a chart without a highlight changed its clip");
    // In panes, only the pane that holds the highlighted series widens its clip.
    var panes=PaneClips(Svg(Stacked() with{Series=[Stacked().Series[0],Stacked().Series[1] with{HighlightLast=true},Stacked().Series[2]]}));
    Check(panes.Select(c=>Attr(c,"x")).SequenceEqual([70d,64,70]));
});
Test("A bar radius rounds only the far end of a column or bar, clamped to half its width and to its length",()=>{
    // Two categories over 794 pixels make bands 397 wide and columns .72 of that, starting .14 in.
    var doc=Svg(Spec(ChartKind.Column) with{YMin=-40,YMax=40,Style=ChartStyle.Light with{BarRadius=6},Series=[new("S",[new(0,20),new(1,-10)])]});
    double At(double v)=>344-(v+40)/80*266;
    double x0=76+397*.14,x1=76+397+397*.14,w=397*.72;
    Draws(BarOf(doc,0,0),M(x0,At(0)),L(x0,At(20)+6),A(6,x0+6,At(20)),L(x0+w-6,At(20)),A(6,x0+w,At(20)+6),L(x0+w,At(0)),Z());
    // A falling column rounds the end at the bottom, and keeps its baseline end square at the top.
    Draws(BarOf(doc,0,1),M(x1+w,At(0)),L(x1+w,At(-10)-6),A(6,x1+w-6,At(-10)),L(x1+6,At(-10)),A(6,x1,At(-10)-6),L(x1,At(0)),Z());
    // A large radius makes a capsule: twelve columns are 47.64 wide, so each end is a semicircle of 23.82.
    var capsules=Svg(Spec(ChartKind.Column) with{YMin=0,YMax=40,Style=ChartStyle.Light with{BarRadius=9999},Series=[new("S",Enumerable.Range(0,12).Select(i=>new ChartPoint(i,i==5?1:30)).ToArray())]});
    var r=794/12d*.72/2;
    var arcs=BarOf(capsules,0,0).Where(c=>c.Op=='A').ToArray();
    Check(arcs.Length==2&&arcs.All(a=>Close(a.Args[0],r)&&Close(a.Args[1],r)),"a capsule's end is not a semicircle");
    Check(Close(arcs[0].Args[5],76+794/12d*.14+r)&&Close(arcs[0].Args[6],FY(30)),"the semicircle does not peak at the column's centre");
    // A column shorter than the radius is rounded only as far as its length, so the curve never passes its baseline.
    Check(BarOf(capsules,0,5).Where(c=>c.Op=='A').All(a=>Close(a.Args[0],266/40d)),"a short column's rounding passes its baseline");
    // A horizontal bar rounds its far end to the right, or to the left when its value is negative.
    var bars=Svg(Spec(ChartKind.Bar) with{YMin=-40,YMax=40,Style=ChartStyle.Light with{BarRadius=4},Series=[new("S",[new(0,20,"A"),new(1,-10,"B")])]});
    double Bx(double v)=>160+(v+40)/80*710;
    double y0=78+133*.14,y1=78+133+133*.14,h=133*.72;
    Draws(BarOf(bars,0,0),M(Bx(0),y0),L(Bx(20)-4,y0),A(4,Bx(20),y0+4),L(Bx(20),y0+h-4),A(4,Bx(20)-4,y0+h),L(Bx(0),y0+h),Z());
    Draws(BarOf(bars,0,1),M(Bx(0),y1+h),L(Bx(-10)+4,y1+h),A(4,Bx(-10),y1+h-4),L(Bx(-10),y1+4),A(4,Bx(-10)+4,y1),L(Bx(0),y1),Z());
    // A stack rounds the far end of its last segment on each side of zero; the segments inside it stay square.
    var stack=Svg(Spec(ChartKind.StackedColumn) with{Style=ChartStyle.Midnight,Series=[new("A",[new(0,2),new(1,-3)]),new("B",[new(0,4),new(1,1)]),new("C",[new(0,-1),new(1,-2)])]});
    int Arcs(int series,int point)=>BarOf(stack,series,point).Count(c=>c.Op=='A');
    Check(Arcs(0,0)==0&&Arcs(1,0)==2&&Arcs(2,0)==2,"the first stack is rounded inside");
    Check(Arcs(0,1)==0&&Arcs(1,1)==2&&Arcs(2,1)==2,"the second stack is rounded inside");
    // A zero radius keeps every corner square, and a radius on a continuous axis rounds a column there too.
    Check(BarOf(Svg(Spec(ChartKind.Column) with{Style=ChartStyle.Light with{BarRadius=0}}),0,0).All(c=>c.Op!='A'));
    var mixed=Svg(Spec() with{Style=ChartStyle.Light with{BarRadius=3},Series=[new("L",[new(0,1),new(10,2)]),new("C",[new(0,4),new(10,-2)]){Kind=ChartKind.Column}]});
    Check(BarOf(mixed,1,0).Count(c=>c.Op=='A')==2&&BarOf(mixed,1,1).Count(c=>c.Op=='A')==2);
});
Test("Value labels sit just past each bar's far end in its axis's format, and are left out where they would not fit",()=>{
    double At(double v)=>344-(v+600)/4200*266;
    var doc=Svg(Spec(ChartKind.Column) with{YFormat=ValueFormat.Duration,YMin=-600,YMax=3600,Series=[new("Time",[new(0,1500,"A"),new(1,-300,"B"),new(2,0,"C")]){ValueLabels=true}]});
    var labels=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("aria-hidden")=="true").ToArray();
    Check(labels.Select(t=>t.Value).SequenceEqual(["25:00","-5:00","0:00"]),string.Join(",",labels.Select(t=>t.Value)));
    // Centred over each column, above a rising one and below a falling one, in the text colour the chart inherits.
    var band=794/3d;
    Check(labels.Select(t=>Attr(t,"x")).Zip(new[]{0,1,2}).All(p=>Close(p.First,76+p.Second*band+band*.14+band*.72/2))&&labels.All(t=>(string?)t.Attribute("text-anchor")=="middle"&&t.Attribute("fill") is null));
    Check(Close(Attr(labels[0],"y"),At(1500)-5)&&Close(Attr(labels[1],"y"),At(-300)+12)&&Close(Attr(labels[2],"y"),At(0)-5),"a label is not just past its column's end");
    // The labels are drawn over the clip, so the tallest column's can rise above the plot.
    Check(labels.All(t=>t.Parent==doc.Root),"a value label is clipped with the marks");
    // Twelve categories of two series make columns 23.8 wide: a long label is left out, the short ones kept.
    var narrow=Svg(Spec(ChartKind.Column) with{Series=[new("A",Enumerable.Range(0,12).Select(i=>new ChartPoint(i,i==3?123456.78:i)).ToArray()){ValueLabels=true},
        new("B",Enumerable.Range(0,12).Select(i=>new ChartPoint(i,i)).ToArray()){ValueLabels=true}]});
    var kept=narrow.Descendants(ns+"text").Where(t=>(string?)t.Attribute("aria-hidden")=="true").Select(t=>t.Value).ToArray();
    Check(kept.Length==23&&!kept.Contains("123456.78"),string.Join(",",kept));
    // A bar's label needs room inside the plot beside it: 100 and -100 reach the plot's edges, 10 has room to its right.
    var bars=Svg(Spec(ChartKind.Bar) with{Series=[new("S",[new(0,100,"Long"),new(1,10,"Short"),new(2,-100,"Negative")]){ValueLabels=true}]});
    var beside=bars.Descendants(ns+"text").Where(t=>(string?)t.Attribute("aria-hidden")=="true").ToArray();
    Check(beside.Length==1&&beside[0].Value=="10"&&Close(Attr(beside[0],"x"),160+110/200d*710+6)&&(string?)beside[0].Attribute("text-anchor")=="start",string.Join(",",beside.Select(t=>t.Value)));
    var falling=Svg(Spec(ChartKind.Bar) with{YMin=-100,YMax=100,Series=[new("S",[new(0,-10,"A")]){ValueLabels=true}]}).Descendants(ns+"text").Single(t=>(string?)t.Attribute("aria-hidden")=="true");
    Check(Close(Attr(falling,"x"),160+90/200d*710-6)&&(string?)falling.Attribute("text-anchor")=="end");
    // A column the plot does not show takes its label with it.
    var zoomed=Svg(Spec() with{XMin=0,XMax=5,Series=[new("L",[new(0,1),new(10,2)]),new("C",[new(2,4),new(8,3)]){Kind=ChartKind.Column,ValueLabels=true}]});
    Check(zoomed.Descendants(ns+"text").Where(t=>(string?)t.Attribute("aria-hidden")=="true").Select(t=>t.Value).SequenceEqual(["4"]));
});
Test("Gridlines can be dotted, dashed or hidden; the labels stay, and a radar keeps its rings",()=>{
    var spec=Classic(Spec() with{MinorGridlines=true,Series=[new("S",[new(0,2),new(1,15),new(2,7)])]});
    var solid=Svg(spec);
    Check(GridStrokes(solid).Length>10&&GridStrokes(solid).All(l=>l.Attribute("stroke-dasharray") is null));
    foreach(var (grid,dash) in new[]{(GridLine.Dotted,"1 3"),(GridLine.Dashed,"4 4")})
    {
        var doc=Svg(spec with{Style=spec.Style! with{Gridlines=grid}});
        Check(GridStrokes(doc).Length==GridStrokes(solid).Length&&GridStrokes(doc).All(l=>(string?)l.Attribute("stroke-dasharray")==dash),$"{grid} gridlines");
        Check(Ticks(doc,"end").SequenceEqual(Ticks(solid,"end"))&&StrokeOf(doc).Attribute("stroke-dasharray") is null,$"{grid} changed more than the grid");
    }
    var hidden=Svg(spec with{Style=spec.Style! with{Gridlines=GridLine.Hidden}});
    Check(GridStrokes(hidden).Length==0&&Ticks(hidden,"end").SequenceEqual(Ticks(solid,"end"))&&Ticks(hidden,"middle").SequenceEqual(Ticks(solid,"middle")),"a hidden grid lost its labels");
    Check(!hidden.Descendants(ns+"style").Single().Value.Contains("lumen-grid-minor"),"a chart that draws no minor lines carries their rule");
    // Horizontal bars, and the frame histograms, box plots and violins share, follow the style too.
    Check(GridStrokes(Svg(Spec(ChartKind.Bar) with{Style=ChartStyle.Light with{Gridlines=GridLine.Dotted}})).All(l=>(string?)l.Attribute("stroke-dasharray")=="1 3"));
    foreach(var kind in new[]{ChartKind.Histogram,ChartKind.Box,ChartKind.Violin})
    {
        Check(GridStrokes(Svg(Sample(kind) with{MinorGridlines=true,Style=ChartStyle.Light with{Gridlines=GridLine.Dashed}})).All(l=>(string?)l.Attribute("stroke-dasharray")=="4 4"),$"{kind} dashed");
        Check(GridStrokes(Svg(Sample(kind) with{Style=ChartStyle.Light with{Gridlines=GridLine.Hidden}})).Length==0,$"{kind} hidden");
    }
    // A radar's rings and spokes are its scale, so they stay as they are.
    var radar=Svg(Sample(ChartKind.Radar));
    foreach(var grid in Enum.GetValues<GridLine>())
    {
        var styled=Svg(Sample(ChartKind.Radar) with{Style=ChartStyle.Light with{Gridlines=grid}});
        Check(styled.Descendants().Count(e=>(string?)e.Attribute("class")=="lumen-grid")==radar.Descendants().Count(e=>(string?)e.Attribute("class")=="lumen-grid")&&!styled.Descendants().Any(e=>e.Attribute("stroke-dasharray") is not null),$"{grid} changed a radar");
    }
});
Test("The Y axis can be labelled on the right, unless a secondary series holds that edge",()=>{
    var doc=Svg(Spec() with{YAxisSide=AxisSide.Right,YLabel="bpm",Series=[new("S",[new(0,2),new(1,15),new(2,7)])]});
    Check(Ticks(doc,"end").Length==0,"a tick label stayed on the left");
    var right=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="start"&&(string?)t.Attribute("class")=="lumen-muted").ToArray();
    Check(right.Select(t=>t.Value).SequenceEqual(Ticks(Svg(Spec() with{Series=[new("S",[new(0,2),new(1,15),new(2,7)])]}),"end"))&&right.All(t=>Attr(t,"x")==836),"the tick labels are not just right of the plot");
    // The plot gives the left margin back and takes the right one: it runs from 30 to 824.
    Check(GridStrokes(doc).All(l=>Attr(l,"x1")==30&&Attr(l,"x2")==824)&&Attr(PaneClips(doc).Single(),"x")==24);
    var title=doc.Descendants(ns+"text").Single(t=>t.Value=="bpm");
    Check(Attr(title,"x")==884&&((string)title.Attribute("transform")!).StartsWith("rotate(90 884 ",StringComparison.Ordinal),"the title does not read downwards at the right edge");
    // Every pane's axis moves, and so does the frame of the statistical kinds.
    var panes=Svg(Stacked() with{YAxisSide=AxisSide.Right});
    Check(Ticks(panes,"end").Length==0&&panes.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="start").All(t=>Attr(t,"x")==836));
    foreach(var label in new[]{"Top","Middle","Bottom"}) Check(Attr(panes.Descendants(ns+"text").Single(t=>t.Value==label&&t.Attribute("transform") is not null),"x")==884,$"pane {label} keeps its title on the left");
    var box=Svg(Sample(ChartKind.Box) with{YAxisSide=AxisSide.Right});
    Check(Ticks(box,"end").Length==0&&Ticks(box,"start").Length>=2&&GridStrokes(box).All(l=>Attr(l,"x1")==30&&Attr(l,"x2")==824));
    Reject(()=>ChartSvg.Render(Paired() with{YAxisSide=AxisSide.Right}));
    foreach(var kind in new[]{ChartKind.Bar,ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar}) Reject(()=>ChartSvg.Render(Sample(kind) with{YAxisSide=AxisSide.Right}));
});
Test("Ends-only tick labels name the lowest and highest tick and keep every gridline",()=>{
    var spec=Spec() with{YMin=0,YMax=100,Series=[new("S",[new(0,10),new(1,90)])]};
    XDocument all=Svg(spec),ends=Svg(spec with{YTickLabels=TickLabels.Ends});
    Check(Ticks(all,"end").SequenceEqual(["0","25","50","75","100"])&&Ticks(ends,"end").SequenceEqual(["0","100"]),string.Join(",",Ticks(ends,"end")));
    Check(Grid(ends).SequenceEqual(Grid(all)),"a gridline went with its label");
    // On a reversed axis the lowest and highest values are still the ones named, at the top and bottom.
    Check(Ticks(Svg(spec with{YReversed=true,YTickLabels=TickLabels.Ends}),"end").SequenceEqual(["0","100"]));
    // A horizontal bar chart's value axis runs along the bottom; a secondary axis labels every tick of its own.
    var bar=Svg(Spec(ChartKind.Bar) with{YMin=0,YMax=100,YTickLabels=TickLabels.Ends,Series=[new("S",[new(0,10,"A"),new(1,90,"B")])]});
    Check(Ticks(bar,"middle").Where(t=>t.Length>0).SequenceEqual(["0","100"]),string.Join(",",Ticks(bar,"middle")));
    var paired=Svg(Paired() with{YTickLabels=TickLabels.Ends});
    Check(Ticks(paired,"end").Length==2&&Ticks(paired,"start").Length==Ticks(Svg(Paired()),"start").Length);
    // The statistical kinds share a frame; a histogram's note on its bins is end-anchored too, so the labels are told by their place.
    foreach(var kind in new[]{ChartKind.Histogram,ChartKind.Box,ChartKind.Violin})
        Check(Svg(Sample(kind) with{YTickLabels=TickLabels.Ends}).Descendants(ns+"text").Count(t=>(string?)t.Attribute("text-anchor")=="end"&&Attr(t,"x")==64)==2,$"{kind} labels more than its ends");
    foreach(var kind in new[]{ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar}) Reject(()=>ChartSvg.Render(Sample(kind) with{YTickLabels=TickLabels.Ends}));
});
Test("A stroke width draws lines, areas and bands at that width, zone pieces and projections included",()=>{
    Check((string?)StrokeOf(Svg(Spec() with{Series=[new("S",[new(0,1),new(1,2)]){StrokeWidth=4.5}]})).Attribute("stroke-width")=="4.5");
    Check((string?)StrokeOf(Svg(Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,2)]){StrokeWidth=.5}]})).Attribute("stroke-width")=="0.5");
    Check((string?)StrokeOf(Svg(Sample(ChartKind.Band) with{Series=[Sample(ChartKind.Band).Series[0] with{StrokeWidth=12}]})).Attribute("stroke-width")=="12");
    var pieces=Svg(Effortful(110,150,130) with{Series=[Effortful(110,150,130).Series[0] with{StrokeWidth=1.5,ProjectedFrom=1.5}]}).Descendants(ns+"path").Where(p=>(string?)p.Attribute("fill")=="none").ToArray();
    Check(pieces.Length>3&&pieces.All(p=>(string?)p.Attribute("stroke-width")=="1.5"),"a zone or projected piece kept the default width");
});
Test("Midnight clears 3:1 for every mark colour and 4.5:1 for its text against a near-black background",()=>{
    var style=ChartStyle.Midnight;
    Check(style.ContrastIssues().Count==0,string.Join("; ",style.ContrastIssues()));
    Check(Lumen.Charts.Contrast.Ratio(style.Background,"#000000")<1.2,"the background is not near black");
    foreach(var colour in style.Series.Concat(style.Zones).Append(style.Rising).Append(style.Falling).Append(style.Edge))
        Check(Lumen.Charts.Contrast.Ratio(colour,style.Background)>=3,$"{colour} is {Lumen.Charts.Contrast.Ratio(colour,style.Background):0.00}");
    Check(Lumen.Charts.Contrast.Ratio(style.Text,style.Background)>=4.5&&Lumen.Charts.Contrast.Ratio(style.Muted,style.Background)>=4.5);
    // A zone band's label is drawn in the text colour over the zone at .12.
    foreach(var zone in style.Zones) Check(Lumen.Charts.Contrast.Ratio(style.Text,Tint(zone,style.Background,.12))>=4.5,$"a label over {zone}");
    Check(style.Series.Distinct().Count()==style.Series.Count&&style.Zones.Count==7&&style.Zones.Distinct().Count()==7);
    // Dotted gridlines, and a radius past half the widest bar any chart can draw, so every bar is a capsule.
    Check(style.Gridlines==GridLine.Dotted&&style.BarRadius>=4096/2d);
    var doc=Svg(Spec(ChartKind.Column) with{Style=style});
    Check(BarOf(doc,0,1).Where(c=>c.Op=='A').All(a=>Close(a.Args[0],794/3d*.72/2))&&GridStrokes(doc).All(l=>(string?)l.Attribute("stroke-dasharray")=="1 3"),"Midnight does not draw capsules over a dotted grid");
    Check(doc.Root!.Attribute("style")!.Value.Contains("background:#0B0E14"));
    foreach(var kind in Enum.GetValues<ChartKind>()) Svg(Sample(kind) with{Style=style});
    // Cascaded from the host, it reaches the component as any brand does.
    var cascaded=RenderInside(style,Spec(ChartKind.Column));
    Check(cascaded.Contains("background:#0B0E14")&&cascaded.Contains("stroke-dasharray='1 3'")&&cascaded.Contains(" A95.28,95.28 0 0 1 "),"a cascaded Midnight did not reach the chart");
});
Test("The finish survives JSON, and a request that names none of it draws as before",()=>{
    var json="{\"kind\":\"Column\",\"yAxisSide\":\"Right\",\"yTickLabels\":\"Ends\",\"style\":{\"gridlines\":\"Dotted\",\"barRadius\":8},\"series\":["+
        "{\"name\":\"Load\",\"fill\":\"Fade\",\"valueLabels\":true,\"points\":[{\"x\":0,\"y\":3},{\"x\":1,\"y\":5}]},"+
        "{\"name\":\"Trend\",\"kind\":\"Line\",\"curve\":\"Smooth\",\"strokeWidth\":3,\"markers\":\"None\",\"highlightLast\":true,\"gradient\":[{\"value\":3,\"color\":\"#2E9B58\"},{\"value\":5,\"color\":\"#DD4B45\"}],\"points\":[{\"x\":0,\"y\":3},{\"x\":1,\"y\":5}]},"+
        "{\"name\":\"Steps\",\"kind\":\"Area\",\"curve\":\"Step\",\"markers\":\"Hollow\",\"points\":[{\"x\":0,\"y\":1},{\"x\":1,\"y\":2}]}]}";
    var spec=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(spec.YAxisSide==AxisSide.Right&&spec.YTickLabels==TickLabels.Ends&&spec.Style!.Gridlines==GridLine.Dotted&&spec.Style.BarRadius==8&&spec.Style.Background==ChartStyle.Light.Background);
    Check(spec.Series[0].Fill==AreaFill.Fade&&spec.Series[0].ValueLabels&&spec.Series[1].Curve==LineCurve.Smooth&&spec.Series[1].StrokeWidth==3&&spec.Series[1].Markers==MarkerStyle.None&&spec.Series[1].HighlightLast
        &&spec.Series[1].Gradient!.SequenceEqual([new ColorStop(3,"#2E9B58"),new ColorStop(5,"#DD4B45")])&&spec.Series[2].Curve==LineCurve.Step&&spec.Series[2].Markers==MarkerStyle.Hollow);
    var svg=ChartSvg.Render(spec);
    Check(svg==ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(System.Text.Json.JsonSerializer.Serialize(spec,finishJson),finishJson)!),"the finish changed in transit");
    Check(svg.Contains("<linearGradient")&&svg.Contains("stroke-dasharray='1 3'")&&svg.Contains(" A8,8 "));
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"style\":{\"background\":\"#F6F3EE\",\"finish\":\"Classic\"},\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]}]}",finishJson)!;
    Check(old.YAxisSide==AxisSide.Left&&old.YTickLabels==TickLabels.All&&old.Style!.Gridlines==GridLine.Solid&&old.Style.BarRadius is null
        &&old.Series[0].Curve==LineCurve.Linear&&old.Series[0].Fill==AreaFill.Flat&&old.Series[0].Markers==MarkerStyle.Auto&&old.Series[0].StrokeWidth is null&&old.Series[0].Gradient is null&&!old.Series[0].HighlightLast&&!old.Series[0].ValueLabels);
    Check(!ChartSvg.Render(old).Contains(" id="));
});
Test("Each finishing touch refuses the marks and values it cannot apply to",()=>{
    ChartSeries s=new("S",[new(0,1),new(1,2),new(2,3)]);
    void No(ChartKind kind,ChartSeries series)=>Reject(()=>ChartSvg.Render(Spec(kind) with{Series=[series]}));
    void Yes(ChartKind kind,ChartSeries series)=>ChartSvg.Render(Spec(kind) with{Series=[series]});
    foreach(var width in new[]{.4,12.01,0,-3,double.NaN,double.PositiveInfinity}) No(ChartKind.Line,s with{StrokeWidth=width});
    Yes(ChartKind.Line,s with{StrokeWidth=.5});Yes(ChartKind.Area,s with{StrokeWidth=12});
    foreach(var kind in new[]{ChartKind.Column,ChartKind.Scatter,ChartKind.Bar}) No(kind,s with{StrokeWidth=2});
    foreach(var kind in new[]{ChartKind.Column,ChartKind.Scatter,ChartKind.Band,ChartKind.Bubble}) { No(kind,s with{Curve=LineCurve.Smooth}); No(kind,s with{Curve=LineCurve.Step}); }
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Bar,ChartKind.Scatter,ChartKind.StackedColumn,ChartKind.Band}) No(kind,s with{Fill=AreaFill.Fade});
    Yes(ChartKind.Line,s with{Kind=ChartKind.Column,Fill=AreaFill.Fade});
    foreach(var kind in new[]{ChartKind.Column,ChartKind.Bubble,ChartKind.Band,ChartKind.Bar}) No(kind,s with{Markers=MarkerStyle.Hollow});
    No(ChartKind.Scatter,s with{Markers=MarkerStyle.None});
    foreach(var kind in new[]{ChartKind.Scatter,ChartKind.Column,ChartKind.Band}) No(kind,s with{HighlightLast=true});
    // 0.33.0 writes value labels on lines and scatter points too.
    foreach(var kind in new[]{ChartKind.Area,ChartKind.Bubble,ChartKind.Band,ChartKind.StackedColumn}) No(kind,s with{ValueLabels=true});
    Yes(ChartKind.Line,s with{ValueLabels=true});Yes(ChartKind.Scatter,s with{ValueLabels=true});
    No(ChartKind.Line,s with{Curve=(LineCurve)7});No(ChartKind.Area,s with{Fill=(AreaFill)3});No(ChartKind.Line,s with{Markers=(MarkerStyle)9});
    ColorStop[] two=[new(1,"#2E9B58"),new(3,"#DD4B45")];
    foreach(var kind in new[]{ChartKind.Scatter,ChartKind.Band,ChartKind.StackedColumn}) No(kind,s with{Gradient=two});
    No(ChartKind.Line,s with{Gradient=two,Zones=new([new("Low",2),new("High",double.PositiveInfinity)])});
    foreach(var stops in new ColorStop[][]{[],[new(1,"#2E9B58")],[new(1,"#2E9B58"),new(1,"#DD4B45")],[new(3,"#2E9B58"),new(1,"#DD4B45")],[new(1,"red"),new(3,"#DD4B45")],
        [new(1,"#2E9B58"),null!],[new(1,null!),new(3,"#DD4B45")],[new(double.NaN,"#2E9B58"),new(3,"#DD4B45")],[new(1,"#2E9B58"),new(1e101,"#DD4B45")],
        Enumerable.Range(0,33).Select(i=>new ColorStop(i,"#2E9B58")).ToArray()})
        No(ChartKind.Line,s with{Gradient=stops});
    Yes(ChartKind.Line,s with{Gradient=Enumerable.Range(0,32).Select(i=>new ColorStop(i,"#2E9B58")).ToArray()});
    Reject(()=>ChartSvg.Render(Spec() with{YAxis=AxisKind.Log,Series=[s with{Gradient=[new(0,"#2E9B58"),new(3,"#DD4B45")]}]}));
    Reject(()=>ChartSvg.Render(Spec() with{Series=[new("L",[new(0,5)]),s with{Secondary=true,Gradient=[new(-1,"#2E9B58"),new(3,"#DD4B45")]}],Y2Axis=AxisKind.Log}));
    foreach(var radius in new[]{-1,double.NaN,double.PositiveInfinity,1e101}) Reject(()=>ChartSvg.Render(Spec(ChartKind.Column) with{Style=ChartStyle.Light with{BarRadius=radius}}));
    Reject(()=>ChartSvg.Render(Spec() with{Style=ChartStyle.Light with{Gridlines=(GridLine)4}}));
    Reject(()=>ChartSvg.Render(Spec() with{YAxisSide=(AxisSide)2}));
    // 0.35.0 made 2 TickLabels.Bounds, so an unknown labelling is a value past it.
    Reject(()=>ChartSvg.Render(Spec() with{YTickLabels=(TickLabels)9}));
});
// 0.24.0: the refined finish is the default and the classic one is the exact way back. The rows below are renderings of the
// release baseline, hashed from 0.23.0's own output, so the classic finish must reproduce every byte of them; the two graph rows
// hold 0.31.0's layout, which moved graphs in both finishes.
string Hash16(string svg)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(svg)))[..16];
ChartPoint[] Twelve()=>Enumerable.Range(0,12).Select(i=>new ChartPoint(i,10+i*3+(i%3)*4,$"P{i}")).ToArray();
ChartSpec Baseline(ChartKind kind,ChartTheme theme)=>kind switch{
    ChartKind.Candlestick or ChartKind.Ohlc=>new(){Kind=kind,Theme=theme,Series=[new("P",Enumerable.Range(0,8).Select(i=>ChartPoint.Candle(i,10+i,14+i,8+i,11+i+(i%2==0?1:-2))).ToArray())]},
    ChartKind.Band=>new(){Kind=kind,Theme=theme,Series=[new("F",Enumerable.Range(0,8).Select(i=>ChartPoint.Interval(i,10+i,8+i,13+i)).ToArray())]},
    ChartKind.Histogram=>new(){Kind=kind,Theme=theme,Series=[new("S",Enumerable.Range(0,60).Select(i=>new ChartPoint(i,i%13+(i==7?40:0))).ToArray())]},
    ChartKind.Box=>new(){Kind=kind,Theme=theme,Series=[new("S",Enumerable.Range(0,60).Select(i=>new ChartPoint(i,i%13+(i==7?40:0))).ToArray()),new("T",Enumerable.Range(0,40).Select(i=>new ChartPoint(i,i%9+3)).ToArray())]},
    ChartKind.Donut=>new(){Kind=kind,Theme=theme,Series=[new("D",[new(0,4,"A"),new(1,3,"B"),new(2,2,"C")])]},
    ChartKind.Radar=>new(){Kind=kind,Theme=theme,Series=[new("R",[new(0,4,"A"),new(1,3,"B"),new(2,5,"C"),new(3,2,"D")]),new("Q",[new(0,2,"A"),new(1,4,"B"),new(2,3,"C"),new(3,4,"D")])]},
    ChartKind.Heatmap=>new(){Kind=kind,Theme=theme,Series=Enumerable.Range(0,3).Select(r=>new ChartSeries($"Row {r}",Enumerable.Range(0,6).Select(c=>new ChartPoint(c,(r*7+c*5)%17,$"C{c}")).ToArray())).ToArray()},
    _=>new(){Kind=kind,Theme=theme,Title="Baseline",Description="Default output",Series=[new("A",Twelve()),new("B",Twelve().Select(p=>p with{Y=p.Y+5}).ToArray())]}};
Test("The classic finish draws 0.23.0's charts byte for byte, gradients and their IDs included, and graphs as 0.31.0 lays them out, and the refined one does not",()=>{
    // The reference hashes were recorded on Windows. Another platform's maths library can differ in the last bits of a
    // sine or a logarithm, which moves the eighth decimal of a donut's arc, so byte equality only means something where
    // the hashes came from. Elsewhere the classic finish is held by the structural checks that follow.
    if(!OperatingSystem.IsWindows())return;
    ChartSpec line=Baseline(ChartKind.Line,ChartTheme.Light),column=Baseline(ChartKind.Column,ChartTheme.Light);
    ChartPoint[] Signed()=>Twelve().Select((p,i)=>p with{Y=i%3==0?-p.Y/2:p.Y-20}).ToArray();
    ChartAnnotation[] References(double at,double from,double to)=>[new(AnnotationAxis.Y,at){Label="Line"},new(AnnotationAxis.Y,from){To=to,Label="Band",Color="#B03A2E"}];
    var harbour=new ChartStyle{Background="#F6F3EE",Text="#1F2A37",Muted="#4B5563",Grid="#E5DED3",Edge="#6B7280",Series=["#1D4E89","#B03A2E","#2E7D5B","#9A6A12"],FontFamily="Georgia,Cambria,serif"};
    var heart=ZoneScale.CogganHeartRate(170);
    var seconds=Training.TimeInZone(Enumerable.Range(0,2400).Select(t=>Math.Round(95+85*(1-Math.Exp(-t/400.0))+12*Math.Sin(t/70.0)+t%5,1)).ToArray(),heart);
    var climb=Enumerable.Range(0,120).Select(i=>new ChartPoint(i*30,Math.Round(24+16*Math.Sin(i/9d)+6*Math.Sin(i/3.1),1))).ToArray();
    double[] volume=[6.5,7.2,8.1,5.0,7.9,8.8,9.4,5.6,9.1,10.2,10.8,6.0];
    var rolling=Statistics.Rolling(volume.Select(v=>(double?)v).ToArray(),4,1);
    ChartSpec weekly=new(){Kind=ChartKind.Column,Title="Weekly volume",YLabel="Hours",Series=[new("Volume",volume.Select((v,i)=>new ChartPoint(i,v,$"W{i+1}")).ToArray()),
        new("Four-week average",rolling.Select((r,i)=>new ChartPoint(i,Math.Round(r!.Mean,2),$"W{i+1}")).ToArray()){Kind=ChartKind.Line}]};
    var graph=new GraphSpec{Nodes=[new("a","A"),new("b","B"),new("c","C"),new("d","D")],Edges=[new("a","b"),new("b","c"),new("a","c","long"),new("c","d"),new("d","d")]};
    string Chart(ChartSpec spec,bool classic,bool titles=true)=>ChartSvg.Render(classic?Classic(spec):spec,includeTitles:titles);
    string Network(GraphSpec spec,bool classic)=>GraphEngine.Render(classic?spec with{Style=(spec.Style ?? (spec.Theme==ChartTheme.Dark?ChartStyle.Dark:ChartStyle.Light)) with{Finish=ChartFinish.Classic}}:spec);
    (string Row,string Hash,Func<bool,string> Draw)[] rows=[
        ("Line/Light/True","CB7DF56598CE861F",c=>Chart(line,c)),
        ("Line/Dark/False","55C2631265A381D5",c=>Chart(Baseline(ChartKind.Line,ChartTheme.Dark),c,false)),
        ("Area/Light/True","C65B9E85D21CE693",c=>Chart(Baseline(ChartKind.Area,ChartTheme.Light),c)),
        ("Scatter/Light/True","C997C248FD160F89",c=>Chart(Baseline(ChartKind.Scatter,ChartTheme.Light),c)),
        ("Bubble/Light/True","65D16775AD95EA30",c=>Chart(Baseline(ChartKind.Bubble,ChartTheme.Light),c)),
        ("Column/Dark/True","D519769B30BA6A40",c=>Chart(Baseline(ChartKind.Column,ChartTheme.Dark),c)),
        ("Bar/Light/False","193C4F6998BF341D",c=>Chart(Baseline(ChartKind.Bar,ChartTheme.Light),c,false)),
        ("StackedColumn/Light/True","6E0798E56574C38E",c=>Chart(Baseline(ChartKind.StackedColumn,ChartTheme.Light),c)),
        ("Donut/Light/True","41B05E947E74CD0F",c=>Chart(Baseline(ChartKind.Donut,ChartTheme.Light),c)),
        ("Heatmap/Light/True","E701C0163185BA1C",c=>Chart(Baseline(ChartKind.Heatmap,ChartTheme.Light),c)),
        ("Radar/Light/True","16406553279DC2CE",c=>Chart(Baseline(ChartKind.Radar,ChartTheme.Light),c)),
        ("Candlestick/Light/True","ABD4EB8E08B1956B",c=>Chart(Baseline(ChartKind.Candlestick,ChartTheme.Light),c)),
        ("Band/Light/True","BA35FFE76F28B956",c=>Chart(Baseline(ChartKind.Band,ChartTheme.Light),c)),
        ("Histogram/Light/True","7A551A8B643A8CBB",c=>Chart(Baseline(ChartKind.Histogram,ChartTheme.Light),c)),
        ("Box/Light/True","95739F92921BA181",c=>Chart(Baseline(ChartKind.Box,ChartTheme.Light),c)),
        ("Violin/Light/True","AECF3380086ED39C",c=>Chart(Baseline(ChartKind.Violin,ChartTheme.Light),c)),
        ("Ohlc/Dark/True","9EC042FF686CC18D",c=>Chart(Baseline(ChartKind.Ohlc,ChartTheme.Dark),c)),
        ("annotated","B8D59813CF5546F6",c=>Chart(line with{Annotations=[new(AnnotationAxis.Y,25){Label="Target"},new(AnnotationAxis.X,3){To=6,Label="Window"}]},c)),
        ("branded","EA336EC369F8AAB7",c=>Chart(Baseline(ChartKind.Area,ChartTheme.Light) with{Style=ChartStyle.Light with{Background="#F6F3EE",Series=["#1D4E89","#B03A2E"],FontFamily="Georgia,serif"}},c)),
        // 0.31.0 moved graphs by design, in both finishes: a circle stands in from the sides by half its widest label, and an edge's
        // label stands at the first free place along the drawn edge. These two rows hold 0.31.0's classic drawings.
        ("graph/Circular/Light","7C46B8ECD7E02490",c=>Network(graph with{Layout=GraphLayout.Circular,Theme=ChartTheme.Light},c)),
        ("graph/Layered/Dark","D1C37290EC07B085",c=>Network(graph with{Layout=GraphLayout.Layered,Theme=ChartTheme.Dark},c)),
        ("guard/y-annotations-Bar","B3B36E22BBCD4641",c=>Chart(Baseline(ChartKind.Bar,ChartTheme.Dark) with{Annotations=References(25,30,40)},c)),
        ("zones/time-in-zone","77633DA66248659B",c=>Chart(Baseline(ChartKind.Bar,ChartTheme.Light) with{YFormat=ValueFormat.Duration,
            Series=[new("Time in zone",heart.Zones.Select((z,i)=>new ChartPoint(i,seconds[i],z.Name){Color=ChartStyle.Light.Zones[i]}).ToArray())]},c)),
        ("guard/style-light-line","A926CA670C8441DC",c=>Chart(line with{Style=ChartStyle.Light,MinorGridlines=true,Annotations=References(25,30,40)},c)),
        ("guard/style-brand-line","C7CF4B40CCDCA415",c=>Chart(line with{Style=harbour,MinorGridlines=true,Annotations=References(25,30,40)},c)),
        // Gradients: on no style, which a classic chart names as 0.23.0 named it; on a preset adjusted in C#; and on Midnight.
        ("finish/smooth-fade-area","4E9548279D4A245D",c=>Chart(new(){Kind=ChartKind.Area,Title="Smooth fade",XFormat=ValueFormat.Duration,
            Series=[new("Climb",climb){Curve=LineCurve.Smooth,Fill=AreaFill.Fade,StrokeWidth=2,Markers=MarkerStyle.None}]},c)),
        ("finish/capsule-value-labels","08631EBEDF404578",c=>Chart(column with{Title="Capsules",Style=ChartStyle.Light with{BarRadius=9999},
            Series=[new("Week",Signed()){ValueLabels=true,Fill=AreaFill.Fade},new("Last",Twelve().Select(p=>p with{Y=p.Y/2}).ToArray()){ValueLabels=true}]},c)),
        ("finish/midnight-weekly","53E74673F793CA0E",c=>Chart(weekly with{Style=ChartStyle.Midnight,Series=[weekly.Series[0] with{ValueLabels=true},weekly.Series[1] with{Curve=LineCurve.Smooth,Markers=MarkerStyle.Hollow}]},c)),
        ("finish/gradient-reversed","86F1C5C81227DEA4",c=>Chart(line with{Title="Reversed gradient",YReversed=true,YFormat=ValueFormat.Duration,
            Series=[new("Pace",Enumerable.Range(0,30).Select(i=>new ChartPoint(i,330-i*3+i%4*6)).ToArray()){Gradient=[new(260,"#DD4B45"),new(300,"#A88200"),new(340,"#3F87D9")],Curve=LineCurve.Smooth,HighlightLast=true}]},c))];
    foreach(var (row,hash,draw) in rows)
    {
        Check(Hash16(draw(true))==hash,$"{row} is not 0.23.0's in the classic finish: {Hash16(draw(true))}");
        Check(Hash16(draw(false))!=hash,$"{row} is unchanged in the refined finish");
    }
});
Test("A style draws refined unless it names the classic finish, whose gridlines are solid unless set, and both survive JSON",()=>{
    Check(ChartStyle.Light.Finish==ChartFinish.Refined&&ChartStyle.Dark.Finish==ChartFinish.Refined&&ChartStyle.Midnight.Finish==ChartFinish.Refined&&new ChartStyle().Finish==ChartFinish.Refined);
    // Every preset and every brand reads dotted gridlines in the refined finish, and solid ones in the classic, as 0.23.0 drew them.
    Check(new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight,new ChartStyle{Background="#F6F3EE"}}.All(s=>s.Gridlines==GridLine.Dotted),"a refined style is not dotted");
    Check((ChartStyle.Light with{Finish=ChartFinish.Classic}).Gridlines==GridLine.Solid&&(ChartStyle.Dark with{Finish=ChartFinish.Classic}).Gridlines==GridLine.Solid,"a classic preset is not solid");
    // A style that sets its gridlines keeps them in either finish: Midnight is dotted in both, and a dashed grid dashed.
    Check((ChartStyle.Midnight with{Finish=ChartFinish.Classic}).Gridlines==GridLine.Dotted&&(ChartStyle.Light with{Gridlines=GridLine.Dashed,Finish=ChartFinish.Classic}).Gridlines==GridLine.Dashed
        &&(ChartStyle.Light with{Gridlines=GridLine.Solid}).Gridlines==GridLine.Solid);
    var json=System.Text.Json.JsonSerializer.Serialize(ChartStyle.Light with{Finish=ChartFinish.Classic},finishJson);
    Check(json.Contains("\"finish\":\"Classic\"")&&json.Contains("\"gridlines\":\"Solid\""),json);
    var read=System.Text.Json.JsonSerializer.Deserialize<ChartStyle>(json,finishJson)!;
    Check(read.Finish==ChartFinish.Classic&&read.Gridlines==GridLine.Solid);
    // A request that names the classic finish draws 0.23.0's 2.5-pixel stroke; one that names no finish the refined 1.6.
    string Request(string style)=>ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\""+style+",\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1},{\"x\":1,\"y\":2}]}]}",finishJson)!);
    Check(Request(",\"style\":{\"finish\":\"Classic\"}")==ChartSvg.Render(Classic(new ChartSpec{Series=[new("S",[new(0,1),new(1,2)])]}))&&Request(",\"style\":{\"finish\":\"Classic\"}").Contains("stroke-width='2.5'"),"a classic request does not draw as 0.23.0");
    Check(Request("").Contains("stroke-width='1.6'")&&Request(",\"style\":{\"background\":\"#FFFFFF\"}").Contains("stroke-width='1.6'"),"a request without a finish does not draw refined");
    Reject(()=>ChartSvg.Render(Spec() with{Style=ChartStyle.Light with{Finish=(ChartFinish)5}}));
    // A chart in each finish names its gradients differently, so the two can share a page.
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}]};
    string Prefix(string svg)=>System.Text.RegularExpressions.Regex.Match(svg,"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    Check(Prefix(ChartSvg.Render(faded)).Length>0&&Prefix(ChartSvg.Render(faded))!=Prefix(ChartSvg.Render(Classic(faded))),"the two finishes share gradient IDs");
    Check(Prefix(ChartSvg.Render(faded))==Prefix(ChartSvg.Render(faded with{Style=ChartStyle.Light})),"two drawings of one refined chart name its gradients differently");
});
// Every element that strokes, by its own attribute or, for a gridline, by the stylesheet. Text is left out: a reference label's
// halo is a stroke that belongs to the text and scales with it.
XElement[] Stroked(XDocument doc)=>doc.Descendants().Where(e=>e.Name!=ns+"text"&&e.Name!=ns+"g"&&
    ((string?)e.Attribute("stroke") is {} stroke&&stroke!="none"||((string?)e.Attribute("class"))?.StartsWith("lumen-grid")==true)).ToArray();
Test("Refined strokes are 1.6 pixels with round ends and joins, and every stroke keeps its width at any display size",()=>{
    var line=StrokeOf(Svg(Spec()));
    Check((string?)line.Attribute("stroke-width")=="1.6"&&(string?)line.Attribute("vector-effect")=="non-scaling-stroke"&&(string?)line.Attribute("stroke-linecap")=="round"&&(string?)line.Attribute("stroke-linejoin")=="round",line.ToString());
    // Areas and bands draw 1.6 too, and so does every zone and projected piece; a stroke width of the series' own still wins.
    Check((string?)StrokeOf(Svg(Spec(ChartKind.Area))).Attribute("stroke-width")=="1.6"&&(string?)StrokeOf(Svg(Sample(ChartKind.Band))).Attribute("stroke-width")=="1.6");
    var pieces=Svg(Spec() with{YMin=100,YMax=200,Series=[new("Heart rate",[new(0,110),new(1,150),new(2,130)]){Zones=Effort(),ProjectedFrom=1.5}]}).Descendants(ns+"path").Where(p=>(string?)p.Attribute("fill")=="none").ToArray();
    Check(pieces.Length>3&&pieces.All(p=>(string?)p.Attribute("stroke-width")=="1.6"&&(string?)p.Attribute("vector-effect")=="non-scaling-stroke"&&(string?)p.Attribute("stroke-linecap")=="round"),"a zone or projected piece is not a refined stroke");
    Check((string?)StrokeOf(Svg(Spec() with{Series=[new("S",[new(0,1),new(1,2)]){StrokeWidth=3}]})).Attribute("stroke-width")=="3");
    // Every stroke of every kind, in both themes and Midnight, with references, zones, trends, markers and panes.
    var extras=new[]{Spec() with{Annotations=[new(AnnotationAxis.Y,3){Label="Line"},new(AnnotationAxis.X,.5){To=1,Label="Band"}],YZones=Effort(),MinorGridlines=true,
            Series=[new("S",[new(0,110),new(1,150),new(2,130)]){Trend=true,Zones=Effort()},new("Hollow",[new(0,120),new(1,125),new(2,170)]){Markers=MarkerStyle.Hollow,HighlightLast=true}]},
        Spec(ChartKind.Scatter) with{Series=[new("S",[new(0,1),new(1,3),new(2,2)]){Trend=true,Markers=MarkerStyle.Hollow}]},Stacked() with{MinorGridlines=true}};
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight})
        foreach(var spec in Enum.GetValues<ChartKind>().Select(Sample).Concat(extras))
        {
            var doc=Svg(spec with{Style=style});
            Check(Stroked(doc).All(e=>(string?)e.Attribute("vector-effect")=="non-scaling-stroke"),$"{spec.Kind}: {Stroked(doc).FirstOrDefault(e=>e.Attribute("vector-effect") is null)}");
            // A hollow marker's ring is set on its group, so the circle it strokes carries the effect.
            Check(doc.Descendants(ns+"g").Where(g=>g.Attribute("stroke") is not null&&g.Attribute("data-point") is not null).All(g=>(string?)g.Element(ns+"circle")!.Attribute("vector-effect")=="non-scaling-stroke"),$"{spec.Kind}: a hollow ring scales");
            // Gridlines are hairlines that land on whole pixels; a radar's rings and spokes run at angles, where that would jag them.
            if(spec.Kind!=ChartKind.Radar) Check(GridStrokes(doc).All(l=>(string?)l.Attribute("shape-rendering")=="crispEdges"),$"{spec.Kind}: a gridline is not crisp");
        }
    var network=XDocument.Parse(GraphEngine.Render(new GraphSpec{Nodes=[new("a","A"),new("b","B"),new("c","C")],Edges=[new("a","b"),new("b","c"),new("a","c"),new("c","c")]}));
    Check(Stroked(network).Length>=6&&Stroked(network).All(e=>(string?)e.Attribute("vector-effect")=="non-scaling-stroke"),"a graph edge or node scales");
    // The classic finish leaves every stroke to scale, as 0.23.0 did.
    Check(Enum.GetValues<ChartKind>().All(kind=>!ChartSvg.Render(Classic(Sample(kind))).Contains("vector-effect")),"a classic chart carries the effect");
});
Test("Refined trend and reference lines are thinner than the data, and a projection keeps its 6 and 4 pixel rhythm with round ends",()=>{
    double Width(XElement e)=>Attr(e,"stroke-width");
    XElement Trend(XDocument doc)=>doc.Descendants(ns+"path").Single(p=>(string?)p.Attribute("class")=="lumen-trend");
    var spec=Spec() with{Series=[new("S",[new(0,1),new(1,3),new(2,2)]){Trend=true}]};
    var doc=Svg(spec);
    Check(Close(Width(Trend(doc)),1.2)&&Width(Trend(doc))<Width(StrokeOf(doc))&&(string?)Trend(doc).Attribute("vector-effect")=="non-scaling-stroke"&&(string?)Trend(doc).Attribute("stroke-dasharray")=="7 5");
    var wide=Svg(spec with{Series=[spec.Series[0] with{StrokeWidth=3}]});
    Check(Close(Width(Trend(wide)),2.25)&&Width(Trend(wide))<Width(StrokeOf(wide)),"a trend does not follow its series' width");
    Check(Close(Width(Trend(Svg(Spec(ChartKind.Scatter) with{Series=[new("S",[new(0,1),new(1,3),new(2,2)]){Trend=true}]}))),1.2),"a scatter trend is not 1.2");
    Check(Width(Trend(Svg(Classic(spec))))==2,"the classic trend moved");
    // A reference line is a 1-pixel guide over its invisible 12-pixel target, both keeping their width.
    var marked=Svg(Spec() with{Annotations=[new(AnnotationAxis.Y,3){Label="Target"}]});
    var lines=marked.Descendants(ns+"g").Single(g=>(string?)g.Attribute("aria-label")=="Target: 3").Elements(ns+"line").ToArray();
    Check(lines.Length==2&&Width(lines[0])==12&&(string?)lines[0].Attribute("stroke-opacity")=="0"&&Width(lines[1])==1&&Width(lines[1])<1.6&&(string?)lines[1].Attribute("stroke-dasharray")=="6 4"
        &&lines.All(l=>(string?)l.Attribute("vector-effect")=="non-scaling-stroke"),"a reference line is not a thin guide");
    // A round end adds half the width to each side of a dash, so the pattern gives it back: 4.4 on and 5.6 off draw 6 and 4.
    string Dash(double? width)=>Svg(Spec() with{Series=[new("S",[new(0,1),new(1,3),new(2,2)]){ProjectedFrom=1,StrokeWidth=width}]}).Descendants(ns+"path").Single(p=>p.Attribute("stroke-dasharray") is not null).Attribute("stroke-dasharray")!.Value;
    Check(Dash(null)=="4.4 5.6"&&Dash(3)=="3 7"&&Dash(8)=="0 12",$"{Dash(null)}, {Dash(3)}, {Dash(8)}");
    Check(Svg(Classic(Spec() with{Series=[new("S",[new(0,1),new(1,3),new(2,2)]){ProjectedFrom=1}]})).Descendants(ns+"path").Single(p=>p.Attribute("stroke-dasharray") is not null).Attribute("stroke-dasharray")!.Value=="6 4");
});
Test("Refined line markers appear on hover or focus, and every point keeps its focusable, labelled mark at the classic size",()=>{
    var spec=Spec() with{Series=[new("S",[new(0,2,"A"),new(1,5,"B"),new(2,3,"C")])]};
    XDocument refined=Svg(spec),classic=Svg(Classic(spec));
    XElement[] Groups(XDocument doc)=>doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
    Check(Groups(refined).Length==3&&Groups(refined).All(g=>(string?)g.Attribute("tabindex")=="0"&&(string?)g.Attribute("role")=="button"&&(string?)g.Attribute("class")=="lumen-datum"
        &&g.Element(ns+"title")!.Value==g.Attribute("aria-label")!.Value),"a point lost its focusable, labelled mark");
    Check(Groups(refined).Select(g=>(string)g.Attribute("aria-label")!).SequenceEqual(Groups(classic).Select(g=>(string)g.Attribute("aria-label")!)),"a point's name changed");
    // The marker is drawn in its colour where the classic one is and as large, so it is as easy to point at; the stylesheet hides it
    // until its point is hovered or focused, when it shows with the focus ring.
    var circles=Groups(refined).Select(g=>g.Element(ns+"circle")!).ToArray();var shown=Groups(classic).Select(g=>g.Element(ns+"circle")!).ToArray();
    Check(circles.All(c=>(string?)c.Attribute("class")=="lumen-marker"&&(string?)c.Attribute("fill")==ChartStyle.Light.Series[0]&&c.Attribute("fill-opacity") is null)
        &&circles.Zip(shown).All(p=>p.First.Attribute("cx")!.Value==p.Second.Attribute("cx")!.Value&&p.First.Attribute("cy")!.Value==p.Second.Attribute("cy")!.Value&&Attr(p.First,"r")>=Attr(p.Second,"r")),"a hidden marker is smaller or elsewhere");
    var sheet=refined.Descendants(ns+"style").Single().Value;
    Check(sheet.Contains(".lumen-svg .lumen-marker{opacity:0}")&&sheet.Contains(".lumen-svg .lumen-datum:hover .lumen-marker,.lumen-svg .lumen-datum:focus .lumen-marker{opacity:1}")&&sheet.Contains(".lumen-svg .lumen-datum:focus{stroke:currentColor;stroke-width:3}"),"the marker is not revealed on hover and focus");
    Check(!classic.Descendants(ns+"style").Single().Value.Contains("lumen-marker")&&!classic.Descendants().Any(e=>(string?)e.Attribute("class")=="lumen-marker"),"a classic marker is hidden");
    // A long line's markers are 2 pixels in both finishes, and an area's hide as a line's do.
    var dense=Spec() with{Series=[new("S",Enumerable.Range(0,100).Select(i=>new ChartPoint(i,i%7)).ToArray())]};
    Check(Svg(dense).Descendants(ns+"circle").All(c=>(string?)c.Attribute("r")=="2"&&(string?)c.Attribute("class")=="lumen-marker")&&Svg(Classic(dense)).Descendants(ns+"circle").All(c=>(string?)c.Attribute("r")=="2"));
    Check(Svg(Spec(ChartKind.Area)).Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).All(g=>(string?)g.Element(ns+"circle")!.Attribute("class")=="lumen-marker"));
    // Scatter points and bubbles are their marks and stay in view, and a marker style a series names keeps 0.23.0's meaning.
    foreach(var kind in new[]{ChartKind.Scatter,ChartKind.Bubble})
        Check(!Svg(Spec(kind)).Descendants().Any(e=>(string?)e.Attribute("class")=="lumen-marker")&&Svg(Spec(kind)).Descendants(ns+"circle").Any(c=>(string?)c.Attribute("fill-opacity")==".7"),$"{kind} hid its marks");
    string Marker(MarkerStyle style)=>Svg(spec with{Series=[spec.Series[0] with{Markers=style}]}).Descendants(ns+"g").First(g=>g.Attribute("data-point") is not null).Element(ns+"circle")!.ToString();
    string Before(MarkerStyle style)=>Svg(Classic(spec with{Series=[spec.Series[0] with{Markers=style}]})).Descendants(ns+"g").First(g=>g.Attribute("data-point") is not null).Element(ns+"circle")!.ToString();
    Check(Marker(MarkerStyle.Filled)==Before(MarkerStyle.Filled)&&Marker(MarkerStyle.None)==Before(MarkerStyle.None)&&!Marker(MarkerStyle.Hollow).Contains("lumen-marker"),"a named marker style changed its meaning");
    // The latest reading stays ringed, and a point between two gaps, which has no line to show it, keeps its marker in view.
    var ringed=Svg(spec with{Series=[spec.Series[0] with{HighlightLast=true}]}).Descendants(ns+"g").Last(g=>g.Attribute("data-point") is not null);
    Check(ringed.Elements(ns+"circle").Count()==2&&ringed.Elements(ns+"circle").All(c=>c.Attribute("class") is null),"the latest reading is hidden");
    var lone=Svg(Spec() with{Series=[new("S",[new(0,1),new(1,null),new(2,3),new(3,null),new(4,2),new(5,4)])]}).Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
    Check(lone.Length==4&&lone.Take(2).All(g=>g.Element(ns+"circle")!.Attribute("class") is null)&&lone.Skip(2).All(g=>(string?)g.Element(ns+"circle")!.Attribute("class")=="lumen-marker"),"a point between two gaps is hidden");
});
// The labelled ticks of a pane, and the spacing between them: Y labels sit 4 pixels below their tick, left of the plot or right of it.
double[] TickYs(XDocument doc,(double Top,double Bottom) span,double x)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-muted"&&t.Attribute("transform") is null
    &&Close(Attr(t,"x"),x)&&Attr(t,"y")-4>=span.Top-1e-6&&Attr(t,"y")-4<=span.Bottom+1e-6).Select(t=>Attr(t,"y")-4).Order().ToArray();
double Tightest(double[] ys)=>ys.Zip(ys.Skip(1)).Select(p=>p.Second-p.First).DefaultIfEmpty(double.PositiveInfinity).Min();
Test("Refined Y ticks stand at least 28 pixels apart in a short pane, as in the activity stream's pace pane, and are left alone with room",()=>{
    // The activity stream as 0.22.0 showed it: heart rate over its zones, pace on a reversed duration axis in a pane 93 pixels
    // tall, and the climb beneath, where five pace labels stood 19 pixels apart.
    var heart=ZoneScale.CogganHeartRate(170);
    double[] t=Enumerable.Range(0,360).Select(i=>i*10d).ToArray();
    double Hard(double s)=>s%900>300&&s%900<600?1:0;
    var stream=new ChartSpec{Kind=ChartKind.Line,XFormat=ValueFormat.Duration,YLabel="Heart rate (bpm)",YZones=heart,Height=520,
        Panes=[new(){Label="Pace (/km)",YFormat=ValueFormat.Duration,YReversed=true,Weight=.6},new(){Label="Elevation (m)",Weight=.45,Y2Label="Grade"}],
        Series=[new("Heart rate",t.Select((s,i)=>new ChartPoint(s,Math.Round(110+55*(1-Math.Exp(-s/500))+22*Hard(s)+i%4))).ToArray()){Zones=heart},
            new("Pace",t.Select((s,i)=>new ChartPoint(s,Math.Round(360-70*Hard(s)+i%12))).ToArray()){Pane=1},
            new("Elevation",t.Select(s=>new ChartPoint(s,Math.Round(40+25*Math.Sin(s/700)+10*Math.Sin(s/230),1))).ToArray()){Kind=ChartKind.Area,Pane=2},
            new("Grade",t.Select(s=>new ChartPoint(s,Math.Round(4*Math.Cos(s/700),2))).ToArray()){Pane=2,Secondary=true}]};
    XDocument refined=Svg(stream),classic=Svg(Classic(stream));
    var spans=PaneClips(refined).Select(PaneSpan).ToArray();
    Check(spans.Length==3&&spans[1].Bottom-spans[1].Top<100,$"the pace pane is {spans[1].Bottom-spans[1].Top} pixels");
    var pace=TickYs(classic,PaneClips(classic).Select(PaneSpan).ToArray()[1],64);
    Check(pace.Length==5&&Tightest(pace)<28,$"the classic pace pane has {pace.Length} labels {Tightest(pace):0.#} pixels apart");
    for(var k=0;k<3;k++)
    {
        var left=TickYs(refined,spans[k],64);
        Check(left.Length>=2&&Tightest(left)>=28,$"pane {k} labels {left.Length} ticks {Tightest(left):0.#} pixels apart");
    }
    Check(TickYs(refined,spans[1],64).Length<pace.Length,"the pace pane kept its crowded labels");
    // The right-hand axis of a short pane is spaced too, and its gridlines follow the ticks left of them.
    Check(Tightest(TickYs(refined,spans[2],836))>=28&&TickYs(refined,spans[2],836).Length>=2,"the right-hand axis is crowded");
    var grid=refined.Descendants(ns+"line").Where(l=>(string?)l.Attribute("class")=="lumen-grid"&&Attr(l,"y1")>=spans[1].Top-1e-6&&Attr(l,"y1")<=spans[1].Bottom+1e-6).Select(l=>Attr(l,"y1")).Order().ToArray();
    Check(grid.SequenceEqual(TickYs(refined,spans[1],64)),"the pace pane's gridlines are not at its ticks");
    // A box plot in a short chart, a logarithmic pane and a pane with minor lines are spaced the same way.
    var box=Svg(Sample(ChartKind.Box) with{Height=240,YMin=0,YMax=100});
    Check(Tightest(TickYs(box,(78,164),64))>=28&&TickYs(box,(78,164),64).Length>=2,"a short box plot is crowded");
    Check(Tightest(TickYs(Svg(Classic(Sample(ChartKind.Box) with{Height=240,YMin=0,YMax=100})),(78,164),64))<28,"the classic box plot was not crowded");
    var logged=Svg(Spec() with{Height=420,Panes=[new(){YAxis=AxisKind.Log,Weight=.3}],MinorGridlines=true,Series=[Spec().Series[0],new("Load",[new(0,1),new(1,40),new(2,3000)]){Pane=1}]});
    var logSpan=PaneClips(logged).Select(PaneSpan).ToArray()[1];
    Check(Tightest(TickYs(logged,logSpan,64))>=28&&TickYs(logged,logSpan,64).Length>=2,$"a short logarithmic pane is crowded: {Tightest(TickYs(logged,logSpan,64)):0.#}");
    // Over two decades a logarithmic axis marks 1, 2 and 5 of each, whatever it is asked for; too crowded, it keeps the decades.
    var decades=Svg(Spec() with{Height=420,Panes=[new(){YAxis=AxisKind.Log,Weight=.3}],Series=[Spec().Series[0],new("Load",[new(0,2),new(1,40),new(2,150)]){Pane=1}]});
    var decadeSpan=PaneClips(decades).Select(PaneSpan).ToArray()[1];
    var named=decades.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-muted"&&Close(Attr(t,"x"),64)&&Attr(t,"y")-4>=decadeSpan.Top-1e-6&&Attr(t,"y")-4<=decadeSpan.Bottom+1e-6).Select(t=>t.Value).ToArray();
    Check(named.SequenceEqual(["10","100"])&&Tightest(TickYs(decades,decadeSpan,64))>=28,$"a short two-decade pane labels {string.Join(",",named)}");
    // A plot with room keeps every tick it had.
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Column,ChartKind.Scatter,ChartKind.Area})
        Check(Ticks(Svg(Spec(kind)),"end").SequenceEqual(Ticks(Svg(Classic(Spec(kind))),"end")),$"{kind} lost a tick it had room for");
});
// The X labels along the bottom of the last pane, and whether any two touch, each at least the generous width of 12-pixel text.
(double X,string Text)[] Bottom(XDocument doc,double y)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="middle"&&(string?)t.Attribute("class")=="lumen-muted"&&Close(Attr(t,"y"),y)&&t.Value.Length>0)
    .Select(t=>(Attr(t,"x"),t.Value)).OrderBy(l=>l.Item1).ToArray();
bool Touching((double X,string Text)[] labels)=>labels.Zip(labels.Skip(1)).Any(p=>p.Second.X-p.First.X<(p.First.Text.Length+p.Second.Text.Length)/2d*.62*12);
Test("Refined X labels are thinned until no two touch",()=>{
    // Twelve zones in a column chart: names twelve characters long a category apart.
    string[] names=["Active recovery","Endurance","Tempo","Lactate threshold","VO2max","Anaerobic capacity","Neuromuscular","Sweet spot","Recovery ride","Over-unders","Threshold block","Race pace"];
    var zones=Spec(ChartKind.Column) with{Series=[new("Time",names.Select((n,i)=>new ChartPoint(i,10+i,n)).ToArray())]};
    Check(Touching(Bottom(Svg(Classic(zones)),365)),"the classic labels did not touch");
    var refined=Bottom(Svg(zones),365);
    Check(!Touching(refined)&&refined.Length>=4&&refined[0].Text=="Active reco…",string.Join("|",refined.Select(l=>l.Text)));
    // Half a year of weeks on a narrow time axis: a month apart, "Feb 2026" and "Mar 2026" touched; fewer ticks are asked for.
    var months=Spec() with{Width=320,XAxis=AxisKind.Time,Series=[new("S",Enumerable.Range(0,26).Select(i=>new ChartPoint(Utc(2026,1,5)+i*7*86_400_000d,i%5)).ToArray())]};
    Check(Touching(Bottom(Svg(Classic(months)),365)),"the classic month labels did not touch");
    var narrow=Svg(months);
    Check(!Touching(Bottom(narrow,365))&&Bottom(narrow,365).Length>=2,string.Join("|",Bottom(narrow,365).Select(l=>l.Text)));
    // Labelled points keep every fourth or fifth rather than every third.
    var sessions=Spec() with{Width=480,Series=[new("S",Enumerable.Range(0,20).Select(i=>new ChartPoint(i,i%4,$"Session {i+1}")).ToArray())]};
    Check(Touching(Bottom(Svg(Classic(sessions)),365)),"the classic session labels did not touch");
    var labelled=Svg(sessions);
    Check(!Touching(Bottom(labelled,365))&&Bottom(labelled,365).Length>=2,string.Join("|",Bottom(labelled,365).Select(l=>l.Text)));
    // Histogram edges already stand 70 pixels apart, wider than any edge's label, in both finishes.
    var bins=Svg(Spec(ChartKind.Histogram) with{Width=360,Bins=30,Series=[new("S",Enumerable.Range(0,300).Select(i=>new ChartPoint(i,Math.Round(1000+i*3.3333,3))).ToArray())]});
    Check(!Touching(Bottom(bins,365))&&Bottom(bins,365).Length>=2,string.Join("|",Bottom(bins,365).Select(l=>l.Text)));
    // Labels with room are left as they were.
    Check(Bottom(Svg(Spec(ChartKind.Column)),365).SequenceEqual(Bottom(Svg(Classic(Spec(ChartKind.Column))),365)),"a category label with room went");
});
// A refined reference label: drawn over the data, with the reference's own group carrying its name.
XElement[] Halos(XDocument doc)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("paint-order")=="stroke").ToArray();
// A generous box for an 11-pixel label at its anchor: 9 above the baseline, 3 below, at most .9 em a character across.
(double Left,double Right,double Top,double Bottom) Box(XElement label)
{
    var width=label.Value.Sum(c=>c is 'm' or 'M' or 'w' or 'W'?.9:.62)*11;
    var x=Attr(label,"x");var anchor=(string?)label.Attribute("text-anchor");
    var left=anchor=="end"?x-width:anchor=="middle"?x-width/2:x;
    return (left,left+width,Attr(label,"y")-9,Attr(label,"y")+3);
}
bool Overlap((double Left,double Right,double Top,double Bottom) a,(double Left,double Right,double Top,double Bottom) b)=>a.Left<b.Right&&b.Left<a.Right&&a.Top<b.Bottom&&b.Top<a.Bottom;
Test("Refined reference labels sit inside their band and the plot, over the data with a halo, or are left out",()=>{
    // Heart rate from 119, so the easy zone, up to 120, is a band 3.5 pixels tall at the foot of the plot.
    var zoned=Spec() with{YZones=Effort(),Series=[new("Heart rate",Enumerable.Range(0,20).Select(i=>new ChartPoint(i,119+i*4)).ToArray())]};
    XDocument classic=Svg(Classic(zoned)),refined=Svg(zoned);
    var clip=PaneClips(refined).Single();var (top,bottom)=PaneSpan(clip);
    XElement Band(XDocument doc,string name)=>doc.Descendants(ns+"g").Single(g=>((string?)g.Attribute("aria-label"))?.StartsWith(name)==true&&(string?)g.Attribute("role")=="img");
    // 0.23.0 wrote the easy zone's label below the plot, where the clip, 6 pixels past it, cut the label off.
    var easy=Band(classic,"Easy").Element(ns+"text")!;
    Check(Attr(easy,"y")>bottom&&Attr(easy,"y")+3>bottom+6,$"the classic easy label was inside the plot, at {Attr(easy,"y")}");
    // Refined, a band too thin for its label leaves it out; its group is still named, for the tooltip and assistive technology.
    Check(Attr(Band(refined,"Easy").Element(ns+"rect")!,"height")<12&&!Halos(refined).Any(t=>t.Value.StartsWith("Easy"))&&Band(refined,"Easy").Attribute("aria-label")!.Value=="Easy: up to 120");
    var labels=Halos(refined);
    Check(labels.Length==3,string.Join("|",labels.Select(t=>t.Value)));
    foreach(var label in labels)
    {
        var rect=Band(refined,label.Value).Element(ns+"rect")!;var box=Box(label);
        Check(box.Top>=Attr(rect,"y")&&box.Bottom<=Attr(rect,"y")+Attr(rect,"height")&&box.Top>=top&&box.Bottom<=bottom&&box.Left>=76&&box.Right<=870,$"{label.Value} leaves its band or the plot");
        // A halo in the background colour, under the fill, so the label reads across a line; the pointer passes through to the marks.
        Check((string?)label.Attribute("stroke")=="#FFFFFF"&&(string?)label.Attribute("stroke-width")=="3"&&(string?)label.Attribute("stroke-linejoin")=="round"
            &&(string?)label.Attribute("pointer-events")=="none"&&(string?)label.Attribute("aria-hidden")=="true"&&(string?)label.Attribute("fill")==ChartStyle.Light.Text,label.ToString());
        Check(Band(refined,label.Value).Element(ns+"text") is null,"a band's group still holds its label");
    }
    // The labels follow the data in the pane's clip, so they are drawn over it.
    var order=clip.Descendants().ToList();
    Check(labels.All(l=>l.Parent==clip&&order.IndexOf(l)>order.FindLastIndex(e=>e.Attribute("data-point") is not null)),"a label is under the data or outside the clip");
    Check((string?)Halos(Svg(zoned with{Theme=ChartTheme.Dark}))[0].Attribute("stroke")==ChartStyle.Dark.Background&&(string?)Halos(Svg(zoned with{Style=ChartStyle.Midnight}))[0].Attribute("stroke")=="#0B0E14","the halo is not the background");
    // A line's label sits just above it, or just below where the plot ends above it; a line off the plot is not labelled.
    double Y(double value)=>344-(value-2)/3*266;
    var lines=Svg(Spec() with{Annotations=[new(AnnotationAxis.Y,3.5){Label="Middle"},new(AnnotationAxis.Y,4.95){Label="Top"},new(AnnotationAxis.Y,9){Label="Off"}]});
    XElement Line(string name)=>Halos(lines).Single(t=>t.Value.StartsWith(name));
    Check(Close(Attr(Line("Middle"),"y"),Y(3.5)-6)&&Close(Attr(Line("Top"),"y"),Y(4.95)+13)&&!Halos(lines).Any(t=>t.Value.StartsWith("Off")),string.Join("|",Halos(lines).Select(t=>$"{t.Value} {t.Attribute("y")}")));
    // An upright band labels inside itself where it is wide enough and is left out where it is not; an upright line at the
    // right edge is labelled on its left.
    var upright=Svg(Spec() with{Annotations=[new(AnnotationAxis.X,0){To=1,Label="Wide"},new(AnnotationAxis.X,1.2){To=1.3,Label="Narrow window"},new(AnnotationAxis.X,1.95){Label="Late"}]});
    var wide=Halos(upright).Single(t=>t.Value.StartsWith("Wide"));
    Check(Attr(wide,"x")==82&&(string?)wide.Attribute("text-anchor")=="start"&&Box(wide).Right<=76+397,"the wide band's label is not inside it");
    Check(!Halos(upright).Any(t=>t.Value.StartsWith("Narrow")),"the narrow band's label runs out of it");
    var late=Halos(upright).Single(t=>t.Value.StartsWith("Late"));
    Check((string?)late.Attribute("text-anchor")=="end"&&Box(late).Right<=76+794*1.95/2&&Box(late).Left>=76,"the late line's label leaves the plot");
});
Test("Refined labels on one edge are nudged apart rather than drawn over each other",()=>{
    // Weekly load against an average and a target band whose top lies just above it: 0.23.0 wrote the two labels over each other.
    var weekly=Spec(ChartKind.Column) with{YMin=0,YMax=600,Annotations=[new(AnnotationAxis.Y,400){To=500,Label="Target"},new(AnnotationAxis.Y,475){Label="Average"}],
        Series=[new("Load",Enumerable.Range(0,12).Select(i=>new ChartPoint(i,380+i*15,$"W{i+1}")).ToArray())]};
    var classic=Svg(Classic(weekly));
    XElement Old(string name)=>classic.Descendants(ns+"g").Single(g=>((string?)g.Attribute("aria-label"))?.StartsWith(name)==true).Element(ns+"text")!;
    Check(Overlap(Box(Old("Target")),Box(Old("Average"))),"the classic labels did not collide");
    var refined=Halos(Svg(weekly));
    Check(refined.Length==2&&!Overlap(Box(refined[0]),Box(refined[1])),string.Join("|",refined.Select(t=>$"{t.Value} {t.Attribute("y")}")));
    double Y(double value)=>344-value/600*266;
    var target=Box(refined.Single(t=>t.Value.StartsWith("Target")));var average=refined.Single(t=>t.Value.StartsWith("Average"));
    Check(target.Top>=Y(500)&&target.Bottom<=Y(400),"the target's label left its band");
    Check(Close(Attr(average,"y"),Y(475)-6),"the average's label left its line");
    // Two upright lines close together: the second label drops a row instead of writing over the first.
    var events=Svg(Spec() with{Annotations=[new(AnnotationAxis.X,.5){Label="Start"},new(AnnotationAxis.X,.55){Label="Restart"}]});
    var oldEvents=Svg(Classic(Spec() with{Annotations=[new(AnnotationAxis.X,.5){Label="Start"},new(AnnotationAxis.X,.55){Label="Restart"}]})).Descendants(ns+"text").Where(t=>t.Value.Contains("tart: ")).ToArray();
    Check(Overlap(Box(oldEvents[0]),Box(oldEvents[1])),"the classic event labels did not collide");
    var halos=Halos(events);
    Check(halos.Length==2&&!Overlap(Box(halos[0]),Box(halos[1]))&&Close(Attr(halos[1],"y")-Attr(halos[0],"y"),14),string.Join("|",halos.Select(t=>$"{t.Value} {t.Attribute("y")}")));
    // Across many references every label shown is clear of every other.
    var crowded=Halos(Svg(Spec() with{YZones=Effort(),Annotations=Enumerable.Range(0,8).Select(i=>new ChartAnnotation(AnnotationAxis.Y,121+i*9){Label=$"Line {i}"}).Append(new(AnnotationAxis.X,.2){To=.9,Label="Window"}).Append(new(AnnotationAxis.X,1.5){Label="Event"}).ToArray(),
        Series=[new("S",Enumerable.Range(0,10).Select(i=>new ChartPoint(i/5d,110+i*9)).ToArray())]}));
    Check(crowded.Length>=6&&crowded.SelectMany((a,i)=>crowded.Skip(i+1).Select(b=>(a,b))).All(p=>!Overlap(Box(p.a),Box(p.b))),"two labels overlap");
});
// The legend key of each series: the elements drawn between its row's start and its name, under the plot.
XElement[] Key(XDocument doc,int index)
{
    var names=doc.Root!.Elements(ns+"text").Where(t=>(string?)t.Attribute("font-size")=="11"&&Attr(t,"y")>420).ToArray();
    var name=names[index];var shapes=new List<XElement>();
    for(var e=name.ElementsBeforeSelf().LastOrDefault();e is not null&&e.Name!=ns+"text"&&e.Name!=ns+"svg";e=e.ElementsBeforeSelf().LastOrDefault()) shapes.Insert(0,e);
    return shapes.ToArray();
}
Test("Refined legend keys take the shape of their marks: a line, a dot or a square, dashed where the whole series is projected",()=>{
    var mixed=Spec() with{Y2Label="Rate",Series=[new("Line",[new(0,1),new(1,2),new(2,3)]),new("Plan",[new(0,1),new(1,2),new(2,4)]){ProjectedFrom=0},new("Part plan",[new(0,1),new(1,2),new(2,4)]){ProjectedFrom=1},
        new("Dots",[new(0,2),new(1,1),new(2,2)]){Kind=ChartKind.Scatter},new("Bars",[new(0,2),new(1,1),new(2,2)]){Kind=ChartKind.Column},new("Fill",[new(0,1),new(1,1),new(2,2)]){Kind=ChartKind.Area,Secondary=true},
        new("Range",[ChartPoint.Interval(0,1,0,2),ChartPoint.Interval(1,2,1,3),ChartPoint.Interval(2,2,1,3)]){Kind=ChartKind.Band}]};
    var doc=Svg(mixed);
    string Shape(int i)=>string.Join(",",Key(doc,i).Select(e=>e.Name.LocalName));
    Check(Enumerable.Range(0,7).Select(Shape).SequenceEqual(["line","line","line","circle","rect","rect","rect"]),string.Join("|",Enumerable.Range(0,7).Select(Shape)));
    var line=Key(doc,0).Single();
    Check((string?)line.Attribute("stroke")==ChartStyle.Light.Series[0]&&(string?)line.Attribute("stroke-width")=="2"&&(string?)line.Attribute("stroke-linecap")=="round"&&line.Attribute("stroke-dasharray") is null&&Attr(line,"y1")==Attr(line,"y2"),line.ToString());
    Check((string?)Key(doc,1).Single().Attribute("stroke-dasharray")=="1 4"&&Key(doc,2).Single().Attribute("stroke-dasharray") is null,"a projection is not dashed only when it covers the whole series");
    Check((string?)Key(doc,3).Single().Attribute("fill")==ChartStyle.Light.Series[3]&&(string?)Key(doc,3).Single().Attribute("r")=="4.5");
    Check(Key(doc,4).Concat(Key(doc,5)).Concat(Key(doc,6)).All(r=>(string?)r.Attribute("width")=="9"&&(string?)r.Attribute("height")=="9"&&(string?)r.Attribute("rx")=="2"));
    // Every key sits in the 14-pixel box before its name, which starts 20 pixels into the column.
    var names=doc.Root!.Elements(ns+"text").Where(t=>(string?)t.Attribute("font-size")=="11"&&Attr(t,"y")>420).ToArray();
    Check(names.Take(4).Select(t=>Attr(t,"x")).SequenceEqual([44d,257,470,683]),string.Join(",",names.Select(t=>Attr(t,"x"))));
    // Scatter and bubble charts draw dots, column, bar, area, histogram and radar charts squares.
    foreach(var (kind,shape) in new[]{(ChartKind.Scatter,"circle"),(ChartKind.Bubble,"circle"),(ChartKind.Column,"rect"),(ChartKind.Bar,"rect"),(ChartKind.StackedColumn,"rect"),(ChartKind.Area,"rect"),(ChartKind.Radar,"rect")})
        Check(Key(Svg(Baseline(kind,ChartTheme.Light)),0).Single().Name.LocalName==shape,$"{kind}");
    Check(Key(Svg(Baseline(ChartKind.Histogram,ChartTheme.Light) with{Series=[..Baseline(ChartKind.Histogram,ChartTheme.Light).Series,new("T",[new(0,1),new(1,2)])]}),1).Single().Name.LocalName=="rect");
    // The classic finish keeps 0.23.0's square in the series colour, whatever the mark.
    var classic=Svg(Classic(mixed));
    Check(Enumerable.Range(0,7).All(i=>Key(classic,i).Single() is var r&&r.Name==ns+"rect"&&(string?)r.Attribute("fill")==ChartStyle.Light.SeriesColor(i)),"a classic key is not a square");
});
Test("A series drawn in colours of its own shows them in its legend key, side by side, instead of a series colour it never draws",()=>{
    // Time in zone: every bar in its zone's colour, so the key is split into the first four of them.
    var heart=ZoneScale.CogganHeartRate(170);
    var zones=Spec(ChartKind.Bar) with{Series=[new("Time in zone",heart.Zones.Select((z,i)=>new ChartPoint(i,60+i*30,z.Name){Color=ChartStyle.Light.Zones[i]}).ToArray())]};
    var key=Key(Svg(zones),0);
    Check(key.Length==4&&key.All(r=>r.Name==ns+"rect"&&(string?)r.Attribute("height")=="9")&&key.Select(r=>(string?)r.Attribute("fill")).SequenceEqual(ChartStyle.Light.Zones.Take(4)),string.Join(",",key.Select(r=>r.ToString())));
    Check(key.Zip(key.Skip(1)).All(p=>Close(Attr(p.First,"x")+Attr(p.First,"width"),Attr(p.Second,"x")))&&Close(key.Sum(r=>Attr(r,"width")),14),"the segments do not fill the key side by side");
    Check(!key.Any(r=>(string?)r.Attribute("fill")==ChartStyle.Light.Series[0]),"the key still shows the series colour");
    // Repeated colours count once, in order; a point without a colour leaves the key in the series colour; one colour throughout is that colour.
    var repeated=Key(Svg(zones with{Series=[zones.Series[0] with{Points=zones.Series[0].Points.Select((p,i)=>p with{Color=i%2==0?"#123456":"#ABCDEF"}).ToArray()}]}),0);
    Check(repeated.Select(r=>(string?)r.Attribute("fill")).SequenceEqual(["#123456","#ABCDEF"]),"repeated colours are not counted once");
    Check((string?)Key(Svg(zones with{Series=[zones.Series[0] with{Points=[..zones.Series[0].Points.Take(4),zones.Series[0].Points[4] with{Color=null}]}]}),0).Single().Attribute("fill")==ChartStyle.Light.Series[0]);
    Check((string?)Key(Svg(zones with{Series=[zones.Series[0] with{Points=zones.Series[0].Points.Select(p=>p with{Color="#123456"}).ToArray()}]}),0).Single().Attribute("fill")=="#123456");
    // A line coloured by grade splits its line key; candles show their rising and falling colours.
    var grade=Key(Svg(Spec() with{Series=[new("Elevation",[new(0,1){Color="#2E9B58"},new(1,2){Color="#DB6A1F"},new(2,3){Color="#DD4B45"}])]}),0);
    Check(grade.Length==3&&grade.All(l=>l.Name==ns+"line")&&grade.Select(l=>(string?)l.Attribute("stroke")).SequenceEqual(["#2E9B58","#DB6A1F","#DD4B45"]),"a grade-coloured line's key is not split");
    Check(Key(Svg(Sample(ChartKind.Candlestick)),0).Select(r=>(string?)r.Attribute("fill")).SequenceEqual([ChartStyle.Light.Rising,ChartStyle.Light.Falling]),"the candles' key is not rising and falling");
    // The classic finish keeps 0.23.0's misleading square in the series colour.
    Check((string?)Key(Svg(Classic(zones)),0).Single().Attribute("fill")==ChartStyle.Light.Series[0]);
});
Test("The component's legend draws the chart's own keys in the refined finish and 0.23.0's dots in the classic",()=>{
    var spec=Spec() with{Series=[new("Line",[new(0,1),new(1,2)]),new("Dots",[new(0,2),new(1,1)]){Kind=ChartKind.Scatter},new("Bars",[new(0,2),new(1,1)]){Kind=ChartKind.Column},
        new("Zones",[new(0,2){Color="#123456"},new(1,1){Color="#ABCDEF"}]){Kind=ChartKind.Column},new("Plan",[new(0,3),new(1,4)]){ProjectedFrom=0}]};
    var html=RenderInside(null,spec);
    var chart=Svg(spec);
    for(var i=0;i<spec.Series.Count;i++)
    {
        // The legend draws exactly the key ChartSvg.LegendKey returns, and it is the chart's own key, moved to the origin.
        var key=ChartSvg.LegendKey(spec,i);
        Check(html.Contains(key),$"series {i}'s button does not draw its key");
        var own=XDocument.Parse(key).Root!.Elements().ToArray();var drawn=Key(chart,i);
        Check(own.Length==drawn.Length&&own.Zip(drawn).All(p=>p.First.Name==p.Second.Name&&(string?)p.First.Attribute("fill")==(string?)p.Second.Attribute("fill")&&(string?)p.First.Attribute("stroke")==(string?)p.Second.Attribute("stroke")
            &&(string?)p.First.Attribute("stroke-dasharray")==(string?)p.Second.Attribute("stroke-dasharray")),$"series {i}'s key differs from the chart's");
        Check((string?)XDocument.Parse(key).Root!.Attribute("aria-hidden")=="true"&&(string?)XDocument.Parse(key).Root!.Attribute("viewBox")=="0 0 14 9");
    }
    Check(!html.Contains("<span style=\"background:"),"a refined legend still draws dots");
    // A cascaded brand reaches the keys; a donut's one series shows its slices, which the chart draws in their own colours.
    Check(RenderInside(ChartStyle.Midnight,spec).Contains(ChartSvg.LegendKey(spec with{Style=ChartStyle.Midnight},0))&&ChartSvg.LegendKey(spec with{Style=ChartStyle.Midnight},0).Contains(ChartStyle.Midnight.Series[0]),"the cascaded brand did not reach the legend");
    var donut=Sample(ChartKind.Donut) with{Series=[new("Mix",[new(0,3,"A"),new(1,2,"B"){Color="#123456"},new(2,1,"C")])]};
    Check(RenderInside(null,donut).Contains(ChartSvg.LegendKey(donut,0))&&XDocument.Parse(ChartSvg.LegendKey(donut,0)).Root!.Elements().Select(e=>(string?)e.Attribute("fill")).SequenceEqual([ChartStyle.Light.Series[0],"#123456",ChartStyle.Light.Series[2]]),"the donut's key is not its slices");
    // A heatmap row is drawn in the ramp from low to high, so that is its key.
    Check(XDocument.Parse(ChartSvg.LegendKey(Sample(ChartKind.Heatmap),0)).Root!.Elements().Select(e=>(string?)e.Attribute("fill")).SequenceEqual([ChartStyle.Light.HeatmapLow,ChartStyle.Light.HeatmapHigh]),"a heatmap row's key is not its ramp");
    // The classic finish keeps the dot it always drew, in the series colour.
    var classic=RenderInside(ChartStyle.Light with{Finish=ChartFinish.Classic},spec);
    Check(Enumerable.Range(0,spec.Series.Count).All(i=>classic.Contains($"<span style=\"background:{ChartStyle.Light.SeriesColor(i)}\"></span>"))&&!classic.Contains("viewBox='0 0 14 9'"),"the classic legend changed");
    // The stylesheet sets the key beside its name.
    var css=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Lumen.Charts.Blazor/wwwroot/lumen.css"));
    Check(css.Contains(".lumen-legend svg{")&&css.Contains(".lumen-legend span{display:inline-block;width:8px;height:8px;border-radius:50%;margin-right:7px}"));
});
// The gallery's Sports & performance page draws one simulated athlete, so its charts must agree with one another.
var sports=SportsData.Cards(ChartTheme.Light,ChartStyle.Light.Zones);
ChartSpec Sports(string id)=>sports.Single(card=>card.Id==id).Spec;
var athlete=SportsData.Season;var latest=athlete.Sessions[^1];
DateOnly DayOf(double x)=>DateOnly.FromDateTime(TimeAxis.Moment(x).UtcDateTime);
Test("Sports page: thirty-one charts in twenty-eight cards, each rendering in light, dark and Midnight at a desktop's and a phone's widths",()=>{
    // 0.34.0's Getting faster? card draws three sparklines in place of one chart; they are checked on their own below. 0.35.0 adds
    // How the field finished to the Racing section, 0.37.0 Ride channels in a Long ride section of its own, and 0.38.0 Season arc and Gap to
    // the leader to the Racing section, 0.39.0 Time in zone, as shares, and Session scores to the Latest session section, and 0.40.0
    // Heart rate by lap to the Latest session section and Best efforts to the Fitness section.
    Check(sports.Count==28&&sports.Select(card=>card.Id).Distinct().Count()==28&&sports.Count(card=>card.Beside is not null)==1&&sports.Count(card=>card.Lines is not null)==1
        &&sports.Sum(card=>card.Lines?.Count??(card.Beside is null?1:2))==31,"the page should have thirty-one charts in twenty-eight cards");
    // 0.27.0 added the Sleep and recovery section last, so the twelve before it keep their order; 0.33.0's Racing section stands
    // before it.
    Check(sports.TakeLast(3).Select(card=>(card.Section,card.Id,card.Spec.Kind)).SequenceEqual([("sleep","hypnogram",ChartKind.Timeline),("sleep","sleep-timing",ChartKind.Range),("sleep","heart-range",ChartKind.Range)]),"the sleep section is not last");
    foreach(var (theme,style,zones) in new[]{(ChartTheme.Light,(ChartStyle?)null,ChartStyle.Light.Zones),(ChartTheme.Dark,null,ChartStyle.Light.Zones),(ChartTheme.Dark,ChartStyle.Midnight,ChartStyle.Midnight.Zones)})
        foreach(var markers in new[]{true,false})
            foreach(var card in SportsData.Cards(theme,zones,markers).Where(card=>card.Lines is null))
                // A wide card with a second chart beside its first gives each half its width.
                foreach(var chart in new[]{card.Spec,card.Beside}.OfType<ChartSpec>())
                {
                    Check(chart.Width==(card.Wide&&card.Beside is null?1100:540)&&(chart.Source.Contains("simulated")||chart.Source.Contains("invented")),$"{card.Id} is not drawn at a desktop's width before it is fitted, or does not say it is simulated or invented");
                    // The page's charts set FitWidth, which draws each at the width its card gives it, as here.
                    foreach(var width in new[]{chart.Width,337})
                        Check(XDocument.Parse(ChartSvg.Render(chart with{Width=width,Style=style})).Descendants().Any(e=>e.Attribute("data-point") is not null),$"{card.Id} drew no marks {width} wide");
                }
    Check(Sports("stream").Annotations.Count==3&&SportsData.Cards(ChartTheme.Light,ChartStyle.Light.Zones,markers:false).Single(card=>card.Id=="stream").Spec.Annotations.Count==0,"the stream's markers do not follow the page");
});
Test("Sports page: this morning's readiness reads the HRV and form the other charts draw, and the day's rings are the last run",()=>{
    Check(sports.Take(2).Select(card=>(card.Section,card.Id,card.Spec.Kind)).SequenceEqual([("today","readiness",ChartKind.Gauge),("today","activity",ChartKind.Ring)]),"the Today row is not first");
    var gauge=Sports("readiness");var score=gauge.Series.Single().Points.Single().Y!.Value;
    // Last night is the HRV chart's last night, measured against the band that chart draws round it, and today's form is the
    // performance chart's. The band is rounded to a tenth, so the score is checked to within a point.
    var band=Sports("hrv").Series[0].Points[^1];var deviation=(band.High!.Value-band.Low!.Value)/2;
    var today=SportsData.When(SportsData.Today);
    var form=Sports("performance").Series.Single(s=>s.Name=="Form").Points.Single(p=>p.X==today).Y!.Value;
    Check(Math.Abs(score-Math.Clamp(Math.Round(60+10*(athlete.Hrv[^1]-band.Y!.Value)/deviation+.5*form),0,100))<=1,$"readiness {score} does not follow last night's HRV and today's form");
    var scores=SportsData.Readiness(athlete);
    Check(scores.Count==SportsData.Weeks*7&&scores[^1]==score&&gauge.Annotations.Single().From==Math.Round(scores.SkipLast(1).TakeLast(SportsData.BaselineNights).Average()),"the tick is not the 28 days before");
    Check(gauge.YZones!.Zones.Select(z=>z.Upper).SequenceEqual([33,66,double.PositiveInfinity])&&gauge.Title==$"Readiness {score}, {gauge.YZones.Zones[gauge.YZones.IndexOf(score)].Name.ToLowerInvariant()}","the tiers or the title disagree with the score");
    // The rings are the last run: its active calories, its minutes and the stress the performance chart scores for today,
    // against yesterday's fitness.
    var rings=Sports("activity").Series;
    var daily=Sports("performance").Series.Single(s=>s.Name=="Daily stress").Points.Single(p=>p.X==today).Y;
    Check(rings.Select(s=>(s.Name,s.Points.Single().Label)).SequenceEqual([("Move","kcal"),("Exercise","min"),("Stress","TSS")]),"the rings are not move, exercise and stress");
    Check(rings[0].Points[0].Y==Math.Round(latest.Metres/1000*SportsData.BodyMass)&&rings[1].Points[0].Y==Math.Round(latest.Seconds/60)&&Sports("stream").Series[0].Points.Count*SportsData.RunSample==latest.Seconds,"move or exercise is not the run");
    Check(rings[2].Points[0].Y==daily&&daily==latest.Stress&&rings[2].Goal==Math.Round(athlete.Load.Single(d=>d.Day==SportsData.Today.AddDays(-1)).Fitness),"stress is not today's against yesterday's fitness");
    Check(Sports("activity").Title==$"{rings.Count(r=>r.Points[0].Y>=r.Goal)} of 3 rings closed"&&rings.Any(r=>r.Points[0].Y>r.Goal),"the title miscounts, or no ring runs past its goal");
});
Test("Sports page: the Today row's zone and ring colours clear 3:1 on every brand's background, and its score's text 4.5:1",()=>{
    foreach(var (style,zones) in new[]{(ChartStyle.Light,ChartStyle.Light.Zones),(ChartStyle.Dark,ChartStyle.Light.Zones),(Brand(),Brand().Zones),(ChartStyle.Midnight,ChartStyle.Midnight.Zones)})
        foreach(var card in SportsData.Cards(ChartTheme.Light,zones).Where(card=>card.Section=="today"))
        {
            var inks=card.Spec.Series.Select(s=>s.Color).Concat(card.Spec.YZones?.Zones.Select(z=>z.Color)??[]).OfType<string>().ToArray();
            Check(inks.Length>=3&&inks.All(ink=>Contrast(ink,style.Background)>=3),$"{card.Id} on {style.Background}: {string.Join(", ",inks.Where(ink=>Contrast(ink,style.Background)<3))}");
            Check(Contrast(style.Text,style.Background)>=4.5&&Svg(card.Spec with{Style=style}).Descendants().Any(e=>e.Attribute("data-point") is not null));
        }
});
Test("Sports page: the stream's run is a day of the performance chart, at the stress it scored, and every day is its sessions",()=>{
    var daily=Sports("performance").Series.Single(s=>s.Name=="Daily stress").Points;
    Check(athlete.Sessions.Count(s=>s.Day==latest.Day)==1&&daily.Single(p=>p.X==SportsData.When(latest.Day)).Y==latest.Stress,"the run's day does not carry its stress");
    Check(latest.Day==SportsData.Today&&Sports("stream").Description.StartsWith("Sunday 27 September"),"the stream is not the last day trained");
    foreach(var p in daily)
        Check(p.Y==athlete.Sessions.Where(s=>s.Day==DayOf(p.X)).Sum(s=>s.Stress)+athlete.Planned.Where(d=>d.Day==DayOf(p.X)).Sum(d=>d.Stress),$"{DayOf(p.X)} is not the sum of its sessions");
    Check(DayOf(daily[0].X)==SportsData.Start&&DayOf(daily[^1].X)==SportsData.Race&&athlete.Planned.All(d=>d.Day>SportsData.Today));
});
Test("Sports page: time in zone counts the heart rate the stream draws, in the zones the stream is coloured by",()=>{
    var stream=Sports("stream");var heart=stream.Series.Single(s=>s.Name=="Heart rate");
    var bars=Sports("time-in-zone").Series.Single().Points;
    var counted=Training.TimeInZone(heart.Points.Select(p=>p.Y!.Value).ToArray(),SportsData.HeartZones,SportsData.RunSample);
    Check(bars.Select(p=>p.Y!.Value).SequenceEqual(counted)&&counted.SequenceEqual(latest.TimeInZone),"the bars are not the stream's time in zone");
    Check(bars.Sum(p=>p.Y)==latest.Seconds&&heart.Points.Count*SportsData.RunSample==latest.Seconds,"the bars do not add up to the run");
    Check(stream.YZones==SportsData.HeartZones&&heart.Zones==SportsData.HeartZones&&bars.Select(p=>p.Color).SequenceEqual(ChartStyle.Light.Zones.Take(5)),"the bars and the stream use different zones");
    Check(Sports("time-in-zone").YMax>bars.Max(p=>p.Y),"the longest bar leaves no room for its value");
});
Test("Sports page: each week's load adds up its days in the performance chart, inside a band of 80 to 130 % of the four weeks before",()=>{
    var daily=Sports("performance").Series.Single(s=>s.Name=="Daily stress").Points;
    var weekly=Sports("weekly-load");var load=weekly.Series[0].Points;var band=weekly.Series[1].Points;
    Check(load.Count==SportsData.Weeks&&band.Count==SportsData.Weeks&&weekly.Series[1].Kind==ChartKind.Band);
    for(var w=0;w<SportsData.Weeks;w++)
    {
        Check(load[w].Y==daily.Skip(7*w).Take(7).Sum(p=>p.Y),$"week {w} is not the sum of its days");
        var prior=Enumerable.Range(w-4,4).Average(k=>k<0?SportsData.SeedFitness*7:load[k].Y!.Value);
        Check(band[w].Y==Math.Round(prior)&&band[w].Low==Math.Round(prior*.8)&&band[w].High==Math.Round(prior*1.3),$"week {w}'s band is not the four weeks before it");
    }
});
Test("Sports page: the weekly zones add up every session of the week, the stream's run included",()=>{
    var stacked=Sports("weekly-zones").Series;var bars=Sports("time-in-zone").Series.Single().Points;
    Check(stacked.Select(s=>s.Name).SequenceEqual(SportsData.HeartZones.Zones.Select(z=>z.Name))&&stacked.Select(s=>s.Color).SequenceEqual(ChartStyle.Light.Zones.Take(5)));
    for(var w=0;w<SportsData.Weeks;w++)
        Check(stacked.Sum(s=>s.Points[w].Y)==athlete.Sessions.Where(s=>(s.Day.DayNumber-SportsData.Start.DayNumber)/7==w).Sum(s=>s.Seconds),$"week {w}'s zones do not add up to its sessions");
    Check(Enumerable.Range(0,5).All(z=>stacked[z].Points[^1].Y>=bars[z].Y),"the last week holds less time in a zone than its last run");
});
Test("Sports page: the kilometre splits, the elevation profile and the stream are one run",()=>{
    var track=latest.Track!;var splits=Sports("splits").Series[0].Points.Select(p=>p.Y!.Value).ToArray();
    Check(splits.Length==(int)(latest.Metres/1000)&&Math.Abs(splits.Sum()-SportsData.TimeAt(track,latest.Metres,splits.Length*1000))<=splits.Length*.5+.5,"the splits do not add up to the run");
    var profile=Sports("elevation").Series[0].Points;var drawn=Sports("stream").Series.Single(s=>s.Name=="Elevation").Points.Select(p=>p.Y!.Value).ToArray();
    Check(latest.Metres-profile[^1].X*1000 is >=0 and <100&&profile.Min(p=>p.Y)>=drawn.Min()-.05&&profile.Max(p=>p.Y)<=drawn.Max()+.05,"the profile is not the stream's route");
    Check(profile.All(p=>p.Color is not null&&p.Label!.Contains("grade")),"a stretch of the profile is not coloured and named by its grade");
    Check(Sports("stream").Series.Single(s=>s.Name=="Pace").Points.Count==track.Pace.Count&&track.Distance.Zip(track.Distance.Skip(1)).All(d=>d.Second>d.First),"the stream does not draw every sample in order");
});
Test("Sports page: the 5 km record only falls, and no run beats the record of its day",()=>{
    var spec=Sports("records");var record=spec.Series[0].Points;var weekly=spec.Series[1].Points;
    Check(spec.YReversed&&record[0].Y==SportsData.PriorRecord&&record[^1].X==SportsData.When(SportsData.Today));
    Check(record.Zip(record.Skip(1)).All(p=>p.Second.Y<=p.First.Y&&p.Second.X>p.First.X),"the record rises");
    double Held(double x)=>record.Last(r=>r.X<=x).Y!.Value;
    Check(athlete.Sessions.Where(s=>s.Best5k is not null).All(s=>s.Best5k>=Held(SportsData.When(s.Day))),"a run beat the record of its day");
    Check(record.Skip(1).SkipLast(1).All(r=>athlete.Sessions.Any(s=>s.Day==DayOf(r.X)&&s.Best5k==r.Y&&s.Name=="5 km time trial")),"a record was not set by a time trial");
    Check(weekly.Count==SportsData.Weeks&&weekly.All(p=>p.Y>=Held(p.X)),"a week's fastest 5 km is missing or beats the record");
});
Test("Sports page: each night's HRV falls where its colour and its note say, against the 28 nights before it, under its seven-night average",()=>{
    var hrv=Sports("hrv").Series;var band=hrv[0].Points;var nights=athlete.Hrv;
    Check(hrv.Count==2&&hrv[1].Name=="Nightly HRV"&&band.Count==SportsData.Weeks*7&&hrv[1].Points.Count==SportsData.Weeks*7&&nights.Count==SportsData.Weeks*7+SportsData.BaselineNights);
    for(var i=0;i<band.Count;i++)
    {
        var window=nights.Skip(i).Take(SportsData.BaselineNights).ToArray();var mean=window.Average();var deviation=Math.Sqrt(window.Sum(v=>(v-mean)*(v-mean))/(window.Length-1));
        Check(Math.Abs(band[i].Y!.Value-mean)<.051&&Math.Abs(band[i].Low!.Value-(mean-deviation))<.051&&Math.Abs(band[i].High!.Value-(mean+deviation))<.051,$"night {i}'s baseline is not the 28 nights before it");
    }
    ChartPoint BandAt(double x)=>band.Single(b=>b.X==x);
    // Each night is the night it stands for, in the colour of its status, and its note names that status, so its name says what its
    // colour shows: green inside the band, orange below it and blue above it.
    var zones=ChartStyle.Light.Zones;
    for(var i=0;i<band.Count;i++)
    {
        var p=hrv[1].Points[i];var b=BandAt(p.X);
        var status=p.Y<b.Low?"below":p.Y>b.High?"above":"inside";
        Check(p.X==band[i].X&&p.Y==nights[i+SportsData.BaselineNights],$"night {i} is not its night");
        Check(p.Color==(status=="below"?zones[4]:status=="above"?zones[1]:zones[2])&&p.ValueNote==$" {status} baseline",$"night {i} is coloured {p.Color} and noted '{p.ValueNote}' {status} its band");
    }
    Check(new[]{"inside","below","above"}.All(status=>hrv[1].Points.Any(p=>p.ValueNote==$" {status} baseline")),"a status has no nights");
    // The seven-night moving average runs in the ramp's purple, the series colour no night is drawn in, and is named for assistive
    // technology; each night's name reads its value and then its status.
    Check(hrv[1].Trend&&hrv[1].TrendFit==TrendFit.MovingAverage&&hrv[1].TrendPoints==7&&hrv[1].Color==zones[6]&&hrv[1].Points.All(p=>p.Color!=zones[6]),"the average is not a seven-night moving average in a colour of its own");
    var doc=Svg(Sports("hrv"));
    var trend=doc.Descendants(ns+"path").Single(e=>(string?)e.Attribute("class")=="lumen-trend");
    Check((string?)trend.Attribute("stroke")==zones[6]&&trend.Attribute("aria-label")!.Value=="Nightly HRV trend: 7-point moving average",trend.ToString()[..200]);
    var named=doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("data-series")=="1").Select(e=>e.Attribute("aria-label")!.Value).ToArray();
    Check(named.Length==band.Count&&named.All(n=>n.StartsWith("Nightly HRV: ")&&(n.EndsWith(" inside baseline")||n.EndsWith(" below baseline")||n.EndsWith(" above baseline"))),named[0]);
    Check(named[^1].EndsWith($", {nights[^1].ToString(CultureInfo.InvariantCulture)}{hrv[1].Points[^1].ValueNote}")&&Sports("hrv").Title.Contains(hrv[1].Points[^1].ValueNote!.Split(' ')[1]),$"last night reads {named[^1]} under {Sports("hrv").Title}");
});
Test("Sports page: the race results are the five invented races, the place each finished coloured and named by its change, its field noted, and the points beneath",()=>{
    var spec=Sports("race-results");var (position,points)=(spec.Series[0],spec.Series[1]);
    Check(spec.Panes.Count==1&&spec.YReversed&&!spec.Panes[0].YReversed&&position.Pane==0&&points.Pane==1&&spec.XMin==-.5&&spec.XMax==4.5,"the panes are not position, first at the top, over points");
    Check(position.ChangeColors==ChangeColors.LowerIsBetter&&position.ValueLabels&&points.ValueLabels&&points.ChangeColors==ChangeColors.None,"the places are not coloured by change, or a series writes no values");
    Check(position.Points.Select(p=>(p.Y,p.ValueNote,p.Label)).SequenceEqual([(31d,"/50","11-04-2026"),(24d,"/48","16-05-2026"),(27d,"/51","04-07-2026"),(21d,"/49","08-08-2026"),(19d,"/52","19-09-2026")])
        &&points.Points.Select(p=>p.Y).SequenceEqual([40d,52,47,58,61]),"the races are not the invented season");
    foreach(var width in new[]{1100,337})
    {
        var doc=Svg(spec with{Width=width});
        var marks=doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("data-series")=="0").ToArray();
        Check(marks.Select(e=>e.Attribute("aria-label")!.Value).SequenceEqual(["Position: 11-04-2026, 31/50","Position: 16-05-2026, 24/48, better than the previous","Position: 04-07-2026, 27/51, worse than the previous",
            "Position: 08-08-2026, 21/49, better than the previous","Position: 19-09-2026, 19/52, better than the previous"]),string.Join(" | ",marks.Select(e=>e.Attribute("aria-label")!.Value)));
        Check(marks.Select(e=>(string?)e.Element(ns+"circle")!.Attribute("fill")).SequenceEqual([ChartStyle.Light.SeriesColor(0),ChartStyle.Light.Rising,ChartStyle.Light.Falling,ChartStyle.Light.Rising,ChartStyle.Light.Rising]),"a place is in the wrong colour");
        // Every place and every points total is written, inside the drawing.
        var values=doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-value").Select(e=>e.Elements(ns+"text").Last()).ToArray();
        Check(values.Select(t=>t.Value).SequenceEqual(["31/50","24/48","27/51","21/49","19/52","40","52","47","58","61"]),string.Join(",",values.Select(t=>t.Value)));
        Check(values.All(t=>double.Parse(t.Attribute("x")!.Value,CultureInfo.InvariantCulture) is var x&&x-(t.Value.Length*.62*11)/2>=0&&x+(t.Value.Length*.62*11)/2<=width),"a value runs past the drawing's edge");
    }
});
Test("Sports page: the power curves are each month's best rides, and the critical-power line is fitted to this month's",()=>{
    var power=Sports("power-curve");
    foreach(var (name,month) in new[]{("September",9),("August",8)})
    {
        var rides=athlete.Sessions.Where(s=>s.Sport==Sport.Ride&&s.Day.Month==month).ToArray();
        Check(rides.Any(s=>s.Name=="20-minute test"),$"{name} has no test");
        foreach(var p in power.Series.Single(s=>s.Name==name).Points)
            Check(p.Y==Math.Round(rides.SelectMany(s=>s.PowerCurve).Where(c=>c.Seconds==p.X).Max(c=>c.Value)),$"{name} at {p.X} s is not its best ride");
    }
    var fit=Training.CriticalPower(power.Series[0].Points.Select(p=>(p.X,p.Y!.Value)))!;
    Check(power.Annotations.Single().From==Math.Round(fit.CriticalPower)&&power.XAxis==AxisKind.Log,"the reference line is not this month's fit");
});
Test("Sports page: the headline numbers are the charts' own, and the season is the same every time it is simulated",()=>{
    var facts=SportsData.Facts();var today=athlete.Load.Single(d=>d.Day==SportsData.Today);
    Check(facts[0].Value==Math.Round(today.Fitness).ToString(CultureInfo.InvariantCulture)&&facts[2].Value==SportsData.Clock(Sports("records").Series[0].Points[^1].Y!.Value)
        &&facts[3].Value==$"{Sports("power-curve").Annotations[0].From.ToString(CultureInfo.InvariantCulture)} W","a headline number differs from its chart");
    var again=SportsData.Simulate();
    Check(again.Sessions.Select(s=>(s.Day,s.Name,s.Stress,s.Seconds,s.Best5k)).SequenceEqual(athlete.Sessions.Select(s=>(s.Day,s.Name,s.Stress,s.Seconds,s.Best5k)))
        &&again.Load.SequenceEqual(athlete.Load)&&again.Hrv.SequenceEqual(athlete.Hrv)&&again.Planned.SequenceEqual(athlete.Planned),"two simulations differ");
});
// 0.25.0: FitWidth draws a chart at the width its box gives it. A chart that leaves it off must render exactly as 0.24.0
// did, and one that sets it is marked for the stylesheet and redrawn at the width its script reports.
RenderFragment ChartElement(ChartSpec spec,bool? fit=null)=>b=>{b.OpenComponent<LumenChart>(0);b.AddAttribute(1,"Spec",spec);if(fit is {} f)b.AddAttribute(2,"FitWidth",f);b.CloseComponent();};
string Prerender(RenderFragment content,ChartStyle? cascaded=null)
{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        return renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<CascadingValue<ChartStyle>>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Value",cascaded},{"ChildContent",content}}));
            return root.ToHtmlString();
        }).GetAwaiter().GetResult();
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
}
Test("Without FitWidth the component renders exactly as 0.24.0 did",()=>{
    // Hashes of the prerendered markup taken from 0.24.0's component before FitWidth was added. Razor keeps the whitespace
    // between elements, line breaks included, so a checkout's line endings reach the markup: it is hashed with LF endings.
    (string Row,string Hash,ChartSpec Spec,ChartStyle? Cascaded)[] rows=[
        ("line","90DEC01423FBCF0A",Spec(),null),
        ("columns","4B0BC9F6E5BB0562",Spec(ChartKind.Column) with{Series=[new("A",[new(0,2,"A"),new(1,5,"B")]),new("B",[new(0,3,"A"),new(1,4,"B")])]},null),
        ("stream","32658617237228CB",Sports("stream"),null),
        ("Midnight","295A098ED64C73D8",Spec(),ChartStyle.Midnight)];
    // 0.36.0 adds one hidden element, the words that name the arrow keys, which the script makes the viewport's description; taken out,
    // the markup is 0.24.0's.
    string Unkeyed(string html)=>System.Text.RegularExpressions.Regex.Replace(html,"\n *<span class=\"lumen-keys\" hidden>[^<]*</span>","");
    var changed=rows.Select(r=>(r.Row,r.Hash,Now:Hash16(Unkeyed(Prerender(ChartElement(r.Spec),r.Cascaded).Replace("\r\n","\n"))))).Where(r=>r.Now!=r.Hash).ToArray();
    Check(changed.Length==0,"renders differently: "+string.Join(", ",changed.Select(r=>$"{r.Row} {r.Now}")));
    Check(rows.All(r=>Prerender(ChartElement(r.Spec,false),r.Cascaded)==Prerender(ChartElement(r.Spec),r.Cascaded)),"FitWidth=\"false\" renders differently from leaving it out");
});
Test("FitWidth marks its own chart, and the stylesheet lifts the 640-pixel minimum for that chart alone",()=>{
    RenderFragment both=b=>{b.AddContent(0,ChartElement(Spec(),true));b.AddContent(1,ChartElement(Spec()));};
    var html=Prerender(both);
    Check(html.Split("class=\"lumen-chart lumen-fit\"").Length==2&&html.Split("class=\"lumen-chart\"").Length==2,"the fitted chart and the other one are not told apart");
    // Until the browser measures it, a fitted chart is drawn at its spec's own width: prerendered, only its class differs.
    var fitted=Prerender(ChartElement(Sports("stream"),true));
    Check(fitted.Contains("viewBox='0 0 1100 640'")&&fitted.Replace(" lumen-fit","")==Prerender(ChartElement(Sports("stream"))),"the prerendered fitted chart differs by more than its class");
    var css=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Lumen.Charts.Blazor/wwwroot/lumen.css"));
    Check(css.Contains(".lumen-viewport>svg{min-width:640px}"),"the minimum is gone for every chart");
    // The only rule naming the class reaches the drawing of the chart that carries it, and nothing else. A chart sets no minimum
    // there; a fitted graph drawn wider than its box sets its drawn width, so that it scrolls rather than shrinks.
    Check(css.Split(".lumen-fit").Length==2&&css.Contains(".lumen-fit>.lumen-viewport>svg{min-width:var(--lumen-drawn,0)}"),"the fitted chart's rule is missing or reaches further");
});
Test("A fitted chart redraws at the width its box reports, keeps its zoom and hidden series, and exports at that width",()=>{
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    string Drawn(LumenChart chart)=>(string)typeof(LumenChart).GetField("svg",flags)!.GetValue(chart)!;
    string Box(string svg)=>XDocument.Parse(svg).Root!.Attribute("viewBox")!.Value;
    var spec=Spec() with{Series=[new("A",[new(0,2),new(1,5),new(2,3),new(3,4)]),new("B",[new(0,1),new(1,2),new(2,4),new(3,3)])]};
    var boxes=new List<string>();string exported="",refitted="";
    var html=Operate(spec,async chart=>{
        typeof(LumenChart).GetMethod("Zoom",flags)!.Invoke(chart,[.5]);typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[1]);
        boxes.Add(Box(Drawn(chart)));
        await chart.Fit(375);boxes.Add(Box(Drawn(chart)));
        Check(typeof(LumenChart).GetField("viewMin",flags)!.GetValue(chart) is double,"fitting reset the zoom");
        Check(!Drawn(chart).Contains("aria-label='B: ")&&Drawn(chart).Contains("aria-label='A: "),"fitting brought back the hidden series");
        // The SVG and PNG exports render the spec the chart draws.
        exported=ChartSvg.Render((ChartSpec)typeof(LumenChart).GetMethod("VisibleSpec",flags)!.Invoke(chart,[])!);
        await chart.Fit(200);boxes.Add(Box(Drawn(chart)));
        await chart.Fit(5000);boxes.Add(Box(Drawn(chart)));
        // A new spec from the page is drawn at the width already measured.
        await chart.Fit(412);
        await chart.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",spec with{Title="Again"}},{"FitWidth",true}}));
        refitted=Drawn(chart);
    },fit:true);
    Check(boxes.SequenceEqual(["0 0 900 420","0 0 375 420","0 0 320 420","0 0 4096 420"]),"drawn at "+string.Join(", ",boxes));
    // An export adds its legend below the chart, so only its width is the chart's.
    Check(Box(exported).StartsWith("0 0 375 ")&&exported.Contains("aria-label='A: ")&&!exported.Contains("aria-label='B: "),"the export is not the fitted chart");
    Check(Box(refitted)=="0 0 412 420"&&refitted.Contains("Again"),"a new spec lost the measured width");
    Check(html.Contains("class=\"lumen-chart lumen-fit\"")&&html.Contains("viewBox='0 0 412 420'"),"the page does not show the fitted chart");
    // A chart that is not asked to fit ignores a width.
    var ignored="";
    Operate(spec,async chart=>{await chart.Fit(375);ignored=Drawn(chart);});
    Check(Box(ignored)=="0 0 900 420","a chart without FitWidth took a measured width");
});
Test("The packages carry their XML documentation beside each assembly, and it covers what a consumer uses most",()=>{
    var documented=new HashSet<string>();
    foreach(var assembly in new[]{typeof(ChartSpec).Assembly,typeof(LumenChart).Assembly,typeof(Lumen.Charts.AspNetCore.ChartEndpoints).Assembly})
    {
        var path=Path.ChangeExtension(assembly.Location,".xml");
        Check(File.Exists(path),$"{Path.GetFileName(path)} is not beside {Path.GetFileName(assembly.Location)}");
        documented.UnionWith(XDocument.Load(path).Descendants("member").Where(m=>!string.IsNullOrWhiteSpace(m.Value)).Select(m=>(string)m.Attribute("name")!));
    }
    Check(documented.Contains("T:Lumen.Charts.ChartSpec")&&documented.Contains("P:Lumen.Charts.ChartSpec.Width")&&documented.Contains("P:Lumen.Charts.Blazor.LumenChart.FitWidth")
        &&documented.Any(m=>m.StartsWith("M:Lumen.Charts.AspNetCore.ChartEndpoints.MapLumenCharts(")),"a headline member is undocumented");
    const System.Reflection.BindingFlags declared=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.DeclaredOnly;
    bool Generated(System.Reflection.MemberInfo member)=>member.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute),false);
    // Every property of the records a chart is built from and of the results the training and statistics classes return,
    // every component parameter, and every method of those classes, of the style and of the series and point factories.
    var properties=new[]{typeof(ChartSpec),typeof(ChartSeries),typeof(ChartPoint),typeof(ChartPane),typeof(ChartAnnotation),typeof(ChartStyle),typeof(ZoneScale),typeof(Zone),
            typeof(LoadDay),typeof(CriticalPowerFit),typeof(BoxSummary),typeof(LinearFit),typeof(RollingWindow),typeof(HistogramBin),typeof(PointSelection)}
        .SelectMany(t=>t.GetProperties(declared).Select(p=>$"P:{t.FullName}.{p.Name}"))
        .Concat(new[]{typeof(LumenChart),typeof(LumenGraph),typeof(LumenBrand),typeof(LumenPlanner)}.SelectMany(t=>t.GetProperties(declared)
            .Where(p=>p.IsDefined(typeof(ParameterAttribute),false)||p.IsDefined(typeof(CascadingParameterAttribute),false)).Select(p=>$"P:{t.FullName}.{p.Name}")));
    var methods=new[]{typeof(Training),typeof(Statistics),typeof(ZoneScale),typeof(ChartStyle),typeof(ChartSeries),typeof(ChartPoint)}
        .SelectMany(t=>t.GetMethods(declared).Where(m=>!m.IsSpecialName&&!Generated(m)).Select(m=>$"M:{t.FullName}.{m.Name}"));
    var missing=properties.Where(id=>!documented.Contains(id))
        .Concat(methods.Where(id=>!documented.Any(d=>d==id||d.StartsWith(id+"(")||d.StartsWith(id+"``")))).Distinct().ToArray();
    Check(missing.Length==0,"undocumented: "+string.Join(", ",missing));
});
// 0.26.0: gauges and rings. Angles are measured as the renderer measures them, in degrees clockwise from twelve o'clock about
// the origin of the group the arcs are drawn in, which the group's transform puts at their centre.
double AngleAt(double x,double y)=>Math.Atan2(x,-y)*180/Math.PI;
bool About(double a,double b,double within=.01)=>Math.Abs(a-b)<within;
// Two angles that name one direction, whichever turn they are written in.
bool Same(double a,double b)=>About(((a-b)%360+540)%360-180,0);
ZoneScale Tiers()=>new([new("Low",33,"#DD4B45"),new("Moderate",66,"#A88200"),new("Good",double.PositiveInfinity,"#2E9B58")]);
ChartSpec Gauge(double value,double sweep=270)=>new(){Kind=ChartKind.Gauge,Title="Recovery",GaugeSweep=sweep,Series=[new("Recovery",[new(0,value,"Recovery")])]};
ChartSpec Rings(params (string Name,double Value,double? Goal)[] rings)=>new(){Kind=ChartKind.Ring,Title="Activity",
    Series=rings.Select(r=>new ChartSeries(r.Name,[new(0,r.Value,"kcal")]){Goal=r.Goal}).ToArray()};
XElement[] Classed(XDocument doc,string name)=>doc.Descendants().Where(e=>(string?)e.Attribute("class")==name).ToArray();
// The radius of a band's or a ring's outer edge, which its first arc follows, and the angles of the points a path visits on a circle.
double OuterOf(XElement path)=>Commands(path.Attribute("d")!.Value).First(c=>c.Op=='A').Args[0];
double[] AnglesOn(XElement path,double radius)=>Commands(path.Attribute("d")!.Value).Where(c=>c.Op is 'M' or 'A' or 'L')
    .Select(c=>(X:c.Args[^2],Y:c.Args[^1])).Where(p=>About(double.Hypot(p.X,p.Y),radius)).Select(p=>AngleAt(p.X,p.Y)).ToArray();
// How far clockwise a path runs round the circle of a radius, arc by arc.
double SweepOn(XElement path,double radius)
{
    double x=0,y=0,total=0;
    foreach(var (op,a) in Commands(path.Attribute("d")!.Value))
    {
        if(op=='A'&&About(a[0],radius)&&a[4]==1){var turn=(AngleAt(a[5],a[6])-AngleAt(x,y)+720)%360;total+=turn==0?360:turn;}
        if(op is 'M' or 'L' or 'A'){x=a[^2];y=a[^1];}
    }
    return total;
}
double KnobAngle(ChartSpec spec){var knob=Classed(Svg(spec),"lumen-gauge-knob").Single();return AngleAt(Attr(knob,"cx"),Attr(knob,"cy"));}
XElement ScoreOf(XDocument doc)=>doc.Descendants(ns+"g").Single(g=>g.Attribute("data-point") is not null);
Test("Gauge: the score stands at its angle on the arc, at the start, the middle and the end, and off the scale at the end it passed",()=>{
    (double Value,double Sweep,double Angle)[] cases=[(0,270,-135),(50,270,0),(100,270,135),(25,270,-67.5),(150,270,135),(-20,270,-135),
        (0,180,-90),(25,180,-45),(100,180,90),(25,360,-90),(50,360,0),(75,360,90),(72,300,-150+300*.72)];
    foreach(var (value,sweep,angle) in cases)
    {
        Check(About(KnobAngle(Gauge(value,sweep)),angle),$"{value} on a {sweep}-degree arc stands at {KnobAngle(Gauge(value,sweep)):0.###}, not {angle}");
        // The score's band runs along the outer edge from the start of the arc to the score, and no further.
        var doc=Svg(Gauge(value,sweep));var track=Classed(doc,"lumen-gauge-track").Single();var band=Classed(doc,"lumen-gauge-value").Single();
        var outer=OuterOf(track);var on=AnglesOn(band,outer);
        Check(Same(on[0],-sweep/2)&&About(SweepOn(band,outer),angle+sweep/2)&&About(SweepOn(track,outer),sweep),$"{value} on {sweep}: the band runs {SweepOn(band,outer)} degrees from {on[0]}");
        Check(band.Parent!.Attribute("data-point")?.Value=="0"&&band.Parent.Attribute("data-series")?.Value=="0"&&(string?)band.Parent.Attribute("role")=="button","the score is not a selectable mark");
    }
    // The scale is YMin to YMax: 14.2 on WHOOP's strain scale of 0 to 21.
    Check(About(KnobAngle(Gauge(14.2) with{YMin=0,YMax=21}),-135+270*14.2/21)&&About(KnobAngle(Gauge(-10) with{YMin=-50,YMax=50}),-27),"the scale's bounds are not where the arc's ends are");
});
Test("Gauge: zones tint their own stretch of track, clipped to the scale, and the score takes its zone's colour",()=>{
    var doc=Svg(Gauge(72) with{YZones=Tiers()});
    var outer=OuterOf(Classed(doc,"lumen-gauge-track").Single());
    var zones=Classed(doc,"lumen-gauge-zone");
    double At(double v)=>-135+270*v/100;
    (double From,double To,string Ink,string Name)[] expected=[(At(0),At(33),"#DD4B45","Low: up to 33"),(At(33),At(66),"#A88200","Moderate: 33 to 66"),(At(66),At(100),"#2E9B58","Good: above 66")];
    Check(zones.Length==3,$"{zones.Length} zones");
    for(var i=0;i<3;i++)
    {
        var on=AnglesOn(zones[i],outer);
        Check(About(on.Min(),expected[i].From)&&About(on.Max(),expected[i].To)&&(string?)zones[i].Attribute("fill")==expected[i].Ink&&(string?)zones[i].Attribute("fill-opacity")==".3",$"zone {i} runs {on.Min():0.##} to {on.Max():0.##}");
        Check((string?)zones[i].Parent!.Attribute("aria-label")==expected[i].Name&&(string?)zones[i].Parent!.Attribute("role")=="img"&&zones[i].Parent!.Attribute("data-point") is null,$"zone {i} is named {zones[i].Parent!.Attribute("aria-label")}");
    }
    Check((string?)Classed(doc,"lumen-gauge-value").Single().Attribute("fill")=="#2E9B58","a good score is not green");
    Check((string?)Classed(Svg(Gauge(20) with{YZones=Tiers()}),"lumen-gauge-value").Single().Attribute("fill")=="#DD4B45","a low score is not red");
    // A zone that ends below the scale is left out, and one that starts below it is clipped to the arc's start.
    var clipped=Classed(Svg(Gauge(72) with{YZones=Tiers(),YMin=40}),"lumen-gauge-zone");
    Check(clipped.Length==2&&About(AnglesOn(clipped[0],outer).Min(),-135)&&About(AnglesOn(clipped[0],outer).Max(),-135+270*26/60.0),"a zone below the scale was drawn or not clipped");
    // Without zones the score takes its series colour, and a zone without a colour the style's ramp at its place.
    Check((string?)Classed(Svg(Gauge(72)),"lumen-gauge-value").Single().Attribute("fill")==ChartStyle.Light.Series[0]);
    var ramped=Svg(Gauge(72) with{YZones=new([new("Low",33),new("Moderate",66),new("Good",double.PositiveInfinity)]),Style=ChartStyle.Midnight});
    Check(Classed(ramped,"lumen-gauge-zone").Select(z=>(string?)z.Attribute("fill")).SequenceEqual(ChartStyle.Midnight.Zones.Take(3))&&(string?)Classed(ramped,"lumen-gauge-value").Single().Attribute("fill")==ChartStyle.Midnight.Zones[2]);
    // The key of a zoned gauge is its zones' colours.
    Check(XDocument.Parse(ChartSvg.LegendKey(Gauge(72) with{YZones=Tiers()},0)).Root!.Elements().Select(e=>(string?)e.Attribute("fill")).SequenceEqual(["#DD4B45","#A88200","#2E9B58"]),"the key is not the zones");
});
Test("Gauge: a target is a tick across the arc at its angle, named and labelled",()=>{
    var doc=Svg(Gauge(72) with{Annotations=[new(AnnotationAxis.Y,60){Label="Average"}]});
    var outer=OuterOf(Classed(doc,"lumen-gauge-track").Single());
    var ticks=Classed(doc,"lumen-gauge-target");
    Check(ticks.Length==2,"the tick and its halo");
    foreach(var tick in ticks)
    {
        double x1=Attr(tick,"x1"),y1=Attr(tick,"y1"),x2=Attr(tick,"x2"),y2=Attr(tick,"y2");
        Check(About(AngleAt(x1,y1),-135+270*.6)&&About(AngleAt(x2,y2),-135+270*.6)&&double.Hypot(x1,y1)<outer-5&&double.Hypot(x2,y2)>outer+4,"the tick is not across the arc at 60");
    }
    Check((string?)ticks[0].Parent!.Attribute("aria-label")=="Average: 60"&&doc.Descendants(ns+"text").Any(t=>t.Value=="Average: 60"),"the target is not named and labelled");
    // A target drawn without a label reads its value, and a label with no room clear of the score is left out but keeps its name.
    Check(Classed(Svg(Gauge(72) with{Annotations=[new(AnnotationAxis.Y,10)]}),"lumen-gauge-target")[0].Parent!.Attribute("aria-label")!.Value=="10");
    var narrow=Svg(Gauge(30) with{Width=337,Height=360,Annotations=[new(AnnotationAxis.Y,85){Label="Seven-day average"}]});
    Check(!narrow.Descendants(ns+"text").Any(t=>t.Value.StartsWith("Seven"))&&Classed(narrow,"lumen-gauge-target")[0].Parent!.Attribute("aria-label")!.Value=="Seven-day average: 85","a label without room was drawn over the score or lost its name");
});
Test("Gauge: a gradient colours the arc along its length, each stop at its value's angle",()=>{
    var stops=new ColorStop[]{new(0,"#3F87D9"),new(50,"#A88200"),new(100,"#DD4B45")};
    var doc=Svg(Gauge(80) with{Series=[new("Strain",[new(0,80)]){Gradient=stops}]});
    var outer=OuterOf(Classed(doc,"lumen-gauge-track").Single());
    var pieces=Classed(doc,"lumen-gauge-value");
    Check(pieces.Length>50&&About(AnglesOn(pieces[0],outer)[0],-135)&&About(AnglesOn(pieces[^1],outer)[^1],-135+270*.8),"the pieces do not run from the start to the score");
    // Each piece starts where the last one did plus its step, so they leave no gap, and takes the gradient's colour at its middle.
    var step=270*.8/pieces.Length;
    for(var k=0;k<pieces.Length;k++)
    {
        var from=AnglesOn(pieces[k],outer)[0];
        Check(About(from,-135+k*step),$"piece {k} starts at {from}");
        var middle=(k*step+step/2)/270*100;
        var expected=middle<=50?Blend("#3F87D9","#A88200",middle/50):Blend("#A88200","#DD4B45",(middle-50)/50);
        var fill=pieces[k].Attribute("fill")!.Value;
        Check(new[]{1,3,5}.All(o=>Math.Abs(C(fill,o)-C(expected,o))<=1),$"piece {k} is {fill}, not {expected}");
    }
    static int C(string hex,int offset)=>int.Parse(hex.AsSpan(offset,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture);
    static string Blend(string a,string b,double t){int B(int o)=>(int)(C(a,o)+(C(b,o)-C(a,o))*t);return $"#{B(1):X2}{B(3):X2}{B(5):X2}";}
});
Test("Ring: progress runs clockwise from twelve o'clock, past 100 % over itself with a shadow ahead of its leading end, and stops at 300 %",()=>{
    (double Percent,double End)[] cases=[(0,0),(50,180),(100,360),(125,450),(150,540),(250,900),(400,1080)];
    foreach(var (percent,end) in cases)
    {
        var doc=Svg(Rings(("Move",percent*6,600)));
        var ring=doc.Descendants(ns+"g").Single(g=>(string?)g.Attribute("data-series")=="0");
        var track=ring.Elements().Single(e=>(string?)e.Attribute("class")=="lumen-ring-track");
        var outer=OuterOf(track);
        var progress=ring.Elements().Where(e=>(string?)e.Attribute("class")=="lumen-ring-progress").ToArray();
        var lead=ring.Elements().Where(e=>(string?)e.Attribute("class")=="lumen-ring-lead").ToArray();
        var shadows=ring.Elements().Where(e=>(string?)e.Attribute("class")=="lumen-ring-shadow").ToArray();
        Check(ring.Attribute("aria-label")!.Value==$"Move: {(percent*6).ToString(CultureInfo.InvariantCulture)} of 600 kcal, {percent} %{(percent>300?", drawn at 300 %":"")}",ring.Attribute("aria-label")!.Value);
        if(percent==0){Check(progress.Length==0&&lead.Length==0&&shadows.Length==0,"an empty ring drew progress");continue;}
        if(end<=360)
        {
            var on=AnglesOn(progress.Single(),outer);
            Check(About(on[0],0)&&About(SweepOn(progress.Single(),outer),end)&&lead.Length==0&&shadows.Length==0,$"{percent} %: the arc runs {SweepOn(progress.Single(),outer)} degrees");
            continue;
        }
        // Past the goal the ring lies whole, its leading end is drawn again over the lap beneath, and the shadow lies ahead of it.
        Check(About(SweepOn(progress.Single(),outer),360)&&Commands(progress.Single().Attribute("d")!.Value).Count(c=>c.Op=='M')==2,$"{percent} %: the lap beneath is not whole");
        var leading=AnglesOn(lead.Single(),outer);
        Check(About((leading[^1]+360)%360,end%360)&&About(SweepOn(lead.Single(),outer),90),$"{percent} %: the leading end stands at {leading[^1]}, not {end%360}");
        var thick=outer-Commands(track.Attribute("d")!.Value).Where(c=>c.Op=='A').Select(c=>c.Args[0]).Min();
        Check(shadows.Length==3&&shadows.All(s=>About(Attr(s,"r"),thick/2,.001)&&About(double.Hypot(Attr(s,"cx"),Attr(s,"cy")),outer-thick/2)),"the shadow is not the ring's width, on its centre line");
        Check(shadows.All(s=>{var ahead=(AngleAt(Attr(s,"cx"),Attr(s,"cy"))-end%360+720)%360;return ahead>0&&ahead<10;}),"the shadow is not just ahead of the leading end");
        var order=ring.Elements().ToList();
        Check(order.IndexOf(progress.Single())<order.IndexOf(shadows[0])&&order.IndexOf(shadows[^1])<order.IndexOf(lead.Single()),"the leading end is not drawn over its shadow");
    }
});
Test("Ring: rings are evenly spaced from the outside in, each .84 of the pitch, sized from the smaller of the width and height",()=>{
    foreach(var (count,width,height) in new[]{(1,900,420),(3,900,420),(3,337,600),(6,540,360)})
    {
        var spec=Rings(Enumerable.Range(0,count).Select(i=>($"R{i}",40d+i*20,(double?)100)).ToArray()) with{Width=width,Height=height};
        var doc=Svg(spec);
        var outerEdge=Math.Min(width-48,height-94)/2d;
        var pitch=Math.Min(outerEdge*.22,outerEdge*.72/count);
        var tracks=Classed(doc,"lumen-ring-track");
        Check(tracks.Length==count,$"{tracks.Length} rings");
        for(var i=0;i<count;i++)
        {
            var radii=Commands(tracks[i].Attribute("d")!.Value).Where(c=>c.Op=='A').Select(c=>c.Args[0]).Distinct().ToArray();
            Check(radii.Length==2&&About(radii.Max(),outerEdge-i*pitch,.001)&&About(radii.Max()-radii.Min(),pitch*.84,.001),$"{count} rings at {width} by {height}: ring {i} spans {radii.Min()} to {radii.Max()}");
        }
        var group=doc.Descendants(ns+"g").Single(g=>(string?)g.Attribute("class")=="lumen-rings");
        Check((string?)group.Attribute("transform")==$"translate({(width/2d).ToString(CultureInfo.InvariantCulture)} {((64+height-30)/2d).ToString(CultureInfo.InvariantCulture)})","the rings are not centred");
    }
});
Test("Gauge and ring: names, labels and legends read the value in the axis's format, with its unit, zone and goal",()=>{
    var gauge=Gauge(72) with{YLabel="%",YZones=Tiers()};
    var doc=Svg(gauge);
    Check(ScoreOf(doc).Attribute("aria-label")!.Value=="Recovery: 72 %, Good",ScoreOf(doc).Attribute("aria-label")!.Value);
    var texts=doc.Descendants(ns+"text").Select(t=>t.Value).ToArray();
    Check(texts.Contains("72%")&&texts.Contains("Recovery")&&texts.Contains("Good")&&texts.Contains("0")&&texts.Contains("100"),string.Join(" | ",texts));
    // A gauge's score is written in its centre, so it draws no legend; a ring chart's legend names each ring with its value and goal.
    Check(!texts.Any(t=>t.StartsWith("Recovery:")),"a gauge drew a legend");
    Check(ScoreOf(Svg(Gauge(104) with{YLabel="%",YZones=Tiers()})).Attribute("aria-label")!.Value=="Recovery: 104 %, above the scale, drawn at 100, Good");
    Check(ScoreOf(Svg(Gauge(-3))).Attribute("aria-label")!.Value=="Recovery: -3, below the scale, drawn at 0");
    var slept=Svg(Gauge(25740) with{YFormat=ValueFormat.Duration,YMax=36000,Series=[new("Sleep",[new(0,25740,"Asleep")])]});
    Check(ScoreOf(slept).Attribute("aria-label")!.Value=="Asleep: 7:09:00"&&slept.Descendants(ns+"text").Any(t=>t.Value=="10:00:00")&&slept.Descendants(ns+"text").Any(t=>t.Value=="0:00"),"a duration gauge does not read h:mm:ss");
    Check(ScoreOf(Svg(Gauge(1500) with{YFormat=ValueFormat.Compact,YMax=2000,Series=[new("Steps",[new(0,1500)])]})).Attribute("aria-label")!.Value=="Steps: 1.5k");
    var rings=Rings(("Move",540,600),("Exercise",47,30),("Stand",9,null));
    var drawn=Svg(rings);
    var marks=drawn.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
    Check(marks.Select(g=>g.Attribute("aria-label")!.Value).SequenceEqual(["Move: 540 of 600 kcal, 90 %","Exercise: 47 of 30 kcal, 157 %","Stand: 9 of 100 kcal, 9 %"]),string.Join(" | ",marks.Select(g=>g.Attribute("aria-label")!.Value)));
    Check(marks.Select(g=>(g.Attribute("data-series")!.Value,g.Attribute("data-point")!.Value,g.Attribute("role")!.Value,g.Attribute("tabindex")!.Value)).SequenceEqual([("0","0","button","0"),("1","0","button","0"),("2","0","button","0")]));
    Check(drawn.Descendants(ns+"text").Select(t=>t.Value).Intersect(["Move: 540 of 600 kcal","Exercise: 47 of 30 kcal","Stand: 9 of 100 kcal"]).Count()==3,"the legend does not name each ring's value and goal");
    Check(ChartSvg.LegendLabel(rings,0)=="Move: 540 of 600 kcal"&&ChartSvg.LegendLabel(gauge,0)=="Recovery: 72 %"&&ChartSvg.LegendLabel(Spec(),0)=="Series","LegendLabel");
    // A ring without a unit, in durations.
    Check(ScoreOf(Svg(new ChartSpec{Kind=ChartKind.Ring,YFormat=ValueFormat.Duration,Series=[new("Exercise",[new(0,2040)]){Goal=1800}]})).Attribute("aria-label")!.Value=="Exercise: 34:00 of 30:00, 113 %");
});
Test("Gauge and ring: every colour follows the style, marks clear 3:1 and the score 4.5:1 against the background, in every preset",()=>{
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight,ChartStyle.Light with{Finish=ChartFinish.Classic}})
    {
        var gauge=Svg(Gauge(72) with{Style=style,YZones=new([new("Low",33),new("Moderate",66),new("Good",double.PositiveInfinity)])});
        var score=gauge.Descendants(ns+"text").Single(t=>(string?)t.Attribute("font-weight")=="600"&&t.Value.StartsWith("72"));
        // The score inherits the text colour and the caption is muted: both are text colours of the style.
        Check(score.Attribute("fill") is null&&Contrast(style.Text,style.Background)>=4.5&&Contrast(style.Muted,style.Background)>=4.5);
        Check((string?)Classed(gauge,"lumen-gauge-track").Single().Attribute("fill")==style.Grid&&(string?)Classed(gauge,"lumen-gauge-knob").Single().Attribute("fill")==style.Background);
        foreach(var ink in Classed(gauge,"lumen-gauge-zone").Concat(Classed(gauge,"lumen-gauge-value")).Select(e=>e.Attribute("fill")!.Value))
            Check(style.Zones.Contains(ink)&&Contrast(ink,style.Background)>=3,$"{ink} on {style.Background}");
        var rings=Svg(Rings(("A",1,10),("B",2,10),("C",3,10),("D",4,10),("E",5,10),("F",6,10)) with{Style=style});
        foreach(var ink in Classed(rings,"lumen-ring-progress").Select(e=>e.Attribute("fill")!.Value))
            Check(style.Series.Contains(ink)&&Contrast(ink,style.Background)>=3,$"{ink} on {style.Background}");
        Check(gauge.Root!.Attribute("style")!.Value.Contains($"background:{style.Background}"));
    }
});
Test("Gauge and ring: what has no meaning on them is refused, each with its reason",()=>{
    string Refusal(ChartSpec spec){try{ChartSvg.Render(spec);}catch(ArgumentException error){return error.Message;}throw new Exception("a chart was accepted that should not be");}
    var gauge=Gauge(72);var ring=Rings(("Move",540,600));
    foreach(var (spec,reason) in new (ChartSpec,string)[]{
        (gauge with{Series=[gauge.Series[0],gauge.Series[0]]},"one series"),
        (gauge with{Series=[new("S",[])]},"exactly one point"),(gauge with{Series=[new("S",[new(0,1),new(1,2)])]},"exactly one point"),(gauge with{Series=[new("S",[new(0,null)])]},"cannot be missing"),
        (gauge with{GaugeSweep=170},"between 180"),(gauge with{GaugeSweep=361},"between 180"),(gauge with{GaugeSweep=double.NaN},"between 180"),
        (gauge with{YMin=150},"YMin must be below YMax"),(gauge with{YMin=10,YMax=10},"YMin must be below YMax"),
        (gauge with{Annotations=[new(AnnotationAxis.X,3)]},"no X axis"),(gauge with{Annotations=[new(AnnotationAxis.Y,30){To=40}]},"not bands"),(gauge with{Annotations=[new(AnnotationAxis.Y,120)]},"on its scale"),
        (gauge with{YZones=Tiers(),Series=[new("S",[new(0,50)]){Gradient=[new(0,"#3F87D9"),new(100,"#DD4B45")]}]},"not both"),
        (gauge with{Series=[gauge.Series[0] with{Goal=80}]},"ring charts only"),(gauge with{Series=[gauge.Series[0] with{Zones=Tiers()}]},"Series zones"),
        (gauge with{XAxis=AxisKind.Time},"Time and log X"),(gauge with{XAxis=AxisKind.Log},"Time and log X"),(gauge with{YAxis=AxisKind.Log},"Log Y"),(gauge with{YReversed=true},"no Y axis"),
        (gauge with{Panes=[new()]},"Panes share"),(gauge with{Series=[gauge.Series[0] with{Secondary=true}]},"secondary axis"),(gauge with{Series=[gauge.Series[0] with{Trend=true}]},"trend line"),
        (gauge with{Series=[gauge.Series[0] with{Kind=ChartKind.Line}]},"own kind"),(gauge with{DensityCells=20},"Density cells"),(gauge with{YAxisSide=AxisSide.Right},"gauge and ring"),(gauge with{YTickLabels=TickLabels.Ends},"gauge and ring"),
        (ring with{Series=Enumerable.Range(0,7).Select(i=>ring.Series[0] with{Name=$"R{i}"}).ToArray()},"one to six"),
        (ring with{Series=[new("S",[])]},"exactly one point"),(ring with{Series=[new("S",[new(0,null)])]},"cannot be missing"),(ring with{Series=[new("S",[new(0,-1)])]},"cannot be negative"),
        (ring with{Series=[ring.Series[0] with{Goal=0}]},"positive and finite"),(ring with{Series=[ring.Series[0] with{Goal=-5}]},"positive and finite"),(ring with{Series=[ring.Series[0] with{Goal=double.NaN}]},"positive and finite"),
        (ring with{YZones=Tiers()},"no zones"),(ring with{Annotations=[new(AnnotationAxis.Y,50)]},"no annotations"),(ring with{Annotations=[new(AnnotationAxis.X,0)]},"no annotations"),
        (ring with{Series=[ring.Series[0] with{Gradient=[new(0,"#3F87D9"),new(100,"#DD4B45")]}]},"gauge's arc"),(ring with{GaugeSweep=180},"gauge charts only"),
        (ring with{XAxis=AxisKind.Time},"Time and log X"),(ring with{YAxis=AxisKind.Log},"Log Y"),(ring with{YReversed=true},"no Y axis"),(ring with{Panes=[new()]},"Panes share"),
        (ring with{Series=[ring.Series[0] with{Secondary=true},ring.Series[0]]},"secondary axis"),(ring with{Series=[ring.Series[0] with{Trend=true}]},"trend line"),(ring with{Series=[ring.Series[0] with{Kind=ChartKind.Column}]},"own kind"),
        (ring with{DensityCells=20},"Density cells"),(ring with{YAxisSide=AxisSide.Right},"gauge and ring"),
        (Spec() with{GaugeSweep=200},"gauge charts only"),(Spec() with{Series=[Spec().Series[0] with{Goal=10}]},"ring charts only"),(Spec(ChartKind.Donut) with{Series=[Spec().Series[0] with{Goal=10}]},"ring charts only")})
        Check(Refusal(spec).Contains(reason),$"{spec.Kind}: \"{Refusal(spec)}\" does not say \"{reason}\"");
    // Within the rules, the ends of each range are accepted, and a chart with no series draws its empty state.
    foreach(var spec in new[]{gauge with{GaugeSweep=180},gauge with{GaugeSweep=360},gauge with{Annotations=[new(AnnotationAxis.Y,0),new(AnnotationAxis.Y,100)]},ring with{Series=[ring.Series[0] with{Points=[new(0,0)]}]},
        ring with{Series=Enumerable.Range(0,6).Select(i=>ring.Series[0] with{Name=$"R{i}"}).ToArray()},gauge with{Series=[new("S",[new(0,50)]){Gradient=[new(0,"#3F87D9"),new(100,"#DD4B45")]}]}})
        ChartSvg.Render(spec);
    Check(ChartSvg.Render(gauge with{Series=[]}).Contains("No data to display")&&ChartSvg.Render(ring with{Series=[]}).Contains("No data to display"));
});
Test("Gauge and ring: specs survive JSON, a request that names no sweep draws 270 degrees, and CSV carries each ring's goal",()=>{
    var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    var gauge=Gauge(72) with{GaugeSweep=180,YLabel="%",YZones=Tiers(),Annotations=[new(AnnotationAxis.Y,60){Label="Average"}]};
    var ring=Rings(("Move",540,600),("Exercise",47,30));
    foreach(var spec in new[]{gauge,ring,gauge with{YZones=null,YMax=21,Annotations=[],Series=[new("Strain",[new(0,14)]){Gradient=[new(0,"#3F87D9"),new(21,"#DD4B45")]}]}})
    {
        var json=System.Text.Json.JsonSerializer.Serialize(spec,options);
        Check(ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,options)!)==ChartSvg.Render(spec),$"{spec.Kind} changed in transit");
    }
    var written=System.Text.Json.JsonSerializer.Serialize(gauge,options)+System.Text.Json.JsonSerializer.Serialize(ring,options);
    Check(written.Contains("\"kind\":\"Gauge\"")&&written.Contains("\"gaugeSweep\":180")&&written.Contains("\"kind\":\"Ring\"")&&written.Contains("\"goal\":600"),written);
    var request=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Gauge\",\"series\":[{\"name\":\"Recovery\",\"points\":[{\"x\":0,\"y\":72}]}]}",options)!;
    Check(request.GaugeSweep==270&&ChartSvg.Render(request)==ChartSvg.Render(Gauge(72) with{Title="Untitled chart",Series=[new("Recovery",[new(0,72)])]}),"a request without a sweep is not 270 degrees");
    var rings=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Ring\",\"series\":[{\"name\":\"Move\",\"goal\":600,\"points\":[{\"x\":0,\"y\":540,\"label\":\"kcal\"}]},{\"name\":\"Stand\",\"points\":[{\"x\":0,\"y\":9}]}]}",options)!;
    Check(rings.Series[0].Goal==600&&rings.Series[1].Goal is null&&ChartSvg.Render(rings).Contains("aria-label='Move: 540 of 600 kcal, 90 %'")&&ChartSvg.Render(rings).Contains("aria-label='Stand: 9 of 100, 9 %'"));
    var csv=ChartExport.Csv(Rings(("Move",540,600),("Stand",9,null)));
    Check(csv.StartsWith("Series,X,Y,Label,Size,Goal\r\n")&&csv.Contains("\"Move\",0,540,\"kcal\",1,600")&&csv.Contains("\"Stand\",0,9,\"kcal\",1,100"),csv);
    Check(ChartExport.Csv(Gauge(72)).StartsWith("Series,X,Y,Label,Size\r\n")&&ChartExport.Csv(Gauge(72)).Contains("\"Recovery\",0,72,\"Recovery\",1"),"a gauge's CSV changed shape");
    Check(!ChartExport.Csv(Spec()).Contains("Goal"),"another kind's CSV carries a goal");
});
Test("A gauge's sweep left at its default is left out of the hash that names gradients, so every other chart keeps its IDs",()=>{
    // 0.25.0 had no sweep: its hash of a spec is the JSON written today less the sweep and, since 0.27.0, the timeline's
    // connectors and, since 0.28.0, the calendar's layout, cell and week start, which are the last five properties written, and,
    // since 0.32.0, each series' trend fit, window and degree, written after its trend, and since 0.33.0 the chart's X ticks, written
    // after its X label, and each series' change colours, written after its value labels, and since 0.34.0 the chart's sparkline,
    // written after its height, and since 0.35.0 the chart's X tick labels, written after its X ticks, and since 0.36.0 the chart's shared
    // readout, written last, which is never hashed, and since 0.37.0 the chart's sampling, written after its rendered points, and its pane
    // titles, written after its panes, and since 0.39.0 its bar tracks and drawn titles, and since 0.41.0 its painted background and
    // fitted height, written last.
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}]};
    string Prefix(string svg)=>System.Text.RegularExpressions.Regex.Match(svg,"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var json=System.Text.Json.JsonSerializer.Serialize(faded with{Style=ChartSvg.ResolveStyle(faded)},new System.Text.Json.JsonSerializerOptions{DefaultIgnoreCondition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull});
    const string defaults=",\"GaugeSweep\":270,\"TimelineConnectors\":true,\"CalendarLayout\":0,\"CalendarCell\":0,\"WeekStart\":1,\"SharedReadout\":false,\"BarTrack\":false,\"DrawTitles\":true,\"PaintBackground\":true,\"FitHeight\":false}";
    const string trended="\"Trend\":false,\"TrendFit\":0,\"TrendPoints\":7,\"TrendDegree\":2,";
    const string ticked="\"XLabel\":\"\",\"XTicks\":0,\"XTickLabels\":0,";const string changed="\"ValueLabels\":false,\"ChangeColors\":0";const string sparked="\"Height\":420,\"Sparkline\":false,";
    const string sampled="\"MaxRenderedPoints\":1200,\"Sampling\":0,";const string titled="\"Panes\":[],\"PaneTitles\":0,";
    Check(json.EndsWith(defaults)&&json.Contains(trended)&&json.Contains(ticked)&&json.Contains(changed)&&json.Contains(sparked)&&json.Contains(sampled)&&json.Contains(titled),json[^120..]);
    var before="lumen-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json.Replace(titled,"\"Panes\":[],").Replace(defaults,"}").Replace(trended,"\"Trend\":false,")
        .Replace(ticked,"\"XLabel\":\"\",").Replace(changed,"\"ValueLabels\":false").Replace(sparked,"\"Height\":420,").Replace(sampled,"\"MaxRenderedPoints\":1200,"))))[..12].ToLowerInvariant();
    Check(Prefix(ChartSvg.Render(faded))==before,$"{Prefix(ChartSvg.Render(faded))} is not 0.25.0's {before}");
    // Gauges and rings define no IDs: a gradient gauge draws its arc in pieces.
    var strain=Gauge(14) with{YMax=21,Series=[new("Strain",[new(0,14)]){Gradient=[new(0,"#3F87D9"),new(21,"#DD4B45")]}]};
    Check(!ChartSvg.Render(strain).Contains(" id=")&&!ChartSvg.Render(Rings(("Move",900,600))).Contains(" id="),"a radial chart defines an ID");
});
Test("Gauge and ring: the component draws them without zoom, its legend and status read their values, and its table each ring's goal",()=>{
    foreach(var spec in new[]{Gauge(72) with{YLabel="%"},Rings(("Move",540,600),("Exercise",47,30))})
    {
        var html=RenderInside(null,spec);
        Check(!html.Contains("aria-label=\"Zoom in\"")&&!html.Contains("Reset view")&&html.Contains("Export CSV"),$"{spec.Kind} offers zoom");
        Check(html.Contains(ChartSvg.LegendLabel(spec,0)+"\n")||html.Contains(ChartSvg.LegendLabel(spec,0)+"\r\n")||html.Contains(ChartSvg.LegendLabel(spec,0)+"</button>"),$"{spec.Kind}'s legend does not read its value");
    }
    var rings=Rings(("Move",540,600),("Exercise",47,30));
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var shown=Operate(rings,async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(1,0);});
    Check(shown.Contains("<tr><td>Move</td><td>kcal</td><td>540 of 600</td></tr>")&&shown.Contains("Exercise: 47 of 30 kcal</span>"),"the table or the status line does not read the ring");
    // Hiding every ring leaves the chart's empty state rather than a refused spec.
    var hidden="";
    Operate(rings,chart=>{var toggle=typeof(LumenChart).GetMethod("Toggle",flags)!;toggle.Invoke(chart,[0]);toggle.Invoke(chart,[1]);hidden=(string)typeof(LumenChart).GetField("svg",flags)!.GetValue(chart)!;return Task.CompletedTask;});
    Check(hidden.Contains("No data to display"),"hiding every ring broke the chart");
    var gauge=Operate(Gauge(72) with{YLabel="%"},async chart=>await chart.SelectPoint(0,0));
    Check(gauge.Contains("Recovery: 72 %</span>"),"the status line does not read the gauge");
});
// 0.27.0: state timelines, range bars and the time of day. A night from 23:00 to 05:00 on a time-of-day axis, in four lanes: the
// plot runs down from 78 to 344, so each lane is 66.5 high and each span 24, on its lane's middle; across, it runs from the clip's
// edge, which the lane names set.
ChartSpec Hypnogram(bool connectors=true)=>new(){Kind=ChartKind.Timeline,Title="Night",TimelineConnectors=connectors,XFormat=ValueFormat.TimeOfDay,
    Series=[new("Awake",[ChartPoint.Span(82800,83400),ChartPoint.Span(97200,97380)]),new("REM",[ChartPoint.Span(90000,91800)]),
        new("Light",[ChartPoint.Span(83400,86400),ChartPoint.Span(91800,97200,"Second cycle"),ChartPoint.Span(97380,104400)]),new("Deep",[ChartPoint.Span(86400,90000)])]};
XElement MarkOf(XDocument doc,int series,int point)=>doc.Descendants(ns+"g").Single(g=>(string?)g.Attribute("data-series")==$"{series}"&&(string?)g.Attribute("data-point")==$"{point}");
XElement SpanOf(XDocument doc,int series,int point)=>MarkOf(doc,series,point).Element(ns+"rect")!;
XElement[] Classes(XDocument doc,string name)=>doc.Descendants().Where(e=>(string?)e.Attribute("class")==name).ToArray();
(double Left,double Right) PlotOf(XDocument doc){var clip=PaneClips(doc).Single();return (Attr(clip,"x")+6,Attr(clip,"x")+Attr(clip,"width")-6);}
double LaneMiddle(int lane,int lanes=4,double height=420)=>78+(lane+.5)*(height-154)/lanes;
Test("Timeline: each span lies in its lane at its exact extents, half the lane high up to 24 pixels, its corners rounded",()=>{
    var spec=Hypnogram();var doc=Svg(spec);var (left,right)=PlotOf(doc);
    double X(double seconds)=>left+(seconds-82800)/21600*(right-left);
    for(var s=0;s<spec.Series.Count;s++)
        for(var p=0;p<spec.Series[s].Points.Count;p++)
        {
            var rect=SpanOf(doc,s,p);var span=spec.Series[s].Points[p];var width=X(span.XEnd!.Value)-X(span.X);
            Check(Close(Attr(rect,"x"),X(span.X))&&Close(Attr(rect,"width"),width)&&Close(Attr(rect,"y"),LaneMiddle(s)-12)&&Close(Attr(rect,"height"),24),$"{spec.Series[s].Name} {p}: {rect}");
            Check(Close(Attr(rect,"rx"),Math.Min(4,width/2))&&(string?)rect.Attribute("fill")==ChartStyle.Light.Series[s]&&(string?)rect.Attribute("class")=="lumen-span",$"{spec.Series[s].Name} {p}: {rect}");
        }
    // The plot leaves the widest name 12 pixels and the Y title room, and ends 30 pixels from the right edge.
    Check(left is >=76 and <=180&&Close(right,870),$"the plot runs from {left} to {right}");
    // The lanes stand top to bottom in series order, each named at its middle on the left.
    var names=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="end"&&Close(Attr(t,"x"),left-12)).ToArray();
    Check(names.Select(t=>t.Value).SequenceEqual(["Awake","REM","Light","Deep"])&&names.Select((t,i)=>Close(Attr(t,"y"),LaneMiddle(i)+4)).All(b=>b),"the lanes are not named in order at their middles");
    // On the right, as YAxisSide puts them, they are named 12 pixels past the plot.
    var flipped=Svg(spec with{YAxisSide=AxisSide.Right});var (_,edge)=PlotOf(flipped);
    Check(flipped.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="start"&&Close(Attr(t,"x"),edge+12)).Select(t=>t.Value).SequenceEqual(["Awake","REM","Light","Deep"])&&Close(PlotOf(flipped).Left,30),"the lanes are not named on the right");
    // A short lane's span is half its height: 340 high leaves 186 for four lanes of 46.5, and spans of 23.25.
    var tall=Svg(spec with{Height=340});
    Check(Close(Attr(SpanOf(tall,3,0),"height"),23.25)&&Close(Attr(SpanOf(tall,3,0),"y"),LaneMiddle(3,4,340)-11.625),"a short lane's span is not half its height");
    // A style's bar radius rounds the corners, Midnight's into capsules, each clamped to half the span's width and height.
    var midnight=Svg(spec with{Style=ChartStyle.Midnight});var squarer=Svg(spec with{Style=ChartStyle.Light with{BarRadius=2}});
    Check(Spans(midnight).All(r=>Close(Attr(r,"rx"),Math.Min(Attr(r,"width"),24)/2))&&Spans(squarer).All(r=>Close(Attr(r,"rx"),Math.Min(2,Attr(r,"width")/2))),"the bar radius does not round the spans");
    // A span too short to see is drawn a pixel wide, from its start.
    var blip=Svg(spec with{Series=[..spec.Series,new("Blip",[ChartPoint.Span(90000,90001)])]});
    Check(Close(Attr(SpanOf(blip,4,0),"width"),1),"a one-second span is not a pixel wide");
    XElement[] Spans(XDocument d)=>Classes(d,"lumen-span");
});
Test("Timeline: a connector joins each span to the one in another lane that starts where it ends, behind the spans, and none when they are off",()=>{
    var doc=Svg(Hypnogram());var (left,right)=PlotOf(doc);
    double X(double seconds)=>left+(seconds-82800)/21600*(right-left);
    (double At,int From,int To)[] Drawn(XDocument d)=>Classes(d,"lumen-connectors").SelectMany(p=>Commands(p.Attribute("d")!.Value).Chunk(2)).Select(pair=>
    {
        Check(pair[0].Op=='M'&&pair[1].Op=='L'&&Close(pair[0].Args[0],pair[1].Args[0]),"a connector is not upright");
        int Lane(double y)=>Enumerable.Range(0,4).Single(i=>Close(LaneMiddle(i),y));
        return (pair[0].Args[0],Lane(pair[0].Args[1]),Lane(pair[1].Args[1]));
    }).ToArray();
    // Lane by lane, each span's end: awake into light at 23:10 and 03:03, REM into light at 01:30, light into deep at 00:00 and
    // into awake at 03:00, and deep into REM at 01:00. The last light span ends the night, so nothing follows it.
    (double,int,int)[] expected=[(X(83400),0,2),(X(97380),0,2),(X(91800),1,2),(X(86400),2,3),(X(97200),2,0),(X(90000),3,1)];
    var drawn=Drawn(doc);
    Check(drawn.Length==6&&drawn.Zip(expected).All(p=>Close(p.First.At,p.Second.Item1)&&p.First.From==p.Second.Item2&&p.First.To==p.Second.Item3),string.Join(" ",drawn));
    var path=Classes(doc,"lumen-connectors").Single();
    Check((string?)path.Attribute("stroke")==ChartStyle.Light.Muted&&(string?)path.Attribute("stroke-width")=="1"&&(string?)path.Attribute("fill")=="none"&&(string?)path.Attribute("vector-effect")=="non-scaling-stroke","the connectors are not hairlines");
    var order=doc.Descendants().ToList();
    Check(order.IndexOf(path)<Classes(doc,"lumen-span").Min(order.IndexOf),"the connectors are drawn over the spans");
    // Turned off, the connectors go and the spans stay exactly where they were.
    var plain=Svg(Hypnogram(false));
    Check(Classes(plain,"lumen-connectors").Length==0&&Classes(plain,"lumen-span").Select(r=>r.ToString()).SequenceEqual(Classes(doc,"lumen-span").Select(r=>r.ToString())),"turning connectors off moved a span or left a connector");
    // A span that starts a second after another ends is not joined to it; spans that touch in one lane need no connector; and two
    // lanes that start where a third ends are both joined to it.
    var gap=Hypnogram() with{Series=[..Hypnogram().Series.Take(3),new("Deep",[ChartPoint.Span(86401,90000)])]};
    Check(Drawn(Svg(gap)).Length==5&&!Drawn(Svg(gap)).Any(c=>c.From==2&&c.To==3),"a span was joined across a gap");
    var forked=Svg(new ChartSpec{Kind=ChartKind.Timeline,Series=[new("A",[ChartPoint.Span(0,10),ChartPoint.Span(10,20)]),new("B",[ChartPoint.Span(20,30)]),new("C",[ChartPoint.Span(20,25)])]});
    var joins=Classes(forked,"lumen-connectors").SelectMany(p=>Commands(p.Attribute("d")!.Value)).Count(c=>c.Op=='M');
    Check(joins==2,$"{joins} connectors where two lanes start as one ends, and a lane touches itself");
});
Test("Timeline: the legend gives each state its total time and its share, and each span names its state, its times and its length",()=>{
    var spec=Hypnogram();
    var legend=Enumerable.Range(0,4).Select(i=>ChartSvg.LegendLabel(spec,i)).ToArray();
    // Awake 13 minutes, REM 30, light 4 h 17 and deep an hour: 4, 8, 71 and 17 % of six hours.
    Check(legend.SequenceEqual(["Awake 0:13, 4 %","REM 0:30, 8 %","Light 4:17, 71 %","Deep 1:00, 17 %"]),string.Join(" | ",legend));
    var doc=Svg(spec);
    Check(legend.All(label=>doc.Descendants(ns+"text").Any(t=>t.Value==label)),"the chart's own legend does not total the states");
    string Name(int s,int p)=>MarkOf(doc,s,p).Attribute("aria-label")!.Value;
    Check(Name(0,0)=="Awake: 23:00 to 23:10, 10 min"&&Name(0,1)=="Awake: 03:00 to 03:03, 3 min"&&Name(3,0)=="Deep: 00:00 to 01:00, 1 h"
        &&Name(2,1)=="Light: Second cycle, 01:30 to 03:00, 1 h 30 min"&&Name(2,2)=="Light: 03:03 to 05:00, 1 h 57 min",$"{Name(0,0)} | {Name(2,1)} | {Name(2,2)}");
    // Every span is a button for its lane and its point, in the tab order, with a tooltip.
    var marks=doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
    Check(marks.Length==7&&marks.All(m=>(string?)m.Attribute("role")=="button"&&(string?)m.Attribute("tabindex")=="0"&&m.Element(ns+"title")?.Value==m.Attribute("aria-label")!.Value),"a span is not a labelled button");
    // On a time axis the clock is the zone's: 02:14 in Johannesburg is 00:14 UTC. Under a minute a span reads in seconds.
    var start=TimeAxis.Value(new DateTimeOffset(2026,9,27,2,14,0,TimeSpan.FromHours(2)));
    var zoned=new ChartSpec{Kind=ChartKind.Timeline,XAxis=AxisKind.Time,TimeZone="Africa/Johannesburg",Series=[new("REM",[ChartPoint.Span(start,start+27*60_000)]),new("Awake",[ChartPoint.Span(start+27*60_000,start+27*60_000+45_000)])]};
    var local=Svg(zoned);
    Check(MarkOf(local,0,0).Attribute("aria-label")!.Value=="REM: 02:14 to 02:41, 27 min"&&MarkOf(local,1,0).Attribute("aria-label")!.Value=="Awake: 02:41 to 02:41, 45 s",MarkOf(local,0,0).Attribute("aria-label")!.Value);
    Check(ChartSvg.LegendLabel(zoned,0)=="REM 0:27, 97 %"&&ChartSvg.LegendLabel(zoned,1)=="Awake 0:01, 3 %"&&local.Descendants(ns+"text").Any(t=>t.Value=="02:20"),"the zone's clock does not reach the legend or the ticks");
    // Over two days or more a span names its day too; on a plain number axis its length is a plain number.
    var shifts=Svg(new ChartSpec{Kind=ChartKind.Timeline,XAxis=AxisKind.Time,Series=[new("Shift",[ChartPoint.Span(Utc(2026,9,1,8),Utc(2026,9,1,9)),ChartPoint.Span(Utc(2026,9,3,8),Utc(2026,9,3,17))])]});
    Check(MarkOf(shifts,0,0).Attribute("aria-label")!.Value=="Shift: 1 Sep 08:00 to 1 Sep 09:00, 1 h"&&MarkOf(shifts,0,1).Attribute("aria-label")!.Value=="Shift: 3 Sep 08:00 to 3 Sep 17:00, 9 h",MarkOf(shifts,0,1).Attribute("aria-label")!.Value);
    var plain=new ChartSpec{Kind=ChartKind.Timeline,Series=[new("A",[ChartPoint.Span(0,2.5)]),new("B",[ChartPoint.Span(2.5,5)])]};
    Check(MarkOf(Svg(plain),0,0).Attribute("aria-label")!.Value=="A: 0 to 2.5, 2.5"&&ChartSvg.LegendLabel(plain,1)=="B 2.5, 50 %","a plain timeline reads units of time");
    // The legend key is a rounded bar along X.
    var key=XDocument.Parse(ChartSvg.LegendKey(spec,1)).Root!.Elements().Single();
    Check(Attr(key,"width")==14&&Attr(key,"height")==6&&Attr(key,"rx")==3&&(string?)key.Attribute("fill")==ChartStyle.Light.Series[1],key.ToString());
});
ChartSpec Ranges(params ChartPoint[] points)=>new(){Kind=ChartKind.Range,Title="Ranges",YMin=40,YMax=200,Series=[new("Heart rate",points)]};
double RY(double value)=>344-(value-40)/160*266;
XElement[] Capsules(XDocument doc)=>doc.Descendants(ns+"rect").Where(r=>(string?)r.Attribute("class")=="lumen-range").ToArray();
Test("Range: each capsule runs exactly from its low to its high, up to 18 pixels wide and centred on its X, with a dot at its typical value",()=>{
    var points=Enumerable.Range(0,5).Select(i=>ChartPoint.Interval(i,i==2?null:70+i,50+i*5,150+i*10,$"D{i}")).ToArray();
    var doc=Svg(Ranges(points));
    // Five bars a quarter of the axis apart take a 34-pixel slot, so the axis is inset 17 pixels each side: X runs 93 to 853.
    double X(double x)=>93+x*190;
    var capsules=Capsules(doc);
    Check(capsules.Length==5,$"{capsules.Length} capsules");
    for(var i=0;i<5;i++)
    {
        var p=points[i];var c=capsules[i];
        Check(Close(Attr(c,"x"),X(i)-9)&&Close(Attr(c,"width"),18)&&Close(Attr(c,"y"),RY(p.High!.Value))&&Close(Attr(c,"height"),RY(p.Low!.Value)-RY(p.High!.Value))&&Close(Attr(c,"rx"),9)&&(string?)c.Attribute("fill")==ChartStyle.Light.Series[0],$"bar {i}: {c}");
        var dot=c.Parent!.Element(ns+"circle");
        if(p.Y is null) Check(dot is null,$"bar {i} has a dot without a value");
        else Check(Close(Attr(dot!,"cx"),X(i))&&Close(Attr(dot!,"cy"),RY(p.Y.Value))&&Close(Attr(dot!,"r"),4.5)&&(string?)dot!.Attribute("fill")==ChartStyle.Light.Background&&(string?)dot!.Attribute("stroke")==ChartStyle.Light.Series[0]&&(string?)dot!.Attribute("vector-effect")=="non-scaling-stroke",$"bar {i}'s dot: {dot}");
    }
    // A bar whose ends meet is a pixel long, rounded by half that, and a day without bounds draws nothing but keeps its place.
    var flat=Capsules(Svg(Ranges(ChartPoint.Interval(0,null,90,90),ChartPoint.Interval(1,null,60,120))))[0];
    Check(Close(Attr(flat,"height"),1)&&Close(Attr(flat,"y"),RY(90)-.5)&&Close(Attr(flat,"rx"),.5),flat.ToString());
    var missing=Capsules(Svg(Ranges([..points.Take(2),new(2,null),..points.Skip(3)])));
    Check(missing.Length==4&&Close(Attr(missing[2],"x"),X(3)-9),"a missing day moved the bars after it");
    // A thin bar's dot stands proud of it: three pixels at least, ringed in its colour.
    var thin=Svg(Ranges(Enumerable.Range(0,120).Select(i=>ChartPoint.Interval(i,90,60,120)).ToArray()));
    Check(Capsules(thin).All(c=>Attr(c,"width")<6)&&thin.Descendants(ns+"circle").Where(e=>e.Parent?.Attribute("data-point") is not null).All(e=>Attr(e,"r")==3),"a thin bar's dot is too small to see");
});
Test("Range: a bar takes the slot a column would, up to 18 pixels: on a continuous axis from the closest gap, inset to stand whole, and on a column chart in its category",()=>{
    double Centre(XElement c)=>Attr(c,"x")+Attr(c,"width")/2;
    // Forty bars a step apart, and one more half a step on: the slot is .7 of the closest gap on screen, under 18 pixels.
    var dense=Capsules(Svg(Ranges([..Enumerable.Range(0,40).Select(i=>ChartPoint.Interval(i,null,60,120)),ChartPoint.Interval(39.5,null,60,120)])));
    var closest=Enumerable.Range(1,dense.Length-1).Min(i=>Centre(dense[i])-Centre(dense[i-1]));
    Check(Close(closest,Centre(dense[^1])-Centre(dense[^2]))&&dense.All(c=>Close(Attr(c,"width"),.7*closest))&&.7*closest<18,$"bars {Attr(dense[0],"width")} wide for a closest gap of {closest}");
    // The axis is inset by half a slot, so the first and last bars stand whole at the plot's edges.
    var even=Capsules(Svg(Ranges(Enumerable.Range(0,40).Select(i=>ChartPoint.Interval(i,null,60,120)).ToArray())));
    Check(Math.Abs(Attr(even[0],"x")-76)<.01&&Math.Abs(Attr(even[^1],"x")+Attr(even[^1],"width")-870)<.01,$"the bars run from {Attr(even[0],"x")} to {Attr(even[^1],"x")+Attr(even[^1],"width")}");
    // A line beside them shares the inset axis.
    var beside=Svg(Ranges(Enumerable.Range(0,5).Select(i=>ChartPoint.Interval(i,null,60,120)).ToArray()) with{Kind=ChartKind.Line,Series=[new("Range",Enumerable.Range(0,5).Select(i=>ChartPoint.Interval(i,90,60,120)).ToArray()){Kind=ChartKind.Range},new("Resting",Enumerable.Range(0,5).Select(i=>new ChartPoint(i,55)).ToArray())]});
    var markers=beside.Descendants(ns+"g").Where(g=>(string?)g.Attribute("data-series")=="1").Select(g=>Attr(g.Element(ns+"circle")!,"cx")).ToArray();
    Check(markers.Zip(Capsules(beside)).All(p=>Close(p.First,Centre(p.Second)))&&Close(markers[0],93),"the line and the bars do not share X");
    // On a column chart a range series takes its share of each category's slot, beside the columns, centred in its share.
    var mixed=Svg(Spec(ChartKind.Column) with{Series=[new("Rain",[new(0,20,"A"),new(1,30,"B"),new(2,25,"C")]),new("Temperature",[ChartPoint.Interval(0,15,8,24,"A"),ChartPoint.Interval(1,16,9,25,"B"),ChartPoint.Interval(2,17,10,26,"C")]){Kind=ChartKind.Range}]});
    double band=794/3d,share=band*.72/2;double CY(double v)=>344-v/30*266;
    var bars=Capsules(mixed);var rain=mixed.Descendants(ns+"g").Where(g=>(string?)g.Attribute("data-series")=="0").Select(g=>g.Elements().Last()).ToArray();
    for(var i=0;i<3;i++)
    {
        Check(Close(Attr(bars[i],"x"),76+i*band+band*.14+share+(share-18)/2)&&Close(Attr(bars[i],"width"),18)&&Close(Attr(bars[i],"y"),CY(24+i))&&Close(Attr(bars[i],"height"),CY(8+i)-CY(24+i)),$"bar {i}: {bars[i]}");
        Check(Close(Attr(rain[i],"x"),76+i*band+band*.14)&&Close(Attr(rain[i],"width"),share),$"column {i} lost its share: {rain[i]}");
    }
});
Test("Range: drawn from no baseline, so a reversed axis puts the low end on top, a logarithmic one measures both ends, and the axis spans the bars, not zero",()=>{
    // Sleep timing: bedtime to waking on a reversed time-of-day axis from 22:00 to 08:00, earlier at the top.
    var timing=new ChartSpec{Kind=ChartKind.Range,YFormat=ValueFormat.TimeOfDay,YReversed=true,YMin=79200,YMax=115200,Series=[new("Sleep",[ChartPoint.Interval(0,null,82800,109800,"Mon"),ChartPoint.Interval(1,null,84600,111600,"Tue")])]};
    double TY(double v)=>78+(v-79200)/36000*266;
    var doc=Svg(timing);var bars=Capsules(doc);
    Check(Close(Attr(bars[0],"y"),TY(82800))&&Close(Attr(bars[0],"height"),TY(109800)-TY(82800))&&Close(Attr(bars[1],"y"),TY(84600)),$"{bars[0]}");
    Check(bars[0].Parent!.Attribute("aria-label")!.Value=="Mon: 23:00 to 06:30"&&bars[1].Parent!.Attribute("aria-label")!.Value=="Tue: 23:30 to 07:00",bars[0].Parent!.Attribute("aria-label")!.Value);
    // Ten hours take three-hour steps, on the hour, each label 4 pixels below its tick on the left.
    foreach(var (tick,label) in new[]{(86400d,"00:00"),(97200d,"03:00"),(108000d,"06:00")})
        Check(doc.Descendants(ns+"text").Any(t=>t.Value==label&&Close(Attr(t,"y"),TY(tick)+4)),$"no {label} at {TY(tick)}");
    // On a logarithmic axis from 1 to 1000 both ends and the dot are measured in decades.
    var log=Svg(new ChartSpec{Kind=ChartKind.Range,YAxis=AxisKind.Log,YMin=1,YMax=1000,Series=[new("Load",[ChartPoint.Interval(0,10,2,50),ChartPoint.Interval(1,100,20,500)])]});
    double LY(double v)=>344-Math.Log10(v)/3*266;
    var logged=Capsules(log);
    Check(Close(Attr(logged[0],"y"),LY(50))&&Close(Attr(logged[0],"height"),LY(2)-LY(50))&&Close(Attr(logged[1].Parent!.Element(ns+"circle")!,"cy"),LY(100)),"a log axis does not measure the bar");
    // Without bounds the axis runs from the lowest low to the highest high: no zero below the bars.
    var floating=Svg(new ChartSpec{Kind=ChartKind.Range,Series=[new("Heart rate",[ChartPoint.Interval(0,70,52,168,"Mon"),ChartPoint.Interval(1,68,48,150,"Tue")])]});
    var free=Capsules(floating);
    Check(Close(Attr(free[0],"y"),78)&&Close(Attr(free[1],"y")+Attr(free[1],"height"),344)&&!floating.Descendants(ns+"text").Any(t=>t.Value=="0"),"the axis does not span the bars, or reaches zero");
    // A bar takes its point's colour ahead of its series', and its dot follows.
    var coloured=Svg(new ChartSpec{Kind=ChartKind.Range,Series=[new("Heart rate",[ChartPoint.Interval(0,70,52,168) with{Color="#123456"},ChartPoint.Interval(1,null,48,150)],"#ABCDEF")]});
    Check(Capsules(coloured).Select(c=>(string?)c.Attribute("fill")).SequenceEqual(["#123456","#ABCDEF"])&&(string?)coloured.Descendants(ns+"circle").Single(e=>e.Parent?.Attribute("data-point") is not null).Attribute("stroke")=="#123456","a point's colour does not win");
});
Test("Range: each bar is a labelled button that reads its day, its two ends and its average, its series named only beside another range",()=>{
    var day=Utc(2026,9,12);
    var spec=new ChartSpec{Kind=ChartKind.Range,XAxis=AxisKind.Time,Series=[new("Heart rate",[ChartPoint.Interval(day,74,52,168,"12 Sep"),ChartPoint.Interval(day+86_400_000,null,50,140),ChartPoint.Interval(day+2*86_400_000,70,49,120,"14 Sep")])]};
    var doc=Svg(spec);
    var marks=doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
    Check(marks.Select(g=>g.Attribute("aria-label")!.Value).SequenceEqual(["12 Sep: 52 to 168, average 74","13 Sep 2026: 50 to 140","14 Sep: 49 to 120, average 70"]),string.Join(" | ",marks.Select(g=>g.Attribute("aria-label")!.Value)));
    Check(marks.Select(g=>(g.Attribute("data-series")!.Value,g.Attribute("data-point")!.Value,g.Attribute("role")!.Value,g.Attribute("tabindex")!.Value)).SequenceEqual([("0","0","button","0"),("0","1","button","0"),("0","2","button","0")]),"a bar is not a button for its point");
    var two=Svg(spec with{Series=[spec.Series[0],new("Last year",[ChartPoint.Interval(day,70,50,150,"12 Sep")])]});
    Check(two.Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")=="Heart rate, 12 Sep: 52 to 168, average 74")&&two.Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")=="Last year, 12 Sep: 50 to 150, average 70"),"two range series are not told apart");
    // In a duration format, and with a line beside it, whose points keep their own names.
    var paced=Svg(new ChartSpec{Kind=ChartKind.Line,YFormat=ValueFormat.Duration,Series=[new("Pace",[new(1,300),new(2,295)]),new("Spread",[ChartPoint.Interval(1,300,280,330,"km 1"),ChartPoint.Interval(2,295,270,320,"km 2")]){Kind=ChartKind.Range}]});
    Check(paced.Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")=="km 1: 4:40 to 5:30, average 5:00")&&paced.Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")=="Pace: 1, 5:00"),"a range beside a line does not read its format");
    // Its legend key is a capsule standing upright.
    var key=XDocument.Parse(ChartSvg.LegendKey(spec,0)).Root!.Elements().Single();
    Check(Attr(key,"width")==6&&Attr(key,"height")==9&&Attr(key,"rx")==3&&(string?)key.Attribute("fill")==ChartStyle.Light.Series[0],key.ToString());
});
Test("Time of day: seconds since a midnight read HH:mm, rounded to the minute and wrapping at 24 hours",()=>{
    var clock=new Axis(AxisKind.Linear,0,1){ValueFormat=ValueFormat.TimeOfDay};
    foreach(var (seconds,text) in new[]{(84600d,"23:30"),(110400d,"06:40"),(0d,"00:00"),(86400d,"00:00"),(-1800d,"23:30"),(29.9,"00:00"),(30d,"00:01"),(86399d,"00:00"),(172800+3600*13.5,"13:30"),(45240d,"12:34")})
        Check(clock.Format(seconds)==text,$"{seconds} reads {clock.Format(seconds)}, not {text}");
    // On a chart: ticks, tooltips and the data table read the clock; CSV keeps the seconds.
    var bed=Spec() with{YFormat=ValueFormat.TimeOfDay,Series=[new("Bedtime",[new(0,82800),new(1,84600),new(2,88200)])]};
    var doc=Svg(bed);
    Check(doc.Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")=="Bedtime: 1, 23:30")&&doc.Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")=="Bedtime: 2, 00:30"),"a point does not read the clock");
    Check(ChartExport.Csv(bed).Contains("\"Bedtime\",1,84600,"),"CSV does not keep the seconds");
});
Test("Time of day: ticks land on whole hours, or on half and quarter hours when the range is short, and minor lines divide them evenly",()=>{
    IReadOnlyList<(double Value,string Label)> Ticks(double min,double max,int count=5)=>new Axis(AxisKind.Linear,min,max){ValueFormat=ValueFormat.TimeOfDay}.Ticks(count);
    // A night from 22:30 to 07:30 takes two-hour steps on the even hour.
    Check(Ticks(81000,113400).Select(t=>t.Label).SequenceEqual(["00:00","02:00","04:00","06:00"])&&Ticks(81000,113400).All(t=>t.Value%7200==0),string.Join(",",Ticks(81000,113400)));
    // Two hours take half hours, and three quarters of an hour take quarters.
    Check(Ticks(82800,90000).Select(t=>t.Label).SequenceEqual(["23:00","23:30","00:00","00:30","01:00"]),string.Join(",",Ticks(82800,90000)));
    Check(Ticks(84600,87300).Select(t=>t.Label).SequenceEqual(["23:30","23:45","00:00","00:15"]),string.Join(",",Ticks(84600,87300)));
    // Ten hours with five ticks take three-hour steps; a day takes six-hour ones; every step is a whole hour from an hour up.
    Check(Ticks(79200,115200).Select(t=>t.Label).SequenceEqual(["00:00","03:00","06:00"])&&Ticks(0,86400).Select(t=>t.Label).SequenceEqual(["00:00","06:00","12:00","18:00","00:00"]),string.Join(",",Ticks(0,86400)));
    foreach(var (min,max) in new[]{(0d,3600d),(3000d,90000d),(80000d,120000d),(-7200d,7200d),(0d,4*86400d)})
        Check(Ticks(min,max).Count is >=2 and <=5&&Ticks(min,max).All(t=>t.Value%900==0&&t.Value>=min&&t.Value<=max),$"{min} to {max}: {string.Join(",",Ticks(min,max))}");
    // An hour's step is divided into quarters and a two-hour one into half hours; a quarter-hour step into fives.
    var minors=new Axis(AxisKind.Linear,79200,108000){ValueFormat=ValueFormat.TimeOfDay}.MinorTicks(5);
    Check(minors.Count>0&&minors.All(v=>v%1800==0&&v%7200!=0),string.Join(",",minors));
    var quarters=new Axis(AxisKind.Linear,84600,87300){ValueFormat=ValueFormat.TimeOfDay}.MinorTicks(5);
    Check(quarters.Count>0&&quarters.All(v=>v%300==0&&v%900!=0),string.Join(",",quarters));
});
Test("Timeline and range: what has no meaning on them is refused, each with its reason",()=>{
    string Refusal(ChartSpec spec){try{ChartSvg.Render(spec);}catch(ArgumentException error){return error.Message;}throw new Exception("a chart was accepted that should not be");}
    var night=Hypnogram();var range=Ranges(ChartPoint.Interval(0,70,50,150),ChartPoint.Interval(1,72,52,160));
    ChartSpec Lane(ChartSeries series)=>night with{Series=[series,..night.Series.Skip(1)]};
    ChartSpec Bar(ChartPoint point)=>range with{Series=[range.Series[0] with{Points=[point]}]};
    foreach(var (spec,reason) in new (ChartSpec,string)[]{
        (night with{YZones=Tiers()},"no zones"),(Lane(night.Series[0] with{Trend=true}),"no trend line"),(Lane(night.Series[0] with{Secondary=true}),"no secondary axis"),
        (Lane(night.Series[0] with{Kind=ChartKind.Line}),"own kind"),(night with{Panes=[new()]},"no panes"),(Lane(night.Series[0] with{Pane=1}),"no panes"),
        (night with{Annotations=[new(AnnotationAxis.Y,1)]},"takes X annotations"),(night with{XFormat=ValueFormat.Number,XAxis=AxisKind.Log},"linear or a time X axis"),
        (night with{YReversed=true},"lanes"),(night with{YAxis=AxisKind.Log},"lanes"),(night with{YFormat=ValueFormat.Compact},"lanes"),(night with{YMin=0},"lanes"),(night with{Y2Max=5},"lanes"),(night with{YTickLabels=TickLabels.Ends},"lanes"),
        (Lane(new("Awake",[new(82800,1)])),"ChartPoint.Span"),(Lane(new("Awake",[ChartPoint.Span(83400,83400)])),"above its X"),(Lane(new("Awake",[ChartPoint.Span(83400,82800)])),"above its X"),
        (Lane(new("Awake",[ChartPoint.Span(82800,double.NaN)])),"above its X"),(Lane(new("Awake",[ChartPoint.Span(82800,84000),ChartPoint.Span(83400,85000)])),"cannot overlap"),
        (Lane(new("Awake",[ChartPoint.Span(97200,97380),ChartPoint.Span(82800,99000)])),"cannot overlap"),(Lane(night.Series[0] with{Points=[night.Series[0].Points[0] with{Color="#123456"}]}),"state a timeline's lane"),
        (Lane(night.Series[0] with{Gradient=[new(0,"#3F87D9"),new(1,"#DD4B45")]}),"gradient"),(Lane(night.Series[0] with{StrokeWidth=3}),"stroke width"),
        (new ChartSpec{Kind=ChartKind.Timeline,XAxis=AxisKind.Time,Series=[new("A",[ChartPoint.Span(Utc(2026,1,1),TimeAxis.MaxValue+1)])]},"Unix milliseconds"),
        (range with{Series=[range.Series[0] with{ProjectedFrom=.5}]},"lines or areas"),(range with{Series=[range.Series[0] with{Trend=true}]},"trend line"),(range with{Series=[range.Series[0] with{Zones=Tiers()}]},"range series takes no zones"),
        (Bar(new ChartPoint(0,70){Low=50}),"both Low and High"),(Bar(new ChartPoint(0,70)),"both Low and High"),(Bar(ChartPoint.Interval(0,null,160,150)),"no greater than High"),
        (Bar(ChartPoint.Interval(0,170,50,150)),"between its Low and its High"),(Bar(ChartPoint.Interval(0,40,50,150)),"between its Low and its High"),(Bar(ChartPoint.Interval(0,null,50,double.PositiveInfinity)),"finite"),
        (range with{YAxis=AxisKind.Log,YMin=null,Series=[range.Series[0] with{Points=[ChartPoint.Interval(0,null,0,150)]}]},"positive range bounds"),
        (range with{Series=[range.Series[0] with{Points=[ChartPoint.Interval(0,70,50,150),ChartPoint.Interval(0,72,52,160)]}]},"unique X values"),
        (range with{Series=[range.Series[0] with{Curve=LineCurve.Smooth}]},"smooth or stepped"),(range with{Series=[range.Series[0] with{Markers=MarkerStyle.Filled}]},"Marker styles"),
        (Spec(ChartKind.Bar) with{Series=[new("S",[ChartPoint.Interval(0,2,1,3)]){Kind=ChartKind.Range}]},"own kind"),(Spec(ChartKind.Donut) with{Series=[new("S",[ChartPoint.Interval(0,2,1,3)]){Kind=ChartKind.Range}]},"own kind"),
        (Spec() with{Series=[new("S",[new ChartPoint(0,1){XEnd=2},new(1,2)])]},"timeline charts and to series drawn as blocks"),(range with{Series=[range.Series[0] with{Points=[ChartPoint.Interval(0,70,50,150) with{XEnd=1}]}]},"timeline charts and to series drawn as blocks"),
        (Spec() with{TimelineConnectors=false},"only a timeline"),(range with{TimelineConnectors=false},"only a timeline"),
        (Spec() with{XAxis=AxisKind.Time,XFormat=ValueFormat.TimeOfDay,Series=[new("S",[new(Utc(2026,1,1),1)])]},"time-of-day"),(Spec() with{XAxis=AxisKind.Log,XFormat=ValueFormat.TimeOfDay,Series=[new("S",[new(1,1)])]},"time-of-day"),
        (Spec() with{YAxis=AxisKind.Log,YFormat=ValueFormat.TimeOfDay},"time-of-day"),(Spec() with{Y2Axis=AxisKind.Log,Y2Format=ValueFormat.TimeOfDay},"time-of-day"),
        (Spec() with{Panes=[new(){YAxis=AxisKind.Log,YFormat=ValueFormat.TimeOfDay}],Series=[Spec().Series[0],Spec().Series[0] with{Pane=1}]},"time-of-day")})
        Check(Refusal(spec).Contains(reason),$"{spec.Kind}: \"{Refusal(spec)}\" does not say \"{reason}\"");
    // Within the rules: spans that touch in a lane, spans that overlap across lanes, X annotations, lanes on the right, empty lanes,
    // a missing day, a range without a value, a dot at either end, and ranges on reversed, logarithmic and right-hand axes.
    foreach(var spec in new[]{Lane(new("Awake",[ChartPoint.Span(82800,83400),ChartPoint.Span(83400,84000)])),night with{Annotations=[new(AnnotationAxis.X,90000){Label="Alarm"},new(AnnotationAxis.X,84000){To=86000}]},
        night with{YAxisSide=AxisSide.Right,MinorGridlines=true,XLabel="Time"},night with{Series=[..night.Series,new("Unscored",[])]},Bar(new ChartPoint(0,null)),Bar(ChartPoint.Interval(0,50,50,150)),Bar(ChartPoint.Interval(0,150,50,150)),
        range with{YReversed=true,YAxis=AxisKind.Log,YMin=10,YMax=1000,YZones=Tiers(),Annotations=[new(AnnotationAxis.Y,100),new(AnnotationAxis.X,.5)]},
        Spec() with{Y2Label="Spread",Series=[Spec().Series[0],new("Spread",[ChartPoint.Interval(0,2,1,3),ChartPoint.Interval(1,5,4,6)]){Kind=ChartKind.Range,Secondary=true}]}})
        ChartSvg.Render(spec);
    var overlapping=new ChartSpec{Kind=ChartKind.Timeline,Series=[new("Stress",[ChartPoint.Span(0,10)]),new("Activity",[ChartPoint.Span(5,15)])]};
    Check(Svg(overlapping).Descendants(ns+"g").Count(g=>g.Attribute("data-point") is not null)==2,"spans overlapping across lanes are refused");
    Check(ChartSvg.Render(night with{Series=[]}).Contains("No data to display")&&ChartSvg.Render(range with{Series=[new("S",[new(0,null)])]}).Contains("No data to display"),"an empty timeline or range does not draw its empty state");
});
Test("Timeline and range: specs survive JSON, a request that names no connectors draws them, and CSV carries each span's end and each bar's bounds",()=>{
    var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    var timing=Ranges(ChartPoint.Interval(0,null,82800,109800,"Mon"),ChartPoint.Interval(1,95000,84600,111600,"Tue")) with{YFormat=ValueFormat.TimeOfDay,YReversed=true,YMin=null,YMax=null};
    foreach(var spec in new[]{Hypnogram(),Hypnogram(false),timing,Hypnogram() with{XFormat=ValueFormat.Number,XAxis=AxisKind.Time,Series=[new("REM",[ChartPoint.Span(Utc(2026,9,27,1),Utc(2026,9,27,2))])],TimeZone="Europe/London"}})
    {
        var json=System.Text.Json.JsonSerializer.Serialize(spec,options);
        Check(ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,options)!)==ChartSvg.Render(spec),$"{spec.Kind} changed in transit");
    }
    var written=System.Text.Json.JsonSerializer.Serialize(Hypnogram(false),options)+System.Text.Json.JsonSerializer.Serialize(timing,options);
    Check(written.Contains("\"kind\":\"Timeline\"")&&written.Contains("\"xEnd\":83400")&&written.Contains("\"timelineConnectors\":false")&&written.Contains("\"kind\":\"Range\"")&&written.Contains("\"yFormat\":\"TimeOfDay\"")&&written.Contains("\"low\":82800"),written);
    var request=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Timeline\",\"xFormat\":\"TimeOfDay\",\"series\":[{\"name\":\"Light\",\"points\":[{\"x\":82800,\"xEnd\":86400}]},{\"name\":\"Deep\",\"points\":[{\"x\":86400,\"xEnd\":90000}]}]}",options)!;
    Check(request.TimelineConnectors&&request.Series[0].Points[0].XEnd==86400&&request.Series[0].Points[0].Y is null&&ChartSvg.Render(request).Contains("class='lumen-connectors'")&&ChartSvg.Render(request).Contains("aria-label='Deep: 00:00 to 01:00, 1 h'"),"a request without connectors does not draw them");
    // CSV adds an XEnd column to a timeline, after a time axis's own columns, and a range's bounds as a band's are.
    var csv=ChartExport.Csv(Hypnogram());
    Check(csv.StartsWith("Series,X,Y,Label,Size,XEnd\r\n")&&csv.Contains("\"Awake\",82800,,\"\",1,83400\r\n")&&csv.Contains("\"Light\",91800,,\"Second cycle\",1,97200\r\n"),csv);
    var timed=ChartExport.Csv(new ChartSpec{Kind=ChartKind.Timeline,XAxis=AxisKind.Time,Series=[new("REM",[ChartPoint.Span(Utc(2026,9,27,1),Utc(2026,9,27,2))])]});
    Check(timed.StartsWith("Series,X,XTime,Y,Label,Size,XEnd\r\n")&&timed.Contains($"\"REM\",{Utc(2026,9,27,1).ToString(CultureInfo.InvariantCulture)},2026-09-27T01:00:00.000Z,,\"\",1,{Utc(2026,9,27,2).ToString(CultureInfo.InvariantCulture)}"),timed);
    var bars=ChartExport.Csv(timing);
    Check(bars.StartsWith("Series,X,Y,Label,Size,Low,High\r\n")&&bars.Contains("\"Heart rate\",0,,\"Mon\",1,82800,109800")&&bars.Contains("\"Heart rate\",1,95000,\"Tue\",1,84600,111600"),bars);
    Check(!ChartExport.Csv(Spec()).Contains("XEnd")&&!ChartExport.Csv(Sample(ChartKind.Band)).Contains("XEnd"),"another kind's CSV carries XEnd");
});
Test("Charts that use none of 0.27.0 draw byte for byte as 0.26.0 did, in both finishes, gradient IDs included",()=>{
    // Five rows of the release baseline, hashed by 0.26.0: its own specs rebuilt here, refined and classic.
    ChartPoint[] Points()=>Enumerable.Range(0,12).Select(i=>new ChartPoint(i,10+i*3+(i%3)*4,$"P{i}")).ToArray();
    ChartSpec Base(ChartKind kind,ChartTheme theme)=>new(){Kind=kind,Theme=theme,Title="Baseline",Description="Default output",Series=[new("A",Points()),new("B",Points().Select(p=>p with{Y=p.Y+5}).ToArray())]};
    var climb=Enumerable.Range(0,120).Select(i=>new ChartPoint(i*30,Math.Round(24+16*Math.Sin(i/9d)+6*Math.Sin(i/3.1),1))).ToArray();
    (string Row,ChartSpec Spec,bool Titles,string Refined,string Classic)[] rows=[
        ("Line/Light/True",Base(ChartKind.Line,ChartTheme.Light),true,"D991008D3106193D","CB7DF56598CE861F"),
        ("Area/Dark/False",Base(ChartKind.Area,ChartTheme.Dark),false,"EDBB1E4E6F34DC8D","CBF026C145B854C8"),
        ("Band/Light/True",new ChartSpec{Kind=ChartKind.Band,Series=[new("F",Enumerable.Range(0,8).Select(i=>ChartPoint.Interval(i,10+i,8+i,13+i)).ToArray())]},true,"5F679A5E47A0F467","BA35FFE76F28B956"),
        ("finish/smooth-fade-area",new ChartSpec{Kind=ChartKind.Area,Title="Smooth fade",XFormat=ValueFormat.Duration,Series=[new("Climb",climb){Curve=LineCurve.Smooth,Fill=AreaFill.Fade,StrokeWidth=2,Markers=MarkerStyle.None}]},true,"975F1AFFAAD75178","4E9548279D4A245D"),
        ("finish/gradient-log",Base(ChartKind.Line,ChartTheme.Light) with{Title="Gradient on a log axis",YAxis=AxisKind.Log,MinorGridlines=true,
            Series=[new("Load",Points().Select((p,i)=>p with{Y=Math.Pow(10,i*.3)}).ToArray()){Gradient=[new(1,"#2E9B58"),new(30,"#A88200"),new(1000,"#DD4B45")],StrokeWidth=3}]},true,"D6E1DE0EDA8D6A08","A7C9FD0BF532D07B")];
    foreach(var (row,spec,titles,refined,classic) in rows)
    {
        Check(Hash16(ChartSvg.Render(spec,includeTitles:titles))==refined,$"{row} is not 0.26.0's: {Hash16(ChartSvg.Render(spec,includeTitles:titles))}");
        Check(Hash16(ChartSvg.Render(Classic(spec),includeTitles:titles))==classic,$"{row} is not 0.26.0's in the classic finish: {Hash16(ChartSvg.Render(Classic(spec),includeTitles:titles))}");
        // Spelling out the connectors' default changes nothing, gradient IDs included.
        Check(ChartSvg.Render(spec with{TimelineConnectors=true},includeTitles:titles)==ChartSvg.Render(spec,includeTitles:titles),$"{row} changes when its connectors are spelled out");
    }
});
Test("Timeline and range: the component zooms their X, reads them in its legend, status line and data table, and keeps a hidden lane",()=>{
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    string Drawn(LumenChart chart)=>(string)typeof(LumenChart).GetField("svg",flags)!.GetValue(chart)!;
    var night=Hypnogram();
    var html=RenderInside(null,night);
    Check(html.Contains("aria-label=\"Zoom in\"")&&new[]{"Awake 0:13, 4 %","REM 0:30, 8 %","Light 4:17, 71 %","Deep 1:00, 17 %"}.All(html.Contains),"the timeline offers no zoom, or its legend does not total the states");
    double before=0,after=0;string hidden="";
    var shown=Operate(night,async chart=>{
        typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);
        before=Attr(XDocument.Parse(Drawn(chart)).Descendants(ns+"rect").First(r=>(string?)r.Attribute("class")=="lumen-span"&&r.Parent!.Attribute("data-series")!.Value=="2"&&r.Parent!.Attribute("data-point")!.Value=="1"),"width");
        // Zooming in halves the X axis about its middle, so a span in the middle is drawn twice as wide.
        typeof(LumenChart).GetMethod("Zoom",flags)!.Invoke(chart,[.5]);
        after=Attr(XDocument.Parse(Drawn(chart)).Descendants(ns+"rect").First(r=>(string?)r.Attribute("class")=="lumen-span"&&r.Parent!.Attribute("data-series")!.Value=="2"&&r.Parent!.Attribute("data-point")!.Value=="1"),"width");
        typeof(LumenChart).GetMethod("ResetView",flags)!.Invoke(chart,[]);
        typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[0]);hidden=Drawn(chart);typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[0]);
        await chart.SelectPoint(2,1);
    });
    Check(Close(after,2*before),$"zooming drew the span {after} wide, {before} before");
    Check(shown.Contains("<tr><td>Awake</td><td>23:00</td><td>to 23:10</td></tr>")&&shown.Contains("Light: 01:30 to 03:00</span>"),"the table or the status line does not read a span");
    var lanes=XDocument.Parse(hidden);
    Check(!lanes.Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")=="Awake: 23:00 to 23:10, 10 min")&&lanes.Descendants(ns+"text").Count(t=>t.Value is "Awake" or "REM" or "Light" or "Deep")==4,"hiding a state removed its lane or kept its spans");
    // A range chart zooms as a continuous chart does, and reads each bar's bounds and average.
    var heart=new ChartSpec{Kind=ChartKind.Range,XAxis=AxisKind.Time,Series=[new("Heart rate",[ChartPoint.Interval(Utc(2026,9,12),74,52,168,"12 Sep"),ChartPoint.Interval(Utc(2026,9,13),null,50,140,"13 Sep")])]};
    Check(RenderInside(null,heart).Contains("aria-label=\"Zoom in\""),"a range chart offers no zoom");
    var read=Operate(heart,async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(0,0);});
    Check(read.Contains("<tr><td>Heart rate</td><td>12 Sep</td><td>74 (52 to 168)</td></tr>")&&read.Contains("<tr><td>Heart rate</td><td>13 Sep</td><td>50 to 140</td></tr>")&&read.Contains("Heart rate: 12 Sep = 52 to 168, average 74</span>"),"the table or the status line does not read a bar");
});
// The gallery's sleep section reads the same athlete: last night is the HRV chart's last night and this morning's readiness.
Test("Sports page: last night's stages fill the night the sleep-timing chart ends on, and its deep sleep follows the HRV the readiness reads",()=>{
    var nights=SportsData.Nights(athlete);var last=nights[^1];
    var hypnogram=Sports("hypnogram");var timing=Sports("sleep-timing").Series.Single().Points;
    var spans=hypnogram.Series.SelectMany(s=>s.Points.Select(p=>(Stage:s.Name,p.X,End:p.XEnd!.Value))).OrderBy(s=>s.X).ToArray();
    var evening=SportsData.When(SportsData.Today.AddDays(-1));
    // The spans run without a gap or an overlap from bedtime to waking, which are the timing chart's last bar, on the morning
    // the HRV chart ends on.
    Check(spans.Zip(spans.Skip(1)).All(p=>p.First.End==p.Second.X)&&spans[0].X==evening+last.Bedtime*1000&&spans[^1].End==evening+last.Wake*1000,"the stages do not fill the night");
    Check(nights.Count==SportsData.SleepNights&&last.Morning==SportsData.Today&&timing[^1].Low==last.Bedtime&&timing[^1].High==last.Wake&&timing[^1].X==Sports("hrv").Series[0].Points[^1].X,"the timing chart does not end on last night");
    Check(hypnogram.Series.Select(s=>s.Name).SequenceEqual(SportsData.SleepStages)&&spans.Zip(spans.Skip(1)).All(p=>p.First.Stage!=p.Second.Stage)&&spans.All(s=>s.X%60_000==0),"the lanes are not the stages, or a stage follows itself, or a span leaves the minute");
    Check(hypnogram.Description.Contains($"HRV {athlete.Hrv[^1].ToString(CultureInfo.InvariantCulture)} ms")&&Sports("readiness").Series[0].Points[0].Y==SportsData.Readiness(athlete)[^1],"the hypnogram does not name the readiness's HRV");
    double Deep(Night n)=>n.Stages.Where(s=>s.Stage=="Deep").Sum(s=>s.To-s.From);
    Check(hypnogram.Title==$"{SportsData.HoursMinutes(last.Wake-last.Bedtime-last.Stages.Where(s=>s.Stage=="Awake").Sum(s=>s.To-s.From))} asleep, {SportsData.HoursMinutes(Deep(last))} deep","the title does not total the stages");
    // Each night's deep sleep rises and its lowest heart rate falls with its HRV against the 28 nights before, as readiness reads it.
    var rolling=Statistics.Rolling(athlete.Hrv.Select(v=>(double?)v).ToArray(),SportsData.BaselineNights);
    var z=nights.Select((n,i)=>{var day=SportsData.Weeks*7-SportsData.SleepNights+i;var w=rolling[day+SportsData.BaselineNights-1]!;return (athlete.Hrv[day+SportsData.BaselineNights]-w.Mean)/w.Deviation;}).ToArray();
    double Correlation(double[] a,double[] b){double ma=a.Average(),mb=b.Average();return a.Zip(b).Sum(p=>(p.First-ma)*(p.Second-mb))/Math.Sqrt(a.Sum(v=>(v-ma)*(v-ma))*b.Sum(v=>(v-mb)*(v-mb)));}
    Check(Correlation(nights.Select(Deep).ToArray(),z)>.5&&Correlation(nights.Select(n=>n.LowestHeartRate).ToArray(),z)<-.5,"deep sleep and the lowest heart rate do not follow the HRV");
    Check((Deep(last)==nights.Min(Deep))==(z[^1]==z.Min()),"last night's HRV and its deep sleep disagree");
    Check(SportsData.Nights(SportsData.Simulate()).Select(n=>(n.Bedtime,n.Wake,n.Stages.Count,n.LowestHeartRate)).SequenceEqual(nights.Select(n=>(n.Bedtime,n.Wake,n.Stages.Count,n.LowestHeartRate))),"two simulations sleep differently");
});
Test("Sports page: each day's heart rate runs from the night's lowest to the day's highest, today's the run's, its average between",()=>{
    var nights=SportsData.Nights(athlete);var bars=Sports("heart-range").Series.Single().Points;var timing=Sports("sleep-timing").Series.Single().Points;
    Check(bars.Count==SportsData.SleepNights&&bars.Select(b=>b.X).SequenceEqual(timing.Select(t=>t.X))&&bars.Select(b=>b.Label).SequenceEqual(timing.Select(t=>t.Label)),"the two charts do not show the same days");
    for(var i=0;i<bars.Count;i++)
    {
        // The day's highest is its hardest session's peak, unless that stayed under an ordinary day's 105 to 125.
        var day=nights[i].Morning;var peak=athlete.Sessions.Where(s=>s.Day==day).Select(s=>s.PeakHeartRate).DefaultIfEmpty(0).Max();
        Check(bars[i].Low==nights[i].LowestHeartRate&&bars[i].Y>bars[i].Low&&bars[i].Y<bars[i].High&&(bars[i].High==peak||peak<=125&&bars[i].High is >=105 and <=125),$"{day}: {bars[i]}");
    }
    Check(bars[^1].High==Sports("stream").Series.Single(s=>s.Name=="Heart rate").Points.Max(p=>p.Y)&&latest.PeakHeartRate==latest.Track!.HeartRate.Max(),"today's highest is not the run's");
    Check(athlete.Sessions.Where(s=>s.Track is not null).All(s=>s.PeakHeartRate==s.Track!.HeartRate.Max()),"a run's peak is not its highest sample");
});
// 0.28.0: calendars. A day's points stand at noon UTC unless a test says otherwise, so each falls on its own date in UTC and in
// New York alike. A day's mark is found by the date its name starts with, and its cell by the shape the mark colours.
double Noon(DateOnly day,double hours=12)=>TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero))+hours*3600e3;
ChartSpec Days(DateOnly from,int count,Func<int,double?> value)=>new(){Kind=ChartKind.Calendar,XAxis=AxisKind.Time,Title="Days",
    Series=[new("Stress",Enumerable.Range(0,count).Select(i=>new ChartPoint(Noon(from.AddDays(i)),value(i))).ToArray())]};
string Dated(DateOnly day)=>day.ToString("ddd d MMM yyyy",CultureInfo.InvariantCulture);
XElement DayGroup(XDocument doc,DateOnly day)=>doc.Descendants(ns+"g").Single(g=>g.Attribute("data-point") is not null&&((string)g.Attribute("aria-label")!).StartsWith(Dated(day)));
XElement DayShape(XDocument doc,DateOnly day)=>DayGroup(doc,day).Elements().Single(e=>(string?)e.Attribute("class")=="lumen-day");
(double X,double Y) DayAt(XDocument doc,DateOnly day){var shape=DayShape(doc,day);return shape.Name==ns+"rect"?(Attr(shape,"x"),Attr(shape,"y")):(Attr(shape,"cx"),Attr(shape,"cy"));}
XElement[] DayTracks(XDocument doc)=>doc.Descendants().Where(e=>(string?)e.Attribute("class")=="lumen-track").ToArray();
string[] KeyText(XDocument doc)=>doc.Root!.Elements(ns+"text").Where(t=>(string?)t.Attribute("font-size")=="11"&&t.Attribute("class") is null).Select(t=>t.Value).ToArray();
Test("Calendar: four weeks at 900 by 420 fill the height on a 41-pixel pitch, 35-pixel cells 6 apart, centred across, each day in its week's column and its weekday's row",()=>{
    // Across, 822 pixels are left beside the weekday names, so four weeks could take 207 each; down, 281 between the month names
    // and the key, so seven rows take 41 each, 6 of them the gap. The grid is 158 wide, centred from 54 + 332 = 386, and 82 is
    // the top of its rows, under the 18 of the month names.
    var from=new DateOnly(2026,9,28);
    var doc=Svg(Days(from,28,i=>i%7==0?0:10+i) with{YZones=Effort()});
    for(var i=0;i<28;i++)
    {
        var day=from.AddDays(i);
        if(i%7==0){Check(!doc.Descendants(ns+"g").Any(g=>((string?)g.Attribute("aria-label"))?.StartsWith(Dated(day))==true),$"{day}, a rest day, takes focus");continue;}
        var shape=DayShape(doc,day);
        Check(Attr(shape,"x")==386+i/7*41&&Attr(shape,"y")==82+i%7*41&&Attr(shape,"width")==35&&Attr(shape,"height")==35&&Attr(shape,"rx")==3,$"{day}: {shape}");
    }
    // The rest days are empty cells in the grid colour on the Monday row.
    var tracks=DayTracks(doc);
    Check(tracks.Length==4&&tracks.All(t=>(string?)t.Attribute("fill")==ChartStyle.Light.Grid&&Attr(t,"y")==82&&Attr(t,"width")==35)&&tracks.Select(t=>Attr(t,"x")).SequenceEqual([386d,427,468,509]),"the rest days are not empty cells on the Monday row");
    // The grid starts on the last days of September, so the first week is named for October, whose first day it holds.
    var texts=doc.Root!.Elements(ns+"text").ToArray();
    Check(!texts.Any(t=>t.Value=="Sep")&&texts.Single(t=>t.Value=="Oct") is var october&&Attr(october,"x")==386&&Attr(october,"y")==76,"October is not named above the first week");
    foreach(var (name,row) in new[]{("Mon",0),("Wed",2),("Fri",4)})
        Check(texts.Single(t=>t.Value==name) is var label&&Attr(label,"x")==380&&Attr(label,"y")==82+row*41+21.5&&(string?)label.Attribute("text-anchor")=="end",$"{name} is not beside its row");
    Check(!texts.Any(t=>t.Value is "Tue" or "Thu" or "Sat" or "Sun"),"a weekday other than Mon, Wed and Fri is named");
    // The key stands 24 under the grid, at its left edge.
    Check(doc.Root!.Elements(ns+"text").Single(t=>t.Value=="Easy") is var easy&&Attr(easy,"x")==400&&Attr(easy,"y")==387,"the key is not under the grid");
});
Test("Calendar: a day's column and row follow its week and the week start, across a month's end, and each month is named above the week of its first day",()=>{
    var from=new DateOnly(2026,9,7);
    foreach(var start in new[]{DayOfWeek.Monday,DayOfWeek.Sunday})
    {
        var doc=Svg(Days(from,56,_=>5) with{WeekStart=start});
        var origin=DayAt(doc,from);var pitch=DayAt(doc,from.AddDays(1)).Y-origin.Y;
        // Monday 7 September is the first day, in the first column, on the first row from Monday and the second from Sunday.
        var top=origin.Y-(start==DayOfWeek.Monday?0:1)*pitch;
        (int Column,int Row) Place(DateOnly day){var (x,y)=DayAt(doc,day);return ((int)Math.Round((x-origin.X)/pitch),(int)Math.Round((y-top)/pitch));}
        (DateOnly Day,int Column,int Row)[] expected=start==DayOfWeek.Monday
            ? [(new(2026,9,7),0,0),(new(2026,9,13),0,6),(new(2026,9,14),1,0),(new(2026,9,30),3,2),(new(2026,10,1),3,3),(new(2026,10,4),3,6),(new(2026,10,5),4,0),(new(2026,11,1),7,6)]
            : [(new(2026,9,7),0,1),(new(2026,9,12),0,6),(new(2026,9,13),1,0),(new(2026,9,30),3,3),(new(2026,10,1),3,4),(new(2026,10,3),3,6),(new(2026,10,4),4,0),(new(2026,11,1),8,0)];
        foreach(var (day,column,row) in expected)
            Check(Place(day)==(column,row),$"{start}: {day} stands at {Place(day)}, not ({column}, {row})");
        // September is named over the first week, October over the week of its first day and November over the week of its own.
        var months=doc.Root!.Elements(ns+"text").Where(t=>t.Value is "Sep" or "Oct" or "Nov").Select(t=>(t.Value,(int)Math.Round((Attr(t,"x")-origin.X)/pitch))).ToArray();
        Check(months.SequenceEqual(start==DayOfWeek.Monday?[("Sep",0),("Oct",3),("Nov",7)]:[("Sep",0),("Oct",3),("Nov",8)]),$"{start}: {string.Join(", ",months)}");
        // Mon, Wed and Fri name the rows those days stand on.
        var cell=Attr(DayShape(doc,from),"width");
        foreach(var (name,row) in new[]{("Mon",0),("Wed",2),("Fri",4)})
            Check(Close(Attr(doc.Root!.Elements(ns+"text").Single(t=>t.Value==name),"y"),top+(row+(start==DayOfWeek.Sunday?1:0))*pitch+cell/2+4),$"{start}: {name} is not beside its row");
    }
});
Test("Calendar: across New Year a weeks grid keeps counting weeks and names both months, and a months grid names each month with its year",()=>{
    var from=new DateOnly(2026,12,21);
    var weeks=Svg(Days(from,21,_=>5));
    var origin=DayAt(weeks,from);var pitch=DayAt(weeks,from.AddDays(1)).Y-origin.Y;
    var newYear=DayAt(weeks,new DateOnly(2027,1,1));
    Check(Math.Round((newYear.X-origin.X)/pitch)==1&&Math.Round((newYear.Y-origin.Y)/pitch)==4,"1 January 2027 is not on the second week's Friday");
    var texts=weeks.Root!.Elements(ns+"text").ToArray();
    Check(Attr(texts.Single(t=>t.Value=="Dec"),"x")==origin.X&&Attr(texts.Single(t=>t.Value=="Jan"),"x")==origin.X+pitch,"December and January are not named above their weeks");
    // December 2026 begins on a Tuesday, so the 21st is the fourth row's Monday; January 2027 begins on a Friday, in the fifth
    // column, and its grid stands eight columns on from December's, on the same line.
    var months=Svg(Days(from,21,_=>5) with{CalendarLayout=CalendarLayout.Months});
    var (decX,decY)=DayAt(months,from);var (janX,janY)=DayAt(months,new DateOnly(2027,1,1));
    var step=DayAt(months,new DateOnly(2027,1,2)).X-janX;
    Check(Close(DayAt(months,from.AddDays(1)).X-decX,step)&&Close(janX-4*step-decX,8*step)&&Close(decY-3*step,janY),$"the months stand at ({decX}, {decY}) and ({janX}, {janY}), {step} apart");
    var titles=months.Root!.Elements(ns+"text").Where(t=>t.Value.EndsWith(" 2026")||t.Value.EndsWith(" 2027")).ToArray();
    Check(titles.Select(t=>t.Value).SequenceEqual(["December 2026","January 2027"])&&Close(Attr(titles[0],"x"),decX)&&Close(Attr(titles[1],"x"),janX-4*step),string.Join(", ",titles.Select(t=>t.Value)));
    // Within one year the months are named without it, and a weekday's initial stands over each column from the week start.
    var autumn=Svg(Days(new DateOnly(2026,9,1),61,_=>5) with{CalendarLayout=CalendarLayout.Months,WeekStart=DayOfWeek.Sunday});
    Check(autumn.Root!.Elements(ns+"text").Any(t=>t.Value=="September")&&autumn.Root!.Elements(ns+"text").Any(t=>t.Value=="October"),"a month in one year is not named alone");
    Check(autumn.Root!.Elements(ns+"text").Where(t=>(string?)t.Attribute("font-size")=="10").Take(7).Select(t=>t.Value).SequenceEqual(["S","M","T","W","T","F","S"]),"the weekdays are not named from Sunday");
});
Test("Calendar: a day is counted in the chart's time zone, so a moment near midnight falls on that zone's date, and one day's points add up",()=>{
    // 00:30 on Thursday 1 October in New York is 04:30 UTC on the 1st, and 23:30 there is 03:30 UTC on Friday the 2nd.
    var spec=new ChartSpec{Kind=ChartKind.Calendar,XAxis=AxisKind.Time,Series=[new("Stress",[new(Utc(2026,10,1,4)+30*60_000,10),new(Utc(2026,10,2,3)+30*60_000,20)])]};
    string[] Names(XDocument doc)=>doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).Select(g=>(string)g.Attribute("aria-label")!).ToArray();
    var local=Svg(spec with{TimeZone="America/New_York"});var utc=Svg(spec);
    Check(Names(local).SequenceEqual(["Thu 1 Oct 2026: 30"]),string.Join("|",Names(local)));
    Check(Names(utc).SequenceEqual(["Thu 1 Oct 2026: 10","Fri 2 Oct 2026: 20"]),string.Join("|",Names(utc)));
    // The day reports its first point, and in UTC Friday stands a row under Thursday.
    Check((string?)local.Descendants(ns+"g").Single(g=>g.Attribute("data-point") is not null).Attribute("data-point")=="0");
    Check(DayAt(utc,new DateOnly(2026,10,2)).Y>DayAt(utc,new DateOnly(2026,10,1)).Y&&DayAt(utc,new DateOnly(2026,10,2)).X==DayAt(utc,new DateOnly(2026,10,1)).X,"Friday is not under Thursday");
});
Test("Calendar: each day with activity is one focusable mark naming its date, its points' labels, its total and its zone; days without are empty cells that take no focus",()=>{
    var from=new DateOnly(2026,9,14);
    var spec=new ChartSpec{Kind=ChartKind.Calendar,XAxis=AxisKind.Time,YZones=Effort(),Series=[new("Stress",[
        new(Noon(from),40),new(Noon(from.AddDays(1),9),30,"Ride"),new(Noon(from.AddDays(1),18),24,"Run"),new(Noon(from.AddDays(1),20),null),
        new(Noon(from.AddDays(2)),0),new(Noon(from.AddDays(3)),null),new(Noon(from.AddDays(6)),150,"Long ride")])]};
    var doc=Svg(spec);
    var marks=doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
    Check(marks.Select(g=>((string?)g.Attribute("aria-label"),(string?)g.Attribute("data-point"))).SequenceEqual([("Mon 14 Sep 2026: 40, Easy","0"),("Tue 15 Sep 2026, Ride, Run: 54, Easy","1"),("Sun 20 Sep 2026, Long ride: 150, Hard","6")]),
        string.Join("|",marks.Select(g=>(string?)g.Attribute("aria-label"))));
    Check(marks.All(g=>(string?)g.Attribute("tabindex")=="0"&&(string?)g.Attribute("role")=="button"&&(string?)g.Attribute("data-series")=="0"&&(string?)g.Attribute("class")=="lumen-datum"),"a day is not a focusable mark");
    // Wednesday's zero, Thursday's missing value, and Friday and Saturday without points are empty cells outside any mark.
    var tracks=DayTracks(doc);
    Check(tracks.Length==4&&tracks.All(t=>t.Parent==doc.Root&&(string?)t.Attribute("fill")==ChartStyle.Light.Grid),"the days without activity are not four empty cells");
    // A duration format writes the totals as a clock; the chart's legend is the series' name.
    Check(Svg(spec with{YFormat=ValueFormat.Duration,YZones=null}).Descendants(ns+"g").Any(g=>(string?)g.Attribute("aria-label")=="Tue 15 Sep 2026, Ride, Run: 0:54"),"a duration format does not reach a day's name");
    Check(ChartSvg.LegendLabel(spec,0)=="Stress"&&doc.Root!.Attribute("aria-label")!.Value=="Untitled chart","the legend or the accessible name changed");
});
Test("Calendar: without zones a day takes a ramp across the active days from a third of the way between an empty day and the heatmap's high end up to that end, exact at its ends and middle, with the heatmap's hairline, and the key reads the ends",()=>{
    var from=new DateOnly(2026,9,14);
    var spec=Days(from,4,i=>new double?[]{10,30,50,0}[i]);
    var doc=Svg(spec);
    // A third of the way from the light grid colour #E8EDF5 to #4069D0 is #B0C1E8, truncated per channel as the heatmap's ramp is.
    Check(Enumerable.Range(0,3).Select(i=>(string?)DayShape(doc,from.AddDays(i)).Attribute("fill")).SequenceEqual(["#B0C1E8","#7895DC","#4069D0"]),"the ramp's ends and middle moved");
    // The hairline is the shape's own stroke, so a second shape that draws nothing carries the focus ring.
    foreach(var i in Enumerable.Range(0,3))
    {
        var parts=DayGroup(doc,from.AddDays(i)).Elements().Where(e=>e.Name!=ns+"title").ToArray();
        Check(parts.Length==2&&(string?)parts[0].Attribute("stroke")=="var(--lumen-muted)"&&(string?)parts[0].Attribute("stroke-opacity")==".4"&&(string?)parts[1].Attribute("fill")=="none"&&parts[1].Attribute("stroke") is null,"a ramp day has no hairline or no focus shape");
    }
    var swatches=doc.Root!.Elements(ns+"rect").Where(r=>r.Attribute("class") is null&&Attr(r,"width")==10).Select(r=>(string?)r.Attribute("fill")).ToArray();
    Check(swatches.SequenceEqual(["#B0C1E8","#94ABE2","#7895DC","#5C7FD6","#4069D0"]),string.Join(",",swatches));
    Check(KeyText(doc).SequenceEqual(["10","50"]),string.Join(",",KeyText(doc)));
    // The ramp is the style's: Midnight's low end, from its own grid colour towards its own high end, and a brand's high end.
    Check((string?)DayShape(Svg(spec with{Style=ChartStyle.Midnight}),from).Attribute("fill")=="#3C5C85"&&(string?)DayShape(Svg(spec with{Style=Brand()}),from.AddDays(2)).Attribute("fill")==Brand().HeatmapHigh,"the ramp is not the style's");
    // A day with no activity anywhere leaves the key without a ramp.
    Check(KeyText(Svg(Days(from,3,_=>0))).Length==0&&DayTracks(Svg(Days(from,3,_=>0))).Length==3);
});
Test("Calendar: with zones a day takes its value's zone colour, a zone's own colour first, without a hairline, and the key names the zones in order",()=>{
    var from=new DateOnly(2026,9,14);
    var zones=new ZoneScale([new("Easy",120),new("Steady",140,"#123456"),new("Hard",160),new("Max",double.PositiveInfinity)]);
    var doc=Svg(Days(from,4,i=>new double[]{100,130,150,170}[i]) with{YZones=zones});
    Check(Enumerable.Range(0,4).Select(i=>(string?)DayShape(doc,from.AddDays(i)).Attribute("fill")).SequenceEqual([ChartStyle.Light.Zones[0],"#123456",ChartStyle.Light.Zones[2],ChartStyle.Light.Zones[3]]),"a day is not in its zone's colour");
    Check(Enumerable.Range(0,4).All(i=>DayShape(doc,from.AddDays(i)).Attribute("stroke") is null&&DayGroup(doc,from.AddDays(i)).Elements().Count(e=>e.Name!=ns+"title")==1),"a zoned day carries a hairline or a second shape");
    Check(new[]{"Easy","Steady","Hard","Max"}.Select((name,i)=>DayGroup(doc,from.AddDays(i)).Attribute("aria-label")!.Value.EndsWith(", "+name)).All(named=>named),"a day does not name its zone");
    Check(KeyText(doc).SequenceEqual(["Easy","Steady","Hard","Max"]),string.Join(",",KeyText(doc)));
    var swatches=doc.Root!.Elements(ns+"rect").Where(r=>r.Attribute("class") is null&&Attr(r,"width")==10).Select(r=>(string?)r.Attribute("fill")).ToArray();
    Check(swatches.SequenceEqual([ChartStyle.Light.Zones[0],"#123456",ChartStyle.Light.Zones[2],ChartStyle.Light.Zones[3]]),string.Join(",",swatches));
});
Test("Calendar: a bubble's area is proportional to its day's total, the largest filling its cell over its track, and a dot fills its cell",()=>{
    var from=new DateOnly(2026,9,14);
    var spec=Days(from,3,i=>new double[]{4,9,16}[i]) with{CalendarCell=CalendarCell.Bubble};
    var doc=Svg(spec);
    double R(int i)=>Attr(DayShape(doc,from.AddDays(i)),"r");
    XElement Track(int i)=>DayGroup(doc,from.AddDays(i)).Elements(ns+"circle").First();
    Check(Close(R(0)*R(0)/(R(2)*R(2)),4/16d)&&Close(R(1)*R(1)/(R(2)*R(2)),9/16d)&&Close(R(2),Attr(Track(2),"r")),$"radii {R(0)}, {R(1)}, {R(2)} over a track of {Attr(Track(2),"r")}");
    Check(Enumerable.Range(0,3).All(i=>(string?)Track(i).Attribute("class")=="lumen-track"&&Attr(Track(i),"cx")==Attr(DayShape(doc,from.AddDays(i)),"cx")&&Attr(Track(i),"cy")==Attr(DayShape(doc,from.AddDays(i)),"cy")),"a bubble is not centred on its track");
    var dots=Svg(spec with{CalendarCell=CalendarCell.Dot});
    var radii=Enumerable.Range(0,3).Select(i=>Attr(DayShape(dots,from.AddDays(i)),"r")).Distinct().ToArray();
    Check(radii.Length==1&&DayShape(dots,from).Name==ns+"circle"&&!DayTracks(dots).Any(),"dots are not one size, or a day with activity drew a track");
    // A track is a circle on a round calendar, empty days included.
    Check(DayTracks(Svg(Days(from,3,i=>i==1?0:5) with{CalendarCell=CalendarCell.Dot})).Single().Name==ns+"circle");
});
Test("Calendar: an X annotation outlines its day's cell in the gap round it, names it, and adds it to the key; one outside the days draws nothing",()=>{
    var from=new DateOnly(2026,9,28);
    var spec=Days(from,28,i=>i%7==0?0:10+i) with{YZones=Effort(),Annotations=[new(AnnotationAxis.X,Noon(new(2026,10,11),9)){Label="Race",Color="#DD4B45"},
        new(AnnotationAxis.X,Noon(new(2026,10,18))),new(AnnotationAxis.X,Noon(new(2026,11,30))){Label="Later"}]};
    var doc=Svg(spec);
    var outlines=doc.Descendants().Where(e=>(string?)e.Attribute("class")=="lumen-outline").ToArray();
    // Sunday 11 October is the second week's Sunday, its cell at (427, 328) and 35 square, so its outline runs 3 out into the gap.
    Check(outlines.Length==2&&Attr(outlines[0],"x")==424&&Attr(outlines[0],"y")==325&&Attr(outlines[0],"width")==41&&Attr(outlines[0],"height")==41&&Attr(outlines[0],"rx")==6
        &&(string?)outlines[0].Attribute("stroke")=="#DD4B45"&&(string?)outlines[0].Attribute("fill")=="none"&&Attr(outlines[1],"x")==465&&(string?)outlines[1].Attribute("stroke")==ChartStyle.Light.Muted,
        string.Join("|",outlines.Select(o=>o.ToString())));
    Check((string?)outlines[0].Parent!.Attribute("aria-label")=="Race: Sun 11 Oct 2026"&&(string?)outlines[1].Parent!.Attribute("aria-label")=="Sun 18 Oct 2026"&&(string?)outlines[0].Parent!.Attribute("role")=="img","an outline is not named");
    Check(KeyText(doc).SequenceEqual(["Easy","Steady","Hard","Max","Race","18 Oct"])&&!doc.ToString().Contains("Later"),string.Join(",",KeyText(doc)));
    var swatch=doc.Root!.Elements(ns+"rect").Single(r=>(string?)r.Attribute("stroke")=="#DD4B45");
    Check((string?)swatch.Attribute("fill")=="none"&&Attr(swatch,"width")==10,"the key's outlined swatch is missing");
    // The outline is drawn over the days, after every one of them.
    Check(doc.Root!.Elements().ToList().IndexOf(outlines[0].Parent!)>doc.Root!.Elements().ToList().IndexOf(DayGroup(doc,new(2026,10,25))),"an outline is drawn under a day");
    // On round cells it is a circle in the gap.
    Check(Attr(Svg(spec with{CalendarCell=CalendarCell.Dot}).Descendants(ns+"circle").First(c=>(string?)c.Attribute("class")=="lumen-outline"),"r")==20.5,"a dot's outline is not in the gap");
});
Test("Calendar: what has no meaning on a calendar is refused, each with its reason",()=>{
    string Refusal(ChartSpec spec){try{ChartSvg.Render(spec);}catch(ArgumentException error){return error.Message;}throw new Exception("a chart was accepted that should not be");}
    var spec=Days(new DateOnly(2026,9,14),7,i=>i*10);
    ChartSpec With(Func<ChartSeries,ChartSeries> change)=>spec with{Series=[change(spec.Series[0])]};
    var start=new DateOnly(2016,1,1);
    foreach(var (bad,reason) in new (ChartSpec,string)[]{
        (spec with{XAxis=AxisKind.Linear},"needs a time X axis"),(spec with{XAxis=AxisKind.Log},"needs a time X axis"),
        (spec with{YAxis=AxisKind.Log},"rather than measuring it on a Y axis"),(spec with{YReversed=true},"rather than measuring"),(spec with{YMin=0},"rather than measuring"),(spec with{YMax=100},"rather than measuring"),
        (spec with{YAxisSide=AxisSide.Right},"rather than measuring"),(spec with{YTickLabels=TickLabels.Ends},"rather than measuring"),
        (spec with{Y2Axis=AxisKind.Log},"no secondary axis"),(spec with{Y2Reversed=true},"no secondary axis"),(spec with{Y2Format=ValueFormat.Duration},"no secondary axis"),(spec with{Y2Min=0},"no secondary axis"),
        (With(s=>s with{Secondary=true}),"no secondary axis"),(spec with{Panes=[new()]},"no panes"),(With(s=>s with{Pane=1}),"no panes"),
        (spec with{Series=[spec.Series[0],spec.Series[0] with{Name="Again"}]},"one series of days"),(With(s=>s with{Kind=ChartKind.Column}),"own kind"),
        (With(s=>s with{Trend=true}),"no trend line"),(With(s=>s with{Zones=Effort()}),"takes its zones from YZones"),
        (spec with{Annotations=[new(AnnotationAxis.Y,10)]},"takes X annotations"),(spec with{Annotations=[new(AnnotationAxis.X,spec.Series[0].Points[0].X){To=spec.Series[0].Points[2].X}]},"lines, not bands"),
        (With(s=>s with{Points=[s.Points[0] with{XEnd=s.Points[1].X}]}),"XEnd ends the spans of a timeline"),
        (With(s=>s with{Points=[s.Points[0] with{Color="#123456"}]}),"take no colours of their own"),(With(s=>s with{Points=[s.Points[0] with{Y=-1}]}),"none can be negative"),
        (spec with{SkipWeekends=true},"skips none"),(spec with{TimeSkips=[TimeAxis.Day(new DateTime(2026,9,15))]},"skips none"),
        (spec with{XFormat=ValueFormat.Duration},"writes its own calendar"),
        (With(s=>s with{Gradient=[new(0,"#3F87D9"),new(1,"#DD4B45")]}),"gradient"),(With(s=>s with{Markers=MarkerStyle.Filled}),"Marker styles"),(With(s=>s with{ProjectedFrom=0}),"lines or areas"),
        (With(s=>s with{StrokeWidth=3}),"stroke width"),(With(s=>s with{Goal=10}),"ring charts only"),(spec with{GaugeSweep=180},"gauge charts only"),(spec with{TimelineConnectors=false},"only a timeline"),
        (spec with{XMin=Noon(start),XMax=Noon(start.AddDays(3660))},"at most 3,660 days"),(With(s=>s with{Points=[new(Noon(start),1),new(Noon(start.AddDays(3700)),1)]}),"at most 3,660 days"),
        (spec with{TimeZone="Mars/Olympus"},"Unknown time zone"),
        (spec with{CalendarLayout=(CalendarLayout)7},"Unknown calendar"),(spec with{CalendarCell=(CalendarCell)(-1)},"Unknown calendar"),(spec with{WeekStart=(DayOfWeek)7},"Unknown calendar"),
        (Spec() with{CalendarLayout=CalendarLayout.Months},"calendar charts only"),(Spec() with{CalendarCell=CalendarCell.Dot},"calendar charts only"),(Spec(ChartKind.Heatmap) with{WeekStart=DayOfWeek.Sunday},"calendar charts only"),
        (Spec(ChartKind.Column) with{XAxis=AxisKind.Time},"and calendars")})
        Check(Refusal(bad).Contains(reason),$"\"{Refusal(bad)}\" does not say \"{reason}\"");
    // Within the rules: 3,659 days, a zone, a labelled X annotation, a duration format, an empty series, days that are all missing,
    // and the smallest drawing in every layout and cell.
    foreach(var good in new[]{spec with{XMin=Noon(start),XMax=Noon(start.AddDays(3659))},spec with{TimeZone="Asia/Tokyo",YFormat=ValueFormat.Duration,Annotations=[new(AnnotationAxis.X,spec.Series[0].Points[3].X){Label="Race"}]},
        spec with{Series=[new("Empty",[])]},With(s=>s with{Points=s.Points.Select(p=>p with{Y=null}).ToArray()}),
        spec with{Width=320,Height=240,CalendarLayout=CalendarLayout.Months,CalendarCell=CalendarCell.Bubble},spec with{Width=320,Height=240,CalendarCell=CalendarCell.Dot,XMin=Noon(start),XMax=Noon(start.AddDays(3659))}})
        Check(XDocument.Parse(ChartSvg.Render(good)).Root!.Name==ns+"svg");
    Check(ChartSvg.Render(new ChartSpec{Kind=ChartKind.Calendar}).Contains("No data to display")&&ChartSvg.Render(spec with{Series=[new("Empty",[])]}).Contains("No data to display"),"an empty calendar does not draw its empty state");
});
Test("Calendar: a spec survives JSON, a request that names no layout draws weeks of squares from Monday, and CSV carries each point as other time charts do",()=>{
    var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    var spec=Days(new DateOnly(2026,9,14),10,i=>i) with{CalendarLayout=CalendarLayout.Months,CalendarCell=CalendarCell.Bubble,WeekStart=DayOfWeek.Sunday,YZones=Effort(),TimeZone="Africa/Johannesburg",
        Annotations=[new(AnnotationAxis.X,Noon(new(2026,9,20))){Label="Race"}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,options);
    Check(json.Contains("\"kind\":\"Calendar\"")&&json.Contains("\"calendarLayout\":\"Months\"")&&json.Contains("\"calendarCell\":\"Bubble\"")&&json.Contains("\"weekStart\":\"Sunday\""),json);
    Check(ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,options)!)==ChartSvg.Render(spec),"the calendar changed in transit");
    var request=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Calendar\",\"xAxis\":\"Time\",\"series\":[{\"name\":\"Stress\",\"points\":[{\"x\":1789387200000,\"y\":40},{\"x\":1789473600000,\"y\":60}]}]}",options)!;
    Check(request.CalendarLayout==CalendarLayout.Weeks&&request.CalendarCell==CalendarCell.Square&&request.WeekStart==DayOfWeek.Monday
        &&ChartSvg.Render(request)==ChartSvg.Render(new ChartSpec{Kind=ChartKind.Calendar,XAxis=AxisKind.Time,Series=[new("Stress",[new(1789387200000,40),new(1789473600000,60)])]}),"a request without a layout is not weeks of squares from Monday");
    // The file carries each original point, two on one day as two rows, with its moment in UTC.
    var csv=ChartExport.Csv(new ChartSpec{Kind=ChartKind.Calendar,XAxis=AxisKind.Time,TimeZone="America/New_York",Series=[new("Stress",[new(1789387200000,40,"Ride"),new(1789387200000+3600e3,15),new(1789473600000,null)])]});
    Check(csv.StartsWith("Series,X,XTime,Y,Label,Size\r\n")&&csv.Contains("\"Stress\",1789387200000,2026-09-14T12:00:00.000Z,40,\"Ride\",1")&&csv.Contains("\"Stress\",1789390800000,2026-09-14T13:00:00.000Z,15,\"\",1")
        &&csv.Contains("\"Stress\",1789473600000,2026-09-15T12:00:00.000Z,,\"\",1")&&csv.Split('\n',StringSplitOptions.RemoveEmptyEntries).Length==4,csv);
});
Test("Calendar: the new properties at their defaults change no other chart, its gradient IDs included, and a calendar defines no IDs",()=>{
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}]};
    foreach(var spec in new[]{faded,Classic(faded),Sample(ChartKind.Line),Sample(ChartKind.Gauge),Sample(ChartKind.Timeline),Sample(ChartKind.Heatmap) with{Theme=ChartTheme.Dark}})
        foreach(var titles in new[]{true,false})
            Check(ChartSvg.Render(spec with{CalendarLayout=CalendarLayout.Weeks,CalendarCell=CalendarCell.Square,WeekStart=DayOfWeek.Monday},includeTitles:titles)==ChartSvg.Render(spec,includeTitles:titles),$"{spec.Kind} changes when the calendar's defaults are spelled out");
    foreach(var layout in Enum.GetValues<CalendarLayout>())
        foreach(var cell in Enum.GetValues<CalendarCell>())
            foreach(var style in new[]{ChartStyle.Light,ChartStyle.Midnight,ChartStyle.Light with{Finish=ChartFinish.Classic}})
            {
                var svg=ChartSvg.Render(Sample(ChartKind.Calendar) with{CalendarLayout=layout,CalendarCell=cell,Style=style,YZones=cell==CalendarCell.Dot?Effort():null});
                Check(!svg.Contains(" id=")&&!svg.Contains("<defs")&&!svg.Contains("url("),$"{layout} {cell} defines an ID");
                Check(style.Finish==ChartFinish.Refined||!svg.Contains("vector-effect"),$"{layout} {cell}: a classic calendar carries the effect");
            }
    // Midnight's capsules round a square day into a circle's outline, and the sample draws at a phone's width.
    var midnight=Svg(Sample(ChartKind.Calendar) with{Style=ChartStyle.Midnight});
    Check(Close(Attr(DayShape(midnight,new(2026,9,14)),"rx"),Attr(DayShape(midnight,new(2026,9,14)),"width")/2),"Midnight's day is not round");
    Check(Svg(Sample(ChartKind.Calendar) with{Width=337}).Descendants(ns+"g").Count(g=>g.Attribute("data-point") is not null)==2);
});
Test("Calendar: the component offers no zoom, keys the zones or the ramp, and reads a selected day's date and total in its status line",()=>{
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    // 00:30 and 16:00 on Tuesday 15 September in New York are one day of 54.
    var spec=new ChartSpec{Kind=ChartKind.Calendar,XAxis=AxisKind.Time,TimeZone="America/New_York",YZones=Effort(),Series=[new("Stress",[new(Utc(2026,9,15,4)+30*60_000,30),new(Utc(2026,9,15,20),24),new(Utc(2026,9,17,12),100)])]};
    var html=RenderInside(null,spec);
    Check(!html.Contains("aria-label=\"Zoom in\"")&&!html.Contains("Reset view")&&html.Contains("Export CSV")&&html.Contains(ChartSvg.LegendKey(spec,0))&&html.Contains("Tue 15 Sep 2026: 54, Easy"),"the component offers zoom, or lost the key or the day");
    Check(XDocument.Parse(ChartSvg.LegendKey(spec,0)).Root!.Elements().Select(e=>(string?)e.Attribute("fill")).SequenceEqual(ChartStyle.Light.Zones.Take(4)),"the key is not the zones");
    Check(XDocument.Parse(ChartSvg.LegendKey(spec with{YZones=null},0)).Root!.Elements().Select(e=>(string?)e.Attribute("fill")).SequenceEqual(["#B0C1E8",ChartStyle.Light.HeatmapHigh]),"the key is not the ramp");
    var shown=Operate(spec,async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(0,0);});
    Check(shown.Contains("Stress: Tue 15 Sep 2026 = 54</span>")&&shown.Contains("<tr><td>Stress</td>"),"the status line does not read the day's total");
    // Hiding the series leaves the chart's empty state rather than a refused spec.
    var hidden="";
    Operate(spec,chart=>{typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[0]);hidden=(string)typeof(LumenChart).GetField("svg",flags)!.GetValue(chart)!;return Task.CompletedTask;});
    Check(hidden.Contains("No data to display"),"hiding the calendar's series broke the chart");
});
Test("Sports page: the training calendar's days are the performance chart's daily stress, in tiers, and its month's bubbles the month's runs",()=>{
    var card=sports.Single(c=>c.Id=="training-calendar");var grid=card.Spec;var month=card.Beside!;
    var daily=Sports("performance").Series.Single(s=>s.Name=="Daily stress").Points;
    Check(card.Section=="load"&&card.Wide&&grid.Kind==ChartKind.Calendar&&grid.CalendarLayout==CalendarLayout.Weeks&&month.Kind==ChartKind.Calendar&&month.CalendarLayout==CalendarLayout.Months&&month.CalendarCell==CalendarCell.Bubble);
    var days=grid.Series.Single().Points;
    Check(days.SequenceEqual(daily.Take(SportsData.Weeks*7))&&DayOf(days[0].X)==SportsData.Start&&DayOf(days[^1].X)==SportsData.Today,"the grid's days are not the season's daily stress");
    Check(grid.YZones!.Zones.Select(z=>z.Upper).SequenceEqual([50,100,150,double.PositiveInfinity])&&grid.Annotations.Single().From==SportsData.When(SportsData.Today),"the tiers or today moved");
    Check(grid.Title==$"{days.Count(p=>p.Y>0)} days trained, {days.Count(p=>p.Y>100)} of them hard","the title miscounts");
    var doc=Svg(grid);
    Check(doc.Descendants(ns+"g").Count(g=>g.Attribute("data-point") is not null)==days.Count(p=>p.Y>0)&&DayTracks(doc).Length==days.Count(p=>p.Y==0),"the grid does not draw every day");
    // The month is every run so far this month, each its distance in km on its day, on a grid of the whole month.
    var runs=athlete.Sessions.Where(s=>s.Sport==Sport.Run&&s.Day.Year==SportsData.Today.Year&&s.Day.Month==SportsData.Today.Month).ToArray();
    Check(month.Series.Single().Points.Select(p=>(DayOf(p.X),p.Y,p.Label)).SequenceEqual(runs.Select(r=>(r.Day,(double?)Math.Round(r.Metres/1000,1),(string?)r.Name))),"the bubbles are not the month's runs");
    Check(DayOf(month.XMin!.Value)==new DateOnly(2026,9,1)&&DayOf(month.XMax!.Value)==new DateOnly(2026,9,30)&&month.Title==$"{(runs.Sum(r=>r.Metres)/1000).ToString("0",CultureInfo.InvariantCulture)} km run in September, {runs.Length} runs",month.Title);
    var bubbles=Svg(month);
    Check(DayTracks(bubbles).Length==30&&bubbles.Descendants(ns+"g").Count(g=>g.Attribute("data-point") is not null)==runs.Select(r=>r.Day).Distinct().Count(),"the month does not draw its thirty days");
    // The tiers and the bubbles take the brand's zone ramp, whose colours clear 3:1 on its background.
    foreach(var (style,zones) in new[]{(ChartStyle.Light,ChartStyle.Light.Zones),(ChartStyle.Dark,ChartStyle.Light.Zones),(ChartStyle.Midnight,ChartStyle.Midnight.Zones)})
    {
        var both=SportsData.Cards(ChartTheme.Light,zones).Single(c=>c.Id=="training-calendar");
        var inks=both.Spec.YZones!.Zones.Concat(both.Beside!.YZones!.Zones).Select(z=>z.Color!).ToArray();
        Check(inks.All(ink=>zones.Contains(ink)&&Contrast(ink,style.Background)>=3),$"{style.Background}: {string.Join(", ",inks)}");
    }
});
// 0.29.0: blocks. A plot runs from x 76 to 870 and y 78 to 344, so on an X axis from 0 to 10 a value x sits at BX(x), and with Y
// held from 0 to 300 a height v at BY(v). A block's outline is read back from its path.
double BX(double x)=>76+x*79.4;
double BY(double v)=>344-v/300*266;
XElement[] BlockPaths(XDocument doc,int series=0)=>doc.Descendants(ns+"path").Where(p=>(string?)p.Attribute("class")=="lumen-block"&&(string?)p.Parent!.Attribute("data-series")==series.ToString(CultureInfo.InvariantCulture)).ToArray();
(double Left,double Right,double Top,double Bottom) BlockBox(XElement path)
{
    var at=Commands((string)path.Attribute("d")!).Where(c=>c.Op!='Z').Select(c=>c.Op=='A'?(X:c.Args[5],Y:c.Args[6]):(X:c.Args[0],Y:c.Args[1])).ToArray();
    return (at.Min(p=>p.X),at.Max(p=>p.X),at.Min(p=>p.Y),at.Max(p=>p.Y));
}
ChartSpec Plan(params ChartPoint[] blocks)=>new(){Kind=ChartKind.Blocks,Title="Plan",IncludeZero=true,YMax=300,Series=[new("Plan",blocks)]};
ChartSpec Laps(params double[] paces)=>new(){Kind=ChartKind.Blocks,Title="Laps",YFormat=ValueFormat.Duration,YReversed=true,
    Series=[new("Laps",paces.Select((pace,i)=>ChartPoint.Block(i,i+1,pace,$"Lap {i+1}")).ToArray())]};
Test("Blocks: each covers its X to its XEnd exactly, and neighbours that touch give up half a hairline each, or a quarter of a narrow block's width",()=>{
    var doc=Svg(Plan(ChartPoint.Block(0,2,150),ChartPoint.Block(2,5,300),ChartPoint.Block(6,10,75)));
    var boxes=BlockPaths(doc).Select(BlockBox).ToArray();
    Check(boxes.Length==3,"three blocks were not drawn");
    // The first starts at the plot's edge and ends half a pixel short of 2, where the second begins half a pixel past it; the
    // second ends exactly at 5, a gap away from the third, which runs exactly from 6 to the plot's right edge at 10.
    Check(Close(boxes[0].Left,BX(0))&&Close(boxes[0].Right,BX(2)-.5)&&Close(boxes[1].Left,BX(2)+.5)&&Close(boxes[1].Right,BX(5))&&Close(boxes[2].Left,BX(6))&&Close(boxes[2].Right,BX(10))&&Close(BX(10),870),
        string.Join(" | ",boxes.Select(b=>$"{b.Left}-{b.Right}")));
    Check(Close(boxes[1].Left-boxes[0].Right,1),"touching neighbours are not a hairline apart");
    // A block narrower than two pixels gives up a quarter of its width at each end it touches, so it keeps half its width.
    var narrow=Svg(Plan(ChartPoint.Block(0,4,100),ChartPoint.Block(4,4.01,200),ChartPoint.Block(4.01,10,150)));
    var middle=BlockBox(BlockPaths(narrow)[1]);
    Check(Close(middle.Left,BX(4)+.01*79.4/4)&&Close(middle.Right,BX(4.01)-.01*79.4/4)&&Close(BlockBox(BlockPaths(narrow)[0]).Right,BX(4)-.5)&&Close(BlockBox(BlockPaths(narrow)[2]).Left,BX(4.01)+.5),
        $"{middle.Left}-{middle.Right}");
    // Blocks of another series that touch these do not part them: the hairline belongs to a series' own steps.
    var two=Svg(Plan(ChartPoint.Block(0,5,100)) with{Series=[new("A",[ChartPoint.Block(0,5,100)]),new("B",[ChartPoint.Block(5,10,200)])]});
    Check(Close(BlockBox(BlockPaths(two,0)[0]).Right,BX(5))&&Close(BlockBox(BlockPaths(two,1)[0]).Left,BX(5)),"blocks of two series part each other");
    // The X axis reaches the last block's end, and keeps its ticks under the blocks rather than their labels.
    var laps=Svg(Laps(330,310,290) with{XLabel="Distance (km)"});
    Check(Close(BlockBox(BlockPaths(laps)[2]).Right,870)&&Ticks(laps,"middle").Contains("3")&&!Ticks(laps,"middle").Any(t=>t.StartsWith("Lap")),string.Join(",",Ticks(laps,"middle")));
});
Test("Blocks: each stands on the bottom edge of its plot, from zero on an axis that includes it, from past the slowest pace on a reversed one, and in a lower pane",()=>{
    // Power with zero included: every block stands on 344, the bottom, and rises to its value.
    var power=BlockPaths(Svg(Plan(ChartPoint.Block(0,2,150),ChartPoint.Block(2,5,300),ChartPoint.Block(6,10,75)))).Select(BlockBox).ToArray();
    Check(power.All(b=>Close(b.Bottom,344))&&Close(power[0].Top,BY(150))&&Close(power[1].Top,78)&&Close(power[2].Top,BY(75)),string.Join(" | ",power.Select(b=>$"{b.Top}-{b.Bottom}")));
    // Pace on a reversed axis fitted to 4:20 to 5:00: its bottom moves out to 5:08, so the slowest lap stands a sixth of the plot,
    // and the fastest reaches the top.
    var laps=BlockPaths(Svg(Laps(300,280,260))).Select(BlockBox).ToArray();
    Check(laps.All(b=>Close(b.Bottom,344))&&Close(344-laps[0].Top,266/6d)&&Close(laps[1].Top,78+20/48d*266)&&Close(laps[2].Top,78),string.Join(" | ",laps.Select(b=>$"{b.Top}")));
    Check(Ticks(Svg(Laps(300,280,260)),"end").Contains("5:00")&&!Ticks(Svg(Laps(300,280,260)),"end").Contains("5:30"),"the axis reaches further than the slowest lap needs");
    // A bottom that is set is kept, so the slowest lap there has no height; and so is one held at zero.
    var held=BlockPaths(Svg(Laps(300,280,260) with{YMax=300})).Select(BlockBox).ToArray();
    Check(Close(held[0].Top,344)&&Close(held[0].Bottom,344)&&Close(held[2].Top,78),"a bottom that is set moved");
    // Without zero the bottom moves out below the lowest block the same way, by a fifth of the span, here from 150 to 130.
    var plain=BlockPaths(Svg(new ChartSpec{Kind=ChartKind.Blocks,Series=[new("Plan",[ChartPoint.Block(0,1,150),ChartPoint.Block(1,2,200),ChartPoint.Block(2,3,250)])]})).Select(BlockBox).ToArray();
    Check(Close(344-plain[0].Top,266/6d)&&Close(plain[1].Top,344-70/120d*266)&&Close(plain[2].Top,78),string.Join(" | ",plain.Select(b=>$"{b.Top}")));
    // A line that already reaches below the lowest block leaves the axis where it was, from 100 to 250.
    var beside=Svg(new ChartSpec{Kind=ChartKind.Blocks,Series=[new("Plan",[ChartPoint.Block(0,1,150),ChartPoint.Block(1,2,250)]),new("Done",[new(0,100),new(2,220)]){Kind=ChartKind.Line}]});
    Check(Close(BlockBox(BlockPaths(beside)[0]).Top,344-50/150d*266),"a line below the blocks did not hold the axis");
    // On a logarithmic axis the bottom moves out in decades: from 10 down to 10^0.6, so 100 stands 1.4 of 2.4 decades up.
    var logged=BlockPaths(Svg(new ChartSpec{Kind=ChartKind.Blocks,YAxis=AxisKind.Log,Series=[new("Load",[ChartPoint.Block(0,1,10),ChartPoint.Block(1,2,100),ChartPoint.Block(2,3,1000)])]})).Select(BlockBox).ToArray();
    Check(Close(344-logged[0].Top,266/6d)&&Math.Abs(logged[1].Top-(344-1.4/2.4*266))<1e-6&&Close(logged[2].Top,78),string.Join(" | ",logged.Select(b=>$"{b.Top}")));
    // In a pane under a line, blocks stand on that pane's bottom edge and the tallest reaches its top.
    var paned=Svg(new ChartSpec{Kind=ChartKind.Line,Height=600,IncludeZero=true,Panes=[new(){Label="Plan"}],
        Series=[new("Power",[new(0,100),new(10,200)]),new("Plan",[ChartPoint.Block(0,4,140),ChartPoint.Block(4,10,280)]){Kind=ChartKind.Blocks,Pane=1}]});
    var span=PaneSpan(PaneClips(paned)[1]);
    var below=BlockPaths(paned,1).Select(BlockBox).ToArray();
    Check(Close(span.Bottom,524)&&below.All(b=>Close(b.Bottom,span.Bottom))&&Close(below[1].Top,span.Top)&&Close(below[0].Top,span.Bottom-.5*(span.Bottom-span.Top)),$"{span} {string.Join(" | ",below.Select(b=>$"{b.Top}-{b.Bottom}"))}");
    // On the right-hand axis they stand on the same edge and rise on their own axis: 10 and 20 from zero, the left one untouched.
    var right=Svg(new ChartSpec{Kind=ChartKind.Line,IncludeZero=true,Y2Label="Plan",Series=[new("Power",[new(0,0),new(10,300)]),new("Plan",[ChartPoint.Block(0,5,10),ChartPoint.Block(5,10,20)]){Kind=ChartKind.Blocks,Secondary=true}]});
    var lifted=BlockPaths(right,1).Select(BlockBox).ToArray();
    Check(lifted.All(b=>Close(b.Bottom,344))&&Close(lifted[0].Top,211)&&Close(lifted[1].Top,78)&&Close(BlockBox(BlockPaths(right,1)[1]).Right,824),string.Join(" | ",lifted.Select(b=>$"{b.Top}-{b.Right}")));
});
Test("Blocks: only the far end is rounded, by the style's bar radius or 4 pixels, up to 6, clamped to half the width and to the height",()=>{
    var spec=Plan(ChartPoint.Block(0,2,150),ChartPoint.Block(2,5,300),ChartPoint.Block(6,10,75));
    // The second block runs from 235.3 to 473 and up to the top at 78: square at the bottom, a 4-pixel arc at each top corner.
    Draws(BarOf(Svg(spec),0,1),M(235.3,344),L(235.3,82),A(4,239.3,78),L(469,78),A(4,473,82),L(473,344),Z());
    Draws(BarOf(Svg(Classic(spec)),0,1),M(235.3,344),L(235.3,82),A(4,239.3,78),L(469,78),A(4,473,82),L(473,344),Z());
    Draws(BarOf(Svg(spec with{Style=ChartStyle.Light with{BarRadius=2}}),0,1),M(235.3,344),L(235.3,80),A(2,237.3,78),L(471,78),A(2,473,80),L(473,344),Z());
    // Midnight's capsule radius is held to 6 pixels, so a wide block is not domed; a radius of 0 draws a square block.
    Draws(BarOf(Svg(spec with{Style=ChartStyle.Midnight}),0,1),M(235.3,344),L(235.3,84),A(6,241.3,78),L(467,78),A(6,473,84),L(473,344),Z());
    Draws(BarOf(Svg(spec with{Style=ChartStyle.Light with{BarRadius=0}}),0,1),M(235.3,344),L(235.3,78),L(473,78),L(473,344),Z());
    // A block 2.66 pixels high rounds by its height, and one 0.794 pixels wide by half its width.
    var low=BarOf(Svg(Plan(ChartPoint.Block(0,5,3),ChartPoint.Block(6,10,300))),0,0);
    Check(low.Where(c=>c.Op=='A').All(c=>Close(c.Args[0],2.66))&&low.Count(c=>c.Op=='A')==2,string.Join(" ",low.Select(c=>c.Op+string.Join(",",c.Args))));
    var thin=BarOf(Svg(Plan(ChartPoint.Block(0,9,100),ChartPoint.Block(9.99,10,300))),0,1);
    Check(thin.Where(c=>c.Op=='A').All(c=>Close(c.Args[0],.397))&&thin.Count(c=>c.Op=='A')==2,string.Join(" ",thin.Select(c=>c.Op+string.Join(",",c.Args))));
});
Test("Blocks: a point's colour beats its zone's, which beats the series', and zoned blocks are keyed by the zone colours they draw in",()=>{
    var ftp=ZoneScale.CogganPower(250);
    // 125 W is active recovery, 200 tempo, 250 threshold and 300, exactly 120 %, VO2max; the last carries a colour of its own.
    ChartPoint[] steps=[ChartPoint.Block(0,1,125),ChartPoint.Block(1,2,200),ChartPoint.Block(2,3,250),ChartPoint.Block(3,4,300),ChartPoint.Block(4,5,125) with{Color="#123456"}];
    var zoned=Plan(steps) with{Series=[new("Plan",steps){Zones=ftp}]};
    string[] Fills(ChartSpec spec)=>BlockPaths(Svg(spec)).Select(p=>(string)p.Attribute("fill")!).ToArray();
    var ramp=ChartStyle.Light.Zones;
    Check(Fills(zoned).SequenceEqual([ramp[0],ramp[2],ramp[3],ramp[4],"#123456"]),string.Join(",",Fills(zoned)));
    Check(Fills(zoned with{Style=ChartStyle.Midnight}).SequenceEqual([ChartStyle.Midnight.Zones[0],ChartStyle.Midnight.Zones[2],ChartStyle.Midnight.Zones[3],ChartStyle.Midnight.Zones[4],"#123456"]),"Midnight's ramp is not used");
    Check(Fills(Plan(steps)).SequenceEqual([..Enumerable.Repeat(ChartStyle.Light.Series[0],4),"#123456"]),"without zones a block is not its series' colour");
    Check(Fills(Plan(steps) with{Series=[new("Plan",steps,"#DD4B45")]}).Take(4).All(f=>f=="#DD4B45"),"the series' own colour is not used");
    // The key: the first four colours the blocks draw in, in their order; one colour draws two steps, the second taller.
    Check(XDocument.Parse(ChartSvg.LegendKey(zoned,0)).Root!.Elements().Select(e=>(string?)e.Attribute("fill")).SequenceEqual([ramp[0],ramp[2],ramp[3],ramp[4]]),ChartSvg.LegendKey(zoned,0));
    var plain=XDocument.Parse(ChartSvg.LegendKey(Plan(steps),0)).Root!.Elements().ToArray();
    Check(plain.Length==2&&plain.All(e=>(string?)e.Attribute("fill")==ChartStyle.Light.Series[0])&&Attr(plain[0],"height")<Attr(plain[1],"height")&&Close(Attr(plain[1],"x")-(Attr(plain[0],"x")+Attr(plain[0],"width")),1),ChartSvg.LegendKey(Plan(steps),0));
    Check(Svg(zoned).Descendants(ns+"rect").Count(r=>(string?)r.Attribute("fill")==ramp[0])>=1,"the chart's own legend does not key the zones");
    // A block names its zone, and a block below the plot's bottom edge, set by YMin, has no height rather than hanging below it.
    Check(Labels(Svg(zoned))[0]=="Plan: 0 to 1, 125, Active recovery"&&Labels(Svg(zoned))[3]=="Plan: 3 to 4, 300, VO2max",string.Join(" | ",Labels(Svg(zoned))));
    var under=BlockBox(BlockPaths(Svg(Plan(steps) with{IncludeZero=false,YMin=150}))[0]);
    Check(Close(under.Top,344)&&Close(under.Bottom,344),"a block below the plot hangs from its bottom edge");
});
Test("Blocks: they draw in the column layer, over bands and areas and under lines and points, whatever the series order, with references behind",()=>{
    ChartPoint[] plan=[ChartPoint.Block(0,4,150),ChartPoint.Block(4,8,250),ChartPoint.Block(8,10,120)];
    var spec=new ChartSpec{Kind=ChartKind.Blocks,IncludeZero=true,Annotations=[new(AnnotationAxis.Y,200){Label="FTP"}],Series=[
        new("Power",Enumerable.Range(0,11).Select(i=>new ChartPoint(i,140+i*10)).ToArray()){Kind=ChartKind.Line},
        new("Dots",[new(1,100),new(5,200)]){Kind=ChartKind.Scatter},
        new("Plan",plan),
        new("Climb",Enumerable.Range(0,11).Select(i=>new ChartPoint(i,40+i)).ToArray()){Kind=ChartKind.Area},
        new("Range",[ChartPoint.Interval(2,null,60,90),ChartPoint.Interval(6,null,50,80)]){Kind=ChartKind.Band}]};
    var doc=Svg(spec);
    var order=doc.Descendants().ToList();
    int First(Func<XElement,bool> which)=>order.FindIndex(e=>which(e));
    int Series(int index)=>First(e=>(string?)e.Attribute("data-series")==index.ToString(CultureInfo.InvariantCulture));
    var band=First(e=>e.Name==ns+"path"&&(string?)e.Attribute("fill-opacity")==".16");
    var area=First(e=>e.Name==ns+"path"&&(string?)e.Attribute("fill-opacity")==".12");
    var blocks=First(e=>(string?)e.Attribute("class")=="lumen-block");
    var line=First(e=>e.Name==ns+"path"&&(string?)e.Attribute("fill")=="none"&&(string?)e.Attribute("stroke")==ChartStyle.Light.Series[0]);
    var reference=First(e=>e.Name==ns+"line"&&(string?)e.Attribute("stroke-dasharray")=="6 4");
    Check(reference<band&&band<area&&area<blocks&&blocks<line&&line<Series(1)&&Series(2)<Series(0),$"reference {reference}, band {band}, area {area}, blocks {blocks}, line {line}, dots {Series(1)}");
    // The reference runs across the whole plot, over the blocks' heights.
    var across=order.Single(e=>e.Name==ns+"line"&&(string?)e.Attribute("stroke-dasharray")=="6 4");
    Check(Attr(across,"x1")==76&&Attr(across,"x2")==870,"the reference does not run across the plot");
    // As a series' own kind beside a line on a line chart, blocks still draw first, whatever their place in the list.
    var mixed=Svg(new ChartSpec{Kind=ChartKind.Line,Series=[new("Power",[new(0,100),new(10,200)]),new("Plan",plan){Kind=ChartKind.Blocks}]}).Descendants().ToList();
    Check(mixed.FindIndex(e=>(string?)e.Attribute("class")=="lumen-block")<mixed.FindIndex(e=>e.Name==ns+"path"&&(string?)e.Attribute("fill")=="none"),"blocks draw over the line");
});
Test("Blocks: each is a focusable button named with its label, its span in the X axis's format, its height in its own axis's and its zone",()=>{
    var laps=new ChartSpec{Kind=ChartKind.Blocks,YFormat=ValueFormat.Duration,YReversed=true,XLabel="Distance (km)",
        Series=[new("Laps",[ChartPoint.Block(0,1,301,"Lap 1"),ChartPoint.Block(1,2,292,"Lap 2"),ChartPoint.Block(2,3.6,284.4,"Lap 3")])]};
    var doc=Svg(laps);
    Check(Labels(doc).SequenceEqual(["Lap 1: 0 to 1, 5:01","Lap 2: 1 to 2, 4:52","Lap 3: 2 to 3.6, 4:44"]),string.Join(" | ",Labels(doc)));
    var marks=doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).ToArray();
    Check(marks.All(g=>(string?)g.Attribute("role")=="button"&&(string?)g.Attribute("tabindex")=="0"&&(string?)g.Attribute("data-series")=="0"&&g.Element(ns+"title")!.Value==(string)g.Attribute("aria-label")!)
        &&marks.Select(g=>(string)g.Attribute("data-point")!).SequenceEqual(["0","1","2"]),"a block is not a labelled button");
    // A workout on a duration axis in Coggan's levels at 270 W; a block without a label is led by its series' name.
    var workout=new ChartSpec{Kind=ChartKind.Blocks,XFormat=ValueFormat.Duration,IncludeZero=true,
        Series=[new("Plan",[ChartPoint.Block(0,600,150),ChartPoint.Block(600,840,275,"Interval 2"),ChartPoint.Block(840,1080,1200,"Sprint")]){Zones=ZoneScale.CogganPower(270)}]};
    Check(Labels(Svg(workout)).SequenceEqual(["Plan: 0:00 to 10:00, 150, Endurance","Interval 2: 10:00 to 14:00, 275, Lactate threshold","Sprint: 14:00 to 18:00, 1200, Neuromuscular"]),string.Join(" | ",Labels(Svg(workout))));
    // Where two series draw blocks each block is led by its series' name too.
    var both=Svg(workout with{Series=[workout.Series[0],new("Done",[ChartPoint.Block(0,600,148,"Warm-up")])]});
    Check(Labels(both)[1]=="Plan, Interval 2: 10:00 to 14:00, 275, Lactate threshold"&&Labels(both)[3]=="Done, Warm-up: 0:00 to 10:00, 148",string.Join(" | ",Labels(both)));
    // On a time axis the span reads as dates; on the right-hand axis the height reads in its format.
    var weeks=new ChartSpec{Kind=ChartKind.Blocks,XAxis=AxisKind.Time,Y2Format=ValueFormat.Compact,
        Series=[new("Distance",[new(Utc(2026,8,3),30),new(Utc(2026,8,24),42)]){Kind=ChartKind.Line},new("Volume",[ChartPoint.Block(Utc(2026,8,3),Utc(2026,8,10),12500,"Week 1"),ChartPoint.Block(Utc(2026,8,10),Utc(2026,8,17),9800)]){Secondary=true}]};
    Check(Labels(Svg(weeks)).Take(2).SequenceEqual(["Week 1: 3 Aug 2026 to 10 Aug 2026, 12.5k","Volume: 10 Aug 2026 to 17 Aug 2026, 9.8k"]),string.Join(" | ",Labels(Svg(weeks))));
});
Test("Blocks: what has no meaning for them is refused, each with its reason, and what does is accepted",()=>{
    string Refusal(ChartSpec spec){try{ChartSvg.Render(spec);}catch(ArgumentException error){return error.Message;}throw new Exception("a chart was accepted that should not be");}
    var spec=Plan(ChartPoint.Block(0,2,150,"A"),ChartPoint.Block(2,5,250,"B"));
    ChartSpec With(Func<ChartSeries,ChartSeries> change)=>spec with{Series=[change(spec.Series[0])]};
    ChartSpec Points(params ChartPoint[] points)=>With(s=>s with{Points=points});
    var line=new ChartSeries("Power",[new(0,100),new(5,200)]);
    foreach(var (bad,reason) in new (ChartSpec,string)[]{
        (Points(new ChartPoint(0,150)),"Every block runs from its X to an XEnd"),(Points(ChartPoint.Block(2,2,150)),"A block ends after it starts"),(Points(ChartPoint.Block(2,1,150)),"A block ends after it starts"),
        (Points(ChartPoint.Block(0,double.NaN,150)),"A block ends after it starts"),(Points(ChartPoint.Block(0,double.PositiveInfinity,150)),"A block ends after it starts"),
        (Points(new ChartPoint(0,null){XEnd=2}),"cannot be missing"),(Points(ChartPoint.Block(0,3,150),ChartPoint.Block(2,5,250)),"Blocks in one series cannot overlap"),
        (Points(ChartPoint.Block(0,5,150),ChartPoint.Block(1,2,250)),"cannot overlap"),(Points(ChartPoint.Block(3,5,150),ChartPoint.Block(0,4,250)),"cannot overlap"),
        (spec with{Kind=ChartKind.Column,Series=[spec.Series[0] with{Kind=ChartKind.Blocks}]},"no slot a block could take"),
        (spec with{XAxis=AxisKind.Log,Series=[With(s=>s with{Points=[ChartPoint.Block(1,2,150)]}).Series[0]]},"linear or a time X axis"),
        (new ChartSpec{Kind=ChartKind.Line,XAxis=AxisKind.Log,Series=[new("Power",[new(1,100),new(5,200)]),new("Plan",[ChartPoint.Block(1,2,150)]){Kind=ChartKind.Blocks}]},"linear or a time X axis"),
        (With(s=>s with{Trend=true}),"no trend line"),(With(s=>s with{ProjectedFrom=1}),"Blocks take no projection"),
        (spec with{Series=[spec.Series[0],line with{Kind=ChartKind.Line,Points=[new(0,100){XEnd=1}]}]},"XEnd ends a span or a block"),(spec with{Series=[spec.Series[0],line with{Kind=ChartKind.Line,Points=[new(0,100){XEnd=1}]}]},"timeline charts and to series drawn as blocks"),
        (spec with{XAxis=AxisKind.Time,Series=[With(s=>s with{Points=[ChartPoint.Block(Utc(2026,9,1),1e15,150)]}).Series[0]]},"Unix milliseconds"),
        (With(s=>s with{StrokeWidth=2}),"stroke width"),(With(s=>s with{Curve=LineCurve.Step}),"smooth or stepped"),(With(s=>s with{Fill=AreaFill.Fade}),"faded fill"),
        (With(s=>s with{Markers=MarkerStyle.Hollow}),"Marker styles"),(With(s=>s with{HighlightLast=true}),"Highlighting the last point"),(With(s=>s with{ValueLabels=true}),"Value labels"),
        (With(s=>s with{Gradient=[new(100,"#3F87D9"),new(300,"#DD4B45")]}),"gradient"),(With(s=>s with{Goal=10}),"ring charts only"),
        (spec with{IncludeZero=false,YMax=null,YAxis=AxisKind.Log,Series=[With(s=>s with{Points=[ChartPoint.Block(0,1,0)]}).Series[0]]},"positive values"),
        (spec with{YMax=null,YAxis=AxisKind.Log},"Log axes cannot include zero"),
        (new ChartSpec{Kind=ChartKind.Timeline,Series=[new("Light",[ChartPoint.Span(0,1)]),new("Plan",[ChartPoint.Block(0,1,1)]){Kind=ChartKind.Blocks}]},"does not apply to a timeline"),
        (new ChartSpec{Kind=ChartKind.Bar,Series=[new("Plan",[ChartPoint.Block(0,1,1)]){Kind=ChartKind.Blocks}]},"own kind applies to"),
        (new ChartSpec{Kind=ChartKind.Calendar,XAxis=AxisKind.Time,Series=[new("Plan",[ChartPoint.Block(Utc(2026,9,1),Utc(2026,9,2),1)]){Kind=ChartKind.Blocks}]},"own kind does not apply to a calendar"),
        (spec with{DensityCells=20},"Density cells apply to scatter"),(spec with{CalendarCell=CalendarCell.Dot},"calendar charts only"),(spec with{TimelineConnectors=false},"only a timeline")})
        Check(Refusal(bad).Contains(reason),$"\"{Refusal(bad)}\" does not say \"{reason}\"");
    // Within the rules: touching, apart and unordered blocks; overlapping blocks of two series; a time axis, duration and time-of-day
    // formats, a reversed or logarithmic axis, zones and point colours, a pane and its right-hand axis, annotations, an axis on the
    // right labelled at its ends, blocks beside candles, and an empty series.
    var candles=Sample(ChartKind.Candlestick);
    foreach(var good in new[]{Points(ChartPoint.Block(3,4,1),ChartPoint.Block(0,1,2),ChartPoint.Block(1,3,3)),spec with{Series=[spec.Series[0],spec.Series[0] with{Name="Again"}]},
        spec with{XAxis=AxisKind.Time,Series=[With(s=>s with{Points=[ChartPoint.Block(Utc(2026,9,1),Utc(2026,9,8),150)]}).Series[0]]},
        spec with{XFormat=ValueFormat.TimeOfDay,YFormat=ValueFormat.Duration,YReversed=true,IncludeZero=false,YMax=null},spec with{XFormat=ValueFormat.Duration,YAxisSide=AxisSide.Right,YTickLabels=TickLabels.Ends},
        spec with{IncludeZero=false,YMax=null,YAxis=AxisKind.Log,Y2Axis=AxisKind.Log,Series=[spec.Series[0],spec.Series[0] with{Name="Right",Secondary=true}]},
        With(s=>s with{Zones=ZoneScale.CogganPower(250),Points=[s.Points[0] with{Color="#123456"},s.Points[1]]}) with{YZones=ZoneScale.CogganPower(250)},
        spec with{Panes=[new(){Y2Axis=AxisKind.Log}],Series=[spec.Series[0],spec.Series[0] with{Name="Below",Pane=1},spec.Series[0] with{Name="Right",Pane=1,Secondary=true,Points=[ChartPoint.Block(0,1,10)]}]},
        spec with{Annotations=[new(AnnotationAxis.Y,200){Label="FTP"},new(AnnotationAxis.X,1){To=3,Label="Main set"}]},
        candles with{Series=[..candles.Series,new("Plan",[ChartPoint.Block(0,1,10)]){Kind=ChartKind.Blocks}]},spec with{Series=[new("Empty",[])]}})
        Check(XDocument.Parse(ChartSvg.Render(good)).Root!.Name==ns+"svg");
    Check(ChartSvg.Render(new ChartSpec{Kind=ChartKind.Blocks}).Contains("No data to display")&&ChartSvg.Render(spec with{Series=[new("Empty",[])]}).Contains("No data to display"),"an empty blocks chart does not draw its empty state");
});
Test("Blocks: a spec survives JSON as \"kind\":\"Blocks\" with each \"xEnd\", and CSV carries XEnd, every record ending CRLF",()=>{
    var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    var spec=Plan(ChartPoint.Block(0,2,150,"Warm-up"),ChartPoint.Block(2,5,250,"Interval") with{Color="#123456"}) with{XFormat=ValueFormat.Duration,
        Series=[new("Plan",[ChartPoint.Block(0,2,150,"Warm-up"),ChartPoint.Block(2,5,250,"Interval") with{Color="#123456"}]){Zones=ZoneScale.CogganPower(250)},new("Power",[new(1,140),new(4,255)]){Kind=ChartKind.Line}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,options);
    Check(json.Contains("\"kind\":\"Blocks\"")&&json.Contains("\"xEnd\":2")&&json.Contains("\"xEnd\":5"),json);
    Check(ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,options)!)==ChartSvg.Render(spec),"the blocks changed in transit");
    var request=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"yFormat\":\"Duration\",\"yReversed\":true,\"series\":[{\"name\":\"Laps\",\"kind\":\"Blocks\",\"points\":[{\"x\":0,\"xEnd\":1,\"y\":301,\"label\":\"Lap 1\"},{\"x\":1,\"xEnd\":2.5,\"y\":292}]}]}",options)!;
    Check(request.Series[0].Kind==ChartKind.Blocks&&request.Series[0].Points[1].XEnd==2.5&&Labels(Svg(request)).SequenceEqual(["Lap 1: 0 to 1, 5:01","Laps: 1 to 2.5, 4:52"]),string.Join(" | ",Labels(Svg(request))));
    // The file carries where each block ends; the line beside it has no end, and every record, the header included, ends CRLF.
    var csv=ChartExport.Csv(spec);
    Check(csv.StartsWith("Series,X,Y,Label,Size,XEnd\r\n")&&csv.Contains("\"Plan\",0,150,\"Warm-up\",1,2\r\n")&&csv.Contains("\"Plan\",2,250,\"Interval\",1,5\r\n")&&csv.Contains("\"Power\",4,255,\"\",1,\r\n"),csv);
    Check(csv.Split("\r\n").Length==6&&csv.EndsWith("\r\n")&&!csv.Replace("\r\n","").Contains('\n'),"a record does not end CRLF");
    var timed=ChartExport.Csv(new ChartSpec{Kind=ChartKind.Blocks,XAxis=AxisKind.Time,Series=[new("Week",[ChartPoint.Block(Utc(2026,8,3),Utc(2026,8,10),320)])]});
    Check(timed==$"Series,X,XTime,Y,Label,Size,XEnd\r\n\"Week\",{Utc(2026,8,3).ToString(CultureInfo.InvariantCulture)},2026-08-03T00:00:00.000Z,320,\"\",1,{Utc(2026,8,10).ToString(CultureInfo.InvariantCulture)}\r\n",timed);
});
Test("Charts that use none of 0.29.0 draw byte for byte as 0.28.0 did, in both finishes, gradient IDs included, and blocks define no IDs",()=>{
    // Five rows of the release baseline, hashed by 0.28.0: its own specs rebuilt here, refined and classic. They cover the paths
    // blocks touch: the X axis, the labels under it, the Y axes of each pane, the column outline and its fade, and legend keys.
    ChartPoint[] Points()=>Enumerable.Range(0,12).Select(i=>new ChartPoint(i,10+i*3+(i%3)*4,$"P{i}")).ToArray();
    ChartPoint[] Signed()=>Points().Select((p,i)=>p with{Y=i%3==0?-p.Y/2:p.Y-20}).ToArray();
    ChartSpec Base(ChartKind kind)=>new(){Kind=kind,Title="Baseline",Description="Default output",Series=[new("A",Points()),new("B",Points().Select(p=>p with{Y=p.Y+5}).ToArray())]};
    var line=Base(ChartKind.Line);
    (string Row,ChartSpec Spec,string Refined,string Classic)[] rows=[
        ("Column/Light/True",Base(ChartKind.Column),"3B8FB1E4B8E95D71","874EE9ADD3323710"),
        ("guard/time",line with{XAxis=AxisKind.Time,Series=line.Series.Select(s=>s with{Points=s.Points.Select(p=>p with{X=1767225600000d+p.X*86400000d,Label=null}).ToArray()}).ToArray()},"6F65F0C3788C47BD","48647563F4A154CE"),
        ("guard/continuous-columns-negative",line with{Y2Label="Rate",Series=[new("Line",Points()),new("Signed",Signed()){Kind=ChartKind.Column},new("Rate",Signed().Select(p=>p with{Y=p.Y/4}).ToArray()){Kind=ChartKind.Column,Secondary=true}]},"61ED7B69CCD4230F","F49791902B33DC81"),
        ("finish/capsule-value-labels",Base(ChartKind.Column) with{Title="Capsules",Style=ChartStyle.Light with{BarRadius=9999},Series=[new("Week",Signed()){ValueLabels=true,Fill=AreaFill.Fade},new("Last",Points().Select(p=>p with{Y=p.Y/2}).ToArray()){ValueLabels=true}]},"A48E1BFCC1A75412","08631EBEDF404578"),
        ("finish/stacked-capsules",Base(ChartKind.StackedColumn) with{Title="Stacked capsules",Style=ChartStyle.Midnight,Series=[new("A",Signed()),new("B",Points()),new("C",Signed().Select(p=>p with{Y=p.Y<0?p.Y:null}).ToArray())]},"E8ED2DBF9D3045E1","61E456DA5633643A")];
    foreach(var (row,spec,refined,classic) in rows)
    {
        Check(Hash16(ChartSvg.Render(spec))==refined,$"{row} is not 0.28.0's: {Hash16(ChartSvg.Render(spec))}");
        Check(Hash16(ChartSvg.Render(Classic(spec)))==classic,$"{row} is not 0.28.0's in the classic finish: {Hash16(ChartSvg.Render(Classic(spec)))}");
    }
    // The capsules' fade is a gradient, so its ID, named after the spec's hash, is 0.28.0's too.
    Check(ChartSvg.Render(rows[3].Spec).Contains("<linearGradient id='lumen-"),"the capsule row defines no gradient");
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight,ChartStyle.Light with{Finish=ChartFinish.Classic}})
    {
        var svg=ChartSvg.Render(Sample(ChartKind.Blocks) with{Style=style,Series=[Sample(ChartKind.Blocks).Series[0] with{Zones=ZoneScale.CogganPower(250)}]});
        Check(!svg.Contains(" id=")&&!svg.Contains("<defs")&&!svg.Contains("url("),$"{style.Background}: blocks define an ID");
    }
});
Test("Blocks: the component zooms their X, reads a block in its status line and data table, keys zoned blocks by their zones, and hides them",()=>{
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    string Drawn(LumenChart chart)=>(string)typeof(LumenChart).GetField("svg",flags)!.GetValue(chart)!;
    var spec=Plan(ChartPoint.Block(0,4,150,"Warm-up"),ChartPoint.Block(4.5,5.5,250,"Interval"),ChartPoint.Block(6,10,120,"Cool-down")) with{XFormat=ValueFormat.Duration,
        Series=[new("Plan",[ChartPoint.Block(0,240,150,"Warm-up"),ChartPoint.Block(270,330,250,"Interval"),ChartPoint.Block(360,600,120,"Cool-down")]){Zones=ZoneScale.CogganPower(250)},new("Power",[new(0,140),new(600,130)]){Kind=ChartKind.Line}]};
    var html=RenderInside(null,spec);
    Check(html.Contains("aria-label=\"Zoom in\"")&&html.Contains(ChartSvg.LegendKey(spec,0))&&html.Contains("Interval: 4:30 to 5:30, 250, Lactate threshold"),"the component offers no zoom, or lost the key or the block");
    double before=0,after=0;string hidden="";
    var shown=Operate(spec,async chart=>{
        typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);
        double Width(string svg)=>BlockBox(BlockPaths(XDocument.Parse(svg))[1]) is var b?b.Right-b.Left:0;
        before=Width(Drawn(chart));
        // Zooming in halves the X axis about its middle, so the block in the middle, which touches no other, is twice as wide.
        typeof(LumenChart).GetMethod("Zoom",flags)!.Invoke(chart,[.5]);
        after=Width(Drawn(chart));
        typeof(LumenChart).GetMethod("ResetView",flags)!.Invoke(chart,[]);
        typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[0]);hidden=Drawn(chart);typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[0]);
        await chart.SelectPoint(0,1);
    });
    Check(Close(before,79.4)&&Close(after,158.8),$"zooming drew the middle block {after} wide, {before} before");
    Check(shown.Contains("Plan: Interval, 4:30 to 5:30 = 250</span>"),"the status line does not read the block");
    Check(shown.Contains("<tr><td>Plan</td><td>Interval</td><td>250 from 4:30 to 5:30</td></tr>")&&shown.Contains("<tr><td>Power</td><td>10:00</td><td>130</td></tr>"),"the data table does not read the block");
    Check(!hidden.Contains("lumen-block")&&hidden.Contains("data-series='0'"),"hiding the blocks hid the line, or left the blocks");
});
Test("Sports page: the laps are the stream's run split where the stream marks the steps of its progression, its pace lap by lap on the elevation's distance axis",()=>{
    var card=sports.Single(c=>c.Id=="laps");var spec=card.Spec;var track=latest.Track!;
    Check(card.Section=="session"&&card.Wide&&spec.Kind==ChartKind.Blocks&&spec.YReversed&&spec.YFormat==ValueFormat.Duration&&spec.XLabel==Sports("elevation").XLabel,"the laps are not blocks of pace on a reversed axis in kilometres");
    var laps=spec.Series.Single().Points;
    // Four laps of 4, 4, 3 and 3 km, touching, the last ending where the run did; the stream marks 4, 8 and 11 km, where they meet.
    Check(laps.Select(p=>p.X).SequenceEqual([0d,4,8,11])&&laps.Select(p=>p.XEnd!.Value).SequenceEqual([4d,8,11,Math.Round(latest.Metres/1000,2)])&&latest.Steps.Select(s=>s.Metres).SequenceEqual([4000d,4000,3000,3000]),
        string.Join(" | ",laps.Select(p=>$"{p.X}-{p.XEnd}")));
    Check(Sports("stream").Annotations.Select(a=>a.From).SequenceEqual(laps.Skip(1).Select(p=>Math.Round(SportsData.TimeAt(track,latest.Metres,p.X*1000)))),"the stream's markers are not where the laps meet");
    // Each lap's pace is its time over its length; the four add up to the run, and each lap is faster than the last.
    var ends=laps.Select((p,i)=>i==laps.Count-1?latest.Metres:p.XEnd!.Value*1000).ToArray();
    var times=laps.Select((p,i)=>SportsData.TimeAt(track,latest.Metres,ends[i])-SportsData.TimeAt(track,latest.Metres,p.X*1000)).ToArray();
    Check(laps.Select((p,i)=>p.Y==Math.Round(times[i]/(ends[i]-p.X*1000)*1000)).All(ok=>ok)&&Math.Abs(times.Sum()-latest.Seconds)<1e-6,"a lap's pace is not its time over its length");
    Check(laps.Zip(laps.Skip(1)).All(p=>p.Second.Y<p.First.Y),"the progression does not get faster lap by lap");
    // The kilometre splits inside each lap add up to it, to their rounding.
    var splits=SportsData.Splits(latest);
    Check(laps.Take(3).All(l=>Math.Abs(splits.Skip((int)l.X).Take((int)(l.XEnd!.Value-l.X)).Sum()-l.Y!.Value*(l.XEnd!.Value-l.X))<=(l.XEnd!.Value-l.X)*.5+.5*(l.XEnd!.Value-l.X)),"the splits and the laps disagree");
    // The average pace is the run's, as the session's summary writes it, and the title reads the first and last laps.
    var average=spec.Annotations.Single();
    Check(average.Label=="Average"&&average.From==latest.Seconds/latest.Metres*1000&&SportsData.SessionSummary().Contains($"{SportsData.Clock(average.From)} per km")&&spec.Title==$"4 laps from {SportsData.Clock(laps[0].Y!.Value)} to {SportsData.Clock(laps[^1].Y!.Value)} per km",spec.Title);
    var names=Labels(Svg(spec));
    Check(names.Length==4&&names[1]==$"Lap 2: 4 to 8, {SportsData.Clock(laps[1].Y!.Value)}",string.Join(" | ",names));
});
Test("Sports page: the next session is the first planned day's workout, in Coggan's levels at its threshold, its stress the performance chart's projected column",()=>{
    var card=sports.Single(c=>c.Id=="next-session");var spec=card.Spec;var next=athlete.Upcoming[0];
    Check(card.Section=="load"&&card.Wide&&spec.Kind==ChartKind.Blocks&&spec.XFormat==ValueFormat.Duration&&spec.IncludeZero,"the next session is not a workout of blocks over planned time");
    Check(sports.SkipWhile(c=>c.Id!="performance").Skip(1).First().Id=="next-session","the next session does not follow the performance chart");
    // It is the first planned day, the Tuesday after today, the Monday between a rest day; its stress is the day's projected column.
    var daily=Sports("performance").Series.Single(s=>s.Name=="Daily stress").Points;
    Check(next.Day==SportsData.Today.AddDays(2)&&athlete.Planned[0]==(next.Day,next.Stress)&&daily.Single(p=>DayOf(p.X)==SportsData.Today.AddDays(1)).Y==0&&daily.Single(p=>DayOf(p.X)==next.Day).Y==next.Stress,"the next session is not the first projected day");
    Check(spec.Title==$"Tuesday: {next.Name.ToLowerInvariant()}, stress {next.Stress.ToString(CultureInfo.InvariantCulture)}"&&spec.Description==$"{SportsData.HoursMinutes(spec.Series[0].Points[^1].XEnd!.Value)} · 4 × 800 m at 324 W, threshold 300 W",spec.Title+" | "+spec.Description);
    // Its steps: a 2 km warm-up, four 800 m repeats each followed by 400 m easy, and a 1.5 km cool-down, planned against the run's
    // threshold of 300 W at the season's final threshold pace, each step as long as its length takes at its target.
    var blocks=spec.Series.Single().Points;
    Check(next.Sport==Sport.Run&&next.Ftp==75*1000/SportsData.PaceAfter&&spec.Series[0].Zones!.Zones.Select(z=>z.Upper).SequenceEqual(ZoneScale.CogganPower(300).Zones.Select(z=>z.Upper))&&spec.Annotations.Single().From==300,"the threshold is not the run's");
    Check(blocks.Select(b=>b.Label).SequenceEqual(["Warm-up, 2 km","Repeat 1, 800 m","Recovery, 400 m","Repeat 2, 800 m","Recovery, 400 m","Repeat 3, 800 m","Recovery, 400 m","Repeat 4, 800 m","Recovery, 400 m","Cool-down, 1.5 km"]),string.Join(" | ",blocks.Select(b=>b.Label)));
    Check(blocks[0].X==0&&blocks.Zip(blocks.Skip(1)).All(p=>p.First.XEnd==p.Second.X)&&blocks.Zip(next.Steps).All(p=>p.First.Y==Math.Round(p.Second.Watts)&&Math.Abs(p.First.XEnd!.Value-p.First.X-p.Second.Seconds)<=1)
        &&next.Steps.Zip(blocks).All(p=>Math.Abs(p.First.Seconds-p.First.Metres/(1000/SportsData.PaceAfter*p.First.Watts/next.Ftp))<1e-9),"a step is not as long as its length takes at its target");
    Check(Math.Abs(next.Steps.Sum(s=>s.Metres)-next.Metres)<50,"the plan is not the run simulated");
    // The repeats are VO2max and the easy stretches endurance, so the blocks read the session at a glance in every brand's ramp.
    var names=Labels(Svg(spec));
    Check(names[1]==$"Repeat 1, 800 m: {SportsData.Clock(blocks[1].X)} to {SportsData.Clock(blocks[1].XEnd!.Value)}, 324, VO2max"&&names[2].EndsWith(", 186, Endurance")&&names[0].EndsWith(", 228, Tempo"),string.Join(" | ",names));
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight})
    {
        var fills=Svg(spec with{Style=style}).Descendants(ns+"path").Where(p=>(string?)p.Attribute("class")=="lumen-block").Select(p=>(string)p.Attribute("fill")!).ToArray();
        Check(fills.Length==10&&fills.Distinct().SequenceEqual([style.Zones[2],style.Zones[4],style.Zones[1]])&&fills.All(f=>Contrast(f,style.Background)>=3),$"{style.Background}: {string.Join(", ",fills.Distinct())}");
    }
});
// 0.30.0: a calendar's ramp steps up from its empty day, and graphs turn top to bottom to fit a narrow box. Colours are mixed per
// channel and truncated, as the heatmap's ramp always has been, so these mix the same way on their own.
string Blend(string from,string to,double t){int Channel(string hex,int at)=>Convert.ToInt32(hex.Substring(at,2),16);return "#"+string.Concat(new[]{1,3,5}.Select(at=>((int)(Channel(from,at)+(Channel(to,at)-Channel(from,at))*t)).ToString("X2")));}
Test("Calendar ramp: on every preset, a brand and the classic finish, the quietest day is a third of the way from an empty day to the heatmap's high end and the busiest that end, in squares, dots and bubbles, the key's swatches and legend key match, and the quietest day stands between a rest day and the busiest",()=>{
    var from=new DateOnly(2026,9,14);
    var spec=Days(from,4,i=>new double?[]{10,0,30,50}[i]);
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight,Brand(),ChartStyle.Light with{Finish=ChartFinish.Classic},ChartStyle.Dark with{Finish=ChartFinish.Classic}})
    {
        var low=Blend(style.Grid,style.HeatmapHigh,1/3d);
        foreach(var cell in Enum.GetValues<CalendarCell>())
        {
            var doc=Svg(spec with{Style=style,CalendarCell=cell});
            var (quiet,busy)=((string?)DayShape(doc,from).Attribute("fill"),(string?)DayShape(doc,from.AddDays(3)).Attribute("fill"));
            Check(quiet==low&&busy==style.HeatmapHigh&&(string?)DayShape(doc,from.AddDays(2)).Attribute("fill")==Blend(low,style.HeatmapHigh,.5),$"{style.Background} {cell}: the ramp runs from {quiet} to {busy}");
            // The rest day's track is the grid colour, and the quietest day lies between it and the busiest, never past it.
            var track=(string?)DayTracks(doc).Single(t=>t.Parent==doc.Root).Attribute("fill");
            Check(track==style.Grid&&quiet!=track&&(Luminance(quiet!)-Luminance(track!))*(Luminance(busy!)-Luminance(quiet!))>0&&Contrast(quiet!,track!)>1.2,$"{style.Background} {cell}: the quietest day {quiet} against the track {track}, {Contrast(quiet!,track!):0.00}:1");
            var swatches=doc.Root!.Elements().Where(e=>e.Attribute("class") is null&&(e.Name==ns+"rect"&&Attr(e,"width")==10||e.Name==ns+"circle"&&Attr(e,"r")==5)).Select(e=>(string?)e.Attribute("fill")).ToArray();
            Check(swatches.SequenceEqual(Enumerable.Range(0,5).Select(k=>Blend(low,style.HeatmapHigh,k/4d))),$"{style.Background} {cell}: the key is {string.Join(",",swatches)}");
        }
        // The component's legend keys the ramp by its ends in the refined finish; the classic one keys every series by a square.
        Check(style.Finish==ChartFinish.Classic||XDocument.Parse(ChartSvg.LegendKey(spec with{Style=style},0)).Root!.Elements().Select(e=>(string?)e.Attribute("fill")).SequenceEqual([low,style.HeatmapHigh]),$"{style.Background}: the legend key is not the ramp");
    }
    // Tiers are untouched: a zoned calendar never takes the ramp.
    Check(!Svg(spec with{YZones=Effort()}).ToString().Contains("#B0C1E8"),"a zoned calendar took the ramp");
});
Test("Heatmaps keep their ramp from HeatmapLow to HeatmapHigh on every preset, cells and legend key alike",()=>{
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight,Brand()})
    {
        var doc=Svg(Spec(ChartKind.Heatmap) with{Style=style,Series=[new("Row",[new(0,0,"A"),new(1,5,"B"),new(2,10,"C")])]});
        var fills=doc.Descendants(ns+"g").Where(g=>g.Attribute("data-point") is not null).Select(g=>(string?)g.Element(ns+"rect")!.Attribute("fill")).ToArray();
        Check(fills.SequenceEqual([style.HeatmapLow,Blend(style.HeatmapLow,style.HeatmapHigh,.5),style.HeatmapHigh]),$"{style.Background}: {string.Join(",",fills)}");
        Check(XDocument.Parse(ChartSvg.LegendKey(Spec(ChartKind.Heatmap) with{Style=style},0)).Root!.Elements().Select(e=>(string?)e.Attribute("fill")).SequenceEqual([style.HeatmapLow,style.HeatmapHigh]),$"{style.Background}: the heatmap's key moved");
    }
});
// A node's label is drawn 12 pixels high, as wide as the library's generous estimate: .62 of an em for most letters, .9 for m and
// w, .3 for a space. The gallery's pipeline is the graph the home page shows, built once so that a spec fitted from it compares
// equal to it changed by hand.
var pipelines=Enum.GetValues<GraphLayout>().ToDictionary(layout=>layout,layout=>DemoData.Graph(layout,ChartTheme.Light));
GraphSpec Pipeline(GraphLayout layout=GraphLayout.Layered)=>pipelines[layout];
string Shape(GraphSpec graph)=>$"{graph.Width} by {graph.Height} {graph.Layout} {graph.Direction}";
Dictionary<string,NodePosition> Placed(GraphSpec spec)=>GraphEngine.Layout(spec).ToDictionary(p=>p.Id);
// A crowded pipeline: ten long labels, two cut at 22 characters, and fourteen edges, half of them labelled. It is acyclic, so it lays
// out in both layouts.
var crowded=new GraphSpec{Title="A crowded pipeline",
    Nodes=[new("orders","Customer orders feed"),new("crm","CRM contacts export"),new("web","Web analytics events"),new("lake","Raw data lake (landing zone)"),new("clean","Cleansing and dedupe"),
        new("join","Identity resolution"),new("model","Revenue attribution model"),new("warehouse","Analytics warehouse"),new("dash","Executive dashboards"),new("alerts","Anomaly alerts")],
    Edges=[new("orders","lake","nightly"),new("crm","lake","hourly"),new("web","lake","stream"),new("lake","clean"),new("clean","join"),new("crm","join","match keys"),new("join","model"),
        new("clean","warehouse","audited rows"),new("model","warehouse"),new("warehouse","dash"),new("warehouse","alerts","thresholds"),new("model","alerts"),new("web","dash","live"),new("orders","model")]};
// The generous width the engine estimates for text: .62 of an em for most characters, .9 for m and w, .3 for a space and punctuation.
double Estimate(string text,double size)=>text.Sum(c=>c is '.' or ',' or ':' or ' ' ? .3 : c is '-' ? .36 : c is 'm' or 'M' or 'w' or 'W' ? .9 : .62)*size;
// A drawn edge as a line of points, its quadratic pieces cut fine, and the point a fraction of its length along it.
(double X,double Y)[] EdgeLine(string d)
{
    var tokens=d.Replace("M"," M ").Replace("L"," L ").Replace("Q"," Q ").Split(' ',StringSplitOptions.RemoveEmptyEntries);
    (double X,double Y) Point(string token){var p=token.Split(',');return (double.Parse(p[0],CultureInfo.InvariantCulture),double.Parse(p[1],CultureInfo.InvariantCulture));}
    var line=new List<(double X,double Y)>();
    for(var i=0;i<tokens.Length;i++)
        if(tokens[i] is "M" or "L")line.Add(Point(tokens[++i]));
        else if(tokens[i]=="Q")
        {
            var (s,c,e)=(line[^1],Point(tokens[++i]),Point(tokens[++i]));
            for(var k=1;k<=64;k++){double t=k/64d,u=1-t;line.Add((u*u*s.X+2*u*t*c.X+t*t*e.X,u*u*s.Y+2*u*t*c.Y+t*t*e.Y));}
        }
    return line.ToArray();
}
(double X,double Y) Partway((double X,double Y)[] line,double fraction)
{
    double Length(int i)=>Math.Sqrt((line[i].X-line[i-1].X)*(line[i].X-line[i-1].X)+(line[i].Y-line[i-1].Y)*(line[i].Y-line[i-1].Y));
    var left=fraction*Enumerable.Range(1,line.Length-1).Sum(Length);
    for(var i=1;i<line.Length;i++){if(left<=Length(i))return (line[i-1].X+(line[i].X-line[i-1].X)*left/Length(i),line[i-1].Y+(line[i].Y-line[i-1].Y)*left/Length(i));left-=Length(i);}
    return line[^1];
}
// A graph as drawn and measured: each node's centre and label box, each edge with its drawn line, and each edge label's box. A label's
// box is the engine's: its estimated width by a line, 1.2 em, centred .35 em above its baseline.
Drawing Measure(GraphSpec spec,IReadOnlyDictionary<string,GraphPoint>? moved=null)
{
    var root=XDocument.Parse(GraphEngine.Render(spec,moved)).Root!;
    Bounds Words(XElement text,double size)
    {
        var (x,y,wide)=(Attr(text,"x"),Attr(text,"y"),Estimate(text.Value,size));
        var left=(string?)text.Attribute("text-anchor") switch{"start"=>x,"end"=>x-wide,_=>x-wide/2};
        return new(left,y-.35*size-.6*size,left+wide,y-.35*size+.6*size);
    }
    var nodes=root.Elements(ns+"g").Where(g=>g.Attribute("data-node") is not null).ToDictionary(g=>(string)g.Attribute("data-node")!,
        g=>(X:Attr(g.Element(ns+"circle")!,"cx"),Y:Attr(g.Element(ns+"circle")!,"cy"),Label:Words(g.Elements(ns+"text").ElementAt(1),12)));
    var lines=root.Elements(ns+"path").Where(p=>(string?)p.Attribute("stroke-width")=="1.5").Select(p=>EdgeLine((string)p.Attribute("d")!)).ToArray();
    return new(nodes,spec.Edges.Where(e=>e.Source!=e.Target).Zip(lines,(e,l)=>(e,l)).ToArray(),
        root.Elements(ns+"text").Where(t=>(string?)t.Attribute("font-size")=="10").Select(t=>(t.Value,Words(t,10))).ToArray());
}
// A node's label as drawn, cut to 22 characters.
string Cut(string label)=>label.Length<=22?label:label[..21]+"…";
// Every pair of a graph's nodes that stand at one height: how far apart their centres stand, and how far their labels' boxes.
(string Pair,double Apart,double Gap)[] Level(GraphSpec spec)
{
    var p=GraphEngine.Layout(spec);
    double Label(int i)=>Estimate(Cut(spec.Nodes[i].Label),12);
    return [..from i in Enumerable.Range(0,p.Count) from j in Enumerable.Range(i+1,p.Count-i-1) where Math.Abs(p[i].Y-p[j].Y)<1e-6
        select ($"{spec.Nodes[i].Label} and {spec.Nodes[j].Label}",Math.Abs(p[i].X-p[j].X),Math.Abs(p[i].X-p[j].X)-(Label(i)+Label(j))/2)];
}
// The home page's graph, or the crowded one, fitted to a wide screen's box, a laptop's and a phone's in each layout and direction.
GraphSpec[] Widths(GraphSpec graph)=>[..from layout in Enum.GetValues<GraphLayout>() from direction in Enum.GetValues<GraphDirection>() from width in new[]{1280,900,375}
    select GraphEngine.Fit(graph with{Layout=layout,Direction=direction},width)];
Test("Top to bottom: a layered graph's levels stand in rows between the 90-pixel ends, each level's slots spread across the width in equal bands, and a circular graph ignores it",()=>{
    var chain=Placed(Graph() with{Direction=GraphDirection.TopToBottom});
    Check(chain.Values.All(p=>p.X==450)&&chain["a"].Y==90&&chain["b"].Y==230&&chain["c"].Y==370,string.Join(" | ",chain.Values));
    // Spanning's middle level holds b and the bend of a to c, each in half of the width inside the 24-pixel margins.
    var spec=Spanning() with{Direction=GraphDirection.TopToBottom};
    var placed=Placed(spec);var bend=GraphEngine.Routes(spec)[2].Points[1];
    Check(placed["a"]==new NodePosition("a",450,90)&&placed["c"]==new NodePosition("c",450,370)&&placed["b"].Y==230&&bend.Y==230&&new[]{placed["b"].X,bend.X}.Order().SequenceEqual([237d,663]),$"{string.Join(" | ",placed.Values)}, bend {bend}");
    // The same ordering as left to right: Crossing's second level is reordered so d stands opposite a, now across rather than down.
    var crossing=Placed(Crossing() with{Direction=GraphDirection.TopToBottom});
    Check(GraphEngine.Crossings(Crossing() with{Direction=GraphDirection.TopToBottom})==0&&crossing["d"].X<crossing["c"].X&&crossing["a"].X<crossing["b"].X&&crossing["a"].Y==crossing["b"].Y&&crossing["d"].Y>crossing["a"].Y,"the levels are not ordered across");
    // One level alone stands at the middle of the height.
    Check(Placed(new GraphSpec{Nodes=[new("a","A"),new("b","B")],Direction=GraphDirection.TopToBottom}).Values.All(p=>p.Y==230),"a single level is not at the middle");
    var circle=Pipeline(GraphLayout.Circular);
    Check(GraphEngine.Render(circle with{Direction=GraphDirection.TopToBottom})==GraphEngine.Render(circle)&&GraphEngine.Layout(circle with{Direction=GraphDirection.TopToBottom}).SequenceEqual(GraphEngine.Layout(circle)),"a circular graph turned");
    Check(XDocument.Parse(GraphEngine.Render(spec)).Descendants(ns+"desc").Single().Value=="3 nodes · 3 directed connections · Layered layout top to bottom · 0 edge crossings","the description does not say which way it runs");
    Reject(()=>GraphEngine.Layout(spec with{Direction=(GraphDirection)2}));
    // The direction survives JSON by name, and a request that names none runs left to right.
    var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,options);
    Check(json.Contains("\"direction\":\"TopToBottom\"")&&GraphEngine.Render(System.Text.Json.JsonSerializer.Deserialize<GraphSpec>(json,options)!)==GraphEngine.Render(spec),json);
    Check(System.Text.Json.JsonSerializer.Deserialize<GraphSpec>("{\"nodes\":[{\"id\":\"a\",\"label\":\"A\"}]}",options)!.Direction==GraphDirection.LeftToRight,"a request without a direction does not run left to right");
});
Test("Top to bottom: edges leave from under their source's label and point down, long ones bend through each row they pass, labels stand beside their edge and a self-loop at its node's right",()=>{
    var spec=new GraphSpec{Direction=GraphDirection.TopToBottom,Nodes=[new("a","A"),new("b","B"),new("c","C")],Edges=[new("a","b"),new("b","c"),new("a","c","long"),new("c","c")]};
    var placed=Placed(spec);var doc=XDocument.Parse(GraphEngine.Render(spec));
    var paths=doc.Root!.Elements(ns+"path").ToArray();
    // Each edge is its line and its arrowhead; the loop has no head.
    var lines=paths.Where(p=>(string?)p.Attribute("stroke-width")=="1.5").Select(p=>(string)p.Attribute("d")!).ToArray();
    var heads=paths.Where(p=>p.Attribute("stroke") is null).Select(p=>((string)p.Attribute("d")!).Split(' ').Select(s=>s.TrimStart('M','L').Split(',')).Where(s=>s.Length==2).Select(s=>(X:double.Parse(s[0],CultureInfo.InvariantCulture),Y:double.Parse(s[1],CultureInfo.InvariantCulture))).ToArray()).ToArray();
    Check(lines.Length==3&&heads.Length==3,$"{lines.Length} lines and {heads.Length} heads");
    Check(lines[0].StartsWith($"M{placed["a"].X.ToString(CultureInfo.InvariantCulture)},{(placed["a"].Y+50).ToString(CultureInfo.InvariantCulture)} ")&&lines[1].StartsWith($"M{placed["b"].X.ToString(CultureInfo.InvariantCulture)},{(placed["b"].Y+50).ToString(CultureInfo.InvariantCulture)} "),"an edge does not leave from under its source's label: "+string.Join(" | ",lines));
    Check(heads.All(h=>h[0].Y>(h[1].Y+h[2].Y)/2),"an arrowhead does not point down");
    // a to c bends once, on b's row and beside b, and arrives at the upper side of c.
    var route=GraphEngine.Routes(spec)[2].Points;
    Check(route.Count==3&&route[1].Y==placed["b"].Y&&Math.Abs(route[1].X-placed["b"].X)>20&&heads[2][0].Y<placed["c"].Y-10,"the long edge does not bend through b's row");
    // Its label stands 6 pixels to the right of the middle of the drawn edge, where it is free (0.31.0; before, at the middle of its
    // first stretch).
    var label=doc.Root!.Elements(ns+"text").Single(t=>t.Value=="long");
    var half=Partway(EdgeLine(lines[2]),.5);
    Check((string?)label.Attribute("text-anchor")=="start"&&Math.Abs(Attr(label,"x")-half.X-6)<.5&&Math.Abs(Attr(label,"y")-half.Y-3.5)<.5,$"the edge's label stands at {Attr(label,"x")},{Attr(label,"y")}, not beside the middle of its edge, {half}");
    var loop=(string)paths.Single(p=>p.Element(ns+"title") is not null).Attribute("d")!;
    Check(loop.StartsWith($"M{(placed["c"].X+17).ToString(CultureInfo.InvariantCulture)},{(placed["c"].Y-12).ToString(CultureInfo.InvariantCulture)} C"),"the loop is not at the node's right: "+loop);
    // A label that would run past the drawing's right edge, 111 pixels of 10-pixel text from 207 in 320, stands on the edge's left.
    var right=new GraphSpec{Direction=GraphDirection.TopToBottom,Width=320,Nodes=[new("a","A"),new("b","B"),new("c","C"),new("d","D")],Edges=[new("a","b"),new("a","c"),new("a","d","a label that is long")]};
    var left=XDocument.Parse(GraphEngine.Render(right)).Root!.Elements(ns+"text").Single(t=>t.Value=="a label that is long");
    Check((string?)left.Attribute("text-anchor")=="end"&&Attr(left,"x")<200&&Attr(left,"x")-111>0,$"the label stands at {Attr(left,"x")}, anchored {left.Attribute("text-anchor")}");
    // An edge that reaches its node from below, once the source is dragged under it, arrives at the foot of the node's label.
    var pair=new GraphSpec{Direction=GraphDirection.TopToBottom,Nodes=[new("a","A"),new("b","B")],Edges=[new("a","b")]};
    var under=(string)XDocument.Parse(GraphEngine.Render(pair,new Dictionary<string,GraphPoint>{["a"]=new(450,600)})).Root!.Elements(ns+"path").First().Attribute("d")!;
    Check(under=="M450,575 L450,420","an edge from below does not reach the foot of the label: "+under);
});
Test("Fit: a graph that fits at its own width and direction comes back unchanged, and one shown wider keeps its layout and height",()=>{
    foreach(var spec in new[]{Pipeline(),Pipeline(GraphLayout.Circular),Spanning(),Graph(),new GraphSpec(),Spanning() with{Direction=GraphDirection.TopToBottom,Height=500}})
        Check(ReferenceEquals(GraphEngine.Fit(spec,spec.Width),spec),$"{spec.Title} {spec.Layout} {spec.Direction} moved at its own width");
    Check(GraphEngine.Fit(Pipeline(),1200)==Pipeline() with{Width=1200}&&GraphEngine.Fit(Pipeline(GraphLayout.Circular),1200)==Pipeline(GraphLayout.Circular) with{Width=1200},"a wider box changed more than the width");
    Check(GraphEngine.Fit(Spanning() with{Direction=GraphDirection.TopToBottom},1200).Direction==GraphDirection.TopToBottom,"a graph set top to bottom turned back");
    Check(Shape(GraphEngine.Fit(new GraphSpec(),375))=="375 by 460 Layered LeftToRight","an empty graph did not take the width");
});
Test("Fit: the home page's pipeline turns top to bottom where its six levels cannot stand its widest label, Validation's 74.4 pixels, and 16 more apart, on a phone among them, and its rows hold its labels apart",()=>{
    // 74.4 + 16 is 90.4, which five gaps between levels reach at 180 + 452 pixels.
    Check(GraphEngine.Fit(Pipeline(),633)==Pipeline() with{Width=633},"it turned with room to stand side by side");
    foreach(var width in new[]{631,375,360,337,322,320})
    {
        var fitted=GraphEngine.Fit(Pipeline(),width);
        Check(fitted==Pipeline() with{Width=width,Height=730,Direction=GraphDirection.TopToBottom},$"at {width}: {Shape(fitted)}");
        // Every node's label, as wide as estimated and 12 high under its node, stays clear of every other label and node.
        var placed=Placed(fitted);var widths=fitted.Nodes.ToDictionary(n=>n.Id,n=>n.Label.Sum(c=>c is ' ' ? .3 : c is 'm' or 'M' or 'w' or 'W' ? .9 : .62)*12);
        foreach(var a in fitted.Nodes)
            foreach(var b in fitted.Nodes.Where(b=>b!=a))
            {
                var (p,q)=(placed[a.Id],placed[b.Id]);
                Check(Math.Abs(p.X-q.X)>=(widths[a.Id]+widths[b.Id])/2||Math.Abs(p.Y-q.Y)>=24,$"at {width}: {a.Label} and {b.Label} overlap");
                Check(Math.Abs(p.X-q.X)>=widths[a.Id]/2+23||p.Y+42+3<q.Y-23||p.Y+30>q.Y+23,$"at {width}: {a.Label}'s label touches {b.Label}");
            }
        Check(fitted.Nodes.Where(n=>placed[n.Id].Y==placed["charts"].Y).Count()==2&&placed["api"].X-placed["charts"].X>=90.4,$"at {width}: Charts and Chart API stand {placed["api"].X-placed["charts"].X} apart");
    }
    Check(GraphEngine.Fit(Pipeline() with{Height=900},360).Height==900,"the rows shrank the graph below its own height");
});
Test("Fit: a level too full for the width even top to bottom takes the narrowest width that holds it, the bends of longer edges counted, within 320 to 4,096",()=>{
    // a fans out to b and three x's and on down a chain to f; a straight to f bends through every level between. Single letters
    // need a node's 46 pixels and 24 more, so five slots need 48 + 350 pixels.
    var fan=new GraphSpec{Nodes=[new("a","A"),new("b","B"),new("x1","X"),new("x2","X"),new("x3","X"),new("c","C"),new("d","D"),new("e","E"),new("f","F")],
        Edges=[new("a","b"),new("a","x1"),new("a","x2"),new("a","x3"),new("b","c"),new("c","d"),new("d","e"),new("e","f"),new("a","f")]};
    var fitted=GraphEngine.Fit(fan,360);
    Check(Shape(fitted)=="398 by 730 Layered TopToBottom",Shape(fitted));
    var placed=Placed(fitted);
    Check(new[]{"b","x1","x2","x3"}.Select(id=>placed[id].X).Order().Zip(new[]{"b","x1","x2","x3"}.Select(id=>placed[id].X).Order().Skip(1)).All(p=>p.Second-p.First>=70),"the fullest level's nodes stand closer than 70");
    Check(GraphEngine.Fit(fan with{Edges=fan.Edges.Take(8).ToArray()},360).Width==360,"without the bends four slots did not fit 360");
    // The width is clamped, and a level of sixty takes no more than 4,096.
    Check(GraphEngine.Fit(Pipeline(),100).Width==320&&GraphEngine.Fit(Pipeline(),100000)==Pipeline() with{Width=4096},"the width is not clamped");
    var wide=new GraphSpec{Direction=GraphDirection.TopToBottom,Nodes=[new("a","A"),..Enumerable.Range(0,60).Select(i=>new GraphNode($"n{i}","N"))],Edges=Enumerable.Range(0,60).Select(i=>new GraphEdge("a",$"n{i}")).ToArray()};
    Check(GraphEngine.Fit(wide,320).Width==4096,"a level of sixty is not capped at 4,096");
    // Thirty levels need 180 + 29 × 110 pixels, more than the 2,160 a graph may be.
    var chain=new GraphSpec{Nodes=Enumerable.Range(0,30).Select(i=>new GraphNode($"n{i}",$"N{i}")).ToArray(),Edges=Enumerable.Range(1,29).Select(i=>new GraphEdge($"n{i-1}",$"n{i}")).ToArray()};
    Check(GraphEngine.Fit(chain,360).Height==2160,"thirty rows are not capped at 2,160");
    Reject(()=>GraphEngine.Fit(Spanning() with{Edges=[new("a","b"),new("b","a")]},360));
});
Test("Fit: a circular graph keeps its circle and grows just tall enough for neighbours to stand 90.4 apart, and nodes side by side at one height take the narrowest width that holds their labels",()=>{
    double Wide(string label)=>label.Sum(c=>c is ' ' ? .3 : c is 'm' or 'M' or 'w' or 'W' ? .9 : .62)*12;
    double Apart(NodePosition p,NodePosition q)=>Math.Sqrt((p.X-q.X)*(p.X-q.X)+(p.Y-q.Y)*(p.Y-q.Y));
    var round=Pipeline(GraphLayout.Circular);
    // Since 0.31.0 the circle stands in from the sides by half its widest label and 24 pixels, 61.2 for Validation's 74.4, not a
    // fixed 100: on a 337-pixel phone it reaches 107.3 either side of the middle, which already stands neighbours 90.4 apart in the
    // graph's own 460 pixels. Drawn 300 high, it grows just tall enough.
    var fitted=GraphEngine.Fit(round,337);
    bool Apart90(GraphSpec spec){var p=GraphEngine.Layout(spec);return Enumerable.Range(0,p.Count).Where(i=>Math.Abs(p[i].Y-p[(i+1)%p.Count].Y)>1e-6).All(i=>Apart(p[i],p[(i+1)%p.Count])>=90.4-1e-9);}
    Check(fitted==round with{Width=337}&&Apart90(fitted),Shape(fitted));
    var low=GraphEngine.Fit(round with{Height=300},337);
    Check(low.Width==337&&low.Height>300&&low.Height<460&&low.Layout==GraphLayout.Circular&&low.Direction==round.Direction,Shape(low));
    Check(Apart90(low)&&!Apart90(low with{Height=low.Height-1}),"the circle is not just tall enough");
    // Transform and Charts stand side by side at the bottom: even at 322 their labels stand more than 16 apart, so the width stays.
    var narrow=GraphEngine.Fit(round,322);
    double Bottom(GraphSpec spec){var p=Placed(spec);return Math.Abs(p["transform"].X-p["charts"].X);}
    Check(narrow==round with{Width=322}&&Bottom(narrow)-(Wide("Transform")+Wide("Charts"))/2>=16&&Apart90(narrow),$"{Shape(narrow)}, {Bottom(narrow)} apart");
    // The crowded graph's pairs at one height need more than a phone, and take the narrowest width that stands their labels 16 apart.
    var crowd=crowded with{Layout=GraphLayout.Circular};
    var wide=GraphEngine.Fit(crowd,375);
    Check(wide.Width>375&&Level(wide).All(p=>p.Gap>=16-1e-9&&p.Apart>=70-1e-9)&&!Level(wide with{Width=wide.Width-1}).All(p=>p.Gap>=16-1e-9),Shape(wide));
    // Forty nodes cannot stand that far apart on a phone, so the circle stops at 2,160.
    Check(GraphEngine.Fit(new GraphSpec{Layout=GraphLayout.Circular,Nodes=Enumerable.Range(0,40).Select(i=>new GraphNode($"n{i}","N")).ToArray()},360).Height==2160,"forty nodes did not stop at 2,160");
});
Test("An edge keeps out of its own nodes' labels in every layout, meeting a node at the foot of its label where its run towards its next point would cross it, and on the home page's graph no edge runs through any label",()=>{
    foreach(var (spec,home) in Widths(Pipeline()).Select(s=>(s,true)).Concat(Widths(crowded).Append(crowded).Append(crowded with{Layout=GraphLayout.Circular}).Select(s=>(s,false))))
    {
        var drawn=Measure(spec);
        foreach(var (edge,line) in drawn.Edges)
        {
            var name=$"{spec.Title} {Shape(spec)}: {edge.Source} to {edge.Target}";
            foreach(var (id,node) in drawn.Nodes.Where(n=>home||n.Key==edge.Source||n.Key==edge.Target))
                Check(!node.Label.Crossed(line),$"{name} runs through {id}'s label");
            // Each end stands 25 pixels from its node's centre, clear of the circle, or at the foot of the node's label, 50 below it.
            foreach(var (id,end) in new[]{(edge.Source,line[0]),(edge.Target,line[^1])})
            {
                var node=drawn.Nodes[id];
                Check(Math.Abs(Math.Sqrt((end.X-node.X)*(end.X-node.X)+(end.Y-node.Y)*(end.Y-node.Y))-25)<1e-6||Math.Abs(end.X-node.X)<1e-6&&Math.Abs(end.Y-node.Y-50)<1e-6,$"{name} meets {id} at {end}");
            }
        }
    }
    // On the home page's circle Ingestion's edge to Validation, below it, leaves from the foot of its label, and Charts and Chart API
    // reach Reports from below at the foot of its label; on a phone Validation's edge down to Transform leaves from its foot too.
    // At 1,280 pixels the circle is wide enough that the edges into Reports pass beside its label.
    string[] Feet(GraphSpec spec){var drawn=Measure(spec);bool Foot(string id,(double X,double Y) end)=>Math.Abs(end.X-drawn.Nodes[id].X)<1e-6&&Math.Abs(end.Y-drawn.Nodes[id].Y-50)<1e-6;
        return [..drawn.Edges.Where(e=>Foot(e.Edge.Source,e.Line[0])).Select(e=>$"from {e.Edge.Source}"),..drawn.Edges.Where(e=>Foot(e.Edge.Target,e.Line[^1])).Select(e=>$"{e.Edge.Source} into {e.Edge.Target}")];}
    var round=Pipeline(GraphLayout.Circular);
    Check(Feet(round).SequenceEqual(["from ingest","charts into reports","api into reports"]),string.Join(", ",Feet(round)));
    Check(Feet(GraphEngine.Fit(round,375)).SequenceEqual(["from ingest","from validate","charts into reports","api into reports"]),string.Join(", ",Feet(GraphEngine.Fit(round,375))));
    Check(Feet(GraphEngine.Fit(round,1280)).SequenceEqual(["from ingest"]),string.Join(", ",Feet(GraphEngine.Fit(round,1280))));
    // Left to right the home page's graph needs no foot, and top to bottom every edge leaves from one, as it always has.
    Check(Feet(Pipeline()).Length==0&&Feet(GraphEngine.Fit(Pipeline(),375)).Length==8&&Feet(GraphEngine.Fit(Pipeline(),375)).All(f=>f.StartsWith("from ")),string.Join(", ",Feet(GraphEngine.Fit(Pipeline(),375))));
    // A dragged node keeps the rule: an edge's source pulled under its target reaches the target at the foot of its label, and leaves
    // the source aimed at that foot.
    var pair=new GraphSpec{Layout=GraphLayout.Circular,Nodes=[new("a","Alpha"),new("b","Beta")],Edges=[new("a","b")]};
    var b=Placed(pair)["b"];
    var dragged=Measure(pair,new Dictionary<string,GraphPoint>{["a"]=new(b.X+5,b.Y+200)});
    var (leaving,reaching)=(dragged.Edges[0].Line[0],dragged.Edges[0].Line[^1]);
    Check(Math.Abs(reaching.X-b.X)<1e-6&&Math.Abs(reaching.Y-b.Y-50)<1e-6,$"the edge reaches b at {reaching}");
    Check(Math.Abs(Math.Sqrt(Math.Pow(leaving.X-b.X-5,2)+Math.Pow(leaving.Y-b.Y-200,2))-25)<1e-6&&Math.Abs((leaving.X-b.X-5)*(reaching.Y-b.Y-200)-(leaving.Y-b.Y-200)*(reaching.X-b.X-5))<1e-6,$"the edge leaves a at {leaving}, not aimed at the foot");
});
Test("An edge's label stands at the first free place along its edge, 4 pixels clear of every node, every node's label, the labels drawn before it and the drawing's sides, and where no place is free where it always stood",()=>{
    foreach(var spec in Widths(Pipeline()).Concat(Widths(crowded)))
    {
        var drawn=Measure(spec);
        foreach(var (text,box) in drawn.Labels)
        {
            var (name,clear)=($"{spec.Title} {Shape(spec)}: '{text}'",box.Grown(4));
            Check(clear.Left>=0&&clear.Top>=0&&clear.Right<=spec.Width&&clear.Bottom<=spec.Height,$"{name} leaves the drawing");
            foreach(var (id,node) in drawn.Nodes) Check(!clear.Reaches(node.X,node.Y,23)&&!clear.Overlaps(node.Label),$"{name} lies on {id}");
            Check(drawn.Labels.Count(other=>other.Box.Overlaps(clear))==1,$"{name} lies on another edge's label");
        }
    }
    // The audit trail runs across the home page's circle just under the Sources label, which its place above the edge would lie on,
    // so it stands below the middle of its edge, as far below as it would have stood above.
    foreach(var width in new[]{1280,900,375})
    {
        var drawn=Measure(GraphEngine.Fit(Pipeline(GraphLayout.Circular),width));
        var (trail,line)=(drawn.Labels.Single(l=>l.Text=="audit trail").Box,drawn.Edges.Single(e=>e.Edge.Label=="audit trail").Line);
        Check(Math.Abs(line[0].Y-line[^1].Y)<1e-6&&Math.Abs((trail.Left+trail.Right)/2-(line[0].X+line[^1].X)/2)<1e-6&&Math.Abs(trail.Top-line[0].Y-6.5)<1e-6,$"at {width} the audit trail stands at {trail}");
    }
    // Three labelled edges between the same two nodes, placed in edge order: the first above the middle, the second below it, and the
    // third above the point four tenths of the way along, clear of the first.
    var three=Measure(new GraphSpec{Nodes=[new("a","A"),new("b","B")],Edges=[new("a","b","first"),new("a","b","second"),new("a","b","third")]});
    var (from,to)=(three.Edges[0].Line[0],three.Edges[0].Line[^1]);
    (double X,double Y) Middle(Bounds box)=>((box.Left+box.Right)/2,box.Bottom-2.5);
    bool Near((double X,double Y) p,double x,double y)=>Math.Abs(p.X-x)<1e-6&&Math.Abs(p.Y-y)<1e-6;
    Check(Near(Middle(three.Labels[0].Box),(from.X+to.X)/2,from.Y-9)&&Near(Middle(three.Labels[1].Box),(from.X+to.X)/2,from.Y+16)&&Near(Middle(three.Labels[2].Box),from.X+(to.X-from.X)*.4,from.Y-9),
        string.Join(" | ",three.Labels.Select(l=>$"{l.Text} {Middle(l.Box)}")));
    // Squeezed into its own 900 by 460 pixels, the crowded graph has labels with no free place, left to right and in a circle: each
    // stands where it always has, above the middle of its edge's middle stretch.
    foreach(var spec in new[]{crowded,crowded with{Layout=GraphLayout.Circular}})
    {
        var (drawn,routes)=(Measure(spec),GraphEngine.Routes(spec));
        var labelled=spec.Edges.Select((e,i)=>(e,i)).Where(x=>x.e.Label is not null&&x.e.Source!=x.e.Target).Select(x=>x.i).ToArray();
        var stuck=0;
        for(var k=0;k<drawn.Labels.Length;k++)
        {
            var (text,box)=drawn.Labels[k];var clear=box.Grown(4);
            if(clear.Left>=0&&clear.Top>=0&&clear.Right<=spec.Width&&clear.Bottom<=spec.Height&&!drawn.Nodes.Values.Any(n=>clear.Reaches(n.X,n.Y,23)||clear.Overlaps(n.Label))&&!drawn.Labels.Take(k).Any(l=>l.Box.Overlaps(clear)))continue;
            stuck++;
            var line=drawn.Edges[Array.IndexOf(drawn.Edges.Select(e=>e.Edge).ToArray(),spec.Edges[labelled[k]])].Line;
            var points=routes[labelled[k]].Points.ToArray();points[0]=new(line[0].X,line[0].Y);points[^1]=new(line[^1].X,line[^1].Y);
            var (middle,previous)=(points[points.Length/2],points[points.Length/2-1]);
            Check(Math.Abs((box.Left+box.Right)/2-(middle.X+previous.X)/2)<1e-6&&Math.Abs(box.Bottom-2.5-((middle.Y+previous.Y)/2-9))<1e-6,$"{spec.Layout}: '{text}' has no free place but stands at {box}");
        }
        Check(stuck>0,$"{spec.Layout}: every label found a free place");
    }
});
Test("A circle stands in from either side by half its widest label, or a node's radius if that is more, and 24 pixels, and keeps its height",()=>{
    var letters=new GraphSpec{Title="Letters",Layout=GraphLayout.Circular,Nodes=[..Enumerable.Range(0,5).Select(i=>new GraphNode($"n{i}",((char)('A'+i)).ToString()))]};
    foreach(var spec in new[]{Pipeline(GraphLayout.Circular),crowded with{Layout=GraphLayout.Circular},letters}.SelectMany(g=>new[]{1280,375,337,320}.Select(w=>GraphEngine.Fit(g,w)).Prepend(g)))
    {
        var margin=Math.Max(23,spec.Nodes.Max(n=>Estimate(Cut(n.Label),12))/2)+24;
        var placed=GraphEngine.Layout(spec);
        for(var i=0;i<placed.Count;i++)
        {
            var angle=2*Math.PI*i/placed.Count-Math.PI/2;
            Check(Math.Abs(placed[i].X-(spec.Width/2d+(spec.Width/2d-margin)*Math.Cos(angle)))<1e-9&&Math.Abs(placed[i].Y-((spec.Height+45)/2d+(spec.Height/2d-100)*Math.Sin(angle)))<1e-9,$"{spec.Title} {Shape(spec)}: {placed[i]}");
            // So every label keeps at least 24 pixels inside the drawing's sides.
            var half=Estimate(Cut(spec.Nodes[i].Label),12)/2;
            Check(placed[i].X-half>=24-1e-9&&placed[i].X+half<=spec.Width-24+1e-9,$"{spec.Title} {Shape(spec)}: {spec.Nodes[i].Label} comes within 24 pixels of a side");
        }
    }
    // The home page's widest label, Validation's 74.4 pixels, stands its circle 61.2 in: 388.8 either side of the middle at 900,
    // where a fixed 100 left 350.
    var home=Placed(Pipeline(GraphLayout.Circular));
    Check(Math.Abs(home["validate"].X-450-388.8*Math.Cos(2*Math.PI*2/7-Math.PI/2))<1e-9,$"{home["validate"]}");
});
Test("Fit: nodes at one height stand their labels at least 16 pixels apart and their circles 24, in every layout and direction",()=>{
    foreach(var spec in Widths(Pipeline()).Concat(Widths(crowded)).Concat(new[]{337,322,320}.SelectMany(w=>new[]{GraphEngine.Fit(Pipeline(GraphLayout.Circular),w),GraphEngine.Fit(crowded with{Layout=GraphLayout.Circular},w)})))
        foreach(var (pair,apart,gap) in Level(spec))
            Check(gap>=16-1e-9&&apart>=70-1e-9,$"{spec.Title} {Shape(spec)}: {pair} stand {apart:0.##} apart, their labels {gap:0.##}");
    // On a 337-pixel phone the home page's bottom pair, Transform and Charts, stand 93.1 apart and their labels 35.6, where they
    // stood 59.4 and 1.9 before 0.31.0.
    var bottom=Level(GraphEngine.Fit(Pipeline(GraphLayout.Circular),337)).Single(p=>p.Pair=="Transform and Charts");
    Check(Math.Abs(bottom.Apart-93.1)<.05&&Math.Abs(bottom.Gap-35.6)<.05,$"{bottom}");
});
string OperateGraph(GraphSpec spec,Func<LumenGraph,Task> act,bool fit=false)
{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        LumenGraph? graph=null;
        RenderFragment content=b=>{b.OpenComponent<LumenGraph>(0);b.AddAttribute(1,"Spec",spec);if(fit)b.AddAttribute(2,"FitWidth",true);b.AddComponentReferenceCapture(3,c=>graph=(LumenGraph)c);b.CloseComponent();};
        return renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<CascadingValue<ChartStyle>>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Value",null},{"ChildContent",content}}));
            await act(graph!);
            await root.QuiescenceTask;
            return root.ToHtmlString();
        }).GetAwaiter().GetResult();
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
}
Test("LumenGraph without FitWidth renders as before, and with it is marked for the stylesheet and drawn at its spec's width until measured",()=>{
    RenderFragment Element(GraphSpec spec,bool? fit)=>b=>{b.OpenComponent<LumenGraph>(0);b.AddAttribute(1,"Spec",spec);if(fit is {} f)b.AddAttribute(2,"FitWidth",f);b.CloseComponent();};
    var plain=Prerender(Element(Pipeline(),null));
    Check(plain.Contains("<div class=\"lumen-chart\">")&&!plain.Contains("lumen-fit")&&plain.Contains(GraphEngine.Render(Pipeline())),"the graph without FitWidth is not drawn as before");
    Check(Prerender(Element(Pipeline(),false))==plain,"FitWidth=\"false\" renders differently from leaving it out");
    var fitted=Prerender(Element(Pipeline(),true));
    Check(fitted.Contains("class=\"lumen-chart lumen-fit\"")&&fitted.Contains("viewBox='0 0 900 460'")&&fitted.Replace(" lumen-fit","")==plain,"the prerendered fitted graph differs by more than its class");
    Check(File.ReadAllText(Path.ChangeExtension(typeof(GraphEngine).Assembly.Location,".xml")).Contains("M:Lumen.Charts.GraphEngine.Fit(Lumen.Charts.GraphSpec,System.Int32)"),"Fit is undocumented");
});
Test("A fitted graph redraws at the width its box reports, holds dragged nodes in proportion, drops them with a word when it turns, and clamps a move to the drawing",()=>{
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    string Drawn(LumenGraph graph)=>(string)typeof(LumenGraph).GetField("svg",flags)!.GetValue(graph)!;
    string Status(LumenGraph graph)=>(string)typeof(LumenGraph).GetField("status",flags)!.GetValue(graph)!;
    string Box(string svg)=>XDocument.Parse(svg).Root!.Attribute("viewBox")!.Value;
    string At(string svg,string id)=>XDocument.Parse(svg).Descendants(ns+"g").Single(g=>(string?)g.Attribute("data-node")==id).Attribute("data-position")!.Value;
    var log=new List<string>();
    var html=OperateGraph(Pipeline(),async graph=>{
        log.Add(Box(Drawn(graph)));
        await graph.Fit(1200);log.Add(Box(Drawn(graph)));
        await graph.MoveNode("validate",300,200);log.Add(At(Drawn(graph),"validate"));
        // Wider, the node keeps its place in proportion: a quarter of the width across and the same height down.
        await graph.Fit(1600);log.Add(Box(Drawn(graph))+" "+At(Drawn(graph),"validate")+" "+Status(graph));
        // On a phone the graph turns, and the node goes back to the layout, which the status line says.
        await graph.Fit(360);log.Add(Box(Drawn(graph))+" "+Status(graph)+" "+(At(Drawn(graph),"validate")==At(GraphEngine.Render(GraphEngine.Fit(Pipeline(),360)),"validate")));
        // A move is kept inside the drawing as it is drawn, 360 by 730, not the spec's 900 by 460.
        await graph.MoveNode("validate",5000,5000);log.Add(At(Drawn(graph),"validate"));
        await graph.Fit(1200);log.Add(Status(graph)+" "+(At(Drawn(graph),"validate")==At(GraphEngine.Render(Pipeline() with{Width=1200}),"validate")));
        await graph.MoveNode("reports",900,300);await graph.Fit(980);log.Add(At(Drawn(graph),"reports"));
        typeof(LumenGraph).GetMethod("ResetLayout",flags)!.Invoke(graph,[]);log.Add(Status(graph)+" "+(Drawn(graph)==GraphEngine.Render(Pipeline() with{Width=980})));
        await graph.SelectNode("api");log.Add(Status(graph));
    },fit:true);
    Check(log.SequenceEqual(["0 0 900 460","0 0 1200 460","300,200","0 0 1600 460 400,200 Moved Validation","0 0 360 730 Turned top to bottom to fit; moved nodes returned True","320,678",
        "Turned left to right to fit; moved nodes returned True","735,300","Layout reset True","Selected Chart API"]),string.Join(" | ",log));
    Check(html.Contains("class=\"lumen-chart lumen-fit\"")&&html.Contains("viewBox='0 0 980 460'"),"the page does not show the fitted graph");
    // A graph not asked to fit ignores a width, and keeps a move inside its spec's own drawing, as before.
    var ignored=new List<string>();
    OperateGraph(Pipeline(),async graph=>{await graph.Fit(375);ignored.Add(Box(Drawn(graph)));await graph.MoveNode("validate",5000,5000);ignored.Add(At(Drawn(graph),"validate"));});
    Check(ignored.SequenceEqual(["0 0 900 460","860,408"]),string.Join(" | ",ignored));
});
// 0.32.0: trend families. A plot runs from x 76 to 870 and y 78 to 344, so on X fixed from 0 to 12 a value x sits at TrendX(x), and
// with Y fixed from 0 to 120 a value y at TrendY(y).
double TrendX(double x)=>76+x/12*794;
double TrendY(double y)=>344-y/120*266;
ChartSpec OnFixedAxes(params ChartSeries[] series)=>new(){Title="Trends",Kind=ChartKind.Scatter,XMin=0,XMax=12,YMin=0,YMax=120,Series=series};
XElement TrendOf(ChartSpec spec)=>Svg(spec).Descendants(ns+"path").Single(p=>(string?)p.Attribute("class")=="lumen-trend");
// A trend's path as its pieces, each begun by a move and continued by lines.
List<List<(double X,double Y)>> Subpaths(XElement path)
{
    var pieces=new List<List<(double X,double Y)>>();
    foreach(var part in path.Attribute("d")!.Value.Split(' '))
    {
        var xy=part[1..].Split(',').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray();
        if(part[0]=='M')pieces.Add([]);
        pieces[^1].Add((xy[0],xy[1]));
    }
    return pieces;
}
Test("Statistics.Polynomial recovers a quadratic, a cubic and a quartic exactly, from the constant term up, a straight line as Fit does, and leaves noise no lean on any power of X",()=>{
    double[][] shapes=[[3,-2,.5],[1,1,-.3,.02],[-4,.5,.25,-.03,.001]];
    foreach(var c in shapes)
    {
        var points=Enumerable.Range(-5,21).Select(i=>(X:(double)i,Y:c.Select((k,j)=>k*Math.Pow(i,j)).Sum())).ToArray();
        var fit=Statistics.Polynomial(points,c.Length-1)!;
        Check(fit.Coefficients.Count==c.Length&&fit.Coefficients.Zip(c).All(p=>Math.Abs(p.First-p.Second)<1e-9),$"degree {c.Length-1}: {string.Join(", ",fit.Coefficients)}");
        Check(fit.R2>1-1e-12&&fit.Count==21&&points.All(p=>Math.Abs(fit.Predict(p.X)-p.Y)<1e-9),$"degree {c.Length-1} misses its points");
    }
    var line=Enumerable.Range(0,10).Select(i=>(X:(double)i,Y:3+i*1.5+(i%3==0?2:0))).ToArray();
    var (one,straight)=(Statistics.Polynomial(line,1)!,Statistics.Fit(line)!);
    Check(Math.Abs(one.Coefficients[0]-straight.Intercept)<1e-9&&Math.Abs(one.Coefficients[1]-straight.Slope)<1e-9&&Math.Abs(one.R2-straight.R2)<1e-9,"a first-degree fit is not the line");
    // Least squares leaves residuals that lean on no power of X it fitted, and R squared is the share of the variance explained.
    var noisy=Enumerable.Range(0,30).Select(i=>(X:i*.7,Y:5+2*i-.1*i*i+Math.Sin(i*1.3)*3)).ToArray();
    var quadratic=Statistics.Polynomial(noisy,2)!;
    for(var j=0;j<3;j++) Check(Math.Abs(noisy.Sum(p=>(p.Y-quadratic.Predict(p.X))*Math.Pow(p.X,j)))<1e-6,$"the residuals lean on x^{j}");
    var mean=noisy.Average(p=>p.Y);
    Check(quadratic.R2<1&&Math.Abs(quadratic.R2-(1-noisy.Sum(p=>Math.Pow(p.Y-quadratic.Predict(p.X),2))/noisy.Sum(p=>Math.Pow(p.Y-mean,2))))<1e-12,$"R squared {quadratic.R2}");
});
Test("Statistics.Polynomial keeps its precision with X in Unix milliseconds, in its coefficients and in what it predicts",()=>{
    var day=86400000d;var start=Utc(2026,6,8);
    var points=Enumerable.Range(0,60).Select(d=>(X:start+d*day,Y:60-.5*d+.02*d*d)).ToArray();
    var fit=Statistics.Polynomial(points,2)!;
    // The same curve in the caller's X: y = 60 − 0.5 (x − s) / D + 0.02 ((x − s) / D)².
    double[] expected=[60+.5*start/day+.02*start*start/(day*day),-.5/day-.04*start/(day*day),.02/(day*day)];
    Check(fit.Coefficients.Zip(expected).All(p=>Math.Abs(p.First-p.Second)<=1e-6*Math.Abs(p.Second)),string.Join(", ",fit.Coefficients));
    Check(points.All(p=>Math.Abs(fit.Predict(p.X)-p.Y)<1e-9)&&fit.R2>1-1e-9,"the fit lost its precision");
    // A quartic over a week of hours, where the coefficients in Unix milliseconds alone would cancel to noise.
    var week=Enumerable.Range(0,7*24).Select(h=>(X:start+h*3600000d,Y:50+Math.Pow(h/24d-3.5,4)-3*Math.Pow(h/24d-3.5,2))).ToArray();
    var quartic=Statistics.Polynomial(week,4)!;
    Check(week.All(p=>Math.Abs(quartic.Predict(p.X)-p.Y)<1e-7)&&quartic.R2>1-1e-9,"a quartic over a week lost its precision");
    // A fit built from coefficients alone, or given others, predicts from them.
    Check(Math.Abs(new PolynomialFit([1,2,3],1,3).Predict(2)-17)<1e-12&&Math.Abs((fit with{Coefficients=[4,0,1]}).Predict(3)-13)<1e-12);
});
Test("Statistics.Polynomial and Statistics.Exponential return null where no fit exists, and a polynomial's degree runs from 1 to 4",()=>{
    Check(Statistics.Polynomial([(1,2),(2,3)],2) is null&&Statistics.Polynomial([(1,2),(1,3),(2,5),(2,1)],2) is null,"a quadratic through two X values");
    Check(Statistics.Polynomial([(1,2),(2,3),(3,1)],2) is {Count:3}&&Statistics.Polynomial([(1,2),(2,3),(3,1),(4,4)],4) is null,"three X values take a quadratic and four no quartic");
    Check(Statistics.Polynomial([(1,4),(2,4),(3,4),(5,4)],3) is {R2:1} flat&&Math.Abs(flat.Coefficients[0]-4)<1e-12&&flat.Coefficients.Skip(1).All(c=>Math.Abs(c)<1e-12),"a flat set is not a flat curve");
    Check(Statistics.Polynomial([],2) is null&&Statistics.Exponential([]) is null);
    foreach(var degree in new[]{0,5,-1}) Reject(()=>Statistics.Polynomial([(1,2),(2,3),(3,4),(4,5),(5,6),(6,7)],degree));
    Check(Statistics.Exponential([(0,-1),(1,0),(2,5)]) is null,"one positive value took a fit");
    Check(Statistics.Exponential([(2,1),(2,5),(2,9)]) is null,"one X took a fit");
});
Test("Statistics.Exponential recovers A, B and an R squared measured on the logarithms, from the positive values only",()=>{
    var exact=Enumerable.Range(0,11).Select(i=>(X:(double)i,Y:3*Math.Exp(.25*i))).ToArray();
    var fit=Statistics.Exponential(exact.Concat([(4.5,0),(5.5,-7)]))!;
    Check(Math.Abs(fit.A-3)<1e-9&&Math.Abs(fit.B-.25)<1e-12&&fit.R2>1-1e-12&&fit.Count==11,$"{fit}");
    Check(exact.All(p=>Math.Abs(fit.Predict(p.X)-p.Y)<1e-9*p.Y));
    // Four readings worked by hand on their logarithms, as Excel's exponential trendline reports them: y = 1.0445 e^(0.6213 x),
    // R squared 0.9337, which is not the R squared of the curve on Y itself.
    (double X,double Y)[] readings=[(1,2),(2,3),(3,9),(4,11)];
    var logs=readings.Select(p=>Math.Log(p.Y)).ToArray();var meanLog=logs.Average();
    double sxy=readings.Select((p,i)=>(p.X-2.5)*(logs[i]-meanLog)).Sum(),syy=logs.Sum(l=>(l-meanLog)*(l-meanLog));
    var worked=Statistics.Exponential(readings)!;
    Check(Math.Abs(worked.B-sxy/5)<1e-12&&Math.Abs(worked.A-Math.Exp(meanLog-sxy/5*2.5))<1e-12&&Math.Abs(worked.R2-sxy*sxy/(5*syy))<1e-12,$"{worked}");
    Check(Math.Abs(worked.B-.6213)<1e-4&&Math.Abs(worked.A-1.0445)<1e-4&&Math.Abs(worked.R2-.9337)<1e-4,$"{worked}");
    var mean=readings.Average(p=>p.Y);
    Check(Math.Abs(worked.R2-(1-readings.Sum(p=>Math.Pow(p.Y-worked.Predict(p.X),2))/readings.Sum(p=>Math.Pow(p.Y-mean,2))))>.01,"R squared was measured on Y");
    // Unix milliseconds put A below the smallest double, and the fit still predicts.
    var day=86400000d;var start=Utc(2026,6,8);
    var growth=Statistics.Exponential(Enumerable.Range(0,30).Select(d=>(start+d*day,50*Math.Exp(.05*d))))!;
    Check(growth.A==0&&Math.Abs(growth.B*day-.05)<1e-9&&Enumerable.Range(0,30).All(d=>Math.Abs(growth.Predict(start+d*day)/(50*Math.Exp(.05*d))-1)<1e-9),$"{growth}");
    // One built by hand, or given another A, predicts from its A and B.
    Check(Math.Abs(new ExponentialFit(2,.5,1,3).Predict(2)-2*Math.E)<1e-12&&Math.Abs((fit with{A=6}).Predict(1)-6*Math.Exp(.25))<1e-9);
});
Test("A moving average averages a trailing window at its last point, holds a missing value's place without counting it, and breaks where less than half a window is present",()=>{
    double?[] values=[10,20,null,40,null,null,null,80,90,100,110,120];
    var spec=OnFixedAxes(new ChartSeries("S",values.Select((v,i)=>new ChartPoint(i,v)).ToArray()){Trend=true,TrendFit=TrendFit.MovingAverage,TrendPoints=4}) with{Kind=ChartKind.Line};
    // The axes are where the constants say: the mark at x 9 is drawn at TrendX(9), TrendY(100).
    Check(Svg(spec).Descendants(ns+"g").Where(g=>(string?)g.Attribute("data-point")=="9").Select(g=>g.Element(ns+"circle")!).Any(c=>Close((double)c.Attribute("cx")!,TrendX(9))&&Close((double)c.Attribute("cy")!,TrendY(100))));
    var pieces=Subpaths(TrendOf(spec));
    (double X,double Y)[][] expected=[[(1,15),(2,15),(3,70/3d),(4,30)],[(8,85),(9,90),(10,95),(11,105)]];
    Check(pieces.Count==2&&pieces.Zip(expected).All(p=>p.First.Count==p.Second.Length&&p.First.Zip(p.Second).All(q=>Close(q.First.X,TrendX(q.Second.X))&&Close(q.First.Y,TrendY(q.Second.Y)))),
        string.Join(" | ",pieces.Select(p=>string.Join(" ",p))));
    // A window of three needs two values: a lone window between gaps has nothing to join to and draws nothing.
    ChartSpec Sparse(params double?[] ys)=>OnFixedAxes(new ChartSeries("S",ys.Select((v,i)=>new ChartPoint(i,v)).ToArray()){Trend=true,TrendFit=TrendFit.MovingAverage,TrendPoints=3}) with{Kind=ChartKind.Line};
    Check(!ChartSvg.Render(Sparse(10,null,30,null,null)).Contains("lumen-trend"),"a lone window was drawn");
    var tail=Subpaths(TrendOf(Sparse(10,null,30,null,null,60,70,80)));
    Check(tail.Count==1&&tail[0].Count==2&&Close(tail[0][0].X,TrendX(6))&&Close(tail[0][0].Y,TrendY(65))&&Close(tail[0][1].Y,TrendY(70)),string.Join(" ",tail[0]));
    // Seven points unless set; fewer present than half of seven draw nothing.
    var week=OnFixedAxes(new ChartSeries("S",Enumerable.Range(0,10).Select(i=>new ChartPoint(i,i*10)).ToArray()){Trend=true,TrendFit=TrendFit.MovingAverage});
    var weekly=Subpaths(TrendOf(week))[0];
    Check(weekly.Count==7&&Close(weekly[0].X,TrendX(3))&&Close(weekly[0].Y,TrendY(15))&&Close(weekly[^1].Y,TrendY(60)),string.Join(" ",weekly));
    Check(!ChartSvg.Render(OnFixedAxes(new ChartSeries("S",[new(0,1),new(1,2),new(2,3)]){Trend=true,TrendFit=TrendFit.MovingAverage})).Contains("lumen-trend"));
    // On a logarithmic axis it averages drawn positions, so a window of 1 and 100 stands at 10, their geometric mean.
    var logged=new ChartSpec{Kind=ChartKind.Line,YAxis=AxisKind.Log,XMin=0,XMax=12,YMin=1,YMax=1000,Series=[new("G",Enumerable.Range(0,13).Select(i=>new ChartPoint(i,i%2==0?1:100)).ToArray()){Trend=true,TrendFit=TrendFit.MovingAverage,TrendPoints=2}]};
    var geometric=Subpaths(TrendOf(logged)).Single();
    Check(geometric.Count==13&&Close(geometric[0].Y,344)&&geometric.Skip(1).All(v=>Close(v.Y,344-266/3d)),string.Join(" ",geometric));
    // A long run is thinned as a line is, to the chart's budget.
    var many=new ChartSpec{Kind=ChartKind.Line,MaxRenderedPoints=100,Series=[new("Long",Enumerable.Range(0,5000).Select(i=>new ChartPoint(i,Math.Sin(i/50d)*10+i%7)).ToArray()){Trend=true,TrendFit=TrendFit.MovingAverage,TrendPoints=20}]};
    var thinned=Subpaths(TrendOf(many));
    Check(thinned.Count==1&&thinned[0].Count is >= 90 and <= 100,$"{thinned[0].Count} vertices");
});
Test("A polynomial, an exponential and a moving average are drawn only across the X their observations cover, a line still across the plot",()=>{
    ChartPoint[] data=Enumerable.Range(2,8).Select(i=>new ChartPoint(i,10+i*i)).ToArray();
    foreach(var fit in Enum.GetValues<TrendFit>())
    {
        var xs=Subpaths(TrendOf(OnFixedAxes(new ChartSeries("S",data){Trend=true,TrendFit=fit}))).SelectMany(p=>p).Select(v=>v.X).ToArray();
        if(fit==TrendFit.Linear){Check(xs.Length==2&&Close(xs[0],76)&&Close(xs[1],870),"a line no longer spans the plot");continue;}
        // Positions are written to eight decimals.
        Check(xs.Min()>=TrendX(2)-1e-6&&xs.Max()<=TrendX(9)+1e-6,$"{fit} runs past the data");
        if(fit==TrendFit.MovingAverage) Check(Close(xs.Min(),TrendX(5))&&Close(xs.Max(),TrendX(9)),"the moving average does not run from its first half-full window to the last point");
        else Check(Close(xs[0],TrendX(2))&&Close(xs[^1],TrendX(9))&&xs.Zip(xs.Skip(1)).All(p=>p.Second>p.First&&p.Second-p.First<=2+1e-6),$"{fit} does not run the data's range every 2 pixels or less");
    }
    // An exponential leaves out the values that have no logarithm, and is drawn across the positive ones only.
    var lifted=Subpaths(TrendOf(OnFixedAxes(new ChartSeries("S",[new(0,0),new(1,-3),..data]){Trend=true,TrendFit=TrendFit.Exponential}))).Single();
    Check(Close(lifted[0].X,TrendX(2))&&Close(lifted[^1].X,TrendX(9)),"the exponential runs past its positive values");
    // Zoomed into the middle, a curve is sampled across the plot it shows and the clip's 12-pixel bleed, not the stretch it hides.
    var zoomed=Subpaths(TrendOf(OnFixedAxes(new ChartSeries("S",data){Trend=true,TrendFit=TrendFit.Polynomial}) with{XMin=5,XMax=5.5}))[0];
    Check(zoomed.Count==410&&Close(zoomed[0].X,64)&&Close(zoomed[^1].X,882),$"{zoomed.Count} vertices from {zoomed[0].X} to {zoomed[^1].X}");
});
Test("An exponential is straight on a logarithmic Y axis, through its exact data, and curves on a linear one",()=>{
    var growth=Enumerable.Range(0,13).Select(i=>new ChartPoint(i,2*Math.Exp(.4*i))).ToArray();
    var series=new ChartSeries("Growth",growth){Trend=true,TrendFit=TrendFit.Exponential};
    var line=Subpaths(TrendOf(OnFixedAxes(series) with{YAxis=AxisKind.Log,YMin=1,YMax=1000})).Single();
    var slope=(line[^1].Y-line[0].Y)/(line[^1].X-line[0].X);
    Check(line.Count==398&&line.All(v=>Math.Abs(line[0].Y+slope*(v.X-line[0].X)-v.Y)<1e-6),"the exponential bends on a log axis");
    Check(growth.All(p=>Math.Abs(line[0].Y+slope*(TrendX(p.X)-line[0].X)-(344-Math.Log10(p.Y!.Value)/3*266))<1e-6),"it misses its own points on a log axis");
    // On a linear axis every vertex is the curve through the data, and the middle sags below the chord between the ends.
    var curve=Subpaths(TrendOf(OnFixedAxes(series) with{YMax=300})).Single();
    Check(curve.All(v=>Math.Abs(v.Y-(344-2*Math.Exp(.4*(v.X-76)/794*12)/300*266))<1e-6),"the curve is not the data's on a linear axis");
    var middle=curve[curve.Count/2];var chord=curve[0].Y+(curve[^1].Y-curve[0].Y)*(middle.X-curve[0].X)/(curve[^1].X-curve[0].X);
    Check(middle.Y-chord>20,"the exponential is straight on a linear axis");
    // A polynomial on a log axis is fitted to the drawn positions, so powers of ten in a line are a straight line there too.
    var decades=Subpaths(TrendOf(OnFixedAxes(new ChartSeries("Decades",Enumerable.Range(1,9).Select(i=>new ChartPoint(i,Math.Pow(10,i/3d))).ToArray()){Trend=true,TrendFit=TrendFit.Polynomial}) with{YAxis=AxisKind.Log,YMin=1,YMax=1000})).Single();
    var rise=(decades[^1].Y-decades[0].Y)/(decades[^1].X-decades[0].X);
    Check(decades.All(v=>Math.Abs(decades[0].Y+rise*(v.X-decades[0].X)-v.Y)<1e-6),"a quadratic through a straight drawn line bends");
});
Test("A polynomial on a trading axis is fitted in the space it draws, so a quadratic in trading days runs through every point",()=>{
    var days=TradingDays(28);
    var spec=new ChartSpec{Kind=ChartKind.Scatter,XAxis=AxisKind.Time,SkipWeekends=true,YMin=0,YMax=200,Series=[new("Close",days.Select((v,i)=>new ChartPoint(v,100+3*i-.2*i*i)).ToArray()){Trend=true,TrendFit=TrendFit.Polynomial}]};
    var doc=Svg(spec);
    var path=doc.Descendants(ns+"path").Single(p=>(string?)p.Attribute("class")=="lumen-trend");
    Check(days.Length==20&&path.Attribute("aria-label")!.Value=="Close trend: quadratic fit, R squared 1.00",path.Attribute("aria-label")!.Value);
    var curve=Subpaths(path).Single();
    var marks=doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("class")=="lumen-datum").Select(g=>g.Element(ns+"circle")!).Select(c=>(X:(double)c.Attribute("cx")!,Y:(double)c.Attribute("cy")!)).ToArray();
    Check(marks.Length==20&&Close(curve[0].X,marks[0].X)&&Close(curve[^1].X,marks[^1].X),"the curve does not run from the first trading day to the last");
    // Each mark lies on the curve, read between the two samples either side of it.
    foreach(var m in marks)
    {
        var k=Math.Max(1,curve.FindIndex(v=>v.X>=m.X-1e-9));var (a,b)=(curve[k-1],curve[k]);
        Check(Math.Abs(a.Y+(b.Y-a.Y)*(m.X-a.X)/(b.X-a.X)-m.Y)<.01,$"the mark at {m.X} is off the curve");
    }
});
Test("Each trend is named for its fit, keeps the line's look and carries a native title only where titles are drawn",()=>{
    ChartPoint[] rising=Enumerable.Range(1,10).Select(i=>new ChartPoint(i,5+i*i*.5)).ToArray();
    string Label(ChartSeries s)=>TrendOf(Spec(ChartKind.Scatter) with{Series=[s]}).Attribute("aria-label")!.Value;
    var load=new ChartSeries("Load",rising){Trend=true};
    Check(Label(load).StartsWith("Load trend: rising, R squared 0.9"),Label(load));
    Check(Label(load with{TrendFit=TrendFit.MovingAverage})=="Load trend: 7-point moving average"&&Label(load with{TrendFit=TrendFit.MovingAverage,TrendPoints=3})=="Load trend: 3-point moving average");
    Check(Label(load with{TrendFit=TrendFit.Polynomial})=="Load trend: quadratic fit, R squared 1.00"&&Label(load with{TrendFit=TrendFit.Polynomial,TrendDegree=3})=="Load trend: cubic fit, R squared 1.00"
        &&Label(load with{TrendFit=TrendFit.Polynomial,TrendDegree=4})=="Load trend: quartic fit, R squared 1.00");
    Check(Label(load with{TrendFit=TrendFit.Exponential}) is var up&&up.StartsWith("Load trend: exponential fit, rising, R squared 0.9")&&up.Length=="Load trend: exponential fit, rising, R squared 0.95".Length,up);
    Check(Label(load with{TrendFit=TrendFit.Exponential,Points=rising.Select(p=>p with{Y=100/p.Y}).ToArray()}).StartsWith("Load trend: exponential fit, falling, R squared 0.9"));
    foreach(var fit in Enum.GetValues<TrendFit>())
    {
        var spec=Spec(ChartKind.Line) with{Series=[new("Load",rising,"#123456"){Trend=true,TrendFit=fit,StrokeWidth=3}]};
        var path=TrendOf(spec);
        Check((string?)path.Attribute("stroke-dasharray")=="7 5"&&(string?)path.Attribute("stroke-opacity")==".85"&&(string?)path.Attribute("fill")=="none"&&(string?)path.Attribute("role")=="img"
            &&(string?)path.Attribute("stroke")=="#123456"&&(string?)path.Attribute("stroke-width")=="2.25"&&(string?)path.Attribute("vector-effect")=="non-scaling-stroke",$"{fit} looks different: {path}");
        Check(path.Element(ns+"title")?.Value==path.Attribute("aria-label")!.Value,$"{fit} has no native title");
        Check(TrendOf(spec with{Series=[spec.Series[0] with{StrokeWidth=null}]}).Attribute("stroke-width")!.Value=="1.2",$"{fit} is not three quarters of the refined stroke");
        var bare=XDocument.Parse(ChartSvg.Render(spec,includeTitles:false)).Descendants(ns+"path").Single(p=>(string?)p.Attribute("class")=="lumen-trend");
        Check(bare.Element(ns+"title") is null&&bare.Attribute("aria-label")!.Value==path.Attribute("aria-label")!.Value,$"{fit} keeps a title without titles");
        var classic=Svg(Classic(spec)).Descendants(ns+"path").Single(p=>(string?)p.Attribute("class")=="lumen-trend");
        Check((string?)classic.Attribute("stroke-width")=="2"&&classic.Attribute("vector-effect") is null,$"{fit} in the classic finish");
    }
});
Test("A trend's fit, window and degree are refused without a trend, out of range and on the wrong fit, and every fit where a trend is",()=>{
    ChartSeries s=new("S",[new(0,1),new(1,2),new(2,4),new(3,3)]);
    string Refusal(ChartSpec spec){try{ChartSvg.Render(spec);}catch(ArgumentException error){return error.Message;}throw new Exception("a chart was accepted that should not be");}
    string Refused(ChartSeries series,ChartKind kind=ChartKind.Line)=>Refusal(Spec(kind) with{Series=[series]});
    foreach(var unasked in new[]{s with{TrendFit=TrendFit.MovingAverage},s with{TrendFit=TrendFit.Polynomial},s with{TrendFit=TrendFit.Exponential},s with{TrendPoints=5},s with{TrendDegree=3}})
        Check(Refused(unasked).Contains("need Trend = true"),Refused(unasked));
    var trended=s with{Trend=true};
    foreach(var points in new[]{1,0,-7,1001}) Check(Refused(trended with{TrendFit=TrendFit.MovingAverage,TrendPoints=points}).Contains("from 2 to 1000"),$"{points} points");
    foreach(var degree in new[]{1,5,0}) Check(Refused(trended with{TrendFit=TrendFit.Polynomial,TrendDegree=degree}).Contains("2, 3 or 4"),$"degree {degree}");
    Check(Refused(trended with{TrendPoints=5}).Contains("applies to TrendFit.MovingAverage")&&Refused(trended with{TrendFit=TrendFit.Polynomial,TrendPoints=5}).Contains("applies to TrendFit.MovingAverage"));
    Check(Refused(trended with{TrendDegree=3}).Contains("applies to TrendFit.Polynomial")&&Refused(trended with{TrendFit=TrendFit.Exponential,TrendDegree=3}).Contains("applies to TrendFit.Polynomial"));
    Check(Refused(trended with{TrendFit=(TrendFit)9})=="Unknown trend fit.");
    // The ends of each range are taken, and so are the defaults written out without a trend.
    foreach(var taken in new[]{trended with{TrendFit=TrendFit.MovingAverage,TrendPoints=2},trended with{TrendFit=TrendFit.MovingAverage,TrendPoints=1000},trended with{TrendFit=TrendFit.Polynomial,TrendDegree=4},s with{TrendPoints=7,TrendDegree=2,TrendFit=TrendFit.Linear}})
        ChartSvg.Render(Spec() with{Series=[taken]});
    // A moving average runs through points in the order they come, so dots out of X order are refused one; the other fits take them.
    var shuffled=new ChartSeries("Dots",[new(2,1),new(0,3),new(1,2),new(3,5)]){Trend=true};
    Check(Refused(shuffled with{TrendFit=TrendFit.MovingAverage},ChartKind.Scatter).Contains("ordered by X"));
    foreach(var fit in new[]{TrendFit.Linear,TrendFit.Polynomial,TrendFit.Exponential}) Check(ChartSvg.Render(Spec(ChartKind.Scatter) with{Series=[shuffled with{TrendFit=fit}]}).Contains("lumen-trend"),$"{fit} refused dots out of order");
    // Every fit is refused where the line is, each with the reason the line is given.
    foreach(var fit in Enum.GetValues<TrendFit>())
    {
        ChartSpec Trending(ChartKind kind)=>Sample(kind) with{Series=Sample(kind).Series.Select(x=>x with{Trend=true,TrendFit=fit}).ToArray()};
        foreach(var kind in (ChartKind[])[ChartKind.Column,ChartKind.Bar,ChartKind.StackedColumn,ChartKind.Band,ChartKind.Range,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Donut,ChartKind.Candlestick])
            Check(Refusal(Trending(kind)).Contains("trend line applies"),$"{fit} on {kind}: {Refusal(Trending(kind))}");
        Check(Refusal(Spec() with{Series=[new("Bars",[new(0,1),new(1,2)]){Kind=ChartKind.Column,Trend=true,TrendFit=fit}]}).Contains("trend line applies"),$"{fit} on a column series");
        Check(Refusal(Trending(ChartKind.Timeline)).Contains("timeline draws no trend")&&Refusal(Trending(ChartKind.Calendar)).Contains("calendar draws no trend")&&Refusal(Trending(ChartKind.Blocks)).Contains("block series draws no trend"),$"{fit} on timelines, calendars or blocks");
    }
});
Test("Trend = true alone draws as 0.31.0 drew it, byte for byte with its gradient IDs, and a fit away from the line names its gradients afresh",()=>{
    // Rows of 0.31.0's rendering baseline that draw trends, in both finishes, and a gradient beside a trend recorded from 0.31.0's
    // own code. Hashes were recorded on Windows; elsewhere the IDs below still hold the hash that names gradients.
    var line=Baseline(ChartKind.Line,ChartTheme.Light);var scatter=Baseline(ChartKind.Scatter,ChartTheme.Light);
    var faded=new ChartSpec{Kind=ChartKind.Area,Title="Fade and trend",Description="A gradient beside a trend",
        Series=[new("Climb",Enumerable.Range(0,24).Select(i=>new ChartPoint(i,20+i%5*3+i)).ToArray()){Fill=AreaFill.Fade},
            new("Pace",Enumerable.Range(0,24).Select(i=>new ChartPoint(i,40-i%4+i/2.0)).ToArray()){Kind=ChartKind.Line,Trend=true}]};
    var dark=faded with{Theme=ChartTheme.Dark,Series=[faded.Series[0],faded.Series[1] with{Gradient=[new(30,"#2E9B58"),new(50,"#DD4B45")]}]};
    var trend=scatter with{Series=[..scatter.Series.Select((s,i)=>s with{Trend=true,Points=s.Points.Select(p=>p with{Y=i==0?p.Y:60-p.Y}).ToArray()})]};
    var pace=line with{YFormat=ValueFormat.Duration,YReversed=true,Annotations=[new(AnnotationAxis.Y,300){Label="Target"}],Series=[new("Pace",Enumerable.Range(0,12).Select(i=>new ChartPoint(i,330-i*4+i%3*5)).ToArray()){Trend=true}]};
    var order=line with{Y2Label="Half",Series=[new("A",Twelve()){Trend=true},new("B",Twelve().Select(p=>p with{Y=50-p.Y}).ToArray()){Trend=true},new("C",Twelve().Select(p=>p with{Y=p.Y/2}).ToArray()){Secondary=true}]};
    (string Row,string Hash,Func<string> Draw)[] rows=[
        ("guard/trend","6F3C39331C0B02AB",()=>ChartSvg.Render(trend)),("guard/trend, classic","7B99FC62158BEB58",()=>ChartSvg.Render(Classic(trend))),
        ("pace-reversed","2A0753C00E774BFF",()=>ChartSvg.Render(pace)),("pace-reversed, classic","779AEF9057093620",()=>ChartSvg.Render(Classic(pace))),
        ("guard/line-order","1023B23438A3732B",()=>ChartSvg.Render(order)),("guard/line-order, classic","63C0222F283AC476",()=>ChartSvg.Render(Classic(order))),
        ("a fade beside a trend","015F07AE1A215C67",()=>ChartSvg.Render(faded)),("a fade beside a trend, classic","1E4C638A63DB4057",()=>ChartSvg.Render(Classic(faded))),
        ("a gradient on a trended line, dark, untitled","65A6F80CFA0FE47D",()=>ChartSvg.Render(dark,includeTitles:false))];
    if(OperatingSystem.IsWindows()) foreach(var (row,hash,draw) in rows) Check(Hash16(draw())==hash,$"{row} moved: {Hash16(draw())}");
    string Id(ChartSpec spec)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(spec),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var spelled=faded with{Series=[faded.Series[0],faded.Series[1] with{TrendFit=TrendFit.Linear,TrendPoints=7,TrendDegree=2}]};
    Check(Id(faded)=="lumen-0b45c8e09ea1"&&ChartSvg.Render(spelled)==ChartSvg.Render(faded),"the defaults written out moved the drawing");
    foreach(var other in new[]{faded.Series[1] with{TrendFit=TrendFit.Polynomial},faded.Series[1] with{TrendFit=TrendFit.MovingAverage,TrendPoints=5},faded.Series[1] with{TrendFit=TrendFit.Polynomial,TrendDegree=3},faded.Series[1] with{TrendFit=TrendFit.Exponential}})
        Check(Id(faded with{Series=[faded.Series[0],other]}) is {Length:18} id&&id!=Id(faded),$"a {other.TrendFit} kept the line's IDs");
});
Test("A trend's fit, window and degree round-trip through JSON, the fit as a string, and a request that names none draws the line",()=>{
    var json="{\"kind\":\"Scatter\",\"series\":["+
        "{\"name\":\"Rate\",\"trend\":true,\"trendFit\":\"MovingAverage\",\"trendPoints\":3,\"points\":[{\"x\":0,\"y\":1},{\"x\":1,\"y\":3},{\"x\":2,\"y\":2},{\"x\":3,\"y\":5},{\"x\":4,\"y\":4}]},"+
        "{\"name\":\"Curve\",\"trend\":true,\"trendFit\":\"Polynomial\",\"trendDegree\":3,\"points\":[{\"x\":0,\"y\":1},{\"x\":1,\"y\":2},{\"x\":2,\"y\":9},{\"x\":3,\"y\":28},{\"x\":4,\"y\":65}]},"+
        "{\"name\":\"Growth\",\"trend\":true,\"trendFit\":\"Exponential\",\"points\":[{\"x\":0,\"y\":1},{\"x\":1,\"y\":2},{\"x\":2,\"y\":4},{\"x\":3,\"y\":8}]},"+
        "{\"name\":\"Line\",\"trend\":true,\"points\":[{\"x\":0,\"y\":4},{\"x\":4,\"y\":1}]}]}";
    var spec=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(spec.Series[0].TrendFit==TrendFit.MovingAverage&&spec.Series[0].TrendPoints==3&&spec.Series[0].TrendDegree==2&&spec.Series[1].TrendFit==TrendFit.Polynomial&&spec.Series[1].TrendDegree==3&&spec.Series[1].TrendPoints==7
        &&spec.Series[2].TrendFit==TrendFit.Exponential&&spec.Series[3].TrendFit==TrendFit.Linear&&spec.Series[3].TrendPoints==7&&spec.Series[3].TrendDegree==2,"a trend setting was lost on the way in");
    var written=System.Text.Json.JsonSerializer.Serialize(spec,finishJson);
    Check(written.Contains("\"trendFit\":\"MovingAverage\"")&&written.Contains("\"trendFit\":\"Polynomial\"")&&written.Contains("\"trendFit\":\"Exponential\"")&&written.Contains("\"trendFit\":\"Linear\"")
        &&written.Contains("\"trendPoints\":3")&&written.Contains("\"trendDegree\":3"),written);
    var svg=ChartSvg.Render(spec);
    Check(svg==ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,finishJson)!),"a trend changed in transit");
    foreach(var label in new[]{"Rate trend: 3-point moving average","Curve trend: cubic fit, R squared 1.00","Growth trend: exponential fit, rising, R squared 1.00","Line trend: falling, R squared 1.00"})
        Check(svg.Contains($"aria-label='{label}'"),label);
    Reject(()=>ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json.Replace("\"trend\":true,\"trendFit\":\"Polynomial\"","\"trendFit\":\"Polynomial\""),finishJson)!));
});
Test("Explorer: the curved fits draw a quadratic through throughput and an exponential through latency on the right, each across the users tested, beside the scatter's least-squares lines",()=>{
    var spec=DemoData.Create(ChartKind.Scatter,ChartTheme.Light,0,AxisDemo.Fits);
    var doc=Svg(spec);
    var trends=doc.Descendants(ns+"path").Where(p=>(string?)p.Attribute("class")=="lumen-trend").ToArray();
    var labels=trends.Select(t=>t.Attribute("aria-label")!.Value).ToArray();
    Check(trends.Length==2&&labels[0].StartsWith("Throughput trend: quadratic fit, R squared 0.9")&&labels[1].StartsWith("Latency trend: exponential fit, rising, R squared 0.9"),string.Join(" | ",labels));
    Check(spec.Series.Count==2&&!spec.Series[0].Secondary&&spec.Series[1].Secondary&&spec.Y2Label=="Latency (ms)");
    // The throughput peaks inside the users tested, so its curve rises and then falls: on screen it climbs, then drops.
    var curve=Subpaths(trends[0]).Single();var top=curve.MinBy(v=>v.Y);
    Check(top.X>curve[0].X+100&&top.X<curve[^1].X-100,$"the throughput peaks at {top.X}");
    Check(DemoData.Create(ChartKind.Scatter,ChartTheme.Light).Series.All(s=>s.Trend&&s.TrendFit==TrendFit.Linear),"the scatter's own trends changed");
    foreach(var kind in Enum.GetValues<ChartKind>()) Check(DemoData.FitsCapable(kind)==(kind==ChartKind.Scatter));
});
// 0.33.0: race results on a line. Change colours, value labels on lines and scatter points, value notes and the X axis's tick source.
// The plot of a 900 by 420 chart with one pane runs from x 76 to 870 and y 78 to 344; with X fixed at 0 to 6 and Y at 0 to 10 a point
// stands where these say.
double RaceX(double x)=>76+x/6*794;
double RaceY(double y)=>344-y/10*266;
// The library's generous width for 11 px text, which a label's place is worked out from.
double Wide11(string text)=>text.Sum(c=>c is '.' or ',' or ':' or ' ' ? .3 : c is '-' ? .36 : c is 'm' or 'M' or 'w' or 'W' ? .9 : .62)*11;
ChartSpec Raced(ChartSeries series,params ChartSeries[] more)=>new(){Title="Races",Kind=ChartKind.Line,XMin=0,XMax=6,YMin=0,YMax=10,Series=[series,..more]};
// Five values, a gap and a sixth: down, level, up, a gap, down again across it, and up.
ChartPoint[] Placings()=>[new(0,5,"R1"),new(1,3,"R2"),new(2,3,"R3"),new(3,4,"R4"),new(4,null,"R5"),new(5,2,"R6"),new(6,6,"R7")];
XElement[] Datums(XDocument doc,int series)=>doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("data-series")==series.ToString(CultureInfo.InvariantCulture)).ToArray();
// The colour of the stroke that arrives at each point, read from every data path in the plot, whose pieces each start at one point
// and end at the next.
Dictionary<(double X,double Y),string> Arrivals(XDocument doc)
{
    var arrivals=new Dictionary<(double X,double Y),string>();
    foreach(var path in doc.Descendants(ns+"path").Where(p=>(string?)p.Attribute("fill")=="none"&&p.Attribute("class") is null))
        foreach(var piece in Subpaths(path))
            for(var i=1;i<piece.Count;i++) arrivals[(Math.Round(piece[i].X,6),Math.Round(piece[i].Y,6))]=path.Attribute("stroke")!.Value;
    return arrivals;
}
Test("Change colours: lower is better colours a fall the rising colour and a rise the falling one, a level or first point the series colour, and names each change",()=>{
    var light=ChartStyle.Light;var ink=light.SeriesColor(0);
    var doc=Svg(Raced(new("Position",Placings()){ChangeColors=ChangeColors.LowerIsBetter,Markers=MarkerStyle.Filled}));
    var marks=Datums(doc,0);
    Check(marks.Select(m=>m.Attribute("data-point")!.Value).SequenceEqual(["0","1","2","3","5","6"]),"the gap drew a mark");
    Check(marks.Select(m=>(string?)m.Element(ns+"circle")!.Attribute("fill")).SequenceEqual([ink,light.Rising,ink,light.Falling,light.Rising,light.Falling]),
        string.Join(",",marks.Select(m=>(string?)m.Element(ns+"circle")!.Attribute("fill"))));
    // The point after the gap compares with the last value before it, 2 against 4, and the words follow the colour.
    Check(marks.Select(m=>m.Attribute("aria-label")!.Value).SequenceEqual(["Position: R1, 5","Position: R2, 3, better than the previous","Position: R3, 3, level with the previous",
        "Position: R4, 4, worse than the previous","Position: R6, 2, better than the previous","Position: R7, 6, worse than the previous"]),string.Join(" | ",marks.Select(m=>m.Attribute("aria-label")!.Value)));
    Check(marks.All(m=>m.Element(ns+"title")!.Value==m.Attribute("aria-label")!.Value),"a tooltip differs from its mark's name");
    // Higher is better reverses every change and leaves the first and the level point alone.
    var higher=Datums(Svg(Raced(new("Points",Placings()){ChangeColors=ChangeColors.HigherIsBetter,Markers=MarkerStyle.Filled})),0);
    Check(higher.Select(m=>(string?)m.Element(ns+"circle")!.Attribute("fill")).SequenceEqual([ink,light.Falling,ink,light.Rising,light.Falling,light.Rising])
        &&higher[1].Attribute("aria-label")!.Value.EndsWith(", worse than the previous")&&higher[3].Attribute("aria-label")!.Value.EndsWith(", better than the previous"),"higher is better does not reverse the changes");
    // Without change colours, the same series names no change and draws one stroke in its colour.
    var plain=Svg(Raced(new("Position",Placings()){Markers=MarkerStyle.Filled}));
    Check(Datums(plain,0).All(m=>!m.Attribute("aria-label")!.Value.Contains("previous")&&(string?)m.Element(ns+"circle")!.Attribute("fill")==ink),"a plain series names a change");
});
Test("Change colours: the segment that arrives at a point takes its colour, a gap draws none, and a step or a smooth curve arrives the same way",()=>{
    var light=ChartStyle.Light;var ink=light.SeriesColor(0);
    foreach(var curve in Enum.GetValues<LineCurve>())
    {
        var doc=Svg(Raced(new("Position",Placings()){ChangeColors=ChangeColors.LowerIsBetter,Curve=curve}));
        var arrivals=Arrivals(doc);
        (double,double) At(double x,double y)=>(Math.Round(RaceX(x),6),Math.Round(RaceY(y),6));
        Check(arrivals[At(1,3)]==light.Rising&&arrivals[At(2,3)]==ink&&arrivals[At(3,4)]==light.Falling&&arrivals[At(6,6)]==light.Falling,$"{curve}: {string.Join(", ",arrivals.Select(a=>$"{a.Key}={a.Value}"))}");
        // The gap leaves the point after it with no segment arriving, though its mark is coloured by the change across the gap.
        Check(!arrivals.ContainsKey(At(5,2))&&!arrivals.ContainsKey(At(0,5)),$"{curve}: a segment arrives at the first point or across the gap");
        // Every piece of a stroke arriving at a point is in its colour: a step's rise and a smooth curve's cuts included.
        foreach(var path in doc.Descendants(ns+"path").Where(p=>(string?)p.Attribute("fill")=="none"&&p.Attribute("class") is null))
            foreach(var piece in Subpaths(path))
                Check(piece.All(v=>v.X>=RaceX(0)-1e-6&&v.X<=RaceX(6)+1e-6),$"{curve}: a piece runs off the plot");
        var rising=doc.Descendants(ns+"path").Where(p=>(string?)p.Attribute("stroke")==light.Rising).SelectMany(Subpaths).ToArray();
        Check(rising.Length==1&&Math.Abs(rising[0][0].X-RaceX(0))<1e-6&&Math.Abs(rising[0][^1].X-RaceX(1))<1e-6&&Math.Abs(rising[0][^1].Y-RaceY(3))<1e-6,$"{curve}: the rise into the second point is not one piece from the first to the second");
        if(curve==LineCurve.Step) Check(rising[0].Count==3&&Math.Abs(rising[0][1].X-RaceX(1))<1e-6&&Math.Abs(rising[0][1].Y-RaceY(5))<1e-6,"a step's corner is not in the colour of the point it arrives at");
    }
    // A point's own colour colours the segment that leaves it, which is the other rule; the two are refused together.
    var leaving=Arrivals(Svg(Raced(new("Position",Placings().Select((p,i)=>i==1?p with{Color="#123456"}:p).ToArray()))));
    Check(leaving[(Math.Round(RaceX(2),6),Math.Round(RaceY(3),6))]=="#123456","a point colour no longer colours the segment that leaves it");
});
Test("Change colours: better follows the setting, not the screen, on a reversed axis and on a scale shared with another measure",()=>{
    var light=ChartStyle.Light;
    string[] Fills(ChartSpec spec,int series)=>Datums(Svg(spec),series).Select(m=>(string)m.Element(ns+"circle")!.Attribute("fill")!).ToArray();
    var position=new ChartSeries("Position",Placings()){ChangeColors=ChangeColors.LowerIsBetter,Markers=MarkerStyle.Filled};
    var upright=Fills(Raced(position),0);
    var reversed=Raced(position) with{YReversed=true};
    Check(Fills(reversed,0).SequenceEqual(upright),"reversing the axis changed which points are better");
    Check(Datums(Svg(reversed),0)[1].Attribute("aria-label")!.Value.EndsWith("better than the previous"),"a place gained on a reversed axis is not better");
    // On one scale, a place and the points it earned each count better their own way: 31 then 24 is better, and 40 then 52 is too.
    ChartSpec shared=new(){Title="Shared",Kind=ChartKind.Line,Series=[
        new("Position",[new(0,31),new(1,24),new(2,27)]){ChangeColors=ChangeColors.LowerIsBetter,Markers=MarkerStyle.Filled},
        new("Points",[new(0,40),new(1,52),new(2,47)]){ChangeColors=ChangeColors.HigherIsBetter,Markers=MarkerStyle.Filled}]};
    Check(Fills(shared,0).Skip(1).SequenceEqual([light.Rising,light.Falling])&&Fills(shared,1).Skip(1).SequenceEqual([light.Rising,light.Falling]),"a shared scale colours one measure by the other's sense");
    // Scatter points take the colours and the words, and draw no segments; Midnight's rising and falling colours are its own.
    var dots=Svg(Raced(position with{Kind=ChartKind.Scatter}) with{Style=ChartStyle.Midnight});
    Check(Datums(dots,0).Select(m=>(string)m.Element(ns+"circle")!.Attribute("fill")!).SequenceEqual([ChartStyle.Midnight.SeriesColor(0),ChartStyle.Midnight.Rising,ChartStyle.Midnight.SeriesColor(0),ChartStyle.Midnight.Falling,ChartStyle.Midnight.Rising,ChartStyle.Midnight.Falling])
        &&Arrivals(dots).Count==0&&Datums(dots,0)[3].Attribute("aria-label")!.Value.EndsWith("worse than the previous"),"scatter points are not coloured and named by change");
    // The classic finish colours them the same.
    Check(Datums(Svg(Classic(Raced(position))),0).Select(m=>(string)m.Element(ns+"circle")!.Attribute("fill")!).SequenceEqual(upright),"the classic finish colours the changes differently");
});
Test("Value labels on a line stand above each point in its colour where it clears 4.5:1, move in from the plot's sides, and go below where above would leave the plot",()=>{
    var light=ChartStyle.Light;
    var series=new ChartSeries("Position",[new(0,5) {ValueNote="/48"},new(3,4),new(6,10){ValueNote="/52"}]){ValueLabels=true,Markers=MarkerStyle.Filled};
    var doc=Svg(Raced(series));
    var labels=doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-value").ToArray();
    Check(labels.Length==3,$"{labels.Length} labels");
    var texts=labels.Select(g=>g.Elements(ns+"text").Last()).ToArray();
    (double X,double Y) Place(XElement t)=>(double.Parse(t.Attribute("x")!.Value,CultureInfo.InvariantCulture),double.Parse(t.Attribute("y")!.Value,CultureInfo.InvariantCulture));
    // At the left edge the label moves right until its start is the plot's; in the middle it is centred 8 pixels over its point; at the
    // top it goes 16 pixels below, and at the right edge it moves left until its end is the plot's.
    Check(Math.Abs(Place(texts[0]).X-(76+Wide11("5/48")/2))<1e-6&&Math.Abs(Place(texts[0]).Y-(RaceY(5)-8))<1e-6,$"left: {Place(texts[0])}");
    Check(Math.Abs(Place(texts[1]).X-RaceX(3))<1e-6&&Math.Abs(Place(texts[1]).Y-(RaceY(4)-8))<1e-6,$"middle: {Place(texts[1])}");
    Check(Math.Abs(Place(texts[2]).X-(870-Wide11("10/52")/2))<1e-6&&Math.Abs(Place(texts[2]).Y-(RaceY(10)+16))<1e-6,$"top right: {Place(texts[2])}");
    // The value is in the axis's format and the point's colour at weight 600, the note muted at normal weight, under a halo in the
    // background colour, all hidden from assistive technology, which reads the mark's name.
    Check(texts[0].Value=="5/48"&&(string?)texts[0].Attribute("fill")==light.Text&&(string?)labels[0].Attribute("font-weight")=="600"&&(string?)labels[0].Attribute("font-size")=="11"
        &&(string?)labels[0].Attribute("aria-hidden")=="true"&&(string?)labels[0].Attribute("pointer-events")=="none","the label's look");
    var note=texts[0].Element(ns+"tspan")!;
    Check(note.Value=="/48"&&(string?)note.Attribute("class")=="lumen-muted"&&(string?)note.Attribute("font-weight")=="400"&&texts[1].Element(ns+"tspan") is null,"the note's look");
    var halo=labels[0].Elements(ns+"text").First();
    Check(halo.Value=="5/48"&&(string?)halo.Attribute("stroke")==light.Background&&(string?)halo.Attribute("fill")==light.Background&&halo.Attribute("x")!.Value==texts[0].Attribute("x")!.Value,"the halo");
    // Labels are written over the plot's clip, so none is cut by it, and a duration axis writes durations.
    Check(labels.All(l=>l.Parent==doc.Root),"a label is inside the clip");
    var pace=Svg(Raced(new("Pace",[new(1,300),new(2,315)]){ValueLabels=true}) with{YMin=240,YMax=360,YFormat=ValueFormat.Duration,YReversed=true});
    Check(pace.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-value").Select(g=>g.Elements(ns+"text").Last().Value).SequenceEqual(["5:00","5:15"]),"a duration axis's labels");
    // A point's colour, a zone's and a gradient's colour at the value reach the label where they clear 4.5:1 as text, and the text
    // colour stands in where they do not: on white, the series' blue, a mid grey and white itself.
    string[] Inks(ChartSeries s)=>Svg(Raced(s)).Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-value").Select(g=>g.Elements(ns+"text").Last().Attribute("fill")!.Value).ToArray();
    Check(Inks(new("P",[new(1,5){Color="#123456"},new(2,4)]){ValueLabels=true}).SequenceEqual(["#123456",light.Text]),"point colours");
    Check(Inks(new("P",[new(1,2),new(2,8)]){ValueLabels=true,Zones=new([new("Low",5,"#1D4E89"),new("High",double.PositiveInfinity,"#9A2A1F")])}).SequenceEqual(["#1D4E89","#9A2A1F"]),"zone colours");
    Check(Inks(new("P",[new(1,0),new(2,5),new(3,10)]){ValueLabels=true,Gradient=[new(0,"#000000"),new(10,"#FFFFFF")]}).SequenceEqual(["#000000",light.Text,light.Text]),"gradient colours");
});
Test("Value labels on lines and scatter points clear 4.5:1 as text: a point colour that does not gives the label the text colour, one that does keeps it, and the note stays muted",()=>{
    string[] Inks(ChartSpec spec)=>Svg(spec).Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-value").Select(g=>g.Elements(ns+"text").Last().Attribute("fill")!.Value).ToArray();
    string[] Markers(ChartSpec spec)=>Datums(Svg(spec),0).Select(m=>(string)m.Element(ns+"circle")!.Attribute("fill")!).ToArray();
    // The light preset's first series colour, #5675E7, stands 4.12:1 on white: its marker keeps it, and its label is written in the text colour.
    var light=Raced(new("Pace",[new(1,5){ValueNote="/48"},new(2,4),new(3,6)]){ValueLabels=true,Markers=MarkerStyle.Filled});
    Check(Contrast(ChartStyle.Light.SeriesColor(0),ChartStyle.Light.Background)<4.5&&Markers(light).All(f=>f==ChartStyle.Light.SeriesColor(0)),"the marker lost its colour");
    Check(Inks(light).SequenceEqual([ChartStyle.Light.Text,ChartStyle.Light.Text,ChartStyle.Light.Text]),$"the labels are {string.Join(",",Inks(light))}");
    var note=Svg(light).Descendants(ns+"g").Single(g=>(string?)g.Attribute("class")=="lumen-value"&&g.Value.Contains("/48")).Elements(ns+"text").Last().Element(ns+"tspan")!;
    Check(note.Value=="/48"&&(string?)note.Attribute("class")=="lumen-muted"&&note.Attribute("fill") is null&&Contrast(ChartStyle.Light.Muted,ChartStyle.Light.Background)>=4.5,"the note is not in the muted colour");
    // Change colours on light, 3.44:1 rising and 3.38:1 falling, fall short too; on Midnight every one clears 4.5:1 and the labels keep them.
    var changing=new ChartSeries("Position",[new(1,5),new(2,4),new(3,6)]){ValueLabels=true,ChangeColors=ChangeColors.LowerIsBetter,Markers=MarkerStyle.Filled};
    Check(Inks(Raced(changing)).All(f=>f==ChartStyle.Light.Text)&&Markers(Raced(changing)).SequenceEqual([ChartStyle.Light.SeriesColor(0),ChartStyle.Light.Rising,ChartStyle.Light.Falling]),"light change colours");
    var midnight=ChartStyle.Midnight;
    Check(Inks(Raced(changing) with{Style=midnight}).SequenceEqual([midnight.SeriesColor(0),midnight.Rising,midnight.Falling]),$"Midnight's labels are {string.Join(",",Inks(Raced(changing) with{Style=midnight}))}");
    // The dark preset is in between: its rising and falling colours clear 4.5:1 and keep the label, its blue does not and gives way.
    var dark=ChartStyle.Dark;
    Check(Contrast(dark.Rising,dark.Background)>=4.5&&Contrast(dark.SeriesColor(0),dark.Background)<4.5
        &&Inks(Raced(changing) with{Theme=ChartTheme.Dark}).SequenceEqual([dark.Text,dark.Rising,dark.Falling]),$"the dark preset's labels are {string.Join(",",Inks(Raced(changing) with{Theme=ChartTheme.Dark}))}");
    // Every label of every style is written in a colour that clears 4.5:1 against its background, scatter points' included.
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,midnight})
        foreach(var kind in new[]{ChartKind.Line,ChartKind.Scatter})
            Check(Inks(Raced(changing with{Kind=kind}) with{Style=style}).All(f=>Contrast(f,style.Background)>=4.5),$"{kind} on {style.Background}");
});
Test("Value labels on lines and scatter points keep clear of each other: one that would meet a label written before it goes below, and one with room in neither place is left out",()=>{
    // Two series at one value: the second's label goes under its point. At the top of the plot the first goes under, so the second has
    // nowhere to go and is left out. A point the plot does not show is left out with its label.
    var doc=Svg(Raced(new("A",[new(1,5),new(3,10),new(5,4)]){ValueLabels=true},new ChartSeries("B",[new(1,5),new(3,10),new(7,4)]){ValueLabels=true}) with{XMax=6});
    var texts=doc.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-value").Select(g=>g.Elements(ns+"text").Last()).ToArray();
    (double X,double Y)[] places=texts.Select(t=>(double.Parse(t.Attribute("x")!.Value,CultureInfo.InvariantCulture),double.Parse(t.Attribute("y")!.Value,CultureInfo.InvariantCulture))).ToArray();
    Check(places.Length==4,$"{places.Length} labels: {string.Join(" ",places)}");
    bool At((double X,double Y) place,double x,double y)=>Math.Abs(place.X-x)<1e-6&&Math.Abs(place.Y-y)<1e-6;
    Check(At(places[0],RaceX(1),RaceY(5)-8)&&At(places[1],RaceX(3),RaceY(10)+16)&&At(places[2],RaceX(5),RaceY(4)-8)&&At(places[3],RaceX(1),RaceY(5)+16),string.Join(" ",places));
    // Scatter points take labels too, by the same rule, and a column's label counts as written before them.
    var mixed=Svg(new ChartSpec{Title="Mixed",Kind=ChartKind.Line,YMin=0,YMax=10,XMin=0,XMax=6,Series=[new("Bars",[new(3,6)]){Kind=ChartKind.Column,ValueLabels=true},new("Dots",[new(3,6.3)]){Kind=ChartKind.Scatter,ValueLabels=true}]});
    var dot=mixed.Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-value").Select(g=>g.Elements(ns+"text").Last()).Single();
    Check(dot.Value=="6.3"&&Math.Abs(double.Parse(dot.Attribute("y")!.Value,CultureInfo.InvariantCulture)-(RaceY(6.3)+16))<1e-6,$"the dot's label stands at {dot.Attribute("y")!.Value} beside a column's");
});
Test("Value notes reach the label, the tooltip and accessible name, the component's table and status line, and a CSV Note column only when a point has one",()=>{
    var spec=Raced(new("Position",[new(0,31,"11-04-2026"){ValueNote="/50"},new(1,24,"16-05-2026"){ValueNote="/48"},new(2,null,"04-07-2026"){ValueNote="/51"},new(3,21,"08-08-2026")]){ChangeColors=ChangeColors.LowerIsBetter}) with{YMax=40};
    var marks=Datums(Svg(spec),0);
    Check(marks.Select(m=>m.Attribute("aria-label")!.Value).SequenceEqual(["Position: 11-04-2026, 31/50","Position: 16-05-2026, 24/48, better than the previous","Position: 08-08-2026, 21, better than the previous"])
        &&marks[1].Element(ns+"title")!.Value=="Position: 16-05-2026, 24/48, better than the previous","a note is not after its value, or a missing value wrote one");
    Check(!ChartSvg.Render(spec).Contains("lumen-value"),"a note wrote a label without ValueLabels");
    var csv=ChartExport.Csv(spec);
    Check(csv.StartsWith("Series,X,Y,Label,Size,Note\r\n")&&csv.Contains("\"Position\",1,24,\"16-05-2026\",1,\"/48\"\r\n")&&csv.Contains("\"Position\",2,,\"04-07-2026\",1,\"/51\"\r\n")&&csv.Contains("\"Position\",3,21,\"08-08-2026\",1,\"\"\r\n"),csv);
    Check(ChartExport.Csv(Spec()).StartsWith("Series,X,Y,Label,Size\r\n"),"a chart without notes has a Note column");
    Check(ChartExport.Csv(Spec() with{Series=[new("S",[new(0,1){ValueNote="+1"}])]}).Contains(",\"'+1\"\r\n"),"a note is not protected against spreadsheet formulas");
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var html=Operate(spec,async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(0,1);});
    Check(html.Contains("<tr><td>Position</td><td>16-05-2026</td><td>24/48</td></tr>")&&html.Contains("<tr><td>Position</td><td>04-07-2026</td><td>Missing</td></tr>")&&html.Contains("<tr><td>Position</td><td>08-08-2026</td><td>21</td></tr>"),"the table");
    Check(html.Contains("Position: 16-05-2026 = 24/48"),"the status line");
    // Columns, bars, blocks, donut slices, heatmap cells and radar points carry a note in their names, and a column's or bar's label
    // writes it muted after the value; at its default it changes nothing.
    var bars=Svg(Spec(ChartKind.Column) with{Series=[new("S",[new(0,2,"A"){ValueNote=" km"},new(1,5,"B")]){ValueLabels=true}]});
    var bar=bars.Descendants(ns+"text").First(t=>t.Value=="2 km");
    Check(bar.Element(ns+"tspan")?.Value==" km"&&(string?)bar.Element(ns+"tspan")!.Attribute("class")=="lumen-muted"&&Datums(bars,0)[0].Attribute("aria-label")!.Value=="S: A, 2 km","a column's note");
    foreach(var kind in new[]{ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Bar,ChartKind.Blocks,ChartKind.Area,ChartKind.Band,ChartKind.Bubble,ChartKind.StackedColumn})
    {
        var sample=Sample(kind);
        var noted=sample with{Series=sample.Series.Select(s=>s with{Points=s.Points.Select((p,i)=>i==1?p with{ValueNote="!n"}:p).ToArray()}).ToArray()};
        Check(Svg(noted).Descendants().Any(e=>((string?)e.Attribute("aria-label"))?.Contains("!n")==true),$"{kind} lost its note");
    }
});
Test("XTicks: a time axis keeps its dates unless every point's label fits, Axis always draws the axis's ticks and PointLabels always the labels",()=>{
    var monday=TimeAxis.Value(new DateTimeOffset(2026,4,6,10,0,0,TimeSpan.Zero));
    ChartSpec Rounds(params string[] names)=>new(){Title="Season",Kind=ChartKind.Line,XAxis=AxisKind.Time,
        Series=[new("Position",names.Select((n,i)=>new ChartPoint(monday+i*35*86400000d,30-i*2,n)).ToArray())]};
    string[] Ticks(ChartSpec spec)=>Svg(spec).Descendants(ns+"text").Where(t=>t.Attribute("y")?.Value=="365").Select(t=>t.Value).ToArray();
    string[] shortNames=["Round 1","Round 2","Round 3","Round 4","Round 5"];
    string[] longNames=["Round 1 · Hilltop","Round 2 · Valley","Round 3 · Quarry","Round 4 · Forest","Round 5 · Ridge"];
    var dates=Ticks(Rounds(longNames));
    Check(dates.Length>=2&&dates.All(t=>System.Text.RegularExpressions.Regex.IsMatch(t,"^\\d{1,2} [A-Z][a-z]{2}$|^[A-Z][a-z]{2} \\d{4}$")),$"long names on a time axis: {string.Join(", ",dates)}");
    Check(Svg(Rounds(longNames)).Descendants(ns+"g").Any(g=>g.Attribute("aria-label")?.Value=="Position: Round 3 · Quarry, 26"),"the long names left the points' names");
    Check(Ticks(Rounds(shortNames)).SequenceEqual(shortNames),$"short names on a time axis: {string.Join(", ",Ticks(Rounds(shortNames)))}");
    // Twelve characters is the most a label keeps, so a twelve-character name still labels the axis and a thirteen-character one does not.
    var twelve=shortNames.Select(n=>n.PadRight(12,'x')).ToArray();
    Check(Ticks(Rounds(twelve)).SequenceEqual(twelve)&&Ticks(Rounds([.. shortNames.Take(4),"Round 5 xxxxx"])).SequenceEqual(dates),"the twelve-character rule");
    Check(Ticks(Rounds(shortNames) with{XTicks=TickSource.Axis}).SequenceEqual(dates),"Axis kept the names");
    Check(Ticks(Rounds(longNames) with{XTicks=TickSource.PointLabels}).SequenceEqual(longNames.Select(n=>n[..11]+"…")),$"PointLabels: {string.Join(", ",Ticks(Rounds(longNames) with{XTicks=TickSource.PointLabels}))}");
    // A linear axis keeps today's rule: up to 24 labelled points label it, long ones cut short, and past 24 the axis's own numbers.
    ChartSpec Indexed(int count,string prefix)=>new(){Title="Index",Kind=ChartKind.Line,Series=[new("S",Enumerable.Range(0,count).Select(i=>new ChartPoint(i,i%5,$"{prefix}{i}")).ToArray())]};
    Check(Ticks(Indexed(5,"Long label number ")).All(t=>t.StartsWith("Long label ")&&t.EndsWith("…"))&&Ticks(Indexed(5,"R")).SequenceEqual(["R0","R1","R2","R3","R4"]),"a linear axis's labels");
    var numbers=Ticks(Indexed(30,"R"));
    Check(numbers.All(t=>double.TryParse(t,CultureInfo.InvariantCulture,out _)),$"thirty labels: {string.Join(", ",numbers)}");
    var thinned=Ticks(Indexed(30,"R") with{XTicks=TickSource.PointLabels});
    Check(thinned.Length is >= 2 and < 30&&thinned.All(t=>t.StartsWith('R'))&&thinned[0]=="R0",$"PointLabels at thirty: {string.Join(", ",thinned)}");
    Check(Ticks(Indexed(5,"R") with{XTicks=TickSource.Axis}).All(t=>double.TryParse(t,CultureInfo.InvariantCulture,out _)),"Axis on a linear axis");
    // Where no labelled point stands in the range, PointLabels falls back to the axis's ticks; a block's label never labels the axis.
    Check(Ticks(Spec() with{Series=[new("S",[new(0,1),new(1,2)])],XTicks=TickSource.PointLabels}).SequenceEqual(Ticks(Spec() with{Series=[new("S",[new(0,1),new(1,2)])]})),"PointLabels without labels");
    var blocks=Sample(ChartKind.Blocks);
    Check(Ticks(blocks with{XTicks=TickSource.PointLabels}).SequenceEqual(Ticks(blocks)),"a block's label labelled the axis");
});
Test("0.33.0 refuses change colours, value labels, value notes and tick sources where they cannot apply, each with its reason",()=>{
    string Refusal(ChartSpec spec){try{ChartSvg.Render(spec);}catch(ArgumentException error){return error.Message;}throw new Exception("a chart was accepted that should not be");}
    var change=new ChartSeries("S",[new(0,3),new(1,2),new(2,4)]){ChangeColors=ChangeColors.LowerIsBetter};
    foreach(var kind in new[]{ChartKind.Area,ChartKind.Column,ChartKind.Bar,ChartKind.Bubble,ChartKind.Band,ChartKind.StackedColumn,ChartKind.Radar,ChartKind.Heatmap,ChartKind.Donut})
        Check(Refusal(Spec(kind) with{Series=[change]}).Contains("Change colours apply to series drawn as lines or scatter points"),$"{kind}: {Refusal(Spec(kind) with{Series=[change]})}");
    foreach(var kind in new[]{ChartKind.Range,ChartKind.Blocks,ChartKind.Timeline,ChartKind.Calendar,ChartKind.Candlestick,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Gauge,ChartKind.Ring})
        Reject(()=>ChartSvg.Render(Sample(kind) with{Series=Sample(kind).Series.Select(s=>s with{ChangeColors=ChangeColors.HigherIsBetter}).ToArray()}));
    Check(Refusal(Spec() with{Series=[change with{Kind=ChartKind.Column}]}).Contains("Change colours apply"),"a column series");
    Check(Refusal(Spec() with{Series=[change with{Zones=new([new("Low",2),new("High",double.PositiveInfinity)])}]}).Contains("Zones colour a point"),"zones");
    Check(Refusal(Spec() with{Series=[change with{Gradient=[new(0,"#000000"),new(5,"#FFFFFF")]}]}).Contains("A gradient colours a series"),"a gradient");
    Check(Refusal(Spec() with{Series=[change with{Points=[new(0,3),new(1,2){Color="#123456"}]}]}).Contains("takes no point colours"),"point colours");
    Check(Refusal(Spec(ChartKind.Scatter) with{Series=[change with{Points=[new(1,3),new(0,2)]}]}).Contains("ordered by X"),"scatter points out of order");
    Check(Refusal(Spec() with{Series=[change with{ChangeColors=(ChangeColors)5}]})=="Unknown change colours.","an unknown sense");
    Check(Refusal(Spec(ChartKind.Scatter) with{DensityCells=10,Series=[change]}).Contains("no value labels or change colours")&&Refusal(Spec(ChartKind.Scatter) with{DensityCells=10,Series=[change with{ChangeColors=ChangeColors.None,ValueLabels=true}]}).Contains("no value labels or change colours"),"a density scatter");
    ChartSvg.Render(Spec(ChartKind.Scatter) with{Series=[change]});ChartSvg.Render(Spec() with{Series=[change with{ProjectedFrom=1,Curve=LineCurve.Smooth,HighlightLast=true}]});
    Check(Refusal(Spec(ChartKind.Area) with{Series=[new("S",[new(0,1)]){ValueLabels=true}]})=="Value labels apply to series drawn as columns, bars, lines or scatter points.","value labels on an area");
    var noted=new ChartPoint(0,1){ValueNote="/48"};
    Check(Refusal(Spec() with{Series=[new("S",[new(0,1){ValueNote=new string('x',21)}])]}).Contains("at most 20 characters"),"a long note");
    ChartSvg.Render(Spec() with{Series=[new("S",[new(0,1){ValueNote=new string('x',20)}])]});
    Reject(()=>ChartSvg.Render(Spec() with{Series=[new("S",[new(0,1){ValueNote="/4\u00078"}])]}));
    foreach(var kind in new[]{ChartKind.Range,ChartKind.Candlestick,ChartKind.Ohlc,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Timeline,ChartKind.Calendar,ChartKind.Gauge,ChartKind.Ring})
    {
        var sample=Sample(kind);
        var with=sample with{Series=sample.Series.Select((s,i)=>i>0?s:s with{Points=s.Points.Select((p,k)=>k>0?p:p with{ValueNote="/48"}).ToArray()}).ToArray()};
        Check(Refusal(with).Contains("A value note is written after a mark's one value"),$"{kind}: {Refusal(with)}");
    }
    Check(Refusal(Spec() with{Series=[new("S",[new(0,1),new(1,2)]),new("R",[ChartPoint.Interval(0,1,0,2) with{ValueNote="/48"}]){Kind=ChartKind.Range}]}).Contains("A value note"),"a range series beside a line");
    foreach(var kind in new[]{ChartKind.Column,ChartKind.Bar,ChartKind.StackedColumn,ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Gauge,ChartKind.Ring,ChartKind.Timeline,ChartKind.Calendar})
        foreach(var source in new[]{TickSource.Axis,TickSource.PointLabels})
            Check(Refusal(Sample(kind) with{XTicks=source}).Contains("XTicks chooses between"),$"{kind} took {source}");
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Area,ChartKind.Scatter,ChartKind.Bubble,ChartKind.Candlestick,ChartKind.Ohlc,ChartKind.Band,ChartKind.Range,ChartKind.Blocks})
        foreach(var source in Enum.GetValues<TickSource>()) ChartSvg.Render(Sample(kind) with{XTicks=source});
    Check(Refusal(Spec() with{XTicks=(TickSource)3})=="Unknown tick source.","an unknown tick source");
    Check(Refusal(Spec() with{Series=[new("S",[noted])],XTicks=(TickSource)(-1)})=="Unknown tick source.","a negative tick source");
});
Test("0.32.0's renderings do not move: rows of its baseline rebuilt here match its hashes in both finishes, value labels on columns and bars and labelled time axes among them",()=>{
    // Hashes of v0.32.0's tests/Lumen.Charts.Baseline/reference files, refined and classic, recorded on Windows.
    if(!OperatingSystem.IsWindows())return;
    ChartSpec line=Baseline(ChartKind.Line,ChartTheme.Light),column=Baseline(ChartKind.Column,ChartTheme.Light),bar=Baseline(ChartKind.Bar,ChartTheme.Light),scatter=Baseline(ChartKind.Scatter,ChartTheme.Light);
    ChartPoint[] Signed()=>Twelve().Select((p,i)=>p with{Y=i%3==0?-p.Y/2:p.Y-20}).ToArray();
    ChartSeries[] Unlabelled(Func<double,double> x)=>line.Series.Select(s=>s with{Points=s.Points.Select(p=>p with{X=x(p.X),Label=null}).ToArray()}).ToArray();
    double[] volume=[6.5,7.2,8.1,5.0,7.9,8.8,9.4,5.6,9.1,10.2,10.8,6.0];
    var rolling=Statistics.Rolling(volume.Select(v=>(double?)v).ToArray(),4,1);
    ChartSpec weekly=new(){Kind=ChartKind.Column,Title="Weekly volume",YLabel="Hours",Series=[new("Volume",volume.Select((v,i)=>new ChartPoint(i,v,$"W{i+1}")).ToArray()),
        new("Four-week average",rolling.Select((r,i)=>new ChartPoint(i,Math.Round(r!.Mean,2),$"W{i+1}")).ToArray()){Kind=ChartKind.Line}]};
    var fortnight=Enumerable.Range(0,14).Select(d=>new DateOnly(2026,9,14).AddDays(d)).ToArray();
    double Morning(DateOnly day)=>TimeAxis.Value(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero));
    var rates=fortnight.Select((day,d)=>(Day:day,Low:46d+d%5,High:d%7 is 1 or 3 or 5?150d+d*2:105d+d%4*5,Average:66d+d%6)).ToArray();
    ChartSpec heart=new(){Kind=ChartKind.Range,XAxis=AxisKind.Time,Title="52 to 174 bpm today",Description="Each day's lowest and highest heart rate, the dot its average",Width=540,Height=360,YLabel="Heart rate (bpm)",
        Series=[new("Heart rate",rates.Select(r=>ChartPoint.Interval(Morning(r.Day),r.Average,r.Low,r.High,r.Day.ToString("d MMM",CultureInfo.InvariantCulture))).ToArray(),ChartStyle.Light.Zones[5])]};
    var heartZones=ZoneScale.CogganHeartRate(170);
    var seconds=Training.TimeInZone(Enumerable.Range(0,2400).Select(t=>Math.Round(95+85*(1-Math.Exp(-t/400.0))+12*Math.Sin(t/70.0)+t%5,1)).ToArray(),heartZones);
    (string Row,string Refined,string Classic,ChartSpec Spec)[] rows=[
        ("Line/Light/True","D991008D3106193D","CB7DF56598CE861F",line),
        ("finish/capsule-value-labels","A48E1BFCC1A75412","08631EBEDF404578",column with{Title="Capsules",Style=ChartStyle.Light with{BarRadius=9999},
            Series=[new("Week",Signed()){ValueLabels=true,Fill=AreaFill.Fade},new("Last",Twelve().Select(p=>p with{Y=p.Y/2}).ToArray()){ValueLabels=true}]}),
        ("finish/bars-dashed-ends","5C96B6407C738771","59360C834E33DCB8",bar with{Title="Bars",Style=ChartStyle.Light with{BarRadius=6,Gridlines=GridLine.Dashed},YTickLabels=TickLabels.Ends,Series=[new("A",Signed()){ValueLabels=true}]}),
        ("finish/midnight-weekly","D2ECDD4B4F702555","53E74673F793CA0E",weekly with{Style=ChartStyle.Midnight,Series=[weekly.Series[0] with{ValueLabels=true},weekly.Series[1] with{Curve=LineCurve.Smooth,Markers=MarkerStyle.Hollow}]}),
        ("range/heart-rate","060F181989F45FAB","904529397C741507",heart),
        ("guard/time","6F65F0C3788C47BD","48647563F4A154CE",line with{XAxis=AxisKind.Time,Series=Unlabelled(x=>1767225600000d+x*86400000d)}),
        ("guard/line-markers","6A03DBA7B8A29EB8","0AB8C0AE5E4BFCDC",line with{Series=[new("A",Twelve().Select((p,i)=>p with{Color=i==3?"#123456":null,Y=i==7?null:p.Y}).ToArray()),
            new("B",Twelve()){Zones=new([new("Low",25),new("High",double.PositiveInfinity)]),ProjectedFrom=8}]}),
        ("zones/time-in-zone","725159C7DD9ECD59","77633DA66248659B",bar with{YFormat=ValueFormat.Duration,Series=[new("Time in zone",heartZones.Zones.Select((z,i)=>new ChartPoint(i,seconds[i],z.Name){Color=ChartStyle.Light.Zones[i]}).ToArray())]}),
        ("guard/scatter-markers","011DBD4AAAD9AF7B","9D9C47D723F1C608",scatter with{Theme=ChartTheme.Dark,Series=[scatter.Series[0] with{Points=scatter.Series[0].Points.Select((p,i)=>p with{Color=i%4==0?"#123456":null,Y=i==5?null:p.Y}).ToArray()},
            scatter.Series[1] with{Secondary=true,Zones=new([new("Low",25),new("High",double.PositiveInfinity)])}]})];
    foreach(var (row,refined,classic,spec) in rows)
    {
        var titles=row!="guard/scatter-markers";
        Check(Hash16(ChartSvg.Render(spec,includeTitles:titles))==refined,$"{row} moved: {Hash16(ChartSvg.Render(spec,includeTitles:titles))}");
        Check(Hash16(ChartSvg.Render(Classic(spec),includeTitles:titles))==classic,$"{row} moved in the classic finish: {Hash16(ChartSvg.Render(Classic(spec),includeTitles:titles))}");
        // Each new setting written out at its default draws the same chart.
        var spelled=spec with{XTicks=TickSource.Auto,Series=spec.Series.Select(s=>s with{ChangeColors=ChangeColors.None,Points=s.Points.Select(p=>p with{ValueNote=null}).ToArray()}).ToArray()};
        Check(ChartSvg.Render(spelled,includeTitles:titles)==ChartSvg.Render(spec,includeTitles:titles),$"{row}: the defaults written out moved it");
    }
});
Test("Change colours, value notes and tick sources round-trip through JSON as strings, a request that names none keeps the defaults, and only a set one renames gradients",()=>{
    var spec=Raced(new("Position",[new(0,31,"R1"){ValueNote="/50"},new(1,24,"R2"){ValueNote="/48"}]){ChangeColors=ChangeColors.LowerIsBetter,ValueLabels=true}) with{XTicks=TickSource.PointLabels};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,finishJson);
    Check(json.Contains("\"changeColors\":\"LowerIsBetter\"")&&json.Contains("\"valueNote\":\"/48\"")&&json.Contains("\"xTicks\":\"PointLabels\"")&&json.Contains("\"valueLabels\":true"),json);
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(back.XTicks==TickSource.PointLabels&&back.Series[0].ChangeColors==ChangeColors.LowerIsBetter&&back.Series[0].Points[1].ValueNote=="/48"&&ChartSvg.Render(back)==ChartSvg.Render(spec),"a setting changed in transit");
    var written="{\"title\":\"Season\",\"kind\":\"Line\",\"xTicks\":\"Axis\",\"series\":[{\"name\":\"Position\",\"changeColors\":\"HigherIsBetter\",\"valueLabels\":true,\"points\":[{\"x\":0,\"y\":3,\"label\":\"A\",\"valueNote\":\" pts\"},{\"x\":1,\"y\":5,\"label\":\"B\"}]}]}";
    var read=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,finishJson)!;
    var svg=ChartSvg.Render(read);
    Check(read.XTicks==TickSource.Axis&&svg.Contains("aria-label='Position: B, 5, better than the previous'")&&svg.Contains("aria-label='Position: A, 3 pts'")&&svg.Contains($"fill='{ChartStyle.Light.Rising}'"),"hand-written JSON");
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]}]}",finishJson)!;
    Check(old.XTicks==TickSource.Auto&&old.Series[0].ChangeColors==ChangeColors.None&&old.Series[0].Points[0].ValueNote is null,"the defaults");
    // A gradient's ID is named after the spec: the defaults leave it alone, and a setting away from them names it afresh.
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}]};
    string Id(ChartSpec s)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(s),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var id=Id(faded);
    Check(Id(faded with{XTicks=TickSource.Auto,Series=[faded.Series[0] with{ChangeColors=ChangeColors.None}]})==id,"the defaults renamed the gradient");
    Check(Id(faded with{XTicks=TickSource.Axis})!=id&&Id(faded with{Series=[faded.Series[0] with{Points=[new(0,1){ValueNote="/2"},new(1,3)]}]})!=id
        &&Id(Spec() with{Series=[new("S",[new(0,1),new(1,3)]){ChangeColors=ChangeColors.HigherIsBetter},new("F",[new(0,1),new(1,3)]){Kind=ChartKind.Area,Fill=AreaFill.Fade}]})
          !=Id(Spec() with{Series=[new("S",[new(0,1),new(1,3)]),new("F",[new(0,1),new(1,3)]){Kind=ChartKind.Area,Fill=AreaFill.Fade}]}),"a setting kept the gradient's name");
});
// 0.34.0: sparklines. A chart can draw its data alone at the size of a word, a point can be ringed by a highlight, and a Y axis can keep
// a minimum span centred on its data. Every example is invented: a run of six 5 km times, oldest first, the second, fourth and sixth
// faster than every time before them, on the dark style a race-results recipe builds from its design tokens.
var raceFace=new ChartStyle{Background="#161618",Text="#F5F6F7",Muted="#80858E",Grid="#2D2D2F",Edge="#80858E",Series=["#FF5A54","#D7DDE5","#F5B642","#3FD17A","#C2C6D2","#CD7F46"],
    Zones=["#80858E","#D7DDE5","#3FD17A","#F5B642","#F2545B"],Rising="#34d399",Falling="#f87171",HeatmapLow="#1E1F22",HeatmapHigh="#E30613",FontFamily="Inter, Segoe UI, Arial, sans-serif"};
double[] fiveK=[1450,1432,1445,1411,1420,1367];
ChartSpec Pb(ChartStyle? style=null)=>new(){Title="5 km: 24:10 to 22:47 over 6 races",Description="Each race's time, oldest first, faster higher",Kind=ChartKind.Line,Width=120,Height=32,
    Sparkline=true,YReversed=true,YFormat=ValueFormat.Duration,Style=style??raceFace,
    Series=[new("5 km",fiveK.Select((t,i)=>new ChartPoint(i,t,$"Race {i+1}"){Highlight=i%2==1?"#E30613":null,ValueNote=i%2==1?" · PB":null}).ToArray(),"#B7BCC4"){StrokeWidth=2}]};
ChartSpec Spark(ChartKind kind=ChartKind.Line)=>new(){Title="Spark",Description="A few values",Kind=kind,Width=120,Height=32,Sparkline=true,
    Series=[new("S",[new(0,3,"A"),new(1,5,"B"),new(2,null,"C"),new(3,4,"D")]),new("T",[new(0,1,"A"),new(1,2,"B"),new(2,3,"C"),new(3,2,"D")])]};
string Refused(ChartSpec spec){try{ChartSvg.Render(spec);}catch(ArgumentException error){return error.Message;}throw new Exception("a chart was accepted that should not be");}
// How far a drawing's circles reach: each one's radius and half its stroke, its own or its group's, a scatter dot's default stroke
// being one unit and a filled marker having none.
(double Left,double Top,double Right,double Bottom) Reach(XDocument doc)
{
    double l=double.MaxValue,t=double.MaxValue,r=double.MinValue,b=double.MinValue;
    foreach(var c in doc.Descendants(ns+"circle"))
    {
        var width=(string?)c.Attribute("stroke-width")??(string?)c.Parent!.Attribute("stroke-width");
        var half=width is not null?double.Parse(width,CultureInfo.InvariantCulture)/2:c.Attribute("stroke") is null&&c.Parent!.Attribute("stroke") is null?0:.5;
        double x=Attr(c,"cx"),y=Attr(c,"cy"),reach=Attr(c,"r")+half;
        (l,t,r,b)=(Math.Min(l,x-reach),Math.Min(t,y-reach),Math.Max(r,x+reach),Math.Max(b,y+reach));
    }
    return (l,t,r,b);
}
Test("A sparkline draws no text at all, only its title and desc, and every point keeps a focusable, named mark with its native tooltip",()=>{
    var specs=new List<ChartSpec>{Pb(),Pb(ChartStyle.Midnight),Classic(Pb()),Pb() with{Style=null,Theme=ChartTheme.Dark}};
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Area,ChartKind.Scatter,ChartKind.Column}){specs.Add(Spark(kind) with{Source="Source: invented",XLabel="Race",YLabel="Time"});specs.Add(Classic(specs[^1]));}
    // Zone bands, annotations, a trend, a series on the right and a density scatter keep their shapes and lose their words, and minor
    // gridlines, which a sparkline does not draw, leave no rule behind.
    var zoned=Spark() with{YZones=new([new("Low",2),new("High",double.PositiveInfinity)]),MinorGridlines=true,Annotations=[new(AnnotationAxis.Y,4){Label="Target"},new(AnnotationAxis.X,1){To=2,Label="Window"}]};
    specs.AddRange([zoned,Classic(zoned),Spark(ChartKind.Scatter) with{DensityCells=10},Spark() with{Series=[Spark().Series[0] with{Trend=true},Spark().Series[1] with{Secondary=true}]}]);
    foreach(var spec in specs)
    {
        var doc=Svg(spec);var root=doc.Root!;
        Check(!doc.Descendants(ns+"text").Any()&&!doc.Descendants(ns+"tspan").Any(),$"{spec.Kind} wrote {string.Join(", ",doc.Descendants(ns+"text").Select(t=>t.Value))}");
        Check(root.Elements(ns+"title").Single().Value==spec.Title&&root.Elements(ns+"desc").Single().Value==spec.Description&&root.Attribute("aria-label")!.Value==$"{spec.Title}. {spec.Description}"&&(string?)root.Attribute("role")=="group",$"{spec.Kind}: its name");
        Check(root.Attribute("viewBox")!.Value=="0 0 120 32"&&root.Attribute("style")!.Value.Contains(";width:120px;max-width:100%;height:auto;display:block;"),root.Attribute("style")!.Value);
        Check(!doc.Descendants().Any(e=>(string?)e.Attribute("class") is "lumen-grid" or "lumen-grid-minor")&&!doc.Descendants(ns+"style").Single().Value.Contains("lumen-grid-minor"),$"{spec.Kind} drew a gridline");
        if(spec.DensityCells is not null){Check(doc.Descendants(ns+"g").Count(g=>(string?)g.Attribute("role")=="img")>0,"the density cells");continue;}
        // Every point with a value is a focusable button named for it, its name its tooltip; a missing value draws none.
        for(var si=0;si<spec.Series.Count;si++)
        {
            var marks=Datums(doc,si);
            Check(marks.Length==spec.Series[si].Points.Count(p=>p.Y.HasValue),$"{spec.Kind}: {marks.Length} marks for series {si}");
            Check(marks.All(m=>(string?)m.Attribute("tabindex")=="0"&&(string?)m.Attribute("role")=="button"&&m.Element(ns+"title")!.Value==m.Attribute("aria-label")!.Value
                &&m.Attribute("aria-label")!.Value.StartsWith(spec.Series[si].Name+": ")),$"{spec.Kind}: a mark is not named");
        }
    }
    // The bands and annotations keep their names for assistive technology.
    Check(Svg(zoned).Descendants(ns+"g").Where(g=>(string?)g.Attribute("role")=="img").Select(g=>g.Attribute("aria-label")!.Value).Order().SequenceEqual(["High: above 2","Low: up to 2","Target: 4","Window: 1 to 2"]),"the references' names");
    Check(Datums(Svg(Pb()),0).Select(m=>m.Attribute("aria-label")!.Value).SequenceEqual(["5 km: Race 1, 24:10","5 km: Race 2, 23:52 · PB","5 km: Race 3, 24:05","5 km: Race 4, 23:31 · PB","5 km: Race 5, 23:40","5 km: Race 6, 22:47 · PB"]),"the names");
    // Without native tooltips the marks keep their names; an empty sparkline is an empty drawing named by its title.
    Check(Datums(XDocument.Parse(ChartSvg.Render(Pb(),includeTitles:false)),0).All(m=>m.Element(ns+"title") is null&&m.Attribute("aria-label") is not null),"without native tooltips");
    var empty=Svg(Pb() with{Series=[]});
    Check(!empty.Descendants(ns+"text").Any()&&empty.Root!.Elements(ns+"title").Single().Value==Pb().Title&&!Datums(empty,0).Any(),"an empty sparkline");
    // The same spec drawn as a chart has its title, axes and legend back. From 0.35.0 its title, 298 pixels by the library's estimate,
    // is cut at a word with an ellipsis to fit the 272 a drawing 320 wide leaves it.
    var full=Svg(Pb() with{Sparkline=false,Width=320,Height=240});
    Check(full.Descendants(ns+"text").Any(t=>t.Value=="5 km: 24:10 to 22:47 over 6…")&&full.Descendants(ns+"text").Any(t=>t.Value=="5 km")&&full.Root!.Attribute("style")!.Value.Contains(";width:100%;"),"a chart lost its words");
});
Test("A sparkline's plot fills its drawing but for a padding that holds its largest ring whole at all four edges",()=>{
    // Rings on the lowest point at the left, the highest, and a point at the right: each touches its edge exactly, on either axis
    // direction, whatever the other markers.
    var corners=new ChartSeries("S",[new(0,0){Highlight="#E30613"},new(1,10){Highlight="#E30613"},new(2,5),new(3,5){Highlight="#E30613"}]);
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Scatter})
        foreach(var reversed in new[]{false,true})
            foreach(var markers in Enum.GetValues<MarkerStyle>().Where(m=>kind==ChartKind.Line||m!=MarkerStyle.None))
                foreach(var classic in new[]{false,true})
                {
                    var spec=new ChartSpec{Title="S",Kind=kind,Width=120,Height=32,Sparkline=true,YReversed=reversed,Series=[corners with{Markers=markers}]};
                    var reach=Reach(Svg(classic?Classic(spec):spec));
                    Check(Near(reach.Left,0,1e-6)&&Near(reach.Top,0,1e-6)&&Near(reach.Right,120,1e-6)&&Near(reach.Bottom,32,1e-6),$"{kind} {markers} reversed {reversed} classic {classic}: {reach}");
                }
    // Without rings the padding is the largest marker: a filled or hovered one 4, a hollow one 5, a scatter dot 4.5 with its stroke, and
    // a line or an area with no markers half its stroke.
    var plain=corners with{Points=corners.Points.Select(p=>p with{Highlight=null}).ToArray()};
    foreach(var (kind,markers,stroke,pad) in new (ChartKind,MarkerStyle,double?,double)[]{(ChartKind.Line,MarkerStyle.Filled,null,4),(ChartKind.Line,MarkerStyle.Hollow,null,5),(ChartKind.Line,MarkerStyle.Auto,null,4),
        (ChartKind.Line,MarkerStyle.None,null,.8),(ChartKind.Line,MarkerStyle.None,3,1.5),(ChartKind.Area,MarkerStyle.None,null,.8),(ChartKind.Scatter,MarkerStyle.Auto,null,4.5),(ChartKind.Scatter,MarkerStyle.Filled,null,4),(ChartKind.Scatter,MarkerStyle.Hollow,null,5)})
    {
        var doc=Svg(new ChartSpec{Title="S",Kind=kind,Width=120,Height=32,Sparkline=true,Series=[plain with{Markers=markers,StrokeWidth=stroke}]});
        var circles=Datums(doc,0).Select(m=>m.Element(ns+"circle")!).ToArray();
        Check(Near(Attr(circles[0],"cx"),pad,1e-6)&&Near(Attr(circles[0],"cy"),32-pad,1e-6)&&Near(Attr(circles[1],"cy"),pad,1e-6)&&Near(Attr(circles[3],"cx"),120-pad,1e-6),$"{kind} {markers} {stroke}: the first point at {Attr(circles[0],"cx")}");
        if(markers!=MarkerStyle.None){var reach=Reach(doc);Check(Near(reach.Left,0,1e-6)&&Near(reach.Right,120,1e-6)&&Near(reach.Top,0,1e-6)&&Near(reach.Bottom,32,1e-6),$"{kind} {markers}: {reach}");}
    }
    // The classic finish's thicker line needs half its 2.5.
    Check(Near(Attr(Datums(Svg(Classic(new ChartSpec{Title="S",Kind=ChartKind.Line,Width=120,Height=32,Sparkline=true,Series=[plain with{Markers=MarkerStyle.None}]})),0)[0].Element(ns+"circle")!,"cx"),1.25,1e-6),"the classic stroke");
    // The latest point's soft ring reaches 10, so the plot stands 10 in.
    var last=Datums(Svg(new ChartSpec{Title="S",Kind=ChartKind.Line,Width=120,Height=32,Sparkline=true,Series=[plain with{HighlightLast=true}]}),0);
    var halo=last[^1].Elements(ns+"circle").First();
    Check(Attr(halo,"r")==10&&Near(Attr(halo,"cx")+10,120,1e-6)&&Near(Attr(last[0].Element(ns+"circle")!,"cx"),10,1e-6),"the latest point's ring");
    // Columns need none: the tallest reaches the top, every one stands on the bottom, and the first and last slots meet the sides.
    var bars=Datums(Svg(new ChartSpec{Title="S",Kind=ChartKind.Column,Width=120,Height=32,Sparkline=true,Series=[new("S",[new(0,2),new(1,8),new(2,4),new(3,6)])]}),0).Select(m=>m.Element(ns+"rect")!).ToArray();
    Check(Near(Attr(bars[1],"y"),0,1e-6)&&bars.All(b=>Near(Attr(b,"y")+Attr(b,"height"),32,1e-6))&&Near(Attr(bars[0],"x"),4.2,1e-6)&&Near(Attr(bars[3],"x")+Attr(bars[3],"width"),115.8,1e-6),"the columns");
    // A drawing too small to hold the ring on both sides keeps two units of plot: 60 by 16 stands it 7 in, from 7 to 9.
    var tiny=Datums(Svg(new ChartSpec{Title="S",Kind=ChartKind.Line,Width=60,Height=16,Sparkline=true,Series=[plain with{HighlightLast=true}]}),0).Select(m=>m.Elements(ns+"circle").Last()).ToArray();
    Check(Near(Attr(tiny[0],"cx"),7,1e-6)&&Near(Attr(tiny[0],"cy"),9,1e-6)&&Near(Attr(tiny[1],"cy"),7,1e-6)&&Near(Attr(tiny[3],"cx"),53,1e-6),"a drawing too small for its ring");
});
Test("A sparkline may be as small as 60 by 16, and only a sparkline; the largest drawing is the same for both",()=>{
    foreach(var (w,h) in new[]{(60,16),(120,32),(4096,2160),(60,2160),(4096,16)}) Check(!Svg(Pb() with{Width=w,Height=h}).Descendants(ns+"text").Any(),$"{w} by {h}");
    foreach(var (w,h) in new[]{(59,16),(60,15),(4097,32),(120,2161),(0,0),(-60,16)}) Check(Refused(Pb() with{Width=w,Height=h})=="A sparkline's dimensions must be 60–4096 by 16–2160.",$"{w} by {h}");
    foreach(var (w,h) in new[]{(60,16),(319,240),(320,239),(120,32)}) Check(Refused(Pb() with{Sparkline=false,Width=w,Height=h})=="Dimensions must be 320–4096 by 240–2160.",$"a chart {w} by {h}");
    Svg(Pb() with{Sparkline=false,Width=320,Height=240});
    // A graph keeps its own limits.
    Reject(()=>GraphEngine.Render(new GraphSpec{Width=120,Height=32}));
});
Test("A sparkline draws lines, areas, scatter points and columns, and refuses the other kinds, other marks, panes and value labels, each with its reason",()=>{
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        var spark=Sample(kind) with{Sparkline=true,Width=120,Height=32};
        if(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Column) Check(!Svg(spark).Descendants(ns+"text").Any(),$"{kind}");
        else Check(Refused(spark).StartsWith("A sparkline draws a line, an area, scatter points or columns, whose shape reads without axes"),$"{kind}: {Refused(spark)}");
    }
    foreach(var (own,point) in new[]{(ChartKind.Band,ChartPoint.Interval(0,2,1,3)),(ChartKind.Range,ChartPoint.Interval(0,2,1,3)),(ChartKind.Blocks,ChartPoint.Block(0,1,2))})
        Check(Refused(Spark() with{Series=[..Spark().Series,new("O",[point]){Kind=own}]}).StartsWith("A sparkline's series are drawn as lines, areas, scatter points or columns"),$"{own}");
    // Lines, columns, areas and scatter points may share one.
    Svg(Spark() with{Series=[..Spark().Series,new("C",[new(0,1),new(1,2),new(3,1)]){Kind=ChartKind.Column}]});
    Svg(Spark(ChartKind.Column) with{Series=[new("C",[new(0,3),new(1,5),new(3,4)]),new("L",[new(0,1),new(1,2),new(3,1)]){Kind=ChartKind.Line}]});
    Svg(Spark(ChartKind.Scatter) with{Series=[..Spark().Series,new("A",[new(0,1),new(1,2),new(3,1)]){Kind=ChartKind.Area}]});
    Check(Refused(Spark() with{Panes=[new()],Series=[Spark().Series[0],Spark().Series[1] with{Pane=1}]}).StartsWith("A sparkline is one small plot, so it takes no panes")
        &&Refused(Spark() with{Panes=[new()]}).StartsWith("A sparkline is one small plot"),"panes");
    Check(Refused(Spark() with{Series=[Spark().Series[0] with{ValueLabels=true}]}).StartsWith("A sparkline draws its data alone, so it writes no value labels")
        &&Refused(Spark(ChartKind.Column) with{Series=[Spark().Series[1] with{ValueLabels=true}]}).Contains("writes no value labels"),"value labels");
});
Test("A highlight rings its point in its colour whatever the markers, leaves the line its colour and course, and its note reaches the name and the tooltip",()=>{
    var light=ChartStyle.Light;var ink=light.SeriesColor(0);
    ChartPoint[] Ringed()=>[new(0,5,"R1"),new(1,3,"R2"){Highlight="#E30613",ValueNote=" · PB"},new(2,4,"R3"),new(3,2,"R4"){Highlight="#E30613",ValueNote=" · PB"},new(4,6,"R5")];
    foreach(var markers in Enum.GetValues<MarkerStyle>())
        foreach(var spark in new[]{false,true})
        {
            var spec=Raced(new("Time",Ringed()){Markers=markers});
            if(spark) spec=spec with{Sparkline=true,Width=120,Height=32,XMin=null,XMax=null,YMin=null,YMax=null};
            var doc=Svg(spec);var marks=Datums(doc,0);
            foreach(var i in new[]{1,3})
            {
                var circle=marks[i].Elements(ns+"circle").Single();
                Check((string?)circle.Attribute("fill")=="#E30613"&&circle.Attribute("r")!.Value=="5.5"&&circle.Attribute("class") is null&&circle.Attribute("fill-opacity") is null&&circle.Attribute("stroke") is null
                    &&(string?)marks[i].Attribute("stroke")==light.Background&&(string?)marks[i].Attribute("stroke-width")=="2",$"{markers} sparkline {spark}: {marks[i]}");
            }
            // The other points keep the series' markers, and the line is one stroke in the series colour along the same course as without rings.
            Check(marks.Where((_,i)=>i is 0 or 2 or 4).All(m=>(string?)m.Element(ns+"circle")!.Attribute("fill")!="#E30613"),$"{markers}: another point took the highlight");
            var strokes=doc.Descendants(ns+"path").Where(p=>(string?)p.Attribute("fill")=="none").ToArray();
            var plain=Svg(spec with{Series=[spec.Series[0] with{Points=Ringed().Select(p=>p with{Highlight=null}).ToArray()}]}).Descendants(ns+"path").Where(p=>(string?)p.Attribute("fill")=="none").ToArray();
            // A ring widens a sparkline's padding, so only a chart keeps the very same course.
            Check(strokes.Length==1&&(string?)strokes[0].Attribute("stroke")==ink&&(spark||strokes[0].ToString()==plain.Single().ToString()),$"{markers}: the line is {string.Join(",",strokes.Select(s=>s.Attribute("stroke")))}");
            Check(marks.Select(m=>m.Attribute("aria-label")!.Value).SequenceEqual(["Time: R1, 5","Time: R2, 3 · PB","Time: R3, 4","Time: R4, 2 · PB","Time: R5, 6"])&&marks.All(m=>m.Element(ns+"title")!.Value==m.Attribute("aria-label")!.Value),"the names");
        }
    // On a full chart the plot's clip reaches 7 past the plot where a pane rings a point, so a ring at its edge, 6.5 from its centre,
    // is drawn whole; without one it reaches 6, as before.
    Check(Attr(Svg(Raced(new("Time",Ringed()))).Root!.Element(ns+"svg")!,"x")==76-7&&Attr(Svg(Raced(new("Time",Placings()))).Root!.Element(ns+"svg")!,"x")==76-6,"the clip round a ring");
    // Scatter points are ringed the same; the latest point's ring takes the highlight; the classic finish rings with a stroke that scales.
    var dots=Datums(Svg(Raced(new("Time",Ringed()){Kind=ChartKind.Scatter})),0);
    Check(dots[1].Element(ns+"circle")!.Attribute("r")!.Value=="5.5"&&(string?)dots[1].Element(ns+"circle")!.Attribute("fill")=="#E30613"&&(string?)dots[0].Element(ns+"circle")!.Attribute("fill")==ink,"scatter points");
    var lastRing=Datums(Svg(Raced(new("Time",[..Ringed()[..4],new(4,1,"R5"){Highlight="#E30613"}]){HighlightLast=true})),0)[4].Elements(ns+"circle").ToArray();
    Check(lastRing.Length==2&&lastRing.All(c=>(string?)c.Attribute("fill")=="#E30613")&&lastRing[0].Attribute("r")!.Value=="10","the latest point's ring");
    var classic=Datums(Svg(Classic(Raced(new("Time",Ringed())))),0)[1];
    Check((string?)classic.Element(ns+"circle")!.Attribute("fill")=="#E30613"&&classic.Element(ns+"circle")!.Attribute("vector-effect") is null&&(string?)classic.Attribute("stroke")==light.Background,"the classic finish");
    // Change colours keep the segment that arrives and the words; a point's own colour still colours the segment that leaves it.
    var changed=Svg(Raced(new("Time",Ringed()){ChangeColors=ChangeColors.LowerIsBetter}));
    Check((string?)Datums(changed,0)[1].Element(ns+"circle")!.Attribute("fill")=="#E30613"&&Arrivals(changed)[(Math.Round(RaceX(1),6),Math.Round(RaceY(3),6))]==light.Rising
        &&Datums(changed,0)[1].Attribute("aria-label")!.Value=="Time: R2, 3 · PB, better than the previous","change colours");
    var coloured=Arrivals(Svg(Raced(new("Time",Ringed().Select((p,i)=>i==1?p with{Color="#123456"}:p).ToArray()))));
    Check(coloured[(Math.Round(RaceX(2),6),Math.Round(RaceY(4),6))]=="#123456","a point colour");
    // Sampling keeps a ringed point however long the run, and a value label stands clear of the larger ring.
    var run=Enumerable.Range(0,3000).Select(i=>new ChartPoint(i,Math.Round(Math.Sin(i/40d)*10+i%7,2)){Highlight=i==1501?"#E30613":null}).ToArray();
    var sampled=Datums(Svg(Spec() with{MaxRenderedPoints=16,Series=[new("S",run)]}),0);
    Check(sampled.Length<40&&sampled.Count(m=>m.Attribute("data-point")!.Value=="1501")==1&&(string?)sampled.Single(m=>m.Attribute("data-point")!.Value=="1501").Element(ns+"circle")!.Attribute("fill")=="#E30613",$"{sampled.Length} marks");
    var labels=Svg(Raced(new("Time",Ringed()){ValueLabels=true})).Descendants(ns+"g").Where(e=>(string?)e.Attribute("class")=="lumen-value").Select(g=>g.Elements(ns+"text").Last()).ToArray();
    Check(Near(Attr(labels[1],"y"),RaceY(3)-9.5,1e-6)&&Near(Attr(labels[0],"y"),RaceY(5)-8,1e-6),"a ringed point's label");
    // It is refused on every other mark, on a density scatter and in any colour but #RRGGBB.
    foreach(var kind in new[]{ChartKind.Area,ChartKind.Column,ChartKind.Bar,ChartKind.StackedColumn,ChartKind.Bubble,ChartKind.Band,ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar})
        Check(Refused(Spec(kind) with{Series=[new("S",[new(0,2,"A"),new(1,5,"B"){Highlight="#E30613"},new(2,3,"C")])]}).StartsWith("A highlight rings one point of a line or scatter series"),$"{kind}: {Refused(Spec(kind) with{Series=[new("S",[new(0,2,"A"),new(1,5,"B"){Highlight="#E30613"},new(2,3,"C")])]})}");
    foreach(var kind in new[]{ChartKind.Range,ChartKind.Blocks,ChartKind.Candlestick,ChartKind.Ohlc,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Timeline,ChartKind.Calendar,ChartKind.Gauge,ChartKind.Ring})
    {
        var sample=Sample(kind);
        Check(Refused(sample with{Series=sample.Series.Select((s,i)=>i>0?s:s with{Points=s.Points.Select((p,k)=>k>0?p:p with{Highlight="#E30613"}).ToArray()}).ToArray()}).StartsWith("A highlight rings"),$"{kind}");
    }
    Check(Refused(Spec() with{Series=[..Spec().Series,new("A",[new(0,1)]){Kind=ChartKind.Area,Points=[new(0,1){Highlight="#E30613"}]}]}).StartsWith("A highlight rings"),"an area series");
    Check(Refused(Spec(ChartKind.Scatter) with{DensityCells=10,Series=[new("S",[new(0,1){Highlight="#E30613"},new(1,2)])]}).Contains("rings no point with a highlight"),"a density scatter");
    Check(Refused(Spec() with{Series=[new("S",[new(0,1){Highlight="red"}])]})=="Colors must be #RRGGBB hex values."&&Refused(Spec() with{Series=[new("S",[new(0,1){Highlight="#E30613' onload='x"}])]})=="Colors must be #RRGGBB hex values.","a colour that is not #RRGGBB");
});
Test("YMinSpan centres a narrow axis on its data, leaves wider data alone, reverses, counts a band's edges, and sets a pane's axis apart from the main plot's",()=>{
    // On a 900 by 420 chart of one pane the plot runs from y 78 to 344, so a value v on an axis from lo to hi stands at 344 - (v - lo) / (hi - lo) × 266.
    double At(double v,double lo,double hi,bool reversed=false)=>reversed?78+(v-lo)/(hi-lo)*266:344-(v-lo)/(hi-lo)*266;
    double[] Ys(XDocument doc,int series=0)=>Datums(doc,series).Select(m=>Attr(m.Elements(ns+"circle").Last(),"cy")).ToArray();
    bool All(double[] ys,double[] values,double lo,double hi,bool reversed=false)=>ys.Length==values.Length&&ys.Zip(values).All(p=>Near(p.First,At(p.Second,lo,hi,reversed),1e-6));
    ChartSpec Weighed(params double[] kg)=>new(){Title="Weight",Kind=ChartKind.Line,YMinSpan=8,Series=[new("Weight",kg.Select((v,i)=>new ChartPoint(i,v)).ToArray()){Markers=MarkerStyle.Filled}]};
    // 37.8 to 38.2 is centred on 38: the axis runs from 38 - 4 = 34 to 38 + 4 = 42, and its end ticks say so.
    double[] kg=[37.9,37.8,38.2,38.1];
    var narrow=Svg(Weighed(kg));
    Check(All(Ys(narrow),kg,34,42),string.Join(",",Ys(narrow)));
    var ticks=narrow.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="end").Select(t=>t.Value).ToArray();
    Check(ticks.First()=="34"&&ticks.Last()=="42",string.Join(",",ticks));
    Check(All(Ys(Svg(Classic(Weighed(kg)))),kg,34,42),"the classic finish");
    // One weight stands in the middle of 34 to 42, not on the 5 % a single value is otherwise padded by.
    Check(All(Ys(Svg(Weighed(38))),[38],34,42)&&Near(Ys(Svg(Weighed(38)))[0],211,1e-6),"one weight");
    // Data as wide as the span or wider is fitted as it would be without one, to the byte.
    var wide=Weighed(30,45,38);
    Check(ChartSvg.Render(wide)==ChartSvg.Render(wide with{YMinSpan=null})&&ChartSvg.Render(wide with{YMinSpan=15})==ChartSvg.Render(wide with{YMinSpan=null}),"wider data moved");
    // Reversed, the lighter weights stand higher about the same middle.
    Check(All(Ys(Svg(Weighed(kg) with{YReversed=true})),kg,34,42,true),string.Join(",",Ys(Svg(Weighed(kg) with{YReversed=true}))));
    // A band's edges are the data too, and a series on the right-hand axis is not: 47 to 54 centres on 50.5, from 45.5 to 55.5.
    var banded=Svg(new ChartSpec{Title="B",Kind=ChartKind.Line,YMinSpan=10,Series=[new("Mid",[new(0,50),new(1,51)]){Markers=MarkerStyle.Filled},
        new("Band",[ChartPoint.Interval(0,50,47,53),ChartPoint.Interval(1,51,48,54)]){Kind=ChartKind.Band},new("Right",[new(0,1000),new(1,2000)]){Secondary=true}]});
    Check(All(Ys(banded),[50,51],45.5,55.5),string.Join(",",Ys(banded)));
    // A pane's span sets its own axis and leaves the main plot's alone. Two panes of weight 1 run from 78 to 199 and from 223 to 344.
    var paned=Svg(new ChartSpec{Title="P",Kind=ChartKind.Line,Panes=[new(){Weight=1,YMinSpan=8}],Series=[new("Main",[new(0,37.8),new(1,38.2)]){Markers=MarkerStyle.Filled},new("Pane",[new(0,37.8),new(1,38.2)]){Pane=1,Markers=MarkerStyle.Filled}]});
    Check(Near(Ys(paned,0)[0],199,1e-6)&&Near(Ys(paned,0)[1],78,1e-6)&&Near(Ys(paned,1)[0],344-3.8/8*121,1e-6)&&Near(Ys(paned,1)[1],344-4.2/8*121,1e-6),$"{string.Join(",",Ys(paned,0))} | {string.Join(",",Ys(paned,1))}");
    var mainOnly=Svg(new ChartSpec{Title="P",Kind=ChartKind.Line,YMinSpan=8,Panes=[new(){Weight=1}],Series=[new("Main",[new(0,37.8),new(1,38.2)]){Markers=MarkerStyle.Filled},new("Pane",[new(0,37.8),new(1,38.2)]){Pane=1,Markers=MarkerStyle.Filled}]});
    Check(Near(Ys(mainOnly,0)[0],199-3.8/8*121,1e-6)&&Near(Ys(mainOnly,1)[0],344,1e-6)&&Near(Ys(mainOnly,1)[1],223,1e-6),"the main plot's span reached the pane");
    // Every kind with such an axis takes it, blocks on a reversed pace axis among them, and a sparkline's axis is centred the same.
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Scatter,ChartKind.Bubble,ChartKind.Band,ChartKind.Range,ChartKind.Candlestick,ChartKind.Ohlc,ChartKind.Blocks}) Svg(Sample(kind) with{YMinSpan=8});
    Svg(new ChartSpec{Title="Laps",Kind=ChartKind.Blocks,YReversed=true,YFormat=ValueFormat.Duration,YMinSpan=60,Series=[new("Laps",[ChartPoint.Block(0,1,290),ChartPoint.Block(1,2,295)])]});
    var spark=Datums(Svg(Weighed(kg) with{Sparkline=true,Width=270,Height=54}),0).Select(m=>Attr(m.Element(ns+"circle")!,"cy")).ToArray();
    Check(spark.Zip(kg).All(p=>Near(p.First,50-(p.Second-34)/8*46,1e-6)),string.Join(",",spark));
    // In the component, hiding the main plot's series moves the pane up into its place with the pane's own bounds and no span.
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var moved=new ChartSpec{Title="P",Kind=ChartKind.Line,YMinSpan=8,Panes=[new(){YMin=0,YMax=100}],Series=[new("Main",[new(0,38),new(1,38.2)]),new("Pane",[new(0,10),new(1,20)]){Pane=1}]};
    ChartSpec? shown=null;
    Operate(moved,chart=>{typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[0]);shown=(ChartSpec)typeof(LumenChart).GetMethod("VisibleSpec",flags)!.Invoke(chart,[])!;return Task.CompletedTask;});
    Check(shown is {YMinSpan:null,YMin:0,YMax:100,Panes.Count:0},"the pane took the main plot's span");
});
Test("YMinSpan is refused beside YMin or YMax, out of range, on a logarithmic axis, on an axis that must include zero and where there is no such axis, each with its reason",()=>{
    var line=new ChartSpec{Title="W",Kind=ChartKind.Line,YMinSpan=8,Series=[new("W",[new(0,38),new(1,38.2)])]};
    foreach(var span in new[]{0,-8,double.NaN,double.PositiveInfinity,1e101}) Check(Refused(line with{YMinSpan=span}).StartsWith("YMinSpan is the least a Y axis spans, so it must be positive and finite"),$"{span}");
    Svg(line with{YMinSpan=1e100});Svg(line with{YMinSpan=1e-9});
    Check(Refused(line with{YMin=30}).StartsWith("YMinSpan centres an axis fitted to the data, and YMin or YMax")&&Refused(line with{YMax=50}).StartsWith("YMinSpan centres an axis fitted to the data"),"bounds");
    Check(Refused(line with{YAxis=AxisKind.Log}).StartsWith("YMinSpan centres a span of values on the data, and a logarithmic axis"),"a log axis");
    Check(Refused(line with{IncludeZero=true}).StartsWith("YMinSpan centres an axis on its data, and an axis that must include zero"),"IncludeZero");
    foreach(var kind in new[]{ChartKind.Area,ChartKind.Column,ChartKind.Bar,ChartKind.StackedColumn,ChartKind.Histogram})
        Check(Refused(Spec(kind) with{YMinSpan=8}).StartsWith("YMinSpan centres an axis on its data, and an axis that must include zero"),$"{kind}: {Refused(Spec(kind) with{YMinSpan=8})}");
    Check(Refused(line with{Series=[..line.Series,new("C",[new(0,1)]){Kind=ChartKind.Column}]}).Contains("must include zero")&&Refused(line with{Series=[..line.Series,new("A",[new(0,1),new(1,2)]){Kind=ChartKind.Area}]}).Contains("must include zero"),"a column or an area series");
    // Columns on the right-hand axis leave the left one free.
    Svg(line with{Series=[..line.Series,new("C",[new(0,1),new(1,2)]){Kind=ChartKind.Column,Secondary=true}]});
    foreach(var kind in new[]{ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Gauge,ChartKind.Ring,ChartKind.Timeline,ChartKind.Calendar,ChartKind.Box,ChartKind.Violin})
        Check(Refused(Sample(kind) with{YMinSpan=8}).StartsWith("YMinSpan widens a Y axis fitted to the data, so it applies to line, scatter"),$"{kind}: {Refused(Sample(kind) with{YMinSpan=8})}");
    // A pane meets the same rules on its own axis, and columns in another pane leave it alone.
    var paned=line with{YMinSpan=null,Panes=[new(){YMinSpan=8}],Series=[..line.Series,new("P",[new(0,38),new(1,38.1)]){Pane=1}]};
    Svg(paned);Svg(paned with{Series=[..paned.Series,new("C",[new(0,1),new(1,2)]){Kind=ChartKind.Column}]});
    Check(Refused(paned with{Panes=[new(){YMinSpan=8,YMin=0}]}).Contains("YMin or YMax")&&Refused(paned with{Panes=[new(){YMinSpan=8,YMax=50}]}).Contains("YMin or YMax")
        &&Refused(paned with{Panes=[new(){YMinSpan=8,YAxis=AxisKind.Log}]}).Contains("logarithmic")&&Refused(paned with{Panes=[new(){YMinSpan=-1}]}).Contains("positive")
        &&Refused(paned with{IncludeZero=true}).Contains("must include zero")&&Refused(paned with{Series=[..paned.Series,new("C",[new(0,1)]){Kind=ChartKind.Column,Pane=1}]}).Contains("must include zero"),"a pane");
});
Test("The component draws a sparkline as its drawing alone: no legend, toolbar, zoom, data table or scrolling region, and fitted down to 60 pixels",()=>{
    // From 0.36.0 the drawing is followed by the hidden words that name the arrow keys, which the script makes the drawing's description.
    var html=Prerender(ChartElement(Pb()));
    Check(html.StartsWith("<div class=\"lumen-chart lumen-spark\"><div class=\"lumen-viewport\"><svg ")&&System.Text.RegularExpressions.Regex.IsMatch(html,"</svg></div><span class=\"lumen-keys\" hidden>Arrow keys move between points[^<]*</span></div>$"),html[..Math.Min(200,html.Length)]);
    foreach(var chrome in new[]{"lumen-legend","lumen-tools","lumen-table","<button","role=\"region\"","tabindex=\"0\" role","Export","View data","lumen-status","Zoom"})
        Check(!html.Contains(chrome),$"the sparkline carries {chrome}");
    // Its drawing is the library's, without native tooltips, which the component draws itself.
    Check(html.Contains(ChartSvg.Render(Pb(),includeLegend:false,includeTitles:false)),"the drawing differs from the library's");
    Check(Prerender(ChartElement(Pb(),true)).StartsWith("<div class=\"lumen-chart lumen-spark lumen-fit\">"),"a fitted sparkline");
    // A fitted sparkline is drawn as narrow as 60 pixels where a chart stops at 320, and as wide as its box otherwise.
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    string Box(LumenChart chart)=>XDocument.Parse((string)typeof(LumenChart).GetField("svg",flags)!.GetValue(chart)!).Root!.Attribute("viewBox")!.Value;
    var boxes=new List<string>();
    Operate(Pb(),async chart=>{await chart.Fit(40);boxes.Add(Box(chart));await chart.Fit(200);boxes.Add(Box(chart));},fit:true);
    Operate(Spec(),async chart=>{await chart.Fit(40);boxes.Add(Box(chart));},fit:true);
    Check(boxes.SequenceEqual(["0 0 60 32","0 0 200 32","0 0 320 420"]),string.Join(" | ",boxes));
    // Selecting a mark still reads its point, with its note, though no status line shows it.
    var status="";
    Operate(Pb(),async chart=>{await chart.SelectPoint(0,5);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(status=="5 km: Race 6 = 22:47 · PB",status);
    var css=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Lumen.Charts.Blazor/wwwroot/lumen.css"));
    Check(css.Contains(".lumen-spark>.lumen-viewport>svg{min-width:0}")&&css.Contains(".lumen-spark>.lumen-tooltip{width:max-content}"),"the stylesheet's rules for a sparkline");
    // A chart that is not a sparkline renders as before.
    Check(!Prerender(ChartElement(Spec())).Contains("lumen-spark"),"a chart took the sparkline's class");
});
Test("Sparklines, highlights and minimum spans round-trip through JSON, a request that names none keeps the defaults, and only a set one renames gradients",()=>{
    var json=System.Text.Json.JsonSerializer.Serialize(Pb(),finishJson);
    Check(json.Contains("\"sparkline\":true")&&json.Contains("\"highlight\":\"#E30613\"")&&json.Contains("\"width\":120"),json);
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(back.Sparkline&&back.Series[0].Points[1].Highlight=="#E30613"&&ChartSvg.Render(back)==ChartSvg.Render(Pb()),"a sparkline changed in transit");
    var spanned=new ChartSpec{Title="W",Kind=ChartKind.Line,YMinSpan=8,Panes=[new(){YMinSpan=4}],Series=[new("A",[new(0,38),new(1,38.2)]),new("B",[new(0,10),new(1,10.5)]){Pane=1}]};
    var spanJson=System.Text.Json.JsonSerializer.Serialize(spanned,finishJson);
    Check(spanJson.Contains("\"yMinSpan\":8")&&spanJson.Contains("\"yMinSpan\":4")&&ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(spanJson,finishJson)!)==ChartSvg.Render(spanned),spanJson);
    // Written by hand, as the HTTP API takes it.
    var written="{\"title\":\"5 km\",\"kind\":\"Line\",\"width\":120,\"height\":32,\"sparkline\":true,\"yReversed\":true,\"yFormat\":\"Duration\",\"series\":[{\"name\":\"5 km\",\"points\":[{\"x\":0,\"y\":1450},{\"x\":1,\"y\":1367,\"highlight\":\"#E30613\",\"valueNote\":\" PB\"}]}]}";
    var svg=ChartSvg.Render(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,finishJson)!);
    Check(!svg.Contains("<text")&&svg.Contains("aria-label='5 km: 1, 22:47 PB'")&&svg.Contains("fill='#E30613'"),svg);
    var weight=Svg(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"title\":\"W\",\"kind\":\"Line\",\"yMinSpan\":8,\"series\":[{\"name\":\"W\",\"points\":[{\"x\":0,\"y\":37.8},{\"x\":1,\"y\":38.2}]}]}",finishJson)!);
    Check(weight.Descendants(ns+"text").Any(t=>t.Value=="34")&&weight.Descendants(ns+"text").Any(t=>t.Value=="42"),"a span written by hand");
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"panes\":[{}],\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]},{\"name\":\"T\",\"pane\":1,\"points\":[{\"x\":0,\"y\":1}]}]}",finishJson)!;
    Check(!old.Sparkline&&old.YMinSpan is null&&old.Panes[0].YMinSpan is null&&old.Series[0].Points[0].Highlight is null,"the defaults");
    // A gradient's ID is named after the spec: the defaults leave it alone, and a setting away from them names it afresh.
    string Id(ChartSpec s)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(s),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var graded=Spec() with{Series=[new("S",[new(0,1),new(1,3)]){Gradient=[new(0,"#000000"),new(5,"#FFFFFF")]}]};
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}]};
    Check(Id(graded).Length>0&&Id(faded).Length>0&&Id(faded with{Sparkline=false,YMinSpan=null})==Id(faded)
        &&Id(graded with{Series=[graded.Series[0] with{Points=graded.Series[0].Points.Select(p=>p with{Highlight=null}).ToArray()}]})==Id(graded),"the defaults renamed a gradient");
    // A sparkline draws its fade over a different plot, so two otherwise equal charts on one page must not share its ID.
    Check(Id(faded with{Sparkline=true})!=Id(faded)&&Id(graded with{YMinSpan=8})!=Id(graded)
        &&Id(graded with{Series=[graded.Series[0] with{Points=[new(0,1){Highlight="#E30613"},new(1,3)]}]})!=Id(graded),"a setting kept a gradient's name");
});
Test("0.33.0's renderings do not move: rows of its baseline rebuilt here match its hashes in both finishes, the latest point's ring and value labels on lines among them",()=>{
    // Hashes of v0.33.0's tests/Lumen.Charts.Baseline/reference files, refined and classic, recorded on Windows.
    if(!OperatingSystem.IsWindows())return;
    var line=Baseline(ChartKind.Line,ChartTheme.Light);
    (int? Position,int? Field,int? Points)[] races=[(31,50,40),(24,48,52),(27,51,47),(21,49,58),(19,52,61)];
    string[] raced=["11-04-2026","16-05-2026","04-07-2026","08-08-2026","19-09-2026"];
    DateOnly[] raceDays=[new(2026,4,11),new(2026,5,16),new(2026,7,4),new(2026,8,8),new(2026,9,19)];
    string[] roundNames=["Round 1 · Hilltop Classic","Round 2 · River Valley","Round 3 · Quarry Loop","Round 4 · Forest Sprint","Round 5 · Final Ridge"];
    ChartSeries Placed()=>new("Position",races.Select((r,i)=>new ChartPoint(i,r.Position,raced[i]){ValueNote=r.Field is int field?$"/{field}":null}).ToArray()){ChangeColors=ChangeColors.LowerIsBetter,ValueLabels=true,Markers=MarkerStyle.Filled};
    ChartSeries Earned()=>new("Points",races.Select((r,i)=>new ChartPoint(i,r.Points,raced[i])).ToArray()){ValueLabels=true,Markers=MarkerStyle.Filled};
    ChartPoint[] Zigzag()=>[new(0,5,"A"),new(1,3,"B"),new(2,3,"C"),new(3,4,"D"),new(4,null,"E"),new(5,2,"F"),new(6,6,"G"),new(7,1,"H")];
    (string Row,string Refined,string Classic,ChartSpec Spec)[] rows=[
        ("Line/Light/True","D991008D3106193D","CB7DF56598CE861F",line),
        ("pace-reversed","2A0753C00E774BFF","779AEF9057093620",line with{YFormat=ValueFormat.Duration,YReversed=true,Annotations=[new(AnnotationAxis.Y,300){Label="Target"}],
            Series=[new("Pace",Enumerable.Range(0,12).Select(i=>new ChartPoint(i,330-i*4+i%3*5)).ToArray()){Trend=true}]}),
        ("guard/line-markers","6A03DBA7B8A29EB8","0AB8C0AE5E4BFCDC",line with{Series=[new("A",Twelve().Select((p,i)=>p with{Color=i==3?"#123456":null,Y=i==7?null:p.Y}).ToArray()),
            new("B",Twelve()){Zones=new([new("Low",25),new("High",double.PositiveInfinity)]),ProjectedFrom=8}]}),
        // 0.35.0 sets this row's description, 309 pixels by the library's estimate in a drawing 340 wide, on two lines as nearly equal as
        // its words allow, and moves its plot down by one; it was C6189E3AF7D67240 and 95A655DDB4E9D931 before.
        ("race/recommended-340-midnight","E23F9462A56D6295","4DB7B28D805EE16E",new ChartSpec{Kind=ChartKind.Line,Style=ChartStyle.Midnight,Title="Position & points by race",Description="Place over field size, first at the top, and points",
            Width=340,Height=380,XMin=-.5,XMax=4.5,YReversed=true,YLabel="Position",Panes=[new(){Label="Points",Weight=1}],Series=[Placed(),Earned() with{Pane=1}]}),
        ("race/season-340-midnight","D19CDFC27AE78F1A","FC505B592334AF49",new ChartSpec{Kind=ChartKind.Line,Style=ChartStyle.Midnight,XAxis=AxisKind.Time,TimeZone="Africa/Johannesburg",YReversed=true,
            Title="Your season, round by round",Description="Your place in each round, first at the top",Width=340,Height=260,
            Series=[new("Position",races.Select((r,i)=>new ChartPoint(TimeAxis.Value(new DateTimeOffset(raceDays[i].ToDateTime(new TimeOnly(12,0)),TimeSpan.FromHours(2))),r.Position,roundNames[i])).ToArray(),"#FF5A54"){Markers=MarkerStyle.Filled,HighlightLast=true}]}),
        ("change/line/Light","EB08EBF8AB376732","D669DED1E5B7A8B5",line with{Title="Change colours",Series=[new("Position",Zigzag()){ChangeColors=ChangeColors.LowerIsBetter},
            new("Points",Zigzag().Select(p=>p with{Y=10-p.Y}).ToArray()){ChangeColors=ChangeColors.HigherIsBetter,Curve=LineCurve.Step}]}),
        ("labels/edges","56F1C0E042038E5A","A8935C09ECF85C13",line with{Title="Value labels at the edges",XMin=0,XMax=6,YMin=0,YMax=10,
            Series=[new("A",[new(0,5){ValueNote="/48"},new(3,4),new(6,10){ValueNote="/52"}]){ValueLabels=true,Markers=MarkerStyle.Filled},new("B",[new(1,6),new(3,4.2),new(5,7)]){Kind=ChartKind.Scatter,ValueLabels=true}]})];
    foreach(var (row,refined,classic,spec) in rows)
    {
        Check(Hash16(ChartSvg.Render(spec))==refined,$"{row} moved: {Hash16(ChartSvg.Render(spec))}");
        Check(Hash16(ChartSvg.Render(Classic(spec)))==classic,$"{row} moved in the classic finish: {Hash16(ChartSvg.Render(Classic(spec)))}");
        // Each new setting written out at its default draws the same chart.
        var spelled=spec with{Sparkline=false,YMinSpan=null,Panes=spec.Panes.Select(p=>p with{YMinSpan=null}).ToArray(),Series=spec.Series.Select(s=>s with{Points=s.Points.Select(p=>p with{Highlight=null}).ToArray()}).ToArray()};
        Check(ChartSvg.Render(spelled)==ChartSvg.Render(spec),$"{row}: the defaults written out moved it");
    }
});
Test("Sports page: Getting faster? rings each time faster than all before it, from the runs the records chart draws, and holds the weigh-ins at an 8 kg scale in grey alone",()=>{
    var card=sports.Single(c=>c.Id=="getting-faster");var lines=card.Lines!;
    var order=sports.Select(c=>c.Id).ToList();
    Check(card.Section=="fitness"&&card.Wide&&lines.Count==3&&card.Spec==lines[0].Spec&&order.IndexOf("getting-faster")==order.IndexOf("records")+1&&order.IndexOf("hrv")==order.IndexOf("getting-faster")+1,"the card or its place");
    // The 5 km line is the records chart's weekly fastest 5 km in order, and the 1 km line the fastest kilometre of each session of repeats.
    var five=lines[0].Spec.Series.Single().Points;var one=lines[1].Spec.Series.Single().Points;
    var repeats=athlete.Sessions.Where(s=>s.Name.EndsWith("repeats")).ToArray();
    Check(five.Select(p=>p.Y!.Value).SequenceEqual(Sports("records").Series[1].Points.Select(p=>p.Y!.Value))&&five.Select(p=>p.X).SequenceEqual(Enumerable.Range(0,five.Count).Select(i=>(double)i)),"the 5 km line");
    Check(one.Count==repeats.Length&&repeats.Length>=10&&one.Select(p=>p.Y!.Value).SequenceEqual(repeats.Select(s=>SportsData.Fastest(s.Track!,s.Metres,1000)!.Value)),"the 1 km line");
    foreach(var line in lines.Take(2))
    {
        var points=line.Spec.Series[0].Points;
        // A best is faster than every time before it, the first only setting the mark; only a best is ringed, and its name says why.
        for(var i=0;i<points.Count;i++)
        {
            var best=i>0&&points.Take(i).All(p=>points[i].Y<p.Y);
            Check((points[i].Highlight is not null)==best&&(points[i].ValueNote==" · PB")==best,$"{line.Name} point {i}");
        }
        Check(line.Spec is {Sparkline:true,YReversed:true,YFormat:ValueFormat.Duration,Width:120,Height:32},$"{line.Name}: the sparkline");
        var unit=line.Name=="5 km"?"weeks":"sessions";
        Check(line.Spec.Title==$"{line.Name}: {SportsData.Clock(points[0].Y!.Value)} to {SportsData.Clock(points[^1].Y!.Value)} over {points.Count} {unit}","the title");
        Check(line.Text.Contains($"best {SportsData.Clock(points.Min(p=>p.Y!.Value))}")&&line.Text.Contains($"{points.Count(p=>p.Highlight is not null)} personal bests"),line.Text);
        Check(Datums(Svg(line.Spec),0).Where((_,i)=>points[i].Highlight is not null).All(m=>m.Attribute("aria-label")!.Value.EndsWith(" · PB")),"a best's name");
    }
    Check(five.Count(p=>p.Highlight is not null)==4&&one.Count(p=>p.Highlight is not null)==6,"the counts of bests");
    // The weigh-ins: invented, in one colour, ringed nowhere, on an axis at least 8 kg tall centred on them. The lightest, 67.7, and the
    // heaviest, 68.3, centre it on 68.0, so it runs from 64 to 72, and on a 270 by 54 drawing standing 4 in a weight w stands at 50 - (w - 64) / 8 × 46.
    var mass=lines[2];var kg=mass.Spec.Series.Single().Points;
    Check(mass.Spec is {YMinSpan:8,Width:270,Height:54,Sparkline:true}&&kg.Select(p=>p.Y!.Value).SequenceEqual(SportsData.WeighIns)&&kg.All(p=>p.Highlight is null&&p.Color is null)&&mass.Text.EndsWith("scale 64–72 kg"),mass.Text);
    Check(Datums(Svg(mass.Spec),0).Select(m=>Attr(m.Element(ns+"circle")!,"cy")).Zip(SportsData.WeighIns).All(p=>Near(p.First,50-(p.Second-64)/8*46,1e-6)),"the weights' heights");
    // Every brand's line and rings clear 3:1 on its background, and none writes a word.
    foreach(var (style,zones) in new[]{(ChartStyle.Light,ChartStyle.Light.Zones),(ChartStyle.Dark,ChartStyle.Light.Zones),(Brand(),Brand().Zones),(ChartStyle.Midnight,ChartStyle.Midnight.Zones)})
        foreach(var line in SportsData.Cards(ChartTheme.Light,zones).Single(c=>c.Id=="getting-faster").Lines!)
        {
            var inks=line.Spec.Series.Select(s=>s.Color!).Concat(line.Spec.Series.SelectMany(s=>s.Points).Select(p=>p.Highlight).OfType<string>()).Distinct().ToArray();
            Check(inks.All(ink=>Contrast(ink,style.Background)>=3),$"{line.Name} on {style.Background}: {string.Join(", ",inks.Where(ink=>Contrast(ink,style.Background)<3))}");
            Check(!Svg(line.Spec with{Style=style}).Descendants(ns+"text").Any(),$"{line.Name} wrote a word");
        }
    // The page draws each at its own size beside its words, and its sparklines and charts number twenty-four.
    Check(sports.Sum(c=>c.Lines?.Count??(c.Beside is null?1:2))==31,"the page's count");
});
// 0.35.0: how the field finished, and text that fits. Blocks keep a visible height; an annotation can draw its label without its value
// and stand over the data; an X axis chooses which of its labels it writes, and either axis can label just its two ends; and a chart's
// title, description and source fit its width. A 900 by 420 chart's plot runs from x 76 to 870 and y 78 to 344; its X labels stand on
// the baseline 21 below the plot and its Y labels end 12 left of it, 4 below their value's height. Every example is invented.
ChartSpec Finish(params (double From,double To,int Count)[] bins)=>new(){Title="How the field finished",Kind=ChartKind.Blocks,IncludeZero=true,XFormat=ValueFormat.Duration,
    Series=[new("Finishers",bins.Select(b=>ChartPoint.Block(b.From,b.To,b.Count)).ToArray())]};
// An invented race of 312 finishers in five-minute bins from 35:00 to 1:20:00, three faster and twelve slower off the chart, 88 the most in a bin.
ChartSpec Binned()=>Finish((2100,2400,18),(2400,2700,64),(2700,3000,88),(3000,3300,57),(3300,3600,33),(3600,3900,18),(3900,4200,10),(4200,4500,8),(4500,4800,1));
// The labels along the bottom of a plot whose bottom is at the given height, and the labels up its left side.
(string Text,double X,string Anchor)[] Along(XDocument doc,double bottom=344)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-muted"&&t.Attribute("transform") is null&&Attr(t,"y")==bottom+21)
    .Select(t=>(t.Value,Attr(t,"x"),(string)t.Attribute("text-anchor")!)).ToArray();
(string Text,double Y)[] Upward(XDocument doc,double x=64)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-muted"&&(string?)t.Attribute("text-anchor")=="end"&&Attr(t,"x")==x)
    .Select(t=>(t.Value,Attr(t,"y"))).ToArray();
// A reference's label as drawn, over the data in the refined finish and inside the reference's group in the classic one, and the references' names.
string[] Printed(XDocument doc)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("paint-order")=="stroke"||(string?)t.Parent!.Attribute("role")=="img").Select(t=>t.Value).OrderBy(t=>t,StringComparer.Ordinal).ToArray();
string[] Voiced(XDocument doc)=>doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("role")=="img").Select(g=>g.Attribute("aria-label")!.Value).OrderBy(t=>t,StringComparer.Ordinal).ToArray();
// The description's lines as drawn, 11 px and muted from x 24 above the plot, and the top of the first plot, six inside its clip.
string[] Said(XDocument doc)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("font-size")=="11"&&(string?)t.Attribute("class")=="lumen-muted"&&Attr(t,"x")==24&&Attr(t,"y")<70).Select(t=>t.Value).ToArray();
double Top35(XDocument doc)=>Attr(doc.Root!.Element(ns+"svg")!,"y")+6;
Test("Blocks keep 2 pixels: one finisher among 300 stands 2 pixels tall where it would be 0.89, a bin of none draws nothing visible and stays named and focusable, and taller blocks keep their heights",()=>{
    // 299, 1, 0 and 3 on an axis from 0 to 299, which IncludeZero holds exactly: one would stand 266 / 299 = 0.89 pixels tall, and three 2.67.
    var spec=Finish((2100,2400,299),(2400,2700,1),(2700,3000,0),(3000,3300,3));
    foreach(var classic in new[]{false,true})
    {
        var doc=Svg(classic?Classic(spec):spec);var boxes=BlockPaths(doc).Select(BlockBox).ToArray();
        Check(boxes.Length==4&&boxes.All(b=>Close(b.Bottom,344))&&Close(boxes[0].Top,78)&&Close(boxes[1].Top,342)&&Close(boxes[2].Top,344)&&Close(boxes[3].Top,344-3*266/299d),
            $"classic {classic}: {string.Join(" | ",boxes.Select(b=>$"{b.Top}-{b.Bottom}"))}");
        var marks=Datums(doc,0);
        Check(marks.All(m=>(string?)m.Attribute("tabindex")=="0"&&m.Element(ns+"title")!.Value==m.Attribute("aria-label")!.Value)
            &&marks.Select(m=>m.Attribute("aria-label")!.Value).SequenceEqual(["Finishers: 35:00 to 40:00, 299","Finishers: 40:00 to 45:00, 1","Finishers: 45:00 to 50:00, 0","Finishers: 50:00 to 55:00, 3"]),"the names");
    }
    // On a reversed pace axis held from 4:00 to 5:00 a lap at 5:00 stands at the bottom and draws nothing, and one at 4:59.9 keeps 2 pixels.
    var laps=BlockPaths(Svg(Laps(300,299.9,270) with{YMin=240,YMax=300})).Select(BlockBox).ToArray();
    Check(Close(laps[0].Top,344)&&Close(laps[1].Top,342)&&Close(laps[2].Top,211),string.Join(" | ",laps.Select(b=>$"{b.Top}-{b.Bottom}")));
});
Test("ShowValue = false draws an annotation's label alone, while its tooltip and accessible name keep the label and the value, in both finishes and on a gauge, and the label takes the room of its own words",()=>{
    var spec=Binned() with{Annotations=[new(AnnotationAxis.X,2832){Label="median",ShowValue=false},new(AnnotationAxis.Y,50){Label="Target"}]};
    foreach(var classic in new[]{false,true})
    {
        var doc=Svg(classic?Classic(spec):spec);
        Check(Printed(doc).SequenceEqual(["Target: 50","median"]),$"classic {classic}: {string.Join(", ",Printed(doc))}");
        Check(Voiced(doc).SequenceEqual(["Target: 50","median: 47:12"])&&doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("role")=="img").All(g=>g.Element(ns+"title")!.Value==g.Attribute("aria-label")!.Value),
            $"classic {classic}: {string.Join(", ",Voiced(doc))}");
    }
    // Shown, the value follows the label, as before.
    Check(Printed(Svg(spec with{Annotations=[spec.Annotations[0] with{ShowValue=true}]})).SequenceEqual(["median: 47:12"]),"a label with its value");
    // A gauge's target is drawn as its label alone and named with its value.
    var gauge=Svg(Sample(ChartKind.Gauge) with{Annotations=[new(AnnotationAxis.Y,60){Label="Average",ShowValue=false}]});
    Check(Printed(gauge).SequenceEqual(["Average"])&&Voiced(gauge).SequenceEqual(["Average: 60"]),$"{string.Join(", ",Printed(gauge))} | {string.Join(", ",Voiced(gauge))}");
    // A band 0.8 wide on an axis of 10, 63.5 pixels, has room inside it for "Window", 47 pixels by the generous estimate and 6 at each
    // side, but not for "Window: 4 to 4.8", which is left out where the band shows its value.
    var band=Spec() with{XMin=0,XMax=10,Annotations=[new(AnnotationAxis.X,4){To=4.8,Label="Window"}]};
    Check(Printed(Svg(band)).Length==0&&Printed(Svg(band with{Annotations=[band.Annotations[0] with{ShowValue=false}]})).SequenceEqual(["Window"]),"the room a label takes");
});
Test("InFront draws a reference over the data instead of behind it, in both finishes, through every pane and on a timeline, a line on a halo of the background colour, the reference otherwise unchanged and its label still over the data",()=>{
    int Index35(XDocument doc,Func<XElement,bool> found)=>doc.Descendants().ToList().FindIndex(e=>found(e));
    int Last35(XDocument doc,Func<XElement,bool> found)=>doc.Descendants().ToList().FindLastIndex(e=>found(e));
    bool IsMark(XElement e)=>e.Attribute("data-point") is not null;
    Func<XElement,bool> Refers(string name)=>e=>(string?)e.Attribute("role")=="img"&&(string?)e.Attribute("aria-label")==name;
    var target=new ChartAnnotation(AnnotationAxis.Y,20){Label="Target"};
    var columns=Baseline(ChartKind.Column,ChartTheme.Light);
    foreach(var classic in new[]{false,true})
    {
        XDocument Behind(ChartAnnotation a)=>Svg(classic?Classic(columns with{Annotations=[a]}):columns with{Annotations=[a]});
        var (behind,front)=(Behind(target),Behind(target with{InFront=true}));
        Check(Index35(behind,Refers("Target: 20"))<Index35(behind,IsMark)&&Index35(front,Refers("Target: 20"))>Last35(front,IsMark),$"classic {classic}: the order");
        // In front, the line stands on a halo of the background colour 2 wider than itself, so it shows over a bar of any colour; the
        // reference is otherwise the one drawn behind.
        var (inFront,behindIt)=(new XElement(front.Descendants().Single(Refers("Target: 20"))),behind.Descendants().Single(Refers("Target: 20")));
        var halo=inFront.Elements(ns+"line").Where(l=>(string?)l.Attribute("stroke")==ChartStyle.Light.Background).ToArray();
        Check(halo.Length==1&&(string?)halo[0].Attribute("stroke-width")==(classic?"3.5":"3")&&!behindIt.Elements(ns+"line").Any(l=>(string?)l.Attribute("stroke")==ChartStyle.Light.Background),$"classic {classic}: the halo");
        halo[0].Remove();
        Check(inFront.ToString()==behindIt.ToString(),$"classic {classic}: the reference itself changed");
    }
    var labelled=Svg(columns with{Annotations=[target with{InFront=true}]});
    Check(Index35(labelled,e=>(string?)e.Attribute("paint-order")=="stroke"&&e.Value=="Target: 20")>Index35(labelled,Refers("Target: 20")),"the label is not over the reference");
    // An X line through two panes stands over the blocks of each, drawn alone in the lower one.
    var paned=new ChartSpec{Title="Panes",Kind=ChartKind.Line,Panes=[new()],Series=[new("A",[ChartPoint.Block(0,1,3),ChartPoint.Block(1,2,5)]){Kind=ChartKind.Blocks},
        new("B",[ChartPoint.Block(0,1,4),ChartPoint.Block(1,2,2)]){Kind=ChartKind.Blocks,Pane=1}],Annotations=[new(AnnotationAxis.X,1.5){Label="Now",InFront=true}]};
    var plots=Svg(paned).Root!.Elements(ns+"svg").Select(p=>p.Descendants().ToList()).ToArray();
    Check(plots.Length==2&&plots.All(p=>p.FindIndex(e=>e.Name==ns+"line"&&(string?)e.Attribute("stroke-opacity")=="0")>p.FindLastIndex(e=>IsMark(e))),"the panes");
    var timeline=Svg(Sample(ChartKind.Timeline) with{Annotations=[new(AnnotationAxis.X,30){Label="Alarm",InFront=true}]});
    Check(Index35(timeline,Refers("Alarm: 30"))>Last35(timeline,IsMark),"the timeline");
});
Test("TickLabels.Bounds labels only an axis's two ends at their exact values in its format, along X and up Y, with the gridlines where they were; Ends keeps the first and last drawn",()=>{
    var spec=Binned() with{XTickLabels=TickLabels.Bounds,YTickLabels=TickLabels.Bounds};
    foreach(var classic in new[]{false,true})
    {
        var doc=Svg(classic?Classic(spec):spec);var all=Svg(classic?Classic(Binned()):Binned());
        // The first bin's start begins at the plot's left edge and the last bin's end ends at its right; 0 and the most, 88, stand at the
        // bottom and the top.
        Check(Along(doc).SequenceEqual([("35:00",76d,"start"),("1:20:00",870d,"end")]),$"classic {classic}: {string.Join(" | ",Along(doc))}");
        Check(Upward(doc).SequenceEqual([("0",348d),("88",82d)]),$"classic {classic}: {string.Join(" | ",Upward(doc))}");
        Check(Grid(doc).SequenceEqual(Grid(all))&&Grid(doc).Length>2&&Along(all).Length>2&&Upward(all).Length>2,$"classic {classic}: the gridlines moved");
    }
    // Set bounds are the ends written, wherever the ticks fall.
    Check(Along(Svg(spec with{XMin=2000,XMax=5000})).Select(l=>l.Text).SequenceEqual(["33:20","1:23:20"]),"set bounds");
    // Ends writes the first and the last of the labels the axis would draw, its ticks or its points' labels.
    var every=Along(Svg(Binned()));
    Check(Along(Svg(Binned() with{XTickLabels=TickLabels.Ends})).SequenceEqual([every[0],every[^1]])&&every.Length>2,string.Join(" | ",every));
    Check(Along(Svg(Spec() with{XTickLabels=TickLabels.Ends})).Select(l=>l.Text).SequenceEqual(["A","C"])&&Along(Svg(Spec())).Length==3,"point labels at the ends");
    // Bounds writes the axis's own ends, never the points' labels.
    Check(Along(Svg(Spec() with{XTickLabels=TickLabels.Bounds})).SequenceEqual([("0",76d,"start"),("2",870d,"end")]),"point labels at the bounds");
    // A reversed axis has its smaller end at the top.
    var paces=Upward(Svg(Laps(330,310,290) with{YMin=270,YMax=345,YTickLabels=TickLabels.Bounds}));
    Check(paces.SequenceEqual([("4:30",82d),("5:45",348d)]),string.Join(" | ",paces));
    // A horizontal bar chart's value axis runs along the bottom, 20 under the plot, from 160 to 870.
    var bars=Svg(Spec(ChartKind.Bar) with{YTickLabels=TickLabels.Bounds}).Descendants(ns+"text").Where(t=>Attr(t,"y")==364).Select(t=>(t.Value,Attr(t,"x"),(string)t.Attribute("text-anchor")!)).ToArray();
    Check(bars.SequenceEqual([("0",160d,"start"),("5",870d,"end")]),string.Join(" | ",bars));
    // A timeline's axis is labelled the same, from its lanes' plot.
    var lanes=Svg(Sample(ChartKind.Timeline) with{XTickLabels=TickLabels.Bounds});
    Check(Along(lanes).SequenceEqual([("0",Attr(lanes.Root!.Element(ns+"svg")!,"x")+6,"start"),("65",870d,"end")]),string.Join(" | ",Along(lanes)));
});
Test("A description too wide for a 340-pixel drawing goes on over a second line, at a clause where both lines fit and otherwise between the words that set the lines most nearly equal, and moves every kind's body down 14 pixels; past two lines it ends in an ellipsis, the whole kept in its desc and name",()=>{
    var spec=new ChartSpec{Title="Field",Width=340,Height=240,Kind=ChartKind.Line,Series=[new("S",[new(0,1),new(1,3),new(2,2)])]};
    var two="Every finisher's time in five-minute bins from the 1st to the 99th percentile";
    var three=two+", the reader's own in race red and the median a dashed line over the bins";
    foreach(var classic in new[]{false,true})
    {
        XDocument Described(string description)=>Svg(classic?Classic(spec with{Description=description}):spec with{Description=description});
        var one=Described("312 finishers · median 47:12");
        Check(Said(one).SequenceEqual(["312 finishers · median 47:12"])&&Top35(one)==78,$"classic {classic}: one line");
        // The two lines fit 340 less 48 by the generous estimate, broken between the words that set them most nearly equal: a description
        // just too wide for one line is not left with one word on the second.
        foreach(var description in new[]{two,"Place over field size, first at the top, and points"})
        {
            var split=Said(Described(description));var parts=description.Split(' ');
            var evenest=Enumerable.Range(1,parts.Length-1).Min(k=>Math.Max(Wide11(string.Join(" ",parts[..k])),Wide11(string.Join(" ",parts[k..]))));
            Check(split.Length==2&&string.Join(" ",split)==description&&split.All(l=>Wide11(l)<=292)&&Math.Max(Wide11(split[0]),Wide11(split[1]))==evenest,$"classic {classic}: {string.Join(" / ",split)}");
        }
        var wrapped=Described(two);var lines=Said(wrapped);
        Check(Top35(wrapped)==92,$"classic {classic}: the plot's top");
        Check(wrapped.Descendants(ns+"text").Where(t=>lines.Contains(t.Value)).Select(t=>Attr(t,"y")).SequenceEqual([49d,63d]),"the lines' baselines");
        // Too long for two, the first line takes as many words as fit and the second is cut at a word.
        var cut=Described(three);var said=Said(cut);
        Check(said.Length==2&&Wide11(said[0]+" "+three[(said[0].Length+1)..].Split(' ')[0])>292&&said[1].EndsWith("…")&&said.All(l=>Wide11(l)<=292)&&three.StartsWith(said[0]+" "+said[1][..^1])&&Top35(cut)==92,$"classic {classic}: {string.Join(" / ",said)}");
        Check(cut.Root!.Element(ns+"desc")!.Value==three&&cut.Root!.Attribute("aria-label")!.Value=="Field. "+three,"the whole description");
    }
    // A description of clauses breaks between them where both lines then fit.
    Check(Said(Svg(spec with{Description="Five invented races in two panes · best 19th of 52 riders"})).SequenceEqual(["Five invented races in two panes","best 19th of 52 riders"]),"the clauses");
    // Every kind's body moves down: no word but the title and the description's lines stands above 76 at a phone's width.
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        var doc=Svg(Sample(kind) with{Width=340,Height=300,Description=two});
        var high=doc.Descendants(ns+"text").Where(t=>t.Attribute("transform") is null&&t.Ancestors().All(a=>a.Attribute("transform") is null)&&!(Attr(t,"x")==24&&Attr(t,"y") is 28 or 49 or 63)&&Attr(t,"y")<76).Select(t=>t.Value).ToArray();
        Check(Said(doc).Length==2&&high.Length==0,$"{kind}: {string.Join(", ",high)}");
    }
    // Plots, bins, lanes, a donut's key, a heatmap's rows, a calendar's days, a gauge, rings, a radar and boxes stand lower.
    XDocument Kind35(ChartKind kind,bool wide)=>Svg(Sample(kind) with{Width=340,Height=300,Description=wide?two:"Short"});
    double Tallest(XDocument doc)=>doc.Descendants(ns+"rect").Where(r=>(string?)r.Parent!.Attribute("role")=="img").Min(r=>Attr(r,"y"));
    double Key(XDocument doc)=>Attr(doc.Root!.Elements(ns+"circle").First(),"cy");
    double Cell35(XDocument doc,ChartKind kind)=>Attr(Datums(doc,0)[0].Element(ns+"rect")!,"y");
    double Centre(XDocument doc)=>double.Parse(doc.Descendants(ns+"g").First(g=>g.Attribute("transform") is not null).Attribute("transform")!.Value.Split(' ',')')[1],CultureInfo.InvariantCulture);
    double Polygon(XDocument doc)=>doc.Descendants(ns+"polygon").First().Attribute("points")!.Value.Split(' ').Min(p=>double.Parse(p.Split(',')[1],CultureInfo.InvariantCulture));
    Check(Top35(Kind35(ChartKind.Line,true))==92&&Top35(Kind35(ChartKind.Line,false))==78&&Top35(Kind35(ChartKind.Timeline,true))==92&&Top35(Kind35(ChartKind.Blocks,true))==92,"plots and lanes");
    Check(Tallest(Kind35(ChartKind.Histogram,true))==92&&Tallest(Kind35(ChartKind.Histogram,false))==78,"bins");
    Check(Key(Kind35(ChartKind.Donut,true))-Key(Kind35(ChartKind.Donut,false))==14&&Cell35(Kind35(ChartKind.Heatmap,true),ChartKind.Heatmap)-Cell35(Kind35(ChartKind.Heatmap,false),ChartKind.Heatmap)==14,"a donut's key and a heatmap's rows");
    Check(Cell35(Kind35(ChartKind.Calendar,true),ChartKind.Calendar)>Cell35(Kind35(ChartKind.Calendar,false),ChartKind.Calendar)&&Centre(Kind35(ChartKind.Gauge,true))>Centre(Kind35(ChartKind.Gauge,false))
        &&Centre(Kind35(ChartKind.Ring,true))>Centre(Kind35(ChartKind.Ring,false))&&Polygon(Kind35(ChartKind.Radar,true))>Polygon(Kind35(ChartKind.Radar,false))
        &&Grid(Kind35(ChartKind.Box,true)).Min()>Grid(Kind35(ChartKind.Box,false)).Min()&&Grid(Kind35(ChartKind.Violin,true)).Min()>Grid(Kind35(ChartKind.Violin,false)).Min(),"the other kinds");
});
Test("A source too wide for its drawing goes on over a second line that grows upward, and moves the plot's bottom, its X axis and the axis's title up 14 pixels",()=>{
    var spec=new ChartSpec{Title="Field",Width=340,Height=240,Kind=ChartKind.Line,XLabel="Finish time",Series=[new("S",[new(0,1),new(1,3),new(2,2)])]};
    (string Text,double Y)[] Under(XDocument doc)=>doc.Descendants(ns+"text").Where(t=>Attr(t,"x")==24&&(string?)t.Attribute("font-size")=="11"&&Attr(t,"y")>200).Select(t=>(t.Value,Attr(t,"y"))).ToArray();
    double Bottom35(XDocument doc){var clip=doc.Root!.Element(ns+"svg")!;return Attr(clip,"y")+Attr(clip,"height")-6;}
    foreach(var classic in new[]{false,true})
    {
        XDocument Sourced(string source)=>Svg(classic?Classic(spec with{Source=source}):spec with{Source=source});
        var one=Sourced("Off the chart: 3 faster and 12 slower");
        var two=Sourced("Off the chart: 3 faster and 12 slower · counted from the race's 312 finishers");
        Check(Under(one).SequenceEqual([("Off the chart: 3 faster and 12 slower",228d)])&&Bottom35(one)==164,$"classic {classic}: {string.Join(" | ",Under(one))}");
        Check(Under(two).SequenceEqual([("Off the chart: 3 faster and 12 slower",214d),("counted from the race's 312 finishers",228d)])&&Bottom35(two)==150,$"classic {classic}: {string.Join(" | ",Under(two))}");
        // The X axis's labels and its title rise with the plot's bottom.
        Check(Along(one,164).Length>1&&Along(two,150).Select(l=>l.Text).SequenceEqual(Along(one,164).Select(l=>l.Text))
            &&one.Descendants(ns+"text").Any(t=>t.Value=="Finish time"&&Attr(t,"y")==208)&&two.Descendants(ns+"text").Any(t=>t.Value=="Finish time"&&Attr(t,"y")==194),$"classic {classic}: the axis");
    }
});
Test("A title wider than its drawing stays one line, cut at a word with an ellipsis, its whole kept in its title element and accessible name; one that fits is drawn as given, on graphs too",()=>{
    var title="Nineteenth of fifty-two riders in the final round, eleven places better than the first";
    string Heading(XDocument doc)=>doc.Descendants(ns+"text").Single(t=>(string?)t.Attribute("font-size")=="17").Value;
    foreach(var width in new[]{320,340,600})
    {
        var doc=Svg(Spec() with{Title=title,Description="Invented",Width=width});var drawn=Heading(doc);
        Check(drawn.EndsWith("…")&&Wide11(drawn)*17/11<=width-48&&title.StartsWith(drawn[..^1])&&title[drawn.Length-1] is ' ' or ',',$"{width}: {drawn}");
        Check(doc.Root!.Element(ns+"title")!.Value==title&&doc.Root!.Attribute("aria-label")!.Value==title+". Invented",$"{width}: the whole title");
    }
    Check(Heading(Svg(Spec() with{Title=title,Width=1200}))==title&&Heading(Svg(Spec() with{Width=320}))=="Example","a title that fits");
    // A word too long for the drawing by itself is cut between its letters.
    var word=Heading(Svg(Spec() with{Title="Pneumonoultramicroscopicsilicovolcanoconiosis",Width=320}));
    Check(word.EndsWith("…")&&Wide11(word)*17/11<=272&&"Pneumonoultramicroscopicsilicovolcanoconiosis".StartsWith(word[..^1]),word);
    var graph=XDocument.Parse(GraphEngine.Render(new GraphSpec{Title=title,Width=340,Nodes=[new("a","A"),new("b","B")],Edges=[new("a","b")]}));
    Check(Heading(graph).EndsWith("…")&&title.StartsWith(Heading(graph)[..^1])&&graph.Root!.Element(ns+"title")!.Value==title,Heading(graph));
});
Test("ShowValue, InFront and XTickLabels are refused where they would draw nothing or contradict another setting, each with its reason, and taken everywhere else",()=>{
    foreach(var label in new[]{null,""," "})
        Check(Refused(Spec() with{Annotations=[new(AnnotationAxis.Y,2){Label=label,ShowValue=false}]}).StartsWith("ShowValue = false draws an annotation's label without its value, so it needs a Label"),$"'{label}'");
    var calendar=Sample(ChartKind.Calendar);
    Check(Refused(calendar with{Annotations=[new(AnnotationAxis.X,Utc(2026,9,15,12)){Label="Today",ShowValue=false}]}).StartsWith("A calendar's key names an outlined day by its label alone already"),"a calendar's ShowValue");
    Check(Refused(calendar with{Annotations=[new(AnnotationAxis.X,Utc(2026,9,15,12)){Label="Today",InFront=true}]}).StartsWith("InFront draws a reference over the data instead of behind it")
        &&Refused(Sample(ChartKind.Gauge) with{Annotations=[new(AnnotationAxis.Y,60){InFront=true}]}).StartsWith("InFront draws a reference over the data"),"InFront on a calendar and a gauge");
    foreach(var kind in Enum.GetValues<ChartKind>())
        foreach(var labels in new[]{TickLabels.Ends,TickLabels.Bounds})
        {
            var spec=Sample(kind) with{XTickLabels=labels};
            if(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band or ChartKind.Range or ChartKind.Blocks or ChartKind.Timeline) Svg(spec);
            else Check(Refused(spec).StartsWith("XTickLabels chooses which labels a continuous X axis writes along the bottom"),$"{kind} {labels}: {Refused(spec)}");
        }
    Check(Refused(Spec() with{XTickLabels=TickLabels.Bounds,XTicks=TickSource.PointLabels}).StartsWith("XTickLabels = Bounds labels the X axis's own two ends, and XTicks = PointLabels"),"bounds beside point labels");
    Svg(Spec() with{XTickLabels=TickLabels.Ends,XTicks=TickSource.PointLabels});Svg(Spec() with{XTickLabels=TickLabels.Bounds,XTicks=TickSource.Axis});
    Check(Refused(Spec() with{XTickLabels=(TickLabels)7})=="Unknown X tick labelling."&&Refused(Spec() with{YTickLabels=(TickLabels)7})=="Unknown Y axis side or tick labelling.","unknown labelling");
    // The Y axis takes Bounds wherever it takes Ends, and refuses it where it always refused Ends.
    foreach(var kind in new[]{ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Gauge,ChartKind.Ring,ChartKind.Timeline,ChartKind.Calendar}) Reject(()=>Svg(Sample(kind) with{YTickLabels=TickLabels.Bounds}));
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Column,ChartKind.Bar,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Blocks}) Svg(Sample(kind) with{YTickLabels=TickLabels.Bounds});
    // Annotations take both settings on every other kind that takes annotations, a sparkline included, where they draw no label.
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Area,ChartKind.Column,ChartKind.Bar,ChartKind.Range,ChartKind.Blocks})
        Svg(Sample(kind) with{Annotations=[new(AnnotationAxis.Y,3){Label="Mark",ShowValue=false,InFront=true}]});
    Check(!Svg(Pb() with{Annotations=[new(AnnotationAxis.Y,1420){Label="Median",ShowValue=false,InFront=true}]}).Descendants(ns+"text").Any(),"a sparkline wrote a label");
});
Test("The new settings round-trip through the HTTP API's JSON, a request that names none keeps the defaults, and only a set one renames gradients",()=>{
    var spec=Binned() with{XTickLabels=TickLabels.Bounds,YTickLabels=TickLabels.Bounds,Annotations=[new(AnnotationAxis.X,2832){Label="median",ShowValue=false,InFront=true}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,finishJson);
    Check(json.Contains("\"xTickLabels\":\"Bounds\"")&&json.Contains("\"yTickLabels\":\"Bounds\"")&&json.Contains("\"showValue\":false")&&json.Contains("\"inFront\":true"),json);
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(back is {XTickLabels:TickLabels.Bounds,YTickLabels:TickLabels.Bounds}&&back.Annotations[0] is {ShowValue:false,InFront:true}&&ChartSvg.Render(back)==ChartSvg.Render(spec),"the spec changed in transit");
    // Written by hand, as the HTTP API takes it.
    var written="{\"title\":\"Field\",\"kind\":\"Blocks\",\"includeZero\":true,\"xFormat\":\"Duration\",\"xTickLabels\":\"Bounds\",\"annotations\":[{\"axis\":\"X\",\"from\":2832,\"label\":\"median\",\"showValue\":false,\"inFront\":true}],"
        +"\"series\":[{\"name\":\"Finishers\",\"points\":[{\"x\":2100,\"xEnd\":2400,\"y\":299},{\"x\":2400,\"xEnd\":2700,\"y\":1},{\"x\":2700,\"xEnd\":3000,\"y\":0}]}]}";
    var drawn=Svg(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,finishJson)!);
    Check(Printed(drawn).SequenceEqual(["median"])&&Voiced(drawn).SequenceEqual(["median: 47:12"])&&Along(drawn).Select(l=>l.Text).SequenceEqual(["35:00","50:00"])&&Close(BlockPaths(drawn).Select(BlockBox).ToArray()[1].Top,342),
        $"a spec written by hand: {string.Join(", ",Printed(drawn))} | {string.Join(", ",Voiced(drawn))} | {string.Join(", ",Along(drawn))}");
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"annotations\":[{\"axis\":\"Y\",\"from\":1}],\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]}]}",finishJson)!;
    Check(old.XTickLabels==TickLabels.All&&old.Annotations[0] is {ShowValue:true,InFront:false},"the defaults");
    // A gradient's ID is named after the spec: 0.34.0 named these two lumen-4bce89394b87 and lumen-6ad38916fb61, and the defaults leave
    // them so; each setting away from its default names a gradient afresh.
    string GradientId(ChartSpec s)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(s),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}],Annotations=[new(AnnotationAxis.Y,2){Label="T"}]};
    Check(GradientId(faded)=="lumen-4bce89394b87"&&GradientId(faded with{YTickLabels=TickLabels.Ends})=="lumen-6ad38916fb61",$"{GradientId(faded)} {GradientId(faded with{YTickLabels=TickLabels.Ends})}");
    var set=new[]{faded with{XTickLabels=TickLabels.Ends},faded with{YTickLabels=TickLabels.Bounds},faded with{Annotations=[faded.Annotations[0] with{ShowValue=false}]},faded with{Annotations=[faded.Annotations[0] with{InFront=true}]}}.Select(GradientId).ToArray();
    Check(set.Distinct().Count()==4&&!set.Contains(GradientId(faded))&&!set.Contains("lumen-6ad38916fb61"),string.Join(", ",set));
});
Test("0.34.0's renderings do not move: rows of its baseline rebuilt here match its hashes in both finishes, and every new setting written out at its default draws the same chart",()=>{
    // Hashes of v0.34.0's tests/Lumen.Charts.Baseline/reference files, refined and classic, recorded on Windows.
    if(!OperatingSystem.IsWindows())return;
    var line=Baseline(ChartKind.Line,ChartTheme.Light);
    ChartSpec Titled(ChartKind kind,params ChartSeries[] series)=>new(){Kind=kind,Theme=ChartTheme.Light,Title="Baseline",Description="Default output",Series=series};
    ChartPoint[] Weights()=>new[]{37.9,37.8,38.2,38.0,38.1,38.0,37.9}.Select((kg,i)=>new ChartPoint(i,kg,$"Week {i+1}")).ToArray();
    double[] pbTimes=[1450,1432,1445,1411,1420,1367];
    var light=ChartStyle.Light;
    (string Row,string Refined,string Classic,ChartSpec Spec)[] rows=[
        ("Gauge/Light/True","77EBB6158970C838","D4E3B4E53144511E",Titled(ChartKind.Gauge,new ChartSeries("Score",[new(0,72,"Score")]))),
        ("Calendar/Light/True","7DF486BA874B3C3B","D05CB415CCD30B80",Titled(ChartKind.Calendar,new ChartSeries("C",Enumerable.Range(0,56).Select(i=>new ChartPoint(1788825600000d+i*86400000d,i%7==0?0:10+i*3%40)).ToArray())) with{XAxis=AxisKind.Time}),
        ("Timeline/Light/True","AB656FD0AD6DA5B1","3A4014195F6691E4",Titled(ChartKind.Timeline,new("A",[ChartPoint.Span(0,2),ChartPoint.Span(5,7)]),new("B",[ChartPoint.Span(2,5),ChartPoint.Span(7,9,"Last")]),new("C",[ChartPoint.Span(9,12)]))),
        ("Blocks/Light/True","ED303664D2DE44C5","33DE20590ACB95F0",Titled(ChartKind.Blocks,new ChartSeries("B",[ChartPoint.Block(0,2,5,"W"),ChartPoint.Block(2,6,9),ChartPoint.Block(6,7,7),ChartPoint.Block(8,12,3,"C")]))),
        ("Donut/Light/True","5668A60FF9FAE3CD","41B05E947E74CD0F",Baseline(ChartKind.Donut,ChartTheme.Light)),
        ("Heatmap/Light/True","43D1B062374DE7AE","E701C0163185BA1C",Baseline(ChartKind.Heatmap,ChartTheme.Light)),
        ("Radar/Light/True","DA6867ECC2A5836C","16406553279DC2CE",Baseline(ChartKind.Radar,ChartTheme.Light)),
        ("Histogram/Light/True","BDD2A861ACC503CD","7A551A8B643A8CBB",Baseline(ChartKind.Histogram,ChartTheme.Light)),
        ("annotated","90C7B27AF8A858FD","B8D59813CF5546F6",line with{Annotations=[new(AnnotationAxis.Y,25){Label="Target"},new(AnnotationAxis.X,3){To=6,Label="Window"}]}),
        ("spark/pb-120-light","4BD6EB9C52C14881","0D333C87A3B4C598",new ChartSpec{Kind=ChartKind.Line,Style=light,Title="5 km: 24:10 to 22:47 over 6 races",Description="Each race's time, oldest first, faster higher",
            Width=120,Height=32,Sparkline=true,YReversed=true,YFormat=ValueFormat.Duration,
            Series=[new("5 km",pbTimes.Select((t,i)=>new ChartPoint(i,t,$"Race {i+1}"){Highlight=i%2==1?light.Zones[5]:null,ValueNote=i%2==1?" · PB":null}).ToArray(),light.Zones[0]){StrokeWidth=2}]}),
        ("highlight/line","3BD29A55527F24D9","43E60A3FBDD8C2E2",line with{Title="Highlights",Series=[line.Series[0] with{Points=line.Series[0].Points.Select((p,i)=>i is 4 or 9?p with{Highlight="#DD4B45",ValueNote=" · best"}:p).ToArray()},
            line.Series[1] with{Kind=ChartKind.Scatter,Points=line.Series[1].Points.Select((p,i)=>i==11?p with{Highlight="#2E9B58"}:p).ToArray()}]}),
        ("span/line","0CC73CA1036DD7A1","D52F9E45BCB6E0A5",line with{Title="Minimum span",YMinSpan=8,Series=[new("Weight",Weights()){Markers=MarkerStyle.Filled}]})];
    foreach(var (row,refined,classic,spec) in rows)
    {
        Check(Hash16(ChartSvg.Render(spec))==refined,$"{row} moved: {Hash16(ChartSvg.Render(spec))}");
        Check(Hash16(ChartSvg.Render(Classic(spec)))==classic,$"{row} moved in the classic finish: {Hash16(ChartSvg.Render(Classic(spec)))}");
        var spelled=spec with{XTickLabels=TickLabels.All,Annotations=spec.Annotations.Select(a=>a with{ShowValue=true,InFront=false}).ToArray()};
        Check(ChartSvg.Render(spelled)==ChartSvg.Render(spec),$"{row}: the defaults written out moved it");
    }
});
Test("Sports page: How the field finished draws the last race's invented field in bins by the page's rule, the athlete's bin in red and named so, the median over the bins without its time, and the finishers off the chart in its source",()=>{
    var card=sports.Single(c=>c.Id=="field");var spec=card.Spec;var order=sports.Select(c=>c.Id).ToList();
    Check(card.Section=="racing"&&card.Wide&&order.IndexOf("field")==order.IndexOf("race-results")+1,"the card or its place");
    // The last race's field of 52, fastest first, the athlete's 40:12 the 19th, its place.
    var field=SportsData.Field;var race=SportsData.Races[^1];
    Check(field.Count==race.Field&&field.SequenceEqual(field.Order())&&field[race.Position-1]==2412,"the field");
    // One-minute bins, the narrowest that set the 1st to the 99th percentile in at most 20: seventeen from 34:00 to 51:00, one finisher
    // off each end.
    var (bins,faster,slower)=SportsData.FinishBins(field);
    Check(bins.Length==17&&bins[0].From==2040&&bins[^1].To==3060&&bins.Zip(bins.Skip(1)).All(p=>p.First.To==p.Second.From&&p.First.To-p.First.From==60)&&faster==1&&slower==1&&bins.Sum(b=>b.Count)+faster+slower==52,
        string.Join(" ",bins.Select(b=>b.Count)));
    var blocks=spec.Series.Single().Points;
    Check(spec is {Kind:ChartKind.Blocks,IncludeZero:true,XFormat:ValueFormat.Duration,XTickLabels:TickLabels.Bounds,YTickLabels:TickLabels.Bounds,Height:300}
        &&blocks.Select(p=>(p.X,p.XEnd,p.Y)).SequenceEqual(bins.Select(b=>(b.From,(double?)b.To,(double?)b.Count))),"the bins drawn");
    // The athlete's bin, 40:00 to 41:00, alone is red, and says why in its name; the others are the ramp's neutral grey.
    Check(blocks.Count(p=>p.Color is not null)==1&&blocks.Single(p=>p.Color is not null) is {X:2400,Color:"#DD4B45",ValueNote:" · you"}&&spec.Series[0].Color==ChartStyle.Light.Zones[0],"the athlete's bin");
    Check(Datums(Svg(spec),0).Count(m=>m.Attribute("aria-label")!.Value=="Finishers: 40:00 to 41:00, 6 · you")==1,"the athlete's bin's name");
    Check(spec.Title=="19th of 52 in 40:12"&&spec.Description=="52 finishers · median 41:29"&&spec.Source=="Off the chart: 1 faster and 1 slower · an invented field","the words");
    Check(spec.Annotations.Single() is {Axis:AnnotationAxis.X,From:2489,Label:"median",ShowValue:false,InFront:true,Dashed:true,To:null},"the median");
    // At a phone's widths every word drawn at the left fits the drawing by the generous estimate, the source on two lines.
    foreach(var width in new[]{322,337,343,1100})
    {
        var doc=Svg(spec with{Width=width});
        var words=doc.Descendants(ns+"text").Where(t=>Attr(t,"x")==24).ToArray();
        Check(words.All(t=>Wide11(t.Value)*double.Parse((string?)t.Attribute("font-size")??"12",CultureInfo.InvariantCulture)/11<=width-48),$"{width}: {string.Join(" | ",words.Select(t=>t.Value))}");
        Check(words.Count(t=>t.Value.StartsWith("Off the chart"))==1&&(width>400||words.Any(t=>t.Value=="an invented field")),$"{width}: the source");
    }
});
// 0.36.0: reading a chart day by day. ValueFormat.Signed writes +5 and −5; YSymmetric holds a Y axis symmetric about zero; and SharedReadout,
// which the component alone draws, reads every series at one X, from ChartSvg.Readout. A 900 by 420 chart's plot runs from x 76 to 870
// and y 78 to 344, and two panes of equal weight split it, after a 24-pixel gap, into 78 to 199 and 223 to 344. Every example is invented.
ChartSpec Form36(params double[] values)=>new(){Title="Form",Kind=ChartKind.Line,YSymmetric=10,YFormat=ValueFormat.Signed,
    Series=[new("Form",values.Select((v,i)=>new ChartPoint(i,v)).ToArray()){Markers=MarkerStyle.Filled}]};
double[] Heights36(XDocument doc,int series=0)=>Datums(doc,series).Select(m=>Attr(m.Element(ns+"circle")!,"cy")).ToArray();
// Three series over three days, the fatigue of the second day missing, form in a pane of its own.
ChartSpec Read36()=>new(){Title="Read",Kind=ChartKind.Line,SharedReadout=true,Panes=[new(){Weight=1,YSymmetric=10,YFormat=ValueFormat.Signed}],
    Series=[new("Fitness",[new(0,50),new(1,52),new(2,51)]){Markers=MarkerStyle.Filled,ChangeColors=ChangeColors.HigherIsBetter},
        new("Fatigue",[new(0,60),new(1,null),new(2,58)]){Markers=MarkerStyle.Filled},
        new("Form",[new(0,-10),new(1,-4.5),new(2,-7){ValueNote=" tired"}]){Pane=1,Markers=MarkerStyle.Filled}]};
Test("ValueFormat.Signed writes a plus for a positive value, a true minus for a negative one and 0 for zero, as Number writes the digits, in ticks, names, value labels, annotations and the component",()=>{
    var signed=new Axis(AxisKind.Linear,0,1){ValueFormat=ValueFormat.Signed};
    (double Value,string Text)[] cases=[(5,"+5"),(-5,"−5"),(0,"0"),(-0.0,"0"),(1.25,"+1.25"),(-0.5,"−0.5"),(-2.25,"−2.25"),(1234.5,"+1234.5"),(2e6,"+2E+6"),(-0.004,"−4E-3")];
    foreach(var (value,text) in cases) Check(signed.Format(value)==text,$"{value}: {signed.Format(value)}");
    // It is Number's digits with a sign: every value Number writes reads the same with its sign taken off.
    foreach(var value in new[]{3.0,47.25,0.75,99999.5}) Check(signed.Format(-value)=="−"+new Axis(AxisKind.Linear,0,1).Format(value)&&signed.Format(value)=="+"+new Axis(AxisKind.Linear,0,1).Format(value),$"{value}");
    // At the end of the enum, so the values before it keep their numbers.
    Check((int)ValueFormat.Number==0&&(int)ValueFormat.Duration==1&&(int)ValueFormat.Compact==2&&(int)ValueFormat.TimeOfDay==3&&(int)ValueFormat.Signed==4,"the enum's values moved");
    Reject(()=>ChartSvg.Render(Spec() with{YFormat=(ValueFormat)5}));
    var spec=new ChartSpec{Title="Change",Kind=ChartKind.Line,YFormat=ValueFormat.Signed,Annotations=[new(AnnotationAxis.Y,2){Label="Target"}],
        Series=[new("Change",[new(0,-3,"Mon"),new(1,0,"Tue"),new(2,4,"Wed")]){ValueLabels=true,Markers=MarkerStyle.Filled}]};
    var doc=Svg(spec);
    Check(Datums(doc,0).Select(m=>m.Attribute("aria-label")!.Value).SequenceEqual(["Change: Mon, −3","Change: Tue, 0","Change: Wed, +4"]),string.Join(" | ",Datums(doc,0).Select(m=>m.Attribute("aria-label")!.Value)));
    var texts=doc.Descendants(ns+"text").Select(t=>t.Value).ToArray();
    Check(texts.Contains("+4")&&texts.Contains("−3")&&texts.Contains("Target: +2")&&Upward(doc).Any(t=>t.Text=="+4")&&Upward(doc).Any(t=>t.Text=="−2"),string.Join(" | ",texts));
    // Up the right-hand axis, along X and in a pane, and through the component's status line and data table.
    Svg(spec with{XFormat=ValueFormat.Signed,Y2Format=ValueFormat.Signed,Panes=[new(){YFormat=ValueFormat.Signed,Y2Format=ValueFormat.Signed}],Series=[..spec.Series,new("Pane",[new(0,-1),new(1,2)]){Pane=1}]});
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var status="";
    var html=Operate(spec,async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(0,0);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(status=="Change: Mon = −3"&&System.Net.WebUtility.HtmlDecode(html).Contains("<td>+4</td>"),status);
    // A time axis writes its calendar, and the kinds without a Y axis or with a count on it refuse it, as they refuse every other format.
    Check(Refused(Spec() with{XAxis=AxisKind.Time,XFormat=ValueFormat.Signed}).StartsWith("A time axis writes its own calendar"),"a signed time axis");
    foreach(var kind in new[]{ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Histogram}) Check(Refused(Sample(kind) with{YFormat=ValueFormat.Signed}).StartsWith("A Y format applies"),$"{kind}");
    // Gauges, rings and calendars write their values in it.
    Check(ChartSvg.Render(Sample(ChartKind.Gauge) with{YFormat=ValueFormat.Signed}).Contains("+72"),"a gauge");
});
Test("YSymmetric holds the Y axis symmetric about zero, at least its value either way and as far as the data reaches, reversed or not, in the main plot or a pane",()=>{
    double Y(double v,double reach,double top=78,double bottom=344,bool reversed=false)=>reversed?top+(v+reach)/(2*reach)*(bottom-top):bottom-(v+reach)/(2*reach)*(bottom-top);
    foreach(var classic in new[]{false,true})
    {
        XDocument Draw(ChartSpec s)=>Svg(classic?Classic(s):s);
        // Data within ±10 stands on an axis from −10 to +10, zero in the middle.
        var inside=Draw(Form36(-3,4,2));
        Check(Heights36(inside).Zip(new[]{-3d,4,2}).All(p=>Close(p.First,Y(p.Second,10))),$"classic {classic}: {string.Join(", ",Heights36(inside))}");
        Check(Upward(inside).SequenceEqual([("−10",348d),("−5",281.5),("0",215d),("+5",148.5),("+10",82d)]),$"classic {classic}: {string.Join(" | ",Upward(inside))}");
        // Data past it widens the axis to the farther of its two ends, both ways.
        var past=Draw(Form36(-3,14,2));
        Check(Heights36(past).Zip(new[]{-3d,14,2}).All(p=>Close(p.First,Y(p.Second,14))),$"classic {classic}: {string.Join(", ",Heights36(past))}");
        var below=Draw(Form36(-22,1));
        Check(Heights36(below).Zip(new[]{-22d,1}).All(p=>Close(p.First,Y(p.Second,22))),$"classic {classic}: {string.Join(", ",Heights36(below))}");
        // Reversed, the smallest value is at the top and zero stays in the middle.
        var reversed=Draw(Form36(5,-2) with{YReversed=true});
        Check(Heights36(reversed).Zip(new[]{5d,-2}).All(p=>Close(p.First,Y(p.Second,10,reversed:true)))&&Upward(reversed).First()==("−10",82d)&&Upward(reversed).Last()==("+10",348d),$"classic {classic}: {string.Join(", ",Heights36(reversed))}");
        // A pane's own: the main plot keeps its fitted axis, from 1 to 3.
        var paned=Draw(new ChartSpec{Title="P",Kind=ChartKind.Line,Panes=[new(){Weight=1,YSymmetric=10,YFormat=ValueFormat.Signed}],
            Series=[new("Fitness",[new(0,1),new(1,2),new(2,3)]){Markers=MarkerStyle.Filled},new("Form",[new(0,-4),new(1,6),new(2,0)]){Pane=1,Markers=MarkerStyle.Filled}]});
        Check(Heights36(paned,1).Zip(new[]{-4d,6,0}).All(p=>Close(p.First,Y(p.Second,10,223,344)))&&Close(Heights36(paned,0)[0],199)&&Close(Heights36(paned,0)[2],78),
            $"classic {classic}: {string.Join(", ",Heights36(paned,1))} | {string.Join(", ",Heights36(paned,0))}");
    }
    // A series on the right-hand axis is not measured on it; an empty chart still draws its axis.
    var right=Svg(Form36(1,2) with{Series=[..Form36(1,2).Series,new("Right",[new(0,500),new(1,900)]){Secondary=true}]});
    Check(Heights36(right).Zip(new[]{1d,2}).All(p=>Close(p.First,Y(p.Second,10,78,344))),string.Join(", ",Heights36(right)));
    // Columns and areas, which stand on zero, blocks, which stand on its bottom, and bars, which run along the bottom, take it.
    var columns=Svg(new ChartSpec{Title="C",Kind=ChartKind.Column,YSymmetric=50,Series=[new("Change",[new(0,-20,"A"),new(1,30,"B")])]});
    Check(Upward(columns).First().Text=="-50"&&Upward(columns).Last().Text=="50","the columns' axis");
    foreach(var kind in new[]{ChartKind.Area,ChartKind.Bar,ChartKind.StackedColumn,ChartKind.Scatter,ChartKind.Bubble,ChartKind.Band,ChartKind.Range,ChartKind.Candlestick,ChartKind.Ohlc,ChartKind.Blocks})
        Svg(Sample(kind) with{YSymmetric=500});
    Svg(Form36(1,2) with{IncludeZero=true});
    // Hidden in the component, the first pane closes and the pane under it takes the main plot's place with its symmetric axis.
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var moved=new ChartSpec{Title="P",Kind=ChartKind.Line,Panes=[new(){YSymmetric=10,YFormat=ValueFormat.Signed}],Series=[new("Main",[new(0,38),new(1,38.2)]),new("Form",[new(0,-1),new(1,2)]){Pane=1}]};
    ChartSpec shown=moved;
    Operate(moved,chart=>{typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[0]);shown=(ChartSpec)typeof(LumenChart).GetMethod("VisibleSpec",flags)!.Invoke(chart,[])!;return Task.CompletedTask;});
    Check(shown is {YSymmetric:10,YFormat:ValueFormat.Signed,Panes.Count:0},"the pane took the main plot's place without its symmetric axis");
});
Test("YSymmetric is refused out of range, beside YMin, YMax or YMinSpan, on a logarithmic axis and where there is no such axis, each with its reason, in the main plot and a pane",()=>{
    var line=Form36(1,2);
    foreach(var least in new[]{0,-10,double.NaN,double.PositiveInfinity,1e101}) Check(Refused(line with{YSymmetric=least}).StartsWith("YSymmetric is the least a Y axis reaches either side of zero, so it must be positive and finite"),$"{least}");
    Svg(line with{YSymmetric=1e100});Svg(line with{YSymmetric=1e-9});
    foreach(var other in new[]{line with{YMin=-20},line with{YMax=20},line with{YMinSpan=8}})
        Check(Refused(other).StartsWith("YSymmetric sets both ends of a Y axis about zero, and YMin, YMax or YMinSpan sets them another way"),Refused(other));
    Check(Refused(line with{YAxis=AxisKind.Log,Series=[new("S",[new(0,1),new(1,2)])]}).StartsWith("YSymmetric holds a Y axis symmetric about zero, and a logarithmic axis"),"a log axis");
    foreach(var kind in new[]{ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Gauge,ChartKind.Ring,ChartKind.Timeline,ChartKind.Calendar})
        Check(Refused(Sample(kind) with{YSymmetric=10}).StartsWith("YSymmetric holds a Y axis symmetric about zero, so it applies to line, area"),$"{kind}: {Refused(Sample(kind) with{YSymmetric=10})}");
    var paned=new ChartSpec{Title="P",Kind=ChartKind.Line,Panes=[new(){YSymmetric=10}],Series=[new("A",[new(0,1)]),new("B",[new(0,1)]){Pane=1}]};
    Svg(paned);Svg(paned with{Panes=[new(){YSymmetric=10,YReversed=true}]});
    Check(Refused(paned with{Panes=[new(){YSymmetric=10,YMin=0}]}).Contains("one or the other")&&Refused(paned with{Panes=[new(){YSymmetric=10,YMinSpan=4}]}).Contains("one or the other")
        &&Refused(paned with{Panes=[new(){YSymmetric=10,YAxis=AxisKind.Log}]}).Contains("logarithmic")&&Refused(paned with{Panes=[new(){YSymmetric=0}]}).Contains("positive"),"a pane's refusals");
});
Test("SharedReadout never changes the drawing: the SVG is byte for byte the same with it and without it in both finishes, and it is refused on a sparkline and on the kinds without a continuous X axis",()=>{
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Area,ChartKind.Scatter,ChartKind.Bubble,ChartKind.Band,ChartKind.Range,ChartKind.Candlestick,ChartKind.Ohlc,ChartKind.Blocks})
        foreach(var classic in new[]{false,true})
        {
            var spec=classic?Classic(Sample(kind)):Sample(kind);
            Check(ChartSvg.Render(spec with{SharedReadout=true})==ChartSvg.Render(spec)&&ChartSvg.Render(spec with{SharedReadout=true},includeLegend:false,includeTitles:false)==ChartSvg.Render(spec,includeLegend:false,includeTitles:false),$"{kind} classic {classic}");
        }
    var card=Sports("performance");
    Check(card.SharedReadout&&ChartSvg.Render(card)==ChartSvg.Render(card with{SharedReadout=false}),"the gallery's chart");
    Check(ChartSvg.Render(Read36())==ChartSvg.Render(Read36() with{SharedReadout=false}),"panes");
    foreach(var kind in new[]{ChartKind.Column,ChartKind.Bar,ChartKind.StackedColumn,ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Gauge,ChartKind.Ring,ChartKind.Timeline,ChartKind.Calendar})
        Check(Refused(Sample(kind) with{SharedReadout=true}).StartsWith("SharedReadout reads every series at one X of a continuous X axis"),$"{kind}: {Refused(Sample(kind) with{SharedReadout=true})}");
    Check(Refused(Pb() with{SharedReadout=true}).StartsWith("A sparkline is read beside the words that give its numbers"),"a sparkline");
});
Test("ChartSvg.Readout reads every series at each X in legend order, where its marks stand, a missing value as missing, and a pane's axis as its own",()=>{
    var spec=Read36();var readout=ChartSvg.Readout(spec);var doc=Svg(spec);
    Check(readout.Top==78&&readout.Bottom==344&&readout.Left==76&&readout.Right==870,$"{readout.Top} {readout.Bottom} {readout.Left} {readout.Right}");
    Check(readout.Columns.Select(c=>c.Text).SequenceEqual(["0 · Fitness 50 · Fatigue 60 · Form −10","1 · Fitness 52, better than the previous · Fatigue missing · Form −4.5","2 · Fitness 51, worse than the previous · Fatigue 58 · Form −7 tired"]),
        string.Join(" | ",readout.Columns.Select(c=>c.Text)));
    // Each entry stands where its mark does, and the missing one has no place and no mark.
    foreach(var column in readout.Columns)
        foreach(var entry in column.Entries)
        {
            var found=doc.Descendants(ns+"g").SingleOrDefault(g=>(string?)g.Attribute("data-series")==entry.Series.ToString(CultureInfo.InvariantCulture)&&(string?)g.Attribute("data-point")==entry.Point.ToString(CultureInfo.InvariantCulture));
            if(entry.Position is null){Check(found is null&&entry.Text=="Fatigue missing","the missing value");continue;}
            var circle=found!.Element(ns+"circle")!;
            Check(Close(Attr(circle,"cx"),column.Position)&&Close(Attr(circle,"cy"),entry.Position.Value)&&entry.Color==ChartStyle.Light.Series[entry.Series],$"{entry.Text}: {Attr(circle,"cx")},{Attr(circle,"cy")} against {column.Position},{entry.Position}");
        }
    // A description on two lines moves the plot, and the readout with it.
    Check(ChartSvg.Readout(spec with{Width=340,Description="Fitness, fatigue and daily stress above, form below · two planned weeks shaded"}).Top==92,"a description on two lines");
    // A point is read only within half the closest spacing of the X values: weekly points at noon stand half a day from the daily ones
    // either side, a quarter day being the most, so they are read at X values of their own.
    var day=86_400_000d;
    var mixed=new ChartSpec{Title="M",Kind=ChartKind.Line,XAxis=AxisKind.Time,Series=[new("Daily",Enumerable.Range(0,14).Select(i=>new ChartPoint(Utc(2026,6,1)+i*day,i)).ToArray()),
        new("Weekly",[new(Utc(2026,6,1,12),100),new(Utc(2026,6,8,12),110)])]};
    var read=ChartSvg.Readout(mixed);
    Check(read.Columns.Count==16&&read.Columns.Count(c=>c.Entries.Count==2)==0&&read.Columns.Single(c=>c.X==Utc(2026,6,1,12)).Label=="1 Jun 2026"&&read.Columns[0].Text=="1 Jun 2026 · Daily 0",
        string.Join(" | ",read.Columns.Take(3).Select(c=>c.Text)));
    // Blocks are read across the X they cover, candles by their prices and range bars by their ends; a chart without a continuous X axis
    // reads nothing.
    var blocks=new ChartSpec{Title="B",Kind=ChartKind.Blocks,Series=[new("Plan",[ChartPoint.Block(0,2,5,"Warm-up"),ChartPoint.Block(2,6,9)]),new("Power",[new(1,4),new(3,8),new(5,7)]){Kind=ChartKind.Line}]};
    Check(ChartSvg.Readout(blocks).Columns.Select(c=>c.Text).SequenceEqual(["1 · Plan 5 · Power 4","3 · Plan 9 · Power 8","5 · Plan 9 · Power 7"]),string.Join(" | ",ChartSvg.Readout(blocks).Columns.Select(c=>c.Text)));
    Check(ChartSvg.Readout(Sample(ChartKind.Candlestick)).Columns[0].Text=="0 · Price open 10, high 12, low 9, close 11"&&ChartSvg.Readout(Sample(ChartKind.Range)).Columns[1].Text=="B · Heart rate 55 to 130",
        $"{ChartSvg.Readout(Sample(ChartKind.Candlestick)).Columns[0].Text} | {ChartSvg.Readout(Sample(ChartKind.Range)).Columns[1].Text}");
    Check(ChartSvg.Readout(Sample(ChartKind.Donut))==ChartReadout.Empty&&ChartSvg.Readout(Sample(ChartKind.Column))==ChartReadout.Empty&&ChartSvg.Readout(new ChartSpec())==ChartReadout.Empty,"a chart without a continuous X axis");
    // A zoomed view reads only the X it shows.
    Check(ChartSvg.Readout(spec with{XMin=.5,XMax=2}).Columns.Select(c=>c.X).SequenceEqual([1d,2]),"a zoomed view");
    Reject(()=>ChartSvg.Readout(spec with{Width=10}));
});
Test("The component reads the shared readout once a drawing, reads a selected point and a keyboard stop as the readout does, and names the keys in hidden words",()=>{
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    ChartReadout? held=null;var status="";var later="";
    Operate(Read36(),async chart=>{held=(ChartReadout?)typeof(LumenChart).GetField("readout",flags)!.GetValue(chart);await chart.SelectPoint(2,1);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;
        await chart.Readout(2);later=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(held is {Columns.Count:3}&&status==held.Columns[1].Text&&later==held.Columns[2].Text,$"{status} | {later}");
    // Hiding a series reads the others alone; without the setting there is nothing to read.
    Operate(Read36(),chart=>{typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[1]);held=(ChartReadout?)typeof(LumenChart).GetField("readout",flags)!.GetValue(chart);return Task.CompletedTask;});
    Check(held!.Columns[1].Text=="1 · Fitness 52, better than the previous · Form −4.5",held.Columns[1].Text);
    Operate(Read36() with{SharedReadout=false},chart=>{held=(ChartReadout?)typeof(LumenChart).GetField("readout",flags)!.GetValue(chart);return Task.CompletedTask;});
    Check(held is null,"a readout without the setting");
    var html=Prerender(ChartElement(Read36()));var plain=Prerender(ChartElement(Spec()));var spark=Prerender(ChartElement(Pb()));
    Check(html.Contains("<span class=\"lumen-keys\" hidden>Arrow keys read the chart: Left and Right move the readout from one X to the next")
        &&plain.Contains("<span class=\"lumen-keys\" hidden>Arrow keys move between points: Left and Right along a series")&&spark.Contains("class=\"lumen-keys\" hidden"),html);
    // The drawing keeps every mark a tab stop of its own, so a page without the script stays readable; the script makes them one.
    Check(Datums(Svg(Read36()),0).All(m=>(string?)m.Attribute("tabindex")=="0"),"a static mark lost its tab stop");
    var script=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Lumen.Charts.Blazor/wwwroot/lumen.js"));
    Check(script.Contains("export function drawn(")&&script.Contains("'tabindex', element === current ? '0' : '-1'")&&script.Contains("ArrowLeft: -1, ArrowRight: 1, PageUp: -10, PageDown: 10, Home: -Infinity, End: Infinity"),"the script's keys");
});
Test("YSymmetric, Signed and SharedReadout round-trip through the HTTP API's JSON, a request that names none keeps the defaults, and only a symmetric axis renames gradients",()=>{
    var spec=Read36() with{YFormat=ValueFormat.Signed};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,finishJson);
    Check(json.Contains("\"sharedReadout\":true")&&json.Contains("\"ySymmetric\":10")&&json.Contains("\"yFormat\":\"Signed\""),json);
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(back is {SharedReadout:true,YFormat:ValueFormat.Signed}&&back.Panes[0].YSymmetric==10&&ChartSvg.Render(back)==ChartSvg.Render(spec),"the spec changed in transit");
    var written="{\"title\":\"Form\",\"kind\":\"Line\",\"ySymmetric\":10,\"yFormat\":\"Signed\",\"sharedReadout\":true,\"series\":[{\"name\":\"Form\",\"points\":[{\"x\":0,\"y\":-3},{\"x\":1,\"y\":4}]}]}";
    var drawn=Svg(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,finishJson)!);
    Check(Upward(drawn).First().Text=="−10"&&Upward(drawn).Last().Text=="+10"&&Datums(drawn,0)[0].Attribute("aria-label")!.Value=="Form: 0, −3","a spec written by hand");
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"panes\":[{}],\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]},{\"name\":\"T\",\"pane\":1,\"points\":[{\"x\":0,\"y\":1}]}]}",finishJson)!;
    Check(!old.SharedReadout&&old.YSymmetric is null&&old.Panes[0].YSymmetric is null,"the defaults");
    // 0.34.0 named this gradient lumen-4bce89394b87; the defaults and a shared readout, set or not, leave it so, and a symmetric axis names it afresh.
    string GradientId(ChartSpec s)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(s),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}],Annotations=[new(AnnotationAxis.Y,2){Label="T"}]};
    Check(GradientId(faded)=="lumen-4bce89394b87"&&GradientId(faded with{SharedReadout=true})=="lumen-4bce89394b87"&&GradientId(faded with{YSymmetric=null})=="lumen-4bce89394b87"&&GradientId(faded with{YFormat=ValueFormat.Number})=="lumen-4bce89394b87",GradientId(faded with{SharedReadout=true}));
    Check(GradientId(faded with{YSymmetric=5})!="lumen-4bce89394b87"&&GradientId(faded with{YFormat=ValueFormat.Signed})!="lumen-4bce89394b87","a setting kept a gradient's name");
});
Test("Sports page: Performance management stands in two panes, fitness and fatigue over the daily stress and form beneath on a signed axis held symmetric about zero, its shared readout reading the four in legend order",()=>{
    var spec=Sports("performance");
    Check(spec is {Kind:ChartKind.Line,XAxis:AxisKind.Time,SharedReadout:true,Panes.Count:1}&&spec.Panes[0] is {YSymmetric:10,YFormat:ValueFormat.Signed,Label:"Form"},"the panes");
    Check(spec.Series.Select(s=>(s.Name,s.Pane)).SequenceEqual([("Fitness",0),("Fatigue",0),("Form",1),("Daily stress",0)])&&spec.Series.All(s=>!s.Secondary)&&spec.Series[2].Kind is null&&spec.Series[3].Kind==ChartKind.Column,"the series");
    Check(spec.Description.Contains("form below")&&spec.Title.StartsWith("Fitness ")&&spec.Title.Contains(", form "),spec.Description);
    // Form stands on an axis at least ±10 tall, written with its sign.
    var doc=Svg(spec);var form=spec.Series[2].Points.Select(p=>Math.Abs(p.Y!.Value)).Max();
    Check(doc.Descendants(ns+"text").Any(t=>t.Value=="0")&&doc.Descendants(ns+"text").Any(t=>t.Value.StartsWith('−'))&&doc.Descendants(ns+"text").Any(t=>t.Value.StartsWith('+')),"the signed ticks");
    var readout=ChartSvg.Readout(spec);var today=SportsData.When(SportsData.Today);
    var column=readout.Columns.Single(c=>c.X==today);
    Check(readout.Columns.Count==spec.Series[0].Points.Count&&column.Entries.Select(e=>e.Text.Split(' ')[0]).SequenceEqual(["Fitness","Fatigue","Form","Daily"])&&column.Label==TimeAxis.Moment(today).ToString("d MMM yyyy",CultureInfo.InvariantCulture),column.Text);
    // The upper pane's clip reaches 12 past its plot for the ring round race-day fitness, and the lower one's 6.
    Check(form>0&&readout.Top==Attr(doc.Root!.Elements(ns+"svg").First(),"y")+12&&Close(readout.Bottom,Attr(doc.Root!.Elements(ns+"svg").Last(),"y")+Attr(doc.Root!.Elements(ns+"svg").Last(),"height")-6),"the guide's ends");
});
// 0.37.0: ride channels. SamplingMethod.Average draws a long line as the means of equal slices of the X range shown; a run longer than
// the budget is thinned over the visible window only; the shared readout reads the points drawn; a chart takes six plots; a pane labels
// its own ticks, TickLabels.None writing none; and PaneTitles.Above names each plot over it. Every example is invented.
ChartSpec Long37(int count,int budget=16,SamplingMethod sampling=SamplingMethod.Average,Func<int,double?>? y=null)=>new(){Title="Long",Kind=ChartKind.Line,
    Sampling=sampling,MaxRenderedPoints=budget,Series=[new("S",Enumerable.Range(0,count).Select(i=>new ChartPoint(i,y is null?i:y(i))).ToArray()){Markers=MarkerStyle.Filled}]};
string[] Names37(XDocument doc,int series=0)=>Datums(doc,series).Select(m=>m.Attribute("aria-label")!.Value).ToArray();
int[] Points37(XDocument doc,int series=0)=>Datums(doc,series).Select(m=>int.Parse(m.Attribute("data-point")!.Value,CultureInfo.InvariantCulture)).ToArray();
// An invented ride of `seconds` one-second samples on three channels, the first missing for 45 seconds from `dropout` when it is set.
ChartSpec Ride37(int seconds,int? dropout=null,int budget=600,SamplingMethod sampling=SamplingMethod.Average)=>new(){Title="Ride",Kind=ChartKind.Line,
    XFormat=ValueFormat.Duration,Sampling=sampling,MaxRenderedPoints=budget,SharedReadout=true,YTickLabels=TickLabels.None,PaneTitles=PaneTitlePlacement.Above,
    YLabel="Heart rate",Panes=[new(){Label="Power",Weight=1},new(){Label="Cadence",Weight=1}],
    Series=[new("Heart rate",Enumerable.Range(0,seconds).Select(t=>new ChartPoint(t,dropout is {} d&&t>=d&&t<d+45?null:Math.Round(140+20*Math.Sin(t/300d)))).ToArray()){Markers=MarkerStyle.None},
        new("Power",Enumerable.Range(0,seconds).Select(t=>new ChartPoint(t,Math.Round(200+60*Math.Sin(t/90d)+15*Math.Sin(t*1.7)))).ToArray()){Pane=1,Markers=MarkerStyle.None},
        new("Cadence",Enumerable.Range(0,seconds).Select(t=>new ChartPoint(t,Math.Round(85+4*Math.Sin(t*.83)))).ToArray()){Pane=2,Markers=MarkerStyle.None}]};
Test("SamplingMethod.Average draws a long line as one point a slice, at the mean X and mean Y of its points, named as the average of them; a series within the budget is drawn whole",()=>{
    // 64 points from 0 to 63 in 16 slices of 4: the first mark stands for points 0 to 3, at X and Y 1.5, the last for 60 to 63.
    var doc=Svg(Long37(64));
    var names=Names37(doc);
    Check(names.Length==16&&names.All(n=>n.EndsWith(", average of 4 points")),string.Join(" | ",names));
    // Each average is written as precisely as the values it averages, whole numbers here, half rounding away from zero.
    Check(names[0]=="S: 1.5, 2, average of 4 points"&&names[^1]=="S: 61.5, 62, average of 4 points",names[0]);
    // Values in tenths average to tenths, and in hundredths or finer to hundredths, at most.
    Check(Names37(Svg(Long37(64,y:i=>i*.25)))[0]=="S: 1.5, 0.38, average of 4 points"&&Names37(Svg(Long37(64,y:i=>Math.Round(i*.2,1))))[0]=="S: 1.5, 0.3, average of 4 points"
        &&Names37(Svg(Long37(64,y:i=>i*.0001+1)))[0]=="S: 1.5, 1, average of 4 points",string.Join(" | ",Names37(Svg(Long37(64,y:i=>i*.25)))[0],Names37(Svg(Long37(64,y:i=>Math.Round(i*.2,1))))[0]));
    Check(Points37(doc).SequenceEqual(Enumerable.Range(0,16).Select(j=>4*j)),"each average reports its slice's first point");
    // Its tooltip says the same, and it stands at its mean: X 1.5 of 0 to 63 across the plot from 76 to 870.
    var first=Datums(doc,0)[0];
    Check(first.Element(ns+"title")!.Value==names[0]&&Close(Attr(first.Element(ns+"circle")!,"cx"),76+1.5/63*794),"the first average's place or tooltip");
    // Uneven slices: 70 points in 16 slices hold 4 or 5 each, and every point is counted once.
    var uneven=Names37(Svg(Long37(70)));
    Check(uneven.Sum(n=>int.Parse(n.Split("average of ")[1].Split(' ')[0],CultureInfo.InvariantCulture))==70&&uneven.All(n=>n.Contains("average of 4 points")||n.Contains("average of 5 points")),string.Join(" | ",uneven));
    // Within the budget the series is drawn whole, as MinMax draws it, with no average in any name.
    Check(ChartSvg.Render(Long37(16))==ChartSvg.Render(Long37(16,sampling:SamplingMethod.MinMax))&&!ChartSvg.Render(Long37(16)).Contains("average of"),"a series within the budget");
    // MinMax, the default, is drawn as before: the same marks Sampling.MinMax picks, and the same SVG as a spec that leaves it unset.
    var wave=Long37(500,40,SamplingMethod.MinMax,i=>Math.Sin(i/7d)*10+i%5);
    Check(ChartSvg.Render(wave)==ChartSvg.Render(wave with{Sampling=default})&&Points37(Svg(wave)).SequenceEqual(Sampling.MinMax(wave.Series[0].Points,40)),"MinMax moved");
    // Averages smooth the noise MinMax keeps: the drawn range of a wave with spikes is narrower.
    double Range(XDocument d)=>Datums(d,0).Select(m=>Attr(m.Element(ns+"circle")!,"cy")).Max()-Datums(d,0).Select(m=>Attr(m.Element(ns+"circle")!,"cy")).Min();
    var spiky=Long37(2000,100,SamplingMethod.MinMax,i=>i%50==0?100:Math.Sin(i/100d)*10);
    Check(Range(Svg(spiky with{Sampling=SamplingMethod.Average}))<Range(Svg(spiky))/2,"the averages kept the spikes");
    // An area averages the same way and closes its fill under its first and last average; a column series is never averaged.
    var area=Svg(Long37(64) with{Kind=ChartKind.Area});
    Check(Names37(area).Length==16&&Names37(area).All(n=>n.EndsWith("average of 4 points")),"the area");
    var columns=Svg(Long37(64) with{Series=[Long37(64).Series[0] with{Kind=ChartKind.Column,Markers=MarkerStyle.Auto}]});
    Check(Names37(columns).Length==64&&!ChartSvg.Render(Long37(64) with{Series=[Long37(64).Series[0] with{Kind=ChartKind.Column,Markers=MarkerStyle.Auto}]}).Contains("average of"),"columns were averaged");
    // Classic draws the same marks.
    Check(Names37(Svg(Classic(Long37(64)))).SequenceEqual(names),"the classic finish");
});
Test("Average keeps a missing value a gap, keeps a highlighted point and the highlighted last point as marks of their own beside their slice's average, and colours a slice whose points share a colour",()=>{
    // 64 points with 30 to 33 missing: two runs, each drawn on the same slices, with no mark or stroke across the gap.
    var gapped=Svg(Long37(64,y:i=>i is >= 30 and < 34?null:i));
    var marks=Datums(gapped,0).Select(m=>(X:Attr(m.Element(ns+"circle")!,"cx"),Name:m.Attribute("aria-label")!.Value)).ToArray();
    double At(double x)=>76+x/63*794;
    Check(!marks.Any(m=>m.X>At(29.5)+1e-6&&m.X<At(34)-1e-6),string.Join(" | ",marks.Select(m=>m.Name)));
    Check(marks.Any(m=>m.Name=="S: 28.5, 29, average of 2 points")&&marks.Any(m=>m.Name=="S: 34.5, 35, average of 2 points"),"the slices at the gap's edges");
    var strokes=gapped.Descendants(ns+"path").Where(p=>(string?)p.Attribute("fill")=="none").ToArray();
    Check(strokes.Length==2,$"{strokes.Length} strokes for two runs");
    // A highlighted point keeps its own ringed mark, at its own value, beside the average of its slice, which the slice's first other
    // point names; the stroke runs through the averages alone.
    var ringed=Long37(64) with{Series=[Long37(64).Series[0] with{Points=Long37(64).Series[0].Points.Select((p,i)=>i is 4 or 9?p with{Highlight="#DD4B45",ValueNote=" · PB"}:p).ToArray()}]};
    var doc=Svg(ringed);
    Check(Points37(doc).Count(i=>i==4)==1&&Points37(doc).Count(i=>i==9)==1&&Points37(doc).Contains(5)&&Points37(doc).Contains(8)&&Points37(doc).Length==18,string.Join(",",Points37(doc)));
    var four=Datums(doc,0).Single(m=>m.Attribute("data-point")!.Value=="4");
    Check(four.Attribute("aria-label")!.Value=="S: 4, 4 · PB"&&four.Element(ns+"circle")!.Attribute("fill")!.Value=="#DD4B45","the highlighted point");
    Check(Names37(doc).Single(n=>n.StartsWith("S: 5.5,"))=="S: 5.5, 6, average of 4 points","its slice's average");
    var stroke=doc.Descendants(ns+"path").Single(p=>(string?)p.Attribute("fill")=="none").Attribute("d")!.Value;
    Check(stroke.Split(' ').Length==16,$"the stroke runs through {stroke.Split(' ').Length} points");
    // The last point keeps its ring, at its own value, beside the last slice's average.
    var last=Svg(Long37(64) with{Series=[Long37(64).Series[0] with{HighlightLast=true}]});
    var end=Datums(last,0).Single(m=>m.Attribute("data-point")!.Value=="63");
    Check(end.Elements(ns+"circle").Count()==2&&end.Attribute("aria-label")!.Value=="S: 63, 63"&&Names37(last).Contains("S: 61.5, 62, average of 4 points"),"the last point's ring");
    // Points of one colour colour their average; mixed colours leave it the series' colour.
    var inked=Long37(64) with{Series=[Long37(64).Series[0] with{Points=Long37(64).Series[0].Points.Select((p,i)=>p with{Color=i<4?"#2E9B58":i==5?"#DD4B45":null}).ToArray()}]};
    var circles=Datums(Svg(inked),0).Select(m=>m.Element(ns+"circle")!.Attribute("fill")!.Value).ToArray();
    Check(circles[0]=="#2E9B58"&&circles[1]==ChartStyle.Light.Series[0]&&circles[2]==ChartStyle.Light.Series[0],string.Join(",",circles));
    // A change-coloured series averages without a change of its own.
    var changed=Svg(Long37(64) with{Series=[Long37(64).Series[0] with{ChangeColors=ChangeColors.HigherIsBetter}]});
    Check(Names37(changed).All(n=>!n.Contains("previous")),"an average named a change");
});
Test("A run longer than the budget is thinned over the X range shown, with the nearest point outside at each side; a run within it is drawn whole, in both methods",()=>{
    foreach(var sampling in new[]{SamplingMethod.MinMax,SamplingMethod.Average})
    {
        var spec=Long37(6000,100,sampling,i=>Math.Sin(i/40d)*20+i%7);
        // Zoomed to 1000 to 4000 the marks stand for points 999 to 4001 alone, the two outside drawn as they are.
        var zoomed=Svg(spec with{XMin=1000,XMax=4000});
        var points=Points37(zoomed);
        Check(points.Min()==999&&points.Max()==4001&&points.Length<=103,$"{sampling}: {points.Length} marks from {points.Min()} to {points.Max()}");
        Check(Names37(zoomed)[0].StartsWith("S: 999,")&&!Names37(zoomed)[0].Contains("average"),Names37(zoomed)[0]);
        // Zoomed far enough, 50 seconds and the two beside them, every point is drawn.
        var close=Points37(Svg(spec with{XMin=2000,XMax=2049}));
        Check(close.SequenceEqual(Enumerable.Range(1999,52)),$"{sampling}: {close.Length} marks");
        // A window between two points keeps those two, so the line still crosses the plot.
        var between=Points37(Svg(spec with{XMin=2000.2,XMax=2000.8}));
        Check(between.SequenceEqual([2000,2001]),$"{sampling}: {string.Join(",",between)}");
        // A run within the budget is drawn whole, outside the window too, as before 0.37.0.
        var whole=Points37(Svg(Long37(50,100,sampling) with{XMin=10,XMax=20}));
        Check(whole.SequenceEqual(Enumerable.Range(0,50)),$"{sampling}: {whole.Length} marks within the budget");
        // A run wholly outside the window draws nothing; the run in view is drawn.
        var parted=Long37(3000,100,sampling,i=>i is >= 1000 and < 1010?null:i%13);
        var shown=Points37(Svg(parted with{XMin=1500,XMax=2500}));
        Check(shown.Min()==1499&&shown.Max()==2501,$"{sampling}: {shown.Min()} to {shown.Max()}");
    }
    // A band's central line and outline keep their whole run, as they always did.
    var band=new ChartSpec{Title="B",Kind=ChartKind.Band,MaxRenderedPoints=50,XMin=100,XMax=200,Series=[new("F",Enumerable.Range(0,600).Select(i=>ChartPoint.Interval(i,i%9,i%9-1,i%9+1)).ToArray())]};
    Check(Points37(Svg(band)).Min()<99,"a band was windowed");
});
Test("The shared readout reads the points the chart draws, an average as its mark is named, so a four-hour ride reads one X a mark rather than one a second",()=>{
    var ride=Ride37(14_400);
    var readout=ChartSvg.Readout(ride);
    Check(readout.Columns.Count==600&&readout.Columns.All(c=>c.Entries.Count==3),$"{readout.Columns.Count} columns");
    // The column says once what its averages take in, a 24-second slice, and each entry reads its average alone.
    Check(readout.Columns[0].Entries.All(e=>!e.Text.Contains("average")&&e.Count==24)&&readout.Columns[0].Label=="0:12 · average of 24 s"&&readout.Columns[0].Text.StartsWith("0:12 · average of 24 s · Heart rate "),readout.Columns[0].Text);
    // Each entry stands where its mark does and names its slice's first point.
    var doc=Svg(ride);
    foreach(var column in readout.Columns.Where((_,i)=>i%97==0))
        foreach(var entry in column.Entries)
        {
            var mark=doc.Descendants(ns+"g").Single(g=>(string?)g.Attribute("data-series")==entry.Series.ToString(CultureInfo.InvariantCulture)&&(string?)g.Attribute("data-point")==entry.Point.ToString(CultureInfo.InvariantCulture));
            var circle=mark.Element(ns+"circle")!;
            Check(Close(Attr(circle,"cx"),column.Position)&&Close(Attr(circle,"cy"),entry.Position!.Value)&&mark.Attribute("aria-label")!.Value.EndsWith($", {entry.Text.Split(' ')[^1]}, average of 24 points"),entry.Text);
        }
    // MinMax reads its own thinned points, at most its budget a series.
    var alone=Ride37(14_400,budget:1200,sampling:SamplingMethod.MinMax);
    var thinned=ChartSvg.Readout(alone with{Panes=[],Series=[alone.Series[0]]});
    Check(thinned.Columns.Count<=1200&&thinned.Columns.Count>600&&!thinned.Columns[5].Text.Contains("average"),$"{thinned.Columns.Count} columns");
    // A dropout in one channel stays a gap: its slices around it are read missing or not at all, the others read throughout, and the
    // columns stay within the budget but for the two slices the gap cuts.
    var dropped=ChartSvg.Readout(Ride37(7200,dropout:3600));
    Check(dropped.Columns.Count<=602&&dropped.Columns.Count(c=>c.Entries.Count==3)>=595,$"{dropped.Columns.Count} columns, {dropped.Columns.Count(c=>c.Entries.Count==3)} of all three");
    Check(dropped.Columns.Where(c=>c.X>3612&&c.X<3630).All(c=>c.Entries.All(e=>e.Series!=0)||c.Entries.Single(e=>e.Series==0).Text=="Heart rate missing"),"the dropout read a value");
    // Zoomed in, the readout reads every second.
    var zoomed=ChartSvg.Readout(ride with{XMin=1000,XMax=1199});
    string At1000(int series)=>LinearScale.Label(ride.Series[series].Points[1000].Y!.Value);
    Check(zoomed.Columns.Count==200&&zoomed.Columns[0].Text==$"16:40 · Heart rate {At1000(0)} · Power {At1000(1)} · Cadence {At1000(2)}",zoomed.Columns[0].Text);
    // The gallery's ride reads its six channels at every X but the gap's edges.
    var card=Sports("ride-channels");
    var gallery=ChartSvg.Readout(card);
    Check(card is {Sampling:SamplingMethod.Average,MaxRenderedPoints:600,PaneTitles:PaneTitlePlacement.Above,SharedReadout:true,Panes.Count:5}&&gallery.Columns.Count<=602&&gallery.Columns.Count(c=>c.Entries.Count==6)>=595,$"{gallery.Columns.Count} columns");
});
Test("A chart draws up to six plots, five panes under the main one, of equal height when their weights are equal",()=>{
    var six=new ChartSpec{Title="Six",Kind=ChartKind.Line,Height=760,Panes=Enumerable.Range(1,5).Select(k=>new ChartPane{Label=$"P{k}",Weight=1}).ToArray(),
        Series=Enumerable.Range(0,6).Select(k=>new ChartSeries($"S{k}",[new(0,k),new(1,k+1)]){Pane=k}).ToArray()};
    var spans=PaneClips(Svg(six)).Select(PaneSpan).ToArray();
    Check(spans.Length==6&&spans.All(s=>Close(s.Bottom-s.Top,(760-78-76-5*24)/6d)),string.Join(" | ",spans));
    Check(Refused(six with{Panes=[..six.Panes,new()],Series=[..six.Series,new("S6",[new(0,1)]){Pane=6}]}).StartsWith("A chart has at most six plots"),"a seventh plot");
});
Test("A pane labels its own Y ticks, or takes the spec's; TickLabels.None writes no tick label and keeps every gridline; the X axis refuses None",()=>{
    var spec=new ChartSpec{Title="Ticks",Kind=ChartKind.Line,Height=600,YTickLabels=TickLabels.All,
        Panes=[new(){Weight=1,YTickLabels=TickLabels.None},new(){Weight=1,YTickLabels=TickLabels.Ends},new(){Weight=1}],
        Series=Enumerable.Range(0,4).Select(k=>new ChartSeries($"S{k}",[new(0,10*k),new(1,10*k+40),new(2,10*k+15)]){Pane=k}).ToArray()};
    foreach(var classic in new[]{false,true})
    {
        var doc=Svg(classic?Classic(spec):spec);
        var spans=PaneClips(doc).Select(PaneSpan).ToArray();
        int Labels(int k)=>doc.Descendants(ns+"text").Count(t=>(string?)t.Attribute("text-anchor")=="end"&&Attr(t,"x")==64&&Attr(t,"y")>=spans[k].Top-1&&Attr(t,"y")<=spans[k].Bottom+5);
        int Lines(int k)=>doc.Descendants(ns+"line").Count(l=>(string?)l.Attribute("class")=="lumen-grid"&&Attr(l,"y1")>=spans[k].Top-.5&&Attr(l,"y1")<=spans[k].Bottom+.5&&Attr(l,"y1")==Attr(l,"y2"));
        Check(Labels(0)>2&&Labels(1)==0&&Labels(2)==2&&Labels(3)==Lines(3)&&Labels(0)==Lines(0),$"classic {classic}: {Labels(0)}, {Labels(1)}, {Labels(2)}, {Labels(3)} labels");
        // Every gridline stays: labelled all round, the chart draws the same lines.
        var labelled=Svg(classic?Classic(spec with{Panes=spec.Panes.Select(p=>p with{YTickLabels=TickLabels.All}).ToArray()}):spec with{Panes=spec.Panes.Select(p=>p with{YTickLabels=TickLabels.All}).ToArray()});
        Check(Lines(1)>=2&&Lines(2)>=2&&Grid(doc).SequenceEqual(Grid(labelled)),$"classic {classic}: {Lines(0)}, {Lines(1)} gridlines");
        // The spec's None reaches a pane that sets nothing.
        var none=Svg(classic?Classic(spec with{YTickLabels=TickLabels.None}):spec with{YTickLabels=TickLabels.None});
        Check(none.Descendants(ns+"text").Count(t=>(string?)t.Attribute("text-anchor")=="end"&&Attr(t,"x")==64)==2,$"classic {classic}: the spec's None");
    }
    // At the end of the enum, so the values before it keep their numbers.
    Check((int)TickLabels.All==0&&(int)TickLabels.Ends==1&&(int)TickLabels.Bounds==2&&(int)TickLabels.None==3,"the enum's values moved");
    Check(Refused(spec with{XTickLabels=TickLabels.None}).StartsWith("TickLabels.None leaves a Y axis unlabelled"),Refused(spec with{XTickLabels=TickLabels.None}));
    Check(Refused(spec with{Panes=[new(){YTickLabels=(TickLabels)9}],Series=spec.Series.Take(2).ToArray()}).StartsWith("Unknown"),"an unknown pane labelling");
    foreach(var kind in new[]{ChartKind.Donut,ChartKind.Gauge,ChartKind.Timeline,ChartKind.Calendar}) Check(Refused(Sample(kind) with{YTickLabels=TickLabels.None}).Length>0,$"{kind}");
    // A horizontal bar chart writes no value along the bottom; a histogram none up the side.
    Check(!Svg(Spec(ChartKind.Bar) with{YTickLabels=TickLabels.None}).Descendants(ns+"text").Any(t=>Attr(t,"y")>344&&Attr(t,"y")<=364&&(string?)t.Attribute("text-anchor")=="middle"),"the bar chart's values");
    Svg(Sample(ChartKind.Histogram) with{YTickLabels=TickLabels.None});
    // Hidden in the component, a pane that took the spec's labelling keeps it when another takes the main plot's place.
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    ChartSpec shown=spec;
    Operate(spec with{Panes=[spec.Panes[0],spec.Panes[1],spec.Panes[2] with{Label="Last"}]},chart=>{typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[0]);shown=(ChartSpec)typeof(LumenChart).GetMethod("VisibleSpec",flags)!.Invoke(chart,[])!;return Task.CompletedTask;});
    Check(shown.YTickLabels==TickLabels.None&&shown.Panes.Select(p=>p.YTickLabels).SequenceEqual(new TickLabels?[]{TickLabels.Ends,TickLabels.All}),$"{shown.YTickLabels} {string.Join(",",shown.Panes.Select(p=>p.YTickLabels))}");
});
Test("PaneTitles.Above writes each plot's name on one line over its left edge, cut to its width, in the text colour, clear of the title, the description and the plot above, at 340 and 1280",()=>{
    var spec=Ride37(600) with{Description="Heart rate, power and cadence, one sample a second",YLabel="Heart rate · avg 148 · max 182 · min 96 bpm",
        Panes=[new(){Label="Power · avg 205 · max 412 · min 0 W",Weight=1},new(){Label="A cadence header far too long for any phone card to hold on one line · avg 85 rpm",Weight=1}]};
    foreach(var width in new[]{340,1280})
        foreach(var classic in new[]{false,true})
        {
            var shown=spec with{Width=width};
            var doc=Svg(classic?Classic(shown):shown);
            var headers=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-pane-title").ToArray();
            var spans=PaneClips(doc).Select(PaneSpan).ToArray();
            var lines=doc.Descendants(ns+"text").Count(t=>Attr(t,"x")==24&&(Attr(t,"y")==49||Attr(t,"y")==63)&&(string?)t.Attribute("font-size")=="11");
            Check(headers.Length==3&&headers.All(h=>Attr(h,"x")==30&&h.Attribute("transform") is null&&h.Attribute("fill") is null),$"{width} classic {classic}: {headers.Length} headers");
            for(var k=0;k<3;k++)
            {
                // Its baseline 8 above its plot, the top of its letters 9 above that, clear of the plot above or the description.
                Check(Close(Attr(headers[k],"y"),spans[k].Top-8),$"{width}: header {k} at {Attr(headers[k],"y")}, its plot at {spans[k].Top}");
                var above=k==0?49+14*(lines-1)+3:spans[k-1].Bottom;
                Check(Attr(headers[k],"y")-9>above+4,$"{width}: header {k} runs into what is above it");
            }
            // What a header shows: its own text, leaving out the title a cut header carries.
            string Shown(XElement header)=>string.Concat(header.Nodes().OfType<XText>().Select(n=>n.Value));
            Check(Shown(headers[0])==spec.YLabel&&Shown(headers[1])==spec.Panes[0].Label&&(width==340?Shown(headers[2]).EndsWith("…")&&spec.Panes[1].Label.StartsWith(Shown(headers[2]).TrimEnd('…')):Shown(headers[2])==spec.Panes[1].Label),$"{width}: {Shown(headers[2])}");
            // A cut header keeps its whole as its accessible name and its tooltip, as a title does; a whole one carries neither.
            Check(width!=340||headers[2].Attribute("aria-label")?.Value==spec.Panes[1].Label&&headers[2].Element(ns+"title")?.Value==spec.Panes[1].Label&&(string?)headers[2].Attribute("role")=="img",$"{width}: the cut header lost its whole");
            Check(headers[0].Attribute("aria-label") is null&&headers[0].Element(ns+"title") is null,$"{width}: a whole header carries a title");
            // The main plot moves down 18 and each gap grows to 30; no title is written up the side.
            Check(Close(spans[0].Top,78+14*(lines-1)+18)&&Close(spans[1].Top-spans[0].Bottom,30)&&!doc.Descendants(ns+"text").Any(t=>t.Attribute("transform")?.Value.StartsWith("rotate(-90")==true),$"{width}: {spans[0].Top}");
        }
    // Written in the text colour, which clears 4.5:1 on every preset; the drawing's fill is that colour.
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight})
        Check(Lumen.Charts.Contrast.Ratio(style.Text,style.Background)>=4.5&&ChartSvg.Render(spec with{Style=style}).Contains($"color:{style.Text}"),$"{style.Background}");
    // With a label on the left the margin stays 76; with none written up the left it narrows to 30. A right-hand title stays up the side.
    var labelled=Svg(spec with{YTickLabels=TickLabels.All});
    Check(PaneClips(labelled).All(c=>Attr(c,"x")==76-6)&&PaneClips(Svg(spec)).All(c=>Attr(c,"x")==30-6),"the left margin");
    var paired=Svg(spec with{Y2Label="Speed",Series=[..spec.Series,new("Speed",[new(0,30),new(1,32)]){Secondary=true}]});
    Check(paired.Descendants(ns+"text").Any(t=>t.Value=="Speed"&&t.Attribute("transform")?.Value.StartsWith("rotate(90")==true),"the right-hand title");
    // Axis, the default, writes titles up the side, as before.
    Check(Svg(spec with{PaneTitles=PaneTitlePlacement.Axis}).Descendants(ns+"text").Count(t=>t.Attribute("transform")?.Value.StartsWith("rotate(-90")==true)==3,"titles up the side");
    // Refused where there is no Y axis up the side, and on a sparkline.
    foreach(var kind in new[]{ChartKind.Bar,ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Gauge,ChartKind.Ring,ChartKind.Timeline,ChartKind.Calendar})
        Check(Refused(Sample(kind) with{PaneTitles=PaneTitlePlacement.Above}).StartsWith("PaneTitles names each plot above it"),$"{kind}: {Refused(Sample(kind) with{PaneTitles=PaneTitlePlacement.Above})}");
    Check(Refused(Pb() with{PaneTitles=PaneTitlePlacement.Above}).StartsWith("A sparkline draws its data alone, with no words"),"a sparkline");
    Check(Refused(Spec() with{PaneTitles=(PaneTitlePlacement)2}).StartsWith("Unknown pane title placement")&&Refused(Spec() with{Sampling=(SamplingMethod)2}).StartsWith("Unknown sampling method"),"unknown values");
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Area,ChartKind.Scatter,ChartKind.Bubble,ChartKind.Column,ChartKind.StackedColumn,ChartKind.Band,ChartKind.Range,ChartKind.Candlestick,ChartKind.Ohlc,ChartKind.Blocks})
        Check(ChartSvg.Render(Sample(kind) with{PaneTitles=PaneTitlePlacement.Above,YLabel="Named"}).Contains("class='lumen-pane-title'>Named</text>"),$"{kind}");
});
Test("ChartSvg.Plot gives where the plots and the X axis stand, as the marks are placed, and the component zooms to a drag across them, no narrower than a hundredth",()=>{
    var spec=Ride37(600);
    var plot=ChartSvg.Plot(spec)!;
    var doc=Svg(spec with{Sampling=SamplingMethod.MinMax});
    var marks=Datums(doc,1);
    Check(Close(Attr(marks[0].Element(ns+"circle")!,"cx"),plot.Left)&&Close(Attr(marks[^1].Element(ns+"circle")!,"cx"),plot.Right)&&plot.X.Min==0&&plot.X.Max==599,$"{plot}");
    var spans=PaneClips(doc).Select(PaneSpan).ToArray();
    Check(Close(plot.Top,spans[0].Top)&&Close(plot.Bottom,spans[^1].Bottom),$"{plot.Top} {plot.Bottom}");
    // A range chart's X stands in from its edges by half a slot; a timeline's by its lanes' names; the other kinds have none.
    var range=ChartSvg.Plot(Sample(ChartKind.Range))!;
    Check(range.Left>76&&range.Right<870,$"{range.Left} {range.Right}");
    Check(ChartSvg.Plot(Sample(ChartKind.Timeline)) is {Top:78}&&ChartSvg.Plot(Sample(ChartKind.Column)) is null&&ChartSvg.Plot(Pb()) is null&&ChartSvg.Plot(new ChartSpec()) is null,"the kinds");
    // The component turns a drag from one position to another into a view of the X between them.
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    (double?,double?) View(LumenChart chart)=>((double?)typeof(LumenChart).GetField("viewMin",flags)!.GetValue(chart),(double?)typeof(LumenChart).GetField("viewMax",flags)!.GetValue(chart));
    (double?,double?) zoomed=default,narrowest=default,reset=default;
    Operate(spec,async chart=>{
        await chart.ZoomTo(plot.X.Map(300,plot.Left,plot.Right),plot.X.Map(150,plot.Left,plot.Right));
        zoomed=View(chart);
        typeof(LumenChart).GetMethod("ResetView",flags)!.Invoke(chart,[]);
        reset=View(chart);
        await chart.ZoomTo(plot.X.Map(300,plot.Left,plot.Right),plot.X.Map(300.5,plot.Left,plot.Right));
        narrowest=View(chart);
    });
    Check(zoomed.Item1 is {} a&&Close(a,150)&&zoomed.Item2 is {} b&&Close(b,300),$"{zoomed}");
    Check(reset==(null,null)&&narrowest.Item1 is {} c&&narrowest.Item2 is {} d&&Close(d-c,5.99)&&Close((c+d)/2,300.25),$"{narrowest}");
    // A chart without zoom ignores it.
    Operate(Spec(ChartKind.Column),async chart=>{await chart.ZoomTo(100,200);zoomed=View(chart);});
    Check(zoomed==(null,null),"a column chart zoomed");
});
Test("The component reads an averaged mark's status as its name does, and draws the same averages as ChartSvg.Render",()=>{
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var status="";
    var spec=Long37(64);
    var html=Operate(spec,async chart=>{await chart.SelectPoint(0,4);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(status=="S: 5.5 = 6, average of 4 points",status);
    Check(html.Contains("aria-label='S: 5.5, 6, average of 4 points'"),"the component's drawing");
    // MinMax marks keep reading their own point.
    Operate(spec with{Sampling=SamplingMethod.MinMax},async chart=>{await chart.SelectPoint(0,4);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(status=="S: 4 = 4",status);
    // The script draws the band, ignores touch and lets Escape go.
    var script=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Lumen.Charts.Blazor/wwwroot/lumen.js"));
    Check(script.Contains("export function drawn(root, readout, plot)")&&script.Contains("event.pointerType === 'touch'")&&script.Contains("< 8) return false")&&script.Contains("'ZoomTo'")&&script.Contains("event.key !== 'Escape'"),"the script's drag");
});
Test("Sampling, PaneTitles and a pane's YTickLabels round-trip through the HTTP API's JSON, a request that names none keeps the defaults, and only a setting away from its default renames gradients",()=>{
    var spec=Ride37(100) with{Panes=[new(){Label="Power",Weight=1,YTickLabels=TickLabels.Ends},new(){Label="Cadence",Weight=1}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,finishJson);
    Check(json.Contains("\"sampling\":\"Average\"")&&json.Contains("\"paneTitles\":\"Above\"")&&json.Contains("\"yTickLabels\":\"Ends\"")&&json.Contains("\"yTickLabels\":\"None\""),json[..400]);
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(back is {Sampling:SamplingMethod.Average,PaneTitles:PaneTitlePlacement.Above,YTickLabels:TickLabels.None}&&back.Panes[0].YTickLabels==TickLabels.Ends&&back.Panes[1].YTickLabels is null&&ChartSvg.Render(back)==ChartSvg.Render(spec),"the spec changed in transit");
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"panes\":[{}],\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]},{\"name\":\"T\",\"pane\":1,\"points\":[{\"x\":0,\"y\":1}]}]}",finishJson)!;
    Check(old.Sampling==SamplingMethod.MinMax&&old.PaneTitles==PaneTitlePlacement.Axis&&old.Panes[0].YTickLabels is null,"the defaults");
    // 0.34.0 named this gradient lumen-4bce89394b87; the defaults leave it so, and each setting away from its default names it afresh.
    string GradientId(ChartSpec s)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(s),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}],Annotations=[new(AnnotationAxis.Y,2){Label="T"}]};
    Check(GradientId(faded)=="lumen-4bce89394b87"&&GradientId(faded with{Sampling=SamplingMethod.MinMax,PaneTitles=PaneTitlePlacement.Axis})=="lumen-4bce89394b87",GradientId(faded));
    Check(GradientId(faded with{Sampling=SamplingMethod.Average})!="lumen-4bce89394b87"&&GradientId(faded with{PaneTitles=PaneTitlePlacement.Above})!="lumen-4bce89394b87"&&GradientId(faded with{YTickLabels=TickLabels.None})!="lumen-4bce89394b87","a setting kept a gradient's name");
    var paned=faded with{Panes=[new()],Series=[..faded.Series,new("P",[new(0,1)]){Pane=1}]};
    Check(GradientId(paned)!=GradientId(paned with{Panes=[new(){YTickLabels=TickLabels.None}]}),"a pane's labelling kept a gradient's name");
});
Test("Sports page: Ride channels draws an invented two-hour ride in six plots named above them, no tick label up the side, each channel averaged into 600 slices, the strap's dropout a gap",()=>{
    var spec=Sports("ride-channels");
    var ride=SportsData.LongRide();
    Check(spec.Series.Count==6&&spec.Series.All(s=>s.Points.Count==SportsData.RideSeconds&&s.Markers==MarkerStyle.None&&s.StrokeWidth==1.5&&s.Color is null)&&spec.Series.Select(s=>s.Pane).SequenceEqual([0,1,2,3,4,5]),"the series");
    Check(spec is {YTickLabels:TickLabels.None,XFormat:ValueFormat.Duration}&&spec.Panes.All(p=>p.YTickLabels is null&&p.Weight==1),"the axes");
    Check(ride.HeartRate.Count(v=>v is null)==SportsData.DropoutSeconds&&ride.HeartRate.Skip(SportsData.DropoutAt).Take(SportsData.DropoutSeconds).All(v=>v is null),"the dropout");
    var hr=ride.HeartRate.OfType<double>().ToArray();
    Check(spec.YLabel==$"HR · avg {hr.Average().ToString("0",CultureInfo.InvariantCulture)} · max {hr.Max().ToString("0",CultureInfo.InvariantCulture)} · min {hr.Min().ToString("0",CultureInfo.InvariantCulture)} bpm",spec.YLabel);
    Check(spec.Panes[2].Label.StartsWith("Speed · avg 3")&&spec.Panes[2].Label.EndsWith(" km/h")&&spec.Series[3].Points[100].Y==Math.Round(ride.Speed[100]*3.6,1),spec.Panes[2].Label);
    var doc=Svg(spec);
    Check(Datums(doc,1).Length==600&&Datums(doc,0).Length<600&&Datums(doc,0).Length>=595&&doc.Descendants(ns+"text").Count(t=>(string?)t.Attribute("class")=="lumen-pane-title")==6,$"{Datums(doc,0).Length} heart-rate marks");
    // The same ride, its data identical on every run.
    Check(SportsData.LongRide().Power.SequenceEqual(ride.Power),"the ride is not deterministic");
});
// 0.38.0: season arc and gap to the leader. Ticks set by hand, a unit after every value an axis writes, end-of-line labels, and the
// component's ShowLegend and ShowToolbar. Every example is invented.
ChartSpec Arc38(ChartStyle? style=null)=>new(){Title="Season arc",Description="Each race's place in its field, front at the top",Kind=ChartKind.Line,Style=style,
    XMin=-.5,XMax=7.5,YReversed=true,YMin=0,YMax=100,YUnit="%",YTickValues=[new(0,"Front"),new(50,"Mid"),new(100,"Back")],
    Series=[new("XCO",[new(0,40,"R1"){ValueNote=" · P17/41"},new(2,30,"R3"),new(4,null,"R5"),new(6,12,"R7")]){Markers=MarkerStyle.Filled},
        new("XCC",[new(1,20,"R2"),new(5,10,"R6")]){Markers=MarkerStyle.Filled},new("XCM",[new(3,55,"R4"),new(7,45,"R8")]){Markers=MarkerStyle.Filled}]};
// Eight invented riders' gaps to the leader over six laps, the last listed "You".
ChartSpec Gap38(int width=900,int height=420)=>new(){Title="Gap to the leader",Description="Seconds behind the leader at each lap",Kind=ChartKind.Line,Width=width,Height=height,
    YReversed=true,YMin=0,YFormat=ValueFormat.Signed,YUnit="s",
    Series=Enumerable.Range(0,8).Select(r=>new ChartSeries(r==7?"You":$"Rider {(char)('A'+r)}",Enumerable.Range(0,7).Select(lap=>new ChartPoint(lap,Math.Max(0,Math.Round(r*lap*1.7-(r==7?6*Math.Min(lap,3):0),1)),lap==0?"Start":$"Lap {lap}")).ToArray())
        {EndLabel=r==7?"You":((char)('A'+r)).ToString(),EndNote=r==0?"leader":null,StrokeWidth=r==7?3.2:2,Markers=MarkerStyle.None}).ToArray()};
XElement[] Ends38(XDocument doc)=>doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("class")=="lumen-end").ToArray();
// An end label's words as written, its note included, and the text that carries them, not the halo under it.
XElement Words38(XElement end)=>end.Elements(ns+"text").Last();
string Text38(XElement end)=>string.Concat(Words38(end).Nodes().Where(n=>n is XText||n is XElement{Name.LocalName:"tspan"}).Select(n=>n is XText t?t.Value:((XElement)n).Value));
Test("YTickValues stands each tick and gridline exactly at its value, labelled as given or in the axis's format and unit, leaves out values outside the axis, and reverses",()=>{
    var doc=Svg(Arc38());
    // The plot runs from 78 to 344, reversed, so 0 stands at the top and 100 at the bottom.
    Check(Grid(doc).SequenceEqual([78d,211,344]),string.Join(",",Grid(doc)));
    Check(Upward(doc).Select(t=>t.Text).SequenceEqual(["Front","Mid","Back"])&&Upward(doc).Select(t=>t.Y).SequenceEqual([82d,215,348]),string.Join(",",Upward(doc)));
    // A tick without a label writes its value in the axis's format with its unit; one outside the axis is left out and does not stretch it.
    var plain=Svg(Arc38() with{YTickValues=[new(0),new(25.5),new(100,"Back"),new(140,"Beyond"),new(-5)]});
    Check(Upward(plain).Select(t=>t.Text).SequenceEqual(["0%","25.5%","Back"])&&Grid(plain).Length==3&&Close(Grid(plain)[1],78+25.5/100*266),string.Join(",",Upward(plain)));
    // Not reversed, the lowest value stands at the bottom; the order given does not matter.
    var upright=Svg(Arc38() with{YReversed=false,YTickValues=[new(100,"Back"),new(0,"Front")]});
    Check(Upward(upright).Select(t=>t.Text).SequenceEqual(["Front","Back"])&&Upward(upright)[0].Y==348&&Upward(upright)[1].Y==82,string.Join(",",Upward(upright)));
    // TickLabels still chooses which labels are written: Ends the lowest and highest, None none, the gridlines staying.
    var ends=Svg(Arc38() with{YTickLabels=TickLabels.Ends,YTickValues=[new(0,"Front"),new(25),new(50,"Mid"),new(100,"Back")]});
    Check(Upward(ends).Select(t=>t.Text).SequenceEqual(["Front","Back"])&&Grid(ends).Length==4,string.Join(",",Upward(ends)));
    var none=Svg(Arc38() with{YTickLabels=TickLabels.None});
    Check(Upward(none).Length==0&&Grid(none).Length==3,"None wrote a label");
    // A pane sets its own; the main plot keeps choosing its ticks.
    var paned=Arc38() with{YTickValues=null,Panes=[new(){Label="Points",Weight=1,YMin=0,YMax=60,YTickValues=[new(0),new(30,"Half"),new(60)]}],Series=[..Arc38().Series,new("Points",[new(0,20),new(7,50)]){Pane=1}]};
    var pdoc=Svg(paned);var spans=PaneClips(pdoc).Select(PaneSpan).ToArray();
    var lower=Upward(pdoc).Where(t=>t.Y>spans[1].Top).Select(t=>t.Text).ToArray();
    Check(lower.SequenceEqual(["0","Half","60"])&&Upward(pdoc).Count(t=>t.Y<spans[0].Bottom+5)>3,string.Join(",",lower));
    // Minor gridlines are not drawn between ticks the axis did not choose.
    Check(!Svg(Arc38() with{MinorGridlines=true}).Descendants(ns+"line").Any(l=>(string?)l.Attribute("class")=="lumen-grid-minor"&&Attr(l,"y1")==Attr(l,"y2")),"minor lines on a set axis");
    // A horizontal bar chart writes its set ticks along the bottom; classic places them alike.
    var bars=Svg(Spec(ChartKind.Bar) with{YTickValues=[new(0,"none"),new(5,"most")]});
    Check(bars.Descendants(ns+"text").Count(t=>t.Value is "none" or "most")==2,"the bar chart's ticks");
    Check(Grid(Svg(Classic(Arc38()))).SequenceEqual([78d,211,344]),"classic");
});
Test("YTickValues is refused when a value is not finite or appears twice, past 24 ticks or 24 characters, at zero or below on a log axis, and where there is no such axis, each with its reason",()=>{
    var spec=Arc38();
    Check(Refused(spec with{YTickValues=[new(double.NaN)]}).StartsWith("A tick's value must be finite"),"NaN");
    Check(Refused(spec with{YTickValues=[new(10),new(10,"Ten")]}).StartsWith("Each value in YTickValues stands once"),"duplicates");
    Check(Refused(spec with{YTickValues=Enumerable.Range(0,25).Select(i=>new AxisTick(i*4)).ToArray()}).StartsWith("YTickValues takes at most 24"),"25 ticks");
    Check(Refused(spec with{YTickValues=[new(10,new string('x',25))]}).StartsWith("A tick's label is written beside the plot"),"a long label");
    Check(Refused(spec with{YTickValues=[null!]}).StartsWith("YTickValues cannot hold a null"),"a null tick");
    var logged=new ChartSpec{Title="L",Kind=ChartKind.Line,YAxis=AxisKind.Log,Series=[new("S",[new(0,1),new(1,100)])]};
    Check(Refused(logged with{YTickValues=[new(0,"none")]}).StartsWith("A logarithmic axis has no zero"),"log");
    Check(Svg(logged with{YTickValues=[new(1),new(10),new(100,"most")]}).Descendants(ns+"text").Any(t=>t.Value=="most"),"positive ticks on a log axis");
    Check(Refused(spec with{Panes=[new(){YTickValues=[new(1),new(1)]}],Series=[..spec.Series,new("P",[new(0,1)]){Pane=1}]}).StartsWith("Each value in YTickValues"),"a pane's duplicates");
    foreach(var kind in new[]{ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Gauge,ChartKind.Ring,ChartKind.Calendar})
        Check(Refused(Sample(kind) with{YTickValues=[new(1)]}).Length>0,$"{kind}");
    Check(Refused(Spark() with{YTickValues=[new(1)]}).StartsWith("A sparkline draws no axes"),"a sparkline");
    Check(Svg(spec with{YTickValues=[]}) is not null&&Grid(Svg(spec with{YTickValues=[]})).Length==0,"an empty set draws no ticks");
});
Test("YUnit follows every value the axis writes: its ticks, names and tooltips, value labels, bounds, annotations, the readout, and the component's status and table, but not a tick labelled by hand or the right-hand axis",()=>{
    var spec=new ChartSpec{Title="Gap",Kind=ChartKind.Line,YFormat=ValueFormat.Signed,YUnit="s",Annotations=[new(AnnotationAxis.Y,10){Label="Target"}],SharedReadout=true,
        Series=[new("Rider",[new(0,0,"Start"),new(1,12.3,"Lap 1"),new(2,20,"Lap 2")]){ValueLabels=true,Markers=MarkerStyle.Filled},new("Climb",[new(0,100),new(2,200)]){Secondary=true}]};
    var doc=Svg(spec);
    Check(Upward(doc).Select(t=>t.Text).All(t=>t.EndsWith('s'))&&Upward(doc).Any(t=>t.Text=="+20s")&&Upward(doc).Any(t=>t.Text=="0s"),string.Join(",",Upward(doc)));
    Check(Names37(doc)[1]=="Rider: Lap 1, +12.3s"&&Datums(doc,0)[1].Element(ns+"title")!.Value=="Rider: Lap 1, +12.3s",Names37(doc)[1]);
    Check(doc.Descendants(ns+"text").Any(t=>t.Value=="+12.3s"),"the value label");
    Check(doc.Descendants().Any(e=>(string?)e.Attribute("aria-label")=="Target: +10s"),"the annotation");
    // The right-hand axis takes none.
    Check(Names37(doc,1)[0]=="Climb: 0, 100"&&doc.Descendants(ns+"text").Any(t=>t.Value=="200"),Names37(doc,1)[0]);
    Check(ChartSvg.Readout(spec).Columns[1].Text=="Lap 1 · Rider +12.3s · Climb 150"||ChartSvg.Readout(spec).Columns[1].Text.StartsWith("Lap 1 · Rider +12.3s"),ChartSvg.Readout(spec).Columns[1].Text);
    // A unit is written exactly as given: " bpm" keeps its space. Bounds labels take it too.
    var hr=Svg(new ChartSpec{Title="HR",Kind=ChartKind.Line,YUnit=" bpm",YTickLabels=TickLabels.Bounds,YMin=100,YMax=180,Series=[new("HR",[new(0,152),new(1,160)])]});
    Check(Upward(hr).Select(t=>t.Text).SequenceEqual(["100 bpm","180 bpm"])&&Names37(hr)[0]=="HR: 0, 152 bpm",string.Join(",",Upward(hr)));
    // A pane takes its own, not the spec's.
    var paned=Svg(spec with{Series=[spec.Series[0],new("Power",[new(0,200),new(2,250)]){Pane=1}],Panes=[new(){YUnit=" W"}],SharedReadout=false});
    Check(Names37(paned,1)[0]=="Power: 0, 200 W"&&Names37(paned,0)[1].EndsWith("+12.3s"),Names37(paned,1)[0]);
    // The component's status line and data table read the unit; CSV keeps raw numbers.
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var status="";
    var html=Operate(spec with{SharedReadout=false},async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(0,1);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    html=html.Replace("&#x2B;","+");
    Check(status=="Rider: Lap 1 = +12.3s"&&html.Contains("<td>Rider</td><td>Lap 1</td><td>+12.3s</td>")&&html.Contains("<td>Climb</td><td>2</td><td>200</td>"),status);
    Check(ChartExport.Csv(spec).Contains(",12.3")&&!ChartExport.Csv(spec).Contains("12.3s"),"CSV took the unit");
    // A pane hidden in the component takes its unit and ticks with it when another takes the main plot's place.
    ChartSpec shown=spec;
    var two=new ChartSpec{Title="T",Kind=ChartKind.Line,YUnit="s",Panes=[new(){YUnit=" W",YTickValues=[new(200,"FTP")]}],Series=[new("A",[new(0,1),new(1,2)]),new("B",[new(0,200),new(1,250)]){Pane=1}]};
    Operate(two,chart=>{typeof(LumenChart).GetMethod("Toggle",flags)!.Invoke(chart,[0]);shown=(ChartSpec)typeof(LumenChart).GetMethod("VisibleSpec",flags)!.Invoke(chart,[])!;return Task.CompletedTask;});
    Check(shown.YUnit==" W"&&shown.YTickValues is [{Label:"FTP"}],$"{shown.YUnit}");
});
Test("YUnit is refused past 8 characters and where there is no such axis, each with its reason",()=>{
    Check(Refused(Spec() with{YUnit="seconds!!"}).StartsWith("YUnit is written after every value"),"long");
    foreach(var kind in new[]{ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Gauge,ChartKind.Ring,ChartKind.Timeline,ChartKind.Calendar})
        Check(Refused(Sample(kind) with{YUnit="s"}).Length>0,$"{kind}");
    Check(Refused(Spec() with{Panes=[new(){YUnit="123456789"}],Series=[..Spec().Series,new("P",[new(0,1)]){Pane=1}]}).StartsWith("YUnit is written"),"a pane's");
    Check(ChartSvg.Render(Spark() with{YUnit=" kg"}).Contains("aria-label='S: A, 3 kg'"),"a sparkline's names");
});
Test("An end label stands just right of its series' last point drawn, centred on it, in the series colour where it clears 4.5:1 and the text colour where it does not, its note muted",()=>{
    var spec=new ChartSpec{Title="Ends",Kind=ChartKind.Line,Series=[
        new("Alpha",[new(0,10),new(1,20),new(2,30)],"#1D4E89"){EndLabel="Alpha",EndNote="+3.2s"},
        new("Beta",[new(0,60),new(1,50),new(2,null)],"#FFD400"){EndLabel="Beta"}]};
    var doc=Svg(spec);var ends=Ends38(doc);
    Check(ends.Length==2,$"{ends.Length} end labels");
    // Alpha ends at x 2; Beta's last value is at x 1, its missing point drawing nothing.
    var alphaMark=Datums(doc,0)[^1].Element(ns+"circle")!;var betaMark=Datums(doc,1)[^1].Element(ns+"circle")!;
    var alpha=Words38(ends.Single(e=>Text38(e).StartsWith("Alpha")));var beta=Words38(ends.Single(e=>Text38(e)=="Beta"));
    Check(Close(Attr(alpha,"x"),Attr(alphaMark,"cx")+8)&&Close(Attr(alpha,"y"),Attr(alphaMark,"cy")+4),$"{Attr(alpha,"x")},{Attr(alpha,"y")} for {Attr(alphaMark,"cx")},{Attr(alphaMark,"cy")}");
    Check(Close(Attr(beta,"x"),Attr(betaMark,"cx")+8)&&Close(Attr(beta,"y"),Attr(betaMark,"cy")+4),"Beta's label is not at its last value");
    Check(Text38(ends[0]).Length>0&&ends.Select(Text38).Contains("Alpha +3.2s"),string.Join("|",ends.Select(Text38)));
    // Navy clears 4.5:1 on white and keeps its colour; yellow does not and takes the text colour. The note is muted at normal weight.
    Check(alpha.Attribute("fill")!.Value=="#1D4E89"&&beta.Attribute("fill")!.Value==ChartStyle.Light.Text,$"{alpha.Attribute("fill")} {beta.Attribute("fill")}");
    var note=alpha.Element(ns+"tspan")!;
    Check((string?)note.Attribute("class")=="lumen-muted"&&(string?)note.Attribute("font-weight")=="400"&&ends[0].Attribute("font-weight")!.Value=="600"&&ends[0].Attribute("font-size")!.Value=="12","the note");
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight,raceFace})
        Check(Lumen.Charts.Contrast.Ratio(style.Muted,style.Background)>=4.5&&Lumen.Charts.Contrast.Ratio(style.Text,style.Background)>=4.5,$"{style.Background}");
    // A muted colour that falls short gives the note the text colour.
    var faint=Svg(spec with{Style=ChartStyle.Light with{Muted="#A0A0A0"}});
    Check(Words38(Ends38(faint).Single(e=>Text38(e).StartsWith("Alpha"))).Element(ns+"tspan")!.Attribute("fill")?.Value==ChartStyle.Light.Text,"a faint note");
    // Each stands on a halo of the background colour, hidden from assistive technology.
    Check(ends.All(e=>e.Elements(ns+"text").First().Attribute("stroke")!.Value==ChartStyle.Light.Background&&(string?)e.Elements(ns+"text").First().Attribute("aria-hidden")=="true"),"the halo");
    // The last point's name says the label and its note, so neither is drawn only.
    Check(Names37(doc,0)[^1]=="Alpha: 2, 30, labelled Alpha · +3.2s"&&Names37(doc,1)[^1]=="Beta: 1, 50, labelled Beta"&&!Names37(doc,0)[0].Contains("labelled"),Names37(doc,0)[^1]);
    // A zoomed view follows the last point in view; a scatter series follows its point furthest along X.
    var zoomed=Svg(spec with{XMax=1});
    Check(Names37(zoomed,0).Count(n=>n.Contains("labelled"))==1&&Names37(zoomed,0).Single(n=>n.Contains("labelled")).StartsWith("Alpha: 1,"),string.Join("|",Names37(zoomed,0)));
    var scatter=Svg(new ChartSpec{Title="S",Kind=ChartKind.Scatter,Series=[new("Dots",[new(3,1),new(5,2),new(1,3)]){EndLabel="Dots"}]});
    Check(Names37(scatter)[1]=="Dots: 5, 2, labelled Dots"&&Ends38(scatter).Length==1,string.Join("|",Names37(scatter)));
    // An area and a classic chart write them the same way; a series without one writes none.
    Check(Ends38(Svg(spec with{Kind=ChartKind.Area,Series=[spec.Series[0]]})).Length==1&&Ends38(Svg(Classic(spec))).Length==2&&Ends38(Svg(Spec())).Length==0,"area, classic or none");
});
Test("End labels widen the right margin to the widest, up to half the drawing, cut past that with an ellipsis keeping their whole as name and tooltip, and the plot's layout follows",()=>{
    var spec=new ChartSpec{Title="Ends",Kind=ChartKind.Line,Width=900,Series=[new("Long",[new(0,1),new(1,2)]){EndLabel="A long rider name here",EndNote="+12.3s"}]};
    var doc=Svg(spec);
    // The plot's clip runs 6 past its right edge; without labels the margin is 30.
    double Right(XDocument d)=>Attr(PaneClips(d)[0],"x")+Attr(PaneClips(d)[0],"width")-6;
    Check(Close(Right(Svg(Spec())),870)&&Right(doc)<870&&Close(900-Right(doc),8+6+(Broad38("A long rider name here +12.3s"))+4),$"{Right(doc)}");
    Check(Text38(Ends38(doc)[0])=="A long rider name here +12.3s"&&Words38(Ends38(doc)[0]).Attribute("role") is null,"uncut");
    // ChartSvg.Plot and the readout stand where the marks do.
    Check(Close(ChartSvg.Plot(spec)!.Right,Right(doc)),"Plot's right edge");
    // At 340 the plot keeps 170: the margin stops at 340 − 76 − 170 = 94, and the label is cut.
    var narrow=Svg(spec with{Width=340});
    Check(Close(340-Right(narrow),94),$"{340-Right(narrow)}");
    var cut=Words38(Ends38(narrow)[0]);
    Check(Text38(Ends38(narrow)[0]).EndsWith("…")&&(string?)cut.Attribute("role")=="img"&&cut.Attribute("aria-label")!.Value=="A long rider name here +12.3s"&&cut.Element(ns+"title")!.Value=="A long rider name here +12.3s",Text38(Ends38(narrow)[0]));
    Check(Attr(cut,"x")+Broad38(Text38(Ends38(narrow)[0]))<=340-4+1e-6,"the cut label runs off the drawing");
    Check(Names37(narrow)[^1].EndsWith(", labelled A long rider name here · +12.3s"),"the whole stays in the name");
});
double Broad38(string text)=>text.Sum(c=>c is '.' or ',' or ':' or ' ' ? .3 : c is '-' ? .36 : c is 'm' or 'M' or 'w' or 'W' ? .9 : .62)*12;
Test("Eight end labels ending within a whisker never overlap: each set by its point's height, moved apart as little as they can be, within the plot, joined to their points when moved",()=>{
    var spec=new ChartSpec{Title="Crowded",Kind=ChartKind.Line,Width=340,Height=300,Series=Enumerable.Range(0,8).Select(k=>new ChartSeries($"S{k}",
        [new(0,10+k*9),new(5,50+k*.3)]){EndLabel=$"S{k}",EndNote=$"+{k}.0"}).ToArray()};
    foreach(var shown in new[]{spec,Classic(spec),spec with{Width=1280,Height=480},spec with{Style=ChartStyle.Midnight}})
    {
        var doc=Svg(shown);var spans=PaneClips(doc).Select(PaneSpan).ToArray();
        var ys=Ends38(doc).Select(e=>Attr(Words38(e),"y")-4).OrderBy(y=>y).ToArray();
        Check(ys.Length==8&&ys.Zip(ys.Skip(1)).All(p=>p.Second-p.First>=14-1e-6),string.Join(",",ys));
        Check(ys[0]>=spans[0].Top-1-1e-6&&ys[^1]<=spans[0].Bottom+1+1e-6,"outside the plot");
        // The highest value on an upright axis stands highest: S7 at the top.
        Check(Text38(Ends38(doc).OrderBy(e=>Attr(Words38(e),"y")).First()).StartsWith("S7"),"the order");
        // Each moved label is joined to its point by a thin line; the labels are centred on their points as a group.
        Check(doc.Descendants(ns+"line").Count(l=>(string?)l.Attribute("aria-hidden")=="true"&&(string?)l.Attribute("stroke-width")=="1")>=6,"connectors");
    }
    // Two sets whose spans never meet are set apart on their own: a label ending mid-plot keeps its place.
    var apart=new ChartSpec{Title="Apart",Kind=ChartKind.Line,Series=[new("Early",[new(0,50),new(2,50)]){EndLabel="Early"},new("Late",[new(0,49),new(10,50)]){EndLabel="Late"}]};
    var adoc=Svg(apart);
    Check(Ends38(adoc).All(e=>Close(Attr(Words38(e),"y")-4,Attr(Datums(adoc,Text38(e)=="Early"?0:1)[^1].Element(ns+"circle")!,"cy"))),"a label was moved");
    // Past what the plot holds even at 12 a line, the lowest are left out, their words kept in their names.
    var many=new ChartSpec{Title="Many",Kind=ChartKind.Line,Height=240,Series=Enumerable.Range(0,20).Select(k=>new ChartSeries($"S{k}",[new(0,k),new(1,50)]){EndLabel=$"S{k}"}).ToArray()};
    var mdoc=Svg(many);var mys=Ends38(mdoc).Select(e=>Attr(Words38(e),"y")).OrderBy(y=>y).ToArray();
    Check(mys.Length<20&&mys.Length>=7&&mys.Zip(mys.Skip(1)).All(p=>p.Second-p.First>=12-1e-6)&&Enumerable.Range(0,20).All(k=>Names37(mdoc,k)[^1].Contains("labelled")),$"{mys.Length} labels");
});
Test("End labels are refused on other marks, on a density scatter, beside a right-hand axis, on a sparkline, past 24 characters, blank, or as a note alone, each with its reason",()=>{
    var line=new ChartSeries("S",[new(0,1),new(1,2)]){EndLabel="S"};
    Check(Refused(Spec(ChartKind.Column) with{Series=[line]}).StartsWith("An end label is written after a series' last point"),"columns");
    Check(Refused(Sample(ChartKind.Band) with{Series=[Sample(ChartKind.Band).Series[0] with{EndLabel="B"}]}).StartsWith("An end label is written"),"a band");
    Check(Refused(Spec(ChartKind.Scatter) with{DensityCells=10,Series=[line]}).StartsWith("A density scatter shades cells"),"density");
    Check(Refused(Spec() with{Series=[line,new("R",[new(0,5)]){Secondary=true}]}).StartsWith("End labels are written in the margin right of the plot"),"a secondary series");
    Check(Refused(Spec() with{YAxisSide=AxisSide.Right,Series=[line]}).StartsWith("End labels are written in the margin"),"a right-hand axis");
    Check(Refused(Spark() with{Series=[Spark().Series[0] with{EndLabel="S"}]}).StartsWith("A sparkline draws its data alone, with no words, so it writes no end labels"),"a sparkline");
    Check(Refused(Spec() with{Series=[line with{EndLabel=new string('x',25)}]}).StartsWith("An end label and its note each take at most 24"),"long");
    Check(Refused(Spec() with{Series=[line with{EndNote=new string('x',25)}]}).StartsWith("An end label and its note each take at most 24"),"a long note");
    Check(Refused(Spec() with{Series=[line with{EndLabel="  "}]}).StartsWith("An end label names its series"),"blank");
    Check(Refused(Spec() with{Series=[line with{EndLabel=null,EndNote="+1s"}]}).StartsWith("EndNote is written after"),"a note alone");
    // A line on a column chart takes one: its last category's slot.
    Check(Ends38(Svg(Spec(ChartKind.Column) with{Series=[new("C",[new(0,1),new(1,2)]),line with{Kind=ChartKind.Line}]})).Length==1,"a line on a column chart");
});
Test("Season arc: lines joined only over their own races at their global index, a missing race a gap, Front, Mid and Back up a reversed percentile axis",()=>{
    var doc=Svg(Arc38(raceFace) with{Width=340,Height=300});
    // XCO runs R1–R3, breaks at its missing R5, and R7 stands alone; XCC joins R2 to R6 over the races between.
    var strokes=doc.Descendants(ns+"path").Where(p=>(string?)p.Attribute("fill")=="none").ToArray();
    Check(strokes.Length==4,$"{strokes.Length} strokes");
    Check(Names37(doc,0).SequenceEqual(["XCO: R1, 40% · P17/41","XCO: R3, 30%","XCO: R7, 12%"])&&Names37(doc,1)[1]=="XCC: R6, 10%",string.Join("|",Names37(doc,0)));
    Check(Upward(doc).Select(t=>t.Text).SequenceEqual(["Front","Mid","Back"]),"the ticks");
});
Test("The gap chart: every rider's end label and note, the leader's note, a readout reading all eight with their units, legend off, and the defaults hashing as before",()=>{
    var spec=Gap38(340,320);
    var doc=Svg(spec);
    Check(Ends38(doc).Length==8&&Ends38(doc).Select(Text38).Contains("A leader")&&Ends38(doc).Select(Text38).Contains("You"),string.Join("|",Ends38(doc).Select(Text38)));
    var readout=ChartSvg.Readout(spec with{SharedReadout=true});
    Check(readout.Columns.Count==7&&readout.Columns.All(c=>c.Entries.Count==8)&&readout.Columns[3].Text.Contains("You +")&&readout.Columns[3].Text.Split(" · ").Skip(1).All(t=>t.EndsWith('s')),readout.Columns[3].Text);
    Check(!ChartSvg.Render(spec,includeLegend:false).Contains("font-size='11'>Rider A</text>"),"the legend");
});
Test("ShowLegend and ShowToolbar: off, the legend and the buttons are gone, the status line stays out of sight and still reads the point chosen; on, the markup is as before",()=>{
    RenderFragment Shown(ChartSpec spec,bool legend,bool toolbar)=>b=>{b.OpenComponent<LumenChart>(0);b.AddAttribute(1,"Spec",spec);b.AddAttribute(2,"ShowLegend",legend);b.AddAttribute(3,"ShowToolbar",toolbar);b.CloseComponent();};
    var spec=Spec();
    var bare=Prerender(Shown(spec,false,false));
    Check(!bare.Contains("lumen-legend")&&!bare.Contains("<button")&&bare.Contains("<div class=\"lumen-tools lumen-quiet\">\n        <span class=\"lumen-status\" role=\"status\">"),bare[..Math.Min(bare.Length,400)]);
    Check(Prerender(Shown(spec,true,true))==Prerender(ChartElement(spec)),"setting both on changed the markup");
    var legendOnly=Prerender(Shown(spec,true,false));var toolsOnly=Prerender(Shown(spec,false,true));
    Check(legendOnly.Contains("lumen-legend")&&!legendOnly.Contains("Export SVG")&&!toolsOnly.Contains("lumen-legend")&&toolsOnly.Contains("Export SVG")&&toolsOnly.Contains("Reset view"),"one without the other");
    var css=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Lumen.Charts.Blazor/wwwroot/lumen.css"));
    Check(css.Contains(".lumen-quiet .lumen-status{position:absolute;width:1px;height:1px;")&&css.Contains("clip-path:inset(50%)"),"the status line is not hidden out of sight");
    // The status line still reads a chosen point.
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try{
        var html=renderer.Dispatcher.InvokeAsync(async()=>{
            LumenChart? chart=null;
            RenderFragment content=b=>{b.OpenComponent<LumenChart>(0);b.AddAttribute(1,"Spec",spec);b.AddAttribute(2,"ShowToolbar",false);b.AddComponentReferenceCapture(3,c=>chart=(LumenChart)c);b.CloseComponent();};
            var root=await renderer.RenderComponentAsync<CascadingValue<ChartStyle>>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Value",ChartStyle.Light},{"ChildContent",content}}));
            await chart!.SelectPoint(0,1);await root.QuiescenceTask;return root.ToHtmlString();}).GetAwaiter().GetResult();
        Check(html.Contains("<span class=\"lumen-status\" role=\"status\">Series: B = 5</span>"),"the hidden status line did not read the point");
    }finally{renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
});
Test("Ticks, units and end labels round-trip through the HTTP API's JSON, a request that names none keeps the defaults, a spec written out with them null draws byte for byte as before, and only a setting renames gradients",()=>{
    var spec=Arc38() with{Panes=[new(){YUnit=" W",YTickValues=[new(1,"One")]}],Series=[..Arc38().Series.Select(s=>s with{EndLabel=s.Name,EndNote="n"}),new("P",[new(0,1),new(1,2)]){Pane=1}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,finishJson);
    Check(json.Contains("\"yTickValues\":[{\"value\":0,\"label\":\"Front\"}")&&json.Contains("\"yUnit\":\"%\"")&&json.Contains("\"endLabel\":\"XCO\"")&&json.Contains("\"endNote\":\"n\"")&&json.Contains("\"yUnit\":\" W\""),json[..300]);
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(back.YUnit=="%"&&back.YTickValues![1]==new AxisTick(50,"Mid")&&back.Panes[0].YTickValues![0]==new AxisTick(1,"One")&&back.Series[0].EndNote=="n"&&ChartSvg.Render(back)==ChartSvg.Render(spec),"the spec changed in transit");
    var written="{\"title\":\"G\",\"kind\":\"Line\",\"yUnit\":\"s\",\"yTickValues\":[{\"value\":0},{\"value\":10,\"label\":\"Ten\"}],\"series\":[{\"name\":\"R\",\"endLabel\":\"R\",\"points\":[{\"x\":0,\"y\":0},{\"x\":1,\"y\":12}]}]}";
    var drawn=Svg(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,finishJson)!);
    Check(Upward(drawn).Select(t=>t.Text).SequenceEqual(["0s","Ten"])&&Ends38(drawn).Length==1&&Names37(drawn)[^1]=="R: 1, 12s, labelled R","a spec written by hand");
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Line\",\"panes\":[{}],\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]},{\"name\":\"T\",\"pane\":1,\"points\":[{\"x\":0,\"y\":1}]}]}",finishJson)!;
    Check(old.YUnit is null&&old.YTickValues is null&&old.Panes[0].YUnit is null&&old.Panes[0].YTickValues is null&&old.Series[0].EndLabel is null&&old.Series[0].EndNote is null,"the defaults");
    string GradientId(ChartSpec s)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(s),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}],Annotations=[new(AnnotationAxis.Y,2){Label="T"}]};
    Check(GradientId(faded)=="lumen-4bce89394b87"&&GradientId(faded with{YUnit=null,YTickValues=null,Series=[faded.Series[0] with{EndLabel=null,EndNote=null}]})=="lumen-4bce89394b87",GradientId(faded));
    Check(GradientId(faded with{YUnit="s"})!="lumen-4bce89394b87"&&GradientId(faded with{YTickValues=[new(2)]})!="lumen-4bce89394b87"&&GradientId(faded with{Series=[faded.Series[0] with{EndLabel="S"}]})!="lumen-4bce89394b87","a setting kept a gradient's name");
    // Every kind's sample draws byte for byte the same with the new settings written out at their defaults.
    foreach(var kind in Enum.GetValues<ChartKind>())
        Check(ChartSvg.Render(Sample(kind))==ChartSvg.Render(Sample(kind) with{YUnit=null,YTickValues=null,Series=Sample(kind).Series.Select(s=>s with{EndLabel=null,EndNote=null}).ToArray()}),$"{kind}");
});
Test("Sports page: Season arc and Gap to the leader close the Racing section, the arc's disciplines joined over each other's races and the gap's riders named at their ends with the legend off",()=>{
    var ids=sports.Where(card=>card.Section=="racing").Select(card=>card.Id).ToArray();
    Check(ids.SequenceEqual(["race-results","field","season-arc","gap"])&&sports.Single(card=>card.Id=="gap").ShowLegend==false&&sports.Where(card=>card.Id is not ("gap" or "scores")).All(card=>card.ShowLegend),string.Join(",",ids));
    var arc=Sports("season-arc");
    Check(arc is {YReversed:true,YMin:0,YMax:100,YUnit:"%"}&&arc.YTickValues!.Select(t=>t.Label).SequenceEqual(["Front","Mid","Back"])&&arc.Series.Select(s=>s.Name).SequenceEqual(["XCC","XCO","XCM","Other"]),"the arc");
    // Every race stands once, in one discipline, at its index in the season; the race not finished is a gap in its own line.
    Check(arc.Series.SelectMany(s=>s.Points).Select(p=>p.X).Order().SequenceEqual(Enumerable.Range(0,SportsData.SeasonRaces.Count).Select(i=>(double)i))&&arc.Series[1].Points.Count(p=>p.Y is null)==1,"the races");
    var gap=Sports("gap");var doc=Svg(gap with{Width=340});
    Check(gap.Series.Count==8&&gap.Series[^1] is {Name:"You",EndLabel:"You",StrokeWidth:3.2}&&Ends38(doc).Length==8&&Ends38(doc).Select(Text38).Count(t=>t.EndsWith(" leader"))==1,string.Join("|",Ends38(doc).Select(Text38)));
    Check(gap.Title==$"You finished +{SportsData.LapGaps()[^1][^1].ToString(CultureInfo.InvariantCulture)}s back"&&Sports("season-arc").Title=="Top quarter in 3 of 10",gap.Title);
    // No end label is cut on a phone's card, and every one stands inside the drawing.
    Check(Ends38(doc).All(e=>!Text38(e).EndsWith("…")&&Attr(Words38(e),"x")+Broad38(Text38(e))<=340),"a label was cut on a phone");
});
// 0.39.0: proportions and meters. A strip of parts as shares of one bar with its own key, tracks behind bars, and charts that keep their
// title and description as their name without drawing them. Every example is invented.
ChartSpec Strip39(params (string Name,double Amount)[] parts)=>new(){Title="Effort zones",Description="Time in each heart-rate zone",Kind=ChartKind.Strip,Width=340,YFormat=ValueFormat.Duration,
    Series=[new("Zones",parts.Select((p,i)=>new ChartPoint(i,p.Amount,p.Name)).ToArray())]};
ChartSpec Zones39()=>Strip39(("Easy",740),("Moderate",1290),("Hard",820),("Very hard",250));
// A part's outline as the drawing writes it: its left and right edges, from every point of its path that is not an arc's radii.
(double Left,double Right,string Path) Part39(XElement mark)
{
    var d=mark.Element(ns+"path")!.Attribute("d")!.Value;
    var xs=d.Split(' ').Where(t=>t.Contains(',')&&!t.StartsWith('A')).Select(t=>double.Parse(t.TrimStart('M','L').Split(',')[0],CultureInfo.InvariantCulture)).ToArray();
    return (xs.Min(),xs.Max(),d);
}
string[] Keys39(XDocument doc)=>doc.Descendants(ns+"g").Single(g=>(string?)g.Attribute("class")=="lumen-strip-key").Elements(ns+"text").Select(t=>t.Value).ToArray();
double Height39(XDocument doc)=>double.Parse(doc.Root!.Attribute("viewBox")!.Value.Split(' ')[3],CultureInfo.InvariantCulture);
Test("A strip draws each part as long as its share across the drawing less 24 each side, parted by 2-unit gaps, its outer ends rounded, a part of zero drawing nothing",()=>{
    var doc=Svg(Zones39());var marks=Datums(doc,0);
    Check(marks.Length==4,$"{marks.Length} parts");
    var parts=marks.Select(Part39).ToArray();
    // 292 units across: 740, 1290, 820 and 250 of 3100.
    double[] ends=[24,24+740/3100d*292,24+2030/3100d*292,24+2850/3100d*292,316];
    for(var i=0;i<4;i++)
        Check(Close(parts[i].Left,ends[i]+(i==0?0:1))&&Close(parts[i].Right,ends[i+1]-(i==3?0:1)),$"part {i}: {parts[i].Left}–{parts[i].Right}");
    // The gap between two neighbours is 2 units, in the background colour that shows through it.
    Check(Close(parts[1].Left-parts[0].Right,2)&&Close(parts[3].Left-parts[2].Right,2),"the gaps");
    // Only the outer ends are rounded, by 6 units unless the style says otherwise; the inner parts are square.
    Check(parts[0].Path.Contains(" A6,6 0 0 1 24,")&&!parts[0].Path.Contains("A6,6 0 0 1 "+SvgN(parts[0].Right))&&!parts[1].Path.Contains(" A")&&!parts[2].Path.Contains(" A")&&parts[3].Path.Contains(" A6,6 0 0 1 316,"),parts[0].Path);
    // 18 units thick, 64 from the top under a one-line description.
    var top=parts[0].Path.Split(' ')[0].Split(',')[1];
    Check(top=="64"&&parts[1].Path.Contains(",82 "),parts[1].Path);
    // A part's colour is its own, else the style's series colours in order.
    Check(marks.Select(m=>m.Element(ns+"path")!.Attribute("fill")!.Value).SequenceEqual(ChartStyle.Light.Series.Take(4)),"the colours");
    // A part of zero draws nothing, its neighbours meet across one gap, and it keeps its entry in the key.
    var zero=Svg(Strip39(("Easy",740),("Moderate",1290),("Hard",0),("Very hard",250)));
    Check(Datums(zero,0).Select(m=>m.Attribute("data-point")!.Value).SequenceEqual(["0","1","3"])&&Close(Part39(Datums(zero,0)[2]).Left-Part39(Datums(zero,0)[1]).Right,2),"a part of zero");
    Check(Keys39(zero).SequenceEqual(["Easy 32%","Moderate 57%","Hard 0%","Very hard 11%"]),string.Join("|",Keys39(zero)));
    // One part fills the whole width, both ends rounded; a style's bar radius rounds them, clamped to half the thickness.
    var whole=Part39(Datums(Svg(Strip39(("All",5))),0)[0]);
    Check(Close(whole.Left,24)&&Close(whole.Right,316)&&whole.Path.Split(" A").Length==5,whole.Path);
    Check(Part39(Datums(Svg(Zones39() with{Style=ChartStyle.Light with{BarRadius=9999}}),0)[0]).Path.Contains(" A9,9 ")&&Part39(Datums(Svg(Zones39() with{Style=ChartStyle.Light with{BarRadius=0}}),0)[0]).Path.Split(" A").Length==1,"the radius");
    // A sliver keeps one unit.
    var sliver=Svg(Strip39(("Most",10000),("Least",1),("Rest",5000)));
    Check(Part39(Datums(sliver,0)[1]) is var thin&&Close(thin.Right-thin.Left,1),"a sliver");
});
string SvgN(double value)=>value.ToString("0.########",CultureInfo.InvariantCulture);
Test("A strip's key writes every part's whole percentage, adding up to exactly 100 by the largest remainder, ties to the part listed later, flowing and wrapping across the width",()=>{
    Check(Keys39(Svg(Zones39())).SequenceEqual(["Easy 24%","Moderate 42%","Hard 26%","Very hard 8%"]),string.Join("|",Keys39(Svg(Zones39()))));
    Check(Keys39(Svg(Strip39(("A",1),("B",1),("C",1)))).SequenceEqual(["A 33%","B 33%","C 34%"]),string.Join("|",Keys39(Svg(Strip39(("A",1),("B",1),("C",1))))));
    Check(Keys39(Svg(Strip39(("A",1),("B",1),("C",1),("D",1),("E",1),("F",1)))).Select(k=>k[2..]).SequenceEqual(["16%","16%","17%","17%","17%","17%"]),"six sixths");
    var random=new Random(39);
    for(var n=0;n<200;n++)
    {
        var amounts=Enumerable.Range(0,1+random.Next(8)).Select(i=>(i.ToString(CultureInfo.InvariantCulture),(double)random.Next(0,5000))).ToArray();
        if(amounts.All(a=>a.Item2==0))continue;
        var shares=Keys39(Svg(Strip39(amounts))).Select(k=>int.Parse(k[(k.IndexOf(' ')+1)..^1],CultureInfo.InvariantCulture)).ToArray();
        Check(shares.Sum()==100&&shares.Zip(amounts).All(p=>Math.Abs(p.First-p.Second.Item2/amounts.Sum(a=>a.Item2)*100)<1),string.Join(",",shares));
    }
    // At 340 the four entries take two rows, 20 apart, every one inside the drawing; at 900 one row.
    var doc=Svg(Zones39());
    var texts=doc.Descendants(ns+"g").Single(g=>(string?)g.Attribute("class")=="lumen-strip-key").Elements(ns+"text").ToArray();
    Check(texts.Select(t=>Attr(t,"y")).Distinct().SequenceEqual([106d,126])&&texts.All(t=>Attr(t,"x")+Broad38(t.Value)<=340-24+1e-6)&&Attr(texts[0],"x")==39,string.Join(",",texts.Select(t=>$"{Attr(t,"x")},{Attr(t,"y")}")));
    Check(Svg(Zones39() with{Width=900}).Descendants(ns+"g").Single(g=>(string?)g.Attribute("class")=="lumen-strip-key").Elements(ns+"text").Select(t=>Attr(t,"y")).Distinct().Count()==1,"one row at 900");
    // Each entry's swatch is its part's colour, 10 units square, in the text colour's words.
    var swatches=doc.Descendants(ns+"g").Single(g=>(string?)g.Attribute("class")=="lumen-strip-key").Elements(ns+"rect").ToArray();
    Check(swatches.Length==4&&swatches.Select(r=>r.Attribute("fill")!.Value).SequenceEqual(ChartStyle.Light.Series.Take(4))&&texts.All(t=>t.Attribute("fill") is null&&t.Attribute("class") is null),"the swatches");
});
Test("A strip's parts are named by their share and their amount in its format and unit, its height is its content's whatever Height says, and neither its legend nor the component's is drawn",()=>{
    var doc=Svg(Zones39());
    Check(Names37(doc).SequenceEqual(["Easy: 24%, 12:20","Moderate: 42%, 21:30","Hard: 26%, 13:40","Very hard: 8%, 4:10"])&&Datums(doc,0)[0].Element(ns+"title")!.Value=="Easy: 24%, 12:20",string.Join("|",Names37(doc)));
    Check(ChartSvg.PartLabel(Zones39(),3)=="Very hard: 8%, 4:10"&&ChartSvg.PartLabel(Zones39() with{YFormat=ValueFormat.Number,YUnit=" min"},0)=="Easy: 24%, 740 min","PartLabel");
    var noted=Strip39(("Pedalling",3000),("Coasting",600)) with{Series=[new("Cadence",[new(0,3000,"Pedalling"),new(1,600,"Coasting"){ValueNote=" · freewheel"}])]};
    Check(Names37(Svg(noted))[1]=="Coasting: 17%, 10:00 · freewheel",Names37(Svg(noted))[1]);
    Check(Datums(doc,0).All(m=>(string?)m.Attribute("tabindex")=="0"&&(string?)m.Attribute("role")=="button"),"focusable marks");
    Reject(()=>ChartSvg.PartLabel(Spec(),0));
    // Two rows of the key under a one-line description: the last baseline 64 + 18 + 24 + 20 = 126, and 16 under it.
    Check(Height39(doc)==142&&Height39(Svg(Zones39() with{Height=16}))==142&&ChartSvg.Render(Zones39() with{Height=2000})==ChartSvg.Render(Zones39()),$"{Height39(doc)}");
    Check(Height39(Svg(Zones39() with{Width=900}))==122&&Height39(Svg(Zones39() with{DrawTitles=false}))==92,"one row, and no titles");
    Check(Height39(Svg(Zones39() with{Description="A description long enough to go on over a second line on a card as narrow as a phone's"}))==156,"two-line description");
    Check(Height39(Svg(Zones39() with{Source="Invented ride"}))==158&&Svg(Zones39() with{Source="Invented ride"}).Descendants(ns+"text").Any(t=>t.Value=="Invented ride"&&Attr(t,"y")==146),"a source line");
    Check(Refused(Zones39() with{Height=15}).StartsWith("A strip's width must be 320–4096")&&Refused(Zones39() with{Width=319}).StartsWith("A strip's width")&&Refused(Zones39() with{Height=2161}).StartsWith("A strip's width"),"dimensions");
    // The series legend under the chart would repeat the key, so it is never drawn; nor is the component's.
    Check(ChartSvg.Render(Zones39(),includeLegend:true)==ChartSvg.Render(Zones39(),includeLegend:false),"the static legend");
    var html=Prerender(ChartElement(Zones39()));
    Check(!html.Contains("lumen-legend")&&html.Contains("Export SVG"),"the component's legend");
    Check(Prerender(ChartElement(Spec())).Contains("lumen-legend"),"a line chart lost its legend");
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var status="";
    Operate(Zones39(),async chart=>{await chart.SelectPoint(0,1);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(status=="Moderate: 42%, 21:30",status);
    // An empty strip draws its empty state; CSV carries its parts as rows.
    Check(ChartSvg.Render(new ChartSpec{Kind=ChartKind.Strip}).Contains("No data to display")&&ChartSvg.Render(Zones39() with{Series=[new("Z",[])]}).Contains("No data to display"),"empty");
    Check(ChartExport.Csv(Zones39()).Contains("\"Zones\",1,1290,\"Moderate\""),ChartExport.Csv(Zones39()));
    // Readout and Plot have nothing to read on a strip.
    Check(ChartSvg.Readout(Zones39()).Columns.Count==0&&ChartSvg.Plot(Zones39()) is null,"readout");
    // In Midnight, every key's words and every part's name read the same; the words are the style's text colour.
    var midnight=Svg(Zones39() with{Style=ChartStyle.Midnight});
    Check(Keys39(midnight).SequenceEqual(Keys39(doc))&&Lumen.Charts.Contrast.Ratio(ChartStyle.Midnight.Text,ChartStyle.Midnight.Background)>=4.5,"Midnight");
});
Test("A strip refuses several series, panes, missing, negative or blank parts, all zeros, more than 24 parts, annotations, zones, value labels and axis settings, each with its reason",()=>{
    var spec=Zones39();
    Check(Refused(spec with{Series=[spec.Series[0],spec.Series[0] with{Name="Again"}]}).StartsWith("A strip draws the parts of one whole"),"two series");
    Check(Refused(spec with{Panes=[new()]}).StartsWith("A strip is one bar, so it takes no panes"),"panes");
    Check(Refused(spec with{Series=[spec.Series[0] with{Pane=1}]}).StartsWith("A strip is one bar"),"a series' pane");
    Check(Refused(Strip39(("A",1),("B",-1))).StartsWith("A part of a strip is an amount"),"negative");
    Check(Refused(Strip39(("A",1),("B",double.NaN))).StartsWith("A part of a strip is an amount"),"NaN");
    Check(Refused(spec with{Series=[new("Z",[new(0,1,"A"),new(1,null,"B")])]}).StartsWith("A part of a strip is an amount"),"missing");
    Check(Refused(spec with{Series=[new("Z",[new(0,1,"A"),new(1,2)])]}).StartsWith("Each part of a strip is named"),"no label");
    Check(Refused(Strip39(("A",0),("B",0))).StartsWith("A strip shows each part's share of the whole, and parts that are all zero"),"all zero");
    Check(Refused(Strip39(Enumerable.Range(0,25).Select(i=>($"P{i}",1d)).ToArray())).StartsWith("A strip takes at most 24 parts"),"25 parts");
    Check(Svg(Strip39(Enumerable.Range(0,24).Select(i=>($"P{i}",1d)).ToArray())) is not null,"24 parts");
    Check(Refused(spec with{Annotations=[new(AnnotationAxis.X,1)]}).StartsWith("A strip has no axes, so it takes no annotations")&&Refused(spec with{Annotations=[new(AnnotationAxis.Y,1)]}).StartsWith("A strip has no axes")&&Refused(spec with{YZones=new([new("Z",double.PositiveInfinity)])}).StartsWith("A strip has no axes"),"annotations and zones");
    Check(Refused(spec with{Series=[spec.Series[0] with{ValueLabels=true}]}).StartsWith("A strip's key writes each part's share"),"value labels");
    Check(Refused(spec with{Series=[spec.Series[0] with{Kind=ChartKind.Column}]}).StartsWith("A strip draws its one series as the parts")&&Refused(spec with{Series=[spec.Series[0] with{Trend=true}]}).StartsWith("A strip draws its one series"),"a series' kind and trend");
    foreach(var axis in new[]{spec with{YMin=0},spec with{YMax=10},spec with{XAxis=AxisKind.Time},spec with{YReversed=true},spec with{YAxis=AxisKind.Log},spec with{IncludeZero=true},spec with{YTickLabels=TickLabels.None},spec with{YAxisSide=AxisSide.Right},spec with{MinorGridlines=true},spec with{XMax=3},spec with{YTickValues=[new(1)]}})
        Check(Refused(axis).StartsWith("A strip draws its parts as shares of one bar and has no axes"),Refused(axis));
    Check(Refused(spec with{BarTrack=true}).StartsWith("BarTrack draws a track"),"a track");
    Check(Refused(spec with{SharedReadout=true}).StartsWith("SharedReadout")&&Refused(spec with{XTicks=TickSource.Axis}).StartsWith("XTicks")&&Refused(spec with{Sparkline=true,Width=120,Height=32}).StartsWith("A sparkline draws"),"readout, ticks and sparkline");
    Check(Refused(spec with{Series=[new("Z",[new(0,1,"A"){Highlight="#123456"}])]}).StartsWith("A highlight rings"),"a highlight");
    // A point's colour, a value note, a unit and a format are what a strip takes.
    Check(Svg(spec with{YUnit=" s",YFormat=ValueFormat.Number,Series=[new("Z",[new(0,1,"A"){Color="#123456",ValueNote=" n"}])]}) is var one&&Names37(one)[0]=="A: 100%, 1 s n"&&Datums(one,0)[0].Element(ns+"path")!.Attribute("fill")!.Value=="#123456",Names37(one)[0]);
});
ChartSpec Scores39(ChartStyle? style=null)=>new(){Title="Race scores",Description="Each out of 100",Kind=ChartKind.Bar,Width=340,Height=240,Style=style,YMin=0,YMax=100,BarTrack=true,YTickLabels=TickLabels.None,
    Series=[new("Score",[new(0,82,"Execution"),new(1,64,"Improvement"),new(2,91,"Effort"),new(3,58,"Consistency")]){ValueLabels=true}]};
XElement[] Tracks39(XDocument doc)=>doc.Descendants().Where(e=>(string?)e.Attribute("class")=="lumen-bar-track").ToArray();
Test("BarTrack draws a track behind each bar from zero to YMax in the grid colour, rounded as the bar is, the bar at most 18 thick and centred, its label just past the track's end",()=>{
    var doc=Svg(Scores39());var tracks=Tracks39(doc);var bars=Datums(doc,0).Select(m=>m.Element(ns+"rect")!).ToArray();
    Check(tracks.Length==4&&tracks.All(t=>t.Name==ns+"rect"&&t.Attribute("fill")!.Value==ChartStyle.Light.Grid&&t.Attribute("rx")!.Value=="2"),"the tracks");
    // Category names: Improvement, with its two m's, is the widest; the left margin is its width and 24.
    var left=Math.Ceiling(new[]{"Execution","Improvement","Effort","Consistency"}.Max(Broad38))+24;
    // Value labels: two digits, 6 past the track's end and 6 of room after them, need less than the least right margin, 30.
    var right=340-Math.Max(30,Math.Ceiling(6+2*.62*11+6));
    for(var i=0;i<4;i++)
    {
        Check(Close(Attr(tracks[i],"x"),left)&&Close(Attr(tracks[i],"width"),right-left)&&Close(Attr(tracks[i],"y"),Attr(bars[i],"y"))&&Close(Attr(tracks[i],"height"),Attr(bars[i],"height")),$"track {i}: {Attr(tracks[i],"x")} {Attr(tracks[i],"width")}");
        Check(Close(Attr(bars[i],"x"),left)&&Close(Attr(bars[i],"width"),(right-left)*new[]{82,64,91,58}[i]/100d)&&Close(Attr(bars[i],"height"),18),$"bar {i}");
    }
    // The tracks stand behind their bars, outside the marks, so they are neither focused nor named.
    Check(tracks.All(t=>t.Parent!.Attribute("class")?.Value!="lumen-datum"),"a track inside a mark");
    // Rows evenly spread down a plot that runs from 78 to 240 − 24, nothing being written under it; each bar centred in its row.
    var band=(216-78)/4d;
    Check(Enumerable.Range(0,4).All(i=>Close(Attr(bars[i],"y")+9,78+band*.14+band*.72/2+i*band)),string.Join(",",bars.Select(b=>Attr(b,"y"))));
    var labels=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="start"&&(string?)t.Attribute("font-size")=="11").ToArray();
    Check(labels.Select(t=>t.Value).SequenceEqual(["82","64","91","58"])&&labels.All(t=>Close(Attr(t,"x"),right+6))&&labels.Zip(bars).All(p=>Close(Attr(p.First,"y"),Attr(p.Second,"y")+9+4)),string.Join(",",labels.Select(t=>$"{t.Value}@{Attr(t,"x")}")));
    // Category names stand 12 left of the plot, in the muted colour.
    Check(doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="end").Select(t=>(t.Value,Attr(t,"x"))).SequenceEqual([("Execution",left-12),("Improvement",left-12),("Effort",left-12),("Consistency",left-12)]),"the names");
    // A bar radius rounds the far end of the bar and of its track alike.
    var round=Svg(Scores39(ChartStyle.Light with{BarRadius=9999}));
    Check(Tracks39(round).All(t=>t.Name==ns+"path"&&t.Attribute("d")!.Value.Contains(" A9,9 "))&&Datums(round,0).All(m=>m.Element(ns+"path")!.Attribute("d")!.Value.Contains(" A9,9 ")),"rounded");
    // A value past the maximum is drawn at the track's end and its name says so; its label keeps its value.
    var over=Svg(Scores39() with{Series=[new("Score",[new(0,108,"Bonus"),new(1,40,"Base")]){ValueLabels=true}]});
    Check(Names37(over)[0]=="Score: Bonus, 108, above the scale, drawn at 100"&&Close(Attr(Datums(over,0)[0].Element(ns+"rect")!,"width"),Attr(Tracks39(over)[0],"width"))&&over.Descendants(ns+"text").Any(t=>t.Value=="108"),Names37(over)[0]);
    // A title on the value axis and its ticks bring the bottom margin back; a title up the left widens the left margin by 18.
    Check(Close(PaneSpan(PaneClips(Svg(Scores39() with{YTickLabels=TickLabels.All}))[0]).Bottom,164)&&Close(PaneSpan(PaneClips(Svg(Scores39() with{YLabel="Score"}))[0]).Bottom,164),"the bottom margin");
    Check(Close(Attr(Tracks39(Svg(Scores39() with{XLabel="Category"}))[0],"x"),left+18),"the left margin with a title");
    // A name too long for 45 % of the width is cut, its whole in its mark's name.
    var longName=Svg(Scores39() with{Series=[new("Score",[new(0,50,"An extraordinarily long category"),new(1,40,"B")])]});
    var cut=longName.Descendants(ns+"text").First(t=>(string?)t.Attribute("text-anchor")=="end").Value;
    Check(Close(Attr(Tracks39(longName)[0],"x"),153)&&cut.EndsWith('…')&&Broad38(cut)<=153-24&&Names37(longName)[0].StartsWith("Score: An extraordinarily long category"),cut);
    // Without value labels the right margin is 30; the classic finish draws the same geometry.
    Check(Close(Attr(Tracks39(Svg(Scores39() with{Series=[Scores39().Series[0] with{ValueLabels=false}]}))[0],"width"),340-30-left),"no labels");
    Check(Close(Attr(Tracks39(Svg(Classic(Scores39())))[0],"width"),right-left),"classic");
});
Test("BarTrack on columns: each track runs from zero to the top of the plot behind its column, the value label above the track's top; it is refused off bar and column charts, without YMax, off zero and beside a secondary series",()=>{
    var spec=new ChartSpec{Title="Columns",Kind=ChartKind.Column,Width=600,Height=300,YMax=100,BarTrack=true,Series=[new("Score",[new(0,82,"A"),new(1,64,"B")]){ValueLabels=true}]};
    var doc=Svg(spec);var tracks=Tracks39(doc);var bars=Datums(doc,0).Select(m=>m.Element(ns+"rect")!).ToArray();
    var (top,bottom)=PaneSpan(PaneClips(doc)[0]);
    Check(tracks.Length==2&&tracks.Zip(bars).All(p=>Close(Attr(p.First,"x"),Attr(p.Second,"x"))&&Close(Attr(p.First,"width"),Attr(p.Second,"width"))&&Close(Attr(p.First,"y"),top)&&Close(Attr(p.First,"height"),bottom-top)),"the tracks");
    Check(doc.Descendants(ns+"text").Where(t=>t.Value is "82" or "64").All(t=>Close(Attr(t,"y"),top-5)),"labels above the tracks");
    Check(Refused(spec with{Kind=ChartKind.StackedColumn}).StartsWith("BarTrack draws a track")&&Refused(spec with{Kind=ChartKind.Line}).StartsWith("BarTrack draws a track")&&Refused(Sample(ChartKind.Gauge) with{BarTrack=true}).StartsWith("BarTrack draws"),"other kinds");
    Check(Refused(spec with{YMax=null}).StartsWith("A bar's track runs to the value axis's maximum, so BarTrack needs YMax"),"no YMax");
    Check(Refused(spec with{YMin=10}).Length>0&&Refused(spec with{YMin=-10}).StartsWith("A bar's track runs from zero")&&Refused(spec with{Series=[new("S",[new(0,-1,"A"),new(1,2,"B")])]}).StartsWith("A bar's track runs from zero"),"off zero");
    Check(Svg(spec with{YMin=0}) is not null,"YMin of zero");
    Check(Refused(spec with{Series=[spec.Series[0],new("R",[new(0,1)]){Secondary=true}]}).StartsWith("A track runs the left-hand axis"),"secondary");
    // A line over tracked columns keeps its own mark; without BarTrack the same columns draw as before.
    Check(Tracks39(Svg(spec with{Series=[..spec.Series,new("L",[new(0,50),new(1,60)]){Kind=ChartKind.Line}]})).Length==2&&Tracks39(Svg(spec with{BarTrack=false})).Length==0,"a line, or none");
});
Test("Fill against track: the recipe's and the gallery's bar colours clear 3:1 against their tracks, and the strip's against their card",()=>{
    foreach(var fill in new[]{"#3FD17A","#D7DDE5","#F5B642","#FF5A54"})
        Check(Lumen.Charts.Contrast.Ratio(fill,"#2D2D2F")>=3,$"{fill} on the Race Face track");
    foreach(var fill in new[]{"#3FD17A","#D7DDE5","#F5B642","#E30613"})
        Check(Lumen.Charts.Contrast.Ratio(fill,"#161618")>=3,$"{fill} on the card");
});
Test("DrawTitles off draws neither title nor description, keeps them as the drawing's title, desc and name, and moves every kind's body up 50 units",()=>{
    foreach(var kind in Enum.GetValues<ChartKind>())
    {
        var spec=Sample(kind) with{Description="Said, not drawn"};
        var drawn=Svg(spec);var bare=Svg(spec with{DrawTitles=false});
        Check(drawn.Descendants(ns+"text").Any(t=>t.Value=="Example")&&!bare.Descendants(ns+"text").Any(t=>t.Value is "Example" or "Said, not drawn"),$"{kind}: drawn");
        Check(bare.Root!.Element(ns+"title")!.Value=="Example"&&bare.Root!.Element(ns+"desc")!.Value=="Said, not drawn"&&bare.Root!.Attribute("aria-label")!.Value=="Example. Said, not drawn",$"{kind}: named");
        Check(Datums(bare,0).Length==Datums(drawn,0).Length,$"{kind}: marks");
    }
    // A chart on axes: the plot's clip moves from 78 − 6 to 28 − 6, and ChartSvg.Plot and the readout follow it.
    var line=Spec() with{Description="D"};
    Check(Close(PaneSpan(PaneClips(Svg(line))[0]).Top,78)&&Close(PaneSpan(PaneClips(Svg(line with{DrawTitles=false}))[0]).Top,28)&&Close(ChartSvg.Plot(line with{DrawTitles=false})!.Top,28)&&Close(ChartSvg.Readout(line with{DrawTitles=false,SharedReadout=true}).Top,28),"the plot");
    // The bottom stays where it was, so the plot grows; a two-line description no longer moves it.
    Check(Close(PaneSpan(PaneClips(Svg(line with{DrawTitles=false}))[0]).Bottom,PaneSpan(PaneClips(Svg(line))[0]).Bottom),"the bottom");
    Check(ChartSvg.Render(line with{DrawTitles=false,Description=new string('x',300)}).Replace(new string('x',300),"D")==ChartSvg.Render(line with{DrawTitles=false}),"a long description moved it");
    // A timeline's lanes, a gauge's arc, a donut and a strip move up with it.
    var lanes=Svg(Sample(ChartKind.Timeline) with{DrawTitles=false});
    Check(Close(PaneSpan(PaneClips(lanes)[0]).Top,28),"timeline");
    Check(Height39(Svg(Zones39()))-Height39(Svg(Zones39() with{DrawTitles=false}))==50,"strip");
    // includeTitles still writes or leaves out each mark's tooltip only, whatever DrawTitles says.
    Check(!ChartSvg.Render(line with{DrawTitles=false},includeTitles:false).Contains("<title>Series:")&&ChartSvg.Render(line with{DrawTitles=false},includeTitles:false).Contains("<title>Example</title>"),"includeTitles");
    // Network graphs have no such setting and draw their title.
    Check(GraphEngine.Render(new GraphSpec{Title="Net",Nodes=[new("a","A")]}).Contains(">Net</text>"),"a graph");
});
Test("Bar tracks, drawn titles and strips round-trip through the HTTP API's JSON, defaults stay out of the gradient hash, and every kind draws byte for byte as before with them written out",()=>{
    var spec=Scores39() with{DrawTitles=false};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,finishJson);
    Check(json.Contains("\"barTrack\":true")&&json.Contains("\"drawTitles\":false"),json[^200..]);
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(back.BarTrack&&!back.DrawTitles&&ChartSvg.Render(back)==ChartSvg.Render(spec),"the spec changed in transit");
    var strip=Zones39();
    var stripBack=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(System.Text.Json.JsonSerializer.Serialize(strip,finishJson),finishJson)!;
    Check(System.Text.Json.JsonSerializer.Serialize(strip,finishJson).Contains("\"kind\":\"Strip\"")&&ChartSvg.Render(stripBack)==ChartSvg.Render(strip),"a strip");
    var written="{\"title\":\"Zones\",\"kind\":\"Strip\",\"drawTitles\":false,\"width\":340,\"series\":[{\"name\":\"Z\",\"points\":[{\"x\":0,\"y\":3,\"label\":\"Easy\"},{\"x\":1,\"y\":1,\"label\":\"Hard\",\"color\":\"#E30613\"}]}]}";
    var drawn=Svg(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,finishJson)!);
    Check(Keys39(drawn).SequenceEqual(["Easy 75%","Hard 25%"])&&Height39(drawn)==92-20,"a strip written by hand");
    var old=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Bar\",\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]}]}",finishJson)!;
    Check(!old.BarTrack&&old.DrawTitles,"the defaults");
    string GradientId(ChartSpec s)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(s),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}],Annotations=[new(AnnotationAxis.Y,2){Label="T"}]};
    Check(GradientId(faded)=="lumen-4bce89394b87"&&GradientId(faded with{BarTrack=false,DrawTitles=true})=="lumen-4bce89394b87"&&GradientId(faded with{DrawTitles=false})!="lumen-4bce89394b87",GradientId(faded));
    var columns=Spec(ChartKind.Column) with{YMax=10,Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}]};
    Check(GradientId(columns)!=GradientId(columns with{BarTrack=true}),"a track kept a gradient's name");
    foreach(var kind in Enum.GetValues<ChartKind>())
        Check(ChartSvg.Render(Sample(kind))==ChartSvg.Render(Sample(kind) with{BarTrack=false,DrawTitles=true}),$"{kind}");
});
Test("Sports page: the latest session's time in zone as a strip of its five zones, adding up to the run's time, and three illustrative scores on tracks, the scores' title kept as its name but not drawn",()=>{
    var ids=sports.Where(card=>card.Section=="session").Select(card=>card.Id).ToArray();
    Check(ids.SequenceEqual(["stream","time-in-zone","zone-strip","scores","splits","laps","lap-heart","elevation"]),string.Join(",",ids));
    var strip=Sports("zone-strip");var bars=Sports("time-in-zone");
    Check(strip.Kind==ChartKind.Strip&&strip.Series[0].Points.Select(p=>p.Y).SequenceEqual(bars.Series[0].Points.Select(p=>p.Y))&&strip.Series[0].Points.Select(p=>p.Color).SequenceEqual(bars.Series[0].Points.Select(p=>p.Color))
        &&strip.Series[0].Points.Select(p=>p.Label).SequenceEqual(SportsData.HeartZones.Zones.Select(z=>z.Name)),"the strip is not the time-in-zone bars' data");
    var doc=Svg(strip with{Width=340});
    Check(Keys39(doc).Length==5&&Keys39(doc).Sum(k=>int.Parse(k[(k.LastIndexOf(' ')+1)..^1],CultureInfo.InvariantCulture))==100&&Names37(doc).All(n=>System.Text.RegularExpressions.Regex.IsMatch(n,@"^.+: \d+%, \d+:\d\d")),string.Join("|",Keys39(doc)));
    var scores=Sports("scores");var card=sports.Single(c=>c.Id=="scores");
    Check(scores is {Kind:ChartKind.Bar,BarTrack:true,YMin:0,YMax:100,DrawTitles:false,YTickLabels:TickLabels.None}&&!card.ShowLegend&&scores.Series[0].Points.Count==3&&scores.Series[0].Points.All(p=>p.Y is >=0 and <=100),"the scores");
    Check(scores.Title==$"Session scores: {string.Join(", ",SportsData.SessionScores(latest).Select(s=>$"{s.Name.ToLowerInvariant()} {s.Score.ToString(CultureInfo.InvariantCulture)}"))}",scores.Title);
    var sdoc=Svg(scores with{Width=340});
    Check(Tracks39(sdoc).Length==3&&!sdoc.Descendants(ns+"text").Any(t=>t.Value==scores.Title)&&sdoc.Root!.Element(ns+"title")!.Value==scores.Title,"the scores' drawing");
    // Every fill clears 3:1 against its track: the brand's first series colour in light, Midnight and Harbour, and in the dark preset, whose
    // blue stands 2.73:1 on its grid colour, the palette's sixth colour, which the page gives the bars there.
    foreach(var (fill,style) in new[]{(ChartStyle.Light.Series[0],ChartStyle.Light),(ChartStyle.Dark.Series[5],ChartStyle.Dark),(ChartStyle.Midnight.Series[0],ChartStyle.Midnight),(DemoData.Harbour.Series[0],DemoData.Harbour)})
        Check(Lumen.Charts.Contrast.Ratio(fill,style.Grid)>=3&&Lumen.Charts.Contrast.Ratio(fill,style.Background)>=3,$"{fill} on {style.Grid}");
    Check(Lumen.Charts.Contrast.Ratio(ChartStyle.Dark.Series[0],ChartStyle.Dark.Grid)<3,"the dark preset's blue now clears 3:1 on its grid: the page's override is no longer needed");
    // The strip's zones clear 3:1 against every brand's background.
    foreach(var (zones,background) in new[]{(ChartStyle.Light.Zones,"#FFFFFF"),(ChartStyle.Light.Zones,ChartStyle.Dark.Background),(ChartStyle.Midnight.Zones,ChartStyle.Midnight.Background),(DemoData.Harbour.Zones,DemoData.Harbour.Background)})
        Check(zones.Take(5).All(z=>Lumen.Charts.Contrast.Ratio(z,background)>=3),$"a zone on {background}");
});
// 0.40.0: lap columns and best efforts. Columns and bars filled by a gradient along their value axis, a second line under a category's
// name, a strip part that says its share once, and end labels that give up their note before their label. Every example is invented.
XElement[] Gradients40(XDocument doc)=>doc.Descendants(ns+"linearGradient").ToArray();
XElement Bar40(XElement mark)=>mark.Elements().First(e=>e.Name==ns+"rect"||e.Name==ns+"path");
ChartSpec Laps40(params (string Label,double Bpm,string? Sub)[] laps)=>new(){Title="Heart rate per lap",Description="Average heart rate in each lap",Kind=ChartKind.Column,Width=600,Height=300,
    Series=[new("Heart rate",laps.Select((l,i)=>new ChartPoint(i,l.Bpm,l.Label){SubLabel=l.Sub}).ToArray())]};
// The category names and sub-labels written along the bottom: every middle-anchored muted text under the plot.
XElement[] Under40(XDocument doc)=>doc.Root!.Elements(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="middle"&&(string?)t.Attribute("class")=="lumen-muted"&&Attr(t,"y")>PaneSpan(PaneClips(doc)[0]).Bottom&&t.Value.Length>0).ToArray();
double Wide40(string text)=>Broad38(text)*11/12;
Test("A gradient fills columns along the value axis: one userSpaceOnUse gradient, each stop at its value's height, every column painting with it so a taller column reaches further, a point's colour flat, labels in the text colour, the key its stops",()=>{
    var spec=Laps40(("L1",50,null),("L2",100,null),("L3",150,null)) with{Series=[new("Heart rate",[new(0,50,"L1"),new(1,100,"L2"),new(2,150,"L3")]){ValueLabels=true,Gradient=[new(50,"#A88200"),new(150,"#DD4B45")]}]};
    var doc=Svg(spec);var defs=Gradients40(doc);var bars=Datums(doc,0).Select(Bar40).ToArray();
    Check(defs.Length==1&&(string?)defs[0].Attribute("gradientUnits")=="userSpaceOnUse"&&defs[0].Attribute("x1")!.Value=="0"&&defs[0].Attribute("x2")!.Value=="0",defs.Length>0?defs[0].ToString():"no gradient");
    var id=defs[0].Attribute("id")!.Value;
    Check(bars.All(b=>b.Attribute("fill")!.Value==$"url(#{id})"),"a column is not filled by the gradient");
    // The first stop stands at the top of the column of 50, the last at the top of the column of 150: the stops are laid along the axis.
    Check(Close(Attr(defs[0],"y1"),Attr(bars[0],"y"))&&Close(Attr(defs[0],"y2"),Attr(bars[2],"y")),$"{Attr(defs[0],"y1")} {Attr(defs[0],"y2")} against {Attr(bars[0],"y")} {Attr(bars[2],"y")}");
    Check(defs[0].Elements(ns+"stop").Select(s=>s.Attribute("stop-color")!.Value).SequenceEqual(["#A88200","#DD4B45"]),"the stops");
    // Every column shares the bottom and the gradient, so the taller reaches further along it.
    Check(bars.All(b=>Close(Attr(b,"y")+Attr(b,"height"),Attr(bars[0],"y")+Attr(bars[0],"height")))&&Attr(bars[2],"y")<Attr(bars[1],"y")&&Attr(bars[1],"y")<Attr(bars[0],"y"),"the heights");
    // Value labels stay in the text colour: they carry no fill of their own.
    var labels=doc.Root!.Elements(ns+"text").Where(t=>(string?)t.Attribute("font-size")=="11"&&t.Value is "50" or "100" or "150").ToArray();
    Check(labels.Length==3&&labels.All(t=>t.Attribute("fill") is null),"the value labels");
    // A point's own colour fills its column flat.
    var own=Svg(spec with{Series=[spec.Series[0] with{Points=[spec.Series[0].Points[0],spec.Series[0].Points[1] with{Color="#123456"},spec.Series[0].Points[2]]}]});
    Check(Datums(own,0).Select(Bar40).Select(b=>b.Attribute("fill")!.Value).ElementAt(1)=="#123456"&&Datums(own,0).Select(Bar40).First().Attribute("fill")!.Value.StartsWith("url(#"),"a point's colour");
    // The legend key shows the stops' colours, in the chart's legend and the component's.
    Check(ChartSvg.LegendKey(spec,0).Contains("#A88200")&&ChartSvg.LegendKey(spec,0).Contains("#DD4B45")&&ChartSvg.Render(spec).Contains("fill='#A88200'"),ChartSvg.LegendKey(spec,0));
    // The classic finish draws the same gradient on the same columns.
    var classic=Svg(Classic(spec));
    Check(Gradients40(classic).Length==1&&Close(Attr(Gradients40(classic)[0],"y1"),Attr(Datums(classic,0).Select(Bar40).First(),"y"))&&Datums(classic,0).Select(Bar40).All(b=>b.Attribute("fill")!.Value.StartsWith("url(#")),"classic");
    // A bar radius keeps its path, filled the same way; a column series on a line chart, and one on the right-hand axis, take it too.
    Check(Datums(Svg(spec with{Style=ChartStyle.Midnight}),0).Select(Bar40).All(b=>b.Name==ns+"path"&&b.Attribute("fill")!.Value.StartsWith("url(#")),"rounded columns");
    var mixed=Svg(new ChartSpec{Title="Mixed",Kind=ChartKind.Line,Series=[new("L",[new(0,5),new(10,9)]),new("C",[new(0,3),new(5,6),new(10,8)]){Kind=ChartKind.Column,Gradient=[new(3,"#A88200"),new(8,"#DD4B45")]}]});
    Check(Datums(mixed,1).Select(Bar40).All(b=>b.Attribute("fill")!.Value.StartsWith("url(#"))&&Gradients40(mixed).Length==1,"a column series on a line chart");
    var right=Svg(new ChartSpec{Title="Right",Kind=ChartKind.Column,Series=[new("A",[new(0,5,"A"),new(1,9,"B")]),new("B",[new(0,300,"A"),new(1,600,"B")]){Secondary=true,Gradient=[new(300,"#A88200"),new(600,"#DD4B45")]}]});
    Check(Close(Attr(Gradients40(right)[0],"y1"),Attr(Datums(right,1).Select(Bar40).First(),"y"))&&Close(Attr(Gradients40(right)[0],"y2"),Attr(Datums(right,1).Select(Bar40).Last(),"y")),"a column on the right-hand axis");
    // Stops beyond the data carry on: a gradient wider than the axis still paints every column.
    Check(Datums(Svg(spec with{Series=[spec.Series[0] with{Gradient=[new(-1000,"#A88200"),new(1000,"#DD4B45")]}]}),0).Select(Bar40).All(b=>b.Attribute("fill")!.Value.StartsWith("url(#")),"wide stops");
});
Test("A gradient fills a bar chart's bars across the plot, along X, on tracks too",()=>{
    var spec=new ChartSpec{Title="Bars",Kind=ChartKind.Bar,Width=600,Height=300,Series=[new("Power",[new(0,100,"A"),new(1,200,"B"),new(2,300,"C")]){Gradient=[new(100,"#A88200"),new(300,"#DD4B45")]}]};
    var doc=Svg(spec);var defs=Gradients40(doc);var bars=Datums(doc,0).Select(Bar40).ToArray();
    Check(defs.Length==1&&defs[0].Attribute("y1")!.Value=="0"&&defs[0].Attribute("y2")!.Value=="0"&&(string?)defs[0].Attribute("gradientUnits")=="userSpaceOnUse",defs[0].ToString());
    Check(Close(Attr(defs[0],"x1"),Attr(bars[0],"x")+Attr(bars[0],"width"))&&Close(Attr(defs[0],"x2"),Attr(bars[2],"x")+Attr(bars[2],"width"))&&Attr(defs[0],"x2")>Attr(defs[0],"x1"),$"{defs[0]}");
    Check(bars.All(b=>b.Attribute("fill")!.Value==$"url(#{defs[0].Attribute("id")!.Value})"),"a bar is not filled by the gradient");
    var tracked=Svg(spec with{YMax=400,BarTrack=true});
    Check(Datums(tracked,0).Select(Bar40).All(b=>b.Attribute("fill")!.Value.StartsWith("url(#"))&&Tracks39(tracked).All(t=>t.Attribute("fill")!.Value==ChartStyle.Light.Grid),"bars on tracks");
});
Test("A gradient is refused on stacked columns, beside a faded fill or zones, and on the marks it cannot colour, each with its reason; columns' log and reversed axes stay refused",()=>{
    var stops=new ColorStop[]{new(1,"#A88200"),new(9,"#DD4B45")};
    var columns=new ChartSpec{Title="C",Kind=ChartKind.Column,Series=[new("S",[new(0,3,"A"),new(1,6,"B")]){Gradient=stops}]};
    Check(Svg(columns) is not null,"columns");
    Check(Refused(columns with{Kind=ChartKind.StackedColumn}).StartsWith("A stacked column's colours tell its stacked series apart"),Refused(columns with{Kind=ChartKind.StackedColumn}));
    Check(Refused(columns with{Series=[columns.Series[0] with{Fill=AreaFill.Fade}]}).StartsWith("A faded column fades its own colour"),"a fade");
    Check(Refused(columns with{Series=[columns.Series[0] with{Zones=new([new("Low",4),new("High",double.PositiveInfinity)])}]}).StartsWith("Zones colour a series in steps"),"zones");
    Check(Refused(columns with{Kind=ChartKind.Scatter}).StartsWith("A gradient colours a series by its value, so it applies to series drawn as lines, areas, columns"),"scatter");
    Check(Refused(columns with{YReversed=true}).StartsWith("A reversed Y axis applies")&&Refused(columns with{YAxis=AxisKind.Log}).StartsWith("Log Y axes require"),"reversed or log");
    Check(Refused(columns with{Series=[columns.Series[0] with{Gradient=[new(1,"#A88200")]}]}).StartsWith("A gradient needs between 2 and 32")&&Refused(columns with{Series=[columns.Series[0] with{Gradient=[new(5,"#A88200"),new(1,"#DD4B45")]}]}).StartsWith("Colour stop values must rise"),"the stops");
});
Test("A sub-label is a second line 14 under each column's name at 11 px in the muted colour; the floor grows 14 only when one is set, the axis title moves down with it, and a chart without any draws as before",()=>{
    var plain=Laps40(("L1",152,null),("L2",161,null),("L3",168,null),("L4",174,null)) with{XLabel="Lap"};
    var subbed=Laps40(("L1",152,"152 bpm"),("L2",161,"161 bpm"),("L3",168,"168 bpm"),("L4",174,"174 bpm")) with{XLabel="Lap"};
    XDocument a=Svg(plain),b=Svg(subbed);
    var (top,bottom)=PaneSpan(PaneClips(b)[0]);
    Check(Close(PaneSpan(PaneClips(a)[0]).Bottom-bottom,14)&&Close(PaneSpan(PaneClips(a)[0]).Top,top),$"{PaneSpan(PaneClips(a)[0]).Bottom} {bottom}");
    var under=Under40(b);
    var names=under.Where(t=>Close(Attr(t,"y"),bottom+21)).ToArray();var subs=under.Where(t=>Close(Attr(t,"y"),bottom+35)).ToArray();
    Check(names.Select(t=>t.Value).SequenceEqual(["L1","L2","L3","L4"])&&subs.Select(t=>t.Value).SequenceEqual(["152 bpm","161 bpm","168 bpm","174 bpm"]),string.Join("|",under.Select(t=>$"{t.Value}@{Attr(t,"y")}")));
    Check(subs.All(t=>(string?)t.Attribute("font-size")=="11")&&names.All(t=>t.Attribute("font-size") is null)&&names.Zip(subs).All(p=>Close(Attr(p.First,"x"),Attr(p.Second,"x"))),"sizes and places");
    Check(under.Any(t=>t.Value=="Lap"&&Close(Attr(t,"y"),bottom+58))&&Under40(a).Any(t=>t.Value=="Lap"&&Close(Attr(t,"y"),PaneSpan(PaneClips(a)[0]).Bottom+44)),"the axis title");
    // A chart whose points carry no sub-label, set or written out as null, draws byte for byte as before.
    Check(ChartSvg.Render(plain)==ChartSvg.Render(plain with{Series=[plain.Series[0] with{Points=plain.Series[0].Points.Select(p=>p with{SubLabel=null}).ToArray()}]}),"null sub-labels");
    // The muted colour clears 4.5:1 in every preset.
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight,DemoData.Harbour})
        Check(Lumen.Charts.Contrast.Ratio(style.Muted,style.Background)>=4.5,$"{style.Muted} on {style.Background}");
    // A sub-label longer than 12 characters is cut as its name is.
    Check(Under40(Svg(Laps40(("L1",152,"0123456789abcdef"),("L2",160,null)))).Any(t=>t.Value=="0123456789a…"),"a long sub-label");
    // Stacked columns write them the same way, and the classic finish too.
    var stacked=Svg(subbed with{Kind=ChartKind.StackedColumn,Series=[..subbed.Series,new("More",[new(0,10,"L1"),new(1,12,"L2"),new(2,9,"L3"),new(3,11,"L4")])]});
    Check(Under40(stacked).Count(t=>t.Value.EndsWith(" bpm"))==4&&Under40(Svg(Classic(subbed))).Count(t=>t.Value.EndsWith(" bpm"))==4,"stacked or classic");
});
Test("Sub-labels are thinned with their names by the room their words take, in either finish, so names and sub-labels never touch; a chart without them keeps its 65-unit rule",()=>{
    double Width40(XElement t)=>(string?)t.Attribute("font-size")=="11"?Wide40(t.Value):Broad38(t.Value);
    foreach(var (count,width) in new[]{(4,340),(6,340),(12,340),(12,900),(30,600)})
        foreach(var finish in new Func<ChartSpec,ChartSpec>[]{s=>s,Classic})
        {
            var spec=finish(Laps40(Enumerable.Range(0,count).Select(i=>($"L{i+1}",150d+i,(string?)$"{150+i} bpm")).ToArray()) with{Width=width});
            var doc=Svg(spec);var bottom=PaneSpan(PaneClips(doc)[0]).Bottom;var under=Under40(doc);
            var names=under.Where(t=>Close(Attr(t,"y"),bottom+21)).ToArray();var subs=under.Where(t=>Close(Attr(t,"y"),bottom+35)).ToArray();
            Check(names.Length==subs.Length&&names.Length>=1&&names.Zip(subs).All(p=>Close(Attr(p.First,"x"),Attr(p.Second,"x"))),$"{count} at {width}: {names.Length} names, {subs.Length} sub-labels");
            // Each column's words keep 8 units from the next column's, by the wider of its two lines.
            var widths=names.Zip(subs).Select(p=>(X:Attr(p.First,"x"),W:Math.Max(Width40(p.First),Width40(p.Second)))).ToArray();
            Check(widths.Zip(widths.Skip(1)).All(p=>p.Second.X-p.First.X>=(p.First.W+p.Second.W)/2+8-1e-6),$"{count} at {width}: touching");
        }
    // Four laps at 340 keep every name and its sub-label; twelve leave some out.
    Check(Under40(Svg(Laps40(Enumerable.Range(0,4).Select(i=>($"L{i+1}",150d+i,(string?)$"{150+i} bpm")).ToArray()) with{Width=340})).Length==8,"four laps at 340");
    Check(Under40(Svg(Laps40(Enumerable.Range(0,12).Select(i=>($"L{i+1}",150d+i,(string?)$"{150+i} bpm")).ToArray()) with{Width=340})).Length<24,"twelve laps at 340");
    // Without sub-labels four columns at 340 keep the 65-unit rule, one name in two.
    Check(Under40(Svg(Laps40(Enumerable.Range(0,4).Select(i=>($"L{i+1}",150d+i,(string?)null)).ToArray()) with{Width=340})).Length==2,"the old rule");
});
Test("On a bar chart a sub-label stands under the category's name beside the bar, the two lines centred on it; rows thin for two lines, and a track's margin fits the wider",()=>{
    var spec=new ChartSpec{Title="Bars",Kind=ChartKind.Bar,Width=600,Height=300,Series=[new("Power",[new(0,780,"5s"){SubLabel="15.0 W/kg"},new(1,420,"1m"),new(2,290,"5m"){SubLabel="5.6 W/kg"}])]};
    var doc=Svg(spec);var (top,bottom)=PaneSpan(PaneClips(doc)[0]);var band=(bottom-top)/3;
    var side=doc.Root!.Elements(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="end"&&(string?)t.Attribute("class")=="lumen-muted").ToArray();
    double Row(int i)=>top+(i+.5)*band+4;
    (string,double)[] expected=[("5s",Row(0)-7),("15.0 W/kg",Row(0)+7),("1m",Row(1)),("5m",Row(2)-7),("5.6 W/kg",Row(2)+7)];
    Check(side.Length==5&&side.Zip(expected).All(p=>p.First.Value==p.Second.Item1&&Close(Attr(p.First,"y"),p.Second.Item2)),string.Join("|",side.Select(t=>$"{t.Value}@{Attr(t,"y")}")));
    Check(side.Where(t=>t.Value.EndsWith("W/kg")).All(t=>(string?)t.Attribute("font-size")=="11"),"the size");
    // Twelve rows in 300 units leave room for some names alone; with sub-labels each row needs 38.
    var many=spec with{Series=[new("P",Enumerable.Range(0,12).Select(i=>new ChartPoint(i,100+i,$"R{i}"){SubLabel=$"sub {i}"}).ToArray())]};
    var mdoc=Svg(many);var mspan=PaneSpan(PaneClips(mdoc)[0]);
    var written=mdoc.Root!.Elements(ns+"text").Count(t=>(string?)t.Attribute("text-anchor")=="end"&&t.Value.StartsWith("R"));
    var every=(int)Math.Ceiling(12/((mspan.Bottom-mspan.Top)/38));
    Check(written==(12+every-1)/every&&every>1,$"{written} rows written");
    // On tracks the left margin fits the wider of a name and its sub-label.
    var tracked=Scores39() with{Series=[new("Score",[new(0,82,"Ex"){SubLabel="A long sub-label"},new(1,64,"Im")])]};
    Check(Close(Attr(Tracks39(Svg(tracked))[0],"x"),Math.Ceiling(Wide40("A long sub-label"))+24),$"{Attr(Tracks39(Svg(tracked))[0],"x")}");
});
Test("A sub-label is said after its category's name in every mark's name and tooltip, a category's first given to every series in it, and in the component's status line and data table",()=>{
    var spec=Laps40(("L1",152,"152 bpm"),("L2",161,null)) with{Series=[new("Heart rate",[new(0,152,"L1"){SubLabel="152 bpm"},new(1,161,"L2")]),new("Last race",[new(0,150,"L1"),new(1,158,"L2"){SubLabel="161 bpm"}]),new("Average",[new(0,151),new(1,159)]){Kind=ChartKind.Line}]};
    var doc=Svg(spec);
    Check(Names37(doc,0).SequenceEqual(["Heart rate: L1 · 152 bpm, 152","Heart rate: L2 · 161 bpm, 161"])&&Names37(doc,1).SequenceEqual(["Last race: L1 · 152 bpm, 150","Last race: L2 · 161 bpm, 158"]),string.Join("|",Names37(doc,0).Concat(Names37(doc,1))));
    Check(Names37(doc,2)[0]=="Average: 0 · 152 bpm, 151"&&Datums(doc,0)[0].Element(ns+"title")!.Value=="Heart rate: L1 · 152 bpm, 152",string.Join("|",Names37(doc,2)));
    // A range series on a column chart says it too.
    var range=Svg(spec with{Series=[spec.Series[0],new("Spread",[ChartPoint.Interval(0,152,140,170,"L1"),ChartPoint.Interval(1,null,145,175,"L2")]){Kind=ChartKind.Range}]});
    Check(Names37(range,1)[0].StartsWith("L1 · 152 bpm: 140 to 170"),Names37(range,1)[0]);
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var status="";
    var html=Operate(spec,async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(1,1);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(status=="Last race: L2 · 161 bpm = 158",status);
    Check(html.Contains("<tr><td>Heart rate</td><td>L1 &#xB7; 152 bpm</td><td>152</td></tr>")&&html.Contains("<tr><td>Last race</td><td>L2 &#xB7; 161 bpm</td><td>158</td></tr>"),"the data table");
    // Without sub-labels the status line and the table read as before.
    var plain=Operate(Spec(ChartKind.Column),async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(0,1);});
    Check(plain.Contains("<tr><td>Series</td><td>B</td><td>5</td></tr>")&&plain.Contains("Series: B = 5</span>"),"a chart without sub-labels");
});
Test("A sub-label is refused off column, bar and stacked column charts, on a sparkline, blank, past 16 characters, across lines, and where two series give one category different ones, each with its reason",()=>{
    ChartSpec With(ChartKind kind,string sub)=>new(){Title="S",Kind=kind,Series=[new("S",[new(0,1,"A"){SubLabel=sub},new(1,2,"B")])]};
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Scatter,ChartKind.Area,ChartKind.Donut,ChartKind.Heatmap,ChartKind.Radar})
        Check(Refused(With(kind,"x")).StartsWith("A sub-label is a second line under a category's name, so it applies to column, bar and stacked column charts"),$"{kind}: {Refused(With(kind,"x"))}");
    Check(Refused(Spark(ChartKind.Column) with{Series=[new("S",[new(0,1){SubLabel="x"},new(1,2)])]}).StartsWith("A sparkline draws its data alone, with no words, so its points take no sub-labels"),"a sparkline");
    Check(Refused(With(ChartKind.Column," ")).StartsWith("A sub-label is written under its category's name, so it needs words"),"blank");
    Check(Refused(With(ChartKind.Column,new string('x',17))).StartsWith("A sub-label is one short line")&&Refused(With(ChartKind.Bar,"one\ntwo")).StartsWith("A sub-label is one short line")&&Svg(With(ChartKind.Column,new string('x',16))) is not null,"long or two lines");
    var two=new ChartSpec{Title="T",Kind=ChartKind.Column,Series=[new("A",[new(0,1,"L1"){SubLabel="152"}]),new("B",[new(0,2,"L1"){SubLabel="153"}])]};
    Check(Refused(two).StartsWith("A category's sub-label is written once under its name"),"different sub-labels");
    Check(Svg(two with{Series=[two.Series[0],new("B",[new(0,2,"L1"){SubLabel="152"}])]}) is not null,"the same one twice");
    Check(Svg(With(ChartKind.StackedColumn,"x")) is not null&&Svg(With(ChartKind.Bar,"x")) is not null,"stacked and bars");
});
Test("A strip part whose amount writes as its share says it once; one that differs, or in another format, keeps both",()=>{
    var shares=new ChartSpec{Title="Zones",Kind=ChartKind.Strip,Width=340,YUnit="%",Series=[new("Zones",[new(0,30,"Easy"),new(1,40,"Moderate"),new(2,30,"Hard"){ValueNote=" n"}])]};
    Check(Names37(Svg(shares)).SequenceEqual(["Easy: 30%","Moderate: 40%","Hard: 30% n"])&&ChartSvg.PartLabel(shares,1)=="Moderate: 40%",string.Join("|",Names37(Svg(shares))));
    // Shares rounded by the app that add up to 99 are shared out over their total: the last takes 34 and keeps its amount beside it.
    var rounded=shares with{Series=[new("Zones",[new(0,33,"A"),new(1,33,"B"),new(2,33,"C")])]};
    Check(Names37(Svg(rounded)).SequenceEqual(["A: 33%","B: 33%","C: 34%, 33%"])&&Keys39(Svg(rounded)).SequenceEqual(["A 33%","B 33%","C 34%"]),string.Join("|",Names37(Svg(rounded))));
    // Seconds, and a number without the unit, keep their amounts.
    Check(Names37(Svg(Zones39()))[0]=="Easy: 24%, 12:20"&&ChartSvg.PartLabel(shares with{YUnit=null},0)=="Easy: 30%, 30","other formats");
});
Test("An end label that does not fit with its note gives up the note first, cut and then left out, and is cut itself only where it does not fit alone; its whole stays its name and tooltip",()=>{
    var (noteCut,noteDropped,labelCut)=(0,0,0);
    foreach(var width in new[]{320,340,360})
        foreach(var length in Enumerable.Range(3,22))
        {
            var label=new string('n',length-2)+" X";
            var spec=new ChartSpec{Title="Ends",Kind=ChartKind.Line,Width=width,Series=[new("S",[new(0,1),new(1,2)]){EndLabel=label,EndNote="+12.3s"}]};
            var end=Ends38(Svg(spec)).Single();var words=Words38(end);var text=Text38(end);var room=width-4-Attr(words,"x");
            var whole=label+" +12.3s";
            if(text==whole){Check(words.Attribute("role") is null,"uncut, yet named apart");continue;}
            Check((string?)words.Attribute("role")=="img"&&words.Attribute("aria-label")!.Value==whole&&words.Element(ns+"title")!.Value==whole,$"{label} at {width}: its whole");
            if(Broad38(label)<=room+1e-6)
            {
                // The label fits: it stands whole, its note cut or left out.
                Check(text==label||text.StartsWith(label+" ")&&text.EndsWith("…"),$"{label} at {width}: {text}");
                if(text==label)noteDropped++;else noteCut++;
            }
            else{Check(text.EndsWith("…")&&!words.Elements(ns+"tspan").Any()&&Broad38(text)<=room+1e-6,$"{label} at {width}: {text}");labelCut++;}
        }
    Check(noteCut>0&&noteDropped>0&&labelCut>0,$"{noteCut} notes cut, {noteDropped} left out, {labelCut} labels cut");
});
Test("Gradients on columns and sub-labels round-trip through the HTTP API's JSON, defaults stay out of the gradient hash, and every kind draws byte for byte as before with them written out",()=>{
    var spec=Laps40(("L1",152,"152 bpm"),("L2",174,"174 bpm")) with{Series=[new("Heart rate",[new(0,152,"L1"){SubLabel="152 bpm"},new(1,174,"L2"){SubLabel="174 bpm"}]){Gradient=[new(152,"#f59e0b"),new(174,"#f87171")]}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,finishJson);
    Check(json.Contains("\"subLabel\":\"152 bpm\"")&&json.Contains("\"gradient\":[{\"value\":152,\"color\":\"#f59e0b\"}"),json);
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(back.Series[0].Points[1].SubLabel=="174 bpm"&&ChartSvg.Render(back)==ChartSvg.Render(spec),"the spec changed in transit");
    var written="{\"title\":\"Laps\",\"kind\":\"Column\",\"series\":[{\"name\":\"HR\",\"gradient\":[{\"value\":1,\"color\":\"#A88200\"},{\"value\":2,\"color\":\"#DD4B45\"}],\"points\":[{\"x\":0,\"y\":1,\"label\":\"L1\",\"subLabel\":\"1 bpm\"},{\"x\":1,\"y\":2,\"label\":\"L2\"}]}]}";
    var drawn=Svg(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(written,finishJson)!);
    Check(Names37(drawn)[0]=="HR: L1 · 1 bpm, 1"&&Gradients40(drawn).Length==1,"a spec written by hand");
    Check(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Column\",\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]}]}",finishJson)!.Series[0].Points[0].SubLabel is null,"the default");
    string GradientId(ChartSpec s)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(s),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}],Annotations=[new(AnnotationAxis.Y,2){Label="T"}]};
    Check(GradientId(faded)=="lumen-4bce89394b87"&&GradientId(faded with{Series=[faded.Series[0] with{Points=faded.Series[0].Points.Select(p=>p with{SubLabel=null}).ToArray()}]})=="lumen-4bce89394b87",GradientId(faded));
    var fadedColumns=Spec(ChartKind.Column) with{Series=[new("S",[new(0,1,"A"),new(1,3,"B")]){Fill=AreaFill.Fade}]};
    Check(GradientId(fadedColumns)!=GradientId(fadedColumns with{Series=[fadedColumns.Series[0] with{Points=[fadedColumns.Series[0].Points[0] with{SubLabel="x"},fadedColumns.Series[0].Points[1]]}]}),"a sub-label kept a gradient's name");
    foreach(var kind in Enum.GetValues<ChartKind>())
        Check(ChartSvg.Render(Sample(kind))==ChartSvg.Render(Sample(kind) with{Series=Sample(kind).Series.Select(s=>s with{Points=s.Points.Select(p=>p with{SubLabel=null}).ToArray()}).ToArray()}),$"{kind}");
});
Test("Sports page: heart rate by lap closes the Latest session section as gradient-filled columns of the laps' heart rates with their paces under them, and best efforts follow the power curve, their watts per kilogram under each",()=>{
    var ids=sports.Where(card=>card.Section=="fitness").Select(card=>card.Id).ToArray();
    Check(ids.SequenceEqual(["power-curve","best-efforts","records","getting-faster","hrv"]),string.Join(",",ids));
    var laps=Sports("laps");var heart=Sports("lap-heart");var hearts=SportsData.LapHeartRates(latest);
    Check(heart.Kind==ChartKind.Column&&heart.Series.Count==1&&heart.Series[0].Points.Select(p=>p.Y!.Value).SequenceEqual(hearts)&&hearts.Count==laps.Series[0].Points.Count,"the laps' heart rates");
    Check(heart.Series[0].Points.Select(p=>p.SubLabel).SequenceEqual(laps.Series[0].Points.Select(p=>SportsData.Clock(p.Y!.Value)))&&heart.Series[0].Points.Select(p=>p.Label).SequenceEqual(laps.Series[0].Points.Select(p=>p.Label)),"the paces");
    Check(heart.Series[0].Gradient!.Select(s=>(s.Value,s.Color)).SequenceEqual([(0d,ChartStyle.Light.Zones[3]),(hearts.Max(),ChartStyle.Light.Zones[5])]),"the stops");
    // Every lap's average lies between its stream's lowest and highest heart rate, and the progression's laps rise.
    Check(hearts.All(h=>h>=latest.Track!.HeartRate.Min()&&h<=latest.Track!.HeartRate.Max())&&hearts.Zip(hearts.Skip(1)).All(p=>p.Second>p.First),string.Join(",",hearts));
    var efforts=Sports("best-efforts");var month=SportsData.MonthBest(athlete,9);
    Check(efforts.Kind==ChartKind.Column&&efforts.Series[0].Points.Select(p=>p.Label).SequenceEqual(["5s","1m","5m","20m","60m"])
        &&efforts.Series[0].Points.Select(p=>p.Y!.Value).SequenceEqual(new double[]{5,60,300,1200,3600}.Select(d=>month.Single(m=>m.Seconds==d).Value)),string.Join(",",efforts.Series[0].Points.Select(p=>p.Label)));
    Check(efforts.Series[0].Points.All(p=>p.SubLabel==(p.Y!.Value/SportsData.BodyMass).ToString("0.0",CultureInfo.InvariantCulture))&&efforts.Series[0].ValueLabels,"the watts per kilogram");
    // At a phone's width every lap and every effort keeps its name and its sub-label, and each mark says it.
    foreach(var spec in new[]{heart,efforts})
        foreach(var width in new[]{322,340,1100})
        {
            var doc=Svg(spec with{Width=width});
            Check(Under40(doc).Count(t=>(string?)t.Attribute("font-size")=="11")==spec.Series[0].Points.Count&&Names37(doc).All(n=>n.Contains(" · ")),$"{spec.Title} at {width}");
        }
    // The gradient's stops clear 3:1 on every brand's background, as do their blends, and the sub-labels' muted colour 4.5:1.
    foreach(var (zones,style) in new[]{(ChartStyle.Light.Zones,ChartStyle.Light),(ChartStyle.Light.Zones,ChartStyle.Dark),(ChartStyle.Midnight.Zones,ChartStyle.Midnight),(ChartStyle.Light.Zones,DemoData.Harbour)})
    {
        for(var t=0;t<=20;t++)
            Check(Lumen.Charts.Contrast.Ratio(Blend40(zones[3],zones[5],t/20d),style.Background)>=3,$"{zones[3]}–{zones[5]} at {t}/20 on {style.Background}");
        Check(Lumen.Charts.Contrast.Ratio(style.Muted,style.Background)>=4.5&&Lumen.Charts.Contrast.Ratio(style.SeriesColor(0),style.Background)>=3,$"{style.Background}");
    }
});
Test("Race Face recipes: the lap columns' amber to red and the best efforts' sky blue clear 3:1 on the card at every stop and blend, and the sub-labels' low grey 4.5:1",()=>{
    for(var t=0;t<=20;t++) Check(Lumen.Charts.Contrast.Ratio(Blend40("#f59e0b","#f87171",t/20d),"#161618")>=3,$"blend {t}");
    Check(Lumen.Charts.Contrast.Ratio("#38bdf8","#161618")>=3&&Lumen.Charts.Contrast.Ratio("#80858E","#161618")>=4.5&&Lumen.Charts.Contrast.Ratio("#F5F6F7","#161618")>=4.5,"the card");
});
// 0.41.0: fits a card. A background left unpainted, points the app averaged itself, and bar charts drawn as tall as their rows. Every example
// is invented.
string Root41(string svg)=>XDocument.Parse(svg).Root!.Attribute("style")!.Value;
double Tall41(string svg)=>double.Parse(XDocument.Parse(svg).Root!.Attribute("viewBox")!.Value.Split(' ')[3],CultureInfo.InvariantCulture);
ChartSpec Meters41(int rows,bool tracked=true)=>new(){Title="Scores",Description="Each out of 100",Kind=ChartKind.Bar,Width=340,Height=240,YMin=0,YMax=100,BarTrack=tracked,FitHeight=true,
    YTickLabels=TickLabels.None,DrawTitles=false,Series=[new("Score",Enumerable.Range(0,rows).Select(i=>new ChartPoint(i,40+i*5%60,$"Score {i+1}")).ToArray()){ValueLabels=true}]};
Test("PaintBackground = false writes no background on the root, only --lumen-ground, and nothing else moves, in every kind and both finishes; a graph takes it too",()=>{
    foreach(var kind in Enum.GetValues<ChartKind>())
        foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight,ChartStyle.Light with{Finish=ChartFinish.Classic}})
        {
            var painted=Sample(kind) with{Style=style};
            var bare=ChartSvg.Render(painted with{PaintBackground=false});
            Check(!Root41(bare).Contains("background:")&&Root41(bare).Contains($"--lumen-ground:{style.Background}"),$"{kind}: {Root41(bare)}");
            // The only change is the root's style: gradients keep their names, and no shape fills the drawing.
            Check(bare==ChartSvg.Render(painted).Replace($"background:{style.Background}",$"--lumen-ground:{style.Background}"),$"{kind} moved");
            Check(!XDocument.Parse(bare).Descendants(ns+"rect").Any(r=>(string?)r.Attribute("width")==$"{painted.Width}"),$"{kind}: a full-size rect");
        }
    // Painted, the root writes its background as before.
    Check(Root41(ChartSvg.Render(Spec())).Contains("background:#FFFFFF")&&!Root41(ChartSvg.Render(Spec())).Contains("--lumen-ground"),"painted");
    var graph=DemoData.Graph(GraphLayout.Layered,ChartTheme.Light);
    var unpainted=GraphEngine.Render(graph with{PaintBackground=false});
    Check(!Root41(unpainted).Contains("background:")&&unpainted==GraphEngine.Render(graph).Replace("background:#FFFFFF","--lumen-ground:#FFFFFF"),"a graph");
});
Test("Left unpainted, a chart still draws its halos and separators in Style.Background and checks contrast against it",()=>{
    var card=ChartStyle.Light with{Background="#F3F6FB"};
    var line=new ChartSpec{Title="Rest",Kind=ChartKind.Line,Style=card,PaintBackground=false,Series=[new("Rest",[new(0,52),new(1,49),new(2,51)],"#5675E7"){ValueLabels=true,EndLabel="Rest"}]};
    var doc=Svg(line);
    // A value label's halo and an end label's are the card's colour.
    var halo=doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("class")=="lumen-value").Select(g=>g.Elements(ns+"text").First()).ToArray();
    Check(halo.Length==3&&halo.All(t=>t.Attribute("stroke")!.Value=="#F3F6FB"&&t.Attribute("fill")!.Value=="#F3F6FB"),"value-label halos");
    // #5675E7 stands 3.81:1 on the card, short of 4.5:1, so its labels are written in the text colour, as on a painted card.
    Check(Lumen.Charts.Contrast.Ratio("#5675E7","#F3F6FB")<4.5&&doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("class")=="lumen-value").All(g=>g.Elements(ns+"text").Last().Attribute("fill")!.Value==card.Text),"the label's colour");
    Check(ChartSvg.Render(line)==ChartSvg.Render(line with{PaintBackground=true}).Replace("background:#F3F6FB","--lumen-ground:#F3F6FB"),"the rest moved");
    // A strip's gaps are left empty, so the card shows through them; a reference in front stands on a halo of the card's colour.
    var strip=ChartSvg.Render(new ChartSpec{Title="Z",Kind=ChartKind.Strip,Width=340,Style=card,PaintBackground=false,Series=[new("Z",[new(0,30,"A"),new(1,40,"B"),new(2,30,"C")])]});
    Check(!strip.Replace("--lumen-ground:#F3F6FB","").Contains("#F3F6FB"),"the strip");
    var front=ChartSvg.Render(Spec(ChartKind.Column) with{Style=card,PaintBackground=false,Annotations=[new(AnnotationAxis.Y,4){Label="Target",InFront=true}]});
    Check(front.Contains("stroke='#F3F6FB'"),"a reference in front");
    // The muted and text colours clear 4.5:1 on the gallery's tinted cards, and its series, zones and candles 3:1; the graph edge colour,
    // which only a graph draws, is not used.
    foreach(var theme in new[]{ChartTheme.Light,ChartTheme.Dark})
    {
        var style=DemoData.Unpainted(theme).Style!;
        Check(style.Background==DemoData.CardTint(theme)&&style.ContrastIssues().All(i=>i.Element=="Graph edges"),$"{theme}: {string.Join(", ",style.ContrastIssues().Select(i=>$"{i.Element} {i.Ratio}"))}");
    }
});
Test("AverageOf ends each mark's name and tooltip with its words, on every mark that names one value; a missing value says nothing, and Lumen's own averages keep theirs",()=>{
    var line=new ChartSpec{Title="Power",Kind=ChartKind.Line,XFormat=ValueFormat.Duration,Series=[new("Power",[new(6,212),new(18,null),new(30,240)]){AverageOf="12 s",Markers=MarkerStyle.Filled}]};
    var doc=Svg(line);
    Check(Names37(doc).SequenceEqual(["Power: 0:06, 212, average of 12 s","Power: 0:30, 240, average of 12 s"])&&Datums(doc,0)[0].Element(ns+"title")!.Value=="Power: 0:06, 212, average of 12 s",string.Join("|",Names37(doc)));
    // The note follows the value's words, before an end label, and a value note, zone or change words come first.
    var ended=Svg(line with{Series=[line.Series[0] with{EndLabel="P",ChangeColors=ChangeColors.HigherIsBetter,Points=[new(6,212){ValueNote=" W"},new(30,240)]}]});
    Check(Names37(ended)[1]=="Power: 0:30, 240, better than the previous, average of 12 s, labelled P",Names37(ended)[1]);
    // Columns, bars on tracks, scatter, bubbles, bands, blocks, heatmaps, radar and stacked columns say it too.
    string First(ChartSpec s,int series=0)=>Names37(Svg(s),series)[0];
    Check(First(Spec(ChartKind.Column) with{Series=[Spec().Series[0] with{AverageOf="a week"}]})=="Series: A, 2, average of a week","columns");
    Check(First(Scores39() with{Series=[new("Score",[new(0,108,"Bonus")]){AverageOf="3 races"}]})=="Score: Bonus, 108, average of 3 races, above the scale, drawn at 100","bars on tracks");
    foreach(var kind in new[]{ChartKind.Scatter,ChartKind.Bubble,ChartKind.Area,ChartKind.Heatmap,ChartKind.Radar,ChartKind.StackedColumn,ChartKind.Band,ChartKind.Blocks,ChartKind.Bar})
        Check(First(Sample(kind) with{Series=Sample(kind).Series.Select(s=>s with{AverageOf="12 s"}).ToArray()}).EndsWith(", average of 12 s"),$"{kind}: {First(Sample(kind) with{Series=Sample(kind).Series.Select(s=>s with{AverageOf="12 s"}).ToArray()})}");
    // A sparkline's marks say it.
    Check(Names37(Svg(Spark() with{Series=[Spark().Series[0] with{AverageOf="a day"}]}))[0].EndsWith(", average of a day"),"a sparkline");
    // Where Lumen averages a slice itself, its own words stand.
    var averaged=Long37(64) with{Series=[Long37(64).Series[0] with{AverageOf="12 s"}]};
    Check(Names37(Svg(averaged)).All(n=>n.EndsWith(", average of 4 points")&&!n.Contains("12 s")),Names37(Svg(averaged))[0]);
    // It changes no drawing, only words, and no gradient's name; CSV keeps the values.
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}]};
    Check(ChartSvg.Render(faded with{Series=[faded.Series[0] with{AverageOf="12 s"}]})==ChartSvg.Render(faded).Replace("S: 0, 1","S: 0, 1, average of 12 s").Replace("S: 1, 3","S: 1, 3, average of 12 s"),"the drawing moved");
    Check(ChartExport.Csv(line)==ChartExport.Csv(line with{Series=[line.Series[0] with{AverageOf=null}]}),"the CSV");
});
Test("The shared readout says an average of the app's once in the column's label when every entry shares it, and after each such entry otherwise; the component's status line reads the same",()=>{
    double[] at=[6,18,30,42];
    ChartSpec Two(string? first,string? second,double? gap=null)=>new(){Title="Ride",Kind=ChartKind.Line,XFormat=ValueFormat.Duration,SharedReadout=true,
        Series=[new("Heart rate",at.Select(x=>new ChartPoint(x,x==gap?null:140+x)).ToArray()){AverageOf=first},new("Power",at.Select(x=>new ChartPoint(x,200+x)).ToArray()){AverageOf=second}]};
    var shared=ChartSvg.Readout(Two("12 s","12 s"));
    Check(shared.Columns.All(c=>c.Label.EndsWith(" · average of 12 s")&&c.Entries.All(e=>!e.Text.Contains("average")))&&shared.Columns[0].Text=="0:06 · average of 12 s · Heart rate 146 · Power 206",shared.Columns[0].Text);
    var one=ChartSvg.Readout(Two("12 s",null));
    Check(one.Columns[0].Text=="0:06 · Heart rate 146, average of 12 s · Power 206",one.Columns[0].Text);
    var differ=ChartSvg.Readout(Two("12 s","1 min"));
    Check(differ.Columns[0].Text=="0:06 · Heart rate 146, average of 12 s · Power 206, average of 1 min",differ.Columns[0].Text);
    // A missing value says nothing: shared, the label still says it once; otherwise the missing entry reads plainly.
    Check(ChartSvg.Readout(Two("12 s","12 s",18)).Columns[1].Text=="0:18 · average of 12 s · Heart rate missing · Power 218","shared with a gap");
    Check(ChartSvg.Readout(Two("12 s","1 min",18)).Columns[1].Text=="0:18 · Heart rate missing · Power 218, average of 1 min","differing with a gap");
    // Without AverageOf the readout reads as before.
    Check(ChartSvg.Readout(Two(null,null)).Columns[0].Text=="0:06 · Heart rate 146 · Power 206","plain");
    // Lumen's own averages keep the slice's words in the label, and a series that says its own says nothing more there.
    var ride=Ride37(14_400);
    var mine=ChartSvg.Readout(ride with{Series=ride.Series.Select(s=>s with{AverageOf="1 s"}).ToArray()});
    Check(mine.Columns[0].Label=="0:12 · average of 24 s"&&mine.Columns[0].Entries.All(e=>!e.Text.Contains("average")),mine.Columns[0].Text);
    // The component's status line reads the column, and a chart without a readout reads the point's words.
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var status="";
    Operate(Two("12 s","12 s"),async chart=>{await chart.Readout(1);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(status=="0:18 · average of 12 s · Heart rate 158 · Power 218",status);
    Operate(Spec(ChartKind.Column) with{Series=[Spec().Series[0] with{AverageOf="a week"}]},async chart=>{await chart.SelectPoint(0,1);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(status=="Series: B = 5, average of a week",status);
    var html=Operate(Spec(ChartKind.Column) with{Series=[Spec().Series[0] with{AverageOf="a week"}]},async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(0,1);});
    Check(html.Contains("<tr><td>Series</td><td>B</td><td>5</td></tr>"),"the data table");
    // The gallery's pre-averaged channels say it once a column.
    var gallery=ChartSvg.Readout(DemoData.PreAveraged(ChartTheme.Light));
    Check(gallery.Columns.Count==100&&gallery.Columns.All(c=>c.Label.EndsWith(" · average of 12 s")&&c.Entries.Count==2&&c.Entries.All(e=>!e.Text.Contains("average"))),gallery.Columns[0].Text);
});
Test("AverageOf is refused blank, past 16 characters, across lines and on marks named by several values, none, a count, a sum or a share, each with its reason",()=>{
    ChartSpec With(ChartSpec s,string of)=>s with{Series=s.Series.Select(x=>x with{AverageOf=of}).ToArray()};
    Check(Refused(With(Spec()," ")).StartsWith("AverageOf says what a series' points are averages of"),"blank");
    Check(Refused(With(Spec(),new string('x',17))).StartsWith("AverageOf is said after each value")&&Refused(With(Spec(),"12\ns")).StartsWith("AverageOf is said after each value")&&Svg(With(Spec(),new string('x',16))) is not null,"long or two lines");
    foreach(var kind in new[]{ChartKind.Candlestick,ChartKind.Ohlc,ChartKind.Range,ChartKind.Histogram,ChartKind.Box,ChartKind.Violin,ChartKind.Timeline,ChartKind.Calendar,ChartKind.Donut,ChartKind.Gauge,ChartKind.Ring})
        Check(Refused(With(Sample(kind),"12 s")).StartsWith("AverageOf is said after a mark's one value"),$"{kind}: {Refused(With(Sample(kind),"12 s"))}");
    Check(Refused(new ChartSpec{Title="Z",Kind=ChartKind.Strip,Width=340,Series=[new("Z",[new(0,1,"A")]){AverageOf="12 s"}]}).StartsWith("AverageOf is said after a mark's one value"),"a strip");
    // A moving-average line beside candles is a line, and takes it.
    Check(Svg(Sample(ChartKind.Candlestick) with{Series=[..Sample(ChartKind.Candlestick).Series,new("Average",[new(0,10),new(1,11)]){Kind=ChartKind.Line,AverageOf="5 days"}]}) is not null,"a line beside candles");
});
Test("FitHeight draws a bar chart as tall as its rows: 36 a row on tracks, 32 without, 38 with sub-labels, round them the room it draws in, and no 240 floor",()=>{
    // Untitled, no ticks or title under the plot, no source: 28 above the rows and 24 under them.
    foreach(var (rows,expected) in new[]{(1,88d),(3,160d),(4,196d),(10,412d)})
        Check(Tall41(ChartSvg.Render(Meters41(rows),includeLegend:false))==expected,$"{rows} rows on tracks: {Tall41(ChartSvg.Render(Meters41(rows),includeLegend:false))}");
    foreach(var (rows,expected) in new[]{(1,84d),(3,148d),(10,372d)})
        Check(Tall41(ChartSvg.Render(Meters41(rows,false),includeLegend:false))==expected,$"{rows} rows without tracks: {Tall41(ChartSvg.Render(Meters41(rows,false),includeLegend:false))}");
    // The plot holds exactly the rows, each 36 tall, and the tracks stand 36 apart.
    var (top,bottom)=PaneSpan(PaneClips(XDocument.Parse(ChartSvg.Render(Meters41(4),includeLegend:false)))[0]);
    Check(Close(top,28)&&Close(bottom,28+4*36),$"{top} {bottom}");
    var tracks=Tracks39(XDocument.Parse(ChartSvg.Render(Meters41(4),includeLegend:false)));
    Check(tracks.Length==4&&tracks.Zip(tracks.Skip(1)).All(p=>Close(Attr(p.Second,"y")-Attr(p.First,"y"),36))&&tracks.All(t=>Close(Attr(t,"height"),18)),"the rows");
    // Titles drawn take 78 above the rows; a description on two lines 14 more.
    Check(Tall41(ChartSvg.Render(Meters41(3) with{DrawTitles=true},includeLegend:false))==210,"titled");
    Check(Tall41(ChartSvg.Render(Meters41(3) with{DrawTitles=true,Description="A description long enough that a 340-unit card has to set it over a second line of its own"},includeLegend:false))==224,"two lines of description");
    // Tick labels or a value-axis title bring the 76 back; a source line keeps 36, a second one 14 more.
    Check(Tall41(ChartSvg.Render(Meters41(3) with{YTickLabels=TickLabels.All},includeLegend:false))==28+108+76&&Tall41(ChartSvg.Render(Meters41(3) with{YLabel="Score"},includeLegend:false))==212,"ticks or a title");
    Check(Tall41(ChartSvg.Render(Meters41(3) with{Source="Invented"},includeLegend:false))==172,"a source");
    Check(Tall41(ChartSvg.Render(Meters41(3) with{Source="An invented source line long enough that it runs over a second line on a phone card"},includeLegend:false))==186,"two source lines");
    // Sub-labels make every row 38, so the two lines never collide; every name and sub-label is written.
    var subbed=Meters41(3) with{Series=[new("Score",[new(0,82,"Pacing"){SubLabel="Top 10%"},new(1,64,"Recovery"),new(2,91,"Technique")])]};
    var sdoc=XDocument.Parse(ChartSvg.Render(subbed,includeLegend:false));
    Check(Tall41(ChartSvg.Render(subbed,includeLegend:false))==28+3*38+24&&sdoc.Root!.Elements(ns+"text").Count(t=>(string?)t.Attribute("text-anchor")=="end")==4,"sub-labels");
    // The legend Render includes goes under, 22 a row; Height is checked but not used.
    Check(Tall41(ChartSvg.Render(Meters41(3)))==182,"the legend");
    Check(ChartSvg.Render(Meters41(3))==ChartSvg.Render(Meters41(3) with{Height=2000}),"Height was used");
    Check(Refused(Meters41(3) with{Height=100}).Length>0&&Refused(Meters41(3) with{Height=3000}).Length>0,"Height unchecked");
    // Without FitHeight a bar chart keeps its height and its 76-unit floor without tracks.
    Check(Tall41(ChartSvg.Render(Meters41(3,false) with{FitHeight=false},includeLegend:false))==240&&Tall41(ChartSvg.Render(Meters41(1) with{FitHeight=false},includeLegend:false))==240,"unfitted");
    var plain=XDocument.Parse(ChartSvg.Render(Meters41(3,false) with{FitHeight=false},includeLegend:false));
    Check(Close(PaneSpan(PaneClips(plain)[0]).Bottom,240-76),"the floor without tracks");
    // The component draws the same height.
    var html=Operate(Meters41(3),_=>Task.CompletedTask);
    Check(html.Contains("viewBox='0 0 340 160'"),"the component");
    // The gallery's fitted meters: titled, three rows.
    Check(Tall41(ChartSvg.Render(DemoData.FittedMeters(ChartTheme.Light),includeLegend:false))==78+108+24,"the gallery's meters");
});
Test("FitHeight is refused on every kind but horizontal bars, with its reason",()=>{
    foreach(var kind in Enum.GetValues<ChartKind>().Where(k=>k!=ChartKind.Bar))
        Check(Refused(Sample(kind) with{FitHeight=true}).StartsWith("FitHeight works out a horizontal bar chart's height"),$"{kind}: {Refused(Sample(kind) with{FitHeight=true})}");
    Check(Svg(Sample(ChartKind.Bar) with{FitHeight=true}) is not null,"bars");
});
Test("The three settings round-trip through the HTTP API's JSON, stay out of the gradient hash at their defaults, and every kind draws byte for byte as before with them written out",()=>{
    var spec=Meters41(3) with{PaintBackground=false,Series=[Meters41(3).Series[0] with{AverageOf="3 races"}]};
    var json=System.Text.Json.JsonSerializer.Serialize(spec,finishJson);
    Check(json.Contains("\"paintBackground\":false")&&json.Contains("\"fitHeight\":true")&&json.Contains("\"averageOf\":\"3 races\""),json);
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(!back.PaintBackground&&back.FitHeight&&back.Series[0].AverageOf=="3 races"&&ChartSvg.Render(back)==ChartSvg.Render(spec),"the spec changed in transit");
    var defaults=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"kind\":\"Bar\",\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":1}]}]}",finishJson)!;
    Check(defaults.PaintBackground&&!defaults.FitHeight&&defaults.Series[0].AverageOf is null,"the defaults");
    var graph=System.Text.Json.JsonSerializer.Deserialize<GraphSpec>("{\"title\":\"G\",\"paintBackground\":false,\"nodes\":[{\"id\":\"a\",\"label\":\"A\"}]}",finishJson)!;
    Check(!graph.PaintBackground&&!GraphEngine.Render(graph).Contains("background:"),"a graph");
    string GradientId(ChartSpec s)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(s),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}],Annotations=[new(AnnotationAxis.Y,2){Label="T"}]};
    Check(GradientId(faded)=="lumen-4bce89394b87"&&GradientId(faded with{PaintBackground=true,FitHeight=false,Series=[faded.Series[0] with{AverageOf=null}]})=="lumen-4bce89394b87","the defaults moved a gradient's name");
    // Unpainted and averaged charts name their gradients as the painted, plain one does; a fitted one does not need to.
    Check(GradientId(faded with{PaintBackground=false})=="lumen-4bce89394b87"&&GradientId(faded with{Series=[faded.Series[0] with{AverageOf="12 s"}]})=="lumen-4bce89394b87","unpainted or averaged");
    var bars=Sample(ChartKind.Bar) with{Series=[Sample(ChartKind.Bar).Series[0] with{Gradient=[new(1,"#A88200"),new(9,"#DD4B45")]}]};
    Check(GradientId(bars with{FitHeight=true})!=GradientId(bars),"fitted bars kept a gradient's name");
    foreach(var kind in Enum.GetValues<ChartKind>())
        Check(ChartSvg.Render(Sample(kind))==ChartSvg.Render(Sample(kind) with{PaintBackground=true,FitHeight=false,Series=Sample(kind).Series.Select(s=>s with{AverageOf=null}).ToArray()}),$"{kind}");
});
// 0.42.0: words at a missing value. A missing value written as a word at the foot of its plot and named as a mark, and the component's
// viewport a tab stop only while it scrolls. Every example is invented.
ChartSpec Season42(string? gap="absent",bool reversed=false,string? own=null,string? series="#22d3ee",ChartStyle? style=null)=>new(){Title="Team rider",Description="Share of the category finished ahead of",
    Kind=ChartKind.Line,Width=340,Height=260,Style=style ?? ChartStyle.Light,XMin=-.5,XMax=4.5,YMin=0,YMax=100,YUnit="%",YReversed=reversed,
    Series=[new("Share",[new(0,78,"Round 1"){ValueNote=" · 9th of 38"},new(1,68,"Round 2"),new(2,null,"Round 3"){GapLabel=gap,Color=own},new(3,85,"Round 5"),new(4,94,"Round 6")],series){StrokeWidth=2,Markers=MarkerStyle.Filled}]};
// The gap labels a drawing writes: each group's halo and its word.
XElement[] Gaps42(XDocument doc)=>doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("class")=="lumen-gap").ToArray();
XElement Gapped42(XDocument doc,int point)=>Datums(doc,0).Single(m=>(string?)m.Attribute("data-point")==point.ToString(CultureInfo.InvariantCulture));
double At42(ChartSpec s,double x)=>76+(x-s.XMin!.Value)/(s.XMax!.Value-s.XMin!.Value)*(s.Width-76-30);
// The library's generous estimate of 11 px text's width.
double Wide42(string text)=>text.Sum(c=>c is '.' or ',' or ':' or ' '?.3:c is '-'?.36:c is 'm' or 'M' or 'w' or 'W'?.9:.62)*11;
// The team rider as another kind of series, its line's stroke width left out.
ChartSpec Kinded42(ChartKind kind)=>Season42() with{Kind=kind,Series=[Season42().Series[0] with{StrokeWidth=null}]};
Test("A gap label is written at its point's X just inside the plot above its bottom edge, or below its top edge on a reversed axis, centred, 11 px, moved in from the plot's sides",()=>{
    var spec=Season42();var doc=Svg(spec);
    var (top,bottom)=PaneSpan(PaneClips(doc)[0]);
    var gaps=Gaps42(doc);
    Check(gaps.Length==1&&(string?)gaps[0].Attribute("text-anchor")=="middle"&&(string?)gaps[0].Attribute("font-size")=="11"&&(string?)gaps[0].Attribute("aria-hidden")=="true"&&(string?)gaps[0].Attribute("pointer-events")=="none","the group");
    var word=gaps[0].Elements(ns+"text").ToArray();
    Check(word.Length==2&&word.All(t=>t.Value=="absent"&&Close(Attr(t,"x"),At42(spec,2))&&Close(Attr(t,"y"),bottom-5)),$"{Attr(word[0],"x")} {Attr(word[0],"y")}, the plot {top}–{bottom}");
    // Reversed, the value axis starts at the top, so the word stands under the plot's top edge.
    var high=Gaps42(Svg(Season42(reversed:true)))[0].Elements(ns+"text").Last();
    Check(Close(Attr(high,"y"),top+13)&&Close(Attr(high,"x"),At42(spec,2)),$"{Attr(high,"y")}");
    // At the first or last X the word is moved in so it is never cut, and the X axis's labels and the Y axis's ticks stay clear of it.
    var edge=Season42() with{Series=[Season42().Series[0] with{Points=[new(0,null,"Round 1"){GapLabel="did not start"[..12]},..Season42().Series[0].Points.Skip(1).Select(p=>p with{GapLabel=null})]}]};
    var first=Svg(edge);var placed=Gaps42(first)[0].Elements(ns+"text").Last();
    var half=Wide42("did not star")/2;
    Check(Close(Attr(placed,"x"),76+half+2)&&Attr(placed,"x")-half>=76,$"{Attr(placed,"x")}");
    var ticks=first.Root!.Elements(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-muted").ToArray();
    Check(ticks.All(t=>Attr(t,"y")>bottom+10||Attr(t,"x")<=64),"a tick label inside the plot");
    // A pane below the main plot writes its word at its own foot; a point the X range leaves out writes none and has no mark.
    var paned=Season42() with{Panes=[new ChartPane{Label="Points"}],Series=[new("Team",[new(0,60),new(4,70)]),Season42().Series[0] with{Pane=1}]};
    var pdoc=Svg(paned);var lower=PaneSpan(PaneClips(pdoc)[1]);
    Check(Close(Attr(Gaps42(pdoc)[0].Elements(ns+"text").Last(),"y"),lower.Bottom-5),"the pane");
    var zoomed=Svg(Season42() with{XMin=2.6});
    Check(Gaps42(zoomed).Length==0&&Datums(zoomed,0).All(m=>(string?)m.Attribute("data-point")!="2"),"out of view");
    // Scatter points and areas take it too.
    Check(Gaps42(Svg(Kinded42(ChartKind.Scatter))).Length==1&&Gaps42(Svg(Kinded42(ChartKind.Area))).Length==1,"scatter and area");
});
Test("A gap label takes its point's colour where it clears 4.5:1, else the series colour where that does, else the text colour, over a halo in the background colour",()=>{
    string Ink(ChartSpec s)=>Gaps42(Svg(s))[0].Elements(ns+"text").Last().Attribute("fill")!.Value;
    // #8A6500 stands 5.33:1 on white, #E0A800 2.15:1; #1F5FA8 6.44:1 and #22d3ee 1.81:1.
    Check(Ink(Season42(own:"#8A6500"))=="#8A6500","its own colour");
    Check(Ink(Season42(own:"#E0A800",series:"#1F5FA8"))=="#1F5FA8","the series colour");
    Check(Ink(Season42(own:"#E0A800"))==ChartStyle.Light.Text&&Ink(Season42())==ChartStyle.Light.Text,"the text colour");
    Check(Lumen.Charts.Contrast.Ratio("#8A6500","#FFFFFF")>=4.5&&Lumen.Charts.Contrast.Ratio("#E0A800","#FFFFFF")<4.5&&Lumen.Charts.Contrast.Ratio("#1F5FA8","#FFFFFF")>=4.5&&Lumen.Charts.Contrast.Ratio("#22d3ee","#FFFFFF")<4.5,"the ratios");
    // On a dark card the gold clears and is used; the halo is the card's colour, as a value label's is.
    var card=ChartStyle.Dark with{Background="#161618"};
    var halo=Gaps42(Svg(Season42(own:"#e0a800",style:card)))[0].Elements(ns+"text").ToArray();
    Check(halo[1].Attribute("fill")!.Value=="#e0a800"&&halo[0].Attribute("fill")!.Value=="#161618"&&halo[0].Attribute("stroke")!.Value=="#161618"&&halo[0].Attribute("stroke-width")!.Value=="3","the halo");
    Check(Gaps42(Svg(Season42(style:ChartStyle.Midnight)))[0].Elements(ns+"text").First().Attribute("stroke")!.Value==ChartStyle.Midnight.Background,"Midnight's halo");
    // Every gap label drawn clears 4.5:1 against the background, whatever the colours asked for.
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight,DemoData.Harbour})
        foreach(var own in new string?[]{null,"#E0A800","#8A6500","#FFFFFF","#000000"})
            foreach(var line in new[]{"#22d3ee","#1F5FA8","#808080"})
                Check(Lumen.Charts.Contrast.Ratio(Ink(Season42(own:own,series:line,style:style)),style.Background)>=4.5,$"{own} {line} on {style.Background}");
});
Test("A gap label's point is a focusable mark named and tooltipped with the word in place of missing, its note after it, and the line still breaks there",()=>{
    var doc=Svg(Season42());
    var mark=Gapped42(doc,2);
    Check((string?)mark.Attribute("class")=="lumen-datum"&&(string?)mark.Attribute("tabindex")=="0"&&(string?)mark.Attribute("role")=="button"&&mark.Attribute("aria-label")!.Value=="Share: Round 3, absent"&&mark.Element(ns+"title")!.Value=="Share: Round 3, absent","the mark");
    // Its target is an invisible box round the word, inside the plot.
    var box=mark.Element(ns+"rect")!;var word=Gaps42(doc)[0].Elements(ns+"text").Last();
    Check((string?)box.Attribute("fill-opacity")=="0"&&Attr(box,"x")<Attr(word,"x")-Wide42("absent")/2&&Attr(box,"x")+Attr(box,"width")>Attr(word,"x")+Wide42("absent")/2&&Attr(box,"y")<Attr(word,"y")-9&&Attr(box,"y")+Attr(box,"height")>Attr(word,"y"),"the target");
    // A note follows the word, and the other marks are named as before.
    var noted=Svg(Season42() with{Series=[Season42().Series[0] with{Points=Season42().Series[0].Points.Select((p,i)=>i==2?p with{ValueNote=" · DNS"}:p).ToArray()}]});
    Check(Gapped42(noted,2).Attribute("aria-label")!.Value=="Share: Round 3, absent · DNS"&&Names37(noted)[0]=="Share: Round 1, 78% · 9th of 38","the note");
    // The line breaks at it exactly as it does without the word: the strokes are the same.
    string[] Strokes(XDocument d)=>d.Descendants(ns+"path").Where(p=>(string?)p.Attribute("fill")=="none").Select(p=>p.ToString()).ToArray();
    var plain=Svg(Season42(gap:null));
    Check(Strokes(doc).Length==2&&Strokes(doc).SequenceEqual(Strokes(plain)),$"{Strokes(doc).Length} strokes");
    // Without the word the missing value has no mark, as before; with it, only the mark and the word are added to the drawing.
    Check(Datums(plain,0).Length==4&&Datums(doc,0).Length==5,"the marks");
    var stripped=System.Text.RegularExpressions.Regex.Replace(ChartSvg.Render(Season42()),"<g class='lumen-datum'[^>]*data-point='2'[^>]*>.*?</g>|<g class='lumen-gap'.*?</g>","");
    Check(stripped==ChartSvg.Render(Season42(gap:null)),"more than the mark and its word moved");
    // A scatter point's mark is named the same way, and an area's.
    Check(Gapped42(Svg(Kinded42(ChartKind.Scatter)),2).Attribute("aria-label")!.Value=="Share: Round 3, absent"&&Gapped42(Svg(Kinded42(ChartKind.Area)),2).Attribute("aria-label")!.Value=="Share: Round 3, absent","scatter and area");
});
Test("Gap labels that would collide are thinned as value labels are: the later word is left out, its mark kept as a narrow target at its X and its word in its name",()=>{
    // Twenty rounds across 340 units stand 11.7 apart, so the word of the round after a written one has no room.
    var crowded=Season42() with{XMax=19.5,Series=[new("Share",Enumerable.Range(0,20).Select(i=>new ChartPoint(i,i is 8 or 9 or 14?null:50+i,$"R{i+1}"){GapLabel=i is 8 or 9 or 14?"absent":null}).ToArray())]};
    var doc=Svg(crowded);var gaps=Gaps42(doc);
    Check(gaps.Length==2&&gaps.Select(g=>Attr(g.Elements(ns+"text").Last(),"x")).SequenceEqual([At42(crowded,8),At42(crowded,14)]),string.Join(",",gaps.Select(g=>Attr(g.Elements(ns+"text").Last(),"x"))));
    var thinned=Gapped42(doc,9);var box=thinned.Element(ns+"rect")!;
    Check(thinned.Attribute("aria-label")!.Value=="Share: R10, absent"&&Close(Attr(box,"width"),10)&&Close(Attr(box,"x")+5,At42(crowded,9)),"the thinned mark");
    // Value labels keep clear of a word written before them, and a word of one written before it.
    var labelled=crowded with{Series=[crowded.Series[0] with{ValueLabels=true}],YMin=50,YMax=80};
    var ldoc=Svg(labelled);
    var boxes=ldoc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("class") is "lumen-value" or "lumen-gap").Select(g=>{var t=g.Elements(ns+"text").Last();var w=Wide42(t.Value);return new Bounds(Attr(t,"x")-w/2,Attr(t,"y")-9,Attr(t,"x")+w/2,Attr(t,"y")+3);}).ToArray();
    Check(boxes.Length>2&&!boxes.SelectMany((a,i)=>boxes.Skip(i+1).Select(b=>a.Overlaps(b))).Any(o=>o),"two labels overlap");
});
Test("A gap label is refused on a point with a value, on other kinds and a density scatter, on a sparkline, blank, past 12 characters and across lines, each with its reason",()=>{
    ChartSpec With(ChartSpec s,string gap,double? y=null)=>s with{Series=[s.Series[0] with{Points=[s.Series[0].Points[0] with{Y=y,GapLabel=gap},..s.Series[0].Points.Skip(1)]},..s.Series.Skip(1)]};
    Check(Refused(With(Spec(),"absent",2)).StartsWith("A gap label is written where a value is missing"),"a valued point");
    foreach(var kind in new[]{ChartKind.Column,ChartKind.Bar,ChartKind.StackedColumn,ChartKind.Bubble,ChartKind.Band,ChartKind.Heatmap,ChartKind.Radar,ChartKind.Donut})
        Check(Refused(With(Sample(kind),"absent")).StartsWith("A gap label is written where a line, an area or scatter points miss a value"),$"{kind}: {Refused(With(Sample(kind),"absent"))}");
    Check(Refused(Spec(ChartKind.Column) with{Series=[Spec().Series[0],new("Share",[new(0,null,"A"){GapLabel="absent"}]){Kind=ChartKind.Column}]}).StartsWith("A gap label is written where a line"),"a column series");
    Check(Refused(With(Spec(ChartKind.Scatter),"absent") with{DensityCells=20}).StartsWith("A density scatter shades cells rather than points, so it writes no gap label"),"a density scatter");
    Check(Refused(With(Spark(),"absent")).StartsWith("A sparkline draws its data alone, with no words, so it writes no gap labels"),"a sparkline");
    Check(Refused(With(Spec()," ")).StartsWith("A gap label is the word a missing value is written as")&&Refused(With(Spec(),"")).StartsWith("A gap label is the word a missing value is written as"),"blank");
    Check(Refused(With(Spec(),"did not finish")).StartsWith("A gap label is one short word or two")&&Refused(With(Spec(),"no\nresult")).StartsWith("A gap label is one short word or two")&&Svg(With(Spec(),"did not star")) is not null,"long or two lines");
    // A line series beside columns takes it, its word at the foot of the plot.
    Check(Gaps42(Svg(Spec(ChartKind.Column) with{Series=[Spec().Series[0],new("Share",[new(0,4,"A"),new(1,null,"B"){GapLabel="absent"},new(2,5,"C")]){Kind=ChartKind.Line}]})).Length==1,"a line beside columns");
});
Test("The shared readout reads a gap label's X, even where no series has a value, as the series' word, and the component's status line and data table say it",()=>{
    var read=ChartSvg.Readout(Season42() with{SharedReadout=true});
    var column=read.Columns.Single(c=>c.X==2);
    Check(read.Columns.Count==5&&column.Text=="Round 3 · Share absent"&&column.Entries.Single() is {Series:0,Point:2,Position:null},column.Text);
    // Without the word the X with no value is not read, as before.
    Check(ChartSvg.Readout(Season42(gap:null) with{SharedReadout=true}).Columns.Count==4,"without the word");
    // Beside another series the word stands in the column with that series' value; a note follows it.
    var two=Season42() with{SharedReadout=true,Series=[Season42().Series[0] with{Points=Season42().Series[0].Points.Select((p,i)=>i==2?p with{ValueNote=" · DNS"}:p).ToArray()},new("Team",[new(0,60),new(1,62),new(2,64),new(3,66),new(4,68)])]};
    Check(ChartSvg.Readout(two).Columns.Single(c=>c.X==2).Text=="Round 3 · Share absent · DNS · Team 64%",ChartSvg.Readout(two).Columns.Single(c=>c.X==2).Text);
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var status="";
    Operate(Season42(),async chart=>{await chart.SelectPoint(0,2);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(status=="Share: Round 3 = absent",status);
    Operate(Season42() with{SharedReadout=true},async chart=>{await chart.SelectPoint(0,2);status=(string)typeof(LumenChart).GetField("status",flags)!.GetValue(chart)!;});
    Check(status=="Round 3 · Share absent",status);
    var html=Operate(Season42(),async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(0,2);});
    Check(html.Contains("<tr><td>Share</td><td>Round 3</td><td>absent</td></tr>")&&Operate(Season42(gap:null),async chart=>{typeof(LumenChart).GetField("showData",flags)!.SetValue(chart,true);await chart.SelectPoint(0,0);}).Contains("<td>Round 3</td><td>Missing</td>"),"the data table");
});
Test("The component's viewport is written as a scrolling region and a tab stop as before, and its script takes both away while the drawing fits and gives them back while it scrolls",()=>{
    var html=Operate(Season42(),_=>Task.CompletedTask,fit:true);
    Check(html.Contains("<div class=\"lumen-viewport\" tabindex=\"0\" role=\"region\" aria-label=\"Scrollable chart\">"),"the markup");
    var script=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Lumen.Charts.Blazor/wwwroot/lumen.js"));
    Check(script.Contains("viewport.scrollWidth > viewport.clientWidth + 1")&&script.Contains("viewport.setAttribute('tabindex', '-1')")&&script.Contains("viewport.removeAttribute('role')")&&script.Contains("viewport.setAttribute('aria-label', 'Scrollable chart')")
        &&script.Contains("new ResizeObserver(region)")&&script.Contains("sized.observe(svg)")&&script.Contains("state.sized?.disconnect()"),"the script");
});
Test("GapLabel round-trips through the HTTP API's JSON, a point that names none keeps it null, it stays out of the gradient hash while null, and every kind draws byte for byte as before",()=>{
    var spec=Season42(own:"#8A6500");
    var json=System.Text.Json.JsonSerializer.Serialize(spec,finishJson);
    Check(json.Contains("\"gapLabel\":\"absent\"")&&json.Contains("\"y\":null"),json);
    var back=System.Text.Json.JsonSerializer.Deserialize<ChartSpec>(json,finishJson)!;
    Check(back.Series[0].Points[2].GapLabel=="absent"&&ChartSvg.Render(back)==ChartSvg.Render(spec),"the spec changed in transit");
    Check(System.Text.Json.JsonSerializer.Deserialize<ChartSpec>("{\"series\":[{\"name\":\"S\",\"points\":[{\"x\":0,\"y\":null}]}]}",finishJson)!.Series[0].Points[0].GapLabel is null,"the default");
    string GradientId(ChartSpec s)=>System.Text.RegularExpressions.Regex.Match(ChartSvg.Render(s),"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}],Annotations=[new(AnnotationAxis.Y,2){Label="T"}]};
    Check(GradientId(faded)=="lumen-4bce89394b87"&&GradientId(faded with{Series=[faded.Series[0] with{Points=faded.Series[0].Points.Select(p=>p with{GapLabel=null}).ToArray()}]})=="lumen-4bce89394b87",GradientId(faded));
    var gapped=faded with{Series=[faded.Series[0] with{Points=[..faded.Series[0].Points,new(2,null){GapLabel="absent"}]}]};
    var ungapped=faded with{Series=[faded.Series[0] with{Points=[..faded.Series[0].Points,new(2,null)]}]};
    Check(GradientId(gapped)!=GradientId(ungapped),"a gap label kept a gradient's name");
    foreach(var kind in Enum.GetValues<ChartKind>())
        Check(ChartSvg.Render(Sample(kind))==ChartSvg.Render(Sample(kind) with{Series=Sample(kind).Series.Select(s=>s with{Points=s.Points.Select(p=>p with{GapLabel=null}).ToArray()}).ToArray()}),$"{kind}");
    // CSV keeps the empty value, as before.
    Check(ChartExport.Csv(spec)==ChartExport.Csv(Season42(gap:null,own:"#8A6500")),"the CSV");
});
Test("Home page: the team rider's missed round is written absent in a gold that clears 4.5:1 in both themes, round 4 has no point, and every other round is named with its place and field",()=>{
    foreach(var theme in new[]{ChartTheme.Light,ChartTheme.Dark})
    {
        var spec=DemoData.TeamRider(theme);var doc=Svg(spec);
        Check(spec.Series[0].Points.Count==7&&spec.Series[0].Points.All(p=>p.Label!="Round 4")&&spec.Series[0].Points.Count(p=>p.Y is null)==1,"the rounds");
        var gaps=Gaps42(doc);var own=spec.Series[0].Points.Single(p=>p.Y is null).Color!;
        Check(gaps.Length==1&&gaps[0].Elements(ns+"text").Last().Attribute("fill")!.Value==own&&Lumen.Charts.Contrast.Ratio(own,ChartSvg.ResolveStyle(spec).Background)>=4.5,$"{theme}");
        Check(Names37(doc).SequenceEqual(["Share: Round 1, 78% · 9th of 38","Share: Round 2, 68% · 14th of 41","Share: Round 3, 89% · 5th of 37","Share: Round 5, 85% · 7th of 40","Share: Round 7, 94% · 3rd of 36","Share: Round 8, 88% · 6th of 42","Share: Round 6, absent"]),string.Join("|",Names37(doc)));
    }
    // Under Midnight the light theme's gold falls short, so the word takes the series colour, which clears.
    var midnight=Svg(DemoData.TeamRider(ChartTheme.Light) with{Style=ChartStyle.Midnight});
    Check(Gaps42(midnight)[0].Elements(ns+"text").Last().Attribute("fill")!.Value==ChartStyle.Midnight.SeriesColor(0)&&Lumen.Charts.Contrast.Ratio(ChartStyle.Midnight.SeriesColor(0),ChartStyle.Midnight.Background)>=4.5,"Midnight");
    Check(DemoData.Ordinal(1)=="1st"&&DemoData.Ordinal(2)=="2nd"&&DemoData.Ordinal(3)=="3rd"&&DemoData.Ordinal(11)=="11th"&&DemoData.Ordinal(12)=="12th"&&DemoData.Ordinal(13)=="13th"&&DemoData.Ordinal(21)=="21st"&&DemoData.Ordinal(14)=="14th","ordinals");
});
Test("Race Face recipe: the team rider's gold word stands 8.41:1 on the card and its cyan line 10.00:1",()=>{
    Check(Math.Round(Lumen.Charts.Contrast.Ratio("#e0a800","#161618"),2)==8.41&&Math.Round(Lumen.Charts.Contrast.Ratio("#22d3ee","#161618"),2)==10.00,$"{Lumen.Charts.Contrast.Ratio("#e0a800","#161618")} {Lumen.Charts.Contrast.Ratio("#22d3ee","#161618")}");
});
// A colour a fraction of the way from one to another, channel by channel, as the library blends a gradient.
string Blend40(string from,string to,double t){int C(string h,int at)=>int.Parse(h.AsSpan(at,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture);return "#"+string.Concat(new[]{1,3,5}.Select(at=>((int)(C(from,at)+(C(to,at)-C(from,at))*t)).ToString("X2",CultureInfo.InvariantCulture)));}
// ---- 0.43.0: event planner (static) ----
PlannerSpec PlanYear(Func<PlannerSpec,PlannerSpec>? change=null)
{
    var spec=PlannerSpec.ForYear(2027) with{
        Title="Season planner",Description="Invented organizers' events",
        Regions=[new("ZA","South Africa"),new("ZA-GP","Gauteng","ZA"),new("ZA-WC","Western Cape","ZA")],
        Periods=[new(new(2027,4,27),null,"Freedom Day",PeriodKind.PublicHoliday,"ZA"),
                 new(new(2027,3,27),new DateOnly(2027,4,5),"School holiday",PeriodKind.SchoolHoliday,"ZA")],
        Events=[new("e1","Hilltop XCO",new(2027,3,13)){Region="ZA-GP",Category="XCO",Audience="Kids",Relevance=PlannerRelevance.Clash},
                new("e2","Coast Stage Race",new(2027,3,12)){End=new DateOnly(2027,3,14),Region="ZA-WC",Category="Stage",Audience="Open",Status=PlannerStatus.Provisional}]};
    return change is null?spec:change(spec);
}
Test("Planner: a valid year passes validation and a year spans 1 January to 31 December",()=>{
    var spec=PlanYear();PlannerValidation.Validate(spec);
    Check(spec.From==new DateOnly(2027,1,1)&&spec.To==new DateOnly(2027,12,31),$"{spec.From}..{spec.To}");
    Check(spec.WeekStart==DayOfWeek.Monday&&spec.Weekend.SequenceEqual([DayOfWeek.Saturday,DayOfWeek.Sunday]));
});
Test("Planner: refuses a period that ends before it starts, is longer than 400 days, a blank title, and too narrow a width",()=>{
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{To=s.From.AddDays(-1)})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{To=s.From.AddDays(400)})));
    PlannerValidation.Validate(PlanYear(s=>s with{To=s.From.AddDays(399)}));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Title=" "})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Width=319})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Weekend=[]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Weekend=[DayOfWeek.Saturday,DayOfWeek.Saturday]})));
});
Test("Planner: refuses duplicate, blank or unknown region codes and a parent chain that loops",()=>{
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Regions=[..s.Regions,new("ZA","Again")]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Regions=[..s.Regions,new(" ","Blank")]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Regions=[..s.Regions,new("ZA-KZN","KwaZulu-Natal","ZZ")]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Regions=[new("A","A","B"),new("B","B","A")]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Regions=[new("A","A","B"),new("B","B","C")]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Periods=[new(new(2027,1,1),null,"Day",PeriodKind.Other,"XX")]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=[new("x","Ride",new(2027,5,1)){Region="XX"}]})));
});
Test("Planner: refuses periods and events with blank names, ends before starts, duplicate event ids and events wholly outside the period",()=>{
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Periods=[new(new(2027,1,2),new DateOnly(2027,1,1),"Back",PeriodKind.Other)]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Periods=[new(new(2027,1,2),null," ",PeriodKind.Other)]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=[new("x","A",new(2027,5,2)){End=new DateOnly(2027,5,1)}]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=[new("x","A",new(2027,5,2)),new("x","B",new(2027,5,3))]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=[new("x"," ",new(2027,5,2))]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=[new("x","Old",new(2026,5,2))]})));
    PlannerValidation.Validate(PlanYear(s=>s with{Events=[new("x","Straddles",new(2026,12,30)){End=new DateOnly(2027,1,2)}]}));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=[new("x","Long note",new(2027,5,2)){Note=new string('n',121)}]})));
});
Test("Planner: a view must lie inside the period",()=>{
    var spec=PlanYear();
    PlannerValidation.Validate(spec,PlannerView.WholePeriod);
    PlannerValidation.Validate(spec,PlannerView.Month(2027,3));
    PlannerValidation.Validate(spec,PlannerView.Day(new(2027,12,31)));
    Reject(()=>PlannerValidation.Validate(spec,PlannerView.Month(2028,1)));
    Reject(()=>PlannerValidation.Validate(spec,PlannerView.Day(new(2026,12,31))));
});
Test("Planner calendar: a year has twelve months in order, and a season crossing a year end runs September to August with its years",()=>{
    Check(PlannerCalendar.Months(PlanYear()).Count==12);
    var season=PlanYear(s=>s with{From=new(2026,9,1),To=new(2027,8,31),Events=[]});
    var months=PlannerCalendar.Months(season);
    Check(months.Count==12&&months[0]==(2026,9)&&months[3]==(2026,12)&&months[4]==(2027,1)&&months[^1]==(2027,8),string.Join(",",months));
});
Test("Planner calendar: rows align by weekday, so every Saturday stands in one of five columns",()=>{
    // 1 January 2027 is a Friday: four blank columns before it when weeks start on Monday, five when they start on Sunday.
    Check(PlannerCalendar.Lead(2027,1,DayOfWeek.Monday)==4,$"{PlannerCalendar.Lead(2027,1,DayOfWeek.Monday)}");
    Check(PlannerCalendar.Lead(2027,1,DayOfWeek.Sunday)==5);
    for(var m=1;m<=12;m++)for(var d=1;d<=DateTime.DaysInMonth(2027,m);d++){
        var day=new DateOnly(2027,m,d);var column=PlannerCalendar.Lead(2027,m,DayOfWeek.Monday)+d-1;
        Check(column<PlannerCalendar.Columns,$"{day} in column {column}");
        Check((column%7==5)==(day.DayOfWeek==DayOfWeek.Saturday),$"{day} column {column}");
    }
});
Test("Planner calendar: a country's holiday shows under one of its provinces, a province's event under its country, and a filtered-out province's event does not",()=>{
    var spec=PlanYear(s=>s with{Filter=new(){Regions=["ZA-GP"]}});
    Check(PlannerCalendar.Includes(spec,"ZA","ZA-GP")&&!PlannerCalendar.Includes(spec,"ZA-GP","ZA"));
    Check(PlannerCalendar.Periods(spec).Any(p=>p.Name=="Freedom Day"),"the national holiday is missing under Gauteng");
    var events=PlannerCalendar.Events(spec).Select(e=>e.Id).ToArray();
    Check(events.SequenceEqual(["e1"]),string.Join(",",events));
    var national=PlanYear(s=>s with{Filter=new(){Regions=["ZA"]}});
    Check(PlannerCalendar.Events(national).Count==2);
});
Test("Planner calendar: filters by category, audience, status and relevance, and orders a day's events clash first",()=>{
    var spec=PlanYear(s=>s with{Events=[..s.Events,new("e3","Club Ride",new(2027,3,13)){Region="ZA-GP",Category="Road",Relevance=PlannerRelevance.Near}]});
    Check(PlannerCalendar.Events(spec with{Filter=new(){Categories=["XCO"]}}).Single().Id=="e1");
    Check(PlannerCalendar.Events(spec with{Filter=new(){Audiences=["Open"]}}).Single().Id=="e2");
    Check(PlannerCalendar.Events(spec with{Filter=new(){Statuses=[PlannerStatus.Provisional]}}).Single().Id=="e2");
    Check(PlannerCalendar.Events(spec with{Filter=new(){Relevances=[PlannerRelevance.Clash,PlannerRelevance.Near]}}).Select(e=>e.Id).SequenceEqual(["e1","e3"]));
    var day=PlannerCalendar.On(spec,new(2027,3,13)).Events.Select(e=>e.Id).ToArray();
    Check(day.SequenceEqual(["e1","e3","e2"]),string.Join(",",day));
});
Test("Planner calendar: long weekends join a public holiday to its weekend, three days or more, and a multi-day event is on every day it spans",()=>{
    // 27 April 2027 is a Tuesday: no long weekend. Add Monday 26 April as a holiday and Saturday 24 to Tuesday 27 becomes one.
    var plain=PlannerCalendar.LongWeekends(PlanYear());
    Check(!plain.Any(w=>w.From<=new DateOnly(2027,4,27)&&new DateOnly(2027,4,27)<=w.To),"a lone Tuesday holiday made a long weekend");
    var spec=PlanYear(s=>s with{Periods=[..s.Periods,new(new(2027,4,26),null,"Invented Monday",PeriodKind.PublicHoliday,"ZA")]});
    Check(PlannerCalendar.LongWeekends(spec).Contains((new DateOnly(2027,4,24),new DateOnly(2027,4,27))),string.Join(";",PlannerCalendar.LongWeekends(spec)));
    // A holiday on a Friday: Friday to Sunday.
    var friday=PlanYear(s=>s with{Periods=[new(new(2027,7,16),null,"Invented Friday",PeriodKind.PublicHoliday)]});
    Check(PlannerCalendar.LongWeekends(friday).Contains((new DateOnly(2027,7,16),new DateOnly(2027,7,18))));
    Check(PlannerCalendar.On(PlanYear(),new(2027,3,14)).Events.Any(e=>e.Id=="e2")&&!PlannerCalendar.On(PlanYear(),new(2027,3,15)).Events.Any(e=>e.Id=="e2"));
});
Test("Planner calendar: names say every field in words, leave out empty ones, and say status, relevance and yours",()=>{
    var spec=PlanYear();
    var e1=PlannerCalendar.Name(spec,spec.Events[0]);
    Check(e1=="Hilltop XCO, Saturday 13 March 2027, Gauteng, XCO, Kids, clash",e1);
    var e2=PlannerCalendar.Name(spec,spec.Events[1]);
    Check(e2=="Coast Stage Race, Friday 12 to Sunday 14 March 2027, Western Cape, Stage, Open, provisional",e2);
    var bare=PlannerCalendar.Name(spec,new PlannerEvent("b","Bare",new(2027,6,5)){Mine=true,Status=PlannerStatus.Cancelled,Note="Moved to June"});
    Check(bare=="Bare, Saturday 5 June 2027, cancelled, yours, Moved to June",bare);
    Check(!bare.Contains(", ,")&&!bare.Contains("null"));
    Check(PlannerCalendar.DayName(spec,new(2027,4,27))=="Tuesday 27 April 2027, Freedom Day (public holiday), no events",PlannerCalendar.DayName(spec,new(2027,4,27)));
    // 13 March is outside the school holiday (27 March to 5 April) and holds two events: Hilltop XCO and day 2 of the stage race.
    Check(PlannerCalendar.DayName(spec,new(2027,3,13))=="Saturday 13 March 2027, 2 events",PlannerCalendar.DayName(spec,new(2027,3,13)));
    Check(PlannerCalendar.DayName(spec,new(2027,3,29)).StartsWith("Monday 29 March 2027, School holiday (school holiday)"),PlannerCalendar.DayName(spec,new(2027,3,29)));
});
XDocument PlanSvg(PlannerSpec spec,PlannerView? view=null,PlannerLayout layout=PlannerLayout.Wide)=>XDocument.Parse(PlannerSvg.Render(spec,view??PlannerView.WholePeriod,layout));
IEnumerable<XElement> PlanMarks(XDocument doc)=>doc.Descendants().Where(e=>(string?)e.Attribute("class")=="lumen-datum");
Test("Planner year view: valid SVG named by its title and description, one month row each, every event a named focusable mark",()=>{
    var doc=PlanSvg(PlanYear());
    Check(doc.Root!.Name==ns+"svg"&&doc.Root.Attribute("aria-label")!.Value=="Season planner. Invented organizers' events");
    Check(doc.Descendants(ns+"text").Count(t=>t.Value is "January 2027" or "December 2027")==2);
    var marks=PlanMarks(doc).ToArray();
    Check(marks.Length==2,$"{marks.Length} marks");
    Check(marks.All(m=>m.Attribute("tabindex")!.Value=="0"&&m.Attribute("role")!.Value=="button"));
    Check(marks.Any(m=>m.Attribute("aria-label")!.Value=="Hilltop XCO, Saturday 13 March 2027, Gauteng, XCO, Kids, clash"&&m.Element(ns+"title")!.Value==m.Attribute("aria-label")!.Value));
    Check(doc.Descendants().Any(e=>(string?)e.Attribute("data-day")=="2027-04-27"&&e.Attribute("aria-label")!.Value.Contains("Freedom Day (public holiday)")));
    Check(!doc.ToString().Contains("NaN")&&!doc.ToString().Contains("Infinity"));
});
Test("Planner year view: draws a mark across a multi-day event's days and a Saturday in a weekend band",()=>{
    var doc=PlanSvg(PlanYear());
    var stage=PlanMarks(doc).Single(m=>m.Attribute("data-event")!.Value=="e2");
    var rects=stage.Descendants(ns+"rect").Select(r=>double.Parse(r.Attribute("width")!.Value,CultureInfo.InvariantCulture)).ToArray();
    Check(rects.Max()>2.5*(1100-76-24)/37.0,"a three-day event is not three days wide");
    Check(doc.Descendants(ns+"rect").Count(r=>(string?)r.Attribute("class")=="lumen-weekend")==104,"52 weekends of two days");
});
Test("Planner year view: ten events on one day draw three stripes and a +7 that names the rest",()=>{
    var many=Enumerable.Range(0,10).Select(i=>new PlannerEvent($"m{i}",$"Invented ride {i}",new(2027,5,8)){Region="ZA-GP"}).ToArray();
    var doc=PlanSvg(PlanYear(s=>s with{Events=many}));
    Check(PlanMarks(doc).Count()==3,$"{PlanMarks(doc).Count()} stripes");
    var more=doc.Descendants().Single(e=>((string?)e.Attribute("class")??"").Split(' ').Contains("lumen-more"));
    Check(more.Value.Contains("+7")&&more.Attribute("aria-label")!.Value.StartsWith("7 more on Saturday 8 May 2027: Invented ride 3"),more.Attribute("aria-label")!.Value);
});
// A "+N" lists at most 20 hidden names then "and N more", so a crowded day cannot make the drawing grow with every event it hides.
void CheckCappedMore(XElement more,string what)
{
    var label=more.Attribute("aria-label")!.Value;
    var hidden=int.Parse(label[..label.IndexOf(' ')],CultureInfo.InvariantCulture);
    Check(hidden>20,$"{what}: only {hidden} hidden");
    var names=label[(label.IndexOf(": ",StringComparison.Ordinal)+2)..].Split("; ");
    Check(names.Length==21&&names[^1]==$"and {hidden-20} more"&&names.Take(20).All(n=>n.StartsWith("Invented ride ")),$"{what}: {label}");
    var title=more.Parent!.Name==ns+"g"&&more.Parent.Attribute("role")?.Value=="presentation"?more.Parent.Element(ns+"title")!.Value:more.Element(ns+"title")!.Value;
    Check(title==label,$"{what}: the tooltip differs from the label");
}
Test("Planner year and month views: a day with 100 events writes +N naming the first 20 hidden and \"and N more\" for the rest",()=>{
    var many=Enumerable.Range(0,100).Select(i=>new PlannerEvent($"m{i}",$"Invented ride {i:000}",new(2027,5,8))).ToArray();
    var spec=PlanYear(s=>s with{Events=many});
    var year=PlanSvg(spec).Descendants().Single(e=>((string?)e.Attribute("class")??"").Split(' ').Contains("lumen-more"));
    Check(year.Value.EndsWith("+97")&&year.Attribute("aria-label")!.Value.StartsWith("97 more on Saturday 8 May 2027: Invented ride 003; Invented ride 004;")&&year.Attribute("aria-label")!.Value.EndsWith("; Invented ride 022; and 77 more"),year.Attribute("aria-label")!.Value);
    CheckCappedMore(year,"year");
    var month=PlanSvg(spec,PlannerView.Month(2027,5)).Descendants(ns+"text").Single(t=>((string?)t.Attribute("class")??"").Split(' ').Contains("lumen-more"));
    CheckCappedMore(month,"month");
    // Twenty or fewer hidden are all listed, with no "and N more".
    var few=PlanSvg(PlanYear(s=>s with{Events=many.Take(23).ToArray()})).Descendants().Single(e=>((string?)e.Attribute("class")??"").Split(' ').Contains("lumen-more"));
    Check(few.Attribute("aria-label")!.Value.StartsWith("20 more on Saturday 8 May 2027: Invented ride 003;")&&few.Attribute("aria-label")!.Value.EndsWith("; Invented ride 022")&&!few.Attribute("aria-label")!.Value.Contains("and "),few.Attribute("aria-label")!.Value);
});
Test("Planner: 2000 events with 200-character names on one 365-day span render the year, the month and the agenda well under 10 MB",()=>{
    var many=Enumerable.Range(0,PlannerValidation.MaxEvents).Select(i=>new PlannerEvent($"m{i}",$"Invented ride {i} ".PadRight(PlannerValidation.MaxText,'x'),new(2027,1,1)){End=new DateOnly(2027,12,31)}).ToArray();
    var spec=PlanYear(s=>s with{Events=many});
    var year=PlannerSvg.Render(spec,PlannerView.WholePeriod).Length;
    var month=PlannerSvg.Render(spec,PlannerView.Month(2027,3)).Length;
    var agenda=PlannerSvg.Render(spec,PlannerView.Month(2027,3),PlannerLayout.Narrow).Length;
    Check(year<10_000_000,$"the year is {year:N0} characters");
    Check(month<10_000_000,$"the month is {month:N0} characters");
    Check(agenda<10_000_000,$"the agenda is {agenda:N0} characters");
});
Test("Planner agenda: a day with 100 events draws 20 event lines and one +80 more line with the capped label",()=>{
    var many=Enumerable.Range(0,100).Select(i=>new PlannerEvent($"m{i}",$"Invented ride {i:000}",new(2027,5,8))).ToArray();
    var doc=PlanSvg(PlanYear(s=>s with{Events=many}),PlannerView.Month(2027,5),PlannerLayout.Narrow);
    Check(PlanMarks(doc).Count()==20,$"{PlanMarks(doc).Count()} event lines");
    var more=doc.Descendants(ns+"text").Single(t=>((string?)t.Attribute("class")??"").Split(' ').Contains("lumen-more"));
    Check(more.Value=="+80 more"&&more.Attribute("aria-label")!.Value.StartsWith("80 more on Saturday 8 May 2027: Invented ride 020; Invented ride 021;")&&more.Attribute("aria-label")!.Value.EndsWith("; Invented ride 039; and 60 more"),more.Attribute("aria-label")!.Value);
    CheckCappedMore(more,"agenda");
    var height=double.Parse(doc.Root!.Attribute("viewBox")!.Value.Split(" ")[3],CultureInfo.InvariantCulture);
    var last=doc.Descendants(ns+"text").Where(t=>t.Attribute("y")!=null).Max(t=>double.Parse(t.Attribute("y")!.Value,CultureInfo.InvariantCulture));
    Check(last<height,$"the last line at {last} is outside the height {height}");
    var twenty=PlanSvg(PlanYear(s=>s with{Events=many.Take(20).ToArray()}),PlannerView.Month(2027,5),PlannerLayout.Narrow);
    Check(PlanMarks(twenty).Count()==20&&!twenty.Descendants(ns+"text").Any(t=>((string?)t.Attribute("class")??"").Contains("lumen-more")),"twenty events are all drawn with no +N more");
});
Test("Planner year view: an event that starts before the period is drawn from its first day and named with its full dates",()=>{
    var doc=PlanSvg(PlanYear(s=>s with{Events=[new("x","New Year Tour",new(2026,12,30)){End=new DateOnly(2027,1,2)}]}));
    var mark=PlanMarks(doc).Single();
    Check(mark.Attribute("aria-label")!.Value.StartsWith("New Year Tour, Wednesday 30 December 2026 to Saturday 2 January 2027"),mark.Attribute("aria-label")!.Value);
    var x=double.Parse(mark.Descendants(ns+"rect").First().Attribute("x")!.Value,CultureInfo.InvariantCulture);
    var col=(1100-76-24)/37.0;
    Check(Math.Abs(x-(76+4*col+1))<0.01,$"starts at {x}, not at 1 January's column");
});
Test("Planner year view: each week is named with its clashes and close events, and a busy week writes its count",()=>{
    var spec=PlanYear(s=>s with{Events=[..s.Events,new("e3","Club Ride",new(2027,3,10)){Relevance=PlannerRelevance.Near}]});
    var doc=PlanSvg(spec);
    var week=doc.Descendants().Single(e=>(string?)e.Attribute("class")=="lumen-week"&&e.Attribute("aria-label")!.Value.StartsWith("Week of 8 March 2027"));
    Check(week.Attribute("aria-label")!.Value=="Week of 8 March 2027: 1 clash, 1 close",week.Attribute("aria-label")!.Value);
    Check(week.Descendants(ns+"text").Single().Value=="2");
    var quiet=doc.Descendants().Single(e=>(string?)e.Attribute("class")=="lumen-week"&&e.Attribute("aria-label")!.Value.StartsWith("Week of 15 March 2027"));
    Check(quiet.Attribute("aria-label")!.Value=="Week of 15 March 2027: no clashes"&&!quiet.Descendants(ns+"text").Any());
});
Test("Planner year view: a September-to-August season draws its rows in order with leap-year February",()=>{
    var doc=PlanSvg(PlanYear(s=>s with{From=new(2027,9,1),To=new(2028,8,31),Events=[],Periods=[]}));
    var months=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-muted lumen-month").Select(t=>t.Value).ToArray();
    Check(months.First()=="September 2027"&&months[4]=="January 2028"&&months.Last()=="August 2028",string.Join(",",months));
    Check(doc.Descendants().Any(e=>(string?)e.Attribute("data-day")=="2028-02-29"));
});
Test("Planner year view: a 60-character name is cut in nothing it draws (the year view writes no event words) and kept whole in its name",()=>{
    var name=new string('L',60);
    var doc=PlanSvg(PlanYear(s=>s with{Events=[new("x",name,new(2027,6,5))]}));
    Check(PlanMarks(doc).Single().Attribute("aria-label")!.Value.StartsWith(name+", Saturday 5 June 2027"));
});
Test("Planner year view: every drawn word clears 4.5:1 and every stripe and symbol 3:1 against what lies behind it in Light, Dark and Midnight, and the render is byte-stable",()=>{
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight})
    // A week starting on Wednesday puts each week's count on the Saturday and Sunday columns.
    foreach(var weekStart in new[]{DayOfWeek.Monday,DayOfWeek.Wednesday}){
        var spec=PlanYear(s=>s with{Style=style,WeekStart=weekStart,Events=[..s.Events,
            new("n1","Club Ride",new(2027,3,10)){Relevance=PlannerRelevance.Near},
            new("c1","Called Off",new(2027,6,12)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Clash},
            new("c2","Called Off Too",new(2027,6,19)){Status=PlannerStatus.Cancelled},
            new("c3","Close Called Off",new(2027,6,26)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Near},
            new("p1","Pencilled Clash",new(2027,8,21)){Status=PlannerStatus.Provisional,Relevance=PlannerRelevance.Clash},
            new("y1","Our Enduro",new(2027,5,15)){Mine=true,Relevance=PlannerRelevance.Clash},
            ..Enumerable.Range(0,5).Select(i=>new PlannerEvent($"m{i}",$"Invented ride {i}",new(2027,5,8)))]});
        var svg=PlannerSvg.Render(spec,PlannerView.WholePeriod);
        Check(svg==PlannerSvg.Render(spec,PlannerView.WholePeriod),"the render is not byte-stable");
        var doc=XDocument.Parse(svg);
        double A(XElement e,string name)=>double.Parse((string?)e.Attribute(name)??"0",CultureInfo.InvariantCulture);
        var bands=doc.Descendants(ns+"rect").Where(r=>(string?)r.Attribute("class")=="lumen-weekend")
            .Select(r=>(L:A(r,"x"),T:A(r,"y"),R:A(r,"x")+A(r,"width"),B:A(r,"y")+A(r,"height"))).ToArray();
        // A word stands where its glyphs' middle does: its anchor, three pixels above its baseline.
        bool OnBand(XElement t)=>bands.Any(b=>b.L<=A(t,"x")&&A(t,"x")<=b.R&&b.T<=A(t,"y")-3&&A(t,"y")-3<=b.B);
        var texts=doc.Descendants(ns+"text").ToArray();
        Check(texts.Any(t=>((string?)t.Attribute("class")??"").Split(' ').Contains("lumen-more")&&OnBand(t)),"no +N on a weekend band to check");
        if(weekStart==DayOfWeek.Wednesday)
            Check(doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("class")=="lumen-week").SelectMany(g=>g.Elements(ns+"text")).Any(OnBand),"no week count on a weekend band to check");
        foreach(var t in texts){
            var ink=(string?)t.Attribute("fill")??(((string?)t.Attribute("class")??"").Split(' ').Contains("lumen-muted")?style.Muted:style.Text);
            var behind=OnBand(t)?style.Grid:style.Background;
            Check(Lumen.Charts.Contrast.Ratio(ink,behind)>=4.5,$"'{t.Value}' in {ink} on {behind}: {Lumen.Charts.Contrast.Ratio(ink,behind):0.00}");
        }
        // Every stripe and symbol is checked against both grounds it may stand on; the weekend bands themselves are the grid colour.
        foreach(var mark in doc.Descendants().Where(e=>e.Name==ns+"rect"||e.Name==ns+"path"||e.Name==ns+"line")){
            Check(mark.Attribute("opacity") is null&&mark.Attribute("fill-opacity") is null&&mark.Attribute("stroke-opacity") is null,$"a faded mark: {mark}");
            foreach(var paint in new[]{(string?)mark.Attribute("fill"),(string?)mark.Attribute("stroke")}.OfType<string>().Where(p=>p!="none"&&p!=style.Grid))
                foreach(var behind in new[]{style.Background,style.Grid})
                    Check(Lumen.Charts.Contrast.Ratio(paint,behind)>=3,$"{mark.Name.LocalName} in {paint} on {behind}: {Lumen.Charts.Contrast.Ratio(paint,behind):0.00}");
        }
    }
});
Test("Planner: the legend says every pattern in words",()=>{
    var words=PlanSvg(PlanYear()).Descendants(ns+"text").Select(t=>t.Value).ToArray();
    foreach(var word in new[]{"clash","close","other","provisional","cancelled","yours","public holiday","school holiday","long weekend","weekend"})
        Check(words.Contains(word),$"the legend lacks '{word}'");
});
Test("Planner month view: a grid of weeks from Monday, each day's events stacked underneath each other with words",()=>{
    // 1400 wide gives each day 193 units, room for "Coast Stage Race · day 2 of 3" before the region is cut.
    var spec=PlanYear(s=>s with{Width=1400,Events=[..s.Events,new("e3","Club Ride",new(2027,3,13)){Region="ZA-GP",Relevance=PlannerRelevance.Near}]});
    var doc=PlanSvg(spec,PlannerView.Month(2027,3));
    var heads=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-muted lumen-weekday").Select(t=>t.Value).ToArray();
    Check(heads.SequenceEqual(["Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday"]),string.Join(",",heads));
    var day=doc.Descendants().Single(e=>(string?)e.Attribute("data-day")=="2027-03-13");
    var lines=day.Descendants().Where(e=>(string?)e.Attribute("class")=="lumen-datum").Select(e=>e.Attribute("aria-label")!.Value).ToArray();
    Check(lines.Length==3&&lines[0].StartsWith("Hilltop XCO")&&lines[1].StartsWith("Club Ride")&&lines[2].StartsWith("Coast Stage Race"),string.Join(" | ",lines));
    var drawn=day.Descendants(ns+"text").Select(t=>t.Value).ToArray();
    Check(drawn.Any(t=>t.Contains("clash"))&&drawn.Any(t=>t.Contains("close"))&&drawn.Any(t=>t.Contains("day 2 of 3")),string.Join(" | ",drawn));
});
Test("Planner month view: twelve events on a day show what fits and +N more naming the rest, inside the cell",()=>{
    var many=Enumerable.Range(0,12).Select(i=>new PlannerEvent($"m{i}",$"Invented ride {i}",new(2027,5,8))).ToArray();
    var doc=PlanSvg(PlanYear(s=>s with{Events=many}),PlannerView.Month(2027,5));
    var day=doc.Descendants().Single(e=>(string?)e.Attribute("data-day")=="2027-05-08");
    var shown=day.Descendants().Count(e=>(string?)e.Attribute("class")=="lumen-datum");
    var more=day.Descendants().Single(e=>((string?)e.Attribute("class")??"").Contains("lumen-more"));
    Check(shown is >=2 and <12&&more.Value==$"+{12-shown} more",$"{shown} shown, '{more.Value}'");
    var cellTop=double.Parse(day.Elements(ns+"rect").First().Attribute("y")!.Value,CultureInfo.InvariantCulture);
    var lowest=day.Descendants(ns+"text").Max(t=>double.Parse(t.Attribute("y")!.Value,CultureInfo.InvariantCulture));
    Check(lowest<=cellTop+104,$"text at {lowest} runs out of a cell starting at {cellTop}");
});
Test("Planner month view: a 60-character name is cut with … in its line and whole in its name, and holidays are written in their cell",()=>{
    var name=new string('L',60);
    var doc=PlanSvg(PlanYear(s=>s with{Events=[new("x",name,new(2027,4,27))]}),PlannerView.Month(2027,4));
    var day=doc.Descendants().Single(e=>(string?)e.Attribute("data-day")=="2027-04-27");
    Check(day.Descendants(ns+"text").Any(t=>t.Value=="Freedom Day"));
    var mark=day.Descendants().Single(e=>(string?)e.Attribute("class")=="lumen-datum");
    Check(mark.Attribute("aria-label")!.Value.StartsWith(name)&&mark.Descendants(ns+"text").Single().Value.EndsWith("…"));
});
Test("Planner month view: an event running from one month into the next is listed on its days in each, counted day by day",()=>{
    var spec=PlanYear(s=>s with{Width=1400,Events=[new("x","Tour",new(2027,3,30)){End=new DateOnly(2027,4,2)}]});
    var april=PlanSvg(spec,PlannerView.Month(2027,4));
    var first=april.Descendants().Single(e=>(string?)e.Attribute("data-day")=="2027-04-01");
    Check(first.Descendants(ns+"text").Any(t=>t.Value.Contains("day 3 of 4")),string.Join(" | ",first.Descendants(ns+"text").Select(t=>t.Value)));
    Check(april.Descendants().Where(e=>(string?)e.Attribute("data-event")=="x").Count()==2,"listed on 1 and 2 April");
    Check(PlanSvg(spec,PlannerView.Month(2027,3)).Descendants().Count(e=>(string?)e.Attribute("data-event")=="x")==2,"listed on 30 and 31 March");
});
Test("Planner month table: rows are weeks, columns weekdays, each cell its day's holidays and events in words",()=>{
    var html=PlannerSvg.Table(PlanYear(),2027,3);
    var doc=XDocument.Parse(html);
    Check(doc.Root!.Name.LocalName=="table"&&doc.Descendants("caption").Single().Value=="Season planner, March 2027");
    Check(doc.Descendants("th").Count(th=>(string?)th.Attribute("scope")=="col")==7);
    var cell=doc.Descendants("td").Single(td=>td.Value.StartsWith("13 "));
    Check(cell.Value.Contains("Hilltop XCO, Saturday 13 March 2027, Gauteng, XCO, Kids, clash"),cell.Value);
});
Test("Planner month view: every word clears 4.5:1 and every mark 3:1 against its cell (the grid colour in a weekend cell) in Light, Dark and Midnight, and the render is byte-stable",()=>{
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight}){
        // Saturday 20 March: a holiday, clashes (one cancelled, one yours) and more than fit. Sunday 21 March: close, provisional and
        // cancelled marks. Wednesday 17 March: a holiday and more than fit, on the plain background.
        var spec=PlanYear(s=>s with{Style=style,
            Periods=[..s.Periods,new(new(2027,3,20),null,"Invented Saturday",PeriodKind.PublicHoliday,"ZA"),new(new(2027,3,17),null,"Invented Wednesday",PeriodKind.Other)],
            Events=[..s.Events,
                new("s1","Clash Ride",new(2027,3,20)){Relevance=PlannerRelevance.Clash},
                new("s2","Called Off",new(2027,3,20)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Clash},
                new("s3","Our Enduro",new(2027,3,20)){Mine=true,Relevance=PlannerRelevance.Clash},
                new("s4","Hidden Ride",new(2027,3,20)),new("s5","Hidden Too",new(2027,3,20)),
                new("u1","Close Ride",new(2027,3,21)){Relevance=PlannerRelevance.Near},
                new("u2","Pencilled Close",new(2027,3,21)){Status=PlannerStatus.Provisional,Relevance=PlannerRelevance.Near},
                new("u3","Called Off Close",new(2027,3,21)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Near},
                new("u4","Pencilled Ride",new(2027,3,21)){Status=PlannerStatus.Provisional},
                ..Enumerable.Range(0,6).Select(i=>new PlannerEvent($"w{i}",$"Invented ride {i}",new(2027,3,17)))]});
        var svg=PlannerSvg.Render(spec,PlannerView.Month(2027,3));
        Check(svg==PlannerSvg.Render(spec,PlannerView.Month(2027,3)),"the render is not byte-stable");
        var doc=XDocument.Parse(svg);
        double A(XElement e,string name)=>double.Parse((string?)e.Attribute(name)??"0",CultureInfo.InvariantCulture);
        var cells=doc.Descendants(ns+"rect").Where(r=>(string?)r.Attribute("class")=="lumen-weekend")
            .Select(r=>(L:A(r,"x"),T:A(r,"y"),R:A(r,"x")+A(r,"width"),B:A(r,"y")+A(r,"height"))).ToArray();
        Check(cells.Length==8,$"{cells.Length} weekend cells in March 2027");
        // A word stands where its glyphs' middle does: its anchor, three pixels above its baseline.
        bool InWeekend(XElement t)=>cells.Any(b=>b.L<=A(t,"x")&&A(t,"x")<=b.R&&b.T<=A(t,"y")-3&&A(t,"y")-3<=b.B);
        string[] Classes(XElement e)=>((string?)e.Attribute("class")??"").Split(' ');
        var texts=doc.Descendants(ns+"text").ToArray();
        Check(texts.Any(t=>t.Value=="Invented Saturday"&&InWeekend(t)),"no holiday name in a weekend cell to check");
        Check(texts.Any(t=>Classes(t).Contains("lumen-more")&&InWeekend(t))&&texts.Any(t=>Classes(t).Contains("lumen-more")&&!InWeekend(t)),"no +N more in a weekend cell and in a weekday cell to check");
        Check(texts.Count(t=>(string?)t.Parent!.Attribute("class")=="lumen-datum"&&InWeekend(t))>=7,"too few event lines in weekend cells to check");
        foreach(var t in texts){
            var ink=(string?)t.Attribute("fill")??(Classes(t).Contains("lumen-muted")?style.Muted:style.Text);
            var behind=InWeekend(t)?style.Grid:style.Background;
            Check(Lumen.Charts.Contrast.Ratio(ink,behind)>=4.5,$"'{t.Value}' in {ink} on {behind}: {Lumen.Charts.Contrast.Ratio(ink,behind):0.00}");
        }
        // Every marker, band and symbol against both grounds; the cells' fills and borders are the grid colour, the ground itself.
        foreach(var mark in doc.Descendants().Where(e=>e.Name==ns+"rect"||e.Name==ns+"path"||e.Name==ns+"line")){
            Check(mark.Attribute("opacity") is null&&mark.Attribute("fill-opacity") is null&&mark.Attribute("stroke-opacity") is null,$"a faded mark: {mark}");
            foreach(var paint in new[]{(string?)mark.Attribute("fill"),(string?)mark.Attribute("stroke")}.OfType<string>().Where(p=>p!="none"&&p!=style.Grid))
                foreach(var behind in new[]{style.Background,style.Grid})
                    Check(Lumen.Charts.Contrast.Ratio(paint,behind)>=3,$"{mark.Name.LocalName} in {paint} on {behind}: {Lumen.Charts.Contrast.Ratio(paint,behind):0.00}");
        }
    }
});
Test("Planner day view: the day's holidays, then every event with its region's full name, category, audience, status and relevance in words",()=>{
    var spec=PlanYear(s=>s with{Events=[..s.Events,new("e4","Freedom Ride",new(2027,4,27)){Region="ZA-GP",Note="Invented charity ride",Mine=true}]});
    var doc=PlanSvg(spec,PlannerView.Day(new(2027,4,27)));
    var words=doc.Descendants(ns+"text").Select(t=>t.Value).ToArray();
    Check(words.Contains("Tuesday 27 April 2027")&&words.Any(w=>w=="Freedom Day (public holiday)"),string.Join(" | ",words));
    Check(words.Contains("Freedom Ride")&&words.Any(w=>w.StartsWith("Gauteng")&&w.Contains("yours"))&&words.Contains("Invented charity ride"),string.Join(" | ",words));
    Check(PlanMarks(doc).Single().Attribute("aria-label")!.Value.StartsWith("Freedom Ride, Tuesday 27 April 2027, Gauteng"));
});
Test("Planner day view: a day with nothing says so, and empty optional fields leave no stray separators",()=>{
    var doc=PlanSvg(PlanYear(s=>s with{Events=[new("b","Bare",new(2027,6,5))]}),PlannerView.Day(new(2027,6,6)));
    Check(doc.Descendants(ns+"text").Any(t=>t.Value=="No events"));
    var bare=PlanSvg(PlanYear(s=>s with{Events=[new("b","Bare",new(2027,6,5))]}),PlannerView.Day(new(2027,6,5)));
    Check(!bare.Descendants(ns+"text").Any(t=>t.Value.Contains(" ·  ")||t.Value.StartsWith(" · ")||t.Value.EndsWith(" · ")));
});
Test("Planner day view: the drawing is as tall as its content, whatever the description's lines and titles, and the date sits below them",()=>{
    var wordy=string.Join(" ",Enumerable.Repeat("Invented organizers' events across regions",4)); // 171 characters: a description is at most 200.
    foreach(var (description,titles) in new[]{("Short",true),(wordy,true),(wordy,false)}){
        var spec=PlanYear(s=>s with{Description=description,DrawTitles=titles,Periods=[..s.Periods,new(new(2027,4,27),null,"Invented Tuesday",PeriodKind.Other)],
            Events=[new("a","With note",new(2027,4,27)){Note="A note"},new("b","Without",new(2027,4,27)){Category="Road"},new("c","Two days",new(2027,4,26)){End=new DateOnly(2027,4,27)}]});
        var doc=PlanSvg(spec,PlannerView.Day(new(2027,4,27)));
        double Y(XElement t)=>double.Parse(t.Attribute("y")!.Value,CultureInfo.InvariantCulture);
        var height=double.Parse(doc.Root!.Attribute("viewBox")!.Value.Split(' ')[3],CultureInfo.InvariantCulture);
        var lowest=doc.Descendants(ns+"text").Max(Y);
        Check(lowest+6<=height&&lowest+30>=height,$"lowest word at {lowest} in a drawing {height} high (titles {titles}, description {description.Length} characters)");
        var date=doc.Descendants(ns+"text").Single(t=>t.Value=="Tuesday 27 April 2027");
        var above=doc.Root!.Elements(ns+"text").Select(Y).DefaultIfEmpty(0).Max();
        Check(Y(date)-11>above,$"the date at {Y(date)} under text at {above}");
    }
});
Test("Planner day view: a 60-character period name is cut with … to stay inside a 340-wide drawing, and whole in the day's name",()=>{
    var name=new string('P',60);
    var spec=PlanYear(s=>s with{Width=340,Periods=[..s.Periods,new(new(2027,6,5),null,name,PeriodKind.PublicHoliday,"ZA")]});
    var doc=PlanSvg(spec,PlannerView.Day(new(2027,6,5)));
    var line=doc.Descendants(ns+"text").Single(t=>t.Value.StartsWith("PPP"));
    Check(line.Value.EndsWith("…"),line.Value);
    Check(Lumen.Charts.ChartSvg.Wide(line.Value)<=340-48,$"'{line.Value}' is {Lumen.Charts.ChartSvg.Wide(line.Value):0} wide");
    Check(doc.Descendants().Single(e=>(string?)e.Attribute("class")=="lumen-day").Attribute("aria-label")!.Value.Contains($"{name} (public holiday)"));
});
Test("Planner day view: every word clears 4.5:1 and every marker 3:1 against the background in Light, Dark and Midnight, and the render is byte-stable",()=>{
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight}){
        var spec=PlanYear(s=>s with{Style=style,
            Periods=[..s.Periods,new(new(2027,4,27),null,"Invented Tuesday",PeriodKind.Other),new(new(2027,4,26),new DateOnly(2027,4,28),"Invented break",PeriodKind.SchoolHoliday,"ZA")],
            Events=[
                new("a","Clash Ride",new(2027,4,27)){Relevance=PlannerRelevance.Clash,Region="ZA-GP",Category="XCO",Audience="Kids",Note="Invented note"},
                new("b","Close Ride",new(2027,4,27)){Relevance=PlannerRelevance.Near},
                new("c","Other Ride",new(2027,4,27)),
                new("d","Called Off",new(2027,4,27)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Clash},
                new("e","Called Off Other",new(2027,4,27)){Status=PlannerStatus.Cancelled},
                new("f","Pencilled Close",new(2027,4,27)){Status=PlannerStatus.Provisional,Relevance=PlannerRelevance.Near},
                new("g","Pencilled Other",new(2027,4,27)){Status=PlannerStatus.Provisional},
                new("h","Our Enduro",new(2027,4,26)){End=new DateOnly(2027,4,28),Mine=true,Relevance=PlannerRelevance.Clash},
                new("i","Our Quiet Ride",new(2027,4,27)){Mine=true}]});
        var svg=PlannerSvg.Render(spec,PlannerView.Day(new(2027,4,27)));
        Check(svg==PlannerSvg.Render(spec,PlannerView.Day(new(2027,4,27))),"the render is not byte-stable");
        var doc=XDocument.Parse(svg);
        var texts=doc.Descendants(ns+"text").ToArray();
        Check(texts.Count(t=>((string?)t.Attribute("class")??"").Split(' ').Contains("lumen-muted"))>=9,"too few muted lines to check");
        foreach(var t in texts){
            var ink=(string?)t.Attribute("fill")??(((string?)t.Attribute("class")??"").Split(' ').Contains("lumen-muted")?style.Muted:style.Text);
            Check(Lumen.Charts.Contrast.Ratio(ink,style.Background)>=4.5,$"'{t.Value}' in {ink} on {style.Background}: {Lumen.Charts.Contrast.Ratio(ink,style.Background):0.00}");
        }
        var markers=doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("class")=="lumen-datum").SelectMany(g=>g.Elements()).Where(e=>e.Name==ns+"rect"||e.Name==ns+"line").ToArray();
        Check(markers.Length>=14,$"{markers.Length} marker shapes to check");
        foreach(var mark in doc.Descendants().Where(e=>e.Name==ns+"rect"||e.Name==ns+"path"||e.Name==ns+"line")){
            Check(mark.Attribute("opacity") is null&&mark.Attribute("fill-opacity") is null&&mark.Attribute("stroke-opacity") is null,$"a faded mark: {mark}");
            foreach(var paint in new[]{(string?)mark.Attribute("fill"),(string?)mark.Attribute("stroke")}.OfType<string>().Where(p=>p!="none"))
                Check(Lumen.Charts.Contrast.Ratio(paint,style.Background)>=3,$"{mark.Name.LocalName} in {paint} on {style.Background}: {Lumen.Charts.Contrast.Ratio(paint,style.Background):0.00}");
        }
    }
});
Test("Planner narrow year: twelve month bars, each weekend named with its clashes, and every word inside a 340-wide drawing",()=>{
    var spec=PlanYear(s=>s with{Width=340});
    var doc=PlanSvg(spec,PlannerView.WholePeriod,PlannerLayout.Narrow);
    Check(doc.Descendants(ns+"text").Count(t=>(string?)t.Attribute("class")=="lumen-month")==12);
    var weekend=doc.Descendants().Single(e=>(string?)e.Attribute("data-weekend")=="2027-03-13");
    Check(weekend.Attribute("aria-label")!.Value=="Weekend of 13 March 2027: 1 clash",weekend.Attribute("aria-label")!.Value);
    foreach(var t in doc.Descendants(ns+"text")){
        var x=double.Parse(t.Attribute("x")!.Value,CultureInfo.InvariantCulture);
        var anchor=(string?)t.Attribute("text-anchor");
        var size=double.Parse((string?)t.Attribute("font-size")??"12",CultureInfo.InvariantCulture);
        var wide=ChartSvg.Wide(t.Value)*size/11;
        var (l,r)=anchor=="end"?(x-wide,x):anchor=="middle"?(x-wide/2,x+wide/2):(x,x+wide);
        Check(l>=0&&r<=340,$"'{t.Value}' runs from {l:0} to {r:0}");
    }
});
Test("Planner narrow month: an agenda of only the days that hold something, events listed under their day",()=>{
    var doc=PlanSvg(PlanYear(s=>s with{Width=340}),PlannerView.Month(2027,3),PlannerLayout.Narrow);
    var heads=doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("class")=="lumen-agenda-day").Select(t=>t.Value).ToArray();
    Check(heads.SequenceEqual(["Friday 12 March","Saturday 13 March","Sunday 14 March","Saturday 27 March","Sunday 28 March","Monday 29 March","Tuesday 30 March","Wednesday 31 March"]),string.Join(",",heads));
    Check(PlanMarks(doc).Count(m=>m.Attribute("data-event")!.Value=="e2")==3,"the stage race is not listed on each of its days");
    var empty=PlanSvg(PlanYear(s=>s with{Width=340,Events=[],Periods=[]}),PlannerView.Month(2027,6),PlannerLayout.Narrow);
    Check(empty.Descendants(ns+"text").Any(t=>t.Value=="Nothing scheduled"));
});
// Every word's estimated extent, by its anchor, lies inside a drawing this wide.
void PlanInside(XDocument doc,double width){
    foreach(var t in doc.Descendants(ns+"text")){
        var x=double.Parse(t.Attribute("x")!.Value,CultureInfo.InvariantCulture);
        var anchor=(string?)t.Attribute("text-anchor");
        var size=double.Parse((string?)t.Attribute("font-size")??"12",CultureInfo.InvariantCulture);
        var wide=ChartSvg.Wide(t.Value)*size/11;
        var (l,r)=anchor=="end"?(x-wide,x):anchor=="middle"?(x-wide/2,x+wide/2):(x,x+wide);
        Check(l>=0&&r<=width,$"'{t.Value}' runs from {l:0} to {r:0} in a drawing {width} wide");
    }
}
Test("Planner narrow layouts: at 320 wide, with long names, holidays and a long description, every word of the year and the agenda stays inside",()=>{
    var wordy=string.Join(" ",Enumerable.Repeat("Invented organizers' events across regions",4)); // 171 characters: a description is at most 200.
    var spec=PlanYear(s=>s with{Width=320,Description=wordy,
        Periods=[..s.Periods,new(new(2027,9,25),new DateOnly(2027,9,26),new string('P',60),PeriodKind.PublicHoliday,"ZA")],
        Events=[..s.Events,
            new("n1","Club Ride",new(2027,3,13)){Region="ZA-GP",Relevance=PlannerRelevance.Near},
            new("l1",new string('L',60),new(2027,9,29)){Region="ZA-WC",Relevance=PlannerRelevance.Clash},
            ..Enumerable.Range(0,4).Select(i=>new PlannerEvent($"c{i}",$"Invented clash {i}",new(2027,9,4+7*i)){Relevance=PlannerRelevance.Clash}),
            ..Enumerable.Range(0,3).Select(i=>new PlannerEvent($"r{i}",$"Invented close {i}",new(2027,9,5+7*i)){Relevance=PlannerRelevance.Near})]});
    var year=PlanSvg(spec,PlannerView.WholePeriod,PlannerLayout.Narrow);
    PlanInside(year,320);
    // March holds one clash and one close event; September five clashes (four on its weekends, the 60-L one on a Wednesday) and three close.
    var summaries=year.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="end").Select(t=>t.Value).ToArray();
    Check(summaries.Contains("1 clash · 1 close")&&summaries.Contains("5 clashes · 3 close"),string.Join(" | ",summaries));
    Check(year.Descendants().Single(e=>(string?)e.Attribute("data-weekend")=="2027-09-25").Attribute("aria-label")!.Value=="Weekend of 25 September 2027: 1 clash, "+new string('P',60),"the holiday is not named in its weekend");
    Check(year.Descendants().Where(e=>(string?)e.Attribute("class")=="lumen-week").All(g=>g.Attribute("tabindex") is null),"a static weekend is focusable");
    foreach(var month in new[]{3,9}) PlanInside(PlanSvg(spec,PlannerView.Month(2027,month),PlannerLayout.Narrow),320);
    var september=PlanSvg(spec,PlannerView.Month(2027,9),PlannerLayout.Narrow);
    Check(september.Descendants(ns+"text").Any(t=>t.Value.StartsWith("LLL")&&t.Value.EndsWith("…")),"the long name is not cut");
});
Test("Planner: every named group, a day's or a week's as well as an event's, has a role that permits its name",()=>{
    var spec=PlanYear(s=>s);
    foreach(var (view,layout) in new[]{(PlannerView.WholePeriod,PlannerLayout.Wide),(PlannerView.Month(2027,3),PlannerLayout.Wide),(PlannerView.WholePeriod,PlannerLayout.Narrow),(PlannerView.Month(2027,3),PlannerLayout.Narrow),(PlannerView.Day(new DateOnly(2027,3,13)),PlannerLayout.Wide)})
    {
        var bare=PlanSvg(spec,view,layout).Descendants(ns+"g").Where(g=>g.Attribute("aria-label") is not null&&g.Attribute("role") is null).ToArray();
        Check(bare.Length==0,$"{view.Zoom} {layout}: {bare.Length} named groups without a role, the first {(string?)bare.FirstOrDefault()?.Attribute("class")}");
    }
});
Test("Planner: a day too full to draw whole writes \"+N\" with a role that permits its name, in the year and the month",()=>{
    // Five events on one day: the year draws three lanes and "+2", the month three lines and "+2 more".
    var spec=PlanYear(s=>s with{Events=[..s.Events,..Enumerable.Range(0,5).Select(i=>new PlannerEvent($"f{i}",$"Invented full {i}",new(2027,3,10)))]});
    foreach(var view in new[]{PlannerView.WholePeriod,PlannerView.Month(2027,3)})
    {
        var doc=PlanSvg(spec,view,PlannerLayout.Wide);
        Check(doc.Descendants(ns+"text").Any(t=>((string?)t.Attribute("class")??"").Split(' ').Contains("lumen-more")),$"{view.Zoom}: no +N to check");
        var bare=doc.Descendants().Where(e=>e.Attribute("aria-label") is not null&&e.Attribute("role") is null).ToArray();
        Check(bare.Length==0,$"{view.Zoom}: {bare.Length} named elements without a role, the first {bare.FirstOrDefault()?.Name.LocalName} {(string?)bare.FirstOrDefault()?.Attribute("class")}");
    }
    // The month's "+N more" keeps its tooltip on the group around it.
    var more=PlanSvg(spec,PlannerView.Month(2027,3),PlannerLayout.Wide).Descendants(ns+"text").Single(t=>((string?)t.Attribute("class")??"").Contains("lumen-more"));
    Check((string?)more.Attribute("role")=="img"&&(string?)more.Parent!.Attribute("role")=="presentation"&&more.Parent.Element(ns+"title")?.Value==(string?)more.Attribute("aria-label"),"the tooltip left the +N more");
});
Test("Planner narrow layouts: each drawing is as tall as its content, whatever the description's lines and titles",()=>{
    var wordy=string.Join(" ",Enumerable.Repeat("Invented organizers' events across regions",4)); // 171 characters: a description is at most 200.
    foreach(var (description,titles) in new[]{("Short",true),(wordy,true),(wordy,false)}){
        var spec=PlanYear(s=>s with{Width=340,Description=description,DrawTitles=titles});
        foreach(var (doc,first) in new[]{(PlanSvg(spec,PlannerView.WholePeriod,PlannerLayout.Narrow),"Jan 2027"),(PlanSvg(spec,PlannerView.Month(2027,3),PlannerLayout.Narrow),"Friday 12 March")}){
            double Y(XElement t)=>double.Parse(t.Attribute("y")!.Value,CultureInfo.InvariantCulture);
            var height=double.Parse(doc.Root!.Attribute("viewBox")!.Value.Split(' ')[3],CultureInfo.InvariantCulture);
            var lowest=doc.Descendants(ns+"text").Max(Y);
            Check(lowest+6<=height&&lowest+36>=height,$"lowest word at {lowest} in a drawing {height} high ({first}, titles {titles}, description {description.Length} characters)");
            var top=doc.Descendants(ns+"text").First(t=>t.Value==first);
            // The title and description are the drawing's own words written before the first of its body.
            var above=doc.Root!.Elements(ns+"text").Where(t=>t.IsBefore(top)).Select(Y).DefaultIfEmpty(0).Max();
            Check(Y(top)-11>above,$"'{first}' at {Y(top)} under text at {above}");
        }
    }
});
Test("Planner narrow layouts: every word clears 4.5:1 and every mark 3:1 against what lies behind it in Light, Dark and Midnight, and the render is byte-stable",()=>{
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight}){
        // A public holiday on a weekend (its diamond on the weekend's grid colour), clashes, close, provisional, cancelled and own events.
        var spec=PlanYear(s=>s with{Width=340,Style=style,
            Periods=[..s.Periods,new(new(2027,3,20),null,"Invented Saturday",PeriodKind.PublicHoliday,"ZA")],
            Events=[..s.Events,
                new("s1","Clash Ride",new(2027,3,20)){Relevance=PlannerRelevance.Clash},
                new("s2","Called Off",new(2027,3,20)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Clash},
                new("s3","Our Enduro",new(2027,3,20)){Mine=true,Relevance=PlannerRelevance.Clash},
                new("u1","Close Ride",new(2027,3,21)){Relevance=PlannerRelevance.Near},
                new("u2","Pencilled Close",new(2027,3,6)){Status=PlannerStatus.Provisional,Relevance=PlannerRelevance.Near},
                new("u3","Called Off Close",new(2027,3,21)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Near},
                new("u4","Pencilled Ride",new(2027,3,21)){Status=PlannerStatus.Provisional}]});
        foreach(var view in new[]{PlannerView.WholePeriod,PlannerView.Month(2027,3)}){
            var svg=PlannerSvg.Render(spec,view,PlannerLayout.Narrow);
            Check(svg==PlannerSvg.Render(spec,view,PlannerLayout.Narrow),"the render is not byte-stable");
            var doc=XDocument.Parse(svg);
            double A(XElement e,string name)=>double.Parse((string?)e.Attribute(name)??"0",CultureInfo.InvariantCulture);
            // Every grid-coloured rect is a ground: a weekend slot, or the legend's weekend sample.
            var grounds=doc.Descendants(ns+"rect").Where(r=>(string?)r.Attribute("fill")==style.Grid)
                .Select(r=>(L:A(r,"x"),T:A(r,"y"),R:A(r,"x")+A(r,"width"),B:A(r,"y")+A(r,"height"))).ToArray();
            if(view.Zoom==PlannerZoom.Year){
                Check(grounds.Length>=52,$"{grounds.Length} grid grounds");
                Check(doc.Descendants().Single(e=>(string?)e.Attribute("data-weekend")=="2027-03-20").Elements(ns+"path").Any(),"no holiday diamond on a weekend to check");
            }
            // A word's extent, from its anchor and estimated width, at its glyphs' middle three pixels above its baseline.
            bool OnGround(XElement t){
                var size=double.Parse((string?)t.Attribute("font-size")??"12",CultureInfo.InvariantCulture);
                var wide=ChartSvg.Wide(t.Value)*size/11;var x=A(t,"x");var anchor=(string?)t.Attribute("text-anchor");
                var (l,r)=anchor=="end"?(x-wide,x):anchor=="middle"?(x-wide/2,x+wide/2):(x,x+wide);
                return grounds.Any(b=>l<=b.R&&b.L<=r&&b.T<=A(t,"y")-3&&A(t,"y")-3<=b.B);
            }
            foreach(var t in doc.Descendants(ns+"text")){
                var ink=(string?)t.Attribute("fill")??(((string?)t.Attribute("class")??"").Split(' ').Contains("lumen-muted")?style.Muted:style.Text);
                var behind=OnGround(t)?style.Grid:style.Background;
                Check(Lumen.Charts.Contrast.Ratio(ink,behind)>=4.5,$"'{t.Value}' in {ink} on {behind}: {Lumen.Charts.Contrast.Ratio(ink,behind):0.00}");
            }
            foreach(var mark in doc.Descendants().Where(e=>e.Name==ns+"rect"||e.Name==ns+"path"||e.Name==ns+"line")){
                Check(mark.Attribute("opacity") is null&&mark.Attribute("fill-opacity") is null&&mark.Attribute("stroke-opacity") is null,$"a faded mark: {mark}");
                var fill=(string?)mark.Attribute("fill");var stroke=(string?)mark.Attribute("stroke");
                // A grid-coloured weekend slot is seen by its outline, checked below like any mark; only the legend's weekend sample is
                // a bare swatch of the grid colour.
                var legend=mark.Ancestors().Any(a=>(string?)a.Attribute("class")=="lumen-legend");
                if(fill==style.Grid)Check(legend||stroke is not null&&stroke!="none",$"a grid-coloured shape with no outline: {mark}");
                foreach(var paint in new[]{fill==style.Grid?null:fill,stroke}.OfType<string>().Where(p=>p!="none"))
                    foreach(var behind in new[]{style.Background,style.Grid})
                        Check(Lumen.Charts.Contrast.Ratio(paint,behind)>=3,$"{mark.Name.LocalName} in {paint} on {behind}: {Lumen.Charts.Contrast.Ratio(paint,behind):0.00}");
            }
        }
    }
});
string[] PlanSums(XDocument doc)=>doc.Descendants(ns+"text").Where(t=>(string?)t.Attribute("text-anchor")=="end").Select(t=>t.Value).ToArray();
XElement[] PlanWeekends(XDocument doc,string start)=>doc.Descendants().Where(e=>(string?)e.Attribute("data-weekend")==start).ToArray();
Test("Planner narrow year: a month's counts take each of its clash and close events once, weekdays included, and a slot marks only its own weekend",()=>{
    // 12 May 2027 is a Wednesday.
    var mid=PlanSvg(PlanYear(s=>s with{Width=340,Events=[new("w","Midweek Clash",new(2027,5,12)){Relevance=PlannerRelevance.Clash}]}),PlannerView.WholePeriod,PlannerLayout.Narrow);
    Check(PlanSums(mid).SequenceEqual(["1 clash"]),string.Join(" | ",PlanSums(mid)));
    var may=mid.Descendants().Where(e=>((string?)e.Attribute("data-weekend")??"").StartsWith("2027-05")).ToArray();
    Check(may.Length>0&&may.All(g=>g.Attribute("aria-label")!.Value.EndsWith(": nothing")&&g.Elements(ns+"rect").Count()==1),string.Join(" | ",may.Select(g=>g.Attribute("aria-label")!.Value)));
    // Saturday 5 to Sunday 13 June: one race over two weekends.
    var stage=PlanSvg(PlanYear(s=>s with{Width=340,Events=[new("r","Two Weekend Race",new(2027,6,5)){End=new DateOnly(2027,6,13),Relevance=PlannerRelevance.Clash}]}),PlannerView.WholePeriod,PlannerLayout.Narrow);
    Check(PlanSums(stage).SequenceEqual(["1 clash"]),string.Join(" | ",PlanSums(stage)));
    foreach(var (start,name) in new[]{("2027-06-05","5 June 2027"),("2027-06-12","12 June 2027")})
        Check(PlanWeekends(stage,start).Single().Attribute("aria-label")!.Value==$"Weekend of {name}: 1 clash",PlanWeekends(stage,start).Single().Attribute("aria-label")!.Value);
});
Test("Planner: a cancelled event counts as neither clash nor close in the narrow year or the wide year's weeks, and is listed as cancelled",()=>{
    // Saturday 19 and Sunday 20 June 2027.
    var spec=PlanYear(s=>s with{Width=340,Events=[new("x","Called Off",new(2027,6,19)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Clash},
        new("y","Called Off Close",new(2027,6,20)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Near}]});
    var narrow=PlanSvg(spec,PlannerView.WholePeriod,PlannerLayout.Narrow);
    var slot=PlanWeekends(narrow,"2027-06-19").Single();
    Check(slot.Attribute("aria-label")!.Value=="Weekend of 19 June 2027: nothing"&&slot.Elements(ns+"rect").Count()==1,slot.ToString());
    Check(PlanSums(narrow).Length==0,string.Join(" | ",PlanSums(narrow)));
    var wide=PlanSvg(spec with{Width=1100});
    var week=wide.Descendants().Single(e=>(string?)e.Attribute("class")=="lumen-week"&&e.Attribute("aria-label")!.Value.StartsWith("Week of 14 June 2027"));
    Check(week.Attribute("aria-label")!.Value=="Week of 14 June 2027: no clashes"&&!week.Descendants(ns+"text").Any(),week.ToString());
    var agenda=PlanSvg(spec,PlannerView.Month(2027,6),PlannerLayout.Narrow);
    var lines=PlanMarks(agenda).Select(m=>m.Descendants(ns+"text").Single().Value).ToArray();
    Check(lines.SequenceEqual(["Called Off · cancelled","Called Off Close · cancelled"]),string.Join(" | ",lines));
});
Test("Planner narrow year: a weekend across two months shows in both bars, each slot covering and naming the whole weekend",()=>{
    // Saturday 31 July and Sunday 1 August 2027.
    var doc=PlanSvg(PlanYear(s=>s with{Width=340,Events=[new("j","Month End Clash",new(2027,7,31)){Relevance=PlannerRelevance.Clash}]}),PlannerView.WholePeriod,PlannerLayout.Narrow);
    var slots=PlanWeekends(doc,"2027-07-31");
    Check(slots.Length==2&&slots.All(g=>g.Attribute("aria-label")!.Value=="Weekend of 31 July to 1 August 2027: 1 clash"),string.Join(" | ",slots.Select(g=>g.Attribute("aria-label")!.Value)));
    Check(slots.All(g=>g.Elements(ns+"rect").Any(r=>r.Attribute("height")!.Value=="4")),"a slot lacks the weekend's clash mark");
    Check(slots.Select(g=>g.Elements(ns+"rect").First().Attribute("y")!.Value).Distinct().Count()==2,"both slots stand in one month's bar");
    Check(PlanWeekends(doc,"2027-08-01").Length==0,"1 August is a weekend of its own");
});
Test("Planner narrow year: a Friday-to-Sunday weekend is one slot, named from its Friday",()=>{
    var doc=PlanSvg(PlanYear(s=>s with{Width=340,Weekend=[DayOfWeek.Friday,DayOfWeek.Saturday,DayOfWeek.Sunday],Events=[new("f","Sunday Clash",new(2027,3,14)){Relevance=PlannerRelevance.Clash}]}),PlannerView.WholePeriod,PlannerLayout.Narrow);
    var march=doc.Descendants().Select(e=>(string?)e.Attribute("data-weekend")).OfType<string>().Where(d=>d.StartsWith("2027-03")).ToArray();
    Check(march.SequenceEqual(["2027-03-05","2027-03-12","2027-03-19","2027-03-26"]),string.Join(",",march));
    Check(PlanWeekends(doc,"2027-03-12").Single().Attribute("aria-label")!.Value=="Weekend of 12 March 2027: 1 clash",PlanWeekends(doc,"2027-03-12").Single().Attribute("aria-label")!.Value);
});
Test("Planner: a spec round-trips through the HTTP API's JSON and draws the same SVG",()=>{
    var json=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    json.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    var request=new PlannerRequest(PlanYear(),PlannerView.Month(2027,3));
    var back=System.Text.Json.JsonSerializer.Deserialize<PlannerRequest>(System.Text.Json.JsonSerializer.Serialize(request,json),json)!;
    Check(PlannerSvg.Render(back.Spec,back.View!.Value,back.Layout)==PlannerSvg.Render(request.Spec,request.View!.Value));
    var text=System.Text.Json.JsonSerializer.Serialize(request,json);
    Check(text.Contains("\"zoom\":\"Month\"")&&text.Contains("\"relevance\":\"Clash\""),text[..200]);
});
// ---- 0.43.0 final review: refusals, scale, culture ----
Test("Planner: refuses a missing list or a missing element in it, as JSON's null gives, with ArgumentException",()=>{
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Weekend=null!})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Regions=null!})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Periods=null!})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=null!})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Description=null!})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Regions=[..s.Regions,null!]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Periods=[..s.Periods,null!]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=[..s.Events,null!]})));
    foreach(var filter in new PlannerFilter[]{new(){Regions=null!},new(){Categories=null!},new(){Audiences=null!},new(){Statuses=null!},new(){Relevances=null!},
        new(){Regions=[null!]},new(){Categories=["XCO",null!]},new(){Audiences=[null!]}})
        Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Filter=filter})));
    Reject(()=>PlannerSvg.Render(PlanYear(s=>s with{Events=null!}),PlannerView.WholePeriod));
});
Test("Planner: takes at most 2000 events, 1000 periods, 500 regions and 200 characters in a title, description or name",()=>{
    PlannerEvent Ride(int i)=>new($"r{i}",$"Invented ride {i}",new DateOnly(2027,1,1).AddDays(i%365));
    PlannerValidation.Validate(PlanYear(s=>s with{Events=Enumerable.Range(0,2000).Select(Ride).ToArray()}));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=Enumerable.Range(0,2001).Select(Ride).ToArray()})));
    PlannerPeriod Break(int i)=>new(new DateOnly(2027,1,1).AddDays(i%365),null,$"Invented day {i}",PeriodKind.Other);
    PlannerValidation.Validate(PlanYear(s=>s with{Periods=Enumerable.Range(0,1000).Select(Break).ToArray()}));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Periods=Enumerable.Range(0,1001).Select(Break).ToArray()})));
    PlannerRegion Place(int i)=>new($"P{i}",$"Invented place {i}");
    PlannerValidation.Validate(PlanYear(s=>s with{Regions=[..s.Regions,..Enumerable.Range(0,497).Select(Place)]}));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Regions=[..s.Regions,..Enumerable.Range(0,498).Select(Place)]})));
    var fits=new string('n',200);var over=new string('n',201);
    PlannerValidation.Validate(PlanYear(s=>s with{Title=fits,Description=fits,Regions=[..s.Regions,new("X",fits)],Periods=[new(new(2027,5,1),null,fits,PeriodKind.Other)],Events=[new("x",fits,new(2027,5,1))]}));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Title=over})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Description=over})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Regions=[..s.Regions,new("X",over)]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Periods=[new(new(2027,5,1),null,over,PeriodKind.Other)]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=[new("x",over,new(2027,5,1))]})));
});
Test("Planner: refuses an undefined zoom, layout, week start, weekend day, period kind, status or relevance",()=>{
    Reject(()=>PlannerValidation.Validate(PlanYear(),new PlannerView((PlannerZoom)7,new(2027,3,1))));
    Reject(()=>PlannerSvg.Render(PlanYear(),PlannerView.WholePeriod,(PlannerLayout)2));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{WeekStart=(DayOfWeek)7})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Weekend=[DayOfWeek.Saturday,(DayOfWeek)9]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Periods=[new(new(2027,5,1),null,"Odd",(PeriodKind)5)]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=[new("x","Odd",new(2027,5,1)){Status=(PlannerStatus)3}]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Events=[new("x","Odd",new(2027,5,1)){Relevance=(PlannerRelevance)3}]})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Filter=new(){Statuses=[(PlannerStatus)4]}})));
    Reject(()=>PlannerValidation.Validate(PlanYear(s=>s with{Filter=new(){Relevances=[(PlannerRelevance)4]}})));
});
Test("Planner: a year of 2000 events over 60 regions filtered to two draws in well under a second, and a month of 200-character names quickly",()=>{
    var regions=new List<PlannerRegion>{new("ZA","South Africa")};
    for(var i=0;i<59;i++)regions.Add(new($"R{i}",$"Invented region {i}",i<9?"ZA":$"R{i%9}"));
    var events=Enumerable.Range(0,2000).Select(i=>new PlannerEvent($"e{i}",$"Invented event {i}",new DateOnly(2027,1,1).AddDays(i*7%365)){
        End=i%5==0?new DateOnly(2027,1,1).AddDays(i*7%365+2):null,Region=regions[i%60].Code,Relevance=(PlannerRelevance)(i%3),Status=(PlannerStatus)(i%3/2)}).ToArray();
    var periods=Enumerable.Range(0,300).Select(i=>new PlannerPeriod(new DateOnly(2027,1,1).AddDays(i%360),i%4==0?new DateOnly(2027,1,1).AddDays(i%360+4):null,$"Invented period {i}",(PeriodKind)(i%3),regions[i%60].Code)).ToArray();
    var spec=PlanYear(s=>s with{Regions=regions,Periods=periods,Events=events,Filter=new(){Regions=["R3","R40"]}});
    PlannerSvg.Render(PlanYear(),PlannerView.WholePeriod);
    var timer=Stopwatch.StartNew();
    var year=PlannerSvg.Render(spec,PlannerView.WholePeriod);
    timer.Stop();
    Console.WriteLine($"     year of 2000 events, 60 regions, 2-region filter: {timer.ElapsedMilliseconds} ms");
    Check(timer.Elapsed.TotalSeconds<1.5,$"{timer.ElapsedMilliseconds} ms");
    Check(PlanMarks(XDocument.Parse(year)).Any(),"nothing drawn");
    timer.Restart();
    foreach(var (view,layout) in new[]{(PlannerView.Month(2027,3),PlannerLayout.Wide),(PlannerView.WholePeriod,PlannerLayout.Narrow),(PlannerView.Month(2027,3),PlannerLayout.Narrow),(PlannerView.Day(new(2027,3,13)),PlannerLayout.Wide)})
        PlannerSvg.Render(spec with{Filter=null},view,layout);
    timer.Stop();
    Console.WriteLine($"     month, narrow year, agenda and day of 2000 unfiltered events: {timer.ElapsedMilliseconds} ms");
    Check(timer.Elapsed.TotalSeconds<1.5,$"{timer.ElapsedMilliseconds} ms");
    var long200=Enumerable.Range(0,40).Select(i=>new PlannerEvent($"l{i}",new string((char)('A'+i%26),200),new(2027,3,1+i%31)){Note=new string('n',120)}).ToArray();
    var wordy=PlanYear(s=>s with{Title=new string('T',200),Description=new string('D',200),Width=4096,Events=long200});
    timer.Restart();
    var month=PlannerSvg.Render(wordy,PlannerView.Month(2027,3));
    PlannerSvg.Render(wordy with{Width=320},PlannerView.Month(2027,3),PlannerLayout.Narrow);
    PlannerSvg.Render(wordy with{Width=320},PlannerView.Day(new(2027,3,1)));
    timer.Stop();
    Console.WriteLine($"     month, agenda and day of 200-character names: {timer.ElapsedMilliseconds} ms");
    Check(timer.Elapsed.TotalSeconds<0.5,$"{timer.ElapsedMilliseconds} ms");
    Check(XDocument.Parse(month).Descendants(ns+"text").Any(t=>t.Value.StartsWith("AAAA")&&t.Value.EndsWith("…")),"no long name cut");
});
Test("Planner: a cut never parts the two halves of a character outside the Basic Multilingual Plane",()=>{
    foreach(var lead in new[]{"","a"})
    foreach(var width in new[]{320,333,347}){
        var name=lead+string.Concat(Enumerable.Repeat("\U0001F6B4",60));
        var svg=PlannerSvg.Render(PlanYear(s=>s with{Width=width,Events=[new("x",name,new(2027,3,3))]}),PlannerView.Month(2027,3),PlannerLayout.Narrow);
        var line=PlanMarks(XDocument.Parse(svg)).Single().Element(ns+"text")!.Value;
        Check(line.EndsWith("…")&&!line.Contains('�'),line);
        for(var i=0;i<line.Length;i++)
            Check(!char.IsSurrogate(line[i])||char.IsHighSurrogate(line[i])&&i+1<line.Length&&char.IsLowSurrogate(line[i+1])||char.IsLowSurrogate(line[i])&&i>0&&char.IsHighSurrogate(line[i-1]),$"a lone half at {i} in '{line}'");
    }
});
Test("Planner: renders byte for byte the same under Thai, Arabic, Persian, French and Swedish cultures as under the invariant one",()=>{
    var spec=PlanYear(s=>s with{Periods=[..s.Periods,new(new(2027,5,10),new DateOnly(2027,5,14),"Invented exams",PeriodKind.Other,"ZA")],
        Events=[..s.Events,new("n","Invented ride",new(2027,3,10)){End=new DateOnly(2027,3,11),Relevance=PlannerRelevance.Near,Note="Invented note 1,5 km"}]});
    var views=new[]{(PlannerView.WholePeriod,PlannerLayout.Wide,1100),(PlannerView.Month(2027,3),PlannerLayout.Wide,1100),(PlannerView.Day(new(2027,3,13)),PlannerLayout.Wide,1100),
        (PlannerView.WholePeriod,PlannerLayout.Narrow,340),(PlannerView.Month(2027,3),PlannerLayout.Narrow,340)};
    string[] All()=>views.Select(v=>PlannerSvg.Render(spec with{Width=v.Item3},v.Item1,v.Item2)).Append(PlannerSvg.Table(spec,2027,3)).ToArray();
    string Refusal(){try{PlannerValidation.Validate(spec,PlannerView.Day(new(2028,1,5)));}catch(ArgumentException x){return x.Message;}return "";}
    var (culture,ui)=(CultureInfo.CurrentCulture,CultureInfo.CurrentUICulture);
    try{
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.InvariantCulture;
        var expected=All();var message=Refusal();
        Check(message.Contains("2028-01-05"),message);
        foreach(var name in new[]{"th-TH","ar-SA","fa-IR","fr-FR","sv-SE"}){
            CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=new CultureInfo(name);
            var drawn=All();
            for(var i=0;i<drawn.Length;i++)Check(drawn[i]==expected[i],$"{name}: drawing {i} differs");
            Check(Refusal()==message,$"{name}: '{Refusal()}'");
        }
    }finally{CultureInfo.CurrentCulture=culture;CultureInfo.CurrentUICulture=ui;}
});
Test("Planner month view: a cancelled clash or close event says \"cancelled\" on its line, not \"clash\" or \"close\", as the agenda does",()=>{
    var spec=PlanYear(s=>s with{Width=1400,Events=[new("x","Called Off",new(2027,6,19)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Clash},
        new("y","Called Off Close",new(2027,6,20)){Status=PlannerStatus.Cancelled,Relevance=PlannerRelevance.Near},new("z","Still On",new(2027,6,20)){Relevance=PlannerRelevance.Clash}]});
    var lines=PlanMarks(PlanSvg(spec,PlannerView.Month(2027,6))).Select(m=>m.Descendants(ns+"text").Single().Value).ToArray();
    Check(lines.SequenceEqual(["Called Off · cancelled","Still On · clash","Called Off Close · cancelled"]),string.Join(" | ",lines));
});
Test("Planner: a focused line keeps its words unstroked and is ringed, in its own scoped style, while charts keep theirs",()=>{
    var spec=PlanYear(s=>s with{Events=[..s.Events,new("n","Freedom Ride",new(2027,3,13)){Note="Invented note"}]});
    foreach(var (view,layout,ringed) in new[]{(PlannerView.WholePeriod,PlannerLayout.Wide,false),(PlannerView.Month(2027,3),PlannerLayout.Wide,true),(PlannerView.Month(2027,3),PlannerLayout.Narrow,true),(PlannerView.Day(new(2027,3,13)),PlannerLayout.Wide,true),(PlannerView.WholePeriod,PlannerLayout.Narrow,false)}){
        var doc=PlanSvg(spec,view,layout);
        Check(((string?)doc.Root!.Attribute("class")??"").Split(' ').SequenceEqual(["lumen-svg","lumen-planner"]),$"{view.Zoom} {layout}: root class {(string?)doc.Root.Attribute("class")}");
        var style=string.Concat(doc.Root.Elements(ns+"style").Select(s=>s.Value));
        Check(style.Contains(".lumen-planner .lumen-datum:focus text{stroke:none}")&&style.Contains(".lumen-planner .lumen-datum:focus .lumen-focus{stroke:currentColor;stroke-width:2}"),$"{view.Zoom} {layout}: {style}");
        foreach(var mark in PlanMarks(doc)){
            var ring=mark.Elements(ns+"rect").SingleOrDefault(r=>(string?)r.Attribute("class")=="lumen-focus");
            Check(ringed==(ring is not null),$"{view.Zoom} {layout}: ring {(ring is null?"missing":"present")} on {(string?)mark.Attribute("data-event")}");
            if(ring is null)continue;
            Check((string?)ring.Attribute("stroke")=="none"&&(string?)ring.Attribute("fill")=="none","the ring shows before focus");
            double A(XElement e,string name)=>double.Parse((string?)e.Attribute(name)??"0",CultureInfo.InvariantCulture);
            // Every word of the line, from its baseline up to its size, lies inside the ring.
            foreach(var t in mark.Elements(ns+"text"))
                Check(A(ring,"x")<=A(t,"x")&&A(t,"x")<A(ring,"x")+A(ring,"width")&&A(ring,"y")<=A(t,"y")-A(t,"font-size")&&A(t,"y")<=A(ring,"y")+A(ring,"height"),$"{view.Zoom} {layout}: '{t.Value}' at {A(t,"x")},{A(t,"y")} outside its ring");
        }
    }
    var chart=ChartSvg.Render(Spec());
    Check(chart.Contains("class='lumen-svg' ")&&!chart.Contains("lumen-planner")&&chart.Contains(".lumen-svg .lumen-datum:focus{stroke:currentColor;stroke-width:3}"),"a chart's root or focus style moved");
});
Test("Planner year view: each event takes the lowest lane free on all its days, so a day beside a span reuses the top lane",()=>{
    // Monday 8 March: a one-day ride and a three-day span to Wednesday; Tuesday and Wednesday one ride each. The span takes the
    // second lane; the Tuesday and Wednesday rides find the top lane free and take it, as counting a day's events would not.
    var spec=PlanYear(s=>s with{Events=[new("a","A Single",new(2027,3,8)),new("c","C Span",new(2027,3,8)){End=new DateOnly(2027,3,10)},
        new("d","D Tuesday",new(2027,3,9)),new("b","B Wednesday",new(2027,3,10))]});
    var doc=PlanSvg(spec);
    double Top(string id)=>double.Parse(PlanMarks(doc).Single(m=>(string?)m.Attribute("data-event")==id).Descendants(ns+"rect").First().Attribute("y")!.Value,CultureInfo.InvariantCulture);
    Check(Top("c")==Top("a")+7&&Top("d")==Top("a")&&Top("b")==Top("a"),$"a {Top("a")}, c {Top("c")}, d {Top("d")}, b {Top("b")}");
});
Test("Planner years: another period is a dotted band, unlike the school holiday's solid one, named in the day and in the legend, 3:1 on both grounds",()=>{
    foreach(var style in new[]{ChartStyle.Light,ChartStyle.Dark,ChartStyle.Midnight}){
        // Monday 10 to Sunday 16 May 2027: invented exams, the weekend included.
        var spec=PlanYear(s=>s with{Style=style,Periods=[..s.Periods,new(new(2027,5,10),new DateOnly(2027,5,16),"Invented exams",PeriodKind.Other,"ZA")]});
        var wide=PlanSvg(spec);
        XElement Day(string day)=>wide.Descendants().Single(e=>(string?)e.Attribute("data-day")==day);
        foreach(var day in new[]{"2027-05-10","2027-05-15"}){
            var dots=Day(day).Elements(ns+"line").SingleOrDefault(l=>(string?)l.Attribute("class")=="lumen-other-period");
            Check(dots is not null&&(string?)dots.Attribute("stroke-dasharray")=="2 2"&&(string?)dots.Attribute("stroke")==style.Muted,$"{day}: {dots}");
            foreach(var behind in new[]{style.Background,style.Grid})Check(Lumen.Charts.Contrast.Ratio(style.Muted,behind)>=3,$"dots on {behind}");
            Check(Day(day).Attribute("aria-label")!.Value.Contains("Invented exams (period)"),Day(day).Attribute("aria-label")!.Value);
        }
        Check(!Day("2027-05-17").Elements(ns+"line").Any(),"the day after the exams is dotted");
        // A school holiday day keeps its solid band and no dots.
        var school=Day("2027-03-29");
        Check(school.Elements(ns+"rect").Any(r=>(string?)r.Attribute("height")=="3"&&(string?)r.Attribute("fill")==style.Muted)&&!school.Elements(ns+"line").Any(),school.ToString());
        var legend=wide.Descendants(ns+"g").Single(g=>(string?)g.Attribute("class")=="lumen-legend");
        Check(legend.Elements(ns+"text").Any(t=>t.Value=="other period")&&legend.Elements(ns+"line").Any(l=>(string?)l.Attribute("stroke-dasharray")=="2 2"),"the legend lacks the other period");
        var narrow=PlanSvg(spec with{Width=340},PlannerView.WholePeriod,PlannerLayout.Narrow);
        var slot=narrow.Descendants().Single(e=>(string?)e.Attribute("data-weekend")=="2027-05-15");
        Check(slot.Elements(ns+"line").Any(l=>(string?)l.Attribute("class")=="lumen-other-period")&&slot.Attribute("aria-label")!.Value.Contains("Invented exams"),slot.ToString());
        Check(!narrow.Descendants().Single(e=>(string?)e.Attribute("data-weekend")=="2027-05-22").Elements(ns+"line").Any(),"the weekend after the exams is dotted");
        Check(narrow.Descendants(ns+"text").Any(t=>t.Value=="other period"),"the narrow legend lacks the other period");
    }
});
Test("Planner year view: a busy day in a long weekend writes \"+N\" clear of the weekend's bracket, and the week's count is clear of it too",()=>{
    foreach(var weekStart in new[]{DayOfWeek.Monday,DayOfWeek.Wednesday}){
        // Monday 26 and Tuesday 27 April 2027 are holidays, so Saturday 24 to Tuesday 27 is a long weekend; five rides on Sunday 25.
        var spec=PlanYear(s=>s with{WeekStart=weekStart,Periods=[..s.Periods,new(new(2027,4,26),null,"Invented Monday",PeriodKind.PublicHoliday,"ZA")],
            Events=Enumerable.Range(0,5).Select(i=>new PlannerEvent($"r{i}",$"Invented ride {i}",new(2027,4,25)){Relevance=PlannerRelevance.Clash}).ToArray()});
        var doc=PlanSvg(spec);
        double A(XElement e,string name)=>double.Parse((string?)e.Attribute(name)??"0",CultureInfo.InvariantCulture);
        // A text's own words, without the tooltip inside it.
        string Own(XElement t)=>string.Concat(t.Nodes().OfType<XText>().Select(n=>n.Value));
        // A word's box: its estimated width about its anchor, from its baseline up 0.75 of its size.
        Bounds Word(XElement t){var size=A(t,"font-size");var wide=ChartSvg.Wide(Own(t))*size/11;return new(A(t,"x")-wide/2,A(t,"y")-.75*size,A(t,"x")+wide/2,A(t,"y"));}
        var brackets=doc.Descendants(ns+"path").Where(p=>(string?)p.Attribute("fill")=="none"&&((string?)p.Attribute("d")??"").Contains(" H")&&p.Ancestors().All(a=>(string?)a.Attribute("class")!="lumen-legend")).Select(p=>{
            var d=p.Attribute("d")!.Value;var m=System.Text.RegularExpressions.Regex.Match(d,@"^M([\d.]+),([\d.]+) v([\d.]+) H([\d.]+)");
            double V(int i)=>double.Parse(m.Groups[i].Value,CultureInfo.InvariantCulture);
            return new Bounds(V(1)-.75,V(2),V(4)+.75,V(2)+V(3)+.75);}).ToArray();
        Check(brackets.Length==1,$"{brackets.Length} long-weekend brackets");
        var more=doc.Descendants(ns+"text").Single(t=>(string?)t.Attribute("class")=="lumen-more");
        Check(Own(more)=="+2"&&Word(more).Overlaps(brackets[0])==false&&Word(more).Left<brackets[0].Right&&brackets[0].Left<Word(more).Right,$"+N {Word(more)} and the bracket {brackets[0]}");
        foreach(var count in doc.Descendants(ns+"g").Where(g=>(string?)g.Attribute("class")=="lumen-week").SelectMany(g=>g.Elements(ns+"text")))
            Check(!Word(count).Overlaps(brackets[0]),$"the count {Word(count)} and the bracket {brackets[0]}");
    }
});
Test("Planner: each view's legend names only what that view draws",()=>{
    string[] Words(XDocument doc)=>doc.Descendants(ns+"g").Single(g=>(string?)g.Attribute("class")=="lumen-legend").Elements(ns+"text").Select(t=>t.Value).ToArray();
    var spec=PlanYear();
    var year=Words(PlanSvg(spec));
    Check(year.SequenceEqual(["clash","close","other","provisional","cancelled","yours","public holiday","school holiday","other period","long weekend","weekend"]),string.Join(",",year));
    // The month writes holidays and other periods in words and draws no long weekends.
    var month=Words(PlanSvg(spec,PlannerView.Month(2027,3)));
    Check(month.SequenceEqual(["clash","close","other","provisional","cancelled","yours","school holiday","weekend"]),string.Join(",",month));
    // The narrow year marks a weekend's clashes and close events, its holiday and other periods, and nothing else.
    var narrow=Words(PlanSvg(spec with{Width=340},PlannerView.WholePeriod,PlannerLayout.Narrow));
    Check(narrow.SequenceEqual(["clash","close","public holiday","other period","weekend"]),string.Join(",",narrow));
});
Test("Planner: every day of the wide year carries an unpainted cell a pointer can hit, and no other view does",()=>{
    var year=PlannerSvg.Render(PlanYear(),PlannerView.WholePeriod);
    Check(Regex.Matches(year,"<g class='lumen-day' role='group' data-day='[0-9-]+' aria-label='[^']*'><rect class='lumen-cell' [^>]*fill='none' stroke='none' pointer-events='all'/>").Count==365,"one cell first in every day");
    Check(Regex.Matches(year,"class='lumen-cell'").Count==365,"no other cells");
    foreach(var view in new[]{PlannerView.Month(2027,3),PlannerView.Day(new(2027,3,13))})
        Check(!PlannerSvg.Render(PlanYear(),view).Contains("lumen-cell"),$"none in {view.Zoom}");
    Check(!PlannerSvg.Render(PlanYear() with{Width=340},PlannerView.WholePeriod,PlannerLayout.Narrow).Contains("lumen-cell"),"none in the narrow year");
});
// ---- 0.44.0: the interactive planner component ----
// Renders a LumenPlanner under a cascaded style, lets a test act on it, and returns its markup once the renderer settles.
// The act receives the component and a function that returns the markup as it stands.
#pragma warning disable ASP0006 // the host's parameters are a dictionary, so their sequence numbers cannot be literals
string PlanComponent(PlannerSpec spec,Func<LumenPlanner,Func<Task<string>>,Task>? act=null,Dictionary<string,object?>? more=null,ChartStyle? style=null)
{
    var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoJs>().BuildServiceProvider();
    var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
    try {
        LumenPlanner? planner=null;
        RenderFragment content=b=>{
            b.OpenComponent<LumenPlanner>(0);b.AddAttribute(1,"Spec",spec);
            var i=2;foreach(var (name,value) in more??new())b.AddAttribute(i++,name,value);
            b.AddComponentReferenceCapture(100,c=>planner=(LumenPlanner)c);b.CloseComponent();
        };
        return renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<CascadingValue<ChartStyle>>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Value",style??ChartStyle.Light},{"ChildContent",content}}));
            if(act is not null)await act(planner!,async()=>{await root.QuiescenceTask;return root.ToHtmlString();});
            await root.QuiescenceTask;
            return root.ToHtmlString();
        }).GetAwaiter().GetResult();
    } finally {renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();services.Dispose();}
}
#pragma warning restore ASP0006
bool PlanDisabled(string html,string label)=>Regex.IsMatch(html,$"<button[^>]*\\bdisabled\\b[^>]*aria-label=\"{Regex.Escape(label)}\"");
bool PlanEnabled(string html,string label)=>Regex.IsMatch(html,$"<button(?![^>]*\\bdisabled\\b)[^>]*aria-label=\"{Regex.Escape(label)}\"");
Task PlanStep(LumenPlanner p,int by)=>(Task)typeof(LumenPlanner).GetMethod("Step",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(p,[by])!;
void PlanChoose(LumenPlanner p,string group,string value)=>typeof(LumenPlanner).GetMethod("Choose",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(p,[group,value]);
void PlanClear(LumenPlanner p)=>typeof(LumenPlanner).GetMethod("ClearFilters",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(p,null);
string Where(string text)=>$"<span class=\"lumen-planner-where\">{text}</span>";
Task PlanSpec(LumenPlanner p,PlannerSpec spec)=>p.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",spec}}));

Test("LumenPlanner: prerender draws the year with its toolbar, filters, keys and status, and calls no script",()=>{
    var html=PlanComponent(PlanYear());   // NoJs throws if the component calls the script while prerendering
    Check(html.Contains("class=\"lumen-planner-box\" data-zoom=\"year\" data-layout=\"wide\""),"root");
    Check(html.Contains("class='lumen-svg lumen-planner'")&&html.Contains("viewBox='0 0 1100 "),"the year at the spec's width");
    Check(html.Contains(Where("2027")),"where");
    Check(PlanDisabled(html,"Back")&&PlanDisabled(html,"Previous")&&PlanDisabled(html,"Next"),"nothing to go back or step to in the year");
    Check(html.Contains("<span class=\"lumen-keys\" hidden>Arrow keys move between days, up and down by month; Enter or Space opens the month.</span>"),"keys");
    Check(html.Contains("<span class=\"lumen-status\" role=\"status\"></span>"),"status");
    foreach(var part in new[]{"<legend>Region</legend>","aria-label=\"Gauteng, in South Africa\"","aria-label=\"Western Cape, in South Africa\"",">South Africa</button>",
        "<legend>Category</legend>",">XCO</button>",">Stage</button>","<legend>Audience</legend>",">Kids</button>",">Open</button>",
        "<legend>Status</legend>",">Confirmed</button>",">Provisional</button>","<legend>Relevance</legend>",">Clash</button>",">Other</button>"})
        Check(html.Contains(part),part);
    Check(Regex.IsMatch(html,"<button type=\"button\" disabled>Clear filters</button>"),"nothing to clear");
});
Test("LumenPlanner: a day of the year opens its month, a day of the month opens the day and raises DaySelected, and Back steps out",()=>{
    var views=new List<PlannerView>();var days=new List<DateOnly>();var receiver=new object();
    var more=new Dictionary<string,object?>{{"ViewChanged",EventCallback.Factory.Create<PlannerView>(receiver,v=>views.Add(v))},{"DaySelected",EventCallback.Factory.Create<DateOnly>(receiver,d=>days.Add(d))}};
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.Open("2027-03-10",false,"year");
        var month=await html();
        Check(month.Contains("data-zoom=\"month\"")&&month.Contains(Where("March 2027"))&&month.Contains("Showing March 2027"),"month");
        Check(PlanEnabled(month,"Back to 2027")&&PlanEnabled(month,"Previous month")&&PlanEnabled(month,"Next month"),"toolbar in a month");
        Check(month.Contains("Arrow keys move between days; Enter or Space opens the day; Escape goes back to the year."),"month keys");
        await p.Open("2027-03-13",false,"month");
        var day=await html();
        Check(day.Contains("data-zoom=\"day\"")&&day.Contains(Where("Saturday 13 March 2027"))&&PlanEnabled(day,"Back to March 2027")&&PlanEnabled(day,"Next day"),"day");
        await p.Open("2027-03-14",false,"day");   // a day view opens nothing
        Check((await html()).Contains(Where("Saturday 13 March 2027")),"a day stays");
        await p.Back(null,"day");
        Check((await html()).Contains(Where("March 2027")),"back to the month");
        await p.Back("2027-03-13","month");
        Check((await html()).Contains("data-zoom=\"year\""),"back to the year");
    },more);
    Check(views.SequenceEqual(new[]{PlannerView.Month(2027,3),PlannerView.Day(new(2027,3,13)),PlannerView.Month(2027,3),PlannerView.WholePeriod}),string.Join(", ",views));
    Check(days.SequenceEqual(new[]{new DateOnly(2027,3,13)}),"DaySelected once");
});
Test("LumenPlanner: Previous and Next step a month or a day and stop at the period's edges",()=>{
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.Open("2027-01-06",false,"year");
        var january=await html();
        Check(PlanDisabled(january,"Previous month")&&PlanEnabled(january,"Next month"),"January");
        await PlanStep(p,1);
        Check((await html()).Contains(Where("February 2027")),"February");
        await PlanStep(p,-1);await PlanStep(p,-1);   // the second step is refused at the edge
        Check((await html()).Contains(Where("January 2027")),"stays in January");
    });
    PlanComponent(PlanYear(),async(p,html)=>{
        var last=await html();
        Check(PlanDisabled(last,"Next day")&&PlanEnabled(last,"Previous day")&&last.Contains(Where("Friday 31 December 2027")),"the last day");
        await PlanStep(p,-1);
        Check((await html()).Contains(Where("Thursday 30 December 2027")),"the day before");
    },new(){{"View",PlannerView.Day(new(2027,12,31))}});
    Check(PlanDisabled(PlanComponent(PlanYear(),null,new(){{"View",PlannerView.Month(2027,12)}}),"Next month"),"December");
});
Test("LumenPlanner: a day outside the period stays shut, and a new period returns the planner to its whole period",()=>{
    PlanComponent(PlanYear(s=>s with{From=new(2027,3,5)}),async(p,html)=>{
        await p.Open("2027-03-10",false,"year");
        await p.Open("2027-03-01",false,"month");
        var shut=await html();
        Check(shut.Contains("data-zoom=\"month\"")&&shut.Contains("Monday 1 March 2027 is outside the planner"),"outside");
        await p.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",PlanYear(s=>s with{From=new(2028,1,1),To=new(2028,12,31),Periods=[],Events=[]})}}));
        var next=await html();
        Check(next.Contains("data-zoom=\"year\"")&&next.Contains(Where("2028")),"a new period starts at the whole period");
        Check(next.Contains("Showing 2028"),"and says so");
    });
    Exception? caught=null;
    try{PlanComponent(PlanYear(),null,new(){{"View",PlannerView.Month(2028,1)}});}catch(Exception e){caught=e;}
    while(caught is not null and not ArgumentException&&caught.InnerException is not null)caught=caught.InnerException;
    Check(caught is ArgumentException,caught?.GetType().Name??"a host's view outside the period was drawn");
});
Test("LumenPlanner: the box's width sets the drawing's width and below 640 pixels the narrow layout, keeping the view",()=>{
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.Open("2027-03-10",false,"year");
        await p.Fit(500);
        var narrow=await html();
        Check(narrow.Contains("data-zoom=\"month\" data-layout=\"narrow\"")&&narrow.Contains("viewBox='0 0 500 ")&&narrow.Contains(Where("March 2027")),"the agenda");
        Check(narrow.Contains("class='lumen-agenda-day'"),"agenda drawn");
        await p.Fit(100);
        Check((await html()).Contains("viewBox='0 0 320 "),"never narrower than 320");
        await p.Fit(900);
        var wide=await html();
        Check(wide.Contains("data-layout=\"wide\"")&&wide.Contains("viewBox='0 0 900 ")&&wide.Contains(Where("March 2027")),"the grid again");
    });
});
Test("LumenPlanner: on a phone a weekend across a month's end opens the month of the bar it stands in",()=>{
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.Fit(400);
        Check((await html()).Contains("Arrow keys move between weekends, up and down by month; Enter or Space opens the month."),"phone keys");
        await p.Open("2027-07-31",true,"year");
        Check((await html()).Contains(Where("August 2027")),"the later bar opens August");
        await p.Back("2027-08-01","month");
        await p.Open("2027-07-31",false,"year");
        Check((await html()).Contains(Where("July 2027")),"the earlier bar opens July");
    });
});
Test("LumenPlanner: selecting an event raises EventSelected and names it in the status line",()=>{
    var picked=new List<PlannerEvent>();
    var html=PlanComponent(PlanYear(),async(p,_)=>{await p.SelectEvent("e1");await p.SelectEvent("nobody");},
        new(){{"EventSelected",EventCallback.Factory.Create<PlannerEvent>(new object(),e=>picked.Add(e))}});
    Check(picked.Count==1&&picked[0].Id=="e1","EventSelected once, for a known id only");
    Check(html.Contains("Selected Hilltop XCO, Saturday 13 March 2027, Gauteng, XCO, Kids, clash"),"status");
});
Test("LumenPlanner: a host's view, a cascaded style and hidden filters are honoured",()=>{
    var html=PlanComponent(PlanYear(),null,new(){{"View",PlannerView.Month(2027,3)},{"ShowFilters",false}},ChartStyle.Midnight);
    Check(html.Contains("data-zoom=\"month\"")&&html.Contains(Where("March 2027")),"host view");
    Check(!html.Contains("lumen-planner-filters"),"no filters");
    Check(html.Contains(ChartStyle.Midnight.Background),"cascaded style");
});
Test("LumenPlanner: a chip filters the drawing, says it is pressed, and Clear filters restores everything",()=>{
    var html=PlanComponent(PlanYear(),async(p,html)=>{
        PlanChoose(p,"Region","ZA-GP");
        var gauteng=await html();
        Check(gauteng.Contains("data-event='e1'")&&!gauteng.Contains("data-event='e2'"),"Western Cape's event drops out");
        Check(gauteng.Contains("aria-pressed=\"true\" aria-label=\"Gauteng, in South Africa\""),"pressed");
        Check(gauteng.Contains("Filter Gauteng on")&&Regex.IsMatch(gauteng,"<button type=\"button\">Clear filters</button>"),"status and clear");
        PlanChoose(p,"Status","Provisional");
        var nothing=await html();
        Check(!nothing.Contains("data-event=")&&nothing.Contains("class='lumen-svg lumen-planner'")&&nothing.Contains("viewBox='"),"filtered to nothing still draws the year");
        PlanClear(p);
    });
    Check(html.Contains("data-event='e2'")&&html.Contains("Filters cleared")&&!html.Contains("aria-pressed=\"true\""),"cleared");
});
Test("LumenPlanner: the spec's filter is where the reader starts, and a new spec drops chosen values it no longer offers",()=>{
    var start=PlanYear(s=>s with{Filter=new(){Categories=["XCO","Track"]}});
    var html=PlanComponent(start,async(p,html)=>{
        var first=await html();
        Check(Regex.IsMatch(first,"aria-pressed=\"true\"[^>]*>XCO</button>")&&!first.Contains("data-event='e2'"),"starts filtered to XCO");
        PlanChoose(p,"Region","ZA-WC");
        // The next spec drops the Western Cape (an event in an unknown region would be refused), so the second event moves to Gauteng.
        await p.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",start with{Regions=[new("ZA","South Africa"),new("ZA-GP","Gauteng","ZA")],Events=[start.Events[0],start.Events[1] with{Region="ZA-GP"}]}}}));
    });
    Check(html.Contains("data-event='e1'")&&!html.Contains("Western Cape"),"Western Cape dropped from the filter with the region");
    Check(Regex.IsMatch(html,"aria-pressed=\"true\"[^>]*>XCO</button>")&&!html.Contains("data-event='e2'"),"XCO stays chosen, so the Stage event stays out");
    Check(html.Contains("Filters updated"),"and the reader is told");
    Check(Regex.IsMatch(html,"<button type=\"button\">Clear filters</button>"),"XCO and the host's Track stay chosen");
});
Test("LumenPlanner: a host filter that changes by content becomes the reader's filter, and one that does not leaves the reader's choices",()=>{
    var xco=PlanYear(s=>s with{Filter=new(){Categories=["XCO"]}});
    PlanComponent(xco,async(p,html)=>{
        await PlanSpec(p,xco with{Filter=new(){Categories=["Stage"]}});
        var stage=await html();
        Check(Regex.IsMatch(stage,"aria-pressed=\"true\"[^>]*>Stage</button>")&&!Regex.IsMatch(stage,"aria-pressed=\"true\"[^>]*>XCO</button>"),"Stage is pressed");
        Check(stage.Contains("data-event='e2'")&&!stage.Contains("data-event='e1'"),"e2 drawn, e1 not");
        Check(stage.Contains("Filters updated"),"the reader is told");
        PlanChoose(p,"Audience","Open");
        // A new filter object with the same content is not a change of the host's filter: the reader's Open stays.
        await PlanSpec(p,xco with{Title="Season planner, later",Filter=new(){Categories=["Stage"]}});
        var same=await html();
        Check(Regex.IsMatch(same,"aria-pressed=\"true\"[^>]*>Open</button>")&&Regex.IsMatch(same,"aria-pressed=\"true\"[^>]*>Stage</button>"),"the reader's choice stays");
        // Removing the host's filter is a change of its content: the reader's filter becomes none.
        await PlanSpec(p,xco with{Filter=null});
        var none=await html();
        Check(!none.Contains("aria-pressed=\"true\"")&&none.Contains("data-event='e1'")&&none.Contains("data-event='e2'"),"no filter left");
    });
});
Test("LumenPlanner: a chosen status or relevance that no event of a new spec uses is dropped with it",()=>{
    PlanComponent(PlanYear(),async(p,html)=>{
        PlanChoose(p,"Status","Provisional");
        Check(!(await html()).Contains("data-event='e1'"),"e1 is hidden by the chosen status");
        await PlanSpec(p,PlanYear(s=>s with{Events=[s.Events[0]]}));
        var next=await html();
        Check(next.Contains("data-event='e1'")&&!next.Contains("aria-pressed=\"true\""),"no hidden status filter left");
    });
    PlanComponent(PlanYear(),async(p,html)=>{
        PlanChoose(p,"Relevance","Clash");
        Check(!(await html()).Contains("data-event='e2'"),"e2 is hidden by the chosen relevance");
        await PlanSpec(p,PlanYear(s=>s with{Events=[s.Events[1]]}));
        var next=await html();
        Check(next.Contains("data-event='e2'")&&!next.Contains("aria-pressed=\"true\""),"no hidden relevance filter left");
    });
});
Test("LumenPlanner: on a phone a weekend whose later month lies outside the period stays shut and says so",()=>{
    PlanComponent(PlanYear(s=>s with{To=new(2027,7,31)}),async(p,html)=>{
        await p.Fit(400);
        await p.Open("2027-07-31",true,"year");
        var shut=await html();
        Check(shut.Contains("data-zoom=\"year\"")&&shut.Contains("August 2027 is outside the planner"),"outside");
    });
});
Test("LumenPlanner: a new spec that returns the planner to its whole period raises ViewChanged, and the host's stale view is ignored",()=>{
    var views=new List<PlannerView>();
    var bound=EventCallback.Factory.Create<PlannerView>(new object(),v=>views.Add(v));
    var later=PlanYear(s=>s with{From=new(2028,1,1),To=new(2028,12,31),Periods=[],Events=[]});
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.Open("2027-03-10",false,"year");
        // As @bind-View does: the host passes March back, then a new spec arrives with that view still bound.
        await p.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",PlanYear()},{"View",PlannerView.Month(2027,3)},{"ViewChanged",bound}}));
        await p.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Spec",later},{"View",PlannerView.Month(2027,3)},{"ViewChanged",bound}}));
        var next=await html();
        Check(next.Contains("data-zoom=\"year\"")&&next.Contains(Where("2028"))&&next.Contains("Showing 2028"),"the whole of 2028");
    },new(){{"ViewChanged",bound}});
    Check(views.SequenceEqual(new[]{PlannerView.Month(2027,3),PlannerView.WholePeriod}),string.Join(", ",views));
});
Test("LumenPlanner: a view the host sets is said in the status line",()=>{
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"View",PlannerView.Day(new(2027,3,13))}}));
        var day=await html();
        Check(day.Contains(Where("Saturday 13 March 2027"))&&day.Contains("Showing Saturday 13 March 2027"),"Showing Saturday 13 March 2027");
    },new(){{"View",PlannerView.Month(2027,3)}});
});
Test("LumenPlanner: a call made in a zoom the planner has left is ignored, so a double-click or a held key acts once",()=>{
    var days=new List<DateOnly>();var views=new List<PlannerView>();
    var more=new Dictionary<string,object?>{{"DaySelected",EventCallback.Factory.Create<DateOnly>(new object(),d=>days.Add(d))},{"ViewChanged",EventCallback.Factory.Create<PlannerView>(new object(),v=>views.Add(v))}};
    PlanComponent(PlanYear(),async(p,html)=>{
        await p.Open("2027-03-10",false,"year");
        Check((await html()).Contains(Where("March 2027")),"the first call opens March");
        await p.Open("2027-03-10",false,"year");
        Check((await html()).Contains(Where("March 2027")),"the second, made from the year, is ignored");
        await p.Back(null,"day");
        Check((await html()).Contains(Where("March 2027")),"a Back made from a day is ignored in a month");
        await p.Back(null,"month");
        await p.Back(null,"month");
        Check((await html()).Contains("data-zoom=\"year\""),"one Back from the month reaches the year");
    },more);
    Check(days.Count==0,"no DaySelected: "+string.Join(", ",days));
    Check(views.SequenceEqual(new[]{PlannerView.Month(2027,3),PlannerView.WholePeriod}),string.Join(", ",views));
});
// 0.45.0: places and points.
Placing RacedResult(int? place,int? field,double? points,int week,string? series=null)=>new(place){Field=field,Points=points,Date=new DateOnly(2027,3,1).AddDays(7*week),Series=series};
string[] PlacedNames(ChartSpec spec)=>Regex.Matches(ChartSvg.Render(spec),"aria-label='([^']*)'").Select(m=>System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).ToArray();
Test("Placings: nothing placed is no chart, so the page shows its own empty state",()=>{
    Check(PlacingsChart.Build([])is null,"no results");
    Check(PlacingsChart.Build([new Placing(null){Points=10},new Placing(0){Field=20},new Placing(-3)])is null,"no place above 0");
});
Test("Placings: places read up as better, carry their field, and say their change in words",()=>{
    var spec=PlacingsChart.Build([RacedResult(30,50,40,0),RacedResult(24,48,52,1),RacedResult(27,51,47,2),RacedResult(19,null,58,3)])!;
    Check(spec.Kind==ChartKind.Line&&spec.YReversed&&spec.YLabel=="Place"&&spec.Title=="Places and points","shape");
    Check(spec.Width==340&&spec.Height==380&&spec.XMin==-.5&&spec.XMax==3.5,"size and X");
    Check(spec.Panes.Count==1&&spec.Panes[0].Label=="Points"&&spec.Panes[0].Weight==1,"points pane");
    Check(spec.Description=="Finishing place out of the field, first at the top, and points. Best: 19.",spec.Description);
    var names=PlacedNames(spec);
    Check(names.Contains("Place: 8 Mar 2027, 24/48, better than the previous"),"better");
    Check(names.Contains("Place: 15 Mar 2027, 27/51, worse than the previous"),"worse");
    Check(names.Contains("Place: 22 Mar 2027, 19, better than the previous"),"no field: the place alone");
    var place=spec.Series[0];
    Check(place.ChangeColors==ChangeColors.LowerIsBetter&&place.ValueLabels&&place.Markers==MarkerStyle.Filled,"place line");
});
Test("Placings: a race without points is a gap, never a zero, and a real zero stays a zero",()=>{
    var points=PlacingsChart.Build([RacedResult(5,20,40,0),RacedResult(6,20,0,1),RacedResult(4,20,null,2),RacedResult(3,20,52,3)])!.Series[^1];
    Check(points.Name=="Points"&&points.Pane==1&&points.ValueLabels&&points.Markers==MarkerStyle.Filled,"points line");
    Check(points.Points[1].Y==0&&points.Points[2].Y is null,"zero and gap");
});
Test("Placings: with no points anywhere there is no points line and no empty pane",()=>{
    var spec=PlacingsChart.Build([RacedResult(5,20,null,0),RacedResult(4,22,null,1)])!;
    Check(spec.Panes.Count==0&&spec.Series.Count==1&&spec.Height==260,"no pane");
    Check(spec.Description=="Finishing place out of the field, first at the top. Best: 4.",spec.Description);
});
Test("Placings: a place is compared only with the previous race of its own series",()=>{
    var spec=PlacingsChart.Build([RacedResult(30,50,40,0,"Invented League"),RacedResult(5,20,null,1,"Invented Open"),RacedResult(24,48,52,2," Invented League "),RacedResult(7,21,null,3,"Invented Open")])!;
    Check(spec.Series.Select(s=>s.Name).SequenceEqual(["Place · Invented League","Place · Invented Open","Points"]),string.Join("|",spec.Series.Select(s=>s.Name)));
    var names=PlacedNames(spec);
    Check(names.Contains("Place · Invented League: 15 Mar 2027, 24/48, better than the previous"),"vs 30th, not vs the open race's 5th");
    Check(names.Contains("Place · Invented Open: 22 Mar 2027, 7/21, worse than the previous"),"open vs open");
    Check(spec.Series[0].Points[1].Y is null&&spec.Series[1].Points[0].Y is null,"gaps at the other series' races");
    var blank=PlacingsChart.Build([RacedResult(3,9,null,0),RacedResult(4,9,null,1,"Invented Cup")])!;
    Check(blank.Series.Select(s=>s.Name).SequenceEqual(["Place","Place · Invented Cup"]),"a result with no series is the place line alone");
});
Test("Placings: races are ordered by date when all have one, else kept as given, and labelled by label, date or number",()=>{
    var sorted=PlacingsChart.Build([RacedResult(3,9,null,2),RacedResult(5,9,null,0),RacedResult(4,9,null,1)])!;
    Check(sorted.Series[0].Points.Select(p=>p.Y).SequenceEqual(new double?[]{5,4,3}),"by date");
    var same=PlacingsChart.Build([new Placing(8){Date=new(2027,3,1)},new Placing(2){Date=new(2027,3,1)}])!;
    Check(same.Series[0].Points.Select(p=>p.Y).SequenceEqual(new double?[]{8,2}),"same date keeps the given order");
    var mixed=PlacingsChart.Build([RacedResult(3,9,null,2),new Placing(5),new Placing(4){Label="Final"}])!;
    Check(mixed.Series[0].Points.Select(p=>p.Y).SequenceEqual(new double?[]{3,5,4}),"an undated race keeps the given order");
    Check(mixed.Series[0].Points.Select(p=>p.Label).SequenceEqual(["15 Mar 2027","#2","Final"]),string.Join("|",mixed.Series[0].Points.Select(p=>p.Label)));
    var custom=PlacingsChart.Build([RacedResult(3,9,null,0),new Placing(5)],new(){DateFormat="dd-MM-yyyy",Unlabelled="R{0}"})!;
    Check(custom.Series[0].Points.Select(p=>p.Label).SequenceEqual(["01-03-2027","R2"]),"custom format and number");
});
Test("Placings: colours come from the style, or cycle through the host's",()=>{
    var light=ChartStyle.Light;
    var two=PlacingsChart.Build([RacedResult(3,9,1,0,"A"),RacedResult(4,9,2,1,"B")])!;
    Check(two.Series[0].Color==light.Text&&two.Series[1].Color==light.Series[0]&&two.Series[2].Color==light.Series[1]&&two.Style==light,"defaults");
    var five=PlacingsChart.Build(Enumerable.Range(0,5).Select(i=>RacedResult(i+1,9,null,i,$"S{i}")),new(){PlaceColors=["#F5F6F7","#F5B642"],PointsColor="#D7DDE5",Style=ChartStyle.Dark})!;
    Check(five.Series.Select(s=>s.Color).SequenceEqual(["#F5F6F7","#F5B642","#F5F6F7","#F5B642","#F5F6F7"]),"cycled");
    Check(five.Series.Select(s=>s.Name).Distinct().Count()==5&&five.Style==ChartStyle.Dark,"names and style");
    var pts=PlacingsChart.Build([RacedResult(3,9,1,0)],new(){PointsColor="#D7DDE5"})!;
    Check(pts.Series[^1].Color=="#D7DDE5","points colour");
});
Test("Placings: odd but real data still draws: no field, a field of 0, a place past its field, a single race",()=>{
    var spec=PlacingsChart.Build([RacedResult(30,0,null,0),RacedResult(25,20,null,1)])!;
    Check(spec.Series[0].Points[0].ValueNote is null&&spec.Series[0].Points[1].ValueNote=="/20","notes");
    var one=PlacingsChart.Build([RacedResult(2,10,5,0)])!;
    Check(one.XMin==-.5&&one.XMax==.5,"one race");
    foreach(var s in new[]{spec,one}){var svg=ChartSvg.Render(s);Check(svg.StartsWith("<svg"),"renders");}
    Check(!PlacedNames(one).Any(n=>n.Contains("previous")),"no previous race, no change words");
});
Test("Placings: refusals say why",()=>{
    Reject(()=>PlacingsChart.Build(null!));
    Reject(()=>PlacingsChart.Build([new Placing(1),null!]));
    foreach(var bad in new PlacingsOptions[]{new(){PlaceName=" "},new(){PointsName=""},new(){DateFormat=""},new(){Unlabelled="R"},new(){PlaceColors=[]}})
        Reject(()=>PlacingsChart.Build([new Placing(1)],bad));
});
Console.WriteLine($"\n{passed} passed; {failures.Count} failed.");
foreach(var failure in failures)Console.Error.WriteLine(failure);
return failures.Count==0?0:1;

// A box in a drawing's pixels, such as a label's.
readonly record struct Bounds(double Left,double Top,double Right,double Bottom)
{
    public bool Overlaps(Bounds other)=>Left<other.Right&&other.Left<Right&&Top<other.Bottom&&other.Top<Bottom;
    public Bounds Grown(double by)=>new(Left-by,Top-by,Right+by,Bottom+by);
    // A circle reaches into the box when its centre is nearer the box than its radius.
    public bool Reaches(double x,double y,double radius){double dx=Math.Max(0,Math.Max(Left-x,x-Right)),dy=Math.Max(0,Math.Max(Top-y,y-Bottom));return dx*dx+dy*dy<radius*radius;}
    // A line crosses the box when some of one of its pieces is left inside it once clipped to each of the box's four sides; a line
    // that only grazes a side does not.
    public bool Crossed((double X,double Y)[] line){for(var i=1;i<line.Length;i++)if(Clips(line[i-1],line[i]))return true;return false;}
    bool Clips((double X,double Y) a,(double X,double Y) b)
    {
        double low=0,high=1,dx=b.X-a.X,dy=b.Y-a.Y;
        foreach(var (p,q) in new[]{(-dx,a.X-Left),(dx,Right-a.X),(-dy,a.Y-Top),(dy,Bottom-a.Y)})
        {
            if(p==0){if(q<=0)return false;continue;}
            if(p<0)low=Math.Max(low,q/p);else high=Math.Min(high,q/p);
            if(low>=high)return false;
        }
        return true;
    }
}
sealed record Drawing(Dictionary<string,(double X,double Y,Bounds Label)> Nodes,(GraphEdge Edge,(double X,double Y)[] Line)[] Edges,(string Text,Bounds Box)[] Labels);

sealed class NoJs:IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Prerender must not invoke JS.");
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken token,object?[]? args)=>InvokeAsync<TValue>(identifier,args);
}

