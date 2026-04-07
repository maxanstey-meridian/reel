namespace Reel.Server.Infrastructure.Persistence;

using System.Text.Json;
using Microsoft.Data.Sqlite;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;

public sealed class DatabaseService
{
    private readonly string _connectionString;
    private readonly string _reelDirectory;
    private readonly ILogger<DatabaseService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public DatabaseService(ReelConfig config, ILogger<DatabaseService> logger)
    {
        _logger = logger;

        _reelDirectory = Path.Combine(config.NormalisedMediaRoot, ".reel");
        Directory.CreateDirectory(_reelDirectory);

        var dbPath = Path.Combine(_reelDirectory, "reel.db");
        _connectionString = $"Data Source={dbPath}";

        Initialise();
    }

    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var fkCmd = connection.CreateCommand();
        fkCmd.CommandText = "PRAGMA foreign_keys = ON;";
        fkCmd.ExecuteNonQuery();

        return connection;
    }

    private void Initialise()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var walCmd = connection.CreateCommand();
        walCmd.CommandText = "PRAGMA journal_mode = WAL;";
        walCmd.ExecuteNonQuery();

        using var fkCmd = connection.CreateCommand();
        fkCmd.CommandText = "PRAGMA foreign_keys = ON;";
        fkCmd.ExecuteNonQuery();

        var currentVersion = GetUserVersion(connection);

        if (currentVersion < 1)
        {
            RunMigrationV1(connection);
            MigrateJsonData(connection);
            _logger.LogInformation("Database migrated to version 1");
        }
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void RunMigrationV1(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = MigrationV1Sql;
        cmd.ExecuteNonQuery();

        using var versionCmd = connection.CreateCommand();
        versionCmd.Transaction = transaction;
        versionCmd.CommandText = "PRAGMA user_version = 1;";
        versionCmd.ExecuteNonQuery();

        transaction.Commit();
    }

    private void MigrateJsonData(SqliteConnection connection)
    {
        MigrateProgressJson(connection);
        MigrateFavouritesJson(connection);
    }

    private void MigrateProgressJson(SqliteConnection connection)
    {
        var filePath = Path.Combine(_reelDirectory, "progress.json");
        if (!File.Exists(filePath))
            return;

        var json = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            File.Delete(filePath);
            return;
        }

        var entries = JsonSerializer.Deserialize<List<WatchProgress>>(json, JsonOptions) ?? [];

        using var transaction = connection.BeginTransaction();

        foreach (var entry in entries)
        {
            using var mediaCmd = connection.CreateCommand();
            mediaCmd.Transaction = transaction;
            mediaCmd.CommandText = "INSERT OR IGNORE INTO media (relative_path) VALUES (@path);";
            mediaCmd.Parameters.AddWithValue("@path", entry.RelativePath);
            mediaCmd.ExecuteNonQuery();

            using var progressCmd = connection.CreateCommand();
            progressCmd.Transaction = transaction;
            progressCmd.CommandText = """
                INSERT OR IGNORE INTO progress (user_id, relative_path, position_seconds, duration_seconds, last_watched)
                VALUES (1, @path, @pos, @dur, @lastWatched);
                """;
            progressCmd.Parameters.AddWithValue("@path", entry.RelativePath);
            progressCmd.Parameters.AddWithValue("@pos", entry.PositionSeconds);
            progressCmd.Parameters.AddWithValue("@dur", entry.DurationSeconds);
            progressCmd.Parameters.AddWithValue("@lastWatched", entry.LastWatched.ToString("o"));
            progressCmd.ExecuteNonQuery();
        }

        transaction.Commit();
        File.Delete(filePath);

        _logger.LogInformation("Migrated {Count} progress entries from JSON to SQLite", entries.Count);
    }

    private void MigrateFavouritesJson(SqliteConnection connection)
    {
        var filePath = Path.Combine(_reelDirectory, "favourites.json");
        if (!File.Exists(filePath))
            return;

        var json = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            File.Delete(filePath);
            return;
        }

        var entries = JsonSerializer.Deserialize<List<Favourite>>(json, JsonOptions) ?? [];

        using var transaction = connection.BeginTransaction();

        foreach (var entry in entries)
        {
            using var mediaCmd = connection.CreateCommand();
            mediaCmd.Transaction = transaction;
            mediaCmd.CommandText = "INSERT OR IGNORE INTO media (relative_path) VALUES (@path);";
            mediaCmd.Parameters.AddWithValue("@path", entry.RelativePath);
            mediaCmd.ExecuteNonQuery();

            using var favCmd = connection.CreateCommand();
            favCmd.Transaction = transaction;
            favCmd.CommandText = """
                INSERT OR IGNORE INTO favourites (user_id, relative_path, added_at)
                VALUES (1, @path, @addedAt);
                """;
            favCmd.Parameters.AddWithValue("@path", entry.RelativePath);
            favCmd.Parameters.AddWithValue("@addedAt", entry.AddedAt.ToString("o"));
            favCmd.ExecuteNonQuery();
        }

        transaction.Commit();
        File.Delete(filePath);

        _logger.LogInformation("Migrated {Count} favourites entries from JSON to SQLite", entries.Count);
    }

    private const string MigrationV1Sql = """
        CREATE TABLE users (
            id   INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            icon TEXT
        );

        INSERT INTO users (name) VALUES ('Default');

        CREATE TABLE media (
            relative_path TEXT PRIMARY KEY,
            parsed_title  TEXT,
            year          INTEGER,
            genre         TEXT,
            rating        TEXT,
            language      TEXT,
            notes         TEXT,
            display_title TEXT,
            tmdb_id       INTEGER,
            poster_url    TEXT,
            backdrop_url  TEXT,
            overview      TEXT,
            release_date  TEXT,
            added_at      TEXT NOT NULL DEFAULT (datetime('now')),
            updated_at    TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE TABLE tags (
            user_id       INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            relative_path TEXT NOT NULL REFERENCES media(relative_path) ON DELETE CASCADE,
            tag           TEXT NOT NULL,
            PRIMARY KEY (user_id, relative_path, tag)
        );
        CREATE INDEX idx_tags_tag ON tags(tag);
        CREATE INDEX idx_tags_user_path ON tags(user_id, relative_path);

        CREATE TABLE progress (
            user_id          INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            relative_path    TEXT NOT NULL REFERENCES media(relative_path) ON DELETE CASCADE,
            position_seconds REAL NOT NULL,
            duration_seconds REAL NOT NULL,
            last_watched     TEXT NOT NULL,
            PRIMARY KEY (user_id, relative_path)
        );

        CREATE TABLE watch_state (
            user_id       INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            relative_path TEXT NOT NULL REFERENCES media(relative_path) ON DELETE CASCADE,
            state         TEXT NOT NULL CHECK (state IN ('watched', 'unwatched')),
            PRIMARY KEY (user_id, relative_path)
        );

        CREATE TABLE favourites (
            user_id       INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            relative_path TEXT NOT NULL REFERENCES media(relative_path) ON DELETE CASCADE,
            added_at      TEXT NOT NULL,
            PRIMARY KEY (user_id, relative_path)
        );

        CREATE TABLE subtitles (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            relative_path TEXT NOT NULL REFERENCES media(relative_path) ON DELETE CASCADE,
            track_index   INTEGER,
            label         TEXT,
            language      TEXT,
            format        TEXT NOT NULL,
            source        TEXT NOT NULL,
            external_path TEXT
        );
        CREATE INDEX idx_subtitles_path ON subtitles(relative_path);

        CREATE TABLE trickplay (
            relative_path TEXT PRIMARY KEY REFERENCES media(relative_path) ON DELETE CASCADE,
            interval_ms   INTEGER NOT NULL,
            columns       INTEGER NOT NULL,
            rows          INTEGER NOT NULL,
            thumb_width   INTEGER NOT NULL,
            thumb_height  INTEGER NOT NULL,
            total_thumbs  INTEGER NOT NULL,
            sprite_path   TEXT NOT NULL
        );
        """;
}
