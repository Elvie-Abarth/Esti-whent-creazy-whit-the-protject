using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Account;

// Rate-limited the same way as Login (see the "auth" policy in Program.cs) -
// registration is exactly the kind of endpoint a script could otherwise
// hammer to probe which emails already have an account, or to spam an inbox
// with confirmation links.
[EnableRateLimiting("auth")]
public class RegisterModel(
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

        // Self-registration only ever creates Customer accounts - Employee
        // and Admin accounts are provisioned separately (seeded, or created
        // by an Admin from /Admin/Users), never chosen by the signup form.
        var hasher = new PasswordHasher<ApplicationUser>();
        var passwordHash = hasher.HashPassword(null!, Input.Password);

        var created = users.CreateUser(Input.Email, passwordHash, Input.DisplayName, UserRole.Customer);
        if (!created)
        {
            ModelState.AddModelError(nameof(Input.Email), "Der findes allerede en konto med den e-mail.");
            return Page();
        }

        var user = users.FindByEmail(Input.Email)!;

        // The account exists, but signing in immediately - as this used to -
        // never actually proved the address belongs to whoever submitted the
        // form. Sending a confirmation link instead and finishing the sign-in
        // in ConfirmLoginModel once it's opened closes that gap, and reuses
        // exactly the same proof-of-inbox-access step Login already requires.
        var safeReturnUrl = IsSafeLocalUrl(returnUrl) ? returnUrl : null;
        var token = pendingLogins.Create(user.UserId, safeReturnUrl, ConfirmationValidFor);
        var confirmUrl = $"{Request.Scheme}://{Request.Host}/Account/ConfirmLogin?token={Uri.EscapeDataString(token)}";

        await emailSender.SendAsync(user.Email, "Bekræft din konto hos Marsvin",
            $"""
            Hej {user.DisplayName},

            Velkommen til Marsvin! Bekræft din nye konto ved at åbne linket herunder - det udløber om 15 minutter:

            {confirmUrl}

            Var det ikke dig, der oprettede kontoen? Så kan du roligt ignorere denne mail.

            Venlig hilsen
            Marsvin
            """);

        return RedirectToPage("CheckEmail", new { purpose = "register", returnUrl = safeReturnUrl });
    }

    public sealed class InputModel
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = "";

        [Required, StringLength(200)]
        public string DisplayName { get; set; } = "";

        [Required, StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Adgangskoderne er ikke ens.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
