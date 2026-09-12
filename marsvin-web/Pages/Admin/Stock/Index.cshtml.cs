using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Stock;

[Authorize(Roles = "Admin,Employee")]
public class IndexModel(ICatalog catalog, ICatalogAdmin catalogAdmin) : PageModel
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
            catalogAdmin.UpdateStockQuantity(productId, stockQuantity);
        return RedirectToPage();
    }

    public IActionResult OnPostUpdateStatus(int productId, AnimalStatus status)
    {
        catalogAdmin.UpdateAnimalStatus(productId, status);
        return RedirectToPage();
    }
}
