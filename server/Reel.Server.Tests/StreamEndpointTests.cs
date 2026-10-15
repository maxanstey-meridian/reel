using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Contracts.Reel;
using Reel.Server.Media;

namespace Reel.Server.Tests;

public sealed class StreamEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _direct;

    public StreamEndpointTests()
    {
        _media.Write("Films/test.mp4", "fake-mp4-content"u8.ToArray());
        _media.Write("Films/movie.mkv", "fake-mkv-content"u8.ToArray());
        _media.Write("song.mp3", "fake-mp3-content"u8.ToArray());
        _direct = _media.CreateFactory();
    }

    public void Dispose()
    {
        _direct.Dispose();
        _media.Dispose();
    }

    private WebApplicationFactory<Program> CreateCompressedFactory() =>
        _media.CreateFactory(builder => builder.UseSetting("Reel:Compression", "480p"));

    [Theory]
    [InlineData("Films/test.mp4&start=30", "video/mp4", "fake-mp4-content")]
    [InlineData("Films/movie.mkv", "video/x-matroska", "fake-mkv-content")]
    [InlineData("song.mp3", "audio/mpeg", "fake-mp3-content")]
    public async Task DirectMode_ServesTheFileWithItsContentTypeIgnoringStart(
        string query,
        string type,
        string body
    )
    {
        using var client = _direct.CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/files/stream?path={query}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(type);
        response.Headers.AcceptRanges.Should().Contain("bytes");
        (await response.Content.ReadAsStringAsync()).Should().Be(body);
    }

    [Fact]
    public async Task DirectMode_RangeRequest_Returns206WithContentRange()
    {
        using var client = _direct.CreateAuthenticatedClient();
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/files/stream?path=Films/test.mp4"
        );
        request.Headers.Range = new RangeHeaderValue(0, 3);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentRange!.From.Should().Be(0);
        response.Content.Headers.ContentRange.To.Should().Be(3);
        (await response.Content.ReadAsByteArrayAsync()).Should().HaveCount(4);
    }

    [Fact]
    public async Task MissingPath_Returns400()
    {
        using var client = _direct.CreateAuthenticatedClient();

        (await client.GetAsync("/api/files/stream"))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Films/nope.mp4")]
    [InlineData("../../../etc/passwd")]
    [InlineData("Films/../../etc/passwd")]
    public async Task NotAPlayableLibraryFile_Returns404(string path)
    {
        using var client = _direct.CreateAuthenticatedClient();

        (await client.GetAsync($"/api/files/stream?path={path}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/files/stream/info?path={path}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }

    // The source is 36 pixels tall, so asking for 240 must not upscale it.
    [Fact]
    public async Task WithHeight_TranscodesVideoToFragmentedMp4NoTallerThanTheSource()
    {
        TestMedia.CreateVideo(_media.Write("Films/real.mp4"), seconds: 3);
        using var client = _direct.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            "/api/files/stream?path=Films/real.mp4&start=1&height=240"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
        var body = await response.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(body, 4, 4).Should().Be("ftyp");
        var output = _media.Write("transcoded.mp4", body);
        var probe = await Ffmpeg.ProbeAsync(
            output,
            "v:0",
            NullLogger.Instance,
            CancellationToken.None
        );
        probe!.Streams!.Single().Height.Should().Be(36);
    }

    // Transcoding maps the first video stream, so audio used to come back as an empty 200.
    [Fact]
    public async Task WithHeight_ServesAudioDirectlyWithRanges()
    {
        using var client = _direct.CreateAuthenticatedClient();
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/files/stream?path=song.mp3&height=480"
        );
        request.Headers.Range = new RangeHeaderValue(0, 3);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentType!.MediaType.Should().Be("audio/mpeg");
    }

    // The stream follows its URL, not the live setting, so a settings edit cannot change a playing stream.
    [Fact]
    public async Task CompressedMode_StreamWithoutHeight_StaysDirect()
    {
        using var factory = CreateCompressedFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/files/stream?path=Films/test.mp4");

        response.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
        (await response.Content.ReadAsStringAsync()).Should().Be("fake-mp4-content");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(481)]
    public async Task InvalidHeight_Returns400(int height)
    {
        using var client = _direct.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/api/files/stream?path=Films/test.mp4&height={height}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(false, "Films/real.mp4", null)]
    [InlineData(true, "Films/real.mp4", 480)]
    [InlineData(true, "song.mp3", null)]
    public async Task StreamInfo_ReportsDurationAndTheHeightToTranscodeTo(
        bool compressed,
        string path,
        int? transcodeHeight
    )
    {
        TestMedia.CreateVideo(_media.Write("Films/real.mp4"), seconds: 3);
        using var factory = compressed ? CreateCompressedFactory() : _media.CreateFactory();
        using var client = factory.CreateAuthenticatedClient();

        var info = await client.GetFromJsonAsync<StreamInfoResponse>(
            $"/api/files/stream/info?path={path}",
            factory.JsonOptions()
        );

        info!.TranscodeHeight.Should().Be(transcodeHeight);
        if (path.EndsWith(".mp4"))
        {
            info.DurationSeconds.Should().BeApproximately(3, 0.5);
        }
    }
}
