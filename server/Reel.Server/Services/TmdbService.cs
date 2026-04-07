namespace Reel.Server.Services;

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reel.Server.Models;

public sealed class TmdbService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _cacheDirectory;
    private readonly ReelConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TmdbService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public TmdbService(
        ReelConfig config,
        ILogger<TmdbService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cacheDirectory = Path.Combine(Path.GetTempPath(), "reel-tmdb-cache");
    }

    public async Task<TmdbResult?> Search(string title, int? year)
    {
        if (string.IsNullOrWhiteSpace(_config.TmdbApiKey))
            return null;

        var cacheKey = ComputeCacheKey(title, year);
        var cachePath = Path.Combine(_cacheDirectory, $"{cacheKey}.json");

        if (TryReadCache(cachePath, out var cached))
            return cached;

        Directory.CreateDirectory(_cacheDirectory);

        var fileLock = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (TryReadCache(cachePath, out cached))
                return cached;

            return await FetchAndCache(title, year, cachePath);
        }
        finally
        {
            fileLock.Release();
        }
    }

    internal static string ComputeCacheKey(string title, int? year)
    {
        var input = $"{title}|{year}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private bool TryReadCache(string cachePath, out TmdbResult? result)
    {
        result = null;

        if (!File.Exists(cachePath))
            return false;

        var lastWrite = File.GetLastWriteTimeUtc(cachePath);
        if (DateTime.UtcNow - lastWrite > CacheTtl)
            return false;

        try
        {
            var json = File.ReadAllText(cachePath);
            result = JsonSerializer.Deserialize<TmdbResult>(json, JsonOptions);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read TMDB cache file {Path}", cachePath);
            return false;
        }
    }

    private async Task<TmdbResult?> FetchAndCache(string title, int? year, string cachePath)
    {
        try
        {
            var result = await FetchFromApi(title, year, retryOnRateLimit: true);

            if (result is not null)
            {
                var json = JsonSerializer.Serialize(result, JsonOptions);
                await File.WriteAllTextAsync(cachePath, json);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TMDB search failed for '{Title}' ({Year})", title, year);
            return null;
        }
    }

    private async Task<TmdbResult?> FetchFromApi(string title, int? year, bool retryOnRateLimit)
    {
        var client = _httpClientFactory.CreateClient();

        var url = BuildSearchUrl(title, year);
        var response = await client.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            if (!retryOnRateLimit)
            {
                _logger.LogWarning("TMDB rate limit hit, no retry remaining");
                return null;
            }

            var retryAfter = response.Headers.RetryAfter?.Delta
                ?? TimeSpan.FromSeconds(2);

            _logger.LogWarning("TMDB rate limit hit, retrying after {Delay}", retryAfter);
            await Task.Delay(retryAfter);

            return await FetchFromApi(title, year, retryOnRateLimit: false);
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var searchResponse = JsonSerializer.Deserialize<TmdbSearchResponse>(body, JsonOptions);

        if (searchResponse?.Results is not { Count: > 0 })
            return null;

        var first = searchResponse.Results[0];

        return new TmdbResult(
            TmdbId: first.Id,
            PosterPath: first.PosterPath,
            BackdropPath: first.BackdropPath,
            Overview: first.Overview,
            ReleaseDate: first.ReleaseDate
        );
    }

    private string BuildSearchUrl(string title, int? year)
    {
        var encodedTitle = Uri.EscapeDataString(title);
        var url = $"https://api.themoviedb.org/3/search/movie?api_key={_config.TmdbApiKey}&query={encodedTitle}";

        if (year.HasValue)
            url += $"&year={year.Value.ToString(CultureInfo.InvariantCulture)}";

        return url;
    }

    private sealed record TmdbSearchResponse(
        [property: JsonPropertyName("results")] List<TmdbSearchResult> Results
    );

    private sealed record TmdbSearchResult(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("poster_path")] string? PosterPath,
        [property: JsonPropertyName("backdrop_path")] string? BackdropPath,
        [property: JsonPropertyName("overview")] string? Overview,
        [property: JsonPropertyName("release_date")] string? ReleaseDate
    );
}
