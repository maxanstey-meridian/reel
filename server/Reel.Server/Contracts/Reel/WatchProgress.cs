namespace Reel.Server.Contracts.Reel;

public sealed record WatchProgress(
    string RelativePath,
    double PositionSeconds,
    double DurationSeconds,
    DateTime LastWatched
);
