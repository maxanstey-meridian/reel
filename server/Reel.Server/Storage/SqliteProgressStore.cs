using Dapper;
using Reel.Server.Contracts.Reel;

namespace Reel.Server.Storage;

public sealed class SqliteProgressStore(DatabaseService db, TimeProvider time)
{
    public async Task UpdateAsync(
        string relativePath,
        double positionSeconds,
        double durationSeconds
    )
    {
        using var connection = db.GetConnection();
        await connection.ExecuteAsync(
            """
            INSERT OR REPLACE INTO progress (relative_path, position_seconds, duration_seconds, last_watched)
            VALUES (@relativePath, @positionSeconds, @durationSeconds, @lastWatched);
            """,
            new
            {
                relativePath,
                positionSeconds,
                durationSeconds,
                lastWatched = time.GetUtcNow().UtcDateTime,
            }
        );
    }

    public async Task<WatchProgress[]> GetAllAsync()
    {
        using var connection = db.GetConnection();
        return
        [
            .. await connection.QueryAsync<WatchProgress>(
                "SELECT * FROM progress ORDER BY last_watched DESC;"
            ),
        ];
    }
}
