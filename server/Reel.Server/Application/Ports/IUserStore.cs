namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface IUserStore
{
    Task<User> CreateAsync(string name, string? icon, CancellationToken ct);
    Task<User?> GetAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct);
    Task<User?> UpdateAsync(int id, string? name, string? icon, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
    Task<bool> ExistsAsync(int id, CancellationToken ct);
}
