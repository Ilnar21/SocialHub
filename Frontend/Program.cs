using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseHttpMetrics();
app.UseStaticFiles();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "frontend" }));
app.MapMetrics();

app.Run();
