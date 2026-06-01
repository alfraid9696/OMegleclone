namespace RandomVideoCallWebpage.Models;

public class Friendship
{
    public int Id { get; set; }

    public string UserAId { get; set; } = string.Empty;

    public string UserBId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public ApplicationUser? UserA { get; set; }

    public ApplicationUser? UserB { get; set; }
}
