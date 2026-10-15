using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class SearchEndpointTests : IAsyncLifetime
{
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SearchEndpointTests()
    {
        _media.Write("action-hero.mp4");
        _media.Write("comedy-show.mp4");
        _factory = _media.CreateFactory();
        _client = _factory.CreateAuthenticatedClient();
    }

    public Task InitializeAsync() => _factory.WaitForStartupIndexAsync();

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        _media.Dispose();
        return Task.CompletedTask;
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
