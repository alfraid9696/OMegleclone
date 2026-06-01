namespace RandomVideoCallWebpage.Models;

public class AdminRegisteredUserRow
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int Age { get; set; }

    public string Gender { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public bool IsOnline { get; set; }

    public bool IsBlocked { get; set; }
}
