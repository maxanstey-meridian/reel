namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface ITrickplayGenerator
{
    Task<TrickplayMetadata?> GetOrGenerateAsync(string relativePath, CancellationToken ct);
}
