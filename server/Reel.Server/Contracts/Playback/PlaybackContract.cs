namespace Reel.Server.Contracts.Playback;

using Rivet;

[RivetContract]
public static class PlaybackContract
{
    public const string SessionRoute = "/api/playback/session";
    public const string IntentRoute = "/api/playback/session/intent";
    public const string ObservationRoute = "/api/playback/session/observation";

    public static readonly RouteDefinition<StartPlaybackSessionRequest, PlaybackSessionResponse> Start = Define
        .Post<StartPlaybackSessionRequest, PlaybackSessionResponse>(SessionRoute)
        .Status(200)
        .Returns<PlaybackErrorResponse>(400);

    public static readonly RouteDefinition<PlaybackSessionEnvelope> Get =
        Define.Get<PlaybackSessionEnvelope>(SessionRoute);

    public static readonly RouteDefinition<UpdatePlaybackIntentRequest, PlaybackSessionResponse> UpdateIntent = Define
        .Put<UpdatePlaybackIntentRequest, PlaybackSessionResponse>(IntentRoute)
        .Returns<PlaybackErrorResponse>(400)
        .Returns<PlaybackErrorResponse>(404);

    public static readonly RouteDefinition<ReportPlaybackObservationRequest, PlaybackSessionResponse> ReportObservation =
        Define
            .Put<ReportPlaybackObservationRequest, PlaybackSessionResponse>(ObservationRoute)
            .Returns<PlaybackErrorResponse>(400)
            .Returns<PlaybackErrorResponse>(404)
            .Returns<PlaybackErrorResponse>(409);
}
