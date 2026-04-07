namespace Reel.Server.Domain;

public sealed record WatchProgress(
    string RelativePath,
    double PositionSeconds,
    double DurationSeconds,
    DateTime LastWatched
);
