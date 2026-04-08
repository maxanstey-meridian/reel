namespace Reel.Server.Endpoints;

using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Extensions;

public static class LibraryEndpoints
{
    public static WebApplication MapLibraryEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.Library.Route,
            async (HttpContext context, ILibraryScanner scanner, IMediaStore mediaStore, CancellationToken ct) =>
                (
                    await ReelContract.Library.Invoke(async () =>
                    {
                        var userId = context.GetUserId();
                        var tree = await scanner.BuildTreeAsync(ct);
                        return await EnrichWithWatchedAsync(tree, userId, mediaStore, ct);
                    })
                ).ToResult()
        );

        return app;
    }

    private static async Task<FileNode> EnrichWithWatchedAsync(
        FileNode node,
        int userId,
        IMediaStore mediaStore,
        CancellationToken ct
    )
    {
        if (node.Type == FileNodeType.File)
        {
            var watched = await mediaStore.IsWatchedAsync(userId, node.RelativePath, ct);
            return node with { Watched = watched };
        }

        if (node.Children is not { Count: > 0 })
            return node;

        var enrichedChildren = new List<FileNode>(node.Children.Count);
        foreach (var child in node.Children)
        {
            enrichedChildren.Add(await EnrichWithWatchedAsync(child, userId, mediaStore, ct));
        }

        return node with
        {
            Children = enrichedChildren,
        };
    }
}
