namespace Reel.Server.Endpoints;

using Reel.Server.Application.Ports;

public static class MediaEndpoints
{
    public static WebApplication MapMediaEndpoints(this WebApplication app)
    {
        app.MapPut(
            ReelContract.MediaSetWatched.Route,
            async (HttpContext context, [Microsoft.AspNetCore.Mvc.FromQuery] string path, IMediaStore mediaStore, CancellationToken ct) =>
                (await ReelContract.MediaSetWatched.Invoke(async () =>
                {
                    var userId = (int)context.Items["UserId"]!;
                    await mediaStore.SetWatchStateAsync(userId, path, "watched", ct);
                })).ToResult()
        );

        app.MapPut(
            ReelContract.MediaSetUnwatched.Route,
            async (HttpContext context, [Microsoft.AspNetCore.Mvc.FromQuery] string path, IMediaStore mediaStore, CancellationToken ct) =>
                (await ReelContract.MediaSetUnwatched.Invoke(async () =>
                {
                    var userId = (int)context.Items["UserId"]!;
                    await mediaStore.SetWatchStateAsync(userId, path, "unwatched", ct);
                })).ToResult()
        );

        app.MapDelete(
            ReelContract.MediaClearWatched.Route,
            async (HttpContext context, [Microsoft.AspNetCore.Mvc.FromQuery] string path, IMediaStore mediaStore, CancellationToken ct) =>
                (await ReelContract.MediaClearWatched.Invoke(async () =>
                {
                    var userId = (int)context.Items["UserId"]!;
                    await mediaStore.SetWatchStateAsync(userId, path, null, ct);
                })).ToResult()
        );

        return app;
    }
}
