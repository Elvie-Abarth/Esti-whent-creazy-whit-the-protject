using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Products;

[Authorize(Roles = "Admin")]
public class IndexModel(ICatalog catalog, ICatalogAdmin catalogAdmin) : PageModel
{
    public IReadOnlyList<StockProduct> Items { get; private set; } = [];

    public void OnGet() => Items = catalog.Accessories;

    public IActionResult OnPostDelete(int productId)
    {
        catalogAdmin.DeleteStockProduct(productId);
        return RedirectToPage();
    }
}
