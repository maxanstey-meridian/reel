using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Reel.Server.Contracts.Playback;
using Reel.Server.Playback;

namespace Reel.Server.Tests;

public sealed class PlaybackSessionServiceTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero)
    );
    private readonly TempMediaRoot _media = new();
    private readonly PlaybackSessionService _service;

    public PlaybackSessionServiceTests()
    {
        _media.Write("Films/movie.mp4");
        _media.Write("Films/notes.txt");
        _service = new PlaybackSessionService(_media.Paths, _time);
    }

    public void Dispose() => _media.Dispose();

    [Fact]
    public void Start_CreatesPlayingSessionAndReplacesExistingSession()
    {
        var first = Start();
        _time.Advance(TimeSpan.FromSeconds(1));

        var second = Start(12.5, 2);

        second.SessionId.Should().NotBe(first.SessionId);
        second.Revision.Should().Be(1);
        second.Intent.DesiredState.Should().Be(PlaybackDesiredStateDto.Playing);
        second.Intent.SubtitleIndex.Should().Be(2);
        second.Intent.PendingSeek.Should().Be(new SeekRequestResponse(1, 12.5));
        second.Observation.Should().BeNull();
        _service.GetCurrent().Should().Be(second);
    }

    [Fact]
    public void Start_StoresTheCanonicalPath() =>
        _service
            .Start(new StartPlaybackSessionRequest("Films//movie.mp4", 0, null))
            .Session()
            .Intent.RelativePath.Should()
            .Be("Films/movie.mp4");

    [Fact]
    public void UpdateIntent_IncrementsRevisionAndRetainsUnacknowledgedSeek()
    {
        var session = Start(10);

        var result = _service.UpdateIntent(
            new UpdatePlaybackIntentRequest(
                session.SessionId,
                PlaybackDesiredStateDto.Paused,
                3,
                null
            )
        );

        result.Session().Revision.Should().Be(2);
        result.Session().Intent.DesiredState.Should().Be(PlaybackDesiredStateDto.Paused);
        result.Session().Intent.SubtitleIndex.Should().Be(3);
        result.Session().Intent.PendingSeek.Should().Be(new SeekRequestResponse(1, 10));
    }

    [Fact]
    public void UpdateIntent_ReplacesPendingSeekAndKeepsSeekIdsMonotonicAfterAcknowledgement()
    {
        var session = Start(10);
        var collapsed = UpdateIntent(session.SessionId, seek: 20).Session();
        collapsed.Intent.PendingSeek.Should().Be(new SeekRequestResponse(2, 20));

        Observe(collapsed, appliedSeekId: 2).Session().Intent.PendingSeek.Should().BeNull();
        var next = UpdateIntent(session.SessionId, seek: 30).Session();

        next.Intent.PendingSeek.Should().Be(new SeekRequestResponse(3, 30));
    }

    [Fact]
    public void UpdateIntent_ClampsSeekToKnownDuration()
    {
        var session = Start();
        Observe(session, duration: 100);

        var result = UpdateIntent(session.SessionId, seek: 150);

        result.Session().Intent.PendingSeek!.PositionSeconds.Should().Be(100);
    }

    [Fact]
    public void UpdateIntent_ReplacesSubtitleWithNull()
    {
        var session = Start(subtitleIndex: 4);

        var result = _service.UpdateIntent(
            new UpdatePlaybackIntentRequest(
                session.SessionId,
                PlaybackDesiredStateDto.Playing,
                null,
                null
            )
        );

        result.Session().Intent.SubtitleIndex.Should().BeNull();
    }

    [Fact]
    public void UpdateIntent_RejectsSeekWhenDesiredStateIsStopped()
    {
        var session = Start();

        var result = UpdateIntent(session.SessionId, PlaybackDesiredStateDto.Stopped, seek: 10);

        result.Error().Should().Be(PlaybackSessionError.InvalidRequest);
        _service.GetCurrent()!.Revision.Should().Be(session.Revision);
    }

    [Fact]
    public void UnknownSessionIds_AreRejected()
    {
        UpdateIntent(Guid.NewGuid()).Error().Should().Be(PlaybackSessionError.SessionNotFound);
        Observe(Start(), sessionId: Guid.NewGuid())
            .Error()
            .Should()
            .Be(PlaybackSessionError.SessionNotFound);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.1)]
    public void Start_RejectsInvalidPositions(double position)
    {
        _service
            .Start(new StartPlaybackSessionRequest("Films/movie.mp4", position, null))
            .Error()
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

        result.Error().Should().Be(PlaybackSessionError.InvalidRequest);
    }

    [Fact]
    public void Observation_RejectsAppliedRevisionRegressionWithoutChangingStoredObservation()
    {
        var session = Start();
        var revised = UpdateIntent(session.SessionId, PlaybackDesiredStateDto.Paused).Session();
        var current = Observe(revised, appliedRevision: 2).Session();

        var result = Observe(current, appliedRevision: 1);

        result.Error().Should().Be(PlaybackSessionError.RevisionConflict);
        _service.GetCurrent()!.Observation!.AppliedRevision.Should().Be(2);
    }

    [Fact]
    public void Observation_RejectsAppliedRevisionAheadOfCurrentSession()
    {
        var session = Start();

        var result = Observe(session, appliedRevision: session.Revision + 1);

        result.Error().Should().Be(PlaybackSessionError.RevisionConflict);
        _service.GetCurrent()!.Observation.Should().BeNull();
    }

    [Fact]
    public void Observation_RejectsReorderedSequenceAtEqualRevision()
    {
        var session = Start();
        Observe(session, observationSequence: 2).Should().BeOfType<PlaybackSessionOutcome.Ok>();

        var result = Observe(session, observationSequence: 1);

        result.Error().Should().Be(PlaybackSessionError.RevisionConflict);
        _service.GetCurrent()!.Observation!.ObservationSequence.Should().Be(2);
    }

    [Fact]
    public void Observation_RejectsSeekAcknowledgementBeyondLatestAllocatedSeek()
    {
        var session = Start(10);

        var result = Observe(session, appliedSeekId: 2);

        result.Error().Should().Be(PlaybackSessionError.InvalidRequest);
        _service.GetCurrent()!.Intent.PendingSeek.Should().Be(new SeekRequestResponse(1, 10));
    }

    [Theory]
    [InlineData(PlaybackObservationStateDto.Loading)]
    [InlineData(PlaybackObservationStateDto.Buffering)]
    [InlineData(PlaybackObservationStateDto.Playing)]
    [InlineData(PlaybackObservationStateDto.Paused)]
    [InlineData(PlaybackObservationStateDto.Ended)]
    public void Observation_RequiresPathForStatesThatReferToMedia(PlaybackObservationStateDto state)
    {
        var result = Observe(Start(), state: state, relativePath: null);

        result.Error().Should().Be(PlaybackSessionError.InvalidRequest);
    }

    [Theory]
    [InlineData(PlaybackObservationStateDto.Idle)]
    [InlineData(PlaybackObservationStateDto.Error)]
    public void Observation_AllowsNullPathForStatesThatMayNotHaveMedia(
        PlaybackObservationStateDto state
    )
    {
        var result = Observe(
            Start(),
            state: state,
            relativePath: null,
            errorCode: state == PlaybackObservationStateDto.Error ? "player" : null,
            errorMessage: state == PlaybackObservationStateDto.Error ? "Playback failed" : null
        );

        result.Should().BeOfType<PlaybackSessionOutcome.Ok>();
    }

    [Fact]
    public void Observation_RejectsPathThatDoesNotNormalizeToIntentPath()
    {
        var session = Start();

        Observe(session, relativePath: "Films/other.mp4")
            .Error()
            .Should()
            .Be(PlaybackSessionError.InvalidRequest);
    }

    [Fact]
    public void Observation_RejectsMissingOrPartialErrorDetails()
    {
        var session = Start();

        Observe(session, state: PlaybackObservationStateDto.Error, errorCode: "decoder")
            .Error()
            .Should()
            .Be(PlaybackSessionError.InvalidRequest);
        Observe(session, state: PlaybackObservationStateDto.Error, errorMessage: "Failed")
            .Error()
            .Should()
            .Be(PlaybackSessionError.InvalidRequest);
    }

    [Fact]
    public void Observation_RejectsErrorDetailsForNonErrorState()
    {
        var result = Observe(Start(), errorCode: "decoder", errorMessage: "Failed");

        result.Error().Should().Be(PlaybackSessionError.InvalidRequest);
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
                PlaybackObservationStateDto.Error,
                "Films/movie.mp4",
                12,
                120,
                5,
                "decoder",
                "Could not decode"
            )
        );

        result.Session().Intent.PendingSeek.Should().BeNull();
        result
            .Session()
            .Observation.Should()
            .Be(
                new PlaybackObservationResponse(
                    1,
                    1,
                    1,
                    PlaybackObservationStateDto.Error,
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
        var first = Observe(session, observationSequence: 1, appliedSeekId: 1).Session();
        var updatedAt = first.Observation!.UpdatedAtUtc;
        _time.Advance(TimeSpan.FromSeconds(10));

        var second = Observe(first, observationSequence: 1, appliedSeekId: 1).Session();

        second.Intent.PendingSeek.Should().BeNull();
        second.Observation.Should().BeSameAs(first.Observation);
        second.Observation!.UpdatedAtUtc.Should().Be(updatedAt);
        _service.GetCurrent()!.TvConnected.Should().BeFalse();
    }

    [Fact]
    public void Observation_ConflictingEqualSequenceReturnsConflictWithoutChangingStoredObservation()
    {
        var session = Start();
        var first = Observe(session, observationSequence: 1).Session();

        var result = Observe(first, observationSequence: 1, position: 42);

        result.Error().Should().Be(PlaybackSessionError.RevisionConflict);
        _service.GetCurrent()!.Observation.Should().BeSameAs(first.Observation);
    }

    [Fact]
    public void TvConnectivity_UsesFiveSecondObservationFreshness()
    {
        Observe(Start()).Session().TvConnected.Should().BeTrue();

        _time.Advance(TimeSpan.FromSeconds(5));
        _service.GetCurrent()!.TvConnected.Should().BeTrue();
        _time.Advance(TimeSpan.FromMilliseconds(1));
        _service.GetCurrent()!.TvConnected.Should().BeFalse();
    }

    [Theory]
    [InlineData(PlaybackDesiredStateDto.Stopped, PlaybackObservationStateDto.Playing)]
    [InlineData(PlaybackDesiredStateDto.Playing, PlaybackObservationStateDto.Ended)]
    public void TerminalSession_ExpiresAfterThirtySeconds(
        PlaybackDesiredStateDto desiredState,
        PlaybackObservationStateDto observedState
    )
    {
        var session = Start();
        session = UpdateIntent(session.SessionId, desiredState).Session();
        Observe(session, state: observedState);

        _time.Advance(TimeSpan.FromSeconds(30));
        _service.GetCurrent().Should().NotBeNull();
        _time.Advance(TimeSpan.FromMilliseconds(1));
        _service.GetCurrent().Should().BeNull();
    }

    [Fact]
    public void StoppedSession_ExpiresThirtySecondsAfterTheTvStopsEvenWhileItKeepsReporting()
    {
        var session = Observe(Start()).Session();
        session = UpdateIntent(session.SessionId, PlaybackDesiredStateDto.Stopped).Session();
        Observe(session, state: PlaybackObservationStateDto.Idle, relativePath: null);

        for (var second = 1; second <= 30; second++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            Observe(
                    _service.GetCurrent()!,
                    state: PlaybackObservationStateDto.Idle,
                    relativePath: null
                )
                .Should()
                .BeOfType<PlaybackSessionOutcome.Ok>();
        }

        _service.GetCurrent().Should().NotBeNull();
        _time.Advance(TimeSpan.FromMilliseconds(1));
        _service.GetCurrent().Should().BeNull();
    }

    [Fact]
    public void NonterminalSession_ExpiresAfterThirtyMinutesWithoutObservation()
    {
        var session = Observe(Start()).Session();
        _time.Advance(TimeSpan.FromMinutes(29));
        UpdateIntent(session.SessionId, PlaybackDesiredStateDto.Paused)
            .Should()
            .BeOfType<PlaybackSessionOutcome.Ok>();
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
        _service
            .Start(new StartPlaybackSessionRequest(path, 0, null))
            .Error()
            .Should()
            .Be(PlaybackSessionError.InvalidRequest);
    }

    private PlaybackSessionResponse Start(double position = 0, int? subtitleIndex = null) =>
        _service
            .Start(new StartPlaybackSessionRequest("Films/movie.mp4", position, subtitleIndex))
            .Session();

    private PlaybackSessionOutcome UpdateIntent(
        Guid sessionId,
        PlaybackDesiredStateDto state = PlaybackDesiredStateDto.Playing,
        double? seek = null
    ) => _service.UpdateIntent(new UpdatePlaybackIntentRequest(sessionId, state, null, seek));

    private PlaybackSessionOutcome Observe(
        PlaybackSessionResponse session,
        Guid? sessionId = null,
        long? appliedRevision = null,
        long? appliedSeekId = null,
        PlaybackObservationStateDto state = PlaybackObservationStateDto.Playing,
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
                observationSequence
                    ?? ((_service.GetCurrent()?.Observation?.ObservationSequence ?? 0) + 1),
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
}

internal static class PlaybackSessionOutcomeAssertions
{
    public static PlaybackSessionResponse Session(this PlaybackSessionOutcome outcome) =>
        outcome.Should().BeOfType<PlaybackSessionOutcome.Ok>().Subject.Session;

    public static PlaybackSessionError Error(this PlaybackSessionOutcome outcome) =>
        outcome.Should().BeOfType<PlaybackSessionOutcome.Failed>().Subject.Error;
}
