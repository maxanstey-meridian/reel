namespace Reel.Server.Infrastructure.Persistence;

using System.Globalization;
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

    public async Task SetWatchStateAsync(int userId, string relativePath, WatchState? state, CancellationToken ct)
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
            var dbState = state == WatchState.Watched ? "watched" : "unwatched";
            using var upsertCmd = conn.CreateCommand();
            upsertCmd.CommandText = """
                INSERT OR REPLACE INTO watch_state (user_id, relative_path, state)
                VALUES (@userId, @path, @state);
                """;
            upsertCmd.Parameters.AddWithValue("@userId", userId);
            upsertCmd.Parameters.AddWithValue("@path", relativePath);
            upsertCmd.Parameters.AddWithValue("@state", dbState);
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

    public async Task UpdateMetaAsync(string relativePath, MediaUpdateRequest update, CancellationToken ct)
    {
        var setClauses = new List<string>();
        var parameters = new List<(string Name, object Value)>();

        AddField(update.Genre, "genre", "@genre", setClauses, parameters);
        AddField(update.Rating, "rating", "@rating", setClauses, parameters);
        AddField(update.Language, "language", "@language", setClauses, parameters);
        AddField(update.Notes, "notes", "@notes", setClauses, parameters);
        AddField(update.DisplayTitle, "display_title", "@displayTitle", setClauses, parameters);

        if (setClauses.Count == 0)
            return;

        setClauses.Add("updated_at = datetime('now')");

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE media SET {string.Join(", ", setClauses)} WHERE relative_path = @path;";
        cmd.Parameters.AddWithValue("@path", relativePath);

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // null = skip (not provided), "" = clear to NULL, other = set value
    private static void AddField(
        string? value,
        string column,
        string paramName,
        List<string> setClauses,
        List<(string Name, object Value)> parameters
    )
    {
        if (value is null)
            return;

        setClauses.Add($"{column} = {paramName}");
        parameters.Add((paramName, value.Length == 0 ? DBNull.Value : value));
    }

    public async Task UpdateTmdbCacheAsync(
        string relativePath,
        int tmdbId,
        string? posterUrl,
        string? backdropUrl,
        string? overview,
        string? releaseDate,
        CancellationToken ct
    )
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE media
            SET tmdb_id = @tmdbId, poster_url = @posterUrl, backdrop_url = @backdropUrl,
                overview = @overview, release_date = @releaseDate, updated_at = datetime('now')
            WHERE relative_path = @path;
            """;
        cmd.Parameters.AddWithValue("@path", relativePath);
        cmd.Parameters.AddWithValue("@tmdbId", tmdbId);
        cmd.Parameters.AddWithValue("@posterUrl", (object?)posterUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@backdropUrl", (object?)backdropUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@overview", (object?)overview ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@releaseDate", (object?)releaseDate ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SetTagsAsync(int userId, string relativePath, IReadOnlyList<string> tags, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        using var transaction = conn.BeginTransaction();

        // Ensure media row exists
        using var mediaCmd = conn.CreateCommand();
        mediaCmd.Transaction = transaction;
        mediaCmd.CommandText = "INSERT OR IGNORE INTO media (relative_path) VALUES (@path);";
        mediaCmd.Parameters.AddWithValue("@path", relativePath);
        await mediaCmd.ExecuteNonQueryAsync(ct);

        // Delete existing tags for this user+path
        using var deleteCmd = conn.CreateCommand();
        deleteCmd.Transaction = transaction;
        deleteCmd.CommandText = "DELETE FROM tags WHERE user_id = @userId AND relative_path = @path;";
        deleteCmd.Parameters.AddWithValue("@userId", userId);
        deleteCmd.Parameters.AddWithValue("@path", relativePath);
        await deleteCmd.ExecuteNonQueryAsync(ct);

        // Insert new tags
        foreach (var tag in tags)
        {
            using var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = """
                INSERT OR IGNORE INTO tags (user_id, relative_path, tag)
                VALUES (@userId, @path, @tag);
                """;
            insertCmd.Parameters.AddWithValue("@userId", userId);
            insertCmd.Parameters.AddWithValue("@path", relativePath);
            insertCmd.Parameters.AddWithValue("@tag", tag);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }

        transaction.Commit();
    }

    public async Task AddTagAsync(int userId, string relativePath, string tag, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        using var transaction = conn.BeginTransaction();

        // Ensure media row exists
        using var mediaCmd = conn.CreateCommand();
        mediaCmd.Transaction = transaction;
        mediaCmd.CommandText = "INSERT OR IGNORE INTO media (relative_path) VALUES (@path);";
        mediaCmd.Parameters.AddWithValue("@path", relativePath);
        await mediaCmd.ExecuteNonQueryAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT OR IGNORE INTO tags (user_id, relative_path, tag)
            VALUES (@userId, @path, @tag);
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@path", relativePath);
        cmd.Parameters.AddWithValue("@tag", tag);
        await cmd.ExecuteNonQueryAsync(ct);

        transaction.Commit();
    }

    public async Task RemoveTagAsync(int userId, string relativePath, string tag, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM tags
            WHERE user_id = @userId AND relative_path = @path AND tag = @tag;
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@path", relativePath);
        cmd.Parameters.AddWithValue("@tag", tag);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetTagsAsync(int userId, string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tag FROM tags
            WHERE user_id = @userId AND relative_path = @path
            ORDER BY tag;
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@path", relativePath);

        var tags = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            tags.Add(reader.GetString(0));

        return tags;
    }

    public async Task<IReadOnlyList<TagCount>> GetAllTagsAsync(int userId, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tag, COUNT(*) as count FROM tags
            WHERE user_id = @userId
            GROUP BY tag
            ORDER BY tag;
            """;
        cmd.Parameters.AddWithValue("@userId", userId);

        var results = new List<TagCount>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(new TagCount(reader.GetString(0), reader.GetInt32(1)));

        return results;
    }

    public async Task<IReadOnlyList<FileNode>> SearchAsync(int userId, SearchQuery query, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        var whereClauses = new List<string>();
        var parameters = new List<(string Name, string Value)>();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            whereClauses.Add(
                "(parsed_title LIKE '%' || @q || '%' COLLATE NOCASE OR display_title LIKE '%' || @q || '%' COLLATE NOCASE)"
            );
            parameters.Add(("@q", query.Q));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            whereClauses.Add(
                "EXISTS (SELECT 1 FROM tags WHERE tags.relative_path = media.relative_path AND tags.user_id = @userId AND tags.tag = @tag)"
            );
            parameters.Add(("@tag", query.Tag));
        }

        if (!string.IsNullOrWhiteSpace(query.Genre))
        {
            whereClauses.Add("genre = @genre");
            parameters.Add(("@genre", query.Genre));
        }

        var sql = "SELECT relative_path, parsed_title, year, display_title FROM media";
        if (whereClauses.Count > 0)
            sql += " WHERE " + string.Join(" AND ", whereClauses);
        sql += " ORDER BY relative_path;";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@userId", userId);
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);

        var candidates = new List<(string RelativePath, string? ParsedTitle, int? Year, string? DbDisplayTitle)>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            candidates.Add((
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)
            ));
        }

        var results = new List<FileNode>();
        foreach (var (relativePath, parsedTitle, year, dbDisplayTitle) in candidates)
        {
            var watched = await IsWatchedAsync(userId, relativePath, ct);

            if (query.Watched is not null)
            {
                var isWatched = watched == true;
                if (query.Watched.Value != isWatched)
                    continue;
            }

            var name = Path.GetFileName(relativePath);
            var extension = Path.GetExtension(relativePath);
            var displayTitle = dbDisplayTitle ?? parsedTitle ?? name;

            results.Add(new FileNode(
                name,
                FileNodeType.File,
                relativePath,
                string.IsNullOrEmpty(extension) ? null : extension,
                SizeBytes: null,
                Children: null,
                parsedTitle,
                year,
                ThumbnailUrl: null,
                watched,
                displayTitle
            ));
        }

        return results;
    }

    public async Task<string?> GetDisplayTitleAsync(string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT display_title FROM media WHERE relative_path = @path;";
        cmd.Parameters.AddWithValue("@path", relativePath);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string displayTitle ? displayTitle : null;
    }

    public async Task SaveSubtitlesAsync(string relativePath, IReadOnlyList<SubtitleTrack> tracks, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        using var transaction = conn.BeginTransaction();

        // Ensure media row exists
        using var mediaCmd = conn.CreateCommand();
        mediaCmd.Transaction = transaction;
        mediaCmd.CommandText = "INSERT OR IGNORE INTO media (relative_path) VALUES (@path);";
        mediaCmd.Parameters.AddWithValue("@path", relativePath);
        await mediaCmd.ExecuteNonQueryAsync(ct);

        // Delete existing subtitles for this path
        using var deleteCmd = conn.CreateCommand();
        deleteCmd.Transaction = transaction;
        deleteCmd.CommandText = "DELETE FROM subtitles WHERE relative_path = @path;";
        deleteCmd.Parameters.AddWithValue("@path", relativePath);
        await deleteCmd.ExecuteNonQueryAsync(ct);

        // Insert new tracks
        foreach (var track in tracks)
        {
            using var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = """
                INSERT INTO subtitles (relative_path, track_index, label, language, format, source, external_path)
                VALUES (@path, @trackIndex, @label, @language, @format, @source, @externalPath);
                """;
            insertCmd.Parameters.AddWithValue("@path", relativePath);
            insertCmd.Parameters.AddWithValue("@trackIndex", (object?)track.Index ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@label", (object?)track.Label ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@language", (object?)track.Language ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@format", track.Format);
            insertCmd.Parameters.AddWithValue("@source", track.Source);
            insertCmd.Parameters.AddWithValue("@externalPath", (object?)track.ExternalPath ?? DBNull.Value);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<SubtitleTrack>> GetSubtitlesAsync(string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT track_index, label, language, format, source, external_path
            FROM subtitles
            WHERE relative_path = @path
            ORDER BY track_index;
            """;
        cmd.Parameters.AddWithValue("@path", relativePath);

        var tracks = new List<SubtitleTrack>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
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
        using var conn = _db.GetConnection();
        using var transaction = conn.BeginTransaction();

        // Ensure media row exists
        using var mediaCmd = conn.CreateCommand();
        mediaCmd.Transaction = transaction;
        mediaCmd.CommandText = "INSERT OR IGNORE INTO media (relative_path) VALUES (@path);";
        mediaCmd.Parameters.AddWithValue("@path", relativePath);
        await mediaCmd.ExecuteNonQueryAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT OR REPLACE INTO trickplay (relative_path, interval_ms, columns, rows, thumb_width, thumb_height, total_thumbs, sprite_path)
            VALUES (@path, @intervalMs, @columns, @rows, @thumbWidth, @thumbHeight, @totalThumbs, @spritePath);
            """;
        cmd.Parameters.AddWithValue("@path", relativePath);
        cmd.Parameters.AddWithValue("@intervalMs", metadata.IntervalMs);
        cmd.Parameters.AddWithValue("@columns", metadata.Columns);
        cmd.Parameters.AddWithValue("@rows", metadata.Rows);
        cmd.Parameters.AddWithValue("@thumbWidth", metadata.ThumbWidth);
        cmd.Parameters.AddWithValue("@thumbHeight", metadata.ThumbHeight);
        cmd.Parameters.AddWithValue("@totalThumbs", metadata.TotalThumbs);
        cmd.Parameters.AddWithValue("@spritePath", metadata.SpritePath);
        await cmd.ExecuteNonQueryAsync(ct);

        transaction.Commit();
    }

    public async Task<TrickplayMetadata?> GetTrickplayAsync(string relativePath, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT interval_ms, columns, rows, thumb_width, thumb_height, total_thumbs, sprite_path
            FROM trickplay
            WHERE relative_path = @path;
            """;
        cmd.Parameters.AddWithValue("@path", relativePath);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new TrickplayMetadata(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetString(6)
        );
    }
}
