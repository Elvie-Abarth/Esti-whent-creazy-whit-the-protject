using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Stock;

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
