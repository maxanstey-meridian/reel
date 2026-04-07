namespace Reel.Server.Models;

public sealed record Favourite(string RelativePath, DateTime AddedAt);

public sealed record FavouriteRequest(string RelativePath);
