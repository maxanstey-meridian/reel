namespace Reel.Server.Application;

using Reel.Server.Domain;

public sealed class CastService
{
    private readonly object _lock = new();
    private readonly List<CastCommand> _queue = new();

    public void SetPlay(string relativePath, double? positionSeconds = null)
    {
        lock (_lock)
        {
            _queue.Clear();
            _queue.Add(new CastCommand(CastCommandType.Play, relativePath, positionSeconds));
        }
    }

    public void SetPlayQueue(string[] paths, double? firstItemPosition = null)
    {
        lock (_lock)
        {
            _queue.Clear();
            for (var i = 0; i < paths.Length; i++)
            {
                _queue.Add(new CastCommand(CastCommandType.Play, paths[i], i == 0 ? firstItemPosition : null));
            }
        }
    }

    public void SetStop()
    {
        lock (_lock)
        {
            _queue.Clear();
            _queue.Add(new CastCommand(CastCommandType.Stop, null));
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
