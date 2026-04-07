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

        app.MapGet("/api/progress", async (IProgressStore store, CancellationToken ct) =>
        {
            return Results.Ok(await store.GetAllAsync(ct));
        });

        return app;
    }
}
