using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Common;

namespace Reel.Server.Tests;

public sealed class SecretKeyMiddlewareTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _factory;

    public SecretKeyMiddlewareTests() => _factory = _media.CreateFactory();

    public void Dispose()
    {
        _factory.Dispose();
        _media.Dispose();
    }

    [Fact]
    public async Task ProtectedRoute_WithCorrectKey_PassesThrough()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/library");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedRoute_WithTokenQuery_PassesThrough()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/library?token={ReelTestHost.SecretKey}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedRoute_WithWrongKey_Returns401WithErrorBody()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "wrong-key");

        var response = await client.GetAsync("/api/library");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!
            .Code.Should()
            .Be("unauthorized");
    }

    [Theory]
    [InlineData("/api/library")]
    [InlineData("/api/files/stream")]
    public async Task ProtectedRoute_WithMissingKey_Returns401(string path)
    {
        using var client = _factory.CreateClient();

        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/connect")]
    [InlineData("/API/CONNECT")]
    public async Task Connect_PassesWithoutKey(string path)
    {
        using var client = _factory.CreateClient();

        (await client.PostAsync(path, null)).StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/API/HEALTH")]
    public async Task Health_PassesWithWrongKey(string path)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "wrong-key");

        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
