namespace Reel.Server.Infrastructure.Ffmpeg;

using System.Diagnostics;
using System.Text.Json;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.FileSystem;

public sealed class FfprobeSubtitleDiscovery : ISubtitleDiscovery
{
    private static readonly HashSet<string> SubtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt",
        ".ass",
        ".vtt",
    };

    private readonly MediaPathResolver _pathResolver;
    private readonly ILogger<FfprobeSubtitleDiscovery> _logger;

    public FfprobeSubtitleDiscovery(MediaPathResolver pathResolver, ILogger<FfprobeSubtitleDiscovery> logger)
    {
        _pathResolver = pathResolver;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubtitleTrack>> DiscoverAsync(string relativePath, CancellationToken ct)
    {
        var fullPath = _pathResolver.ResolvePlayableFile(relativePath);
        if (fullPath is null)
            return [];

        var tracks = new List<SubtitleTrack>();

        var embedded = await DiscoverEmbeddedAsync(fullPath, ct);
        tracks.AddRange(embedded);

        var external = DiscoverExternal(fullPath, relativePath);
        // Assign external tracks indices starting after embedded count
        var externalIndex = tracks.Count;
        foreach (var ext in external)
        {
            tracks.Add(ext with { Index = externalIndex });
            externalIndex++;
        }

        return tracks;
    }

    private async Task<List<SubtitleTrack>> DiscoverEmbeddedAsync(string fullPath, CancellationToken ct)
    {
        var tracks = new List<SubtitleTrack>();

        try
        {
            var result = await ProcessRunner.RunAsync(new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v quiet -print_format json -show_streams -select_streams s \"{fullPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }, ct);

            if (result.ExitCode != 0)
            {
                _logger.LogWarning("ffprobe exited with code {ExitCode}: {Error}", result.ExitCode, result.StandardError);
                return tracks;
            }

            tracks.AddRange(ParseFfprobeOutput(result.StandardOutputText));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffprobe subtitle discovery failed for {Path}", fullPath);
        }

        return tracks;
    }

    internal static List<SubtitleTrack> ParseFfprobeOutput(string json)
    {
        var tracks = new List<SubtitleTrack>();

        if (string.IsNullOrWhiteSpace(json))
            return tracks;

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("streams", out var streams))
            return tracks;

        var index = 0;
        foreach (var stream in streams.EnumerateArray())
        {
            var codecName = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() ?? "" : "";
            var label = "";
            var language = "";

            if (stream.TryGetProperty("tags", out var tags))
            {
                if (tags.TryGetProperty("title", out var titleProp))
                    label = titleProp.GetString() ?? "";
                if (tags.TryGetProperty("language", out var langProp))
                    language = langProp.GetString() ?? "";
            }

            tracks.Add(new SubtitleTrack(
                Index: index,
                Label: label,
                Language: language,
                Format: codecName,
                Source: "embedded",
                ExternalPath: null
            ));

            index++;
        }

        return tracks;
    }

    private List<SubtitleTrack> DiscoverExternal(string fullPath, string relativePath)
    {
        var tracks = new List<SubtitleTrack>();
        var directory = Path.GetDirectoryName(fullPath);

        if (directory is null || !Directory.Exists(directory))
            return tracks;

        var stem = Path.GetFileNameWithoutExtension(fullPath);
        var relativeDir = Path.GetDirectoryName(relativePath) ?? "";

        foreach (var file in Directory.GetFiles(directory))
        {
            var fileName = Path.GetFileName(file);
            var ext = Path.GetExtension(fileName);

            if (!SubtitleExtensions.Contains(ext))
                continue;

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            // Match {stem}.{ext} or {stem}.{lang}.{ext}
            if (!nameWithoutExt.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
                continue;

            var remainder = nameWithoutExt[stem.Length..];

            string language;
            if (remainder.Length == 0)
            {
                // Exact match: movie.srt
                language = "";
            }
            else if (remainder.StartsWith('.'))
            {
                // Language tag: movie.en.srt
                language = remainder[1..];
            }
            else
            {
                // Not a match (e.g., "movie-extras.srt" for stem "movie")
                continue;
            }

            var format = ext[1..]; // strip leading dot
            var externalRelativePath = string.IsNullOrEmpty(relativeDir)
                ? fileName
                : Path.Combine(relativeDir, fileName);

            tracks.Add(new SubtitleTrack(
                Index: 0, // placeholder, assigned by caller
                Label: string.IsNullOrEmpty(language) ? fileName : language,
                Language: language,
                Format: format,
                Source: "external",
                ExternalPath: externalRelativePath
            ));
        }

        return tracks;
    }
}
