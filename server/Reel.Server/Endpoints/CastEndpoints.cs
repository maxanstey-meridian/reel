namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Models;
using Reel.Server.Services;

public static class CastEndpoints
{
    public static WebApplication MapCastEndpoints(this WebApplication app)
    {
        app.MapPost("/api/cast", ([FromBody] CastRequest request, CastService castService) =>
        {
            castService.SetPlay(request.RelativePath);
            return Results.NoContent();
        });

        app.MapGet("/api/cast/current", (CastService castService) =>
        {
            var command = castService.GetPending();
            return command is not null
                ? Results.Ok(new CastStatus(command))
                : Results.NoContent();
        });

        app.MapPost("/api/cast/ack", (CastService castService) =>
        {
            castService.Acknowledge();
            return Results.NoContent();
        });

        app.MapPost("/api/cast/stop", (CastService castService) =>
        {
            castService.SetStop();
            return Results.NoContent();
        });

        return app;
    }
}
