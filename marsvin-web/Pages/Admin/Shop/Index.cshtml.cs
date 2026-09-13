using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Shop;

// Running the shop's catalog, stock and promotions - distinct from
// /Admin/Index, which is about the staff themselves (accounts, schedule).
[Authorize(Roles = "Admin,Employee")]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
