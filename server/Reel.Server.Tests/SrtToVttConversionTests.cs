using FluentAssertions;
using Reel.Server.Infrastructure.Ffmpeg;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;

namespace Reel.Server.Tests;

public sealed class SrtToVttConversionTests
{
    private readonly FfmpegSubtitleExtractor _extractor;

    public SrtToVttConversionTests()
    {
        var config = new ReelConfig
        {
            MediaRoot = Path.GetTempPath(),
            SecretKey = "test",
            Port = 8080,
        };
        _extractor = new FfmpegSubtitleExtractor(config, NullLogger<FfmpegSubtitleExtractor>.Instance);
    }

    [Fact]
    public void BasicSrtConversion_ProducesValidVtt()
    {
        var srt = """
            1
            00:00:01,000 --> 00:00:04,000
            Hello, world!

            2
            00:00:05,500 --> 00:00:08,300
            This is a test.
            """;

        var result = _extractor.ConvertSrtToVtt(srt);

        result.Should().StartWith("WEBVTT\n\n");
        result.Should().Contain("00:00:01.000 --> 00:00:04.000");
        result.Should().Contain("Hello, world!");
        result.Should().Contain("00:00:05.500 --> 00:00:08.300");
        result.Should().Contain("This is a test.");
        // Timestamp commas replaced with dots
        result.Should().NotContain("00:00:01,000");
        result.Should().NotContain("00:00:05,500");
    }

    [Fact]
    public void StripsBom()
    {
        var srt = "\uFEFF1\n00:00:01,000 --> 00:00:02,000\nHello\n";

        var result = _extractor.ConvertSrtToVtt(srt);

        result.Should().StartWith("WEBVTT");
        result.Should().NotStartWith("\uFEFF");
    }

    [Fact]
    public void HandlesWindowsLineEndings()
    {
        var srt = "1\r\n00:00:01,000 --> 00:00:02,000\r\nHello\r\n\r\n2\r\n00:00:03,000 --> 00:00:04,000\r\nWorld\r\n";

        var result = _extractor.ConvertSrtToVtt(srt);

        result.Should().StartWith("WEBVTT\n\n");
        result.Should().Contain("00:00:01.000 --> 00:00:02.000");
        result.Should().Contain("Hello");
        result.Should().Contain("00:00:03.000 --> 00:00:04.000");
        result.Should().Contain("World");
    }

    [Fact]
    public void StripsSequenceNumbers()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\nFirst\n\n2\n00:00:03,000 --> 00:00:04,000\nSecond\n";

        var result = _extractor.ConvertSrtToVtt(srt);

        // Sequence numbers should not appear in output
        result.Should().NotMatchRegex(@"^1$", "because sequence numbers should be stripped");
        // But the text and timestamps should be present
        result.Should().Contain("First");
        result.Should().Contain("Second");
    }

    [Fact]
    public void PreservesMultilineSubtitleText()
    {
        var srt = "1\n00:00:01,000 --> 00:00:04,000\nLine one\nLine two\n";

        var result = _extractor.ConvertSrtToVtt(srt);

        result.Should().Contain("Line one\n");
        result.Should().Contain("Line two\n");
    }

    [Fact]
    public void EmptyInput_ProducesHeaderOnly()
    {
        var result = _extractor.ConvertSrtToVtt("");

        result.Should().Be("WEBVTT\n\n");
    }

    [Fact]
    public void HandlesMixedLineEndings()
    {
        var srt = "1\r00:00:01,000 --> 00:00:02,000\rHello\r\r";

        var result = _extractor.ConvertSrtToVtt(srt);

        result.Should().StartWith("WEBVTT\n\n");
        result.Should().Contain("Hello");
    }
}
