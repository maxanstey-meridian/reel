namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface IFavouritesStore
{
    Task AddAsync(int userId, string relativePath, CancellationToken ct);
    Task RemoveAsync(int userId, string relativePath, CancellationToken ct);
    Task<IReadOnlyList<Favourite>> GetAllAsync(int userId, CancellationToken ct);
    Task<bool> IsFavouriteAsync(int userId, string relativePath, CancellationToken ct);
}
