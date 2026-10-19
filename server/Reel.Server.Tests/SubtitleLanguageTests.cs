using FluentAssertions;
using Reel.Server.Media;

namespace Reel.Server.Tests;

public sealed class SubtitleLanguageTests
{
    [Theory]
    [InlineData("eng", "en")]
    [InlineData("en", "en")]
    [InlineData("English", "en")]
    [InlineData("en-US", "en")]
    [InlineData("7_English", "en")]
    [InlineData("1000_en-US", "en")]
    [InlineData("en.forced", "en")]
    [InlineData("ger", "de")]
    [InlineData("fre", "fr")]
    [InlineData("1007_es-419", "es")]
    [InlineData("1025_sr-Latn-RS", "sr")]
    [InlineData("", null)]
    [InlineData("forced", null)]
    [InlineData(null, null)]
    public void FromTag_ReadsCodesNamesAndNumberedFileNames(string? tag, string? language) =>
        SubtitleLanguage.FromTag(tag).Should().Be(language);

    // Labels are free text, so only a language spelled out in full counts; "is" and "it" are words, not codes.
    [Theory]
    [InlineData("English (SDH)", "en")]
    [InlineData("Forced - Spanish", "es")]
    [InlineData("This is it", null)]
    [InlineData("Track 1", null)]
    public void FromLabel_ReadsOnlyFullLanguageNames(string label, string? language) =>
        SubtitleLanguage.FromLabel(label).Should().Be(language);

    [Fact]
    public void Match_PrefersATextTrackThenAFullOneThenTheFirst()
    {
        Subtitle[] tracks =
        [
            new(0, "English", "eng", "hdmv_pgs_subtitle", null),
            new(1, "English (Forced)", "eng", "subrip", null),
            new(2, "Track 3", "fre", "subrip", null),
            new(3, "English", "eng", "subrip", null),
            new(4, "External SRT", "en", "srt", "Films/movie.en.srt"),
        ];

        SubtitleLanguage.Match(tracks, "en")!.Index.Should().Be(3);
        SubtitleLanguage.Match(tracks, "fr")!.Index.Should().Be(2);
    }

    [Fact]
    public void Match_FallsBackToASidecarThatNamesNoLanguage()
    {
        Subtitle[] tracks =
        [
            new(0, "Track 1", "fre", "subrip", null),
            new(1, "External SRT", "", "srt", "Shows/Show.S01E01.srt"),
        ];

        SubtitleLanguage.Match(tracks, "en")!.Index.Should().Be(1);
        SubtitleLanguage.Match([tracks[0]], "en").Should().BeNull();
    }
}
