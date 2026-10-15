using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Rivet;

namespace Reel.Server.Pairing;

public static class ConnectEndpoints
{
    public static IEndpointRouteBuilder MapConnectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            ReelContract.ConnectRoute,
            ([FromBody] ConnectRequest request, IOptions<ReelOptions> options) =>
            {
                var endpoint = ReelContract.Connect.Bind(request);
                if (request.Key != options.Value.SecretKey)
                {
                    return endpoint
                        .Error(
                            StatusCodes.Status401Unauthorized,
                            new ErrorResponse("unauthorized", "That code doesn't match your TV.")
                        )
                        .ToResult();
                }

                return endpoint.Success(new ConnectResponse(Environment.MachineName)).ToResult();
            }
        );

        return endpoints;
    }
}
