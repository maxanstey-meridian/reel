namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface IProgressStore
{
    Task UpdateAsync(string relativePath, double positionSeconds, double durationSeconds, CancellationToken ct);
    Task<WatchProgress?> GetAsync(string relativePath, CancellationToken ct);
    Task<IReadOnlyList<WatchProgress>> GetAllAsync(CancellationToken ct);
}
