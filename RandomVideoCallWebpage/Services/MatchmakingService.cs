using System.Collections.Concurrent;

namespace RandomVideoCallWebpage.Services;

public class MatchmakingService
{
    private readonly Queue<string> _waitingUsers = new();
    private readonly ConcurrentDictionary<string, string> _activeChats = new();

    public string? FindPartner(string connectionId)
    {
        lock (_waitingUsers)
        {
            if (_activeChats.ContainsKey(connectionId))
            {
                return null;
            }

            RemoveFromWaitingQueue(connectionId);

            if (_waitingUsers.Count > 0)
            {
                var partner = _waitingUsers.Dequeue();

                _activeChats[connectionId] = partner;
                _activeChats[partner] = connectionId;

                return partner;
            }

            _waitingUsers.Enqueue(connectionId);
            return null;
        }
    }

    public string? GetPartner(string connectionId)
    {
        return _activeChats.TryGetValue(connectionId, out var partner)
            ? partner
            : null;
    }

    public int GetWaitingCount()
    {
        lock (_waitingUsers)
        {
            return _waitingUsers.Count;
        }
    }

    public void EndChat(string connectionId)
    {
        lock (_waitingUsers)
        {
            RemoveFromWaitingQueue(connectionId);

            if (_activeChats.TryRemove(connectionId, out var partner))
            {
                _activeChats.TryRemove(partner, out _);
            }
        }
    }

    public void EnqueueUser(string connectionId)
    {
        lock (_waitingUsers)
        {
            if (_activeChats.ContainsKey(connectionId))
            {
                return;
            }

            RemoveFromWaitingQueue(connectionId);
            _waitingUsers.Enqueue(connectionId);
        }
    }

    public void RemoveUser(string connectionId)
    {
        EndChat(connectionId);
    }

    private void RemoveFromWaitingQueue(string connectionId)
    {
        if (_waitingUsers.Count == 0)
        {
            return;
        }

        var remaining = _waitingUsers
            .Where(id => id != connectionId)
            .ToList();

        _waitingUsers.Clear();

        foreach (var id in remaining)
        {
            _waitingUsers.Enqueue(id);
        }
    }
}
