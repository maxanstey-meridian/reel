namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;

public static class UserEndpoints
{
    public static WebApplication MapUserEndpoints(this WebApplication app)
    {
        app.MapPost("/api/users", async ([FromBody] UserRequest request, IUserStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Name is required" });

            var user = await store.CreateAsync(request.Name, request.Icon, ct);
            return Results.Created($"/api/users/{user.Id}", user);
        });

        app.MapGet("/api/users", async (IUserStore store, CancellationToken ct) =>
        {
            return Results.Ok(await store.GetAllAsync(ct));
        });

        app.MapGet("/api/users/{id:int}", async (int id, IUserStore store, CancellationToken ct) =>
        {
            var user = await store.GetAsync(id, ct);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        });

        app.MapMethods("/api/users/{id:int}", ["PATCH"], async (int id, [FromBody] UserRequest request, IUserStore store, CancellationToken ct) =>
        {
            var user = await store.UpdateAsync(id, request.Name, request.Icon, ct);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        });

        app.MapDelete("/api/users/{id:int}", async (int id, IUserStore store, CancellationToken ct) =>
        {
            if (id == 1)
                return Results.BadRequest(new { error = "Cannot delete default user" });

            return await store.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
