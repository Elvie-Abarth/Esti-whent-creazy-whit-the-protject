using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Promotions;

[Authorize(Roles = "Admin")]
public class IndexModel(IPromotionStore promotions, IAuditLogStore audit) : PageModel
{
    public IReadOnlyList<Promotion> Items { get; private set; } = [];

    public void OnGet() => Items = promotions.GetAll();

    public IActionResult OnPostDelete(int promotionId)
    {
        var promotion = promotions.GetAll().FirstOrDefault(p => p.PromotionId == promotionId);
        promotions.Delete(promotionId);
        audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Promotion.Deleted", promotion?.Title ?? $"#{promotionId}");
        return RedirectToPage();
    }
}
