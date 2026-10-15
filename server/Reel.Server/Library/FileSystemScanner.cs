using Reel.Server.Storage;

namespace Reel.Server.Library;

public sealed record LibraryScanResult(IReadOnlyList<MediaRecord> Media, bool IsComplete);

public sealed class FileSystemScanner(MediaPathResolver paths, ILogger<FileSystemScanner> logger)
{
    /// <summary>Walks the whole library synchronously; callers on a request or startup path run it off-thread.</summary>
    public LibraryScanResult Scan(CancellationToken ct)
    {
        var media = new List<MediaRecord>();
        var isComplete = ScanDirectory(new DirectoryInfo(paths.MediaRoot), string.Empty, media, ct);
        return new LibraryScanResult(media, isComplete);
    }

    private bool ScanDirectory(
        DirectoryInfo directory,
        string relativePath,
        List<MediaRecord> media,
        CancellationToken ct
    )
    {
        var isComplete = true;
        try
        {
            foreach (
                var entry in directory
                    .EnumerateFileSystemInfos()
                    .Where(MediaPathResolver.IsLibraryEntry)
            )
            {
                ct.ThrowIfCancellationRequested();
                var childPath =
                    relativePath.Length == 0 ? entry.Name : $"{relativePath}/{entry.Name}";
                if (entry is DirectoryInfo childDirectory)
                {
                    isComplete &= ScanDirectory(childDirectory, childPath, media, ct);
                }
                else if (entry is FileInfo file && MediaPathResolver.IsPlayable(file.Name))
                {
                    media.Add(LibraryEntries.Index(childPath, file.Length));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {Path}", directory.FullName);
            return false;
        }

        return isComplete;
    }
}
