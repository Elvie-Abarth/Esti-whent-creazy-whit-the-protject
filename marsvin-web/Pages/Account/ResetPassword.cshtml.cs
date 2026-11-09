using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace MarsvinWebExample.Pages.Account;

[EnableRateLimiting("auth")]
public class ResetPasswordModel(
    IUserAccountStore users, IPendingLoginStore pendingLogins, LoginLockoutTracker lockout, IEmailSender emailSender)
    : PageModel
{
    [BindProperty]
    public string Token { get; set; } = "";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool TokenIsValid { get; private set; }

    /// <summary>The minimum length the meter should show - 15 for an admin
    /// account, 12 otherwise. Resolved from the token so the meter matches
    /// what the server will enforce for this specific account.</summary>
    public int MinPasswordLength { get; private set; } = PasswordPolicy.MinLength;

    [TempData]
    public string? ToastMessage { get; set; }

    public void OnGet(string? token)
    {
        Token = token ?? "";
        TokenIsValid = !string.IsNullOrEmpty(token) && pendingLogins.IsValid(token);
        MinPasswordLength = PasswordPolicy.MinLengthFor(RoleFromToken(Token));
    }

    // The account's role from a valid, unspent token - Customer when the token
    // can't be resolved, so the meter never over-promises. Peek, never Consume:
    // reading the role must not spend the one-time link.
    private UserRole RoleFromToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return UserRole.Customer;
        var peeked = pendingLogins.Peek(token);
        var user = peeked is null ? null : users.FindById(peeked.UserId);
        return user?.Role ?? UserRole.Customer;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        TokenIsValid = !string.IsNullOrEmpty(Token) && pendingLogins.IsValid(Token);
        if (!TokenIsValid) return Page();

        if (!ModelState.IsValid) return Page();

        // Resolve the account from the token WITHOUT spending it yet (Peek,
        // not Consume), so we can check the account's own name/email against
        // the new password and still leave the one-time link usable if the
        // password is rejected.
        var peeked = pendingLogins.Peek(Token);
        var user = peeked is null ? null : users.FindById(peeked.UserId);
        if (user is null)
        {
            TokenIsValid = false;
            return Page();
        }

        MinPasswordLength = PasswordPolicy.MinLengthFor(user.Role);
        var passwordProblem = PasswordPolicy.Validate(Input.NewPassword, user.Email, user.DisplayName, this.IsEnglish(), user.Role);
        if (passwordProblem is not null)
        {
            ModelState.AddModelError("Input.NewPassword", passwordProblem);
            return Page();
        }

        // Only now spend the token, at the point the change actually happens.
        if (pendingLogins.Consume(Token) is null)
        {
            // Raced with another use between the Peek and here - treat the
            // link as gone rather than changing the password twice.
            TokenIsValid = false;
            return Page();
        }

        var hasher = new PasswordHasher<ApplicationUser>();
        users.UpdatePassword(user.UserId, hasher.HashPassword(user, Input.NewPassword));

        // The only thing that can lift a RequiresManualReset lockout -
        // choosing a new password here is what that escalation demanded.
        lockout.Clear(user.Email.Trim().ToLowerInvariant());

        // ASVS 2.5.5 - notify on every auth-factor change, this recovery
        // path included, not just the self-service one on Profile.
        if (this.IsEnglish())
        {
            await emailSender.SendAsync(user.Email, "Your Marsvin password has been changed",
                $"""
                Hello {user.DisplayName},

                Your password has just been changed through "forgot password".

                If that wasn't you, contact us right away using the contact details on the website.

                Kind regards
                Marsvin
                """, english: true);
        }
        else
        {
            await emailSender.SendAsync(user.Email, "Din adgangskode er ændret på Marsvin",
                $"""
                Hej {user.DisplayName},

                Din adgangskode er lige blevet ændret via "glemt adgangskode".

                Var det ikke dig, så kontakt os med det samme via kontaktoplysningerne på hjemmesiden.

                Venlig hilsen
                Marsvin
                """);
        }

        ToastMessage = new Bilingual(
            "Din adgangskode er ændret. Log ind med den nye.",
            "Your password has been changed. Log in with the new one.");
        return RedirectToPage("Login");
    }

    public sealed class InputModel
    {
        // Full policy enforced in the handler by PasswordPolicy (one localized
        // message per rule); the attribute only caps length and requires a value.
        [Required, StringLength(100)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required, DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Adgangskoderne er ikke ens.")]
        public string ConfirmNewPassword { get; set; } = "";
    }
}
