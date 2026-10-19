using System.Globalization;
using System.Text.RegularExpressions;

namespace Reel.Server.Media;

/// <summary>
/// Turns the many ways a subtitle names its language ("eng", "English", "en-US", "7_English", "1000_en-US", a ".en"
/// sidecar suffix) into one ISO 639-1 code, so a remembered language can be matched against any file's tracks.
/// </summary>
public static partial class SubtitleLanguage
{
    // ISO 639-2/B codes, which Matroska tags use, where they differ from the 639-2/T codes .NET knows.
    private static readonly Dictionary<string, string> BibliographicCodes = new()
    {
        ["alb"] = "sq",
        ["arm"] = "hy",
        ["baq"] = "eu",
        ["bur"] = "my",
        ["chi"] = "zh",
        ["cze"] = "cs",
        ["dut"] = "nl",
        ["fre"] = "fr",
        ["geo"] = "ka",
        ["ger"] = "de",
        ["gre"] = "el",
        ["ice"] = "is",
        ["mac"] = "mk",
        ["may"] = "ms",
        ["per"] = "fa",
        ["rum"] = "ro",
        ["slo"] = "sk",
        ["wel"] = "cy",
    };

    // Codes ("en", "eng") and English names ("english") of every language .NET knows, keyed to the two-letter code.
    private static readonly Dictionary<string, string> Codes = BuildCodes();

    // Names alone, for free-text labels where a stray two- or three-letter word must not read as a language.
    private static readonly Dictionary<string, string> Names = CultureInfo
        .GetCultures(CultureTypes.NeutralCultures)
        .Where(culture => culture.TwoLetterISOLanguageName.Length == 2)
        .GroupBy(culture => culture.EnglishName.ToLowerInvariant())
        .ToDictionary(group => group.Key, group => group.First().TwoLetterISOLanguageName);

    /// <summary>
    /// The two-letter code of a language tag or sidecar suffix, read from its first recognised part, or null.
    /// </summary>
    public static string? FromTag(string? tag) =>
        tag is null
            ? null
            : Parts()
                .Split(LeadingNumber().Replace(tag.Trim(), "").ToLowerInvariant())
                .Select(part => Codes.GetValueOrDefault(part))
                .FirstOrDefault(code => code is not null);

    /// <summary>The two-letter code of a language named in full in a free-text label such as "English (SDH)".</summary>
    public static string? FromLabel(string? label) =>
        label is null
            ? null
            : Parts()
                .Split(label.ToLowerInvariant())
                .Select(part => Names.GetValueOrDefault(part))
                .FirstOrDefault(code => code is not null);

    /// <summary>A track's language: its tag first, then a language its label spells out.</summary>
    public static string? Of(Subtitle track) => FromTag(track.Language) ?? FromLabel(track.Label);

    /// <summary>
    /// The track to show for a remembered language: a text track before an image one, a full track before a forced
    /// one, then the first. With no track in that language, a sidecar that names no language at all, since such a
    /// file almost always carries the release's own language.
    /// </summary>
    public static Subtitle? Match(IReadOnlyList<Subtitle> tracks, string language)
    {
        var matching = tracks
            .Where(track => Of(track) == language)
            .OrderBy(track => FfprobeSubtitleDiscovery.IsImageBased(track))
            .ThenBy(IsForced)
            .ThenBy(track => track.Index)
            .FirstOrDefault();
        return matching
            ?? tracks.FirstOrDefault(track =>
                track.ExternalPath is not null && Of(track) is null && !IsForced(track)
            );
    }

    private static bool IsForced(Subtitle track) =>
        $"{track.Label} {track.Language} {track.ExternalPath}".Contains(
            "forced",
            StringComparison.OrdinalIgnoreCase
        );

    private static Dictionary<string, string> BuildCodes()
    {
        var codes = new Dictionary<string, string>(BibliographicCodes);
        foreach (
            var culture in CultureInfo
                .GetCultures(CultureTypes.NeutralCultures)
                .Where(culture => culture.TwoLetterISOLanguageName.Length == 2)
        )
        {
            var code = culture.TwoLetterISOLanguageName;
            codes.TryAdd(code, code);
            codes.TryAdd(culture.ThreeLetterISOLanguageName, code);
            codes.TryAdd(culture.EnglishName.ToLowerInvariant(), code);
        }

        return codes;
    }

    // RARBG numbers its subtitle files: "7_English", "1000_en-US".
    [GeneratedRegex(@"^\d+_")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"[^\p{L}]+")]
    private static partial Regex Parts();
}
