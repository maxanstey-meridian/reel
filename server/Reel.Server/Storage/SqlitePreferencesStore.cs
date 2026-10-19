using Dapper;

namespace Reel.Server.Storage;

/// <summary>The last quality and subtitle language chosen on the phone; null means full quality and no subtitles.</summary>
public sealed record PlaybackPreferences(int? MaxHeight, string? SubtitleLanguage);

public sealed class SqlitePreferencesStore(DatabaseService db)
{
    public async Task<PlaybackPreferences> GetAsync()
    {
        using var connection = db.GetConnection();
        // SQLite integers arrive as long, which Dapper won't narrow into the record's constructor.
        var row = await connection.QuerySingleOrDefaultAsync<(long? MaxHeight, string? Language)>(
            "SELECT max_height, subtitle_language FROM preferences WHERE id = 1;"
        );
        return new PlaybackPreferences((int?)row.MaxHeight, row.Language);
    }

    public async Task SaveAsync(PlaybackPreferences preferences)
    {
        using var connection = db.GetConnection();
        await connection.ExecuteAsync(
            """
            INSERT OR REPLACE INTO preferences (id, max_height, subtitle_language)
            VALUES (1, @MaxHeight, @SubtitleLanguage);
            """,
            preferences
        );
    }
}
