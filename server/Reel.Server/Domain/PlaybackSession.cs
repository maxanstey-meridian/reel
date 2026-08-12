namespace Reel.Server.Domain;

public sealed record PlaybackSession(
    Guid SessionId,
    long Revision,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    PlaybackIntent Intent,
    PlaybackObservation? Observation
);

public sealed record PlaybackIntent(
    string RelativePath,
    PlaybackDesiredState DesiredState,
    int? SubtitleIndex,
    SeekRequest? PendingSeek
);

public sealed record SeekRequest(long SeekId, double PositionSeconds, long IntroducedRevision);

public sealed record PlaybackObservation(
    long ObservationSequence,
    long AppliedRevision,
    long? AppliedSeekId,
    PlaybackObservationState State,
    string? RelativePath,
    double PositionSeconds,
    double DurationSeconds,
    int? SubtitleIndex,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset UpdatedAtUtc
);

public enum PlaybackDesiredState
{
    Playing,
    Paused,
    Stopped,
}

public enum PlaybackObservationState
{
    Idle,
    Loading,
    Buffering,
    Playing,
    Paused,
    Ended,
    Error,
}
