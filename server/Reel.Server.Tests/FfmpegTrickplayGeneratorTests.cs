using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Ffmpeg;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class FfmpegTrickplayGeneratorTests : IDisposable
{
    private readonly string _mediaRoot;
    private readonly FfmpegTrickplayGenerator _generator;
    private readonly SqliteMediaStore _mediaStore;
    private readonly ReelConfig _config;

    public FfmpegTrickplayGeneratorTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-trickplay-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        _config = new ReelConfig
        {
            MediaRoot = _mediaRoot,
            SecretKey = "TEST",
            Port = 8080,
        };

        var db = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);
        _mediaStore = new SqliteMediaStore(db);
        _generator = new FfmpegTrickplayGenerator(_config, _mediaStore, NullLogger<FfmpegTrickplayGenerator>.Instance);
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
    public async Task GetOrGenerateAsync_CacheHit_ReturnsCachedWithoutGenerating()
    {
        // Pre-seed metadata in DB
        const string relativePath = "Films/cached.mp4";
        var spriteKey = FfmpegTrickplayGenerator.ComputeCacheKey(relativePath);
        var metadata = new TrickplayMetadata(10000, 10, 10, 320, 180, 6, spriteKey);
        await _mediaStore.SaveTrickplayAsync(relativePath, metadata, CancellationToken.None);

        // Create the sprite file so cache validation passes
        var cacheDir = Path.Combine(_mediaRoot, ".reel", "trickplay");
        Directory.CreateDirectory(cacheDir);
        await File.WriteAllBytesAsync(Path.Combine(cacheDir, $"{spriteKey}_0.jpg"), new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });

        // Create the media file so path traversal guard passes
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "Films"));
        await File.WriteAllBytesAsync(Path.Combine(_mediaRoot, relativePath), new byte[10]);

        // Use a fake that would throw if generation were attempted
        var fake = new FakeTrickplayGenerator(_config, _mediaStore, shouldThrowOnGenerate: true);

        var result = await fake.GetOrGenerateAsync(relativePath, CancellationToken.None);

        result.Should().NotBeNull();
        result!.TotalThumbs.Should().Be(6);
        result.SpritePath.Should().Be(spriteKey);
    }

    [Fact]
    public async Task GetOrGenerateAsync_GeneratesAndPersistsMetadata()
    {
        const string relativePath = "Films/generate.mp4";
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "Films"));
        await File.WriteAllBytesAsync(Path.Combine(_mediaRoot, relativePath), new byte[10]);

        var fake = new FakeTrickplayGenerator(_config, _mediaStore, fakeDuration: 60.0);

        var result = await fake.GetOrGenerateAsync(relativePath, CancellationToken.None);

        result.Should().NotBeNull();
        result!.TotalThumbs.Should().Be(6); // 60s / 10s
        result.IntervalMs.Should().Be(10000);
        result.Columns.Should().Be(10);
        fake.GenerateCallCount.Should().Be(1); // Single sheet for 60s

        // Verify metadata was persisted to DB
        var persisted = await _mediaStore.GetTrickplayAsync(relativePath, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.TotalThumbs.Should().Be(6);
        persisted.SpritePath.Should().Be(result.SpritePath);
    }

    [Fact]
    public async Task GetOrGenerateAsync_MultiSheet_GeneratesAllSheets()
    {
        const string relativePath = "Films/long-movie.mp4";
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "Films"));
        await File.WriteAllBytesAsync(Path.Combine(_mediaRoot, relativePath), new byte[10]);

        // 1200s = 120 thumbs = 2 sheets
        var fake = new FakeTrickplayGenerator(_config, _mediaStore, fakeDuration: 1200.0);

        var result = await fake.GetOrGenerateAsync(relativePath, CancellationToken.None);

        result.Should().NotBeNull();
        result!.TotalThumbs.Should().Be(120);
        fake.GenerateCallCount.Should().Be(2); // Two sheets
    }

    [Fact]
    public async Task GetOrGenerateAsync_ConcurrentCalls_DeduplicateGeneration()
    {
        const string relativePath = "Films/concurrent.mp4";
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "Films"));
        await File.WriteAllBytesAsync(Path.Combine(_mediaRoot, relativePath), new byte[10]);

        // Use a slow fake that delays generation to expose concurrency
        var fake = new FakeTrickplayGenerator(_config, _mediaStore, fakeDuration: 30.0, generateDelay: TimeSpan.FromMilliseconds(200));

        // Fire 5 concurrent requests
        var tasks = Enumerable
            .Range(0, 5)
            .Select(_ => fake.GetOrGenerateAsync(relativePath, CancellationToken.None))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // All should return a result
        results.Should().AllSatisfy(r => r.Should().NotBeNull());

        // Only one should have actually generated (the rest hit the double-check cache)
        // 30s = 3 thumbs = 1 sheet, so exactly 1 generate call
        fake.GenerateCallCount.Should().Be(1);
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

    private sealed class FakeTrickplayGenerator : FfmpegTrickplayGenerator
    {
        private readonly double? _fakeDuration;
        private readonly bool _shouldThrowOnGenerate;
        private readonly TimeSpan _generateDelay;
        private int _generateCallCount;

        public int GenerateCallCount => _generateCallCount;

        public FakeTrickplayGenerator(
            ReelConfig config,
            IMediaStore mediaStore,
            double? fakeDuration = null,
            bool shouldThrowOnGenerate = false,
            TimeSpan generateDelay = default
        )
            : base(config, mediaStore, NullLogger<FfmpegTrickplayGenerator>.Instance)
        {
            _fakeDuration = fakeDuration;
            _shouldThrowOnGenerate = shouldThrowOnGenerate;
            _generateDelay = generateDelay;
        }

        protected override Task<double?> GetDuration(string fullPath, CancellationToken ct)
        {
            if (_shouldThrowOnGenerate)
                throw new InvalidOperationException("GetDuration should not be called on cache hit");
            return Task.FromResult(_fakeDuration);
        }

        protected override async Task<bool> GenerateSpriteSheet(
            string fullPath,
            string outputPath,
            int sheetIndex,
            int rowsOnSheet,
            CancellationToken ct
        )
        {
            if (_shouldThrowOnGenerate)
                throw new InvalidOperationException("GenerateSpriteSheet should not be called on cache hit");

            if (_generateDelay > TimeSpan.Zero)
                await Task.Delay(_generateDelay, ct);

            Interlocked.Increment(ref _generateCallCount);

            // Write a minimal valid JPEG (SOI + SOF0 with 180px height + EOI)
            var height = 180 * rowsOnSheet;
            var sof = new byte[]
            {
                0xFF, 0xD8, // SOI
                0xFF, 0xC0, // SOF0
                0x00, 0x0B, // segment length = 11
                0x08,       // precision
                (byte)(height >> 8), (byte)(height & 0xFF), // height
                0x01, 0x40, // width = 320
                0x01,       // components
                0x01, 0x11, 0x00, // component 1
                0xFF, 0xD9, // EOI
            };
            await File.WriteAllBytesAsync(outputPath, sof, ct);
            return true;
        }
    }
}
