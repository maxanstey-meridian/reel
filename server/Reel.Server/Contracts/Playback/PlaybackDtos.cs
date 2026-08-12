namespace Reel.Server.Contracts.Playback;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record StartPlaybackSessionRequest(
    string RelativePath,
    double StartPositionSeconds,
    int? SubtitleIndex
);

public sealed record UpdatePlaybackIntentRequest(
    Guid SessionId,
    PlaybackDesiredStateDto DesiredState,
    int? SubtitleIndex,
    double? SeekPositionSeconds
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

public sealed record PlaybackIntentResponse(
    string RelativePath,
    PlaybackDesiredStateDto DesiredState,
    int? SubtitleIndex,
    SeekRequestResponse? PendingSeek
);

public sealed record SeekRequestResponse(long SeekId, double PositionSeconds, long IntroducedRevision);

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

public sealed record PlaybackErrorResponse(string Error);

[JsonConverter(typeof(PlaybackDesiredStateJsonConverter))]
public enum PlaybackDesiredStateDto
{
    Playing,
    Paused,
    Stopped,
}

[JsonConverter(typeof(PlaybackObservationStateJsonConverter))]
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

public sealed class PlaybackDesiredStateJsonConverter()
    : JsonStringEnumConverter<PlaybackDesiredStateDto>(JsonNamingPolicy.CamelCase, allowIntegerValues: false);

public sealed class PlaybackObservationStateJsonConverter()
    : JsonStringEnumConverter<PlaybackObservationStateDto>(JsonNamingPolicy.CamelCase, allowIntegerValues: false);
