using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Reel.Server.Common;
using Reel.Server.Contracts.Playback;
using Rivet;

namespace Reel.Server.Playback;

// Each handler spells out its Success/Error calls because Rivet's contract check reads them from the handler body.
public static class PlaybackSessionEndpoints
{
    public static IEndpointRouteBuilder MapPlaybackSessionEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapPost(
            PlaybackContract.Start.Route,
            ([FromBody] StartPlaybackSessionRequest request, PlaybackSessionService service) =>
            {
                var endpoint = PlaybackContract.Start.Bind(request);
                return service.Start(request) switch
                {
                    PlaybackSessionOutcome.Ok ok => endpoint.Success(ok.Session).ToResult(),
                    PlaybackSessionOutcome.Failed failed => endpoint
                        .Error(StatusFor(failed.Error), ToErrorResponse(failed))
                        .ToResult(),
                    _ => throw new UnreachableException(),
                };
            }
        );

        endpoints.MapGet(
            PlaybackContract.Get.Route,
            (PlaybackSessionService service) =>
            {
                var envelope = new PlaybackSessionEnvelope(service.GetCurrent());
                return PlaybackContract.Get.Success(envelope).ToResult();
            }
        );

        endpoints.MapPut(
            PlaybackContract.UpdateIntent.Route,
            ([FromBody] UpdatePlaybackIntentRequest request, PlaybackSessionService service) =>
            {
                var endpoint = PlaybackContract.UpdateIntent.Bind(request);
                return service.UpdateIntent(request) switch
                {
                    PlaybackSessionOutcome.Ok ok => endpoint.Success(ok.Session).ToResult(),
                    PlaybackSessionOutcome.Failed failed => endpoint
                        .Error(StatusFor(failed.Error), ToErrorResponse(failed))
                        .ToResult(),
                    _ => throw new UnreachableException(),
                };
            }
        );

        endpoints.MapPut(
            PlaybackContract.ReportObservation.Route,
            ([FromBody] ReportPlaybackObservationRequest request, PlaybackSessionService service) =>
            {
                var endpoint = PlaybackContract.ReportObservation.Bind(request);
                return service.ReportObservation(request) switch
                {
                    PlaybackSessionOutcome.Ok ok => endpoint.Success(ok.Session).ToResult(),
                    PlaybackSessionOutcome.Failed failed => endpoint
                        .Error(StatusFor(failed.Error), ToErrorResponse(failed))
                        .ToResult(),
                    _ => throw new UnreachableException(),
                };
            }
        );

        return endpoints;
    }

    private static int StatusFor(PlaybackSessionError error) =>
        error switch
        {
            PlaybackSessionError.InvalidRequest => StatusCodes.Status400BadRequest,
            PlaybackSessionError.SessionNotFound => StatusCodes.Status404NotFound,
            PlaybackSessionError.RevisionConflict => StatusCodes.Status409Conflict,
            _ => throw new ArgumentOutOfRangeException(nameof(error)),
        };

    private static ErrorResponse ToErrorResponse(PlaybackSessionOutcome.Failed failed) =>
        new(
            failed.Error switch
            {
                PlaybackSessionError.InvalidRequest => "invalid_request",
                PlaybackSessionError.SessionNotFound => "session_not_found",
                PlaybackSessionError.RevisionConflict => "revision_conflict",
                _ => throw new ArgumentOutOfRangeException(nameof(failed)),
            },
            failed.Message
        );
}
