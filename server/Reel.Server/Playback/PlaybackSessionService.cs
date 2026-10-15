using Reel.Server.Contracts.Playback;
using Reel.Server.Library;
using Reel.Server.Storage;

namespace Reel.Server.Playback;

public enum PlaybackSessionError
{
    InvalidRequest,
    SessionNotFound,
    RevisionConflict,
}

public abstract record PlaybackSessionOutcome
{
    private PlaybackSessionOutcome() { }

    public sealed record Ok(PlaybackSessionResponse Session) : PlaybackSessionOutcome;

    public sealed record Failed(PlaybackSessionError Error, string Message)
        : PlaybackSessionOutcome;
}

// The phone requests playback (intent); the TV reports what actually happened (observation).
// TvConnected is not stored: it is derived from observation freshness whenever the session is read.
public sealed class PlaybackSessionService(
    MediaPathResolver pathResolver,
    TimeProvider timeProvider
)
{
    private static readonly TimeSpan TvFreshness = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TerminalExpiry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan NonterminalExpiry = TimeSpan.FromMinutes(30);

    private readonly Lock _lock = new();
    private long _lastSeekId;
    private Session? _session;

    public PlaybackSessionOutcome Start(StartPlaybackSessionRequest request)
    {
        if (!IsFiniteNonnegative(request.StartPositionSeconds))
        {
            return Invalid("Start position must be finite and non-negative.");
        }

        if (request.SubtitleIndex is < 0)
        {
            return Invalid("Subtitle index must be non-negative.");
        }

        var fullPath = pathResolver.ResolvePlayableFile(request.RelativePath);
        if (fullPath is null)
        {
            return Invalid("Relative path must identify an existing playable media file.");
        }

        var now = timeProvider.GetUtcNow();
        var pendingSeek =
            request.StartPositionSeconds > 0
                ? new SeekRequestResponse(1, request.StartPositionSeconds, 1)
                : null;
        var session = new Session(
            Guid.NewGuid(),
            1,
            now,
            now,
            new PlaybackIntentResponse(
                pathResolver.ToRelativePath(fullPath),
                FilenameParser.Parse(Path.GetFileName(fullPath)).Title,
                PlaybackDesiredStateDto.Playing,
                request.SubtitleIndex,
                pendingSeek
            ),
            null
        );

        lock (_lock)
        {
            _session = session;
            _lastSeekId = pendingSeek?.SeekId ?? 0;
            return Success(session);
        }
    }

    public PlaybackSessionResponse? GetCurrent()
    {
        lock (_lock)
        {
            ExpireIfNeeded(timeProvider.GetUtcNow());
            return _session is null ? null : ToResponse(_session);
        }
    }

    public PlaybackSessionOutcome UpdateIntent(UpdatePlaybackIntentRequest request)
    {
        if (request.SubtitleIndex is < 0)
        {
            return Invalid("Subtitle index must be non-negative.");
        }

        if (request.SeekPositionSeconds is { } seek && !IsFiniteNonnegative(seek))
        {
            return Invalid("Seek position must be finite and non-negative.");
        }

        if (
            request.DesiredState == PlaybackDesiredStateDto.Stopped
            && request.SeekPositionSeconds is not null
        )
        {
            return Invalid("Stopped intent cannot include a seek request.");
        }

        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            ExpireIfNeeded(now);
            if (_session is null || _session.SessionId != request.SessionId)
            {
                return NotFound();
            }

            if (request.ExpectedRevision is { } expected && expected != _session.Revision)
            {
                return Conflict("Playback session changed since it was last read.");
            }

            var pendingSeek = _session.Intent.PendingSeek;
            if (request.SeekPositionSeconds is { } requestedSeek)
            {
                var duration = _session.Observation?.DurationSeconds;
                var position = duration is > 0
                    ? Math.Min(requestedSeek, duration.Value)
                    : requestedSeek;
                pendingSeek = new SeekRequestResponse(
                    ++_lastSeekId,
                    position,
                    _session.Revision + 1
                );
            }

            _session = _session with
            {
                Revision = _session.Revision + 1,
                UpdatedAtUtc = now,
                Intent = _session.Intent with
                {
                    DesiredState = request.DesiredState,
                    SubtitleIndex = request.SubtitleIndex,
                    PendingSeek = pendingSeek,
                },
            };
            return Success(_session);
        }
    }

    public PlaybackSessionOutcome ReportObservation(ReportPlaybackObservationRequest request)
    {
        if (
            request.ObservationSequence <= 0
            || request.AppliedRevision < 0
            || request.AppliedSeekId is <= 0
        )
        {
            return Invalid(
                "Observation sequence must be positive, applied revision non-negative, and seek ID positive."
            );
        }

        if (
            !IsFiniteNonnegative(request.PositionSeconds)
            || !IsFiniteNonnegative(request.DurationSeconds)
        )
        {
            return Invalid("Position and duration must be finite and non-negative.");
        }

        if (request.SubtitleIndex is < 0)
        {
            return Invalid("Subtitle index must be non-negative.");
        }

        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            ExpireIfNeeded(now);
            if (_session is null || _session.SessionId != request.SessionId)
            {
                return NotFound();
            }

            if (request.AppliedRevision > _session.Revision)
            {
                return Conflict("Applied revision cannot exceed the current session revision.");
            }

            if (
                _session.Observation is { } sequenced
                && request.ObservationSequence <= sequenced.ObservationSequence
            )
            {
                if (
                    request.ObservationSequence == sequenced.ObservationSequence
                    && ObservationEquals(request, sequenced)
                )
                {
                    return Success(_session);
                }

                return Conflict("Observation sequence must increase.");
            }
            if (
                _session.Observation is { } existing
                && request.AppliedRevision < existing.AppliedRevision
            )
            {
                return Conflict("Applied revision cannot regress.");
            }

            if (request.AppliedSeekId is { } acknowledgedSeekId && acknowledgedSeekId > _lastSeekId)
            {
                return Invalid("Applied seek ID cannot exceed the latest allocated seek ID.");
            }

            var mayOmitPath =
                request.State
                is PlaybackObservationStateDto.Idle
                    or PlaybackObservationStateDto.Error;
            if (request.RelativePath is null && !mayOmitPath)
            {
                return Invalid("Relative path is required for this observation state.");
            }

            if (
                request.RelativePath is not null
                && !string.Equals(
                    request.RelativePath,
                    _session.Intent.RelativePath,
                    StringComparison.Ordinal
                )
            )
            {
                return Invalid("Observed relative path must match the current playback intent.");
            }

            var hasErrorCode = !string.IsNullOrWhiteSpace(request.ErrorCode);
            var hasErrorMessage = !string.IsNullOrWhiteSpace(request.ErrorMessage);
            if (
                request.State == PlaybackObservationStateDto.Error
                && (!hasErrorCode || !hasErrorMessage)
            )
            {
                return Invalid("Error observations require both an error code and message.");
            }

            if (
                request.State != PlaybackObservationStateDto.Error
                && (request.ErrorCode is not null || request.ErrorMessage is not null)
            )
            {
                return Invalid("Non-error observations cannot include error details.");
            }

            var pendingSeek = _session.Intent.PendingSeek;
            if (
                pendingSeek is not null
                && request.AppliedSeekId == pendingSeek.SeekId
                && request.AppliedRevision >= pendingSeek.IntroducedRevision
            )
            {
                pendingSeek = null;
            }

            _session = _session with
            {
                UpdatedAtUtc = now,
                Intent = _session.Intent with { PendingSeek = pendingSeek },
                Observation = new PlaybackObservationResponse(
                    request.ObservationSequence,
                    request.AppliedRevision,
                    request.AppliedSeekId,
                    request.State,
                    request.RelativePath,
                    ClampedPosition(request),
                    request.DurationSeconds,
                    request.SubtitleIndex,
                    request.ErrorCode,
                    request.ErrorMessage,
                    now
                ),
            };
            return Success(_session);
        }
    }

    private void ExpireIfNeeded(DateTimeOffset now)
    {
        if (_session is null)
        {
            return;
        }

        var terminal =
            _session.Intent.DesiredState == PlaybackDesiredStateDto.Stopped
            || _session.Observation?.State == PlaybackObservationStateDto.Ended;
        var lastObservation = _session.Observation?.UpdatedAtUtc ?? _session.CreatedAtUtc;
        var expiryBase = terminal ? _session.UpdatedAtUtc : lastObservation;
        var expiry = terminal ? TerminalExpiry : NonterminalExpiry;
        if (now - expiryBase > expiry)
        {
            _session = null;
        }
    }

    private PlaybackSessionOutcome.Ok Success(Session session) => new(ToResponse(session));

    private PlaybackSessionResponse ToResponse(Session session) =>
        new(
            session.SessionId,
            session.Revision,
            session.CreatedAtUtc,
            session.UpdatedAtUtc,
            session.Intent,
            session.Observation,
            TvConnected: session.Observation is { } observation
                && timeProvider.GetUtcNow() - observation.UpdatedAtUtc <= TvFreshness
        );

    private static bool IsFiniteNonnegative(double value) => double.IsFinite(value) && value >= 0;

    private static double ClampedPosition(ReportPlaybackObservationRequest request) =>
        request.DurationSeconds > 0
            ? Math.Min(request.PositionSeconds, request.DurationSeconds)
            : request.PositionSeconds;

    private static bool ObservationEquals(
        ReportPlaybackObservationRequest request,
        PlaybackObservationResponse observation
    ) =>
        request.AppliedRevision == observation.AppliedRevision
        && request.AppliedSeekId == observation.AppliedSeekId
        && request.State == observation.State
        && request.RelativePath == observation.RelativePath
        && ClampedPosition(request) == observation.PositionSeconds
        && request.DurationSeconds == observation.DurationSeconds
        && request.SubtitleIndex == observation.SubtitleIndex
        && request.ErrorCode == observation.ErrorCode
        && request.ErrorMessage == observation.ErrorMessage;

    private static PlaybackSessionOutcome.Failed Invalid(string message) =>
        new(PlaybackSessionError.InvalidRequest, message);

    private static PlaybackSessionOutcome.Failed NotFound() =>
        new(PlaybackSessionError.SessionNotFound, "Playback session was not found.");

    private static PlaybackSessionOutcome.Failed Conflict(string message) =>
        new(PlaybackSessionError.RevisionConflict, message);

    private sealed record Session(
        Guid SessionId,
        long Revision,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        PlaybackIntentResponse Intent,
        PlaybackObservationResponse? Observation
    );
}
