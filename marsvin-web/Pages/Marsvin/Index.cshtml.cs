using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Marsvin;

public class IndexModel(ICatalog catalog) : PageModel
{
    public IReadOnlyList<IReadOnlyList<Animal>> Groups { get; private set; } = [];

    // Guinea pigs with a real photo come first, the ones that only have a
    // drawing after them. A bonded pair stays together and counts as "with
    // photo" as soon as one of the two has one. OrderBy is stable, so within
    // each half the catalog's own order is kept.
    public void OnGet() => Groups = catalog.AnimalGroups()
        .OrderByDescending(group => group.Any(animal => !string.IsNullOrWhiteSpace(animal.PhotoUrl)))
        .ToList();
}
