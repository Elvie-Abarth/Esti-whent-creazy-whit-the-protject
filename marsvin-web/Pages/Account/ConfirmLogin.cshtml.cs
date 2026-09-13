using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Account;

// The second half of the email login-confirmation flow started in LoginModel:
// opening this link (proof of access to the account's inbox, not just its
// password) is what actually creates the signed-in session.
public class ConfirmLoginModel(IUserAccountStore users, IPendingLoginStore pendingLogins) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string? token)
    {
        var ticket = string.IsNullOrEmpty(token) ? null : pendingLogins.Consume(token);
        var user = ticket is null ? null : users.FindById(ticket.UserId);

        if (user is null || !user.IsActive)
        {
            // Missing, expired, or already-used token - or the account was
            // deleted/deactivated in the few minutes between requesting the
            // link and opening it. Either way, nothing to sign in to; the
            // page just shows "log in again".
            return Page();
        }

        users.RecordActivity(user.UserId);
        await SignInAsync(user);

        return !string.IsNullOrEmpty(ticket!.ReturnUrl) ? LocalRedirect(ticket.ReturnUrl) : RedirectToPage("/Index");
    }

    private async Task SignInAsync(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
