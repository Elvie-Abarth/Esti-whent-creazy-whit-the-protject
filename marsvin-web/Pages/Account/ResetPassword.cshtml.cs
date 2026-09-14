using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace MarsvinWebExample.Pages.Account;

[EnableRateLimiting("auth")]
public class ResetPasswordModel(IUserAccountStore users, IPendingLoginStore pendingLogins) : PageModel
{
    [BindProperty]
    public string Token { get; set; } = "";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool TokenIsValid { get; private set; }

    [TempData]
    public string? ToastMessage { get; set; }

    public void OnGet(string? token)
    {
        Token = token ?? "";
        TokenIsValid = !string.IsNullOrEmpty(token) && pendingLogins.IsValid(token);
    }

    public IActionResult OnPost()
    {
        TokenIsValid = !string.IsNullOrEmpty(Token) && pendingLogins.IsValid(Token);
        if (!TokenIsValid) return Page();

        if (!ModelState.IsValid) return Page();

        // Consumed here, at the point of actual use - the OnGet check above
        // only peeked, so the token is still good for this one redemption
        // even if the visitor sat on the form for a while first.
        var ticket = pendingLogins.Consume(Token);
        var user = ticket is null ? null : users.FindById(ticket.UserId);
        if (user is null)
        {
            TokenIsValid = false;
            return Page();
        }

        var hasher = new PasswordHasher<ApplicationUser>();
        users.UpdatePassword(user.UserId, hasher.HashPassword(user, Input.NewPassword));

        ToastMessage = new Bilingual(
            "Din adgangskode er ændret. Log ind med den nye.",
            "Your password has been changed. Log in with the new one.");
        return RedirectToPage("Login");
    }

    public sealed class InputModel
    {
        [Required, StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required, DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Adgangskoderne er ikke ens.")]
        public string ConfirmNewPassword { get; set; } = "";
    }
}
