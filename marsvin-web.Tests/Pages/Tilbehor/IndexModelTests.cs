using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Tilbehor;

namespace MarsvinWebExample.Tests.Pages.Tilbehor;

public class IndexModelTests
{
    [Fact]
    public void OnGet_NoCategory_ReturnsAllAccessories()
    {
        var catalog = new DemoCatalog();
        var model = new IndexModel(catalog);

        model.OnGet(null);

        Assert.Null(model.Active);
        Assert.Equal(catalog.Accessories.Count, model.Items.Count);
    }

    [Fact]
    public void OnGet_ValidCategory_FiltersItems()
    {
        var catalog = new DemoCatalog();
        var model = new IndexModel(catalog);

        model.OnGet("hay");

        Assert.Equal(AccessoryCategory.Hay, model.Active);
        Assert.NotEmpty(model.Items);
        Assert.All(model.Items, item => Assert.Equal(AccessoryCategory.Hay, item.Category));
    }

    [Fact]
    public void OnGet_InvalidCategory_FallsBackToAllItems()
    {
        var catalog = new DemoCatalog();
        var model = new IndexModel(catalog);

        model.OnGet("not-a-real-category");

        Assert.Null(model.Active);
        Assert.Equal(catalog.Accessories.Count, model.Items.Count);
    }

    [Fact]
    public void OnGet_SplitsTheItemsIntoPages_WithoutLosingOrRepeatingAny()
    {
        var catalog = new DemoCatalog();
        var seen = new List<int>();

        var model = new IndexModel(catalog);
        model.OnGet();
        for (var page = 1; page <= model.PageCount; page++)
        {
            model = new IndexModel(catalog);
            model.OnGet(side: page);
            Assert.InRange(model.PageItems.Count, 1, IndexModel.PageSize);
            seen.AddRange(model.PageItems.Select(a => a.ProductId));
        }

        Assert.Equal(catalog.Accessories.Select(a => a.ProductId).Order(), seen.Order());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(9999)]
    public void OnGet_APageThatDoesNotExist_IsClampedToOneThatDoes(int side)
    {
        var model = new IndexModel(new DemoCatalog());

        model.OnGet(side: side);

        Assert.InRange(model.CurrentPage, 1, model.PageCount);
        Assert.NotEmpty(model.PageItems);
    }

    [Fact]
    public void OnGet_PriceRange_KeepsOnlyItemsInsideIt()
    {
        var model = new IndexModel(new DemoCatalog());

        model.OnGet(pris: "50-100");

        Assert.NotEmpty(model.Items);
        Assert.All(model.Items, item => Assert.InRange(item.Price, 50, 99.99m));
        Assert.Equal(model.Items.Count, model.PriceCounts["50-100"]);
    }

    [Fact]
    public void OnGet_SortByPrice_OrdersCheapestFirst()
    {
        var model = new IndexModel(new DemoCatalog());

        model.OnGet(sort: "pris-lav");

        var prices = model.Items.Select(a => a.Price).ToList();
        Assert.Equal(prices.Order(), prices);
    }

    [Fact]
    public void OnGet_UnknownSortAndPrice_AreIgnored_AndNeverEndUpInALink()
    {
        var model = new IndexModel(new DemoCatalog());

        model.OnGet(pris: "\"><script>", sort: "\"><script>");

        Assert.Null(model.ActivePrice);
        Assert.Equal(IndexModel.Sorts[0], model.Sort);
        Assert.Equal("/Tilbehor?side=2", model.PageUrl(2));
    }

    [Fact]
    public void CategoryCounts_SayWhatAClickWouldShow()
    {
        var catalog = new DemoCatalog();
        var model = new IndexModel(catalog);

        model.OnGet(maerke: "Trixie");

        var trixieToys = catalog.Accessories.Count(a => a.Brand == "Trixie" && a.Category == AccessoryCategory.Toy);
        Assert.Equal(trixieToys, model.CategoryCounts[AccessoryCategory.Toy]);
        Assert.Equal("/Tilbehor?kategori=Toy&maerke=Trixie", model.CategoryUrl(AccessoryCategory.Toy));
    }

    [Fact]
    public void Categories_CoversEveryAccessoryCategoryInUse()
    {
        var catalog = new DemoCatalog();
        var categoriesInData = catalog.Accessories.Select(a => a.Category).Distinct();

        foreach (var category in categoriesInData)
        {
            Assert.Contains(IndexModel.Categories, c => c.Value == category);
        }
    }
}
