using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Common;

namespace Reel.Server.Tests;

public sealed class HealthEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests() => _factory = _media.CreateFactory();

    public void Dispose()
    {
        _factory.Dispose();
        _media.Dispose();
    }

    [Fact]
    public async Task Health_WhenDatabaseAndMediaRootAreReachable_ReturnsHealthyWithoutAuth()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("""{"status":"healthy"}""");
    }

    [Fact]
    public async Task Health_WhenMediaRootDisappears_ReturnsUnavailableWithReason()
    {
        using var client = _factory.CreateClient();
        await client.GetAsync("/api/health");
        Directory.Delete(_media.Root, recursive: true);

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())
            .Should()
            .Be(new ErrorResponse("unavailable", "Media folder is not available."));
    }
}
