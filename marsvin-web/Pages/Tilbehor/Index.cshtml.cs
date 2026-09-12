using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Tilbehor;

public class IndexModel(ICatalog catalog) : PageModel
{
    public IReadOnlyList<StockProduct> Items { get; private set; } = [];
    public AccessoryCategory? Active { get; private set; }

    public static readonly (AccessoryCategory Value, string Label, string LabelEn)[] Categories =
    [
        (AccessoryCategory.Hay,     "Hø",       "Hay"),
        (AccessoryCategory.Food,    "Foder",    "Food"),
        (AccessoryCategory.Cage,    "Bure",     "Cages"),
        (AccessoryCategory.House,   "Huse",     "Houses"),
        (AccessoryCategory.Toy,     "Legetøj",  "Toys"),
        (AccessoryCategory.Bedding, "Strøelse", "Bedding")
    ];

    public void OnGet(string? kategori)
    {
        if (Enum.TryParse<AccessoryCategory>(kategori, ignoreCase: true, out var parsed))
            Active = parsed;

        Items = Active is null
            ? catalog.Accessories
            : catalog.Accessories.Where(a => a.Category == Active).ToList();
    }
}
