using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using RandomVideoCallWebpage.Models;
using RandomVideoCallWebpage.Services;

namespace RandomVideoCallWebpage.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly MatchmakingService _matchmaking;
    private readonly OnlinePresenceService _presence;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChatHub(
        MatchmakingService matchmaking,
        OnlinePresenceService presence,
        UserManager<ApplicationUser> userManager)
    {
        _matchmaking = matchmaking;
        _presence = presence;
        _userManager = userManager;
    }

    public override async Task OnConnectedAsync()
    {
        var user = await _userManager.GetUserAsync(Context.User!);

        if (user != null)
        {
            if (user.IsBlocked)
            {
                await Clients.Caller.SendAsync("AccountBlocked");
                Context.Abort();
                return;
            }

            _presence.Register(new UserPresence
            {
                ConnectionId = Context.ConnectionId,
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Country = user.Country,
                CountryCode = CountryCodes.GetCode(user.Country)
            });
        }

        await BroadcastLiveCountAsync();
        await base.OnConnectedAsync();
    }

    public async Task StartChat()
    {
        if (await RejectIfBlockedAsync())
        {
            return;
        }

        var connectionId = Context.ConnectionId;

        if (_matchmaking.GetPartner(connectionId) is string existingPartner)
        {
            _matchmaking.EndChat(connectionId);
            await Clients.Client(existingPartner).SendAsync("PartnerDisconnected");
        }

        var partner = _matchmaking.FindPartner(connectionId);

        if (partner != null)
        {
            await NotifyMatchedAsync(connectionId, partner);
        }
        else
        {
            await Clients.Client(connectionId).SendAsync("WaitingForPartner");
        }

        await BroadcastLiveCountAsync();
    }

    public async Task NextStranger()
    {
        if (await RejectIfBlockedAsync())
        {
            return;
        }

        var connectionId = Context.ConnectionId;
        var partner = _matchmaking.GetPartner(connectionId);

        _matchmaking.EndChat(connectionId);

        if (partner != null)
        {
            await Clients.Client(partner).SendAsync("PartnerDisconnected");
        }

        var newPartner = _matchmaking.FindPartner(connectionId);

        if (newPartner != null)
        {
            await NotifyMatchedAsync(connectionId, newPartner);
        }
        else
        {
            await Clients.Client(connectionId).SendAsync("WaitingForPartner");
        }

        await BroadcastLiveCountAsync();
    }

    public async Task<bool> SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (await RejectIfBlockedAsync())
        {
            return false;
        }

        var connectionId = Context.ConnectionId;
        var partner = _matchmaking.GetPartner(connectionId);

        if (partner == null)
        {
            return false;
        }

        var sender = _presence.GetPresence(connectionId);

        if (sender == null)
        {
            return false;
        }

        await Clients.Client(partner).SendAsync(
            "ReceiveMessage",
            sender.DisplayName,
            message.Trim());

        return true;
    }

    public async Task SendOffer(string sdp) =>
        await ForwardToPartnerAsync("ReceiveOffer", sdp);

    public async Task SendAnswer(string sdp) =>
        await ForwardToPartnerAsync("ReceiveAnswer", sdp);

    public async Task SendIceCandidate(
        string candidate,
        string? sdpMid,
        int? sdpMLineIndex)
    {
        if (await RejectIfBlockedAsync())
        {
            return;
        }

        var partner = _matchmaking.GetPartner(Context.ConnectionId);

        if (partner != null)
        {
            await Clients.Client(partner).SendAsync(
                "ReceiveIceCandidate",
                candidate,
                sdpMid,
                sdpMLineIndex);
        }
    }

    public async Task SendPing(long sentAt)
    {
        var partner = _matchmaking.GetPartner(Context.ConnectionId);

        if (partner != null)
        {
            await Clients.Client(partner).SendAsync("PartnerPing", sentAt);
        }
    }

    public async Task ReplyPing(long sentAt)
    {
        var partner = _matchmaking.GetPartner(Context.ConnectionId);

        if (partner != null)
        {
            await Clients.Client(partner).SendAsync("PartnerPong", sentAt);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var partner = _matchmaking.GetPartner(Context.ConnectionId);

        _matchmaking.RemoveUser(Context.ConnectionId);
        _presence.Unregister(Context.ConnectionId);

        if (partner != null)
        {
            await Clients.Client(partner).SendAsync("PartnerDisconnected");
        }

        await BroadcastLiveCountAsync();
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<bool> RejectIfBlockedAsync()
    {
        var user = await _userManager.GetUserAsync(Context.User!);
        if (user?.IsBlocked != true)
        {
            return false;
        }

        var connectionId = Context.ConnectionId;
        var partner = _matchmaking.GetPartner(connectionId);
        _matchmaking.RemoveUser(connectionId);
        _presence.Unregister(connectionId);

        if (partner != null)
        {
            await Clients.Client(partner).SendAsync("PartnerDisconnected");
        }

        await Clients.Caller.SendAsync("AccountBlocked");
        Context.Abort();
        return true;
    }

    private async Task ForwardToPartnerAsync(string method, string payload)
    {
        if (await RejectIfBlockedAsync())
        {
            return;
        }

        var partner = _matchmaking.GetPartner(Context.ConnectionId);

        if (partner != null)
        {
            await Clients.Client(partner).SendAsync(method, payload);
        }
    }

    private async Task NotifyMatchedAsync(string userA, string userB)
    {
        var profileA = _presence.GetPresence(userA);
        var profileB = _presence.GetPresence(userB);

        if (profileA == null || profileB == null)
        {
            _matchmaking.EndChat(userA);
            _matchmaking.EnqueueUser(userA);
            _matchmaking.EnqueueUser(userB);

            await Clients.Client(userA).SendAsync("WaitingForPartner");
            await Clients.Client(userB).SendAsync("WaitingForPartner");
            return;
        }

        await Clients.Client(userA).SendAsync(
            "Matched",
            CreatePartnerPayload(profileB),
            IsInitiator(userA, userB));

        await Clients.Client(userB).SendAsync(
            "Matched",
            CreatePartnerPayload(profileA),
            IsInitiator(userB, userA));
    }

    private static object CreatePartnerPayload(UserPresence profile) => new
    {
        name = profile.DisplayName,
        country = profile.Country,
        countryCode = profile.CountryCode
    };

    private async Task BroadcastLiveCountAsync()
    {
        await Clients.All.SendAsync("LiveCountUpdated", _presence.GetLiveCount());
    }

    private static bool IsInitiator(string connectionId, string partnerId) =>
        string.Compare(connectionId, partnerId, StringComparison.Ordinal) < 0;
}
