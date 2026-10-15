namespace Reel.Server.Media;

// Serialized as the trickplay cache's metadata file, so property names are part of that file format.
public sealed record TrickplaySheets(
    int IntervalMs,
    int Columns,
    int Rows,
    int ThumbWidth,
    int ThumbHeight,
    int TotalThumbs,
    string SpritePath
);
