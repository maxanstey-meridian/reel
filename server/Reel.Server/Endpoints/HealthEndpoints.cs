namespace Reel.Server.Endpoints;

using System.Diagnostics;
using Reel.Server.Models;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", () =>
        {
            var elapsed = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            var uptime = $"{(int)elapsed.TotalDays}d {elapsed.Hours:D2}h {elapsed.Minutes:D2}m {elapsed.Seconds:D2}s";

            return Results.Ok(new HealthResponse("healthy", uptime));
        });

        return app;
    }
}
