using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Reel.Server;

/// <summary>
/// The "Reel" settings section. Compression is read per request, so editing it applies to the next stream;
/// the rest is read once at startup (the port, mDNS key and media index are bound to it) and needs a restart.
/// </summary>
public sealed class ReelOptions
{
    public const string Section = "Reel";

    [Required]
    public string MediaRoot { get; set; } = "";

    [Required]
    public string SecretKey { get; set; } = "";

    [Range(1, 65535)]
    public int Port { get; set; }

    /// <summary>The phone app's origin; pairing links redirect there.</summary>
    public string? WebUrl { get; set; }

    /// <summary>"off" streams files as they are; "&lt;height&gt;p" (e.g. "720p") live-transcodes video to that height.</summary>
    public string Compression { get; set; } = "off";

    public int? TranscodeHeight =>
        Compression.EndsWith('p')
        && int.TryParse(
            Compression.AsSpan(0, Compression.Length - 1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var height
        )
        && IsTranscodeHeight(height)
            ? height
            : null;

    public static bool IsTranscodeHeight(int height) => height > 0 && height % 2 == 0;
}
