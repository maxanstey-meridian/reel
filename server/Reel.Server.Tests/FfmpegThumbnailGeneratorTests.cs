namespace Reel.Server.Tests;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Ffmpeg;

public sealed class FfmpegThumbnailGeneratorTests : IDisposable
{
    private readonly string _mediaRoot;
    private readonly FfmpegThumbnailGenerator _generator;

    public FfmpegThumbnailGeneratorTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-thumb-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_mediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _mediaRoot,
            SecretKey = "TEST",
            Port = 8080,
        };

        _generator = new FfmpegThumbnailGenerator(config, NullLogger<FfmpegThumbnailGenerator>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    [Fact]
    public async Task GetOrGenerateAsync_PathTraversal_ReturnsNull()
    {
        var result = await _generator.GetOrGenerateAsync("../../etc/passwd", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrGenerateAsync_NestedPathTraversal_ReturnsNull()
    {
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "Films"));

        var result = await _generator.GetOrGenerateAsync("Films/../../etc/passwd", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrGenerateAsync_TraversalToExistingFile_ReturnsNull()
    {
        // Create a file outside the media root that actually exists
        var outsideFile = Path.Combine(Path.GetDirectoryName(_mediaRoot)!, "outside-reel-test.mp4");
        File.WriteAllBytes(outsideFile, new byte[10]);

        try
        {
            var result = await _generator.GetOrGenerateAsync("../outside-reel-test.mp4", CancellationToken.None);

            result.Should().BeNull();
        }
        finally
        {
            File.Delete(outsideFile);
        }
    }
}
