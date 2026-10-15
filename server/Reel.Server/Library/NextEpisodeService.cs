using System.Text.RegularExpressions;
using Reel.Server.Contracts.Reel;
using Reel.Server.Storage;

namespace Reel.Server.Library;

public sealed partial class NextEpisodeService(MediaPathResolver paths)
{
    // Only an unambiguous adjacent SxxExx episode in the same folder and series.
    public NextEpisodeResponse Find(string relativePath)
    {
        var fullPath = paths.ResolvePlayableFile(relativePath);
        if (fullPath is null)
        {
            return new(null, null);
        }

        var current = EpisodePattern().Match(Path.GetFileNameWithoutExtension(fullPath));
        if (!current.Success)
        {
            return new(null, null);
        }

        var series = Normalize(current.Groups[1].Value);
        var season = int.Parse(current.Groups[2].Value);
        var episode = int.Parse(current.Groups[3].Value);
        var matches = Directory
            .EnumerateFiles(Path.GetDirectoryName(fullPath)!)
            .Where(MediaPathResolver.IsPlayable)
            .Where(file =>
            {
                var match = EpisodePattern().Match(Path.GetFileNameWithoutExtension(file));
                return match.Success
                    && Normalize(match.Groups[1].Value) == series
                    && int.Parse(match.Groups[2].Value) == season
                    && int.Parse(match.Groups[3].Value) == episode + 1;
            })
            .Select(paths.ToRelativePath)
            .Where(file => paths.ResolvePlayableFile(file) is not null)
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? new(matches[0], FilenameParser.Parse(Path.GetFileName(matches[0])).Title)
            : new(null, null);
    }

    private static string Normalize(string value) =>
        Regex.Replace(value, @"[\s._-]+", " ").Trim().ToUpperInvariant();

    [GeneratedRegex(@"^(.*?)[ ._-]*S(\d{1,2})E(\d{1,3})(?!\d|E\d)", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodePattern();
}
