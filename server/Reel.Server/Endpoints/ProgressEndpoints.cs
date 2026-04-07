namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;

public static class ProgressEndpoints
{
    public static WebApplication MapProgressEndpoints(this WebApplication app)
    {
        app.MapPost(
            ReelContract.ProgressUpdate.Route,
            async (
                HttpContext context,
                [FromBody] ProgressRequest request,
                IProgressStore store,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.ProgressUpdate.Invoke(
                        request,
                        async req =>
                        {
                            var userId = (int)context.Items["UserId"]!;
                            await store.UpdateAsync(
                                userId,
                                req.RelativePath,
                                req.PositionSeconds,
                                req.DurationSeconds,
                                ct
                            );
                        }
                    )
                ).ToResult()
        );

        app.MapGet(
            ReelContract.ProgressAll.Route,
            async (HttpContext context, IProgressStore store, CancellationToken ct) =>
                (
                    await ReelContract.ProgressAll.Invoke(async () =>
                    {
                        var userId = (int)context.Items["UserId"]!;
                        var progress = await store.GetAllAsync(userId, ct);
                        return progress.ToArray();
                    })
                ).ToResult()
        );

        return app;
    }
}
