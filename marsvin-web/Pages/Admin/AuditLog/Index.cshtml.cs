using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.AuditLog;

// The audit log: who changed what, and when - the 200 most recent entries.
// Admin only, and read-only: there is no handler here that edits or deletes
// an entry, and nothing else in the app does either.
[Authorize(Roles = "Admin")]
public class IndexModel(IAuditLogStore audit) : PageModel
{
    public IReadOnlyList<AuditLogEntry> Entries { get; private set; } = [];

    public void OnGet() => Entries = audit.GetRecent(200);
}
