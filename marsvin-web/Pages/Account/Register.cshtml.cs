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

        // The full password policy, checked here (not just the length cap on
        // the field) so the server enforces exactly what the strength meter
        // shows - see Models/PasswordPolicy.cs. Done before the hash so a weak
        // password doesn't even pay for one.
        var passwordProblem = PasswordPolicy.Validate(Input.Password, Input.Email, Input.DisplayName, this.IsEnglish());
        if (passwordProblem is not null)
        {
            ModelState.AddModelError("Input.Password", passwordProblem);
            return Page();
        }

        // Hashing is the one expensive step, and it runs on both paths below
        // (real signup and already-taken email) so the two take about the
        // same time - response timing can't be used to tell them apart either.
        var hasher = new PasswordHasher<ApplicationUser>();
        var passwordHash = hasher.HashPassword(null!, Input.Password);

        // Normalised the same way ForgotPassword looks an address up, so a
        // capitalised retype can't slip past the "already registered" check
        // and create a second account for the same inbox.
        var email = Input.Email.Trim().ToLowerInvariant();
        var browserEnglish = this.IsEnglish();
        var safeReturnUrl = IsSafeLocalUrl(returnUrl) ? returnUrl : null;

        // Self-registration only ever creates Customer accounts - Employee
        // and Admin accounts are provisioned separately (seeded, or created
        // by an Admin from /Admin/Users), never chosen by the signup form.
        var created = users.CreateUser(email, passwordHash, Input.DisplayName.Trim(), UserRole.Customer);

        if (!created)
        {
            // The email is already in use. Saying so to the browser would let
            // anyone test which addresses have an account (user enumeration),
            // so the response is identical to a real signup - "check your
            // email" - and the notice goes to the address itself instead,
            // exactly the way ForgotPassword never reveals whether an account
            // exists. The owner hears about it in their own language.
            var existing = users.FindByEmail(email);
            if (existing is not null)
                await SendAlreadyRegisteredEmailAsync(existing);

            return RedirectToPage("CheckEmail", new { purpose = "register", returnUrl = safeReturnUrl });
        }

        var user = users.FindByEmail(email)!;

        // The new account starts out in the language it was created in.
        if (browserEnglish) users.SetPrefersEnglish(user.UserId, true);

        // Creating the account doesn't sign anyone in - as this used to -
        // because that never actually proved the address belongs to whoever
        // submitted the form. A confirmation link goes out instead, and
        // ConfirmLoginModel finishes the sign-in once it's opened, the same
        // proof-of-inbox-access step Login already requires.
        await SendConfirmationEmailAsync(user, safeReturnUrl, browserEnglish);

        return RedirectToPage("CheckEmail", new { purpose = "register", returnUrl = safeReturnUrl });
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user, string? safeReturnUrl, bool english)
    {
        var token = pendingLogins.Create(user.UserId, safeReturnUrl, ConfirmationValidFor);
        var confirmUrl = $"{Request.Scheme}://{Request.Host}/Account/ConfirmLogin?token={Uri.EscapeDataString(token)}";

        if (english)
        {
            await emailSender.SendAsync(user.Email, "Confirm your account at Marsvin",
                $"""
                Hello {user.DisplayName},

                Welcome to Marsvin! Confirm your new account by opening the link below - it expires in 15 minutes:

                {confirmUrl}

                Wasn't it you who created the account? Then you can safely ignore this email.

                Kind regards
                Marsvin
                """, english: true);
        }
        else
        {
            await emailSender.SendAsync(user.Email, "Bekræft din konto hos Marsvin",
                $"""
                Hej {user.DisplayName},

                Velkommen til Marsvin! Bekræft din nye konto ved at åbne linket herunder - det udløber om 15 minutter:

                {confirmUrl}

                Var det ikke dig, der oprettede kontoen? Så kan du roligt ignorere denne mail.

                Venlig hilsen
                Marsvin
                """);
        }
    }

    // Sent to an address that is already registered when someone tries to
    // sign up with it again. It never goes back to the browser, so it can't
    // be used to confirm the account exists; it just points the real owner
    // at the ways in. Nothing the submitter typed is echoed - only the
    // existing account's own name - and it is written in the owner's
    // language, not the submitter's.
    private async Task SendAlreadyRegisteredEmailAsync(ApplicationUser existing)
    {
        var loginUrl = $"{Request.Scheme}://{Request.Host}/Account/Login";
        var resetUrl = $"{Request.Scheme}://{Request.Host}/Account/ForgotPassword";

        if (existing.PrefersEnglish)
        {
            await emailSender.SendAsync(existing.Email, "You already have a Marsvin account",
                $"""
                Hello {existing.DisplayName},

                Someone just tried to create a Marsvin account with this email address, but you already have one.

                If that was you, there's nothing to do - just log in:
                {loginUrl}

                Forgotten your password? Reset it here:
                {resetUrl}

                If it wasn't you, you can safely ignore this email - no new account was created and nothing has changed.

                Kind regards
                Marsvin
                """, english: true);
        }
        else
        {
            await emailSender.SendAsync(existing.Email, "Du har allerede en konto hos Marsvin",
                $"""
                Hej {existing.DisplayName},

                Nogen forsøgte lige at oprette en Marsvin-konto med denne e-mailadresse, men du har allerede en.

                Var det dig, er der ikke noget at gøre - log bare ind:
                {loginUrl}

                Har du glemt din adgangskode? Nulstil den her:
                {resetUrl}

                Var det ikke dig, kan du roligt ignorere denne mail - der blev ikke oprettet nogen ny konto, og intet er ændret.

                Venlig hilsen
                Marsvin
                """);
        }
    }

    public sealed class InputModel
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = "";

        [Required, StringLength(200)]
        public string DisplayName { get; set; } = "";

        // The minimum (and the rest of the policy) is enforced in the handler
        // by PasswordPolicy, which produces one localized message for every
        // rule; the attribute only caps the length and requires a value.
        [Required, StringLength(100)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Adgangskoderne er ikke ens.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
