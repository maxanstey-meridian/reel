using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreSubtitleTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly SqliteMediaStore _store;

    public SqliteMediaStoreSubtitleTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-sub-store-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080,
        };

        var db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _store = new SqliteMediaStore(db);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
    }

    [Fact]
    public async Task SaveAndGetSubtitles_Roundtrip()
    {
        await IndexAsync("Films/movie.mp4");
        var tracks = new List<SubtitleTrack>
        {
            new(0, "English", "eng", "subrip", "embedded", null),
            new(1, "French", "fre", "ass", "embedded", null),
            new(2, "movie.en.srt", "en", "srt", "external", "Films/movie.en.srt"),
        };

        await _store.SaveSubtitlesAsync("Films/movie.mp4", tracks, CancellationToken.None);

        var result = await _store.GetSubtitlesAsync("Films/movie.mp4", CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Index.Should().Be(0);
        result[0].Label.Should().Be("English");
        result[0].Language.Should().Be("eng");
        result[0].Format.Should().Be("subrip");
        result[0].Source.Should().Be("embedded");
        result[0].ExternalPath.Should().BeNull();

        result[2].Index.Should().Be(2);
        result[2].Source.Should().Be("external");
        result[2].ExternalPath.Should().Be("Films/movie.en.srt");
    }

    [Fact]
    public async Task SaveSubtitles_ReplacesExisting()
    {
        await IndexAsync("Films/movie.mp4");
        var initial = new List<SubtitleTrack>
        {
            new(0, "English", "eng", "subrip", "embedded", null),
            new(1, "French", "fre", "ass", "embedded", null),
        };

        await _store.SaveSubtitlesAsync("Films/movie.mp4", initial, CancellationToken.None);

        var replacement = new List<SubtitleTrack>
        {
            new(0, "Spanish", "spa", "subrip", "embedded", null),
        };

        await _store.SaveSubtitlesAsync("Films/movie.mp4", replacement, CancellationToken.None);

        var result = await _store.GetSubtitlesAsync("Films/movie.mp4", CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Label.Should().Be("Spanish");
    }

    [Fact]
    public async Task GetSubtitles_NonexistentMedia_ReturnsEmpty()
    {
        var result = await _store.GetSubtitlesAsync("Films/nonexistent.mp4", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveSubtitles_RequiresIndexedMedia()
    {
        var tracks = new List<SubtitleTrack>
        {
            new(0, "English", "eng", "subrip", "embedded", null),
        };

        var action = () => _store.SaveSubtitlesAsync("Films/new-movie.mp4", tracks, CancellationToken.None);
        await action.Should().ThrowAsync<Microsoft.Data.Sqlite.SqliteException>();
    }

    [Fact]
    public async Task SaveSubtitles_EmptyList_ClearsExisting()
    {
        await IndexAsync("Films/movie.mp4");
        var tracks = new List<SubtitleTrack>
        {
            new(0, "English", "eng", "subrip", "embedded", null),
        };

        await _store.SaveSubtitlesAsync("Films/movie.mp4", tracks, CancellationToken.None);
        await _store.SaveSubtitlesAsync("Films/movie.mp4", [], CancellationToken.None);

        var result = await _store.GetSubtitlesAsync("Films/movie.mp4", CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DifferentMedia_IsolatedSubtitles()
    {
        await _store.ReconcileAsync(
            [new MediaRecord("Films/movie1.mp4", null, null), new MediaRecord("Films/movie2.mp4", null, null)],
            CancellationToken.None
        );
        await _store.SaveSubtitlesAsync(
            "Films/movie1.mp4",
            [new SubtitleTrack(0, "English", "eng", "subrip", "embedded", null)],
            CancellationToken.None
        );

        await _store.SaveSubtitlesAsync(
            "Films/movie2.mp4",
            [new SubtitleTrack(0, "French", "fre", "ass", "embedded", null)],
            CancellationToken.None
        );

        var result1 = await _store.GetSubtitlesAsync("Films/movie1.mp4", CancellationToken.None);
        var result2 = await _store.GetSubtitlesAsync("Films/movie2.mp4", CancellationToken.None);

        result1.Should().HaveCount(1);
        result1[0].Language.Should().Be("eng");

        result2.Should().HaveCount(1);
        result2[0].Language.Should().Be("fre");
    }

    private Task IndexAsync(string path) =>
        _store.ReconcileAsync([new MediaRecord(path, null, null)], CancellationToken.None);
}
