using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lumen.Charts.AspNetCore;

public static class ChartEndpoints
{
    public static RouteGroupBuilder MapLumenCharts(this IEndpointRouteBuilder endpoints, string prefix = "/api/charts")
    {
        var group=endpoints.MapGroup(prefix);
        group.MapGet("/types",()=>Enum.GetNames<ChartKind>());
        group.MapPost("/svg",(ChartSpec spec)=>Render(()=>ChartSvg.Render(spec),"image/svg+xml"));
        group.MapPost("/csv",(ChartSpec spec)=>Render(()=>ChartExport.Csv(spec),"text/csv"));
        group.MapPost("/graph/svg",(GraphSpec spec)=>Render(()=>GraphEngine.Render(spec),"image/svg+xml"));
        group.MapPost("/graph/layout",(GraphSpec spec)=>Json(()=>GraphEngine.Layout(spec)));
        group.MapPost("/graph/routes",(GraphSpec spec)=>Json(()=>GraphEngine.Routes(spec)));
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
