namespace Reel.Server.Tests;

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

public sealed class MetadataEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string SecretKey = "REEL-TEST";
    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public MetadataEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-metadata-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_mediaRoot);

        var filmsDir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(filmsDir);
        File.WriteAllBytes(Path.Combine(filmsDir, "The.Matrix.1999.1080p.BluRay.x264.mkv"), "not-a-real-video"u8.ToArray());

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
    public async Task MissingPath_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/metadata");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptyPath_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/metadata?path=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PathTraversal_Returns403()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/metadata?path=../../../etc/passwd");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PathTraversalNested_Returns403()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/metadata?path=Films/../../etc/passwd");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonExistentFile_Returns404()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/metadata?path=Films/nonexistent.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ValidPath_NoTmdbKey_Returns404()
    {
        // No TmdbApiKey configured → TmdbService returns null → 404
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            "/api/files/metadata?path=Films/The.Matrix.1999.1080p.BluRay.x264.mkv");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WithoutSecretKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/files/metadata?path=Films/The.Matrix.1999.1080p.BluRay.x264.mkv");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
