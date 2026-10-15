using FluentAssertions;
using Reel.Server.Storage;

namespace Reel.Server.Tests;

public sealed class MediaPathResolverTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly MediaPathResolver _paths;

    public MediaPathResolverTests() => _paths = _media.Paths;

    public void Dispose() => _media.Dispose();

    [Fact]
    public void Resolve_ValidFile_ReturnsFullPath()
    {
        var fullPath = _media.Write("Films/video.mp4");

        _paths.ResolvePlayableFile("Films/video.mp4").Should().Be(fullPath);
    }

    [Fact]
    public void AppDataPath_IsUnderTheReelFolderOfTheMediaRoot()
    {
        _paths
            .AppDataPath("reel-v2.db")
            .Should()
            .Be(Path.Combine(_media.Root, ".reel", "reel-v2.db"));
        _paths.MediaRoot.Should().EndWith(Path.DirectorySeparatorChar.ToString());
    }

    [Theory]
    [InlineData("missing.mp4")]
    [InlineData("../../etc/passwd")]
    [InlineData("Films/../../etc/passwd")]
    [InlineData("/tmp/video.mp4")]
    [InlineData(".reel/private.mp4")]
    [InlineData("reel.db")]
    [InlineData("")]
    public void Resolve_MissingEscapingReservedOrUnplayablePath_ReturnsNull(string path)
    {
        _media.Write("reel.db");

        _paths.ResolvePlayableFile(path).Should().BeNull();
    }

    [Fact]
    public void Resolve_TraversalToExistingFileOutsideRoot_ReturnsNull()
    {
        var outside = Path.Combine(
            Path.GetDirectoryName(_media.Root)!,
            $"outside-{Guid.NewGuid():N}.mp4"
        );
        File.WriteAllBytes(outside, [1]);
        try
        {
            _paths.ResolvePlayableFile($"../{Path.GetFileName(outside)}").Should().BeNull();
        }
        finally
        {
            File.Delete(outside);
        }
    }

    // Anything a scan would not list must not resolve either, or indexing it lets the next reconcile
    // delete the row again and cascade away its progress and favourites.
    [Fact]
    public void Resolve_EntriesTheScannerSkips_ReturnNull()
    {
        _media.Write("Films/.hidden/secret.mp4");
        _media.Write(".hidden.mp4");
        var real = _media.Write("Films/real.mp4");
        File.CreateSymbolicLink(Path.Combine(_media.Root, "Films", "link.mp4"), real);
        Directory.CreateSymbolicLink(
            Path.Combine(_media.Root, "LinkDir"),
            Path.Combine(_media.Root, "Films")
        );

        _paths.ResolvePlayableFile("Films/.hidden/secret.mp4").Should().BeNull();
        _paths.ResolvePlayableFile(".hidden.mp4").Should().BeNull();
        _paths.ResolvePlayableFile("Films/link.mp4").Should().BeNull();
        _paths.ResolvePlayableFile("LinkDir/real.mp4").Should().BeNull();
        _paths.ResolvePlayableFile("films/REAL.mp4").Should().BeNull();
    }

    [Fact]
    public void Normalize_CollapsesEmptySegments()
    {
        _media.Write("Films/a.mp4");

        MediaPathResolver.Normalize("Films//a.mp4/").Should().Be("Films/a.mp4");
        _paths
            .ToRelativePath(_paths.ResolvePlayableFile("Films//a.mp4")!)
            .Should()
            .Be("Films/a.mp4");
    }
}
