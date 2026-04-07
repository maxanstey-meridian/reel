namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;

public static class ProgressEndpoints
{
    public static WebApplication MapProgressEndpoints(this WebApplication app)
    {
        app.MapPost("/api/progress", async (HttpContext context, [FromBody] ProgressRequest request, IProgressStore store, CancellationToken ct) =>
        {
            var userId = (int)context.Items["UserId"]!;
            await store.UpdateAsync(userId, request.RelativePath, request.PositionSeconds, request.DurationSeconds, ct);
            return Results.Ok();
        });

        app.MapGet("/api/progress", async (HttpContext context, IProgressStore store, CancellationToken ct) =>
        {
            var userId = (int)context.Items["UserId"]!;
            return Results.Ok(await store.GetAllAsync(userId, ct));
        });

        return app;
    }
}
