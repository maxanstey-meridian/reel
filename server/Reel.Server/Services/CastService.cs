namespace Reel.Server.Services;

using Reel.Server.Models;

public sealed class CastService
{
    private readonly object _lock = new();
    private CastCommand? _pendingCommand;

    public void SetPlay(string relativePath)
    {
        lock (_lock)
        {
            _pendingCommand = new CastCommand("play", relativePath);
        }
    }

    public void SetStop()
    {
        lock (_lock)
        {
            _pendingCommand = new CastCommand("stop", null);
        }
    }

    public CastCommand? GetPending()
    {
        lock (_lock)
        {
            return _pendingCommand;
        }
    }

    public void Acknowledge()
    {
        lock (_lock)
        {
            _pendingCommand = null;
        }
    }
}
