using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Application.Models;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;
using Reel.Server.Domain;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreSearchTests : IDisposable
{
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-search-test-{Guid.NewGuid():N}");
    private readonly SqliteMediaStore _store;

    public SqliteMediaStoreSearchTests()
    {
        Directory.CreateDirectory(_mediaRoot);
        var database = new DatabaseService(
            new ReelConfig { MediaRoot = _mediaRoot, SecretKey = "test", Port = 8080 },
            NullLogger<DatabaseService>.Instance
        );
        _store = new SqliteMediaStore(database);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, true);
    }

    [Fact]
    public async Task SearchAsync_MatchesTitleCaseInsensitively()
    {
        await _store.ReconcileAsync(
            [
                new MediaRecord("Films/inception.mp4", "Inception", 2010),
                new MediaRecord("Films/matrix.mp4", "The Matrix", 1999),
            ],
            CancellationToken.None
        );

        var results = await _store.SearchAsync(new SearchQuery("INCEP"), CancellationToken.None);

        results.Should().ContainSingle().Which.RelativePath.Should().Be("Films/inception.mp4");
    }

    [Fact]
    public async Task SearchAsync_MatchesRelativePath()
    {
        await _store.ReconcileAsync(
            [new MediaRecord("Documentaries/space.mp4", "Unknown", null)],
            CancellationToken.None
        );

        var results = await _store.SearchAsync(new SearchQuery("space"), CancellationToken.None);

        results.Should().ContainSingle().Which.RelativePath.Should().Be("Documentaries/space.mp4");
    }

    [Fact]
    public async Task SearchAsync_EmptyQueryReturnsAllMedia()
    {
        await _store.ReconcileAsync(
            [new MediaRecord("Films/a.mp4", "Film A", 2020), new MediaRecord("Films/b.mp4", "Film B", 2021)],
            CancellationToken.None
        );

        var results = await _store.SearchAsync(new SearchQuery(null), CancellationToken.None);

        results.Should().HaveCount(2);
    }
}
