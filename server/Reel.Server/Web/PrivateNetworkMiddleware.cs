using System.Net;
using Reel.Server.Common;

namespace Reel.Server.Web;

public sealed class PrivateNetworkMiddleware(RequestDelegate next)
{
    // Loopback, private and link-local ranges, plus CGNAT, from which Tailscale assigns tailnet addresses.
    private static readonly IPNetwork[] AllowedNetworks =
    [
        .. new[]
        {
            "127.0.0.0/8",
            "10.0.0.0/8",
            "172.16.0.0/12",
            "192.168.0.0/16",
            "169.254.0.0/16",
            "100.64.0.0/10",
            "::1/128",
            "fe80::/10",
            "fc00::/7",
        }.Select(IPNetwork.Parse),
    ];

    public Task InvokeAsync(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote?.IsIPv4MappedToIPv6 == true)
        {
            remote = remote.MapToIPv4();
        }

        if (remote is null || AllowedNetworks.Any(network => network.Contains(remote)))
        {
            return next(context);
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(
            new ErrorResponse(
                "not_private_network",
                "Reel only answers devices on your local network."
            )
        );
    }
}
