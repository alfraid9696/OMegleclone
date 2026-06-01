using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomVideoCallWebpage.Services;

namespace RandomVideoCallWebpage.Pages.Admin;

[Authorize(AuthenticationSchemes = AdminAuthConstants.Scheme)]
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(AdminAuthConstants.Scheme);
        return RedirectToPage("/Admin/Login");
    }
}
