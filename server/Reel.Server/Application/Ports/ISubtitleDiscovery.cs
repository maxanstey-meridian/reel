namespace Reel.Server.Application.Ports;

using Reel.Server.Domain;

public interface ISubtitleDiscovery
{
    Task<IReadOnlyList<SubtitleTrack>> DiscoverAsync(string relativePath, CancellationToken ct);
}
