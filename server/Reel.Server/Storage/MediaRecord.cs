namespace Reel.Server.Storage;

/// <summary>One indexed media file. SizeBytes is null only for rows indexed before sizes were recorded.</summary>
public sealed record MediaRecord(
    string RelativePath,
    string? ParsedTitle,
    int? Year,
    long? SizeBytes
);
