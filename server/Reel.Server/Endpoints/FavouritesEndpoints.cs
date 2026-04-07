namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;

public static class FavouritesEndpoints
{
    public static WebApplication MapFavouritesEndpoints(this WebApplication app)
    {
        app.MapPost("/api/favourites", async ([FromBody] FavouriteRequest request, IFavouritesStore store, CancellationToken ct) =>
        {
            await store.AddAsync(request.RelativePath, ct);
            return Results.Ok();
        });

        app.MapDelete("/api/favourites", async ([FromQuery] string path, IFavouritesStore store, CancellationToken ct) =>
        {
            await store.RemoveAsync(path, ct);
            return Results.Ok();
        });

        app.MapGet("/api/favourites", async (IFavouritesStore store, CancellationToken ct) =>
        {
            return Results.Ok(await store.GetAllAsync(ct));
        });

        return app;
    }
}
