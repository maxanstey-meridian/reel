namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Rivet;

public static class SearchEndpoints
{
    public static WebApplication MapSearchEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.Search.Route,
            async (
                [FromQuery] string? q,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
            {
                var request = new SearchRequest(q);
                var endpoint = ReelContract.Search.Bind(request);
                var results = await mediaStore.SearchAsync(new SearchQuery(request.Q), ct);
                return endpoint.Success(results.ToArray()).ToResult();
            }
        );

        return app;
    }
}
