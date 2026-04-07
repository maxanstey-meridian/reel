namespace Reel.Server.Tests;

using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reel.Server.Infrastructure.Configuration;
using Reel.Server.Infrastructure.Tmdb;

public sealed class TmdbMetadataLookupTests
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

    private static readonly string TmdbNullPosterResponse = """
        {
            "results": [
                {
                    "id": 999,
                    "poster_path": null,
                    "backdrop_path": null,
                    "overview": "Some movie without images.",
                    "release_date": "2020-01-01"
                }
            ]
        }
        """;

    [Fact]
    public async Task LookupAsync_ReturnsResolvedPosterUrl()
    {
        using var cacheDir = new TempCacheDir();
        var lookup = CreateLookup(FakeApiKey, new FakeHttpHandler(TmdbMovieResponse), cacheDir.Path);

        var result = await lookup.LookupAsync("The Matrix", 1999, CancellationToken.None);

        result.Should().NotBeNull();
        result!.PosterUrl.Should().Be("https://image.tmdb.org/t/p/w342/f89U3ADr1oiB1s9GkdPOEpXUk5H.jpg");
    }

    [Fact]
    public async Task LookupAsync_ReturnsResolvedBackdropUrl()
    {
        using var cacheDir = new TempCacheDir();
        var lookup = CreateLookup(FakeApiKey, new FakeHttpHandler(TmdbMovieResponse), cacheDir.Path);

        var result = await lookup.LookupAsync("The Matrix", 1999, CancellationToken.None);

        result.Should().NotBeNull();
        result!.BackdropUrl.Should().Be("https://image.tmdb.org/t/p/w780/fNG7i7RqMErkcqhohV2a6cV1Ehy.jpg");
    }

    [Fact]
    public async Task LookupAsync_NullPosterPath_ReturnsNullPosterUrl()
    {
        using var cacheDir = new TempCacheDir();
        var lookup = CreateLookup(FakeApiKey, new FakeHttpHandler(TmdbNullPosterResponse), cacheDir.Path);

        var result = await lookup.LookupAsync("Unknown", null, CancellationToken.None);

        result.Should().NotBeNull();
        result!.PosterUrl.Should().BeNull();
        result.BackdropUrl.Should().BeNull();
    }

    [Fact]
    public async Task LookupAsync_PassesThroughAllFields()
    {
        using var cacheDir = new TempCacheDir();
        var lookup = CreateLookup(FakeApiKey, new FakeHttpHandler(TmdbMovieResponse), cacheDir.Path);

        var result = await lookup.LookupAsync("The Matrix", 1999, CancellationToken.None);

        result.Should().NotBeNull();
        result!.TmdbId.Should().Be(603);
        result.Overview.Should().Contain("computer hacker");
        result.ReleaseDate.Should().Be("1999-03-30");
    }

    [Fact]
    public async Task LookupAsync_NoApiKey_ReturnsNull()
    {
        using var cacheDir = new TempCacheDir();
        var handler = new FakeHttpHandler(TmdbMovieResponse);
        var lookup = CreateLookup(null, handler, cacheDir.Path);

        var result = await lookup.LookupAsync("The Matrix", 1999, CancellationToken.None);

        result.Should().BeNull();
        handler.CallCount.Should().Be(0);
    }

    private static TmdbMetadataLookup CreateLookup(
        string? tmdbApiKey,
        HttpMessageHandler handler,
        string cacheDir)
    {
        var config = new ReelConfig
        {
            MediaRoot = "/tmp/reel-test-media",
            SecretKey = "test",
            Port = 8080,
            TmdbApiKey = tmdbApiKey,
        };

        var httpClient = new HttpClient(handler);
        var factory = new SingleClientFactory(httpClient);
        var lookup = new TmdbMetadataLookup(config, NullLogger<TmdbMetadataLookup>.Instance, factory);

        // Override cache directory via reflection to use isolated temp dir
        var field = typeof(TmdbMetadataLookup).GetField("_cacheDirectory",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(lookup, cacheDir);

        return lookup;
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private int _callCount;

        public int CallCount => _callCount;

        public FakeHttpHandler(string responseBody) => _responseBody = responseBody;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class TempCacheDir : IDisposable
    {
        public string Path { get; }

        public TempCacheDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"reel-tmdb-lookup-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
