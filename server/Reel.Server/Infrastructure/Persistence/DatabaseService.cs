namespace Reel.Server.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Reel.Server.Infrastructure.Configuration;

public sealed class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(ReelConfig config, ILogger<DatabaseService>? logger = null)
    {
        var reelDirectory = Path.Combine(config.NormalisedMediaRoot, ".reel");
        Directory.CreateDirectory(reelDirectory);

        _connectionString = $"Data Source={Path.Combine(reelDirectory, "reel-v2.db")}";
        Initialise();
    }

    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();

        return connection;
    }

    private void Initialise()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = SchemaSql;
        command.ExecuteNonQuery();
    }

    private const string SchemaSql = """
        PRAGMA journal_mode = WAL;
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS media (
            relative_path TEXT PRIMARY KEY,
            parsed_title  TEXT,
            year          INTEGER
        );

        CREATE TABLE IF NOT EXISTS progress (
            relative_path    TEXT PRIMARY KEY REFERENCES media(relative_path) ON DELETE CASCADE,
            position_seconds REAL NOT NULL,
            duration_seconds REAL NOT NULL,
            last_watched     TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS favourites (
            relative_path TEXT PRIMARY KEY REFERENCES media(relative_path) ON DELETE CASCADE,
            added_at      TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS subtitles (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            relative_path TEXT NOT NULL REFERENCES media(relative_path) ON DELETE CASCADE,
            track_index   INTEGER,
            label         TEXT,
            language      TEXT,
            format        TEXT NOT NULL,
            source        TEXT NOT NULL,
            external_path TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_subtitles_path ON subtitles(relative_path);

        CREATE TABLE IF NOT EXISTS trickplay (
            relative_path TEXT PRIMARY KEY REFERENCES media(relative_path) ON DELETE CASCADE,
            interval_ms   INTEGER NOT NULL,
            columns       INTEGER NOT NULL,
            rows          INTEGER NOT NULL,
            thumb_width   INTEGER NOT NULL,
            thumb_height  INTEGER NOT NULL,
            total_thumbs  INTEGER NOT NULL,
            sprite_path   TEXT NOT NULL
        );

        PRAGMA user_version = 1;
        """;
}
