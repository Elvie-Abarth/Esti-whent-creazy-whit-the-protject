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
public class LoginModel(IUserAccountStore users, IPendingLoginStore pendingLogins, IEmailSender emailSender) : PageModel
{
    // Long enough that "check your email" doesn't feel like a race against the
    // inbox, short enough that a link sitting unread stops being useful fast.
    private static readonly TimeSpan ConfirmationValidFor = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);
    private const int MaxFailedAttempts = 5;

    // Demo-scale, in-memory login throttling keyed by email. A real
    // deployment would persist this (or use a proper rate limiter) so it
    // survives app restarts and works across multiple instances. Entries
    // older than EntryTtl are swept out on every access below, so a flood of
    // distinct throwaway emails can't grow this dictionary forever the way
    // it could when entries never expired.
    private static readonly TimeSpan EntryTtl = TimeSpan.FromHours(1);
    private static readonly Dictionary<string, (int Attempts, DateTime? LockedUntil, DateTime LastSeenAt)> FailedAttempts = new();
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

        return RedirectToPage("CheckEmail", new { returnUrl = safeReturnUrl });
    }

    private static bool IsLockedOut(string email)
    {
        lock (FailedAttemptsLock)
        {
            PruneStaleEntries();
            return FailedAttempts.TryGetValue(email, out var entry) &&
                   entry.LockedUntil is DateTime until && until > DateTime.UtcNow;
        }
    }

    private static void RegisterFailedAttempt(string email)
    {
        lock (FailedAttemptsLock)
        {
            PruneStaleEntries();
            var (attempts, _, _) = FailedAttempts.GetValueOrDefault(email);
            attempts++;
            var lockedUntil = attempts >= MaxFailedAttempts ? DateTime.UtcNow.Add(LockoutDuration) : (DateTime?)null;
            FailedAttempts[email] = (attempts, lockedUntil, DateTime.UtcNow);
        }
    }

    private static void ClearFailedAttempts(string email)
    {
        lock (FailedAttemptsLock)
        {
            FailedAttempts.Remove(email);
        }
    }

    // Called with FailedAttemptsLock already held. A stale entry (nothing
    // seen from that email in over EntryTtl) is long past being locked out -
    // removing it here, a little at a time on every real request, means the
    // dictionary never needs its own background sweep.
    private static void PruneStaleEntries()
    {
        var cutoff = DateTime.UtcNow - EntryTtl;
        foreach (var key in FailedAttempts.Where(kv => kv.Value.LastSeenAt < cutoff).Select(kv => kv.Key).ToList())
            FailedAttempts.Remove(key);
    }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }
}
