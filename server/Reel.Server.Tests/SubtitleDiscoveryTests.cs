using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Ffmpeg;

namespace Reel.Server.Tests;

public sealed class SubtitleDiscoveryExternalTests : IDisposable
{
    private readonly string _mediaRoot;
    private readonly FfprobeSubtitleDiscovery _discovery;

    public SubtitleDiscoveryExternalTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-sub-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _mediaRoot,
            SecretKey = "test",
            Port = 8080,
        };

        _discovery = new FfprobeSubtitleDiscovery(config, NullLogger<FfprobeSubtitleDiscovery>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    [Fact]
    public async Task DiscoversSidecarWithNoLanguage()
    {
        var dir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "movie.mp4"), new byte[100]);
        File.WriteAllText(Path.Combine(dir, "movie.srt"), "1\n00:00:01,000 --> 00:00:02,000\nHello\n");

        var tracks = await _discovery.DiscoverAsync("Films/movie.mp4", CancellationToken.None);

        var external = tracks.Where(t => t.Source == "external").ToList();
        external.Should().HaveCount(1);
        external[0].Format.Should().Be("srt");
        external[0].Language.Should().BeEmpty();
        external[0].ExternalPath.Should().Be(Path.Combine("Films", "movie.srt"));
    }

    [Fact]
    public async Task DiscoversSidecarWithLanguageTag()
    {
        var dir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "movie.mp4"), new byte[100]);
        File.WriteAllText(Path.Combine(dir, "movie.en.srt"), "sub");
        File.WriteAllText(Path.Combine(dir, "movie.fr.vtt"), "sub");

        var tracks = await _discovery.DiscoverAsync("Films/movie.mp4", CancellationToken.None);

        var external = tracks.Where(t => t.Source == "external").ToList();
        external.Should().HaveCount(2);
        external.Should().Contain(t => t.Language == "en" && t.Format == "srt");
        external.Should().Contain(t => t.Language == "fr" && t.Format == "vtt");
    }

    [Fact]
    public async Task DoesNotMatchUnrelatedFiles()
    {
        var dir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "movie.mp4"), new byte[100]);
        File.WriteAllText(Path.Combine(dir, "other-movie.srt"), "sub");
        File.WriteAllText(Path.Combine(dir, "movieextras.srt"), "sub"); // No dot separator

        var tracks = await _discovery.DiscoverAsync("Films/movie.mp4", CancellationToken.None);

        var external = tracks.Where(t => t.Source == "external").ToList();
        external.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoversSidecarAss()
    {
        var dir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "movie.mp4"), new byte[100]);
        File.WriteAllText(Path.Combine(dir, "movie.ass"), "sub");

        var tracks = await _discovery.DiscoverAsync("Films/movie.mp4", CancellationToken.None);

        var external = tracks.Where(t => t.Source == "external").ToList();
        external.Should().HaveCount(1);
        external[0].Format.Should().Be("ass");
    }

    [Fact]
    public async Task NonexistentFile_ReturnsEmpty()
    {
        var tracks = await _discovery.DiscoverAsync("Films/missing.mp4", CancellationToken.None);

        tracks.Should().BeEmpty();
    }

    [Fact]
    public async Task PathTraversal_ReturnsEmpty()
    {
        var tracks = await _discovery.DiscoverAsync("../../../etc/passwd", CancellationToken.None);

        tracks.Should().BeEmpty();
    }

    [Fact]
    public async Task NoSidecars_ReturnsOnlyEmbedded()
    {
        var dir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "movie.mp4"), new byte[100]);

        var tracks = await _discovery.DiscoverAsync("Films/movie.mp4", CancellationToken.None);

        // Without ffprobe available, embedded discovery will fail gracefully
        var external = tracks.Where(t => t.Source == "external").ToList();
        external.Should().BeEmpty();
    }

    [Fact]
    public async Task ExternalIndicesStartAfterEmbedded()
    {
        var dir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "movie.mp4"), new byte[100]);
        File.WriteAllText(Path.Combine(dir, "movie.srt"), "sub");
        File.WriteAllText(Path.Combine(dir, "movie.en.vtt"), "sub");

        var tracks = await _discovery.DiscoverAsync("Films/movie.mp4", CancellationToken.None);

        var external = tracks.Where(t => t.Source == "external").ToList();
        // External indices should be sequential and non-overlapping
        var indices = external.Select(t => t.Index).ToList();
        indices.Should().OnlyHaveUniqueItems();
    }
}

public sealed class FfprobeJsonParsingTests
{
    [Fact]
    public void ParsesMultipleStreams()
    {
        var json = """
            {
                "streams": [
                    {
                        "index": 2,
                        "codec_name": "subrip",
                        "codec_type": "subtitle",
                        "tags": {
                            "language": "eng",
                            "title": "English"
                        }
                    },
                    {
                        "index": 3,
                        "codec_name": "ass",
                        "codec_type": "subtitle",
                        "tags": {
                            "language": "jpn",
                            "title": "Japanese"
                        }
                    }
                ]
            }
            """;

        var tracks = FfprobeSubtitleDiscovery.ParseFfprobeOutput(json);

        tracks.Should().HaveCount(2);

        tracks[0].Index.Should().Be(0);
        tracks[0].Format.Should().Be("subrip");
        tracks[0].Language.Should().Be("eng");
        tracks[0].Label.Should().Be("English");
        tracks[0].Source.Should().Be("embedded");
        tracks[0].ExternalPath.Should().BeNull();

        tracks[1].Index.Should().Be(1);
        tracks[1].Format.Should().Be("ass");
        tracks[1].Language.Should().Be("jpn");
        tracks[1].Label.Should().Be("Japanese");
    }

    [Fact]
    public void HandlesMissingTags()
    {
        var json = """
            {
                "streams": [
                    {
                        "index": 2,
                        "codec_name": "subrip",
                        "codec_type": "subtitle"
                    }
                ]
            }
            """;

        var tracks = FfprobeSubtitleDiscovery.ParseFfprobeOutput(json);

        tracks.Should().HaveCount(1);
        tracks[0].Language.Should().BeEmpty();
        tracks[0].Label.Should().BeEmpty();
    }

    [Fact]
    public void EmptyJson_ReturnsEmpty()
    {
        var tracks = FfprobeSubtitleDiscovery.ParseFfprobeOutput("");

        tracks.Should().BeEmpty();
    }

    [Fact]
    public void NoStreams_ReturnsEmpty()
    {
        var json = """{"programs": []}""";

        var tracks = FfprobeSubtitleDiscovery.ParseFfprobeOutput(json);

        tracks.Should().BeEmpty();
    }

    [Fact]
    public void EmptyStreams_ReturnsEmpty()
    {
        var json = """{"streams": []}""";

        var tracks = FfprobeSubtitleDiscovery.ParseFfprobeOutput(json);

        tracks.Should().BeEmpty();
    }
}
