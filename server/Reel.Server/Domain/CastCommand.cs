namespace Reel.Server.Domain;

public sealed record CastCommand(string Type, string? RelativePath, double? PositionSeconds = null);
