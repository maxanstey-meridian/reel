namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface ILibraryScanner
{
    Task<FileNode> BuildTreeAsync(CancellationToken ct);
}
