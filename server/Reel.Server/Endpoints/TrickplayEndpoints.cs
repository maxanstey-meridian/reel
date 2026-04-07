namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Infrastructure.Configuration;

public static class TrickplayEndpoints
{
    public static WebApplication MapTrickplayEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.TrickplayInfo.Route,
            async (
                [FromQuery] string? path,
                ITrickplayGenerator generator,
                ReelConfig config,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.TrickplayInfo.Invoke(
                        new FileRequest(path ?? ""),
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(req.Path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, req.Path));
                            if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                                throw new ForbiddenException("Access denied.");

                            if (!File.Exists(fullPath))
                                throw new NotFoundException("File not found.");

                            var metadata = await generator.GetOrGenerateAsync(req.Path, ct);
                            if (metadata is null)
                                throw new InvalidOperationException(
                                    "Failed to generate trickplay data. Ensure ffmpeg is installed."
                                );

                            return metadata;
                        }
                    )
                ).ToResult()
        );

        app.MapGet(
            ReelContract.TrickplaySprite.Route,
            async (
                [FromQuery] string? path,
                [FromQuery] int? sheet,
                IMediaStore mediaStore,
                ReelConfig config,
                CancellationToken ct
            ) =>
            {
                if (string.IsNullOrWhiteSpace(path))
                    return Results.BadRequest(new { error = "Missing required query parameter: path" });

                var sheetIndex = sheet ?? 0;

                var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, path));
                if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                    return Results.StatusCode(403);

                var metadata = await mediaStore.GetTrickplayAsync(path, ct);
                if (metadata is null)
                    return Results.NotFound(new { error = "Trickplay data not found. Request metadata first." });

                var sheetCount = (int)Math.Ceiling(
                    (double)metadata.TotalThumbs / (metadata.Columns * metadata.Rows)
                );
                if (sheetIndex < 0 || sheetIndex >= sheetCount)
                    return Results.NotFound(new { error = $"Sheet index out of range. Valid range: 0-{sheetCount - 1}" });

                var cacheDir = Path.Combine(config.MediaRoot, ".reel", "trickplay");
                var spritePath = Path.Combine(cacheDir, $"{metadata.SpritePath}_{sheetIndex}.jpg");

                if (!File.Exists(spritePath))
                    return Results.NotFound(new { error = "Sprite sheet file not found." });

                return Results.File(spritePath, "image/jpeg");
            }
        );

        return app;
    }
}
