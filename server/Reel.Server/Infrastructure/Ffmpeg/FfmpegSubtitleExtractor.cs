namespace Reel.Server.Infrastructure.Ffmpeg;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Reel.Server.Application.Ports;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.FileSystem;

public sealed partial class FfmpegSubtitleExtractor : ISubtitleExtractor
{
    private readonly MediaPathResolver _pathResolver;
    private readonly ILogger<FfmpegSubtitleExtractor> _logger;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public FfmpegSubtitleExtractor(
        ReelConfig config,
        MediaPathResolver pathResolver,
        ILogger<FfmpegSubtitleExtractor> logger
    )
    {
        _pathResolver = pathResolver;
        _logger = logger;
        _cacheDirectory = Path.Combine(config.MediaRoot, ".reel", "subtitles");
    }

    public async Task<string?> ExtractAsync(
        string relativePath,
        int trackIndex,
        string outputFormat,
        CancellationToken ct
    )
    {
        var fullPath = _pathResolver.ResolvePlayableFile(relativePath);
        if (fullPath is null)
            return null;

        Directory.CreateDirectory(_cacheDirectory);

        var lockKey = $"{relativePath}:{trackIndex}:{outputFormat}";
        var fileLock = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(ct);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var fingerprint = SourceFingerprint.Read(fullPath);
                var cacheKey = fingerprint.ComputeKey(relativePath, trackIndex, outputFormat);
                var cachedPath = Path.Combine(_cacheDirectory, $"{cacheKey}.{outputFormat}");
                if (File.Exists(cachedPath))
                    return cachedPath;

                var temporaryPath = $"{cachedPath}.{Guid.NewGuid():N}.tmp";
                var success = await ExtractTrack(fullPath, trackIndex, outputFormat, temporaryPath, ct);
                if (!success)
                    return null;
                if (SourceFingerprint.Read(fullPath) != fingerprint)
                {
                    File.Delete(temporaryPath);
                    continue;
                }

                File.Move(temporaryPath, cachedPath, overwrite: true);
                return cachedPath;
            }

            return null;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public string ConvertSrtToVtt(string srtContent)
    {
        // Strip BOM
        var content = srtContent.TrimStart('\uFEFF');

        // Normalise line endings to \n
        content = content.Replace("\r\n", "\n").Replace('\r', '\n');

        var sb = new StringBuilder();
        sb.Append("WEBVTT\n\n");

        var blocks = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var first = true;

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                continue;

            // Find the timestamp line — skip the sequence number
            var startLineIndex = 0;
            if (TimestampPattern().IsMatch(lines[0]))
            {
                startLineIndex = 0;
            }
            else if (lines.Length >= 2 && TimestampPattern().IsMatch(lines[1]))
            {
                startLineIndex = 1;
            }
            else
            {
                continue;
            }

            if (!first)
                sb.Append('\n');
            first = false;

            // Timestamp line: replace commas with dots
            sb.Append(lines[startLineIndex].Replace(',', '.'));
            sb.Append('\n');

            // Text lines
            for (var i = startLineIndex + 1; i < lines.Length; i++)
            {
                sb.Append(lines[i]);
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    private async Task<bool> ExtractTrack(
        string fullPath,
        int trackIndex,
        string outputFormat,
        string outputPath,
        CancellationToken ct
    )
    {
        try
        {
            var result = await ProcessRunner.RunAsync(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{fullPath}\" -map 0:s:{trackIndex} -f {outputFormat} -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }, ct);

            if (result.ExitCode != 0)
            {
                _logger.LogWarning(
                    "ffmpeg subtitle extraction exited with code {ExitCode}: {Error}",
                    result.ExitCode,
                    result.StandardError
                );
                return false;
            }

            await File.WriteAllBytesAsync(outputPath, result.StandardOutput, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffmpeg subtitle extraction failed for {Path}", fullPath);

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            return false;
        }
    }

    [GeneratedRegex(@"\d{2}:\d{2}:\d{2}[,\.]\d{3}\s*-->")]
    private static partial Regex TimestampPattern();
}
