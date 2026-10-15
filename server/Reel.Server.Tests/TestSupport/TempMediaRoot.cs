using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Reel.Server.Library;
using Reel.Server.Storage;

namespace Reel.Server.Tests;

/// <summary>A throwaway media root, with Reel's services and hosts over it, deleted on dispose.</summary>
internal sealed class TempMediaRoot : IDisposable
{
    private DatabaseService? _database;

    public string Root { get; } = Directory.CreateTempSubdirectory("reel-test-").FullName;

    public MediaPathResolver Paths =>
        new(
            Options.Create(
                new ReelOptions
                {
                    MediaRoot = Root,
                    SecretKey = ReelTestHost.SecretKey,
                    Port = 8080,
                }
            )
        );

    /// <summary>The media root's database, migrated to the latest schema.</summary>
    public DatabaseService Database => _database ??= Migrated(new DatabaseService(Paths));

    public SqliteMediaStore MediaStore => new(Database, TimeProvider.System);

    public SqliteProgressStore ProgressStore => new(Database, TimeProvider.System);

    public SqliteFavouritesStore FavouritesStore => new(Database, TimeProvider.System);

    public LibraryIndexReconciler Reconciler =>
        new(
            new FileSystemScanner(Paths, NullLogger<FileSystemScanner>.Instance),
            MediaStore,
            TimeProvider.System
        );

    /// <summary>Writes a file (a single byte unless given content) and returns its full path.</summary>
    public string Write(string relativePath, byte[]? content = null)
    {
        var fullPath = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content ?? [1]);
        return fullPath;
    }

    public WebApplicationFactory<Program> CreateFactory(
        Action<IWebHostBuilder>? configure = null
    ) => new WebApplicationFactory<Program>().WithReelSettings(Root, configure: configure);

    private static DatabaseService Migrated(DatabaseService database)
    {
        database.MigrateAsync().GetAwaiter().GetResult();
        return database;
    }

    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }
        // Tests lock folders to simulate unreadable volumes; unlock them so they can be deleted.
        foreach (
            var directory in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories)
        )
        {
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }

        Directory.Delete(Root, recursive: true);
    }
}
