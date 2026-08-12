namespace Reel.Server.Application.Ports;

using Reel.Server.Application.Models;
using Reel.Server.Domain;

public interface IMediaStore
{
    Task ReconcileAsync(IReadOnlyCollection<MediaRecord> media, CancellationToken ct);
    Task<MediaRecord?> GetAsync(string relativePath, CancellationToken ct);
    Task<bool?> IsWatchedAsync(string relativePath, CancellationToken ct);
    Task<IReadOnlyList<FileNode>> SearchAsync(SearchQuery query, CancellationToken ct);
    Task SaveSubtitlesAsync(string relativePath, IReadOnlyList<SubtitleTrack> tracks, CancellationToken ct);
    Task<IReadOnlyList<SubtitleTrack>> GetSubtitlesAsync(string relativePath, CancellationToken ct);
    Task SaveTrickplayAsync(string relativePath, TrickplayMetadata metadata, CancellationToken ct);
    Task<TrickplayMetadata?> GetTrickplayAsync(string relativePath, CancellationToken ct);
}
