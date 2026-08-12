namespace Reel.Server.Tests;

using FluentAssertions;
using Reel.Server.Application;
using Reel.Server.Application.Models;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.FileSystem;

public sealed class PlaybackSessionServiceTests : IDisposable
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero));
    private readonly string _mediaRoot;
    private readonly PlaybackSessionService _service;

    public PlaybackSessionServiceTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-playback-service-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "Films"));
        File.WriteAllBytes(Path.Combine(_mediaRoot, "Films", "movie.mp4"), [1]);
        File.WriteAllText(Path.Combine(_mediaRoot, "Films", "notes.txt"), "not media");
        var resolver = new MediaPathResolver(
            new ReelConfig
            {
                MediaRoot = _mediaRoot,
                SecretKey = "test",
                Port = 8080,
            }
        );
        _service = new PlaybackSessionService(resolver, _time);
    }

    public void Dispose() => Directory.Delete(_mediaRoot, recursive: true);

    [Fact]
    public void Start_CreatesPlayingSessionAndReplacesExistingSession()
    {
        var first = Start();
        _time.Advance(TimeSpan.FromSeconds(1));

        var second = Start(12.5, 2);

        second.SessionId.Should().NotBe(first.SessionId);
        second.Revision.Should().Be(1);
        second.CreatedAtUtc.Should().Be(_time.GetUtcNow());
        second.Intent.DesiredState.Should().Be(PlaybackDesiredState.Playing);
        second.Intent.SubtitleIndex.Should().Be(2);
        second.Intent.PendingSeek.Should().Be(new SeekRequest(1, 12.5, 1));
        second.Observation.Should().BeNull();
        _service.GetCurrent().Should().Be(second);
    }

    [Fact]
    public void UpdateIntent_IncrementsRevisionAndRetainsUnacknowledgedSeek()
    {
        var session = Start(10);

        var result = _service.UpdateIntent(
            new UpdatePlaybackIntentRequest(session.SessionId, PlaybackDesiredState.Paused, 3, null)
        );

        result.Value!.Revision.Should().Be(2);
        result.Value.Intent.DesiredState.Should().Be(PlaybackDesiredState.Paused);
        result.Value.Intent.SubtitleIndex.Should().Be(3);
        result.Value.Intent.PendingSeek.Should().Be(new SeekRequest(1, 10, 1));
    }

    [Fact]
    public void UpdateIntent_ReplacesPendingSeekAndKeepsSeekIdsMonotonicAfterAcknowledgement()
    {
        var session = Start(10);
        var collapsed = UpdateIntent(session.SessionId, seek: 20).Value!;
        collapsed.Intent.PendingSeek.Should().Be(new SeekRequest(2, 20, 2));

        Observe(collapsed, appliedSeekId: 2).Value!.Intent.PendingSeek.Should().BeNull();
        var next = UpdateIntent(session.SessionId, seek: 30).Value!;

        next.Intent.PendingSeek.Should().Be(new SeekRequest(3, 30, 3));
    }

    [Fact]
    public void UpdateIntent_ClampsSeekToKnownDuration()
    {
        var session = Start();
        Observe(session, duration: 100);

        var result = UpdateIntent(session.SessionId, seek: 150);

        result.Value!.Intent.PendingSeek!.PositionSeconds.Should().Be(100);
    }

    [Fact]
    public void UpdateIntent_ReplacesSubtitleWithNull()
    {
        var session = Start(subtitleIndex: 4);

        var result = _service.UpdateIntent(
            new UpdatePlaybackIntentRequest(session.SessionId, PlaybackDesiredState.Playing, null, null)
        );

        result.Value!.Intent.SubtitleIndex.Should().BeNull();
    }

    [Fact]
    public void UpdateIntent_RejectsSeekWhenDesiredStateIsStopped()
    {
        var session = Start();

        var result = UpdateIntent(session.SessionId, PlaybackDesiredState.Stopped, seek: 10);

        result.Error.Should().Be(PlaybackSessionError.InvalidRequest);
        _service.GetCurrent()!.Revision.Should().Be(session.Revision);
    }

    [Fact]
    public void UnknownSessionIds_AreRejected()
    {
        UpdateIntent(Guid.NewGuid()).Error.Should().Be(PlaybackSessionError.SessionNotFound);
        Observe(Start(), sessionId: Guid.NewGuid()).Error.Should().Be(PlaybackSessionError.SessionNotFound);
    }

    [Fact]
    public void IntentAndObservation_RejectUndefinedStates()
    {
        var session = Start();

        _service
            .UpdateIntent(new UpdatePlaybackIntentRequest(session.SessionId, (PlaybackDesiredState)999, null, null))
            .Error.Should()
            .Be(PlaybackSessionError.InvalidRequest);
        _service
            .ReportObservation(
                new ReportPlaybackObservationRequest(
                    session.SessionId,
                    1,
                    1,
                    null,
                    (PlaybackObservationState)999,
                    null,
                    0,
                    0,
                    null,
                    null,
                    null
                )
            )
            .Error.Should()
            .Be(PlaybackSessionError.InvalidRequest);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.1)]
    public void Start_RejectsInvalidPositions(double position)
    {
        _service.Start(new StartPlaybackRequest("Films/movie.mp4", position, null)).Error
            .Should()
            .Be(PlaybackSessionError.InvalidRequest);
    }

    [Theory]
    [InlineData(double.NaN, 10)]
    [InlineData(1, double.PositiveInfinity)]
    [InlineData(-1, 10)]
    [InlineData(1, -10)]
    public void Observation_RejectsInvalidNumerics(double position, double duration)
    {
        var result = Observe(Start(), position: position, duration: duration);

        result.Error.Should().Be(PlaybackSessionError.InvalidRequest);
    }

    [Fact]
    public void Observation_RejectsAppliedRevisionRegressionWithoutChangingStoredObservation()
    {
        var session = Start();
        var revised = UpdateIntent(session.SessionId, PlaybackDesiredState.Paused).Value!;
        var current = Observe(revised, appliedRevision: 2).Value!;

        var result = Observe(current, appliedRevision: 1);

        result.Error.Should().Be(PlaybackSessionError.ObservationRevisionConflict);
        _service.GetCurrent()!.Observation!.AppliedRevision.Should().Be(2);
    }

    [Fact]
    public void Observation_RejectsAppliedRevisionAheadOfCurrentSession()
    {
        var session = Start();

        var result = Observe(session, appliedRevision: session.Revision + 1);

        result.Error.Should().Be(PlaybackSessionError.ObservationRevisionConflict);
        _service.GetCurrent()!.Observation.Should().BeNull();
    }

    [Fact]
    public void Observation_RejectsReorderedSequenceAtEqualRevision()
    {
        var session = Start();
        Observe(session, observationSequence: 2).IsSuccess.Should().BeTrue();

        var result = Observe(session, observationSequence: 1);

        result.Error.Should().Be(PlaybackSessionError.ObservationRevisionConflict);
        _service.GetCurrent()!.Observation!.ObservationSequence.Should().Be(2);
    }

    [Fact]
    public void Observation_DoesNotAcknowledgeSeekBeforeItsIntroducedRevision()
    {
        var session = Start();
        var revised = UpdateIntent(session.SessionId, seek: 20).Value!;

        var result = Observe(revised, appliedRevision: 1, appliedSeekId: 1);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Intent.PendingSeek.Should().Be(new SeekRequest(1, 20, 2));
    }

    [Fact]
    public void Observation_RejectsSeekAcknowledgementBeyondLatestAllocatedSeek()
    {
        var session = Start(10);

        var result = Observe(session, appliedSeekId: 2);

        result.Error.Should().Be(PlaybackSessionError.InvalidRequest);
        _service.GetCurrent()!.Intent.PendingSeek.Should().Be(new SeekRequest(1, 10, 1));
    }

    [Theory]
    [InlineData(PlaybackObservationState.Loading)]
    [InlineData(PlaybackObservationState.Buffering)]
    [InlineData(PlaybackObservationState.Playing)]
    [InlineData(PlaybackObservationState.Paused)]
    [InlineData(PlaybackObservationState.Ended)]
    public void Observation_RequiresPathForStatesThatReferToMedia(PlaybackObservationState state)
    {
        var result = Observe(Start(), state: state, relativePath: null);

        result.Error.Should().Be(PlaybackSessionError.InvalidRequest);
    }

    [Theory]
    [InlineData(PlaybackObservationState.Idle)]
    [InlineData(PlaybackObservationState.Error)]
    public void Observation_AllowsNullPathForStatesThatMayNotHaveMedia(PlaybackObservationState state)
    {
        var result = Observe(
            Start(),
            state: state,
            relativePath: null,
            errorCode: state == PlaybackObservationState.Error ? "player" : null,
            errorMessage: state == PlaybackObservationState.Error ? "Playback failed" : null
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Observation_RejectsPathThatDoesNotNormalizeToIntentPath()
    {
        var session = Start();

        Observe(session, relativePath: "Films/other.mp4").Error
            .Should()
            .Be(PlaybackSessionError.InvalidRequest);
    }

    [Fact]
    public void Observation_RejectsMissingOrPartialErrorDetails()
    {
        var session = Start();

        Observe(session, state: PlaybackObservationState.Error, errorCode: "decoder").Error
            .Should()
            .Be(PlaybackSessionError.InvalidRequest);
        Observe(session, state: PlaybackObservationState.Error, errorMessage: "Failed").Error
            .Should()
            .Be(PlaybackSessionError.InvalidRequest);
    }

    [Fact]
    public void Observation_RejectsErrorDetailsForNonErrorState()
    {
        var result = Observe(Start(), errorCode: "decoder", errorMessage: "Failed");

        result.Error.Should().Be(PlaybackSessionError.InvalidRequest);
    }

    [Fact]
    public void Observation_RecordsActualStateAndClearsAcknowledgedPendingSeek()
    {
        var session = Start(10);

        var result = _service.ReportObservation(
            new ReportPlaybackObservationRequest(
                session.SessionId,
                1,
                1,
                1,
                PlaybackObservationState.Error,
                "Films/movie.mp4",
                12,
                120,
                5,
                "decoder",
                "Could not decode"
            )
        );

        result.Value!.Intent.PendingSeek.Should().BeNull();
        result.Value.Observation.Should().Be(
            new PlaybackObservation(
                1,
                1,
                1,
                PlaybackObservationState.Error,
                "Films/movie.mp4",
                12,
                120,
                5,
                "decoder",
                "Could not decode",
                _time.GetUtcNow()
            )
        );
    }

    [Fact]
    public void Observation_ExactSequenceRetryIsIdempotentWithoutRefreshingFreshness()
    {
        var session = Start(10);
        var first = Observe(session, observationSequence: 1, appliedSeekId: 1).Value!;
        var updatedAt = first.Observation!.UpdatedAtUtc;
        _time.Advance(TimeSpan.FromSeconds(10));

        var second = Observe(first, observationSequence: 1, appliedSeekId: 1).Value!;

        second.Intent.PendingSeek.Should().BeNull();
        second.Observation.Should().BeSameAs(first.Observation);
        second.Observation!.UpdatedAtUtc.Should().Be(updatedAt);
        _service.IsTvConnected(second).Should().BeFalse();
    }

    [Fact]
    public void Observation_ConflictingEqualSequenceReturnsConflictWithoutChangingStoredObservation()
    {
        var session = Start();
        var first = Observe(session, observationSequence: 1).Value!;

        var result = Observe(first, observationSequence: 1, position: 42);

        result.Error.Should().Be(PlaybackSessionError.ObservationRevisionConflict);
        _service.GetCurrent()!.Observation.Should().BeSameAs(first.Observation);
    }

    [Fact]
    public void TvConnectivity_UsesFiveSecondObservationFreshness()
    {
        var session = Observe(Start()).Value!;
        _service.IsTvConnected(session).Should().BeTrue();

        _time.Advance(TimeSpan.FromSeconds(5));
        _service.IsTvConnected(session).Should().BeTrue();
        _time.Advance(TimeSpan.FromMilliseconds(1));
        _service.IsTvConnected(session).Should().BeFalse();
    }

    [Theory]
    [InlineData(PlaybackDesiredState.Stopped, PlaybackObservationState.Playing)]
    [InlineData(PlaybackDesiredState.Playing, PlaybackObservationState.Ended)]
    public void TerminalSession_ExpiresAfterThirtySeconds(
        PlaybackDesiredState desiredState,
        PlaybackObservationState observedState
    )
    {
        var session = Start();
        session = UpdateIntent(session.SessionId, desiredState).Value!;
        Observe(session, state: observedState);

        _time.Advance(TimeSpan.FromSeconds(30));
        _service.GetCurrent().Should().NotBeNull();
        _time.Advance(TimeSpan.FromMilliseconds(1));
        _service.GetCurrent().Should().BeNull();
    }

    [Fact]
    public void NonterminalSession_ExpiresAfterThirtyMinutesWithoutObservation()
    {
        var session = Observe(Start()).Value!;
        _time.Advance(TimeSpan.FromMinutes(29));
        UpdateIntent(session.SessionId, PlaybackDesiredState.Paused).IsSuccess.Should().BeTrue();
        _time.Advance(TimeSpan.FromMinutes(1));
        _service.GetCurrent().Should().NotBeNull();
        _time.Advance(TimeSpan.FromMilliseconds(1));
        _service.GetCurrent().Should().BeNull();
    }

    [Theory]
    [InlineData("missing.mp4")]
    [InlineData("Films/notes.txt")]
    [InlineData("../outside.mp4")]
    [InlineData("")]
    public void Start_RejectsInvalidMediaPaths(string path)
    {
        _service.Start(new StartPlaybackRequest(path, 0, null)).Error
            .Should()
            .Be(PlaybackSessionError.InvalidRequest);
    }

    private PlaybackSession Start(double position = 0, int? subtitleIndex = null) =>
        _service.Start(new StartPlaybackRequest("Films/movie.mp4", position, subtitleIndex)).Value!;

    private PlaybackSessionResult<PlaybackSession> UpdateIntent(
        Guid sessionId,
        PlaybackDesiredState state = PlaybackDesiredState.Playing,
        double? seek = null
    ) => _service.UpdateIntent(new UpdatePlaybackIntentRequest(sessionId, state, null, seek));

    private PlaybackSessionResult<PlaybackSession> Observe(
        PlaybackSession session,
        Guid? sessionId = null,
        long? appliedRevision = null,
        long? appliedSeekId = null,
        PlaybackObservationState state = PlaybackObservationState.Playing,
        double position = 1,
        double duration = 100,
        string? relativePath = "Films/movie.mp4",
        string? errorCode = null,
        string? errorMessage = null,
        long? observationSequence = null
    ) =>
        _service.ReportObservation(
            new ReportPlaybackObservationRequest(
                sessionId ?? session.SessionId,
                observationSequence ?? ((_service.GetCurrent()?.Observation?.ObservationSequence ?? 0) + 1),
                appliedRevision ?? session.Revision,
                appliedSeekId,
                state,
                relativePath,
                position,
                duration,
                session.Intent.SubtitleIndex,
                errorCode,
                errorMessage
            )
        );

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
