namespace Reel.Server.Endpoints;

using Reel.Server.Application;
using Reel.Server.Application.Ports;
using Reel.Server.Infrastructure.Configuration;

public static class MetadataEndpoints
{
    public static WebApplication MapMetadataEndpoints(this WebApplication app)
    {
        app.MapGet("/api/files/metadata", async (string? path, ReelConfig config, IMetadataLookup lookup, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(path))
                return Results.BadRequest(new { error = "Missing required query parameter: path" });

            var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, path));

            if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                return Results.Problem(detail: "Access denied.", statusCode: StatusCodes.Status403Forbidden);

            if (!File.Exists(fullPath))
                return Results.NotFound(new { error = "File not found" });

            var parsed = FilenameParser.Parse(Path.GetFileName(fullPath));
            var response = await lookup.LookupAsync(parsed.Title, parsed.Year, ct);

            if (response is null)
                return Results.NotFound(new { error = "No metadata found" });

            return Results.Ok(response);
        });

        return app;
    }
}
