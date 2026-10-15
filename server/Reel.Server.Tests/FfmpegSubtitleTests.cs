using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Reel.Server.Contracts.Reel;
using Reel.Server.Media;

namespace Reel.Server.Tests;

// Embedded subtitles exercised end to end with real ffprobe/ffmpeg on a file whose name would
// have broken the old interpolated command lines.
public sealed class FfmpegSubtitleTests : IDisposable
{
    private const string RelativePath = "Films/Quote \"Test\" -y.mkv";
    private readonly TempMediaRoot _media = new();
    private readonly string _fullPath;

    public FfmpegSubtitleTests()
    {
        _fullPath = _media.Write(RelativePath);
        TestMedia.CreateVideoWithSubtitle(_fullPath, "Embedded hello");
    }

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task EmbeddedTrack_IsDiscoveredAndServedAsWebVtt()
    {
        using var factory = _media.CreateFactory();
        using var client = factory.CreateAuthenticatedClient();
        var path = Uri.EscapeDataString(RelativePath);

        var tracks = await client.GetFromJsonAsync<SubtitleTrack[]>(
            $"/api/files/subtitles?path={path}",
            factory.JsonOptions()
        );
        var embedded = tracks!
            .Should()
            .ContainSingle(track => track.Source == SubtitleSource.Embedded)
            .Subject;
        var response = await client.GetAsync(
            $"/api/files/subtitles/content?path={path}&index={embedded.Index}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/vtt");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().StartWith("WEBVTT").And.Contain("Embedded hello");
    }

    [Fact]
    public async Task Extraction_AbandonedByTheRequester_StillFillsTheCache()
    {
        var extractor = new FfmpegSubtitleExtractor(
            _media.Paths,
            TimeProvider.System,
            NullLogger<FfmpegSubtitleExtractor>.Instance
        );
        var cacheDirectory = _media.Paths.AppDataPath("subtitles");
        using var request = new CancellationTokenSource();

        var abandoned = extractor.ExtractAsync(_fullPath, 0, request.Token);
        // Joins the run the abandoned caller started, so a cancelled run would fail it.
        var patient = extractor.ExtractAsync(_fullPath, 0, CancellationToken.None);
        await request.CancelAsync();

        await abandoned.Invoking(task => task).Should().ThrowAsync<OperationCanceledException>();
        (await patient).Should().NotBeNull();

        CachedFiles()
            .Should()
            .ContainSingle(file => File.ReadAllText(file).Contains("Embedded hello"));

        IEnumerable<string> CachedFiles() =>
            Directory.Exists(cacheDirectory)
                ? Directory.EnumerateFiles(cacheDirectory, "*.vtt")
                : [];
    }

    [Fact]
    public async Task Extraction_RunningOutOfTime_IsUnavailable()
    {
        var time = new FakeTimeProvider();
        var extractor = new FfmpegSubtitleExtractor(
            _media.Paths,
            time,
            NullLogger<FfmpegSubtitleExtractor>.Instance
        );

        var extraction = extractor.ExtractAsync(_fullPath, 0, CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(11));

        (await extraction).Should().BeNull();
    }
}
