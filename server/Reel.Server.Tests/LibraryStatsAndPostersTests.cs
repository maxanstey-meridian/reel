namespace Reel.Server.Tests;

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

public sealed class LibraryStatsAndPostersTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public LibraryStatsAndPostersTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-stats-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_mediaRoot);

        File.WriteAllBytes(Path.Combine(_mediaRoot, "movie-a.mp4"), new byte[4096]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "movie-b.mkv"), new byte[2048]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "movie-c.avi"), new byte[1024]);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", _mediaRoot);
            builder.UseSetting("Reel:SecretKey", "REEL-TEST");
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
        client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");
        return client;
    }

    [Fact]
    public async Task Stats_ReturnsCorrectFileCountAndTotalBytes()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/library/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("totalFiles").GetInt32().Should().Be(3);
        root.GetProperty("totalBytes").GetInt64().Should().Be(4096 + 2048 + 1024);
    }

    [Fact]
    public async Task Stats_WithoutKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/library/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Posters_ReturnsRequestedCount()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/library/posters?count=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Posters_ReturnsAllFiles_WhenCountExceedsTotal()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/library/posters?count=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task Posters_ReturnsEmptyArray_WhenCountIsZero()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/library/posters?count=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Posters_WithoutKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/library/posters");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Posters_DefaultCount_Returns20OrAllFiles()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/library/posters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        // Default count is 20, but we only have 3 files
        doc.RootElement.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task Posters_ReturnsValidRelativePaths()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/library/posters?count=3");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var paths = doc.RootElement.EnumerateArray().Select(e => e.GetString()).ToList();

        paths.Should().OnlyContain(p => !string.IsNullOrEmpty(p));
        paths.Should().OnlyContain(p => p!.EndsWith(".mp4") || p.EndsWith(".mkv") || p.EndsWith(".avi"));
    }
}
