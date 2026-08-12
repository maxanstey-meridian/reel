namespace Reel.Server.Endpoints;

using Reel.Server.Infrastructure.Configuration;

public static class PairEndpoints
{
    public static WebApplication MapPairEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.PairRoute,
            (HttpContext context, ReelConfig config) =>
            {
                var key = context.Request.Query["key"].FirstOrDefault() ?? "";
                if (!string.Equals(key, config.SecretKey, StringComparison.Ordinal))
                    return Results.Unauthorized();

                if (string.IsNullOrWhiteSpace(config.WebUrl) || string.IsNullOrWhiteSpace(config.ServerUrl))
                    return Results.Json(
                        new ErrorResponse("Reel:WebUrl and Reel:ServerUrl are required for pairing."),
                        statusCode: 503
                    );

                var serverUrl = config.ServerUrl.TrimEnd('/');
                var webUrl = config.WebUrl.TrimEnd('/');
                var webLink = $"{webUrl}/connect?url={Uri.EscapeDataString(serverUrl)}&key={Uri.EscapeDataString(key)}";
                return Results.Redirect(webLink);
            }
        );

        return app;
    }
}
