using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin;

// The staff area's front page: a set of links, nothing to load. Open to both
// staff roles - the page itself hides the Admin-only links from an Employee,
// and each page behind them enforces its own role, so hiding a link is only
// tidiness, never the access control.
[Authorize(Roles = "Admin,Employee")]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
