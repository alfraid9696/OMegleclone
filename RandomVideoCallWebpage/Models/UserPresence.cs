namespace RandomVideoCallWebpage.Models;

public class UserPresence
{
    public string ConnectionId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;
}
