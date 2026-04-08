namespace Reel.Server.Endpoints;

using Reel.Server.Application.Models;
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

        app.MapGet(
            ReelContract.LibraryStats.Route,
            async (ILibraryScanner scanner, CancellationToken ct) =>
                (
                    await ReelContract.LibraryStats.Invoke(async () =>
                    {
                        var tree = await scanner.BuildTreeAsync(ct);
                        var files = new List<FileNode>();
                        CollectFiles(tree, files);
                        var totalBytes = files.Sum(f => f.SizeBytes ?? 0L);
                        return new LibraryStatsResponse(files.Count, totalBytes);
                    })
                ).ToResult()
        );

        app.MapGet(
            ReelContract.LibraryPosters.Route,
            async (ILibraryScanner scanner, int count = 20, CancellationToken ct = default) =>
                (
                    await ReelContract.LibraryPosters.Invoke(async () =>
                    {
                        var tree = await scanner.BuildTreeAsync(ct);
                        var files = new List<FileNode>();
                        CollectFiles(tree, files);
                        return files
                            .OrderBy(_ => Random.Shared.Next())
                            .Take(Math.Min(count, files.Count))
                            .Select(f => f.RelativePath)
                            .ToArray();
                    })
                ).ToResult()
        );

        return app;
    }

    private static void CollectFiles(FileNode node, List<FileNode> files)
    {
        if (node.Type == FileNodeType.File)
        {
            files.Add(node);
            return;
        }

        if (node.Children is not { Count: > 0 })
            return;

        foreach (var child in node.Children)
        {
            CollectFiles(child, files);
        }
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
