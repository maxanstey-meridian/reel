using System.Text.Json.Serialization;
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
    public const string LibraryRoute = "/api/library";
    public const string NextEpisodeRoute = "/api/library/next";
    public const string LibraryRescanRoute = "/api/library/rescan";
    public const string ProgressRoute = "/api/progress";
    public const string FavouritesRoute = "/api/favourites";
    public const string StreamRoute = "/api/files/stream";
    public const string StreamInfoRoute = "/api/files/stream/info";
    public const string ThumbnailRoute = "/api/files/thumbnail";
    public const string SearchRoute = "/api/search";
    public const string SubtitlesRoute = "/api/files/subtitles";
    public const string SubtitlesContentRoute = "/api/files/subtitles/content";
    public const string TrickplayRoute = "/api/files/trickplay";
    public const string TrickplaySpriteRoute = "/api/files/trickplay/sprite";

    public static readonly RouteDefinition<ConnectRequest, ConnectResponse> Connect = Define
        .Post<ConnectRequest, ConnectResponse>(ConnectRoute)
        .Returns<ErrorResponse>(401);

    public static readonly RouteDefinition<HealthResponse> Health = Define
        .Get<HealthResponse>(HealthRoute)
        .Returns<ErrorResponse>(503);

    public static readonly RouteDefinition<FileNode> Library = Define.Get<FileNode>(LibraryRoute);

    public static readonly RouteDefinition<FileRequest, NextEpisodeResponse> NextEpisode =
        Define.Get<FileRequest, NextEpisodeResponse>(NextEpisodeRoute);

    // Refused when the scan looks like an unreadable or missing volume rather than a real library change.
    public static readonly RouteDefinition<LibraryRescanResponse> LibraryRescan = Define
        .Post<LibraryRescanResponse>(LibraryRescanRoute)
        .Status(200)
        .Returns<ErrorResponse>(503);

    public static readonly RouteDefinition<Dictionary<string, string[]>> Tags = Define.Get<
        Dictionary<string, string[]>
    >(TagsRoute);

    public static readonly RouteDefinition<TagUpdateRequest, string[]> TagsUpdate = Define
        .Put<TagUpdateRequest, string[]>(TagsRoute)
        .Returns<ErrorResponse>(400);

    public static readonly InputRouteDefinition<ProgressRequest> ProgressUpdate = Define
        .Post(ProgressRoute)
        .Accepts<ProgressRequest>()
        .Status(200)
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404);

    public static readonly RouteDefinition<WatchProgress[]> ProgressAll =
        Define.Get<WatchProgress[]>(ProgressRoute);

    public static readonly InputRouteDefinition<FavouriteRequest> FavouritesAdd = Define
        .Post(FavouritesRoute)
        .Accepts<FavouriteRequest>()
        .Status(200)
        .Returns<ErrorResponse>(404);

    public static readonly RouteDefinition<Favourite[]> FavouritesAll = Define.Get<Favourite[]>(
        FavouritesRoute
    );

    public static readonly InputRouteDefinition<FileRequest> FavouritesRemove = Define
        .Delete(FavouritesRoute)
        .Accepts<FileRequest>()
        .Status(200);

    // Ranged file bytes, or with Height (transcode) or Remux a live MP4 starting at Start seconds, in the mode
    // StreamInfo planned for the session's MaxHeight.
    public static readonly FileRouteDefinition<StreamRequest> Stream = Define
        .File<StreamRequest>(StreamRoute)
        .ProducesFile("video/*")
        .ResponseBinaryContent(200, "audio/*")
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404)
        .QueryAuth();

    public static readonly RouteDefinition<PlaybackFileRequest, StreamInfoResponse> StreamInfo =
        Define
            .Get<PlaybackFileRequest, StreamInfoResponse>(StreamInfoRoute)
            .Returns<ErrorResponse>(400)
            .Returns<ErrorResponse>(404);

    public static readonly FileRouteDefinition<FileRequest> Thumbnail = Define
        .File<FileRequest>(ThumbnailRoute)
        .ProducesFile("image/jpeg")
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404)
        .QueryAuth();

    public static readonly RouteDefinition<SearchRequest, FileNode[]> Search = Define.Get<
        SearchRequest,
        FileNode[]
    >(SearchRoute);

    public static readonly RouteDefinition<PlaybackFileRequest, SubtitleTrack[]> SubtitlesList =
        Define
            .Get<PlaybackFileRequest, SubtitleTrack[]>(SubtitlesRoute)
            .Returns<ErrorResponse>(400)
            .Returns<ErrorResponse>(404);

    public static readonly FileRouteDefinition<SubtitleContentRequest> SubtitlesContent = Define
        .File<SubtitleContentRequest>(SubtitlesContentRoute)
        .ProducesFile("text/vtt")
        .Returns<ErrorResponse>(404)
        .QueryAuth();

    public static readonly RouteDefinition<FileRequest, TrickplayMetadata> TrickplayInfo = Define
        .Get<FileRequest, TrickplayMetadata>(TrickplayRoute)
        .Returns<ErrorResponse>(404);

    public static readonly FileRouteDefinition<TrickplaySpriteRequest> TrickplaySprite = Define
        .File<TrickplaySpriteRequest>(TrickplaySpriteRoute)
        .ProducesFile("image/jpeg")
        .Returns<ErrorResponse>(400)
        .Returns<ErrorResponse>(404)
        .QueryAuth();
}

public sealed record ConnectRequest(string Key);

public sealed record ConnectResponse(string ServerName);

public sealed record HealthResponse(string Status);

public sealed record FileRequest(string Path);

// A file as the playback session's MaxHeight would stream it: that decides whether the stream is live.
public sealed record PlaybackFileRequest(string Path, int? MaxHeight);

public sealed record SearchRequest(string? Q);

public sealed record ProgressRequest(
    string RelativePath,
    double PositionSeconds,
    double DurationSeconds
);

public sealed record FavouriteRequest(string RelativePath);

public sealed record TagUpdateRequest(string Path, string[] Tags);

// Height transcodes to that height; Remux keeps the video and re-encodes the audio. They cannot be combined.
public sealed record StreamRequest(string Path, double? Start, int? Height, bool? Remux);

/// <summary>
/// Direct serves the file. Remux and transcode are live MP4s that seek by reloading from a new Start: remux keeps
/// the video and re-encodes the audio, transcode re-encodes both to TranscodeHeight (the session's MaxHeight, or the
/// source's own height for video the TV cannot decode).
/// </summary>
[JsonConverter(typeof(RivetCamelCaseEnumConverter<StreamMode>))]
public enum StreamMode
{
    Direct,
    Remux,
    Transcode,
}

// DurationSeconds is null when ffprobe can't tell; TranscodeHeight is set only for Mode transcode.
public sealed record StreamInfoResponse(
    double? DurationSeconds,
    StreamMode Mode,
    int? TranscodeHeight
);

// Start shifts cues back to match a live stream that began at that second.
public sealed record SubtitleContentRequest(string Path, int Index, double? Start);

public sealed record TrickplaySpriteRequest(string Path, int? Sheet);

public sealed record LibraryRescanResponse(int MediaCount);

public sealed record NextEpisodeResponse(string? RelativePath, string? Title);

[JsonConverter(typeof(RivetCamelCaseEnumConverter<SubtitleSource>))]
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
