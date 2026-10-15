using Reel.Server.Common;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;
using Rivet;

namespace Reel.Server.Web;

public static class HealthEndpoints
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(2);

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ReelContract.HealthRoute,
            async (DatabaseService database, MediaPathResolver paths, ILogger<Program> logger) =>
            {
                var problem = await FindProblemAsync(database, paths, logger);
                return problem is null
                    ? ReelContract.Health.Success(new HealthResponse("healthy")).ToResult()
                    : ReelContract
                        .Health.Error(503, new ErrorResponse("unavailable", problem))
                        .ToResult();
            }
        );

        return endpoints;
    }

    // Both checks can block indefinitely on a hung volume, so they run off the request thread.
    private static async Task<string?> FindProblemAsync(
        DatabaseService database,
        MediaPathResolver paths,
        ILogger logger
    )
    {
        try
        {
            return await Task.Run(() =>
                {
                    if (!Directory.Exists(paths.MediaRoot))
                    {
                        return "Media folder is not available.";
                    }

                    database.Ping();
                    return null;
                })
                .WaitAsync(CheckTimeout);
        }
        catch (TimeoutException)
        {
            return "Media folder or database did not respond in time.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health check could not reach the database");
            return "Database is not available.";
        }
    }
}
