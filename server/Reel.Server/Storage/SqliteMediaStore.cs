using System.Text.Json;
using Dapper;

namespace Reel.Server.Storage;

public sealed class SqliteMediaStore(DatabaseService db, TimeProvider time)
{
    private const string Columns = "relative_path, parsed_title, year, size_bytes";

    // Dapper converts SQLite's 64-bit integers to int? when setting properties, not when calling a constructor.
    private readonly record struct Row(
        string RelativePath,
        string? ParsedTitle,
        int? Year,
        long? SizeBytes
    );

    /// <summary>
    /// Makes the index match a completed scan that started at scanStartedAt. Rows indexed after that (by playback
    /// or favouriting a file the scan had already passed) are kept, so their progress and favourites survive.
    /// </summary>
    public async Task ReconcileAsync(IReadOnlyCollection<MediaRecord> media, DateTime scanStartedAt)
    {
        using var connection = db.GetConnection();
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(
            """
            DROP TABLE IF EXISTS temp.current_media;
            CREATE TEMP TABLE current_media (relative_path TEXT PRIMARY KEY);
            """,
            transaction: transaction
        );
        await connection.ExecuteAsync(
            """
            INSERT INTO media (relative_path, parsed_title, year, size_bytes, indexed_at)
            VALUES (@RelativePath, @ParsedTitle, @Year, @SizeBytes, @scanStartedAt)
            ON CONFLICT(relative_path) DO UPDATE SET
                parsed_title = excluded.parsed_title,
                year = excluded.year,
                size_bytes = excluded.size_bytes,
                indexed_at = excluded.indexed_at;
            INSERT INTO current_media (relative_path) VALUES (@RelativePath);
            """,
            media.Select(record => new
            {
                record.RelativePath,
                record.ParsedTitle,
                record.Year,
                record.SizeBytes,
                scanStartedAt,
            }),
            transaction
        );
        await connection.ExecuteAsync(
            """
            DELETE FROM media
            WHERE relative_path NOT IN (SELECT relative_path FROM current_media)
              AND (indexed_at IS NULL OR indexed_at < @scanStartedAt);
            """,
            new { scanStartedAt },
            transaction
        );
        // Tags on a file or folder that no longer holds any media would otherwise outlive it.
        await connection.ExecuteAsync(
            """
            DELETE FROM item_tags
            WHERE NOT EXISTS (
                SELECT 1 FROM media
                WHERE media.relative_path = item_tags.relative_path
                   OR substr(media.relative_path, 1, length(item_tags.relative_path) + 1)
                      = item_tags.relative_path || '/');
            """,
            transaction: transaction
        );
        transaction.Commit();
    }

    /// <summary>Indexes a file added since the last scan; an existing row keeps its scanned metadata.</summary>
    public async Task EnsureIndexedAsync(MediaRecord media)
    {
        using var connection = db.GetConnection();
        await connection.ExecuteAsync(
            """
            INSERT OR IGNORE INTO media (relative_path, parsed_title, year, size_bytes, indexed_at)
            VALUES (@RelativePath, @ParsedTitle, @Year, @SizeBytes, @indexedAt);
            """,
            new
            {
                media.RelativePath,
                media.ParsedTitle,
                media.Year,
                media.SizeBytes,
                indexedAt = time.GetUtcNow().UtcDateTime,
            }
        );
    }

    public async Task<bool> HasMediaAsync()
    {
        using var connection = db.GetConnection();
        return await connection.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM media);");
    }

    /// <summary>Indexed media matching a title, path or #tag query (all media when blank), in library order.</summary>
    public async Task<MediaRecord[]> SearchAsync(string? query)
    {
        using var connection = db.GetConnection();
        // Plain text searches titles and paths; "#tag" searches tags, where untagged media counts as #media.
        var rows = string.IsNullOrWhiteSpace(query)
            ? await connection.QueryAsync<Row>($"SELECT {Columns} FROM media;")
            : await connection.QueryAsync<Row>(
                $"""
                SELECT {Columns}
                FROM media
                WHERE (@tagsOnly = 0 AND (
                    parsed_title LIKE '%' || @like || '%' ESCAPE '\'
                    OR relative_path LIKE '%' || @like || '%' ESCAPE '\'))
                   OR (@tagsOnly = 1 AND (
                       EXISTS (
                           SELECT 1 FROM item_tags t, json_each(t.tags_json) tag
                           WHERE (t.relative_path = media.relative_path
                               OR substr(media.relative_path, 1, length(t.relative_path) + 1) = t.relative_path || '/')
                             AND instr(lower(tag.value), lower(@tag)) > 0)
                       OR (instr('media', lower(@tag)) > 0 AND NOT EXISTS (
                           SELECT 1 FROM item_tags t WHERE t.relative_path = media.relative_path))));
                """,
                new
                {
                    // Searched text is literal: % and _ are LIKE wildcards otherwise.
                    like = query.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_"),
                    tag = query.Trim().TrimStart('#'),
                    tagsOnly = query.TrimStart().StartsWith('#'),
                }
            );
        return
        [
            .. rows.Select(row => new MediaRecord(
                    row.RelativePath,
                    row.ParsedTitle,
                    row.Year,
                    row.SizeBytes
                ))
                .OrderBy(media => media.RelativePath, MediaPathResolver.NaturalOrder),
        ];
    }

    /// <summary>Tags by library path (a file or a folder whose tags its contents inherit).</summary>
    public async Task<Dictionary<string, string[]>> GetTagsAsync()
    {
        using var connection = db.GetConnection();
        var rows = await connection.QueryAsync<(string Path, string Json)>(
            "SELECT relative_path, tags_json FROM item_tags;"
        );
        return rows.ToDictionary(
            row => row.Path,
            row => JsonSerializer.Deserialize<string[]>(row.Json)!
        );
    }

    public async Task SetTagsAsync(string relativePath, string[] tags)
    {
        using var connection = db.GetConnection();
        await connection.ExecuteAsync(
            """
            INSERT INTO item_tags VALUES (@relativePath, @tags)
            ON CONFLICT(relative_path) DO UPDATE SET tags_json = excluded.tags_json;
            """,
            new { relativePath, tags = JsonSerializer.Serialize(tags) }
        );
    }
}
