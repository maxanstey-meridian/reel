namespace Reel.Server.Application.Ports;

public interface ISubtitleExtractor
{
    Task<string?> ExtractAsync(string relativePath, int trackIndex, string outputFormat, CancellationToken ct);
    string ConvertSrtToVtt(string srtContent);
}
