namespace Reel.Server.Endpoints;

using Reel.Server.Application.Ports;
using Reel.Server.Application.Models;
using Rivet;

public static class StreamEndpoints
{
    public static WebApplication MapStreamEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.Stream.Route,
            (string? path, IFileStreamer streamer) =>
            {
                var endpoint = ReelContract.Stream.Bind(new FileRequest(path ?? ""));
                if (string.IsNullOrWhiteSpace(path))
                    return endpoint.Error(400, new ErrorResponse("Missing required query parameter: path")).ToResult();

                var resolved = streamer.Resolve(path);

                if (resolved is null)
                    return endpoint.Error(404, new ErrorResponse("File not found")).ToResult();

                return Results.File(
                    resolved.Value.fullPath,
                    resolved.Value.contentType,
                    enableRangeProcessing: true
                );
            }
        );

        return app;
    }
}
