namespace Reel.Server.Endpoints;

using Reel.Server.Models;
using Reel.Server.Services;

public static class MetadataEndpoints
{
    private const string PosterBaseUrl = "https://image.tmdb.org/t/p/w342";
    private const string BackdropBaseUrl = "https://image.tmdb.org/t/p/w780";

    public static WebApplication MapMetadataEndpoints(this WebApplication app)
    {
        app.MapGet("/api/files/metadata", async (string? path, ReelConfig config, TmdbService tmdbService) =>
        {
            if (string.IsNullOrWhiteSpace(path))
                return Results.BadRequest(new { error = "Missing required query parameter: path" });

            var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, path));

            if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                return Results.Problem(detail: "Access denied.", statusCode: StatusCodes.Status403Forbidden);

            if (!File.Exists(fullPath))
                return Results.NotFound(new { error = "File not found" });

            var parsed = FilenameParser.Parse(Path.GetFileName(fullPath));
            var tmdbResult = await tmdbService.Search(parsed.Title, parsed.Year);

            if (tmdbResult is null)
                return Results.NotFound(new { error = "No metadata found" });

            var response = new MetadataResponse(
                TmdbId: tmdbResult.TmdbId,
                PosterUrl: tmdbResult.PosterPath is not null ? $"{PosterBaseUrl}{tmdbResult.PosterPath}" : null,
                BackdropUrl: tmdbResult.BackdropPath is not null ? $"{BackdropBaseUrl}{tmdbResult.BackdropPath}" : null,
                Overview: tmdbResult.Overview,
                ReleaseDate: tmdbResult.ReleaseDate
            );

            return Results.Ok(response);
        });

        return app;
    }
}
