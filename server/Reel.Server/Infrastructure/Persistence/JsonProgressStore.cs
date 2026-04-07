namespace Reel.Server.Infrastructure.Persistence;

using System.Text.Json;
using Reel.Server.Application.Ports;
using Reel.Server.Domain;
using Reel.Server.Services;

public sealed class JsonProgressStore : IProgressStore
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly ILogger<JsonProgressStore> _logger;
    private readonly Dictionary<string, WatchProgress> _entries;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonProgressStore(ReelConfig config, ILogger<JsonProgressStore> logger)
    {
        _logger = logger;

        var directory = Path.Combine(config.NormalisedMediaRoot, ".reel");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "progress.json");

        _entries = LoadFromDisk();
    }

    public Task UpdateAsync(string relativePath, double positionSeconds, double durationSeconds, CancellationToken ct)
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

        return Task.CompletedTask;
    }

    public Task<WatchProgress?> GetAsync(string relativePath, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult(_entries.GetValueOrDefault(relativePath));
        }
    }

    public Task<IReadOnlyList<WatchProgress>> GetAllAsync(CancellationToken ct)
    {
        lock (_lock)
        {
            IReadOnlyList<WatchProgress> result = _entries.Values
                .OrderByDescending(e => e.LastWatched)
                .ToList();
            return Task.FromResult(result);
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
