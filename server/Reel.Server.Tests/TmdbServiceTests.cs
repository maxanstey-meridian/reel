namespace Reel.Server.Tests;

using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Services;

public sealed class TmdbServiceTests
{
    private const string FakeApiKey = "test-tmdb-key";

    private static readonly string TmdbMovieResponse = """
        {
            "results": [
                {
                    "id": 603,
                    "poster_path": "/f89U3ADr1oiB1s9GkdPOEpXUk5H.jpg",
                    "backdrop_path": "/fNG7i7RqMErkcqhohV2a6cV1Ehy.jpg",
                    "overview": "A computer hacker learns about the true nature of reality.",
                    "release_date": "1999-03-30"
                }
            ]
        }
        """;

    private static readonly string TmdbEmptyResponse = """
        {
            "results": []
        }
        """;

    [Fact]
    public async Task Search_NoApiKey_ReturnsNull()
    {
        var config = CreateConfig(tmdbApiKey: null);
        var handler = new FakeHttpHandler(TmdbMovieResponse);
        var service = CreateService(config, handler);

        var result = await service.Search("The Matrix", 1999);

        result.Should().BeNull();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Search_CachesResult()
    {
        using var cacheDir = new TempCacheDir();
        var config = CreateConfig(tmdbApiKey: FakeApiKey);
        var handler = new FakeHttpHandler(TmdbMovieResponse);
        var service = CreateService(config, handler, cacheDir.Path);

        var first = await service.Search("The Matrix", 1999);
        var second = await service.Search("The Matrix", 1999);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first!.TmdbId.Should().Be(603);
        second!.TmdbId.Should().Be(603);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Search_ExpiredCache_RefetchesFromApi()
    {
        using var cacheDir = new TempCacheDir();
        var config = CreateConfig(tmdbApiKey: FakeApiKey);
        var handler = new FakeHttpHandler(TmdbMovieResponse);
        var service = CreateService(config, handler, cacheDir.Path);

        // First call populates cache
        await service.Search("The Matrix", 1999);
        handler.CallCount.Should().Be(1);

        // Backdate the cache file to 31 days ago
        var cacheKey = TmdbService.ComputeCacheKey("The Matrix", 1999);
        var cachePath = Path.Combine(cacheDir.Path, $"{cacheKey}.json");
        File.SetLastWriteTimeUtc(cachePath, DateTime.UtcNow.AddDays(-31));

        // Second call should re-fetch
        var result = await service.Search("The Matrix", 1999);

        result.Should().NotBeNull();
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Search_NoResults_ReturnsNull()
    {
        using var cacheDir = new TempCacheDir();
        var config = CreateConfig(tmdbApiKey: FakeApiKey);
        var handler = new FakeHttpHandler(TmdbEmptyResponse);
        var service = CreateService(config, handler, cacheDir.Path);

        var result = await service.Search("Nonexistent Movie", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Search_HttpError_ReturnsNull()
    {
        using var cacheDir = new TempCacheDir();
        var config = CreateConfig(tmdbApiKey: FakeApiKey);
        var handler = new FakeHttpHandler(statusCode: HttpStatusCode.InternalServerError);
        var service = CreateService(config, handler, cacheDir.Path);

        var result = await service.Search("The Matrix", 1999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Search_RateLimit_RetriesOnce()
    {
        using var cacheDir = new TempCacheDir();
        var config = CreateConfig(tmdbApiKey: FakeApiKey);
        var handler = new RateLimitThenSuccessHandler(TmdbMovieResponse);
        var service = CreateService(config, handler, cacheDir.Path);

        var result = await service.Search("The Matrix", 1999);

        result.Should().NotBeNull();
        result!.TmdbId.Should().Be(603);
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Search_ReturnsCorrectFields()
    {
        using var cacheDir = new TempCacheDir();
        var config = CreateConfig(tmdbApiKey: FakeApiKey);
        var handler = new FakeHttpHandler(TmdbMovieResponse);
        var service = CreateService(config, handler, cacheDir.Path);

        var result = await service.Search("The Matrix", 1999);

        result.Should().NotBeNull();
        result!.TmdbId.Should().Be(603);
        result.PosterPath.Should().Be("/f89U3ADr1oiB1s9GkdPOEpXUk5H.jpg");
        result.BackdropPath.Should().Be("/fNG7i7RqMErkcqhohV2a6cV1Ehy.jpg");
        result.Overview.Should().Contain("computer hacker");
        result.ReleaseDate.Should().Be("1999-03-30");
    }

    [Fact]
    public void CacheKey_IsDeterministic()
    {
        var key1 = TmdbService.ComputeCacheKey("The Matrix", 1999);
        var key2 = TmdbService.ComputeCacheKey("The Matrix", 1999);

        key1.Should().Be(key2);
    }

    [Fact]
    public void CacheKey_DiffersForDifferentInputs()
    {
        var key1 = TmdbService.ComputeCacheKey("The Matrix", 1999);
        var key2 = TmdbService.ComputeCacheKey("Inception", 2010);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void CacheKey_DiffersWithAndWithoutYear()
    {
        var key1 = TmdbService.ComputeCacheKey("The Matrix", 1999);
        var key2 = TmdbService.ComputeCacheKey("The Matrix", null);

        key1.Should().NotBe(key2);
    }

    private static ReelConfig CreateConfig(string? tmdbApiKey) =>
        new()
        {
            MediaRoot = "/tmp/reel-test-media",
            SecretKey = "test",
            Port = 8080,
            TmdbApiKey = tmdbApiKey,
        };

    private static TmdbService CreateService(
        ReelConfig config,
        HttpMessageHandler handler,
        string? cacheDir = null)
    {
        var httpClient = new HttpClient(handler);
        var factory = new SingleClientFactory(httpClient);
        var service = new TmdbService(config, NullLogger<TmdbService>.Instance, factory);

        if (cacheDir is not null)
        {
            // Override cache directory via reflection to use isolated temp dir
            var field = typeof(TmdbService).GetField("_cacheDirectory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            field.SetValue(service, cacheDir);
        }

        return service;
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string? _responseBody;
        private readonly HttpStatusCode _statusCode;

        public int CallCount { get; private set; }

        public FakeHttpHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        public FakeHttpHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(_statusCode);
            if (_responseBody is not null)
                response.Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }

    private sealed class RateLimitThenSuccessHandler : HttpMessageHandler
    {
        private readonly string _successBody;
        private int _callCount;

        public int CallCount => _callCount;

        public RateLimitThenSuccessHandler(string successBody) => _successBody = successBody;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);

            if (_callCount == 1)
            {
                var rateLimitResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                rateLimitResponse.Headers.RetryAfter =
                    new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(50));
                return Task.FromResult(rateLimitResponse);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_successBody, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class TempCacheDir : IDisposable
    {
        public string Path { get; }

        public TempCacheDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"reel-tmdb-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
