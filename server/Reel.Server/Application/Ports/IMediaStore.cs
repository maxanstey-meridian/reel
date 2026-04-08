namespace Reel.Server.Application.Ports;

using Reel.Server.Application.Models;
using Reel.Server.Domain;

public interface IMediaStore
{
    Task UpsertFromScanAsync(string relativePath, string? parsedTitle, int? year, CancellationToken ct);
    Task<MediaRecord?> GetAsync(string relativePath, CancellationToken ct);
    Task SetWatchStateAsync(int userId, string relativePath, WatchState? state, CancellationToken ct);
    Task<bool?> IsWatchedAsync(int userId, string relativePath, CancellationToken ct);
    Task UpdateMetaAsync(string relativePath, MediaUpdateRequest update, CancellationToken ct);
    Task UpdateTmdbCacheAsync(
        string relativePath,
        int tmdbId,
        string? posterUrl,
        string? backdropUrl,
        string? overview,
        string? releaseDate,
        CancellationToken ct
    );
    Task SetTagsAsync(int userId, string relativePath, IReadOnlyList<string> tags, CancellationToken ct);
    Task AddTagAsync(int userId, string relativePath, string tag, CancellationToken ct);
    Task RemoveTagAsync(int userId, string relativePath, string tag, CancellationToken ct);
    Task<IReadOnlyList<string>> GetTagsAsync(int userId, string relativePath, CancellationToken ct);
    Task<IReadOnlyList<TagCount>> GetAllTagsAsync(int userId, CancellationToken ct);
    Task<IReadOnlyList<FileNode>> SearchAsync(int userId, SearchQuery query, CancellationToken ct);
    Task<string?> GetDisplayTitleAsync(string relativePath, CancellationToken ct);
    Task SaveSubtitlesAsync(string relativePath, IReadOnlyList<SubtitleTrack> tracks, CancellationToken ct);
    Task<IReadOnlyList<SubtitleTrack>> GetSubtitlesAsync(string relativePath, CancellationToken ct);
    Task SaveTrickplayAsync(string relativePath, TrickplayMetadata metadata, CancellationToken ct);
    Task<TrickplayMetadata?> GetTrickplayAsync(string relativePath, CancellationToken ct);
}
