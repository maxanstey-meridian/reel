using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    [InlineData(false, ReelTestHost.SecretKey, "*MediaRoot*")]
    [InlineData(true, "", "*SecretKey*")]
    public void Startup_RejectsInvalidSettings(bool hasMediaRoot, string secretKey, string message)
    {
        var act = () =>
        {
            using var factory = new WebApplicationFactory<Program>().WithReelSettings(
                hasMediaRoot ? _media.Root : "",
                secretKey
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
}
