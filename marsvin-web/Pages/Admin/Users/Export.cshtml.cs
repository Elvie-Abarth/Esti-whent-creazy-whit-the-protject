using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class ExportModel(IUserAccountStore users) : PageModel
{
    public ApplicationUser Account { get; private set; } = null!;

    public IActionResult OnGet(int id)
    {
        var account = users.FindById(id);
        if (account is null) return NotFound();

        Account = account;
        return Page();
    }
}
