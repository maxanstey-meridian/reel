namespace Reel.Server.Endpoints;

using HeyRed.Mime;
using Reel.Server.Services;

public static class StreamEndpoints
{
    public static WebApplication MapStreamEndpoints(this WebApplication app)
    {
        app.MapGet("/api/files/stream", (string? path, ReelConfig config) =>
        {
            if (string.IsNullOrWhiteSpace(path))
                return Results.BadRequest(new { error = "Missing required query parameter: path" });

            var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, path));

            if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (!File.Exists(fullPath))
                return Results.NotFound(new { error = "File not found" });

            var contentType = MimeTypesMap.GetMimeType(Path.GetExtension(fullPath));

            return Results.File(fullPath, contentType, enableRangeProcessing: true);
        });

        return app;
    }
}
