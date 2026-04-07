using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.FileSystem;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class NextEpisodeEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string SecretKey = "REEL-TEST";
    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public NextEpisodeEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-next-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        var seriesDir = Path.Combine(_mediaRoot, "Series");
        Directory.CreateDirectory(seriesDir);
        File.WriteAllBytes(Path.Combine(seriesDir, "Episode 1.mp4"), new byte[100]);
        File.WriteAllBytes(Path.Combine(seriesDir, "Episode 2.mp4"), new byte[200]);
        File.WriteAllBytes(Path.Combine(seriesDir, "Episode 10.mp4"), new byte[300]);
        // Non-playable files should be skipped
        File.WriteAllBytes(Path.Combine(seriesDir, "Episode 3.srt"), new byte[50]);
        File.WriteAllBytes(Path.Combine(seriesDir, "notes.txt"), new byte[10]);

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
    public async Task NextFile_ReturnsNextEpisodeInNaturalOrder()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/next?path=Series/Episode%201.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var node = await response.Content.ReadFromJsonAsync<FileNode>(JsonOptions);
        node.Should().NotBeNull();
        node!.Name.Should().Be("Episode 2.mp4");
        node.RelativePath.Should().Be("Series/Episode 2.mp4");
        node.Type.Should().Be(FileNodeType.File);
    }

    [Fact]
    public async Task NextFile_SkipsNonPlayableFiles()
    {
        using var client = CreateAuthenticatedClient();

        // Episode 2 is followed by Episode 10 (skipping .srt and .txt)
        var response = await client.GetAsync("/api/files/next?path=Series/Episode%202.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var node = await response.Content.ReadFromJsonAsync<FileNode>(JsonOptions);
        node!.Name.Should().Be("Episode 10.mp4");
    }

    [Fact]
    public async Task LastFile_Returns204()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/next?path=Series/Episode%2010.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SingleFileInDirectory_Returns204()
    {
        var soloDir = Path.Combine(_mediaRoot, "Solo");
        Directory.CreateDirectory(soloDir);
        File.WriteAllBytes(Path.Combine(soloDir, "movie.mp4"), new byte[100]);

        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/next?path=Solo/movie.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MissingPathParam_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/next");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptyPathParam_Returns400()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/next?path=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NonexistentFile_Returns204()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/next?path=Series/nope.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PathTraversal_Returns204()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/next?path=../../../etc/passwd");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task NextFile_IncludesDisplayTitle()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/next?path=Series/Episode%201.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var node = await response.Content.ReadFromJsonAsync<FileNode>(JsonOptions);
        node!.DisplayTitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task NextFile_IncludesThumbnailUrl()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/next?path=Series/Episode%201.mp4");

        var node = await response.Content.ReadFromJsonAsync<FileNode>(JsonOptions);
        node!.ThumbnailUrl.Should().Contain("/api/files/thumbnail");
        node.ThumbnailUrl.Should().Contain("Series");
    }

    [Fact]
    public async Task WithoutSecretKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/files/next?path=Series/Episode%201.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public sealed class NextEpisodeScannerTests : IDisposable
{
    private readonly string _mediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteMediaStore _mediaStore;
    private readonly FileSystemScanner _scanner;

    public NextEpisodeScannerTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-next-scanner-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _mediaRoot,
            SecretKey = "test",
            Port = 8080,
        };

        _db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _mediaStore = new SqliteMediaStore(_db);
        _scanner = new FileSystemScanner(config, _mediaStore, NullLogger<FileSystemScanner>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    [Fact]
    public async Task GetNextFileAsync_ReturnsNextInNaturalOrder()
    {
        var dir = Path.Combine(_mediaRoot, "Show");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "S01E01.mkv"), new byte[100]);
        File.WriteAllBytes(Path.Combine(dir, "S01E02.mkv"), new byte[200]);
        File.WriteAllBytes(Path.Combine(dir, "S01E10.mkv"), new byte[300]);

        var next = await _scanner.GetNextFileAsync("Show/S01E01.mkv", CancellationToken.None);

        next.Should().NotBeNull();
        next!.Name.Should().Be("S01E02.mkv");
        next.RelativePath.Should().Be("Show/S01E02.mkv");
        next.Type.Should().Be(FileNodeType.File);
        next.Extension.Should().Be(".mkv");
        next.SizeBytes.Should().Be(200);
    }

    [Fact]
    public async Task GetNextFileAsync_LastFile_ReturnsNull()
    {
        var dir = Path.Combine(_mediaRoot, "Show2");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "E01.mp4"), new byte[100]);
        File.WriteAllBytes(Path.Combine(dir, "E02.mp4"), new byte[200]);

        var next = await _scanner.GetNextFileAsync("Show2/E02.mp4", CancellationToken.None);

        next.Should().BeNull();
    }

    [Fact]
    public async Task GetNextFileAsync_SingleFile_ReturnsNull()
    {
        var dir = Path.Combine(_mediaRoot, "Movie");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "film.mp4"), new byte[100]);

        var next = await _scanner.GetNextFileAsync("Movie/film.mp4", CancellationToken.None);

        next.Should().BeNull();
    }

    [Fact]
    public async Task GetNextFileAsync_SkipsNonPlayableFiles()
    {
        var dir = Path.Combine(_mediaRoot, "Mixed");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "ep1.mp4"), new byte[100]);
        File.WriteAllBytes(Path.Combine(dir, "ep1.srt"), new byte[50]);
        File.WriteAllBytes(Path.Combine(dir, "ep2.mp4"), new byte[200]);
        File.WriteAllBytes(Path.Combine(dir, "readme.txt"), new byte[10]);

        var next = await _scanner.GetNextFileAsync("Mixed/ep1.mp4", CancellationToken.None);

        next.Should().NotBeNull();
        next!.Name.Should().Be("ep2.mp4");
    }

    [Fact]
    public async Task GetNextFileAsync_PathTraversal_ReturnsNull()
    {
        var next = await _scanner.GetNextFileAsync("../../../etc/passwd", CancellationToken.None);

        next.Should().BeNull();
    }

    [Fact]
    public async Task GetNextFileAsync_NonexistentFile_ReturnsNull()
    {
        var next = await _scanner.GetNextFileAsync("nope/missing.mp4", CancellationToken.None);

        next.Should().BeNull();
    }

    [Fact]
    public async Task GetNextFileAsync_FileInRoot_ReturnsNext()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "a.mp4"), new byte[100]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "b.mp4"), new byte[200]);

        var next = await _scanner.GetNextFileAsync("a.mp4", CancellationToken.None);

        next.Should().NotBeNull();
        next!.Name.Should().Be("b.mp4");
        next.RelativePath.Should().Be("b.mp4");
    }

    [Fact]
    public async Task GetNextFileAsync_UpsertsMediaAndSetsDisplayTitle()
    {
        var dir = Path.Combine(_mediaRoot, "Titles");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "The.Matrix.1999.BluRay.mp4"), new byte[100]);
        File.WriteAllBytes(Path.Combine(dir, "The.Matrix.Reloaded.2003.mp4"), new byte[200]);

        var next = await _scanner.GetNextFileAsync("Titles/The.Matrix.1999.BluRay.mp4", CancellationToken.None);

        next.Should().NotBeNull();
        next!.DisplayTitle.Should().Be("The Matrix Reloaded");

        // Verify it was upserted into the media store
        var record = await _mediaStore.GetAsync("Titles/The.Matrix.Reloaded.2003.mp4", CancellationToken.None);
        record.Should().NotBeNull();
    }

    [Fact]
    public async Task GetNextFileAsync_SkipsDotFiles()
    {
        var dir = Path.Combine(_mediaRoot, "DotTest");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, ".hidden.mp4"), new byte[50]);
        File.WriteAllBytes(Path.Combine(dir, "visible1.mp4"), new byte[100]);
        File.WriteAllBytes(Path.Combine(dir, "visible2.mp4"), new byte[200]);

        var next = await _scanner.GetNextFileAsync("DotTest/visible1.mp4", CancellationToken.None);

        next.Should().NotBeNull();
        next!.Name.Should().Be("visible2.mp4");
    }
}
