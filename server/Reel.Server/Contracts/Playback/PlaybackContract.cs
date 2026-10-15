using Reel.Server.Common;
using Rivet;

namespace Reel.Server.Contracts.Playback;

[RivetContract]
public static class PlaybackContract
{
    public const string SessionRoute = "/api/playback/session";
    public const string IntentRoute = "/api/playback/session/intent";
    public const string ObservationRoute = "/api/playback/session/observation";

    public static readonly RouteDefinition<
        StartPlaybackSessionRequest,
        PlaybackSessionResponse
    > Start = Define
        .Post<StartPlaybackSessionRequest, PlaybackSessionResponse>(SessionRoute)
        .Status(200)
        .Returns<ErrorResponse>(400);

    public static readonly RouteDefinition<PlaybackSessionEnvelope> Get =
        Define.Get<PlaybackSessionEnvelope>(SessionRoute);

    public static readonly RouteDefinition<
        UpdatePlaybackIntentRequest,
        PlaybackSessionResponse
    > UpdateIntent = Define
        .Put<UpdatePlaybackIntentRequest, PlaybackSessionResponse>(IntentRoute)
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404)
        .Returns<ErrorResponse>(409);

    public static readonly RouteDefinition<
        ReportPlaybackObservationRequest,
        PlaybackSessionResponse
    > ReportObservation = Define
        .Put<ReportPlaybackObservationRequest, PlaybackSessionResponse>(ObservationRoute)
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404)
        .Returns<ErrorResponse>(409);
}
