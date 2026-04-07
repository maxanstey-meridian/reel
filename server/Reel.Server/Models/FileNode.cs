namespace Reel.Server.Models;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FileNodeType
{
    File,
    Directory
}

public sealed record FileNode(
    string Name,
    FileNodeType Type,
    string RelativePath,
    string? Extension,
    long? SizeBytes,
    IReadOnlyList<FileNode>? Children,
    string? ParsedTitle,
    int? Year,
    string? ThumbnailUrl);
