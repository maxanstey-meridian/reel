namespace Reel.Server.Infrastructure.Persistence;

using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;

public sealed class SqliteMediaStore : IMediaStore
{
    private readonly DatabaseService _db;

    public SqliteMediaStore(DatabaseService db)
    {
        _db = db;
    }

    public async Task ReconcileAsync(IReadOnlyCollection<MediaRecord> media, CancellationToken ct)
    {
        using var connection = _db.GetConnection();
        using var transaction = connection.BeginTransaction();
        using var current = connection.CreateCommand();
        current.Transaction = transaction;
        current.CommandText = """
            DROP TABLE IF EXISTS temp.current_media;
            CREATE TEMP TABLE current_media (relative_path TEXT PRIMARY KEY);
            """;
        await current.ExecuteNonQueryAsync(ct);

        foreach (var item in media)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO media (relative_path, parsed_title, year)
                VALUES (@path, @parsedTitle, @year)
                ON CONFLICT(relative_path) DO UPDATE SET
                    parsed_title = excluded.parsed_title,
                    year = excluded.year;
                INSERT INTO current_media (relative_path) VALUES (@path);
                """;
            command.Parameters.AddWithValue("@path", item.RelativePath);
            command.Parameters.AddWithValue("@parsedTitle", (object?)item.ParsedTitle ?? DBNull.Value);
            command.Parameters.AddWithValue("@year", (object?)item.Year ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(ct);
        }

        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM media WHERE relative_path NOT IN (SELECT relative_path FROM current_media);";
        await delete.ExecuteNonQueryAsync(ct);
        transaction.Commit();
    }

    public async Task<MediaRecord?> GetAsync(string relativePath, CancellationToken ct)
    {
        using var connection = _db.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT relative_path, parsed_title, year FROM media WHERE relative_path = @path;";
        command.Parameters.AddWithValue("@path", relativePath);

        using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new MediaRecord(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2)
        );
    }

    public async Task<bool?> IsWatchedAsync(string relativePath, CancellationToken ct)
    {
        using var connection = _db.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT position_seconds, duration_seconds
            FROM progress
            WHERE relative_path = @path;
            """;
        command.Parameters.AddWithValue("@path", relativePath);

        using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.GetDouble(1) <= 0)
            return null;

        return reader.GetDouble(0) / reader.GetDouble(1) >= 0.90;
    }

    public async Task<IReadOnlyList<FileNode>> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        using var connection = _db.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = string.IsNullOrWhiteSpace(query.Q)
            ? "SELECT relative_path, parsed_title, year FROM media ORDER BY relative_path;"
            : """
                SELECT relative_path, parsed_title, year
                FROM media
                WHERE parsed_title LIKE '%' || @q || '%' COLLATE NOCASE
                   OR relative_path LIKE '%' || @q || '%' COLLATE NOCASE
                ORDER BY relative_path;
                """;
        if (!string.IsNullOrWhiteSpace(query.Q))
            command.Parameters.AddWithValue("@q", query.Q);

        var rows = new List<(string Path, string? Title, int? Year)>();
        using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                rows.Add((
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetInt32(2)
                ));
            }
        }

        var results = new List<FileNode>(rows.Count);
        foreach (var row in rows)
        {
            var name = Path.GetFileName(row.Path);
            var extension = Path.GetExtension(row.Path);
            results.Add(
                new FileNode(
                    name,
                    FileNodeType.File,
                    row.Path,
                    string.IsNullOrEmpty(extension) ? null : extension,
                    null,
                    null,
                    row.Title,
                    row.Year,
                    await IsWatchedAsync(row.Path, ct)
                )
            );
        }

        return results;
    }

    public async Task SaveSubtitlesAsync(string relativePath, IReadOnlyList<SubtitleTrack> tracks, CancellationToken ct)
    {
        using var connection = _db.GetConnection();
        using var transaction = connection.BeginTransaction();
        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM subtitles WHERE relative_path = @path;";
        delete.Parameters.AddWithValue("@path", relativePath);
        await delete.ExecuteNonQueryAsync(ct);

        foreach (var track in tracks)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO subtitles (relative_path, track_index, label, language, format, source, external_path)
                VALUES (@path, @trackIndex, @label, @language, @format, @source, @externalPath);
                """;
            insert.Parameters.AddWithValue("@path", relativePath);
            insert.Parameters.AddWithValue("@trackIndex", (object?)track.Index ?? DBNull.Value);
            insert.Parameters.AddWithValue("@label", (object?)track.Label ?? DBNull.Value);
            insert.Parameters.AddWithValue("@language", (object?)track.Language ?? DBNull.Value);
            insert.Parameters.AddWithValue("@format", track.Format);
            insert.Parameters.AddWithValue("@source", track.Source);
            insert.Parameters.AddWithValue("@externalPath", (object?)track.ExternalPath ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(ct);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<SubtitleTrack>> GetSubtitlesAsync(string relativePath, CancellationToken ct)
    {
        using var connection = _db.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT track_index, label, language, format, source, external_path
            FROM subtitles WHERE relative_path = @path ORDER BY track_index;
            """;
        command.Parameters.AddWithValue("@path", relativePath);

        var tracks = new List<SubtitleTrack>();
        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tracks.Add(new SubtitleTrack(
                reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)
            ));
        }

        return tracks;
    }

    public async Task SaveTrickplayAsync(string relativePath, TrickplayMetadata metadata, CancellationToken ct)
    {
        using var connection = _db.GetConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO trickplay
                (relative_path, interval_ms, columns, rows, thumb_width, thumb_height, total_thumbs, sprite_path)
            VALUES (@path, @intervalMs, @columns, @rows, @thumbWidth, @thumbHeight, @totalThumbs, @spritePath);
            """;
        command.Parameters.AddWithValue("@path", relativePath);
        command.Parameters.AddWithValue("@intervalMs", metadata.IntervalMs);
        command.Parameters.AddWithValue("@columns", metadata.Columns);
        command.Parameters.AddWithValue("@rows", metadata.Rows);
        command.Parameters.AddWithValue("@thumbWidth", metadata.ThumbWidth);
        command.Parameters.AddWithValue("@thumbHeight", metadata.ThumbHeight);
        command.Parameters.AddWithValue("@totalThumbs", metadata.TotalThumbs);
        command.Parameters.AddWithValue("@spritePath", metadata.SpritePath);
        await command.ExecuteNonQueryAsync(ct);
        transaction.Commit();
    }

    public async Task<TrickplayMetadata?> GetTrickplayAsync(string relativePath, CancellationToken ct)
    {
        using var connection = _db.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT interval_ms, columns, rows, thumb_width, thumb_height, total_thumbs, sprite_path
            FROM trickplay WHERE relative_path = @path;
            """;
        command.Parameters.AddWithValue("@path", relativePath);

        using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new TrickplayMetadata(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetString(6)
            )
            : null;
    }

}
