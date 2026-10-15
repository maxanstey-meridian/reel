using Microsoft.AspNetCore.Mvc;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Library;

public static class ProgressEndpoints
{
    public static IEndpointRouteBuilder MapProgressEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            ReelContract.ProgressUpdate.Route,
            async (
                [FromBody] ProgressRequest request,
                SqliteProgressStore store,
                SqliteMediaStore mediaStore,
                MediaPathResolver paths
            ) =>
            {
                var endpoint = ReelContract.ProgressUpdate.Bind(request);
                if (
                    !double.IsFinite(request.PositionSeconds)
                    || request.PositionSeconds < 0
                    || !double.IsFinite(request.DurationSeconds)
                    || request.DurationSeconds <= 0
                )
                {
                    return endpoint
                        .Error(
                            400,
                            new ErrorResponse(
                                "invalid_request",
                                "Position must be finite and non-negative, and duration must be finite and positive."
                            )
                        )
                        .ToResult();
                }

                var relativePath = await LibraryEntries.EnsureIndexedAsync(
                    request.RelativePath,
                    paths,
                    mediaStore
                );
                if (relativePath is null)
                {
                    return endpoint.Error(404, EndpointErrors.MediaNotFound).ToResult();
                }

                await store.UpdateAsync(
                    relativePath,
                    Math.Min(request.PositionSeconds, request.DurationSeconds),
                    request.DurationSeconds
                );
                return endpoint.Success().ToResult();
            }
        );

        endpoints.MapGet(
            ReelContract.ProgressAll.Route,
            async (SqliteProgressStore store) =>
                ReelContract.ProgressAll.Success(await store.GetAllAsync()).ToResult()
        );

        return endpoints;
    }
}
