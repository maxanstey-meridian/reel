namespace Reel.Server.Infrastructure.FileSystem;

using HeyRed.Mime;
using Reel.Server.Application.Ports;

public sealed class FileSystemStreamer : IFileStreamer
{
    private readonly MediaPathResolver _pathResolver;

    public FileSystemStreamer(MediaPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    public (string fullPath, string contentType)? Resolve(string relativePath)
    {
        var fullPath = _pathResolver.ResolvePlayableFile(relativePath);
        if (fullPath is null)
            return null;

        var contentType = MimeTypesMap.GetMimeType(Path.GetExtension(fullPath));

        return (fullPath, contentType);
    }
}
