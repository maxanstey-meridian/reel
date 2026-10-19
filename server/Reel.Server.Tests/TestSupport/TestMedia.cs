using CliWrap;
using CliWrap.Buffered;

namespace Reel.Server.Tests;

// Real, tiny media made with ffmpeg so FFmpeg-backed code is tested against the actual tools.
internal static class TestMedia
{
    public static void CreateVideo(string path, int seconds) =>
        Ffmpeg([
            "-f",
            "lavfi",
            "-i",
            $"color=c=gray:s=64x36:r=2:d={seconds}",
            "-c:v",
            "mpeg4",
            path,
        ]);

    /// <summary>Two seconds of video in the given codec, with a stereo tone in the given audio codec if one is set.</summary>
    public static void CreateVideo(string path, string videoCodec, string? audioCodec) =>
        Ffmpeg(
        // csharpier-ignore
        [
            "-f",
            "lavfi",
            "-i",
            "color=c=gray:s=64x36:r=10:d=2",
            .. audioCodec is null
                ? new[] { "-c:v", videoCodec }
                :
                [
                    "-f",
                    "lavfi",
                    "-i",
                    "sine=d=2:sample_rate=48000",
                    "-ac",
                    "2",
                    "-c:v",
                    videoCodec,
                    "-c:a",
                    audioCodec,
                ],
            path,
        ]);

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
