namespace Reel.Server.Endpoints;

using Reel.Server.Application.Ports;

public static class StreamEndpoints
{
    public static WebApplication MapStreamEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.Stream.Route,
            (string? path, IFileStreamer streamer) =>
            {
                if (string.IsNullOrWhiteSpace(path))
                    return Results.BadRequest(new { error = "Missing required query parameter: path" });

                var resolved = streamer.Resolve(path);

                if (resolved is null)
                    return Results.NotFound(new { error = "File not found" });

                return Results.File(resolved.Value.fullPath, resolved.Value.contentType, enableRangeProcessing: true);
            }
        );

        app.MapGet(
            ReelContract.NextFile.Route,
            async (string? path, ILibraryScanner scanner, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(path))
                    return Results.BadRequest(new { error = "Missing required query parameter: path" });

                var next = await scanner.GetNextFileAsync(path, ct);

                if (next is null)
                    return Results.NoContent();

                return (
                    await ReelContract.NextFile.Invoke(
                        new Application.Models.FileRequest(path),
                        _ => Task.FromResult(next)
                    )
                ).ToResult();
            }
        );

        return app;
    }
}
