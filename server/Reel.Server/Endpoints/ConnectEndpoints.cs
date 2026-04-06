namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Models;
using Reel.Server.Services;

public static class ConnectEndpoints
{
    public static WebApplication MapConnectEndpoints(this WebApplication app)
    {
        app.MapPost("/api/connect", ([FromBody] ConnectRequest request, ReelConfig config) =>
        {
            if (request.Key == config.SecretKey)
            {
                return Results.Ok(new ConnectResponse("ok", Environment.MachineName));
            }

            return Results.Json(new ConnectResponse("unauthorized"), statusCode: StatusCodes.Status401Unauthorized);
        });

        return app;
    }
}
