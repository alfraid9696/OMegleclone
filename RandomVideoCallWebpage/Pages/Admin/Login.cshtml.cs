using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomVideoCallWebpage.Services;

namespace RandomVideoCallWebpage.Pages.Admin;

public class LoginModel : PageModel
{
    private readonly AdminAuthService _adminAuth;

    public LoginModel(AdminAuthService adminAuth)
    {
        _adminAuth = adminAuth;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var existing = await HttpContext.AuthenticateAsync(AdminAuthConstants.Scheme);
        if (existing.Succeeded)
        {
            return RedirectToPage("/Admin/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!_adminAuth.ValidateCredentials(Input.Email, Input.Password))
        {
            ModelState.AddModelError(string.Empty, "Invalid admin email or password.");
            return Page();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, Input.Email.Trim()),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, AdminAuthConstants.Scheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            AdminAuthConstants.Scheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return RedirectToPage("/Admin/Index");
    }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
