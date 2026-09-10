using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Lumen.Charts;
using Lumen.Charts.Blazor;
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
ChartSpec Sample(ChartKind kind)=>kind switch{
    ChartKind.Candlestick=>Spec(kind) with{Series=[new("Price",[ChartPoint.Candle(0,10,12,9,11),ChartPoint.Candle(1,11,13,10,10.5),ChartPoint.Candle(2,10.5,11,8,9)])]},
    ChartKind.Band=>Spec(kind) with{Series=[new("Forecast",[ChartPoint.Interval(0,2,1,3),ChartPoint.Interval(1,5,4,6),ChartPoint.Interval(2,3,2,4)])]},
    ChartKind.Histogram or ChartKind.Box=>Spec(kind) with{Series=[new("Sample",Enumerable.Range(0,40).Select(i=>new ChartPoint(i,i%7+1)).ToArray())]},
    _=>Spec(kind)};
foreach(var kind in Enum.GetValues<ChartKind>())
{
    Test($"{kind}: valid SVG and accessible marks",()=>{
        var doc=Svg(Sample(kind));Check(doc.Root!.Name==ns+"svg");Check(doc.Descendants(ns+"title").Any());
        Check(doc.Descendants().Any(e=>(string?)e.Attribute("class")=="lumen-datum"));
        // Histogram bins and box glyphs are aggregates: focusable and labelled, but not observation indices.
        Check(kind is ChartKind.Histogram or ChartKind.Box || doc.Descendants().Any(e=>e.Attribute("data-point") is not null));
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
    var doc=Svg(Spec() with{Series=[new("S",[new(0,2),new(1,3),new(2,null),new(3,1),new(4,2)])]});
    Check(doc.Descendants(ns+"path").Count(e=>(string?)e.Attribute("stroke-width")=="2.5")==2);
    Check(doc.Descendants().Count(e=>e.Attribute("data-point") is not null)==4);
});
Test("Stacked positive and negative sums",()=>{
    var doc=Svg(Spec(ChartKind.StackedColumn) with{Series=[new("A",[new(0,4),new(1,-3)]),new("B",[new(0,6),new(1,-7)])]});
    var labels=doc.Descendants(ns+"text").Select(e=>e.Value).ToArray();Check(labels.Contains("10")&&labels.Contains("-10"));
    Check(doc.Descendants(ns+"rect").All(e=>double.Parse(e.Attribute("height")!.Value,CultureInfo.InvariantCulture)>=0));
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
    var doc=Svg(Spec(ChartKind.Scatter) with{YAxis=AxisKind.Log,Series=[new("S",[new(1,0.01),new(2,1),new(3,1000)])]});
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
    var doc=Svg(Bands());
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
    Reject(()=>ChartSvg.Render(Distribution(6) with{Series=[Distribution(6).Series[0],Distribution(6).Series[0]]}));
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
Console.WriteLine($"\n{passed} passed; {failures.Count} failed.");
foreach(var failure in failures)Console.Error.WriteLine(failure);
return failures.Count==0?0:1;

sealed class NoJs:IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Prerender must not invoke JS.");
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken token,object?[]? args)=>InvokeAsync<TValue>(identifier,args);
}
