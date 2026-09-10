using System.Text.Json.Serialization;
using Lumen.Charts.AspNetCore;
using Lumen.Gallery.Components;

var builder=WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment()) builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.ConfigureHttpJsonOptions(options=>options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.WebHost.ConfigureKestrel(options=>options.Limits.MaxRequestBodySize=16*1024*1024);
var app=builder.Build();
app.MapStaticAssets();
app.UseAntiforgery();
app.MapGet("/health",()=>Results.Ok(new { status="healthy" }));
app.MapLumenCharts();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

