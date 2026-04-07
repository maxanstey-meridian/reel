using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class DisplayTitleEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _mediaRoot;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DisplayTitleEndpointTests(WebApplicationFactory<Program> factory)
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-dt-ep-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        File.WriteAllBytes(Path.Combine(_mediaRoot, "The.Matrix.1999.1080p.BluRay.x264.mkv"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "raw-file.mp4"), new byte[1024]);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", _mediaRoot);
            builder.UseSetting("Reel:SecretKey", "REEL-TEST");
            builder.UseSetting("Reel:Port", "8080");
        });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Secret-Key", "REEL-TEST");
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    [Fact]
    public async Task Library_FileNodes_IncludeDisplayTitle()
    {
        var response = await _client.GetAsync("/api/library");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var children = doc.RootElement.GetProperty("children");

        var matrixNode = children
            .EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == "The.Matrix.1999.1080p.BluRay.x264.mkv");

        // displayTitle should be the parsed title (no manual override set)
        matrixNode.GetProperty("displayTitle").GetString().Should().Be("The Matrix");
    }

    [Fact]
    public async Task Library_DisplayTitle_FallsToFilename_WhenNoParsedTitle()
    {
        var response = await _client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var children = doc.RootElement.GetProperty("children");

        var rawNode = children
            .EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == "raw-file.mp4");

        // raw-file.mp4 doesn't have a year, so parser returns "raw-file" as title
        // DisplayTitle should be the parsed title or the raw name
        rawNode.GetProperty("displayTitle").ValueKind.Should().Be(JsonValueKind.String);
        rawNode.GetProperty("displayTitle").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Library_DisplayTitle_UsesManualOverride_AfterPatch()
    {
        // Populate media rows
        await _client.GetAsync("/api/library");

        // Set manual display_title
        await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Patch, "/api/media/detail?path=The.Matrix.1999.1080p.BluRay.x264.mkv")
            {
                Content = JsonContent.Create(new { displayTitle = "The Matrix (Remastered)" }, options: JsonOptions),
            }
        );

        // Fetch library again
        var response = await _client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var children = doc.RootElement.GetProperty("children");

        var matrixNode = children
            .EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == "The.Matrix.1999.1080p.BluRay.x264.mkv");

        matrixNode.GetProperty("displayTitle").GetString().Should().Be("The Matrix (Remastered)");
    }

    [Fact]
    public async Task Library_DisplayTitle_RevertsOnClear()
    {
        await _client.GetAsync("/api/library");

        // Set then clear
        await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Patch, "/api/media/detail?path=The.Matrix.1999.1080p.BluRay.x264.mkv")
            {
                Content = JsonContent.Create(new { displayTitle = "Custom" }, options: JsonOptions),
            }
        );
        await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Patch, "/api/media/detail?path=The.Matrix.1999.1080p.BluRay.x264.mkv")
            {
                Content = JsonContent.Create(new { displayTitle = "" }, options: JsonOptions),
            }
        );

        var response = await _client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var children = doc.RootElement.GetProperty("children");

        var matrixNode = children
            .EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == "The.Matrix.1999.1080p.BluRay.x264.mkv");

        // Should revert to parsed title
        matrixNode.GetProperty("displayTitle").GetString().Should().Be("The Matrix");
    }

    [Fact]
    public async Task Library_DirectoryNodes_HaveNullDisplayTitle()
    {
        // Create a subdirectory with a file
        var subDir = Path.Combine(_mediaRoot, "Series");
        Directory.CreateDirectory(subDir);
        File.WriteAllBytes(Path.Combine(subDir, "ep1.mp4"), new byte[1024]);

        var response = await _client.GetAsync("/api/library");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        // Root directory
        doc.RootElement.GetProperty("displayTitle").ValueKind.Should().Be(JsonValueKind.Null);

        // Series subdirectory
        var seriesNode = doc.RootElement
            .GetProperty("children")
            .EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == "Series");
        seriesNode.GetProperty("displayTitle").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Search_Results_IncludeDisplayTitle()
    {
        await _client.GetAsync("/api/library");

        var response = await _client.GetAsync("/api/search?q=Matrix");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetArrayLength().Should().Be(1);
        doc.RootElement[0].GetProperty("displayTitle").GetString().Should().Be("The Matrix");
    }

    [Fact]
    public async Task Search_Results_ReflectManualOverride()
    {
        await _client.GetAsync("/api/library");

        await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Patch, "/api/media/detail?path=The.Matrix.1999.1080p.BluRay.x264.mkv")
            {
                Content = JsonContent.Create(new { displayTitle = "The Matrix Special Edition" }, options: JsonOptions),
            }
        );

        var response = await _client.GetAsync("/api/search?q=Matrix");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement[0].GetProperty("displayTitle").GetString().Should().Be("The Matrix Special Edition");
    }
}
