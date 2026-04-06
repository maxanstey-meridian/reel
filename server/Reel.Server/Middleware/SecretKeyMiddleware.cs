namespace Reel.Server.Middleware;

using Reel.Server.Services;

public sealed class SecretKeyMiddleware
{
    private static readonly string[] ExcludedPaths = ["/api/connect", "/api/health"];

    private readonly RequestDelegate _next;
    private readonly ReelConfig _config;

    public SecretKeyMiddleware(RequestDelegate next, ReelConfig config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (ExcludedPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var key = context.Request.Headers["X-Secret-Key"].FirstOrDefault();

        if (key != _config.SecretKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"status":"unauthorized"}""");
            return;
        }

        await _next(context);
    }
}
