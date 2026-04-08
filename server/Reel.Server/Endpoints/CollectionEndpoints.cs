namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Extensions;

public static class CollectionEndpoints
{
    public static WebApplication MapCollectionEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.CollectionsList.Route,
            async (HttpContext context, ICollectionStore store, CancellationToken ct) =>
                (
                    await ReelContract.CollectionsList.Invoke(async () =>
                    {
                        var userId = context.GetUserId();
                        return await store.GetAllAsync(userId, ct);
                    })
                ).ToResult()
        );

        app.MapPost(
            ReelContract.CollectionsCreate.Route,
            async (
                HttpContext context,
                [FromBody] CreateCollectionRequest request,
                ICollectionStore store,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.CollectionsCreate.Invoke(
                        request,
                        async req =>
                        {
                            var userId = context.GetUserId();
                            if (string.IsNullOrWhiteSpace(req.Name))
                                throw new BadRequestException("Name is required");

                            return await store.CreateAsync(userId, req.Name, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapGet(
            ReelContract.CollectionsGet.Route,
            async (int id, HttpContext context, ICollectionStore store, CancellationToken ct) =>
                (
                    await ReelContract.CollectionsGet.Invoke(async () =>
                    {
                        var userId = context.GetUserId();
                        return await store.GetWithItemsAsync(id, userId, ct)
                            ?? throw new NotFoundException("Collection not found");
                    })
                ).ToResult()
        );

        app.MapMethods(
            ReelContract.CollectionsRename.Route,
            ["PATCH"],
            async (
                int id,
                [FromBody] RenameCollectionRequest request,
                HttpContext context,
                ICollectionStore store,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.CollectionsRename.Invoke(
                        request,
                        async req =>
                        {
                            var userId = context.GetUserId();
                            if (string.IsNullOrWhiteSpace(req.Name))
                                throw new BadRequestException("Name is required");

                            await store.RenameAsync(id, userId, req.Name, ct);

                            var all = await store.GetAllAsync(userId, ct);
                            return all.FirstOrDefault(c => c.Id == id)
                                ?? throw new NotFoundException("Collection not found");
                        }
                    )
                ).ToResult()
        );

        app.MapDelete(
            ReelContract.CollectionsDelete.Route,
            async (int id, HttpContext context, ICollectionStore store, CancellationToken ct) =>
                (
                    await ReelContract.CollectionsDelete.Invoke(async () =>
                    {
                        var userId = context.GetUserId();
                        await store.DeleteAsync(id, userId, ct);
                    })
                ).ToResult()
        );

        app.MapPost(
            ReelContract.CollectionsAddItem.Route,
            async (
                HttpContext context,
                [FromBody] CollectionItemRequest request,
                ICollectionStore store,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.CollectionsAddItem.Invoke(
                        request,
                        async req =>
                        {
                            var userId = context.GetUserId();
                            await store.AddItemAsync(req.CollectionId, userId, req.Path, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapDelete(
            ReelContract.CollectionsRemoveItem.Route,
            async (
                HttpContext context,
                [FromQuery] int collectionId,
                [FromQuery] string path,
                ICollectionStore store,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.CollectionsRemoveItem.Invoke(
                        new CollectionItemRequest(collectionId, path),
                        async req =>
                        {
                            var userId = context.GetUserId();
                            await store.RemoveItemAsync(req.CollectionId, userId, req.Path, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapMethods(
            ReelContract.CollectionsReorder.Route,
            ["PUT"],
            async (
                HttpContext context,
                [FromBody] ReorderItemsRequest request,
                ICollectionStore store,
                CancellationToken ct
            ) =>
                (
                    await ReelContract.CollectionsReorder.Invoke(
                        request,
                        async req =>
                        {
                            var userId = context.GetUserId();
                            await store.ReorderAsync(req.CollectionId, userId, req.Paths, ct);
                        }
                    )
                ).ToResult()
        );

        return app;
    }
}
