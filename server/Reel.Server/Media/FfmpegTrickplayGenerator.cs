using System.Text.Json;
using Reel.Server.Storage;

namespace Reel.Server.Media;

public sealed class FfmpegTrickplayGenerator(
    MediaPathResolver paths,
    ILogger<FfmpegTrickplayGenerator> logger
)
{
    private const int IntervalSeconds = 10;
    private const int TileColumns = 10;
    private const int TileRows = 10;
    private const int MaxThumbsPerSheet = TileColumns * TileRows;
    private const int ThumbWidth = 320;

    // Generation outlives the request so an abandoned scrub still leaves sprites for the next one.
    private static readonly TimeSpan GenerationTimeout = TimeSpan.FromMinutes(10);

    private readonly string _cacheDirectory = paths.AppDataPath("trickplay");
    private readonly SingleFlight<TrickplaySheets?> _generations = new();

    /// <summary>Metadata whose sprites all exist, generating them if needed.</summary>
    public Task<TrickplaySheets?> GetOrGenerateAsync(string fullPath, CancellationToken ct)
    {
        var spriteKey = SourceFingerprint.Key(fullPath);
        return _generations.RunAsync(spriteKey, () => GenerateAsync(fullPath, spriteKey), ct);
    }

    public string SpritePath(TrickplaySheets metadata, int sheetIndex) =>
        SpritePath(metadata.SpritePath, sheetIndex);

    internal string MetadataPath(string spriteKey) =>
        Path.Combine(_cacheDirectory, $"{spriteKey}.json");

    internal static int CalculateTotalThumbs(double durationSeconds) =>
        (int)Math.Ceiling(durationSeconds / IntervalSeconds);

    internal static int CalculateSheetCount(int totalThumbs) =>
        (int)Math.Ceiling((double)totalThumbs / MaxThumbsPerSheet);

    internal static int CalculateRowsOnSheet(int totalThumbs, int sheetIndex)
    {
        var thumbsOnSheet = Math.Min(
            MaxThumbsPerSheet,
            totalThumbs - sheetIndex * MaxThumbsPerSheet
        );
        return (int)Math.Ceiling((double)thumbsOnSheet / TileColumns);
    }

    private async Task<TrickplaySheets?> GenerateAsync(string fullPath, string spriteKey)
    {
        using var timeout = new CancellationTokenSource(GenerationTimeout);
        var ct = timeout.Token;
        var metadataPath = MetadataPath(spriteKey);

        // The metadata file is written last, so its presence means every sprite sheet exists.
        if (File.Exists(metadataPath))
        {
            return JsonSerializer.Deserialize<TrickplaySheets>(
                await File.ReadAllTextAsync(metadataPath, ct)
            );
        }

        Directory.CreateDirectory(_cacheDirectory);
        var duration = (await Ffmpeg.ProbeAsync(fullPath, "v:0", logger, ct))?.Format?.Duration;
        if (duration is not > 0)
        {
            return null;
        }

        var totalThumbs = CalculateTotalThumbs(duration.Value);
        for (var sheet = 0; sheet < CalculateSheetCount(totalThumbs); sheet++)
        {
            if (
                !await Ffmpeg.TryWriteAsync(
                    SpritePath(spriteKey, sheet),
                    SheetArguments(fullPath, sheet, totalThumbs),
                    logger,
                    ct
                )
            )
            {
                return null;
            }
        }

        var sheetHeight = (await Ffmpeg.ProbeAsync(SpritePath(spriteKey, 0), "v:0", logger, ct))
            ?.Streams?.FirstOrDefault()
            ?.Height;
        if (sheetHeight is null)
        {
            return null;
        }

        // Rows is the full grid; clients stop at TotalThumbs on the last, shorter sheet.
        var metadata = new TrickplaySheets(
            IntervalMs: IntervalSeconds * 1000,
            Columns: TileColumns,
            Rows: TileRows,
            ThumbWidth: ThumbWidth,
            ThumbHeight: sheetHeight.Value / CalculateRowsOnSheet(totalThumbs, 0),
            TotalThumbs: totalThumbs,
            SpritePath: spriteKey
        );
        await File.WriteAllTextAsync($"{metadataPath}.tmp", JsonSerializer.Serialize(metadata), ct);
        File.Move($"{metadataPath}.tmp", metadataPath, overwrite: true);
        return metadata;
    }

    private string SpritePath(string spriteKey, int sheetIndex) =>
        Path.Combine(_cacheDirectory, $"{spriteKey}_{sheetIndex}.jpg");

    // -ss before -i seeks by keyframe, which is fast and precise enough for 10-second tiles.
    // csharpier-ignore
    private static string[] SheetArguments(string fullPath, int sheet, int totalThumbs) =>
        [
            "-ss", $"{sheet * MaxThumbsPerSheet * IntervalSeconds}",
            "-t", $"{MaxThumbsPerSheet * IntervalSeconds}",
            "-i", fullPath,
            "-vf", $"fps=1/{IntervalSeconds},scale={ThumbWidth}:-1,tile={TileColumns}x{CalculateRowsOnSheet(totalThumbs, sheet)}",
            "-frames:v", "1",
            "-f", "mjpeg",
        ];
}
