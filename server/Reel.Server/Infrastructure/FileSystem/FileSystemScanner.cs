namespace Reel.Server.Infrastructure.FileSystem;

using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;

public sealed class FileSystemScanner : ILibraryScanner
{
    private static readonly HashSet<string> _playableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v",
        ".mp3", ".flac", ".aac", ".m4a", ".ogg", ".wav",
    };

    private readonly string _mediaRoot;
    private readonly ILogger<FileSystemScanner> _logger;

    public FileSystemScanner(ReelConfig config, ILogger<FileSystemScanner> logger)
    {
        _mediaRoot = config.NormalisedMediaRoot;
        _logger = logger;
    }

    public FileNode BuildTree() => BuildNode(_mediaRoot, string.Empty);

    private FileNode BuildNode(string absolutePath, string relativePath)
    {
        var dirInfo = new DirectoryInfo(absolutePath);
        var children = new List<FileNode>();

        try
        {
            var entries = Directory.EnumerateFileSystemEntries(absolutePath)
                .Order()
                .ToList();

            foreach (var entryPath in entries)
            {
                var name = Path.GetFileName(entryPath);
                if (name.StartsWith('.'))
                    continue;

                var childRelative = string.IsNullOrEmpty(relativePath)
                    ? name
                    : $"{relativePath}/{name}";

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
                        children.Add(new FileNode(
                            name,
                            FileNodeType.File,
                            childRelative,
                            string.IsNullOrEmpty(extension) ? null : extension,
                            fileInfo.Length,
                            null,
                            parsed.Title,
                            parsed.Year,
                            thumbnailUrl));
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
            null);
    }
}
