using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class MediaEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MediaEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-media-ep-test-{Guid.NewGuid():N}");
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

    [Fact]
    public async Task PutWatched_Returns204()
    {
        var response = await _client.PutAsync("/api/media/watched?path=test.mp4", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PutUnwatched_Returns204()
    {
        var response = await _client.PutAsync("/api/media/unwatched?path=test.mp4", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteWatched_Returns204()
    {
        // Set then clear
        await _client.PutAsync("/api/media/watched?path=test.mp4", null);
        var response = await _client.DeleteAsync("/api/media/watched?path=test.mp4");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task LibraryResponse_IncludesWatchedField()
    {
        var response = await _client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        // Root directory should have null watched
        root.GetProperty("watched").ValueKind.Should().Be(JsonValueKind.Null);

        // File nodes should also have watched (null by default since no progress)
        var children = root.GetProperty("children");
        children.GetArrayLength().Should().BeGreaterThan(0);
        var fileNode = children.EnumerateArray().First();
        fileNode.GetProperty("watched").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task LibraryResponse_ReflectsWatchedOverride()
    {
        // Mark as watched
        await _client.PutAsync("/api/media/watched?path=test.mp4", null);

        var response = await _client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var children = doc.RootElement.GetProperty("children");
        var fileNode = children.EnumerateArray().First();
        fileNode.GetProperty("watched").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task LibraryResponse_ClearedOverrideRevertsToNull()
    {
        // Mark as watched then clear
        await _client.PutAsync("/api/media/watched?path=test.mp4", null);
        await _client.DeleteAsync("/api/media/watched?path=test.mp4");

        var response = await _client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var children = doc.RootElement.GetProperty("children");
        var fileNode = children.EnumerateArray().First();
        fileNode.GetProperty("watched").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
