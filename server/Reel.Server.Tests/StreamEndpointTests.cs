namespace Reel.Server.Tests;

using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

public sealed class StreamEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string SecretKey = "REEL-TEST";
    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public StreamEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-stream-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_mediaRoot);

        // Create test files
        var filmsDir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(filmsDir);
        File.WriteAllBytes(Path.Combine(filmsDir, "test.mp4"), "fake-mp4-content"u8.ToArray());
        File.WriteAllBytes(Path.Combine(_mediaRoot, "song.mp3"), "fake-mp3-content"u8.ToArray());
        File.WriteAllBytes(Path.Combine(filmsDir, "movie.mkv"), "fake-mkv-content"u8.ToArray());

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
    public async Task ValidPath_Returns200WithFileContent()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream?path=Films/test.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsByteArrayAsync();
        content.Should().BeEquivalentTo("fake-mp4-content"u8.ToArray());
    }

    [Fact]
    public async Task ValidPath_ReturnsCorrectMimeType()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream?path=Films/test.mp4");

        response.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
    }

    [Fact]
    public async Task Mp3File_ReturnsAudioMimeType()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream?path=song.mp3");

        response.Content.Headers.ContentType!.MediaType.Should().Be("audio/mpeg");
    }

    [Fact]
    public async Task MkvFile_ReturnsCorrectContentType()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream?path=Films/movie.mkv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("video/x-matroska");
    }

    [Fact]
    public async Task MissingPathParam_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptyPathParam_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream?path=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NonexistentFile_Returns404()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream?path=Films/nope.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PathTraversal_Returns403()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream?path=../../../etc/passwd");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PathTraversalWithEncodedDots_Returns403()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream?path=Films/../../etc/passwd");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WithoutSecretKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/files/stream?path=Films/test.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RangeRequest_Returns206PartialContent()
    {
        using var client = CreateAuthenticatedClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/files/stream?path=Films/test.mp4");
        request.Headers.Range = new RangeHeaderValue(0, 3);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        var content = await response.Content.ReadAsByteArrayAsync();
        content.Should().HaveCount(4); // bytes 0-3 inclusive
    }

    [Fact]
    public async Task RangeRequest_ReturnsContentRangeHeader()
    {
        using var client = CreateAuthenticatedClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/files/stream?path=Films/test.mp4");
        request.Headers.Range = new RangeHeaderValue(0, 3);

        var response = await client.SendAsync(request);

        response.Content.Headers.ContentRange.Should().NotBeNull();
        response.Content.Headers.ContentRange!.From.Should().Be(0);
        response.Content.Headers.ContentRange.To.Should().Be(3);
    }

    [Fact]
    public async Task FullRequest_ReturnsAcceptRangesHeader()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream?path=Films/test.mp4");

        response.Headers.AcceptRanges.Should().Contain("bytes");
    }
}
