using Reel.Server.Storage;

namespace Reel.Server.Media;

public sealed class FfmpegSubtitleExtractor(
    MediaPathResolver paths,
    TimeProvider time,
    ILogger<FfmpegSubtitleExtractor> logger
)
{
    // Extraction outlives the request: players give up after a few seconds, but large files take
    // longer to demux, and the cache must still fill so the next attempt succeeds.
    private static readonly TimeSpan GenerationTimeout = TimeSpan.FromMinutes(10);

    private readonly string _cacheDirectory = paths.AppDataPath("subtitles");
    private readonly SingleFlight<string?> _extractions = new();

    /// <summary>Path of a cached WebVTT conversion of one subtitle stream of a media or sidecar file.</summary>
    public Task<string?> ExtractAsync(string source, int stream, CancellationToken ct)
    {
        var cached = Path.Combine(_cacheDirectory, $"{SourceFingerprint.Key(source, stream)}.vtt");
        return _extractions.RunAsync(cached, () => ExtractToCacheAsync(source, stream, cached), ct);
    }

    private async Task<string?> ExtractToCacheAsync(string source, int stream, string cached)
    {
        if (File.Exists(cached))
        {
            return cached;
        }

        using var timeout = new CancellationTokenSource(GenerationTimeout, time);
        Directory.CreateDirectory(_cacheDirectory);
        var arguments = new[] { "-i", source, "-map", $"0:s:{stream}", "-f", "webvtt" };
        try
        {
            return await Ffmpeg.TryWriteAsync(cached, arguments, logger, timeout.Token)
                ? cached
                : null;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning(
                "Subtitle extraction from {Source} stream {Stream} ran out of time",
                source,
                stream
            );
            return null;
        }
    }
}
