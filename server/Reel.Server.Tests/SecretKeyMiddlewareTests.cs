using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class SecretKeyMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestSecretKey = "REEL-TEST";

    private readonly WebApplicationFactory<Program> _factory;

    public SecretKeyMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", "/tmp/reel-test-media");
            builder.UseSetting("Reel:SecretKey", TestSecretKey);
            builder.UseSetting("Reel:Port", "8080");
        });
    }

    [Fact]
    public async Task ProtectedRoute_WithCorrectKey_PassesThrough()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", TestSecretKey);

        var response = await client.GetAsync("/api/some-protected-route");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedRoute_WithWrongKey_Returns401WithJsonBody()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "wrong-key");

        var response = await client.GetAsync("/api/library");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadFromJsonAsync<StatusResponse>();
        body!.Status.Should().Be("unauthorized");
    }

    [Fact]
    public async Task ProtectedRoute_WithMissingHeader_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/library");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/connect")]
    [InlineData("/api/health")]
    [InlineData("/API/CONNECT")]
    [InlineData("/API/HEALTH")]
    public async Task ExcludedPaths_PassWithoutHeader(string path)
    {
        var client = _factory.CreateClient();

        HttpResponseMessage response;
        if (path.Contains("connect", StringComparison.OrdinalIgnoreCase))
        {
            response = await client.PostAsync(path, null);
        }
        else
        {
            response = await client.GetAsync(path);
        }

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<StatusResponse>();
        body!.Status.Should().Be("ok");
    }

    [Fact]
    public async Task StreamEndpoint_WithNoKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/files/stream");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record StatusResponse(string Status);
}
