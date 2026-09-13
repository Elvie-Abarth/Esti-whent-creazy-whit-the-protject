using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class IndexModel(IUserAccountStore users) : PageModel
{
    public IReadOnlyList<ApplicationUser> Items { get; private set; } = [];

    [BindProperty]
    public NewStaffInputModel NewStaff { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    public void OnGet() => Items = users.GetAll();

    public IActionResult OnPostUpdateRole(int userId, UserRole role)
    {
        if (userId == CurrentUserId)
        {
            ErrorMessage = "Du kan ikke ændre din egen rolle.";
            return RedirectToPage();
        }
        users.UpdateRole(userId, role);
        return RedirectToPage();
    }

    public IActionResult OnPostToggleActive(int userId, bool isActive)
    {
        if (userId == CurrentUserId)
        {
            ErrorMessage = "Du kan ikke deaktivere din egen konto.";
            return RedirectToPage();
        }
        users.SetActive(userId, isActive);
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int userId)
    {
        if (userId == CurrentUserId)
        {
            ErrorMessage = "Du kan ikke slette din egen konto.";
            return RedirectToPage();
        }
        users.DeleteUser(userId);
        return RedirectToPage();
    }

    public IActionResult OnPostCreateStaff()
    {
        Items = users.GetAll();
        if (!ModelState.IsValid) return Page();

        var hasher = new PasswordHasher<ApplicationUser>();
        var passwordHash = hasher.HashPassword(null!, NewStaff.Password);

        var created = users.CreateUser(NewStaff.Email, passwordHash, NewStaff.DisplayName, NewStaff.Role);
        if (!created)
        {
            ModelState.AddModelError(nameof(NewStaff.Email), "Der findes allerede en konto med den e-mail.");
            return Page();
        }

        return RedirectToPage();
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public sealed class NewStaffInputModel
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = "";

        [Required, StringLength(200)]
        public string DisplayName { get; set; } = "";

        [Required, StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        public UserRole Role { get; set; } = UserRole.Employee;
    }
}
