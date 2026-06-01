using Microsoft.EntityFrameworkCore;
using RandomVideoCallWebpage.Data;
using RandomVideoCallWebpage.Models;
namespace RandomVideoCallWebpage.Services;

public class FriendService
{
    private readonly ApplicationDbContext _db;
    private readonly OnlinePresenceService _presence;

    public FriendService(ApplicationDbContext db, OnlinePresenceService presence)
    {
        _db = db;
        _presence = presence;
    }

    public async Task<(bool Success, string? Error, int? RequestId)> SendRequestAsync(
        string fromUserId,
        string toUserId)
    {
        if (fromUserId == toUserId)
        {
            return (false, "You cannot add yourself.", null);
        }

        if (await AreFriendsAsync(fromUserId, toUserId))
        {
            return (false, "You are already friends.", null);
        }

        var existing = await _db.FriendRequests
            .Where(request =>
                request.Status == FriendRequestStatus.Pending &&
                ((request.FromUserId == fromUserId && request.ToUserId == toUserId) ||
                 (request.FromUserId == toUserId && request.ToUserId == fromUserId)))
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            return (false, "A friend request is already pending.", existing.Id);
        }

        var friendRequest = new FriendRequest
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Status = FriendRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.FriendRequests.Add(friendRequest);
        await _db.SaveChangesAsync();

