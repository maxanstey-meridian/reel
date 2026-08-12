namespace Reel.Server.Application;

using Reel.Server.Application.Models;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.FileSystem;

public sealed class PlaybackSessionService
{
    private static readonly TimeSpan TvFreshness = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TerminalExpiry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan NonterminalExpiry = TimeSpan.FromMinutes(30);

    private readonly Lock _lock = new();
    private readonly MediaPathResolver _pathResolver;
    private readonly TimeProvider _timeProvider;
    private long _lastSeekId;
    private PlaybackSession? _session;

    public PlaybackSessionService(MediaPathResolver pathResolver, TimeProvider timeProvider)
    {
        _pathResolver = pathResolver;
        _timeProvider = timeProvider;
    }

    public PlaybackSessionResult<PlaybackSession> Start(StartPlaybackRequest request)
    {
        if (!IsFiniteNonnegative(request.StartPositionSeconds))
            return Invalid<PlaybackSession>("Start position must be finite and non-negative.");
        if (request.SubtitleIndex is < 0)
            return Invalid<PlaybackSession>("Subtitle index must be non-negative.");
        if (_pathResolver.ResolvePlayableFile(request.RelativePath) is null)
            return Invalid<PlaybackSession>("Relative path must identify an existing playable media file.");

        var now = _timeProvider.GetUtcNow();
        var pendingSeek = request.StartPositionSeconds > 0
            ? new SeekRequest(1, request.StartPositionSeconds, 1)
            : null;
        var session = new PlaybackSession(
            Guid.NewGuid(),
            1,
            now,
            now,
            new PlaybackIntent(
                NormalizeRelativePath(request.RelativePath),
                PlaybackDesiredState.Playing,
                request.SubtitleIndex,
                pendingSeek
            ),
            null
        );

        lock (_lock)
        {
            _session = session;
            _lastSeekId = pendingSeek?.SeekId ?? 0;
        }

        return PlaybackSessionResult<PlaybackSession>.Success(session);
    }

    public PlaybackSession? GetCurrent()
    {
        lock (_lock)
        {
            ExpireIfNeeded(_timeProvider.GetUtcNow());
            return _session;
        }
    }

    public bool IsTvConnected(PlaybackSession session) =>
        session.Observation is not null
        && _timeProvider.GetUtcNow() - session.Observation.UpdatedAtUtc <= TvFreshness;

    public PlaybackSessionResult<PlaybackSession> UpdateIntent(UpdatePlaybackIntentRequest request)
    {
        if (!Enum.IsDefined(request.DesiredState))
            return Invalid<PlaybackSession>("Desired state is invalid.");
        if (request.SubtitleIndex is < 0)
            return Invalid<PlaybackSession>("Subtitle index must be non-negative.");
        if (request.SeekPositionSeconds is { } seek && !IsFiniteNonnegative(seek))
            return Invalid<PlaybackSession>("Seek position must be finite and non-negative.");
        if (request.DesiredState == PlaybackDesiredState.Stopped && request.SeekPositionSeconds is not null)
            return Invalid<PlaybackSession>("Stopped intent cannot include a seek request.");

        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();
            ExpireIfNeeded(now);
            if (_session is null || _session.SessionId != request.SessionId)
                return NotFound<PlaybackSession>();

            var pendingSeek = _session.Intent.PendingSeek;
            if (request.SeekPositionSeconds is { } requestedSeek)
            {
                var duration = _session.Observation?.DurationSeconds;
                var position = duration is > 0 ? Math.Min(requestedSeek, duration.Value) : requestedSeek;
                pendingSeek = new SeekRequest(++_lastSeekId, position, _session.Revision + 1);
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
            return PlaybackSessionResult<PlaybackSession>.Success(_session);
        }
    }

