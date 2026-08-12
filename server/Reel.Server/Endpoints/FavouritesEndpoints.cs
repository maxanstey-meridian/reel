namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Infrastructure.FileSystem;
using Rivet;

public static class FavouritesEndpoints
{
    public static WebApplication MapFavouritesEndpoints(this WebApplication app)
    {
        app.MapPost(
            ReelContract.FavouritesAdd.Route,
            async (
                [FromBody] FavouriteRequest request,
                IFavouritesStore store,
                IMediaStore mediaStore,
                MediaPathResolver pathResolver,
                CancellationToken ct
            ) =>
            {
                var endpoint = ReelContract.FavouritesAdd.Bind(request);
                if (pathResolver.ResolvePlayableFile(request.RelativePath) is null
                    || await mediaStore.GetAsync(request.RelativePath, ct) is null)
                    throw new NotFoundException("Indexed playable media was not found.");
                await store.AddAsync(request.RelativePath, ct);
                return endpoint.Success().ToResult();
            }
        );

        app.MapDelete(
            ReelContract.FavouritesRemove.Route,
            async ([FromQuery] string path, IFavouritesStore store, CancellationToken ct) =>
            {
                var request = new FileRequest(path);
                var endpoint = ReelContract.FavouritesRemove.Bind(request);
                await store.RemoveAsync(request.Path, ct);
                return endpoint.Success().ToResult();
            }
        );

        app.MapGet(
            ReelContract.FavouritesAll.Route,
            async (IFavouritesStore store, CancellationToken ct) =>
            {
                var favourites = await store.GetAllAsync(ct);
                return ReelContract.FavouritesAll.Success(favourites.ToArray()).ToResult();
            }
        );

        return app;
    }
}
