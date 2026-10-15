using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Reel.Server.Media;

namespace Reel.Server.Tests;

public sealed class FfmpegTrickplayGeneratorTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly FakeTimeProvider _time = new();
    private readonly FfmpegTrickplayGenerator _generator;

    public FfmpegTrickplayGeneratorTests() =>
        _generator = new FfmpegTrickplayGenerator(
            _media.Paths,
            _time,
            NullLogger<FfmpegTrickplayGenerator>.Instance
        );

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task GetOrGenerateAsync_CurrentCachedMetadata_IsReturnedWithoutFfmpeg()
    {
        // The media bytes are not a real video, so anything but a cache hit would fail.
        var fullPath = _media.Write("Films/cached.mp4");
        var metadata = new TrickplaySheets(
            10000,
            10,
            10,
            320,
            180,
            6,
            SourceFingerprint.Key(fullPath)
        );
        _media.Write(
            _generator.MetadataPath(metadata.SpritePath),
            JsonSerializer.SerializeToUtf8Bytes(metadata)
        );

        (await _generator.GetOrGenerateAsync(fullPath, CancellationToken.None))
            .Should()
            .Be(metadata);
    }

    [Fact]
    public async Task GetOrGenerateAsync_GeneratesSpritesAndMetadata_ForNamesWithQuotes()
    {
        var fullPath = _media.Write("Films/Quote \"Test\" -y.mp4");
        TestMedia.CreateVideo(fullPath, seconds: 25);

        var result = await _generator.GetOrGenerateAsync(fullPath, CancellationToken.None);

        result.Should().NotBeNull();
        result!.TotalThumbs.Should().Be(3);
        result.ThumbWidth.Should().Be(320);
        result.ThumbHeight.Should().Be(180);
        File.Exists(_generator.SpritePath(result, 0)).Should().BeTrue();
        File.Exists(_generator.MetadataPath(result.SpritePath)).Should().BeTrue();
    }

    [Fact]
    public async Task GetOrGenerateAsync_ConcurrentCalls_ShareOneResult()
    {
        var fullPath = _media.Write("Films/concurrent.mp4");
        TestMedia.CreateVideo(fullPath, seconds: 5);

        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 5)
                .Select(_ => _generator.GetOrGenerateAsync(fullPath, CancellationToken.None))
        );

        results.Should().AllSatisfy(result => result.Should().Be(results[0]));
        results[0].Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrGenerateAsync_AbandonedRequest_StillFillsTheCache()
    {
        var fullPath = _media.Write("Films/abandoned.mp4");
        TestMedia.CreateVideo(fullPath, seconds: 5);
        var metadataPath = _generator.MetadataPath(SourceFingerprint.Key(fullPath));
        using var request = new CancellationTokenSource();

        var abandoned = _generator.GetOrGenerateAsync(fullPath, request.Token);
        await request.CancelAsync();

        await abandoned.Invoking(task => task).Should().ThrowAsync<OperationCanceledException>();
        // A patient caller joins the run the abandoned one started.
        (await _generator.GetOrGenerateAsync(fullPath, CancellationToken.None))
            .Should()
            .NotBeNull();

        File.Exists(metadataPath).Should().BeTrue();
    }

    [Fact]
    public async Task GetOrGenerateAsync_RunningOutOfTime_IsUnavailable()
    {
        var fullPath = _media.Write("Films/slow.mp4");
        TestMedia.CreateVideo(fullPath, seconds: 5);

        var generation = _generator.GetOrGenerateAsync(fullPath, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(11));

        (await generation).Should().BeNull();
    }

    [Fact]
    public async Task GetOrGenerateAsync_AfterAnInterruptedRun_KeepsFinishedSheets()
    {
        var fullPath = _media.Write("Films/resumed.mp4");
        TestMedia.CreateVideo(fullPath, seconds: 5);
        var first = (await _generator.GetOrGenerateAsync(fullPath, CancellationToken.None))!;
        // Losing the metadata leaves the cache as a run cut short after its sheets were written.
        File.Delete(_generator.MetadataPath(first.SpritePath));
        var sprite = _generator.SpritePath(first, 0);
        var written = File.GetLastWriteTimeUtc(sprite);

        var resumed = await _generator.GetOrGenerateAsync(fullPath, CancellationToken.None);

        resumed.Should().Be(first);
        File.GetLastWriteTimeUtc(sprite).Should().Be(written);
    }

    [Theory]
    [InlineData(60, 6)]
    [InlineData(100, 10)]
    [InlineData(65, 7)]
    [InlineData(7200, 720)]
    public void CalculateTotalThumbs_CeilsToTenSecondIntervals(double duration, int expected)
    {
        FfmpegTrickplayGenerator.CalculateTotalThumbs(duration).Should().Be(expected);
    }

    [Theory]
    [InlineData(6, 1)]
    [InlineData(100, 1)]
    [InlineData(101, 2)]
    [InlineData(720, 8)]
    public void CalculateSheetCount_UsesTenByTenSheets(int totalThumbs, int expected)
    {
        FfmpegTrickplayGenerator.CalculateSheetCount(totalThumbs).Should().Be(expected);
    }

    [Theory]
    [InlineData(720, 0, 10)]
    [InlineData(720, 7, 2)]
    [InlineData(3, 0, 1)]
    [InlineData(10, 0, 1)]
    [InlineData(11, 0, 2)]
    public void CalculateRowsOnSheet_CoversRemainingThumbs(int totalThumbs, int sheet, int expected)
    {
        FfmpegTrickplayGenerator.CalculateRowsOnSheet(totalThumbs, sheet).Should().Be(expected);
    }
}
