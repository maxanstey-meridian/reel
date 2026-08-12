namespace Reel.Server.Domain;

public sealed record FileNode(
    string Name,
    FileNodeType Type,
    string RelativePath,
    string? Extension,
    long? SizeBytes,
    IReadOnlyList<FileNode>? Children,
    string? ParsedTitle,
    int? Year,
    bool? Watched
);
