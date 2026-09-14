using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Schedule;

// An Admin assigns shifts to any staff member; an Employee only ever sees
// their own - never anyone else's, and never with the controls to add or
// remove a shift, staff or otherwise (that stays an Admin-only action, even
// for an Employee looking at their own row).
[Authorize(Roles = "Admin,Employee")]
public class IndexModel(
    IShiftStore shifts, IUserAccountStore users, ITimeOffRequestStore timeOffRequests, IEmailSender emailSender)
    : PageModel
{
    public IReadOnlyList<Shift> Shifts { get; private set; } = [];

    public IReadOnlyList<ApplicationUser> StaffMembers { get; private set; } = [];

    // Only the ones still awaiting a decision - Admin-only, this is the
    // "needs your attention" list the day-off-request email points back to.
    public IReadOnlyList<TimeOffRequest> PendingRequests { get; private set; } = [];

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? ToastMessage { get; set; }

    public void OnGet()
    {
        Shifts = User.IsInRole("Admin") ? shifts.GetAll() : shifts.GetForUser(this.CurrentUserId());

        if (User.IsInRole("Admin"))
        {
            StaffMembers = users.GetAll().Where(u => u.Role != UserRole.Customer && u.IsActive).ToList();
            PendingRequests = timeOffRequests.GetAll().Where(r => r.Status == TimeOffStatus.Pending).ToList();
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(int userId, DateOnly date, TimeOnly startTime, TimeOnly endTime, string? note)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (endTime <= startTime)
        {
            ErrorMessage = new Bilingual(
                "Sluttidspunktet skal være efter starttidspunktet.",
                "The end time must be after the start time.");
            return RedirectToPage();
        }
        // Matches the maxlength on the form field and dbo.Shifts.Note
        // (NVARCHAR(200)) - the HTML attribute is only a hint, not
        // enforcement, so an over-length POST needs the same limit checked
        // here too, rather than reaching the database and failing there.
        if (note is { Length: > 200 })
        {
            ErrorMessage = new Bilingual(
                "Noten må højst fylde 200 tegn.",
                "The note can be at most 200 characters.");
            return RedirectToPage();
        }

        var staff = users.FindById(userId);
        if (staff is null || staff.Role == UserRole.Customer)
        {
            ErrorMessage = new Bilingual("Vælg en gyldig medarbejder.", "Choose a valid staff member.");
            return RedirectToPage();
        }

        var newStart = date.ToDateTime(startTime);
        var newEnd = date.ToDateTime(endTime);

        // Two shifts "overlap" when one starts before the other ends, both
        // ways - the standard interval-overlap check. Caught here rather
        // than left for whoever notices the schedule looks wrong later.
        var hasOverlappingShift = shifts.GetForUser(userId).Any(s => s.StartAt < newEnd && newStart < s.EndAt);
        if (hasOverlappingShift)
        {
            ErrorMessage = new Bilingual(
                $"{staff.DisplayName} har allerede en vagt, der overlapper med det tidsrum.",
                $"{staff.DisplayName} already has a shift that overlaps with that time.");
            return RedirectToPage();
        }

        var newDateRange = DateOnly.FromDateTime(newStart);
        var hasApprovedDayOff = timeOffRequests.GetForUser(userId)
            .Any(r => r.Status == TimeOffStatus.Approved && newDateRange >= r.StartDate && newDateRange <= r.EndDate);
        if (hasApprovedDayOff)
        {
            ErrorMessage = new Bilingual(
                $"{staff.DisplayName} har godkendt fri den dag.",
                $"{staff.DisplayName} has approved time off that day.");
            return RedirectToPage();
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        shifts.Create(userId, newStart, newEnd, trimmedNote);

        await emailSender.SendAsync(staff.Email, "Din vagtplan er blevet opdateret",
            $"""
            Hej {staff.DisplayName},

            Der er tilføjet en ny vagt til din vagtplan:

            {date:d MMM yyyy}, {startTime:HH\:mm}–{endTime:HH\:mm}{(trimmedNote is null ? "" : $" ({trimmedNote})")}

            Se hele din vagtplan under Personale.

            Venlig hilsen
            Marsvin
            """);

        ToastMessage = new Bilingual($"Vagt tilføjet for {staff.DisplayName}.", $"Shift added for {staff.DisplayName}.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int shiftId)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        // Read the shift before it's gone - that's the only chance to know who
        // to notify and what the removed shift actually was.
        var shift = shifts.GetById(shiftId);
        shifts.Delete(shiftId);

        if (shift is not null)
        {
            var staff = users.FindById(shift.UserId);
            if (staff is not null)
            {
                await emailSender.SendAsync(staff.Email, "Din vagtplan er blevet opdateret",
                    $"""
                    Hej {staff.DisplayName},

                    En vagt er blevet fjernet fra din vagtplan:

                    {shift.StartAt:d MMM yyyy}, {shift.StartAt:HH\:mm}–{shift.EndAt:HH\:mm}

                    Se hele din vagtplan under Personale.

                    Venlig hilsen
                    Marsvin
                    """);
            }
        }

        ToastMessage = new Bilingual("Vagten er slettet.", "The shift has been deleted.");
        return RedirectToPage();
    }

    public IActionResult OnPostDecideRequest(int requestId, bool approve)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        var decidingAdmin = users.FindById(this.CurrentUserId())!;
        timeOffRequests.Decide(requestId, approve ? TimeOffStatus.Approved : TimeOffStatus.Denied, decidingAdmin.DisplayName);
        ToastMessage = approve
            ? new Bilingual("Anmodningen er godkendt.", "The request has been approved.")
            : new Bilingual("Anmodningen er afvist.", "The request has been denied.");
        return RedirectToPage();
    }
}
