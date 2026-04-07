namespace Reel.Server.Infrastructure.Persistence;

using System.Text.Json;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Services;

public sealed class JsonFavouritesStore : IFavouritesStore
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly ILogger<JsonFavouritesStore> _logger;
    private readonly Dictionary<string, Favourite> _entries;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonFavouritesStore(ReelConfig config, ILogger<JsonFavouritesStore> logger)
    {
        _logger = logger;

        var directory = Path.Combine(config.NormalisedMediaRoot, ".reel");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "favourites.json");

        _entries = LoadFromDisk();
    }

    public Task AddAsync(string relativePath, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_entries.ContainsKey(relativePath))
                return Task.CompletedTask;

            _entries[relativePath] = new Favourite(relativePath, DateTime.UtcNow);
            SaveToDisk();
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string relativePath, CancellationToken ct)
    {
        lock (_lock)
        {
            if (!_entries.Remove(relativePath))
                return Task.CompletedTask;

            SaveToDisk();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Favourite>> GetAllAsync(CancellationToken ct)
    {
        lock (_lock)
        {
            IReadOnlyList<Favourite> result = _entries.Values
                .OrderByDescending(e => e.AddedAt)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<bool> IsFavouriteAsync(string relativePath, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult(_entries.ContainsKey(relativePath));
        }
    }

    private Dictionary<string, Favourite> LoadFromDisk()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, Favourite>();

        try
        {
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<Favourite>>(json, JsonOptions)
                       ?? [];
            return list.ToDictionary(e => e.RelativePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load favourites from {Path}, starting fresh", _filePath);
            return new Dictionary<string, Favourite>();
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var json = JsonSerializer.Serialize(_entries.Values.ToList(), JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save favourites to {Path}", _filePath);
        }
    }
}
