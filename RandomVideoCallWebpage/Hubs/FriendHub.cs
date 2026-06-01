using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using RandomVideoCallWebpage.Models;
using RandomVideoCallWebpage.Services;

namespace RandomVideoCallWebpage.Hubs;

[Authorize]
public class FriendHub : Hub
{
    private readonly FriendService _friends;
    private readonly FriendCallSessionService _callSessions;
    private readonly OnlinePresenceService _presence;
    private readonly UserManager<ApplicationUser> _userManager;

    public FriendHub(
        FriendService friends,
        FriendCallSessionService callSessions,
        OnlinePresenceService presence,
        UserManager<ApplicationUser> userManager)
    {
        _friends = friends;
        _callSessions = callSessions;
        _presence = presence;
        _userManager = userManager;
    }

    public override async Task OnConnectedAsync()
    {
        var user = await _userManager.GetUserAsync(Context.User!);
        if (user?.IsBlocked == true)
        {
            Context.Abort();
            return;
        }

        if (user != null)
        {
            await PushSnapshotToUserAsync(user.Id);
            await NotifyMissedCallsAsync(user.Id);
        }

        await base.OnConnectedAsync();
    }

    public async Task SendFriendRequest(string toUserId)
    {
        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            return;
        }

        var (success, error, requestId) = await _friends.SendRequestAsync(userId, toUserId);
        if (!success)
        {
            await Clients.Caller.SendAsync("FriendRequestFailed", error);
            return;
        }

        var sender = await _userManager.FindByIdAsync(userId);
        var payload = new
        {
            requestId,
            fromUserId = userId,
            name = sender?.DisplayName ?? "User",
            country = sender?.Country ?? "",
            countryCode = CountryCodes.GetCode(sender?.Country ?? "")
        };

        foreach (var connectionId in _presence.GetConnectionIdsForUser(toUserId))
        {
            await Clients.Client(connectionId).SendAsync("FriendRequestReceived", payload);
        }

