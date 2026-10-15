using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Library;
using Reel.Server.Storage;

namespace Reel.Server.Tests;

public sealed class FileSystemScannerTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly FileSystemScanner _scanner;

    public FileSystemScannerTests() =>
        _scanner = new FileSystemScanner(_media.Paths, NullLogger<FileSystemScanner>.Instance);

    public void Dispose() => _media.Dispose();

    private LibraryScanResult Scan() => _scanner.Scan(CancellationToken.None);

    [Fact]
    public void NestedFile_IsIndexedWithItsRelativePathParsedTitleAndSize()
    {
        _media.Write("Films/The.Matrix.1999.MKV", new byte[512]);

        Scan()
            .Media.Should()
            .Equal(new MediaRecord("Films/The.Matrix.1999.MKV", "The Matrix", 1999, 512));
    }

    [Fact]
    public void HiddenEntriesSymlinksAndUnplayableFiles_AreExcluded()
    {
        Directory.CreateDirectory(Path.Combine(_media.Root, "empty"));
        _media.Write(".DS_Store");
        _media.Write(".hidden/movie.mp4");
        _media.Write("notes.txt");
        var real = _media.Write("RealDir/inside.mp4");
        File.CreateSymbolicLink(Path.Combine(_media.Root, "link.mp4"), real);
        Directory.CreateSymbolicLink(
            Path.Combine(_media.Root, "LinkDir"),
            Path.GetDirectoryName(real)!
        );

        Scan().Media.Select(media => media.RelativePath).Should().Equal("RealDir/inside.mp4");
    }

    [Fact]
    public void PermissionDeniedDir_IsSkippedAndMarksTheScanIncomplete()
    {
        _media.Write("restricted/secret.mp4");
        File.SetUnixFileMode(Path.Combine(_media.Root, "restricted"), UnixFileMode.None);

        var scan = Scan();

        scan.Media.Should().BeEmpty();
        scan.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void MissingMediaRoot_MarksTheScanIncomplete()
    {
        Directory.Delete(_media.Root, recursive: true);

        Scan().IsComplete.Should().BeFalse();
    }
}
