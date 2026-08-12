namespace Reel.Server.Infrastructure.Ffmpeg;

using System.Diagnostics;
using System.Text;

internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken ct)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {startInfo.FileName} process.");
        using var registration = ct.Register(() => Kill(process));
        using var stdout = new MemoryStream();

        try
        {
            var stdoutTask = startInfo.RedirectStandardOutput
                ? process.StandardOutput.BaseStream.CopyToAsync(stdout, ct)
                : Task.CompletedTask;
            var stderrTask = startInfo.RedirectStandardError
                ? process.StandardError.ReadToEndAsync(ct)
                : Task.FromResult(string.Empty);

            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(ct));
            return new ProcessResult(process.ExitCode, stdout.ToArray(), await stderrTask);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and kill.
        }
    }
}

internal sealed record ProcessResult(int ExitCode, byte[] StandardOutput, string StandardError)
{
    public string StandardOutputText => Encoding.UTF8.GetString(StandardOutput);
}
