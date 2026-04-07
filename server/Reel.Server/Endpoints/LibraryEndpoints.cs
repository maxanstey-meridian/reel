namespace Reel.Server.Endpoints;

using Reel.Server.Application.Ports;

public static class LibraryEndpoints
{
    public static WebApplication MapLibraryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/library", (ILibraryScanner scanner) => Results.Ok(scanner.BuildTree()));

        return app;
    }
}
