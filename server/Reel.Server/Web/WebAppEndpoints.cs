namespace Reel.Server.Web;

public static class WebAppEndpoints
{
    // The phone UI is a client-routed SPA served from wwwroot; unknown non-file paths load its shell.
    public static IEndpointRouteBuilder MapWebAppEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFallbackToFile("{*path:nonfile}", "index.html");
        // Detail and library routes end in media or folder names (e.g. ".mp4", "Mr. Robot"), which `nonfile` rejects.
        foreach (var section in new[] { "detail", "library" })
        {
            endpoints.MapFallbackToFile($"{section}/{{*path}}", "index.html");
        }

        return endpoints;
    }
}
