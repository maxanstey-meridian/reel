namespace Reel.Server.Application.Models;

public sealed record SearchQuery(string? Q, string? Tag, string? Genre, bool? Watched);
