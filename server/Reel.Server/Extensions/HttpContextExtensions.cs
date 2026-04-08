namespace Reel.Server.Extensions;

using Reel.Server.Domain.Exceptions;

public static class HttpContextExtensions
{
    public static int GetUserId(this HttpContext context) =>
        context.Items["UserId"] is int id
            ? id
            : throw new UnauthorizedException("No authenticated user");
}
