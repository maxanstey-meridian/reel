namespace Reel.Server.Tests;

using FluentAssertions;
using Reel.Server.Services;

public sealed class ThumbnailServiceTests
{
    [Fact]
    public void CacheKey_IsDeterministic()
    {
        var key1 = ThumbnailService.ComputeCacheKey("Films/test.mp4");
        var key2 = ThumbnailService.ComputeCacheKey("Films/test.mp4");

        key1.Should().Be(key2);
    }

    [Fact]
    public void CacheKey_DiffersForDifferentPaths()
    {
        var key1 = ThumbnailService.ComputeCacheKey("Films/test.mp4");
        var key2 = ThumbnailService.ComputeCacheKey("Films/other.mp4");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void CacheKey_IsValidHexString()
    {
        var key = ThumbnailService.ComputeCacheKey("Films/test.mp4");

        key.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void CacheKey_IsCaseSensitive()
    {
        var key1 = ThumbnailService.ComputeCacheKey("Films/Test.mp4");
        var key2 = ThumbnailService.ComputeCacheKey("Films/test.mp4");

        key1.Should().NotBe(key2);
    }
}
