namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface IFavouritesStore
{
    Task AddAsync(string relativePath, CancellationToken ct);
    Task RemoveAsync(string relativePath, CancellationToken ct);
    Task<IReadOnlyList<Favourite>> GetAllAsync(CancellationToken ct);
    Task<bool> IsFavouriteAsync(string relativePath, CancellationToken ct);
}
