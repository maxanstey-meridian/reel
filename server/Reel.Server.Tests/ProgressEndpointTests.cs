using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class ProgressEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string TestSecretKey = "REEL-TEST";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _tempMediaRoot;

    public ProgressEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-progress-test-{Guid.NewGuid():N}");
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
    public async Task PostProgress_StoresEntry()
    {
        var client = CreateAuthenticatedClient();

        var postResponse = await client.PostAsJsonAsync("/api/progress", new
        {
            relativePath = "Films/test.mp4",
            positionSeconds = 120.5,
            durationSeconds = 7200.0
        });
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync("/api/progress");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var entries = await getResponse.Content.ReadFromJsonAsync<List<WatchProgressDto>>();
        entries.Should().ContainSingle(e => e.RelativePath == "Films/test.mp4");

        var entry = entries!.First(e => e.RelativePath == "Films/test.mp4");
        entry.PositionSeconds.Should().Be(120.5);
        entry.DurationSeconds.Should().Be(7200.0);
        entry.LastWatched.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PostProgress_Upserts()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/progress", new
        {
            relativePath = "Films/upsert.mp4",
            positionSeconds = 60.0,
            durationSeconds = 3600.0
        });

        await client.PostAsJsonAsync("/api/progress", new
        {
            relativePath = "Films/upsert.mp4",
            positionSeconds = 300.0,
            durationSeconds = 3600.0
        });

        var response = await client.GetAsync("/api/progress?path=Films/upsert.mp4");
        var entry = await response.Content.ReadFromJsonAsync<WatchProgressDto>();
        entry!.PositionSeconds.Should().Be(300.0);
    }

    [Fact]
    public async Task GetAll_ReturnsSortedByLastWatched()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/progress", new
        {
            relativePath = "Films/older.mp4",
            positionSeconds = 10.0,
            durationSeconds = 100.0
        });

        // Small delay so timestamps differ
        await Task.Delay(50);

        await client.PostAsJsonAsync("/api/progress", new
        {
            relativePath = "Films/newer.mp4",
            positionSeconds = 20.0,
            durationSeconds = 200.0
        });

        var response = await client.GetAsync("/api/progress");
        var entries = await response.Content.ReadFromJsonAsync<List<WatchProgressDto>>();

        entries.Should().HaveCountGreaterThanOrEqualTo(2);
        var olderIdx = entries!.FindIndex(e => e.RelativePath == "Films/older.mp4");
        var newerIdx = entries.FindIndex(e => e.RelativePath == "Films/newer.mp4");
        newerIdx.Should().BeLessThan(olderIdx, "newer entry should appear first (descending order)");
    }

    [Fact]
    public async Task GetByPath_ReturnsSpecificEntry()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/progress", new
        {
            relativePath = "Films/a.mp4",
            positionSeconds = 10.0,
            durationSeconds = 100.0
        });

        await client.PostAsJsonAsync("/api/progress", new
        {
            relativePath = "Films/b.mp4",
            positionSeconds = 20.0,
            durationSeconds = 200.0
        });

        var response = await client.GetAsync("/api/progress?path=Films/a.mp4");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var entry = await response.Content.ReadFromJsonAsync<WatchProgressDto>();
        entry!.RelativePath.Should().Be("Films/a.mp4");
        entry.PositionSeconds.Should().Be(10.0);
    }

    [Fact]
    public async Task GetByPath_NotFound_Returns404()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/progress?path=nonexistent.mp4");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("POST", "/api/progress")]
    [InlineData("GET", "/api/progress")]
    public async Task ProgressEndpoints_RequireAuth(string method, string path)
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

    private sealed record WatchProgressDto(
        string RelativePath,
        double PositionSeconds,
        double DurationSeconds,
        DateTime LastWatched
    );
}
