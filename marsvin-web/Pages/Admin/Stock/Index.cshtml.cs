using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Stock;

// Day-to-day stock keeping, open to both staff roles: set how many of an
// accessory are in stock, and set a guinea pig's status (available, reserved,
// sold, not for sale). The one part of the catalog an Employee may change -
// prices, names and deleting stay Admin-only. Every change is written to the
// audit log with the old and the new value.
[Authorize(Roles = "Admin,Employee")]
public class IndexModel(ICatalog catalog, ICatalogAdmin catalogAdmin, IAuditLogStore audit) : PageModel
{
    public IReadOnlyList<StockProduct> Accessories { get; private set; } = [];
    public IReadOnlyList<Animal> Animals { get; private set; } = [];

    public void OnGet()
    {
        Accessories = catalog.Accessories;
        Animals = catalog.Animals;
    }

    // A negative number is ignored rather than saved; the database has a CHECK constraint for the same rule.
    public IActionResult OnPostUpdateStock(int productId, int stockQuantity)
    {
        if (stockQuantity >= 0)
        {
            var product = catalog.Accessories.FirstOrDefault(p => p.ProductId == productId);
            catalogAdmin.UpdateStockQuantity(productId, stockQuantity);
            audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Product.StockChanged",
                $"{product?.Name ?? $"#{productId}"}: {product?.StockQuantity} -> {stockQuantity}");
        }
        return RedirectToPage();
    }

    public IActionResult OnPostUpdateStatus(int productId, AnimalStatus status)
    {
        var animal = catalog.FindAnimal(productId);
        catalogAdmin.UpdateAnimalStatus(productId, status);
        audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Animal.StatusChanged",
            $"{animal?.Name ?? $"#{productId}"}: {animal?.Status} -> {status}");
        return RedirectToPage();
    }
}
