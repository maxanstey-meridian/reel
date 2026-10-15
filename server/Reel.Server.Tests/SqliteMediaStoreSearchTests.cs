using Dapper;
using FluentAssertions;
using Reel.Server.Storage;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreSearchTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly SqliteMediaStore _store;

    public SqliteMediaStoreSearchTests() => _store = _media.MediaStore;

    public void Dispose() => _media.Dispose();

    private void Tag(string sql)
    {
        using var connection = _media.Database.GetConnection();
        connection.Execute(sql);
    }

    [Theory]
    [InlineData("comedy")]
    [InlineData("#COMEDY")]
    public async Task SearchMatchesFileAndFolderTags(string query)
    {
        await _store.ReconcileAsync(
            [
                new MediaRecord("Shows/Season/a.mkv", "A", null, null),
                new MediaRecord("Films/b.mp4", "B", null, null),
                new MediaRecord("ShowsExtra/c.mp4", "C", null, null),
            ],
            DateTime.UtcNow
        );
        Tag(
            """INSERT INTO item_tags VALUES ('Shows', '["comedy"]'), ('Films/b.mp4', '["comedy"]');"""
        );

        var results = await _store.SearchAsync(query);
        results
            .Select(item => item.RelativePath)
            .Should()
            .BeEquivalentTo("Shows/Season/a.mkv", "Films/b.mp4");
    }

    [Fact]
    public async Task DefaultMediaTagIsSearchableButRemovedTagStaysRemoved()
    {
        await _store.ReconcileAsync(
            [new MediaRecord("a.mp4", "A", null, null), new MediaRecord("b.mp4", "B", null, null)],
            DateTime.UtcNow
        );
        Tag("INSERT INTO item_tags VALUES ('b.mp4', '[]')");

        var results = await _store.SearchAsync("#media");
        results.Should().ContainSingle().Which.RelativePath.Should().Be("a.mp4");
    }

    [Theory]
    [InlineData("me")]
    [InlineData("media")]
    public async Task PlainSearch_DoesNotTreatUntaggedMediaAsMatchingTheMediaPseudoTag(string query)
    {
        await _store.ReconcileAsync(
            [
                new MediaRecord("Films/alpha.mp4", "Alpha", null, null),
                new MediaRecord("Films/bravo.mp4", "Bravo", null, null),
            ],
            DateTime.UtcNow
        );

        var results = await _store.SearchAsync(query);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task TagSearch_MatchesMediaPseudoTagByPrefix()
    {
        await _store.ReconcileAsync(
            [
                new MediaRecord("Films/one.mp4", "One", null, null),
                new MediaRecord("Films/two.mp4", "Two", null, null),
            ],
            DateTime.UtcNow
        );

        var results = await _store.SearchAsync("#med");

        results
            .Select(item => item.RelativePath)
            .Should()
            .BeEquivalentTo("Films/one.mp4", "Films/two.mp4");
    }

    [Fact]
    public async Task SearchAsync_MatchesTitleCaseInsensitively()
    {
        await _store.ReconcileAsync(
            [
                new MediaRecord("Films/inception.mp4", "Inception", 2010, null),
                new MediaRecord("Films/matrix.mp4", "The Matrix", 1999, null),
            ],
            DateTime.UtcNow
        );

        var results = await _store.SearchAsync("INCEP");

        results.Should().ContainSingle().Which.RelativePath.Should().Be("Films/inception.mp4");
    }

    [Theory]
    [InlineData("_", "Films/a_b.mp4")]
    [InlineData("%", "Films/100%.mp4")]
    [InlineData(@"\", @"Films/c\d.mp4")]
    public async Task SearchAsync_TreatsLikeWildcardsAsText(string query, string expected)
    {
        await _store.ReconcileAsync(
            [
                new MediaRecord("Films/a_b.mp4", null, null, null),
                new MediaRecord("Films/100%.mp4", null, null, null),
                new MediaRecord(@"Films/c\d.mp4", null, null, null),
                new MediaRecord("Films/plain.mp4", null, null, null),
            ],
            DateTime.UtcNow
        );

        var results = await _store.SearchAsync(query);

        results.Should().ContainSingle().Which.RelativePath.Should().Be(expected);
    }

    [Fact]
    public async Task SearchAsync_MatchesRelativePath()
    {
        await _store.ReconcileAsync(
            [new MediaRecord("Documentaries/space.mp4", "Unknown", null, null)],
            DateTime.UtcNow
        );

        var results = await _store.SearchAsync("space");

        results.Should().ContainSingle().Which.RelativePath.Should().Be("Documentaries/space.mp4");
    }

    [Fact]
    public async Task SearchAsync_EmptyQueryReturnsAllMedia()
    {
        await _store.ReconcileAsync(
            [
                new MediaRecord("Films/a.mp4", "Film A", 2020, null),
                new MediaRecord("Films/b.mp4", "Film B", 2021, null),
            ],
            DateTime.UtcNow
        );

        var results = await _store.SearchAsync(null);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNaturalOrder()
    {
        await _store.ReconcileAsync(
            [
                new MediaRecord("Show/Episode 10.mkv", null, null, null),
                new MediaRecord("Show/Episode 2.mkv", null, null, null),
            ],
            DateTime.UtcNow
        );

        var results = await _store.SearchAsync(null);

        results
            .Select(item => item.RelativePath)
            .Should()
            .Equal("Show/Episode 2.mkv", "Show/Episode 10.mkv");
    }
}
