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
    /// <summary>
    /// The remembered quality, and the file's track in the remembered language among those a stream at that quality
    /// can carry. Preferences never stop playback starting: if they can't be read it starts at full quality without
    /// subtitles, and if the file's subtitles can't be listed it starts without them.
    /// </summary>
    public async Task<(int? MaxHeight, int? SubtitleIndex)> StartingChoicesAsync(
        string relativePath,
        CancellationToken ct
    )
    {
        PlaybackPreferences preferences;
        try
        {
            preferences = await store.GetAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read playback preferences");
            return (null, null);
        }

        var fullPath = paths.ResolvePlayableFile(relativePath);
        if (preferences.SubtitleLanguage is not { } language || fullPath is null)
        {
            return (preferences.MaxHeight, null);
        }

        try
        {
            var offered = await Transcoding.OfferedSubtitlesAsync(
                discovery,
                fullPath,
                preferences.MaxHeight,
                logger,
                ct
            );
            return (preferences.MaxHeight, SubtitleLanguage.Match(offered, language)?.Index);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not list subtitles for {Path}", relativePath);
            return (preferences.MaxHeight, null);
        }
    }

    /// <summary>
    /// Records a quality or subtitle change made to the playing file. A track whose language can't be told leaves
    /// the remembered language as it was. The intent is already applied, so a failure here is only logged.
    /// </summary>
    public async Task RememberAsync(
        PlaybackIntentResponse before,
        PlaybackIntentResponse after,
        CancellationToken ct
    )
    {
        try
        {
            if (before.MaxHeight != after.MaxHeight)
            {
                await store.SaveMaxHeightAsync(after.MaxHeight);
            }

            if (before.SubtitleIndex == after.SubtitleIndex)
            {
                return;
            }

            if (after.SubtitleIndex is not { } index)
            {
                await store.SaveSubtitleLanguageAsync(null);
            }
            else if (await LanguageOfAsync(after.RelativePath, index, ct) is { } language)
            {
                await store.SaveSubtitleLanguageAsync(language);
            }
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
