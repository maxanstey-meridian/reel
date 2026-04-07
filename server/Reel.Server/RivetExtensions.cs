namespace Reel.Server;

using Rivet;

public static class RivetExtensions
{
    public static IResult ToResult<T>(this RivetResult<T> result) =>
        Results.Json(result.Data, statusCode: result.StatusCode);

    public static IResult ToResult(this RivetResult result) =>
        result.StatusCode == 204 ? Results.NoContent() : Results.StatusCode(result.StatusCode);
}
