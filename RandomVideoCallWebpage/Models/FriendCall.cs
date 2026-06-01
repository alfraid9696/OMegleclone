namespace RandomVideoCallWebpage.Models;

public class FriendCall
{
    public int Id { get; set; }

    public string CallerId { get; set; } = string.Empty;

    public string ReceiverId { get; set; } = string.Empty;

    public FriendCallStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ApplicationUser? Caller { get; set; }

    public ApplicationUser? Receiver { get; set; }
}
