using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lumen.Charts.AspNetCore;

/// <summary>The chart HTTP API.</summary>
public static class ChartEndpoints
{
    /// <summary>
    /// Maps the chart API under <paramref name="prefix"/>: <c>GET types</c> lists the chart kinds; <c>POST svg</c> and
    /// <c>POST csv</c> take a <see cref="ChartSpec"/> as JSON and answer with its SVG or its data; <c>POST graph/svg</c>,
    /// <c>graph/layout</c> and <c>graph/routes</c> take a <see cref="GraphSpec"/>; <c>POST planner/svg</c> takes a <see cref="PlannerRequest"/>. A spec that breaks a rule is answered with
    /// 400 and the rule's message. Returns the group, so the host can add authorization or rate limiting to it.
    /// </summary>
    public static RouteGroupBuilder MapLumenCharts(this IEndpointRouteBuilder endpoints, string prefix = "/api/charts")
    {
        var group=endpoints.MapGroup(prefix);
        group.MapGet("/types",()=>Enum.GetNames<ChartKind>());
        group.MapPost("/svg",(ChartSpec spec)=>Render(()=>ChartSvg.Render(spec),"image/svg+xml"));
        group.MapPost("/csv",(ChartSpec spec)=>Render(()=>ChartExport.Csv(spec),"text/csv"));
        group.MapPost("/graph/svg",(GraphSpec spec)=>Render(()=>GraphEngine.Render(spec),"image/svg+xml"));
        group.MapPost("/graph/layout",(GraphSpec spec)=>Json(()=>GraphEngine.Layout(spec)));
        group.MapPost("/graph/routes",(GraphSpec spec)=>Json(()=>GraphEngine.Routes(spec)));
        group.MapPost("/planner/svg",(PlannerRequest request)=>Render(()=>PlannerSvg.Render(request.Spec,request.View??PlannerView.WholePeriod,request.Layout),"image/svg+xml"));
        return group;
    }
    private static IResult Json<T>(Func<T> value)
    {
        try { return Results.Ok(value()); }
        catch(ArgumentException error) { return Results.Problem(error.Message,statusCode:400,title:"Invalid graph"); }
    }
    private static IResult Render(Func<string> render,string type)
    {
        try { return Results.Text(render(),type); }
        catch(ArgumentException error) { return Results.Problem(error.Message,statusCode:400,title:"Invalid chart"); }
    }
}
