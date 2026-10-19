using System.ComponentModel.DataAnnotations;

namespace Reel.Server;

/// <summary>
/// The "Reel" settings section, read once at startup (the port, mDNS key and media index are bound to it), so
/// editing it needs a restart. Playback quality is not here: the phone chooses it and the server remembers it.
/// </summary>
public sealed class ReelOptions
{
    public const string Section = "Reel";

    [Required]
    public string MediaRoot { get; set; } = "";

    [Required]
    public string SecretKey { get; set; } = "";

    [Range(1, 65535)]
    public int Port { get; set; } = 8080;

    /// <summary>The phone app's origin; pairing links redirect there.</summary>
    [Required, Url]
    public string WebUrl { get; set; } = "";
}
