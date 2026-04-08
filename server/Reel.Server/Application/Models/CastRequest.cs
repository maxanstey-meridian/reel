namespace Reel.Server.Application.Models;

public sealed record CastRequest(string RelativePath);

public sealed record CastQueueRequest(string[] Paths);
