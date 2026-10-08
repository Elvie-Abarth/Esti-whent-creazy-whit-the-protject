using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Tilbehor;

public class IndexModel(ICatalog catalog) : PageModel
{
    public const int PageSize = 24;

    /// <summary>Everything that matches the search and filters, sorted - across all pages.</summary>
    public IReadOnlyList<StockProduct> Items { get; private set; } = [];

    /// <summary>The slice of <see cref="Items"/> shown on the current page.</summary>
    public IReadOnlyList<StockProduct> PageItems { get; private set; } = [];

    public AccessoryCategory? Active { get; private set; }
    public string? Query { get; private set; }

    /// <summary>The brand being filtered by - always one that actually exists in the catalog, or null.</summary>
    public string? ActiveBrand { get; private set; }

    /// <summary>The price range being filtered by - always one of <see cref="PriceRanges"/>, or null.</summary>
    public PriceRange? ActivePrice { get; private set; }

    /// <summary>Always one of <see cref="Sorts"/>; the first one when nothing (or nonsense) was asked for.</summary>
    public SortOption Sort { get; private set; } = Sorts[0];

    public int CurrentPage { get; private set; } = 1;
    public int PageCount { get; private set; } = 1;

    public IReadOnlyList<string> Brands { get; private set; } = [];

