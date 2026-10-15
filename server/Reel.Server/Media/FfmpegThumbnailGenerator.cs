using Reel.Server.Storage;

namespace Reel.Server.Media;

public sealed class FfmpegThumbnailGenerator(
    MediaPathResolver paths,
    ILogger<FfmpegThumbnailGenerator> logger
)
{
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(20);

    // Keep browsing from launching a process for every visible card at once.
    private readonly SemaphoreSlim _workers = new(2);
    private readonly string _cacheDirectory = paths.AppDataPath("thumbnails");

    public async Task<string?> GetOrGenerateAsync(string source, CancellationToken ct)
    {
        var output = Path.Combine(_cacheDirectory, $"{SourceFingerprint.Key(source)}.jpg");
        if (File.Exists(output))
        {
            return output;
        }

        await _workers.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            // Second 30 skips most black intros; second 0 covers clips shorter than that.
            foreach (var second in new[] { "30", "0" })
            {
                if (File.Exists(output))
                {
                    return output;
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(AttemptTimeout);
                // csharpier-ignore
                string[] arguments =
                [
                    "-ss", second, "-i", source, "-frames:v", "1", "-vf", "scale=480:-2", "-threads", "1", "-f", "mjpeg",
                ];
                try
                {
                    if (await Ffmpeg.TryWriteAsync(output, arguments, logger, timeout.Token))
                    {
                        return output;
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            }

            return null;
        }
        finally
        {
            _workers.Release();
        }
    }
}
