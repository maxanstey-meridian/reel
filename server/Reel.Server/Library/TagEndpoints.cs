using Microsoft.AspNetCore.Mvc;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Library;

public static class TagEndpoints
{
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ReelContract.Tags.Route,
            async (SqliteMediaStore mediaStore) =>
                ReelContract.Tags.Success(await mediaStore.GetTagsAsync()).ToResult()
        );

        endpoints.MapPut(
            ReelContract.TagsUpdate.Route,
            async ([FromBody] TagUpdateRequest request, SqliteMediaStore mediaStore) =>
            {
                var endpoint = ReelContract.TagsUpdate.Bind(request);
                var path = MediaPathResolver.Normalize(request.Path);
                if (path is null || request.Tags is null)
                {
                    return endpoint
                        .Error(400, new ErrorResponse("invalid_request", "Invalid item or tags."))
                        .ToResult();
                }

                // JSON can still carry null elements despite the non-nullable element type.
                var tags = request
                    .Tags.Select(tag => (tag ?? "").Trim().TrimStart('#').ToLowerInvariant())
                    .Where(tag => tag.Length > 0)
                    .Distinct()
                    .ToArray();
                if (
                    tags.Length > 30
                    || tags.Any(tag => tag.Length > 40 || tag.Any(char.IsWhiteSpace))
                )
                {
                    return endpoint
                        .Error(
                            400,
                            new ErrorResponse(
                                "invalid_request",
                                "Use up to 30 tags, each up to 40 characters with no spaces."
                            )
                        )
                        .ToResult();
                }

                await mediaStore.SetTagsAsync(path, tags);
                return endpoint.Success(tags).ToResult();
            }
        );

        return endpoints;
    }
}
