using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Administration;

// Admin-only oversight: accounts, the audit log and the security log, split
// out from the Staff area so day-to-day staff tasks (schedule, messages,
// donations) aren't mixed in with account and security administration. A set
// of links, like the other two hubs; each page behind them enforces its own
// Admin role, so this page is only tidiness.
[Authorize(Roles = "Admin")]
public class IndexModel(ISecurityEventStore securityEvents) : PageModel
{
    /// <summary>A small badge on the security-log link - events in the last 24 hours.</summary>
    public int SecurityEventsLast24Hours { get; private set; }

    public void OnGet() =>
        SecurityEventsLast24Hours = securityEvents.CountSince(DateTime.UtcNow.AddDays(-1));
}
