using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteUserStoreTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteUserStore _store;

    public SqliteUserStoreTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-userstore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080,
        };

        _db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _store = new SqliteUserStore(_db);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
    }

    [Fact]
    public async Task CreateAsync_ReturnsUserWithAutoIncrementedId()
    {
        var user = await _store.CreateAsync("Alice", "avatar.png", CancellationToken.None);

        user.Id.Should().BeGreaterThan(1, "id 1 is the default user");
        user.Name.Should().Be("Alice");
        user.Icon.Should().Be("avatar.png");
    }

    [Fact]
    public async Task CreateAsync_WithNullIcon_ReturnsUserWithNullIcon()
    {
        var user = await _store.CreateAsync("Bob", null, CancellationToken.None);

        user.Icon.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsDefaultUser()
    {
        var user = await _store.GetAsync(1, CancellationToken.None);

        user.Should().NotBeNull();
        user!.Id.Should().Be(1);
        user.Name.Should().Be("Default");
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForNonexistent()
    {
        var user = await _store.GetAsync(999, CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsCreatedUser()
    {
        var created = await _store.CreateAsync("Charlie", "icon.png", CancellationToken.None);
        var fetched = await _store.GetAsync(created.Id, CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Charlie");
        fetched.Icon.Should().Be("icon.png");
    }

    [Fact]
    public async Task GetAllAsync_IncludesDefaultUser()
    {
        var users = await _store.GetAllAsync(CancellationToken.None);

        users.Should().ContainSingle(u => u.Id == 1 && u.Name == "Default");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllCreatedUsers()
    {
        await _store.CreateAsync("User A", null, CancellationToken.None);
        await _store.CreateAsync("User B", null, CancellationToken.None);

        var users = await _store.GetAllAsync(CancellationToken.None);

        users.Should().HaveCount(3); // Default + 2 created
    }

    [Fact]
    public async Task UpdateAsync_UpdatesName()
    {
        var created = await _store.CreateAsync("Original", null, CancellationToken.None);
        var updated = await _store.UpdateAsync(created.Id, "Renamed", null, CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesIcon()
    {
        var created = await _store.CreateAsync("WithIcon", null, CancellationToken.None);
        var updated = await _store.UpdateAsync(created.Id, null, "new-icon.png", CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("WithIcon", "name should be unchanged");
        updated.Icon.Should().Be("new-icon.png");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesBothFields()
    {
        var created = await _store.CreateAsync("Both", "old.png", CancellationToken.None);
        var updated = await _store.UpdateAsync(created.Id, "NewName", "new.png", CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("NewName");
        updated.Icon.Should().Be("new.png");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullForNonexistent()
    {
        var result = await _store.UpdateAsync(999, "Name", null, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithNoFieldsProvided_ReturnsUnchangedUser()
    {
        var created = await _store.CreateAsync("NoChange", "icon.png", CancellationToken.None);
        var updated = await _store.UpdateAsync(created.Id, null, null, CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("NoChange");
        updated.Icon.Should().Be("icon.png");
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrueAndRemovesUser()
    {
        var created = await _store.CreateAsync("ToDelete", null, CancellationToken.None);
        var deleted = await _store.DeleteAsync(created.Id, CancellationToken.None);

        deleted.Should().BeTrue();

        var fetched = await _store.GetAsync(created.Id, CancellationToken.None);
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForNonexistent()
    {
        var result = await _store.DeleteAsync(999, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueForDefaultUser()
    {
        var exists = await _store.ExistsAsync(1, CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseForNonexistent()
    {
        var exists = await _store.ExistsAsync(999, CancellationToken.None);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueForCreatedUser()
    {
        var created = await _store.CreateAsync("Exists", null, CancellationToken.None);
        var exists = await _store.ExistsAsync(created.Id, CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_CascadesPerUserData()
    {
        var user = await _store.CreateAsync("CascadeUser", null, CancellationToken.None);

        // Insert per-user data directly via SQL
        using var conn = _db.GetConnection();

        using var mediaCmd = conn.CreateCommand();
        mediaCmd.CommandText = "INSERT OR IGNORE INTO media (relative_path) VALUES ('test.mp4');";
        mediaCmd.ExecuteNonQuery();

        using var progressCmd = conn.CreateCommand();
        progressCmd.CommandText = """
            INSERT INTO progress (user_id, relative_path, position_seconds, duration_seconds, last_watched)
            VALUES (@userId, 'test.mp4', 100, 200, '2025-01-01');
            """;
        progressCmd.Parameters.AddWithValue("@userId", user.Id);
        progressCmd.ExecuteNonQuery();

        using var favCmd = conn.CreateCommand();
        favCmd.CommandText = """
            INSERT INTO favourites (user_id, relative_path, added_at)
            VALUES (@userId, 'test.mp4', '2025-01-01');
            """;
        favCmd.Parameters.AddWithValue("@userId", user.Id);
        favCmd.ExecuteNonQuery();

        using var tagCmd = conn.CreateCommand();
        tagCmd.CommandText = """
            INSERT INTO tags (user_id, relative_path, tag)
            VALUES (@userId, 'test.mp4', 'action');
            """;
        tagCmd.Parameters.AddWithValue("@userId", user.Id);
        tagCmd.ExecuteNonQuery();

        using var watchCmd = conn.CreateCommand();
        watchCmd.CommandText = """
            INSERT INTO watch_state (user_id, relative_path, state)
            VALUES (@userId, 'test.mp4', 'watched');
            """;
        watchCmd.Parameters.AddWithValue("@userId", user.Id);
        watchCmd.ExecuteNonQuery();

        conn.Dispose();

        // Delete user
        await _store.DeleteAsync(user.Id, CancellationToken.None);

        // Verify cascade
        using var verifyConn = _db.GetConnection();

        using var checkProgress = verifyConn.CreateCommand();
        checkProgress.CommandText = "SELECT COUNT(*) FROM progress WHERE user_id = @userId;";
        checkProgress.Parameters.AddWithValue("@userId", user.Id);
        Convert.ToInt32(checkProgress.ExecuteScalar()).Should().Be(0, "progress should be cascade deleted");

        using var checkFav = verifyConn.CreateCommand();
        checkFav.CommandText = "SELECT COUNT(*) FROM favourites WHERE user_id = @userId;";
        checkFav.Parameters.AddWithValue("@userId", user.Id);
        Convert.ToInt32(checkFav.ExecuteScalar()).Should().Be(0, "favourites should be cascade deleted");

        using var checkTags = verifyConn.CreateCommand();
        checkTags.CommandText = "SELECT COUNT(*) FROM tags WHERE user_id = @userId;";
        checkTags.Parameters.AddWithValue("@userId", user.Id);
        Convert.ToInt32(checkTags.ExecuteScalar()).Should().Be(0, "tags should be cascade deleted");

        using var checkWatch = verifyConn.CreateCommand();
        checkWatch.CommandText = "SELECT COUNT(*) FROM watch_state WHERE user_id = @userId;";
        checkWatch.Parameters.AddWithValue("@userId", user.Id);
        Convert.ToInt32(checkWatch.ExecuteScalar()).Should().Be(0, "watch_state should be cascade deleted");
    }
}
