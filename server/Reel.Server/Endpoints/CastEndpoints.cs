namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Extensions;

public static class CastEndpoints
{
    public static WebApplication MapCastEndpoints(this WebApplication app)
    {
        app.MapPost(
            ReelContract.Cast.Route,
            async (
                HttpContext context,
                [FromBody] CastRequest request,
                CastService castService,
                IProgressStore progressStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.Cast.Invoke(
                        request,
                        async req =>
                        {
                            var position = req.PositionSeconds;
                            if (position is null)
                            {
                                var userId = context.GetUserId();
                                var progress = await progressStore.GetAsync(userId, req.RelativePath, ct);
                                position = progress?.PositionSeconds;
                            }
                            castService.SetPlay(req.RelativePath, position, req.SubtitleIndex);
                        }
                    )
                ).ToResult()
        );

        app.MapGet(
            ReelContract.CastCurrent.Route,
            async (CastService castService) =>
                (
                    await ReelContract.CastCurrent.Invoke(() =>
                    {
                        var command = castService.GetPending();
                        return Task.FromResult(new CastStatus(command));
                    })
                ).ToResult()
        );

        app.MapPost(
            ReelContract.CastAck.Route,
            async (CastService castService) =>
                (
                    await ReelContract.CastAck.Invoke(() =>
                    {
                        castService.Acknowledge();
                        return Task.CompletedTask;
                    })
                ).ToResult()
        );

        app.MapPost(
            ReelContract.CastStop.Route,
            async (CastService castService) =>
                (
                    await ReelContract.CastStop.Invoke(() =>
                    {
                        castService.SetStop();
                        return Task.CompletedTask;
                    })
                ).ToResult()
        );

        app.MapPost(
            ReelContract.CastQueuePlay.Route,
            async (
                HttpContext context,
                [FromBody] CastQueueRequest request,
                CastService castService,
                IProgressStore progressStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.CastQueuePlay.Invoke(
                        request,
                        async req =>
                        {
                            double? firstItemPosition = null;
                            if (req.Paths.Length > 0)
                            {
                                var userId = context.GetUserId();
                                var progress = await progressStore.GetAsync(userId, req.Paths[0], ct);
                                firstItemPosition = progress?.PositionSeconds;
                            }
                            castService.SetPlayQueue(req.Paths, firstItemPosition);
                        }
                    )
                ).ToResult()
        );

        app.MapGet(
            ReelContract.CastQueueGet.Route,
            async (CastService castService) =>
                (
                    await ReelContract.CastQueueGet.Invoke(() =>
                    {
                        return Task.FromResult(castService.GetQueue());
                    })
                ).ToResult()
        );

        return app;
    }
}
