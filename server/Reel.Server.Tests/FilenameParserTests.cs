using FluentAssertions;
using Reel.Server.Library;

namespace Reel.Server.Tests;

public sealed class FilenameParserTests
{
    [Theory]
    [InlineData("The.Matrix.1999.1080p.BluRay.x264.mkv", "The Matrix", 1999)]
    [InlineData("Blade_Runner_2049_2017_UHD.mp4", "Blade Runner 2049", 2017)]
    [InlineData("Some.Movie.1080p.mkv", "Some Movie", null)]
    [InlineData("My Movie.mp4", "My Movie", null)]
    [InlineData("Film.2020.x265.HEVC.DTS.mkv", "Film", 2020)]
    [InlineData("The.Band.Live.2020.flac", "The Band Live", 2020)]
    [InlineData("Apollo.13.1995.BluRay.mkv", "Apollo 13", 1995)]
    [InlineData("Movie.2019.4K.HDR.mp4", "Movie", 2019)]
    [InlineData("Inception (2010).mkv", "Inception", 2010)]
    [InlineData("2001.A.Space.Odyssey.1968.REMUX.mkv", "2001 A Space Odyssey", 1968)]
    [InlineData(
        "No.Country.for.Old.Men.2007.x265.HEVC.10bit.AAC.mkv",
        "No Country for Old Men",
        2007
    )]
    [InlineData("The.Final.Cut.2004.mkv", "The Final Cut", 2004)]
    [InlineData("Charlotte's.Web.Directors.Cut.mkv", "Charlotte's Web Directors Cut", null)]
    [InlineData("Show [Group] - Episode.WEB-DL.mkv", "Show Episode", null)]
    public void Parses_TitleAndYear(string filename, string title, int? year)
    {
        FilenameParser.Parse(filename).Should().Be(new ParsedTitle(title, year));
    }

    [Fact]
    public void Handles_EmptyExtensionOnly()
    {
        FilenameParser.Parse(".mp4").Title.Should().NotBeNullOrWhiteSpace();
    }
}
