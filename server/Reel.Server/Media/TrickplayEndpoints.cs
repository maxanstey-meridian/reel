using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Media;

public static class TrickplayEndpoints
{
    public static IEndpointRouteBuilder MapTrickplayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ReelContract.TrickplayInfo.Route,
            async (
                [AsParameters] FileRequest request,
                FfmpegTrickplayGenerator generator,
                MediaPathResolver paths,
                CancellationToken ct
            ) =>
            {
                var endpoint = ReelContract.TrickplayInfo.Bind(request);
                var fullPath = paths.ResolvePlayableFile(request.Path);
                if (fullPath is null)
                {
                    return endpoint.Error(404, EndpointErrors.MediaNotFound).ToResult();
                }

                var sheets = await GetSheetsAsync(generator, fullPath, ct);
                return sheets is null
                    ? endpoint.Error(404, EndpointErrors.TrickplayUnavailable).ToResult()
                    : endpoint
                        .Success(
                            new TrickplayMetadata(
                                sheets.IntervalMs,
                                sheets.Columns,
                                sheets.Rows,
                                sheets.ThumbWidth,
                                sheets.ThumbHeight,
                                sheets.TotalThumbs
                            )
                        )
                        .ToResult();
            }
        );

        endpoints.MapGet(
            ReelContract.TrickplaySprite.Route,
            async (
                [AsParameters] TrickplaySpriteRequest request,
                FfmpegTrickplayGenerator generator,
                MediaPathResolver paths,
                CancellationToken ct
            ) =>
            {
                var endpoint = ReelContract.TrickplaySprite.Bind(request);
                var fullPath = paths.ResolvePlayableFile(request.Path);
                if (fullPath is null)
                {
                    return endpoint.Error(404, EndpointErrors.MediaNotFound).ToResult();
                }

                var metadata = await GetSheetsAsync(generator, fullPath, ct);
                if (metadata is null)
                {
                    return endpoint.Error(404, EndpointErrors.TrickplayUnavailable).ToResult();
                }

                var sheet = request.Sheet ?? 0;
                return
                    sheet < 0
                    || sheet >= FfmpegTrickplayGenerator.CalculateSheetCount(metadata.TotalThumbs)
                    ? endpoint
                        .Error(
                            404,
                            new ErrorResponse("sprite_not_found", "Sprite sheet not found.")
                        )
                        .ToResult()
                    : endpoint.File(generator.SpritePath(metadata, sheet)).ToResult();
            }
        );

        return endpoints;
    }

    private static Task<TrickplaySheets?> GetSheetsAsync(
        FfmpegTrickplayGenerator generator,
        string fullPath,
        CancellationToken ct
    ) =>
        MediaPathResolver.IsAudio(fullPath)
            ? Task.FromResult<TrickplaySheets?>(null)
            : generator.GetOrGenerateAsync(fullPath, ct);
}
