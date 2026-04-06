using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Reel.Server.Services;

namespace Reel.Server.Tests;

public sealed class ServerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", "/tmp/reel-test-media");
            builder.UseSetting("Reel:SecretKey", "REEL-TEST");
            builder.UseSetting("Reel:Port", "8080");
        });
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void ReelConfig_IsResolvableFromDI()
    {
        using var scope = _factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetService<ReelConfig>();

        config.Should().NotBeNull();
        config!.MediaRoot.Should().Be("/tmp/reel-test-media");
        config.SecretKey.Should().Be("REEL-TEST");
        config.Port.Should().Be(8080);
    }
}
