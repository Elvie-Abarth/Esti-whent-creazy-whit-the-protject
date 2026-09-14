using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Tilbehor;

public class IndexModel(ICatalog catalog) : PageModel
{
    public IReadOnlyList<StockProduct> Items { get; private set; } = [];
    public AccessoryCategory? Active { get; private set; }
    public string? Query { get; private set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public static readonly (AccessoryCategory Value, string Label, string LabelEn)[] Categories =
    [
        (AccessoryCategory.Hay,     "Hø",       "Hay"),
        (AccessoryCategory.Food,    "Foder",    "Food"),
        (AccessoryCategory.Cage,    "Bure",     "Cages"),
        (AccessoryCategory.House,   "Huse",     "Houses"),
        (AccessoryCategory.Toy,     "Legetøj",  "Toys"),
        (AccessoryCategory.Bedding, "Strøelse", "Bedding")
    ];

    public void OnGet(string? kategori = null, string? q = null)
    {
        if (Enum.TryParse<AccessoryCategory>(kategori, ignoreCase: true, out var parsed))
            Active = parsed;
        Query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        Items = catalog.Accessories;
        if (Active is not null)
            Items = Items.Where(a => a.Category == Active).ToList();
        if (Query is not null)
        {
            // Matches Name/NameEn/Description/DescriptionEn - whichever
            // language a search term happens to be typed in, it's checked
            // against both, not just whichever one the page is currently
            // showing (the Danish/English toggle is client-side only, so the
            // server never knows which the visitor is looking at).
            Items = Items.Where(a =>
                a.Name.Contains(Query, StringComparison.OrdinalIgnoreCase) ||
                (a.NameEn?.Contains(Query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                a.Description.Contains(Query, StringComparison.OrdinalIgnoreCase) ||
                (a.DescriptionEn?.Contains(Query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }
    }
}
