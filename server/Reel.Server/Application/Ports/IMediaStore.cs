namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface IMediaStore
{
    Task UpsertFromScanAsync(string relativePath, string? parsedTitle, int? year, CancellationToken ct);
    Task<MediaRecord?> GetAsync(string relativePath, CancellationToken ct);
    Task SetWatchStateAsync(int userId, string relativePath, string? state, CancellationToken ct);
    Task<bool?> IsWatchedAsync(int userId, string relativePath, CancellationToken ct);
}
