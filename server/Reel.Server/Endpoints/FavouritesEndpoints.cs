namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Extensions;

public static class FavouritesEndpoints
{
    public static WebApplication MapFavouritesEndpoints(this WebApplication app)
    {
        app.MapPost(
            ReelContract.FavouritesAdd.Route,
            async (
                HttpContext context,
                [FromBody] FavouriteRequest request,
                IFavouritesStore store,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.FavouritesAdd.Invoke(
                        request,
                        async req =>
                        {
                            var userId = context.GetUserId();
                            await store.AddAsync(userId, req.RelativePath, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapDelete(
            ReelContract.FavouritesRemove.Route,
            async (HttpContext context, [FromQuery] string path, IFavouritesStore store, CancellationToken ct) =>
                (
                    await ReelContract.FavouritesRemove.Invoke(
                        new FileRequest(path),
                        async req =>
                        {
                            var userId = context.GetUserId();
                            await store.RemoveAsync(userId, req.Path, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapGet(
            ReelContract.FavouritesAll.Route,
            async (HttpContext context, IFavouritesStore store, CancellationToken ct) =>
                (
                    await ReelContract.FavouritesAll.Invoke(async () =>
                    {
                        var userId = context.GetUserId();
                        var favourites = await store.GetAllAsync(userId, ct);
                        return favourites.ToArray();
                    })
                ).ToResult()
        );

        return app;
    }
}
