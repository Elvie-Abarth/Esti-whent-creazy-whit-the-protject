using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Account;

// Rate-limited (see the "auth" policy in Program.cs) on top of the per-email
// lockout below - the policy caps how many login attempts one IP can make
// per minute regardless of which email(s) it tries, which the per-email
// lockout alone doesn't: without it, an attacker who already knows a
// victim's email could re-lock their account indefinitely, and nothing
// stopped one client from trying thousands of different emails per minute.
[EnableRateLimiting("auth")]
public class LoginModel(
    IUserAccountStore users, IPendingLoginStore pendingLogins, IEmailSender emailSender,
    IRecaptchaVerifier recaptcha, LoginLockoutTracker lockout)
    : PageModel
{
    // Long enough that "check your email" doesn't feel like a race against the
    // inbox, short enough that a link sitting unread stops being useful fast.
    private static readonly TimeSpan ConfirmationValidFor = TimeSpan.FromMinutes(15);

    [BindProperty]
    public InputModel Input { get; set; } = new();

    // Populated by model binding from the hidden <textarea name="g-recaptcha-
    // response"> reCAPTCHA's own JS injects into the form - not set directly
    // by anything in this project's markup.
    [BindProperty(Name = "g-recaptcha-response")]
    public string? RecaptchaResponse { get; set; }

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return Page();

        if (!await recaptcha.VerifyAsync(RecaptchaResponse))
        {
            ModelState.AddModelError(string.Empty, "Bekræft venligst, at du ikke er en robot.");
            return Page();
        }

        var email = Input.Email.Trim().ToLowerInvariant();

        if (lockout.IsLockedOut(email))
        {
            ModelState.AddModelError(string.Empty, lockout.RequiresManualReset(email)
                ? "Din konto er låst efter flere mislykkede forsøg. Nulstil din adgangskode for at logge ind igen."
                : "For mange forkerte forsøg. Prøv igen om lidt.");
            return Page();
        }

        var user = users.FindByEmail(email);
        var hasher = new PasswordHasher<ApplicationUser>();
        var verified = user is not null && user.IsActive &&
            hasher.VerifyHashedPassword(user, user.PasswordHash, Input.Password) != PasswordVerificationResult.Failed;

        if (!verified)
        {
            var shouldNotify = lockout.RegisterFailedAttempt(email);
            // Only ever reaches a real inbox if the email actually matches
            // an account - an attacker probing random addresses never
            // learns anything from whether this fires.
            if (shouldNotify && user is not null)
            {
                await emailSender.SendAsync(user.Email, "Mistænkelig aktivitet på din Marsvin-konto",
                    $"""
                    Hej {user.DisplayName},

                    Der har været flere mislykkede loginforsøg på din Marsvin-konto for nylig. Hvis det ikke var dig, bør du nulstille din adgangskode:

                    {Request.Scheme}://{Request.Host}/Account/ForgotPassword

                    Var det dig selv, der tastede forkert nogle gange? Så kan du roligt ignorere denne mail.

                    Venlig hilsen
                    Marsvin
                    """);
            }

            ModelState.AddModelError(string.Empty, "Forkert e-mail eller adgangskode.");
            return Page();
        }

        lockout.Clear(email);

        // Password alone doesn't sign you in - a confirmation link goes to the
        // account's own email first (proof you also control the inbox, not
        // just the password), and ConfirmLoginModel finishes the sign-in once
        // that link is opened.
        var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && IsSafeLocalUrl(returnUrl) ? returnUrl : null;
        var token = pendingLogins.Create(user!.UserId, safeReturnUrl, ConfirmationValidFor);
        var confirmUrl = $"{Request.Scheme}://{Request.Host}/Account/ConfirmLogin?token={Uri.EscapeDataString(token)}";

        await emailSender.SendAsync(user.Email, "Bekræft login til Marsvin",
            $"""
            Hej {user.DisplayName},

            Nogen (forhåbentlig dig) forsøgte at logge ind på din Marsvin-konto. Bekræft det er dig ved at åbne linket herunder - det udløber om 15 minutter:

            {confirmUrl}

            Var det ikke dig? Så kan du roligt ignorere denne mail - der sker ikke noget uden bekræftelse.

            Venlig hilsen
            Marsvin
            """);

        return RedirectToPage("CheckEmail", new { returnUrl = safeReturnUrl });
    }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }
}
