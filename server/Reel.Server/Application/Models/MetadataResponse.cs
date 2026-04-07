namespace Reel.Server.Application.Models;

public sealed record MetadataResponse(
    int TmdbId,
    string? PosterUrl,
    string? BackdropUrl,
    string? Overview,
    string? ReleaseDate
);
