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
    private readonly WebApplicationFactory<Program> _factory;

    public StreamEndpointTests()
    {
        _media.Write("Films/test.mp4", "fake-mp4-content"u8.ToArray());
        _media.Write("Films/movie.mkv", "fake-mkv-content"u8.ToArray());
        _media.Write("song.mp3", "fake-mp3-content"u8.ToArray());
        _media.Write("track.ogg", "fake-ogg-content"u8.ToArray());
        _factory = _media.CreateFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
        _media.Dispose();
    }

    [Theory]
    [InlineData("Films/test.mp4&start=30", "video/mp4", "fake-mp4-content")]
    [InlineData("Films/movie.mkv", "video/x-matroska", "fake-mkv-content")]
    [InlineData("song.mp3", "audio/mpeg", "fake-mp3-content")]
    [InlineData("track.ogg", "audio/ogg", "fake-ogg-content")]
    public async Task DirectMode_ServesTheFileWithItsContentTypeIgnoringStart(
        string query,
        string type,
        string body
    )
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/files/stream?path={query}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(type);
        response.Headers.AcceptRanges.Should().Contain("bytes");
        (await response.Content.ReadAsStringAsync()).Should().Be(body);
    }

    [Fact]
    public async Task DirectMode_RangeRequest_Returns206WithContentRange()
    {
        using var client = _factory.CreateAuthenticatedClient();
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
        using var client = _factory.CreateAuthenticatedClient();

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
        using var client = _factory.CreateAuthenticatedClient();

        (await client.GetAsync($"/api/files/stream?path={path}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/files/stream/info?path={path}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }

    // The source is 36 pixels tall, so asking for 240 must not upscale it.
    [Fact]
    public async Task TranscodeMode_TranscodesVideoToFragmentedMp4NoTallerThanTheSource()
    {
        TestMedia.CreateVideo(_media.Write("Films/real.mp4"), seconds: 3);
        using var client = _factory.CreateAuthenticatedClient();

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
    public async Task TranscodeMode_ServesAudioDirectlyWithRanges()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/files/stream?path=song.mp3&height=480"
        );
        request.Headers.Range = new RangeHeaderValue(0, 3);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentType!.MediaType.Should().Be("audio/mpeg");
    }

    [Theory]
    [InlineData("height=0")]
    [InlineData("height=481")]
    [InlineData("remux=true&height=480")]
    public async Task InvalidHeightOrRemuxWithHeight_Returns400(string query)
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/files/stream?path=Films/test.mp4&{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // The video is copied untouched. The TV plays MP3 once it's out of AVI, so it is copied too; MP2 becomes AC3.
    [Theory]
    [InlineData("Films/old.avi", "libmp3lame", "mp3")]
    [InlineData("Films/mp2.mkv", "mp2", "ac3")]
    public async Task RemuxMode_CopiesTheVideoAndConvertsOnlyAudioTheTvCannotPlay(
        string path,
        string sourceAudio,
        string streamedAudio
    )
    {
        TestMedia.CreateVideo(_media.Write(path), audioCodec: sourceAudio);
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/files/stream?path={path}&start=1&remux=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
        var output = _media.Write("remuxed.mp4", await response.Content.ReadAsByteArrayAsync());
        var probe = await Ffmpeg.ProbeAsync(
            output,
            streams: null,
            NullLogger.Instance,
            CancellationToken.None
        );
        probe!
            .Streams!.Select(stream => (stream.CodecType, stream.CodecName, stream.Height))
            .Should()
            .Equal(("video", "mpeg4", 36), ("audio", streamedAudio, null));
    }

    // The source is 36 pixels tall: a cap at or above that leaves it alone, one below transcodes down to the cap.
    // Audio, and a file ffprobe can't read (the fake test.mp4), are served as they are whatever the cap.
    [Theory]
    [InlineData("Films/real.mp4", StreamMode.Direct, null)]
    [InlineData("Films/real.mp4&maxHeight=480", StreamMode.Direct, null)]
    [InlineData("Films/real.mp4&maxHeight=24", StreamMode.Transcode, 24)]
    [InlineData("song.mp3&maxHeight=24", StreamMode.Direct, null)]
    [InlineData("Films/test.mp4&maxHeight=24", StreamMode.Direct, null)]
    public async Task StreamInfo_ReportsDurationAndHowToStream(
        string query,
        StreamMode mode,
        int? transcodeHeight
    )
    {
        TestMedia.CreateVideo(_media.Write("Films/real.mp4"), seconds: 3);
        using var client = _factory.CreateAuthenticatedClient();

        var info = await client.GetFromJsonAsync<StreamInfoResponse>(
            $"/api/files/stream/info?path={query}",
            _factory.JsonOptions()
        );

        info!.Mode.Should().Be(mode);
        info.TranscodeHeight.Should().Be(transcodeHeight);
        if (query.StartsWith("Films/real"))
        {
            info.DurationSeconds.Should().BeApproximately(3, 0.5);
        }
        else
        {
            info.DurationSeconds.Should().BeNull();
        }
    }

    // Without a cap only what the TV cannot play is touched: MPEG-4 Part 2 and MP3 are fine in MKV, AVI is
    // remuxed, MP2 audio is remuxed, and WMV and H.264 other than 8-bit 4:2:0 are transcoded at their own height.
    [Theory]
    [InlineData("Films/fine.mkv", "mpeg4", "libmp3lame", StreamMode.Direct, null)]
    [InlineData("Films/old.avi", "mpeg4", "libmp3lame", StreamMode.Remux, null)]
    [InlineData("Films/mp2.mkv", "mpeg4", "mp2", StreamMode.Remux, null)]
    [InlineData("Films/wmv.mkv", "wmv2", "libmp3lame", StreamMode.Transcode, 36)]
    [InlineData("Films/h264.mkv", "libx264", "libmp3lame", StreamMode.Direct, null, "yuv420p")]
    [InlineData("Films/hi10.mkv", "libx264", "libmp3lame", StreamMode.Transcode, 36, "yuv420p10le")]
    [InlineData("Films/422.mkv", "libx264", "libmp3lame", StreamMode.Transcode, 36, "yuv422p")]
    public async Task StreamInfo_TouchesOnlyWhatTheTvCannotPlay(
        string path,
        string videoCodec,
        string audioCodec,
        StreamMode mode,
        int? transcodeHeight,
        string? pixelFormat = null
    )
    {
        TestMedia.CreateVideo(
            _media.Write(path),
            videoCodec: videoCodec,
            audioCodec: audioCodec,
            pixelFormat: pixelFormat
        );
        using var client = _factory.CreateAuthenticatedClient();

        var info = await client.GetFromJsonAsync<StreamInfoResponse>(
            $"/api/files/stream/info?path={path}",
            _factory.JsonOptions()
        );

        info!.Mode.Should().Be(mode);
        info.TranscodeHeight.Should().Be(transcodeHeight);
    }

    [Theory]
    [InlineData("stream/info")]
    [InlineData("subtitles")]
    public async Task AMaxHeightThatIsNotAPositiveEvenNumber_IsRejected(string route)
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/api/files/{route}?path=Films/test.mp4&maxHeight=721"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Cover art is a picture, not the video: a file with only cover art is never transcoded, and when its audio needs
    // converting it becomes an audio-only stream.
    [Theory]
    [InlineData("Films/cover.mp4", "aac", "&maxHeight=480", StreamMode.Direct)]
    [InlineData("Films/cover-alac.mp4", "alac", "", StreamMode.Remux)]
    public async Task CoverArt_IsNotTheVideo(
        string path,
        string audioCodec,
        string cap,
        StreamMode mode
    )
    {
        TestMedia.CreateAudioWithCoverArt(_media.Write(path), audioCodec);
        using var client = _factory.CreateAuthenticatedClient();

        var info = await client.GetFromJsonAsync<StreamInfoResponse>(
            $"/api/files/stream/info?path={path}{cap}",
            _factory.JsonOptions()
        );
        info!.Mode.Should().Be(mode);
        if (mode == StreamMode.Remux)
        {
            var response = await client.GetAsync($"/api/files/stream?path={path}&remux=true");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
        }
    }

    [Theory]
    [InlineData(StreamMode.Direct, "hdmv_pgs_subtitle", true)]
    [InlineData(StreamMode.Remux, "hdmv_pgs_subtitle", false)]
    [InlineData(StreamMode.Transcode, "dvd_subtitle", false)]
    [InlineData(StreamMode.Transcode, "subrip", true)]
    public void ALiveStreamCarriesOnlyTextSubtitles(StreamMode mode, string format, bool carried) =>
        Transcoding
            .Carries(mode, new Subtitle(0, "Track 1", "eng", format, null))
            .Should()
            .Be(carried);
}
