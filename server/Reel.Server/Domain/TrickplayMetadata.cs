namespace Reel.Server.Domain;

public sealed record TrickplayMetadata(
    int IntervalMs,
    int Columns,
    int Rows,
    int ThumbWidth,
    int ThumbHeight,
    int TotalThumbs,
    string SpritePath
);
