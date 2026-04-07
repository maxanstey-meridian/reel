namespace Reel.Server.Infrastructure.FileSystem;

using HeyRed.Mime;
using Reel.Server.Application.Ports;
using Reel.Server.Services;

public sealed class FileSystemStreamer : IFileStreamer
{
    private readonly ReelConfig _config;

    public FileSystemStreamer(ReelConfig config)
    {
        _config = config;
    }

    public (string fullPath, string contentType)? Resolve(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_config.NormalisedMediaRoot, relativePath));

        if (!fullPath.StartsWith(_config.NormalisedMediaRoot, StringComparison.Ordinal))
            return null;

        if (!File.Exists(fullPath))
            return null;

        var contentType = MimeTypesMap.GetMimeType(Path.GetExtension(fullPath));

        return (fullPath, contentType);
    }
}
