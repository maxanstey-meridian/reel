using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Persistence;

namespace Reel.Server.Tests;

public sealed class DatabaseServiceTests : IDisposable
{
    private readonly string _tempMediaRoot;
    private readonly ReelConfig _config;

    public DatabaseServiceTests()
    {
        _tempMediaRoot = Path.Combine(Path.GetTempPath(), $"reel-db-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempMediaRoot);

        _config = new ReelConfig
        {
            MediaRoot = _tempMediaRoot,
            SecretKey = "test",
            Port = 8080
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempMediaRoot))
            Directory.Delete(_tempMediaRoot, recursive: true);
    }

    [Fact]
    public void CreatesDbFile()
    {
        _ = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        var dbPath = Path.Combine(_tempMediaRoot, ".reel", "reel.db");
        File.Exists(dbPath).Should().BeTrue();
    }

    [Fact]
    public void CreatesAllEightTables()
    {
        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        using var conn = svc.GetConnection();
        var tables = GetTableNames(conn);

        tables.Should().BeEquivalentTo([
            "users", "media", "tags", "progress",
            "watch_state", "favourites", "subtitles", "trickplay"
        ]);
    }

    [Fact]
    public void SetsWalMode()
    {
        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        using var conn = svc.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var mode = cmd.ExecuteScalar()?.ToString();

        mode.Should().Be("wal");
    }

    [Fact]
    public void EnablesForeignKeysOnGetConnection()
    {
        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        using var conn = svc.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";
        var result = Convert.ToInt32(cmd.ExecuteScalar());

        result.Should().Be(1);
    }

    [Fact]
    public void SetsUserVersionToOne()
    {
        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        using var conn = svc.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(cmd.ExecuteScalar());

        version.Should().Be(1);
    }

    [Fact]
    public void SeedsDefaultUserWithIdOne()
    {
        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        using var conn = svc.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM users;";
        using var reader = cmd.ExecuteReader();

        reader.Read().Should().BeTrue();
        reader.GetInt32(0).Should().Be(1);
        reader.GetString(1).Should().Be("Default");
        reader.Read().Should().BeFalse("there should be exactly one user");
    }

    [Fact]
    public void IdempotentOnSecondConstruction()
    {
        _ = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);
        var svc2 = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        using var conn = svc2.GetConnection();

        using var versionCmd = conn.CreateCommand();
        versionCmd.CommandText = "PRAGMA user_version;";
        Convert.ToInt32(versionCmd.ExecuteScalar()).Should().Be(1);

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM users;";
        Convert.ToInt32(countCmd.ExecuteScalar()).Should().Be(1, "default user should not be duplicated");
    }

    [Fact]
    public void GetConnectionReturnsOpenConnection()
    {
        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        using var conn = svc.GetConnection();

        conn.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public void ForeignKeyConstraintsEnforced()
    {
        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        using var conn = svc.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO progress (user_id, relative_path, position_seconds, duration_seconds, last_watched)
            VALUES (999, 'nonexistent.mp4', 0, 0, '2024-01-01');
            """;

        var act = () => cmd.ExecuteNonQuery();
        act.Should().Throw<SqliteException>();
    }

    private static List<string> GetTableNames(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        using var reader = cmd.ExecuteReader();

        var tables = new List<string>();
        while (reader.Read())
            tables.Add(reader.GetString(0));
        return tables;
    }
}
