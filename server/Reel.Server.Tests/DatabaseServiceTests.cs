using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Reel.Server.Storage;

namespace Reel.Server.Tests;

public sealed class DatabaseServiceTests : IDisposable
{
    private const int LatestVersion = 3;
    private readonly TempMediaRoot _media = new();

    public void Dispose() => _media.Dispose();

    [Fact]
    public async Task FreshDatabase_MigratesToTheLatestSchemaWithWalAndEnforcedForeignKeys()
    {
        var database = new DatabaseService(_media.Paths);

        await database.MigrateAsync();
        await database.MigrateAsync();

        using var connection = database.GetConnection();
        connection.ExecuteScalar<int>("PRAGMA user_version;").Should().Be(LatestVersion);
        TableNames(connection)
            .Should()
            .BeEquivalentTo("media", "progress", "favourites", "item_tags");
        connection
            .Query<string>("SELECT name FROM pragma_table_info('media');")
            .Should()
            .Equal("relative_path", "parsed_title", "year", "size_bytes", "indexed_at");
        connection.ExecuteScalar<string>("PRAGMA journal_mode;").Should().Be("wal");
        var orphan = () =>
            connection.Execute("INSERT INTO progress VALUES ('missing.mp4', 0, 0, '2026-01-01');");
        orphan.Should().Throw<SqliteException>();
    }

    // The first reel-v2.db release stamped user_version 1 and still had the cache tables but no tags.
    [Fact]
    public async Task FirstReleaseDatabase_MigratesInPlaceAndKeepsProgressAndFavourites()
    {
        CreateExisting(
            """
            CREATE TABLE media (relative_path TEXT PRIMARY KEY, parsed_title TEXT, year INTEGER);
            CREATE TABLE progress (relative_path TEXT PRIMARY KEY REFERENCES media(relative_path) ON DELETE CASCADE,
                position_seconds REAL NOT NULL, duration_seconds REAL NOT NULL, last_watched TEXT NOT NULL);
            CREATE TABLE favourites (relative_path TEXT PRIMARY KEY REFERENCES media(relative_path) ON DELETE CASCADE,
                added_at TEXT NOT NULL);
            CREATE TABLE subtitles (id INTEGER PRIMARY KEY, relative_path TEXT REFERENCES media(relative_path));
            CREATE TABLE trickplay (relative_path TEXT PRIMARY KEY REFERENCES media(relative_path));
            INSERT INTO media VALUES ('a.mp4', 'A', NULL);
            INSERT INTO progress VALUES ('a.mp4', 10, 100, '2026-01-01T00:00:00.0000000Z');
            INSERT INTO favourites VALUES ('a.mp4', '2026-01-01T00:00:00.0000000Z');
            INSERT INTO subtitles VALUES (1, 'a.mp4');
            PRAGMA user_version = 1;
            """
        );

        await AssertMigratedKeepingUserDataAsync();
    }

    // Builds between that release and versioned migrations created tags but left user_version at 0.
    [Fact]
    public async Task UnversionedDatabase_AdoptsTheSchemaAndKeepsProgressFavouritesAndTags()
    {
        CreateExisting(
            """
            CREATE TABLE item_tags (relative_path TEXT PRIMARY KEY, tags_json TEXT NOT NULL);
            CREATE TABLE media (relative_path TEXT PRIMARY KEY, parsed_title TEXT, year INTEGER);
            CREATE TABLE progress (relative_path TEXT PRIMARY KEY REFERENCES media(relative_path) ON DELETE CASCADE,
                position_seconds REAL NOT NULL, duration_seconds REAL NOT NULL, last_watched TEXT NOT NULL);
            CREATE TABLE favourites (relative_path TEXT PRIMARY KEY REFERENCES media(relative_path) ON DELETE CASCADE,
                added_at TEXT NOT NULL);
            INSERT INTO media VALUES ('a.mp4', 'A', NULL);
            INSERT INTO progress VALUES ('a.mp4', 10, 100, '2026-01-01T00:00:00.0000000Z');
            INSERT INTO favourites VALUES ('a.mp4', '2026-01-01T00:00:00.0000000Z');
            INSERT INTO item_tags VALUES ('a.mp4', '["comedy"]');
            """
        );

        await AssertMigratedKeepingUserDataAsync();
        using var connection = _media.Database.GetConnection();
        connection
            .ExecuteScalar<string>("SELECT tags_json FROM item_tags;")
            .Should()
            .Be("""["comedy"]""");
    }

    private void CreateExisting(string sql)
    {
        var path = _media.Paths.AppDataPath("reel-v2.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Execute(sql);
    }

    private async Task AssertMigratedKeepingUserDataAsync()
    {
        await _media.Database.MigrateAsync();

        using var connection = _media.Database.GetConnection();
        connection.ExecuteScalar<int>("PRAGMA user_version;").Should().Be(LatestVersion);
        TableNames(connection)
            .Should()
            .BeEquivalentTo("media", "progress", "favourites", "item_tags");
        connection
            .QuerySingle<(string, string?, long?)>(
                "SELECT relative_path, parsed_title, size_bytes FROM media;"
            )
            .Should()
            .Be(("a.mp4", "A", null));
        connection.ExecuteScalar<int>("SELECT COUNT(*) FROM progress;").Should().Be(1);
        connection.ExecuteScalar<int>("SELECT COUNT(*) FROM favourites;").Should().Be(1);
    }

    private static IEnumerable<string> TableNames(SqliteConnection connection) =>
        connection.Query<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';"
        );
}
