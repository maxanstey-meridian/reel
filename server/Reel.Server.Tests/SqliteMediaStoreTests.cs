using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Reel.Server.Storage;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly SqliteMediaStore _store;

    public SqliteMediaStoreTests() => _store = _media.MediaStore;

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task ReconcileAsync_CreatesMinimalMediaRecord()
    {
        await _store.ReconcileAsync(
            [new MediaRecord("Films/test.mp4", "Test", 2024, null)],
            DateTime.UtcNow
        );

        var record = (await _store.SearchAsync(null)).Should().ContainSingle().Subject;

        record.RelativePath.Should().Be("Films/test.mp4");
        record.ParsedTitle.Should().Be("Test");
        record.Year.Should().Be(2024);
    }

    [Fact]
    public async Task ReconcileAsync_DropsTagsOnItemsNoLongerInTheLibrary()
    {
        await _store.ReconcileAsync(
            [
                new MediaRecord("Shows/a.mkv", "A", null, null),
                new MediaRecord("Gone.mkv", "G", null, null),
            ],
            DateTime.UtcNow.AddMinutes(-1)
        );
        await _store.SetTagsAsync("Shows", ["comedy"]);
        await _store.SetTagsAsync("Shows/a.mkv", ["drama"]);
        await _store.SetTagsAsync("Gone.mkv", ["drama"]);
        await _store.SetTagsAsync("Sho", ["drama"]);

        await _store.ReconcileAsync(
            [new MediaRecord("Shows/a.mkv", "A", null, null)],
            DateTime.UtcNow
        );

        (await _store.GetTagsAsync()).Keys.Should().BeEquivalentTo("Shows", "Shows/a.mkv");
    }

    [Fact]
    public async Task EnsureIndexedAsync_AddsParsedRowForNewMedia()
    {
        await _store.EnsureIndexedAsync(
            new MediaRecord("Films/Inception.2010.1080p.mkv", "Inception", 2010, 512)
        );

        (await _store.SearchAsync(null))
            .Should()
            .Equal(new MediaRecord("Films/Inception.2010.1080p.mkv", "Inception", 2010, 512));
    }

    [Fact]
    public async Task EnsureIndexedAsync_LeavesExistingRowAndDependentsUntouched()
    {
        await _store.ReconcileAsync(
            [new MediaRecord("Films/a.mp4", "Kept Title", 1999, null)],
            DateTime.UtcNow
        );
        var progress = _media.ProgressStore;
        await progress.UpdateAsync("Films/a.mp4", 10, 100);

        await _store.EnsureIndexedAsync(new MediaRecord("Films/a.mp4", "a", null, 1));

        var record = (await _store.SearchAsync(null)).Should().ContainSingle().Subject;
        record.ParsedTitle.Should().Be("Kept Title");
        record.Year.Should().Be(1999);
        (await progress.GetAllAsync()).Should().ContainSingle();
    }

    // A scan that has already passed a folder cannot see a file added there, so the row that playback or
    // favouriting indexes meanwhile must survive the reconcile, along with its progress and favourite.
    [Fact]
    public async Task ReconcileAsync_KeepsRowsIndexedAfterTheScanStartedAndRemovesOlderMissingRows()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var store = new SqliteMediaStore(_media.Database, time);
        await store.EnsureIndexedAsync(new MediaRecord("Films/removed.mp4", "removed", null, 1));
        time.Advance(TimeSpan.FromMinutes(1));
        var scanStartedAt = time.GetUtcNow().UtcDateTime;
        time.Advance(TimeSpan.FromSeconds(30));
        await store.EnsureIndexedAsync(new MediaRecord("Films/added.mp4", "added", null, 1));
        await new SqliteProgressStore(_media.Database, time).UpdateAsync(
            "Films/added.mp4",
            10,
            100
        );
        await new SqliteFavouritesStore(_media.Database, time).AddAsync("Films/added.mp4");

        await store.ReconcileAsync(
            [new MediaRecord("Films/kept.mp4", "kept", null, 1)],
            scanStartedAt
        );

        (await store.SearchAsync(null))
            .Select(record => record.RelativePath)
            .Should()
            .Equal("Films/added.mp4", "Films/kept.mp4");
        (await _media.ProgressStore.GetAllAsync()).Should().ContainSingle();
        (await _media.FavouritesStore.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Timestamps_AreStoredAsRoundTripUtcText()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        await new SqliteMediaStore(_media.Database, time).EnsureIndexedAsync(
            new MediaRecord("a.mp4", "a", null, 1)
        );
        await new SqliteProgressStore(_media.Database, time).UpdateAsync("a.mp4", 1, 2);
        await new SqliteFavouritesStore(_media.Database, time).AddAsync("a.mp4");

        using var connection = _media.Database.GetConnection();
        connection
            .Query<string>(
                """
                SELECT indexed_at FROM media UNION ALL SELECT last_watched FROM progress
                UNION ALL SELECT added_at FROM favourites;
                """
            )
            .Should()
            .AllBe("2026-10-01T12:00:00.0000000Z");
    }
}
