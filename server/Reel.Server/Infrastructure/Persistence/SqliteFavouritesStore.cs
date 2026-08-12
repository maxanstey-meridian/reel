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

    public async Task AddAsync(string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO favourites (relative_path, added_at)
            VALUES (@path, @addedAt);
            """;
        cmd.Parameters.AddWithValue("@path", relativePath);
        cmd.Parameters.AddWithValue("@addedAt", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveAsync(string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM favourites WHERE relative_path = @path;";
        cmd.Parameters.AddWithValue("@path", relativePath);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Favourite>> GetAllAsync(CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT relative_path, added_at
            FROM favourites
            ORDER BY added_at DESC;
            """;

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Favourite>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(
                new Favourite(
                    reader.GetString(0),
                    DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind)
                )
            );
        }

        return results;
    }

    public async Task<bool> IsFavouriteAsync(string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM favourites WHERE relative_path = @path);";
        cmd.Parameters.AddWithValue("@path", relativePath);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
    }
}
