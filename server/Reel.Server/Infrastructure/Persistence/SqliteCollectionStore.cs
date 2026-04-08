namespace Reel.Server.Infrastructure.Persistence;

using Reel.Server.Application.Ports;
using Reel.Server.Domain;

public sealed class SqliteCollectionStore : ICollectionStore
{
    private readonly DatabaseService _db;

    public SqliteCollectionStore(DatabaseService db)
    {
        _db = db;
    }

    public async Task<Collection[]> GetAllAsync(int userId, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.id, c.name, c.created_at,
                   (SELECT COUNT(*) FROM collection_items WHERE collection_id = c.id) AS item_count
            FROM collections c
            WHERE c.user_id = @userId
            ORDER BY c.created_at DESC;
            """;
        cmd.Parameters.AddWithValue("@userId", userId);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Collection>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(
                new Collection(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3)
                )
            );
        }

        return results.ToArray();
    }

    public async Task<CollectionWithItems?> GetWithItemsAsync(int collectionId, int userId, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var collCmd = conn.CreateCommand();
        collCmd.CommandText = """
            SELECT id, name, created_at
            FROM collections
            WHERE id = @id AND user_id = @userId;
            """;
        collCmd.Parameters.AddWithValue("@id", collectionId);
        collCmd.Parameters.AddWithValue("@userId", userId);

        using var collReader = await collCmd.ExecuteReaderAsync(ct);
        if (!await collReader.ReadAsync(ct))
            return null;

        var id = collReader.GetInt32(0);
        var name = collReader.GetString(1);
        var createdAt = collReader.GetString(2);
        collReader.Close();

        using var itemsCmd = conn.CreateCommand();
        itemsCmd.CommandText = """
            SELECT relative_path, position, added_at
            FROM collection_items
            WHERE collection_id = @id
            ORDER BY position ASC;
            """;
        itemsCmd.Parameters.AddWithValue("@id", id);

        using var itemsReader = await itemsCmd.ExecuteReaderAsync(ct);
        var items = new List<CollectionItem>();
        while (await itemsReader.ReadAsync(ct))
        {
            items.Add(
                new CollectionItem(
                    itemsReader.GetString(0),
                    itemsReader.GetInt32(1),
                    itemsReader.GetString(2)
                )
            );
        }

        return new CollectionWithItems(id, name, createdAt, items.ToArray());
    }

    public async Task<Collection> CreateAsync(int userId, string name, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO collections (user_id, name, created_at)
            VALUES (@userId, @name, @createdAt)
            RETURNING id, name, created_at;
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));

        using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        return new Collection(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            0
        );
    }

    public async Task RenameAsync(int collectionId, int userId, string name, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE collections SET name = @name
            WHERE id = @id AND user_id = @userId;
            """;
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@id", collectionId);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int collectionId, int userId, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM collections WHERE id = @id AND user_id = @userId;";
        cmd.Parameters.AddWithValue("@id", collectionId);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AddItemAsync(int collectionId, string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var mediaCmd = conn.CreateCommand();
        mediaCmd.CommandText = "INSERT OR IGNORE INTO media (relative_path) VALUES (@path);";
        mediaCmd.Parameters.AddWithValue("@path", relativePath);
        await mediaCmd.ExecuteNonQueryAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO collection_items (collection_id, relative_path, position, added_at)
            VALUES (@collectionId, @path,
                    COALESCE((SELECT MAX(position) FROM collection_items WHERE collection_id = @collectionId), 0) + 1,
                    @addedAt);
            """;
        cmd.Parameters.AddWithValue("@collectionId", collectionId);
        cmd.Parameters.AddWithValue("@path", relativePath);
        cmd.Parameters.AddWithValue("@addedAt", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveItemAsync(int collectionId, string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM collection_items WHERE collection_id = @collectionId AND relative_path = @path;";
        cmd.Parameters.AddWithValue("@collectionId", collectionId);
        cmd.Parameters.AddWithValue("@path", relativePath);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ReorderAsync(int collectionId, string[] paths, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        using var transaction = conn.BeginTransaction();

        for (var i = 0; i < paths.Length; i++)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                UPDATE collection_items SET position = @position
                WHERE collection_id = @collectionId AND relative_path = @path;
                """;
            cmd.Parameters.AddWithValue("@position", i + 1);
            cmd.Parameters.AddWithValue("@collectionId", collectionId);
            cmd.Parameters.AddWithValue("@path", paths[i]);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        transaction.Commit();
    }
}
