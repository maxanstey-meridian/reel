using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Domain;
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

    public FileSystemScannerMediaTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-scanner-media-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _mediaRoot,
            SecretKey = "test",
            Port = 8080
        };

        _db = new DatabaseService(config, NullLogger<DatabaseService>.Instance);
        _mediaStore = new SqliteMediaStore(_db);
        _scanner = new FileSystemScanner(config, _mediaStore, NullLogger<FileSystemScanner>.Instance);
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

        await _scanner.BuildTreeAsync(CancellationToken.None);

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

        await _scanner.BuildTreeAsync(CancellationToken.None);

        var record = await _mediaStore.GetAsync("Films/Inception.2010.mp4", CancellationToken.None);
        record.Should().NotBeNull();
        record!.ParsedTitle.Should().Be("Inception");
        record.Year.Should().Be(2010);
    }

    [Fact]
    public async Task BuildTreeAsync_EmptyDirectory_NoMediaRows()
    {
        await _scanner.BuildTreeAsync(CancellationToken.None);

        // Verify no media rows by trying to get a path
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media;";
        Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(0);
    }

    [Fact]
    public async Task BuildTreeAsync_DoesNotOverwriteExistingMediaRows()
    {
        // Pre-populate a media row with extra metadata
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO media (relative_path, parsed_title, year, genre)
            VALUES ('movie.mp4', 'Custom Title', 2020, 'Sci-Fi');
            """;
        cmd.ExecuteNonQuery();
        conn.Dispose();

        // Create the actual file and scan
        File.WriteAllBytes(Path.Combine(_mediaRoot, "movie.mp4"), new byte[100]);
        await _scanner.BuildTreeAsync(CancellationToken.None);

        // Existing row should be preserved
        var record = await _mediaStore.GetAsync("movie.mp4", CancellationToken.None);
        record.Should().NotBeNull();
        record!.ParsedTitle.Should().Be("Custom Title");
        record.Year.Should().Be(2020);
        record.Genre.Should().Be("Sci-Fi");
    }

    [Fact]
    public async Task BuildTreeAsync_SkipsNonPlayableFiles()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "readme.txt"), new byte[100]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "movie.mp4"), new byte[100]);

        await _scanner.BuildTreeAsync(CancellationToken.None);

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
}
