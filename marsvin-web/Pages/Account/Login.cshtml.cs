using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Account;

public class LoginModel(IUserAccountStore users, IPendingLoginStore pendingLogins, IEmailSender emailSender) : PageModel
{
    // Long enough that "check your email" doesn't feel like a race against the
    // inbox, short enough that a link sitting unread stops being useful fast.
    private static readonly TimeSpan ConfirmationValidFor = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);
    private const int MaxFailedAttempts = 5;

    // Demo-scale, in-memory login throttling keyed by email. A real
    // deployment would persist this (or use a proper rate limiter) so it
    // survives app restarts and works across multiple instances.
    private static readonly Dictionary<string, (int Attempts, DateTime? LockedUntil)> FailedAttempts = new();
    private static readonly object FailedAttemptsLock = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim().ToLowerInvariant();

        if (IsLockedOut(email))
        {
            ModelState.AddModelError(string.Empty,
                "For mange forkerte forsøg. Prøv igen om et par minutter.");
            return Page();
        }

        var user = users.FindByEmail(email);
        var hasher = new PasswordHasher<ApplicationUser>();
        var verified = user is not null && user.IsActive &&
            hasher.VerifyHashedPassword(user, user.PasswordHash, Input.Password) != PasswordVerificationResult.Failed;

        if (!verified)
        {
            RegisterFailedAttempt(email);
            ModelState.AddModelError(string.Empty, "Forkert e-mail eller adgangskode.");
            return Page();
        }

        ClearFailedAttempts(email);

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

        return RedirectToPage("CheckEmail");
    }

    private static bool IsLockedOut(string email)
    {
        lock (FailedAttemptsLock)
        {
            return FailedAttempts.TryGetValue(email, out var entry) &&
                   entry.LockedUntil is DateTime until && until > DateTime.UtcNow;
        }
    }

    private static void RegisterFailedAttempt(string email)
    {
        lock (FailedAttemptsLock)
        {
            var (attempts, _) = FailedAttempts.GetValueOrDefault(email);
            attempts++;
            var lockedUntil = attempts >= MaxFailedAttempts ? DateTime.UtcNow.Add(LockoutDuration) : (DateTime?)null;
            FailedAttempts[email] = (attempts, lockedUntil);
        }
    }

    private static void ClearFailedAttempts(string email)
    {
        lock (FailedAttemptsLock)
        {
            FailedAttempts.Remove(email);
        }
    }

    // Deliberately not PageModel.Url.IsLocalUrl: that needs an IUrlHelper wired up
    // through the full request pipeline, which a PageModel constructed directly in
    // a unit test doesn't have (Url is null there), so it throws where this doesn't.
    // Same local-path shape Url.IsLocalUrl checks - see Cart/Index.cshtml.cs.
    private static bool IsSafeLocalUrl(string url) =>
        url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }
}
