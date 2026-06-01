using System.Collections.Concurrent;

namespace RandomVideoCallWebpage.Services;

public class FriendCallSession
{
    public int CallId { get; init; }

    public string CallerId { get; init; } = string.Empty;

    public string ReceiverId { get; init; } = string.Empty;

    public string? CallerConnectionId { get; set; }

    public string? ReceiverConnectionId { get; set; }
}

public class FriendCallSessionService
{
    private readonly ConcurrentDictionary<int, FriendCallSession> _sessions = new();

    public void Register(int callId, string callerId, string receiverId, string callerConnectionId)
    {
        _sessions[callId] = new FriendCallSession
        {
            CallId = callId,
            CallerId = callerId,
            ReceiverId = receiverId,
            CallerConnectionId = callerConnectionId
        };
    }

    public FriendCallSession? Get(int callId) =>
        _sessions.TryGetValue(callId, out var session) ? session : null;

    public void SetReceiverConnection(int callId, string connectionId)
    {
        if (_sessions.TryGetValue(callId, out var session))
        {
            session.ReceiverConnectionId = connectionId;
        }
    }

    public void Remove(int callId) => _sessions.TryRemove(callId, out _);

    public string? GetPartnerConnectionId(int callId, string userId)
    {
        if (!_sessions.TryGetValue(callId, out var session))
        {
            return null;
        }

        if (session.CallerId == userId)
        {
            return session.ReceiverConnectionId;
        }

        if (session.ReceiverId == userId)
        {
            return session.CallerConnectionId;
        }

        return null;
    }

    public bool IsInitiator(int callId, string userId)
    {
        var session = Get(callId);
        return session?.CallerId == userId;
    }
}
