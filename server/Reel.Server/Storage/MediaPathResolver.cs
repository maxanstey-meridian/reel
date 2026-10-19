using System.Globalization;
using Microsoft.Extensions.Options;

namespace Reel.Server.Storage;

public sealed class MediaPathResolver(IOptions<ReelOptions> options)
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
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
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".flac",
        ".aac",
        ".m4a",
        ".ogg",
        ".wav",
    };

    /// <summary>Library order: "Episode 2" sorts before "Episode 10".</summary>
    public static readonly StringComparer NaturalOrder = StringComparer.Create(
        CultureInfo.InvariantCulture,
        CompareOptions.NumericOrdering
    );

    /// <summary>Absolute media root, always ending in a directory separator.</summary>
    public string MediaRoot { get; } =
        Path.GetFullPath(options.Value.MediaRoot).TrimEnd(Path.DirectorySeparatorChar)
        + Path.DirectorySeparatorChar;

    /// <summary>Path of Reel's own data (database and generated caches) under MediaRoot/.reel.</summary>
    public string AppDataPath(string name) => Path.Combine(MediaRoot, ".reel", name);

    public string ToRelativePath(string fullPath) => Path.GetRelativePath(MediaRoot, fullPath);

    public static bool IsAudio(string path) => AudioExtensions.Contains(Path.GetExtension(path));

    public static bool IsVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    public static bool IsPlayable(string path) =>
        IsAudio(path) || VideoExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// The one definition of what belongs to the library, shared by the scanner and path resolution.
    /// Hidden entries (including Reel's own .reel folder) and symbolic links are never library entries,
    /// so nothing can be indexed, and later reconciled away with its progress, that a scan would not list.
    /// </summary>
    public static bool IsLibraryEntry(FileSystemInfo entry) =>
        !entry.Name.StartsWith('.') && entry.LinkTarget is null;

    /// <summary>Canonical "a/b" form of a library-relative path, or null if it cannot name a library entry.</summary>
    public static string? Normalize(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return null;
        }

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 || segments.Any(segment => segment.StartsWith('.'))
            ? null
            : string.Join('/', segments);
    }

    /// <summary>Full path of a playable library file, matched with the exact names a scan reports.</summary>
    public string? ResolvePlayableFile(string? relativePath)
    {
        var segments = Normalize(relativePath)?.Split('/');
        if (segments is null || !IsPlayable(segments[^1]))
        {
            return null;
        }

        FileSystemInfo? entry = new DirectoryInfo(MediaRoot);
        try
        {
            // Walking the listing, not File.Exists, keeps the volume's case-insensitive matches out.
            foreach (var segment in segments)
            {
                entry = (entry as DirectoryInfo)
                    ?.EnumerateFileSystemInfos()
                    .FirstOrDefault(child => child.Name == segment && IsLibraryEntry(child));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return entry is FileInfo ? entry.FullName : null;
    }
}
