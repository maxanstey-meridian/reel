namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;

public static class FavouritesEndpoints
{
    public static WebApplication MapFavouritesEndpoints(this WebApplication app)
    {
        app.MapPost("/api/favourites", async (HttpContext context, [FromBody] FavouriteRequest request, IFavouritesStore store, CancellationToken ct) =>
        {
            var userId = (int)context.Items["UserId"]!;
            await store.AddAsync(userId, request.RelativePath, ct);
            return Results.Ok();
        });

        app.MapDelete("/api/favourites", async (HttpContext context, [FromQuery] string path, IFavouritesStore store, CancellationToken ct) =>
        {
            var userId = (int)context.Items["UserId"]!;
            await store.RemoveAsync(userId, path, ct);
            return Results.Ok();
        });

        app.MapGet("/api/favourites", async (HttpContext context, IFavouritesStore store, CancellationToken ct) =>
        {
            var userId = (int)context.Items["UserId"]!;
            return Results.Ok(await store.GetAllAsync(userId, ct));
        });

        return app;
    }
}
