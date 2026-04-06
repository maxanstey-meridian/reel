using FluentAssertions;
using Reel.Server.Services;

namespace Reel.Server.Tests;

public sealed class ReelConfigTests
{
    [Fact]
    public void NormalisedMediaRoot_AppendsDirectorySeparator_WhenMissing()
    {
        var config = new ReelConfig
        {
            MediaRoot = "/Users/test/Media",
            SecretKey = "REEL-TEST",
            Port = 8080
        };

        config.NormalisedMediaRoot.Should().EndWith(Path.DirectorySeparatorChar.ToString());
        config.NormalisedMediaRoot.Should().Be("/Users/test/Media/");
    }

    [Fact]
    public void NormalisedMediaRoot_DoesNotDoubleAppend_WhenAlreadyPresent()
    {
        var config = new ReelConfig
        {
            MediaRoot = "/Users/test/Media/",
            SecretKey = "REEL-TEST",
            Port = 8080
        };

        config.NormalisedMediaRoot.Should().Be("/Users/test/Media/");
    }

    [Fact]
    public void RequiredProperties_AreSet()
    {
        var config = new ReelConfig
        {
            MediaRoot = "/tmp/media",
            SecretKey = "REEL-ABC",
            Port = 9090
        };

        config.MediaRoot.Should().Be("/tmp/media");
        config.SecretKey.Should().Be("REEL-ABC");
        config.Port.Should().Be(9090);
    }
}
