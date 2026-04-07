namespace Reel.Server.Endpoints;

using Reel.Server.Application;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Domain.Exceptions;
using Reel.Server.Infrastructure.Configuration;

public static class MetadataEndpoints
{
    public static WebApplication MapMetadataEndpoints(this WebApplication app)
    {
        app.MapGet(
            ReelContract.Metadata.Route,
            async (string? path, ReelConfig config, IMetadataLookup lookup, CancellationToken ct) =>
                (
                    await ReelContract.Metadata.Invoke(
                        new FileRequest(path ?? ""),
                        async req =>
                        {
                            if (string.IsNullOrWhiteSpace(req.Path))
                                throw new BadRequestException("Missing required query parameter: path");

                            var fullPath = Path.GetFullPath(Path.Combine(config.NormalisedMediaRoot, req.Path));

                            if (!fullPath.StartsWith(config.NormalisedMediaRoot, StringComparison.Ordinal))
                                throw new ForbiddenException("Access denied.");

                            if (!File.Exists(fullPath))
                                throw new NotFoundException("File not found");

                            var parsed = FilenameParser.Parse(Path.GetFileName(fullPath));
                            var response = await lookup.LookupAsync(parsed.Title, parsed.Year, ct);

                            return response ?? throw new NotFoundException("No metadata found");
                        }
                    )
                ).ToResult()
        );

        return app;
    }
}
