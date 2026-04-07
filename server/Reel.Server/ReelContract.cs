using Reel.Server.Application.Models;
using Reel.Server.Domain;
using Rivet;

namespace Reel.Server;

[RivetContract]
public static class ReelContract
{
    public static readonly RouteDefinition<ConnectRequest, ConnectResponse> Connect = Define
        .Post<ConnectRequest, ConnectResponse>("/api/connect")
        .Returns<ConnectResponse>(401);

    public static readonly RouteDefinition<HealthResponse> Health = Define.Get<HealthResponse>("/api/health");

    public static readonly RouteDefinition<FileNode> Library = Define.Get<FileNode>("/api/library");

    public static readonly InputRouteDefinition<CastRequest> Cast = Define
        .Post("/api/cast")
        .Accepts<CastRequest>()
        .Status(204);

    public static readonly RouteDefinition<CastStatus> CastCurrent = Define.Get<CastStatus>("/api/cast/current");

    public static readonly RouteDefinition CastAck = Define.Post("/api/cast/ack").Status(204);

    public static readonly RouteDefinition CastStop = Define.Post("/api/cast/stop").Status(204);

    public static readonly InputRouteDefinition<ProgressRequest> ProgressUpdate = Define
        .Post("/api/progress")
        .Accepts<ProgressRequest>()
        .Status(200);

    public static readonly RouteDefinition<WatchProgress[]> ProgressAll = Define.Get<WatchProgress[]>("/api/progress");

    public static readonly InputRouteDefinition<FavouriteRequest> FavouritesAdd = Define
        .Post("/api/favourites")
        .Accepts<FavouriteRequest>()
        .Status(200);

    public static readonly RouteDefinition<Favourite[]> FavouritesAll = Define.Get<Favourite[]>("/api/favourites");

    public static readonly InputRouteDefinition<FileRequest> FavouritesRemove = Define
        .Delete("/api/favourites")
        .Accepts<FileRequest>();

    public static readonly FileRouteDefinition<FileRequest> Stream = Define
        .File<FileRequest>("/api/files/stream")
        .QueryAuth();

    public static readonly FileRouteDefinition<FileRequest> Thumbnail = Define
        .File<FileRequest>("/api/files/thumbnail")
        .ContentType("image/jpeg")
        .QueryAuth();

    public static readonly RouteDefinition<FileRequest, MetadataResponse> Metadata = Define.Get<
        FileRequest,
        MetadataResponse
    >("/api/files/metadata");

    public static readonly RouteDefinition<UserRequest, User> UserCreate = Define.Post<UserRequest, User>("/api/users");

    public static readonly RouteDefinition<User[]> UserList = Define.Get<User[]>("/api/users");

    public static readonly RouteDefinition<User> UserGet = Define.Get<User>("/api/users/{id}");

    public static readonly RouteDefinition<UserRequest, User> UserUpdate = Define.Patch<UserRequest, User>(
        "/api/users/{id}"
    );

    public static readonly RouteDefinition UserDelete = Define.Delete("/api/users/{id}");

    public static readonly RouteDefinition MediaSetWatched = Define.Put("/api/media/watched").Status(204);

    public static readonly RouteDefinition MediaSetUnwatched = Define.Put("/api/media/unwatched").Status(204);

    public static readonly RouteDefinition MediaClearWatched = Define.Delete("/api/media/watched").Status(204);
}
