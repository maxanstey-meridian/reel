using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Reel.Server.Web;

namespace Reel.Server.Tests;

public sealed class PrivateNetworkMiddlewareTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("192.168.1.20")]
    [InlineData("10.1.2.3")]
    [InlineData("172.31.255.255")]
    [InlineData("::ffff:192.168.1.20")]
    [InlineData("fe80::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.254")]
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

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("172.32.0.1")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("2001:db8::1")]
    public async Task PublicAddress_ReturnsForbidden(string address)
    {
        var middleware = new PrivateNetworkMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