    public PlaybackSessionResult<PlaybackSession> ReportObservation(ReportPlaybackObservationRequest request)
    {
        if (!Enum.IsDefined(request.State))
            return Invalid<PlaybackSession>("Observation state is invalid.");
        if (request.ObservationSequence <= 0 || request.AppliedRevision < 0 || request.AppliedSeekId is <= 0)
            return Invalid<PlaybackSession>("Observation sequence must be positive, applied revision non-negative, and seek ID positive.");
        if (!IsFiniteNonnegative(request.PositionSeconds) || !IsFiniteNonnegative(request.DurationSeconds))
            return Invalid<PlaybackSession>("Position and duration must be finite and non-negative.");
        if (request.SubtitleIndex is < 0)
            return Invalid<PlaybackSession>("Subtitle index must be non-negative.");

        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();
            ExpireIfNeeded(now);
            if (_session is null || _session.SessionId != request.SessionId)
                return NotFound<PlaybackSession>();
            if (request.AppliedRevision > _session.Revision)
                return RevisionConflict("Applied revision cannot exceed the current session revision.");
            if (_session.Observation is { } sequenced && request.ObservationSequence <= sequenced.ObservationSequence)
            {
                if (request.ObservationSequence == sequenced.ObservationSequence && ObservationEquals(request, sequenced))
                    return PlaybackSessionResult<PlaybackSession>.Success(_session);
                return RevisionConflict("Observation sequence must increase.");
            }
            if (
                _session.Observation is { } existing
                && request.AppliedRevision < existing.AppliedRevision
            )
                return RevisionConflict("Applied revision cannot regress.");
            if (request.AppliedSeekId is { } acknowledgedSeekId && acknowledgedSeekId > _lastSeekId)
                return Invalid<PlaybackSession>("Applied seek ID cannot exceed the latest allocated seek ID.");

            var mayOmitPath = request.State is PlaybackObservationState.Idle or PlaybackObservationState.Error;
            if (request.RelativePath is null && !mayOmitPath)
                return Invalid<PlaybackSession>("Relative path is required for this observation state.");
            if (
                request.RelativePath is not null
                && !string.Equals(
                    NormalizeRelativePath(request.RelativePath),
                    _session.Intent.RelativePath,
                    StringComparison.Ordinal
                )
            )
            {
                return Invalid<PlaybackSession>("Observed relative path must match the current playback intent.");
            }

            var hasErrorCode = !string.IsNullOrWhiteSpace(request.ErrorCode);
            var hasErrorMessage = !string.IsNullOrWhiteSpace(request.ErrorMessage);
            if (request.State == PlaybackObservationState.Error && (!hasErrorCode || !hasErrorMessage))
                return Invalid<PlaybackSession>("Error observations require both an error code and message.");
            if (request.State != PlaybackObservationState.Error && (request.ErrorCode is not null || request.ErrorMessage is not null))
                return Invalid<PlaybackSession>("Non-error observations cannot include error details.");

            var pendingSeek = _session.Intent.PendingSeek;
            if (
                pendingSeek is not null
                && request.AppliedSeekId is { } appliedSeekId
                && appliedSeekId == pendingSeek.SeekId
                && request.AppliedRevision >= pendingSeek.IntroducedRevision
            )
            {
                pendingSeek = null;
            }

            var duration = request.DurationSeconds;
            var position = duration > 0 ? Math.Min(request.PositionSeconds, duration) : request.PositionSeconds;
            _session = _session with
            {
                UpdatedAtUtc = now,
                Intent = _session.Intent with { PendingSeek = pendingSeek },
                Observation = new PlaybackObservation(
                    request.ObservationSequence,
                    request.AppliedRevision,
                    request.AppliedSeekId,
                    request.State,
                    request.RelativePath is null ? null : NormalizeRelativePath(request.RelativePath),
                    position,
                    duration,
                    request.SubtitleIndex,
                    request.ErrorCode,
                    request.ErrorMessage,
                    now
                ),
            };
            return PlaybackSessionResult<PlaybackSession>.Success(_session);
        }
    }

    private void ExpireIfNeeded(DateTimeOffset now)
    {
        if (_session is null)
            return;

        var terminal = _session.Intent.DesiredState == PlaybackDesiredState.Stopped
            || _session.Observation?.State == PlaybackObservationState.Ended;
        var lastObservation = _session.Observation?.UpdatedAtUtc ?? _session.CreatedAtUtc;
        var expiryBase = terminal ? _session.UpdatedAtUtc : lastObservation;
        var expiry = terminal ? TerminalExpiry : NonterminalExpiry;
        if (now - expiryBase > expiry)
            _session = null;
    }

    private static bool IsFiniteNonnegative(double value) => double.IsFinite(value) && value >= 0;

    private static bool ObservationEquals(ReportPlaybackObservationRequest request, PlaybackObservation observation) =>
        request.AppliedRevision == observation.AppliedRevision
        && request.AppliedSeekId == observation.AppliedSeekId
        && request.State == observation.State
        && (request.RelativePath is null ? null : NormalizeRelativePath(request.RelativePath)) == observation.RelativePath
        && (request.DurationSeconds > 0 ? Math.Min(request.PositionSeconds, request.DurationSeconds) : request.PositionSeconds)
            == observation.PositionSeconds
        && request.DurationSeconds == observation.DurationSeconds
        && request.SubtitleIndex == observation.SubtitleIndex
        && request.ErrorCode == observation.ErrorCode
        && request.ErrorMessage == observation.ErrorMessage;

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private static PlaybackSessionResult<T> Invalid<T>(string message) =>
        PlaybackSessionResult<T>.Failure(PlaybackSessionError.InvalidRequest, message);

    private static PlaybackSessionResult<T> NotFound<T>() =>
        PlaybackSessionResult<T>.Failure(PlaybackSessionError.SessionNotFound, "Playback session was not found.");

    private static PlaybackSessionResult<PlaybackSession> RevisionConflict(string message) =>
        PlaybackSessionResult<PlaybackSession>.Failure(PlaybackSessionError.ObservationRevisionConflict, message);
}
