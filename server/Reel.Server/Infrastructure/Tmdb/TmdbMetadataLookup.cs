namespace Reel.Server.Infrastructure.Tmdb;

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reel.Server.Application.Models;
using Reel.Server.Application.Ports;
using Reel.Server.Infrastructure.Configuration;

public sealed class TmdbMetadataLookup : IMetadataLookup
{
    private const string PosterBaseUrl = "https://image.tmdb.org/t/p/w342";
    private const string BackdropBaseUrl = "https://image.tmdb.org/t/p/w780";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _cacheDirectory;
    private readonly ReelConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TmdbMetadataLookup> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public TmdbMetadataLookup(
        ReelConfig config,
        ILogger<TmdbMetadataLookup> logger,
        IHttpClientFactory httpClientFactory
    )
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cacheDirectory = Path.Combine(Path.GetTempPath(), "reel-tmdb-cache");
    }

    public async Task<MetadataResponse?> LookupAsync(string title, int? year, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config.TmdbApiKey))
            return null;

        var cacheKey = ComputeCacheKey(title, year);
        var cachePath = Path.Combine(_cacheDirectory, $"{cacheKey}.json");

        if (TryReadCache(cachePath, out var cached))
            return ToResponse(cached!);

        Directory.CreateDirectory(_cacheDirectory);

        var fileLock = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (TryReadCache(cachePath, out cached))
                return ToResponse(cached!);

            var result = await FetchAndCache(title, year, cachePath, ct);
            return result is not null ? ToResponse(result) : null;
        }
        finally
        {
            fileLock.Release();
            _locks.TryRemove(cacheKey, out _);
        }
    }

    private static string ComputeCacheKey(string title, int? year)
    {
        var input = $"{title}|{year}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static MetadataResponse ToResponse(TmdbResult result) =>
        new(
            TmdbId: result.TmdbId,
            PosterUrl: result.PosterPath is not null ? $"{PosterBaseUrl}{result.PosterPath}" : null,
            BackdropUrl: result.BackdropPath is not null ? $"{BackdropBaseUrl}{result.BackdropPath}" : null,
            Overview: result.Overview,
            ReleaseDate: result.ReleaseDate
        );

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
            return result is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read TMDB cache file {Path}", cachePath);
            return false;
        }
    }

    private async Task<TmdbResult?> FetchAndCache(string title, int? year, string cachePath, CancellationToken ct)
    {
        try
        {
            var result = await FetchFromApi(title, year, retryOnRateLimit: true, ct);

            if (result is not null)
            {
                var json = JsonSerializer.Serialize(result, JsonOptions);
                await File.WriteAllTextAsync(cachePath, json, ct);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TMDB search failed for '{Title}' ({Year})", title, year);
            return null;
        }
    }

    private async Task<TmdbResult?> FetchFromApi(string title, int? year, bool retryOnRateLimit, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();

        var url = BuildSearchUrl(title, year);
        var response = await client.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            if (!retryOnRateLimit)
            {
                _logger.LogWarning("TMDB rate limit hit, no retry remaining");
                return null;
            }

            var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);

            _logger.LogWarning("TMDB rate limit hit, retrying after {Delay}", retryAfter);
            await Task.Delay(retryAfter, ct);

            return await FetchFromApi(title, year, retryOnRateLimit: false, ct);
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
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
}
