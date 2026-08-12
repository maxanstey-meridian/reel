namespace Reel.Server.Infrastructure.Persistence;

using Reel.Server.Application.Ports;
using Reel.Server.Domain;

public sealed class SqliteProgressStore : IProgressStore
{
    private readonly DatabaseService _db;

    public SqliteProgressStore(DatabaseService db)
    {
        _db = db;
    }

    public async Task UpdateAsync(
        string relativePath,
        double positionSeconds,
        double durationSeconds,
        CancellationToken ct
    )
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO progress (relative_path, position_seconds, duration_seconds, last_watched)
            VALUES (@path, @pos, @dur, @lastWatched);
            """;
        cmd.Parameters.AddWithValue("@path", relativePath);
        cmd.Parameters.AddWithValue("@pos", positionSeconds);
        cmd.Parameters.AddWithValue("@dur", durationSeconds);
        cmd.Parameters.AddWithValue("@lastWatched", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<WatchProgress?> GetAsync(string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT relative_path, position_seconds, duration_seconds, last_watched
            FROM progress
            WHERE relative_path = @path;
            """;
        cmd.Parameters.AddWithValue("@path", relativePath);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return ReadProgress(reader);
    }

    public async Task<IReadOnlyList<WatchProgress>> GetAllAsync(CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT relative_path, position_seconds, duration_seconds, last_watched
            FROM progress
            ORDER BY last_watched DESC;
            """;

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<WatchProgress>();
        while (await reader.ReadAsync(ct))
            results.Add(ReadProgress(reader));

        return results;
    }

    private static WatchProgress ReadProgress(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new WatchProgress(
            reader.GetString(0),
            reader.GetDouble(1),
            reader.GetDouble(2),
            DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind)
        );
    }
}
