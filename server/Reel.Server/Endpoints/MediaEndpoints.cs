namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Extensions;
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

                            var userId = context.GetUserId();
                            var record = await mediaStore.GetAsync(req.Path, ct);

                            if (record is null)
                                throw new NotFoundException("Media not found");

                            var watched = await mediaStore.IsWatchedAsync(userId, req.Path, ct);
                            var progress = await progressStore.GetAsync(userId, req.Path, ct);
                            var tags = await mediaStore.GetTagsAsync(userId, req.Path, ct);

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
                                Tags: tags
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
                    await ReelContract.MediaUpdate.Invoke(
                        request,
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, path));
                            if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                                throw new ForbiddenException("Access denied.");

                            await mediaStore.UpdateMetaAsync(path, req, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapPut(
            ReelContract.MediaSetWatched.Route,
            async (
                HttpContext context,
                [FromQuery] string? path,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.MediaSetWatched.Invoke(
                        new FileRequest(path ?? ""),
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(req.Path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var userId = context.GetUserId();
                            await mediaStore.SetWatchStateAsync(userId, req.Path, WatchState.Watched, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapPut(
            ReelContract.MediaSetUnwatched.Route,
            async (
                HttpContext context,
                [FromQuery] string? path,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.MediaSetUnwatched.Invoke(
                        new FileRequest(path ?? ""),
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(req.Path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var userId = context.GetUserId();
                            await mediaStore.SetWatchStateAsync(userId, req.Path, WatchState.Unwatched, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapDelete(
            ReelContract.MediaClearWatched.Route,
            async (
                HttpContext context,
                [FromQuery] string? path,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.MediaClearWatched.Invoke(
                        new FileRequest(path ?? ""),
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(req.Path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var userId = context.GetUserId();
                            await mediaStore.SetWatchStateAsync(userId, req.Path, null, ct);
                        }
                    )
                ).ToResult()
        );

        // Tag endpoints
        app.MapGet(
            ReelContract.TagsList.Route,
            async (
                HttpContext context,
                [FromQuery] string? path,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.TagsList.Invoke(
                        new FileRequest(path ?? ""),
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(req.Path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var userId = context.GetUserId();
                            var tags = await mediaStore.GetTagsAsync(userId, req.Path, ct);
                            return tags.ToArray();
                        }
                    )
                ).ToResult()
        );

        app.MapPut(
            ReelContract.TagsReplace.Route,
            async (
                HttpContext context,
                [FromQuery] string? path,
                [FromBody] SetTagsRequest request,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.TagsReplace.Invoke(
                        request,
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var userId = context.GetUserId();
                            await mediaStore.SetTagsAsync(userId, path, req.Tags, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapPost(
            ReelContract.TagsAdd.Route,
            async (
                HttpContext context,
                [FromQuery] string? path,
                [FromBody] AddTagRequest request,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.TagsAdd.Invoke(
                        request,
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var userId = context.GetUserId();
                            await mediaStore.AddTagAsync(userId, path, req.Tag, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapDelete(
            ReelContract.TagsRemove.Route,
            async (
                HttpContext context,
                [FromQuery] string? path,
                [FromQuery] string? tag,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.TagsRemove.Invoke(
                        new RemoveTagRequest(path ?? "", tag ?? ""),
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(req.Path))
                                throw new BadRequestException("Missing required query parameter: path");
                            if (string.IsNullOrWhiteSpace(req.Tag))
                                throw new BadRequestException("Missing required query parameter: tag");

                            var userId = context.GetUserId();
                            await mediaStore.RemoveTagAsync(userId, req.Path, req.Tag, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapGet(
            ReelContract.TagsAll.Route,
            async (HttpContext context, IMediaStore mediaStore, CancellationToken ct) =>
                (
                    await ReelContract.TagsAll.Invoke(async () =>
                    {
                        var userId = context.GetUserId();
                        var tags = await mediaStore.GetAllTagsAsync(userId, ct);
                        return tags.ToArray();
                    })
                ).ToResult()
        );

        return app;
    }
}
