using System.Globalization;
using System.Text;
using CliWrap;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Media;

public static class StreamEndpoints
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new()
    {
        Mappings = { [".mkv"] = "video/x-matroska", [".flac"] = "audio/flac" },
    };

    public static IEndpointRouteBuilder MapStreamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ReelContract.Stream.Route,
            (
                [AsParameters] StreamRequest request,
                MediaPathResolver paths,
                ILogger<Program> logger,
                HttpContext http
            ) =>
            {
                var endpoint = ReelContract.Stream.Bind(request);
                var fullPath = paths.ResolvePlayableFile(request.Path);
                if (fullPath is null)
                {
                    return endpoint.Error(404, EndpointErrors.MediaNotFound).ToResult();
                }

                if (request.Height is not { } height || MediaPathResolver.IsAudio(fullPath))
                {
                    return Results.File(
                        fullPath,
                        ContentTypes.TryGetContentType(fullPath, out var contentType)
                            ? contentType
                            : "application/octet-stream",
                        enableRangeProcessing: true
                    );
                }

                if (!ReelOptions.IsTranscodeHeight(height))
                {
                    return endpoint
                        .Error(
                            400,
                            new ErrorResponse(
                                "invalid_request",
                                "Height must be a positive even number."
                            )
                        )
                        .ToResult();
                }

                // The transcode has no byte ranges; the TV seeks by reloading with ?start=<seconds>.
                return Results.Stream(
                    output =>
                        TranscodeAsync(
                            fullPath,
                            Transcoding.StartSeconds(request.Start),
                            height,
                            output,
                            logger,
                            http
                        ),
                    "video/mp4"
                );
            }
        );

        endpoints.MapGet(
            ReelContract.StreamInfo.Route,
            async (
                [AsParameters] FileRequest request,
                MediaPathResolver paths,
                IOptionsMonitor<ReelOptions> options,
                ILogger<Program> logger,
                CancellationToken ct
            ) =>
            {
                var endpoint = ReelContract.StreamInfo.Bind(request);
                var fullPath = paths.ResolvePlayableFile(request.Path);
                if (fullPath is null)
                {
                    return endpoint.Error(404, EndpointErrors.MediaNotFound).ToResult();
                }

                var duration =
                    (await Ffmpeg.ProbeAsync(fullPath, "v:0", logger, ct))?.Format?.Duration ?? 0;
                return endpoint
                    .Success(
                        new StreamInfoResponse(
                            duration,
                            Transcoding.Height(options.CurrentValue, fullPath)
                        )
                    )
                    .ToResult();
            }
        );

        return endpoints;
    }

    private static async Task TranscodeAsync(
        string fullPath,
        double start,
        int height,
        Stream output,
        ILogger logger,
        HttpContext http
    )
    {
        var errors = new StringBuilder();
        // Smaller sources keep their own height rather than being upscaled. Bitrates scale with height from
        // 1200k/1500k/3000k at 480p.
        // csharpier-ignore
        string[] arguments =
        [
            "-hide_banner", "-loglevel", "error", "-nostdin",
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", fullPath,
            "-map", "0:v:0", "-map", "0:a:0?",
            "-vf", $"scale=-2:'trunc(min(ih,{height})/2)*2',format=yuv420p",
            "-c:v", "h264_videotoolbox", "-b:v", $"{height * 5 / 2}k", "-maxrate", $"{height * 25 / 8}k", "-bufsize", $"{height * 25 / 4}k",
            "-c:a", "aac", "-ac", "2", "-b:a", "128k",
            "-sn", "-dn",
            "-f", "mp4", "-movflags", "frag_keyframe+empty_moov+default_base_moof",
            "pipe:1",
        ];
        try
        {
            await Cli.Wrap("ffmpeg")
                .WithArguments(arguments)
                .WithStandardOutputPipe(PipeTarget.ToStream(output))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errors))
                .ExecuteAsync(http.RequestAborted);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transcoding {Path} failed: {Error}", fullPath, errors);
            if (!http.Response.HasStarted)
            {
                http.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }
    }
}
