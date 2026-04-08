namespace Reel.Server.Application.Models;

public sealed record SearchRequest(string? Q, string? Tag, string? Genre, bool? Watched);
