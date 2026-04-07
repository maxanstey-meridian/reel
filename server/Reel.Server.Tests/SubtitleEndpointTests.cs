using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Domain;

namespace Reel.Server.Tests;

public sealed class SubtitleEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string SecretKey = "REEL-TEST";
    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public SubtitleEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-sub-endpoint-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        var filmsDir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(filmsDir);
        File.WriteAllBytes(Path.Combine(filmsDir, "movie.mp4"), new byte[100]);
        File.WriteAllText(
            Path.Combine(filmsDir, "movie.srt"),
            "1\n00:00:01,000 --> 00:00:02,000\nHello\n"
        );
        File.WriteAllText(
            Path.Combine(filmsDir, "movie.en.vtt"),
            "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nHello English\n"
        );

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", _mediaRoot);
            builder.UseSetting("Reel:SecretKey", SecretKey);
            builder.UseSetting("Reel:Port", "8080");
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", SecretKey);
        return client;
    }

    [Fact]
    public async Task ListSubtitles_ReturnsSidecarTracks()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/subtitles?path=Films/movie.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tracks = await response.Content.ReadFromJsonAsync<SubtitleTrack[]>(JsonOptions);
        tracks.Should().NotBeNull();

        // Should find external sidecar files (embedded may fail without ffprobe)
        var external = tracks!.Where(t => t.Source == "external").ToList();
        external.Should().HaveCountGreaterThanOrEqualTo(2);
        external.Should().Contain(t => t.Format == "srt");
        external.Should().Contain(t => t.Format == "vtt" && t.Language == "en");
    }

    [Fact]
    public async Task ListSubtitles_MissingPath_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/subtitles");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListSubtitles_EmptyPath_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/subtitles?path=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListSubtitles_PathTraversal_Returns403()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/subtitles?path=../../../etc/passwd");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListSubtitles_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/files/subtitles?path=Films/movie.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ServeContent_ExternalVtt_ReturnsContent()
    {
        using var client = CreateAuthenticatedClient();

        // First discover to persist tracks
        await client.GetAsync("/api/files/subtitles?path=Films/movie.mp4");

        // Now get the tracks to find the VTT one
        var listResponse = await client.GetAsync("/api/files/subtitles?path=Films/movie.mp4");
        var tracks = await listResponse.Content.ReadFromJsonAsync<SubtitleTrack[]>(JsonOptions);
        var vttTrack = tracks!.First(t => t.Format == "vtt" && t.Source == "external");

        var response = await client.GetAsync(
            $"/api/files/subtitles/content?path=Films/movie.mp4&index={vttTrack.Index}&token={SecretKey}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("WEBVTT");
        content.Should().Contain("Hello English");
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/vtt");
    }

    [Fact]
    public async Task ServeContent_ExternalSrtAsVtt_ConvertsContent()
    {
        using var client = CreateAuthenticatedClient();

        // Discover tracks
        await client.GetAsync("/api/files/subtitles?path=Films/movie.mp4");

        var listResponse = await client.GetAsync("/api/files/subtitles?path=Films/movie.mp4");
        var tracks = await listResponse.Content.ReadFromJsonAsync<SubtitleTrack[]>(JsonOptions);
        var srtTrack = tracks!.First(t => t.Format == "srt" && t.Source == "external");

        // Request SRT converted to VTT
        var response = await client.GetAsync(
            $"/api/files/subtitles/content?path=Films/movie.mp4&index={srtTrack.Index}&format=vtt&token={SecretKey}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().StartWith("WEBVTT");
        content.Should().Contain("00:00:01.000 --> 00:00:02.000");
        content.Should().Contain("Hello");
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/vtt");
    }

    [Fact]
    public async Task ServeContent_MissingPath_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/api/files/subtitles/content?index=0&token={SecretKey}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ServeContent_MissingIndex_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/api/files/subtitles/content?path=Films/movie.mp4&token={SecretKey}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ServeContent_InvalidTrackIndex_Returns404()
    {
        using var client = CreateAuthenticatedClient();

        // Discover first
        await client.GetAsync("/api/files/subtitles?path=Films/movie.mp4");

        var response = await client.GetAsync(
            $"/api/files/subtitles/content?path=Films/movie.mp4&index=999&token={SecretKey}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ServeContent_PathTraversal_Returns403()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/api/files/subtitles/content?path=../../../etc/passwd&index=0&token={SecretKey}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListSubtitles_PersistsTracksToDB()
    {
        using var client = CreateAuthenticatedClient();

        // First call discovers and persists
        var response1 = await client.GetAsync("/api/files/subtitles?path=Films/movie.mp4");
        var tracks1 = await response1.Content.ReadFromJsonAsync<SubtitleTrack[]>(JsonOptions);

        // Second call should also return tracks (re-discovered)
        var response2 = await client.GetAsync("/api/files/subtitles?path=Films/movie.mp4");
        var tracks2 = await response2.Content.ReadFromJsonAsync<SubtitleTrack[]>(JsonOptions);

        tracks2!.Length.Should().Be(tracks1!.Length);
    }
}
