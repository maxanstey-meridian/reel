namespace Reel.Server.Models;

public sealed record WatchProgress(
    string RelativePath,
    double PositionSeconds,
    double DurationSeconds,
    DateTime LastWatched
);

public sealed record ProgressRequest(
    string RelativePath,
    double PositionSeconds,
    double DurationSeconds
);
