namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Infrastructure.Configuration;

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
                ReelConfig config,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.SubtitlesList.Invoke(
                        new FileRequest(path ?? ""),
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(req.Path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, req.Path));
                            if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                                throw new ForbiddenException("Access denied.");

                            var tracks = await discovery.DiscoverAsync(req.Path, ct);
                            await mediaStore.SaveSubtitlesAsync(req.Path, tracks, ct);
                            return tracks.ToArray();
                        }
                    )
                ).ToResult()
        );

        app.MapGet(
            ReelContract.SubtitlesContent.Route,
            async (
                [FromQuery] string? path,
                [FromQuery] int? index,
                [FromQuery] string? format,
                IMediaStore mediaStore,
                ISubtitleExtractor extractor,
                ReelConfig config,
                CancellationToken ct
            ) =>
            {
                if (string.IsNullOrWhiteSpace(path))
                    return Results.BadRequest(new { error = "Missing required query parameter: path" });

                if (index is null)
                    return Results.BadRequest(new { error = "Missing required query parameter: index" });

                if (format is not null && !AllowedFormats.Contains(format))
                    return Results.BadRequest(new { error = "Unsupported subtitle format" });

                var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, path));
                if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                    return Results.StatusCode(403);

                // Look up the track from persisted data
                var tracks = await mediaStore.GetSubtitlesAsync(path, ct);
                var track = tracks.FirstOrDefault(t => t.Index == index.Value);

                if (track is null)
                    return Results.NotFound(new { error = "Subtitle track not found" });

                if (track.Source == "external" && track.ExternalPath is not null)
                {
                    var externalFullPath = Path.GetFullPath(
                        Path.Combine(config.NormalisedMediaRoot, track.ExternalPath)
                    );
                    if (
                        !externalFullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal)
                        || !File.Exists(externalFullPath)
                    )
                        return Results.NotFound(new { error = "Subtitle file not found" });

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
                    return Results.BadRequest(new { error = "Unsupported subtitle format" });

                var cachedPath = await extractor.ExtractAsync(path, track.Index, outputFormat, ct);

                if (cachedPath is null)
                    return Results.StatusCode(500);

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
