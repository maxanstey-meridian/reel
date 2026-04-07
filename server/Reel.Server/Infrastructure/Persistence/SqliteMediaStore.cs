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

    public async Task SetWatchStateAsync(int userId, string relativePath, string? state, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        // Ensure media row exists
        using var mediaCmd = conn.CreateCommand();
        mediaCmd.CommandText = "INSERT OR IGNORE INTO media (relative_path) VALUES (@path);";
        mediaCmd.Parameters.AddWithValue("@path", relativePath);
        await mediaCmd.ExecuteNonQueryAsync(ct);

        if (state is null)
        {
            using var deleteCmd = conn.CreateCommand();
            deleteCmd.CommandText = """
                DELETE FROM watch_state
                WHERE user_id = @userId AND relative_path = @path;
                """;
            deleteCmd.Parameters.AddWithValue("@userId", userId);
            deleteCmd.Parameters.AddWithValue("@path", relativePath);
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            using var upsertCmd = conn.CreateCommand();
            upsertCmd.CommandText = """
                INSERT OR REPLACE INTO watch_state (user_id, relative_path, state)
                VALUES (@userId, @path, @state);
                """;
            upsertCmd.Parameters.AddWithValue("@userId", userId);
            upsertCmd.Parameters.AddWithValue("@path", relativePath);
            upsertCmd.Parameters.AddWithValue("@state", state);
            await upsertCmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<bool?> IsWatchedAsync(int userId, string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        // Check explicit override first
        using var overrideCmd = conn.CreateCommand();
        overrideCmd.CommandText = """
            SELECT state FROM watch_state
            WHERE user_id = @userId AND relative_path = @path;
            """;
        overrideCmd.Parameters.AddWithValue("@userId", userId);
        overrideCmd.Parameters.AddWithValue("@path", relativePath);

        var overrideResult = await overrideCmd.ExecuteScalarAsync(ct);
        if (overrideResult is string overrideState)
            return overrideState == "watched";

        // Fall back to progress-based derivation
        using var progressCmd = conn.CreateCommand();
        progressCmd.CommandText = """
            SELECT position_seconds, duration_seconds FROM progress
            WHERE user_id = @userId AND relative_path = @path;
            """;
        progressCmd.Parameters.AddWithValue("@userId", userId);
        progressCmd.Parameters.AddWithValue("@path", relativePath);

        using var reader = await progressCmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var position = reader.GetDouble(0);
        var duration = reader.GetDouble(1);

        if (duration <= 0)
            return null;

        return position / duration >= 0.90;
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
            DateTime.Parse(reader.GetString(14), null, DateTimeStyles.RoundtripKind)
        );
    }
}
