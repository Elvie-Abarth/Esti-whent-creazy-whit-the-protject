using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin;

[Authorize(Roles = "Admin,Employee")]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
