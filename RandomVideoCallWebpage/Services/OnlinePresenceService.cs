using System.Collections.Concurrent;
using RandomVideoCallWebpage.Models;

namespace RandomVideoCallWebpage.Services;

public class OnlinePresenceService
{
    private readonly ConcurrentDictionary<string, UserPresence> _connections = new();

    public void Register(UserPresence presence) =>
        _connections[presence.ConnectionId] = presence;

    public void Unregister(string connectionId) =>
        _connections.TryRemove(connectionId, out _);

    public int GetLiveCount() => _connections.Count;

    public UserPresence? GetPresence(string connectionId) =>
        _connections.TryGetValue(connectionId, out var presence) ? presence : null;

    public IReadOnlyList<UserPresence> GetAllOnline() =>
        _connections.Values.ToList();

    public IReadOnlyList<string> UnregisterByUserId(string userId)
    {
        var connectionIds = _connections
            .Where(entry => entry.Value.UserId == userId)
            .Select(entry => entry.Key)
            .ToList();

        foreach (var connectionId in connectionIds)
        {
            _connections.TryRemove(connectionId, out _);
        }

        return connectionIds;
    }
}
