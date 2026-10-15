using System.Collections.Concurrent;

namespace Reel.Server.Media;

/// <summary>
/// One run per key at a time: concurrent callers share it, and it keeps going when they give up
/// so the cache it fills is ready for the next caller.
/// </summary>
internal sealed class SingleFlight<T>
{
    private readonly ConcurrentDictionary<string, Lazy<Task<T>>> _running = new();

    public Task<T> RunAsync(string key, Func<Task<T>> work, CancellationToken ct) =>
        _running
            .GetOrAdd(key, _ => new Lazy<Task<T>>(() => RunThenForgetAsync(key, work)))
            .Value.WaitAsync(ct);

    private async Task<T> RunThenForgetAsync(string key, Func<Task<T>> work)
    {
        try
        {
            return await work();
        }
        finally
        {
            _running.TryRemove(key, out _);
        }
    }
}
