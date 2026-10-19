using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;

namespace Reel.Server.Media;

/// <summary>The live-transcode rules the stream and the subtitles served alongside it must agree on.</summary>
internal static class Transcoding
{
    // What Reel's Fire TV (Stick 4K Max, AFTKRT) plays straight from a file through Media3. Its MP3 decoder works,
    // but not on MP3 read out of AVI, so containers outside these are remuxed even when every codec is playable.
    private static readonly HashSet<string> DirectContainers = ["mov", "matroska", "mpegts"];
    private static readonly HashSet<string> DirectVideo =
    [
        "h264",
        "hevc",
        "av1",
        "vp8",
        "vp9",
        "mpeg4",
        "mpeg2video",
    ];
    private static readonly HashSet<string> DirectAudio =
    [
        "aac",
        "ac3",
        "eac3",
        "mp3",
        "flac",
        "opus",
        "vorbis",
    ];

    /// <summary>
    /// How this file reaches the TV. Audio files and files ffprobe cannot read are served as they are. Video taller
    /// than maxHeight (or of unknown height) is transcoded down to it; otherwise only what the TV cannot play is
    /// touched: unplayable video is transcoded at its own height, and a playable video in an unplayable container or
    /// with unplayable audio is remuxed with the video kept as it is.
    /// </summary>
    public static async Task<StreamInfoResponse> PlanAsync(
        int? maxHeight,
        string fullPath,
        ILogger logger,
        CancellationToken ct
    )
    {
        var probe = await Ffmpeg.ProbeAsync(fullPath, streams: null, logger, ct);
        var duration = probe?.Format?.Duration;
        var video = probe?.Streams?.FirstOrDefault(stream => stream.IsMovingVideo);
        var audio = probe?.Streams?.FirstOrDefault(stream => stream.CodecType == "audio");
        if (MediaPathResolver.IsAudio(fullPath) || probe is null)
        {
            return new StreamInfoResponse(duration, StreamMode.Direct, null);
        }

        if (maxHeight is { } cap && video is not null && !(video.Height <= cap))
        {
            return new StreamInfoResponse(duration, StreamMode.Transcode, cap);
        }

        if (video is not null && !IsDirectVideo(video))
        {
            // The scale filter never upscales, so the source height only sets the bitrate.
            return new StreamInfoResponse(
                duration,
                StreamMode.Transcode,
                video.Height is > 1 and var h ? h / 2 * 2 : 1080
            );
        }

        var container = probe.Format?.FormatName?.Split(',') ?? [];
        return container.Any(DirectContainers.Contains) && (audio is null || IsDirectAudio(audio))
            ? new StreamInfoResponse(duration, StreamMode.Direct, null)
            : new StreamInfoResponse(duration, StreamMode.Remux, null);
    }

    /// <summary>A height video can be transcoded to: H.264 in 4:2:0 needs it even.</summary>
    public static bool IsHeight(int height) => height > 0 && height % 2 == 0;

    /// <summary>The second a transcoded stream starts at; anything unusable starts at the beginning.</summary>
    public static double StartSeconds(double? start) =>
        start is > 0 and < double.PositiveInfinity ? start.Value : 0;

    // The TV decodes only 8-bit 4:2:0 H.264 in hardware; 10-bit (Hi10P) is common in anime releases, and 4:2:2 or
    // 4:4:4 turn up in camera and screen recordings.
    private static bool IsDirectVideo(ProbeStream video) =>
        video.CodecName is { } codec
        && DirectVideo.Contains(codec)
        && !(codec == "h264" && video.PixFmt is { } format and not ("yuv420p" or "yuvj420p"));

    private static bool IsDirectAudio(ProbeStream audio) =>
        audio.CodecName is { } codec && (DirectAudio.Contains(codec) || codec.StartsWith("pcm_"));
}
