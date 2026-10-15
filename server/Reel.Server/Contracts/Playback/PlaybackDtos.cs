using System.Text.Json.Serialization;
using Rivet;

namespace Reel.Server.Contracts.Playback;

public sealed record StartPlaybackSessionRequest(
    string RelativePath,
    double StartPositionSeconds,
    [property: RivetOptional] int? SubtitleIndex
);

// ExpectedRevision lets a client apply a change only to the session state it last saw.
public sealed record UpdatePlaybackIntentRequest(
    Guid SessionId,
    PlaybackDesiredStateDto DesiredState,
    [property: RivetOptional] int? SubtitleIndex,
    [property: RivetOptional] double? SeekPositionSeconds,
    [property: RivetOptional] long? ExpectedRevision = null
);

public sealed record ReportPlaybackObservationRequest(
    Guid SessionId,
    long ObservationSequence,
    long AppliedRevision,
    [property: RivetOptional] long? AppliedSeekId,
    PlaybackObservationStateDto State,
    [property: RivetOptional] string? RelativePath,
    double PositionSeconds,
    double DurationSeconds,
    [property: RivetOptional] int? SubtitleIndex,
    [property: RivetOptional] string? ErrorCode,
    [property: RivetOptional] string? ErrorMessage
);

public sealed record PlaybackSessionEnvelope(PlaybackSessionResponse? Session);

public sealed record PlaybackSessionResponse(
    Guid SessionId,
    long Revision,
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

public sealed record SeekRequestResponse(long SeekId, double PositionSeconds);

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

[JsonConverter(typeof(RivetCamelCaseEnumConverter<PlaybackDesiredStateDto>))]
public enum PlaybackDesiredStateDto
{
    Playing,
    Paused,
    Stopped,
}

[JsonConverter(typeof(RivetCamelCaseEnumConverter<PlaybackObservationStateDto>))]
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
