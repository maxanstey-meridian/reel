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
            BadRequestException e => (StatusCodes.Status400BadRequest, new ErrorBody(e.Message)),
            JsonException => (StatusCodes.Status400BadRequest, new ErrorBody("Invalid request body")),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, new ErrorBody("Invalid request body")),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, new ErrorBody(e.Message)),
            ForbiddenException e => (StatusCodes.Status403Forbidden, new ErrorBody(e.Message)),
            NotFoundException e => (StatusCodes.Status404NotFound, new ErrorBody(e.Message)),
            _ => (0, (ErrorBody?)null),
        };

        if (statusCode == 0)
            return false;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);
        return true;
    }
}

internal sealed record ErrorBody(string Error);
