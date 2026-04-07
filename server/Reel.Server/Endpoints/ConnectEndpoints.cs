namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Infrastructure.Configuration;

public static class ConnectEndpoints
{
    public static WebApplication MapConnectEndpoints(this WebApplication app)
    {
        app.MapPost(
            ReelContract.Connect.Route,
            async ([FromBody] ConnectRequest request, ReelConfig config) =>
                (
                    await ReelContract.Connect.Invoke(
                        request,
                        req =>
                        {
                            if (req.Key != config.SecretKey)
                                throw new UnauthorizedException("Invalid key", new ConnectResponse("unauthorized"));

                            return Task.FromResult(new ConnectResponse("ok", Environment.MachineName));
                        }
                    )
                ).ToResult()
        );

        return app;
    }
}
