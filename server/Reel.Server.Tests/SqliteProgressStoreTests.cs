using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteProgressStoreTests : IDisposable
{
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-progress-test-{Guid.NewGuid():N}");
    private readonly SqliteProgressStore _store;
    private readonly SqliteMediaStore _mediaStore;

    public SqliteProgressStoreTests()
    {
        Directory.CreateDirectory(_mediaRoot);
        var database = new DatabaseService(
            new ReelConfig { MediaRoot = _mediaRoot, SecretKey = "test", Port = 8080 },
            NullLogger<DatabaseService>.Instance
        );
        _store = new SqliteProgressStore(database);
        _mediaStore = new SqliteMediaStore(database);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, true);
    }

    [Fact]
    public async Task UpdateAndGet_RoundTripsAndUpsertsByPath()
    {
        await SeedMedia("Films/test.mp4");
        await _store.UpdateAsync("Films/test.mp4", 120.5, 7200, CancellationToken.None);
        await _store.UpdateAsync("Films/test.mp4", 300, 7200, CancellationToken.None);

        var progress = await _store.GetAsync("Films/test.mp4", CancellationToken.None);

        progress.Should().NotBeNull();
        progress!.PositionSeconds.Should().Be(300);
        progress.LastWatched.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsNewestFirst()
    {
        await _mediaStore.ReconcileAsync(
            [
                new Reel.Server.Domain.MediaRecord("Films/older.mp4", null, null),
                new Reel.Server.Domain.MediaRecord("Films/newer.mp4", null, null),
            ],
            CancellationToken.None
        );
        await _store.UpdateAsync("Films/older.mp4", 10, 100, CancellationToken.None);
        await Task.Delay(50);
        await _store.UpdateAsync("Films/newer.mp4", 20, 100, CancellationToken.None);

        var progress = await _store.GetAllAsync(CancellationToken.None);

        progress.Select(x => x.RelativePath).Should().ContainInOrder("Films/newer.mp4", "Films/older.mp4");
    }

    private Task SeedMedia(string path) =>
        _mediaStore.ReconcileAsync([new Reel.Server.Domain.MediaRecord(path, null, null)], CancellationToken.None);
}
