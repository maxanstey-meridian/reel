namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain.Exceptions;

public static class UserEndpoints
{
    public static WebApplication MapUserEndpoints(this WebApplication app)
    {
        app.MapPost(
            ReelContract.UserCreate.Route,
            async ([FromBody] UserRequest request, IUserStore store, CancellationToken ct) =>
                (
                    await ReelContract.UserCreate.Invoke(
                        request,
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(req.Name))
                                throw new BadRequestException("Name is required");

                            return await store.CreateAsync(req.Name, req.Icon, ct);
                        }
                    )
                ).ToResult()
        );

        app.MapGet(
            ReelContract.UserList.Route,
            async (IUserStore store, CancellationToken ct) =>
                (
                    await ReelContract.UserList.Invoke(async () =>
                    {
                        var users = await store.GetAllAsync(ct);
                        return users.ToArray();
                    })
                ).ToResult()
        );

        app.MapGet(
            ReelContract.UserGet.Route,
            async (int id, IUserStore store, CancellationToken ct) =>
                (
                    await ReelContract.UserGet.Invoke(async () =>
                    {
                        var user = await store.GetAsync(id, ct);
                        return user ?? throw new NotFoundException("User not found");
                    })
                ).ToResult()
        );

        app.MapMethods(
            ReelContract.UserUpdate.Route,
            ["PATCH"],
            async (int id, [FromBody] UserRequest request, IUserStore store, CancellationToken ct) =>
                (
                    await ReelContract.UserUpdate.Invoke(
                        request,
                        async req =>
                        {
                            var user = await store.UpdateAsync(id, req.Name, req.Icon, ct);
                            return user ?? throw new NotFoundException("User not found");
                        }
                    )
                ).ToResult()
        );

        app.MapDelete(
            ReelContract.UserDelete.Route,
            async (int id, IUserStore store, CancellationToken ct) =>
                (
                    await ReelContract.UserDelete.Invoke(async () =>
                    {
                        if (id == 1)
                            throw new BadRequestException("Cannot delete default user");

                        if (!await store.DeleteAsync(id, ct))
                            throw new NotFoundException("User not found");
                    })
                ).ToResult()
        );

        return app;
    }
}
