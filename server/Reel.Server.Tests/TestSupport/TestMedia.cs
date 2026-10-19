using CliWrap;
using CliWrap.Buffered;

namespace Reel.Server.Tests;

// Real, tiny media made with ffmpeg so FFmpeg-backed code is tested against the actual tools.
internal static class TestMedia
{
    /// <summary>
    /// Gray video in the given codec (and pixel format, if set), with a stereo tone in the given audio codec if one
    /// is set.
    /// </summary>
    public static void CreateVideo(
        string path,
        int seconds = 2,
        string videoCodec = "mpeg4",
        string? audioCodec = null,
        string? pixelFormat = null
    ) =>
        Ffmpeg([
            "-f",
            "lavfi",
            "-i",
            $"color=c=gray:s=64x36:r=2:d={seconds}",
            .. audioCodec is null
                ? Array.Empty<string>()
                :
                [
                    "-f",
                    "lavfi",
                    "-i",
                    $"sine=d={seconds}:sample_rate=48000",
                    "-ac",
                    "2",
                    "-c:a",
                    audioCodec,
                ],
            "-c:v",
            videoCodec,
            .. pixelFormat is null ? Array.Empty<string>() : ["-pix_fmt", pixelFormat],
            path,
        ]);

    /// <summary>
    /// A stereo tone in the given audio codec with a tall PNG as MP4 cover art, which ffprobe lists as a video
    /// stream marked as an attached picture.
    /// </summary>
    public static void CreateAudioWithCoverArt(string path, string audioCodec)
    {
        var audio = $"{path}.audio.m4a";
        var cover = $"{path}.cover.png";
        try
        {
            // csharpier-ignore
            Ffmpeg(["-f", "lavfi", "-i", "sine=d=2:sample_rate=48000", "-ac", "2", "-c:a", audioCodec, audio]);
            Ffmpeg(["-f", "lavfi", "-i", "color=c=red:s=64x720", "-frames:v", "1", cover]);
            // csharpier-ignore
            Ffmpeg(["-i", audio, "-i", cover, "-map", "0", "-map", "1", "-c", "copy", "-disposition:v:0", "attached_pic", "-f", "mp4", path]);
        }
        finally
        {
            File.Delete(audio);
            File.Delete(cover);
        }
    }

    /// <summary>Video with one SRT track; tags such as "language=eng" are set on that track.</summary>
    public static void CreateVideoWithSubtitle(
        string path,
        string subtitleText,
        params string[] subtitleTags
    ) =>
        Ffmpeg(
            // csharpier-ignore
            [
                "-f",
                "lavfi",
                "-i",
                "color=c=gray:s=64x36:r=2:d=2",
                "-f",
                "srt",
                "-i",
                "pipe:0",
                "-map",
                "0",
                "-map",
                "1",
                "-c:v",
                "mpeg4",
                "-c:s",
                "srt",
                .. subtitleTags.SelectMany(tag => new[] { "-metadata:s:s:0", tag }),
                path,
            ],
            $"1\n00:00:00,500 --> 00:00:01,500\n{subtitleText}\n"
        );

    /// <summary>
    /// A named pipe nobody writes to: ffmpeg blocks opening it, so only a timeout ends the run.
    /// </summary>
    public static void CreateStalledSource(string path)
    {
        File.Delete(path);
        Cli.Wrap("mkfifo").WithArguments([path]).ExecuteAsync().GetAwaiter().GetResult();
    }

    private static void Ffmpeg(string[] arguments, string input = "") =>
        Cli.Wrap("ffmpeg")
            .WithArguments(["-v", "error", "-y", .. arguments])
            .WithStandardInputPipe(PipeSource.FromString(input))
            .ExecuteBufferedAsync()
            .GetAwaiter()
            .GetResult();
}
