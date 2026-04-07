namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface IMediaStore
{
    Task UpsertFromScanAsync(string relativePath, string? parsedTitle, int? year, CancellationToken ct);
    Task<MediaRecord?> GetAsync(string relativePath, CancellationToken ct);
}
