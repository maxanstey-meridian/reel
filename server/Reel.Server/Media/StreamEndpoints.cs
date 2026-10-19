using System.Globalization;
using System.Text;
using CliWrap;
using Microsoft.AspNetCore.StaticFiles;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Media;

public static class StreamEndpoints
{
    // Covers every playable extension; .ogg is audio here, not ASP.NET's video/ogg.
    private static readonly FileExtensionContentTypeProvider ContentTypes = new()
    {
        Mappings =
        {
            [".mkv"] = "video/x-matroska",
            [".flac"] = "audio/flac",
            [".ogg"] = "audio/ogg",
        },
    };

    public static IEndpointRouteBuilder MapStreamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ReelContract.StreamRoute,
            async (
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

                if (
                    request is { Height: null, Remux: not true }
                    || MediaPathResolver.IsAudio(fullPath)
                )
                {
                    // Rivet rejects a missing or out-of-contract type, so an unmapped extension fails loudly.
                    ContentTypes.TryGetContentType(fullPath, out var contentType);
                    return endpoint
                        .File(fullPath, enableRangeProcessing: true, contentType: contentType)
                        .ToResult();
                }

                var invalid = request switch
                {
                    { Remux: true, Height: not null } =>
                        "A remux keeps the video, so it takes no height.",
                    { Height: { } h } when !Transcoding.IsHeight(h) =>
                        "Height must be a positive even number.",
                    _ => null,
                };
                if (invalid is not null)
                {
                    return endpoint
                        .Error(400, new ErrorResponse("invalid_request", invalid))
                        .ToResult();
                }

                string[] video;
                string[] audio;
                if (request.Height is { } height)
                {
                    // Smaller sources keep their own height rather than being upscaled. Bitrates scale with height
                    // from 1200k/1500k/3000k at 480p.
                    // csharpier-ignore
                    video =
                    [
                        "-vf", $"scale=-2:'trunc(min(ih,{height})/2)*2',format=yuv420p",
                        "-c:v", "h264_videotoolbox", "-b:v", $"{height * 5 / 2}k", "-maxrate", $"{height * 25 / 8}k", "-bufsize", $"{height * 25 / 4}k",
                    ];
                    audio = ["-c:a", "aac", "-ac", "2", "-b:a", "128k"];
                }
                else
                {
                    // Packed B-frames (common in DivX/XviD AVIs) must be unpacked before they can go into MP4.
                    var codec = (
                        await Ffmpeg.ProbeAsync(fullPath, "v:0", logger, http.RequestAborted)
                    )
                        ?.Streams?.FirstOrDefault()
                        ?.CodecName;
                    video =
                        codec == "mpeg4"
                            ? ["-c:v", "copy", "-bsf:v", "mpeg4_unpack_bframes"]
                            : ["-c:v", "copy"];
                    // AC3 keeps up to 5.1 and the TV decodes it; wider layouts are folded down to 5.1.
                    // csharpier-ignore
                    audio = ["-c:a", "ac3", "-af", "aformat=channel_layouts=5.1|5.1(side)|stereo|mono"];
                }

                // A live stream has no byte ranges; the TV seeks by reloading with ?start=<seconds>. It stays a plain
                // result because Rivet's File(...) takes a finished stream, not ffmpeg writing into the response.
                return Results.Stream(
                    output =>
                        StreamLiveAsync(
                            fullPath,
                            Transcoding.StartSeconds(request.Start),
                            video,
                            audio,
                            output,
                            logger,
                            http
                        ),
                    "video/mp4"
                );
            }
        );

        endpoints.MapGet(
            ReelContract.StreamInfoRoute,
            async (
                [AsParameters] PlaybackFileRequest request,
                MediaPathResolver paths,
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

                return endpoint
                    .Success(await Transcoding.PlanAsync(request.MaxHeight, fullPath, logger, ct))
                    .ToResult();
            }
        );

        return endpoints;
    }

    private static async Task StreamLiveAsync(
        string fullPath,
        double start,
        string[] video,
        string[] audio,
        Stream output,
        ILogger logger,
        HttpContext http
    )
    {
        var errors = new StringBuilder();
        // genpts fills the timestamps AVI leaves out, which a copied video stream needs in MP4.
        // csharpier-ignore
        string[] arguments =
        [
            "-hide_banner", "-loglevel", "error", "-nostdin", "-fflags", "+genpts",
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", fullPath,
            "-map", "0:v:0", "-map", "0:a:0?",
            .. video,
            .. audio,
            "-sn", "-dn",
            "-f", "mp4", "-movflags", "frag_keyframe+empty_moov+delay_moov+default_base_moof",
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
