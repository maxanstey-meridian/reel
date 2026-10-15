using Microsoft.Extensions.Options;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;

namespace Reel.Server.Pairing;

public static class PairEndpoints
{
    // The TV's QR code opens this on the phone, which lands on the phone app served from the same origin as the API.
    public static IEndpointRouteBuilder MapPairEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ReelContract.PairRoute,
            (HttpContext context, IOptions<ReelOptions> options) =>
            {
                var config = options.Value;
                var key = context.Request.Query["key"].FirstOrDefault() ?? "";
                if (!string.Equals(key, config.SecretKey, StringComparison.Ordinal))
                {
                    return Results.Text(
                        "This Reel pairing link is out of date. Scan the QR code currently shown on your TV again.",
                        "text/plain; charset=utf-8",
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                if (string.IsNullOrWhiteSpace(config.WebUrl))
                {
                    return Results.Json(
                        new ErrorResponse(
                            "pairing_not_configured",
                            "Reel:WebUrl is required for pairing."
                        ),
                        statusCode: StatusCodes.Status503ServiceUnavailable
                    );
                }

                return Results.Redirect(
                    $"{config.WebUrl.TrimEnd('/')}/connect?key={Uri.EscapeDataString(key)}"
                );
            }
        );

        return endpoints;
    }
}
