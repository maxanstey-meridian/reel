namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application;
using Reel.Server.Application.Models;
using Reel.Server.Contracts.Playback;
using Reel.Server.Domain;
using Rivet;
using ApplicationIntentRequest = Application.Models.UpdatePlaybackIntentRequest;
using ApplicationObservationRequest = Application.Models.ReportPlaybackObservationRequest;
using TransportIntentRequest = Contracts.Playback.UpdatePlaybackIntentRequest;
using TransportObservationRequest = Contracts.Playback.ReportPlaybackObservationRequest;

public static class PlaybackSessionEndpoints
{
    public static IEndpointRouteBuilder MapPlaybackSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            PlaybackContract.Start.Route,
            ([FromBody] StartPlaybackSessionRequest request, PlaybackSessionService service) =>
            {
                var endpoint = PlaybackContract.Start.Bind(request);
                var result = service.Start(
                    new StartPlaybackRequest(request.RelativePath, request.StartPositionSeconds, request.SubtitleIndex)
                );
                if (result.IsSuccess)
                    return endpoint.Success(Map(result.Value!, service)).ToResult();
                return endpoint.Error(400, new PlaybackErrorResponse(result.Message!)).ToResult();
            }
        );

        endpoints.MapGet(
            PlaybackContract.Get.Route,
            (PlaybackSessionService service) =>
            {
                var session = service.GetCurrent();
                var response = new PlaybackSessionEnvelope(session is null ? null : Map(session, service));
                return PlaybackContract.Get.Success(response).ToResult();
            }
        );

        endpoints.MapPut(
            PlaybackContract.UpdateIntent.Route,
            ([FromBody] TransportIntentRequest request, PlaybackSessionService service) =>
            {
                var endpoint = PlaybackContract.UpdateIntent.Bind(request);
                var result = service.UpdateIntent(
                    new ApplicationIntentRequest(
                        request.SessionId,
                        Map(request.DesiredState),
                        request.SubtitleIndex,
                        request.SeekPositionSeconds
                    )
                );
                if (result.IsSuccess)
                    return endpoint.Success(Map(result.Value!, service)).ToResult();
                var status = result.Error == PlaybackSessionError.InvalidRequest ? 400 : 404;
                return endpoint.Error(status, new PlaybackErrorResponse(result.Message!)).ToResult();
            }
        );

        endpoints.MapPut(
            PlaybackContract.ReportObservation.Route,
            ([FromBody] TransportObservationRequest request, PlaybackSessionService service) =>
            {
                var endpoint = PlaybackContract.ReportObservation.Bind(request);
                var result = service.ReportObservation(
                    new ApplicationObservationRequest(
                        request.SessionId,
                        request.ObservationSequence,
                        request.AppliedRevision,
                        request.AppliedSeekId,
                        Map(request.State),
                        request.RelativePath,
                        request.PositionSeconds,
                        request.DurationSeconds,
                        request.SubtitleIndex,
                        request.ErrorCode,
                        request.ErrorMessage
                    )
                );
                if (result.IsSuccess)
                    return endpoint.Success(Map(result.Value!, service)).ToResult();
                var status = result.Error switch
                {
                    PlaybackSessionError.InvalidRequest => 400,
                    PlaybackSessionError.SessionNotFound => 404,
                    PlaybackSessionError.ObservationRevisionConflict => 409,
                    _ => throw new InvalidOperationException("Unknown playback session error."),
                };
                return endpoint.Error(status, new PlaybackErrorResponse(result.Message!)).ToResult();
            }
        );

        return endpoints;
    }

    private static PlaybackSessionResponse Map(PlaybackSession session, PlaybackSessionService service) =>
        new(
            session.SessionId,
            session.Revision,
            session.CreatedAtUtc,
            session.UpdatedAtUtc,
            new PlaybackIntentResponse(
                session.Intent.RelativePath,
                Map(session.Intent.DesiredState),
                session.Intent.SubtitleIndex,
                session.Intent.PendingSeek is null
                    ? null
                    : new SeekRequestResponse(
                        session.Intent.PendingSeek.SeekId,
                        session.Intent.PendingSeek.PositionSeconds,
                        session.Intent.PendingSeek.IntroducedRevision
                    )
            ),
            session.Observation is null
                ? null
                : new PlaybackObservationResponse(
                    session.Observation.ObservationSequence,
                    session.Observation.AppliedRevision,
                    session.Observation.AppliedSeekId,
                    Map(session.Observation.State),
                    session.Observation.RelativePath,
                    session.Observation.PositionSeconds,
                    session.Observation.DurationSeconds,
                    session.Observation.SubtitleIndex,
                    session.Observation.ErrorCode,
                    session.Observation.ErrorMessage,
                    session.Observation.UpdatedAtUtc
                ),
            service.IsTvConnected(session)
        );

    private static PlaybackDesiredState Map(PlaybackDesiredStateDto state) => state switch
    {
        PlaybackDesiredStateDto.Playing => PlaybackDesiredState.Playing,
        PlaybackDesiredStateDto.Paused => PlaybackDesiredState.Paused,
        PlaybackDesiredStateDto.Stopped => PlaybackDesiredState.Stopped,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static PlaybackDesiredStateDto Map(PlaybackDesiredState state) => state switch
    {
        PlaybackDesiredState.Playing => PlaybackDesiredStateDto.Playing,
        PlaybackDesiredState.Paused => PlaybackDesiredStateDto.Paused,
        PlaybackDesiredState.Stopped => PlaybackDesiredStateDto.Stopped,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static PlaybackObservationState Map(PlaybackObservationStateDto state) => state switch
    {
        PlaybackObservationStateDto.Idle => PlaybackObservationState.Idle,
        PlaybackObservationStateDto.Loading => PlaybackObservationState.Loading,
        PlaybackObservationStateDto.Buffering => PlaybackObservationState.Buffering,
        PlaybackObservationStateDto.Playing => PlaybackObservationState.Playing,
        PlaybackObservationStateDto.Paused => PlaybackObservationState.Paused,
        PlaybackObservationStateDto.Ended => PlaybackObservationState.Ended,
        PlaybackObservationStateDto.Error => PlaybackObservationState.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static PlaybackObservationStateDto Map(PlaybackObservationState state) => state switch
    {
        PlaybackObservationState.Idle => PlaybackObservationStateDto.Idle,
        PlaybackObservationState.Loading => PlaybackObservationStateDto.Loading,
        PlaybackObservationState.Buffering => PlaybackObservationStateDto.Buffering,
        PlaybackObservationState.Playing => PlaybackObservationStateDto.Playing,
        PlaybackObservationState.Paused => PlaybackObservationStateDto.Paused,
        PlaybackObservationState.Ended => PlaybackObservationStateDto.Ended,
        PlaybackObservationState.Error => PlaybackObservationStateDto.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
