using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class DatabaseServiceTests : IDisposable
{
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-db-test-{Guid.NewGuid():N}");

    public DatabaseServiceTests()
    {
        Directory.CreateDirectory(_mediaRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, true);
    }

    [Fact]
    public void CreatesFreshCurrentSchema()
    {
        var database = CreateDatabase();

        using var connection = database.GetConnection();
        GetTableNames(connection).Should().BeEquivalentTo(["media", "progress", "favourites", "subtitles", "trickplay"]);

        using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        Convert.ToInt32(version.ExecuteScalar()).Should().Be(1);
    }

    [Fact]
    public void EnablesWalAndForeignKeys()
    {
        var database = CreateDatabase();
        using var connection = database.GetConnection();

        using var journal = connection.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode;";
        journal.ExecuteScalar().Should().Be("wal");

        using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys;";
        Convert.ToInt32(foreignKeys.ExecuteScalar()).Should().Be(1);
        connection.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public void PathForeignKeysAreEnforced()
    {
        var database = CreateDatabase();
        using var connection = database.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO progress (relative_path, position_seconds, duration_seconds, last_watched)
            VALUES ('missing.mp4', 0, 0, '2026-01-01');
            """;

        var act = () => command.ExecuteNonQuery();
        act.Should().Throw<SqliteException>();
    }

    private DatabaseService CreateDatabase() =>
        new(
            new ReelConfig { MediaRoot = _mediaRoot, SecretKey = "test", Port = 8080 },
            NullLogger<DatabaseService>.Instance
        );

    private static List<string> GetTableNames(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        using var reader = command.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
            tables.Add(reader.GetString(0));
        return tables;
    }
}
