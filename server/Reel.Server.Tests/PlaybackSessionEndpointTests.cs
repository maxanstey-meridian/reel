using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Common;
using Reel.Server.Contracts.Playback;

namespace Reel.Server.Tests;

// HTTP mapping of the session service; validation rules themselves are covered by PlaybackSessionServiceTests.
public sealed class PlaybackSessionEndpointTests : IDisposable
{
    private readonly TempMediaRoot _media = new();
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _json;
    private readonly WebApplicationFactory<Program> _factory;

    public PlaybackSessionEndpointTests()
    {
        _media.Write("Films/movie.mp4");
        _factory = _media.CreateFactory();
        _json = _factory.JsonOptions();
        _client = _factory.CreateAuthenticatedClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _media.Dispose();
    }

    [Fact]
    public async Task Get_ReturnsTypedEnvelopeWithNullSession()
    {
        var response = await _client.GetAsync(PlaybackContract.Get.Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<PlaybackSessionEnvelope>(_json))!
            .Session.Should()
            .BeNull();
    }

    [Fact]
    public async Task Start_ReturnsCompleteSessionAndReplacesIt()
    {
        var first = await Start();
        var second = await Start(15);

        second.SessionId.Should().NotBe(first.SessionId);
        second.Intent.PendingSeek.Should().Be(new SeekRequestResponse(1, 15));
        second.TvConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Intent_ReturnsUpdatedTransportContractAndStaleIdReturnsNotFound()
    {
        var session = await Start();
        var update = await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new UpdatePlaybackIntentRequest(
                session.SessionId,
                PlaybackDesiredStateDto.Paused,
                2,
                null,
                30
            ),
            _json
        );
        var stale = await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new UpdatePlaybackIntentRequest(
                Guid.NewGuid(),
                PlaybackDesiredStateDto.Playing,
                null,
                null,
                null
            ),
            _json
        );

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<PlaybackSessionResponse>(_json);
        updated!.Revision.Should().Be(2);
        updated.Intent.DesiredState.Should().Be(PlaybackDesiredStateDto.Paused);
        updated.Intent.SubtitleIndex.Should().Be(2);
        stale.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Observation_ReturnsBadRequestNotFoundConflictAndSuccessfulContract()
    {
        var session = await Start(10);
        var validRequest = Observation(session, appliedRevision: 1, appliedSeekId: 1);
        var valid = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            validRequest,
            _json
        );
        var invalid = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            Observation(session, position: -1),
            _json
        );
        var missing = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            Observation(session with { SessionId = Guid.NewGuid() }),
            _json
        );
        await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new { sessionId = session.SessionId, desiredState = "paused" },
            _json
        );
        var regressing = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            Observation(session, sequence: 2, appliedRevision: 0),
            _json
        );

        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        var observed = await valid.Content.ReadFromJsonAsync<PlaybackSessionResponse>(_json);
        observed!.Observation!.State.Should().Be(PlaybackObservationStateDto.Playing);
        observed.Intent.PendingSeek.Should().BeNull();
        observed.TvConnected.Should().BeTrue();
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        regressing.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await regressing.Content.ReadFromJsonAsync<ErrorResponse>(_json))!
            .Message.Should()
            .Be("Applied revision cannot regress.");
    }

    [Fact]
    public async Task IntegerEnumValues_AreRejectedAsBadRequest()
    {
        var session = await Start();

        var intent = await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new { sessionId = session.SessionId, desiredState = 9 },
            _json
        );
        var observation = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            new
            {
                sessionId = session.SessionId,
                observationSequence = 1,
                appliedRevision = 1,
                state = 9,
                relativePath = session.Intent.RelativePath,
                positionSeconds = 1,
                durationSeconds = 10,
            },
            _json
        );

        intent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        observation.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Intent_WithMatchingExpectedRevision_Applies()
    {
        var session = await Start();

        var response = await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new
            {
                sessionId = session.SessionId,
                desiredState = "playing",
                subtitleIndex = 1,
                expectedRevision = session.Revision,
            },
            _json
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<PlaybackSessionResponse>(_json);
        updated!.Revision.Should().Be(session.Revision + 1);
        updated.Intent.SubtitleIndex.Should().Be(1);
    }

    [Fact]
    public async Task Intent_WithStaleExpectedRevision_ReturnsConflictWithoutChangingSession()
    {
        var session = await Start();
        await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new { sessionId = session.SessionId, desiredState = "paused" },
            _json
        );

        var response = await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new
            {
                sessionId = session.SessionId,
                desiredState = "playing",
                subtitleIndex = 1,
                expectedRevision = session.Revision,
            },
            _json
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>(_json))!
            .Code.Should()
            .Be("revision_conflict");
        var current = await _client.GetFromJsonAsync<PlaybackSessionEnvelope>(
            PlaybackContract.Get.Route,
            _json
        );
        current!.Session!.Revision.Should().Be(2);
        current.Session.Intent.SubtitleIndex.Should().BeNull();
        current.Session.Intent.DesiredState.Should().Be(PlaybackDesiredStateDto.Paused);
    }

    // Sidecars sort by name, so movie's English track is 0 and next's is 1 (after "de").
    [Fact]
    public async Task QualityAndSubtitleLanguageChosenWhilePlaying_StartTheNextSession()
    {
        _media.Write("Films/movie.en.srt");
        _media.Write("Films/movie.fr.srt");
        _media.Write("Films/next.mp4");
        _media.Write("Films/Subs/next/1_German.srt");
        _media.Write("Films/Subs/next/2_English.srt");

        var first = await Start();
        first.Intent.MaxHeight.Should().BeNull();
        first.Intent.SubtitleIndex.Should().BeNull();
        await ChangeIntent(first, subtitleIndex: 0, maxHeight: 480);
        var next = await Start(path: "Films/next.mp4");

        next.Intent.MaxHeight.Should().Be(480);
        next.Intent.SubtitleIndex.Should().Be(1);
    }

    // movie.srt names no language, so choosing it keeps French; turning subtitles off is remembered as off.
    [Fact]
    public async Task ATrackOfUnknownLanguageKeepsTheRememberedOneAndOffIsRemembered()
    {
        _media.Write("Films/movie.fr.srt");
        _media.Write("Films/movie.srt");

        var session = await ChangeIntent(await Start(), subtitleIndex: 0, maxHeight: null);
        session = await ChangeIntent(session, subtitleIndex: 1, maxHeight: null);
        (await Start()).Intent.SubtitleIndex.Should().Be(0);

        await ChangeIntent(await Start(), subtitleIndex: null, maxHeight: 720);
        var off = await Start();

        off.Intent.SubtitleIndex.Should().BeNull();
        off.Intent.MaxHeight.Should().Be(720);
    }

    private async Task<PlaybackSessionResponse> ChangeIntent(
        PlaybackSessionResponse session,
        int? subtitleIndex,
        int? maxHeight
    )
    {
        var response = await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new UpdatePlaybackIntentRequest(
                session.SessionId,
                PlaybackDesiredStateDto.Playing,
                subtitleIndex,
                maxHeight,
                null
            ),
            _json
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PlaybackSessionResponse>(_json))!;
    }

    private async Task<PlaybackSessionResponse> Start(
        double position = 0,
        string path = "Films/movie.mp4"
    )
    {
        var response = await _client.PostAsJsonAsync(
            PlaybackContract.Start.Route,
            new StartPlaybackSessionRequest(path, position),
            _json
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PlaybackSessionResponse>(_json))!;
    }

    private static ReportPlaybackObservationRequest Observation(
        PlaybackSessionResponse session,
        long sequence = 1,
        long appliedRevision = 1,
        long? appliedSeekId = null,
        double position = 10
    ) =>
        new(
            session.SessionId,
            sequence,
            appliedRevision,
            appliedSeekId,
            PlaybackObservationStateDto.Playing,
            session.Intent.RelativePath,
            position,
            100,
            null,
            null,
            null
        );
}
