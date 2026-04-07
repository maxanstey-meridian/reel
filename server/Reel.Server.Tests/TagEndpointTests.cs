using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Application.Models;

namespace Reel.Server.Tests;

public sealed class TagEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TagEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-tag-ep-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        File.WriteAllBytes(Path.Combine(_mediaRoot, "test.mp4"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "other.mp4"), new byte[1024]);

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
        await _client.GetAsync("/api/library");
    }

    [Fact]
    public async Task PostTag_Returns204()
    {
        await EnsureMediaRowExists();

        var response = await _client.PostAsJsonAsync(
            "/api/media/tags?path=test.mp4",
            new AddTagRequest("action"),
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetTags_ReturnsAddedTags()
    {
        await EnsureMediaRowExists();

        await _client.PostAsJsonAsync("/api/media/tags?path=test.mp4", new AddTagRequest("action"), JsonOptions);
        await _client.PostAsJsonAsync("/api/media/tags?path=test.mp4", new AddTagRequest("comedy"), JsonOptions);

        var response = await _client.GetAsync("/api/media/tags?path=test.mp4");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var tags = doc.RootElement.EnumerateArray().Select(e => e.GetString()).ToArray();

        tags.Should().BeEquivalentTo(["action", "comedy"]);
    }

    [Fact]
    public async Task PutTags_ReplacesAllTags()
    {
        await EnsureMediaRowExists();

        await _client.PostAsJsonAsync("/api/media/tags?path=test.mp4", new AddTagRequest("old-tag"), JsonOptions);

        var response = await _client.PutAsJsonAsync(
            "/api/media/tags?path=test.mp4",
            new SetTagsRequest(["drama", "sci-fi"]),
            JsonOptions
        );
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync("/api/media/tags?path=test.mp4");
        var json = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var tags = doc.RootElement.EnumerateArray().Select(e => e.GetString()).ToArray();

        tags.Should().BeEquivalentTo(["drama", "sci-fi"]);
        tags.Should().NotContain("old-tag");
    }

    [Fact]
    public async Task DeleteTag_RemovesSpecificTag()
    {
        await EnsureMediaRowExists();

        await _client.PostAsJsonAsync("/api/media/tags?path=test.mp4", new AddTagRequest("action"), JsonOptions);
        await _client.PostAsJsonAsync("/api/media/tags?path=test.mp4", new AddTagRequest("comedy"), JsonOptions);

        var response = await _client.DeleteAsync("/api/media/tags?path=test.mp4&tag=action");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync("/api/media/tags?path=test.mp4");
        var json = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var tags = doc.RootElement.EnumerateArray().Select(e => e.GetString()).ToArray();

        tags.Should().ContainSingle().Which.Should().Be("comedy");
    }

    [Fact]
    public async Task GetAllTags_ReturnsTagsWithCounts()
    {
        await EnsureMediaRowExists();

        await _client.PostAsJsonAsync("/api/media/tags?path=test.mp4", new AddTagRequest("action"), JsonOptions);
        await _client.PostAsJsonAsync("/api/media/tags?path=other.mp4", new AddTagRequest("action"), JsonOptions);
        await _client.PostAsJsonAsync("/api/media/tags?path=test.mp4", new AddTagRequest("comedy"), JsonOptions);

        var response = await _client.GetAsync("/api/tags");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetArrayLength().Should().Be(2);

        var actionTag = root.EnumerateArray().First(e => e.GetProperty("tag").GetString() == "action");
        actionTag.GetProperty("count").GetInt32().Should().Be(2);

        var comedyTag = root.EnumerateArray().First(e => e.GetProperty("tag").GetString() == "comedy");
        comedyTag.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task MediaDetail_IncludesTags()
    {
        await EnsureMediaRowExists();

        await _client.PostAsJsonAsync("/api/media/tags?path=test.mp4", new AddTagRequest("action"), JsonOptions);

        var response = await _client.GetAsync("/api/media/detail?path=test.mp4");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var tags = doc.RootElement.GetProperty("tags");
        tags.GetArrayLength().Should().Be(1);
        tags[0].GetString().Should().Be("action");
    }

    [Fact]
    public async Task GetTags_Returns400WhenPathMissing()
    {
        var response = await _client.GetAsync("/api/media/tags");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteTag_Returns400WhenTagMissing()
    {
        var response = await _client.DeleteAsync("/api/media/tags?path=test.mp4");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
