namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;

public static class SearchEndpoints
{
    public static WebApplication MapSearchEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.Search.Route,
            async (
                HttpContext context,
                [FromQuery] string? q,
                [FromQuery] string? tag,
                [FromQuery] string? genre,
                [FromQuery] bool? watched,
                IMediaStore mediaStore,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.Search.Invoke(async () =>
                    {
                        var userId = (int)context.Items["UserId"]!;
                        var query = new SearchQuery(q, tag, genre, watched);
                        var results = await mediaStore.SearchAsync(userId, query, ct);
                        return results.ToArray();
                    })
                ).ToResult()
        );

        return app;
    }
}
