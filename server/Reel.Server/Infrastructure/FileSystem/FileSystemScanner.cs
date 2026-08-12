namespace Reel.Server.Infrastructure.FileSystem;

using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;

public sealed class FileSystemScanner : ILibraryScanner
{
    private readonly string _mediaRoot;
    private readonly MediaPathResolver _pathResolver;
    private readonly ILogger<FileSystemScanner> _logger;

    public FileSystemScanner(ReelConfig config, MediaPathResolver pathResolver, ILogger<FileSystemScanner> logger)
    {
        _mediaRoot = config.NormalisedMediaRoot;
        _pathResolver = pathResolver;
        _logger = logger;
    }

    public async Task<FileNode> BuildTreeAsync(CancellationToken ct) => (await ScanAsync(ct)).Root;

    public Task<LibraryScanResult> ScanAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var isComplete = true;
        var root = BuildNode(_mediaRoot, string.Empty, ct, ref isComplete);
        return Task.FromResult(new LibraryScanResult(root, isComplete));
    }

    private FileNode BuildNode(string absolutePath, string relativePath, CancellationToken ct, ref bool isComplete)
    {
        var dirInfo = new DirectoryInfo(absolutePath);
        var children = new List<FileNode>();

        try
        {
            var entries = Directory.EnumerateFileSystemEntries(absolutePath).Order().ToList();

            foreach (var entryPath in entries)
            {
                ct.ThrowIfCancellationRequested();
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

                        var childNode = BuildNode(entryPath, childRelative, ct, ref isComplete);
                        if (childNode.Children is { Count: > 0 } || childNode.Type == FileNodeType.File)
                            children.Add(childNode);
                    }
                    else
                    {
                        var fileInfo = new FileInfo(entryPath);
                        if (fileInfo.LinkTarget is not null)
                            continue;

                        var extension = Path.GetExtension(name).ToLowerInvariant();
                        if (!_pathResolver.IsPlayable(entryPath))
                            continue;

                        var parsed = Application.FilenameParser.Parse(name);
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
                                Watched: null
                            )
                        );
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    isComplete = false;
                    _logger.LogWarning(ex, "Permission denied accessing {Path}", entryPath);
                }
                catch (IOException ex)
                {
                    isComplete = false;
                    _logger.LogWarning(ex, "I/O error accessing {Path}", entryPath);
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            isComplete = false;
            _logger.LogWarning(ex, "Permission denied enumerating {Path}", absolutePath);
        }
        catch (IOException ex)
        {
            isComplete = false;
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
            Watched: null,
            Year: null
        );
    }
}
