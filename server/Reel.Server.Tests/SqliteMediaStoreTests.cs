using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;
using Reel.Server.Domain;

namespace Reel.Server.Tests;

public sealed class SqliteMediaStoreTests : IDisposable
{
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-media-test-{Guid.NewGuid():N}");
    private readonly SqliteMediaStore _store;

    public SqliteMediaStoreTests()
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
    public async Task ReconcileAsync_CreatesMinimalMediaRecord()
    {
        await _store.ReconcileAsync(
            [new MediaRecord("Films/test.mp4", "Test", 2024)],
            CancellationToken.None
        );

        var record = await _store.GetAsync("Films/test.mp4", CancellationToken.None);

        record.Should().NotBeNull();
        record!.RelativePath.Should().Be("Films/test.mp4");
        record.ParsedTitle.Should().Be("Test");
        record.Year.Should().Be(2024);
    }

    [Fact]
    public async Task IsWatchedAsync_DerivesCompletionFromProgress()
    {
        await _store.ReconcileAsync(
            [new MediaRecord("Films/test.mp4", "Test", 2024)],
            CancellationToken.None
        );
        var progress = new SqliteProgressStore(GetDatabase());
        await progress.UpdateAsync("Films/test.mp4", 95, 100, CancellationToken.None);

        (await _store.IsWatchedAsync("Films/test.mp4", CancellationToken.None)).Should().BeTrue();
    }

    private DatabaseService GetDatabase() =>
        new(
            new ReelConfig { MediaRoot = _mediaRoot, SecretKey = "test", Port = 8080 },
            NullLogger<DatabaseService>.Instance
        );
}
