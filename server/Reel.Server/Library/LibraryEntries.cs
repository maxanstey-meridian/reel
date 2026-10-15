using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;

namespace Reel.Server.Library;

/// <summary>How library files become index rows, and index rows become the browse tree and search results.</summary>
public static class LibraryEntries
{
    public static MediaRecord Index(string relativePath, long sizeBytes)
    {
        var parsed = FilenameParser.Parse(Path.GetFileName(relativePath));
        return new MediaRecord(relativePath, parsed.Title, parsed.Year, sizeBytes);
    }

    /// <summary>
    /// Canonical relative path of a playable library file, indexed first if it arrived after the last scan so
    /// rows that reference it can be stored; null when the path names no playable library file.
    /// </summary>
    public static async Task<string?> EnsureIndexedAsync(
        string? relativePath,
        MediaPathResolver paths,
        SqliteMediaStore mediaStore
    )
    {
        var fullPath = paths.ResolvePlayableFile(relativePath);
        if (fullPath is null)
        {
            return null;
        }

        var canonical = paths.ToRelativePath(fullPath);
        await mediaStore.EnsureIndexedAsync(Index(canonical, new FileInfo(fullPath).Length));
        return canonical;
    }

    public static FileNode File(MediaRecord media) =>
        new(
            Path.GetFileName(media.RelativePath),
            FileNodeType.File,
            MediaPathResolver.IsAudio(media.RelativePath) ? MediaKind.Audio : MediaKind.Video,
            media.RelativePath,
            Path.GetExtension(media.RelativePath).ToLowerInvariant(),
            media.SizeBytes,
            null,
            media.ParsedTitle,
            media.Year
        );

    /// <summary>The folder tree of indexed media; folders exist only where they contain media.</summary>
    public static FileNode Tree(string rootName, IEnumerable<MediaRecord> media) =>
        Directory(
            rootName,
            "",
            0,
            [.. media.Select(record => (record.RelativePath.Split('/'), record))]
        );

    private static FileNode Directory(
        string name,
        string relativePath,
        int depth,
        IReadOnlyCollection<(string[] Segments, MediaRecord Record)> media
    )
    {
        var files = media
            .Where(entry => entry.Segments.Length == depth + 1)
            .Select(entry => File(entry.Record));
        var directories = media
            .Where(entry => entry.Segments.Length > depth + 1)
            .GroupBy(entry => entry.Segments[depth], StringComparer.Ordinal)
            .Select(folder =>
                Directory(
                    folder.Key,
                    depth == 0 ? folder.Key : $"{relativePath}/{folder.Key}",
                    depth + 1,
                    [.. folder]
                )
            );
        return new FileNode(
            name,
            FileNodeType.Directory,
            null,
            relativePath,
            null,
            null,
            [
                .. files
                    .Concat(directories)
                    .OrderBy(node => node.Name, MediaPathResolver.NaturalOrder),
            ],
            null,
            null
        );
    }
}
