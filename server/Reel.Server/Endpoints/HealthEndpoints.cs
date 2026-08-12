namespace Reel.Server.Endpoints;

using System.Diagnostics;
using Reel.Server.Application.Models;
using Rivet;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.Health.Route,
            () =>
            {
                var elapsed = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                var uptime =
                    $"{(int)elapsed.TotalDays}d {elapsed.Hours:D2}h {elapsed.Minutes:D2}m {elapsed.Seconds:D2}s";
                return ReelContract.Health.Success(new HealthResponse("healthy", uptime)).ToResult();
            }
        );

        return app;
    }
}
