using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Marsvin;

public class DetailsModel(ICatalog catalog) : PageModel
{
    public Animal Animal { get; private set; } = null!;
    public Animal? Partner { get; private set; }

    public IActionResult OnGet(int id)
    {
        var animal = catalog.FindAnimal(id);
        if (animal is null) return NotFound();

        Animal = animal;
        Partner = animal.BondedWithId is int partnerId ? catalog.FindAnimal(partnerId) : null;
        return Page();
    }
}
