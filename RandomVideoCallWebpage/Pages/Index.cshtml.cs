using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomVideoCallWebpage.Models;
using RandomVideoCallWebpage.Services;

namespace RandomVideoCallWebpage.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public string UserDisplayName { get; private set; } = string.Empty;

    public string UserEmail { get; private set; } = string.Empty;

    public int UserAge { get; private set; }

    public string UserGender { get; private set; } = string.Empty;

    public string UserCountry { get; private set; } = string.Empty;

    public string UserCountryCode { get; private set; } = "XX";

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user != null)
        {
            UserDisplayName = user.DisplayName;
            UserEmail = user.Email ?? string.Empty;
            UserAge = user.Age;
            UserGender = user.Gender;
            UserCountry = user.Country;
            UserCountryCode = CountryCodes.GetCode(user.Country);
        }
    }
}
