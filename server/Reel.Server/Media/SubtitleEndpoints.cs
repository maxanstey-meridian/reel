using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Media;

public static partial class SubtitleEndpoints
{
    public static IEndpointRouteBuilder MapSubtitleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ReelContract.SubtitlesList.Route,
            async (
                [AsParameters] FileRequest request,
                FfprobeSubtitleDiscovery discovery,
                MediaPathResolver paths,
                IOptionsMonitor<ReelOptions> options,
                CancellationToken ct
            ) =>
            {
                var endpoint = ReelContract.SubtitlesList.Bind(request);
                var fullPath = paths.ResolvePlayableFile(request.Path);
                if (fullPath is null)
                {
                    return endpoint.Error(404, EndpointErrors.MediaNotFound).ToResult();
                }

                // A transcoded stream carries no subtitle streams (-sn), and image-based tracks cannot become WebVTT.
                var tracks = await discovery.DiscoverAsync(fullPath, ct);
                var transcoded = Transcoding.Height(options.CurrentValue, fullPath) is not null;
                return endpoint
                    .Success([
                        .. tracks
                            .Select(track => new SubtitleTrack(
                                track.Index,
                                track.Label,
                                track.Language,
                                track.Format,
                                track.ExternalPath is null
                                    ? SubtitleSource.Embedded
                                    : SubtitleSource.External,
                                FfprobeSubtitleDiscovery.IsImageBased(track)
                            ))
                            .Where(track => !transcoded || !track.ImageBased),
                    ])
                    .ToResult();
            }
        );

        endpoints.MapGet(
            ReelContract.SubtitlesContent.Route,
            async (
                [AsParameters] SubtitleContentRequest request,
                FfprobeSubtitleDiscovery discovery,
                FfmpegSubtitleExtractor extractor,
                MediaPathResolver paths,
                CancellationToken ct
            ) =>
            {
                var endpoint = ReelContract.SubtitlesContent.Bind(request);
                var fullPath = paths.ResolvePlayableFile(request.Path);
                if (fullPath is null)
                {
                    return endpoint.Error(404, EndpointErrors.MediaNotFound).ToResult();
                }

                var track = (await discovery.DiscoverAsync(fullPath, ct)).FirstOrDefault(t =>
                    t.Index == request.Index
                );
                if (track is null)
                {
                    return endpoint
                        .Error(
                            404,
                            new ErrorResponse("subtitle_not_found", "Subtitle track not found.")
                        )
                        .ToResult();
                }

                var vtt = track.ExternalPath is null
                    ? await extractor.ExtractAsync(fullPath, track.Index, ct)
                    : await extractor.ExtractAsync(
                        Path.Join(paths.MediaRoot, track.ExternalPath),
                        0,
                        ct
                    );
                if (vtt is null)
                {
                    return endpoint
                        .Error(
                            404,
                            new ErrorResponse(
                                "subtitle_unavailable",
                                "Subtitle track could not be converted."
                            )
                        )
                        .ToResult();
                }

                var content = await File.ReadAllTextAsync(vtt, ct);
                return Results.Content(
                    ShiftVtt(content, Transcoding.StartSeconds(request.Start)),
                    "text/vtt"
                );
            }
        );

        return endpoints;
    }

    // ffmpeg's own seeking (-ss) shifts sparse subtitle streams by the wrong amount, so a transcoded stream that
    // restarted at ?start= gets its cues moved back here; cues that already ended are dropped.
    private static string ShiftVtt(string vtt, double startSeconds)
    {
        if (startSeconds <= 0)
        {
            return vtt;
        }

        var offset = TimeSpan.FromSeconds(startSeconds);
        var cues = vtt.ReplaceLineEndings("\n")
            .Split("\n\n")
            .Select(block =>
            {
                var timing = CueTiming().Match(block);
                if (!timing.Success)
                {
                    return block;
                }

                var end = ParseVttTime(timing.Groups[2].Value) - offset;
                if (end <= TimeSpan.Zero)
                {
                    return null;
                }

                var start = TimeSpan.FromTicks(
                    Math.Max(0, (ParseVttTime(timing.Groups[1].Value) - offset).Ticks)
                );
                return $"{block[..timing.Index]}{FormatVttTime(start)} --> {FormatVttTime(end)}{block[(timing.Index + timing.Length)..]}";
            });
        return string.Join("\n\n", cues.OfType<string>());
    }

    private static TimeSpan ParseVttTime(string value) =>
        TimeSpan.ParseExact(
            value,
            [@"mm\:ss\.fff", @"hh\:mm\:ss\.fff"],
            CultureInfo.InvariantCulture
        );

    private static string FormatVttTime(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";

    [GeneratedRegex(
        @"^((?:\d+:)?\d{2}:\d{2}\.\d{3}) --> ((?:\d+:)?\d{2}:\d{2}\.\d{3})",
        RegexOptions.Multiline
    )]
    private static partial Regex CueTiming();
}
