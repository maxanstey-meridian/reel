using FluentAssertions;
using Reel.Server.Library;

namespace Reel.Server.Tests;

public sealed class NextEpisodeTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly NextEpisodeService _next;

    public NextEpisodeTests() => _next = new NextEpisodeService(_media.Paths);

    public void Dispose() => _media.Dispose();

    private void Add(params string[] names)
    {
        foreach (var name in names)
        {
            _media.Write(name);
        }
    }

    [Fact]
    public void SelectsOnlyAdjacentEpisodeInSameSeries()
    {
        Add("Show.S01E09.mkv", "Show.S01E10.mkv", "Show.S01E11.mkv", "Other.S01E10.mkv");
        _next.Find("Show.S01E09.mkv").RelativePath.Should().Be("Show.S01E10.mkv");
    }

    [Fact]
    public void DoesNotGuessWhenMissingOrAmbiguous()
    {
        Add("Show.S01E01.mkv", "Show.S01E03.mkv", "Movie.mp4");
        _next.Find("Show.S01E01.mkv").RelativePath.Should().BeNull();
        Add("Show.S01E02.mkv", "Show.S01E02.mp4");
        _next.Find("Show.S01E01.mkv").RelativePath.Should().BeNull();
        _next.Find("Movie.mp4").RelativePath.Should().BeNull();
        _next.Find("../Show.S01E01.mkv").RelativePath.Should().BeNull();
    }
}
