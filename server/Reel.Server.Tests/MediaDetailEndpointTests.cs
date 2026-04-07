using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Application.Models;

namespace Reel.Server.Tests;

public sealed class MediaDetailEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MediaDetailEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-detail-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        File.WriteAllBytes(Path.Combine(_mediaRoot, "test.mp4"), new byte[1024]);

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

    private async Task EnsureMediaRowExists()
    {
        // Trigger library scan to populate media table
        await _client.GetAsync("/api/library");
    }

    [Fact]
    public async Task GetMediaDetail_Returns200WithFullRecord()
    {
        await EnsureMediaRowExists();

        var response = await _client.GetAsync("/api/media/detail?path=test.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("relativePath").GetString().Should().Be("test.mp4");
        root.GetProperty("tags").GetArrayLength().Should().Be(0);
        root.TryGetProperty("addedAt", out _).Should().BeTrue();
        root.TryGetProperty("updatedAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetMediaDetail_Returns404ForUnknownPath()
    {
        var response = await _client.GetAsync("/api/media/detail?path=nonexistent.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMediaDetail_Returns400WhenPathMissing()
    {
        var response = await _client.GetAsync("/api/media/detail");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchMedia_Returns204()
    {
        await EnsureMediaRowExists();

        var update = new MediaUpdateRequest(
            Genre: "Action",
            Rating: null,
            Language: null,
            Notes: null,
            DisplayTitle: null
        );

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/media/detail?path=test.mp4")
        {
            Content = JsonContent.Create(update, options: JsonOptions),
        };

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PatchMedia_UpdatesOnlyProvidedFields()
    {
        await EnsureMediaRowExists();

        // Set genre and rating
        var update1 = new MediaUpdateRequest(
            Genre: "Horror",
            Rating: "R",
            Language: null,
            Notes: null,
            DisplayTitle: null
        );
        var req1 = new HttpRequestMessage(HttpMethod.Patch, "/api/media/detail?path=test.mp4")
        {
            Content = JsonContent.Create(update1, options: JsonOptions),
        };
        await _client.SendAsync(req1);

        // Now update only language — genre and rating should be preserved
        var update2 = new MediaUpdateRequest(
            Genre: null,
            Rating: null,
            Language: "en",
            Notes: null,
            DisplayTitle: null
        );
        var req2 = new HttpRequestMessage(HttpMethod.Patch, "/api/media/detail?path=test.mp4")
        {
            Content = JsonContent.Create(update2, options: JsonOptions),
        };
        await _client.SendAsync(req2);

        // Verify via GET
        var detail = await _client.GetAsync("/api/media/detail?path=test.mp4");
        var json = await detail.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("genre").GetString().Should().Be("Horror");
        root.GetProperty("rating").GetString().Should().Be("R");
        root.GetProperty("language").GetString().Should().Be("en");
    }

    [Fact]
    public async Task GetMediaDetail_IncludesWatchedState()
    {
        await EnsureMediaRowExists();

        // Mark as watched
        await _client.PutAsync("/api/media/watched?path=test.mp4", null);

        var response = await _client.GetAsync("/api/media/detail?path=test.mp4");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("watched").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task PatchMedia_PathTraversalBlocked()
    {
        var update = new MediaUpdateRequest(
            Genre: "Evil",
            Rating: null,
            Language: null,
            Notes: null,
            DisplayTitle: null
        );
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/media/detail?path=../../etc/passwd")
        {
            Content = JsonContent.Create(update, options: JsonOptions),
        };

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
