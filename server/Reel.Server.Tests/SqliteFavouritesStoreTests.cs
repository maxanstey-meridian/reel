using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteFavouritesStoreTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteFavouritesStore _store;

    public SqliteFavouritesStoreTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-favstore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080
        };

        _db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _store = new SqliteFavouritesStore(_db);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
    }

    [Fact]
    public async Task AddAndGetAll_RoundTrips()
    {
        await _store.AddAsync(1, "Films/fav.mp4", CancellationToken.None);

        var all = await _store.GetAllAsync(1, CancellationToken.None);

        all.Should().ContainSingle(f => f.RelativePath == "Films/fav.mp4");
        all[0].AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AddAsync_IsIdempotent()
    {
        await _store.AddAsync(1, "Films/dup.mp4", CancellationToken.None);
        await _store.AddAsync(1, "Films/dup.mp4", CancellationToken.None);

        var all = await _store.GetAllAsync(1, CancellationToken.None);
        all.Where(f => f.RelativePath == "Films/dup.mp4").Should().ContainSingle();
    }

    [Fact]
    public async Task RemoveAsync_DeletesFavourite()
    {
        await _store.AddAsync(1, "Films/del.mp4", CancellationToken.None);
        await _store.RemoveAsync(1, "Films/del.mp4", CancellationToken.None);

        var all = await _store.GetAllAsync(1, CancellationToken.None);
        all.Should().NotContain(f => f.RelativePath == "Films/del.mp4");
    }

    [Fact]
    public async Task RemoveAsync_NonExistent_DoesNotThrow()
    {
        var act = async () => await _store.RemoveAsync(1, "Films/nope.mp4", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IsFavouriteAsync_ReturnsTrueWhenFavourited()
    {
        await _store.AddAsync(1, "Films/check.mp4", CancellationToken.None);

        var result = await _store.IsFavouriteAsync(1, "Films/check.mp4", CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsFavouriteAsync_ReturnsFalseWhenNotFavourited()
    {
        var result = await _store.IsFavouriteAsync(1, "Films/nope.mp4", CancellationToken.None);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_AutoCreatesMediaRow()
    {
        await _store.AddAsync(1, "Films/auto-media.mp4", CancellationToken.None);

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE relative_path = 'Films/auto-media.mp4';";
        Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(1);
    }

    [Fact]
    public async Task PerUserIsolation()
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (name) VALUES ('User2');";
        cmd.ExecuteNonQuery();
        var user2Id = 2;
        conn.Dispose();

        await _store.AddAsync(1, "Films/user1.mp4", CancellationToken.None);
        await _store.AddAsync(user2Id, "Films/user2.mp4", CancellationToken.None);

        var user1Favs = await _store.GetAllAsync(1, CancellationToken.None);
        var user2Favs = await _store.GetAllAsync(user2Id, CancellationToken.None);

        user1Favs.Should().ContainSingle(f => f.RelativePath == "Films/user1.mp4");
        user1Favs.Should().NotContain(f => f.RelativePath == "Films/user2.mp4");

        user2Favs.Should().ContainSingle(f => f.RelativePath == "Films/user2.mp4");
        user2Favs.Should().NotContain(f => f.RelativePath == "Films/user1.mp4");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSortedByAddedAtDescending()
    {
        await _store.AddAsync(1, "Films/older.mp4", CancellationToken.None);
        await Task.Delay(50);
        await _store.AddAsync(1, "Films/newer.mp4", CancellationToken.None);

        var all = await _store.GetAllAsync(1, CancellationToken.None);

        all.Should().HaveCountGreaterThanOrEqualTo(2);
        var olderIdx = all.ToList().FindIndex(f => f.RelativePath == "Films/older.mp4");
        var newerIdx = all.ToList().FindIndex(f => f.RelativePath == "Films/newer.mp4");
        newerIdx.Should().BeLessThan(olderIdx, "newer should appear first");
    }

    [Fact]
    public async Task IsFavouriteAsync_PerUserIsolation()
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (name) VALUES ('User2');";
        cmd.ExecuteNonQuery();
        var user2Id = 2;
        conn.Dispose();

        await _store.AddAsync(1, "Films/shared.mp4", CancellationToken.None);

        (await _store.IsFavouriteAsync(1, "Films/shared.mp4", CancellationToken.None)).Should().BeTrue();
        (await _store.IsFavouriteAsync(user2Id, "Films/shared.mp4", CancellationToken.None)).Should().BeFalse();
    }
}
