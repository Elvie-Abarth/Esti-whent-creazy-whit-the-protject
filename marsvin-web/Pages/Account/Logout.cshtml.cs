using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Account;

public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        // The server-side session too (a guest cart, a receipt pass from
        // before logging in): nothing of this visit is left for whoever
        // uses the browser next.
        this.ClearServerSession();
        return RedirectToPage("/Index");
    }
}
