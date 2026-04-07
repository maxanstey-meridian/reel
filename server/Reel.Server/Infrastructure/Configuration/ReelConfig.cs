namespace Reel.Server.Infrastructure.Configuration;

public sealed record ReelConfig
{
    public required string MediaRoot { get; init; }
    public required string SecretKey { get; init; }
    public required int Port { get; init; }
    public string? TmdbApiKey { get; init; }

    public string NormalisedMediaRoot =>
        MediaRoot.EndsWith(Path.DirectorySeparatorChar)
            ? MediaRoot
            : MediaRoot + Path.DirectorySeparatorChar;
}
