namespace Reel.Server.Endpoints;

using Reel.Server.Services;

public static class ThumbnailEndpoints
{
    public static WebApplication MapThumbnailEndpoints(this WebApplication app)
    {
        app.MapGet("/api/files/thumbnail", async (string? path, ReelConfig config, ThumbnailService thumbnailService) =>
        {
            if (string.IsNullOrWhiteSpace(path))
                return Results.BadRequest(new { error = "Missing required query parameter: path" });

            var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, path));

            if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                return Results.Problem(detail: "Access denied.", statusCode: StatusCodes.Status403Forbidden);

            if (!File.Exists(fullPath))
                return Results.NotFound(new { error = "File not found" });

            var thumbnailPath = await thumbnailService.GetOrCreateThumbnail(fullPath, path);

            if (thumbnailPath is null)
                return Results.Problem(
                    detail: "Failed to generate thumbnail. Ensure ffmpeg is installed.",
                    statusCode: StatusCodes.Status500InternalServerError);

            return Results.File(thumbnailPath, "image/jpeg");
        });

        return app;
    }
}
