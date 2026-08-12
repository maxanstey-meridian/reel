namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Infrastructure.FileSystem;
using Rivet;

public static class ProgressEndpoints
{
    public static WebApplication MapProgressEndpoints(this WebApplication app)
    {
        app.MapPost(
            ReelContract.ProgressUpdate.Route,
            async (
                [FromBody] ProgressRequest request,
                IProgressStore store,
                IMediaStore mediaStore,
                MediaPathResolver pathResolver,
                CancellationToken ct
            ) =>
            {
                var endpoint = ReelContract.ProgressUpdate.Bind(request);
                if (!double.IsFinite(request.PositionSeconds) || request.PositionSeconds < 0
                    || !double.IsFinite(request.DurationSeconds) || request.DurationSeconds <= 0)
                    throw new BadRequestException("Position must be finite and non-negative, and duration must be finite and positive.");
                if (pathResolver.ResolvePlayableFile(request.RelativePath) is null
                    || await mediaStore.GetAsync(request.RelativePath, ct) is null)
                    throw new NotFoundException("Indexed playable media was not found.");
                await store.UpdateAsync(
                    request.RelativePath,
                    Math.Min(request.PositionSeconds, request.DurationSeconds),
                    request.DurationSeconds,
                    ct
                );
                return endpoint.Success().ToResult();
            }
        );

        app.MapGet(
            ReelContract.ProgressAll.Route,
            async (IProgressStore store, CancellationToken ct) =>
            {
                var progress = await store.GetAllAsync(ct);
                return ReelContract.ProgressAll.Success(progress.ToArray()).ToResult();
            }
        );

        return app;
    }
}
