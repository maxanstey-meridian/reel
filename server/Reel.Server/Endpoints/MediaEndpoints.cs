namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Infrastructure.Configuration;

public static class MediaEndpoints
{
    public static WebApplication MapMediaEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.MediaDetail.Route,
            async (
                HttpContext context,
                [FromQuery] string? path,
                IMediaStore mediaStore,
                IProgressStore progressStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.MediaDetail.Invoke(
                        new FileRequest(path ?? ""),
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(req.Path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var userId = (int)context.Items["UserId"]!;
                            var record = await mediaStore.GetAsync(req.Path, ct);

                            if (record is null)
                                throw new NotFoundException("Media not found");

                            var watched = await mediaStore.IsWatchedAsync(userId, req.Path, ct);
                            var progress = await progressStore.GetAsync(userId, req.Path, ct);

                            return new MediaDetailResponse(
                                record.RelativePath,
                                record.ParsedTitle,
                                record.Year,
                                record.Genre,
                                record.Rating,
                                record.Language,
                                record.Notes,
                                record.DisplayTitle,
                                record.TmdbId,
                                record.PosterUrl,
                                record.BackdropUrl,
                                record.Overview,
                                record.ReleaseDate,
                                record.AddedAt,
                                record.UpdatedAt,
                                watched,
                                progress?.PositionSeconds,
                                progress?.DurationSeconds,
                                Tags: []
                            );
                        }
                    )
                ).ToResult()
        );

        app.MapMethods(
            ReelContract.MediaUpdate.Route,
            ["PATCH"],
            async (
                HttpContext context,
                [FromQuery] string? path,
                [FromBody] MediaUpdateRequest request,
                IMediaStore mediaStore,
                ReelConfig config,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.MediaUpdate.Invoke(request, async req =>
                    {
                        if (string.IsNullOrWhiteSpace(path))
                            throw new BadRequestException("Missing required query parameter: path");

                        var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, path));
                        if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                            throw new ForbiddenException("Access denied.");

                        await mediaStore.UpdateMetaAsync(path, req, ct);
                    })
                ).ToResult()
        );

        app.MapPut(
            ReelContract.MediaSetWatched.Route,
            async (
                HttpContext context,
                [Microsoft.AspNetCore.Mvc.FromQuery] string path,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.MediaSetWatched.Invoke(async () =>
                    {
                        var userId = (int)context.Items["UserId"]!;
                        await mediaStore.SetWatchStateAsync(userId, path, "watched", ct);
                    })
                ).ToResult()
        );

        app.MapPut(
            ReelContract.MediaSetUnwatched.Route,
            async (
                HttpContext context,
                [Microsoft.AspNetCore.Mvc.FromQuery] string path,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.MediaSetUnwatched.Invoke(async () =>
                    {
                        var userId = (int)context.Items["UserId"]!;
                        await mediaStore.SetWatchStateAsync(userId, path, "unwatched", ct);
                    })
                ).ToResult()
        );

        app.MapDelete(
            ReelContract.MediaClearWatched.Route,
            async (
                HttpContext context,
                [Microsoft.AspNetCore.Mvc.FromQuery] string path,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.MediaClearWatched.Invoke(async () =>
                    {
                        var userId = (int)context.Items["UserId"]!;
                        await mediaStore.SetWatchStateAsync(userId, path, null, ct);
                    })
                ).ToResult()
        );

        return app;
    }
}
