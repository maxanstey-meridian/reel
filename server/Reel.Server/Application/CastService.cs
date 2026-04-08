namespace Reel.Server.Application;

using Reel.Server.Domain;

public sealed class CastService
{
    private readonly object _lock = new();
    private readonly List<CastCommand> _queue = new();

    public void SetPlay(string relativePath)
    {
        lock (_lock)
        {
            _queue.Clear();
            _queue.Add(new CastCommand("play", relativePath));
        }
    }

    public void SetPlayQueue(string[] paths)
    {
        lock (_lock)
        {
            _queue.Clear();
            foreach (var path in paths)
            {
                _queue.Add(new CastCommand("play", path));
            }
        }
    }

    public void SetStop()
    {
        lock (_lock)
        {
            _queue.Clear();
            _queue.Add(new CastCommand("stop", null));
        }
    }

    public CastCommand? GetPending()
    {
        lock (_lock)
        {
            return _queue.Count > 0 ? _queue[0] : null;
        }
    }

    public void Acknowledge()
    {
        lock (_lock)
        {
            if (_queue.Count > 0)
            {
                _queue.RemoveAt(0);
            }
        }
    }

    public string[] GetQueue()
    {
        lock (_lock)
        {
            return _queue
                .Skip(1)
                .Where(c => c.RelativePath != null)
                .Select(c => c.RelativePath!)
                .ToArray();
        }
    }
}
