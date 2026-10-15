using FluentAssertions;
using Reel.Server.Library;
using Reel.Server.Storage;

namespace Reel.Server.Tests;

public sealed class LibraryIndexReconcilerTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly string _mediaRoot;
    private readonly SqliteMediaStore _mediaStore;
    private readonly LibraryIndexReconciler _reconciler;

    public LibraryIndexReconcilerTests()
    {
        _mediaRoot = _media.Root;
        _mediaStore = _media.MediaStore;
        _reconciler = _media.Reconciler;
    }

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task Reconcile_IndexesPlayableFilesWithParsedTitles()
    {
        File.WriteAllBytes(
            Path.Combine(_mediaRoot, "The.Matrix.1999.1080p.BluRay.mkv"),
            new byte[1024]
        );
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "Films"));
        File.WriteAllBytes(Path.Combine(_mediaRoot, "Films", "Inception.2010.mp4"), new byte[256]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "readme.txt"), new byte[100]);

        var outcome = await _reconciler.ReconcileAsync(CancellationToken.None);

        outcome.Should().Be(new ReconcileOutcome.Indexed(2));
        var indexed = await IndexedAsync();
        indexed
            .Should()
            .BeEquivalentTo([
                new MediaRecord("Films/Inception.2010.mp4", "Inception", 2010, 256),
                new MediaRecord("The.Matrix.1999.1080p.BluRay.mkv", "The Matrix", 1999, 1024),
            ]);
    }

    [Fact]
    public async Task Reconcile_EmptyLibraryWithEmptyIndex_IndexesNothing()
    {
        (await _reconciler.ReconcileAsync(CancellationToken.None))
            .Should()
            .Be(new ReconcileOutcome.Indexed(0));

        (await IndexedAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Reconcile_RefreshesScannerOwnedMetadata()
    {
        await _mediaStore.ReconcileAsync(
            [new MediaRecord("movie.mp4", "Custom Title", 2020, null)],
            DateTime.UtcNow
        );
        File.WriteAllBytes(Path.Combine(_mediaRoot, "movie.mp4"), new byte[100]);

        await _reconciler.ReconcileAsync(CancellationToken.None);

        (await IndexedAsync()).Should().Equal(new MediaRecord("movie.mp4", "movie", null, 100));
    }

    [Fact]
    public async Task Reconcile_RemovesMissingMediaAndItsProgress()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "kept.mp4"), [1]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "gone.mp4"), [1]);
        await _reconciler.ReconcileAsync(CancellationToken.None);
        var progress = _media.ProgressStore;
        await progress.UpdateAsync("gone.mp4", 10, 100);
        File.Delete(Path.Combine(_mediaRoot, "gone.mp4"));

        await _reconciler.ReconcileAsync(CancellationToken.None);

        (await IndexedAsync()).Select(record => record.RelativePath).Should().Equal("kept.mp4");
        (await progress.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task IncompleteScan_PreservesMediaProgressAndFavouritesWithoutPartialAdditions()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "existing.mp4"), [1]);
        await _reconciler.ReconcileAsync(CancellationToken.None);
        var progress = _media.ProgressStore;
        var favourites = _media.FavouritesStore;
        await progress.UpdateAsync("existing.mp4", 10, 100);
        await favourites.AddAsync("existing.mp4");

        File.WriteAllBytes(Path.Combine(_mediaRoot, "partial.mp4"), [1]);
        var locked = Path.Combine(_mediaRoot, "Locked");
        Directory.CreateDirectory(locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);

        (await _reconciler.ReconcileAsync(CancellationToken.None))
            .Should()
            .BeOfType<ReconcileOutcome.Refused>();

        (await IndexedAsync()).Select(record => record.RelativePath).Should().Equal("existing.mp4");
        (await progress.GetAllAsync()).Should().ContainSingle();
        (await favourites.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task CompletedScanFindingNothing_PreservesNonEmptyIndexAndProgress()
    {
        var file = Path.Combine(_mediaRoot, "existing.mp4");
        File.WriteAllBytes(file, [1]);
        await _reconciler.ReconcileAsync(CancellationToken.None);
        var progress = _media.ProgressStore;
        await progress.UpdateAsync("existing.mp4", 10, 100);
        File.Delete(file);

        (await _reconciler.ReconcileAsync(CancellationToken.None))
            .Should()
            .BeOfType<ReconcileOutcome.Refused>();

        (await IndexedAsync()).Should().ContainSingle();
        (await progress.GetAllAsync()).Should().ContainSingle();
    }

    private async Task<MediaRecord[]> IndexedAsync() => await _mediaStore.SearchAsync(null);
}
