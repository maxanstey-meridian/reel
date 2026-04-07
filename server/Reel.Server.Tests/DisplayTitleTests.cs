using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Application.Models;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class DisplayTitleTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteMediaStore _store;

    public DisplayTitleTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-displaytitle-test-{Guid.NewGuid():N}");
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
    public async Task GetDisplayTitleAsync_ReturnsDisplayTitle_WhenSet()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);
        await _store.UpdateMetaAsync(
            "Films/test.mp4",
            new MediaUpdateRequest(null, null, null, null, "My Custom Title"),
            CancellationToken.None
        );

        var displayTitle = await _store.GetDisplayTitleAsync("Films/test.mp4", CancellationToken.None);

        displayTitle.Should().Be("My Custom Title");
    }

    [Fact]
    public async Task GetDisplayTitleAsync_ReturnsNull_WhenNoDisplayTitle()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Test", 2024, CancellationToken.None);

        var displayTitle = await _store.GetDisplayTitleAsync("Films/test.mp4", CancellationToken.None);

        displayTitle.Should().BeNull();
    }

    [Fact]
    public async Task GetDisplayTitleAsync_ReturnsNull_ForNonexistentPath()
    {
        var displayTitle = await _store.GetDisplayTitleAsync("nonexistent.mp4", CancellationToken.None);

        displayTitle.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_DisplayTitle_UsesManualOverride()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Parsed Title", 2024, CancellationToken.None);
        await _store.UpdateMetaAsync(
            "Films/test.mp4",
            new MediaUpdateRequest(null, null, null, null, "Manual Override"),
            CancellationToken.None
        );

        var results = await _store.SearchAsync(1, new SearchQuery(null, null, null, null), CancellationToken.None);

        results.Should().ContainSingle().Which.DisplayTitle.Should().Be("Manual Override");
    }

    [Fact]
    public async Task SearchAsync_DisplayTitle_FallsToParsedTitle()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Parsed Title", 2024, CancellationToken.None);

        var results = await _store.SearchAsync(1, new SearchQuery(null, null, null, null), CancellationToken.None);

        results.Should().ContainSingle().Which.DisplayTitle.Should().Be("Parsed Title");
    }

    [Fact]
    public async Task SearchAsync_DisplayTitle_FallsToFilename()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", null, null, CancellationToken.None);

        var results = await _store.SearchAsync(1, new SearchQuery(null, null, null, null), CancellationToken.None);

        results.Should().ContainSingle().Which.DisplayTitle.Should().Be("test.mp4");
    }

    [Fact]
    public async Task SearchAsync_DisplayTitle_ClearedOverrideReverts()
    {
        await _store.UpsertFromScanAsync("Films/test.mp4", "Parsed Title", 2024, CancellationToken.None);
        await _store.UpdateMetaAsync(
            "Films/test.mp4",
            new MediaUpdateRequest(null, null, null, null, "Override"),
            CancellationToken.None
        );

        // Clear the override
        await _store.UpdateMetaAsync(
            "Films/test.mp4",
            new MediaUpdateRequest(null, null, null, null, ""),
            CancellationToken.None
        );

        var results = await _store.SearchAsync(1, new SearchQuery(null, null, null, null), CancellationToken.None);

        results.Should().ContainSingle().Which.DisplayTitle.Should().Be("Parsed Title");
    }
}
