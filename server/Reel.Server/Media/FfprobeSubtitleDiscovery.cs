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

        // Sidecars named after the file ("{stem}.srt", "{stem}.en.srt"); what follows the stem is the language.
        IEnumerable<(FileInfo File, string Language)> NamedAfterFile(IEnumerable<FileInfo> files) =>
            files
                .Select(file => (File: file, Name: Path.GetFileNameWithoutExtension(file.Name)))
                .Where(sidecar =>
                    sidecar.Name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)
                    && (sidecar.Name.Length == stem.Length || sidecar.Name[stem.Length] == '.')
                )
                .Select(sidecar => (sidecar.File, sidecar.Name[stem.Length..].TrimStart('.')));
        // Here the whole file name is the language, e.g. "7_English" or "1000_en-US".
        static IEnumerable<(FileInfo File, string Language)> NamedByLanguage(
            IEnumerable<FileInfo> files
        ) => files.Select(file => (file, Path.GetFileNameWithoutExtension(file.Name)));

        var beside = NamedAfterFile(SidecarFiles(directory));
        // RARBG puts an episode's subtitles in Subs/{stem}/ and a film's loose in Subs/. Loose ones named after a
        // file belong to that file, and ones named by language alone only to a video that is alone in its folder,
        // so a season folder's episodes never take each other's. A linked Subs/ would lead out of the library, so
        // neither it nor its episode folders are read.
        var subs = new DirectoryInfo(Path.Join(directory.FullName, "Subs"));
        List<(FileInfo File, string Language)> inSubs = [];
        if (subs.Exists && MediaPathResolver.IsLibraryEntry(subs))
        {
            var loose = SidecarFiles(subs).ToList();
            var named = NamedAfterFile(loose).ToList();
            var alone =
                directory
                    .EnumerateFiles()
                    .Count(file =>
                        MediaPathResolver.IsLibraryEntry(file)
                        && MediaPathResolver.IsVideo(file.Name)
                    ) == 1;
            inSubs =
            [
                .. NamedByLanguage(SidecarFiles(new DirectoryInfo(Path.Join(subs.FullName, stem)))),
                .. named,
                .. alone
                    ? NamedByLanguage(loose.Except(named.Select(sidecar => sidecar.File)))
                    : [],
            ];
        }
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
