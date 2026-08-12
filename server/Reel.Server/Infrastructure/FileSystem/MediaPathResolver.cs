namespace Reel.Server.Infrastructure.FileSystem;

using Reel.Server.Infrastructure.Configuration;

public sealed class MediaPathResolver
{
    private static readonly HashSet<string> PlayableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts",
        ".mp3", ".flac", ".aac", ".m4a", ".ogg", ".wav",
    };

    private readonly string _mediaRoot;

    public MediaPathResolver(ReelConfig config)
    {
        _mediaRoot = Path.GetFullPath(config.MediaRoot).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }

    public string? ResolvePlayableFile(string relativePath) =>
        ResolveExistingFile(relativePath, requirePlayable: true);

    public string? ResolveExistingFile(string relativePath) =>
        ResolveExistingFile(relativePath, requirePlayable: false);

    public bool IsPlayable(string path) => PlayableExtensions.Contains(Path.GetExtension(path));

    public bool IsSafeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            return false;

        var segments = GetSegments(relativePath);
        if (
            segments.Length == 0
            || segments.Any(segment =>
                segment is "." or ".." || segment.Equals(".reel", StringComparison.OrdinalIgnoreCase)
            )
        )
            return false;

        return IsWithinRoot(Path.GetFullPath(Path.Combine(_mediaRoot, relativePath)));
    }

    private string? ResolveExistingFile(string relativePath, bool requirePlayable)
    {
        if (!IsSafeRelativePath(relativePath))
            return null;

        var segments = GetSegments(relativePath);

        var fullPath = Path.GetFullPath(Path.Combine(_mediaRoot, relativePath));
        if (!IsWithinRoot(fullPath) || !File.Exists(fullPath) || (requirePlayable && !IsPlayable(fullPath)))
            return null;

        var currentPath = _mediaRoot.TrimEnd(Path.DirectorySeparatorChar);
        foreach (var segment in segments)
        {
            currentPath = Path.Combine(currentPath, segment);
            FileSystemInfo entry = Directory.Exists(currentPath)
                ? new DirectoryInfo(currentPath)
                : new FileInfo(currentPath);
            if (entry.LinkTarget is null)
                continue;

            var target = entry.ResolveLinkTarget(returnFinalTarget: true);
            if (target is null || !IsWithinRoot(target.FullName))
                return null;
        }

        return fullPath;
    }

    private bool IsWithinRoot(string path) =>
        Path.GetFullPath(path).StartsWith(_mediaRoot, StringComparison.Ordinal);

    private static string[] GetSegments(string relativePath) =>
        relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries
        );
}
