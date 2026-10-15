using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reel.Server.Tests;

public sealed class WebAppFallbackTests : IDisposable
{
    private const string Shell = "<html>reel shell</html>";
    private readonly TempMediaRoot _media = new();
    private readonly WebApplicationFactory<Program> _factory;

    public WebAppFallbackTests()
    {
        var webRoot = Path.Combine(_media.Root, "webroot");
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(Path.Combine(webRoot, "index.html"), Shell);
        _factory = _media.CreateFactory(builder =>
            builder.UseSetting(WebHostDefaults.WebRootKey, webRoot)
        );
    }

    public void Dispose()
    {
        _factory.Dispose();
        _media.Dispose();
    }

    [Theory]
    [InlineData("/detail/Movies%2FFilm%20(2013).mp4")]
    [InlineData("/detail/Movies/Film.mkv")]
    [InlineData("/library")]
    [InlineData("/library/TV/Mr.%20Robot")]
    public async Task RefreshingAClientRoute_ServesTheAppShell(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(Shell);
    }

    [Fact]
    public async Task MissingAssetOutsideClientRoutes_Returns404()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/_nuxt/missing.js");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
