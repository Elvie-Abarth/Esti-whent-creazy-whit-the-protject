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

    /// <summary>The brand being filtered by - always one that actually exists in the catalog, or null.</summary>
    public string? ActiveBrand { get; private set; }

    public IReadOnlyList<string> Brands { get; private set; } = [];

    [TempData]
    public string? ErrorMessage { get; set; }

    public static readonly (AccessoryCategory Value, string Label, string LabelEn)[] Categories =
    [
        (AccessoryCategory.Hay,     "Hø",       "Hay"),
        (AccessoryCategory.Food,    "Foder",    "Food"),
        (AccessoryCategory.Cage,    "Bure",     "Cages"),
        (AccessoryCategory.House,   "Huse",     "Houses"),
        (AccessoryCategory.Toy,     "Legetøj",  "Toys"),
        (AccessoryCategory.Bedding, "Strøelse", "Bedding"),
        (AccessoryCategory.Care,    "Pleje",    "Care")
    ];

    public void OnGet(string? kategori = null, string? q = null, string? maerke = null)
    {
        if (Enum.TryParse<AccessoryCategory>(kategori, ignoreCase: true, out var parsed))
            Active = parsed;
        Query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        Items = catalog.Accessories;
        Brands = Items.Select(a => a.Brand).OfType<string>().Distinct().Order().ToList();
        // Matched against the catalog's own list rather than echoed back: the
        // page only ever shows a brand name that came from the database.
        ActiveBrand = Brands.FirstOrDefault(b => string.Equals(b, maerke?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (ActiveBrand is not null)
            Items = Items.Where(a => a.Brand == ActiveBrand).ToList();
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
                (a.Brand?.Contains(Query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                a.Description.Contains(Query, StringComparison.OrdinalIgnoreCase) ||
                (a.DescriptionEn?.Contains(Query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }
    }
}
