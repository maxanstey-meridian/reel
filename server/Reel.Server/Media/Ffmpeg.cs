using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.Exceptions;

namespace Reel.Server.Media;

internal static class Ffmpeg
{
    private static readonly JsonSerializerOptions ProbeJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Format and selected streams of a file, or null when ffprobe cannot read it.</summary>
    public static async Task<Probe?> ProbeAsync(
        string path,
        string streams,
        ILogger logger,
        CancellationToken ct
    )
    {
        try
        {
            var result = await Cli.Wrap("ffprobe")
                .WithArguments([
                    "-v",
                    "error",
                    "-of",
                    "json",
                    "-show_format",
                    "-show_streams",
                    "-select_streams",
                    streams,
                    path,
                ])
                .ExecuteBufferedAsync(ct);
            return JsonSerializer.Deserialize<Probe>(result.StandardOutput, ProbeJson);
        }
        catch (CommandExecutionException ex)
        {
            logger.LogWarning("ffprobe could not read {Path}: {Error}", path, ex.Message);
            return null;
        }
    }

    // ffmpeg writes a temporary file that is moved into place only once complete, so a cache entry is never partial.
    public static async Task<bool> TryWriteAsync(
        string output,
        IEnumerable<string> arguments,
        ILogger logger,
        CancellationToken ct
    )
    {
        var temporary = $"{output}.{Guid.NewGuid():N}.tmp";
        try
        {
            await Cli.Wrap("ffmpeg")
                .WithArguments(["-nostdin", "-v", "error", .. arguments, "-y", temporary])
                .ExecuteBufferedAsync(ct);
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
            {
                return false;
            }

            File.Move(temporary, output, overwrite: true);
            return true;
        }
        catch (CommandExecutionException ex)
        {
            logger.LogWarning("ffmpeg could not write {Output}: {Error}", output, ex.Message);
            return false;
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}

internal sealed record Probe(ProbeFormat? Format, ProbeStream[]? Streams);

internal sealed record ProbeFormat(double? Duration);

internal sealed record ProbeStream(
    string? CodecName,
    int? Height,
    Dictionary<string, string>? Tags
);
