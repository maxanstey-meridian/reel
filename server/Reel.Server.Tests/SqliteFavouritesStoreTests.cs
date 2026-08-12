using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class SqliteFavouritesStoreTests : IDisposable
{
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-favourites-test-{Guid.NewGuid():N}");
    private readonly SqliteFavouritesStore _store;
    private readonly SqliteMediaStore _mediaStore;

    public SqliteFavouritesStoreTests()
    {
        Directory.CreateDirectory(_mediaRoot);
        var database = new DatabaseService(
            new ReelConfig { MediaRoot = _mediaRoot, SecretKey = "test", Port = 8080 },
            NullLogger<DatabaseService>.Instance
        );
        _store = new SqliteFavouritesStore(database);
        _mediaStore = new SqliteMediaStore(database);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, true);
    }

    [Fact]
    public async Task AddAsync_IsIdempotentAndKeyedByPath()
    {
        await SeedMedia("Films/favourite.mp4");
        await _store.AddAsync("Films/favourite.mp4", CancellationToken.None);
        await _store.AddAsync("Films/favourite.mp4", CancellationToken.None);

        var favourites = await _store.GetAllAsync(CancellationToken.None);

        favourites.Should().ContainSingle().Which.RelativePath.Should().Be("Films/favourite.mp4");
        (await _store.IsFavouriteAsync("Films/favourite.mp4", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_DeletesFavourite()
    {
        await SeedMedia("Films/favourite.mp4");
        await _store.AddAsync("Films/favourite.mp4", CancellationToken.None);
        await _store.RemoveAsync("Films/favourite.mp4", CancellationToken.None);

        (await _store.IsFavouriteAsync("Films/favourite.mp4", CancellationToken.None)).Should().BeFalse();
    }

    private Task SeedMedia(string path) =>
        _mediaStore.ReconcileAsync([new Reel.Server.Domain.MediaRecord(path, null, null)], CancellationToken.None);
}
