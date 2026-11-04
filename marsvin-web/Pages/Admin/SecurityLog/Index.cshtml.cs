using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.SecurityLog;

// The security log: recorded attack *attempts* - failed logins, locked
// accounts, wrong-role requests, rate-limit hits, rejected forms. Admin only,
// and read-only (no handler changes anything). Deliberately NOT a "breach
// detector": it shows what was attempted and the suspicious patterns, which
// is all an app can honestly see of its own logs.
[Authorize(Roles = "Admin")]
public class IndexModel(ISecurityEventStore events) : PageModel
{
    public IReadOnlyList<SecurityEvent> Events { get; private set; } = [];
    public int Last24Hours { get; private set; }

    public void OnGet()
    {
        Events = events.GetRecent(200);
        Last24Hours = events.CountSince(DateTime.UtcNow.AddDays(-1));
    }
}
