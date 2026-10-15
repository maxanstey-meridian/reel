using Microsoft.Extensions.Options;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;

namespace Reel.Server.Web;

public sealed class SecretKeyMiddleware(RequestDelegate next, IOptions<ReelOptions> options)
{
    private static readonly string[] ExcludedPaths =
    [
        ReelContract.ConnectRoute,
        ReelContract.HealthRoute,
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        // JSON calls send the header. Media elements (<img>, <video>, ExoPlayer) cannot, so they pass
        // ?token=; the key is advertised over mDNS anyway, and PrivateNetworkMiddleware is the boundary.
        var key =
            request.Headers["X-Secret-Key"].FirstOrDefault()
            ?? request.Query["token"].FirstOrDefault();
        if (
            !request.Path.StartsWithSegments("/api")
            || ExcludedPaths.Any(path =>
                request.Path.Equals(path, StringComparison.OrdinalIgnoreCase)
            )
            || key == options.Value.SecretKey
        )
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(
            new ErrorResponse("unauthorized", "Missing or invalid secret key.")
        );
    }
}
