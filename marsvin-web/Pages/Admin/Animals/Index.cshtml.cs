using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Animals;

[Authorize(Roles = "Admin")]
public class IndexModel(ICatalog catalog, ICatalogAdmin catalogAdmin, IAuditLogStore audit) : PageModel
{
    public IReadOnlyList<Animal> Items { get; private set; } = [];

    public void OnGet() => Items = catalog.Animals;

    public IActionResult OnPostDelete(int productId)
    {
        var animal = catalog.FindAnimal(productId);
        catalogAdmin.DeleteAnimal(productId);
        audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Animal.Deleted", animal?.Name ?? $"#{productId}");
        return RedirectToPage();
    }
}
