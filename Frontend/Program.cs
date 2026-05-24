using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseHttpMetrics();
app.UseStaticFiles();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "frontend" }));
app.MapMetrics();

app.Run();
