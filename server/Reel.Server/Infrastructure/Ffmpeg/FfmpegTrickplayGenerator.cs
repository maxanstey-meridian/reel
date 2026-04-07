namespace Reel.Server.Infrastructure.Ffmpeg;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;

public sealed class FfmpegTrickplayGenerator : ITrickplayGenerator
{
    private const int IntervalSeconds = 10;
    private const int TileColumns = 10;
    private const int TileRows = 10;
    private const int MaxThumbsPerSheet = TileColumns * TileRows;
    private const int ThumbWidth = 320;

    private readonly string _cacheDirectory;
    private readonly ReelConfig _config;
    private readonly IMediaStore _mediaStore;
    private readonly ILogger<FfmpegTrickplayGenerator> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public FfmpegTrickplayGenerator(
        ReelConfig config,
        IMediaStore mediaStore,
        ILogger<FfmpegTrickplayGenerator> logger
    )
    {
        _config = config;
        _mediaStore = mediaStore;
        _logger = logger;
        _cacheDirectory = Path.Combine(config.MediaRoot, ".reel", "trickplay");
    }

    public async Task<TrickplayMetadata?> GetOrGenerateAsync(string relativePath, CancellationToken ct)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_config.NormalisedMediaRoot, relativePath));

        if (!fullPath.StartsWith(_config.NormalisedMediaRoot, StringComparison.Ordinal))
            return null;

        // Check DB cache first
        var cached = await _mediaStore.GetTrickplayAsync(relativePath, ct);
        if (cached is not null)
        {
            // Verify at least the first sprite file still exists
            var firstSprite = GetSpriteFilePath(cached.SpritePath, 0);
            if (File.Exists(firstSprite))
                return cached;
        }

        Directory.CreateDirectory(_cacheDirectory);

        var fileLock = _locks.GetOrAdd(relativePath, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            cached = await _mediaStore.GetTrickplayAsync(relativePath, ct);
            if (cached is not null)
            {
                var firstSprite = GetSpriteFilePath(cached.SpritePath, 0);
                if (File.Exists(firstSprite))
                    return cached;
            }

            var duration = await GetDuration(fullPath, ct);
            if (duration is null or <= 0)
            {
                _logger.LogWarning("Could not determine duration for {Path}", relativePath);
                return null;
            }

            var totalThumbs = (int)Math.Ceiling(duration.Value / IntervalSeconds);
            if (totalThumbs <= 0)
                return null;

            var sheetCount = (int)Math.Ceiling((double)totalThumbs / MaxThumbsPerSheet);
            var spriteKey = ComputeCacheKey(relativePath);

            for (var sheet = 0; sheet < sheetCount; sheet++)
            {
                var thumbsOnSheet = Math.Min(MaxThumbsPerSheet, totalThumbs - sheet * MaxThumbsPerSheet);
                var rowsOnSheet = (int)Math.Ceiling((double)thumbsOnSheet / TileColumns);
                var spritePath = GetSpriteFilePath(spriteKey, sheet);

                var success = await GenerateSpriteSheet(fullPath, spritePath, sheet, rowsOnSheet, ct);
                if (!success)
                {
                    _logger.LogWarning(
                        "Failed to generate trickplay sheet {Sheet} for {Path}",
                        sheet,
                        relativePath
                    );
                    CleanupPartialSprites(spriteKey, sheet);
                    return null;
                }
            }

            // Use full grid rows for metadata — the last sheet may have fewer but the
            // client uses total_thumbs to know when to stop
            var metadata = new TrickplayMetadata(
                IntervalMs: IntervalSeconds * 1000,
                Columns: TileColumns,
                Rows: TileRows,
                ThumbWidth: ThumbWidth,
                ThumbHeight: 0, // Will be determined from actual output
                TotalThumbs: totalThumbs,
                SpritePath: spriteKey
            );

            // Read actual thumb height from the first sprite
            metadata = metadata with { ThumbHeight = GetThumbHeight(spriteKey, totalThumbs) };

            await _mediaStore.SaveTrickplayAsync(relativePath, metadata, ct);

            return metadata;
        }
        finally
        {
            fileLock.Release();
            _locks.TryRemove(relativePath, out _);
        }
    }

    private string GetSpriteFilePath(string spriteKey, int sheetIndex) =>
        Path.Combine(_cacheDirectory, $"{spriteKey}_{sheetIndex}.jpg");

    internal static string ComputeCacheKey(string relativePath)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(relativePath));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    internal static int CalculateTotalThumbs(double durationSeconds) =>
        (int)Math.Ceiling(durationSeconds / IntervalSeconds);

    internal static int CalculateSheetCount(int totalThumbs) =>
        (int)Math.Ceiling((double)totalThumbs / MaxThumbsPerSheet);

    internal static int CalculateRowsOnSheet(int totalThumbs, int sheetIndex)
    {
        var thumbsOnSheet = Math.Min(MaxThumbsPerSheet, totalThumbs - sheetIndex * MaxThumbsPerSheet);
        return (int)Math.Ceiling((double)thumbsOnSheet / TileColumns);
    }

    private async Task<double?> GetDuration(string fullPath, CancellationToken ct)
    {
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments =
                        $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{fullPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            );

            if (process is null)
            {
                _logger.LogWarning("Failed to start ffprobe process");
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ffprobe exited with code {ExitCode}", process.ExitCode);
                return null;
            }

            if (
                double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration)
                && duration > 0
            )
                return duration;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffprobe failed for {Path}", fullPath);
            return null;
        }
    }

    private async Task<bool> GenerateSpriteSheet(
        string fullPath,
        string outputPath,
        int sheetIndex,
        int rowsOnSheet,
        CancellationToken ct
    )
    {
        try
        {
            var startSeconds = sheetIndex * MaxThumbsPerSheet * IntervalSeconds;
            var durationSeconds = MaxThumbsPerSheet * IntervalSeconds;
            var startArg = startSeconds.ToString(CultureInfo.InvariantCulture);
            var durationArg = durationSeconds.ToString(CultureInfo.InvariantCulture);

            // -ss before -i for fast seeking, -t to limit duration for this sheet
            var arguments = startSeconds == 0
                ? $"-t {durationArg} -i \"{fullPath}\" -vf \"fps=1/{IntervalSeconds},scale={ThumbWidth}:-1,tile={TileColumns}x{rowsOnSheet}\" -frames:v 1 -y \"{outputPath}\""
                : $"-ss {startArg} -t {durationArg} -i \"{fullPath}\" -vf \"fps=1/{IntervalSeconds},scale={ThumbWidth}:-1,tile={TileColumns}x{rowsOnSheet}\" -frames:v 1 -y \"{outputPath}\"";

            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = arguments,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            );

            if (process is null)
            {
                _logger.LogWarning("Failed to start ffmpeg process");
                return false;
            }

            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ffmpeg trickplay exited with code {ExitCode}: {Error}", process.ExitCode, stderr);

                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                return false;
            }

            return File.Exists(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffmpeg trickplay failed for {Path}", fullPath);

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            return false;
        }
    }

    private int GetThumbHeight(string spriteKey, int totalThumbs)
    {
        var firstSprite = GetSpriteFilePath(spriteKey, 0);
        if (!File.Exists(firstSprite))
            return 180;

        var rowsOnFirst = CalculateRowsOnSheet(totalThumbs, 0);
        try
        {
            var imageHeight = ReadJpegHeight(firstSprite);
            if (imageHeight > 0 && rowsOnFirst > 0)
                return imageHeight / rowsOnFirst;
        }
        catch
        {
            // Fall through to estimate
        }

        return 180; // 320 / (16/9) fallback
    }

    private static int ReadJpegHeight(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> buf = stackalloc byte[2];

        // Verify JPEG SOI marker
        if (fs.Read(buf) < 2 || buf[0] != 0xFF || buf[1] != 0xD8)
            return 0;

        while (true)
        {
            // Read marker
            if (fs.Read(buf) < 2)
                return 0;

            if (buf[0] != 0xFF)
                return 0;

            var marker = buf[1];

            // Skip padding bytes
            while (marker == 0xFF)
            {
                if (fs.Read(buf.Slice(1, 1)) < 1)
                    return 0;
                marker = buf[1];
            }

            // SOF markers (SOF0-SOF3, SOF5-SOF7, SOF9-SOF11, SOF13-SOF15)
            if (marker is (>= 0xC0 and <= 0xC3) or (>= 0xC5 and <= 0xC7)
                or (>= 0xC9 and <= 0xCB) or (>= 0xCD and <= 0xCF))
            {
                // Read segment length (2) + precision (1) + height (2)
                Span<byte> sof = stackalloc byte[5];
                if (fs.Read(sof) < 5)
                    return 0;
                return (sof[3] << 8) | sof[4];
            }

            // Read segment length and skip
            if (fs.Read(buf) < 2)
                return 0;
            var segmentLength = (buf[0] << 8) | buf[1];
            if (segmentLength < 2)
                return 0;
            fs.Seek(segmentLength - 2, SeekOrigin.Current);
        }
    }

    private void CleanupPartialSprites(string spriteKey, int failedSheetIndex)
    {
        for (var i = 0; i < failedSheetIndex; i++)
        {
            var path = GetSpriteFilePath(spriteKey, i);
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch { /* best effort cleanup */ }
            }
        }
    }
}
