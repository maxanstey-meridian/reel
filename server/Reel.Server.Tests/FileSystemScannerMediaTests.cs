using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Domain;
using Reel.Server.Application;
using Reel.Server.Application.Ports;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.FileSystem;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class FileSystemScannerMediaTests : IDisposable
{
    private readonly string _mediaRoot;
    private readonly DatabaseService _db;
    private readonly SqliteMediaStore _mediaStore;
    private readonly FileSystemScanner _scanner;
    private readonly LibraryIndexReconciler _reconciler;

    public FileSystemScannerMediaTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-scanner-media-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _mediaRoot,
            SecretKey = "test",
            Port = 8080,
        };

        _db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _mediaStore = new SqliteMediaStore(_db);
        _scanner = new FileSystemScanner(config, new MediaPathResolver(config), NullLogger<FileSystemScanner>.Instance);
        _reconciler = new LibraryIndexReconciler(_scanner, _mediaStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    [Fact]
    public async Task BuildTreeAsync_PopulatesMediaTableForFiles()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "The.Matrix.1999.1080p.BluRay.mkv"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "test.mp4"), new byte[512]);

        await _reconciler.ReconcileAsync(CancellationToken.None);

        var matrix = await _mediaStore.GetAsync("The.Matrix.1999.1080p.BluRay.mkv", CancellationToken.None);
        matrix.Should().NotBeNull();
        matrix!.ParsedTitle.Should().Be("The Matrix");
        matrix.Year.Should().Be(1999);

        var test = await _mediaStore.GetAsync("test.mp4", CancellationToken.None);
        test.Should().NotBeNull();
        test!.ParsedTitle.Should().Be("test");
    }

    [Fact]
    public async Task BuildTreeAsync_PopulatesNestedFiles()
    {
        var filmsDir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(filmsDir);
        File.WriteAllBytes(Path.Combine(filmsDir, "Inception.2010.mp4"), new byte[256]);

        await _reconciler.ReconcileAsync(CancellationToken.None);

        var record = await _mediaStore.GetAsync("Films/Inception.2010.mp4", CancellationToken.None);
        record.Should().NotBeNull();
        record!.ParsedTitle.Should().Be("Inception");
        record.Year.Should().Be(2010);
    }

    [Fact]
    public async Task BuildTreeAsync_EmptyDirectory_NoMediaRows()
    {
        await _reconciler.ReconcileAsync(CancellationToken.None);

        // Verify no media rows by trying to get a path
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media;";
        Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(0);
    }

    [Fact]
    public async Task ReconcileAsync_RefreshesScannerOwnedMetadata()
    {
        // Pre-populate a media row with scanner-owned metadata.
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO media (relative_path, parsed_title, year)
            VALUES ('movie.mp4', 'Custom Title', 2020);
            """;
        cmd.ExecuteNonQuery();
        conn.Dispose();

        // Create the actual file and scan
        File.WriteAllBytes(Path.Combine(_mediaRoot, "movie.mp4"), new byte[100]);
        await _reconciler.ReconcileAsync(CancellationToken.None);

        var record = await _mediaStore.GetAsync("movie.mp4", CancellationToken.None);
        record.Should().NotBeNull();
        record!.ParsedTitle.Should().Be("movie");
        record.Year.Should().BeNull();
    }

    [Fact]
    public async Task BuildTreeAsync_SkipsNonPlayableFiles()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "readme.txt"), new byte[100]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "movie.mp4"), new byte[100]);

        await _reconciler.ReconcileAsync(CancellationToken.None);

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media;";
        Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(1);

        var record = await _mediaStore.GetAsync("movie.mp4", CancellationToken.None);
        record.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildTreeAsync_ReturnsValidTree()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "film.mp4"), new byte[1024]);

        var root = await _scanner.BuildTreeAsync(CancellationToken.None);

        root.Type.Should().Be(FileNodeType.Directory);
        root.Children.Should().HaveCount(1);
        root.Children![0].Name.Should().Be("film.mp4");
        root.Children[0].Type.Should().Be(FileNodeType.File);
    }

    [Fact]
    public async Task IncompleteScan_PreservesMediaProgressAndFavouritesWithoutPartialAdditions()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "existing.mp4"), [1]);
        await _reconciler.ReconcileAsync(CancellationToken.None);
        var progress = new SqliteProgressStore(_db);
        var favourites = new SqliteFavouritesStore(_db);
        await progress.UpdateAsync("existing.mp4", 10, 100, CancellationToken.None);
        await favourites.AddAsync("existing.mp4", CancellationToken.None);

        File.WriteAllBytes(Path.Combine(_mediaRoot, "partial.mp4"), [1]);
        var partial = (await _scanner.ScanAsync(CancellationToken.None)) with { IsComplete = false };
        var reconciler = new LibraryIndexReconciler(new FixedScanner(partial), _mediaStore);

        await reconciler.Invoking(x => x.ReconcileAsync(CancellationToken.None)).Should().ThrowAsync<IOException>();
        (await _mediaStore.GetAsync("existing.mp4", CancellationToken.None)).Should().NotBeNull();
        (await _mediaStore.GetAsync("partial.mp4", CancellationToken.None)).Should().BeNull();
        (await progress.GetAsync("existing.mp4", CancellationToken.None)).Should().NotBeNull();
        (await favourites.IsFavouriteAsync("existing.mp4", CancellationToken.None)).Should().BeTrue();
    }

    private sealed class FixedScanner(LibraryScanResult result) : ILibraryScanner
    {
        public Task<FileNode> BuildTreeAsync(CancellationToken ct) => Task.FromResult(result.Root);

        public Task<LibraryScanResult> ScanAsync(CancellationToken ct) => Task.FromResult(result);
    }
}
