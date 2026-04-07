namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Models;
using Reel.Server.Services;

public static class FavouritesEndpoints
{
    public static WebApplication MapFavouritesEndpoints(this WebApplication app)
    {
        app.MapPost("/api/favourites", ([FromBody] FavouriteRequest request, FavouritesService favouritesService) =>
        {
            favouritesService.Add(request.RelativePath);
            return Results.Ok();
        });

        app.MapDelete("/api/favourites", ([FromQuery] string path, FavouritesService favouritesService) =>
        {
            favouritesService.Remove(path);
            return Results.Ok();
        });

        app.MapGet("/api/favourites", (FavouritesService favouritesService) =>
        {
            return Results.Ok(favouritesService.GetAll());
        });

        return app;
    }
}
