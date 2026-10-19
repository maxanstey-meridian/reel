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

    // One column per write, so a quality change and a subtitle change saved together cannot undo each other.
    public Task SaveMaxHeightAsync(int? maxHeight) => UpsertAsync("max_height", maxHeight);

    public Task SaveSubtitleLanguageAsync(string? language) =>
        UpsertAsync("subtitle_language", language);

    private async Task UpsertAsync(string column, object? value)
    {
        using var connection = db.GetConnection();
        await connection.ExecuteAsync(
            $"""
            INSERT INTO preferences (id, {column}) VALUES (1, @value)
            ON CONFLICT (id) DO UPDATE SET {column} = excluded.{column};
            """,
            new { value }
        );
    }
}
