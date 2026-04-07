using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreTrickplayTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly SqliteMediaStore _store;

    public SqliteMediaStoreTrickplayTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-trick-store-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080,
        };

        var db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _store = new SqliteMediaStore(db);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
    }

    [Fact]
    public async Task SaveAndGetTrickplay_Roundtrip()
    {
        var metadata = new TrickplayMetadata(
            IntervalMs: 10000,
            Columns: 10,
            Rows: 10,
            ThumbWidth: 320,
            ThumbHeight: 180,
            TotalThumbs: 72,
            SpritePath: "abc123"
        );

        await _store.SaveTrickplayAsync("Films/movie.mp4", metadata, CancellationToken.None);

        var result = await _store.GetTrickplayAsync("Films/movie.mp4", CancellationToken.None);

        result.Should().NotBeNull();
        result!.IntervalMs.Should().Be(10000);
        result.Columns.Should().Be(10);
        result.Rows.Should().Be(10);
        result.ThumbWidth.Should().Be(320);
        result.ThumbHeight.Should().Be(180);
        result.TotalThumbs.Should().Be(72);
        result.SpritePath.Should().Be("abc123");
    }

    [Fact]
    public async Task GetTrickplay_Nonexistent_ReturnsNull()
    {
        var result = await _store.GetTrickplayAsync("Films/nonexistent.mp4", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveTrickplay_ReplacesExisting()
    {
        var initial = new TrickplayMetadata(10000, 10, 10, 320, 180, 72, "abc123");
        await _store.SaveTrickplayAsync("Films/movie.mp4", initial, CancellationToken.None);

        var replacement = new TrickplayMetadata(10000, 10, 5, 320, 180, 42, "def456");
        await _store.SaveTrickplayAsync("Films/movie.mp4", replacement, CancellationToken.None);

        var result = await _store.GetTrickplayAsync("Films/movie.mp4", CancellationToken.None);

        result.Should().NotBeNull();
        result!.TotalThumbs.Should().Be(42);
        result.SpritePath.Should().Be("def456");
        result.Rows.Should().Be(5);
    }

    [Fact]
    public async Task SaveTrickplay_CreatesMediaRow()
    {
        var metadata = new TrickplayMetadata(10000, 10, 10, 320, 180, 72, "abc123");

        await _store.SaveTrickplayAsync("Films/new-movie.mp4", metadata, CancellationToken.None);

        var media = await _store.GetAsync("Films/new-movie.mp4", CancellationToken.None);
        media.Should().NotBeNull();
    }

    [Fact]
    public async Task DifferentMedia_IsolatedTrickplay()
    {
        await _store.SaveTrickplayAsync(
            "Films/movie1.mp4",
            new TrickplayMetadata(10000, 10, 10, 320, 180, 72, "key1"),
            CancellationToken.None
        );

        await _store.SaveTrickplayAsync(
            "Films/movie2.mp4",
            new TrickplayMetadata(10000, 10, 5, 320, 180, 42, "key2"),
            CancellationToken.None
        );

        var result1 = await _store.GetTrickplayAsync("Films/movie1.mp4", CancellationToken.None);
        var result2 = await _store.GetTrickplayAsync("Films/movie2.mp4", CancellationToken.None);

        result1!.TotalThumbs.Should().Be(72);
        result1.SpritePath.Should().Be("key1");

        result2!.TotalThumbs.Should().Be(42);
        result2.SpritePath.Should().Be("key2");
    }
}
