using System.Text.Json;
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
            // Tests must never advertise themselves to TVs on the network.
            builder.ConfigureTestServices(services =>
                services.Remove(
                    services.Single(service => service.ImplementationType == typeof(MdnsAdvertiser))
                )
            );
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
}
