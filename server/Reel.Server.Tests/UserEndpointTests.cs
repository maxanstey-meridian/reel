using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class UserEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string TestSecretKey = "REEL-TEST";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _tempMediaRoot;

    public UserEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-user-test-{Guid.NewGuid():N}");
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
    public async Task PostUser_CreatesAndReturns201()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/users", new { name = "Alice", icon = "avatar.png" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user.Should().NotBeNull();
        user!.Id.Should().BeGreaterThan(1);
        user.Name.Should().Be("Alice");
        user.Icon.Should().Be("avatar.png");
    }

    [Fact]
    public async Task PostUser_WithMissingName_Returns400()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/users", new { icon = "avatar.png" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostUser_WithEmptyName_Returns400()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/users", new { name = "", icon = "avatar.png" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUsers_IncludesDefaultUser()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        users.Should().ContainSingle(u => u.Id == 1 && u.Name == "Default");
    }

    [Fact]
    public async Task GetUsers_IncludesCreatedUsers()
    {
        var client = CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/users", new { name = "User1" });
        await client.PostAsJsonAsync("/api/users", new { name = "User2" });

        var response = await client.GetAsync("/api/users");
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();

        users.Should().HaveCountGreaterThanOrEqualTo(3);
        users.Should().Contain(u => u.Name == "User1");
        users.Should().Contain(u => u.Name == "User2");
    }

    [Fact]
    public async Task GetUserById_ReturnsUser()
    {
        var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync("/api/users", new { name = "Specific" });
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        var response = await client.GetAsync($"/api/users/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user!.Name.Should().Be("Specific");
    }

    [Fact]
    public async Task GetUserById_Returns404ForNonexistent()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/users/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchUser_UpdatesAndReturns200()
    {
        var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync("/api/users", new { name = "BeforePatch" });
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/users/{created!.Id}")
        {
            Content = JsonContent.Create(new { name = "AfterPatch", icon = "new.png" })
        };
        var response = await client.SendAsync(patchRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user!.Name.Should().Be("AfterPatch");
        user.Icon.Should().Be("new.png");
    }

    [Fact]
    public async Task PatchUser_Returns404ForNonexistent()
    {
        var client = CreateAuthenticatedClient();

        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/users/999")
        {
            Content = JsonContent.Create(new { name = "Whatever" })
        };
        var response = await client.SendAsync(patchRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_Returns204()
    {
        var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync("/api/users", new { name = "ToDelete" });
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        var response = await client.DeleteAsync($"/api/users/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/users/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_Returns404ForNonexistent()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync("/api/users/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_Returns400ForDefaultUser()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync("/api/users/1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("POST", "/api/users")]
    [InlineData("GET", "/api/users")]
    [InlineData("GET", "/api/users/1")]
    [InlineData("DELETE", "/api/users/1")]
    public async Task UserEndpoints_RequireAuth(string method, string path)
    {
        var client = _factory.CreateClient();

        var response = method switch
        {
            "GET" => await client.GetAsync(path),
            "POST" => await client.PostAsync(path, null),
            "DELETE" => await client.DeleteAsync(path),
            _ => throw new ArgumentException($"Unknown method: {method}")
        };

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", TestSecretKey);
        return client;
    }

    private sealed record UserDto(int Id, string Name, string? Icon);
}
