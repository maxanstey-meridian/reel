using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Reel.Server.Common;
using Reel.Server.Media;

namespace Reel.Server.Tests;

public sealed class TrickplayEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TrickplayEndpointTests()
    {
        _media.Write("Films/movie.mp4", new byte[100]);
        _media.Write("Music/song.mp3", new byte[100]);
        _factory = _media.CreateFactory();
        _client = _factory.CreateAuthenticatedClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _media.Dispose();
    }

    [Theory]
    [InlineData("/api/files/trickplay?path=../../etc/passwd")]
    [InlineData("/api/files/trickplay?path=Films/nonexistent.mp4")]
    [InlineData("/api/files/trickplay/sprite?path=../../etc/passwd")]
    public async Task Unplayable_Returns404(string url)
    {
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // The mp4 is not a real video, so ffmpeg cannot preview it; audio has nothing to preview.
    [Theory]
    [InlineData("/api/files/trickplay?path=Films/movie.mp4")]
    [InlineData("/api/files/trickplay/sprite?path=Films/movie.mp4&sheet=0")]
    [InlineData("/api/files/trickplay?path=Music/song.mp3")]
    [InlineData("/api/files/trickplay/sprite?path=Music/song.mp3&sheet=0")]
    public async Task Ungeneratable_Returns404Unavailable(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!
            .Code.Should()
            .Be("trickplay_unavailable");
    }

    [Theory]
    [InlineData("/api/files/trickplay")]
    [InlineData("/api/files/trickplay/sprite")]
    public async Task MissingPath_Returns400(string url)
    {
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0, HttpStatusCode.OK)]
    [InlineData(1, HttpStatusCode.NotFound)]
    [InlineData(-1, HttpStatusCode.NotFound)]
    public async Task Sprite_WithCachedMetadata_ServesOnlySheetsInRange(
        int sheet,
        HttpStatusCode expected
    )
    {
        // 6 thumbs on a 10x10 grid make exactly one sheet. The key matches the file, so this is a cache hit.
        var generator = _factory.Services.GetRequiredService<FfmpegTrickplayGenerator>();
        var metadata = new TrickplaySheets(
            10000,
            10,
            10,
            320,
            180,
            6,
            SourceFingerprint.Key(_media.Paths.ResolvePlayableFile("Films/movie.mp4")!)
        );
        _media.Write(
            generator.MetadataPath(metadata.SpritePath),
            JsonSerializer.SerializeToUtf8Bytes(metadata)
        );
        _media.Write(generator.SpritePath(metadata, 0), [0xFF, 0xD8, 0xFF, 0xD9]);

        var response = await _client.GetAsync(
            $"/api/files/trickplay/sprite?path=Films/movie.mp4&sheet={sheet}"
        );

        response.StatusCode.Should().Be(expected);
        if (expected == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        }
    }
}
