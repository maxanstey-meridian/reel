using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Application.Models;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreSearchTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteMediaStore _store;
    private readonly SqliteProgressStore _progressStore;

    public SqliteMediaStoreSearchTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-search-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080,
        };

        _db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _store = new SqliteMediaStore(_db);
        _progressStore = new SqliteProgressStore(_db);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
    }

    [Fact]
    public async Task SearchAsync_ByTitleSubstring_MatchesParsedTitle()
    {
        await _store.UpsertFromScanAsync("Films/inception.mp4", "Inception", 2010, CancellationToken.None);
        await _store.UpsertFromScanAsync("Films/interstellar.mp4", "Interstellar", 2014, CancellationToken.None);
        await _store.UpsertFromScanAsync("Films/matrix.mp4", "The Matrix", 1999, CancellationToken.None);

        var results = await _store.SearchAsync(1, new SearchQuery("incep", null, null, null), CancellationToken.None);

        results.Should().ContainSingle();
        results[0].RelativePath.Should().Be("Films/inception.mp4");
        results[0].ParsedTitle.Should().Be("Inception");
    }

    [Fact]
    public async Task SearchAsync_ByTitleSubstring_CaseInsensitive()
    {
        await _store.UpsertFromScanAsync("Films/inception.mp4", "Inception", 2010, CancellationToken.None);

        var results = await _store.SearchAsync(1, new SearchQuery("INCEP", null, null, null), CancellationToken.None);

        results.Should().ContainSingle();
        results[0].RelativePath.Should().Be("Films/inception.mp4");
    }

    [Fact]
    public async Task SearchAsync_ByTitleSubstring_MatchesDisplayTitle()
    {
        await _store.UpsertFromScanAsync("Films/movie.mp4", "Raw Name", 2020, CancellationToken.None);
        await _store.UpdateMetaAsync(
            "Films/movie.mp4",
            new MediaUpdateRequest(null, null, null, null, "Custom Display Title"),
            CancellationToken.None
        );

        var results = await _store.SearchAsync(
            1,
            new SearchQuery("Custom Display", null, null, null),
            CancellationToken.None
        );

        results.Should().ContainSingle();
        results[0].RelativePath.Should().Be("Films/movie.mp4");
    }

    [Fact]
    public async Task SearchAsync_TagFilter_ScopedToUser()
    {
        // Create user 2 for isolation test
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (name) VALUES ('User 2');";
        cmd.ExecuteNonQuery();
        conn.Dispose();

        await _store.UpsertFromScanAsync("Films/a.mp4", "Film A", 2020, CancellationToken.None);
        await _store.UpsertFromScanAsync("Films/b.mp4", "Film B", 2021, CancellationToken.None);

        await _store.AddTagAsync(1, "Films/a.mp4", "action", CancellationToken.None);
        await _store.AddTagAsync(2, "Films/b.mp4", "action", CancellationToken.None);

        var user1Results = await _store.SearchAsync(
            1,
            new SearchQuery(null, "action", null, null),
            CancellationToken.None
        );
        var user2Results = await _store.SearchAsync(
            2,
            new SearchQuery(null, "action", null, null),
            CancellationToken.None
        );

        user1Results.Should().ContainSingle().Which.RelativePath.Should().Be("Films/a.mp4");
        user2Results.Should().ContainSingle().Which.RelativePath.Should().Be("Films/b.mp4");
    }

    [Fact]
    public async Task SearchAsync_GenreExactMatch()
    {
        await _store.UpsertFromScanAsync("Films/a.mp4", "Film A", 2020, CancellationToken.None);
        await _store.UpsertFromScanAsync("Films/b.mp4", "Film B", 2021, CancellationToken.None);

        await _store.UpdateMetaAsync(
            "Films/a.mp4",
            new MediaUpdateRequest("Action", null, null, null, null),
            CancellationToken.None
        );
        await _store.UpdateMetaAsync(
            "Films/b.mp4",
            new MediaUpdateRequest("Comedy", null, null, null, null),
            CancellationToken.None
        );

        var results = await _store.SearchAsync(
            1,
            new SearchQuery(null, null, "Action", null),
            CancellationToken.None
        );

        results.Should().ContainSingle().Which.RelativePath.Should().Be("Films/a.mp4");
    }

    [Fact]
    public async Task SearchAsync_WatchedTrue_ReturnsWatchedOnly()
    {
        await _store.UpsertFromScanAsync("Films/watched.mp4", "Watched Film", 2020, CancellationToken.None);
        await _store.UpsertFromScanAsync("Films/unwatched.mp4", "Unwatched Film", 2021, CancellationToken.None);

        // 95% progress = watched
        await _progressStore.UpdateAsync(1, "Films/watched.mp4", 95, 100, CancellationToken.None);
        // 10% progress = not watched
        await _progressStore.UpdateAsync(1, "Films/unwatched.mp4", 10, 100, CancellationToken.None);

        var results = await _store.SearchAsync(1, new SearchQuery(null, null, null, true), CancellationToken.None);

        results.Should().ContainSingle().Which.RelativePath.Should().Be("Films/watched.mp4");
    }

    [Fact]
    public async Task SearchAsync_WatchedFalse_ReturnsUnwatchedOnly()
    {
        await _store.UpsertFromScanAsync("Films/watched.mp4", "Watched Film", 2020, CancellationToken.None);
        await _store.UpsertFromScanAsync("Films/unwatched.mp4", "Unwatched Film", 2021, CancellationToken.None);

        await _progressStore.UpdateAsync(1, "Films/watched.mp4", 95, 100, CancellationToken.None);
        await _progressStore.UpdateAsync(1, "Films/unwatched.mp4", 10, 100, CancellationToken.None);

        var results = await _store.SearchAsync(1, new SearchQuery(null, null, null, false), CancellationToken.None);

        // unwatched.mp4 has progress < 90% so not watched, and items with no progress also count as not watched
        results.Select(r => r.RelativePath).Should().Contain("Films/unwatched.mp4");
        results.Select(r => r.RelativePath).Should().NotContain("Films/watched.mp4");
    }

    [Fact]
    public async Task SearchAsync_WatchedFilter_RespectsExplicitOverride()
    {
        await _store.UpsertFromScanAsync("Films/movie.mp4", "Movie", 2020, CancellationToken.None);

        // 95% progress would make it watched, but override to unwatched
        await _progressStore.UpdateAsync(1, "Films/movie.mp4", 95, 100, CancellationToken.None);
        await _store.SetWatchStateAsync(1, "Films/movie.mp4", "unwatched", CancellationToken.None);

        var watchedResults = await _store.SearchAsync(
            1,
            new SearchQuery(null, null, null, true),
            CancellationToken.None
        );
        var unwatchedResults = await _store.SearchAsync(
            1,
            new SearchQuery(null, null, null, false),
            CancellationToken.None
        );

        watchedResults.Should().BeEmpty();
        unwatchedResults.Should().ContainSingle().Which.RelativePath.Should().Be("Films/movie.mp4");
    }

    [Fact]
    public async Task SearchAsync_CombinedFilters()
    {
        await _store.UpsertFromScanAsync("Films/a.mp4", "Action Hero", 2020, CancellationToken.None);
        await _store.UpsertFromScanAsync("Films/b.mp4", "Action Villain", 2021, CancellationToken.None);
        await _store.UpsertFromScanAsync("Films/c.mp4", "Comedy Show", 2022, CancellationToken.None);

        await _store.UpdateMetaAsync(
            "Films/a.mp4",
            new MediaUpdateRequest("Action", null, null, null, null),
            CancellationToken.None
        );
        await _store.UpdateMetaAsync(
            "Films/b.mp4",
            new MediaUpdateRequest("Action", null, null, null, null),
            CancellationToken.None
        );

        await _store.AddTagAsync(1, "Films/a.mp4", "favourite", CancellationToken.None);

        var results = await _store.SearchAsync(
            1,
            new SearchQuery("Action", "favourite", "Action", null),
            CancellationToken.None
        );

        results.Should().ContainSingle().Which.RelativePath.Should().Be("Films/a.mp4");
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsAllMedia()
    {
        await _store.UpsertFromScanAsync("Films/a.mp4", "Film A", 2020, CancellationToken.None);
        await _store.UpsertFromScanAsync("Films/b.mp4", "Film B", 2021, CancellationToken.None);

        var results = await _store.SearchAsync(1, new SearchQuery(null, null, null, null), CancellationToken.None);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmptyArray()
    {
        await _store.UpsertFromScanAsync("Films/a.mp4", "Film A", 2020, CancellationToken.None);

        var results = await _store.SearchAsync(
            1,
            new SearchQuery("nonexistent", null, null, null),
            CancellationToken.None
        );

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ReturnsCorrectFileNodeShape()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test Movie", 2023, CancellationToken.None);

        var results = await _store.SearchAsync(1, new SearchQuery(null, null, null, null), CancellationToken.None);

        var node = results.Should().ContainSingle().Subject;
        node.Name.Should().Be("test.mp4");
        node.Type.Should().Be(Domain.FileNodeType.File);
        node.RelativePath.Should().Be("Films/test.mp4");
        node.Extension.Should().Be(".mp4");
        node.SizeBytes.Should().BeNull();
        node.Children.Should().BeNull();
        node.ParsedTitle.Should().Be("Test Movie");
        node.Year.Should().Be(2023);
    }
}
