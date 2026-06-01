using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RandomVideoCallWebpage.Hubs;
using RandomVideoCallWebpage.Models;
using RandomVideoCallWebpage.Services;

namespace RandomVideoCallWebpage.Pages.Admin;

[Authorize(AuthenticationSchemes = AdminAuthConstants.Scheme, Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly OnlinePresenceService _presence;
    private readonly MatchmakingService _matchmaking;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<ChatHub> _hubContext;

    public IndexModel(
        OnlinePresenceService presence,
        MatchmakingService matchmaking,
        UserManager<ApplicationUser> userManager,
        IHubContext<ChatHub> hubContext)
    {
        _presence = presence;
        _matchmaking = matchmaking;
        _userManager = userManager;
        _hubContext = hubContext;
    }

    public int OnlineCount { get; private set; }

    public int WaitingCount { get; private set; }

    public int ActiveChatPairs { get; private set; }

    public int TotalRegisteredUsers { get; private set; }

    public int BlockedUserCount { get; private set; }

    public IReadOnlyList<UserPresence> OnlineUsers { get; private set; } = [];

    public IReadOnlyList<AdminRegisteredUserRow> RegisteredUsers { get; private set; } = [];

    public DateTime LastUpdated { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadDashboardAsync();
    }

    public async Task<IActionResult> OnPostBlockAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToPage();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return RedirectToPage();
        }

        user.IsBlocked = true;
        await _userManager.UpdateAsync(user);
        await DisconnectUserAsync(userId);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUnblockAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToPage();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return RedirectToPage();
        }

        user.IsBlocked = false;
        await _userManager.UpdateAsync(user);

        return RedirectToPage();
    }

    private async Task LoadDashboardAsync()
    {
        LastUpdated = DateTime.UtcNow;
        OnlineCount = _presence.GetLiveCount();
        WaitingCount = _matchmaking.GetWaitingCount();
        ActiveChatPairs = _matchmaking.GetActiveChatPairCount();

        OnlineUsers = _presence.GetAllOnline()
            .OrderBy(u => u.DisplayName)
            .ToList();

        var onlineUserIds = OnlineUsers
            .Select(u => u.UserId)
            .ToHashSet(StringComparer.Ordinal);

        var allUsers = await _userManager.Users
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        TotalRegisteredUsers = allUsers.Count;
        BlockedUserCount = allUsers.Count(u => u.IsBlocked);

        RegisteredUsers = allUsers
            .Select(u => new AdminRegisteredUserRow
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Email = u.Email ?? string.Empty,
                Age = u.Age,
                Gender = u.Gender,
                Country = u.Country,
                IsOnline = onlineUserIds.Contains(u.Id),
                IsBlocked = u.IsBlocked
            })
            .ToList();
    }

    private async Task DisconnectUserAsync(string userId)
    {
        var connectionIds = _presence.UnregisterByUserId(userId);

        foreach (var connectionId in connectionIds)
        {
            var partner = _matchmaking.GetPartner(connectionId);
            _matchmaking.RemoveUser(connectionId);

            if (partner != null)
            {
                await _hubContext.Clients.Client(partner).SendAsync("PartnerDisconnected");
            }

            await _hubContext.Clients.Client(connectionId).SendAsync("AccountBlocked");
        }
    }
}
