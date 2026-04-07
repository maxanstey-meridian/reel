namespace Reel.Server.Domain.Exceptions;

public sealed class BadRequestException(string message) : Exception(message);
