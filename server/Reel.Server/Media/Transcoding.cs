using Reel.Server.Storage;

namespace Reel.Server.Media;

/// <summary>The live-transcode rules the stream and the subtitles served alongside it must agree on.</summary>
internal static class Transcoding
{
    /// <summary>The height to transcode this file to under the current settings; audio is always served as it is.</summary>
    public static int? Height(ReelOptions reel, string fullPath) =>
        MediaPathResolver.IsAudio(fullPath) ? null : reel.TranscodeHeight;

    /// <summary>The second a transcoded stream starts at; anything unusable starts at the beginning.</summary>
    public static double StartSeconds(double? start) =>
        start is > 0 and < double.PositiveInfinity ? start.Value : 0;
}