    // How many products each choice in the sidebar would give, with the
    // *other* filters kept as they are - so a number next to "Hay" is what
    // you'd actually see after clicking it, not the size of the whole shelf.
    public IReadOnlyDictionary<AccessoryCategory, int> CategoryCounts { get; private set; } = new Dictionary<AccessoryCategory, int>();
    public IReadOnlyDictionary<string, int> BrandCounts { get; private set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> PriceCounts { get; private set; } = new Dictionary<string, int>();

    /// <summary>One photo per category for the tiles at the top - the first product in it that has one.</summary>
    public IReadOnlyDictionary<AccessoryCategory, string> CategoryPhotos { get; private set; } = new Dictionary<AccessoryCategory, string>();

    public int TotalInShop { get; private set; }

    public bool HasFilters => Active is not null || ActiveBrand is not null || ActivePrice is not null || Query is not null;

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

    public sealed record PriceRange(string Key, decimal Min, decimal? Max, string Label, string LabelEn)
    {
        public bool Contains(decimal price) => price >= Min && (Max is null || price < Max);
    }

    public static readonly PriceRange[] PriceRanges =
    [
        new("0-50",    0,   50,   "Under 50 kr.",  "Under 50 kr."),
        new("50-100",  50,  100,  "50-100 kr.",    "50-100 kr."),
        new("100-250", 100, 250,  "100-250 kr.",   "100-250 kr."),
        new("250-",    250, null, "Over 250 kr.",  "Over 250 kr.")
    ];

    public sealed record SortOption(string Key, string Label, string LabelEn);

    public static readonly SortOption[] Sorts =
    [
        new("anbefalet", "Anbefalet",         "Featured"),
        new("pris-lav",  "Pris: lav til høj", "Price: low to high"),
        new("pris-hoej", "Pris: høj til lav", "Price: high to low"),
        new("navn",      "Navn A-Å",          "Name A-Z"),
        new("nyeste",    "Nyeste først",      "Newest first")
    ];

    public void OnGet(string? kategori = null, string? q = null, string? maerke = null,
        string? pris = null, string? sort = null, int side = 1)
    {
        if (Enum.TryParse<AccessoryCategory>(kategori, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            Active = parsed;
        Query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var all = catalog.Accessories;
        TotalInShop = all.Count;
        Brands = all.Select(a => a.Brand).OfType<string>().Distinct().Order().ToList();
        // Brand, price range and sort are all matched against the page's own
        // lists rather than echoed back: nothing typed into the address bar
        // ever ends up in the HTML unless it's one of the known values.
        ActiveBrand = Brands.FirstOrDefault(b => string.Equals(b, maerke?.Trim(), StringComparison.OrdinalIgnoreCase));
        ActivePrice = PriceRanges.FirstOrDefault(r => r.Key == pris);
        Sort = Sorts.FirstOrDefault(s => s.Key == sort) ?? Sorts[0];

        CategoryPhotos = all
            .Where(a => !string.IsNullOrWhiteSpace(a.PhotoUrl))
            .GroupBy(a => a.Category)
            .ToDictionary(g => g.Key, g => g.First().PhotoUrl!);

        var searched = Query is null ? all : all.Where(MatchesQuery).ToList();

        bool InCategory(StockProduct a) => Active is null || a.Category == Active;
        bool InBrand(StockProduct a) => ActiveBrand is null || a.Brand == ActiveBrand;
        bool InPrice(StockProduct a) => ActivePrice is null || ActivePrice.Contains(a.Price);

        CategoryCounts = searched.Where(a => InBrand(a) && InPrice(a))
            .GroupBy(a => a.Category).ToDictionary(g => g.Key, g => g.Count());
        BrandCounts = searched.Where(a => InCategory(a) && InPrice(a) && a.Brand is not null)
            .GroupBy(a => a.Brand!).ToDictionary(g => g.Key, g => g.Count());
        PriceCounts = PriceRanges.ToDictionary(r => r.Key,
            r => searched.Count(a => InCategory(a) && InBrand(a) && r.Contains(a.Price)));

        Items = Sorted(searched.Where(a => InCategory(a) && InBrand(a) && InPrice(a))).ToList();

        PageCount = Math.Max(1, (int)Math.Ceiling(Items.Count / (double)PageSize));
        CurrentPage = Math.Clamp(side, 1, PageCount);
        PageItems = Items.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
    }

    // Matches Name/NameEn/Description/DescriptionEn - whichever language a
    // search term happens to be typed in, it's checked against both, not
    // just whichever one the page is currently showing (the Danish/English
    // toggle is client-side only, so the server never knows which the
    // visitor is looking at).
    private bool MatchesQuery(StockProduct a) =>
        a.Name.Contains(Query!, StringComparison.OrdinalIgnoreCase) ||
        (a.NameEn?.Contains(Query!, StringComparison.OrdinalIgnoreCase) ?? false) ||
        (a.Brand?.Contains(Query!, StringComparison.OrdinalIgnoreCase) ?? false) ||
        a.Description.Contains(Query!, StringComparison.OrdinalIgnoreCase) ||
        (a.DescriptionEn?.Contains(Query!, StringComparison.OrdinalIgnoreCase) ?? false);

    private IEnumerable<StockProduct> Sorted(IEnumerable<StockProduct> items) => Sort.Key switch
    {
        "pris-lav"  => items.OrderBy(a => a.Price).ThenBy(a => a.Name),
        "pris-hoej" => items.OrderByDescending(a => a.Price).ThenBy(a => a.Name),
        "navn"      => items.OrderBy(a => a.Name, StringComparer.Create(new System.Globalization.CultureInfo("da-DK"), ignoreCase: true)),
        "nyeste"    => items.OrderByDescending(a => a.ProductId),
        // "Featured": a bit of everything - the first product of each
        // category, then the second of each, and so on - rather than a
        // front page that is hay, hay and more hay. Sold-out items go last.
        _ => items
            .GroupBy(a => a.Category)
            .SelectMany(g => g.Select((item, index) => (item, index)))
            .OrderBy(x => x.item.StockLevel == StockLevel.OutOfStock)
            .ThenBy(x => x.index)
            .ThenBy(x => x.item.Category)
            .Select(x => x.item)
    };

    // ---- Links that change one thing and keep the rest ----
    // Every filter link goes back to page 1; only the pager keeps the page.

    public string CategoryUrl(AccessoryCategory? category) => Link(category, ActiveBrand, ActivePrice, Query, Sort, 1);
    public string BrandUrl(string? brand) => Link(Active, brand, ActivePrice, Query, Sort, 1);
    public string PriceUrl(PriceRange? range) => Link(Active, ActiveBrand, range, Query, Sort, 1);
    public string WithoutQueryUrl() => Link(Active, ActiveBrand, ActivePrice, null, Sort, 1);
    public string PageUrl(int page) => Link(Active, ActiveBrand, ActivePrice, Query, Sort, page);
    public string ClearUrl() => Link(null, null, null, null, Sort, 1);

    private static string Link(AccessoryCategory? category, string? brand, PriceRange? price,
        string? query, SortOption sort, int page)
    {
        var parts = new List<string>();
        void Add(string name, string value) => parts.Add($"{name}={Uri.EscapeDataString(value)}");

        if (category is not null) Add("kategori", category.Value.ToString());
        if (brand is not null) Add("maerke", brand);
        if (price is not null) Add("pris", price.Key);
        if (query is not null) Add("q", query);
        if (sort != Sorts[0]) Add("sort", sort.Key);
        if (page > 1) Add("side", page.ToString());

        return parts.Count == 0 ? "/Tilbehor" : "/Tilbehor?" + string.Join("&", parts);
    }
}
