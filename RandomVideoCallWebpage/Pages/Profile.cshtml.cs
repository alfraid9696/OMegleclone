using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomVideoCallWebpage.Models;
using RandomVideoCallWebpage.Services;

namespace RandomVideoCallWebpage.Pages;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public ProfileModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public string DisplayName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public int Age { get; private set; }

    public string Gender { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = "XX";

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.IsBlocked == true)
        {
            await _signInManager.SignOutAsync();
            return RedirectToPage("/Account/Login", new { blocked = true });
        }

        if (user == null)
        {
            return Page();
        }

        DisplayName = user.DisplayName;
        Email = user.Email ?? string.Empty;
        Age = user.Age;
        Gender = user.Gender;
        Country = user.Country;
        CountryCode = CountryCodes.GetCode(user.Country);
        return Page();
    }
}
