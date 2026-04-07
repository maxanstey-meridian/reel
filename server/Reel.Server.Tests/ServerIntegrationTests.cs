using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Reel.Server.Infrastructure.Configuration;

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
    public async Task UnknownRoute_WithKey_Returns404()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");

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

    [Fact]
    public void ReelConfig_PortFromConfig_IsUsedByDI()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", "/tmp/reel-test-media");
            builder.UseSetting("Reel:SecretKey", "REEL-TEST");
            builder.UseSetting("Reel:Port", "9999");
        });

        using var scope = factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<ReelConfig>();

        config.Port.Should().Be(9999);
    }

    [Fact]
    public void Startup_ThrowsInvalidOperationException_WhenMediaRootMissing()
    {
        var act = () =>
        {
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Reel:MediaRoot", "");
                builder.UseSetting("Reel:SecretKey", "REEL-TEST");
                builder.UseSetting("Reel:Port", "8080");
            });

            _ = factory.Services;
        };

        act.Should().Throw<InvalidOperationException>().WithMessage("*MediaRoot*required*");
    }

    [Fact]
    public void Startup_ThrowsInvalidOperationException_WhenSecretKeyMissing()
    {
        var act = () =>
        {
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Reel:MediaRoot", "/tmp/reel-test-media");
                builder.UseSetting("Reel:SecretKey", "");
                builder.UseSetting("Reel:Port", "8080");
            });

            _ = factory.Services;
        };

        act.Should().Throw<InvalidOperationException>().WithMessage("*SecretKey*required*");
    }
}
