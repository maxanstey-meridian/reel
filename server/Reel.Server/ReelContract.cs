using Reel.Server.Models;
using Rivet;

namespace Reel.Server;

[RivetContract]
public static class ReelContract
{
    public static readonly RouteDefinition<ConnectRequest, ConnectResponse> Connect =
        Define.Post<ConnectRequest, ConnectResponse>("/api/connect")
            .Returns<ConnectResponse>(401);

    public static readonly RouteDefinition<HealthResponse> Health =
        Define.Get<HealthResponse>("/api/health");

    public static readonly RouteDefinition<FileNode> Library =
        Define.Get<FileNode>("/api/library");

    public static readonly InputRouteDefinition<CastRequest> Cast =
        Define.Post("/api/cast").Accepts<CastRequest>();

    public static readonly RouteDefinition<CastStatus> CastCurrent =
        Define.Get<CastStatus>("/api/cast/current");

    public static readonly RouteDefinition CastAck =
        Define.Post("/api/cast/ack");

    public static readonly RouteDefinition CastStop =
        Define.Post("/api/cast/stop");
}
