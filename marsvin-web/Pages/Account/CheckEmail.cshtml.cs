using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Account;

// The "we sent you a link" page shown after a login, a registration or a
// password-reset request. Purpose only picks which wording to show.
// ReturnUrl comes from the query string, so it is checked to be a local path
// before use - otherwise a crafted link could send someone to another site
// once they have logged in (an open redirect).
public class CheckEmailModel : PageModel
{
    public string Purpose { get; private set; } = "";
    public string ReturnUrl { get; private set; } = "/";

    public void OnGet(string? purpose, string? returnUrl)
    {
        Purpose = purpose ?? "";
        ReturnUrl = IsSafeLocalUrl(returnUrl) ? returnUrl! : "/";
    }
}
