namespace Reel.Server.Endpoints;

using Reel.Server.Application.Ports;

public static class LibraryEndpoints
{
    public static WebApplication MapLibraryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/library", async (ILibraryScanner scanner, CancellationToken ct) =>
            Results.Ok(await scanner.BuildTreeAsync(ct)));

        return app;
    }
}
