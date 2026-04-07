namespace Reel.Server.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Reel.Server.Infrastructure.Configuration;

public sealed class DatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(ReelConfig config, ILogger<DatabaseService> logger)
    {
        _logger = logger;

        var directory = Path.Combine(config.NormalisedMediaRoot, ".reel");
        Directory.CreateDirectory(directory);

        var dbPath = Path.Combine(directory, "reel.db");
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
            _logger.LogInformation("Database migrated to version 1");
        }
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void SetUserVersion(SqliteConnection connection, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private static void RunMigrationV1(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = MigrationV1Sql;
        cmd.ExecuteNonQuery();

        transaction.Commit();

        SetUserVersion(connection, 1);
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
