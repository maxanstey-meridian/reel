namespace Reel.Server.Services;

using System.Text.Json;
using Reel.Server.Models;

public sealed class FavouritesService
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly ILogger<FavouritesService> _logger;
    private readonly Dictionary<string, Favourite> _entries;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FavouritesService(ReelConfig config, ILogger<FavouritesService> logger)
    {
        _logger = logger;

        var directory = Path.Combine(config.NormalisedMediaRoot, ".reel");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "favourites.json");

        _entries = LoadFromDisk();
    }

    public void Add(string relativePath)
    {
        lock (_lock)
        {
            if (_entries.ContainsKey(relativePath))
                return;

            _entries[relativePath] = new Favourite(relativePath, DateTime.UtcNow);
            SaveToDisk();
        }
    }

    public void Remove(string relativePath)
    {
        lock (_lock)
        {
            if (!_entries.Remove(relativePath))
                return;

            SaveToDisk();
        }
    }

    public IReadOnlyList<Favourite> GetAll()
    {
        lock (_lock)
        {
            return _entries.Values
                .OrderByDescending(e => e.AddedAt)
                .ToList();
        }
    }

    public bool IsFavourite(string relativePath)
    {
        lock (_lock)
        {
            return _entries.ContainsKey(relativePath);
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
