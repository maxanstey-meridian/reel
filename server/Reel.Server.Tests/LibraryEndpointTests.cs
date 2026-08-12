namespace Reel.Server.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

public sealed class LibraryEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public LibraryEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-lib-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_mediaRoot);

        // Create test files
        File.WriteAllBytes(Path.Combine(_mediaRoot, "test.mp4"), new byte[2048]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "The.Matrix.1999.1080p.BluRay.x264.mkv"), new byte[1024]);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", _mediaRoot);
            builder.UseSetting("Reel:SecretKey", "REEL-TEST");
            builder.UseSetting("Reel:Port", "8080");
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    [Fact]
    public async Task ValidKey_ReturnsTree()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");

        var response = await client.GetAsync("/api/library");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("type").GetString().Should().Be("directory");
        root.GetProperty("relativePath").GetString().Should().BeEmpty();
        root.GetProperty("children").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WithoutKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/library");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JsonProperties_AreCamelCase()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");

        var response = await client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("\"relativePath\"");
        json.Should().Contain("\"sizeBytes\"");
        json.Should().NotContain("\"RelativePath\"");
        json.Should().NotContain("\"SizeBytes\"");
    }

    [Fact]
    public async Task FileNodes_ContainParsedTitleAndYear()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");

        var response = await client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var children = doc.RootElement.GetProperty("children");
        var matrixNode = children
            .EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == "The.Matrix.1999.1080p.BluRay.x264.mkv");

        matrixNode.GetProperty("parsedTitle").GetString().Should().Be("The Matrix");
        matrixNode.GetProperty("year").GetInt32().Should().Be(1999);
    }

    [Fact]
    public async Task DirectoryNodes_HaveNullParsedTitleAndYear()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");

        var response = await client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        // Root node is a directory
        doc.RootElement.GetProperty("parsedTitle").ValueKind.Should().Be(JsonValueKind.Null);
        doc.RootElement.GetProperty("year").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task NullFields_AreSerialisedAsNull()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");

        var response = await client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        // Root directory should have null extension and sizeBytes
        var root = doc.RootElement;
        root.GetProperty("extension").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("sizeBytes").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Rescan_DeletesMissingMediaAndDependentRows()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");
        await client.GetAsync("/api/search");

        const string path = "test.mp4";
        await client.PostAsJsonAsync(
            "/api/progress",
            new { path, positionSeconds = 10, durationSeconds = 100 }
        );
        File.Delete(Path.Combine(_mediaRoot, path));

        var response = await client.PostAsync("/api/library/rescan", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var connection = new SqliteConnection(
            $"Data Source={Path.Combine(_mediaRoot, ".reel", "reel-v2.db")}"
        );
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM media WHERE relative_path = 'test.mp4'),
                (SELECT COUNT(*) FROM progress WHERE relative_path = 'test.mp4');
            """;
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetInt32(0).Should().Be(0);
        reader.GetInt32(1).Should().Be(0);
    }

    [Fact]
    public async Task Rescan_WithoutKeyReturns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/library/rescan", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
