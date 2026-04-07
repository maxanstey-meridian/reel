namespace Reel.Server.Application;

using System.Text.RegularExpressions;
using Reel.Server.Domain;

public static partial class FilenameParser
{
    private static readonly string[] VideoExtensions =
    [
        ".mkv",
        ".mp4",
        ".avi",
        ".mov",
        ".wmv",
        ".flv",
        ".webm",
        ".m4v",
        ".ts",
    ];

    private static readonly string[] KnownTags =
    [
        "1080p",
        "720p",
        "2160p",
        "4K",
        "UHD",
        "BluRay",
        "Blu-Ray",
        "BRRip",
        "BDRip",
        "WEBRip",
        "WEB-DL",
        "WEB",
        "HDRip",
        "DVDRip",
        "DVDScr",
        "x264",
        "x265",
        "H264",
        "H265",
        "HEVC",
        "AVC",
        "AAC",
        "AC3",
        "DTS",
        "DTS-HD",
        "FLAC",
        "MP3",
        "YIFY",
        "RARBG",
        "EVO",
        "SPARKS",
        "GECKOS",
        "10bit",
        "8bit",
        "HDR",
        "HDR10",
        "SDR",
        "DV",
        "Dolby",
        "Atmos",
        "REMUX",
        "PROPER",
        "REPACK",
        "EXTENDED",
        "UNRATED",
        "IMAX",
        "Directors",
        "Cut",
    ];

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex YearPattern();

    public static ParsedTitle Parse(string filename)
    {
        var name = filename;

        // Strip known video extensions
        foreach (var ext in VideoExtensions)
        {
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^ext.Length];
                break;
            }
        }

        // Replace dots and underscores with spaces
        name = name.Replace('.', ' ').Replace('_', ' ');

        // Extract year — take the last match (handles "2001 A Space Odyssey 1968")
        // Also handles parenthesized years like "Inception (2010)"
        int? year = null;
        var yearMatches = YearPattern().Matches(name);
        if (yearMatches.Count > 0)
        {
            var lastMatch = yearMatches[^1];
            year = int.Parse(lastMatch.Value);

            // Check if the year was in parentheses and remove the opening paren too
            var truncateIndex = lastMatch.Index;
            if (truncateIndex > 0 && name[truncateIndex - 1] == '(')
                truncateIndex--;

            // Truncate everything from the year (or its opening paren) onward
            name = name[..truncateIndex];
        }

        // Strip known tags (case-insensitive)
        foreach (var tag in KnownTags)
        {
            name = Regex.Replace(name, @"\b" + Regex.Escape(tag) + @"\b", " ", RegexOptions.IgnoreCase);
        }

        // Strip content in square brackets or parentheses (release group markers)
        name = Regex.Replace(name, @"\[.*?\]", " ");
        name = Regex.Replace(name, @"\(.*?\)", " ");

        // Replace hyphens surrounded by spaces (separators, not part of hyphenated words)
        name = Regex.Replace(name, @"\s-\s", " ");
        // Strip trailing hyphen + release group (e.g. "- YIFY")
        name = Regex.Replace(name, @"\s*-\s*$", "");

        // Collapse multiple spaces and trim
        name = Regex.Replace(name, @"\s{2,}", " ").Trim();

        // Fallback if result is empty
        if (string.IsNullOrWhiteSpace(name))
        {
            var fallback = Path.GetFileNameWithoutExtension(filename);
            name = string.IsNullOrWhiteSpace(fallback) ? filename : fallback;
        }

        return new ParsedTitle(name, year);
    }
}
