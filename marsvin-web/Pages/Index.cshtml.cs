using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages;

// The front page: the first two guinea pig groups (a bonded pair counts as
// one group), how many animals are for sale right now, and three accessories
// from the categories a new owner needs on day one - hay, food and a cage.
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
