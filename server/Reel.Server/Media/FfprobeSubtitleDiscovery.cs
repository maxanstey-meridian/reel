using Reel.Server.Contracts.Reel;
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
    /// The tracks a stream of this file at maxHeight can carry. A live stream carries no subtitle streams (-sn), and
    /// image-based tracks cannot become WebVTT, so it offers none of them.
    /// </summary>
    public async Task<IReadOnlyList<Subtitle>> OfferedAsync(
        string fullPath,
        int? maxHeight,
        CancellationToken ct
    )
    {
        var tracks = await DiscoverAsync(fullPath, ct);
        if (!tracks.Any(IsImageBased))
        {
            return tracks;
        }

        var plan = await Transcoding.PlanAsync(maxHeight, fullPath, logger, ct);
        return plan.Mode == StreamMode.Direct ? tracks : [.. tracks.Where(t => !IsImageBased(t))];
    }

    /// <summary>
    /// Embedded tracks keep their subtitle-stream number as Index; sidecars ({stem}.srt or {stem}.{lang}.srt)
    /// follow in name order, then RARBG-style Subs/{stem}/{n}_{lang}.srt and Subs/{n}_{lang}.srt (a film's) together
    /// in name order, so indexes stay stable between calls.
    /// </summary>
    public async Task<IReadOnlyList<Subtitle>> DiscoverAsync(string fullPath, CancellationToken ct)
    {
        var probe = await Ffmpeg.ProbeAsync(fullPath, "s", logger, ct);
        var embedded = (probe?.Streams ?? [])
            .Select(
                (stream, index) =>
                {
                    var title = stream.Tags?.GetValueOrDefault("title");
                    var language = stream.Tags?.GetValueOrDefault("language") ?? "";
                    return new Subtitle(
                        index,
                        // Clients show the language beside the label, e.g. "Track 2 (eng)".
                        !string.IsNullOrWhiteSpace(title) ? title : $"Track {index + 1}",
                        language,
                        stream.CodecName ?? "",
                        null
                    );
                }
            )
            .ToList();

        var stem = Path.GetFileNameWithoutExtension(fullPath);
        var directory = new DirectoryInfo(Path.GetDirectoryName(fullPath)!);
        var beside = SidecarFiles(directory)
            .Select(file => (File: file, Name: Path.GetFileNameWithoutExtension(file.Name)))
            .Where(sidecar =>
                sidecar.Name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)
                && (sidecar.Name.Length == stem.Length || sidecar.Name[stem.Length] == '.')
            )
            .Select(sidecar =>
                (sidecar.File, Language: sidecar.Name[stem.Length..].TrimStart('.'))
            );
        // The whole file name is the language here, e.g. "7_English" or "1000_en-US".
        // A linked Subs/ would lead out of the library, so neither it nor its episode folders are read.
        var subs = new DirectoryInfo(Path.Join(directory.FullName, "Subs"));
        IEnumerable<FileInfo> subsFiles =
            subs.Exists && MediaPathResolver.IsLibraryEntry(subs)
                ?
                [
                    .. SidecarFiles(new DirectoryInfo(Path.Join(subs.FullName, stem))),
                    .. SidecarFiles(subs),
                ]
                : [];
        var inSubs = subsFiles.Select(file =>
            (File: file, Language: Path.GetFileNameWithoutExtension(file.Name))
        );
        var sidecars = beside
            .OrderBy(sidecar => sidecar.File.Name, MediaPathResolver.NaturalOrder)
            .Concat(inSubs.OrderBy(sidecar => sidecar.File.Name, MediaPathResolver.NaturalOrder))
            .Select(
                (sidecar, index) =>
                    // The format tells sidecars of one language apart, e.g. "External SRT (en)".
                    new Subtitle(
                        embedded.Count + index,
                        $"External {sidecar.File.Extension[1..].ToUpperInvariant()}",
                        sidecar.Language,
                        sidecar.File.Extension[1..],
                        paths.ToRelativePath(sidecar.File.FullName)
                    )
            );
        return [.. embedded, .. sidecars];
    }

    private static IEnumerable<FileInfo> SidecarFiles(DirectoryInfo directory) =>
        directory.Exists && MediaPathResolver.IsLibraryEntry(directory)
            ? directory
                .EnumerateFiles()
                .Where(file =>
                    MediaPathResolver.IsLibraryEntry(file)
                    && SidecarExtensions.Contains(file.Extension)
                )
            : [];
}
