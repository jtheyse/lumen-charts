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
    ChartKind.Candlestick or ChartKind.Ohlc=>Spec(kind) with{Series=[new("Price",[ChartPoint.Candle(0,10,12,9,11),ChartPoint.Candle(1,11,13,10,10.5),ChartPoint.Candle(2,10.5,11,8,9)])]},
    ChartKind.Band=>Spec(kind) with{Series=[new("Forecast",[ChartPoint.Interval(0,2,1,3),ChartPoint.Interval(1,5,4,6),ChartPoint.Interval(2,3,2,4)])]},
    ChartKind.Histogram or ChartKind.Box or ChartKind.Violin=>Spec(kind) with{Series=[new("Sample",Enumerable.Range(0,40).Select(i=>new ChartPoint(i,i%7+1)).ToArray())]},
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
    var spec=Spec(ChartKind.Scatter) with{Series=[new("Rising",Enumerable.Range(0,10).Select(i=>new ChartPoint(i,i*2+1)).ToArray()){Trend=true}]};
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
    var spec=Spec(ChartKind.Scatter) with{YAxis=AxisKind.Log,
        Series=[new("Growth",Enumerable.Range(1,5).Select(i=>new ChartPoint(i,Math.Pow(10,i))).ToArray()){Trend=true}]};
    var d=(string)Svg(spec).Descendants(ns+"path").Single(e=>(string?)e.Attribute("class")=="lumen-trend").Attribute("d")!;
    var ends=d.Split(' ').Select(part=>part.TrimStart('M','L').Split(',').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray()).ToArray();
    var marks=Svg(spec).Descendants(ns+"circle").Select(c=>(X:(double)c.Attribute("cx")!,Y:(double)c.Attribute("cy")!)).ToArray();
    var slope=(ends[1][1]-ends[0][1])/(ends[1][0]-ends[0][0]);
    Check(marks.All(m=>Math.Abs(ends[0][1]+slope*(m.X-ends[0][0])-m.Y)<.5),"the log fit missed its own points");
});
Test("A compressed axis takes one straight trend, not a jump per weekend",()=>{
    var days=TradingDays(15);
    var spec=Spec() with{Kind=ChartKind.Scatter,XAxis=AxisKind.Time,SkipWeekends=true,
        Series=[new("Close",days.Select((v,i)=>new ChartPoint(v,100+i)).ToArray()){Trend=true}]};
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
Console.WriteLine($"\n{passed} passed; {failures.Count} failed.");
foreach(var failure in failures)Console.Error.WriteLine(failure);
return failures.Count==0?0:1;

sealed class NoJs:IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Prerender must not invoke JS.");
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken token,object?[]? args)=>InvokeAsync<TValue>(identifier,args);
}
