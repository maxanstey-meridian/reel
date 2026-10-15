using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;

namespace Reel.Server.Tests;

public sealed class LibraryEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public LibraryEndpointTests()
    {
        _media.Write("test.mp4", new byte[2048]);
        _media.Write("The.Matrix.1999.1080p.BluRay.x264.mkv", new byte[1024]);
        _factory = _media.CreateFactory();
        _client = _factory.CreateAuthenticatedClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _media.Dispose();
    }

    [Fact]
    public async Task Library_ReturnsCamelCaseTreeWithParsedTitlesAndNullsForDirectories()
    {
        await _factory.WaitForStartupIndexAsync();
        var json = await _client.GetStringAsync("/api/library");
        var root = JsonDocument.Parse(json).RootElement;

        json.Should().NotContain("\"RelativePath\"").And.NotContain("watched");
        root.GetProperty("type").GetString().Should().Be("directory");
        root.GetProperty("relativePath").GetString().Should().BeEmpty();
        foreach (var field in new[] { "parsedTitle", "year", "extension", "sizeBytes" })
        {
            root.GetProperty(field).ValueKind.Should().Be(JsonValueKind.Null);
        }

        var matrix = root.GetProperty("children")
            .EnumerateArray()
            .Single(c =>
                c.GetProperty("name").GetString() == "The.Matrix.1999.1080p.BluRay.x264.mkv"
            );
        matrix.GetProperty("parsedTitle").GetString().Should().Be("The Matrix");
        matrix.GetProperty("year").GetInt32().Should().Be(1999);
        matrix.GetProperty("sizeBytes").GetInt64().Should().Be(1024);
    }

    [Fact]
    public async Task Rescan_IndexesFilesAddedAfterStartupForSearch()
    {
        await _factory.WaitForStartupIndexAsync();
        _media.Write("New.Release.2026.mkv");
        (await _client.GetFromJsonAsync<JsonElement>("/api/search?q=New.Release"))
            .GetArrayLength()
            .Should()
            .Be(0);

        var response = await _client.PostAsync("/api/library/rescan", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetFromJsonAsync<JsonElement>("/api/search?q=New.Release"))
            .EnumerateArray()
            .Select(node => node.GetProperty("relativePath").GetString())
            .Should()
            .Contain("New.Release.2026.mkv");
    }

    [Fact]
    public async Task Rescan_DeletesMissingMediaAndDependentRows()
    {
        await _factory.WaitForStartupIndexAsync();
        var saved = await _client.PostAsJsonAsync(
            "/api/progress",
            new
            {
                relativePath = "test.mp4",
                positionSeconds = 10,
                durationSeconds = 100,
            }
        );
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        File.Delete(Path.Combine(_media.Root, "test.mp4"));

        (await _client.PostAsync("/api/library/rescan", null))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);

        using var connection = _media.Database.GetConnection();
        connection
            .ExecuteScalar<int>(
                "SELECT (SELECT COUNT(*) FROM media WHERE relative_path = 'test.mp4') + (SELECT COUNT(*) FROM progress);"
            )
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task BrowseAndSearch_DescribeEachFileIdentically()
    {
        _media.Write("Shows/Show/Show.S01E01.mkv", new byte[64]);
        await _factory.WaitForStartupIndexAsync();
        (await _client.PostAsync("/api/library/rescan", null)).EnsureSuccessStatusCode();

        var tree = await _client.GetFromJsonAsync<FileNode>("/api/library", _factory.JsonOptions());
        var search = await _client.GetFromJsonAsync<FileNode[]>(
            "/api/search",
            _factory.JsonOptions()
        );

        search
            .Should()
            .HaveCount(3)
            .And.BeEquivalentTo(Files(tree!), options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task RemovedFile_StaysInBrowseUntilARescan()
    {
        await _factory.WaitForStartupIndexAsync();
        File.Delete(Path.Combine(_media.Root, "test.mp4"));

        (await BrowsedPathsAsync()).Should().Contain("test.mp4");
        (await _client.PostAsync("/api/library/rescan", null)).EnsureSuccessStatusCode();
        (await BrowsedPathsAsync()).Should().NotContain("test.mp4");
    }

    [Fact]
    public async Task Rescan_FindingNoMediaIsRefusedAndKeepsTheIndex()
    {
        await _factory.WaitForStartupIndexAsync();
        foreach (var file in Directory.EnumerateFiles(_media.Root))
        {
            File.Delete(file);
        }

        var response = await _client.PostAsync("/api/library/rescan", null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!
            .Code.Should()
            .Be("rescan_refused");
        (await BrowsedPathsAsync()).Should().HaveCount(2);
    }

    private async Task<IEnumerable<string>> BrowsedPathsAsync() =>
        Files((await _client.GetFromJsonAsync<FileNode>("/api/library", _factory.JsonOptions()))!)
            .Select(node => node.RelativePath);

    private static IEnumerable<FileNode> Files(FileNode node) =>
        node.Children is null ? [node] : node.Children.SelectMany(Files);
}