        return (true, null, friendRequest.Id);
    }

    public async Task<FriendRequest?> GetRequestAsync(int requestId) =>
        await _db.FriendRequests.FindAsync(requestId);

    public async Task<(bool Success, string? Error)> AcceptRequestAsync(int requestId, string userId)
    {
        var request = await _db.FriendRequests.FindAsync(requestId);
        if (request == null || request.ToUserId != userId || request.Status != FriendRequestStatus.Pending)
        {
            return (false, "Friend request not found.");
        }

        request.Status = FriendRequestStatus.Accepted;

        var (userA, userB) = OrderUserPair(request.FromUserId, request.ToUserId);
        var friendshipExists = await _db.Friendships
            .AnyAsync(friendship => friendship.UserAId == userA && friendship.UserBId == userB);

        if (!friendshipExists)
        {
            _db.Friendships.Add(new Friendship
            {
                UserAId = userA,
                UserBId = userB,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeclineRequestAsync(int requestId, string userId)
    {
        var request = await _db.FriendRequests.FindAsync(requestId);
        if (request == null || request.ToUserId != userId || request.Status != FriendRequestStatus.Pending)
        {
            return (false, "Friend request not found.");
        }

        request.Status = FriendRequestStatus.Declined;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> AreFriendsAsync(string userAId, string userBId)
    {
        var (userA, userB) = OrderUserPair(userAId, userBId);
        return await _db.Friendships
            .AnyAsync(friendship => friendship.UserAId == userA && friendship.UserBId == userB);
    }

    public async Task<List<FriendListItem>> GetFriendsAsync(string userId)
    {
        var friendships = await _db.Friendships
            .Where(friendship => friendship.UserAId == userId || friendship.UserBId == userId)
            .ToListAsync();

        var friendIds = friendships
            .Select(friendship => friendship.UserAId == userId ? friendship.UserBId : friendship.UserAId)
            .Distinct()
            .ToList();

        if (friendIds.Count == 0)
        {
            return [];
        }

        var users = await _db.Users
            .Where(user => friendIds.Contains(user.Id))
            .ToListAsync();

        var unreadCounts = await _db.FriendMessages
            .Where(message => message.ReceiverId == userId && message.ReadAtUtc == null)
            .GroupBy(message => message.SenderId)
            .Select(group => new { SenderId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.SenderId, item => item.Count);

        return users
            .Select(user => new FriendListItem
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Country = user.Country,
                CountryCode = CountryCodes.GetCode(user.Country),
                IsOnline = _presence.IsUserOnline(user.Id),
                UnreadCount = unreadCounts.GetValueOrDefault(user.Id)
            })
            .OrderByDescending(friend => friend.IsOnline)
            .ThenBy(friend => friend.DisplayName)
            .ToList();
    }

    public async Task<List<FriendRequestItem>> GetIncomingRequestsAsync(string userId)
    {
        return await _db.FriendRequests
            .Where(request => request.ToUserId == userId && request.Status == FriendRequestStatus.Pending)
            .Include(request => request.FromUser)
            .OrderByDescending(request => request.CreatedAtUtc)
            .Select(request => new FriendRequestItem
            {
                RequestId = request.Id,
                FromUserId = request.FromUserId,
                DisplayName = request.FromUser!.DisplayName,
                Country = request.FromUser.Country,
                CountryCode = CountryCodes.GetCode(request.FromUser.Country),
                CreatedAtUtc = request.CreatedAtUtc
            })
            .ToListAsync();
    }

    public async Task<FriendMessage?> SaveMessageAsync(string senderId, string receiverId, string body)
    {
        if (!await AreFriendsAsync(senderId, receiverId))
        {
            return null;
        }

        var trimmed = body.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var message = new FriendMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Body = trimmed,
            SentAtUtc = DateTime.UtcNow
        };

        _db.FriendMessages.Add(message);
        await _db.SaveChangesAsync();
        return message;
    }

    public async Task<List<FriendMessageItem>> GetMessagesAsync(string userId, string friendUserId)
    {
        if (!await AreFriendsAsync(userId, friendUserId))
        {
            return [];
        }

        return await _db.FriendMessages
            .Where(message =>
                (message.SenderId == userId && message.ReceiverId == friendUserId) ||
                (message.SenderId == friendUserId && message.ReceiverId == userId))
            .OrderBy(message => message.SentAtUtc)
            .Select(message => new FriendMessageItem
            {
                Id = message.Id,
                SenderId = message.SenderId,
                Body = message.Body,
                SentAtUtc = message.SentAtUtc,
                IsMine = message.SenderId == userId
            })
            .ToListAsync();
    }

    public async Task MarkMessagesReadAsync(string userId, string friendUserId)
    {
        var unread = await _db.FriendMessages
            .Where(message =>
                message.SenderId == friendUserId &&
                message.ReceiverId == userId &&
                message.ReadAtUtc == null)
            .ToListAsync();

        if (unread.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var message in unread)
        {
            message.ReadAtUtc = now;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<int> GetPendingRequestCountAsync(string userId) =>
        await _db.FriendRequests.CountAsync(request =>
            request.ToUserId == userId && request.Status == FriendRequestStatus.Pending);

    public async Task<FriendCall> CreateCallAsync(string callerId, string receiverId)
    {
        var call = new FriendCall
        {
            CallerId = callerId,
            ReceiverId = receiverId,
            Status = _presence.IsUserOnline(receiverId)
                ? FriendCallStatus.Ringing
                : FriendCallStatus.Missed,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.FriendCalls.Add(call);
        await _db.SaveChangesAsync();
        return call;
    }

    public async Task UpdateCallStatusAsync(int callId, FriendCallStatus status)
    {
        var call = await _db.FriendCalls.FindAsync(callId);
        if (call == null)
        {
            return;
        }

        call.Status = status;
        await _db.SaveChangesAsync();
    }

    public async Task<List<FriendCallItem>> GetMissedCallsAsync(string userId)
    {
        return await _db.FriendCalls
            .Where(call => call.ReceiverId == userId && call.Status == FriendCallStatus.Missed)
            .Include(call => call.Caller)
            .OrderByDescending(call => call.CreatedAtUtc)
            .Take(20)
            .Select(call => new FriendCallItem
            {
                CallId = call.Id,
                CallerId = call.CallerId,
                DisplayName = call.Caller!.DisplayName,
                CreatedAtUtc = call.CreatedAtUtc
            })
            .ToListAsync();
    }

    private static (string UserA, string UserB) OrderUserPair(string userAId, string userBId) =>
        string.Compare(userAId, userBId, StringComparison.Ordinal) < 0
            ? (userAId, userBId)
            : (userBId, userAId);
}

public class FriendListItem
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public bool IsOnline { get; set; }

    public int UnreadCount { get; set; }
}

public class FriendRequestItem
{
    public int RequestId { get; set; }

    public string FromUserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

public class FriendMessageItem
{
    public int Id { get; set; }

    public string SenderId { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; }

    public bool IsMine { get; set; }
}

public class FriendCallItem
{
    public int CallId { get; set; }

    public string CallerId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
