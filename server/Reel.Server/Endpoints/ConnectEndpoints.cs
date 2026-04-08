namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Infrastructure.Configuration;

public static class ConnectEndpoints
{
    public static WebApplication MapConnectEndpoints(this WebApplication app)
    {
        app.MapPost(
            ReelContract.Connect.Route,
            async ([FromBody] ConnectRequest request, ReelConfig config) =>
            {
                if (request.Key != config.SecretKey)
                    return Results.Json(new ConnectResponse("unauthorized"), statusCode: StatusCodes.Status401Unauthorized);

                return (
                    await ReelContract.Connect.Invoke(
                        request,
                        req => Task.FromResult(new ConnectResponse("ok", Environment.MachineName))
                    )
                ).ToResult();
            }
        );

        return app;
    }
}
