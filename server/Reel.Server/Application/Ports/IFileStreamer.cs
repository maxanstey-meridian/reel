namespace Reel.Server.Application.Ports;

public interface IFileStreamer
{
    (string fullPath, string contentType)? Resolve(string relativePath);
}
