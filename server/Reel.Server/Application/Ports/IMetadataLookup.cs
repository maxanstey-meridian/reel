namespace Reel.Server.Application.Ports;

using Reel.Server.Application.Models;

public interface IMetadataLookup
{
    Task<MetadataResponse?> LookupAsync(string title, int? year, CancellationToken ct);
}
