namespace Reel.Server.Domain;

public sealed record MediaRecord(
    string RelativePath,
    string? ParsedTitle,
    int? Year
);
