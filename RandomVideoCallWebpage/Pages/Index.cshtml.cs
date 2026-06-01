using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomVideoCallWebpage.Models;
using RandomVideoCallWebpage.Services;

namespace RandomVideoCallWebpage.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public string UserDisplayName { get; private set; } = string.Empty;

    public string UserEmail { get; private set; } = string.Empty;

    public int UserAge { get; private set; }

    public string UserGender { get; private set; } = string.Empty;

    public string UserCountry { get; private set; } = string.Empty;

    public string UserCountryCode { get; private set; } = "XX";

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.IsBlocked == true)
        {
            await _signInManager.SignOutAsync();
            return RedirectToPage("/Account/Login", new { blocked = true });
        }

        if (user != null)
        {
            UserDisplayName = user.DisplayName;
            UserEmail = user.Email ?? string.Empty;
            UserAge = user.Age;
            UserGender = user.Gender;
            UserCountry = user.Country;
            UserCountryCode = CountryCodes.GetCode(user.Country);
        }

        return Page();
    }
}
