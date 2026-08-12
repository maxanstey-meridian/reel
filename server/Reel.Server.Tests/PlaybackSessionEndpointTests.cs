namespace Reel.Server.Tests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Reel.Server.Contracts.Playback;

public sealed class PlaybackSessionEndpointTests : IDisposable
{
    private const string SecretKey = "REEL-TEST";
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _mediaRoot;

    public PlaybackSessionEndpointTests()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"reel-playback-endpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_mediaRoot, "Films"));
        File.WriteAllBytes(Path.Combine(_mediaRoot, "Films", "movie.mp4"), [1]);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", _mediaRoot);
            builder.UseSetting("Reel:SecretKey", SecretKey);
            builder.UseSetting("Reel:Port", "8080");
        });
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Secret-Key", SecretKey);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        Directory.Delete(_mediaRoot, recursive: true);
    }

    [Fact]
    public void Contract_OwnsExactPlaybackRoutes()
    {
        PlaybackContract.Start.Route.Should().Be("/api/playback/session");
        PlaybackContract.Get.Route.Should().Be("/api/playback/session");
        PlaybackContract.UpdateIntent.Route.Should().Be("/api/playback/session/intent");
        PlaybackContract.ReportObservation.Route.Should().Be("/api/playback/session/observation");
    }

    [Fact]
    public async Task Get_ReturnsTypedEnvelopeWithNullSession()
    {
        var response = await _client.GetAsync(PlaybackContract.Get.Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<PlaybackSessionEnvelope>())!.Session.Should().BeNull();
    }

    [Fact]
    public async Task Start_ReturnsCompleteSessionAndReplacesIt()
    {
        var first = await Start();
        var second = await Start(15);

        second.SessionId.Should().NotBe(first.SessionId);
        second.Intent.PendingSeek.Should().Be(new SeekRequestResponse(1, 15, 1));
        second.TvConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Start_InvalidPathAndPosition_ReturnExplicitBadRequest()
    {
        var pathResponse = await _client.PostAsJsonAsync(
            PlaybackContract.Start.Route,
            new StartPlaybackSessionRequest("missing.mp4", 0, null)
        );
        var positionResponse = await _client.PostAsJsonAsync(
            PlaybackContract.Start.Route,
            new StartPlaybackSessionRequest("Films/movie.mp4", -1, null)
        );

        pathResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        positionResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await pathResponse.Content.ReadFromJsonAsync<PlaybackErrorResponse>())!.Error.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Intent_ReturnsUpdatedTransportContractAndStaleIdReturnsNotFound()
    {
        var session = await Start();
        var update = await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new UpdatePlaybackIntentRequest(session.SessionId, PlaybackDesiredStateDto.Paused, 2, 30)
        );
        var stale = await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new UpdatePlaybackIntentRequest(Guid.NewGuid(), PlaybackDesiredStateDto.Playing, null, null)
        );

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<PlaybackSessionResponse>();
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
        var valid = await _client.PutAsJsonAsync(PlaybackContract.ReportObservation.Route, validRequest);
        var invalid = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            Observation(session, position: -1)
        );
        var missing = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            Observation(session with { SessionId = Guid.NewGuid() })
        );
        var regressing = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            Observation(session, appliedRevision: 0)
        );

        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        var observed = await valid.Content.ReadFromJsonAsync<PlaybackSessionResponse>();
        observed!.Observation!.State.Should().Be(PlaybackObservationStateDto.Playing);
        observed.Intent.PendingSeek.Should().BeNull();
        observed.TvConnected.Should().BeTrue();
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        regressing.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Observation_RevisionAheadOfSessionReturnsDeclaredConflict()
    {
        var session = await Start();

        var response = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            Observation(session, appliedRevision: session.Revision + 1)
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<PlaybackErrorResponse>())!.Error
            .Should()
            .Contain("current session revision");
    }

    [Fact]
    public async Task Intent_StoppedWithSeekAndObservationImpossibleValuesReturnBadRequest()
    {
        var session = await Start(10);
        var stoppedSeek = await _client.PutAsJsonAsync(
            PlaybackContract.UpdateIntent.Route,
            new UpdatePlaybackIntentRequest(session.SessionId, PlaybackDesiredStateDto.Stopped, null, 20)
        );
        var wrongPath = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            Observation(session) with { RelativePath = "Films/other.mp4" }
        );
        var futureSeek = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            Observation(session, appliedSeekId: 2)
        );
        var inconsistentError = await _client.PutAsJsonAsync(
            PlaybackContract.ReportObservation.Route,
            Observation(session) with { ErrorCode = "decoder", ErrorMessage = "Failed" }
        );

        stoppedSeek.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        wrongPath.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        futureSeek.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        inconsistentError.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<PlaybackSessionResponse> Start(double position = 0)
    {
        var response = await _client.PostAsJsonAsync(
            PlaybackContract.Start.Route,
            new StartPlaybackSessionRequest("Films/movie.mp4", position, null)
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PlaybackSessionResponse>())!;
    }

    private static ReportPlaybackObservationRequest Observation(
        PlaybackSessionResponse session,
        long appliedRevision = 1,
        long? appliedSeekId = null,
        double position = 10
    ) =>
        new(
            session.SessionId,
            1,
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
