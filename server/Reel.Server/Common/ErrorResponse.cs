namespace Reel.Server.Common;

/// <summary>Every declared failure: a stable snake_case Code for clients to match, and a Message for people.</summary>
public sealed record ErrorResponse(string Code, string Message);
