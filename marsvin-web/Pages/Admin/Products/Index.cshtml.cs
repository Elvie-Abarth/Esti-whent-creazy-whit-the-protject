using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Products;

// Admin list of every accessory, with delete. Admin only - an Employee can
// adjust stock on /Admin/Stock, but not add, edit or remove a product.
// Creating and editing happen on the Edit page.
[Authorize(Roles = "Admin")]
public class IndexModel(ICatalog catalog, ICatalogAdmin catalogAdmin, IAuditLogStore audit) : PageModel
{
    public IReadOnlyList<StockProduct> Items { get; private set; } = [];

    public void OnGet() => Items = catalog.Accessories;

    // Looked up first only to get the name for the audit log - once it is deleted there is nothing left to name.
    public IActionResult OnPostDelete(int productId)
    {
        var product = catalog.Accessories.FirstOrDefault(p => p.ProductId == productId);
        catalogAdmin.DeleteStockProduct(productId);
        audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Product.Deleted", product?.Name ?? $"#{productId}");
        return RedirectToPage();
    }
}
