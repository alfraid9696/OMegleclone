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
}
