using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace Reel.Server.Tests;

public sealed class PairEndpointTests : IDisposable
{
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-pair-{Guid.NewGuid():N}");

    public PairEndpointTests()
    {
        Directory.CreateDirectory(_mediaRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, true);
    }

    [Fact]
    public async Task Pair_WithoutWebUrlReturnsServiceUnavailableWithoutRedirect()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://192.168.1.20:8080"),
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/pair?key=REEL-TEST");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Pair_WithConfiguredUrlsDoesNotUseIncomingHost()
    {
        using var factory = CreateFactory("https://reel.example/app/", "http://192.168.1.10:8080/");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://attacker.example"),
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/pair?key=REEL-TEST");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().StartWith("https://reel.example/app/connect?");
        response.Headers.Location!.Query.Should().Contain("url=http%3A%2F%2F192.168.1.10%3A8080");
        response.Headers.Location!.ToString().Should().NotContain("attacker.example");
    }

    [Fact]
    public async Task Pair_WithInvalidKeyReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/pair?key=%27%3Cscript%3Ealert(1)%3C%2Fscript%3E");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
    }

    private WebApplicationFactory<Program> CreateFactory(string? webUrl = null, string? serverUrl = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", _mediaRoot);
            builder.UseSetting("Reel:SecretKey", "REEL-TEST");
            builder.UseSetting("Reel:Port", "8080");
            builder.UseSetting("Reel:WebUrl", webUrl ?? "");
            builder.UseSetting("Reel:ServerUrl", serverUrl ?? "");
        });
}
