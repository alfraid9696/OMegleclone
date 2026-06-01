namespace RandomVideoCallWebpage.Models;

public class FriendMessage
{
    public int Id { get; set; }

    public string SenderId { get; set; } = string.Empty;

    public string ReceiverId { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public ApplicationUser? Sender { get; set; }

    public ApplicationUser? Receiver { get; set; }
}
