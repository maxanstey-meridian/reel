namespace Reel.Server.Tests;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.FileSystem;

public sealed class LibraryServiceTests : IDisposable
{
    private readonly string _mediaRoot;
    private readonly FileSystemScanner _scanner;

    public LibraryServiceTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_mediaRoot);

        var config = new ReelConfig
        {
            MediaRoot = _mediaRoot,
            SecretKey = "TEST",
            Port = 8080,
        };

        _scanner = new FileSystemScanner(config, new NullMediaStore(), NullLogger<FileSystemScanner>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
    }

    [Fact]
    public async Task SingleFile_ReturnsRootWithOneChild()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "film.mp4"), new byte[1024]);

        var root = await _scanner.BuildTreeAsync(CancellationToken.None);

        root.Type.Should().Be(FileNodeType.Directory);
        root.Children.Should().HaveCount(1);

        var file = root.Children![0];
        file.Name.Should().Be("film.mp4");
        file.Type.Should().Be(FileNodeType.File);
        file.Extension.Should().Be(".mp4");
        file.SizeBytes.Should().Be(1024);
        file.Children.Should().BeNull();
    }

    [Fact]
    public async Task NestedDirs_ReturnsCorrectTree()
    {
        var filmsDir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(filmsDir);
        File.WriteAllBytes(Path.Combine(filmsDir, "a.mp4"), new byte[512]);

        var root = await _scanner.BuildTreeAsync(CancellationToken.None);

        root.Children.Should().HaveCount(1);
        var dir = root.Children![0];
        dir.Name.Should().Be("Films");
        dir.Type.Should().Be(FileNodeType.Directory);
        dir.SizeBytes.Should().BeNull();
        dir.Extension.Should().BeNull();
        dir.Children.Should().HaveCount(1);
        dir.Children![0].Name.Should().Be("a.mp4");
    }

    [Fact]
    public async Task EmptyDir_IsPruned()
    {
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "empty"));

        var root = await _scanner.BuildTreeAsync(CancellationToken.None);

        root.Children.Should().NotBeNull();
        root.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task Dotfile_IsExcluded()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, ".DS_Store"), new byte[10]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "real.mp4"), new byte[10]);

        var root = await _scanner.BuildTreeAsync(CancellationToken.None);

        root.Children.Should().HaveCount(1);
        root.Children![0].Name.Should().Be("real.mp4");
    }

    [Fact]
    public async Task DotDirectory_IsExcluded()
    {
        Directory.CreateDirectory(Path.Combine(_mediaRoot, ".hidden"));
        var visibleDir = Path.Combine(_mediaRoot, "visible");
        Directory.CreateDirectory(visibleDir);
        File.WriteAllBytes(Path.Combine(visibleDir, "movie.mp4"), new byte[10]);

        var root = await _scanner.BuildTreeAsync(CancellationToken.None);

        root.Children.Should().HaveCount(1);
        root.Children![0].Name.Should().Be("visible");
    }

    [Fact]
    public async Task ExtensionCasing_IsLowerCased()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "FILM.MKV"), new byte[10]);

        var root = await _scanner.BuildTreeAsync(CancellationToken.None);
        root.Children![0].Extension.Should().Be(".mkv");
    }

    [Fact]
    public async Task AlphabeticalOrder_IsSorted()
    {
        File.WriteAllBytes(Path.Combine(_mediaRoot, "b.mp4"), new byte[10]);
        File.WriteAllBytes(Path.Combine(_mediaRoot, "a.mp4"), new byte[10]);

        var root = await _scanner.BuildTreeAsync(CancellationToken.None);

        root.Children![0].Name.Should().Be("a.mp4");
        root.Children![1].Name.Should().Be("b.mp4");
    }

    [Fact]
    public async Task RootRelativePath_IsEmptyString()
    {
        var root = await _scanner.BuildTreeAsync(CancellationToken.None);
        root.RelativePath.Should().BeEmpty();
    }

    [Fact]
    public async Task NestedRelativePath_UsesForwardSlash()
    {
        var filmsDir = Path.Combine(_mediaRoot, "Films");
        Directory.CreateDirectory(filmsDir);
        File.WriteAllBytes(Path.Combine(filmsDir, "a.mp4"), new byte[10]);

        var root = await _scanner.BuildTreeAsync(CancellationToken.None);
        var file = root.Children![0].Children![0];

        file.RelativePath.Should().Be("Films/a.mp4");
    }

    [Fact]
    public async Task Symlinks_AreExcluded()
    {
        // Real file and directory (directory needs content to avoid pruning)
        File.WriteAllBytes(Path.Combine(_mediaRoot, "real.mp4"), new byte[10]);
        var realDir = Path.Combine(_mediaRoot, "RealDir");
        Directory.CreateDirectory(realDir);
        File.WriteAllBytes(Path.Combine(realDir, "inside.mp4"), new byte[10]);

        // Symlinks to the real entries
        File.CreateSymbolicLink(Path.Combine(_mediaRoot, "link.mp4"), Path.Combine(_mediaRoot, "real.mp4"));
        Directory.CreateSymbolicLink(Path.Combine(_mediaRoot, "LinkDir"), realDir);

        var root = await _scanner.BuildTreeAsync(CancellationToken.None);
        var names = root.Children!.Select(c => c.Name).ToList();

        names.Should().Contain("real.mp4");
        names.Should().Contain("RealDir");
        names.Should().NotContain("link.mp4");
        names.Should().NotContain("LinkDir");
    }

    [Fact]
    public async Task PermissionDeniedDir_IsPruned()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return; // chmod semantics only reliable on Unix

        var restrictedDir = Path.Combine(_mediaRoot, "restricted");
        Directory.CreateDirectory(restrictedDir);
        File.WriteAllBytes(Path.Combine(restrictedDir, "secret.mp4"), new byte[10]);

        // Remove all permissions so enumeration throws UnauthorizedAccessException
        File.SetUnixFileMode(restrictedDir, (UnixFileMode)0);

        try
        {
            var root = await _scanner.BuildTreeAsync(CancellationToken.None);

            // Restricted dir has no accessible children, so it gets pruned
            root.Children.Should().NotBeNull();
            root.Children.Should().BeEmpty();
        }
        finally
        {
            // Restore permissions so Dispose() cleanup can delete
            File.SetUnixFileMode(
                restrictedDir,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
    }

    private sealed class NullMediaStore : IMediaStore
    {
        public Task UpsertFromScanAsync(string relativePath, string? parsedTitle, int? year, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<MediaRecord?> GetAsync(string relativePath, CancellationToken ct) =>
            Task.FromResult<MediaRecord?>(null);

        public Task SetWatchStateAsync(int userId, string relativePath, string? state, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<bool?> IsWatchedAsync(int userId, string relativePath, CancellationToken ct) =>
            Task.FromResult<bool?>(null);

        public Task UpdateMetaAsync(
            string relativePath,
            Application.Models.MediaUpdateRequest update,
            CancellationToken ct
        ) => Task.CompletedTask;

        public Task UpdateTmdbCacheAsync(
            string relativePath,
            int tmdbId,
            string? posterUrl,
            string? backdropUrl,
            string? overview,
            string? releaseDate,
            CancellationToken ct
        ) => Task.CompletedTask;

        public Task SetTagsAsync(int userId, string relativePath, IReadOnlyList<string> tags, CancellationToken ct) =>
            Task.CompletedTask;

        public Task AddTagAsync(int userId, string relativePath, string tag, CancellationToken ct) =>
            Task.CompletedTask;

        public Task RemoveTagAsync(int userId, string relativePath, string tag, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetTagsAsync(int userId, string relativePath, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<TagCount>> GetAllTagsAsync(int userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TagCount>>([]);

        public Task<IReadOnlyList<FileNode>> SearchAsync(
            int userId,
            Application.Models.SearchQuery query,
            CancellationToken ct
        ) => Task.FromResult<IReadOnlyList<FileNode>>([]);

        public Task<string?> GetDisplayTitleAsync(string relativePath, CancellationToken ct) =>
            Task.FromResult<string?>(null);

        public Task SaveSubtitlesAsync(
            string relativePath,
            IReadOnlyList<SubtitleTrack> tracks,
            CancellationToken ct
        ) => Task.CompletedTask;

        public Task<IReadOnlyList<SubtitleTrack>> GetSubtitlesAsync(string relativePath, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SubtitleTrack>>([]);
    }
}
