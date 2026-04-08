using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class CollectionEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string TestSecretKey = "REEL-TEST";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _tempMediaRoot;

    public CollectionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-collections-test-{Guid.NewGuid():N}");
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
    public async Task Create_ReturnsCollection()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/collections", new { name = "My Playlist" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<CollectionDto>();
        created!.Name.Should().Be("My Playlist");
        created.Id.Should().BeGreaterThan(0);
        created.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_GetAll_ReturnsEntry()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/collections", new { name = "Test List" });

        var response = await client.GetAsync("/api/collections");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var entries = await response.Content.ReadFromJsonAsync<List<CollectionDto>>();
        entries.Should().ContainSingle(e => e.Name == "Test List");
    }

    [Fact]
    public async Task Get_ReturnsCollectionWithItems()
    {
        var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync("/api/collections", new { name = "Detail" });
        var created = await createResponse.Content.ReadFromJsonAsync<CollectionDto>();

        await client.PostAsJsonAsync($"/api/collections/{created!.Id}/items", new { path = "Films/a.mp4" });

        var response = await client.GetAsync($"/api/collections/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<CollectionWithItemsDto>();
        detail!.Name.Should().Be("Detail");
        detail.Items.Should().ContainSingle(i => i.RelativePath == "Films/a.mp4");
    }

    [Fact]
    public async Task Get_NonExistent_Returns404()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/collections/999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rename_UpdatesName()
    {
        var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync("/api/collections", new { name = "Old" });
        var created = await createResponse.Content.ReadFromJsonAsync<CollectionDto>();

        var patchResponse = await client.PatchAsJsonAsync($"/api/collections/{created!.Id}", new { name = "New" });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/collections/{created.Id}");
        var detail = await getResponse.Content.ReadFromJsonAsync<CollectionWithItemsDto>();
        detail!.Name.Should().Be("New");
    }

    [Fact]
    public async Task Delete_RemovesCollection()
    {
        var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync("/api/collections", new { name = "Delete Me" });
        var created = await createResponse.Content.ReadFromJsonAsync<CollectionDto>();

        var deleteResponse = await client.DeleteAsync($"/api/collections/{created!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/collections/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddItem_RemoveItem_RoundTrips()
    {
        var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync("/api/collections", new { name = "Items" });
        var created = await createResponse.Content.ReadFromJsonAsync<CollectionDto>();

        await client.PostAsJsonAsync($"/api/collections/{created!.Id}/items", new { path = "Films/x.mp4" });

        var getResponse = await client.GetAsync($"/api/collections/{created.Id}");
        var detail = await getResponse.Content.ReadFromJsonAsync<CollectionWithItemsDto>();
        detail!.Items.Should().ContainSingle(i => i.RelativePath == "Films/x.mp4");

        await client.DeleteAsync($"/api/collections/{created.Id}/items?path=Films/x.mp4");

        var getResponse2 = await client.GetAsync($"/api/collections/{created.Id}");
        var detail2 = await getResponse2.Content.ReadFromJsonAsync<CollectionWithItemsDto>();
        detail2!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Reorder_UpdatesOrder()
    {
        var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync("/api/collections", new { name = "Order" });
        var created = await createResponse.Content.ReadFromJsonAsync<CollectionDto>();

        await client.PostAsJsonAsync($"/api/collections/{created!.Id}/items", new { path = "Films/a.mp4" });
        await client.PostAsJsonAsync($"/api/collections/{created.Id}/items", new { path = "Films/b.mp4" });
        await client.PostAsJsonAsync($"/api/collections/{created.Id}/items", new { path = "Films/c.mp4" });

        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/collections/{created.Id}/items/order",
            new { paths = new[] { "Films/c.mp4", "Films/a.mp4", "Films/b.mp4" } }
        );
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/collections/{created.Id}");
        var detail = await getResponse.Content.ReadFromJsonAsync<CollectionWithItemsDto>();
        detail!.Items[0].RelativePath.Should().Be("Films/c.mp4");
        detail.Items[1].RelativePath.Should().Be("Films/a.mp4");
        detail.Items[2].RelativePath.Should().Be("Films/b.mp4");
    }

    [Theory]
    [InlineData("GET", "/api/collections")]
    [InlineData("POST", "/api/collections")]
    [InlineData("GET", "/api/collections/1")]
    [InlineData("DELETE", "/api/collections/1")]
    public async Task CollectionEndpoints_RequireAuth(string method, string path)
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

    private sealed record CollectionDto(int Id, string Name, string CreatedAt, int ItemCount);

    private sealed record CollectionItemDto(string RelativePath, int Position, string AddedAt);

    private sealed record CollectionWithItemsDto(int Id, string Name, string CreatedAt, CollectionItemDto[] Items);
}
