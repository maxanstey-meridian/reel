namespace Reel.Server.Infrastructure.Tmdb;

using System.Text.Json.Serialization;

internal sealed record TmdbResult(
    int TmdbId,
    string? PosterPath,
    string? BackdropPath,
    string? Overview,
    string? ReleaseDate
);

internal sealed record TmdbSearchResponse([property: JsonPropertyName("results")] List<TmdbSearchResult> Results);

internal sealed record TmdbSearchResult(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("poster_path")] string? PosterPath,
    [property: JsonPropertyName("backdrop_path")] string? BackdropPath,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("release_date")] string? ReleaseDate
);
