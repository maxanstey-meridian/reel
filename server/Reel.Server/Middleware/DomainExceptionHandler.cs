namespace Reel.Server.Middleware;

using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Reel.Server.Domain.Exceptions;

public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var (statusCode, body) = exception switch
        {
            BadRequestException e => (StatusCodes.Status400BadRequest, (object)new { error = e.Message }),
            JsonException => (StatusCodes.Status400BadRequest, (object)new { error = "Invalid request body" }),
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                (object)new { error = "Invalid request body" }
            ),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, e.Body ?? new { error = e.Message }),
            ForbiddenException e => (StatusCodes.Status403Forbidden, (object)new { error = e.Message }),
            NotFoundException e => (StatusCodes.Status404NotFound, (object)new { error = e.Message }),
            _ => (0, (object?)null),
        };

        if (statusCode == 0)
            return false;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);
        return true;
    }
}
