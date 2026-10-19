using Reel.Server.Contracts.Playback;
using Reel.Server.Media;
using Reel.Server.Storage;

namespace Reel.Server.Playback;

/// <summary>
/// Remembers the quality and subtitle language last chosen for a playing file and starts every new session with
/// them, including one the TV starts itself for the next episode.
/// </summary>
public sealed class PlaybackPreferenceService(
    SqlitePreferencesStore store,
    FfprobeSubtitleDiscovery discovery,
    MediaPathResolver paths,
    ILogger<PlaybackPreferenceService> logger
)
{
    /// <summary>The remembered quality, and the file's track in the remembered language if it has one.</summary>
    public async Task<(int? MaxHeight, int? SubtitleIndex)> StartingChoicesAsync(
        string relativePath,
        CancellationToken ct
    )
    {
        var preferences = await store.GetAsync();
        var fullPath = paths.ResolvePlayableFile(relativePath);
        if (preferences.SubtitleLanguage is not { } language || fullPath is null)
        {
            return (preferences.MaxHeight, null);
        }

        var track = SubtitleLanguage.Match(await discovery.DiscoverAsync(fullPath, ct), language);
        return (preferences.MaxHeight, track?.Index);
    }

    /// <summary>
    /// Records a quality or subtitle change made to the playing file. A track whose language can't be told leaves
    /// the remembered language as it was. The intent is already applied, so a failure here is only logged.
    /// </summary>
    public async Task RememberAsync(
        PlaybackIntentResponse? before,
        PlaybackIntentResponse after,
        CancellationToken ct
    )
    {
        if (
            before is null
            || before.RelativePath != after.RelativePath
            || (before.MaxHeight == after.MaxHeight && before.SubtitleIndex == after.SubtitleIndex)
        )
        {
            return;
        }

        try
        {
            var preferences = await store.GetAsync();
            if (before.SubtitleIndex != after.SubtitleIndex && after.SubtitleIndex is null)
            {
                preferences = preferences with { SubtitleLanguage = null };
            }
            else if (
                before.SubtitleIndex != after.SubtitleIndex
                && await LanguageOfAsync(after.RelativePath, after.SubtitleIndex!.Value, ct)
                    is { } language
            )
            {
                preferences = preferences with { SubtitleLanguage = language };
            }

            await store.SaveAsync(preferences with { MaxHeight = after.MaxHeight });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not remember playback preferences");
        }
    }

    private async Task<string?> LanguageOfAsync(
        string relativePath,
        int index,
        CancellationToken ct
    )
    {
        var fullPath = paths.ResolvePlayableFile(relativePath);
        var track = fullPath is null
            ? null
            : (await discovery.DiscoverAsync(fullPath, ct)).FirstOrDefault(t => t.Index == index);
        return track is null ? null : SubtitleLanguage.Of(track);
    }
}
