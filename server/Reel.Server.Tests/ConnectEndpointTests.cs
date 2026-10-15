using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;

namespace Reel.Server.Tests;

public sealed class ConnectEndpointTests : IDisposable
{
    private const string TestSecretKey = ReelTestHost.SecretKey;
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ConnectEndpointTests() => _factory = _media.CreateFactory();

    public void Dispose()
    {
        _factory.Dispose();
        _media.Dispose();
    }

    [Fact]
    public async Task CorrectKey_ReturnsOkWithServerName()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/connect", new { key = TestSecretKey });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadFromJsonAsync<ConnectResponse>();
        body!.ServerName.Should().Be(Environment.MachineName);
    }

    [Fact]
    public async Task WrongKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/connect", new { key = "wrong-key" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        body!.Code.Should().Be("unauthorized");
        body.Message.Should().Be("That code doesn't match your TV.");
    }

    [Fact]
    public async Task MissingBody_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/connect",
            new StringContent("", Encoding.UTF8, "application/json")
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NoSecretKeyHeader_StillAccessible()
    {
        var client = _factory.CreateClient();
        // No X-Secret-Key header — middleware should not block /api/connect
        var response = await client.PostAsJsonAsync("/api/connect", new { key = TestSecretKey });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
