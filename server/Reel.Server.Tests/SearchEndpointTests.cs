using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class SearchEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-search-endpoint-{Guid.NewGuid():N}");
    private readonly HttpClient _client;

    public SearchEndpointTests(WebApplicationFactory<Program> factory)
    {
        Directory.CreateDirectory(_mediaRoot);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "action-hero.mp4"), new byte[10]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "comedy-show.mp4"), new byte[10]);

        var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", _mediaRoot);
            builder.UseSetting("Reel:SecretKey", "REEL-TEST");
            builder.UseSetting("Reel:Port", "8080");
        });
        _client = configured.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");
    }

    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, true);
    }

    [Fact]
    public async Task Search_ByQueryReturnsMatchingTitle()
    {
        var response = await _client.GetAsync("/api/search?q=action");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetArrayLength().Should().Be(1);
        json.RootElement[0].GetProperty("relativePath").GetString().Should().Be("action-hero.mp4");
    }

    [Fact]
    public async Task Search_WithoutQueryReturnsAllIndexedMedia()
    {
        var response = await _client.GetAsync("/api/search");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetArrayLength().Should().Be(2);
    }
}
