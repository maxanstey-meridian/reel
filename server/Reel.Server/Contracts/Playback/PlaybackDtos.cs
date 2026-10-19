using System.Text.Json.Serialization;
using Rivet;

namespace Reel.Server.Contracts.Playback;

// The session starts with the remembered quality and subtitle language (see PlaybackIntentResponse).
public sealed record StartPlaybackSessionRequest(string RelativePath, double StartPositionSeconds);

// ExpectedRevision lets a client apply a change only to the session state it last saw. SubtitleIndex and MaxHeight
// are the whole intent rather than a change, so a request without them is rejected rather than read as "off" and
// "full quality": clients send the session's current value to keep it.
public sealed record UpdatePlaybackIntentRequest(
    Guid SessionId,
    PlaybackDesiredStateDto DesiredState,
    [property: JsonRequired] int? SubtitleIndex,
    [property: JsonRequired] int? MaxHeight,
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

// Title is the same parsed title the library shows for RelativePath. MaxHeight caps the video's height (null plays
// it at its own); changing it or SubtitleIndex is remembered and becomes the next session's starting choice.
public sealed record PlaybackIntentResponse(
    string RelativePath,
    string Title,
    PlaybackDesiredStateDto DesiredState,
    int? SubtitleIndex,
    int? MaxHeight,
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
