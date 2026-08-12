using Reel.Server.Application.Models;
using Reel.Server.Domain;
using Rivet;

namespace Reel.Server;

[RivetContract]
public static class ReelContract
{
    public const string PairRoute = "/pair";

    public static readonly RouteDefinition<ConnectRequest, ConnectResponse> Connect = Define
        .Post<ConnectRequest, ConnectResponse>("/api/connect")
        .Returns<ConnectResponse>(401);

    public static readonly RouteDefinition<HealthResponse> Health = Define.Get<HealthResponse>("/api/health");

    public static readonly RouteDefinition<FileNode> Library = Define.Get<FileNode>("/api/library");

    public static readonly RouteDefinition<LibraryRescanResponse> LibraryRescan =
        Define.Post<LibraryRescanResponse>("/api/library/rescan").Status(200);

    public static readonly InputRouteDefinition<ProgressRequest> ProgressUpdate = Define
        .Post("/api/progress")
        .Accepts<ProgressRequest>()
        .Status(200)
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404);

    public static readonly RouteDefinition<WatchProgress[]> ProgressAll = Define.Get<WatchProgress[]>("/api/progress");

    public static readonly InputRouteDefinition<FavouriteRequest> FavouritesAdd = Define
        .Post("/api/favourites")
        .Accepts<FavouriteRequest>()
        .Status(200)
        .Returns<ErrorResponse>(404);

    public static readonly RouteDefinition<Favourite[]> FavouritesAll = Define.Get<Favourite[]>("/api/favourites");

    public static readonly InputRouteDefinition<FileRequest> FavouritesRemove = Define
        .Delete("/api/favourites")
        .Accepts<FileRequest>()
        .Status(200);

    public static readonly FileRouteDefinition<FileRequest> Stream = Define
        .File<FileRequest>("/api/files/stream")
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404)
        .QueryAuth();

    public static readonly RouteDefinition<SearchRequest, FileNode[]> Search = Define.Get<SearchRequest, FileNode[]>(
        "/api/search"
    );

    public static readonly RouteDefinition<FileRequest, SubtitleTrack[]> SubtitlesList = Define.Get<
        FileRequest,
        SubtitleTrack[]
    >("/api/files/subtitles");

    public static readonly FileRouteDefinition<SubtitleContentRequest> SubtitlesContent = Define
        .File<SubtitleContentRequest>("/api/files/subtitles/content")
        .Returns<ErrorResponse>(400)
        .Returns(403)
        .Returns<ErrorResponse>(404)
        .Returns(500)
        .QueryAuth();

    public static readonly RouteDefinition<FileRequest, TrickplayMetadata> TrickplayInfo = Define.Get<
        FileRequest,
        TrickplayMetadata
    >("/api/files/trickplay");

    public static readonly FileRouteDefinition<TrickplaySpriteRequest> TrickplaySprite = Define
        .File<TrickplaySpriteRequest>("/api/files/trickplay/sprite")
        .ContentType("image/jpeg")
        .Returns<ErrorResponse>(400)
        .Returns(403)
        .Returns<ErrorResponse>(404)
        .QueryAuth();

}

public sealed record ErrorResponse(string Error);

public sealed record LibraryRescanResponse(int MediaCount);
