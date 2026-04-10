namespace Reel.Server.Application.Models;

public sealed record CastRequest(string RelativePath, double? PositionSeconds = null, int? SubtitleIndex = null);

public sealed record CastQueueRequest(string[] Paths);
