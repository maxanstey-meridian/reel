using System.Data;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Domain;
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

    [Fact]
    public void MigratesProgressJson()
    {
        var reelDir = Path.Combine(_tempMediaRoot, ".reel");
        Directory.CreateDirectory(reelDir);

        var entries = new List<WatchProgress>
        {
            new("Films/movie.mp4", 120.5, 7200.0, new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)),
            new("Shows/ep1.mkv", 300.0, 2400.0, new DateTime(2025, 2, 20, 14, 0, 0, DateTimeKind.Utc))
        };
        WriteJson(Path.Combine(reelDir, "progress.json"), entries);

        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        File.Exists(Path.Combine(reelDir, "progress.json")).Should().BeFalse("JSON file should be deleted after migration");

        using var conn = svc.GetConnection();

        using var progressCmd = conn.CreateCommand();
        progressCmd.CommandText = "SELECT relative_path, position_seconds, duration_seconds, user_id FROM progress ORDER BY relative_path;";
        using var reader = progressCmd.ExecuteReader();

        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("Films/movie.mp4");
        reader.GetDouble(1).Should().Be(120.5);
        reader.GetDouble(2).Should().Be(7200.0);
        reader.GetInt32(3).Should().Be(1);

        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("Shows/ep1.mkv");

        reader.Read().Should().BeFalse();

        using var mediaCmd = conn.CreateCommand();
        mediaCmd.CommandText = "SELECT COUNT(*) FROM media;";
        Convert.ToInt32(mediaCmd.ExecuteScalar()).Should().Be(2, "media rows should be created for each progress entry");
    }

    [Fact]
    public void MigratesFavouritesJson()
    {
        var reelDir = Path.Combine(_tempMediaRoot, ".reel");
        Directory.CreateDirectory(reelDir);

        var entries = new List<Favourite>
        {
            new("Films/fav1.mp4", new DateTime(2025, 3, 10, 8, 0, 0, DateTimeKind.Utc)),
            new("Films/fav2.mkv", new DateTime(2025, 3, 11, 9, 0, 0, DateTimeKind.Utc))
        };
        WriteJson(Path.Combine(reelDir, "favourites.json"), entries);

        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        File.Exists(Path.Combine(reelDir, "favourites.json")).Should().BeFalse("JSON file should be deleted after migration");

        using var conn = svc.GetConnection();

        using var favCmd = conn.CreateCommand();
        favCmd.CommandText = "SELECT relative_path, user_id FROM favourites ORDER BY relative_path;";
        using var reader = favCmd.ExecuteReader();

        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("Films/fav1.mp4");
        reader.GetInt32(1).Should().Be(1);

        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("Films/fav2.mkv");

        reader.Read().Should().BeFalse();
    }

    [Fact]
    public void MigratesBothJsonFiles()
    {
        var reelDir = Path.Combine(_tempMediaRoot, ".reel");
        Directory.CreateDirectory(reelDir);

        WriteJson(Path.Combine(reelDir, "progress.json"), new List<WatchProgress>
        {
            new("Films/shared.mp4", 60.0, 3600.0, DateTime.UtcNow)
        });
        WriteJson(Path.Combine(reelDir, "favourites.json"), new List<Favourite>
        {
            new("Films/shared.mp4", DateTime.UtcNow)
        });

        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        using var conn = svc.GetConnection();

        using var mediaCmd = conn.CreateCommand();
        mediaCmd.CommandText = "SELECT COUNT(*) FROM media;";
        Convert.ToInt32(mediaCmd.ExecuteScalar()).Should().Be(1, "same path should produce one media row");

        using var progressCmd = conn.CreateCommand();
        progressCmd.CommandText = "SELECT COUNT(*) FROM progress;";
        Convert.ToInt32(progressCmd.ExecuteScalar()).Should().Be(1);

        using var favCmd = conn.CreateCommand();
        favCmd.CommandText = "SELECT COUNT(*) FROM favourites;";
        Convert.ToInt32(favCmd.ExecuteScalar()).Should().Be(1);
    }

    [Fact]
    public void HandlesEmptyJsonFiles()
    {
        var reelDir = Path.Combine(_tempMediaRoot, ".reel");
        Directory.CreateDirectory(reelDir);

        File.WriteAllText(Path.Combine(reelDir, "progress.json"), "[]");
        File.WriteAllText(Path.Combine(reelDir, "favourites.json"), "[]");

        var svc = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        File.Exists(Path.Combine(reelDir, "progress.json")).Should().BeFalse();
        File.Exists(Path.Combine(reelDir, "favourites.json")).Should().BeFalse();

        using var conn = svc.GetConnection();

        using var progressCmd = conn.CreateCommand();
        progressCmd.CommandText = "SELECT COUNT(*) FROM progress;";
        Convert.ToInt32(progressCmd.ExecuteScalar()).Should().Be(0);

        using var favCmd = conn.CreateCommand();
        favCmd.CommandText = "SELECT COUNT(*) FROM favourites;";
        Convert.ToInt32(favCmd.ExecuteScalar()).Should().Be(0);
    }

    [Fact]
    public void HandlesMissingJsonFiles()
    {
        var act = () => new DatabaseService(_config, NullLogger<DatabaseService>.Instance);
        act.Should().NotThrow();
    }

    [Fact]
    public void IdempotentAfterJsonDeletion()
    {
        var reelDir = Path.Combine(_tempMediaRoot, ".reel");
        Directory.CreateDirectory(reelDir);

        WriteJson(Path.Combine(reelDir, "progress.json"), new List<WatchProgress>
        {
            new("Films/test.mp4", 50.0, 1000.0, DateTime.UtcNow)
        });

        _ = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);
        var svc2 = new DatabaseService(_config, NullLogger<DatabaseService>.Instance);

        using var conn = svc2.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM progress;";
        Convert.ToInt32(cmd.ExecuteScalar()).Should().Be(1, "second construction should not duplicate or remove data");
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

    private static void WriteJson<T>(string path, T data)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        File.WriteAllText(path, JsonSerializer.Serialize(data, options));
    }
}
