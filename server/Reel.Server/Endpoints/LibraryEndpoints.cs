namespace Reel.Server.Endpoints;

using Reel.Server.Services;

public static class LibraryEndpoints
{
    public static WebApplication MapLibraryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/library", (LibraryService library) => Results.Ok(library.GetTree()));

        return app;
    }
}
