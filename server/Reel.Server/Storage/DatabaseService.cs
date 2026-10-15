using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Reel.Server.Storage;

public sealed class DatabaseService
{
    private readonly string _connectionString;

    static DatabaseService()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        // Dapper's built-in DateTime mapping would otherwise bypass the handler when writing parameters.
        SqlMapper.RemoveTypeMap(typeof(DateTime));
        SqlMapper.RemoveTypeMap(typeof(DateTime?));
        SqlMapper.AddTypeHandler(new RoundTripDateTimeHandler());
    }

    public DatabaseService(MediaPathResolver paths)
    {
        var databasePath = paths.AppDataPath("reel-v2.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();
    }

    /// <summary>Brings the database up to the latest schema; runs once at startup before anything else uses it.</summary>
    public async Task MigrateAsync()
    {
        using var connection = GetConnection();
        await connection.ExecuteAsync("PRAGMA journal_mode = WAL;");
        var version = await connection.ExecuteScalarAsync<int>("PRAGMA user_version;");
        for (var next = version; next < Migrations.Length; next++)
        {
            using var transaction = connection.BeginTransaction();
            await connection.ExecuteAsync(Migrations[next], transaction: transaction);
            await connection.ExecuteAsync(
                $"PRAGMA user_version = {next + 1};",
                transaction: transaction
            );
            transaction.Commit();
        }
    }

    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void Ping()
    {
        using var connection = GetConnection();
        connection.ExecuteScalar("SELECT 1;");
    }

    // Timestamps are stored as round-trip ("o") UTC text, which also sorts and compares chronologically as text.
    private sealed class RoundTripDateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override DateTime Parse(object value) =>
            DateTime.Parse(
                (string)value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind
            );

        public override void SetValue(IDbDataParameter parameter, DateTime value) =>
            parameter.Value = value.ToString("o");
    }

    // Applied in order; user_version records how many have run. Append new steps, never edit shipped ones.
    // Version 1 is the first reel-v2.db schema, which existing databases already stamp as user_version 1;
    // CREATE IF NOT EXISTS lets unstamped (version 0) databases with the same tables adopt it.
    private static readonly string[] Migrations =
    [
        """
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
            """,
        // Tags arrive; subtitle and trickplay caches moved out of the database into .reel files.
        """
            CREATE TABLE IF NOT EXISTS item_tags (relative_path TEXT PRIMARY KEY, tags_json TEXT NOT NULL);
            DROP TABLE IF EXISTS subtitles;
            DROP TABLE IF EXISTS trickplay;
            """,
        // The library tree is served from the index (size_bytes); reconcile keeps rows indexed during a scan.
        """
            ALTER TABLE media ADD COLUMN size_bytes INTEGER;
            ALTER TABLE media ADD COLUMN indexed_at TEXT;
            """,
    ];
}
