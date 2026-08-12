namespace Reel.Server.Infrastructure.Ffmpeg;

using System.Security.Cryptography;
using System.Text;

internal readonly record struct SourceFingerprint(long Length, DateTime LastWriteTimeUtc)
{
    public static SourceFingerprint Read(string fullPath)
    {
        var file = new FileInfo(fullPath);
        file.Refresh();
        return new SourceFingerprint(file.Length, file.LastWriteTimeUtc);
    }

    public string ComputeKey(params object[] parts)
    {
        var input = string.Join(':', parts.Append(Length).Append(LastWriteTimeUtc.Ticks));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
