using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

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
        await this.SignInAsync(user);

        return !string.IsNullOrEmpty(ticket!.ReturnUrl) ? LocalRedirect(ticket.ReturnUrl) : RedirectToPage("/Index");
    }
}
