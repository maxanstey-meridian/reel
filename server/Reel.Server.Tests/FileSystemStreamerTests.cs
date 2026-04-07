namespace Reel.Server.Tests;

using FluentAssertions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.FileSystem;

public sealed class FileSystemStreamerTests : IDisposable
{
    private readonly string _mediaRoot;
    private readonly FileSystemStreamer _streamer;

    public FileSystemStreamerTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-streamer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_mediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _mediaRoot,
            SecretKey = "TEST",
            Port = 8080,
        };

        _streamer = new FileSystemStreamer(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    [Fact]
    public void Resolve_ValidFile_ReturnsTuple()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "video.mp4"), new byte[10]);

        var result = _streamer.Resolve("video.mp4");

        result.Should().NotBeNull();
        result!.Value.fullPath.Should().EndWith("video.mp4");
        result.Value.contentType.Should().Be("video/mp4");
    }

    [Fact]
    public void Resolve_NonexistentFile_ReturnsNull()
    {
        var result = _streamer.Resolve("missing.mp4");

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_PathTraversal_ReturnsNull()
    {
        var result = _streamer.Resolve("../../etc/passwd");

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_NestedPathTraversal_ReturnsNull()
    {
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "Films"));

        var result = _streamer.Resolve("Films/../../etc/passwd");

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_TraversalToExistingFile_ReturnsNull()
    {
        // Create a file outside the media root that actually exists
        var outsideFile = Path.Combine(Path.GetDirectoryName(_mediaRoot)!, "outside-reel-test.mp4");
        File.WriteAllBytes(outsideFile, new byte[10]);

        try
        {
            var result = _streamer.Resolve("../outside-reel-test.mp4");

            result.Should().BeNull();
        }
        finally
        {
            File.Delete(outsideFile);
        }
    }
}
