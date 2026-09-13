using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Schedule;

// An Admin assigns shifts to any staff member; an Employee only ever sees
// their own - never anyone else's, and never with the controls to add or
// remove a shift, staff or otherwise (that stays an Admin-only action, even
// for an Employee looking at their own row).
[Authorize(Roles = "Admin,Employee")]
public class IndexModel(IShiftStore shifts, IUserAccountStore users) : PageModel
{
    public IReadOnlyList<Shift> Shifts { get; private set; } = [];

    public IReadOnlyList<ApplicationUser> StaffMembers { get; private set; } = [];

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? ToastMessage { get; set; }

    public void OnGet()
    {
        Shifts = User.IsInRole("Admin") ? shifts.GetAll() : shifts.GetForUser(CurrentUserId);

        if (User.IsInRole("Admin"))
        {
            StaffMembers = users.GetAll().Where(u => u.Role != UserRole.Customer && u.IsActive).ToList();
        }
    }

    public IActionResult OnPostCreate(int userId, DateOnly date, TimeOnly startTime, TimeOnly endTime, string? note)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (endTime <= startTime)
        {
            ErrorMessage = "Sluttidspunktet skal være efter starttidspunktet.";
            return RedirectToPage();
        }

        var staff = users.FindById(userId);
        if (staff is null || staff.Role == UserRole.Customer)
        {
            ErrorMessage = "Vælg en gyldig medarbejder.";
            return RedirectToPage();
        }

        shifts.Create(userId, date.ToDateTime(startTime), date.ToDateTime(endTime),
            string.IsNullOrWhiteSpace(note) ? null : note.Trim());
        ToastMessage = $"Vagt tilføjet for {staff.DisplayName}.";
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int shiftId)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        shifts.Delete(shiftId);
        ToastMessage = "Vagten er slettet.";
        return RedirectToPage();
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
