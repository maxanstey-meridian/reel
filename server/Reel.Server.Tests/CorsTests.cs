using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class CorsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", "/tmp/reel-test-media");
            builder.UseSetting("Reel:SecretKey", "REEL-TEST");
            builder.UseSetting("Reel:Port", "8080");
        });
    }

    [Fact]
    public async Task DevMode_PreflightRequest_ReturnsCorsHeaders()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/progress");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type, X-Secret-Key");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("*");
        response
            .Headers.GetValues("Access-Control-Allow-Methods")
            .Should()
            .Contain(m => m.Contains("POST", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DevMode_SimpleRequest_IncludesCorsOriginHeader()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:3000");

        var response = await client.GetAsync("/api/health");

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("*");
    }
}
