using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteProgressStoreTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteProgressStore _store;

    public SqliteProgressStoreTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-progressstore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080
        };

        _db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _store = new SqliteProgressStore(_db);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
    }

    [Fact]
    public async Task UpdateAndGet_RoundTrips()
    {
        await _store.UpdateAsync(1, "Films/test.mp4", 120.5, 7200.0, CancellationToken.None);

        var progress = await _store.GetAsync(1, "Films/test.mp4", CancellationToken.None);

        progress.Should().NotBeNull();
        progress!.RelativePath.Should().Be("Films/test.mp4");
        progress.PositionSeconds.Should().Be(120.5);
        progress.DurationSeconds.Should().Be(7200.0);
        progress.LastWatched.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForNonexistent()
    {
        var progress = await _store.GetAsync(1, "nonexistent.mp4", CancellationToken.None);

        progress.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpsertsPosition()
    {
        await _store.UpdateAsync(1, "Films/upsert.mp4", 60.0, 3600.0, CancellationToken.None);
        await _store.UpdateAsync(1, "Films/upsert.mp4", 300.0, 3600.0, CancellationToken.None);

        var progress = await _store.GetAsync(1, "Films/upsert.mp4", CancellationToken.None);

        progress!.PositionSeconds.Should().Be(300.0);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyRequestingUsersData()
    {
        // Create a second user
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (name) VALUES ('User2');";
        cmd.ExecuteNonQuery();
        var user2Id = 2;
        conn.Dispose();

        await _store.UpdateAsync(1, "Films/user1.mp4", 10.0, 100.0, CancellationToken.None);
        await _store.UpdateAsync(user2Id, "Films/user2.mp4", 20.0, 200.0, CancellationToken.None);

        var user1Progress = await _store.GetAllAsync(1, CancellationToken.None);
        var user2Progress = await _store.GetAllAsync(user2Id, CancellationToken.None);

        user1Progress.Should().ContainSingle(p => p.RelativePath == "Films/user1.mp4");
        user1Progress.Should().NotContain(p => p.RelativePath == "Films/user2.mp4");

        user2Progress.Should().ContainSingle(p => p.RelativePath == "Films/user2.mp4");
        user2Progress.Should().NotContain(p => p.RelativePath == "Films/user1.mp4");
    }

    [Fact]
    public async Task UpdateAsync_AutoCreatesMediaRow()
    {
        await _store.UpdateAsync(1, "Films/auto-media.mp4", 50.0, 500.0, CancellationToken.None);

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE relative_path = 'Films/auto-media.mp4';";
        Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(1);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSortedByLastWatchedDescending()
    {
        await _store.UpdateAsync(1, "Films/older.mp4", 10.0, 100.0, CancellationToken.None);
        await Task.Delay(50);
        await _store.UpdateAsync(1, "Films/newer.mp4", 20.0, 200.0, CancellationToken.None);

        var all = await _store.GetAllAsync(1, CancellationToken.None);

        all.Should().HaveCountGreaterThanOrEqualTo(2);
        var olderIdx = all.ToList().FindIndex(p => p.RelativePath == "Films/older.mp4");
        var newerIdx = all.ToList().FindIndex(p => p.RelativePath == "Films/newer.mp4");
        newerIdx.Should().BeLessThan(olderIdx, "newer should appear first");
    }

    [Fact]
    public async Task PerUserIsolation_SamePathDifferentUsers()
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (name) VALUES ('User2');";
        cmd.ExecuteNonQuery();
        var user2Id = 2;
        conn.Dispose();

        await _store.UpdateAsync(1, "Films/shared.mp4", 100.0, 1000.0, CancellationToken.None);
        await _store.UpdateAsync(user2Id, "Films/shared.mp4", 500.0, 1000.0, CancellationToken.None);

        var user1 = await _store.GetAsync(1, "Films/shared.mp4", CancellationToken.None);
        var user2 = await _store.GetAsync(user2Id, "Films/shared.mp4", CancellationToken.None);

        user1!.PositionSeconds.Should().Be(100.0);
        user2!.PositionSeconds.Should().Be(500.0);
    }
}
