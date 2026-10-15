namespace Reel.Server.Common;

internal static class EndpointErrors
{
    public static readonly ErrorResponse MediaNotFound = new(
        "media_not_found",
        "Playable media was not found."
    );

    // Audio has no frames to preview, and ffmpeg may fail or run out of time on a file.
    public static readonly ErrorResponse TrickplayUnavailable = new(
        "trickplay_unavailable",
        "Trickplay previews are not available for this file."
    );
}
