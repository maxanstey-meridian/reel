namespace Reel.Server.Infrastructure.Ffmpeg;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.FileSystem;

public class FfmpegTrickplayGenerator : ITrickplayGenerator
{
    private const int IntervalSeconds = 10;
    private const int TileColumns = 10;
    private const int TileRows = 10;
    private const int MaxThumbsPerSheet = TileColumns * TileRows;
    private const int ThumbWidth = 320;

    private readonly string _cacheDirectory;
    private readonly ReelConfig _config;
    private readonly IMediaStore _mediaStore;
    private readonly MediaPathResolver _pathResolver;
    private readonly ILogger<FfmpegTrickplayGenerator> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public FfmpegTrickplayGenerator(
        ReelConfig config,
        IMediaStore mediaStore,
        MediaPathResolver pathResolver,
        ILogger<FfmpegTrickplayGenerator> logger
    )
    {
        _config = config;
        _mediaStore = mediaStore;
        _pathResolver = pathResolver;
        _logger = logger;
        _cacheDirectory = Path.Combine(config.MediaRoot, ".reel", "trickplay");
    }

    public async Task<TrickplayMetadata?> GetOrGenerateAsync(string relativePath, CancellationToken ct)
    {
        var fullPath = _pathResolver.ResolvePlayableFile(relativePath);
        if (fullPath is null)
            return null;

        Directory.CreateDirectory(_cacheDirectory);

        var fileLock = _locks.GetOrAdd(relativePath, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(ct);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var fingerprint = SourceFingerprint.Read(fullPath);
                var spriteKey = ComputeCacheKey(relativePath, fingerprint);
                var cached = await _mediaStore.GetTrickplayAsync(relativePath, ct);
                if (cached is not null && cached.SpritePath == spriteKey && AllSpritesExist(cached))
                    return cached;

                var duration = await GetDuration(fullPath, ct);
                if (duration is null or <= 0)
                {
                    _logger.LogWarning("Could not determine duration for {Path}", relativePath);
                    return null;
                }

                var totalThumbs = CalculateTotalThumbs(duration.Value);
                if (totalThumbs <= 0)
                    return null;

                var sheetCount = CalculateSheetCount(totalThumbs);
                for (var sheet = 0; sheet < sheetCount; sheet++)
                {
                    var rowsOnSheet = CalculateRowsOnSheet(totalThumbs, sheet);
                    var spritePath = GetSpriteFilePath(spriteKey, sheet);

                    var success = await GenerateSpriteSheet(fullPath, spritePath, sheet, rowsOnSheet, ct);
                    if (!success)
                    {
                        CleanupSprites(spriteKey, sheetCount);
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

                if (SourceFingerprint.Read(fullPath) != fingerprint)
                {
                    CleanupSprites(spriteKey, sheetCount);
                    continue;
                }

                await _mediaStore.SaveTrickplayAsync(relativePath, metadata, ct);
                return metadata;
            }

            return null;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private string GetSpriteFilePath(string spriteKey, int sheetIndex) =>
        Path.Combine(_cacheDirectory, $"{spriteKey}_{sheetIndex}.jpg");

    internal static string ComputeCacheKey(string relativePath, string fullPath) =>
        ComputeCacheKey(relativePath, SourceFingerprint.Read(fullPath));

    internal static string ComputeCacheKey(string relativePath, long size, DateTime lastWriteTimeUtc) =>
        ComputeCacheKey(relativePath, new SourceFingerprint(size, lastWriteTimeUtc));

    internal static string ComputeCacheKey(string relativePath) =>
        ComputeCacheKey(relativePath, new SourceFingerprint(0, DateTime.MinValue));

    private static string ComputeCacheKey(string relativePath, SourceFingerprint fingerprint) =>
        fingerprint.ComputeKey(relativePath);

    internal static int CalculateTotalThumbs(double durationSeconds) =>
        (int)Math.Ceiling(durationSeconds / IntervalSeconds);

    internal static int CalculateSheetCount(int totalThumbs) =>
        (int)Math.Ceiling((double)totalThumbs / MaxThumbsPerSheet);

    internal static int CalculateRowsOnSheet(int totalThumbs, int sheetIndex)
    {
        var thumbsOnSheet = Math.Min(MaxThumbsPerSheet, totalThumbs - sheetIndex * MaxThumbsPerSheet);
        return (int)Math.Ceiling((double)thumbsOnSheet / TileColumns);
    }

    protected virtual async Task<double?> GetDuration(string fullPath, CancellationToken ct)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(
                new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments =
                        $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{fullPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }, ct
            );

            if (result.ExitCode != 0)
            {
                _logger.LogWarning("ffprobe exited with code {ExitCode}", result.ExitCode);
                return null;
            }

            if (
                double.TryParse(result.StandardOutputText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration)
                && duration > 0
            )
                return duration;

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffprobe failed for {Path}", fullPath);
            return null;
        }
    }

    protected virtual async Task<bool> GenerateSpriteSheet(
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

            var result = await ProcessRunner.RunAsync(
                new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = arguments,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }, ct
            );

            if (result.ExitCode != 0)
            {
                _logger.LogWarning("ffmpeg trickplay exited with code {ExitCode}: {Error}", result.ExitCode, result.StandardError);

                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                return false;
            }

            return File.Exists(outputPath);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            throw;
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

    private bool AllSpritesExist(TrickplayMetadata metadata)
    {
        var sheetCount = CalculateSheetCount(metadata.TotalThumbs);
        return Enumerable.Range(0, sheetCount).All(sheet => File.Exists(GetSpriteFilePath(metadata.SpritePath, sheet)));
    }

    private void CleanupSprites(string spriteKey, int sheetCount)
    {
        for (var i = 0; i < sheetCount; i++)
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
