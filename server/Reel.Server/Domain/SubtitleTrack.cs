namespace Reel.Server.Domain;

public sealed record SubtitleTrack(
    int Index,
    string Label,
    string Language,
    string Format,
    string Source,
    string? ExternalPath
);
