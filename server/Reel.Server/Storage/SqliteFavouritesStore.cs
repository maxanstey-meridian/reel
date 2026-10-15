using Dapper;
using Reel.Server.Contracts.Reel;

namespace Reel.Server.Storage;

public sealed class SqliteFavouritesStore(DatabaseService db, TimeProvider time)
{
    public async Task AddAsync(string relativePath)
    {
        using var connection = db.GetConnection();
        await connection.ExecuteAsync(
            "INSERT OR IGNORE INTO favourites (relative_path, added_at) VALUES (@relativePath, @addedAt);",
            new { relativePath, addedAt = time.GetUtcNow().UtcDateTime }
        );
    }

    public async Task RemoveAsync(string relativePath)
    {
        using var connection = db.GetConnection();
        await connection.ExecuteAsync(
            "DELETE FROM favourites WHERE relative_path = @relativePath;",
            new { relativePath }
        );
    }

    public async Task<Favourite[]> GetAllAsync()
    {
        using var connection = db.GetConnection();
        return
        [
            .. await connection.QueryAsync<Favourite>(
                "SELECT * FROM favourites ORDER BY added_at DESC;"
            ),
        ];
    }
}
