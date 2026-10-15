using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Reel.Server.Contracts.Reel;

namespace Reel.Server.Tests;

public sealed class ProgressEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ProgressEndpointTests()
    {
        foreach (var name in new[] { "test.mp4", "upsert.mp4", "older.mp4", "newer.mp4" })
        {
            _media.Write($"Films/{name}");
        }

        _factory = _media.CreateFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
        _media.Dispose();
    }

    [Fact]
    public async Task PostProgress_StoresEntry()
    {
        var client = _factory.CreateAuthenticatedClient();

        var postResponse = await client.PostAsJsonAsync(
            "/api/progress",
            new
            {
                relativePath = "Films/test.mp4",
                positionSeconds = 120.5,
                durationSeconds = 7200.0,
            }
        );
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync("/api/progress");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var entries = await getResponse.Content.ReadFromJsonAsync<List<WatchProgress>>();
        entries.Should().ContainSingle(e => e.RelativePath == "Films/test.mp4");

        var entry = entries!.First(e => e.RelativePath == "Films/test.mp4");
        entry.PositionSeconds.Should().Be(120.5);
        entry.DurationSeconds.Should().Be(7200.0);
        entry.LastWatched.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PostProgress_Upserts()
    {
        var client = _factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync(
            "/api/progress",
            new
            {
                relativePath = "Films/upsert.mp4",
                positionSeconds = 60.0,
                durationSeconds = 3600.0,
            }
        );

        await client.PostAsJsonAsync(
            "/api/progress",
            new
            {
                relativePath = "Films/upsert.mp4",
                positionSeconds = 300.0,
                durationSeconds = 3600.0,
            }
        );

        var response = await client.GetAsync("/api/progress");
        var entries = await response.Content.ReadFromJsonAsync<List<WatchProgress>>();
        var entry = entries!.First(e => e.RelativePath == "Films/upsert.mp4");
        entry.PositionSeconds.Should().Be(300.0);
    }

    [Fact]
    public async Task GetAll_ReturnsSortedByLastWatched()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = _media.CreateFactory(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time))
        );
        using var client = factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync(
            "/api/progress",
            new
            {
                relativePath = "Films/older.mp4",
                positionSeconds = 10.0,
                durationSeconds = 100.0,
            }
        );

        time.Advance(TimeSpan.FromMinutes(1));

        await client.PostAsJsonAsync(
            "/api/progress",
            new
            {
                relativePath = "Films/newer.mp4",
                positionSeconds = 20.0,
                durationSeconds = 200.0,
            }
        );

        var response = await client.GetAsync("/api/progress");
        var entries = await response.Content.ReadFromJsonAsync<List<WatchProgress>>();

        entries.Should().HaveCountGreaterThanOrEqualTo(2);
        var olderIdx = entries!.FindIndex(e => e.RelativePath == "Films/older.mp4");
        var newerIdx = entries.FindIndex(e => e.RelativePath == "Films/newer.mp4");
        newerIdx
            .Should()
            .BeLessThan(olderIdx, "newer entry should appear first (descending order)");
    }

    [Fact]
    public async Task PostProgress_ForPlayableFileAddedAfterStartup_IndexesAndStores()
    {
        await _factory.WaitForStartupIndexAsync();
        _media.Write("Films/late.mp4");
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(
            "/api/progress",
            new
            {
                relativePath = "Films/late.mp4",
                positionSeconds = 5.0,
                durationSeconds = 50.0,
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = await client.GetFromJsonAsync<List<WatchProgress>>("/api/progress");
        entries.Should().ContainSingle(e => e.RelativePath == "Films/late.mp4");
    }

    [Fact]
    public async Task PostProgress_ForMissingFile_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(
            "/api/progress",
            new
            {
                relativePath = "Films/missing.mp4",
                positionSeconds = 5.0,
                durationSeconds = 50.0,
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // A path that differs only by an empty segment must land on the scanned row, which a rescan keeps.
    [Fact]
    public async Task PostProgress_StoresTheCanonicalPathThatSurvivesARescan()
    {
        var client = _factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync(
            "/api/progress",
            new
            {
                relativePath = "Films//test.mp4",
                positionSeconds = 5.0,
                durationSeconds = 50.0,
            }
        );
        (await client.PostAsync("/api/library/rescan", null))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);

        var entries = await client.GetFromJsonAsync<List<WatchProgress>>("/api/progress");
        entries.Should().ContainSingle().Which.RelativePath.Should().Be("Films/test.mp4");
    }
}
