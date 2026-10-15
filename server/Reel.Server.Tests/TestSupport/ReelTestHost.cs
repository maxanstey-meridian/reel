using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Reel.Server.Library;
using Reel.Server.Pairing;

namespace Reel.Server.Tests;

internal static class ReelTestHost
{
    public const string SecretKey = "REEL-TEST";

    public static WebApplicationFactory<Program> WithReelSettings(
        this WebApplicationFactory<Program> factory,
        string mediaRoot,
        string secretKey = SecretKey,
        Action<IWebHostBuilder>? configure = null
    ) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reel:MediaRoot", mediaRoot);
            builder.UseSetting("Reel:SecretKey", secretKey);
            builder.UseSetting("Reel:Port", "8080");
            builder.UseSetting("Reel:WebUrl", "http://127.0.0.1:3000");
            builder.ConfigureTestServices(services =>
            {
                // Tests must never advertise themselves to TVs on the network.
                services.Remove(
                    services.Single(service => service.ImplementationType == typeof(MdnsAdvertiser))
                );
                services.AddSingleton<IStartupFilter, LoopbackPeer>();
            });
            configure?.Invoke(builder);
        });

    /// <summary>Starts the host if needed and waits for the background startup reconcile to finish.</summary>
    public static Task WaitForStartupIndexAsync(this WebApplicationFactory<Program> factory) =>
        factory
            .Services.GetServices<IHostedService>()
            .OfType<StartupLibraryIndex>()
            .Single()
            .ExecuteTask!;

    /// <summary>The JSON options the app itself uses for request and response bodies.</summary>
    public static JsonSerializerOptions JsonOptions(this WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

    public static HttpClient CreateAuthenticatedClient(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Secret-Key", SecretKey);
        return client;
    }

    // The test server has no network peer, which PrivateNetworkMiddleware refuses; act as a client on this machine.
    private sealed class LoopbackPeer : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(
                    (context, nextMiddleware) =>
                    {
                        context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                        return nextMiddleware(context);
                    }
                );
                next(app);
            };
    }
}
