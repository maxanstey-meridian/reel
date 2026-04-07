namespace Reel.Server.Endpoints;

using Reel.Server.Application.Ports;

public static class StreamEndpoints
{
    public static WebApplication MapStreamEndpoints(this WebApplication app)
    {
        app.MapGet("/api/files/stream", (string? path, IFileStreamer streamer) =>
        {
            if (string.IsNullOrWhiteSpace(path))
                return Results.BadRequest(new { error = "Missing required query parameter: path" });

            var resolved = streamer.Resolve(path);

            if (resolved is null)
                return Results.NotFound(new { error = "File not found" });

            return Results.File(resolved.Value.fullPath, resolved.Value.contentType, enableRangeProcessing: true);
        });

        return app;
    }
}
