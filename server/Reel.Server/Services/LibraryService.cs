namespace Reel.Server.Services;

using Reel.Server.Models;

public sealed class LibraryService
{
    private readonly string _mediaRoot;
    private readonly ILogger<LibraryService> _logger;

    public LibraryService(ReelConfig config, ILogger<LibraryService> logger)
    {
        _mediaRoot = config.NormalisedMediaRoot;
        _logger = logger;
    }

    public FileNode GetTree() => BuildNode(_mediaRoot, string.Empty);

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

                        children.Add(BuildNode(entryPath, childRelative));
                    }
                    else
                    {
                        var fileInfo = new FileInfo(entryPath);
                        if (fileInfo.LinkTarget is not null)
                            continue;

                        var extension = Path.GetExtension(name).ToLowerInvariant();
                        var parsed = FilenameParser.Parse(name);
                        children.Add(new FileNode(
                            name,
                            FileNodeType.File,
                            childRelative,
                            string.IsNullOrEmpty(extension) ? null : extension,
                            fileInfo.Length,
                            null,
                            parsed.Title,
                            parsed.Year));
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
            null);
    }
}
