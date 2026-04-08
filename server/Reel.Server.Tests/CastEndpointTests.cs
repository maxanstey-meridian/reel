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
        castResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var currentResponse = await client.GetAsync("/api/cast/current");
        currentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command.Should().NotBeNull();
        status.Command!.Type.Should().Be("play");
        status.Command.RelativePath.Should().Be("Films/test.mp4");
    }

    [Fact]
    public async Task GetCurrent_NoPending_Returns200WithNullCommand()
    {
        var client = CreateAuthenticatedClient();

        // Clear any leftover state from other tests — stop clears queue
        // and adds a stop command; ack removes the stop command.
        await client.PostAsync("/api/cast/stop", null);
        await client.PostAsync("/api/cast/ack", null);

        var response = await client.GetAsync("/api/cast/current");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await response.Content.ReadFromJsonAsync<CastStatusDto>();
        status.Should().NotBeNull();
        status!.Command.Should().BeNull();
    }

    [Fact]
    public async Task PostAck_ClearsPendingCommand()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/cast", new { relativePath = "Films/test.mp4" });
        await client.PostAsync("/api/cast/ack", null);

        var response = await client.GetAsync("/api/cast/current");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await response.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command.Should().BeNull();
    }

    [Fact]
    public async Task PostStop_StoresPendingStopCommand()
    {
        var client = CreateAuthenticatedClient();

        var stopResponse = await client.PostAsync("/api/cast/stop", null);
        stopResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

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

        var response = method == "GET" ? await client.GetAsync(path) : await client.PostAsync(path, null);

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

    [Fact]
    public async Task PostCastQueue_LoadsMultipleItems_GetCurrentReturnsFirst()
    {
        var client = CreateAuthenticatedClient();

        var paths = new[] { "Films/a.mp4", "Films/b.mp4", "Films/c.mp4" };
        var response = await client.PostAsJsonAsync("/api/cast/queue", new { paths });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var currentResponse = await client.GetAsync("/api/cast/current");
        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command.Should().NotBeNull();
        status.Command!.Type.Should().Be("play");
        status.Command.RelativePath.Should().Be("Films/a.mp4");
    }

    [Fact]
    public async Task PostCastQueue_AckPopsFront_NextBecomesPending()
    {
        var client = CreateAuthenticatedClient();

        var paths = new[] { "Films/a.mp4", "Films/b.mp4", "Films/c.mp4" };
        await client.PostAsJsonAsync("/api/cast/queue", new { paths });

        // Ack first item
        await client.PostAsync("/api/cast/ack", null);

        var currentResponse = await client.GetAsync("/api/cast/current");
        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command!.RelativePath.Should().Be("Films/b.mp4");
    }

    [Fact]
    public async Task PostCastQueue_AckThroughEntireQueue_CurrentBecomesNull()
    {
        var client = CreateAuthenticatedClient();

        var paths = new[] { "Films/a.mp4", "Films/b.mp4" };
        await client.PostAsJsonAsync("/api/cast/queue", new { paths });

        await client.PostAsync("/api/cast/ack", null); // pop a
        await client.PostAsync("/api/cast/ack", null); // pop b

        var currentResponse = await client.GetAsync("/api/cast/current");
        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command.Should().BeNull();
    }

    [Fact]
    public async Task PostCast_ClearsExistingQueue()
    {
        var client = CreateAuthenticatedClient();

        var paths = new[] { "Films/a.mp4", "Films/b.mp4", "Films/c.mp4" };
        await client.PostAsJsonAsync("/api/cast/queue", new { paths });

        // Single cast should replace the queue
        await client.PostAsJsonAsync("/api/cast", new { relativePath = "Films/single.mp4" });

        var currentResponse = await client.GetAsync("/api/cast/current");
        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command!.RelativePath.Should().Be("Films/single.mp4");

        // Queue should be empty (only the single item, no remaining)
        var queueResponse = await client.GetAsync("/api/cast/queue");
        var queue = await queueResponse.Content.ReadFromJsonAsync<string[]>();
        queue.Should().BeEmpty();
    }

    [Fact]
    public async Task PostStop_ClearsQueue()
    {
        var client = CreateAuthenticatedClient();

        var paths = new[] { "Films/a.mp4", "Films/b.mp4" };
        await client.PostAsJsonAsync("/api/cast/queue", new { paths });

        await client.PostAsync("/api/cast/stop", null);

        // Current should be stop command
        var currentResponse = await client.GetAsync("/api/cast/current");
        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command!.Type.Should().Be("stop");

        // Queue should be empty
        var queueResponse = await client.GetAsync("/api/cast/queue");
        var queue = await queueResponse.Content.ReadFromJsonAsync<string[]>();
        queue.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCastQueue_ReturnsRemainingPathsExcludingCurrent()
    {
        var client = CreateAuthenticatedClient();

        var paths = new[] { "Films/a.mp4", "Films/b.mp4", "Films/c.mp4" };
        await client.PostAsJsonAsync("/api/cast/queue", new { paths });

        var queueResponse = await client.GetAsync("/api/cast/queue");
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var queue = await queueResponse.Content.ReadFromJsonAsync<string[]>();
        queue.Should().Equal("Films/b.mp4", "Films/c.mp4");
    }

    [Theory]
    [InlineData("POST", "/api/cast/queue")]
    [InlineData("GET", "/api/cast/queue")]
    public async Task CastQueueEndpoints_RequireAuth(string method, string path)
    {
        var client = _factory.CreateClient();

        var response = method == "GET"
            ? await client.GetAsync(path)
            : await client.PostAsync(path, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", TestSecretKey);
        return client;
    }

    [Fact]
    public async Task PostCast_WithExplicitPosition_IncludesPositionInCommand()
    {
        var client = CreateAuthenticatedClient();

        var castResponse = await client.PostAsJsonAsync(
            "/api/cast",
            new { relativePath = "Films/resume-explicit.mp4", positionSeconds = 42.5 }
        );
        castResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var currentResponse = await client.GetAsync("/api/cast/current");
        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command!.RelativePath.Should().Be("Films/resume-explicit.mp4");
        status.Command.PositionSeconds.Should().Be(42.5);
    }

    [Fact]
    public async Task PostCast_WithoutPosition_AutoPopulatesFromProgressStore()
    {
        var client = CreateAuthenticatedClient();

        // Seed progress for the file
        await client.PostAsJsonAsync(
            "/api/progress",
            new { relativePath = "Films/resume-auto.mp4", positionSeconds = 300.0, durationSeconds = 7200.0 }
        );

        // Cast without explicit position
        var castResponse = await client.PostAsJsonAsync(
            "/api/cast",
            new { relativePath = "Films/resume-auto.mp4" }
        );
        castResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var currentResponse = await client.GetAsync("/api/cast/current");
        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command!.RelativePath.Should().Be("Films/resume-auto.mp4");
        status.Command.PositionSeconds.Should().Be(300.0);
    }

    [Fact]
    public async Task PostCast_WithoutPosition_NoProgress_PositionIsNull()
    {
        var client = CreateAuthenticatedClient();

        var castResponse = await client.PostAsJsonAsync(
            "/api/cast",
            new { relativePath = "Films/no-progress.mp4" }
        );
        castResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var currentResponse = await client.GetAsync("/api/cast/current");
        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command!.RelativePath.Should().Be("Films/no-progress.mp4");
        status.Command.PositionSeconds.Should().BeNull();
    }

    [Fact]
    public async Task PostCastQueue_AutoPopulatesFirstItemPosition()
    {
        var client = CreateAuthenticatedClient();

        // Seed progress for the first item only
        await client.PostAsJsonAsync(
            "/api/progress",
            new { relativePath = "Films/queue-a.mp4", positionSeconds = 120.0, durationSeconds = 3600.0 }
        );

        var paths = new[] { "Films/queue-a.mp4", "Films/queue-b.mp4" };
        var castResponse = await client.PostAsJsonAsync("/api/cast/queue", new { paths });
        castResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // First item should have position from progress
        var currentResponse = await client.GetAsync("/api/cast/current");
        var status = await currentResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        status!.Command!.RelativePath.Should().Be("Films/queue-a.mp4");
        status.Command.PositionSeconds.Should().Be(120.0);

        // Ack first, second should have no position
        await client.PostAsync("/api/cast/ack", null);
        var secondResponse = await client.GetAsync("/api/cast/current");
        var secondStatus = await secondResponse.Content.ReadFromJsonAsync<CastStatusDto>();
        secondStatus!.Command!.RelativePath.Should().Be("Films/queue-b.mp4");
        secondStatus.Command.PositionSeconds.Should().BeNull();
    }

    private sealed record CastCommandDto(string Type, string? RelativePath, double? PositionSeconds = null);

    private sealed record CastStatusDto(CastCommandDto? Command);
}
