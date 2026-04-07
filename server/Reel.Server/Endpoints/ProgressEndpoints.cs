namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Models;
using Reel.Server.Services;

public static class ProgressEndpoints
{
    public static WebApplication MapProgressEndpoints(this WebApplication app)
    {
        app.MapPost("/api/progress", ([FromBody] ProgressRequest request, ProgressService progressService) =>
        {
            progressService.UpdateProgress(request.RelativePath, request.PositionSeconds, request.DurationSeconds);
            return Results.Ok();
        });

        app.MapGet("/api/progress", ([FromQuery] string? path, ProgressService progressService) =>
        {
            if (path is not null)
            {
                var entry = progressService.Get(path);
                return entry is not null
                    ? Results.Ok(entry)
                    : Results.NotFound();
            }

            return Results.Ok(progressService.GetAll());
        });

        return app;
    }
}
