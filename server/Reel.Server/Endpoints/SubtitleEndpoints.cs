namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Infrastructure.FileSystem;
using Rivet;

public static class SubtitleEndpoints
{
    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "webvtt",
        "srt",
        "ass",
        "vtt",
    };
    public static WebApplication MapSubtitleEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.SubtitlesList.Route,
            async (
                [FromQuery] string? path,
                ISubtitleDiscovery discovery,
                IMediaStore mediaStore,
                MediaPathResolver pathResolver,
                CancellationToken ct
            ) =>
            {
                var request = new FileRequest(path ?? "");
                var endpoint = ReelContract.SubtitlesList.Bind(request);
                if (string.IsNullOrWhiteSpace(request.Path))
                    throw new BadRequestException("Missing required query parameter: path");

                if (pathResolver.ResolvePlayableFile(request.Path) is null)
                    throw new ForbiddenException("Access denied.");
                if (await mediaStore.GetAsync(request.Path, ct) is null)
                    throw new NotFoundException("Indexed media not found.");

                var tracks = await discovery.DiscoverAsync(request.Path, ct);
                await mediaStore.SaveSubtitlesAsync(request.Path, tracks, ct);
                return endpoint.Success(tracks.ToArray()).ToResult();
            }
        );

        app.MapGet(
            ReelContract.SubtitlesContent.Route,
            async (
                [FromQuery] string? path,
                [FromQuery] int? index,
                [FromQuery] string? format,
                IMediaStore mediaStore,
                ISubtitleExtractor extractor,
                MediaPathResolver pathResolver,
                CancellationToken ct
            ) =>
            {
                var endpoint = ReelContract.SubtitlesContent.Bind(
                    new SubtitleContentRequest(path ?? "", index ?? 0, format)
                );
                if (string.IsNullOrWhiteSpace(path))
                    return endpoint.Error(400, new ErrorResponse("Missing required query parameter: path")).ToResult();

                if (index is null)
                    return endpoint.Error(400, new ErrorResponse("Missing required query parameter: index")).ToResult();

                if (format is not null && !AllowedFormats.Contains(format))
                    return endpoint.Error(400, new ErrorResponse("Unsupported subtitle format")).ToResult();

                if (pathResolver.ResolvePlayableFile(path) is null)
                    return endpoint.Error(403).ToResult();
                if (await mediaStore.GetAsync(path, ct) is null)
                    return endpoint.Error(404, new ErrorResponse("Indexed media not found.")).ToResult();

                // Look up the track from persisted data
                var tracks = await mediaStore.GetSubtitlesAsync(path, ct);
                var track = tracks.FirstOrDefault(t => t.Index == index.Value);

                if (track is null)
                    return endpoint.Error(404, new ErrorResponse("Subtitle track not found")).ToResult();

                if (track.Source == "external" && track.ExternalPath is not null)
                {
                    var externalFullPath = pathResolver.ResolveExistingFile(track.ExternalPath);
                    if (externalFullPath is null)
                        return endpoint.Error(404, new ErrorResponse("Subtitle file not found")).ToResult();

                    var content = await File.ReadAllTextAsync(externalFullPath, ct);

                    // Convert SRT to VTT if requested or if format is srt
                    var requestedFormat = format ?? track.Format;
                    if (
                        track.Format.Equals("srt", StringComparison.OrdinalIgnoreCase)
                        && requestedFormat.Equals("vtt", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        content = extractor.ConvertSrtToVtt(content);
                        return Results.Content(content, "text/vtt");
                    }

                    var contentType = track.Format.Equals("vtt", StringComparison.OrdinalIgnoreCase)
                        ? "text/vtt"
                        : "text/plain";
                    return Results.Content(content, contentType);
                }

                // Embedded track — extract via ffmpeg
                var outputFormat = format ?? "webvtt";
                if (!AllowedFormats.Contains(outputFormat))
                    return endpoint.Error(400, new ErrorResponse("Unsupported subtitle format")).ToResult();

                var cachedPath = await extractor.ExtractAsync(path, track.Index, outputFormat, ct);

                if (cachedPath is null)
                    return endpoint.Error(500).ToResult();

                var extractedContent = await File.ReadAllTextAsync(cachedPath, ct);
                var mime = outputFormat.Equals("webvtt", StringComparison.OrdinalIgnoreCase)
                    ? "text/vtt"
                    : "text/plain";
                return Results.Content(extractedContent, mime);
            }
        );

        return app;
    }
}
