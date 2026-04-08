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

    public static readonly RouteDefinition<LibraryStatsResponse> LibraryStats =
        Define.Get<LibraryStatsResponse>("/api/library/stats");

    public static readonly RouteDefinition<string[]> LibraryPosters =
        Define.Get<string[]>("/api/library/posters");

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

    public static readonly RouteDefinition UserDelete = Define.Delete("/api/users/{id}").Status(204);

    public static readonly RouteDefinition<FileRequest, MediaDetailResponse> MediaDetail = Define.Get<
        FileRequest,
        MediaDetailResponse
    >("/api/media/detail");

    public static readonly InputRouteDefinition<MediaUpdateRequest> MediaUpdate = Define
        .Patch("/api/media/detail")
        .Accepts<MediaUpdateRequest>()
        .Status(204);

    public static readonly InputRouteDefinition<FileRequest> MediaSetWatched = Define
        .Put("/api/media/watched")
        .Accepts<FileRequest>()
        .Status(204);

    public static readonly InputRouteDefinition<FileRequest> MediaSetUnwatched = Define
        .Put("/api/media/unwatched")
        .Accepts<FileRequest>()
        .Status(204);

    public static readonly InputRouteDefinition<FileRequest> MediaClearWatched = Define
        .Delete("/api/media/watched")
        .Accepts<FileRequest>()
        .Status(204);

    public static readonly RouteDefinition<FileRequest, string[]> TagsList = Define.Get<FileRequest, string[]>(
        "/api/media/tags"
    );

    public static readonly InputRouteDefinition<SetTagsRequest> TagsReplace = Define
        .Put("/api/media/tags")
        .Accepts<SetTagsRequest>()
        .Status(204);

    public static readonly InputRouteDefinition<AddTagRequest> TagsAdd = Define
        .Post("/api/media/tags")
        .Accepts<AddTagRequest>()
        .Status(204);

    public static readonly InputRouteDefinition<RemoveTagRequest> TagsRemove = Define
        .Delete("/api/media/tags")
        .Accepts<RemoveTagRequest>()
        .Status(204);

    public static readonly RouteDefinition<TagCount[]> TagsAll = Define.Get<TagCount[]>("/api/tags");

    public static readonly RouteDefinition<SearchRequest, FileNode[]> Search = Define.Get<SearchRequest, FileNode[]>(
        "/api/search"
    );

    public static readonly RouteDefinition<FileRequest, FileNode> NextFile = Define.Get<FileRequest, FileNode>(
        "/api/files/next"
    );

    public static readonly RouteDefinition<FileRequest, SubtitleTrack[]> SubtitlesList = Define.Get<
        FileRequest,
        SubtitleTrack[]
    >("/api/files/subtitles");

    public static readonly FileRouteDefinition<SubtitleContentRequest> SubtitlesContent = Define
        .File<SubtitleContentRequest>("/api/files/subtitles/content")
        .QueryAuth();

    public static readonly RouteDefinition<FileRequest, TrickplayMetadata> TrickplayInfo = Define.Get<
        FileRequest,
        TrickplayMetadata
    >("/api/files/trickplay");

    public static readonly FileRouteDefinition<TrickplaySpriteRequest> TrickplaySprite = Define
        .File<TrickplaySpriteRequest>("/api/files/trickplay/sprite")
        .ContentType("image/jpeg")
        .QueryAuth();

    public static readonly RouteDefinition<Collection[]> CollectionsList = Define.Get<Collection[]>(
        "/api/collections"
    );

    public static readonly RouteDefinition<CreateCollectionRequest, Collection> CollectionsCreate = Define.Post<
        CreateCollectionRequest,
        Collection
    >("/api/collections");

    public static readonly RouteDefinition<CollectionWithItems> CollectionsGet = Define.Get<CollectionWithItems>(
        "/api/collections/{id}"
    );

    public static readonly RouteDefinition<RenameCollectionRequest, Collection> CollectionsRename = Define.Patch<
        RenameCollectionRequest,
        Collection
    >("/api/collections/{id}");

    public static readonly RouteDefinition CollectionsDelete = Define.Delete("/api/collections/{id}").Status(204);

    public static readonly InputRouteDefinition<CollectionItemRequest> CollectionsAddItem = Define
        .Post("/api/collections/{id}/items")
        .Accepts<CollectionItemRequest>()
        .Status(204);

    public static readonly InputRouteDefinition<CollectionItemRequest> CollectionsRemoveItem = Define
        .Delete("/api/collections/{id}/items")
        .Accepts<CollectionItemRequest>();

    public static readonly InputRouteDefinition<ReorderItemsRequest> CollectionsReorder = Define
        .Put("/api/collections/{id}/items/order")
        .Accepts<ReorderItemsRequest>()
        .Status(204);
}
