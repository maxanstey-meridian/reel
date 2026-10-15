using System.Globalization;
using CliWrap;
using Microsoft.Extensions.Options;

namespace Reel.Server.Pairing;

/// <summary>
/// Advertises the server and its key over Bonjour so the TV can find and pair with it. macOS's own dns-sd keeps the
/// registration for as long as it runs; stopping the server kills it, which withdraws the advertisement.
/// </summary>
public sealed class MdnsAdvertiser(
    IHostApplicationLifetime lifetime,
    IOptions<ReelOptions> options,
    ILogger<MdnsAdvertiser> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reel = options.Value;
        try
        {
            // A TV that discovers the server should be able to connect straight away.
            var started = new TaskCompletionSource();
            using (lifetime.ApplicationStarted.Register(started.SetResult))
            {
                await started.Task.WaitAsync(stoppingToken);
            }

            logger.LogInformation(
                "Reel is listening on port {Port} and advertising itself over mDNS",
                reel.Port
            );
            var result = await Cli.Wrap("dns-sd")
                .WithArguments([
                    "-R",
                    "Reel",
                    "_reel._tcp",
                    "local",
                    reel.Port.ToString(CultureInfo.InvariantCulture),
                    $"key={reel.SecretKey}",
                ])
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(stoppingToken);
            logger.LogError(
                "dns-sd exited with code {ExitCode}; the TV cannot discover Reel until the server restarts",
                result.ExitCode
            );
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // An escaping exception would stop the whole server; pairing by QR code still works without discovery.
            logger.LogError(
                ex,
                "Could not advertise Reel over mDNS; the TV cannot discover it automatically"
            );
        }
    }
}
