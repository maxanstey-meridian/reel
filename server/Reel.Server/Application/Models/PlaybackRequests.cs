namespace Reel.Server.Application.Models;

using Reel.Server.Domain;

public sealed record StartPlaybackRequest(string RelativePath, double StartPositionSeconds, int? SubtitleIndex);

public sealed record UpdatePlaybackIntentRequest(
    Guid SessionId,
    PlaybackDesiredState DesiredState,
    int? SubtitleIndex,
    double? SeekPositionSeconds
);

public sealed record ReportPlaybackObservationRequest(
    Guid SessionId,
    long ObservationSequence,
    long AppliedRevision,
    long? AppliedSeekId,
    PlaybackObservationState State,
    string? RelativePath,
    double PositionSeconds,
    double DurationSeconds,
    int? SubtitleIndex,
    string? ErrorCode,
    string? ErrorMessage
);

public enum PlaybackSessionError
{
    InvalidRequest,
    SessionNotFound,
    ObservationRevisionConflict,
}

public readonly record struct PlaybackSessionResult<T>(T? Value, PlaybackSessionError? Error, string? Message)
{
    public bool IsSuccess => Error is null;

    public static PlaybackSessionResult<T> Success(T value) => new(value, null, null);

    public static PlaybackSessionResult<T> Failure(PlaybackSessionError error, string message) =>
        new(default, error, message);
}
