namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface ILibraryScanner
{
    Task<FileNode> BuildTreeAsync(CancellationToken ct);
    Task<LibraryScanResult> ScanAsync(CancellationToken ct);
}

public sealed record LibraryScanResult(FileNode Root, bool IsComplete);
