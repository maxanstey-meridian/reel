using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Reel.Server.Middleware;

namespace Reel.Server.Tests;

public sealed class PrivateNetworkMiddlewareTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("192.168.1.20")]
    public async Task AllowedAddress_PassesThrough(string address)
    {
        var called = false;
        var middleware = new PrivateNetworkMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task PublicAddress_ReturnsForbidden()
    {
        var middleware = new PrivateNetworkMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("8.8.8.8");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
