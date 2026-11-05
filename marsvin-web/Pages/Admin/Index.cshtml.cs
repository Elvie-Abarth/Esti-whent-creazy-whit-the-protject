using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin;

// The admin dashboard: the landing page of the admin shell (the sidebar in
// Shared/_AdminLayout.cshtml does the navigating). Open to both staff roles;
// the security figure below is only read and shown for an Admin.
[Authorize(Roles = "Admin,Employee")]
public class IndexModel(ISecurityEventStore securityEvents) : PageModel
{
    /// <summary>Security events in the last 24 hours - a dashboard highlight for admins.</summary>
    public int SecurityEventsLast24Hours { get; private set; }

    public void OnGet()
    {
        if (User.IsInRole("Admin"))
        {
            SecurityEventsLast24Hours = securityEvents.CountSince(DateTime.UtcNow.AddDays(-1));
        }
    }
}
