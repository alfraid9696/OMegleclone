using Microsoft.AspNetCore.Identity;

namespace RandomVideoCallWebpage.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public int Age { get; set; }

    public string Gender { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;
}
