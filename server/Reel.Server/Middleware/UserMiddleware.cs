namespace Reel.Server.Middleware;

using Reel.Server.Application.Ports;

public sealed class UserMiddleware
{
    private readonly RequestDelegate _next;

    public UserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserStore userStore)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/api/users", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var headerValue = context.Request.Headers["X-User-Id"].FirstOrDefault();

        if (headerValue is null)
        {
            context.Items["UserId"] = 1;
            await _next(context);
            return;
        }

        if (!int.TryParse(headerValue, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"Invalid X-User-Id header"}""");
            return;
        }

        if (!await userStore.ExistsAsync(userId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"User not found"}""");
            return;
        }

        context.Items["UserId"] = userId;
        await _next(context);
    }
}
