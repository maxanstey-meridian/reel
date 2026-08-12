namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Infrastructure.Configuration;
using Rivet;

public static class ConnectEndpoints
{
    public static WebApplication MapConnectEndpoints(this WebApplication app)
    {
        app.MapPost(
            ReelContract.Connect.Route,
            async ([FromBody] ConnectRequest request, ReelConfig config) =>
            {
                var endpoint = ReelContract.Connect.Bind(request);
                if (request.Key != config.SecretKey)
                    return endpoint
                        .Error(StatusCodes.Status401Unauthorized, new ConnectResponse("unauthorized"))
                        .ToResult();

                await Task.CompletedTask;
                return endpoint.Success(new ConnectResponse("ok", Environment.MachineName)).ToResult();
            }
        );

        return app;
    }
}
