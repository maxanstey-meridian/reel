namespace Reel.Server.Endpoints;

using Reel.Server.Application;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Rivet;

public static class LibraryEndpoints
{
    public static WebApplication MapLibraryEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.Library.Route,
            async (ILibraryScanner scanner, IMediaStore mediaStore, CancellationToken ct) =>
            {
                var tree = await scanner.BuildTreeAsync(ct);
                var enriched = await EnrichWithWatchedAsync(tree, mediaStore, ct);
                return ReelContract.Library.Success(enriched).ToResult();
            }
        );

        app.MapPost(
            ReelContract.LibraryRescan.Route,
            async (LibraryIndexReconciler reconciler, CancellationToken ct) =>
            {
                var mediaCount = await reconciler.ReconcileAsync(ct);
                return ReelContract.LibraryRescan.Success(new LibraryRescanResponse(mediaCount)).ToResult();
            }
        );

        return app;
    }

    private static async Task<FileNode> EnrichWithWatchedAsync(
        FileNode node,
        IMediaStore mediaStore,
        CancellationToken ct
    )
    {
        if (node.Type == FileNodeType.File)
        {
            var watched = await mediaStore.IsWatchedAsync(node.RelativePath, ct);
            return node with { Watched = watched };
        }

        if (node.Children is not { Count: > 0 })
            return node;

        var enrichedChildren = new List<FileNode>(node.Children.Count);
        foreach (var child in node.Children)
        {
            enrichedChildren.Add(await EnrichWithWatchedAsync(child, mediaStore, ct));
        }

        return node with
        {
            Children = enrichedChildren,
        };
    }
}
