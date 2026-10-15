using System.Diagnostics;
using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Library;

public static class LibraryEndpoints
{
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ReelContract.LibraryRoute,
            async (SqliteMediaStore mediaStore, MediaPathResolver paths) =>
            {
                var rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(paths.MediaRoot));
                var root = LibraryEntries.Tree(rootName, await mediaStore.SearchAsync(null));
                return ReelContract.Library.Success(root).ToResult();
            }
        );

        endpoints.MapPost(
            ReelContract.LibraryRescanRoute,
            async (LibraryIndexReconciler reconciler, CancellationToken ct) =>
            {
                switch (await reconciler.ReconcileAsync(ct))
                {
                    case ReconcileOutcome.Indexed indexed:
                        var response = new LibraryRescanResponse(indexed.MediaCount);
                        return ReelContract.LibraryRescan.Success(response).ToResult();
                    case ReconcileOutcome.Refused refused:
                        var error = new ErrorResponse("rescan_refused", refused.Reason);
                        return ReelContract.LibraryRescan.Error(503, error).ToResult();
                    default:
                        throw new UnreachableException();
                }
            }
        );

        endpoints.MapGet(
            ReelContract.NextEpisodeRoute,
            ([AsParameters] FileRequest request, NextEpisodeService next) =>
                ReelContract.NextEpisode.Bind(request).Success(next.Find(request.Path)).ToResult()
        );

        return endpoints;
    }
}
