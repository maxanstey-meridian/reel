namespace Reel.Server.Infrastructure.Persistence;

using Reel.Server.Application.Ports;
using Reel.Server.Domain;

public sealed class SqliteFavouritesStore : IFavouritesStore
{
    private readonly DatabaseService _db;

    public SqliteFavouritesStore(DatabaseService db)
    {
        _db = db;
    }

    public async Task AddAsync(int userId, string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var mediaCmd = conn.CreateCommand();
        mediaCmd.CommandText = "INSERT OR IGNORE INTO media (relative_path) VALUES (@path);";
        mediaCmd.Parameters.AddWithValue("@path", relativePath);
        await mediaCmd.ExecuteNonQueryAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO favourites (user_id, relative_path, added_at)
            VALUES (@userId, @path, @addedAt);
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@path", relativePath);
        cmd.Parameters.AddWithValue("@addedAt", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveAsync(int userId, string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM favourites WHERE user_id = @userId AND relative_path = @path;";
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@path", relativePath);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Favourite>> GetAllAsync(int userId, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT relative_path, added_at
            FROM favourites
            WHERE user_id = @userId
            ORDER BY added_at DESC;
            """;
        cmd.Parameters.AddWithValue("@userId", userId);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Favourite>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new Favourite(
                reader.GetString(0),
                DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind)
            ));
        }

        return results;
    }

    public async Task<bool> IsFavouriteAsync(int userId, string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM favourites WHERE user_id = @userId AND relative_path = @path);";
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@path", relativePath);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
    }
}
