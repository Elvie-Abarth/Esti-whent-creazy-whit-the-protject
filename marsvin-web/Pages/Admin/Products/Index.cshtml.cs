using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Products;

[Authorize(Roles = "Admin")]
public class IndexModel(ICatalog catalog, ICatalogAdmin catalogAdmin, IAuditLogStore audit) : PageModel
{
    public IReadOnlyList<StockProduct> Items { get; private set; } = [];

    public void OnGet() => Items = catalog.Accessories;

    public IActionResult OnPostDelete(int productId)
    {
        var product = catalog.Accessories.FirstOrDefault(p => p.ProductId == productId);
        catalogAdmin.DeleteStockProduct(productId);
        audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Product.Deleted", product?.Name ?? $"#{productId}");
        return RedirectToPage();
    }
}
