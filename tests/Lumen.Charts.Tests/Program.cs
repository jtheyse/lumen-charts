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
    var branded=RenderInside(Brand(),Spec());
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
    var doc=Svg(Marked(new ChartAnnotation(AnnotationAxis.Y,25){Label="Target"}));
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
    var doc=Svg(Spec() with{XAxis=AxisKind.Time,Annotations=[new(AnnotationAxis.X,start+86400000d*15){Label="Launch"}],
        Series=[new("S",Enumerable.Range(0,30).Select(i=>new ChartPoint(start+i*86400000d,i)).ToArray())]});
    var group=Annotation(doc);
    Check(group.Attribute("aria-label")!.Value=="Launch: 16 Jan 2026",group.Attribute("aria-label")!.Value);
    var line=group.Elements(ns+"line").Last();
    Check(line.Attribute("x1")!.Value==line.Attribute("x2")!.Value,"an X reference is not vertical");
});
Test("A band covers the range it names",()=>{
    var doc=Svg(Marked(new ChartAnnotation(AnnotationAxis.Y,15){To=35,Label="Acceptable"}));
    var group=Annotation(doc);
    Check(group.Attribute("aria-label")!.Value=="Acceptable: 15 to 35");
    var rect=group.Element(ns+"rect")!;
    Check((string?)rect.Attribute("fill-opacity")==".12");
    var height=double.Parse(rect.Attribute("height")!.Value,CultureInfo.InvariantCulture);
    Check(height>50,$"the band is only {height} tall");
});
Test("Annotations render behind the data and inside the plot's clip",()=>{
    var markup=ChartSvg.Render(Marked(new ChartAnnotation(AnnotationAxis.Y,25)));
    var clip=markup.IndexOf("overflow='hidden'");
    var annotation=markup.IndexOf("stroke-dasharray");
    var firstMark=markup.IndexOf("data-point=");
    Check(clip<annotation&&annotation<firstMark,"an annotation is outside the clip or drawn over the data");
});
Test("An annotation takes the style's muted colour unless it names one",()=>{
    var branded=Svg(Marked(new ChartAnnotation(AnnotationAxis.Y,25)) with{Style=ChartStyle.Light with{Muted="#4B5563"}});
    Check((string?)Annotation(branded).Elements(ns+"line").Last().Attribute("stroke")=="#4B5563");
    var own=Svg(Marked(new ChartAnnotation(AnnotationAxis.Y,25){Color="#B03A2E",Dashed=false}));
    var line=Annotation(own).Elements(ns+"line").Last();
    Check((string?)line.Attribute("stroke")=="#B03A2E"&&line.Attribute("stroke-dasharray") is null);
});
Test("An annotation without a label still reads as a value",()=>{
    Check(Annotation(Svg(Marked(new ChartAnnotation(AnnotationAxis.Y,25)))).Attribute("aria-label")!.Value=="25");
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
    var doc=Svg(Spec(ChartKind.Column) with{Annotations=[new(AnnotationAxis.Y,4){Label="Budget"}]});
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
Console.WriteLine($"\n{passed} passed; {failures.Count} failed.");
foreach(var failure in failures)Console.Error.WriteLine(failure);
return failures.Count==0?0:1;

sealed class NoJs:IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Prerender must not invoke JS.");
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken token,object?[]? args)=>InvokeAsync<TValue>(identifier,args);
}
