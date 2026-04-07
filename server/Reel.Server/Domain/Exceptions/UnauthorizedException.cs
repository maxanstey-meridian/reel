namespace Reel.Server.Domain.Exceptions;

public sealed class UnauthorizedException(string message, object? body = null) : Exception(message)
{
    public object? Body { get; } = body;
}
