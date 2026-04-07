namespace Reel.Server.Models;

public sealed record TmdbResult(
    int TmdbId,
    string? PosterPath,
    string? BackdropPath,
    string? Overview,
    string? ReleaseDate
);
