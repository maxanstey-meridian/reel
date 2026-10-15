using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Contracts.Reel;

namespace Reel.Server.Tests;

public sealed class SubtitleEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SubtitleEndpointTests()
    {
        _media.Write("Films/movie.mp4", new byte[100]);
        _media.Write("Films/movie.srt", "1\n00:00:01,000 --> 00:00:02,000\nHello\n"u8.ToArray());
        _media.Write(
            "Films/movie.en.vtt",
            "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nHello English\n"u8.ToArray()
        );
        _factory = _media.CreateFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _media.Dispose();
    }

    private async Task<SubtitleTrack[]> ListAsync() =>
        (
            await _client.GetFromJsonAsync<SubtitleTrack[]>(
                $"/api/files/subtitles?path=Films/movie.mp4&token={ReelTestHost.SecretKey}",
                _factory.JsonOptions()
            )
        )!;

    private Task<HttpResponseMessage> ContentAsync(string query) =>
        _client.GetAsync($"/api/files/subtitles/content?{query}&token={ReelTestHost.SecretKey}");

    [Fact]
    public async Task ListSubtitles_ReturnsSidecarTracks()
    {
        var tracks = await ListAsync();

        tracks
            .Should()
            .BeEquivalentTo([
                new SubtitleTrack(0, "en", "en", "vtt", SubtitleSource.External, ImageBased: false),
                new SubtitleTrack(
                    1,
                    "movie.srt",
                    "",
                    "srt",
                    SubtitleSource.External,
                    ImageBased: false
                ),
            ]);
    }

    [Theory]
    [InlineData("vtt", 0, "00:01.000 --> 00:02.000", "Hello English")]
    [InlineData("srt", 0, "00:01.000 --> 00:02.000", "Hello")]
    [InlineData("srt", 1.5, "00:00:00.000 --> 00:00:00.500", "Hello")]
    public async Task ServeContent_ConvertsSidecarsToWebVttShiftedByStart(
        string format,
        double start,
        string timing,
        string text
    )
    {
        var track = (await ListAsync()).First(t => t.Format == format);

        var response = await ContentAsync(
            $"path=Films/movie.mp4&index={track.Index}&start={start}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/vtt");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().StartWith("WEBVTT").And.Contain(timing).And.Contain(text);
    }

    [Theory]
    [InlineData("/api/files/subtitles?path=")]
    [InlineData("/api/files/subtitles?path=../../../etc/passwd")]
    [InlineData("/api/files/subtitles/content?path=../../../etc/passwd&index=0")]
    [InlineData("/api/files/subtitles/content?path=Films/movie.mp4&index=999")]
    public async Task UnplayableFileOrUnknownTrack_Returns404(string url)
    {
        (await _client.GetAsync($"{url}&token={ReelTestHost.SecretKey}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/files/subtitles?")]
    [InlineData("/api/files/subtitles/content?index=0")]
    [InlineData("/api/files/subtitles/content?path=Films/movie.mp4")]
    public async Task MissingRequiredParameter_Returns400(string url)
    {
        (await _client.GetAsync($"{url}&token={ReelTestHost.SecretKey}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
    }
}
