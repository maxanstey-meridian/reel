namespace Reel.Server.Contracts.Reel;

/// <summary>A library folder or media file; Kind and the other file details are null for folders.</summary>
public sealed record FileNode(
    string Name,
    FileNodeType Type,
    MediaKind? Kind,
    string RelativePath,
    string? Extension,
    long? SizeBytes,
    IReadOnlyList<FileNode>? Children,
    string? ParsedTitle,
    int? Year
);
