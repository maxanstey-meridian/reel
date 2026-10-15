using Reel.Server.Storage;

namespace Reel.Server.Library;

// Reconciling deletes media rows that are no longer on disk, which cascades to progress and
// favourites. It therefore refuses any scan that might be a symptom of an unreadable or
// unmounted volume (a missing media root fails the scan) rather than a real change to the library.
public sealed class LibraryIndexReconciler(
    FileSystemScanner scanner,
    SqliteMediaStore mediaStore,
    TimeProvider time
)
{
    public async Task<ReconcileOutcome> ReconcileAsync(CancellationToken ct)
    {
        var scanStartedAt = time.GetUtcNow().UtcDateTime;
        var scan = await Task.Run(() => scanner.Scan(ct), ct);
        if (!scan.IsComplete)
        {
            return new ReconcileOutcome.Refused(
                "Some folders could not be read, so the library was left as it was. Check the media drive."
            );
        }

        if (scan.Media.Count == 0 && await mediaStore.HasMediaAsync())
        {
            return new ReconcileOutcome.Refused(
                "No media was found, so the library was left as it was. Check the media drive is connected."
            );
        }

        await mediaStore.ReconcileAsync(scan.Media, scanStartedAt);
        return new ReconcileOutcome.Indexed(scan.Media.Count);
    }
}

public abstract record ReconcileOutcome
{
    private ReconcileOutcome() { }

    public sealed record Indexed(int MediaCount) : ReconcileOutcome;

    public sealed record Refused(string Reason) : ReconcileOutcome;
}

/// <summary>Reconciles the index once after startup without delaying or failing the server.</summary>
public sealed class StartupLibraryIndex(
    LibraryIndexReconciler reconciler,
    ILogger<StartupLibraryIndex> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            switch (await reconciler.ReconcileAsync(stoppingToken))
            {
                case ReconcileOutcome.Indexed indexed:
                    logger.LogInformation(
                        "Library index reconciled with {Count} media files",
                        indexed.MediaCount
                    );
                    break;
                case ReconcileOutcome.Refused refused:
                    logger.LogWarning("Startup library index refused: {Reason}", refused.Reason);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Startup library index failed; serving the existing index");
        }
    }
}
