namespace Reel.Server.Media;

/// <summary>A subtitle track of a media file: embedded when ExternalPath is null, else a sidecar file.</summary>
public sealed record Subtitle(
    int Index,
    string Label,
    string Language,
    string Format,
    string? ExternalPath
);
