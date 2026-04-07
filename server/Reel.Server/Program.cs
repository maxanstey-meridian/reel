using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reel.Server.Endpoints;
using Reel.Server.Middleware;
using Reel.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var reelConfig = builder.Configuration
    .GetSection("Reel")
    .Get<ReelConfig>()
    ?? throw new InvalidOperationException(
        "Reel config section is missing from appsettings.json.");

if (string.IsNullOrWhiteSpace(reelConfig.MediaRoot))
    throw new InvalidOperationException("Reel:MediaRoot is required in appsettings.json.");
if (string.IsNullOrWhiteSpace(reelConfig.SecretKey))
    throw new InvalidOperationException("Reel:SecretKey is required in appsettings.json.");
if (reelConfig.Port <= 0)
    throw new InvalidOperationException("Reel:Port must be a positive integer in appsettings.json.");

builder.Services.AddSingleton(reelConfig);
builder.Services.AddSingleton<LibraryService>();
builder.Services.AddSingleton<CastService>();
builder.Services.AddCors();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors(policy => policy
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
}

app.UseMiddleware<SecretKeyMiddleware>();

app.MapHealthEndpoints();
app.MapConnectEndpoints();
app.MapLibraryEndpoints();
app.MapStreamEndpoints();
app.MapCastEndpoints();

app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{reelConfig.Port}");

var localIp = GetLocalIpAddress() ?? "unknown (check network connection)";

Console.WriteLine();
Console.WriteLine("  Reel is running");
Console.WriteLine($"  Address:    http://{localIp}:{reelConfig.Port}");
Console.WriteLine($"  Secret key: {reelConfig.SecretKey}");
Console.WriteLine();

app.Run();

static string? GetLocalIpAddress()
{
    return NetworkInterface
        .GetAllNetworkInterfaces()
        .Where(n =>
            n.OperationalStatus == OperationalStatus.Up &&
            n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
            n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
        .Where(a =>
            a.Address.AddressFamily == AddressFamily.InterNetwork &&
            !IPAddress.IsLoopback(a.Address))
        .Select(a => a.Address.ToString())
        .FirstOrDefault();
}

public partial class Program { }
