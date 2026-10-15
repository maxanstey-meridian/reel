using Reel.Server.Storage;

namespace Reel.Server.Media;

public sealed class FfprobeSubtitleDiscovery(
    MediaPathResolver paths,
    ILogger<FfprobeSubtitleDiscovery> logger
)
{
    private static readonly HashSet<string> SidecarExtensions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".srt",
        ".ass",
        ".vtt",
    };

    private static readonly HashSet<string> ImageCodecs =
    [
        "hdmv_pgs_subtitle",
        "dvd_subtitle",
        "dvb_subtitle",
        "xsub",
    ];

    public static bool IsImageBased(Subtitle track) => ImageCodecs.Contains(track.Format);

    /// <summary>
    /// Embedded tracks keep their subtitle-stream number as Index; sidecars ({stem}.srt or {stem}.{lang}.srt)
    /// follow in name order, so indexes stay stable between calls.
    /// </summary>
    public async Task<IReadOnlyList<Subtitle>> DiscoverAsync(string fullPath, CancellationToken ct)
    {
        var probe = await Ffmpeg.ProbeAsync(fullPath, "s", logger, ct);
        var embedded = (probe?.Streams ?? [])
            .Select(
                (stream, index) =>
                    new Subtitle(
                        index,
                        stream.Tags?.GetValueOrDefault("title") ?? "",
                        stream.Tags?.GetValueOrDefault("language") ?? "",
                        stream.CodecName ?? "",
                        null
                    )
            )
            .ToList();

        var stem = Path.GetFileNameWithoutExtension(fullPath);
        var relativeDirectory = Path.GetDirectoryName(paths.ToRelativePath(fullPath))!;
        var sidecars = new DirectoryInfo(Path.GetDirectoryName(fullPath)!)
            .EnumerateFiles()
            .Where(file =>
                MediaPathResolver.IsLibraryEntry(file) && SidecarExtensions.Contains(file.Extension)
            )
            .Select(file => (File: file, Suffix: Path.GetFileNameWithoutExtension(file.Name)))
            .Where(sidecar =>
                sidecar.Suffix.StartsWith(stem, StringComparison.OrdinalIgnoreCase)
                && (sidecar.Suffix.Length == stem.Length || sidecar.Suffix[stem.Length] == '.')
            )
            .OrderBy(sidecar => sidecar.File.Name, MediaPathResolver.NaturalOrder)
            .Select(
                (sidecar, index) =>
                {
                    var language = sidecar.Suffix[stem.Length..].TrimStart('.');
                    return new Subtitle(
                        embedded.Count + index,
                        language.Length == 0 ? sidecar.File.Name : language,
                        language,
                        sidecar.File.Extension[1..],
                        Path.Join(relativeDirectory, sidecar.File.Name)
                    );
                }
            );
        return [.. embedded, .. sidecars];
    }
}
