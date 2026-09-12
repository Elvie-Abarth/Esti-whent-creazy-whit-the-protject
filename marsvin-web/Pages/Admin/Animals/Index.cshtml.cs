using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Animals;

[Authorize(Roles = "Admin")]
public class IndexModel(ICatalog catalog, ICatalogAdmin catalogAdmin) : PageModel
{
    public IReadOnlyList<Animal> Items { get; private set; } = [];

    public void OnGet() => Items = catalog.Animals;

    public IActionResult OnPostDelete(int productId)
    {
        catalogAdmin.DeleteAnimal(productId);
        return RedirectToPage();
    }
}
