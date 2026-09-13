using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Account;

// Open to every signed-in role, not just Customer - clicking your own name in
// the header should let you manage your own account whether you're a
// customer, an employee, or the admin themself.
[Authorize]
public class ProfileModel(IUserAccountStore users) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public void OnGet()
    {
        var user = users.FindById(CurrentUserId)!;
        Input.DisplayName = user.DisplayName;
        Input.Email = user.Email;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = users.FindById(CurrentUserId)!;
        var hasher = new PasswordHasher<ApplicationUser>();

        // Changing your email or password is exactly the kind of thing that
        // shouldn't be possible from a hijacked, still-logged-in browser tab
        // alone - re-check the current password every time, the same way a
        // bank or email provider would before letting you change account details.
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, Input.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(Input.CurrentPassword), "Forkert adgangskode.");
            return Page();
        }

        var email = Input.Email.Trim().ToLowerInvariant();
        var updated = users.UpdateProfile(CurrentUserId, Input.DisplayName.Trim(), email);
        if (!updated)
        {
            ModelState.AddModelError(nameof(Input.Email), "Der findes allerede en konto med den e-mail.");
            return Page();
        }

        if (!string.IsNullOrEmpty(Input.NewPassword))
        {
            users.UpdatePassword(CurrentUserId, hasher.HashPassword(user, Input.NewPassword));
        }

        // The auth cookie's claims (name, email) were fixed at login - refresh
        // them now, otherwise the header would keep showing the old name/email
        // until the next login even though the database is already updated.
        var refreshed = users.FindById(CurrentUserId)!;
        await SignInAsync(refreshed);

        SuccessMessage = "Dine oplysninger er opdateret.";
        return RedirectToPage();
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

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public sealed class InputModel
    {
        [Required, StringLength(200)]
        public string DisplayName { get; set; } = "";

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = "";

        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Adgangskoderne er ikke ens.")]
        public string? ConfirmNewPassword { get; set; }

        [Required, DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = "";
    }
}
