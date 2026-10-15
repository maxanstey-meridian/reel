using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Library;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ReelContract.SearchRoute,
            async ([AsParameters] SearchRequest request, SqliteMediaStore mediaStore) =>
            {
                var endpoint = ReelContract.Search.Bind(request);
                var media = await mediaStore.SearchAsync(request.Q);
                return endpoint.Success([.. media.Select(LibraryEntries.File)]).ToResult();
            }
        );

        return endpoints;
    }
}
