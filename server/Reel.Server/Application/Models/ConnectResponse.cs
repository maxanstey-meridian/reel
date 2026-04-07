namespace Reel.Server.Application.Models;

public sealed record ConnectResponse(string Status, string? ServerName = null);
