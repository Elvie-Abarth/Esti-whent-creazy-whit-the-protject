using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Promotions;

[Authorize(Roles = "Admin")]
public class IndexModel(IPromotionStore promotions) : PageModel
{
    public IReadOnlyList<Promotion> Items { get; private set; } = [];

    public void OnGet() => Items = promotions.GetAll();

    public IActionResult OnPostDelete(int promotionId)
    {
        promotions.Delete(promotionId);
        return RedirectToPage();
    }
}
