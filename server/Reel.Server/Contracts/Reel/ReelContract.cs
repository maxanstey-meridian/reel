using Reel.Server.Common;
using Rivet;

namespace Reel.Server.Contracts.Reel;

[RivetContract]
public static class ReelContract
{
    public const string ConnectRoute = "/api/connect";
    public const string HealthRoute = "/api/health";
    public const string PairRoute = "/pair";
    public const string TagsRoute = "/api/tags";

    public static readonly RouteDefinition<ConnectRequest, ConnectResponse> Connect = Define
        .Post<ConnectRequest, ConnectResponse>(ConnectRoute)
        .Returns<ErrorResponse>(401);

    public static readonly RouteDefinition<HealthResponse> Health = Define
        .Get<HealthResponse>(HealthRoute)
        .Returns<ErrorResponse>(503);

    public static readonly RouteDefinition<FileNode> Library = Define.Get<FileNode>("/api/library");

    public static readonly RouteDefinition<FileRequest, NextEpisodeResponse> NextEpisode =
        Define.Get<FileRequest, NextEpisodeResponse>("/api/library/next");

    // Refused when the scan looks like an unreadable or missing volume rather than a real library change.
    public static readonly RouteDefinition<LibraryRescanResponse> LibraryRescan = Define
        .Post<LibraryRescanResponse>("/api/library/rescan")
        .Status(200)
        .Returns<ErrorResponse>(503);

    public static readonly RouteDefinition<Dictionary<string, string[]>> Tags = Define.Get<
        Dictionary<string, string[]>
    >(TagsRoute);

    public static readonly RouteDefinition<TagUpdateRequest, string[]> TagsUpdate = Define
        .Put<TagUpdateRequest, string[]>(TagsRoute)
        .Returns<ErrorResponse>(400);

    public static readonly InputRouteDefinition<ProgressRequest> ProgressUpdate = Define
        .Post("/api/progress")
        .Accepts<ProgressRequest>()
        .Status(200)
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404);

    public static readonly RouteDefinition<WatchProgress[]> ProgressAll =
        Define.Get<WatchProgress[]>("/api/progress");

    public static readonly InputRouteDefinition<FavouriteRequest> FavouritesAdd = Define
        .Post("/api/favourites")
        .Accepts<FavouriteRequest>()
        .Status(200)
        .Returns<ErrorResponse>(404);

    public static readonly RouteDefinition<Favourite[]> FavouritesAll = Define.Get<Favourite[]>(
        "/api/favourites"
    );

    public static readonly InputRouteDefinition<FileRequest> FavouritesRemove = Define
        .Delete("/api/favourites")
        .Accepts<FileRequest>()
        .Status(200);

    // Ranged file bytes, or with Height a live MP4 transcode of video starting at Start seconds. StreamInfo picks
    // Height from Reel:Compression once per load, so a settings edit never changes a stream mid-playback.
    public static readonly FileRouteDefinition<StreamRequest> Stream = Define
        .File<StreamRequest>("/api/files/stream")
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404)
        .QueryAuth();

    public static readonly RouteDefinition<FileRequest, StreamInfoResponse> StreamInfo = Define
        .Get<FileRequest, StreamInfoResponse>("/api/files/stream/info")
        .Returns<ErrorResponse>(404);

    public static readonly FileRouteDefinition<FileRequest> Thumbnail = Define
        .File<FileRequest>("/api/files/thumbnail")
        .ContentType("image/jpeg")
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404)
        .QueryAuth();

    public static readonly RouteDefinition<SearchRequest, FileNode[]> Search = Define.Get<
        SearchRequest,
        FileNode[]
    >("/api/search");

    public static readonly RouteDefinition<FileRequest, SubtitleTrack[]> SubtitlesList = Define
        .Get<FileRequest, SubtitleTrack[]>("/api/files/subtitles")
        .Returns<ErrorResponse>(404);

    public static readonly FileRouteDefinition<SubtitleContentRequest> SubtitlesContent = Define
        .File<SubtitleContentRequest>("/api/files/subtitles/content")
        .ContentType("text/vtt")
        .Returns<ErrorResponse>(404)
        .QueryAuth();

    public static readonly RouteDefinition<FileRequest, TrickplayMetadata> TrickplayInfo = Define
        .Get<FileRequest, TrickplayMetadata>("/api/files/trickplay")
        .Returns<ErrorResponse>(404);

    public static readonly FileRouteDefinition<TrickplaySpriteRequest> TrickplaySprite = Define
        .File<TrickplaySpriteRequest>("/api/files/trickplay/sprite")
        .ContentType("image/jpeg")
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404)
        .QueryAuth();
}

public sealed record ConnectRequest(string Key);

public sealed record ConnectResponse(string ServerName);

public sealed record HealthResponse(string Status);

public sealed record FileRequest(string Path);

public sealed record SearchRequest(string? Q);

public sealed record ProgressRequest(
    string RelativePath,
    double PositionSeconds,
    double DurationSeconds
);

public sealed record FavouriteRequest(string RelativePath);

public sealed record TagUpdateRequest(string Path, string[] Tags);

public sealed record StreamRequest(string Path, double? Start, int? Height);

// DurationSeconds is null when ffprobe can't tell.
public sealed record StreamInfoResponse(double? DurationSeconds, int? TranscodeHeight);

// Start shifts cues back to match a transcoded stream that began at that second.
public sealed record SubtitleContentRequest(string Path, int Index, double? Start);

public sealed record TrickplaySpriteRequest(string Path, int? Sheet);

public sealed record LibraryRescanResponse(int MediaCount);

public sealed record NextEpisodeResponse(string? RelativePath, string? Title);

public enum SubtitleSource
{
    Embedded,
    External,
}

/// <summary>ImageBased tracks (PGS, VobSub, DVB) cannot be converted to WebVTT text by the content route.</summary>
public sealed record SubtitleTrack(
    int Index,
    string Label,
    string Language,
    string Format,
    SubtitleSource Source,
    bool ImageBased
);

public sealed record TrickplayMetadata(
    int IntervalMs,
    int Columns,
    int Rows,
    int ThumbWidth,
    int ThumbHeight,
    int TotalThumbs
);
