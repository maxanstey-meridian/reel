using System.Security.Cryptography;
using System.Text;

namespace Reel.Server.Media;

internal static class SourceFingerprint
{
    /// <summary>Cache key for output derived from a file; it changes whenever the file is replaced or modified.</summary>
    public static string Key(string fullPath, int? stream = null)
    {
        var file = new FileInfo(fullPath);
        var streamPart = stream is null ? "" : $"{stream}:";
        var input = $"{fullPath}:{streamPart}{file.Length}:{file.LastWriteTimeUtc.Ticks}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
