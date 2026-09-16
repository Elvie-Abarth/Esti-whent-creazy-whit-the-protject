using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Account;

// The second half of login for a TOTP-enrolled account (see LoginModel):
// password already checked, this is the "prove you have the authenticator
// app too" step. Same lockout tracker as password attempts - a 6-digit code
// only has a million possibilities, so it needs the same brute-force
// protection a password does, arguably more.
[EnableRateLimiting("auth")]
public class VerifyTotpModel(IUserAccountStore users, IPendingLoginStore pendingLogins, LoginLockoutTracker lockout)
    : PageModel
{
    [BindProperty]
    public string Token { get; set; } = "";

    [BindProperty]
    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";

    public IActionResult OnGet(string? token)
    {
        Token = token ?? "";
        if (string.IsNullOrEmpty(Token) || pendingLogins.Peek(Token) is null)
            return RedirectToPage("Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var ticket = pendingLogins.Peek(Token);
        if (ticket is null)
        {
            ModelState.AddModelError(string.Empty, "Login-anmodningen er udløbet. Log ind igen.");
            return Page();
        }

        var user = users.FindById(ticket.UserId);
        if (user is null || !user.IsActive || !user.TotpEnabled || user.TotpSecret is null)
            return RedirectToPage("Login");

        if (lockout.IsLockedOut(user.Email))
        {
            ModelState.AddModelError(string.Empty, lockout.RequiresManualReset(user.Email)
                ? "Din konto er låst efter flere mislykkede forsøg. Nulstil din adgangskode for at logge ind igen."
                : "For mange forkerte forsøg. Prøv igen om lidt.");
            return Page();
        }

        if (!ModelState.IsValid || !Totp.ValidateCode(user.TotpSecret, Code))
        {
            lockout.RegisterFailedAttempt(user.Email);
            ModelState.AddModelError(string.Empty, "Forkert kode. Prøv igen.");
            return Page();
        }

        lockout.Clear(user.Email);
        pendingLogins.Consume(Token);
        users.RecordActivity(user.UserId);
        await this.SignInAsync(user);

        return !string.IsNullOrEmpty(ticket.ReturnUrl) ? LocalRedirect(ticket.ReturnUrl) : RedirectToPage("/Index");
    }
}
