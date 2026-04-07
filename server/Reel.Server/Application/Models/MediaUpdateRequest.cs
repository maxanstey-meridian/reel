namespace Reel.Server.Application.Models;

public sealed record MediaUpdateRequest(
    string? Genre,
    string? Rating,
    string? Language,
    string? Notes,
    string? DisplayTitle
);
