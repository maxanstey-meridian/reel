namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface IProgressStore
{
    Task UpdateAsync(int userId, string relativePath, double positionSeconds, double durationSeconds, CancellationToken ct);
    Task<WatchProgress?> GetAsync(int userId, string relativePath, CancellationToken ct);
    Task<IReadOnlyList<WatchProgress>> GetAllAsync(int userId, CancellationToken ct);
}
