namespace Reel.Server.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Reel.Server.Application;
using Reel.Server.Application.Models;
using Reel.Server.Domain;

public static class CastEndpoints
{
    public static WebApplication MapCastEndpoints(this WebApplication app)
    {
        app.MapPost(
            ReelContract.Cast.Route,
            async ([FromBody] CastRequest request, CastService castService) =>
                (
                    await ReelContract.Cast.Invoke(
                        request,
                        req =>
                        {
                            castService.SetPlay(req.RelativePath);
                            return Task.CompletedTask;
                        }
                    )
                ).ToResult()
        );

        app.MapGet(
            ReelContract.CastCurrent.Route,
            async (CastService castService) =>
                (
                    await ReelContract.CastCurrent.Invoke(() =>
                    {
                        var command = castService.GetPending();
                        return Task.FromResult(new CastStatus(command));
                    })
                ).ToResult()
        );

        app.MapPost(
            ReelContract.CastAck.Route,
            async (CastService castService) =>
                (
                    await ReelContract.CastAck.Invoke(() =>
                    {
                        castService.Acknowledge();
                        return Task.CompletedTask;
                    })
                ).ToResult()
        );

        app.MapPost(
            ReelContract.CastStop.Route,
            async (CastService castService) =>
                (
                    await ReelContract.CastStop.Invoke(() =>
                    {
                        castService.SetStop();
                        return Task.CompletedTask;
                    })
                ).ToResult()
        );

        return app;
    }
}
