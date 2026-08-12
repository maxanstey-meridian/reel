namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.FileSystem;
using Rivet;

public static class TrickplayEndpoints
{
    public static WebApplication MapTrickplayEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.TrickplayInfo.Route,
            async (
                [FromQuery] string? path,
                ITrickplayGenerator generator,
                IMediaStore mediaStore,
                MediaPathResolver pathResolver,
                CancellationToken ct
            ) =>
            {
                var request = new FileRequest(path ?? "");
                var endpoint = ReelContract.TrickplayInfo.Bind(request);
                if (string.IsNullOrWhiteSpace(request.Path))
                    throw new BadRequestException("Missing required query parameter: path");

                if (pathResolver.ResolvePlayableFile(request.Path) is null)
                {
                    if (pathResolver.IsSafeRelativePath(request.Path))
                        throw new NotFoundException("File not found.");
                    throw new ForbiddenException("Access denied.");
                }
                if (await mediaStore.GetAsync(request.Path, ct) is null)
                    throw new NotFoundException("Indexed media not found.");

                var metadata = await generator.GetOrGenerateAsync(request.Path, ct);
                if (metadata is null)
                    throw new InvalidOperationException(
                        "Failed to generate trickplay data. Ensure ffmpeg is installed."
                    );

                return endpoint.Success(metadata).ToResult();
            }
        );

        app.MapGet(
            ReelContract.TrickplaySprite.Route,
            async (
                [FromQuery] string? path,
                [FromQuery] int? sheet,
                IMediaStore mediaStore,
                ITrickplayGenerator generator,
                ReelConfig config,
                MediaPathResolver pathResolver,
                CancellationToken ct
            ) =>
            {
                var endpoint = ReelContract.TrickplaySprite.Bind(
                    new TrickplaySpriteRequest(path ?? "", sheet ?? 0)
                );
                if (string.IsNullOrWhiteSpace(path))
                    return endpoint.Error(400, new ErrorResponse("Missing required query parameter: path")).ToResult();

                var sheetIndex = sheet ?? 0;

                if (pathResolver.ResolvePlayableFile(path) is null)
                    return endpoint.Error(403).ToResult();
                if (await mediaStore.GetAsync(path, ct) is null)
                    return endpoint.Error(404, new ErrorResponse("Indexed media not found.")).ToResult();

                var metadata = await mediaStore.GetTrickplayAsync(path, ct);
                var fullPath = pathResolver.ResolvePlayableFile(path)!;
                var expectedKey = Infrastructure.Ffmpeg.FfmpegTrickplayGenerator.ComputeCacheKey(path, fullPath);
                if (metadata is null || metadata.SpritePath != expectedKey)
                    metadata = await generator.GetOrGenerateAsync(path, ct);
                if (metadata is null || metadata.SpritePath != expectedKey)
                    return endpoint
                        .Error(404, new ErrorResponse("Trickplay data not found. Request metadata first."))
                        .ToResult();

                var sheetCount = (int)Math.Ceiling(
                    (double)metadata.TotalThumbs / (metadata.Columns * metadata.Rows)
                );
                if (sheetIndex < 0 || sheetIndex >= sheetCount)
                    return endpoint
                        .Error(404, new ErrorResponse($"Sheet index out of range. Valid range: 0-{sheetCount - 1}"))
                        .ToResult();

                var cacheDir = Path.Combine(config.MediaRoot, ".reel", "trickplay");
                var spritePath = Path.Combine(cacheDir, $"{metadata.SpritePath}_{sheetIndex}.jpg");

                if (!File.Exists(spritePath))
                    return endpoint.Error(404, new ErrorResponse("Sprite sheet file not found.")).ToResult();

                return endpoint.File(spritePath).ToResult();
            }
        );

        return app;
    }
}
