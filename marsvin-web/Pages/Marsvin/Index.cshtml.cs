using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Marsvin;

public class IndexModel(ICatalog catalog) : PageModel
{
    public IReadOnlyList<IReadOnlyList<Animal>> Groups { get; private set; } = [];

    public void OnGet() => Groups = catalog.AnimalGroups().ToList();
}
