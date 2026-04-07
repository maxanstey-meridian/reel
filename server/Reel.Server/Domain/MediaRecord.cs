namespace Reel.Server.Domain;

public sealed record MediaRecord(
    string RelativePath,
    string? ParsedTitle,
    int? Year,
    string? Genre,
    string? Rating,
    string? Language,
    string? Notes,
    string? DisplayTitle,
    int? TmdbId,
    string? PosterUrl,
    string? BackdropUrl,
    string? Overview,
    string? ReleaseDate,
    DateTime AddedAt,
    DateTime UpdatedAt);
