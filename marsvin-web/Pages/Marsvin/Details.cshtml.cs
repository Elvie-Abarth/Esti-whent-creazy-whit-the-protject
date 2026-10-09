using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Marsvin;

// One guinea pig's own page. Public. An id that matches no animal gives a
// 404 - and because the id is bound as an int, anything that is not a number
// never reaches the database at all. Partner is the animal it is bonded
// with, if any: the two are shown, and sold, together.
public class DetailsModel(ICatalog catalog) : PageModel
{
    public Animal Animal { get; private set; } = null!;
    public Animal? Partner { get; private set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(int id)
    {
        var animal = catalog.FindAnimal(id);
        if (animal is null) return NotFound();

        Animal = animal;
        Partner = animal.BondedWithId is int partnerId ? catalog.FindAnimal(partnerId) : null;
        return Page();
    }
}
