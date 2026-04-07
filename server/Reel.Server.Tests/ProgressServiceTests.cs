using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Services;

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
    public void PersistsToFile()
    {
        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080
        };

        var service1 = new ProgressService(config, NullLogger<ProgressService>.Instance);
        service1.UpdateProgress("Films/test.mp4", 120.5, 7200.0);

        // Create a new instance pointing to the same file — data should survive
        var service2 = new ProgressService(config, NullLogger<ProgressService>.Instance);
        var recovered = service2.Get("Films/test.mp4");

        recovered.Should().NotBeNull();
        recovered!.PositionSeconds.Should().Be(120.5);
        recovered.DurationSeconds.Should().Be(7200.0);
    }

    [Fact]
    public void Upserts_ExistingEntry()
    {
        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080
        };

        var service = new ProgressService(config, NullLogger<ProgressService>.Instance);
        service.UpdateProgress("Films/test.mp4", 60.0, 3600.0);
        service.UpdateProgress("Films/test.mp4", 300.0, 3600.0);

        var all = service.GetAll();
        all.Should().ContainSingle(e => e.RelativePath == "Films/test.mp4");
        all[0].PositionSeconds.Should().Be(300.0);
    }
}
