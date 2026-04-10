namespace Reel.Server.Domain;

public enum CastCommandType
{
    Play,
    Stop,
}

public sealed record CastCommand(CastCommandType Type, string? RelativePath, double? PositionSeconds = null, int? SubtitleIndex = null);
