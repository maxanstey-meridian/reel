namespace Reel.Server.Infrastructure.FileSystem;

using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;

public sealed class FileSystemScanner : ILibraryScanner
{
    private static readonly HashSet<string> _playableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mkv",
        ".avi",
        ".mov",
        ".wmv",
        ".flv",
        ".webm",
        ".m4v",
        ".ts",
        ".mp3",
        ".flac",
        ".aac",
        ".m4a",
        ".ogg",
        ".wav",
    };

    private readonly string _mediaRoot;
    private readonly IMediaStore _mediaStore;
    private readonly ILogger<FileSystemScanner> _logger;

    public FileSystemScanner(ReelConfig config, IMediaStore mediaStore, ILogger<FileSystemScanner> logger)
    {
        _mediaRoot = config.NormalisedMediaRoot;
        _mediaStore = mediaStore;
        _logger = logger;
    }

    public async Task<FileNode> BuildTreeAsync(CancellationToken ct)
    {
        var root = BuildNode(_mediaRoot, string.Empty);

        return await UpsertAndEnrichAsync(root, ct);
    }

    public async Task<FileNode?> GetNextFileAsync(string relativePath, CancellationToken ct)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_mediaRoot, relativePath));
        if (!fullPath.StartsWith(_mediaRoot, StringComparison.Ordinal))
            return null;

        if (!File.Exists(fullPath))
            return null;

        var directory = Path.GetDirectoryName(fullPath)!;
        var playableFiles = new List<string>();

        foreach (var entry in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(entry);
            if (name.StartsWith('.'))
                continue;

            var ext = Path.GetExtension(name);
            if (!_playableExtensions.Contains(ext))
                continue;

            var fileInfo = new FileInfo(entry);
            if (fileInfo.LinkTarget is not null)
                continue;

            playableFiles.Add(entry);
        }

        playableFiles.Sort(new NaturalSortComparer());

        var currentIndex = playableFiles.FindIndex(f =>
            string.Equals(f, fullPath, StringComparison.Ordinal));

        if (currentIndex < 0 || currentIndex >= playableFiles.Count - 1)
            return null;

        var nextPath = playableFiles[currentIndex + 1];
        var nextName = Path.GetFileName(nextPath);
        var nextFileInfo = new FileInfo(nextPath);
        var directoryRelative = Path.GetRelativePath(_mediaRoot, directory);
        var nextRelative = directoryRelative == "."
            ? nextName
            : $"{directoryRelative}/{nextName}";
        var nextExtension = Path.GetExtension(nextName).ToLowerInvariant();
        var parsed = Application.FilenameParser.Parse(nextName);
        var thumbnailUrl = $"/api/files/thumbnail?path={Uri.EscapeDataString(nextRelative)}";

        var node = new FileNode(
            nextName,
            FileNodeType.File,
            nextRelative,
            string.IsNullOrEmpty(nextExtension) ? null : nextExtension,
            nextFileInfo.Length,
            null,
            parsed.Title,
            parsed.Year,
            thumbnailUrl,
            Watched: null,
            DisplayTitle: null
        );

        await _mediaStore.UpsertFromScanAsync(node.RelativePath, node.ParsedTitle, node.Year, ct);
        var dbDisplayTitle = await _mediaStore.GetDisplayTitleAsync(node.RelativePath, ct);
        var displayTitle = dbDisplayTitle ?? node.ParsedTitle ?? node.Name;

        return node with { DisplayTitle = displayTitle };
    }

    private async Task<FileNode> UpsertAndEnrichAsync(FileNode node, CancellationToken ct)
    {
        if (node.Type == FileNodeType.File)
        {
            await _mediaStore.UpsertFromScanAsync(node.RelativePath, node.ParsedTitle, node.Year, ct);

            var dbDisplayTitle = await _mediaStore.GetDisplayTitleAsync(node.RelativePath, ct);
            var displayTitle = dbDisplayTitle ?? node.ParsedTitle ?? node.Name;

            return node with { DisplayTitle = displayTitle };
        }

        if (node.Children is not { Count: > 0 })
            return node;

        var enrichedChildren = new List<FileNode>(node.Children.Count);
        foreach (var child in node.Children)
        {
            enrichedChildren.Add(await UpsertAndEnrichAsync(child, ct));
        }

        return node with { Children = enrichedChildren };
    }

    private FileNode BuildNode(string absolutePath, string relativePath)
    {
        var dirInfo = new DirectoryInfo(absolutePath);
        var children = new List<FileNode>();

        try
        {
            var entries = Directory.EnumerateFileSystemEntries(absolutePath).Order().ToList();

            foreach (var entryPath in entries)
            {
                var name = Path.GetFileName(entryPath);
                if (name.StartsWith('.'))
                    continue;

                var childRelative = string.IsNullOrEmpty(relativePath) ? name : $"{relativePath}/{name}";

                try
                {
                    var attrs = File.GetAttributes(entryPath);

                    if (attrs.HasFlag(FileAttributes.Directory))
                    {
                        var childDirInfo = new DirectoryInfo(entryPath);
                        if (childDirInfo.LinkTarget is not null)
                            continue;

                        var childNode = BuildNode(entryPath, childRelative);
                        if (childNode.Children is { Count: > 0 } || childNode.Type == FileNodeType.File)
                            children.Add(childNode);
                    }
                    else
                    {
                        var fileInfo = new FileInfo(entryPath);
                        if (fileInfo.LinkTarget is not null)
                            continue;

                        var extension = Path.GetExtension(name).ToLowerInvariant();
                        if (!_playableExtensions.Contains(extension))
                            continue;

                        var parsed = Application.FilenameParser.Parse(name);
                        var thumbnailUrl = $"/api/files/thumbnail?path={Uri.EscapeDataString(childRelative)}";
                        children.Add(
                            new FileNode(
                                name,
                                FileNodeType.File,
                                childRelative,
                                string.IsNullOrEmpty(extension) ? null : extension,
                                fileInfo.Length,
                                null,
                                parsed.Title,
                                parsed.Year,
                                thumbnailUrl,
                                Watched: null,
                                DisplayTitle: null
                            )
                        );
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Permission denied accessing {Path}", entryPath);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "I/O error accessing {Path}", entryPath);
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission denied enumerating {Path}", absolutePath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "I/O error enumerating {Path}", absolutePath);
        }

        return new FileNode(
            dirInfo.Name,
            FileNodeType.Directory,
            relativePath,
            null,
            null,
            children,
            null,
            null,
            null,
            Watched: null,
            DisplayTitle: null
        );
    }
}
