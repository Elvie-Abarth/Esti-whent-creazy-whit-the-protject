using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace MarsvinWebExample.Pages.Account;

// Same "auth" rate-limit policy as Login/Register (see Program.cs) - without
// it, this endpoint would let anyone flood an arbitrary inbox with reset
// emails, or (paired with response timing) probe which addresses have an
// account.
[EnableRateLimiting("auth")]
public class ForgotPasswordModel(
    IUserAccountStore users, IPendingLoginStore pendingLogins, IEmailSender emailSender, IRecaptchaVerifier recaptcha)
    : PageModel
{
    private static readonly TimeSpan ConfirmationValidFor = TimeSpan.FromMinutes(15);

    [BindProperty]
    public InputModel Input { get; set; } = new();

    // Populated by model binding from the hidden <textarea name="g-recaptcha-
    // response"> reCAPTCHA's own JS injects into the form - not set directly
    // by anything in this project's markup.
    [BindProperty(Name = "g-recaptcha-response")]
    public string? RecaptchaResponse { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (!await recaptcha.VerifyAsync(RecaptchaResponse))
        {
            ModelState.AddModelError(string.Empty, "Bekræft venligst, at du ikke er en robot.");
            return Page();
        }

        var user = users.FindByEmail(Input.Email.Trim().ToLowerInvariant());

        // Whether or not the email matches an account, the response is
        // identical and always says "check your email" - answering
        // differently would let this form be used to test which addresses
        // are registered.
        if (user is not null && user.IsActive)
        {
            var token = pendingLogins.Create(user.UserId, returnUrl: null, ConfirmationValidFor);
            var resetUrl = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?token={Uri.EscapeDataString(token)}";

            await emailSender.SendAsync(user.Email, "Nulstil din adgangskode til Marsvin",
                $"""
                Hej {user.DisplayName},

                Nogen (forhåbentlig dig) bad om at nulstille adgangskoden til din Marsvin-konto. Vælg en ny adgangskode ved at åbne linket herunder - det udløber om 15 minutter:

                {resetUrl}

                Var det ikke dig? Så kan du roligt ignorere denne mail - din adgangskode er ikke ændret.

                Venlig hilsen
                Marsvin
                """);
        }

        return RedirectToPage("CheckEmail", new { purpose = "reset" });
    }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";
    }
}
