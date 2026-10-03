using System.Diagnostics;
using System.Globalization;
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
        if(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Candlestick or ChartKind.Ohlc or ChartKind.Band)
            ChartSvg.Render(spec with{XFormat=ValueFormat.Duration});
        else Check(Refusal(spec with{XFormat=ValueFormat.Duration}).Contains("X format"),$"{kind}: X format");
        if(kind is ChartKind.Donut or ChartKind.Heatmap or ChartKind.Radar or ChartKind.Histogram)
        {
            Check(Refusal(spec with{YFormat=ValueFormat.Compact}).Contains("Y format"),$"{kind}: Y format");
            Check(Refusal(spec with{Y2Format=ValueFormat.Duration}).Contains("Y format"),$"{kind}: Y2 format");
        }
        else ChartSvg.Render(spec with{YFormat=ValueFormat.Duration,Y2Format=ValueFormat.Compact});
        if(kind is ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Area or ChartKind.Histogram)
        {
            Check(Refusal(spec with{YReversed=true}).Contains("zero baseline"),$"{kind}: reversed Y");
            Check(Refusal(spec with{Y2Reversed=true}).Contains("zero baseline"),$"{kind}: reversed Y2");
        }
        else if(kind is ChartKind.Donut or ChartKind.Heatmap or ChartKind.Radar or ChartKind.Gauge or ChartKind.Ring) Check(Refusal(spec with{YReversed=true}).Contains("no Y axis"),$"{kind}: reversed Y");
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
        Check(Accepts(zoned)==(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar),$"zones on {kind}");
        Check(Accepts(coloured)==(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar or ChartKind.Donut),$"point colours on {kind}");
        // Zone bands go wherever a Y annotation goes, and nowhere else.
        Check(Accepts(sample with{YZones=Effort()})==Accepts(sample with{Annotations=[new(AnnotationAxis.Y,1)]}),$"zone bands on {kind}");
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
        if(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Column) ChartSvg.Render(lined);
        else Check(Refusal(lined).Contains("own kind"),$"{kind}: {Refusal(lined)}");
    }
    // A series can be a line, area, column, scatter or band, and nothing else.
    foreach(var mark in Enum.GetValues<ChartKind>().Append((ChartKind)99))
    {
        var spec=Spec() with{Series=[new("S",[new(0,1),new(1,2)]){Kind=mark}]};
        if(mark is ChartKind.Line or ChartKind.Area or ChartKind.Column or ChartKind.Scatter or ChartKind.Band) ChartSvg.Render(spec);
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
        if(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Band or ChartKind.Candlestick or ChartKind.Ohlc)
        {
            ChartSvg.Render(below);
            Check(Refusal(pointed).Contains("pane is 0"),$"{kind}: {Refusal(pointed)}");
        }
        else Check(Refusal(below).Contains("Panes share")&&Refusal(pointed).Contains("Panes share")&&Refusal(Sample(kind) with{Panes=[new()]}).Contains("Panes share"),$"{kind}: {Refusal(below)}");
    }
    var spec=Stacked();
    Check(Refusal(spec with{Series=[..spec.Series,new("Lost",[new(0,1)]){Pane=3}]}).Contains("pane is 0"));
    Check(Refusal(spec with{Series=[..spec.Series,new("Lost",[new(0,1)]){Pane=-1}]}).Contains("pane is 0"));
    Check(Refusal(spec with{Series=[spec.Series[0],spec.Series[2]]}).Contains("pane 1 has none"));
    Check(Refusal(spec with{Series=spec.Series.Skip(1).ToArray()}).Contains("pane 0 has none"));
    Check(Refusal(spec with{Panes=[new(),new(),new(),new()]}).Contains("at most four panes")&&Refusal(spec with{Panes=null!}).Contains("at most four panes"));
    ChartSvg.Render(spec with{Panes=[..spec.Panes,new()],Series=[..spec.Series,new("Fourth",[new(0,1)]){Pane=3}]});
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
        if(kind is ChartKind.Line or ChartKind.Area or ChartKind.Scatter or ChartKind.Bubble or ChartKind.Column or ChartKind.Bar or ChartKind.StackedColumn or ChartKind.Candlestick or ChartKind.Band or ChartKind.Ohlc)
            Check(PaneClips(doc).Select(PaneSpan).SequenceEqual([(78d,344d)])&&doc.Descendants(ns+"g").Where(g=>g.Attribute("data-series") is not null).All(g=>(string?)g.Attribute("data-series")=="0"),$"{kind} is not one plot");
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
    foreach(var kind in new[]{ChartKind.Line,ChartKind.Area,ChartKind.Scatter,ChartKind.StackedColumn}) No(kind,s with{ValueLabels=true});
    No(ChartKind.Line,s with{Curve=(LineCurve)7});No(ChartKind.Area,s with{Fill=(AreaFill)3});No(ChartKind.Line,s with{Markers=(MarkerStyle)9});
    ColorStop[] two=[new(1,"#2E9B58"),new(3,"#DD4B45")];
    foreach(var kind in new[]{ChartKind.Scatter,ChartKind.Column,ChartKind.Band}) No(kind,s with{Gradient=two});
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
    Reject(()=>ChartSvg.Render(Spec() with{YTickLabels=(TickLabels)2}));
});
// 0.24.0: the refined finish is the default and the classic one is the exact way back. The rows below are renderings of the
// release baseline, hashed from 0.23.0's own output, so the classic finish must reproduce every byte of them.
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
Test("The classic finish draws 0.23.0's charts byte for byte, gradients and their IDs included, and the refined one does not",()=>{
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
        ("graph/Circular/Light","1DF550CDF523B601",c=>Network(graph with{Layout=GraphLayout.Circular,Theme=ChartTheme.Light},c)),
        ("graph/Layered/Dark","6E16348F752690A3",c=>Network(graph with{Layout=GraphLayout.Layered,Theme=ChartTheme.Dark},c)),
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
Test("Sports page: twelve charts, each rendering in light, dark and Midnight at a desktop's and a phone's widths",()=>{
    Check(sports.Count==12&&sports.Select(card=>card.Id).Distinct().Count()==12,"the page should have twelve charts");
    foreach(var (theme,style,zones) in new[]{(ChartTheme.Light,(ChartStyle?)null,ChartStyle.Light.Zones),(ChartTheme.Dark,null,ChartStyle.Light.Zones),(ChartTheme.Dark,ChartStyle.Midnight,ChartStyle.Midnight.Zones)})
        foreach(var markers in new[]{true,false})
            foreach(var card in SportsData.Cards(theme,zones,markers))
            {
                Check(card.Spec.Width==(card.Wide?1100:540)&&card.Spec.Source.Contains("simulated"),$"{card.Id} is not drawn at a desktop's width before it is fitted, or does not say it is simulated");
                // The page's charts set FitWidth, which draws each at the width its card gives it, as here.
                foreach(var width in new[]{card.Spec.Width,337})
                    Check(XDocument.Parse(ChartSvg.Render(card.Spec with{Width=width,Style=style})).Descendants().Any(e=>e.Attribute("data-point") is not null),$"{card.Id} drew no marks {width} wide");
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
Test("Sports page: each night's HRV falls where its series says, against the 28 nights before it",()=>{
    var hrv=Sports("hrv").Series;var band=hrv[0].Points;var nights=athlete.Hrv;
    Check(band.Count==SportsData.Weeks*7&&hrv.Skip(1).Sum(s=>s.Points.Count)==SportsData.Weeks*7&&nights.Count==SportsData.Weeks*7+SportsData.BaselineNights);
    for(var i=0;i<band.Count;i++)
    {
        var window=nights.Skip(i).Take(SportsData.BaselineNights).ToArray();var mean=window.Average();var deviation=Math.Sqrt(window.Sum(v=>(v-mean)*(v-mean))/(window.Length-1));
        Check(Math.Abs(band[i].Y!.Value-mean)<.051&&Math.Abs(band[i].Low!.Value-(mean-deviation))<.051&&Math.Abs(band[i].High!.Value-(mean+deviation))<.051,$"night {i}'s baseline is not the 28 nights before it");
    }
    ChartPoint BandAt(double x)=>band.Single(b=>b.X==x);
    Check(hrv[1].Points.All(p=>p.Y>=BandAt(p.X).Low&&p.Y<=BandAt(p.X).High)&&hrv[2].Points.All(p=>p.Y<BandAt(p.X).Low)&&hrv[3].Points.All(p=>p.Y>BandAt(p.X).High),"a night is in the wrong series");
    Check(hrv.Skip(1).All(s=>s.Points.Count>0),"a status has no nights");
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
    var changed=rows.Select(r=>(r.Row,r.Hash,Now:Hash16(Prerender(ChartElement(r.Spec),r.Cascaded).Replace("\r\n","\n")))).Where(r=>r.Now!=r.Hash).ToArray();
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
    // The only rule naming the class reaches the drawing of the chart that carries it, and nothing else.
    Check(css.Split(".lumen-fit").Length==2&&css.Contains(".lumen-fit>.lumen-viewport>svg{min-width:0}"),"the fitted chart's rule is missing or reaches further");
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
        .Concat(new[]{typeof(LumenChart),typeof(LumenGraph),typeof(LumenBrand)}.SelectMany(t=>t.GetProperties(declared)
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
    // 0.25.0 had no sweep: its hash of a spec is the JSON written today less the sweep, which is the last property written.
    var faded=Spec(ChartKind.Area) with{Series=[new("S",[new(0,1),new(1,3)]){Fill=AreaFill.Fade}]};
    string Prefix(string svg)=>System.Text.RegularExpressions.Regex.Match(svg,"id='(lumen-[0-9a-f]{12})-0'").Groups[1].Value;
    var json=System.Text.Json.JsonSerializer.Serialize(faded with{Style=ChartSvg.ResolveStyle(faded)},new System.Text.Json.JsonSerializerOptions{DefaultIgnoreCondition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull});
    Check(json.EndsWith(",\"GaugeSweep\":270}"),json[^60..]);
    var before="lumen-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json.Replace(",\"GaugeSweep\":270}","}"))))[..12].ToLowerInvariant();
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
Console.WriteLine($"\n{passed} passed; {failures.Count} failed.");
foreach(var failure in failures)Console.Error.WriteLine(failure);
return failures.Count==0?0:1;

sealed class NoJs:IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Prerender must not invoke JS.");
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken token,object?[]? args)=>InvokeAsync<TValue>(identifier,args);
}
