using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Account;

public class LoginModel(IUserAccountStore users) : PageModel
{
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
        users.RecordActivity(user!.UserId);
        await SignInAsync(user!);

        return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
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

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }
}
