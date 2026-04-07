namespace Reel.Server.Domain.Exceptions;

public sealed class ForbiddenException(string message) : Exception(message);
