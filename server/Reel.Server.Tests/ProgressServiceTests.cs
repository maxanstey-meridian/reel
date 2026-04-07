using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class ProgressServiceTests : IDisposable
{
    private readonly string _tempMediaRoot;

    public ProgressServiceTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-progress-svc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
    }

    [Fact]
    public async Task PersistsToFile()
    {
        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080
        };

        var store1 = new JsonProgressStore(config, NullLogger<JsonProgressStore>.Instance);
        await store1.UpdateAsync("Films/test.mp4", 120.5, 7200.0, CancellationToken.None);

        // Create a new instance pointing to the same file — data should survive
        var store2 = new JsonProgressStore(config, NullLogger<JsonProgressStore>.Instance);
        var recovered = await store2.GetAsync("Films/test.mp4", CancellationToken.None);

        recovered.Should().NotBeNull();
        recovered!.PositionSeconds.Should().Be(120.5);
        recovered.DurationSeconds.Should().Be(7200.0);
    }

    [Fact]
    public async Task Upserts_ExistingEntry()
    {
        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080
        };

        var store = new JsonProgressStore(config, NullLogger<JsonProgressStore>.Instance);
        await store.UpdateAsync("Films/test.mp4", 60.0, 3600.0, CancellationToken.None);
        await store.UpdateAsync("Films/test.mp4", 300.0, 3600.0, CancellationToken.None);

        var all = await store.GetAllAsync(CancellationToken.None);
        all.Should().ContainSingle(e => e.RelativePath == "Films/test.mp4");
        all[0].PositionSeconds.Should().Be(300.0);
    }
}
