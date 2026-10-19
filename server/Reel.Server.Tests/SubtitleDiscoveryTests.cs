using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Media;

namespace Reel.Server.Tests;

public sealed class SubtitleDiscoveryTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly FfprobeSubtitleDiscovery _discovery;
    private readonly string _movie;

    public SubtitleDiscoveryTests()
    {
        _discovery = new FfprobeSubtitleDiscovery(
            _media.Paths,
            NullLogger<FfprobeSubtitleDiscovery>.Instance
        );
        _movie = _media.Write("Films/movie.mp4", new byte[100]);
    }

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task UnreadableMedia_StillListsMatchingSidecarsInNameOrder()
    {
        _media.Write("Films/movie.srt");
        _media.Write("Films/movie.fr.vtt");
        _media.Write("Films/movie.en.ass");
        _media.Write("Films/other-movie.srt");
        _media.Write("Films/movieextras.srt");
        _media.Write("Films/.movie.de.srt");

        var tracks = await _discovery.DiscoverAsync(_movie, CancellationToken.None);

        tracks
            .Should()
            .Equal(
                new Subtitle(0, "External ASS", "en", "ass", "Films/movie.en.ass"),
                new Subtitle(1, "External VTT", "fr", "vtt", "Films/movie.fr.vtt"),
                new Subtitle(2, "External SRT", "", "srt", "Films/movie.srt")
            );
    }

    // RARBG puts a show's subtitles in Subs/{episode}/ and a film's in Subs/, numbered and named by language;
    // both come after the sidecars beside the file, together in name order.
    [Fact]
    public async Task SubsFolderSidecarsFollowTheOnesBesideTheFileAndKeepTheirNameAsLanguage()
    {
        _media.Write("Films/movie.en.srt");
        _media.Write("Films/Subs/movie/1000_en-US.srt");
        _media.Write("Films/Subs/movie/7_English.srt");
        _media.Write("Films/Subs/other/7_English.srt");
        _media.Write("Films/Subs/2_French.srt");

        var tracks = await _discovery.DiscoverAsync(_movie, CancellationToken.None);

        tracks
            .Should()
            .Equal(
                new Subtitle(0, "External SRT", "en", "srt", "Films/movie.en.srt"),
                new Subtitle(1, "External SRT", "2_French", "srt", "Films/Subs/2_French.srt"),
                new Subtitle(
                    2,
                    "External SRT",
                    "7_English",
                    "srt",
                    "Films/Subs/movie/7_English.srt"
                ),
                new Subtitle(
                    3,
                    "External SRT",
                    "1000_en-US",
                    "srt",
                    "Films/Subs/movie/1000_en-US.srt"
                )
            );
    }

    // In a season folder the loose Subs/ files are named after their episodes; one named by language alone could
    // be any episode's, so none takes it.
    [Fact]
    public async Task LooseSubsBesideSeveralVideos_GoOnlyToTheEpisodeTheyAreNamedAfter()
    {
        var episode = _media.Write("Show/Show.S01E01.mkv", new byte[100]);
        _media.Write("Show/Show.S01E02.mkv", new byte[100]);
        _media.Write("Show/Subs/Show.S01E01.en.srt");
        _media.Write("Show/Subs/Show.S01E02.en.srt");
        _media.Write("Show/Subs/2_English.srt");

        var tracks = await _discovery.DiscoverAsync(episode, CancellationToken.None);

        tracks
            .Should()
            .Equal(new Subtitle(0, "External SRT", "en", "srt", "Show/Subs/Show.S01E01.en.srt"));
    }

    [Fact]
    public async Task LinkedSubsFolder_IsNotRead()
    {
        var outside = Directory.CreateTempSubdirectory("reel-outside-");
        try
        {
            Directory.CreateDirectory(Path.Join(outside.FullName, "movie"));
            File.WriteAllText(Path.Join(outside.FullName, "movie", "7_English.srt"), "");
            File.WriteAllText(Path.Join(outside.FullName, "2_French.srt"), "");
            Directory.CreateSymbolicLink(Path.Join(_media.Root, "Films", "Subs"), outside.FullName);

            var tracks = await _discovery.DiscoverAsync(_movie, CancellationToken.None);

            tracks.Should().BeEmpty();
        }
        finally
        {
            outside.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task SidecarsAreNumberedAfterEmbeddedTracks()
    {
        var movie = _media.Write("Films/real.mkv");
        TestMedia.CreateVideoWithSubtitle(movie, "Embedded");
        _media.Write("Films/real.srt");

        var tracks = await _discovery.DiscoverAsync(movie, CancellationToken.None);

        tracks
            .Should()
            .Equal(
                new Subtitle(0, "Track 1", "", "subrip", null),
                new Subtitle(1, "External SRT", "", "srt", "Films/real.srt")
            );
    }

    [Theory]
    [InlineData("Commentary", "eng", "Commentary")]
    [InlineData(null, "eng", "Track 1")]
    public async Task EmbeddedTrackLabel_IsItsTitleElseItsTrackNumber(
        string? title,
        string language,
        string label
    )
    {
        var movie = _media.Write("Films/tagged.mkv");
        TestMedia.CreateVideoWithSubtitle(
            movie,
            "Embedded",
            [$"language={language}", .. title is null ? Array.Empty<string>() : [$"title={title}"]]
        );

        var tracks = await _discovery.DiscoverAsync(movie, CancellationToken.None);

        tracks.Should().ContainSingle().Which.Label.Should().Be(label);
    }
}
