using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class FavouritesEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string TestSecretKey = "REEL-TEST";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _tempMediaRoot;

    public FavouritesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-favourites-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempMediaRoot, "Films"));
        foreach (var name in new[] { "x.mp4", "fav.mp4", "dup.mp4", "del.mp4", "older.mp4", "newer.mp4" })
            File.WriteAllBytes(Path.Combine(_tempMediaRoot, "Films", name), [1]);

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
    public async Task Add_ReturnsSuccess()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films/x.mp4" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Add_GetAll_ReturnsEntry()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films/fav.mp4" });

        var response = await client.GetAsync("/api/favourites");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var entries = await response.Content.ReadFromJsonAsync<List<FavouriteDto>>();
        entries.Should().ContainSingle(e => e.RelativePath == "Films/fav.mp4");
        entries![0].AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Add_Duplicate_IsIdempotent()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films/dup.mp4" });
        await client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films/dup.mp4" });

        var response = await client.GetAsync("/api/favourites");
        var entries = await response.Content.ReadFromJsonAsync<List<FavouriteDto>>();

        entries!.Where(e => e.RelativePath == "Films/dup.mp4").Should().ContainSingle();
    }

    [Fact]
    public async Task Remove_DeletesEntry()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films/del.mp4" });
        await client.DeleteAsync("/api/favourites?path=Films/del.mp4");

        var response = await client.GetAsync("/api/favourites");
        var entries = await response.Content.ReadFromJsonAsync<List<FavouriteDto>>();

        entries!.Where(e => e.RelativePath == "Films/del.mp4").Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_NonExistent_Returns200()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync("/api/favourites?path=Films/nope.mp4");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_SortedByAddedAt()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films/older.mp4" });
        await Task.Delay(50);
        await client.PostAsJsonAsync("/api/favourites", new { relativePath = "Films/newer.mp4" });

        var response = await client.GetAsync("/api/favourites");
        var entries = await response.Content.ReadFromJsonAsync<List<FavouriteDto>>();

        entries.Should().HaveCountGreaterThanOrEqualTo(2);
        var olderIdx = entries!.FindIndex(e => e.RelativePath == "Films/older.mp4");
        var newerIdx = entries.FindIndex(e => e.RelativePath == "Films/newer.mp4");
        newerIdx.Should().BeLessThan(olderIdx, "newer entry should appear first (descending order)");
    }

    [Theory]
    [InlineData("POST", "/api/favourites")]
    [InlineData("DELETE", "/api/favourites?path=x")]
    [InlineData("GET", "/api/favourites")]
    public async Task FavouritesEndpoints_RequireAuth(string method, string path)
    {
        var client = _factory.CreateClient();

        var response = method switch
        {
            "GET" => await client.GetAsync(path),
            "DELETE" => await client.DeleteAsync(path),
            _ => await client.PostAsync(path, null),
        };

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", TestSecretKey);
        return client;
    }

    private sealed record FavouriteDto(string RelativePath, DateTime AddedAt);
}
