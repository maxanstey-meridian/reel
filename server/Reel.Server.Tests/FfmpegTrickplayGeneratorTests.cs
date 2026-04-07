using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Ffmpeg;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class FfmpegTrickplayGeneratorTests : IDisposable
{
    private readonly string _mediaRoot;
    private readonly FfmpegTrickplayGenerator _generator;

    public FfmpegTrickplayGeneratorTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-trickplay-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _mediaRoot,
            SecretKey = "TEST",
            Port = 8080,
        };

        var db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        var mediaStore = new SqliteMediaStore(db);
        _generator = new FfmpegTrickplayGenerator(config, mediaStore, NullLogger<FfmpegTrickplayGenerator>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    [Fact]
    public async Task GetOrGenerateAsync_PathTraversal_ReturnsNull()
    {
        var result = await _generator.GetOrGenerateAsync("../../etc/passwd", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrGenerateAsync_NestedPathTraversal_ReturnsNull()
    {
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "Films"));

        var result = await _generator.GetOrGenerateAsync("Films/../../etc/passwd", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public void CalculateTotalThumbs_ShortVideo_CorrectCount()
    {
        // 60 seconds / 10s interval = 6 thumbs
        FfmpegTrickplayGenerator.CalculateTotalThumbs(60).Should().Be(6);
    }

    [Fact]
    public void CalculateTotalThumbs_ExactMultiple_NoExtra()
    {
        // 100 seconds / 10s interval = 10 thumbs
        FfmpegTrickplayGenerator.CalculateTotalThumbs(100).Should().Be(10);
    }

    [Fact]
    public void CalculateTotalThumbs_LongVideo_CorrectCount()
    {
        // 2 hours = 7200s / 10 = 720 thumbs
        FfmpegTrickplayGenerator.CalculateTotalThumbs(7200).Should().Be(720);
    }

    [Fact]
    public void CalculateTotalThumbs_FractionalDuration_CeilsUp()
    {
        // 65 seconds / 10 = 6.5 → 7 thumbs
        FfmpegTrickplayGenerator.CalculateTotalThumbs(65).Should().Be(7);
    }

    [Fact]
    public void CalculateSheetCount_SingleSheet_FitsInOne()
    {
        // 6 thumbs fits in one 10x10 sheet
        FfmpegTrickplayGenerator.CalculateSheetCount(6).Should().Be(1);
    }

    [Fact]
    public void CalculateSheetCount_ExactlyOneSheet()
    {
        // 100 thumbs = exactly one 10x10 sheet
        FfmpegTrickplayGenerator.CalculateSheetCount(100).Should().Be(1);
    }

    [Fact]
    public void CalculateSheetCount_OverOneSheet()
    {
        // 101 thumbs = 2 sheets
        FfmpegTrickplayGenerator.CalculateSheetCount(101).Should().Be(2);
    }

    [Fact]
    public void CalculateSheetCount_TwoHourFilm()
    {
        // 720 thumbs / 100 = 7.2 → 8 sheets
        FfmpegTrickplayGenerator.CalculateSheetCount(720).Should().Be(8);
    }

    [Fact]
    public void CalculateRowsOnSheet_FirstSheet_Full()
    {
        // 720 thumbs, sheet 0 → 100 thumbs → 10 rows
        FfmpegTrickplayGenerator.CalculateRowsOnSheet(720, 0).Should().Be(10);
    }

    [Fact]
    public void CalculateRowsOnSheet_LastSheet_PartialRows()
    {
        // 720 thumbs, sheet 7 → 720 - 700 = 20 thumbs → 2 rows
        FfmpegTrickplayGenerator.CalculateRowsOnSheet(720, 7).Should().Be(2);
    }

    [Fact]
    public void CalculateRowsOnSheet_SmallFile_SingleRow()
    {
        // 3 thumbs, sheet 0 → 1 row
        FfmpegTrickplayGenerator.CalculateRowsOnSheet(3, 0).Should().Be(1);
    }

    [Fact]
    public void CalculateRowsOnSheet_ExactlyOneRow()
    {
        // 10 thumbs, sheet 0 → 1 row
        FfmpegTrickplayGenerator.CalculateRowsOnSheet(10, 0).Should().Be(1);
    }

    [Fact]
    public void CalculateRowsOnSheet_11Thumbs_TwoRows()
    {
        // 11 thumbs, sheet 0 → 2 rows
        FfmpegTrickplayGenerator.CalculateRowsOnSheet(11, 0).Should().Be(2);
    }

    [Fact]
    public void ComputeCacheKey_Deterministic()
    {
        var key1 = FfmpegTrickplayGenerator.ComputeCacheKey("Films/movie.mp4");
        var key2 = FfmpegTrickplayGenerator.ComputeCacheKey("Films/movie.mp4");

        key1.Should().Be(key2);
    }

    [Fact]
    public void ComputeCacheKey_DifferentPaths_DifferentKeys()
    {
        var key1 = FfmpegTrickplayGenerator.ComputeCacheKey("Films/movie1.mp4");
        var key2 = FfmpegTrickplayGenerator.ComputeCacheKey("Films/movie2.mp4");

        key1.Should().NotBe(key2);
    }
}
