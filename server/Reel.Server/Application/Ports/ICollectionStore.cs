namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface ICollectionStore
{
    Task<Collection[]> GetAllAsync(int userId, CancellationToken ct);
    Task<CollectionWithItems?> GetWithItemsAsync(int collectionId, int userId, CancellationToken ct);
    Task<Collection> CreateAsync(int userId, string name, CancellationToken ct);
    Task RenameAsync(int collectionId, int userId, string name, CancellationToken ct);
    Task DeleteAsync(int collectionId, int userId, CancellationToken ct);
    Task AddItemAsync(int collectionId, int userId, string relativePath, CancellationToken ct);
    Task RemoveItemAsync(int collectionId, int userId, string relativePath, CancellationToken ct);
    Task ReorderAsync(int collectionId, int userId, string[] paths, CancellationToken ct);
}
