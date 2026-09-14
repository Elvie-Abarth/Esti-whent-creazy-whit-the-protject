using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Account;

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
