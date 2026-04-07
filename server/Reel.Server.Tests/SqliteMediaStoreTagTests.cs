using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreTagTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteMediaStore _store;

    public SqliteMediaStoreTagTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-tag-test-{Guid.NewGuid():N}");
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

    private async Task SeedMedia(string path)
    {
        await _store.UpsertFromScanAsync(path, null, null, CancellationToken.None);
    }

    private async Task SeedUser(int id, string name)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO users (id, name) VALUES (@id, @name);";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task AddTagAsync_AddsTagToMedia()
    {
        await SeedMedia("Films/test.mp4");

        await _store.AddTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);

        var tags = await _store.GetTagsAsync(1, "Films/test.mp4", CancellationToken.None);
        tags.Should().ContainSingle().Which.Should().Be("action");
    }

    [Fact]
    public async Task AddTagAsync_DuplicateTagIsNoOp()
    {
        await SeedMedia("Films/test.mp4");

        await _store.AddTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);
        await _store.AddTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);

        var tags = await _store.GetTagsAsync(1, "Films/test.mp4", CancellationToken.None);
        tags.Should().ContainSingle();
    }

    [Fact]
    public async Task AddTagAsync_AutoCreatesMediaRow()
    {
        await _store.AddTagAsync(1, "Films/new.mp4", "comedy", CancellationToken.None);

        var tags = await _store.GetTagsAsync(1, "Films/new.mp4", CancellationToken.None);
        tags.Should().ContainSingle().Which.Should().Be("comedy");
    }

    [Fact]
    public async Task RemoveTagAsync_RemovesExistingTag()
    {
        await SeedMedia("Films/test.mp4");
        await _store.AddTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);
        await _store.AddTagAsync(1, "Films/test.mp4", "comedy", CancellationToken.None);

        await _store.RemoveTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);

        var tags = await _store.GetTagsAsync(1, "Films/test.mp4", CancellationToken.None);
        tags.Should().ContainSingle().Which.Should().Be("comedy");
    }

    [Fact]
    public async Task RemoveTagAsync_NonexistentTagIsNoOp()
    {
        await SeedMedia("Films/test.mp4");

        // Should not throw
        await _store.RemoveTagAsync(1, "Films/test.mp4", "nonexistent", CancellationToken.None);
    }

    [Fact]
    public async Task SetTagsAsync_ReplacesAllTags()
    {
        await SeedMedia("Films/test.mp4");
        await _store.AddTagAsync(1, "Films/test.mp4", "old-tag", CancellationToken.None);

        await _store.SetTagsAsync(1, "Films/test.mp4", ["action", "drama"], CancellationToken.None);

        var tags = await _store.GetTagsAsync(1, "Films/test.mp4", CancellationToken.None);
        tags.Should().BeEquivalentTo(["action", "drama"]);
    }

    [Fact]
    public async Task SetTagsAsync_EmptyListClearsAllTags()
    {
        await SeedMedia("Films/test.mp4");
        await _store.AddTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);

        await _store.SetTagsAsync(1, "Films/test.mp4", [], CancellationToken.None);

        var tags = await _store.GetTagsAsync(1, "Films/test.mp4", CancellationToken.None);
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task SetTagsAsync_AutoCreatesMediaRow()
    {
        await _store.SetTagsAsync(1, "Films/auto.mp4", ["sci-fi"], CancellationToken.None);

        var tags = await _store.GetTagsAsync(1, "Films/auto.mp4", CancellationToken.None);
        tags.Should().ContainSingle().Which.Should().Be("sci-fi");
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsSortedTags()
    {
        await SeedMedia("Films/test.mp4");
        await _store.AddTagAsync(1, "Films/test.mp4", "comedy", CancellationToken.None);
        await _store.AddTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);
        await _store.AddTagAsync(1, "Films/test.mp4", "drama", CancellationToken.None);

        var tags = await _store.GetTagsAsync(1, "Films/test.mp4", CancellationToken.None);
        tags.Should().BeInAscendingOrder();
        tags.Should().BeEquivalentTo(["action", "comedy", "drama"]);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsEmptyForUntaggedMedia()
    {
        await SeedMedia("Films/test.mp4");

        var tags = await _store.GetTagsAsync(1, "Films/test.mp4", CancellationToken.None);
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task PerUserIsolation_User1TagsNotVisibleToUser2()
    {
        await SeedUser(2, "User 2");
        await SeedMedia("Films/test.mp4");

        await _store.AddTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);
        await _store.AddTagAsync(2, "Films/test.mp4", "comedy", CancellationToken.None);

        var user1Tags = await _store.GetTagsAsync(1, "Films/test.mp4", CancellationToken.None);
        var user2Tags = await _store.GetTagsAsync(2, "Films/test.mp4", CancellationToken.None);

        user1Tags.Should().ContainSingle().Which.Should().Be("action");
        user2Tags.Should().ContainSingle().Which.Should().Be("comedy");
    }

    [Fact]
    public async Task GetAllTagsAsync_ReturnsTagsWithCounts()
    {
        await SeedMedia("Films/a.mp4");
        await SeedMedia("Films/b.mp4");
        await SeedMedia("Films/c.mp4");

        await _store.AddTagAsync(1, "Films/a.mp4", "action", CancellationToken.None);
        await _store.AddTagAsync(1, "Films/b.mp4", "action", CancellationToken.None);
        await _store.AddTagAsync(1, "Films/c.mp4", "action", CancellationToken.None);
        await _store.AddTagAsync(1, "Films/a.mp4", "comedy", CancellationToken.None);

        var allTags = await _store.GetAllTagsAsync(1, CancellationToken.None);
        allTags.Should().HaveCount(2);
        allTags.Should().Contain(t => t.Tag == "action" && t.Count == 3);
        allTags.Should().Contain(t => t.Tag == "comedy" && t.Count == 1);
    }

    [Fact]
    public async Task GetAllTagsAsync_IsolatedPerUser()
    {
        await SeedUser(2, "User 2");
        await SeedMedia("Films/test.mp4");

        await _store.AddTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);
        await _store.AddTagAsync(2, "Films/test.mp4", "comedy", CancellationToken.None);

        var user1Tags = await _store.GetAllTagsAsync(1, CancellationToken.None);
        var user2Tags = await _store.GetAllTagsAsync(2, CancellationToken.None);

        user1Tags.Should().ContainSingle().Which.Tag.Should().Be("action");
        user2Tags.Should().ContainSingle().Which.Tag.Should().Be("comedy");
    }

    [Fact]
    public async Task GetAllTagsAsync_TagDisappearsWhenLastUsageRemoved()
    {
        await SeedMedia("Films/test.mp4");
        await _store.AddTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);

        var before = await _store.GetAllTagsAsync(1, CancellationToken.None);
        before.Should().ContainSingle();

        await _store.RemoveTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);

        var after = await _store.GetAllTagsAsync(1, CancellationToken.None);
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task SetTagsAsync_DoesNotAffectOtherUsers()
    {
        await SeedUser(2, "User 2");
        await SeedMedia("Films/test.mp4");

        await _store.AddTagAsync(1, "Films/test.mp4", "action", CancellationToken.None);
        await _store.AddTagAsync(2, "Films/test.mp4", "comedy", CancellationToken.None);

        // Replace user 1's tags — user 2's should be unaffected
        await _store.SetTagsAsync(1, "Films/test.mp4", ["drama"], CancellationToken.None);

        var user1Tags = await _store.GetTagsAsync(1, "Films/test.mp4", CancellationToken.None);
        var user2Tags = await _store.GetTagsAsync(2, "Films/test.mp4", CancellationToken.None);

        user1Tags.Should().ContainSingle().Which.Should().Be("drama");
        user2Tags.Should().ContainSingle().Which.Should().Be("comedy");
    }
}
