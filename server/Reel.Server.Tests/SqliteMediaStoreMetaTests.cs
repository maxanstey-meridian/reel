using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Application.Models;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreMetaTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteMediaStore _store;

    public SqliteMediaStoreMetaTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-meta-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080,
        };

        _db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _store = new SqliteMediaStore(_db);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
    }

    [Fact]
    public async Task UpdateMetaAsync_UpdatesOnlySpecifiedFields()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test Film", 2024, CancellationToken.None);

        await _store.UpdateMetaAsync(
            "Films/test.mp4",
            new MediaUpdateRequest(Genre: "Action", Rating: null, Language: null, Notes: null, DisplayTitle: null),
            CancellationToken.None
        );

        var record = await _store.GetAsync("Films/test.mp4", CancellationToken.None);
        record.Should().NotBeNull();
        record!.Genre.Should().Be("Action");
        record.Rating.Should().BeNull();
        record.Language.Should().BeNull();
        record.Notes.Should().BeNull();
        record.DisplayTitle.Should().BeNull();
        record.ParsedTitle.Should().Be("Test Film");
    }

    [Fact]
    public async Task UpdateMetaAsync_MultipleFieldsAtOnce()
    {
        await _store.UpsertFromScanAsync("Films/multi.mp4", "Multi", 2023, CancellationToken.None);

        await _store.UpdateMetaAsync(
            "Films/multi.mp4",
            new MediaUpdateRequest(
                Genre: "Comedy",
                Rating: "PG-13",
                Language: null,
                Notes: "Great film",
                DisplayTitle: null
            ),
            CancellationToken.None
        );

        var record = await _store.GetAsync("Films/multi.mp4", CancellationToken.None);
        record.Should().NotBeNull();
        record!.Genre.Should().Be("Comedy");
        record.Rating.Should().Be("PG-13");
        record.Notes.Should().Be("Great film");
        record.Language.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMetaAsync_PreservesUnspecifiedFields()
    {
        await _store.UpsertFromScanAsync("Films/preserve.mp4", "Preserve", 2022, CancellationToken.None);

        // First update: set genre and rating
        await _store.UpdateMetaAsync(
            "Films/preserve.mp4",
            new MediaUpdateRequest(Genre: "Horror", Rating: "R", Language: null, Notes: null, DisplayTitle: null),
            CancellationToken.None
        );

        // Second update: set only language — genre and rating should be preserved
        await _store.UpdateMetaAsync(
            "Films/preserve.mp4",
            new MediaUpdateRequest(Genre: null, Rating: null, Language: "en", Notes: null, DisplayTitle: null),
            CancellationToken.None
        );

        var record = await _store.GetAsync("Films/preserve.mp4", CancellationToken.None);
        record.Should().NotBeNull();
        record!.Genre.Should().Be("Horror");
        record.Rating.Should().Be("R");
        record.Language.Should().Be("en");
    }

    [Fact]
    public async Task UpdateMetaAsync_NoFieldsProvided_NoOp()
    {
        await _store.UpsertFromScanAsync("Films/noop.mp4", "NoOp", 2021, CancellationToken.None);

        var before = await _store.GetAsync("Films/noop.mp4", CancellationToken.None);

        await _store.UpdateMetaAsync(
            "Films/noop.mp4",
            new MediaUpdateRequest(Genre: null, Rating: null, Language: null, Notes: null, DisplayTitle: null),
            CancellationToken.None
        );

        var after = await _store.GetAsync("Films/noop.mp4", CancellationToken.None);
        after!.Genre.Should().Be(before!.Genre);
        after.Rating.Should().Be(before.Rating);
    }

    [Fact]
    public async Task UpdateMetaAsync_NonexistentPath_NoError()
    {
        // Should not throw — UPDATE on non-existent row is a no-op
        await _store.UpdateMetaAsync(
            "nonexistent.mp4",
            new MediaUpdateRequest(Genre: "Action", Rating: null, Language: null, Notes: null, DisplayTitle: null),
            CancellationToken.None
        );

        var record = await _store.GetAsync("nonexistent.mp4", CancellationToken.None);
        record.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMetaAsync_SetsDisplayTitle()
    {
        await _store.UpsertFromScanAsync("Films/title.mp4", "Auto Title", 2024, CancellationToken.None);

        await _store.UpdateMetaAsync(
            "Films/title.mp4",
            new MediaUpdateRequest(
                Genre: null,
                Rating: null,
                Language: null,
                Notes: null,
                DisplayTitle: "My Custom Title"
            ),
            CancellationToken.None
        );

        var record = await _store.GetAsync("Films/title.mp4", CancellationToken.None);
        record!.DisplayTitle.Should().Be("My Custom Title");
        record.ParsedTitle.Should().Be("Auto Title");
    }

    [Fact]
    public async Task UpdateTmdbCacheAsync_StoresAllTmdbFields()
    {
        await _store.UpsertFromScanAsync("Films/tmdb.mp4", "TMDB Film", 2024, CancellationToken.None);

        await _store.UpdateTmdbCacheAsync(
            "Films/tmdb.mp4",
            tmdbId: 12345,
            posterUrl: "https://image.tmdb.org/poster.jpg",
            backdropUrl: "https://image.tmdb.org/backdrop.jpg",
            overview: "A great film about testing.",
            releaseDate: "2024-01-15",
            CancellationToken.None
        );

        var record = await _store.GetAsync("Films/tmdb.mp4", CancellationToken.None);
        record.Should().NotBeNull();
        record!.TmdbId.Should().Be(12345);
        record.PosterUrl.Should().Be("https://image.tmdb.org/poster.jpg");
        record.BackdropUrl.Should().Be("https://image.tmdb.org/backdrop.jpg");
        record.Overview.Should().Be("A great film about testing.");
        record.ReleaseDate.Should().Be("2024-01-15");
    }

    [Fact]
    public async Task UpdateTmdbCacheAsync_OverwritesPreviousCache()
    {
        await _store.UpsertFromScanAsync("Films/tmdb2.mp4", "TMDB2", 2024, CancellationToken.None);

        await _store.UpdateTmdbCacheAsync(
            "Films/tmdb2.mp4",
            tmdbId: 111,
            posterUrl: "old-poster.jpg",
            backdropUrl: null,
            overview: "Old overview",
            releaseDate: "2020-01-01",
            CancellationToken.None
        );

        await _store.UpdateTmdbCacheAsync(
            "Films/tmdb2.mp4",
            tmdbId: 222,
            posterUrl: "new-poster.jpg",
            backdropUrl: "new-backdrop.jpg",
            overview: "New overview",
            releaseDate: "2024-06-01",
            CancellationToken.None
        );

        var record = await _store.GetAsync("Films/tmdb2.mp4", CancellationToken.None);
        record!.TmdbId.Should().Be(222);
        record.PosterUrl.Should().Be("new-poster.jpg");
        record.BackdropUrl.Should().Be("new-backdrop.jpg");
        record.Overview.Should().Be("New overview");
        record.ReleaseDate.Should().Be("2024-06-01");
    }

    [Fact]
    public async Task UpdateTmdbCacheAsync_WithNullOptionalFields()
    {
        await _store.UpsertFromScanAsync("Films/tmdb3.mp4", "TMDB3", 2024, CancellationToken.None);

        await _store.UpdateTmdbCacheAsync(
            "Films/tmdb3.mp4",
            tmdbId: 999,
            posterUrl: null,
            backdropUrl: null,
            overview: null,
            releaseDate: null,
            CancellationToken.None
        );

        var record = await _store.GetAsync("Films/tmdb3.mp4", CancellationToken.None);
        record!.TmdbId.Should().Be(999);
        record.PosterUrl.Should().BeNull();
        record.BackdropUrl.Should().BeNull();
        record.Overview.Should().BeNull();
        record.ReleaseDate.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMetaAsync_UpdatesTimestamp()
    {
        await _store.UpsertFromScanAsync("Films/ts.mp4", "Timestamp", 2024, CancellationToken.None);
        var before = await _store.GetAsync("Films/ts.mp4", CancellationToken.None);

        // Small delay to ensure timestamp difference
        await Task.Delay(50);

        await _store.UpdateMetaAsync(
            "Films/ts.mp4",
            new MediaUpdateRequest(Genre: "Drama", Rating: null, Language: null, Notes: null, DisplayTitle: null),
            CancellationToken.None
        );

        var after = await _store.GetAsync("Films/ts.mp4", CancellationToken.None);
        after!.UpdatedAt.Should().BeOnOrAfter(before!.UpdatedAt);
    }
}
