namespace Reel.Server.Infrastructure.Ffmpeg;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Reel.Server.Application.Ports;
using Reel.Server.Infrastructure.Configuration;

public sealed class FfmpegThumbnailGenerator : IThumbnailGenerator
{
    private readonly string _cacheDirectory;
    private readonly ReelConfig _config;
    private readonly ILogger<FfmpegThumbnailGenerator> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public FfmpegThumbnailGenerator(ReelConfig config, ILogger<FfmpegThumbnailGenerator> logger)
    {
        _config = config;
        _logger = logger;
        _cacheDirectory = Path.Combine(Path.GetTempPath(), "reel-thumbnails");
    }

    public async Task<string?> GetOrGenerateAsync(string relativePath, CancellationToken ct)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_config.NormalisedMediaRoot, relativePath));

        if (!fullPath.StartsWith(_config.NormalisedMediaRoot, StringComparison.Ordinal))
            return null;

        var cacheKey = ComputeCacheKey(relativePath);
        var cachedPath = Path.Combine(_cacheDirectory, $"{cacheKey}.jpg");

        if (File.Exists(cachedPath))
            return cachedPath;

        Directory.CreateDirectory(_cacheDirectory);

        var fileLock = _locks.GetOrAdd(relativePath, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (File.Exists(cachedPath))
                return cachedPath;

            var seekSeconds = await GetSeekPosition(fullPath);
            var success = await ExtractThumbnail(fullPath, cachedPath, seekSeconds);

            return success ? cachedPath : null;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private static string ComputeCacheKey(string relativePath)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(relativePath));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<double> GetSeekPosition(string fullPath)
    {
        const double fallbackSeconds = 5.0;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{fullPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                _logger.LogWarning("Failed to start ffprobe process");
                return fallbackSeconds;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ffprobe exited with code {ExitCode}", process.ExitCode);
                return fallbackSeconds;
            }

            if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) && duration > 0)
                return duration * 0.1;

            return fallbackSeconds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffprobe failed for {Path}", fullPath);
            return fallbackSeconds;
        }
    }

    private async Task<bool> ExtractThumbnail(string fullPath, string outputPath, double seekSeconds)
    {
        try
        {
            var seekArg = seekSeconds.ToString("F2", CultureInfo.InvariantCulture);

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-ss {seekArg} -i \"{fullPath}\" -vframes 1 -vf scale=320:-1 -q:v 5 -y \"{outputPath}\"",
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                _logger.LogWarning("Failed to start ffmpeg process");
                return false;
            }

            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ffmpeg exited with code {ExitCode}: {Error}", process.ExitCode, stderr);

                // Clean up partial output
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                return false;
            }

            return File.Exists(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffmpeg failed for {Path}", fullPath);

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            return false;
        }
    }
}
