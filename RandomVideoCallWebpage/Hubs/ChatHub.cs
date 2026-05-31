using Microsoft.AspNetCore.SignalR;
using RandomVideoCallWebpage.Services;

namespace RandomVideoCallWebpage.Hubs;

public class ChatHub : Hub
{
    private readonly MatchmakingService _matchmaking;

    public ChatHub(MatchmakingService matchmaking)
    {
        _matchmaking = matchmaking;
    }

    public async Task StartChat()
    {
        var connectionId = Context.ConnectionId;
        var partner = _matchmaking.FindPartner(connectionId);

        if (partner != null)
        {
            await NotifyMatchedAsync(connectionId, partner);
        }
        else
        {
            await Clients.Client(connectionId)
                .SendAsync("WaitingForPartner");
        }
    }

    public async Task NextStranger()
    {
        var connectionId = Context.ConnectionId;
        var partner = _matchmaking.GetPartner(connectionId);

        _matchmaking.EndChat(connectionId);

        if (partner != null)
        {
            await Clients.Client(partner)
                .SendAsync("StrangerDisconnected");
        }

        var newPartner = _matchmaking.FindPartner(connectionId);

        if (newPartner != null)
        {
            await NotifyMatchedAsync(connectionId, newPartner);
        }
        else
        {
            await Clients.Client(connectionId)
                .SendAsync("WaitingForPartner");
        }
    }

    public async Task SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var partner = _matchmaking.GetPartner(Context.ConnectionId);

        if (partner != null)
        {
            await Clients.Client(partner)
                .SendAsync("ReceiveMessage", message.Trim());
        }
    }

    public async Task SendOffer(string sdp)
    {
        await ForwardToPartnerAsync("ReceiveOffer", sdp);
    }

    public async Task SendAnswer(string sdp)
    {
        await ForwardToPartnerAsync("ReceiveAnswer", sdp);
    }

    public async Task SendIceCandidate(
        string candidate,
        string? sdpMid,
        int? sdpMLineIndex)
    {
        var partner = _matchmaking.GetPartner(Context.ConnectionId);

        if (partner != null)
        {
            await Clients.Client(partner)
                .SendAsync(
                    "ReceiveIceCandidate",
                    candidate,
                    sdpMid,
                    sdpMLineIndex);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var partner = _matchmaking.GetPartner(Context.ConnectionId);

        _matchmaking.RemoveUser(Context.ConnectionId);

        if (partner != null)
        {
            await Clients.Client(partner)
                .SendAsync("StrangerDisconnected");
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task ForwardToPartnerAsync(string method, string payload)
    {
        var partner = _matchmaking.GetPartner(Context.ConnectionId);

        if (partner != null)
        {
            await Clients.Client(partner).SendAsync(method, payload);
        }
    }

    private async Task NotifyMatchedAsync(string userA, string userB)
    {
        await Clients.Client(userA)
            .SendAsync("Matched", IsInitiator(userA, userB));

        await Clients.Client(userB)
            .SendAsync("Matched", IsInitiator(userB, userA));
    }

    private static bool IsInitiator(string connectionId, string partnerId) =>
        string.Compare(connectionId, partnerId, StringComparison.Ordinal) < 0;
}
