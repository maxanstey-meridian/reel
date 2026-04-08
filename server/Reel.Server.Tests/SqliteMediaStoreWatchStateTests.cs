using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreWatchStateTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteMediaStore _store;

    public SqliteMediaStoreWatchStateTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-watchstate-test-{Guid.NewGuid():N}");
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
    public async Task IsWatchedAsync_NoProgressNoOverride_ReturnsNull()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);

        var result = await _store.IsWatchedAsync(1, "Films/test.mp4", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task IsWatchedAsync_ProgressAbove90Percent_ReturnsTrue()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);
        await InsertProgress(1, "Films/test.mp4", 95.0, 100.0);

        var result = await _store.IsWatchedAsync(1, "Films/test.mp4", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWatchedAsync_ProgressExactly90Percent_ReturnsTrue()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);
        await InsertProgress(1, "Films/test.mp4", 90.0, 100.0);

        var result = await _store.IsWatchedAsync(1, "Films/test.mp4", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWatchedAsync_ProgressBelow90Percent_ReturnsFalse()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);
        await InsertProgress(1, "Films/test.mp4", 50.0, 100.0);

        var result = await _store.IsWatchedAsync(1, "Films/test.mp4", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsWatchedAsync_ZeroDuration_ReturnsNull()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);
        await InsertProgress(1, "Films/test.mp4", 0.0, 0.0);

        var result = await _store.IsWatchedAsync(1, "Films/test.mp4", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task IsWatchedAsync_OverrideWatched_ReturnsTrueRegardlessOfProgress()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);
        await InsertProgress(1, "Films/test.mp4", 10.0, 100.0); // only 10%
        await _store.SetWatchStateAsync(1, "Films/test.mp4", WatchState.Watched, CancellationToken.None);

        var result = await _store.IsWatchedAsync(1, "Films/test.mp4", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWatchedAsync_OverrideUnwatched_ReturnsFalseRegardlessOfProgress()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);
        await InsertProgress(1, "Films/test.mp4", 95.0, 100.0); // 95%
        await _store.SetWatchStateAsync(1, "Films/test.mp4", WatchState.Unwatched, CancellationToken.None);

        var result = await _store.IsWatchedAsync(1, "Films/test.mp4", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetWatchStateAsync_ClearOverride_RevertsToProgressDerived()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);
        await InsertProgress(1, "Films/test.mp4", 95.0, 100.0);
        await _store.SetWatchStateAsync(1, "Films/test.mp4", WatchState.Unwatched, CancellationToken.None);

        // Verify override is active
        (await _store.IsWatchedAsync(1, "Films/test.mp4", CancellationToken.None))
            .Should()
            .BeFalse();

        // Clear override
        await _store.SetWatchStateAsync(1, "Films/test.mp4", null, CancellationToken.None);

        // Should revert to progress-derived (95% = watched)
        var result = await _store.IsWatchedAsync(1, "Films/test.mp4", CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetWatchStateAsync_NullState_DeletesOverrideRow()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);
        await _store.SetWatchStateAsync(1, "Films/test.mp4", WatchState.Watched, CancellationToken.None);
        await _store.SetWatchStateAsync(1, "Films/test.mp4", null, CancellationToken.None);

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM watch_state WHERE user_id = 1 AND relative_path = 'Films/test.mp4';";
        Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(0);
    }

    [Fact]
    public async Task SetWatchStateAsync_AutoCreatesMediaRow()
    {
        await _store.SetWatchStateAsync(1, "Films/new.mp4", WatchState.Watched, CancellationToken.None);

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE relative_path = 'Films/new.mp4';";
        Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(1);
    }

    [Fact]
    public async Task IsWatchedAsync_PerUserIsolation()
    {
        // Create user 2
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (name) VALUES ('User2');";
        cmd.ExecuteNonQuery();
        conn.Dispose();

        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);
        await _store.SetWatchStateAsync(1, "Films/test.mp4", WatchState.Watched, CancellationToken.None);

        // User 1 has override, user 2 does not
        (await _store.IsWatchedAsync(1, "Films/test.mp4", CancellationToken.None))
            .Should()
            .BeTrue();
        (await _store.IsWatchedAsync(2, "Films/test.mp4", CancellationToken.None)).Should().BeNull();
    }

    private async Task InsertProgress(int userId, string relativePath, double position, double duration)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO progress (user_id, relative_path, position_seconds, duration_seconds, last_watched)
            VALUES (@userId, @path, @pos, @dur, datetime('now'));
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@path", relativePath);
        cmd.Parameters.AddWithValue("@pos", position);
        cmd.Parameters.AddWithValue("@dur", duration);
        await cmd.ExecuteNonQueryAsync();
    }
}
