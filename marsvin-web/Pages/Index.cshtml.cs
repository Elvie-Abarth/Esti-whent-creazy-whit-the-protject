using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages;

public class IndexModel(ICatalog catalog) : PageModel
{
    public IReadOnlyList<IReadOnlyList<Animal>> Groups { get; private set; } = [];
    public IReadOnlyList<StockProduct> Essentials { get; private set; } = [];
    public int AvailableCount { get; private set; }

    public void OnGet()
    {
        Groups = catalog.AnimalGroups().Take(2).ToList();
        AvailableCount = catalog.AvailableAnimals().Count();
        Essentials = catalog.Accessories
            .Where(a => a.Category is AccessoryCategory.Hay
                                   or AccessoryCategory.Food
                                   or AccessoryCategory.Cage)
            .Take(3).ToList();
    }
}
