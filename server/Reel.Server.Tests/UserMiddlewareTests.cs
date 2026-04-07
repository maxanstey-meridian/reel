using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class UserMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string TestSecretKey = "REEL-TEST";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _tempMediaRoot;

    public UserMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-middleware-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", _tempMediaRoot);
            builder.UseSetting("Reel:SecretKey", TestSecretKey);
            builder.UseSetting("Reel:Port", "8080");
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task WithoutUserIdHeader_DefaultsTo1_RequestSucceeds()
    {
        var client = CreateAuthenticatedClient();

        // GET /api/progress goes through UserMiddleware — should succeed with default user 1
        var response = await client.GetAsync("/api/progress");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithValidUserIdHeader_RequestSucceeds()
    {
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "1");

        var response = await client.GetAsync("/api/progress");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithNonexistentUserId_Returns404()
    {
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "999");

        var response = await client.GetAsync("/api/progress");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WithInvalidUserIdFormat_Returns400()
    {
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "not-a-number");

        var response = await client.GetAsync("/api/progress");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UserEndpoints_SkipMiddlewareValidation()
    {
        var client = CreateAuthenticatedClient();
        // Send request to /api/users with an invalid X-User-Id — should still work
        // because UserMiddleware skips /api/users paths
        client.DefaultRequestHeaders.Add("X-User-Id", "999");

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UserEndpoints_SkipMiddlewareValidation_ForSubPaths()
    {
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "999");

        // /api/users/1 is a sub-path of /api/users — should also skip middleware
        var response = await client.GetAsync("/api/users/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", TestSecretKey);
        return client;
    }
}
