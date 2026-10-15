using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Reel.Server.Contracts.Reel;

namespace Reel.Server.Tests;

public sealed class FavouritesEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public FavouritesEndpointTests()
    {
        foreach (var name in new[] { "fav.mp4", "older.mp4", "newer.mp4" })
        {
            _media.Write($"Films/{name}");
        }

        _factory = _media.CreateFactory();
        _client = _factory.CreateAuthenticatedClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _media.Dispose();
    }

    private Task<Favourite[]?> GetAll() =>
        _client.GetFromJsonAsync<Favourite[]>("/api/favourites", _factory.JsonOptions());

    [Fact]
    public async Task AddTwiceThenRemove_IsIdempotentAndNormalisesThePath()
    {
        (await _client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films//fav.mp4" }))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        await _client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films/fav.mp4" });

        var favourite = (await GetAll()).Should().ContainSingle().Subject;
        favourite.RelativePath.Should().Be("Films/fav.mp4");
        favourite.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        (await _client.DeleteAsync("/api/favourites?path=Films/fav.mp4"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await GetAll()).Should().BeEmpty();
        (await _client.DeleteAsync("/api/favourites?path=Films/nope.mp4"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_SortedNewestFirst()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = _media.CreateFactory(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time))
        );
        using var client = factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films/older.mp4" });
        time.Advance(TimeSpan.FromMinutes(1));
        await client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films/newer.mp4" });

        (await client.GetFromJsonAsync<Favourite[]>("/api/favourites", factory.JsonOptions()))!
            .Select(e => e.RelativePath)
            .Should()
            .Equal("Films/newer.mp4", "Films/older.mp4");
    }

    [Fact]
    public async Task Add_ForPlayableFileAddedAfterStartup_IndexesAndStores()
    {
        await _factory.WaitForStartupIndexAsync();
        _media.Write("Films/late.mp4");

        var response = await _client.PostAsJsonAsync(
            "/api/favourites",
            new { relativePath = "Films/late.mp4" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAll()).Should().ContainSingle(e => e.RelativePath == "Films/late.mp4");
    }
}
