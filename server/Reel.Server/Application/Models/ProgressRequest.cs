namespace Reel.Server.Application.Models;

public sealed record ProgressRequest(string RelativePath, double PositionSeconds, double DurationSeconds);
