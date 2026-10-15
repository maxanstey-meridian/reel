namespace Reel.Server.Contracts.Playback;

public sealed record StartPlaybackSessionRequest(
    string RelativePath,
    double StartPositionSeconds,
    int? SubtitleIndex
);

// ExpectedRevision lets a client apply a change only to the session state it last saw.
public sealed record UpdatePlaybackIntentRequest(
    Guid SessionId,
    PlaybackDesiredStateDto DesiredState,
    int? SubtitleIndex,
    double? SeekPositionSeconds,
    long? ExpectedRevision = null
);

public sealed record ReportPlaybackObservationRequest(
    Guid SessionId,
    long ObservationSequence,
    long AppliedRevision,
    long? AppliedSeekId,
    PlaybackObservationStateDto State,
    string? RelativePath,
    double PositionSeconds,
    double DurationSeconds,
    int? SubtitleIndex,
    string? ErrorCode,
    string? ErrorMessage
);

public sealed record PlaybackSessionEnvelope(PlaybackSessionResponse? Session);

public sealed record PlaybackSessionResponse(
    Guid SessionId,
    long Revision,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    PlaybackIntentResponse Intent,
    PlaybackObservationResponse? Observation,
    bool TvConnected
);

// Title is the same parsed title the library shows for RelativePath.
public sealed record PlaybackIntentResponse(
    string RelativePath,
    string Title,
    PlaybackDesiredStateDto DesiredState,
    int? SubtitleIndex,
    SeekRequestResponse? PendingSeek
);

public sealed record SeekRequestResponse(
    long SeekId,
    double PositionSeconds,
    long IntroducedRevision
);

public sealed record PlaybackObservationResponse(
    long ObservationSequence,
    long AppliedRevision,
    long? AppliedSeekId,
    PlaybackObservationStateDto State,
    string? RelativePath,
    double PositionSeconds,
    double DurationSeconds,
    int? SubtitleIndex,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset UpdatedAtUtc
);

public enum PlaybackDesiredStateDto
{
    Playing,
    Paused,
    Stopped,
}

public enum PlaybackObservationStateDto
{
    Idle,
    Loading,
    Buffering,
    Playing,
    Paused,
    Ended,
    Error,
}
