using Microsoft.AspNetCore.Mvc;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Library;

public static class FavouritesEndpoints
{
    public static IEndpointRouteBuilder MapFavouritesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            ReelContract.FavouritesRoute,
            async (
                [FromBody] FavouriteRequest request,
                SqliteFavouritesStore store,
                SqliteMediaStore mediaStore,
                MediaPathResolver paths
            ) =>
            {
                var endpoint = ReelContract.FavouritesAdd.Bind(request);
                var relativePath = await LibraryEntries.EnsureIndexedAsync(
                    request.RelativePath,
                    paths,
                    mediaStore
                );
                if (relativePath is null)
                {
                    return endpoint.Error(404, EndpointErrors.MediaNotFound).ToResult();
                }

                await store.AddAsync(relativePath);
                return endpoint.Success().ToResult();
            }
        );

        endpoints.MapDelete(
            ReelContract.FavouritesRoute,
            async ([AsParameters] FileRequest request, SqliteFavouritesStore store) =>
            {
                var endpoint = ReelContract.FavouritesRemove.Bind(request);
                if (MediaPathResolver.Normalize(request.Path) is { } relativePath)
                {
                    await store.RemoveAsync(relativePath);
                }

                return endpoint.Success().ToResult();
            }
        );

        endpoints.MapGet(
            ReelContract.FavouritesRoute,
            async (SqliteFavouritesStore store) =>
                ReelContract.FavouritesAll.Success(await store.GetAllAsync()).ToResult()
        );

        return endpoints;
    }
}
