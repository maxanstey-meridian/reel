namespace Reel.Server.Application.Models;

public sealed record SetTagsRequest(IReadOnlyList<string> Tags);

public sealed record AddTagRequest(string Tag);
