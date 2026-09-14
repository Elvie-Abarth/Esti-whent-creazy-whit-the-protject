using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class IndexModel(IUserAccountStore users, IAuditLogStore audit) : PageModel
{
    public IReadOnlyList<ApplicationUser> Items { get; private set; } = [];

    [BindProperty]
    public NewStaffInputModel NewStaff { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    public void OnGet() => Items = users.GetAll();

    public IActionResult OnPostUpdateRole(int userId, UserRole role)
    {
        if (userId == this.CurrentUserId())
        {
            ErrorMessage = new Bilingual("Du kan ikke ændre din egen rolle.", "You can't change your own role.");
            return RedirectToPage();
        }
        // Self-protection above stops an admin locking themselves out, but
        // says nothing about demoting a *different* admin down to the last
        // one standing - guard that here too, the same way as toggle-active
        // and delete below, so all three can never take the active-admin
        // count to zero.
        if (role != UserRole.Admin && IsLastActiveAdmin(userId))
        {
            ErrorMessage = new Bilingual(
                "Der skal være mindst én aktiv admin-konto - denne kan ikke ændres til en anden rolle.",
                "There must be at least one active admin account - this one can't be changed to another role.");
            return RedirectToPage();
        }
        var target = users.FindById(userId);
        users.UpdateRole(userId, role);
        RecordAudit("User.RoleChanged", $"{target?.DisplayName ?? $"#{userId}"} -> {role}");
        return RedirectToPage();
    }

    public IActionResult OnPostToggleActive(int userId, bool isActive)
    {
        if (userId == this.CurrentUserId())
        {
            ErrorMessage = new Bilingual("Du kan ikke deaktivere din egen konto.", "You can't deactivate your own account.");
            return RedirectToPage();
        }
        if (!isActive && IsLastActiveAdmin(userId))
        {
            ErrorMessage = new Bilingual(
                "Der skal være mindst én aktiv admin-konto - denne kan ikke deaktiveres.",
                "There must be at least one active admin account - this one can't be deactivated.");
            return RedirectToPage();
        }
        var target = users.FindById(userId);
        users.SetActive(userId, isActive);
        RecordAudit(isActive ? "User.Activated" : "User.Deactivated", target?.DisplayName ?? $"#{userId}");
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int userId)
    {
        if (userId == this.CurrentUserId())
        {
            ErrorMessage = new Bilingual("Du kan ikke slette din egen konto.", "You can't delete your own account.");
            return RedirectToPage();
        }
        if (IsLastActiveAdmin(userId))
        {
            ErrorMessage = new Bilingual(
                "Der skal være mindst én aktiv admin-konto - denne kan ikke slettes.",
                "There must be at least one active admin account - this one can't be deleted.");
            return RedirectToPage();
        }
        var target = users.FindById(userId);
        users.DeleteUser(userId);
        RecordAudit("User.Deleted", target?.DisplayName ?? $"#{userId}");
        return RedirectToPage();
    }

    // True only for a target who is themselves an active Admin *and* is the
    // only one left - never blocks touching an Employee/Customer, and never
    // blocks a second, third, etc. admin while at least one other remains.
    private bool IsLastActiveAdmin(int userId)
    {
        var target = users.FindById(userId);
        return target is { Role: UserRole.Admin, IsActive: true } && users.CountActiveAdmins() <= 1;
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

        RecordAudit("User.Created", $"{NewStaff.DisplayName} ({NewStaff.Email}), {NewStaff.Role}");
        return RedirectToPage();
    }

    private void RecordAudit(string action, string details) =>
        audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), action, details);

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
