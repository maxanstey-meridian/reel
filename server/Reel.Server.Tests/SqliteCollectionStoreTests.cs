using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteCollectionStoreTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteCollectionStore _store;

    public SqliteCollectionStoreTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-collstore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080,
        };

        _db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _store = new SqliteCollectionStore(_db);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
    }

    [Fact]
    public async Task CreateAndGetAll_RoundTrips()
    {
        var created = await _store.CreateAsync(1, "My Playlist", CancellationToken.None);

        created.Id.Should().BeGreaterThan(0);
        created.Name.Should().Be("My Playlist");
        created.ItemCount.Should().Be(0);

        var all = await _store.GetAllAsync(1, CancellationToken.None);
        all.Should().ContainSingle(c => c.Name == "My Playlist");
    }

    [Fact]
    public async Task GetWithItems_ReturnsNullWhenNotFound()
    {
        var result = await _store.GetWithItemsAsync(999, 1, CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetWithItems_ReturnsCollectionAndItems()
    {
        var created = await _store.CreateAsync(1, "Watch Later", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/a.mp4", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/b.mp4", CancellationToken.None);

        var result = await _store.GetWithItemsAsync(created.Id, 1, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Watch Later");
        result.Items.Should().HaveCount(2);
        result.Items[0].RelativePath.Should().Be("Films/a.mp4");
        result.Items[1].RelativePath.Should().Be("Films/b.mp4");
    }

    [Fact]
    public async Task RenameAsync_UpdatesName()
    {
        var created = await _store.CreateAsync(1, "Old Name", CancellationToken.None);
        await _store.RenameAsync(created.Id, 1, "New Name", CancellationToken.None);

        var all = await _store.GetAllAsync(1, CancellationToken.None);
        all.Should().ContainSingle(c => c.Name == "New Name");
    }

    [Fact]
    public async Task DeleteAsync_RemovesCollection()
    {
        var created = await _store.CreateAsync(1, "To Delete", CancellationToken.None);
        await _store.DeleteAsync(created.Id, 1, CancellationToken.None);

        var all = await _store.GetAllAsync(1, CancellationToken.None);
        all.Should().NotContain(c => c.Name == "To Delete");
    }

    [Fact]
    public async Task DeleteAsync_CascadeDeletesItems()
    {
        var created = await _store.CreateAsync(1, "Cascade Test", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/cascade.mp4", CancellationToken.None);
        await _store.DeleteAsync(created.Id, 1, CancellationToken.None);

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM collection_items WHERE collection_id = @id;";
        cmd.Parameters.AddWithValue("@id", created.Id);
        Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(0);
    }

    [Fact]
    public async Task AddItemAsync_SetsPositionIncrementally()
    {
        var created = await _store.CreateAsync(1, "Ordered", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/first.mp4", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/second.mp4", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/third.mp4", CancellationToken.None);

        var result = await _store.GetWithItemsAsync(created.Id, 1, CancellationToken.None);

        result!.Items[0].Position.Should().Be(1);
        result.Items[1].Position.Should().Be(2);
        result.Items[2].Position.Should().Be(3);
    }

    [Fact]
    public async Task AddItemAsync_IsIdempotent()
    {
        var created = await _store.CreateAsync(1, "Idempotent", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/dup.mp4", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/dup.mp4", CancellationToken.None);

        var result = await _store.GetWithItemsAsync(created.Id, 1, CancellationToken.None);
        result!.Items.Should().ContainSingle(i => i.RelativePath == "Films/dup.mp4");
    }

    [Fact]
    public async Task RemoveItemAsync_DeletesItem()
    {
        var created = await _store.CreateAsync(1, "Remove Test", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/remove.mp4", CancellationToken.None);
        await _store.RemoveItemAsync(created.Id, 1, "Films/remove.mp4", CancellationToken.None);

        var result = await _store.GetWithItemsAsync(created.Id, 1, CancellationToken.None);
        result!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ReorderAsync_UpdatesPositions()
    {
        var created = await _store.CreateAsync(1, "Reorder", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/a.mp4", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/b.mp4", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/c.mp4", CancellationToken.None);

        await _store.ReorderAsync(created.Id, 1, ["Films/c.mp4", "Films/a.mp4", "Films/b.mp4"], CancellationToken.None);

        var result = await _store.GetWithItemsAsync(created.Id, 1, CancellationToken.None);
        result!.Items[0].RelativePath.Should().Be("Films/c.mp4");
        result.Items[1].RelativePath.Should().Be("Films/a.mp4");
        result.Items[2].RelativePath.Should().Be("Films/b.mp4");
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

        await _store.CreateAsync(1, "User1 Collection", CancellationToken.None);
        await _store.CreateAsync(user2Id, "User2 Collection", CancellationToken.None);

        var user1Collections = await _store.GetAllAsync(1, CancellationToken.None);
        var user2Collections = await _store.GetAllAsync(user2Id, CancellationToken.None);

        user1Collections.Should().ContainSingle(c => c.Name == "User1 Collection");
        user1Collections.Should().NotContain(c => c.Name == "User2 Collection");

        user2Collections.Should().ContainSingle(c => c.Name == "User2 Collection");
        user2Collections.Should().NotContain(c => c.Name == "User1 Collection");
    }

    [Fact]
    public async Task GetWithItems_RespectsUserScoping()
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (name) VALUES ('User2');";
        cmd.ExecuteNonQuery();
        conn.Dispose();

        var created = await _store.CreateAsync(1, "User1 Only", CancellationToken.None);

        var result = await _store.GetWithItemsAsync(created.Id, 2, CancellationToken.None);
        result.Should().BeNull("user 2 should not see user 1's collection");
    }

    [Fact]
    public async Task GetAllAsync_IncludesItemCount()
    {
        var created = await _store.CreateAsync(1, "Counted", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/one.mp4", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/two.mp4", CancellationToken.None);

        var all = await _store.GetAllAsync(1, CancellationToken.None);
        var collection = all.Single(c => c.Name == "Counted");
        collection.ItemCount.Should().Be(2);
    }

    [Fact]
    public async Task AddItemAsync_AutoCreatesMediaRow()
    {
        var created = await _store.CreateAsync(1, "Media Auto", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/auto-media.mp4", CancellationToken.None);

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE relative_path = 'Films/auto-media.mp4';";
        Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(1);
    }

    [Fact]
    public async Task AddItemAsync_WrongUser_DoesNotInsert()
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (name) VALUES ('User2');";
        cmd.ExecuteNonQuery();
        conn.Dispose();

        var created = await _store.CreateAsync(1, "User1 Collection", CancellationToken.None);

        await _store.AddItemAsync(created.Id, 2, "Films/sneaky.mp4", CancellationToken.None);

        var result = await _store.GetWithItemsAsync(created.Id, 1, CancellationToken.None);
        result!.Items.Should().BeEmpty("user 2 should not be able to add items to user 1's collection");
    }

    [Fact]
    public async Task RemoveItemAsync_WrongUser_DoesNotDelete()
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (name) VALUES ('User2');";
        cmd.ExecuteNonQuery();
        conn.Dispose();

        var created = await _store.CreateAsync(1, "User1 Collection", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/keep.mp4", CancellationToken.None);

        await _store.RemoveItemAsync(created.Id, 2, "Films/keep.mp4", CancellationToken.None);

        var result = await _store.GetWithItemsAsync(created.Id, 1, CancellationToken.None);
        result!.Items.Should().ContainSingle("user 2 should not be able to remove items from user 1's collection");
    }

    [Fact]
    public async Task ReorderAsync_WrongUser_DoesNotUpdate()
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (name) VALUES ('User2');";
        cmd.ExecuteNonQuery();
        conn.Dispose();

        var created = await _store.CreateAsync(1, "User1 Collection", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/a.mp4", CancellationToken.None);
        await _store.AddItemAsync(created.Id, 1, "Films/b.mp4", CancellationToken.None);

        await _store.ReorderAsync(created.Id, 2, ["Films/b.mp4", "Films/a.mp4"], CancellationToken.None);

        var result = await _store.GetWithItemsAsync(created.Id, 1, CancellationToken.None);
        result!.Items[0].RelativePath.Should().Be("Films/a.mp4", "original order should be preserved");
        result.Items[1].RelativePath.Should().Be("Films/b.mp4", "original order should be preserved");
    }
}
