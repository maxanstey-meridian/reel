using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Application.Models;

namespace Reel.Server.Tests;

public sealed class SearchEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SearchEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-search-ep-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        File.WriteAllBytes(Path.Combine(_mediaRoot, "action-hero.mp4"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "comedy-show.mp4"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "drama-film.mp4"), new byte[1024]);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", _mediaRoot);
            builder.UseSetting("Reel:SecretKey", "REEL-TEST");
            builder.UseSetting("Reel:Port", "8080");
        });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    private async Task EnsureMediaRowsExist()
    {
        await _client.GetAsync("/api/library");
    }

    [Fact]
    public async Task Search_NoFilters_ReturnsAllMedia()
    {
        await EnsureMediaRowsExist();

        var response = await _client.GetAsync("/api/search");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task Search_ByTitle_ReturnsMatchingFiles()
    {
        await EnsureMediaRowsExist();

        var response = await _client.GetAsync("/api/search?q=action");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
        doc.RootElement[0].GetProperty("relativePath").GetString().Should().Be("action-hero.mp4");
    }

    [Fact]
    public async Task Search_ByTag_ReturnsTaggedFiles()
    {
        await EnsureMediaRowsExist();

        await _client.PostAsJsonAsync(
            "/api/media/tags?path=action-hero.mp4",
            new AddTagRequest("favourite"),
            JsonOptions
        );

        var response = await _client.GetAsync("/api/search?tag=favourite");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
        doc.RootElement[0].GetProperty("relativePath").GetString().Should().Be("action-hero.mp4");
    }

    [Fact]
    public async Task Search_ByGenre_ReturnsMatchingGenre()
    {
        await EnsureMediaRowsExist();

        await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Patch, "/api/media/detail?path=action-hero.mp4")
            {
                Content = JsonContent.Create(new { genre = "Action" }, options: JsonOptions),
            }
        );

        var response = await _client.GetAsync("/api/search?genre=Action");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
        doc.RootElement[0].GetProperty("relativePath").GetString().Should().Be("action-hero.mp4");
    }

    [Fact]
    public async Task Search_NoResults_ReturnsEmptyArray()
    {
        await EnsureMediaRowsExist();

        var response = await _client.GetAsync("/api/search?q=nonexistent");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Search_ResultShape_IsFlatFileNodeArray()
    {
        await EnsureMediaRowsExist();

        var response = await _client.GetAsync("/api/search?q=action");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var node = doc.RootElement[0];
        node.GetProperty("name").GetString().Should().Be("action-hero.mp4");
        node.GetProperty("type").GetString().Should().Be("file");
        node.GetProperty("relativePath").GetString().Should().Be("action-hero.mp4");
        node.GetProperty("extension").GetString().Should().Be(".mp4");
        node.TryGetProperty("children", out var children).Should().BeTrue();
        children.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Search_WatchedFilter_ReturnsWatchedOnly()
    {
        await EnsureMediaRowsExist();

        // Mark action-hero as watched via progress
        await _client.PostAsJsonAsync(
            "/api/progress",
            new ProgressRequest("action-hero.mp4", 95, 100),
            JsonOptions
        );

        var response = await _client.GetAsync("/api/search?watched=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
        doc.RootElement[0].GetProperty("relativePath").GetString().Should().Be("action-hero.mp4");
    }
}
