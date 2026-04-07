namespace Reel.Server.Infrastructure.Ffmpeg;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Reel.Server.Application.Ports;
using Reel.Server.Infrastructure.Configuration;

public sealed partial class FfmpegSubtitleExtractor : ISubtitleExtractor
{
    private readonly ReelConfig _config;
    private readonly ILogger<FfmpegSubtitleExtractor> _logger;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public FfmpegSubtitleExtractor(ReelConfig config, ILogger<FfmpegSubtitleExtractor> logger)
    {
        _config = config;
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
        var fullPath = Path.GetFullPath(Path.Combine(_config.NormalisedMediaRoot, relativePath));
        if (!fullPath.StartsWith(_config.NormalisedMediaRoot, StringComparison.Ordinal))
            return null;

        if (!File.Exists(fullPath))
            return null;

        var cacheKey = ComputeCacheKey(relativePath, trackIndex, outputFormat);
        var cachedPath = Path.Combine(_cacheDirectory, $"{cacheKey}.{outputFormat}");

        if (File.Exists(cachedPath))
            return cachedPath;

        Directory.CreateDirectory(_cacheDirectory);

        var lockKey = $"{relativePath}:{trackIndex}:{outputFormat}";
        var fileLock = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (File.Exists(cachedPath))
                return cachedPath;

            var success = await ExtractTrack(fullPath, trackIndex, outputFormat, cachedPath, ct);
            return success ? cachedPath : null;
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

    private static string ComputeCacheKey(string relativePath, int trackIndex, string outputFormat)
    {
        var input = $"{relativePath}:{trackIndex}:{outputFormat}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{fullPath}\" -map 0:s:{trackIndex} -f {outputFormat} -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                _logger.LogWarning("Failed to start ffmpeg process");
                return false;
            }

            // Drain stdout and stderr concurrently to avoid deadlock
            // when stderr exceeds the OS pipe buffer
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            using var ms = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(ms, ct);
            var stderr = await stderrTask;
            var outputBytes = ms.ToArray();
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning(
                    "ffmpeg subtitle extraction exited with code {ExitCode}: {Error}",
                    process.ExitCode,
                    stderr
                );
                return false;
            }

            await File.WriteAllBytesAsync(outputPath, outputBytes, ct);
            return true;
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
