namespace RandomVideoCallWebpage.Models;

public class FriendRequest
{
    public int Id { get; set; }

    public string FromUserId { get; set; } = string.Empty;

    public string ToUserId { get; set; } = string.Empty;

    public FriendRequestStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ApplicationUser? FromUser { get; set; }

    public ApplicationUser? ToUser { get; set; }
}
