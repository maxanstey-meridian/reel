namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;

public static class ProgressEndpoints
{
    public static WebApplication MapProgressEndpoints(this WebApplication app)
    {
        app.MapPost("/api/progress", async ([FromBody] ProgressRequest request, IProgressStore store, CancellationToken ct) =>
        {
            await store.UpdateAsync(request.RelativePath, request.PositionSeconds, request.DurationSeconds, ct);
            return Results.Ok();
        });

        app.MapGet("/api/progress", async ([FromQuery] string? path, IProgressStore store, CancellationToken ct) =>
        {
            if (path is not null)
            {
                var entry = await store.GetAsync(path, ct);
                return entry is not null
                    ? Results.Ok(entry)
                    : Results.NotFound();
            }

            return Results.Ok(await store.GetAllAsync(ct));
        });

        return app;
    }
}
