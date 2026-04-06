using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class ConnectEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestSecretKey = "REEL-TEST";

    private readonly WebApplicationFactory<Program> _factory;

    public ConnectEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", "/tmp/reel-test-media");
            builder.UseSetting("Reel:SecretKey", TestSecretKey);
            builder.UseSetting("Reel:Port", "8080");
        });
    }

    [Fact]
    public async Task CorrectKey_ReturnsOkWithServerName()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/connect", new { key = TestSecretKey });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadFromJsonAsync<ConnectResponseDto>();
        body!.Status.Should().Be("ok");
        body.ServerName.Should().NotBeNullOrEmpty();
        body.ServerName.Should().Be(Environment.MachineName);
    }

    [Fact]
    public async Task WrongKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/connect", new { key = "wrong-key" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadFromJsonAsync<ConnectResponseDto>();
        body!.Status.Should().Be("unauthorized");
        body.ServerName.Should().BeNull();
    }

    [Fact]
    public async Task MissingBody_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/connect",
            new StringContent("", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NoSecretKeyHeader_StillAccessible()
    {
        var client = _factory.CreateClient();
        // No X-Secret-Key header — middleware should not block /api/connect
        var response = await client.PostAsJsonAsync("/api/connect", new { key = TestSecretKey });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record ConnectResponseDto(
        string Status,
        string? ServerName);
}
