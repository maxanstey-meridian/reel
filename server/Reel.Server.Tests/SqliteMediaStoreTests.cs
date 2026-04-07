using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteMediaStore _store;

    public SqliteMediaStoreTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-mediastore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080
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
    public async Task UpsertFromScanAsync_CreatesMediaRow()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);

        var record = await _store.GetAsync("Films/test.mp4", CancellationToken.None);

        record.Should().NotBeNull();
        record!.RelativePath.Should().Be("Films/test.mp4");
        record.ParsedTitle.Should().Be("Test");
        record.Year.Should().Be(2024);
    }

    [Fact]
    public async Task UpsertFromScanAsync_WithNullTitleAndYear()
    {
        await _store.UpsertFromScanAsync("Films/unknown.mp4", null, null, CancellationToken.None);

        var record = await _store.GetAsync("Films/unknown.mp4", CancellationToken.None);

        record.Should().NotBeNull();
        record!.ParsedTitle.Should().BeNull();
        record.Year.Should().BeNull();
    }

    [Fact]
    public async Task UpsertFromScanAsync_DoesNotOverwriteExistingRow()
    {
        // Pre-populate a row with extra meta via raw SQL
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO media (relative_path, parsed_title, year, genre)
            VALUES ('Films/existing.mp4', 'Original Title', 2020, 'Action');
            """;
        cmd.ExecuteNonQuery();
        conn.Dispose();

        // Upsert with different title/year — should be ignored
        await _store.UpsertFromScanAsync("Films/existing.mp4", "New Title", 2025, CancellationToken.None);

        var record = await _store.GetAsync("Films/existing.mp4", CancellationToken.None);

        record.Should().NotBeNull();
        record!.ParsedTitle.Should().Be("Original Title");
        record.Year.Should().Be(2020);
        record.Genre.Should().Be("Action");
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForNonexistentPath()
    {
        var record = await _store.GetAsync("nonexistent.mp4", CancellationToken.None);

        record.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsAllFields()
    {
        await _store.UpsertFromScanAsync("Films/full.mp4", "Full Movie", 2023, CancellationToken.None);

        var record = await _store.GetAsync("Films/full.mp4", CancellationToken.None);

        record.Should().NotBeNull();
        record!.RelativePath.Should().Be("Films/full.mp4");
        record.ParsedTitle.Should().Be("Full Movie");
        record.Year.Should().Be(2023);
        record.Genre.Should().BeNull();
        record.Rating.Should().BeNull();
        record.Language.Should().BeNull();
        record.Notes.Should().BeNull();
        record.DisplayTitle.Should().BeNull();
        record.TmdbId.Should().BeNull();
        record.PosterUrl.Should().BeNull();
        record.BackdropUrl.Should().BeNull();
        record.Overview.Should().BeNull();
        record.ReleaseDate.Should().BeNull();
        record.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        record.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpsertFromScanAsync_MultipleDistinctPaths()
    {
        await _store.UpsertFromScanAsync("Films/a.mp4", "Film A", 2020, CancellationToken.None);
        await _store.UpsertFromScanAsync("Films/b.mp4", "Film B", 2021, CancellationToken.None);

        var a = await _store.GetAsync("Films/a.mp4", CancellationToken.None);
        var b = await _store.GetAsync("Films/b.mp4", CancellationToken.None);

        a.Should().NotBeNull();
        b.Should().NotBeNull();
        a!.ParsedTitle.Should().Be("Film A");
        b!.ParsedTitle.Should().Be("Film B");
    }
}
