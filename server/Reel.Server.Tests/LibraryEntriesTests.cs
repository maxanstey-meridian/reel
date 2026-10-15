using FluentAssertions;
using Reel.Server.Contracts.Reel;
using Reel.Server.Library;
using Reel.Server.Storage;

namespace Reel.Server.Tests;

public sealed class LibraryEntriesTests
{
    [Theory]
    [InlineData("Films/a.MKV", MediaKind.Video)]
    [InlineData("Music/b.flac", MediaKind.Audio)]
    public void File_CarriesItsMediaKind(string path, MediaKind kind) =>
        LibraryEntries.File(new MediaRecord(path, null, null, 1)).Kind.Should().Be(kind);

    [Fact]
    public void Tree_NestsMediaInFoldersInNaturalOrder()
    {
        var root = LibraryEntries.Tree(
            "Reel",
            [
                new MediaRecord("Shows/Show/Episode 10.mkv", "Episode 10", null, 10),
                new MediaRecord("b.mp4", "b", null, 2),
                new MediaRecord("Shows/Show/Episode 2.MKV", "Episode 2", null, 20),
                new MediaRecord("A.mp4", "A", 2001, 1),
            ]
        );

        root.Should()
            .BeEquivalentTo(
                new
                {
                    Name = "Reel",
                    Type = FileNodeType.Directory,
                    RelativePath = "",
                }
            );
        root.Children!.Select(node => node.Name).Should().Equal("A.mp4", "b.mp4", "Shows");
        root.Children![0]
            .Should()
            .Be(
                new FileNode(
                    "A.mp4",
                    FileNodeType.File,
                    MediaKind.Video,
                    "A.mp4",
                    ".mp4",
                    1,
                    null,
                    "A",
                    2001
                )
            );
        var show = root.Children![2].Children!.Should().ContainSingle().Subject;
        show.Should()
            .BeEquivalentTo(
                new
                {
                    Name = "Show",
                    Type = FileNodeType.Directory,
                    RelativePath = "Shows/Show",
                    Extension = (string?)null,
                    SizeBytes = (long?)null,
                }
            );
        show.Children!.Select(node => (node.RelativePath, node.Extension))
            .Should()
            .Equal(("Shows/Show/Episode 2.MKV", ".mkv"), ("Shows/Show/Episode 10.mkv", ".mkv"));
    }

    [Fact]
    public void Tree_OfNoMediaIsAnEmptyRoot() =>
        LibraryEntries.Tree("Reel", []).Children.Should().BeEmpty();
}
