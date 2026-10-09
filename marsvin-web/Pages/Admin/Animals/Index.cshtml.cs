using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Animals;

// Admin list of every guinea pig, with delete. Admin only - an Employee can
// change an animal's status on /Admin/Stock, but not add or remove one.
// Creating and editing happen on the Edit page.
[Authorize(Roles = "Admin")]
public class IndexModel(ICatalog catalog, ICatalogAdmin catalogAdmin, IAuditLogStore audit) : PageModel
{
    public IReadOnlyList<Animal> Items { get; private set; } = [];

    public void OnGet() => Items = catalog.Animals;

    // Looked up first only to get the name for the audit log - once it is deleted there is nothing left to name.
    public IActionResult OnPostDelete(int productId)
    {
        var animal = catalog.FindAnimal(productId);
        catalogAdmin.DeleteAnimal(productId);
        audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Animal.Deleted", animal?.Name ?? $"#{productId}");
        return RedirectToPage();
    }
}
