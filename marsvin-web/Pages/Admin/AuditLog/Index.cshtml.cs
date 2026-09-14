using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.AuditLog;

[Authorize(Roles = "Admin")]
public class IndexModel(IAuditLogStore audit) : PageModel
{
    public IReadOnlyList<AuditLogEntry> Entries { get; private set; } = [];

    public void OnGet() => Entries = audit.GetRecent(200);
}
