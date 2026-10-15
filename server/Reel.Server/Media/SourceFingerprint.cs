using System.Security.Cryptography;
using System.Text;

namespace Reel.Server.Media;

internal static class SourceFingerprint
{
    /// <summary>Cache key for output derived from a file; it changes whenever the file is replaced or modified.</summary>
    public static string Key(string fullPath, params object[] parts)
    {
        var file = new FileInfo(fullPath);
        var input = string.Join(
            ':',
            parts.Prepend(fullPath).Append(file.Length).Append(file.LastWriteTimeUtc.Ticks)
        );
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
