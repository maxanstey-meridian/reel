using FluentAssertions;
using Reel.Server.Application;

namespace Reel.Server.Tests;

public sealed class FilenameParserTests
{
    [Fact]
    public void Parses_DottedFilename_WithYear()
    {
        var result = FilenameParser.Parse("The.Matrix.1999.1080p.BluRay.x264.mkv");

        result.Title.Should().Be("The Matrix");
        result.Year.Should().Be(1999);
    }

    [Fact]
    public void Parses_UnderscoredFilename()
    {
        var result = FilenameParser.Parse("Blade_Runner_2049_2017_UHD.mp4");

        result.Title.Should().Be("Blade Runner 2049");
        result.Year.Should().Be(2017);
    }

    [Fact]
    public void Handles_NoYear()
    {
        var result = FilenameParser.Parse("Some.Movie.1080p.mkv");

        result.Title.Should().Be("Some Movie");
        result.Year.Should().BeNull();
    }

    [Fact]
    public void Handles_CleanFilename()
    {
        var result = FilenameParser.Parse("My Movie.mp4");

        result.Title.Should().Be("My Movie");
        result.Year.Should().BeNull();
    }

    [Fact]
    public void Strips_MultipleCodecTags()
    {
        var result = FilenameParser.Parse("Film.2020.x265.HEVC.DTS.mkv");

        result.Title.Should().Be("Film");
        result.Year.Should().Be(2020);
    }

    [Fact]
    public void Handles_NoExtension()
    {
        var result = FilenameParser.Parse("The.Movie.2020");

        result.Title.Should().Be("The Movie");
        result.Year.Should().Be(2020);
    }

    [Fact]
    public void Preserves_NumbersInTitle()
    {
        var result = FilenameParser.Parse("Apollo.13.1995.BluRay.mkv");

        result.Title.Should().Be("Apollo 13");
        result.Year.Should().Be(1995);
    }

    [Fact]
    public void Handles_4K_Tag()
    {
        var result = FilenameParser.Parse("Movie.2019.4K.HDR.mp4");

        result.Title.Should().Be("Movie");
        result.Year.Should().Be(2019);
    }

    [Fact]
    public void Handles_ParenthesizedYear()
    {
        var result = FilenameParser.Parse("Inception (2010).mkv");

        result.Title.Should().Be("Inception");
        result.Year.Should().Be(2010);
    }

    [Fact]
    public void Handles_2001_SpaceOdyssey()
    {
        var result = FilenameParser.Parse("2001.A.Space.Odyssey.1968.REMUX.mkv");

        result.Title.Should().Be("2001 A Space Odyssey");
        result.Year.Should().Be(1968);
    }

    [Fact]
    public void Handles_EmptyExtensionOnly()
    {
        var result = FilenameParser.Parse(".mp4");

        result.Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Strips_ComplexTags()
    {
        var result = FilenameParser.Parse("No.Country.for.Old.Men.2007.x265.HEVC.10bit.AAC.mkv");

        result.Title.Should().Be("No Country for Old Men");
        result.Year.Should().Be(2007);
    }
}
