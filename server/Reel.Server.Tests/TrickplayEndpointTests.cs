using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Ffmpeg;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class TrickplayEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string SecretKey = "REEL-TEST";
    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public TrickplayEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-trick-endpoint-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        var filmsDir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(filmsDir);
        File.WriteAllBytes(Path.Combine(filmsDir, "movie.mp4"), new byte[100]);

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
    public async Task TrickplayMetadata_MissingPath_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/trickplay");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TrickplayMetadata_PathTraversal_Returns403()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/trickplay?path=../../etc/passwd");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TrickplayMetadata_NonexistentFile_Returns404()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/trickplay?path=Films/nonexistent.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TrickplaySprite_MissingPath_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/trickplay/sprite");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TrickplaySprite_PathTraversal_Returns403()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/trickplay/sprite?path=../../etc/passwd");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TrickplaySprite_NoMetadata_Returns404()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/trickplay/sprite?path=Films/movie.mp4&sheet=0");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TrickplaySprite_WithMetadata_ServesJpeg()
    {
        // Pre-seed metadata and create a fake sprite file
        var config = new ReelConfig { MediaRoot = _mediaRoot, SecretKey = SecretKey, Port = 8080 };
        var db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        var store = new SqliteMediaStore(db);
        await store.ReconcileAsync([new MediaRecord("Films/movie.mp4", null, null)], CancellationToken.None);

        var spriteKey = FfmpegTrickplayGenerator.ComputeCacheKey(
            "Films/movie.mp4",
            Path.Combine(_mediaRoot, "Films", "movie.mp4")
        );
        var metadata = new TrickplayMetadata(10000, 10, 10, 320, 180, 6, spriteKey);
        await store.SaveTrickplayAsync("Films/movie.mp4", metadata, CancellationToken.None);

        // Create the sprite file
        var cacheDir = Path.Combine(_mediaRoot, ".reel", "trickplay");
        Directory.CreateDirectory(cacheDir);
        var spriteFile = Path.Combine(cacheDir, $"{spriteKey}_0.jpg");
        // Write minimal JPEG (SOI + EOI markers)
        await File.WriteAllBytesAsync(spriteFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });

        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/trickplay/sprite?path=Films/movie.mp4&sheet=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task TrickplaySprite_SheetOutOfRange_Returns404()
    {
        var config = new ReelConfig { MediaRoot = _mediaRoot, SecretKey = SecretKey, Port = 8080 };
        var db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        var store = new SqliteMediaStore(db);
        await store.ReconcileAsync([new MediaRecord("Films/movie.mp4", null, null)], CancellationToken.None);

        var metadata = new TrickplayMetadata(10000, 10, 10, 320, 180, 6, "testkey");
        await store.SaveTrickplayAsync("Films/movie.mp4", metadata, CancellationToken.None);

        using var client = CreateAuthenticatedClient();

        // 6 thumbs, 10x10 grid = 1 sheet (index 0). Index 1 is out of range.
        var response = await client.GetAsync("/api/files/trickplay/sprite?path=Films/movie.mp4&sheet=1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TrickplaySprite_NegativeSheet_Returns404()
    {
        var config = new ReelConfig { MediaRoot = _mediaRoot, SecretKey = SecretKey, Port = 8080 };
        var db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        var store = new SqliteMediaStore(db);
        await store.ReconcileAsync([new MediaRecord("Films/movie.mp4", null, null)], CancellationToken.None);

        var metadata = new TrickplayMetadata(10000, 10, 10, 320, 180, 6, "testkey2");
        await store.SaveTrickplayAsync("Films/movie.mp4", metadata, CancellationToken.None);

        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/trickplay/sprite?path=Films/movie.mp4&sheet=-1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
