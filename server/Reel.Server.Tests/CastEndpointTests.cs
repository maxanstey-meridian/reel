using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class CastEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestSecretKey = "REEL-TEST";

    private readonly WebApplicationFactory<Program> _factory;

    public CastEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", "/tmp/reel-test-media");
            builder.UseSetting("Reel:SecretKey", TestSecretKey);
            builder.UseSetting("Reel:Port", "8080");
        });
    }

    [Fact]
    public async Task PostCast_StoresPendingPlayCommand()
    {
        var client = CreateAuthenticatedClient();

        var castResponse = await client.PostAsJsonAsync("/api/cast", new { relativePath = "Films/test.mp4" });
        castResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var currentResponse = await client.GetAsync("/api/cast/current");
        currentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command.Should().NotBeNull();
        status.Command!.Type.Should().Be("play");
        status.Command.RelativePath.Should().Be("Films/test.mp4");
    }

    [Fact]
    public async Task GetCurrent_NoPending_Returns204()
    {
        var client = CreateAuthenticatedClient();

        // Clear any leftover state from other tests
        await client.PostAsync("/api/cast/ack", null);

        var response = await client.GetAsync("/api/cast/current");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PostAck_ClearsPendingCommand()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/cast", new { relativePath = "Films/test.mp4" });
        await client.PostAsync("/api/cast/ack", null);

        var response = await client.GetAsync("/api/cast/current");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PostStop_StoresPendingStopCommand()
    {
        var client = CreateAuthenticatedClient();

        var stopResponse = await client.PostAsync("/api/cast/stop", null);
        stopResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var currentResponse = await client.GetAsync("/api/cast/current");
        currentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command.Should().NotBeNull();
        status.Command!.Type.Should().Be("stop");
        status.Command.RelativePath.Should().BeNull();
    }

    [Theory]
    [InlineData("POST", "/api/cast")]
    [InlineData("GET", "/api/cast/current")]
    [InlineData("POST", "/api/cast/ack")]
    [InlineData("POST", "/api/cast/stop")]
    public async Task CastEndpoints_RequireAuth(string method, string path)
    {
        var client = _factory.CreateClient();
        // No X-Secret-Key header

        var response = method == "GET"
            ? await client.GetAsync(path)
            : await client.PostAsync(path, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostCast_OverwritesPreviousPending()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/cast", new { relativePath = "Films/first.mp4" });
        await client.PostAsJsonAsync("/api/cast", new { relativePath = "Films/second.mp4" });

        var currentResponse = await client.GetAsync("/api/cast/current");
        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command!.RelativePath.Should().Be("Films/second.mp4");
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", TestSecretKey);
        return client;
    }

    private sealed record CastCommandDto(string Type, string? RelativePath);
    private sealed record CastStatusDto(CastCommandDto? Command);
}
