using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Reel.Server.Contracts.Reel;
using Reel.Server.Pairing;

namespace Reel.Server.Tests;

public sealed class ServerIntegrationTests : IDisposable
{
    private readonly TempMediaRoot _media = new();

    public void Dispose() => _media.Dispose();

    [Fact]
    public void TestHost_DoesNotAdvertiseOverMdns()
    {
        using var factory = _media.CreateFactory();

        factory
            .Services.GetServices<IHostedService>()
            .Should()
            .NotContain(service => service is MdnsAdvertiser);
    }

    [Theory]
    [InlineData(false, ReelTestHost.SecretKey, "off", "*MediaRoot*")]
    [InlineData(true, "", "off", "*SecretKey*")]
    [InlineData(true, ReelTestHost.SecretKey, "abc", "*Compression*")]
    [InlineData(true, ReelTestHost.SecretKey, "0p", "*Compression*")]
    [InlineData(true, ReelTestHost.SecretKey, "481p", "*Compression*")]
    public void Startup_RejectsInvalidSettings(
        bool hasMediaRoot,
        string secretKey,
        string compression,
        string message
    )
    {
        var act = () =>
        {
            using var factory = new WebApplicationFactory<Program>().WithReelSettings(
                hasMediaRoot ? _media.Root : "",
                secretKey,
                builder => builder.UseSetting("Reel:Compression", compression)
            );
            _ = factory.Services;
        };

        act.Should().Throw<OptionsValidationException>().WithMessage(message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("studio.local:8080")]
    public void Startup_RejectsMissingOrInvalidWebUrl(string webUrl)
    {
        var act = () =>
        {
            using var factory = _media.CreateFactory(builder =>
                builder.UseSetting("Reel:WebUrl", webUrl)
            );
            _ = factory.Services;
        };

        act.Should().Throw<OptionsValidationException>().WithMessage("*WebUrl*");
    }

    [Fact]
    public async Task Compression_EditedInTheSettingsFile_AppliesToTheNextStream()
    {
        _media.Write("Films/a.mp4");
        var settings = Path.Combine(_media.Root, "settings.json");
        await File.WriteAllTextAsync(settings, """{ "Reel": { "Compression": "off" } }""");
        using var factory = _media.CreateFactory(builder =>
            builder.ConfigureAppConfiguration(config =>
                config.AddJsonFile(settings, optional: false, reloadOnChange: true)
            )
        );
        using var client = factory.CreateAuthenticatedClient();
        async Task<bool> Transcoded() =>
            (
                await client.GetFromJsonAsync<StreamInfoResponse>(
                    "/api/files/stream/info?path=Films/a.mp4"
                )
            )!.TranscodeHeight
                is not null;
        (await Transcoded()).Should().BeFalse();

        await File.WriteAllTextAsync(settings, """{ "Reel": { "Compression": "720p" } }""");

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!await Transcoded() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        (await Transcoded()).Should().BeTrue();
    }
}
