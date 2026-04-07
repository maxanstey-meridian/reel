namespace Reel.Server.Infrastructure.Persistence;

using System.Globalization;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;

public sealed class SqliteMediaStore : IMediaStore
{
    private readonly DatabaseService _db;

    public SqliteMediaStore(DatabaseService db)
    {
        _db = db;
    }

    public async Task UpsertFromScanAsync(string relativePath, string? parsedTitle, int? year, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO media (relative_path, parsed_title, year)
            VALUES (@path, @parsedTitle, @year);
            """;
        cmd.Parameters.AddWithValue("@path", relativePath);
        cmd.Parameters.AddWithValue("@parsedTitle", (object?)parsedTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@year", (object?)year ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<MediaRecord?> GetAsync(string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT relative_path, parsed_title, year, genre, rating, language, notes,
                   display_title, tmdb_id, poster_url, backdrop_url, overview,
                   release_date, added_at, updated_at
            FROM media
            WHERE relative_path = @path;
            """;
        cmd.Parameters.AddWithValue("@path", relativePath);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new MediaRecord(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetInt32(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            DateTime.Parse(reader.GetString(13), null, DateTimeStyles.RoundtripKind),
            DateTime.Parse(reader.GetString(14), null, DateTimeStyles.RoundtripKind));
    }
}
