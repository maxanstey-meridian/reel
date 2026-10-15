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
                new Subtitle(0, "en", "en", "ass", "Films/movie.en.ass"),
                new Subtitle(1, "fr", "fr", "vtt", "Films/movie.fr.vtt"),
                new Subtitle(2, "movie.srt", "", "srt", "Films/movie.srt")
            );
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
                new Subtitle(0, "", "", "subrip", null),
                new Subtitle(1, "real.srt", "", "srt", "Films/real.srt")
            );
    }
}
