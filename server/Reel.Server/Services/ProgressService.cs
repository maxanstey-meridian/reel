namespace Reel.Server.Services;

using System.Text.Json;
using Reel.Server.Models;

public sealed class ProgressService
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly ILogger<ProgressService> _logger;
    private readonly Dictionary<string, WatchProgress> _entries;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProgressService(ReelConfig config, ILogger<ProgressService> logger)
    {
        _logger = logger;

        var directory = Path.Combine(config.NormalisedMediaRoot, ".reel");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "progress.json");

        _entries = LoadFromDisk();
    }

    public void UpdateProgress(string relativePath, double positionSeconds, double durationSeconds)
    {
        lock (_lock)
        {
            _entries[relativePath] = new WatchProgress(
                relativePath,
                positionSeconds,
                durationSeconds,
                DateTime.UtcNow
            );
            SaveToDisk();
        }
    }

    public IReadOnlyList<WatchProgress> GetAll()
    {
        lock (_lock)
        {
            return _entries.Values
                .OrderByDescending(e => e.LastWatched)
                .ToList();
        }
    }

    public WatchProgress? Get(string relativePath)
    {
        lock (_lock)
        {
            return _entries.GetValueOrDefault(relativePath);
        }
    }

    private Dictionary<string, WatchProgress> LoadFromDisk()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, WatchProgress>();

        try
        {
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<WatchProgress>>(json, JsonOptions)
                       ?? [];
            return list.ToDictionary(e => e.RelativePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load progress from {Path}, starting fresh", _filePath);
            return new Dictionary<string, WatchProgress>();
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
            _logger.LogError(ex, "Failed to save progress to {Path}", _filePath);
        }
    }
}
