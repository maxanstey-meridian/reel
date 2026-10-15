using System.Net;
using FluentAssertions;

namespace Reel.Server.Tests;

public sealed class ThumbnailEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task ShortVideo_FallsBackToTheFirstFrameAndIsCached()
    {
        TestMedia.CreateVideo(_media.Write("Films/short.mp4"), seconds: 2);
        _media.Write("Films/broken.mp4");
        using var factory = _media.CreateFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/thumbnail?path=Films/short.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        (await response.Content.ReadAsByteArrayAsync()).Take(2).Should().Equal(0xFF, 0xD8);
        Directory.EnumerateFiles(_media.Paths.AppDataPath("thumbnails")).Should().ContainSingle();
        (await client.GetAsync("/api/files/thumbnail?path=Films/broken.mp4"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }
}
