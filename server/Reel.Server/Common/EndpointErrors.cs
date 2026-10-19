namespace Reel.Server.Common;

internal static class EndpointErrors
{
    public static readonly ErrorResponse MediaNotFound = new(
        "media_not_found",
        "Playable media was not found."
    );

    public static readonly ErrorResponse InvalidMaxHeight = new(
        "invalid_request",
        "Max height must be a positive even number."
    );
}