        await Clients.Caller.SendAsync("FriendRequestSent", toUserId);
        await PushSnapshotToCallerAsync();
    }

    public async Task AcceptFriendRequest(int requestId)
    {
        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            return;
        }

        var pending = await _friends.GetRequestAsync(requestId);
        var (success, error) = await _friends.AcceptRequestAsync(requestId, userId);
        if (!success)
        {
            await Clients.Caller.SendAsync("FriendActionFailed", error);
            return;
        }

        if (pending != null)
        {
            foreach (var connectionId in _presence.GetConnectionIdsForUser(pending.FromUserId))
            {
                await Clients.Client(connectionId).SendAsync("FriendRequestAccepted", new
                {
                    requestId,
                    friendUserId = userId,
                    name = (await _userManager.FindByIdAsync(userId))?.DisplayName ?? "Friend"
                });
            }
        }

        await PushSnapshotToCallerAsync();
    }

    public async Task DeclineFriendRequest(int requestId)
    {
        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            return;
        }

        var (success, error) = await _friends.DeclineRequestAsync(requestId, userId);
        if (!success)
        {
            await Clients.Caller.SendAsync("FriendActionFailed", error);
            return;
        }

        await BroadcastRequestResolvedAsync(requestId, accepted: false);
        await PushSnapshotToCallerAsync();
    }

    public async Task GetFriendsSnapshot() => await PushSnapshotToCallerAsync();

    public async Task SendFriendMessage(string friendUserId, string message)
    {
        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            return;
        }

        var saved = await _friends.SaveMessageAsync(userId, friendUserId, message);
        if (saved == null)
        {
            await Clients.Caller.SendAsync("FriendActionFailed", "Could not send message.");
            return;
        }

        var sender = await _userManager.FindByIdAsync(userId);
        var payload = new
        {
            id = saved.Id,
            senderId = userId,
            senderName = sender?.DisplayName ?? "Friend",
            body = saved.Body,
            sentAtUtc = saved.SentAtUtc
        };

        await Clients.Caller.SendAsync("FriendMessageSent", payload);

        foreach (var connectionId in _presence.GetConnectionIdsForUser(friendUserId))
        {
            await Clients.Client(connectionId).SendAsync("FriendMessageReceived", payload);
        }
    }

    public async Task LoadFriendMessages(string friendUserId)
    {
        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            return;
        }

        var messages = await _friends.GetMessagesAsync(userId, friendUserId);
        await _friends.MarkMessagesReadAsync(userId, friendUserId);
        await Clients.Caller.SendAsync("FriendMessagesLoaded", friendUserId, messages);
        await PushSnapshotToCallerAsync();
    }

    public async Task<int> StartFriendCall(string friendUserId)
    {
        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            return 0;
        }

        if (!await _friends.AreFriendsAsync(userId, friendUserId))
        {
            await Clients.Caller.SendAsync("FriendActionFailed", "You can only call friends.");
            return 0;
        }

        var call = await _friends.CreateCallAsync(userId, friendUserId);
        _callSessions.Register(call.Id, userId, friendUserId, Context.ConnectionId);

        var caller = await _userManager.FindByIdAsync(userId);
        var payload = new
        {
            callId = call.Id,
            callerId = userId,
            name = caller?.DisplayName ?? "Friend",
            countryCode = CountryCodes.GetCode(caller?.Country ?? "")
        };

        if (call.Status == FriendCallStatus.Missed)
        {
            await Clients.Caller.SendAsync("FriendCallMissed", payload);
            _callSessions.Remove(call.Id);
            return call.Id;
        }

        await Clients.Caller.SendAsync("FriendCallStarted", new { callId = call.Id, isInitiator = true });

        foreach (var connectionId in _presence.GetConnectionIdsForUser(friendUserId))
        {
            await Clients.Client(connectionId).SendAsync("IncomingFriendCall", payload);
        }

        return call.Id;
    }

    public async Task AcceptFriendCall(int callId)
    {
        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            return;
        }

        var session = _callSessions.Get(callId);
        if (session == null || session.ReceiverId != userId)
        {
            await Clients.Caller.SendAsync("FriendActionFailed", "Call not found.");
            return;
        }

        _callSessions.SetReceiverConnection(callId, Context.ConnectionId);
        await _friends.UpdateCallStatusAsync(callId, FriendCallStatus.Answered);

        var isInitiator = false;
        await Clients.Caller.SendAsync("FriendCallStarted", new { callId, isInitiator });

        if (session.CallerConnectionId != null)
        {
            await Clients.Client(session.CallerConnectionId)
                .SendAsync("FriendCallAccepted", new { callId });
        }
    }

    public async Task DeclineFriendCall(int callId)
    {
        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            return;
        }

        await EndCallAsync(callId, userId, FriendCallStatus.Declined, "FriendCallDeclined");
    }

    public async Task CancelFriendCall(int callId)
    {
        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            return;
        }

        await EndCallAsync(callId, userId, FriendCallStatus.Cancelled, "FriendCallCancelled");
    }

    public async Task EndFriendCall(int callId) =>
        await EndCallAsync(callId, await GetUserIdAsync() ?? "", FriendCallStatus.Cancelled, "FriendCallEnded");

    public async Task SendFriendOffer(int callId, string sdp) =>
        await ForwardCallSignalAsync(callId, "ReceiveFriendOffer", sdp, null, null, null);

    public async Task SendFriendAnswer(int callId, string sdp) =>
        await ForwardCallSignalAsync(callId, "ReceiveFriendAnswer", sdp, null, null, null);

    public async Task SendFriendIceCandidate(int callId, string candidate, string? sdpMid, int? sdpMLineIndex) =>
        await ForwardCallSignalAsync(callId, "ReceiveFriendIceCandidate", candidate, sdpMid, sdpMLineIndex, null);

    private async Task ForwardCallSignalAsync(
        int callId,
        string method,
        string? arg1,
        string? arg2,
        int? arg3,
        object? _)
    {
        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            return;
        }

        var partnerConnection = _callSessions.GetPartnerConnectionId(callId, userId);
        if (partnerConnection == null)
        {
            return;
        }

        if (method == "ReceiveFriendIceCandidate")
        {
            await Clients.Client(partnerConnection).SendAsync(method, callId, arg1, arg2, arg3);
            return;
        }

        await Clients.Client(partnerConnection).SendAsync(method, callId, arg1);
    }

    private async Task EndCallAsync(int callId, string userId, FriendCallStatus status, string clientEvent)
    {
        var session = _callSessions.Get(callId);
        if (session == null)
        {
            return;
        }

        await _friends.UpdateCallStatusAsync(callId, status);
        _callSessions.Remove(callId);

        if (session.CallerConnectionId != null)
        {
            await Clients.Client(session.CallerConnectionId).SendAsync(clientEvent, callId);
        }

        if (session.ReceiverConnectionId != null)
        {
            await Clients.Client(session.ReceiverConnectionId).SendAsync(clientEvent, callId);
        }
    }

    private async Task BroadcastRequestResolvedAsync(int requestId, bool accepted)
    {
        await Clients.All.SendAsync("FriendRequestResolved", new { requestId, accepted });
    }

    private async Task PushSnapshotToCallerAsync()
    {
        var userId = await GetUserIdAsync();
        if (userId != null)
        {
            await PushSnapshotToUserAsync(userId);
        }
    }

    private async Task PushSnapshotToUserAsync(string userId)
    {
        var connections = _presence.GetConnectionIdsForUser(userId);
        if (connections.Count == 0)
        {
            return;
        }

        var friends = await _friends.GetFriendsAsync(userId);
        var requests = await _friends.GetIncomingRequestsAsync(userId);
        var pendingCount = requests.Count;

        foreach (var connectionId in connections)
        {
            await Clients.Client(connectionId).SendAsync("FriendsSnapshot", new
            {
                friends,
                requests,
                pendingCount
            });
        }
    }

    private async Task NotifyMissedCallsAsync(string userId)
    {
        var missed = await _friends.GetMissedCallsAsync(userId);
        if (missed.Count == 0)
        {
            return;
        }

        foreach (var connectionId in _presence.GetConnectionIdsForUser(userId))
        {
            await Clients.Client(connectionId).SendAsync("MissedFriendCalls", missed);
        }
    }

    private async Task<string?> GetUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(Context.User!);
        return user?.Id;
    }
}
