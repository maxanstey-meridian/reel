namespace Reel.Server.Application.Models;

public sealed record MediaDetailResponse(
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
    DateTime UpdatedAt,
    bool? Watched,
    double? PositionSeconds,
    double? DurationSeconds,
    IReadOnlyList<string> Tags
);
