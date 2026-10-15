using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class PairEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task Pair_WithoutWebUrlReturnsServiceUnavailableWithoutRedirect()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://192.168.1.20:8080"),
                AllowAutoRedirect = false,
            }
        );

        var response = await client.GetAsync("/pair?key=REEL-TEST");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Pair_WithConfiguredUrlsDoesNotUseIncomingHost()
    {
        using var factory = CreateFactory("https://reel.example/app/");
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://attacker.example"),
                AllowAutoRedirect = false,
            }
        );

        var response = await client.GetAsync("/pair?key=REEL-TEST");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response
            .Headers.Location!.ToString()
            .Should()
            .Be("https://reel.example/app/connect?key=REEL-TEST");
    }

    [Fact]
    public async Task Pair_WithInvalidKeyReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var response = await client.GetAsync("/pair?key=%27%3Cscript%3Ealert(1)%3C%2Fscript%3E");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
        (await response.Content.ReadAsStringAsync()).Should().Contain("Scan the QR code");
    }

    private WebApplicationFactory<Program> CreateFactory(string? webUrl = null) =>
        _media.CreateFactory(builder => builder.UseSetting("Reel:WebUrl", webUrl ?? ""));
}
