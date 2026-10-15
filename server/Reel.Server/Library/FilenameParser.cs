using System.Text.RegularExpressions;

namespace Reel.Server.Library;

public static partial class FilenameParser
{
    public static ParsedTitle Parse(string filename)
    {
        var stem = Path.GetFileNameWithoutExtension(filename);
        var name = stem.Replace('.', ' ').Replace('_', ' ');

        // The last year wins ("2001 A Space Odyssey 1968"); everything after it is release detail.
        int? year = null;
        if (YearPattern().Matches(name) is { Count: > 0 } years)
        {
            year = int.Parse(years[^1].Value);
            name = name[..years[^1].Index].TrimEnd('(');
        }

        name = Spaces().Replace(ReleaseNoise().Replace(name, " "), " ").Trim();
        return new ParsedTitle(
            name.Length > 0 ? name
                : stem.Length > 0 ? stem
                : filename,
            year
        );
    }

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex YearPattern();

    // Only tags that never occur as title words, plus [group] and (note) markers and " - " separators.
    [GeneratedRegex(
        @"\b(?:480p|576p|720p|1080p|2160p|4K|UHD|Blu-?Ray|BDRip|BRRip|WEB-?DL|WEBRip|HDRip|DVDRip|REMUX|"
            + @"[xh]26[45]|HEVC|AVC|AAC|AC3|DTS(?:-HD)?|10bit|HDR(?:10)?)\b|\[.*?\]|\(.*?\)|\s-\s|\s*-\s*$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex ReleaseNoise();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
