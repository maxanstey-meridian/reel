namespace Reel.Server.Infrastructure.Persistence;

using Reel.Server.Application.Ports;
using Reel.Server.Domain;

public sealed class SqliteUserStore : IUserStore
{
    private readonly DatabaseService _db;

    public SqliteUserStore(DatabaseService db)
    {
        _db = db;
    }

    public async Task<User> CreateAsync(string name, string? icon, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users (name, icon) VALUES (@name, @icon);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@icon", (object?)icon ?? DBNull.Value);

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return new User(id, name, icon);
    }

    public async Task<User?> GetAsync(int id, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, icon FROM users WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return ReadUser(reader);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, icon FROM users ORDER BY id;";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var users = new List<User>();
        while (await reader.ReadAsync(ct))
            users.Add(ReadUser(reader));

        return users;
    }

    public async Task<User?> UpdateAsync(int id, string? name, string? icon, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        var setClauses = new List<string>();
        var parameters = new List<(string Name, string Value)>();

        if (name is not null)
        {
            setClauses.Add("name = @name");
            parameters.Add(("@name", name));
        }

        if (icon is not null)
        {
            setClauses.Add("icon = @icon");
            parameters.Add(("@icon", icon));
        }

        if (setClauses.Count == 0)
            return await GetAsync(id, ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE users SET {string.Join(", ", setClauses)} WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        foreach (var (paramName, value) in parameters)
            cmd.Parameters.AddWithValue(paramName, value);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
            return null;

        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM users WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken ct)
    {
        using var conn = _db.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM users WHERE id = @id);";
        cmd.Parameters.AddWithValue("@id", id);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
    }

    private static User ReadUser(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new User(reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }
}
