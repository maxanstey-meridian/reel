using System.Net;
using System.Net.Http.Json;
using Dapper;
using FluentAssertions;
using Reel.Server.Common;

namespace Reel.Server.Tests;

public sealed class TagEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task TagsPersistNormaliseAndRemainEmptyAfterDeletion()
    {
        using var factory = _media.CreateFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PutAsJsonAsync(
            "/api/tags",
            new { path = "Shows//", tags = new[] { "#Media", "media", "comedy" } }
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<Dictionary<string, string[]>>("/api/tags"))!
            ["Shows"]
            .Should()
            .Equal("media", "comedy");
        await client.PutAsJsonAsync(
            "/api/tags",
            new { path = "Shows", tags = Array.Empty<string>() }
        );
        (await client.GetFromJsonAsync<Dictionary<string, string[]>>("/api/tags"))!
            ["Shows"]
            .Should()
            .BeEmpty();
        (
            await client.PutAsJsonAsync(
                "/api/tags",
                new { path = "../escape", tags = new[] { "test" } }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
    }

    // Unreadable stored data is a server fault, not a bad request.
    [Fact]
    public async Task CorruptStoredTags_Returns500()
    {
        // Startup reconciling keeps tags only on folders that still hold media.
        _media.Write("Shows/a.mkv");
        using (var connection = _media.Database.GetConnection())
        {
            connection.Execute("INSERT INTO item_tags VALUES ('Shows', 'not json');");
        }

        using var factory = _media.CreateFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!
            .Code.Should()
            .Be("server_error");
    }
}
