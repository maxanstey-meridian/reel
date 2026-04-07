namespace Reel.Server.Application.Ports;

public interface IThumbnailGenerator
{
    Task<string?> GetOrGenerateAsync(string relativePath, CancellationToken ct);
}
