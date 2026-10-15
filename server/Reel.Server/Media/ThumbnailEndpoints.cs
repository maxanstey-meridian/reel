using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Media;

public static class ThumbnailEndpoints
{
    public static IEndpointRouteBuilder MapThumbnailEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ReelContract.Thumbnail.Route,
            async (
                [AsParameters] FileRequest request,
                MediaPathResolver paths,
                FfmpegThumbnailGenerator thumbnails,
                HttpContext context,
                CancellationToken ct
            ) =>
            {
                var endpoint = ReelContract.Thumbnail.Bind(request);
                var source = paths.ResolvePlayableFile(request.Path);
                var thumbnail = source is null
                    ? null
                    : await thumbnails.GetOrGenerateAsync(source, ct);
                if (thumbnail is null)
                {
                    return endpoint
                        .Error(
                            404,
                            new ErrorResponse(
                                "thumbnail_unavailable",
                                "Thumbnail is not available."
                            )
                        )
                        .ToResult();
                }

                context.Response.Headers.CacheControl = "private, max-age=86400";
                return endpoint.File(thumbnail).ToResult();
            }
        );

        return endpoints;
    }
}
